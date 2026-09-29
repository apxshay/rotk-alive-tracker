using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace RotkAlive
{
    // One public leaderboard search. The season place and K/D are cached for the session.
    // A confirmed miss is cached too. A transport failure or HTTP 429 is not, so that name is tried again.
    public sealed class LadderStats
    {
        public bool Miss;
        public bool HasRank;
        public int Rank;
        public bool HasKd;
        public double Kd;
        public string KdText;

        public static LadderStats Missed()
        {
            LadderStats s = new LadderStats();
            s.Miss = true;
            return s;
        }

        public static LadderStats Hit(int rank, double kd)
        {
            LadderStats s = new LadderStats();
            s.HasRank = true;
            s.Rank = rank;
            s.HasKd = true;
            s.Kd = kd;
            s.KdText = FormatKd(kd);
            return s;
        }

        public static double KdRatio(int kills, int deaths)
        {
            if (deaths <= 0) return kills;
            return kills / (double)deaths;
        }

        public static string FormatKd(double kd)
        {
            return kd.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static bool TryParse(string json, string wantedName, out LadderStats stats)
        {
            stats = null;
            try
            {
                stats = Parse(json, wantedName);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static LadderStats Parse(string json, string wantedName)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
            if (root == null) return Missed();
            object rowsObj;
            if (!root.TryGetValue("rows", out rowsObj) || rowsObj == null || rowsObj is string) return Missed();
            IEnumerable rows = rowsObj as IEnumerable;
            if (rows == null) return Missed();

            Dictionary<string, object> match = null;
            int matches = 0;
            foreach (object item in rows)
            {
                Dictionary<string, object> row = item as Dictionary<string, object>;
                if (row == null) continue;
                object nameObj;
                if (!row.TryGetValue("displayName", out nameObj) || nameObj == null) continue;
                if (!string.Equals(nameObj.ToString(), wantedName, StringComparison.OrdinalIgnoreCase)) continue;
                matches++;
                match = row;
            }
            if (matches != 1 || match == null) return Missed();

            LadderStats s = new LadderStats();
            int rank;
            if (TryInt(match, "rank", out rank))
            {
                s.HasRank = true;
                s.Rank = rank;
            }
            int kills, deaths;
            if (TryInt(match, "kills", out kills) && TryInt(match, "deaths", out deaths))
            {
                s.HasKd = true;
                s.Kd = KdRatio(kills, deaths);
                s.KdText = FormatKd(s.Kd);
            }
            if (!s.HasRank && !s.HasKd) return Missed();
            return s;
        }

        static bool TryInt(Dictionary<string, object> row, string key, out int value)
        {
            value = 0;
            object o;
            if (!row.TryGetValue(key, out o) || o == null) return false;
            try
            {
                value = Convert.ToInt32(o, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public sealed class LadderCache
    {
        readonly object gate = new object();
        readonly Dictionary<string, LadderStats> map = new Dictionary<string, LadderStats>(StringComparer.OrdinalIgnoreCase);

        public bool Contains(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            lock (gate) return map.ContainsKey(name);
        }

        public LadderStats Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            lock (gate)
            {
                LadderStats s;
                return map.TryGetValue(name, out s) ? s : null;
            }
        }

        public void Store(string name, LadderStats stats)
        {
            if (string.IsNullOrEmpty(name) || stats == null) return;
            lock (gate) map[name] = stats;
        }
    }

    public sealed class LadderFetchResult
    {
        public bool Failed;
        public bool RateLimited;
        public string Reason;
        public LadderStats Stats;
    }

    // The gate uses the log clock. The gap uses the wall clock, because it is the time since the last request finished.
    public static class LadderQueue
    {
        public const double GateMinutes = 5;
        public const double GapSeconds = 15;
        public const double BackoffSeconds = 120;

        public static bool GateOpen(MatchSnapshot snap)
        {
            if (snap == null) return false;
            if (snap.Phase != MatchPhase.Started) return false;
            if (!snap.HasLastEvent) return false;
            return snap.LastEventTime - snap.StartTime >= TimeSpan.FromMinutes(GateMinutes);
        }

        public static bool Due(DateTime utcNow, DateTime lastFinishedUtc, bool backoff)
        {
            if (lastFinishedUtc == DateTime.MinValue) return true;
            double need = backoff ? BackoffSeconds : GapSeconds;
            return (utcNow - lastFinishedUtc).TotalSeconds >= need;
        }

        public static List<AlivePlayer> Visible(MatchSnapshot snap, int maxRows)
        {
            List<AlivePlayer> list = new List<AlivePlayer>();
            if (snap == null || snap.Phase != MatchPhase.Started) return list;
            int n = snap.Alive.Count;
            if (maxRows < n) n = maxRows;
            if (n < 0) n = 0;
            for (int i = 0; i < n; i++) list.Add(snap.Alive[i]);
            return list;
        }

        // visible is already in panel order: in-game rank, then kills this match.
        public static string NextName(IList<AlivePlayer> visible, LadderCache cache)
        {
            if (visible == null || cache == null) return null;
            foreach (AlivePlayer p in visible)
            {
                if (p == null || string.IsNullOrEmpty(p.Name)) continue;
                if (!cache.Contains(p.Name)) return p.Name;
            }
            return null;
        }
    }

    public static class LadderClient
    {
        public const string UserAgent = "ROTK-alive-tracker";
        const int TimeoutMs = 8000;
        const int MaxChars = 256 * 1024;
        static bool tlsReady;

        // Framework 4 starts on TLS 1.0 (protocol 240). rotk.app closes that handshake.
        static void EnsureTls()
        {
            if (tlsReady) return;
            tlsReady = true;
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
            catch (NotSupportedException) { }
        }

        public static LadderFetchResult Fetch(string name, string region, string mode, Action<HttpWebRequest> onStart)
        {
            EnsureTls();
            LadderFetchResult fail = new LadderFetchResult();
            fail.Failed = true;
            if (string.IsNullOrEmpty(name))
            {
                fail.Reason = "empty name";
                return fail;
            }
            if (string.IsNullOrEmpty(region)) region = "eu";
            if (string.IsNullOrEmpty(mode)) mode = "solo";

            string url = "https://rotk.app/api/leaderboard/players?region=" + Uri.EscapeDataString(region)
                + "&mode=" + Uri.EscapeDataString(mode)
                + "&type=public&limit=5&q=" + Uri.EscapeDataString(name);
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Accept = "application/json";
                req.UserAgent = UserAgent;
                req.Timeout = TimeoutMs;
                req.ReadWriteTimeout = TimeoutMs;
                if (onStart != null) onStart(req);

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream stream = resp.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    char[] buf = new char[MaxChars];
                    int n = reader.Read(buf, 0, buf.Length);
                    string json = n > 0 ? new string(buf, 0, n) : "";
                    LadderStats stats;
                    if (!LadderStats.TryParse(json, name, out stats))
                    {
                        fail.Reason = "unreadable response";
                        return fail;
                    }
                    LadderFetchResult ok = new LadderFetchResult();
                    ok.Stats = stats;
                    return ok;
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse resp = ex.Response as HttpWebResponse;
                if (resp != null && (int)resp.StatusCode == 429)
                {
                    LadderFetchResult limited = new LadderFetchResult();
                    limited.RateLimited = true;
                    limited.Reason = "429";
                    return limited;
                }
                fail.Reason = ex.Message;
                return fail;
            }
            catch (Exception ex)
            {
                fail.Reason = ex.Message;
                return fail;
            }
        }
    }

    // One request at a time. The UI thread only publishes the latest snapshot and reads the cache.
    public sealed class LadderService
    {
        readonly LadderCache cache = new LadderCache();
        readonly object stateGate = new object();
        readonly object requestGate = new object();
        Thread worker;
        volatile bool stop;
        MatchSnapshot latest;
        int maxRows = 15;
        string region = "eu";
        string mode = "solo";
        HttpWebRequest inflight;
        int generation;

        public LadderCache Cache { get { return cache; } }
        public int Generation { get { return Thread.VolatileRead(ref generation); } }

        public void Start()
        {
            if (worker != null) return;
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Name = "ladder";
            worker.Start();
        }

        public void Stop()
        {
            stop = true;
            lock (requestGate)
            {
                if (inflight != null)
                {
                    try { inflight.Abort(); }
                    catch (Exception) { }
                }
            }
        }

        public void Observe(MatchSnapshot snap, int rows, string ladderRegion, string ladderMode)
        {
            lock (stateGate)
            {
                latest = snap;
                maxRows = rows;
                if (!string.IsNullOrEmpty(ladderRegion)) region = ladderRegion;
                if (!string.IsNullOrEmpty(ladderMode)) mode = ladderMode;
            }
        }

        void Track(HttpWebRequest req)
        {
            lock (requestGate) inflight = req;
            if (stop) req.Abort();
        }

        void Loop()
        {
            DateTime lastFinished = DateTime.MinValue;
            bool backoff = false;
            while (!stop)
            {
                MatchSnapshot snap;
                int rows;
                string reg;
                string md;
                lock (stateGate)
                {
                    snap = latest;
                    rows = maxRows;
                    reg = region;
                    md = mode;
                }

                string name = null;
                if (LadderQueue.GateOpen(snap) && LadderQueue.Due(DateTime.UtcNow, lastFinished, backoff))
                    name = LadderQueue.NextName(LadderQueue.Visible(snap, rows), cache);

                if (name == null)
                {
                    Thread.Sleep(250);
                    continue;
                }

                LadderFetchResult result = LadderClient.Fetch(name, reg, md, Track);
                lock (requestGate) inflight = null;
                if (stop) break;

                lastFinished = DateTime.UtcNow;
                if (result.RateLimited)
                {
                    backoff = true;
                    DiagLog.Once("ladder-429", "leaderboard asked us to slow down; waiting two minutes");
                }
                else if (result.Failed || result.Stats == null)
                {
                    backoff = false;
                    DiagLog.Once("ladder-fail", "leaderboard lookup failed: " + result.Reason);
                }
                else
                {
                    backoff = false;
                    cache.Store(name, result.Stats);
                    Interlocked.Increment(ref generation);
                    if (result.Stats.Miss)
                        DiagLog.Write("ladder " + name + ": no exact match");
                    else
                        DiagLog.Write("ladder " + name + " #" + result.Stats.Rank.ToString(CultureInfo.InvariantCulture)
                            + " " + (result.Stats.KdText ?? ""));
                }
            }
        }
    }
}
