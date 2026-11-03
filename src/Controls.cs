using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Real Windows controls underneath, so keyboard use and focus keep working. Only the painting is ours.
sealed class FlatButton : Button
{
    readonly bool primary;
    bool hover, down;

    public FlatButton(bool primary)
    {
        this.primary = primary;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Page);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var box = new Rectangle(1, 1, Width - 3, Height - 3);
        Color fill = primary
            ? (down ? Theme.AccentDown : hover ? Theme.AccentHover : Theme.Accent)
            : (down ? Theme.Down : hover ? Theme.Hover : Theme.Field);
        Theme.FillRounded(g, box, 6, fill, primary ? (Color?)null : Theme.Line);
        if (Focused && ShowFocusCues)
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 7))
            using (var ring = new Pen(Theme.Text))
                g.DrawPath(ring, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, primary ? Theme.OnAccent : Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// The Text stays set though it is not painted, so screen readers still announce the language.
sealed class FlagButton : Button
{
    const int FlagWidth = 24, FlagHeight = 16;
    static readonly ImageAttributes Dimmed = MakeDimmed();

    readonly Bitmap flag;
    bool selected, hover;

    public bool Selected
    {
        set
        {
            selected = value;
            Invalidate();
        }
    }

    public FlagButton(Bitmap flag)
    {
        this.flag = flag;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }

    static ImageAttributes MakeDimmed()
    {
        var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 0.45f });
        return attributes;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Page);
        float scale = Ui.ScaleOf(Handle);
        int flagWidth = Ui.Px(FlagWidth, scale), flagHeight = Ui.Px(FlagHeight, scale);
        var box = new Rectangle((Width - flagWidth) / 2, (Height - flagHeight) / 2, flagWidth, flagHeight);
        if (selected || hover) g.DrawImage(flag, box);
        else g.DrawImage(flag, box, 0, 0, flag.Width, flag.Height, GraphicsUnit.Pixel, Dimmed);
        if (!selected && !(Focused && ShowFocusCues)) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        box.Inflate(Ui.Px(3, scale), Ui.Px(3, scale));
        using (var path = Theme.Rounded(box, Ui.Px(3, scale)))
        using (var ring = new Pen(selected ? Theme.Accent : Theme.Text, 2f * scale))
            g.DrawPath(ring, path);
    }
}

// Drawn in code at twice the size shown, so they stay clean if a high-DPI screen scales them up.
static class Flags
{
    const int W = 48, H = 32;
    static readonly Color Blue = Color.FromArgb(1, 33, 105);
    static readonly Color Red = Color.FromArgb(200, 16, 46);
    static readonly Color ThaiRed = Color.FromArgb(165, 25, 49);
    static readonly Color ThaiBlue = Color.FromArgb(45, 42, 74);

    static Bitmap Make(Action<Graphics> draw)
    {
        var image = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(image))
        using (var clip = Theme.Rounded(new Rectangle(0, 0, W - 1, H - 1), 4))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SetClip(clip);
            draw(g);
        }
        return image;
    }

    public static readonly Bitmap English = Make(g =>
    {
        g.Clear(Blue);
        using (var white = new Pen(Color.White, 7f))
        using (var red = new Pen(Red, 3f))
        using (var cross = new SolidBrush(Red))
        {
            g.DrawLine(white, 0, 0, W, H);
            g.DrawLine(white, W, 0, 0, H);
            g.DrawLine(red, 0, 0, W, H);
            g.DrawLine(red, W, 0, 0, H);
            g.FillRectangle(Brushes.White, W / 2 - 5, 0, 10, H);
            g.FillRectangle(Brushes.White, 0, H / 2 - 5, W, 10);
            g.FillRectangle(cross, W / 2 - 3, 0, 6, H);
            g.FillRectangle(cross, 0, H / 2 - 3, W, 6);
        }
    });

    public static readonly Bitmap Thai = Make(g =>
    {
        using (var red = new SolidBrush(ThaiRed))
        using (var blue = new SolidBrush(ThaiBlue))
        {
            g.FillRectangle(red, 0, 0, W, H);
            g.FillRectangle(Brushes.White, 0, 5, W, 22);
            g.FillRectangle(blue, 0, 11, W, 10);
        }
    });
}

sealed class ToggleSwitch : CheckBox
{
    const int TrackWidth = 40, TrackHeight = 20, TextGap = 12;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Page);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = Ui.ScaleOf(Handle);
        int trackWidth = Ui.Px(TrackWidth, scale), trackHeight = Ui.Px(TrackHeight, scale), inset = Ui.Px(4, scale);
        var track = new Rectangle(1, (Height - trackHeight) / 2, trackWidth, trackHeight);
        Theme.FillRounded(g, track, trackHeight / 2, Checked ? Theme.Accent : Theme.Field, Checked ? (Color?)null : Theme.Muted);
        int knob = trackHeight - inset * 2;
        int knobX = Checked ? track.Right - knob - inset : track.X + inset;
        using (var fill = new SolidBrush(Checked ? Theme.OnAccent : Theme.Muted))
            g.FillEllipse(fill, knobX, track.Y + inset, knob, knob);
        if (Focused && ShowFocusCues)
        {
            var ring = track;
            ring.Inflate(1, 1);
            using (var path = Theme.Rounded(ring, ring.Height / 2))
            using (var pen = new Pen(Theme.Text))
                g.DrawPath(pen, path);
        }
        int labelLeft = trackWidth + Ui.Px(TextGap, scale);
        var label = new Rectangle(labelLeft, 0, Width - labelLeft, Height);
        TextRenderer.DrawText(g, Text, Font, label, Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

sealed class HotkeyBox : TextBox
{
    public event Action<Keys> Pressed = delegate { };
    public event Action ListeningChanged = delegate { };

    [DllImport("user32.dll")]
    static extern bool HideCaret(IntPtr window);

    public bool Listening { get; private set; }

    public HotkeyBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        BorderStyle = BorderStyle.None;
        TextAlign = HorizontalAlignment.Center;
        Cursor = Cursors.Hand;
    }

    public void StopListening() { SetListening(false); }

    void SetListening(bool value)
    {
        if (Listening == value) return;
        Listening = value;
        ListeningChanged();
    }

    void HideEditing()
    {
        Select(0, 0);
        HideCaret(Handle);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        HideEditing();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        SetListening(false);
        base.OnLostFocus(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        SetListening(true);
        HideEditing();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!Listening)
        {
            if (keyData != Keys.Space) return base.ProcessCmdKey(ref msg, keyData);
            SetListening(true);
            return true;
        }
        if (keyData == Keys.Tab || keyData == (Keys.Tab | Keys.Shift)) return base.ProcessCmdKey(ref msg, keyData);
        if (keyData == Keys.Escape) SetListening(false);
        else if (!HotkeyRules.IsModifier(keyData & Keys.KeyCode)) Pressed(keyData);
        HideEditing();
        return true;
    }
}
