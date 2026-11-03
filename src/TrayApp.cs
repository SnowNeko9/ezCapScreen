using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class TrayApp : ApplicationContext
{
    const int WM_HOTKEY = 0x0312, HotkeyId = 1, SW_RESTORE = 9;
    const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr window, int command);

    // Hidden window for hotkeys. Another copy of the program finds it by title to ask this one to exit.
    sealed class HotkeyWindow : NativeWindow
    {
        readonly Action pressed, closing;

        public HotkeyWindow(Action pressed, Action closing)
        {
            this.pressed = pressed;
            this.closing = closing;
            CreateHandle(new CreateParams { Caption = Program.ControlWindowTitle });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.WM_CLOSE)
            {
                closing();
                return;
            }
            if (m.Msg == WM_HOTKEY) pressed();
            base.WndProc(ref m);
        }
    }

    readonly HotkeyWindow window;
    readonly NotifyIcon tray;
    readonly Icon windowIcon = LoadIcon(new Size(32, 32));
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly ToolStripMenuItem captureItem = new ToolStripMenuItem();
    readonly ToolStripMenuItem settingsItem = new ToolStripMenuItem();
    readonly ToolStripMenuItem exitItem = new ToolStripMenuItem();
    readonly System.Windows.Forms.Timer retry = new System.Windows.Forms.Timer { Interval = 5000 };
    Keys hotkey = Settings.Hotkey;
    bool hotkeyBound;
    bool failureAnnounced;
    Overlay overlay;
    SettingsForm settings;

    public TrayApp(bool quietStart)
    {
        window = new HotkeyWindow(StartCapture, ExitThread);
        captureItem.Click += delegate { StartCapture(); };
        settingsItem.Click += delegate { OpenSettings(); };
        exitItem.Click += delegate { ExitThread(); };
        menu.Items.Add(captureItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        ApplyLanguage();

        tray = new NotifyIcon { Icon = LoadIcon(SystemInformation.SmallIconSize), ContextMenuStrip = menu, Text = "ezCapScreen", Visible = true };
        tray.MouseClick += (sender, e) => { if (e.Button == MouseButtons.Left) StartCapture(); };

        retry.Tick += delegate { BindHotkey(true); };
        BindHotkey(!quietStart);
    }

    void ApplyLanguage()
    {
        captureItem.Text = Strings.MenuCapture;
        settingsItem.Text = Strings.MenuSettings;
        exitItem.Text = Strings.MenuExit;
    }

    void BindHotkey(bool announce)
    {
        hotkeyBound = Register(hotkey);
        retry.Enabled = !hotkeyBound;
        captureItem.ShortcutKeyDisplayString = HotkeyRules.Label(hotkey);
        if (!hotkeyBound)
        {
            if (!failureAnnounced)
                Notify(Strings.HotkeyBusy(HotkeyRules.Label(hotkey)));
            failureAnnounced = true;
            return;
        }
        if (announce) Notify(Strings.PressToCapture(HotkeyRules.Label(hotkey)));
        failureAnnounced = false;
    }

    bool Register(Keys keys)
    {
        uint modifiers = MOD_NOREPEAT;
        if ((keys & Keys.Alt) != 0) modifiers |= MOD_ALT;
        if ((keys & Keys.Control) != 0) modifiers |= MOD_CONTROL;
        if ((keys & Keys.Shift) != 0) modifiers |= MOD_SHIFT;
        return RegisterHotKey(window.Handle, HotkeyId, modifiers, (uint)(keys & Keys.KeyCode));
    }

    bool CanRegister(Keys keys)
    {
        if (!Register(keys)) return false;
        UnregisterHotKey(window.Handle, HotkeyId);
        return true;
    }

    void OpenSettings()
    {
        if (overlay != null || settings != null) return;
        // While the box listens, the current hotkey must reach it instead of opening a capture.
        if (hotkeyBound) UnregisterHotKey(window.Handle, HotkeyId);
        hotkeyBound = false;
        retry.Stop();
        tray.ContextMenuStrip = null;

        var chosen = hotkey;
        Theme.Load();
        using (settings = new SettingsForm(hotkey, windowIcon, CanRegister))
        {
            if (settings.ShowDialog() == DialogResult.OK) chosen = settings.Hotkey;
        }
        settings = null;

        ApplyLanguage();
        tray.ContextMenuStrip = menu;
        bool changed = chosen != hotkey;
        hotkey = chosen;
        failureAnnounced = false;
        BindHotkey(changed);
    }

    void Notify(string message)
    {
        tray.ShowBalloonTip(3000, "ezCapScreen", message, ToolTipIcon.Info);
    }

    void StartCapture()
    {
        if (settings != null)
        {
            settings.Activate();
            return;
        }
        if (overlay != null) return;
        var previous = GetForegroundWindow();
        bool previousWasMinimized = IsIconic(previous);
        try
        {
            overlay = new Overlay();
        }
        catch (Win32Exception ex)
        {
            // The secure desktop (UAC prompt, lock screen) refuses BitBlt.
            Notify(Strings.CaptureFailed(ex.Message));
            return;
        }
        overlay.FormClosed += delegate
        {
            overlay.Dispose();
            overlay = null;
            // An exclusive-fullscreen game minimizes itself when the overlay takes focus and does not come back alone.
            if (!previousWasMinimized && IsWindow(previous) && IsIconic(previous))
            {
                ShowWindow(previous, SW_RESTORE);
                SetForegroundWindow(previous);
            }
        };
        overlay.Show();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            retry.Dispose();
            tray.Dispose();
            menu.Dispose();
            if (hotkeyBound) UnregisterHotKey(window.Handle, HotkeyId);
            window.DestroyHandle();
        }
        base.Dispose(disposing);
    }

    static Icon LoadIcon(Size size)
    {
        using (var stream = typeof(TrayApp).Assembly.GetManifestResourceStream("ezCapScreen.ico"))
            return new Icon(stream, size);
    }
}
