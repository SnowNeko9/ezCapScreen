using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

enum Tool { None, Rectangle, Oval, Arrow, Brush, Text }

enum Drag { None, NewRegion, Move, Resize, Draw }

sealed class ToolButton
{
    public Rectangle Bounds;
    public bool SecondRow;
    public Action<Graphics> Icon;
    public Action Click;
    public Func<bool> IsChecked = () => false;
    public Func<bool> IsEnabled = () => true;
}

// The whole virtual screen frozen at hotkey time, painted over by one window.
// Client coordinates equal bitmap coordinates.
sealed class Overlay : Form
{
    // Toolbar layout at 100% scale. Everything is multiplied by the monitor's scale when used.
    const int ToolbarWidth = 318, RowHeight = 40, ButtonSize = 26, ButtonStep = 35, ButtonInset = 6;
    const int HandleSize = 6, HandleSlop = 3, ToolbarGap = 8, LabelHeight = 30, CellSize = 11, MinRegionSize = 5, FocusGuardTicks = 15;
    static readonly Rectangle CurrentColorBox = new Rectangle(149, RowHeight + 9, 21, 21);
    static readonly Rectangle PaletteBox = new Rectangle(175, RowHeight + 3, CellSize * 13, CellSize * 3);

    static readonly int[,] Palette =
    {
        { 0xFF9090, 0xFFB180, 0xFFD274, 0xFFF600, 0xDEFF72, 0xB1FF56, 0x00FFF9, 0x49D2FF, 0xA0B5FF, 0xFCA7FF, 0xFFA7F8, 0xFFBCD7, 0xE7E7E7 },
        { 0xFF2222, 0xFF6600, 0xFFBB00, 0xECE100, 0xC5E100, 0x18E464, 0x00E1D3, 0x00BFFF, 0x748BFF, 0xC85DF5, 0xFD80E3, 0xFF88A2, 0x9A9A9A },
        { 0xFA2900, 0xFF6100, 0xFF9700, 0xD3C900, 0xB3BA00, 0x00BC00, 0x00B4BA, 0x008DEB, 0x3D5AE5, 0x8F3DD7, 0xF850C5, 0xF85073, 0x131313 },
    };

    static readonly Point[] Handles =
    {
        new Point(-1, -1), new Point(0, -1), new Point(1, -1), new Point(-1, 0),
        new Point(1, 0), new Point(-1, 1), new Point(0, 1), new Point(1, 1),
    };

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("gdi32.dll", SetLastError = true)]
    static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, int rop);

    static readonly Cursor CrossCursor = MakeCrossCursor();
    static readonly Brush HoverBrush = new SolidBrush(Color.FromArgb(232, 232, 232));
    static readonly Brush CheckedBrush = new SolidBrush(Color.FromArgb(207, 207, 207));
    static readonly Pen RowDivider = new Pen(Color.FromArgb(25, 0, 0, 0));
    static readonly Pen DashedFrame = new Pen(Color.Black) { DashStyle = DashStyle.Dash };
    static readonly ImageAttributes Ghost = MakeGhost();

    readonly Point screenOrigin;
    readonly Bitmap frozen, dimmed;
    readonly List<ToolButton> buttons = new List<ToolButton>();
    readonly List<Shape> shapes = new List<Shape>();

    Rectangle? region;
    Tool tool = Tool.None;
    Color color = Rgb(0xFF2222);
    int penWidth = 2;

    Drag drag = Drag.None;
    Point anchor;
    Rectangle dragStart;
    Point dragHandle;
    Shape current;
    ToolButton hovered, pressed;

    // A real TextBox parked outside the window does the typing: Thai input, paste, arrows and selection all come free.
    // The overlay paints its text, so what is on screen while typing is exactly what ends up in the image.
    readonly TextBox textInput = new TextBox { Multiline = true, AcceptsReturn = true, WordWrap = false, Visible = false, TabStop = false, Bounds = new Rectangle(-4000, -4000, 400, 200) };
    readonly System.Windows.Forms.Timer caretBlink = new System.Windows.Forms.Timer { Interval = 500 };
    readonly System.Windows.Forms.Timer focusGuard = new System.Windows.Forms.Timer { Interval = 100 };
    Shape editing;
    bool caretOn;

    public Overlay()
    {
        var bounds = SystemInformation.VirtualScreen;
        screenOrigin = bounds.Location;
        frozen = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        CopyScreen(frozen, bounds.Location);

        dimmed = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(dimmed))
        using (var veil = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
        {
            g.DrawImageUnscaled(frozen, 0, 0);
            g.FillRectangle(veil, 0, 0, bounds.Width, bounds.Height);
        }

        Text = "ezCapScreen overlay";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Cursor = CrossCursor;
        KeyPreview = true;

        Controls.Add(textInput);
        textInput.TextChanged += delegate
        {
            editing.Text = textInput.Text;
            ShowCaret();
        };
        textInput.KeyUp += delegate { ShowCaret(); };
        caretBlink.Tick += delegate
        {
            caretOn = !caretOn;
            Invalidate();
        };

        AddToolButton(0, Tool.Rectangle, Icons.Rectangle);
        AddToolButton(1, Tool.Oval, Icons.Oval);
        AddToolButton(2, Tool.Arrow, Icons.Arrow);
        AddToolButton(3, Tool.Brush, Icons.Brush);
        AddToolButton(4, Tool.Text, Icons.Text);
        AddButton(new ToolButton { Bounds = Slot(5, 0), Icon = Icons.Undo, Click = Undo, IsEnabled = () => shapes.Count > 0 || editing != null });
        AddButton(new ToolButton { Bounds = Slot(6, 0), Icon = Icons.Save, Click = SaveToFile });
        AddButton(new ToolButton { Bounds = Slot(7, 0), Icon = Icons.Cancel, Click = Close });
        AddButton(new ToolButton { Bounds = Slot(8, 0), Icon = Icons.Done, Click = CopyToClipboard });
        AddWidthButton(0, 2, Icons.DotSmall);
        AddWidthButton(1, 4, Icons.DotMedium);
        AddWidthButton(2, 8, Icons.DotLarge);
    }

    static Color Rgb(int value) { return Color.FromArgb(255, Color.FromArgb(value)); }

    // Graphics.CopyFromScreen rejects SRCCOPY combined with CAPTUREBLT, and CAPTUREBLT is what includes layered windows.
    static void CopyScreen(Bitmap target, Point origin)
    {
        const int SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;
        using (var g = Graphics.FromImage(target))
        {
            var destination = g.GetHdc();
            var screen = GetDC(IntPtr.Zero);
            try
            {
                if (!BitBlt(destination, 0, 0, target.Width, target.Height, screen, origin.X, origin.Y, SRCCOPY | CAPTUREBLT))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screen);
                g.ReleaseHdc(destination);
            }
        }
    }

    static Rectangle Slot(int column, int row)
    {
        return new Rectangle(ButtonInset + column * ButtonStep, row * RowHeight + (RowHeight - ButtonSize) / 2, ButtonSize, ButtonSize);
    }

    void AddButton(ToolButton button) { buttons.Add(button); }

    void AddToolButton(int column, Tool value, Action<Graphics> icon)
    {
        AddButton(new ToolButton { Bounds = Slot(column, 0), Icon = icon, Click = () => { CommitText(); tool = value; }, IsChecked = () => tool == value });
    }

    void AddWidthButton(int column, int value, Action<Graphics> icon)
    {
        AddButton(new ToolButton { Bounds = Slot(column, 1), SecondRow = true, Icon = icon, Click = () => { penWidth = value; RestyleEditing(); }, IsChecked = () => penWidth == value });
    }

    // When an exclusive-fullscreen game minimizes itself, Windows can hand focus to another window, such as
    // Steam's overlay, just after this one appears. Without focus Esc and typing go nowhere, so take it back.
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        int ticksLeft = FocusGuardTicks;
        focusGuard.Tick += delegate
        {
            if (GetForegroundWindow() != Handle && !OwnedForms.Any()) SetForegroundWindow(Handle);
            if (--ticksLeft <= 0) focusGuard.Stop();
        };
        focusGuard.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            frozen.Dispose();
            dimmed.Dispose();
            caretBlink.Dispose();
            focusGuard.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    void ShowCaret()
    {
        caretOn = true;
        caretBlink.Stop();
        caretBlink.Start();
        Invalidate();
    }

    void BeginText(Point p)
    {
        editing = new Shape(Tool.Text, color, penWidth, UiScale, p);
        textInput.Text = "";
        textInput.Visible = true;
        textInput.Focus();
        ShowCaret();
    }

    void EndEditing()
    {
        editing = null;
        textInput.Visible = false;
        caretBlink.Stop();
        Invalidate();
    }

    void CommitText()
    {
        if (editing == null) return;
        if (!editing.IsEmpty) shapes.Add(editing);
        EndEditing();
    }

    void RestyleEditing()
    {
        if (editing != null) editing.Restyle(color, penWidth);
    }

    public static Rectangle Corners(Point a, Point b)
    {
        return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    static Point Clamp(Point p, Rectangle r)
    {
        return new Point(Math.Max(r.Left, Math.Min(p.X, r.Right)), Math.Max(r.Top, Math.Min(p.Y, r.Bottom)));
    }

    float UiScale
    {
        get
        {
            var r = region.Value;
            return Ui.ScaleAt(new Rectangle(r.X + screenOrigin.X, r.Y + screenOrigin.Y, r.Width, r.Height));
        }
    }

    int Px(int value) { return Ui.Px(value, UiScale); }

    Rectangle Scaled(Rectangle r) { return new Rectangle(Px(r.X), Px(r.Y), Px(r.Width), Px(r.Height)); }

    bool ToolbarVisible
    {
        get { return region.HasValue && (drag == Drag.None || drag == Drag.Draw); }
    }

    // Below the region and right-aligned, flipped above when it would leave the monitor, inside as a last resort.
    Rectangle ToolbarRect()
    {
        var r = region.Value;
        var screen = Screen.FromRectangle(new Rectangle(r.X + screenOrigin.X, r.Y + screenOrigin.Y, r.Width, r.Height)).Bounds;
        screen.Offset(-screenOrigin.X, -screenOrigin.Y);
        int width = Px(ToolbarWidth), gap = Px(ToolbarGap);
        int height = Px(tool == Tool.None ? RowHeight : RowHeight * 2);
        int y = r.Bottom + gap;
        if (y + height > screen.Bottom) y = r.Top - gap - height;
        if (y < screen.Top) y = r.Bottom - gap - height;
        int x = Math.Max(screen.Left, Math.Min(r.Right - width, screen.Right - width));
        return new Rectangle(x, y, width, height);
    }

    bool OverToolbar(Point p) { return ToolbarVisible && ToolbarRect().Contains(p); }

    ToolButton ButtonAt(Point p)
    {
        if (!OverToolbar(p)) return null;
        var o = ToolbarRect().Location;
        var local = new Point(p.X - o.X, p.Y - o.Y);
        return buttons.FirstOrDefault(b => (!b.SecondRow || tool != Tool.None) && Scaled(b.Bounds).Contains(local));
    }

    bool PickColor(Point p)
    {
        if (tool == Tool.None) return false;
        var o = ToolbarRect().Location;
        var palette = Scaled(PaletteBox);
        var local = new Point(p.X - o.X - palette.X, p.Y - o.Y - palette.Y);
        if (local.X < 0 || local.Y < 0 || local.X >= palette.Width || local.Y >= palette.Height) return false;
        color = Rgb(Palette[local.Y * 3 / palette.Height, local.X * 13 / palette.Width]);
        RestyleEditing();
        return true;
    }

    Rectangle HandleRect(Rectangle r, Point h)
    {
        int size = Px(HandleSize);
        int x = h.X < 0 ? r.Left - size : h.X > 0 ? r.Right : r.Left + (r.Width - size) / 2;
        int y = h.Y < 0 ? r.Top - size : h.Y > 0 ? r.Bottom : r.Top + (r.Height - size) / 2;
        return new Rectangle(x, y, size, size);
    }

    Point? HandleAt(Point p)
    {
        if (!region.HasValue) return null;
        foreach (var h in Handles)
        {
            var hit = HandleRect(region.Value, h);
            hit.Inflate(Px(HandleSlop), Px(HandleSlop));
            if (hit.Contains(p)) return h;
        }
        return null;
    }

    static Cursor HandleCursor(Point h)
    {
        int slope = h.X * h.Y;
        if (slope > 0) return Cursors.SizeNWSE;
        if (slope < 0) return Cursors.SizeNESW;
        return h.X != 0 ? Cursors.SizeWE : Cursors.SizeNS;
    }

    Cursor CursorAt(Point p)
    {
        if (OverToolbar(p)) return Cursors.Arrow;
        var handle = HandleAt(p);
        if (handle.HasValue) return HandleCursor(handle.Value);
        if (!region.HasValue || !region.Value.Contains(p)) return CrossCursor;
        if (tool == Tool.Text) return Cursors.IBeam;
        return tool == Tool.None ? Cursors.SizeAll : CrossCursor;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var p = e.Location;
        if (OverToolbar(p))
        {
            pressed = ButtonAt(p);
            if (pressed == null && PickColor(p)) Invalidate();
            return;
        }

        CommitText();
        anchor = p;
        var handle = HandleAt(p);
        if (handle.HasValue)
        {
            drag = Drag.Resize;
            dragHandle = handle.Value;
            dragStart = region.Value;
        }
        else if (!region.HasValue || !region.Value.Contains(p))
        {
            drag = Drag.NewRegion;
        }
        else
        {
            if (tool == Tool.None)
            {
                drag = Drag.Move;
                dragStart = region.Value;
            }
            else if (tool == Tool.Text)
            {
                BeginText(p);
            }
            else
            {
                drag = Drag.Draw;
                current = new Shape(tool, color, penWidth, UiScale, p);
            }
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = Clamp(e.Location, ClientRectangle);
        int dx = p.X - anchor.X, dy = p.Y - anchor.Y;
        switch (drag)
        {
            case Drag.None:
                var over = ButtonAt(p);
                if (over != hovered)
                {
                    hovered = over;
                    Invalidate();
                }
                var cursor = CursorAt(p);
                if (Cursor != cursor) Cursor = cursor;
                return;
            case Drag.NewRegion:
                // A stray click, even with a shaky hand, replaces nothing, so it cannot cost finished annotations.
                var fresh = Corners(anchor, p);
                if (fresh.Width >= MinRegionSize && fresh.Height >= MinRegionSize)
                {
                    region = fresh;
                    shapes.Clear();
                    tool = Tool.None;
                }
                break;
            case Drag.Move:
                region = new Rectangle(
                    Math.Max(0, Math.Min(dragStart.X + dx, ClientSize.Width - dragStart.Width)),
                    Math.Max(0, Math.Min(dragStart.Y + dy, ClientSize.Height - dragStart.Height)),
                    dragStart.Width, dragStart.Height);
                break;
            case Drag.Resize:
                var a = new Point(dragStart.Left + (dragHandle.X < 0 ? dx : 0), dragStart.Top + (dragHandle.Y < 0 ? dy : 0));
                var b = new Point(dragStart.Right + (dragHandle.X > 0 ? dx : 0), dragStart.Bottom + (dragHandle.Y > 0 ? dy : 0));
                var resized = Corners(Clamp(a, ClientRectangle), Clamp(b, ClientRectangle));
                region = new Rectangle(resized.X, resized.Y, Math.Max(1, resized.Width), Math.Max(1, resized.Height));
                break;
            case Drag.Draw:
                current.Extend(Clamp(p, region.Value));
                break;
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var finished = drag;
        drag = Drag.None;
        if (finished == Drag.Draw)
        {
            if (!current.IsEmpty) shapes.Add(current);
            current = null;
        }

        var button = pressed;
        pressed = null;
        if (button != null && button == ButtonAt(e.Location) && button.IsEnabled()) button.Click();
        if (!IsDisposed) Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape) return;
        e.SuppressKeyPress = true;
        if (editing != null) EndEditing();
        else Close();
    }

    void Undo()
    {
        if (editing != null) EndEditing();
        else if (shapes.Count > 0) shapes.RemoveAt(shapes.Count - 1);
    }

    Bitmap Flatten()
    {
        var r = region.Value;
        var result = new Bitmap(r.Width, r.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(result))
        {
            g.DrawImage(frozen, new Rectangle(Point.Empty, r.Size), r, GraphicsUnit.Pixel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(-r.X, -r.Y);
            foreach (var s in shapes) s.Draw(g);
        }
        return result;
    }

    void CopyToClipboard()
    {
        CommitText();
        try
        {
            using (var image = Flatten())
                Clipboard.SetDataObject(image, true, 10, 100);
        }
        catch (ExternalException ex)
        {
            MessageBox.Show(this, Strings.CopyFailed(ex.Message), "ezCapScreen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Close();
    }

    void SaveToFile()
    {
        CommitText();
        string path;
        using (var dialog = new SaveFileDialog())
        {
            dialog.Title = Strings.SaveDialogTitle;
            dialog.FileName = "ScreenCapture";
            dialog.Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            path = dialog.FileName;
        }

        var format = ImageFormat.Png;
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".jpg" || extension == ".jpeg") format = ImageFormat.Jpeg;
        if (extension == ".bmp") format = ImageFormat.Bmp;
        try
        {
            using (var image = Flatten())
                image.Save(path, format);
        }
        catch (Exception ex)
        {
            if (!(ex is ExternalException || ex is IOException || ex is UnauthorizedAccessException)) throw;
            MessageBox.Show(this, Strings.SaveFailed(ex.Message), "ezCapScreen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Close();
    }

    // ponytail: repaints the whole virtual screen per mouse move, dirty rectangles if it ever feels slow.
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImageUnscaled(dimmed, 0, 0);
        if (!region.HasValue) return;

        var r = region.Value;
        g.DrawImage(frozen, r, r, GraphicsUnit.Pixel);
        g.CompositingMode = CompositingMode.SourceOver;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.SetClip(r);
        foreach (var s in shapes) s.Draw(g);
        if (current != null) current.Draw(g);
        if (editing != null) editing.Draw(g);
        g.ResetClip();
        g.SmoothingMode = SmoothingMode.None;

        g.DrawRectangle(Pens.White, r.X - 1, r.Y - 1, r.Width + 1, r.Height + 1);
        foreach (var h in Handles)
        {
            var box = HandleRect(r, h);
            g.FillRectangle(Brushes.White, box);
            box.Inflate(-Px(1), -Px(1));
            g.FillRectangle(Brushes.Black, box);
        }

        int handle = Px(HandleSize), labelHeight = Px(LabelHeight);
        int labelY = r.Top - handle - labelHeight >= 0 ? r.Top - handle - labelHeight : r.Top + handle;
        using (var labelFont = new Font("Segoe UI", 12f * UiScale, GraphicsUnit.Pixel))
            TextRenderer.DrawText(g, string.Format("{0} x {1}", r.Width, r.Height), labelFont,
                new Rectangle(r.Left, labelY, Px(100), labelHeight), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        if (editing != null) PaintTextEditor(g);
        if (ToolbarVisible) PaintToolbar(g);
    }

    void PaintTextEditor(Graphics g)
    {
        var frame = Rectangle.Ceiling(editing.TextBounds(g));
        frame.Inflate(3, 3);
        g.DrawRectangle(Pens.White, frame);
        g.DrawRectangle(DashedFrame, frame);
        if (!caretOn) return;
        using (var caret = new SolidBrush(color))
            g.FillRectangle(caret, editing.CaretBounds(g, textInput.SelectionStart));
    }

    void PaintToolbar(Graphics g)
    {
        var rect = ToolbarRect();
        g.FillRectangle(Brushes.White, rect);
        foreach (var b in buttons)
        {
            if (b.SecondRow && tool == Tool.None) continue;
            var box = Scaled(b.Bounds);
            box.Offset(rect.Location);
            var icon = Icons.At(b.Icon, box.Width);
            if (!b.IsEnabled())
            {
                g.DrawImage(icon, box, 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, Ghost);
                continue;
            }
            if (b.IsChecked())
            {
                g.FillRectangle(CheckedBrush, box);
                g.DrawRectangle(Pens.White, box.X, box.Y, box.Width - 1, box.Height - 1);
            }
            else if (b == hovered)
            {
                g.FillRectangle(HoverBrush, box);
            }
            g.DrawImageUnscaled(icon, box.Location);
        }
        if (tool == Tool.None) return;

        g.DrawLine(RowDivider, rect.Left, rect.Top + Px(RowHeight), rect.Right - 1, rect.Top + Px(RowHeight));
        var swatchBox = Scaled(CurrentColorBox);
        swatchBox.Offset(rect.Location);
        using (var swatch = new SolidBrush(color))
            g.FillRectangle(swatch, swatchBox);
        var palette = Scaled(PaletteBox);
        palette.Offset(rect.Location);
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 13; column++)
            {
                int left = palette.X + palette.Width * column / 13, top = palette.Y + palette.Height * row / 3;
                int right = palette.X + palette.Width * (column + 1) / 13, bottom = palette.Y + palette.Height * (row + 1) / 3;
                using (var cell = new SolidBrush(Rgb(Palette[row, column])))
                    g.FillRectangle(cell, left, top, right - left, bottom - top);
            }
    }

    static ImageAttributes MakeGhost()
    {
        var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 0.3f });
        return attributes;
    }

    static Cursor MakeCrossCursor()
    {
        using (var image = new Bitmap(25, 25, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(image))
            using (var outline = new Pen(Color.FromArgb(160, 0, 0, 0), 3))
            {
                g.DrawLine(outline, 12, 1, 12, 23);
                g.DrawLine(outline, 1, 12, 23, 12);
                g.DrawLine(Pens.White, 12, 1, 12, 23);
                g.DrawLine(Pens.White, 1, 12, 23, 12);
            }
            return new Cursor(image.GetHicon());
        }
    }
}
