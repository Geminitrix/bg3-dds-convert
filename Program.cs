using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DDS_Convert;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Application.ThreadException += (_, e) => ReportUnhandledException(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportUnhandledException(e.ExceptionObject as Exception);

        var files = args.Where(a => !a.StartsWith('-')).ToArray();

        Application.Run(new MainForm(files));
        return 0;
    }

    static void ReportUnhandledException(Exception? ex)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // If we can't even write the crash log, there's nothing more we can do here.
        }

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{ex?.Message}\n\nDetails were written to crash.log.",
            "BG3 DDS Convert - Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
