using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    public const string ControlWindowTitle = "ezCapScreen control";
    public const int WM_CLOSE = 0x0010;
    const string ExitArgument = "--exit";
    static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);

    [DllImport("user32.dll")]
    static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string windowClass, string title);

    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    // Used by the installer and uninstaller. Waits, so the running exe is unlocked when this returns.
    static void AskRunningCopyToExit(Mutex running)
    {
        var window = FindWindow(null, ControlWindowTitle);
        if (window == IntPtr.Zero) return;
        PostMessage(window, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        try
        {
            if (running.WaitOne(5000)) running.ReleaseMutex();
        }
        catch (AbandonedMutexException) { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        bool first;
        using (var mutex = new Mutex(true, "ezCapScreen-single-instance", out first))
        {
            bool exitRequested = args.Contains(ExitArgument);
            if (!first && exitRequested) AskRunningCopyToExit(mutex);
            if (!first || exitRequested) return;
            Application.EnableVisualStyles();
            Strings.Current = Settings.Language;
            using (var app = new TrayApp(args.Contains(Settings.AutoStartArgument)))
                Application.Run(app);
        }
    }
}
