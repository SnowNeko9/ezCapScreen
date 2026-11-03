using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

static class Ui
{
    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);

    [DllImport("shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr window);

    public static float ScaleAt(Rectangle screenRect)
    {
        const uint NearestMonitor = 2;
        var rect = new NativeRect { Left = screenRect.Left, Top = screenRect.Top, Right = screenRect.Right, Bottom = screenRect.Bottom };
        uint x, y;
        return GetDpiForMonitor(MonitorFromRect(ref rect, NearestMonitor), 0, out x, out y) == 0 ? x / 96f : 1f;
    }

    public static float ScaleOf(IntPtr window) { return GetDpiForWindow(window) / 96f; }

    public static int Px(int value, float scale) { return (int)Math.Round(value * scale); }
}

// Follows the Windows app theme, read each time the settings window opens.
static class Theme
{
    public static bool IsLight;
    public static Color Page, PanelTop, PanelBottom, Field, FieldListening, ListeningText, Line;
    public static Color Accent, AccentHover, AccentDown, OnAccent, Text, Muted, Error, Hover, Down;

    static Theme() { Load(); }

    static Color Pick(int dark, int light) { return Color.FromArgb(255, Color.FromArgb(IsLight ? light : dark)); }

    public static void Load()
    {
        const string Personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        var stored = Registry.GetValue(Personalize, "AppsUseLightTheme", 1);
        IsLight = !(stored is int) || (int)stored != 0;
        Page = Pick(0x202024, 0xF9F9FB);
        PanelTop = Pick(0x263A5C, 0xDCEBFD);
        PanelBottom = Pick(0x161E30, 0xBCD8F8);
        Field = Pick(0x2C2C32, 0xFFFFFF);
        FieldListening = Pick(0x183458, 0xDCEBFD);
        ListeningText = Pick(0x66ACF5, 0x14508C);
        Line = Pick(0x484850, 0xC6CAD2);
        Accent = Pick(0x4A9BEF, 0x1F6FBF);
        AccentHover = Pick(0x66ACF5, 0x2C80D4);
        AccentDown = Pick(0x3882D2, 0x185C9F);
        OnAccent = Pick(0x0C182C, 0xFFFFFF);
        Text = Pick(0xF2F2F5, 0x1C1C20);
        Muted = Pick(0xA5A8B0, 0x5C6068);
        Error = Pick(0xFF8A80, 0xC62828);
        Hover = Pick(0x3A3A42, 0xEEF0F3);
        Down = Pick(0x26262C, 0xE1E4E9);
    }

    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Rectangle r, int radius, Color fill, Color? outline)
    {
        using (var path = Rounded(r, radius))
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
            if (!outline.HasValue) return;
            using (var pen = new Pen(outline.Value))
                g.DrawPath(pen, path);
        }
    }
}
