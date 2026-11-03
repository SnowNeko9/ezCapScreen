using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class SettingsForm : Form
{
    const int ColumnLeft = 374, ColumnWidth = 302, DWMWA_USE_IMMERSIVE_DARK_MODE = 20, WTNCA_NODRAWICON = 2, WM_DPICHANGED = 0x02E0;
    const int DesignWidth = 700, DesignHeight = 350;

    [StructLayout(LayoutKind.Sequential)]
    struct SuggestedRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct ThemeOptions
    {
        public int Flags;
        public int Mask;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll")]
    static extern int SetWindowThemeAttribute(IntPtr window, int attribute, ref ThemeOptions options, int size);

    readonly Keys original;
    readonly Func<Keys, bool> canRegister;
    readonly HotkeyBox box = new HotkeyBox();
    readonly Label heading = new Label();
    readonly Label label = new Label();
    readonly Label hint = new Label();
    readonly ToggleSwitch autoStart = new ToggleSwitch();
    readonly FlatButton accept = new FlatButton(true);
    readonly FlatButton cancel = new FlatButton(false);
    readonly FlagButton english = new FlagButton(Flags.English);
    readonly FlagButton thai = new FlagButton(Flags.Thai);
    readonly Bitmap art;
    Font titleFont, bodyFont;
    float uiScale = 1f;
    bool rejected;

    public Keys Hotkey { get; private set; }

    public SettingsForm(Keys current, Icon icon, Func<Keys, bool> canRegister)
    {
        original = current;
        Hotkey = current;
        this.canRegister = canRegister;
        using (var stream = typeof(SettingsForm).Assembly.GetManifestResourceStream("ezCapScreen.png"))
            art = new Bitmap(stream);

        Icon = icon;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        BackColor = Theme.Page;
        ForeColor = Theme.Text;
        DoubleBuffered = true;

        heading.ForeColor = Theme.Text;
        heading.Location = new Point(ColumnLeft, 18);
        heading.AutoSize = true;
        label.ForeColor = Theme.Muted;
        label.Location = new Point(ColumnLeft, 72);
        label.AutoSize = true;

        english.Text = "EN";
        english.Bounds = new Rectangle(ColumnLeft + ColumnWidth - 72, 26, 34, 26);
        english.Click += delegate { SwitchLanguage(Language.English); };
        thai.Text = "TH";
        thai.Bounds = new Rectangle(ColumnLeft + ColumnWidth - 34, 26, 34, 26);
        thai.Click += delegate { SwitchLanguage(Language.Thai); };

        box.Location = new Point(ColumnLeft + 12, 104);
        box.Width = ColumnWidth - 24;
        box.Pressed += OnHotkeyPressed;
        box.ListeningChanged += delegate
        {
            rejected = false;
            RefreshHotkeyField();
        };
        box.Enter += delegate { Invalidate(FieldBounds()); };
        box.Leave += delegate { Invalidate(FieldBounds()); };

        hint.Location = new Point(ColumnLeft, 136);
        hint.Size = new Size(ColumnWidth, 42);

        autoStart.Location = new Point(ColumnLeft, 186);
        autoStart.Size = new Size(ColumnWidth, 30);
        autoStart.Checked = Settings.AutoStart;

        accept.Location = new Point(ColumnLeft + 104, 296);
        accept.Size = new Size(96, 36);
        accept.Click += OnAccept;
        cancel.Location = new Point(ColumnLeft + 206, 296);
        cancel.Size = new Size(96, 36);
        cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = accept;
        CancelButton = cancel;

        Controls.AddRange(new Control[] { heading, english, thai, label, box, hint, autoStart, accept, cancel });
        // Starting on OK leaves the box idle, so it only reacts once it has been clicked.
        ActiveControl = accept;
        // CenterScreen places a window with no owner on the monitor under the cursor.
        ApplyScale(Ui.ScaleAt(Screen.FromPoint(Cursor.Position).Bounds));
        ApplyLanguage();
    }

    void ApplyScale(float next)
    {
        float ratio = next / uiScale;
        uiScale = next;
        var oldTitle = titleFont;
        var oldBody = bodyFont;
        titleFont = new Font("Leelawadee UI", 24f * next, FontStyle.Bold, GraphicsUnit.Pixel);
        bodyFont = new Font("Leelawadee UI", 12.67f * next, GraphicsUnit.Pixel);
        Font = bodyFont;
        heading.Font = titleFont;
        if (oldTitle != null) oldTitle.Dispose();
        if (oldBody != null) oldBody.Dispose();
        Scale(new SizeF(ratio, ratio));
        ClientSize = new Size(Ui.Px(DesignWidth, next), Ui.Px(DesignHeight, next));
        Invalidate(true);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg != WM_DPICHANGED)
        {
            base.WndProc(ref m);
            return;
        }
        var suggested = (SuggestedRect)Marshal.PtrToStructure(m.LParam, typeof(SuggestedRect));
        ApplyScale((m.WParam.ToInt32() & 0xFFFF) / 96f);
        Location = new Point(suggested.Left, suggested.Top);
        m.Result = IntPtr.Zero;
    }

    void ApplyLanguage()
    {
        Text = Strings.WindowTitle;
        heading.Text = Strings.Heading;
        label.Text = Strings.HotkeyLabel;
        autoStart.Text = Strings.AutoStart;
        accept.Text = Strings.Accept;
        cancel.Text = Strings.Cancel;
        english.Selected = Strings.Current == Language.English;
        thai.Selected = Strings.Current == Language.Thai;
        RefreshHotkeyField();
    }

    // Saved at once rather than on OK: this is the language being looked at, not a setting being drafted.
    void SwitchLanguage(Language language)
    {
        Strings.Current = language;
        TrySave(delegate { Settings.Language = language; });
        ApplyLanguage();
        autoStart.Invalidate();
    }

    void RefreshHotkeyField()
    {
        bool listening = box.Listening;
        box.BackColor = listening ? Theme.FieldListening : Theme.Field;
        box.ForeColor = listening ? Theme.ListeningText : Theme.Text;
        box.Text = listening ? Strings.HotkeyPrompt : HotkeyRules.Label(Hotkey);
        hint.Text = !listening ? Strings.HintIdle : rejected ? Strings.HintRejected : Strings.HintListening;
        hint.ForeColor = listening && rejected ? Theme.Error : Theme.Muted;
        Invalidate(FieldBounds());
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = Theme.IsLight ? 0 : 1;
        DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        // Hides only the title bar icon. The window keeps its icon, so the taskbar and Alt+Tab still show it.
        var options = new ThemeOptions { Flags = WTNCA_NODRAWICON, Mask = WTNCA_NODRAWICON };
        SetWindowThemeAttribute(Handle, 1, ref options, Marshal.SizeOf(typeof(ThemeOptions)));
    }

    Rectangle FieldBounds()
    {
        int pad = box.Left - hint.Left;
        return new Rectangle(hint.Left, box.Top - Ui.Px(8, uiScale), box.Width + pad * 2, box.Height + Ui.Px(16, uiScale));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int half = ClientSize.Width / 2;
        var panel = new Rectangle(0, 0, half, ClientSize.Height);
        using (var fill = new LinearGradientBrush(panel, Theme.PanelTop, Theme.PanelBottom, LinearGradientMode.Vertical))
            g.FillRectangle(fill, panel);

        // The artwork is cut off at its own top, left and bottom edges, so the panel must match it exactly or the cuts show.
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(art, panel);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.None;
        var field = FieldBounds();
        field.Inflate(-1, -1);
        Theme.FillRounded(g, field, 6, box.BackColor, box.Focused ? Theme.Accent : Theme.Line);
        if (!box.Listening) return;
        using (var path = Theme.Rounded(field, 6))
        using (var ring = new Pen(Theme.Accent, 2.5f))
            g.DrawPath(ring, path);
    }

    void OnHotkeyPressed(Keys pressed)
    {
        if (HotkeyRules.IsValid(pressed))
        {
            Hotkey = pressed;
            box.StopListening();
            return;
        }
        rejected = true;
        RefreshHotkeyField();
    }

    bool TrySave(Action save)
    {
        try
        {
            save();
            return true;
        }
        catch (Exception ex)
        {
            if (!(ex is UnauthorizedAccessException || ex is System.Security.SecurityException)) throw;
            MessageBox.Show(this, Strings.SettingsFailed(ex.Message), "ezCapScreen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    void OnAccept(object sender, EventArgs e)
    {
        if (Hotkey != original && !canRegister(Hotkey))
        {
            MessageBox.Show(this, Strings.HotkeyTaken(HotkeyRules.Label(Hotkey)), "ezCapScreen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        bool saved = TrySave(delegate
        {
            Settings.Hotkey = Hotkey;
            Settings.AutoStart = autoStart.Checked;
        });
        if (saved) DialogResult = DialogResult.OK;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            art.Dispose();
            titleFont.Dispose();
            bodyFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
