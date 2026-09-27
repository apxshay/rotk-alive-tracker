using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RotkAlive
{
    public enum SourceFile { MatchEndScreen = 0, KillFeed = 1 }

    public enum EventKind { Start, Kill, Death, VictimOnly, Ignored, Unparsed }

    public sealed class PlayerRef
    {
        public string Id;
        public string Name;
        public double Rank;
        public string RankText;
    }

    public sealed class LogEvent
    {
        public SourceFile Source;
        public int LineNo;
        public string Raw;
        public EventKind Kind;

        public bool HasPrefix;
        public bool Inherited;
        public DateTime Time;
        public long RunId;
        public bool HasSeq;
        public long Seq;
        public long Tick;
        public string Message;

        public PlayerRef Killer;
        public PlayerRef Assist;
        public PlayerRef Victim;
    }

    public static class LineParser
    {
        public const string StartMessage = "EVENT_START_MATCH";

        // A name may not contain "[rank:", so one player token can never swallow the next one.
        const string PlayerFormat =
            @"(?<{0}name>(?:(?!\[rank:).)+?) \((?<{0}id>\d+)\) \[rank:(?<{0}rank>\d+(?:\.\d+)?)\] \[ping:(?<{0}ping>-?\d+)\]";

        static readonly Regex KillRx = new Regex(
            "^" + P("k") + @"(?: \(ASSIST " + P("a") + @"\))? KILLED " + P("v") + "(?: HEADSHOT)?$",
            RegexOptions.CultureInvariant);

        static readonly Regex DeathRx = new Regex("^DEATH " + P("v") + "$", RegexOptions.CultureInvariant);

        static readonly Regex DeathPrefixRx = new Regex("^DEATH " + P("v"), RegexOptions.CultureInvariant);

        static readonly Regex VictimTailRx = new Regex("^" + P("v") + "(?: HEADSHOT)?$", RegexOptions.CultureInvariant);

        static string P(string prefix)
        {
            return string.Format(CultureInfo.InvariantCulture, PlayerFormat, prefix);
        }

        public static LogEvent Parse(string line, SourceFile source, int lineNo)
        {
            LogEvent e = new LogEvent();
            e.Source = source;
            e.LineNo = lineNo;
            e.Raw = line;
            e.Kind = EventKind.Unparsed;

            string[] f = line.Split(new char[] { '\t' }, 8);
            if (f.Length < 8) return e;

            DateTime time;
            if (!DateTime.TryParseExact(f[0] + " " + f[1], "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                return e;

            long runId;
            if (!long.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out runId))
                return e;

            e.HasPrefix = true;
            e.Time = time;
            e.RunId = runId;
            e.HasSeq = long.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out e.Seq);
            long.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out e.Tick);
            e.Message = f[7].TrimEnd();

            if (source == SourceFile.MatchEndScreen)
            {
                e.Kind = e.Message == StartMessage ? EventKind.Start : EventKind.Ignored;
                return e;
            }

            ParseKillFeedMessage(e);
            return e;
        }

        static void ParseKillFeedMessage(LogEvent e)
        {
            string msg = e.Message;

            Match m = KillRx.Match(msg);
            if (m.Success)
            {
                e.Kind = EventKind.Kill;
                e.Killer = Player(m, "k");
                if (m.Groups["aid"].Success) e.Assist = Player(m, "a");
                e.Victim = Player(m, "v");
                return;
            }

            m = DeathRx.Match(msg);
            if (m.Success)
            {
                e.Kind = EventKind.Death;
                e.Victim = Player(m, "v");
                return;
            }

            // Unknown shape: only a victim can be applied safely.
            int idx = msg.LastIndexOf(" KILLED ", StringComparison.Ordinal);
            while (idx >= 0)
            {
                m = VictimTailRx.Match(msg.Substring(idx + 8));
                if (m.Success)
                {
                    e.Kind = EventKind.VictimOnly;
                    e.Victim = Player(m, "v");
                    return;
                }
                idx = idx == 0 ? -1 : msg.LastIndexOf(" KILLED ", idx - 1, StringComparison.Ordinal);
            }

            m = DeathPrefixRx.Match(msg);
            if (m.Success)
            {
                e.Kind = EventKind.VictimOnly;
                e.Victim = Player(m, "v");
                return;
            }

            e.Kind = EventKind.Unparsed;
        }

        static PlayerRef Player(Match m, string prefix)
        {
            PlayerRef p = new PlayerRef();
            p.Name = m.Groups[prefix + "name"].Value;
            p.Id = m.Groups[prefix + "id"].Value;
            p.RankText = m.Groups[prefix + "rank"].Value;
            p.Rank = double.Parse(p.RankText, NumberStyles.Float, CultureInfo.InvariantCulture);
            return p;
        }
    }
}
