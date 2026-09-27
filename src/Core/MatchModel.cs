using System;
using System.Collections.Generic;
using System.Globalization;

namespace RotkAlive
{
    public enum MatchPhase { NoData, WaitingForStart, Started }

    public sealed class AlivePlayer
    {
        public string Id;
        public string Name;
        public double Rank;
        public string RankText;
        public int Order;
    }

    public sealed class MatchSnapshot
    {
        public MatchPhase Phase;
        public long RunId;
        public DateTime StartTime;
        public bool HasLastEvent;
        public DateTime LastEventTime;
        public int Posthumous;
        public int Unparsed;
        public readonly List<AlivePlayer> Alive = new List<AlivePlayer>();

        public AlivePlayer Top { get { return Alive.Count > 0 ? Alive[0] : null; } }

        public AlivePlayer Find(string id)
        {
            foreach (AlivePlayer p in Alive)
                if (p.Id == id) return p;
            return null;
        }
    }

    // Keeps every event of the files as read so far and rebuilds the alive list from scratch
    // on each change, so mid-match launch, late cross-file arrival and file replacement share one path.
    public sealed class MatchModel
    {
        readonly List<LogEvent>[] events = { new List<LogEvent>(), new List<LogEvent>() };
        readonly LogEvent[] lastParsed = new LogEvent[2];
        readonly HashSet<string> loggedPosthumous = new HashSet<string>();
        long loggedRun = long.MinValue;
        string loggedStart;

        sealed class PlayerState
        {
            public string Name;
            public double Rank;
            public string RankText;
            public int Order;
            public bool Dead;
        }

        public void ResetSource(SourceFile source)
        {
            events[(int)source].Clear();
            lastParsed[(int)source] = null;
        }

        public void Add(LogEvent e)
        {
            int i = (int)e.Source;
            if (e.HasPrefix)
            {
                lastParsed[i] = e;
            }
            else
            {
                LogEvent prev = lastParsed[i];
                if (prev == null) return;
                e.Inherited = true;
                e.Time = prev.Time;
                e.RunId = prev.RunId;
                e.HasSeq = prev.HasSeq;
                e.Seq = prev.Seq;
            }
            events[i].Add(e);
        }

        public MatchSnapshot Recompute()
        {
            MatchSnapshot snap = new MatchSnapshot();

            LogEvent latest = null;
            foreach (LogEvent lp in lastParsed)
            {
                if (lp == null) continue;
                if (latest == null || lp.Time > latest.Time) latest = lp;
            }
            if (latest == null)
            {
                snap.Phase = MatchPhase.NoData;
                return snap;
            }

            long run = latest.RunId;
            snap.RunId = run;
            snap.HasLastEvent = true;
            snap.LastEventTime = latest.Time;
            if (run != loggedRun)
            {
                loggedRun = run;
                loggedPosthumous.Clear();
                DiagLog.Write("current client run id " + run.ToString(CultureInfo.InvariantCulture));
            }

            List<LogEvent> list = new List<LogEvent>();
            bool allSeq = true;
            foreach (List<LogEvent> src in events)
                foreach (LogEvent e in src)
                {
                    if (e.RunId != run) continue;
                    list.Add(e);
                    if (!e.HasSeq) allSeq = false;
                }
            list.Sort(allSeq ? (Comparison<LogEvent>)CompareBySeq : CompareByTime);

            int lastStart = -1;
            for (int k = list.Count - 1; k >= 0; k--)
                if (list[k].Kind == EventKind.Start) { lastStart = k; break; }

            if (lastStart < 0)
            {
                snap.Phase = MatchPhase.WaitingForStart;
                return snap;
            }

            LogEvent start = list[lastStart];
            snap.Phase = MatchPhase.Started;
            snap.StartTime = start.Time;
            string startKey = run + ":" + start.Seq + ":" + start.Time.Ticks;
            if (startKey != loggedStart)
            {
                loggedStart = startKey;
                DiagLog.Write("match start " + start.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                              " seq " + start.Seq + ": alive list reset");
            }

            Dictionary<string, PlayerState> players = new Dictionary<string, PlayerState>();
            int order = 0;
            for (int k = lastStart + 1; k < list.Count; k++)
            {
                LogEvent e = list[k];
                switch (e.Kind)
                {
                    case EventKind.Kill:
                        Touch(players, ref order, e.Killer, true, e, snap);
                        if (e.Assist != null) Touch(players, ref order, e.Assist, true, e, snap);
                        Touch(players, ref order, e.Victim, false, e, snap);
                        break;
                    case EventKind.Death:
                        Touch(players, ref order, e.Victim, false, e, snap);
                        break;
                    case EventKind.VictimOnly:
                        Touch(players, ref order, e.Victim, false, e, snap);
                        snap.Unparsed++;
                        break;
                    case EventKind.Unparsed:
                        snap.Unparsed++;
                        break;
                }
            }

            foreach (KeyValuePair<string, PlayerState> kv in players)
            {
                if (kv.Value.Dead) continue;
                AlivePlayer a = new AlivePlayer();
                a.Id = kv.Key;
                a.Name = kv.Value.Name;
                a.Rank = kv.Value.Rank;
                a.RankText = kv.Value.RankText;
                a.Order = kv.Value.Order;
                snap.Alive.Add(a);
            }
            snap.Alive.Sort(delegate(AlivePlayer x, AlivePlayer y)
            {
                int c = y.Rank.CompareTo(x.Rank);
                return c != 0 ? c : x.Order.CompareTo(y.Order);
            });
            return snap;
        }

        void Touch(Dictionary<string, PlayerState> players, ref int order, PlayerRef p, bool alive,
                   LogEvent e, MatchSnapshot snap)
        {
            PlayerState st;
            if (!players.TryGetValue(p.Id, out st))
            {
                st = new PlayerState();
                st.Name = p.Name;
                st.Rank = p.Rank;
                st.RankText = p.RankText;
                st.Order = order++;
                st.Dead = !alive;
                players.Add(p.Id, st);
                return;
            }

            if (!alive)
            {
                st.Dead = true;
                return;
            }

            if (st.Dead)
            {
                snap.Posthumous++;
                string key = e.RunId + ":" + e.Seq + ":" + (int)e.Source + ":" + e.LineNo + ":" + p.Id;
                if (loggedPosthumous.Add(key))
                    DiagLog.Write("posthumous: " + p.Name + " (" + p.Id + ") credited at " +
                                  e.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " seq " + e.Seq +
                                  " after dying in this match; stays dead");
            }
        }

        static int CompareBySeq(LogEvent a, LogEvent b)
        {
            int c = a.Seq.CompareTo(b.Seq);
            if (c != 0) return c;
            c = ((int)a.Source).CompareTo((int)b.Source);
            return c != 0 ? c : a.LineNo.CompareTo(b.LineNo);
        }

        static int CompareByTime(LogEvent a, LogEvent b)
        {
            int c = a.Time.CompareTo(b.Time);
            if (c != 0) return c;
            int sa = a.Kind == EventKind.Start ? 0 : 1;
            int sb = b.Kind == EventKind.Start ? 0 : 1;
            c = sa.CompareTo(sb);
            if (c != 0) return c;
            c = ((int)a.Source).CompareTo((int)b.Source);
            return c != 0 ? c : a.LineNo.CompareTo(b.LineNo);
        }
    }
}
