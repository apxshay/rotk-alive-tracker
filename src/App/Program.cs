using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace RotkAlive.App
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, @"Local\RotkAliveOverlay.SingleInstance", out created))
            {
                if (!created) return;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                DiagLog.Init(Path.Combine(baseDir, "overlay.log"));
                DiagLog.Write("overlay started");

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    DiagLog.Error(e.Exception.ToString());
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    DiagLog.Error("unhandled: " + e.ExceptionObject);
                };

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Settings settings = Settings.Load(Path.Combine(baseDir, "overlay.ini"));
                Application.Run(new OverlayForm(settings));
                DiagLog.Write("overlay stopped");
                GC.KeepAlive(mutex);
            }
        }
    }
}
