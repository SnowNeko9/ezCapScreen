// End-to-end check of the real ezCapScreen.exe. It covers every monitor with a generated test
// pattern first, so the screens it captures and saves never contain the user's desktop.
// Mouse input goes to the overlay as window messages. The real cursor moves only for the taps below.
// Usage: verify.exe [--only binding|capture|settings] [folder for screenshots and saved files]
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Verify
{
    const uint WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205;
    const uint WM_SETTEXT = 0xC, BM_CLICK = 0xF5, WM_QUIT = 0x12, WM_TRAY = 0x800;
    const uint MOD_ALT = 1, MOD_CONTROL = 2;
    const int VK_TAB = 0x09, VK_SPACE = 0x20, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_ESCAPE = 0x1B, VK_DOWN = 0x28, VK_UP = 0x26, VK_RETURN = 0x0D, VK_F9 = 0x78;
    const uint WM_GETTEXT = 0xD, WM_CHAR = 0x102;
    const string SettingsTitle = "ezCapScreen Settings", AcceptText = "OK", CancelText = "Cancel", AutoStartText = "Start ezCapScreen when Windows starts", PromptText = "Press the new shortcut now...";
    const string ThaiTitle = "ตั้งค่า ezCapScreen", ThaiCancel = "ยกเลิก", ThaiAutoStart = "เปิดโปรแกรมอัตโนมัติตอนเข้า Windows", ThaiPrompt = "กดปุ่มลัดใหม่ได้เลย...";
    const string AppKey = @"HKEY_CURRENT_USER\Software\ezCapScreen", RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static float uiScale = 1f;
    static int ToolbarW = 318, Gap = 8, Row = 40;

    static int Px(int value) { return (int)Math.Round(value * uiScale); }

    struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point p, uint flags);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr w, out RECT r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr w, IntPtr after, int x, int y, int cx, int cy, uint flags);

    static float ScaleOfScreen(Screen screen)
    {
        uint x, y;
        var center = new Point(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2);
        return GetDpiForMonitor(MonitorFromPoint(center, 2), 0, out x, out y) == 0 ? x / 96f : 1f;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr w, uint m, IntPtr a, IntPtr b);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr w, uint m, IntPtr a, string b);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr w, int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool UpdateWindow(IntPtr w);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr w);
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr w, int id, uint mod, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr w, int id);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread, uint m, IntPtr a, IntPtr b);
    [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    delegate bool EnumProc(IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr l);
    [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr w);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr w, System.Text.StringBuilder name, int size);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr w, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr w, uint m, IntPtr a, System.Text.StringBuilder b);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool PostMessage(IntPtr w, uint m, IntPtr a, IntPtr b);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr w);

    static int failures;
    static string outDir;
    static Form pattern;
    static Bitmap patternImage;
    static Rectangle virt;
    static object savedHotkey, savedRun, savedLanguage;
    static string settingsTitle = SettingsTitle;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what);
        if (!ok) failures++;
    }

    static void Pump(int ms)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        do { Application.DoEvents(); Thread.Sleep(10); } while (DateTime.UtcNow < until);
    }

    static bool Wait(Func<bool> condition, int ms)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
        {
            if (condition()) return true;
            Pump(25);
        }
        return condition();
    }

    static Color Expected(int x, int y)
    {
        return Color.FromArgb(255, x & 0xFF, y & 0xFF, ((x >> 8) * 37 + (y >> 8) * 91) & 0xFF);
    }

    static Bitmap MakePattern(Rectangle v)
    {
        var image = new Bitmap(v.Width, v.Height, PixelFormat.Format32bppRgb);
        var data = image.LockBits(new Rectangle(0, 0, v.Width, v.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        var row = new int[v.Width];
        for (int y = 0; y < v.Height; y++)
        {
            for (int x = 0; x < v.Width; x++) row[x] = Expected(x, y).ToArgb();
            Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, v.Width);
        }
        image.UnlockBits(data);
        return image;
    }

    static IntPtr Overlay() { return FindWindow(null, "ezCapScreen overlay"); }

    static IntPtr LParam(int x, int y) { return new IntPtr((y << 16) | (x & 0xFFFF)); }

    static void Move(IntPtr w, int x, int y) { SendMessage(w, WM_MOUSEMOVE, IntPtr.Zero, LParam(x, y)); }

    static void DragMouse(IntPtr w, int x1, int y1, int x2, int y2)
    {
        Move(w, x1, y1);
        SendMessage(w, WM_LBUTTONDOWN, new IntPtr(1), LParam(x1, y1));
        for (int i = 1; i <= 5; i++)
            Move(w, x1 + (x2 - x1) * i / 5, y1 + (y2 - y1) * i / 5);
        SendMessage(w, WM_LBUTTONUP, IntPtr.Zero, LParam(x2, y2));
    }

    static void Click(IntPtr w, int x, int y)
    {
        Move(w, x, y);
        SendMessage(w, WM_LBUTTONDOWN, new IntPtr(1), LParam(x, y));
        SendMessage(w, WM_LBUTTONUP, IntPtr.Zero, LParam(x, y));
    }

    // Windows answers a capture change with a mouse move at the real cursor. A tap counts as a tap only
    // if the real cursor is under it, otherwise that move looks like a long drag from the tap point.
    static void Tap(IntPtr w, int x, int y, int jitter = 0)
    {
        var real = Cursor.Position;
        SetCursorPos(x + virt.X, y + virt.Y);
        Pump(60);
        Move(w, x, y);
        SendMessage(w, WM_LBUTTONDOWN, new IntPtr(1), LParam(x, y));
        Move(w, x + jitter, y + jitter);
        SendMessage(w, WM_LBUTTONUP, IntPtr.Zero, LParam(x + jitter, y + jitter));
        Pump(60);
        SetCursorPos(real.X, real.Y);
    }

    static void ToolbarClick(IntPtr w, Point toolbar, int column, int row = 0)
    {
        Click(w, toolbar.X + Px(6 + column * 35 + 13), toolbar.Y + Px(row * 40 + 20));
    }

    static void PaletteClick(IntPtr w, Point toolbar, int column, int row)
    {
        Click(w, toolbar.X + Px(175 + column * 11 + 5), toolbar.Y + Px(43 + row * 11 + 5));
    }

    static void PressHotkey(byte vk)
    {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 2, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, 2, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, 2, UIntPtr.Zero);
    }

    static IntPtr OpenOverlay(byte vk)
    {
        Check(Overlay() == IntPtr.Zero, "no overlay is open before the hotkey");
        PressHotkey(vk);
        IntPtr w = IntPtr.Zero;
        bool opened = Wait(() => (w = Overlay()) != IntPtr.Zero, 3000);
        Check(opened, "hotkey opens the overlay");
        if (!opened) throw new InvalidOperationException("overlay did not open, every later step would be meaningless");
        Wait(() => GetForegroundWindow() == w, 1500);
        return w;
    }

    static bool Closed(IntPtr w) { return w != IntPtr.Zero && Wait(() => !IsWindow(w), 2000); }

    static void Shot(string name)
    {
        Pump(120);
        using (var b = new Bitmap(virt.Width, virt.Height))
        using (var g = Graphics.FromImage(b))
        {
            g.CopyFromScreen(virt.Location, Point.Empty, virt.Size);
            b.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
        }
    }

    static bool SameColor(Color a, Color b, int tolerance = 0)
    {
        return Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance;
    }

    static bool MatchesPattern(Bitmap image, int originX, int originY)
    {
        for (int y = 0; y < image.Height; y += Math.Max(1, image.Height / 40))
            for (int x = 0; x < image.Width; x += Math.Max(1, image.Width / 40))
                if (!SameColor(image.GetPixel(x, y), Expected(originX + x, originY + y)))
                {
                    Console.WriteLine("  mismatch at " + x + "," + y + " got " + image.GetPixel(x, y) + " want " + Expected(originX + x, originY + y));
                    image.Save(Path.Combine(outDir, "mismatch-" + originX + "-" + originY + ".png"), ImageFormat.Png);
                    return false;
                }
        return true;
    }

    static Bitmap ClipboardImage()
    {
        Image image = null;
        for (int i = 0; i < 20 && image == null; i++)
        {
            try { image = Clipboard.GetImage(); }
            catch (ExternalException) { Pump(50); }
        }
        return image == null ? null : new Bitmap(image);
    }

    static bool Held(char key)
    {
        bool free = RegisterHotKey(IntPtr.Zero, 0x7003, MOD_CONTROL | MOD_ALT, key);
        if (free) UnregisterHotKey(IntPtr.Zero, 0x7003);
        return !free;
    }

    // NotifyIcon delivers tray clicks to its hidden window as this callback message.
    static void TrayMessage(Process app, uint down, uint up)
    {
        EnumWindows((w, l) =>
        {
            uint pid;
            GetWindowThreadProcessId(w, out pid);
            if (pid == app.Id)
            {
                SendMessage(w, WM_TRAY, IntPtr.Zero, new IntPtr(down));
                SendMessage(w, WM_TRAY, IntPtr.Zero, new IntPtr(up));
            }
            return true;
        }, IntPtr.Zero);
    }

    static void PressKey(int vk)
    {
        keybd_event((byte)vk, 0, 0, UIntPtr.Zero);
        keybd_event((byte)vk, 0, 2, UIntPtr.Zero);
    }

    static bool AppIsForeground(Process app)
    {
        uint pid;
        GetWindowThreadProcessId(GetForegroundWindow(), out pid);
        return pid == app.Id;
    }

    // Real key presses follow, so stop unless they are certain to land in the app.
    static void RequireForeground(Process app, string why)
    {
        if (!Wait(() => AppIsForeground(app), 2000)) throw new InvalidOperationException("app is not foreground: " + why);
    }

    static void TrayMenu(Process app, params int[] keys)
    {
        AllowSetForegroundWindow(app.Id);
        TrayMessage(app, WM_RBUTTONDOWN, WM_RBUTTONUP);
        RequireForeground(app, "tray menu");
        Pump(300);
        foreach (var key in keys)
        {
            PressKey(key);
            Pump(60);
        }
    }

    static IntPtr OpenSettings(Process app)
    {
        TrayMenu(app, VK_DOWN, VK_DOWN, VK_RETURN);
        IntPtr d = IntPtr.Zero;
        if (!Wait(() => (d = FindWindow(null, settingsTitle)) != IntPtr.Zero, 3000)) throw new InvalidOperationException("settings did not open from the tray menu");
        RequireForeground(app, "settings dialog");
        Pump(250);
        return d;
    }

    // A real click on the box is what starts listening. Keys pressed before it must do nothing.
    static void Listen(Process app, IntPtr dialog)
    {
        RequireForeground(app, "before clicking the hotkey box");
        var box = HotkeyBox(dialog);
        SendMessage(box, WM_LBUTTONDOWN, new IntPtr(1), LParam(10, 5));
        SendMessage(box, WM_LBUTTONUP, IntPtr.Zero, LParam(10, 5));
        Pump(120);
    }

    static void ClickChild(IntPtr dialog, string text)
    {
        var child = FindWindowEx(dialog, IntPtr.Zero, null, text);
        if (child == IntPtr.Zero) throw new InvalidOperationException("no control named " + text);
        PostMessage(child, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
    }

    static IntPtr HotkeyBox(IntPtr dialog)
    {
        var box = IntPtr.Zero;
        EnumChildWindows(dialog, (c, l) =>
        {
            var cls = new System.Text.StringBuilder(64);
            GetClassName(c, cls, 64);
            if (cls.ToString().Contains("EDIT")) box = c;
            return box == IntPtr.Zero;
        }, IntPtr.Zero);
        return box;
    }

    static string BoxText(IntPtr dialog)
    {
        Pump(150);
        var text = new System.Text.StringBuilder(64);
        SendMessage(HotkeyBox(dialog), WM_GETTEXT, new IntPtr(64), text);
        return text.ToString();
    }

    static void TypeInto(IntPtr window, string text)
    {
        var box = HotkeyBox(window);
        if (box == IntPtr.Zero) throw new InvalidOperationException("no text box in the window");
        foreach (var c in text) PostMessage(box, WM_CHAR, new IntPtr(c), IntPtr.Zero);
        Pump(150);
    }

    static int CountColor(Bitmap image, Color color, Rectangle area)
    {
        int count = 0;
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                if (SameColor(image.GetPixel(x, y), color, 2)) count++;
        return count;
    }

    static int ColorHeight(Bitmap image, Color color, Rectangle area)
    {
        int top = int.MaxValue, bottom = -1;
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                if (SameColor(image.GetPixel(x, y), color, 2))
                {
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
        return bottom < 0 ? 0 : bottom - top + 1;
    }

    static void CloseWith(IntPtr dialog, string button, string what)
    {
        ClickChild(dialog, button);
        Check(Closed(dialog), what);
    }

    static void QuitApp(Process app)
    {
        app.Refresh();
        foreach (ProcessThread t in app.Threads) PostThreadMessage((uint)t.Id, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }

    static object RunEntry()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
            return key.GetValue("ezCapScreen");
    }

    static void SetRunEntry(object value)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (value == null) key.DeleteValue("ezCapScreen", false);
            else key.SetValue("ezCapScreen", value);
        }
    }

    static void SetHotkeyEntry(object value)
    {
        if (value != null)
        {
            Registry.SetValue(AppKey, "Hotkey", value, RegistryValueKind.DWord);
            return;
        }
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\ezCapScreen", true))
            if (key != null) key.DeleteValue("Hotkey", false);
    }

    static void SetLanguageEntry(object value)
    {
        if (value != null)
        {
            Registry.SetValue(AppKey, "Language", value, RegistryValueKind.String);
            return;
        }
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\ezCapScreen", true))
            if (key != null) key.DeleteValue("Language", false);
    }

    static void RestoreUserSettings()
    {
        SetHotkeyEntry(savedHotkey);
        SetLanguageEntry(savedLanguage);
        SetRunEntry(savedRun);
        if (savedHotkey == null && savedLanguage == null) Registry.CurrentUser.DeleteSubKey(@"Software\ezCapScreen", false);
    }

    static Color ScreenPixel(int x, int y)
    {
        Pump(120);
        using (var b = new Bitmap(1, 1))
        using (var g = Graphics.FromImage(b))
        {
            g.CopyFromScreen(x + virt.X, y + virt.Y, 0, 0, new Size(1, 1));
            return b.GetPixel(0, 0);
        }
    }

    static IntPtr WaitSaveDialog()
    {
        IntPtr d = IntPtr.Zero;
        Wait(() => (d = FindWindow("#32770", "Save Screen Capture")) != IntPtr.Zero, 5000);
        return d;
    }

    static void SaveAs(IntPtr overlay, Point toolbar, string path)
    {
        // Save opens a modal dialog inside the mouse-up handler, so the click cannot be a blocking SendMessage.
        var thread = new Thread(() => ToolbarClick(overlay, toolbar, 6));
        thread.Start();
        var dialog = WaitSaveDialog();
        Check(dialog != IntPtr.Zero, "save dialog appears");
        if (dialog == IntPtr.Zero) throw new InvalidOperationException("save dialog did not open");
        Pump(400);
        // The file name box is the Edit with id 1001. A toolbar in the dialog shares that id.
        var edit = IntPtr.Zero;
        EnumChildWindows(dialog, (c, l) =>
        {
            var cls = new System.Text.StringBuilder(16);
            GetClassName(c, cls, 16);
            if (GetDlgCtrlID(c) == 1001 && cls.ToString() == "Edit") edit = c;
            return edit == IntPtr.Zero;
        }, IntPtr.Zero);
        if (edit == IntPtr.Zero)
        {
            // Pressing Save now would write the default name into the user's folder.
            SendMessage(GetDlgItem(dialog, 2), BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            throw new InvalidOperationException("file name box not found in the save dialog");
        }
        SendMessage(edit, WM_SETTEXT, IntPtr.Zero, path);
        SendMessage(GetDlgItem(dialog, 1), BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        thread.Join(5000);
    }

    [STAThread]
    static int Main(string[] args)
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        var exe = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ezCapScreen.exe"));
        string only = args.Length >= 2 && args[0] == "--only" ? args[1] : null;
        var rest = only == null ? args : args.Skip(2).ToArray();
        Func<string, bool> want = name => only == null || only == name;
        outDir = rest.Length > 0 ? rest[0] : Path.Combine(Path.GetTempPath(), "ezCapScreen-verify");
        Directory.CreateDirectory(outDir);

        if (Process.GetProcessesByName("ezCapScreen").Length > 0)
        {
            Console.WriteLine("ezCapScreen is already running. Exit it first.");
            return 2;
        }

        savedHotkey = Registry.GetValue(AppKey, "Hotkey", null);
        savedRun = RunEntry();
        savedLanguage = Registry.GetValue(AppKey, "Language", null);
        SetLanguageEntry(null);
        SetHotkeyEntry(null);
        SetRunEntry(null);

        // A topmost full-screen window must never outlive a hung run.
        new Thread(() =>
        {
            Thread.Sleep(300000);
            Console.WriteLine("FAIL watchdog: run took over 300 seconds");
            foreach (var p in Process.GetProcessesByName("ezCapScreen")) p.Kill();
            RestoreUserSettings();
            Environment.Exit(3);
        }) { IsBackground = true }.Start();

        virt = SystemInformation.VirtualScreen;
        // Every test region sits on the left-most monitor, so its scale is the one the toolbar uses.
        var leftMonitor = Screen.AllScreens.OrderBy(m => m.Bounds.Left).First();
        uiScale = ScaleOfScreen(leftMonitor);
        ToolbarW = Px(318);
        Gap = Px(8);
        Row = Px(40);
        Console.WriteLine("virtual screen " + virt + ", left monitor scale " + uiScale);
        patternImage = MakePattern(virt);
        pattern = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Bounds = virt, TopMost = true, ShowInTaskbar = false, BackgroundImage = patternImage, BackgroundImageLayout = ImageLayout.None };
        pattern.Show();
        pattern.Activate();
        Pump(400);

        // Another app may own the default hotkey. Test with a free one in that case, and leave a held key alone.
        char key = Held('A') ? 'Q' : 'A';
        if (key == 'Q') SetHotkeyEntry((int)(Keys.Control | Keys.Alt | Keys.Q));
        Console.WriteLine("Testing with Ctrl+Alt+" + key);
        // The app starts with its hotkey taken, then gets it from the retry timer once it is free.
        if (want("binding")) Check(RegisterHotKey(IntPtr.Zero, 0x7001, MOD_CONTROL | MOD_ALT, key), "harness can hold the hotkey before launch");
        var app = Process.Start(exe);
        Pump(1500);
        IntPtr w = IntPtr.Zero, d = IntPtr.Zero;
        var bar = Point.Empty;

        byte vk = (byte)key;
        try
        {
            if (!want("binding"))
            {
                Check(Wait(() => Held(key), 3000), "app holds its hotkey");
            }
            else
            {
            var runner = Process.Start(exe);
            Check(runner.WaitForExit(4000) && !app.HasExited, "second launch exits, first instance keeps running");

            TrayMessage(app, WM_LBUTTONDOWN, WM_LBUTTONUP);
            IntPtr fromTray = IntPtr.Zero;
            bool trayOpened = Wait(() => (fromTray = Overlay()) != IntPtr.Zero, 3000);
            Check(trayOpened, "with the hotkey taken, a tray click still opens the overlay");
            if (!trayOpened) throw new InvalidOperationException("tray click did not open the overlay");
            DragMouse(fromTray, 400, 100, 800, 300);
            ToolbarClick(fromTray, new Point(800 - ToolbarW, 300 + Gap), 7);
            Check(Closed(fromTray), "Cancel closes the tray-opened overlay");

            Check(!Held('S'), "no Ctrl+Alt+S fallback grabbed while the hotkey is taken");
            UnregisterHotKey(IntPtr.Zero, 0x7001);
            Check(Wait(() => Held(key), 7000), "app binds its hotkey on retry once it is free");

            TrayMenu(app, VK_DOWN, VK_RETURN);
            IntPtr fromMenu = IntPtr.Zero;
            Check(Wait(() => (fromMenu = Overlay()) != IntPtr.Zero, 3000), "tray menu capture item opens the overlay");
            if (fromMenu == IntPtr.Zero) throw new InvalidOperationException("tray menu capture item failed");
            DragMouse(fromMenu, 400, 100, 800, 300);
            ToolbarClick(fromMenu, new Point(800 - ToolbarW, 300 + Gap), 7);
            Check(Closed(fromMenu), "Cancel closes the menu-opened overlay");
            }

            if (want("capture"))
            {
            // Region, every tool, palette, width, undo, then Done to the clipboard.
            w = OpenOverlay(vk);
            Check(GetForegroundWindow() == w, "overlay takes the foreground");
            Shot("1-frozen-dim");
            PressHotkey(vk);
            Pump(300);
            Check(Overlay() == w, "hotkey during capture keeps the same overlay");

            DragMouse(w, 200, 150, 600, 450);
            bar = new Point(600 - ToolbarW, 450 + Gap);
            Shot("2-region-selected");

            ToolbarClick(w, bar, 0);
            ToolbarClick(w, bar, 2, 1);
            PaletteClick(w, bar, 0, 1);
            DragMouse(w, 240, 190, 340, 260);
            ToolbarClick(w, bar, 1);
            ToolbarClick(w, bar, 1, 1);
            PaletteClick(w, bar, 5, 2);
            DragMouse(w, 360, 190, 460, 260);
            ToolbarClick(w, bar, 2);
            PaletteClick(w, bar, 7, 2);
            DragMouse(w, 240, 300, 340, 400);
            ToolbarClick(w, bar, 3);
            PaletteClick(w, bar, 9, 2);
            var swatch = ScreenPixel(bar.X + Px(159), bar.Y + Px(59));
            Check(SameColor(swatch, Color.FromArgb(0x8F, 0x3D, 0xD7)), "current-color box shows the picked color, got " + swatch);
            DragMouse(w, 380, 300, 560, 400);
            Shot("3-annotated");
            ToolbarClick(w, bar, 0);
            PaletteClick(w, bar, 12, 2);
            DragMouse(w, 500, 170, 580, 210);
            Shot("4-before-undo");
            ToolbarClick(w, bar, 5);
            Shot("5-after-undo");
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "Done closes the overlay");
            Pump(200);

            using (var image = ClipboardImage())
            {
                Check(image != null, "clipboard holds an image after Done");
                if (image != null)
                {
                    image.Save(Path.Combine(outDir, "clipboard.png"), ImageFormat.Png);
                    Check(image.Width == 400 && image.Height == 300, "clipboard image is 400x300, got " + image.Width + "x" + image.Height);
                    Check(SameColor(image.GetPixel(40, 75), Color.FromArgb(0xFF, 0x22, 0x22), 2), "rectangle stroke uses the palette red, got " + image.GetPixel(40, 75));
                    Check(SameColor(image.GetPixel(160, 75), Color.FromArgb(0x00, 0xBC, 0x00), 8), "oval stroke uses the palette green, got " + image.GetPixel(160, 75));
                    Check(SameColor(image.GetPixel(210, 75), Expected(410, 225)), "inside of the oval stays unfilled");
                    Check(SameColor(image.GetPixel(10, 10), Expected(210, 160)), "untouched pixel equals the frozen screen");
                    Check(SameColor(image.GetPixel(300, 20), Expected(500, 170)) && SameColor(image.GetPixel(340, 40), Expected(540, 190)), "undone rectangle is gone");
                    Check(SameColor(image.GetPixel(399, 299), Expected(599, 449)), "last pixel equals the frozen screen");
                }
            }

            // A white frame follows the region during the drag and after it, and stays out of the crop.
            w = OpenOverlay(vk);
            // This drag waits for the screen to update, so the real cursor must sit where the drag ends.
            var cursorHome = Cursor.Position;
            SetCursorPos(1000 + virt.X, 500 + virt.Y);
            Pump(60);
            Move(w, 700, 200);
            SendMessage(w, WM_LBUTTONDOWN, new IntPtr(1), LParam(700, 200));
            Move(w, 1000, 500);
            Check(SameColor(ScreenPixel(699, 260), Color.White) && SameColor(ScreenPixel(800, 199), Color.White), "white frame on left and top edges during the drag");
            Check(SameColor(ScreenPixel(1000, 260), Color.White) && SameColor(ScreenPixel(800, 500), Color.White), "white frame on right and bottom edges during the drag");
            Shot("2b-white-frame-during-drag");
            SendMessage(w, WM_LBUTTONUP, IntPtr.Zero, LParam(1000, 500));
            Check(SameColor(ScreenPixel(699, 260), Color.White), "white frame stays after the drag");
            SetCursorPos(cursorHome.X, cursorHome.Y);
            ToolbarClick(w, new Point(1000 - ToolbarW, 500 + Gap), 8);
            Check(Closed(w), "Done closes after the frame check");
            using (var image = ClipboardImage())
            {
                Check(image != null && image.Width == 300 && image.Height == 300, "frame adds nothing to the crop size");
                if (image != null) Check(MatchesPattern(image, 700, 200) && SameColor(image.GetPixel(299, 299), Expected(999, 499)), "no frame pixels in the crop");
            }

            // A stray click outside keeps everything, a drag outside replaces the selection and its annotations.
            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 600, 450);
            bar = new Point(600 - ToolbarW, 450 + Gap);
            ToolbarClick(w, bar, 0);
            ToolbarClick(w, bar, 2, 1);
            DragMouse(w, 240, 190, 340, 260);
            Tap(w, 900, 700);
            Tap(w, 900, 700, 3);
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "stray clicks left the toolbar where it was");
            using (var image = ClipboardImage())
                Check(image != null && image.Width == 400 && image.Height == 300 && SameColor(image.GetPixel(40, 75), Color.FromArgb(0xFF, 0x22, 0x22), 2), "stray clicks, even with 3px of jitter, keep the region and its annotation");

            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 600, 450);
            ToolbarClick(w, new Point(600 - ToolbarW, 450 + Gap), 0);
            DragMouse(w, 240, 190, 340, 260);
            DragMouse(w, 800, 300, 1100, 500);
            Shot("2c-reselected");
            DragMouse(w, 900, 400, 910, 410);
            ToolbarClick(w, new Point(1110 - ToolbarW, 510 + Gap), 8);
            Check(Closed(w), "toolbar follows the new region");
            using (var image = ClipboardImage())
            {
                Check(image != null && image.Width == 300 && image.Height == 200, "drag outside replaces the region, got " + (image == null ? "none" : image.Width + "x" + image.Height));
                if (image != null) Check(MatchesPattern(image, 810, 310), "new region has no old annotation, and dragging it moves it because the tool was reset");
            }

            // Text tool: Thai and Latin, live restyle, several notes, no editor chrome in the output.
            var red = Color.FromArgb(0xFF, 0x22, 0x22);
            var green = Color.FromArgb(0x00, 0xBC, 0x00);
            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 800, 550);
            bar = new Point(800 - ToolbarW, 550 + Gap);
            ToolbarClick(w, bar, 4);
            Click(w, 260, 200);
            Check(IsWindowVisible(HotkeyBox(w)), "clicking the region with the Text tool opens the text input");
            TypeInto(w, "Hello สวัสดี");
            Check(BoxText(w) == "Hello สวัสดี", "text input holds the typed Thai and Latin, got " + BoxText(w));
            Check(GetForegroundWindow() == w, "overlay is foreground before real typing");
            if (GetForegroundWindow() == w) PressKey('K');
            Pump(150);
            Check(BoxText(w).Length == "Hello สวัสดี".Length + 1, "a real key press reaches the text input, got " + BoxText(w));
            Shot("10-text-editing");
            ToolbarClick(w, bar, 2, 1);
            PaletteClick(w, bar, 5, 2);
            Check(GetForegroundWindow() == w, "overlay stays foreground after using the toolbar");
            PressKey('K');
            Pump(150);
            Check(BoxText(w).Length == "Hello สวัสดี".Length + 2, "typing continues after the toolbar was used, got " + BoxText(w));
            Shot("11-text-restyled");
            Click(w, 260, 400);
            PaletteClick(w, bar, 0, 1);
            ToolbarClick(w, bar, 0, 1);
            TypeInto(w, "line one\rบรรทัดสอง");
            Shot("12-text-two-lines");
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "Done closes while text is still being edited");
            using (var image = ClipboardImage())
            {
                Check(image != null && image.Width == 600 && image.Height == 400, "text capture is 600x400");
                if (image != null)
                {
                    image.Save(Path.Combine(outDir, "text-output.png"), ImageFormat.Png);
                    var first = new Rectangle(0, 0, 600, 200);
                    var second = new Rectangle(0, 200, 600, 200);
                    Check(CountColor(image, green, first) > 300, "restyle applied while typing: first note is green");
                    Check(CountColor(image, red, first) == 0, "first note has no old color left");
                    int firstHeight = ColorHeight(image, green, first), secondHeight = ColorHeight(image, red, second);
                    Check(CountColor(image, red, second) > 100, "text still being edited at Done reaches the image");
                    Check(firstHeight > 24, "large size applied while typing, height " + firstHeight);
                    Check(secondHeight > 30, "Enter makes a second line, height " + secondHeight);
                    Check(SameColor(image.GetPixel(57, 250), Expected(257, 400)), "no editor frame or caret in the image");
                    Check(SameColor(image.GetPixel(5, 5), Expected(205, 155)), "untouched pixel equals the frozen screen");
                }
            }

            // Esc leaves the note first and the capture second.
            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 800, 550);
            ToolbarClick(w, bar, 4);
            Click(w, 260, 200);
            TypeInto(w, "discard me");
            Check(GetForegroundWindow() == w, "overlay is foreground before Esc in text");
            if (GetForegroundWindow() == w) PressKey(VK_ESCAPE);
            Pump(200);
            Check(IsWindow(w) && !IsWindowVisible(HotkeyBox(w)), "Esc while typing drops the note and keeps the capture");
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "Done closes after the dropped note");
            using (var image = ClipboardImage())
                Check(image != null && MatchesPattern(image, 200, 150), "dropped note is not in the image");

            // Undo drops the note being typed, then the finished one. Whitespace alone leaves nothing to undo.
            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 800, 550);
            ToolbarClick(w, bar, 4);
            Click(w, 260, 200);
            TypeInto(w, "first");
            Click(w, 260, 300);
            TypeInto(w, "second");
            ToolbarClick(w, bar, 5);
            Click(w, 260, 400);
            TypeInto(w, "   ");
            Click(w, 500, 400);
            ToolbarClick(w, bar, 5);
            ToolbarClick(w, bar, 5);
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "Done closes after undoing the notes");
            using (var image = ClipboardImage())
                Check(image != null && MatchesPattern(image, 200, 150), "undo removes notes in order");

            // Each way out of typing keeps the note: another tool, a stray tap, and Save.
            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 800, 550);
            ToolbarClick(w, bar, 4);
            PaletteClick(w, bar, 0, 1);
            Click(w, 260, 200);
            TypeInto(w, "keep tool");
            ToolbarClick(w, bar, 0);
            ToolbarClick(w, bar, 4);
            Click(w, 260, 300);
            TypeInto(w, "keep tap");
            Tap(w, 1000, 800);
            ToolbarClick(w, bar, 4);
            Click(w, 260, 400);
            TypeInto(w, "keep save");
            var textPath = Path.Combine(outDir, "text-saved.png");
            File.Delete(textPath);
            SaveAs(w, bar, textPath);
            Check(Closed(w), "saving closes while text is being edited");
            using (var image = new Bitmap(textPath))
            {
                Check(CountColor(image, red, new Rectangle(0, 0, 600, 100)) > 100, "switching tool keeps the note");
                Check(CountColor(image, red, new Rectangle(0, 100, 600, 100)) > 100, "a stray tap outside keeps the note");
                Check(CountColor(image, red, new Rectangle(0, 200, 600, 200)) > 100, "Save includes the note being typed");
            }

            // The Save dialog's closing fade is still on screen for a moment and would land in the next capture.
            Pump(1500);
            // Real Ctrl+V pastes into the note, and a note can be replaced with the selection.
            Clipboard.SetText("วางข้อความ");
            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 800, 550);
            ToolbarClick(w, bar, 4);
            Click(w, 260, 200);
            Pump(100);
            Check(GetForegroundWindow() == w, "overlay is foreground before paste");
            if (GetForegroundWindow() == w)
            {
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                PressKey('V');
                keybd_event(VK_CONTROL, 0, 2, UIntPtr.Zero);
            }
            Pump(200);
            Check(BoxText(w) == "วางข้อความ", "Ctrl+V pastes into the note, got " + BoxText(w));
            DragMouse(w, 900, 200, 1200, 400);
            Check(!IsWindowVisible(HotkeyBox(w)), "dragging a new selection ends typing");
            ToolbarClick(w, new Point(1200 - ToolbarW, 400 + Gap), 8);
            Check(Closed(w), "Done closes after the replaced selection");
            using (var image = ClipboardImage())
                Check(image != null && image.Width == 300 && MatchesPattern(image, 900, 200), "replacing the selection drops the note");

            // Ctrl+Z removes the last shape. In a note it undoes typing first, then leaves the empty note.
            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 600, 450);
            bar = new Point(600 - ToolbarW, 450 + Gap);
            ToolbarClick(w, bar, 0);
            DragMouse(w, 240, 190, 340, 260);
            DragMouse(w, 360, 190, 460, 260);
            Action ctrlZ = () =>
            {
                if (GetForegroundWindow() != w) throw new InvalidOperationException("overlay is not foreground for Ctrl+Z");
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                PressKey('Z');
                keybd_event(VK_CONTROL, 0, 2, UIntPtr.Zero);
                Pump(200);
            };
            ctrlZ();
            ToolbarClick(w, bar, 4);
            Click(w, 260, 350);
            TypeInto(w, "typed");
            ctrlZ();
            Check(IsWindowVisible(HotkeyBox(w)) && BoxText(w).Length == 0, "Ctrl+Z in a note undoes the typing, got [" + BoxText(w) + "]");
            ctrlZ();
            Check(!IsWindowVisible(HotkeyBox(w)), "Ctrl+Z on an empty note leaves the note");
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "Done closes after Ctrl+Z");
            using (var image = ClipboardImage())
            {
                Check(image != null && SameColor(image.GetPixel(40, 75), Color.FromArgb(0xFF, 0x22, 0x22), 40), "Ctrl+Z keeps the earlier shape");
                Check(image != null && SameColor(image.GetPixel(160, 75), Expected(360, 225)), "Ctrl+Z removes the last shape");
                Check(image != null && SameColor(image.GetPixel(60, 200), Expected(260, 350)), "the undone note is not in the image");
            }

            // Ctrl+Y brings back what Ctrl+Z removed, inside a note and for shapes. New work clears what can be redone.
            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 600, 450);
            bar = new Point(600 - ToolbarW, 450 + Gap);
            ToolbarClick(w, bar, 0);
            DragMouse(w, 240, 190, 340, 260);
            DragMouse(w, 360, 190, 460, 260);
            Action<char> ctrlKey = letter =>
            {
                if (GetForegroundWindow() != w) throw new InvalidOperationException("overlay is not foreground for Ctrl+" + letter);
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                PressKey(letter);
                keybd_event(VK_CONTROL, 0, 2, UIntPtr.Zero);
                Pump(200);
            };
            ctrlKey('Z');
            ctrlKey('Y');
            ToolbarClick(w, bar, 4);
            Click(w, 260, 350);
            TypeInto(w, "typed");
            ctrlKey('Z');
            Check(BoxText(w).Length == 0, "Ctrl+Z empties the note");
            ctrlKey('Y');
            Check(BoxText(w) == "typed", "Ctrl+Y restores the typing, got [" + BoxText(w) + "]");
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "Done closes after redo");
            using (var image = ClipboardImage())
            {
                Check(image != null && SameColor(image.GetPixel(160, 75), Color.FromArgb(0xFF, 0x22, 0x22), 40), "Ctrl+Y brings the removed shape back");
                Check(image != null && CountColor(image, Color.FromArgb(0xFF, 0x22, 0x22), new Rectangle(55, 195, 200, 60)) > 20, "the restored note is in the image");
            }

            w = OpenOverlay(vk);
            DragMouse(w, 200, 150, 600, 450);
            ToolbarClick(w, bar, 0);
            DragMouse(w, 240, 190, 340, 260);
            ctrlKey('Z');
            DragMouse(w, 360, 190, 460, 260);
            ctrlKey('Y');
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "Done closes after the cleared redo");
            using (var image = ClipboardImage())
                Check(image != null && SameColor(image.GetPixel(40, 75), Expected(240, 225)), "a new shape clears what Ctrl+Y could bring back");

            // The pen must be usable before anyone touches the palette or the width buttons.
            w = OpenOverlay(vk);
            DragMouse(w, 100, 100, 300, 300);
            bar = new Point(300 - ToolbarW < 0 ? 0 : 300 - ToolbarW, 300 + Gap);
            ToolbarClick(w, bar, 0);
            DragMouse(w, 150, 150, 250, 250);
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "Done closes after drawing with defaults");
            using (var image = ClipboardImage())
                Check(image != null && SameColor(image.GetPixel(50, 100), Color.FromArgb(0xFF, 0x22, 0x22), 40), "default pen draws visibly in red, got " + (image == null ? "no image" : image.GetPixel(50, 100).ToString()));

            // Move and resize change what gets cropped, with no annotation on it.
            w = OpenOverlay(vk);
            DragMouse(w, 100, 100, 300, 300);
            DragMouse(w, 200, 200, 400, 250);
            DragMouse(w, 500, 350, 560, 400);
            bar = new Point(560 - ToolbarW, 400 + Gap);
            ToolbarClick(w, bar, 8);
            Check(Closed(w), "Done closes after move and resize");
            using (var image = ClipboardImage())
            {
                Check(image != null && image.Width == 260 && image.Height == 250, "move then corner resize gives 260x250");
                if (image != null) Check(MatchesPattern(image, 300, 150), "moved crop is pixel-exact");
            }

            // Region across the seam between two monitors, saved in all three formats. Cancel keeps the overlay open.
            var seam = Screen.AllScreens.Length > 1 ? leftMonitor.Bounds.Right - virt.X : 1000;
            foreach (var extension in new[] { "png", "jpg", "bmp" })
            {
                var path = Path.Combine(outDir, "saved." + extension);
                File.Delete(path);
                // The previous Save dialog's closing fade would land in this capture.
                Pump(1500);
                w = OpenOverlay(vk);
                // Mostly on the left monitor, so the toolbar stays there, flush with the seam.
                DragMouse(w, seam - 150, 200, seam + 50, 400);
                bar = new Point(seam - ToolbarW, 400 + Gap);
                if (extension == "png")
                {
                    var thread = new Thread(() => ToolbarClick(w, bar, 6));
                    thread.Start();
                    var dialog = WaitSaveDialog();
                    if (dialog == IntPtr.Zero) throw new InvalidOperationException("save dialog did not open");
                    Pump(300);
                    SendMessage(GetDlgItem(dialog, 2), BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    thread.Join(3000);
                    Check(IsWindow(w), "cancelling the save dialog keeps the overlay");
                }
                SaveAs(w, bar, path);
                Check(Closed(w), "saving closes the overlay (" + extension + ")");
                Check(File.Exists(path), "file written: saved." + extension);
                if (!File.Exists(path)) continue;
                var head = File.ReadAllBytes(path).Take(2).ToArray();
                var magic = extension == "png" ? new byte[] { 0x89, 0x50 } : extension == "jpg" ? new byte[] { 0xFF, 0xD8 } : new byte[] { 0x42, 0x4D };
                Check(head.SequenceEqual(magic), "saved." + extension + " really is that format");
                using (var image = new Bitmap(path))
                {
                    Check(image.Width == 200 && image.Height == 200, "saved image is 200x200 across the monitor seam");
                    if (extension != "jpg") Check(MatchesPattern(image, seam - 150, 200), "saved." + extension + " is pixel-exact across the seam");
                }
            }

            // Esc and Cancel leave the clipboard alone.
            Clipboard.SetText("verify-marker");
            w = OpenOverlay(vk);
            DragMouse(w, 100, 100, 300, 300);
            Check(GetForegroundWindow() == w, "overlay is foreground before the real Esc key");
            if (GetForegroundWindow() == w)
            {
                keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
                keybd_event(VK_ESCAPE, 0, 2, UIntPtr.Zero);
            }
            Check(Closed(w), "Esc closes the overlay");
            Pump(200);
            Check(Clipboard.ContainsText() && Clipboard.GetText() == "verify-marker", "Esc leaves the clipboard unchanged");

            // Toolbar flips above a region at the bottom of a monitor and moves when the second row appears.
            int floor = leftMonitor.Bounds.Bottom - virt.Y;
            w = OpenOverlay(vk);
            DragMouse(w, 300, floor - 300, 700, floor - 10);
            bar = new Point(700 - ToolbarW, floor - 300 - Gap - Row);
            Shot("6-toolbar-flipped");
            ToolbarClick(w, bar, 3);
            bar = new Point(700 - ToolbarW, floor - 300 - Gap - Row * 2);
            Shot("7-toolbar-flipped-tool");
            ToolbarClick(w, bar, 7);
            Check(Closed(w), "Cancel button works at the flipped toolbar position");
            Check(Clipboard.ContainsText() && Clipboard.GetText() == "verify-marker", "Cancel leaves the clipboard unchanged");

            // On a second monitor with a different scale, the toolbar follows that monitor's scale.
            var rightMonitor = Screen.AllScreens.OrderBy(m => m.Bounds.Left).Last();
            if (ScaleOfScreen(rightMonitor) != uiScale)
            {
                float savedScale = uiScale;
                int savedW = ToolbarW, savedGap = Gap;
                uiScale = ScaleOfScreen(rightMonitor);
                ToolbarW = Px(318);
                Gap = Px(8);
                int left = rightMonitor.Bounds.Left - virt.X;
                w = OpenOverlay(vk);
                DragMouse(w, left + 200, 150, left + 800, 500);
                Shot("6b-other-monitor-toolbar");
                ToolbarClick(w, new Point(left + 800 - ToolbarW, 500 + Gap), 7);
                Check(Closed(w), "Cancel hits the toolbar at the other monitor's scale");
                uiScale = savedScale;
                ToolbarW = savedW;
                Gap = savedGap;
            }

            // A click with no drag makes no region, so no toolbar and nothing to hit where it would be.
            w = OpenOverlay(vk);
            Tap(w, 500, 500);
            Tap(w, 500 - ToolbarW + Px(6 + 7 * 35 + 13), 500 + Gap + Px(20));
            Check(IsWindow(w), "a click with no drag makes no region");
            Shot("8-no-region");
            // Right button does nothing, Esc is the way out.
            SendMessage(w, WM_RBUTTONDOWN, new IntPtr(2), LParam(500, 500));
            SendMessage(w, WM_RBUTTONUP, IntPtr.Zero, LParam(500, 500));
            Check(IsWindow(w), "right click does not close the overlay");

            // Paint cost per mouse move while dragging a large region.
            SendMessage(w, WM_LBUTTONDOWN, new IntPtr(1), LParam(100, 100));
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 60; i++)
            {
                Move(w, 1500 + i, 900);
                UpdateWindow(w);
            }
            watch.Stop();
            Console.WriteLine("MEASURE paint with drag: " + (watch.ElapsedMilliseconds / 60.0).ToString("0.0") + " ms per frame over 60 frames, " + virt.Width + "x" + virt.Height);
            SendMessage(w, WM_LBUTTONUP, IntPtr.Zero, LParam(1559, 900));
            ToolbarClick(w, new Point(1559 - ToolbarW, 900 + Gap), 7);
            Check(Closed(w), "Cancel closes after the paint run");

            // Twenty captures must not leak GDI objects or memory.
            Pump(500);
            app.Refresh();
            // The first batch settles caches and waits on the garbage collector. A leak keeps growing in the second.
            Action capture20 = () =>
            {
                for (int i = 0; i < 20; i++)
                {
                    PressHotkey(vk);
                    IntPtr o = IntPtr.Zero;
                    Wait(() => (o = Overlay()) != IntPtr.Zero, 3000);
                    DragMouse(o, 10, 10, 200, 200);
                    ToolbarClick(o, new Point(200 - ToolbarW < 0 ? 0 : 200 - ToolbarW, 200 + Gap), 7);
                    Closed(o);
                }
                Pump(500);
            };
            uint gdiStart = GetGuiResources(app.Handle, 0), userStart = GetGuiResources(app.Handle, 1);
            capture20();
            uint gdiBefore = GetGuiResources(app.Handle, 0), userBefore = GetGuiResources(app.Handle, 1);
            capture20();
            capture20();
            uint gdiAfter = GetGuiResources(app.Handle, 0), userAfter = GetGuiResources(app.Handle, 1);
            app.Refresh();
            Console.WriteLine("MEASURE captures: GDI " + gdiStart + " then " + gdiBefore + " after 20 then " + gdiAfter + " after 60, USER " + userStart + " then " + userBefore + " then " + userAfter + ", private " + app.PrivateMemorySize64 / (1024 * 1024) + " MB");
            Check(gdiAfter <= gdiBefore + 5 && userAfter <= userBefore + 5, "handles stop growing across 40 more captures");

            }

            if (want("settings"))
            {
            // Language: English by default, switches live, saved at once, survives a restart even after Cancel.
            d = OpenSettings(app);
            Check(FindWindowEx(d, IntPtr.Zero, null, AutoStartText) != IntPtr.Zero, "default language is English");
            Shot("8a-settings-english");
            ClickChild(d, "TH");
            Check(Wait(() => FindWindow(null, ThaiTitle) == d, 2000), "TH switches the window title at once");
            Check(FindWindowEx(d, IntPtr.Zero, null, ThaiAutoStart) != IntPtr.Zero && FindWindowEx(d, IntPtr.Zero, null, ThaiCancel) != IntPtr.Zero, "TH switches the controls at once");
            Check(Equals(Registry.GetValue(AppKey, "Language", null), "th"), "language is saved at once");
            Listen(app, d);
            Check(BoxText(d) == ThaiPrompt, "the prompt follows the language, got " + BoxText(d));
            Shot("8b-settings-thai-listening");
            PressKey(VK_ESCAPE);
            Pump(150);
            CloseWith(d, ThaiCancel, "Thai Cancel closes settings");
            QuitApp(app);
            Check(app.WaitForExit(4000), "app quits to check the language is remembered");
            app = Process.Start(exe);
            Check(Wait(() => Held(key), 3000), "app restarts for the language check");
            w = OpenOverlay(vk);
            DragMouse(w, 400, 100, 800, 300);
            ToolbarClick(w, new Point(800 - ToolbarW, 300 + Gap), 7);
            Check(Closed(w), "Cancel closes the overlay after the restart");
            Pump(200);
            settingsTitle = ThaiTitle;
            d = OpenSettings(app);
            Check(FindWindow(null, ThaiTitle) == d, "Thai is remembered across a restart");
            ClickChild(d, "EN");
            Check(Wait(() => FindWindow(null, SettingsTitle) == d, 2000), "EN switches back at once");
            settingsTitle = SettingsTitle;
            Check(Equals(Registry.GetValue(AppKey, "Language", null), "en"), "English is saved");
            CloseWith(d, CancelText, "Cancel closes settings after switching back");
            Pump(200);

            // The box waits for a click, then shows a prompt instead of the hotkey until it gets a key.
            d = OpenSettings(app);
            Check(!Held(key), "settings suspends the hotkey so it can be typed into the box");
            Check(BoxText(d) == "Ctrl+Alt+" + key, "box shows the current hotkey, got " + BoxText(d));
            RequireForeground(app, "keys before the click");
            PressHotkey((byte)'K');
            Check(BoxText(d) == "Ctrl+Alt+" + key, "keys do nothing before the box is clicked");
            Shot("9-settings");
            Listen(app, d);
            Check(BoxText(d) == PromptText, "clicking the box replaces the hotkey with the prompt, got " + BoxText(d));
            Shot("9a-settings-listening");
            PressKey('B');
            Check(BoxText(d) == PromptText, "a bare letter is refused and the box keeps listening");
            Shot("9b-settings-refused");
            PressKey(VK_ESCAPE);
            Check(BoxText(d) == "Ctrl+Alt+" + key && IsWindow(d), "Esc stops listening without closing settings");
            PressKey(VK_SPACE);
            Check(BoxText(d) == PromptText, "Space starts listening from the keyboard");
            PressKey(VK_TAB);
            Check(BoxText(d) == "Ctrl+Alt+" + key, "leaving the box stops listening and restores the hotkey");
            Listen(app, d);
            PressHotkey((byte)'K');
            Check(BoxText(d) == "Ctrl+Alt+K", "box takes a new chord, got " + BoxText(d));
            object hotkeyBeforeCancel = Registry.GetValue(AppKey, "Hotkey", null);
            CloseWith(d, CancelText, "Cancel closes settings");
            Check(Wait(() => Held(key), 2000) && !Held('K'), "Cancel restores the old hotkey");
            Check(Equals(Registry.GetValue(AppKey, "Hotkey", null), hotkeyBeforeCancel), "Cancel writes nothing");

            // A key another app takes between typing and OK is refused and settings stays open.
            d = OpenSettings(app);
            Listen(app, d);
            PressHotkey((byte)'J');
            Check(BoxText(d) == "Ctrl+Alt+J", "box takes Ctrl+Alt+J");
            Check(RegisterHotKey(IntPtr.Zero, 0x7005, MOD_CONTROL | MOD_ALT, 'J'), "harness takes Ctrl+Alt+J");
            ClickChild(d, AcceptText);
            IntPtr warning = IntPtr.Zero;
            Check(Wait(() => (warning = FindWindow("#32770", "ezCapScreen")) != IntPtr.Zero, 3000), "a taken hotkey is reported");
            Shot("9c-settings-conflict");
            PostMessage(FindWindowEx(warning, IntPtr.Zero, "Button", null), BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Check(Wait(() => !IsWindow(warning), 2000) && IsWindow(d), "settings stays open after the report");
            UnregisterHotKey(IntPtr.Zero, 0x7005);

            Pump(200);
            Listen(app, d);
            PressHotkey((byte)'K');
            Check(BoxText(d) == "Ctrl+Alt+K", "box takes the replacement chord");
            CloseWith(d, AcceptText, "OK closes settings");
            Check(Wait(() => Held('K'), 2000) && !Held(key), "new hotkey is bound and the old one released");
            Check(Equals(Registry.GetValue(AppKey, "Hotkey", null), (int)(Keys.Control | Keys.Alt | Keys.K)), "new hotkey is saved");
            vk = (byte)'K';
            w = OpenOverlay(vk);
            DragMouse(w, 400, 100, 800, 300);
            ToolbarClick(w, new Point(800 - ToolbarW, 300 + Gap), 7);
            Check(Closed(w), "new hotkey opens the overlay");

            // A function key may stand alone.
            d = OpenSettings(app);
            Listen(app, d);
            PressKey(VK_F9);
            Check(BoxText(d) == "F9", "box takes bare F9, got " + BoxText(d));
            CloseWith(d, AcceptText, "OK closes settings for F9");
            Pump(200);
            PressKey(VK_F9);
            Check(Wait(() => Overlay() != IntPtr.Zero, 3000), "F9 opens the overlay");
            w = Overlay();
            DragMouse(w, 400, 100, 800, 300);
            ToolbarClick(w, new Point(800 - ToolbarW, 300 + Gap), 7);
            Check(Closed(w), "Cancel closes the F9 overlay");

            // Dragged to a monitor with a different scale, the window must resize itself to stay sharp and in proportion.
            var otherMonitor = Screen.AllScreens.FirstOrDefault(m => ScaleOfScreen(m) != ScaleOfScreen(Screen.PrimaryScreen));
            if (otherMonitor != null)
            {
                d = OpenSettings(app);
                RECT before, after;
                GetWindowRect(d, out before);
                SetWindowPos(d, IntPtr.Zero, otherMonitor.Bounds.X + 100, otherMonitor.Bounds.Y + 100, 0, 0, 0x0001 | 0x0004);
                Pump(600);
                GetWindowRect(d, out after);
                Console.WriteLine("MEASURE settings window width " + (before.R - before.L) + " to " + (after.R - after.L) + " on a monitor at scale " + ScaleOfScreen(otherMonitor));
                Check((after.R - after.L) != (before.R - before.L), "settings resizes on a monitor with a different scale");
                using (var b = new Bitmap(after.R - after.L, after.B - after.T))
                using (var g = Graphics.FromImage(b))
                {
                    g.CopyFromScreen(after.L, after.T, 0, 0, b.Size);
                    b.Save(Path.Combine(outDir, "9f-settings-other-monitor.png"), ImageFormat.Png);
                }
                CloseWith(d, CancelText, "Cancel closes settings on the other monitor");
                Pump(200);
            }

            // Back to Ctrl+Alt+K for the rest of the run, with autostart on.
            d = OpenSettings(app);
            Listen(app, d);
            PressHotkey((byte)'K');
            Check(BoxText(d) == "Ctrl+Alt+K", "box takes Ctrl+Alt+K again");
            ClickChild(d, AutoStartText);
            Pump(150);
            CloseWith(d, AcceptText, "OK closes settings with autostart on");
            var expectedRun = "\"" + exe + "\" --autostart";
            Check(Equals(RunEntry(), expectedRun), "Run entry is the quoted exe path, got " + RunEntry());

            // What Windows will do at login: run that exact command. The hotkey must survive the restart.
            QuitApp(app);
            Check(app.WaitForExit(4000), "app quits for the restart");
            Check(!Held('K'), "hotkey released on quit");
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + RunEntry() + "\"") { UseShellExecute = false, CreateNoWindow = true });
            Check(Wait(() => Process.GetProcessesByName("ezCapScreen").Length > 0 && Held('K'), 5000), "the Run command starts the app with the saved hotkey");
            app = Process.GetProcessesByName("ezCapScreen")[0];

            // Autostart is written only when the user changes it. Starting the program must leave the entry alone.
            QuitApp(app);
            Check(app.WaitForExit(4000), "app quits before the unchanged-entry check");
            SetRunEntry("\"C:\\old\\place\\ezCapScreen.exe\" --autostart");
            app = Process.Start(exe);
            Pump(1500);
            Check(Equals(RunEntry(), "\"C:\\old\\place\\ezCapScreen.exe\" --autostart"), "starting the program does not rewrite the autostart entry");

            // The quit took the foreground with it. A hotkey capture hands it back, as a real tray click would.
            Check(Wait(() => Held('K'), 3000), "restarted app holds the saved hotkey");
            w = OpenOverlay(vk);
            DragMouse(w, 400, 100, 800, 300);
            ToolbarClick(w, new Point(800 - ToolbarW, 300 + Gap), 7);
            Check(Closed(w), "Cancel closes after the restart");
            Pump(200);

            d = OpenSettings(app);
            ClickChild(d, AutoStartText);
            Pump(150);
            CloseWith(d, AcceptText, "OK closes settings with autostart off");
            Check(RunEntry() == null, "one click turned autostart off, so settings had shown it ticked, and the Run entry is gone");

            // A damaged saved value falls back to the default instead of binding nonsense.
            QuitApp(app);
            Check(app.WaitForExit(4000), "app quits before the damaged-setting check");
            if (key == 'A')
            {
                SetHotkeyEntry(0x41);
                app = Process.Start(exe);
                Check(Wait(() => Held('A'), 3000), "an invalid saved hotkey falls back to Ctrl+Alt+A");
                QuitApp(app);
                Check(app.WaitForExit(4000), "app quits after the damaged-setting check");
            }
            SetHotkeyEntry((int)(Keys.Control | Keys.Alt | Keys.K));
            app = Process.Start(exe);
            Check(Wait(() => Held('K'), 3000), "app restarts for the exit check");

            // The menu's Exit, and the Done image must outlive the process that made it.
            w = OpenOverlay(vk);
            DragMouse(w, 50, 50, 150, 120);
            ToolbarClick(w, new Point(0, 120 + Gap), 8);
            Check(Closed(w), "Done closes before exit");
            Pump(200);
            TrayMenu(app, VK_UP, VK_RETURN);
            Check(app.WaitForExit(4000), "tray menu Exit quits the app");
            using (var image = ClipboardImage())
                Check(image != null && image.Width == 100 && image.Height == 70, "clipboard image survives the app exiting");
            bool freed = RegisterHotKey(IntPtr.Zero, 0x7004, MOD_CONTROL | MOD_ALT, vk);
            if (freed) UnregisterHotKey(IntPtr.Zero, 0x7004);
            Check(freed, "hotkey is released after exit");
            }
        }
        catch (InvalidOperationException ex)
        {
            Check(false, ex.Message);
        }
        finally
        {
            foreach (var p in Process.GetProcessesByName("ezCapScreen")) p.Kill();
            RestoreUserSettings();
            pattern.Close();
        }

        Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
