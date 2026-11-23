namespace RobotLoop;

internal sealed class MiniForm : ChromeForm
{
    private readonly TimerDisplay time = new();
    private readonly NumericUpDown editor = new();
    private readonly Func<decimal> readPause;
    private readonly Action<decimal> savePause;
    private readonly Action restore;
    private readonly ContextMenuStrip menu = new();
    private readonly ToolTip tip = new();
    private Point dragOrigin, windowOrigin;
    private bool dragging, moved, editing;

    public MiniForm(Func<decimal> readPause, Action<decimal> savePause, Action restore)
    {
        this.readPause = readPause; this.savePause = savePause; this.restore = restore;
        Text = "Robot Loop — таймер"; AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(176, 52); FormBorderStyle = FormBorderStyle.None;
        TopMost = true; ShowInTaskbar = true; StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Bg; Padding = new Padding(1);
        DoubleBuffered = true;
        time.Dock = DockStyle.Fill; time.BackColor = BackColor;
        time.Font = Theme.Mono(26); time.ForeColor = Theme.Accent;
        time.Cursor = Cursors.Hand; Controls.Add(time);
        editor.SetBounds(10, 10, 156, 32); editor.DecimalPlaces = 1; editor.Increment = 0.5m;
        editor.Minimum = 0; editor.Maximum = 86400; editor.Font = Theme.Mono(18);
        editor.BackColor = BackColor; editor.ForeColor = time.ForeColor; editor.Visible = false;
        Controls.Add(editor);
        menu.Items.Add("Открыть полное окно", null, (_, _) => { FinishEdit(true); restore(); });
        time.ContextMenuStrip = menu;
        tip.SetToolTip(time, "Щелчок — пауза в секундах. Перетаскивание — переместить. Правый клик — полное окно.");
        time.MouseDown += (_, e) => {
            if (e.Button != MouseButtons.Left) return;
            dragOrigin = Cursor.Position; windowOrigin = Location; dragging = true; moved = false; time.Capture = true;
        };
        time.MouseMove += (_, _) => {
            if (!dragging) return;
            var delta = new Size(Cursor.Position.X - dragOrigin.X, Cursor.Position.Y - dragOrigin.Y);
            if (Math.Abs(delta.Width) + Math.Abs(delta.Height) > 5) moved = true;
            if (moved) Location = windowOrigin + delta;
        };
        time.MouseUp += (_, e) => {
            if (e.Button != MouseButtons.Left || !dragging) return;
            dragging = false; time.Capture = false;
            if (!moved) BeginEdit();
        };
        editor.KeyDown += (_, e) => {
            if (e.KeyCode is Keys.Enter or Keys.Escape)
            { e.SuppressKeyPress = true; FinishEdit(e.KeyCode == Keys.Enter); }
        };
        Deactivate += (_, _) => FinishEdit(true);
        FormClosing += (_, e) => {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true; FinishEdit(true); restore();
        };
        Shown += (_,_) => Theme.ApplyWindowChrome(this);
        ApplyTheme();
    }
    internal void ApplyTheme()
    {
        BackColor=Theme.Bg; time.BackColor=Theme.Bg; time.ForeColor=Theme.Accent;
        editor.BackColor=Theme.Card; editor.ForeColor=Theme.Text;
        menu.BackColor=Theme.Card; menu.ForeColor=Theme.Text;
        Theme.ApplyWindowChrome(this);
        time.Invalidate(); Invalidate();
    }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Theme.Bg);
    private void BeginEdit()
    {
        editing = true; editor.Value = readPause(); time.Visible = false;
        editor.Visible = true; editor.BringToFront(); Activate(); editor.Focus(); editor.Select(0, editor.Text.Length);
        tip.SetToolTip(editor, "Пауза между повторами, секунды. Enter — сохранить, Esc — отменить.");
    }
    private void FinishEdit(bool save)
    {
        if (!editing) return;
        editing = false;
        if (save) { editor.ValidateEditTextCompat(); savePause(editor.Value); }
        editor.Visible = false; time.Visible = true;
    }
    internal void UpdateTime(double seconds, bool recording, bool error)
    {
        time.SetDisplay(FormatTime(seconds), error ? Theme.Accent : recording ? Theme.Text : Theme.Accent);
    }
    internal static string FormatTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, seconds) * 10) / 10);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds/100}";
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { tip.Dispose(); menu.Dispose(); }
        base.Dispose(disposing);
    }
}

// Paint the complete timer as one opaque frame. A standard Label can erase its
// background separately from its text, producing flashes during the countdown.
internal sealed class TimerDisplay : Control
{
    internal TimerDisplay()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque |
            ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }
    internal void SetDisplay(string text, Color color)
    {
        if (Text == text && ForeColor == color) return;
        Text = text; ForeColor = color; Invalidate();
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, BackColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }
}

internal static class NumericEdit
{
    internal static void ValidateEditTextCompat(this NumericUpDown input)
    {
        if (decimal.TryParse(input.Text, out var value)) input.Value = Math.Clamp(value, input.Minimum, input.Maximum);
    }
}
