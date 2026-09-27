using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace RotkAlive.Tests
{
    static class Checks
    {
        static int passed;
        static int failed;
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        static int Main(string[] args)
        {
            Console.OutputEncoding = Utf8;
            // The PC runs an Italian locale; ranks must still parse with a dot.
            Thread.CurrentThread.CurrentCulture = new CultureInfo("it-IT");

            if (args.Length > 0 && args[0] == "replay")
                return Replay(args.Length > 1 ? args[1] : null);

            ParserChecks();
            ModelChecks();
            FileChecks();

            Console.WriteLine();
            Console.WriteLine("passed " + passed + ", failed " + failed);
            return failed == 0 ? 0 : 1;
        }

        // ---------- parser ----------

        static void ParserChecks()
        {
            Section("parser");

            LogEvent k = Kf(Fixtures.BriefKill);
            Check(k.Kind == EventKind.Kill, "kill parses");
            Check(k.Killer.Name == "theCITYisRED" && k.Killer.Id == "3497557662702907908" && k.Killer.RankText == "5.3", "killer fields");
            Check(k.Victim.Name == "Cyb" && k.Victim.Id == "5619943147552144769", "victim fields");
            Check(k.Assist == null, "no assist on plain kill");
            Check(k.RunId == 1790529693 && k.Seq == 11721 && k.Tick == 450385324, "prefix numbers");
            Check(k.Time == new DateTime(2026, 9, 27, 19, 24, 21), "prefix time");

            Check(Kf(Fixtures.BriefHeadshot).Kind == EventKind.Kill, "headshot kill parses");
            Check(Kf(Fixtures.BriefHeadshot).Victim.Name == "Karspin", "headshot victim name excludes HEADSHOT");

            LogEvent a = Kf(Fixtures.BriefAssist);
            Check(a.Kind == EventKind.Kill && a.Assist != null, "assist kill parses");
            Check(a.Killer.Name == "Lekid" && a.Assist.Name == "Horizon_" && a.Assist.Id == "14461580563613653473", "assist fields");
            Check(a.Victim.Name == "Hostyle", "assist victim");

            LogEvent d = Kf(Fixtures.BriefDeath);
            Check(d.Kind == EventKind.Death && d.Victim.Name == "KingDady" && d.Killer == null, "DEATH parses");

            LogEvent s = Kf(Fixtures.M2SousFiltre);
            Check(s.Kind == EventKind.Kill && s.Killer.Name == "Sous 3x Filtré", "non-ASCII spaced name kept whole");
            LogEvent b = Kf(Fixtures.M2PosthumousAssist);
            Check(b.Killer.Name == "BARA NO STOP" && b.Assist.Name == "Survivor63153485", "spaced killer name with assist");

            Check(Kf(Fixtures.BriefHostChanged).Kind == EventKind.Kill, "machine-name host field accepted");
            Check(Kf(Fixtures.BriefKill).Kind == EventKind.Kill, "[HOST] host field accepted");

            Check(Kf(Fixtures.M2ZeroRank).Killer.Rank == 0.0 && Kf(Fixtures.M2ZeroRank).Killer.RankText == "0.0", "rank 0.0 is a value");
            Check(Math.Abs(Kf(Fixtures.BriefKill).Killer.Rank - 5.3) < 1e-9, "rank parses with invariant dot under it-IT");

            Check(Mes(Fixtures.BriefStart).Kind == EventKind.Start, "EVENT_START_MATCH is start");
            Check(Mes(Fixtures.BriefStargetPacket).Kind == EventKind.Ignored, "HandleMatchStargetPacket is not a start");
            Check(Mes(Fixtures.SummaryNoise).Kind == EventKind.Ignored, "HandleMatchPlayerSummaryPacket ignored");

            LogEvent two = Kf(Fixtures.SynthTwoAssists);
            Check(two.Kind == EventKind.VictimOnly && two.Victim.Name == "Golf" && two.Victim.Id == "888888888888888888", "two assists: victim only");
            LogEvent ds = Kf(Fixtures.SynthDeathSuffix);
            Check(ds.Kind == EventKind.VictimOnly && ds.Victim.Name == "Hotel", "DEATH with unknown suffix: victim only");
            Check(Kf(Fixtures.SynthGarbage).Kind == EventKind.Unparsed, "unknown text unparsed");
            Check(Kf(Fixtures.SynthShort).Kind == EventKind.Unparsed && !Kf(Fixtures.SynthShort).HasPrefix, "short line unparsed");
        }

        // ---------- model ----------

        static void ModelChecks()
        {
            Section("model");

            MatchSnapshot brief = Run(
                Mes(Fixtures.BriefStargetPacket), Mes(Fixtures.BriefStart),
                Kf(Fixtures.BriefKill), Kf(Fixtures.BriefHeadshot), Kf(Fixtures.BriefAssist), Kf(Fixtures.BriefDeath));
            Check(brief.Phase == MatchPhase.Started, "brief match started");
            Check(Names(brief) == "Screedy 5.4, theCITYisRED 5.3, Lekid 5.3, Horizon_ 5.3", "brief alive list sorted by rank then first seen: " + Names(brief));
            Check(brief.Find("5619943147552144769") == null && brief.Find("12808836654268928371") == null, "victim and DEATH never listed");

            MatchSnapshot m1 = Run(Mes(Fixtures.Start1), Kf(Fixtures.M1Kill1), Kf(Fixtures.M1Kill2), Kf(Fixtures.M1Kill3));
            Check(Names(m1) == "lIlIlIlIlIlIlIlIlIlI 5.4, leodakappa 4.3, N4KL 4.1", "match 1 alive: " + Names(m1));

            MatchSnapshot m2 = Run(
                Mes(Fixtures.Start1), Kf(Fixtures.M1Kill1), Kf(Fixtures.M1Kill2), Kf(Fixtures.M1Kill3),
                Mes(Fixtures.Stargetpacket2), Mes(Fixtures.Start2), Mes(Fixtures.SummaryNoise),
                Kf(Fixtures.M2DaqzzKillsSurvivor), Kf(Fixtures.M2PosthumousAssist), Kf(Fixtures.M2SousFiltre),
                Kf(Fixtures.M2TytKillsKayzah), Kf(Fixtures.M2KayzahPosthumousKill), Kf(Fixtures.M2TottinhoDies));
            Check(m2.StartTime == new DateTime(2026, 9, 27, 20, 54, 59), "second start is the reset");
            Check(m2.Find("580198822161572562") == null && m2.Find("4775357920797685090") == null && m2.Find("2687193247964705511") == null,
                "second EVENT_START_MATCH drops match 1 survivors");
            Check(Names(m2) == "BARA NO STOP 5.2, Sous 3x Filtré 4.4, Lapurge76 3.3", "match 2 alive: " + Names(m2));
            Check(m2.Find("10344157588046667421") == null, "posthumous assist stays dead");
            Check(m2.Find("8302646291024083693") == null && m2.Find("7652327888861413276") == null, "posthumous kill: both dead");
            Check(m2.Posthumous == 2, "two posthumous credits counted: " + m2.Posthumous);
            Check(m2.Find("13856340092514558465") == null && m2.Find("3650647432991908622") == null, "both Tottinho ids are victims only");

            MatchSnapshot sticky = Run(Mes(Fixtures.SynthStart3), Kf(Fixtures.SynthSanchezKill), Kf(Fixtures.SynthSanchezAssist));
            AlivePlayer sanchez = sticky.Find("111111111111111111");
            Check(sanchez != null && sanchez.RankText == "5.3", "first rank sticks (5.3 not 5.2)");

            MatchSnapshot zero = Run(Mes(Fixtures.Start2), Kf(Fixtures.M2ZeroRank));
            Check(zero.Alive.Count == 1 && zero.Alive[0].RankText == "0.0", "rank 0.0 player listed");

            MatchSnapshot unknown = Run(Mes(Fixtures.SynthStart3), Kf(Fixtures.SynthTwoAssists), Kf(Fixtures.SynthDeathSuffix), Kf(Fixtures.SynthGarbage));
            Check(unknown.Alive.Count == 0, "nobody from unknown lines is added alive");
            Check(unknown.Unparsed == 3, "unknown lines counted: " + unknown.Unparsed);

            MatchSnapshot waiting = Run(Mes(Fixtures.BriefStargetPacket), Mes(Fixtures.SummaryNoise));
            Check(waiting.Phase == MatchPhase.WaitingForStart || waiting.Phase == MatchPhase.Started, "noise only is not NoData");
            MatchSnapshot noStart = Run(Mes(Fixtures.BriefStargetPacket));
            Check(noStart.Phase == MatchPhase.WaitingForStart, "no start yet: waiting");

            MatchSnapshot empty = Run(Mes(Fixtures.Start2));
            Check(empty.Phase == MatchPhase.Started && empty.Alive.Count == 0, "started with zero kills is empty");

            MatchSnapshot none = Run();
            Check(none.Phase == MatchPhase.NoData, "no events: no data");
        }

        // ---------- files ----------

        static void FileChecks()
        {
            Section("files");
            string dir = Path.Combine(Path.GetTempPath(), "rotk-alive-checks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string mes = Path.Combine(dir, LogLocator.MatchEndScreenName);
                string kf = Path.Combine(dir, LogLocator.KillFeedName);

                LogSession missing = new LogSession(dir);
                missing.Poll();
                Check(missing.Snapshot.Phase == MatchPhase.NoData && !missing.AnyFileExists, "missing files: no data, no error");

                // Mid-match launch: both files already hold match 1, match 2 start and some kills.
                Write(mes, Fixtures.Start1, Fixtures.Stargetpacket2, Fixtures.Start2, Fixtures.SummaryNoise);
                Write(kf, Fixtures.M1Kill1, Fixtures.M1Kill2, Fixtures.M1Kill3, Fixtures.M2SousFiltre);
                LogSession mid = new LogSession(dir);
                mid.Poll();
                Check(Names(mid.Snapshot) == "Sous 3x Filtré 4.4", "overlay launched mid-match rebuilds from last start: " + Names(mid.Snapshot));

                // Start written to the other file after kills were already read.
                string dir2 = dir + "-late";
                Directory.CreateDirectory(dir2);
                try
                {
                    string mes2 = Path.Combine(dir2, LogLocator.MatchEndScreenName);
                    string kf2 = Path.Combine(dir2, LogLocator.KillFeedName);
                    Write(mes2, Fixtures.Start1);
                    Write(kf2, Fixtures.M1Kill1, Fixtures.M2SousFiltre);
                    LogSession late = new LogSession(dir2);
                    late.Poll();
                    Check(late.Snapshot.Alive.Count == 2, "before late start both kills count");
                    Append(mes2, Fixtures.Start2);
                    late.Poll();
                    Check(Names(late.Snapshot) == "Sous 3x Filtré 4.4", "late-arriving start is ordered by seq: " + Names(late.Snapshot));
                }
                finally { Directory.Delete(dir2, true); }

                // Torn line: half a line (cut inside the UTF-8 'é') must yield nothing.
                string torn = Path.Combine(dir, "torn.log");
                byte[] full = Utf8.GetBytes(Fixtures.M2SousFiltre + "\r\n");
                int cut = Utf8.GetBytes(Fixtures.M2SousFiltre.Substring(0, Fixtures.M2SousFiltre.IndexOf('é'))).Length + 1;
                File.WriteAllBytes(torn, Sub(full, 0, cut));
                LogTailer t = new LogTailer(torn);
                TailResult r1 = t.Poll();
                Check(r1.Lines.Count == 0, "torn line not emitted");
                using (FileStream fs = new FileStream(torn, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    fs.Write(full, cut, full.Length - cut);
                TailResult r2 = t.Poll();
                Check(r2.Lines.Count == 1 && r2.Lines[0] == Fixtures.M2SousFiltre, "completed line emitted intact");
                Check(Kf(r2.Lines[0]).Killer.Name == "Sous 3x Filtré", "multi-byte char across reads survives");

                // Game holds a write handle while we read.
                using (FileStream writer = new FileStream(torn, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    byte[] more = Utf8.GetBytes(Fixtures.M2ZeroRank + "\r\n");
                    writer.Write(more, 0, more.Length);
                    writer.Flush();
                    TailResult r3 = t.Poll();
                    Check(r3.Error == null && r3.Lines.Count == 1, "reads while another handle is writing");
                }

                // Replacement: shorter file with a new run id resets state.
                LogSession rep = new LogSession(dir);
                rep.Poll();
                Check(rep.Snapshot.Alive.Count == 1, "before replacement one alive");
                string newRunStart = Fixtures.Start1.Replace("1790534889", "1790600000").Replace("20:51:55", "22:10:00");
                string newRunKill = Fixtures.M2ZeroRank.Replace("1790534889", "1790600000").Replace("20:56:00", "22:11:00").Replace("\t13717\t", "\t11000\t");
                Write(mes, newRunStart);
                rep.Poll();
                Check(rep.Snapshot.RunId == 1790600000 && rep.Snapshot.Phase == MatchPhase.Started && rep.Snapshot.Alive.Count == 0,
                    "replaced MatchEndScreen.log with new run: old kill feed ignored");
                Write(kf, newRunKill);
                rep.Poll();
                Check(Names(rep.Snapshot) == "Survivor15641363 0.0", "replaced KillFeed.log re-read from start: " + Names(rep.Snapshot));

                // Stale KillFeed.log from the previous client run next to a fresh MatchEndScreen.log.
                Write(kf, Fixtures.BriefKill, Fixtures.BriefHeadshot, Fixtures.BriefHostChanged);
                Write(mes, Fixtures.Start1);
                LogSession stale = new LogSession(dir);
                stale.Poll();
                Check(stale.Snapshot.Phase == MatchPhase.Started && stale.Snapshot.Alive.Count == 0 && stale.Snapshot.RunId == 1790534889,
                    "previous run kill feed does not leak into new run");

                // Staleness uses the files' last write time.
                File.SetLastWriteTimeUtc(kf, DateTime.UtcNow.AddMinutes(-30));
                File.SetLastWriteTimeUtc(mes, DateTime.UtcNow.AddMinutes(-30));
                stale.Poll();
                Check(stale.IsStale(DateTime.UtcNow, TimeSpan.FromMinutes(5)), "old files are stale");
                Append(mes, Fixtures.SummaryNoise);
                stale.Poll();
                Check(!stale.IsStale(DateTime.UtcNow, TimeSpan.FromMinutes(5)), "a fresh line clears stale");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // ---------- replay ----------

        static int Replay(string dir)
        {
            if (dir == null)
            {
                string first;
                dir = LogLocator.Resolve(null, out first);
                if (dir == null)
                {
                    Console.WriteLine("Log folder not found: " + first);
                    return 2;
                }
            }
            LogSession session = new LogSession(dir);
            session.Poll();
            MatchSnapshot s = session.Snapshot;
            Console.WriteLine("log dir     " + dir);
            Console.WriteLine("phase       " + s.Phase);
            Console.WriteLine("run id      " + s.RunId);
            if (s.Phase == MatchPhase.Started) Console.WriteLine("match start " + s.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            if (s.HasLastEvent) Console.WriteLine("last event  " + s.LastEventTime.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("stale       " + session.IsStale(DateTime.UtcNow, TimeSpan.FromMinutes(5)));
            Console.WriteLine("posthumous  " + s.Posthumous + ", unparsed " + s.Unparsed);
            Console.WriteLine("alive       " + s.Alive.Count);
            foreach (AlivePlayer p in s.Alive)
                Console.WriteLine("  " + p.RankText.PadLeft(4) + "  " + p.Name + "  (" + p.Id + ")");
            return 0;
        }

        // ---------- helpers ----------

        static LogEvent Kf(string line) { return LineParser.Parse(line, SourceFile.KillFeed, 1); }
        static LogEvent Mes(string line) { return LineParser.Parse(line, SourceFile.MatchEndScreen, 1); }

        static MatchSnapshot Run(params LogEvent[] events)
        {
            MatchModel m = new MatchModel();
            int[] lineNo = new int[2];
            foreach (LogEvent e in events)
            {
                e.LineNo = ++lineNo[(int)e.Source];
                m.Add(e);
            }
            return m.Recompute();
        }

        static string Names(MatchSnapshot s)
        {
            List<string> parts = new List<string>();
            foreach (AlivePlayer p in s.Alive) parts.Add(p.Name + " " + p.RankText);
            return string.Join(", ", parts.ToArray());
        }

        static void Write(string path, params string[] lines)
        {
            File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", Utf8);
        }

        static void Append(string path, params string[] lines)
        {
            File.AppendAllText(path, string.Join("\r\n", lines) + "\r\n", Utf8);
        }

        static byte[] Sub(byte[] b, int start, int count)
        {
            byte[] r = new byte[count];
            Buffer.BlockCopy(b, start, r, 0, count);
            return r;
        }

        static void Section(string name)
        {
            Console.WriteLine("-- " + name);
        }

        static void Check(bool ok, string what)
        {
            if (ok) { passed++; Console.WriteLine("  ok    " + what); }
            else { failed++; Console.WriteLine("  FAIL  " + what); }
        }
    }
}
