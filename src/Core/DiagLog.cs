using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RotkAlive
{
    // Plain text diagnostics next to the exe. Never shows UI. Disabled until Init is called.
    public static class DiagLog
    {
        const long MaxBytes = 256 * 1024;
        static readonly object Gate = new object();
        static readonly HashSet<string> OnceKeys = new HashSet<string>();
        static readonly Encoding Utf8 = new UTF8Encoding(false);
        static string path;

        public static int ErrorCount { get; private set; }

        public static string FilePath { get { return path; } }

        public static void Init(string filePath)
        {
            path = filePath;
        }

        public static void Write(string message)
        {
            if (path == null) return;
            lock (Gate)
            {
                try
                {
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > MaxBytes)
                    {
                        string old = path + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    File.AppendAllText(path, stamp + "  " + message + "\r\n", Utf8);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static void Once(string key, string message)
        {
            lock (Gate)
            {
                if (!OnceKeys.Add(key)) return;
            }
            Write(message);
        }

        public static void Error(string message)
        {
            lock (Gate) { ErrorCount++; }
            Write("ERROR " + message);
        }
    }
}
