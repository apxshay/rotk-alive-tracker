using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace RotkAlive
{
    public sealed class TailResult
    {
        public bool Missing;
        public bool Replaced;
        public string Error;
        public int FirstLineNo;
        public readonly List<string> Lines = new List<string>();
    }

    // Reads only complete lines appended since the last poll. The file is opened with full
    // sharing and closed before Poll returns, so the game can keep writing, replace or delete it.
    public sealed class LogTailer
    {
        const int PrefixLength = 256;
        static readonly Encoding Utf8 = new UTF8Encoding(false, false);

        readonly string path;
        long offset;
        bool haveIdentity;
        uint volumeSerial;
        ulong fileIndex;
        byte[] prefix = new byte[0];
        byte[] pending = new byte[0];
        int nextLineNo = 1;

        public LogTailer(string path)
        {
            this.path = path;
            LastWriteUtc = DateTime.MinValue;
        }

        public string FilePath { get { return path; } }
        public bool Exists { get; private set; }
        public DateTime LastWriteUtc { get; private set; }

        public TailResult Poll()
        {
            TailResult r = new TailResult();
            FileStream fs;
            try
            {
                fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096);
            }
            catch (FileNotFoundException) { Exists = false; r.Missing = true; return r; }
            catch (DirectoryNotFoundException) { Exists = false; r.Missing = true; return r; }
            catch (IOException ex) { r.Error = ex.Message; return r; }
            catch (UnauthorizedAccessException ex) { r.Error = ex.Message; return r; }

            try
            {
                using (fs)
                {
                    Exists = true;
                    ByHandleFileInformation info;
                    bool gotInfo = GetFileInformationByHandle(fs.SafeFileHandle, out info);
                    ulong index = gotInfo ? (((ulong)info.FileIndexHigh << 32) | info.FileIndexLow) : 0;
                    LastWriteUtc = gotInfo ? FromFileTime(info.LastWriteTime) : File.GetLastWriteTimeUtc(path);

                    long length = fs.Length;
                    byte[] head = ReadAt(fs, 0, (int)Math.Min(PrefixLength, length));

                    if (haveIdentity)
                    {
                        bool replaced =
                            (gotInfo && (info.VolumeSerialNumber != volumeSerial || index != fileIndex)) ||
                            length < offset ||
                            !SamePrefix(prefix, head);
                        if (replaced)
                        {
                            offset = 0;
                            pending = new byte[0];
                            nextLineNo = 1;
                            prefix = new byte[0];
                            r.Replaced = true;
                        }
                    }

                    haveIdentity = true;
                    if (gotInfo) { volumeSerial = info.VolumeSerialNumber; fileIndex = index; }
                    if (head.Length >= prefix.Length) prefix = head;

                    r.FirstLineNo = nextLineNo;
                    if (length > offset)
                    {
                        byte[] fresh = ReadAt(fs, offset, (int)(length - offset));
                        offset += fresh.Length;
                        SplitLines(fresh, r.Lines);
                        nextLineNo += r.Lines.Count;
                    }
                }
            }
            catch (IOException ex) { r.Error = ex.Message; }
            return r;
        }

        void SplitLines(byte[] fresh, List<string> lines)
        {
            byte[] buf;
            if (pending.Length == 0) buf = fresh;
            else
            {
                buf = new byte[pending.Length + fresh.Length];
                Buffer.BlockCopy(pending, 0, buf, 0, pending.Length);
                Buffer.BlockCopy(fresh, 0, buf, pending.Length, fresh.Length);
            }

            int start = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i] != 0x0A) continue;
                int end = i;
                if (end > start && buf[end - 1] == 0x0D) end--;
                lines.Add(Utf8.GetString(buf, start, end - start));
                start = i + 1;
            }

            int rest = buf.Length - start;
            pending = new byte[rest];
            if (rest > 0) Buffer.BlockCopy(buf, start, pending, 0, rest);
        }

        static byte[] ReadAt(FileStream fs, long position, int count)
        {
            byte[] buf = new byte[count];
            fs.Seek(position, SeekOrigin.Begin);
            int total = 0;
            while (total < count)
            {
                int n = fs.Read(buf, total, count - total);
                if (n <= 0) break;
                total += n;
            }
            if (total == count) return buf;
            byte[] shorter = new byte[total];
            Buffer.BlockCopy(buf, 0, shorter, 0, total);
            return shorter;
        }

        static bool SamePrefix(byte[] known, byte[] head)
        {
            int n = Math.Min(known.Length, head.Length);
            for (int i = 0; i < n; i++)
                if (known[i] != head[i]) return false;
            return true;
        }

        static DateTime FromFileTime(FILETIME ft)
        {
            long v = ((long)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
            return DateTime.FromFileTimeUtc(v);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out ByHandleFileInformation info);
    }
}
