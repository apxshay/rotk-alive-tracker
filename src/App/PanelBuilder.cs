using System;
using System.Globalization;

namespace RotkAlive.App
{
    // Turns the match state into what the panel shows. The emblem is the in-game tier.
    // The gold column is the season ladder place, the dim column is season K/D, and the
    // far-right number is kills this match.
    static class PanelBuilder
    {
        public sealed class State
        {
            public bool HaveLogDir;
            public string LogDir;
            public string MissingDir;
            public MatchSnapshot Snapshot;
            public bool Stale;
            public TimeSpan Age;
            public int Errors;
        }

        public static PanelModel Build(State st, Settings settings, bool moveMode, LadderCache ladder)
        {
            PanelModel m = new PanelModel();
            m.MoveMode = moveMode;

            if (!st.HaveLogDir)
            {
                m.Title = "NO LOG FOLDER";
                m.SubLine = st.MissingDir ?? LogLocator.DefaultRoot + "\\Logs";
                return m;
            }

            MatchSnapshot s = st.Snapshot;
            string extras = "";
            if (s.Unparsed > 0) extras += "  " + s.Unparsed + " unparsed";
            if (st.Errors > 0) extras += "  !";

            if (s.Phase == MatchPhase.NoData)
            {
                m.Title = "WAITING";
                m.SubLine = "Waiting for game logs" + extras;
                return m;
            }

            m.Stale = st.Stale;
            string lastClock = s.LastEventTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            m.Status = st.Stale ? "STALE " + lastClock : Age(st.Age) + extras;

            if (s.Phase == MatchPhase.WaitingForStart)
            {
                m.Title = "WAITING";
                m.SubLine = "Waiting for match start";
                return m;
            }

            m.Title = "ALIVE " + s.Alive.Count.ToString(CultureInfo.InvariantCulture);
            if (s.Alive.Count == 0)
            {
                m.SubLine = "NOBODY REVEALED YET  ·  START " + s.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
                return m;
            }

            m.SubLine = "TOP  " + DisplayName(s.Top.Name, settings);
            m.SubLineGold = true;

            int shown = Math.Min(s.Alive.Count, settings.MaxRows);
            for (int i = 0; i < shown; i++)
            {
                AlivePlayer p = s.Alive[i];
                PanelRow r = new PanelRow();
                r.IconKey = p.RankInfo.IconKey;
                r.Name = DisplayName(p.Name, settings);
                r.Kills = p.Kills;
                r.Color = OverlayRenderer.TierColor(r.IconKey);
                LadderStats stats = ladder == null ? null : ladder.Find(p.Name);
                if (stats != null && !stats.Miss && stats.HasRank)
                {
                    r.HasLadderRank = true;
                    r.LadderRank = stats.Rank;
                }
                if (stats != null && !stats.Miss && stats.HasKd)
                    r.SideText = stats.KdText;
                m.Rows.Add(r);
            }

            int hidden = s.Alive.Count - shown;
            if (hidden > 0) m.Footer = "+" + hidden.ToString(CultureInfo.InvariantCulture) + " MORE";
            return m;
        }

        public static string DisplayName(string name, Settings settings)
        {
            return settings.UppercaseNames ? name.ToUpperInvariant() : name;
        }

        static string Age(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            if (t.TotalSeconds < 60) return ((int)t.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "S AGO";
            if (t.TotalMinutes < 60) return ((int)t.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "M AGO";
            return ((int)t.TotalHours).ToString(CultureInfo.InvariantCulture) + "H AGO";
        }
    }
}
