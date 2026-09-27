using System;
using System.IO;

namespace RotkAlive
{
    // Both log files of one Logs directory, polled together and merged into one match model.
    public sealed class LogSession
    {
        readonly LogTailer[] tailers;
        readonly MatchModel model = new MatchModel();

        public LogSession(string logDir)
        {
            LogDir = logDir;
            tailers = new LogTailer[2];
            tailers[(int)SourceFile.MatchEndScreen] = new LogTailer(Path.Combine(logDir, LogLocator.MatchEndScreenName));
            tailers[(int)SourceFile.KillFeed] = new LogTailer(Path.Combine(logDir, LogLocator.KillFeedName));
            Snapshot = model.Recompute();
        }

        public string LogDir { get; private set; }
        public MatchSnapshot Snapshot { get; private set; }

        public bool AnyFileExists
        {
            get { return tailers[0].Exists || tailers[1].Exists; }
        }

        public DateTime LastWriteUtc
        {
            get
            {
                DateTime best = DateTime.MinValue;
                foreach (LogTailer t in tailers)
                    if (t.Exists && t.LastWriteUtc > best) best = t.LastWriteUtc;
                return best;
            }
        }

        public bool IsStale(DateTime nowUtc, TimeSpan threshold)
        {
            DateTime last = LastWriteUtc;
            return last == DateTime.MinValue || nowUtc - last > threshold;
        }

        public bool Poll()
        {
            bool changed = false;
            for (int i = 0; i < tailers.Length; i++)
            {
                SourceFile source = (SourceFile)i;
                LogTailer t = tailers[i];
                TailResult r = t.Poll();

                if (r.Error != null)
                    DiagLog.Once("io:" + i + ":" + r.Error, "read failed, will retry: " + t.FilePath + ": " + r.Error);

                if (r.Replaced)
                {
                    model.ResetSource(source);
                    changed = true;
                    DiagLog.Write("file replaced, re-reading from start: " + t.FilePath);
                }

                for (int k = 0; k < r.Lines.Count; k++)
                {
                    string line = r.Lines[k];
                    if (line.Length == 0) continue;
                    LogEvent e = LineParser.Parse(line, source, r.FirstLineNo + k);
                    Report(e, t.FilePath);
                    model.Add(e);
                    changed = true;
                }
            }

            if (changed) Snapshot = model.Recompute();
            return changed;
        }

        static void Report(LogEvent e, string file)
        {
            string name = Path.GetFileName(file);
            if (e.Kind == EventKind.Unparsed || e.Kind == EventKind.VictimOnly)
            {
                string raw = e.Raw.Length > 300 ? e.Raw.Substring(0, 300) + "..." : e.Raw;
                string what = e.Kind == EventKind.VictimOnly ? "unfamiliar line, applied victim only" : "unparsed line, skipped";
                DiagLog.Write(what + " (" + name + " line " + e.LineNo + "): " + raw);
            }
            else if (e.Kind == EventKind.Ignored && e.Message != null && !e.Message.StartsWith("HandleMatch", StringComparison.Ordinal))
            {
                DiagLog.Once("mes:" + e.Message, "unknown " + name + " message ignored: " + e.Message);
            }
        }
    }
}
