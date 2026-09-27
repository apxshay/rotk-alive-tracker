using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace RotkAlive
{
    public static class LogLocator
    {
        public const string KillFeedName = "KillFeed.log";
        public const string MatchEndScreenName = "MatchEndScreen.log";
        public const string DefaultRoot = @"C:\Games\ROTK";

        public static List<string> Candidates(string overrideDir)
        {
            List<string> list = new List<string>();
            if (!string.IsNullOrEmpty(overrideDir)) AddUnique(list, overrideDir.Trim());

            string launcher = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ROTK Launcher");
            foreach (string name in new string[] { "config.v1.json", "config.v1.backup.json" })
            {
                string root = ReadInstallationRoot(Path.Combine(launcher, name));
                if (!string.IsNullOrEmpty(root)) AddUnique(list, Path.Combine(root, "Logs"));
            }

            AddUnique(list, Path.Combine(DefaultRoot, "Logs"));
            return list;
        }

        // Returns the first candidate directory that exists, or null. firstCandidate is what to show when none exist.
        public static string Resolve(string overrideDir, out string firstCandidate)
        {
            List<string> list = Candidates(overrideDir);
            firstCandidate = list[0];
            foreach (string dir in list)
                if (Directory.Exists(dir)) return dir;
            return null;
        }

        public static string ReadInstallationRoot(string configPath)
        {
            try
            {
                if (!File.Exists(configPath)) return null;
                string json;
                using (FileStream fs = new FileStream(configPath, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    json = sr.ReadToEnd();

                Dictionary<string, object> doc =
                    new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (doc == null) return null;
                object inst;
                if (!doc.TryGetValue("installation", out inst)) return null;
                Dictionary<string, object> installation = inst as Dictionary<string, object>;
                if (installation == null) return null;
                object root;
                if (!installation.TryGetValue("root", out root)) return null;
                return root as string;
            }
            catch (Exception ex)
            {
                DiagLog.Once("config:" + configPath, "could not read " + configPath + ": " + ex.Message);
                return null;
            }
        }

        static void AddUnique(List<string> list, string dir)
        {
            foreach (string d in list)
                if (string.Equals(d.TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
            list.Add(dir);
        }
    }
}
