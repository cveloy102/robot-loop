namespace RobotLoop;

internal sealed class MainForm : ChromeForm
{
    private readonly MacroEngine engine = new();
    private readonly Settings settings = Settings.Load();
    private readonly StatusHeadline status = new();
    private readonly TerminalLabel details = new(), counter = new(), infoTitle = new(), badge = new();
    private readonly TerminalButton record = new(), play = new();
    private readonly SettingRowButton recordBind = new(), playBind = new(), pauseButton = new();
    private readonly TerminalButton themeButton = new();
    private readonly NumericUpDown pause = new();
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 80 };
    private bool pendingRecord, closing;
    private int recordRequest;
    private Keys? capturing;
    private Button? captureButton;
    private GlobalHotkeys? hotkeys;
    private readonly ToolTip errorTip = new();
    private string? reportedError;
    private MiniForm? mini;

    public MainForm(bool enableHotkeys = true)
    {
        Theme.Light = settings.LightTheme;
        Text = "ROBOT LOOP"; AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96); ClientSize = new Size(386, 462);
        using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("RobotLoop.AppIcon"))
            if (stream is not null) Icon = new Icon(stream);
        FormBorderStyle = FormBorderStyle.None; DoubleBuffered = true;
        MaximizeBox = false; TopMost = true; StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.Mono(13); KeyPreview = true;
        var miniButton = new TerminalButton { Text = "Мини", Bounds = new Rectangle(278,10,84,34), Font=Theme.Sans(14), CornerRadius=8, AccessibleName="Мини-режим" };
        miniButton.Click+=(_,_)=>EnterMini(); Controls.Add(miniButton);
        Controls.Add(Label("[ MOUSE LOOP ]",24,19,230,20,12,"accent"));
        status.SetBounds(24,51,338,42); status.Font=Theme.Display(30); status.AccessibleRole=AccessibleRole.StaticText; Controls.Add(status);
        details.SetBounds(24,98,338,24); details.Font=Theme.Sans(14); details.Tag="muted"; details.AutoEllipsis=true; Controls.Add(details);
        var info=new CardPanel {Bounds=new Rectangle(24,134,338,60)}; Controls.Add(info);
        infoTitle.SetBounds(14,10,234,18); infoTitle.Font=Theme.Mono(11); infoTitle.Tag="muted"; info.Controls.Add(infoTitle);
        counter.SetBounds(14,31,238,22); counter.Font=Theme.Mono(13); info.Controls.Add(counter);
        badge.SetBounds(254,19,72,24); badge.Font=Theme.Mono(11); badge.TextAlign=ContentAlignment.MiddleCenter; badge.Tag="muted"; info.Controls.Add(badge);
        SetButton(record,"Записать",24,206,165,46,true); record.Arrow="↗";
        SetButton(play,"Повторить",197,206,165,46,false); play.Arrow="↻";
        record.Click+=(_,_)=>ToggleRecord(true); play.Click+=(_,_)=>TogglePlayback();
        var section=Label("Управление",24,266,338,22,14,"muted"); section.Font=Theme.Sans(14); Controls.Add(section);
        var settingsCard=new CardPanel {Bounds=new Rectangle(24,296,338,126),Padding=new Padding(12,3,12,3)}; Controls.Add(settingsCard);
        var rows=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty,Padding=Padding.Empty};
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int i=0;i<3;i++)rows.RowStyles.Add(new RowStyle(SizeType.Percent,100f/3));
        settingsCard.Controls.Add(rows);
        recordBind.Heading="Запись / стоп"; recordBind.Text=Settings.KeyName(settings.RecordKey);
        playBind.Heading="Повтор / стоп"; playBind.Text=Settings.KeyName(settings.PlayKey);
        pauseButton.Heading="Пауза повтора"; pauseButton.Divider=false; pauseButton.AccessibleName="Изменить паузу между повторами";
        foreach(var row in new[]{recordBind,playBind,pauseButton}){row.Dock=DockStyle.Fill;row.Margin=Padding.Empty;rows.Controls.Add(row);}
        recordBind.Click+=(_,_)=>BeginBindCapture(recordBind,settings.RecordKey);
        playBind.Click+=(_,_)=>BeginBindCapture(playBind,settings.PlayKey);
        pause.Minimum=0;pause.Maximum=86400;pause.DecimalPlaces=1;pause.Value=Math.Clamp(settings.PauseSeconds,0,86400);
        pause.ValueChanged+=(_,_)=>{settings.PauseSeconds=pause.Value;engine.RepeatPauseSeconds=(double)pause.Value;SaveSettings();UpdateStatus();};
        pauseButton.Click+=(_,_)=>{
            if(capturing is not null)CancelCapture();
            using var dialog=new IntervalDialog(pause.Value);
            if(dialog.ShowDialog(this)==DialogResult.OK)pause.Value=dialog.Value;
        };
        Controls.Add(Label("Поверх окон · 1.2.3",24,433,230,22,12,"muted"));
        themeButton.Plain=true;themeButton.SetBounds(270,428,92,28);themeButton.Font=Theme.Sans(13);themeButton.AccessibleName="Сменить тему";
        themeButton.Click+=(_,_)=>{settings.LightTheme=!Theme.Light;Theme.Light=settings.LightTheme;ApplyTheme();mini?.ApplyTheme();SaveSettings();};
        Controls.Add(themeButton);
        foreach(Control control in Controls)control.Top+=40;
        ClientSize=new Size(386,502);
        AddCaption("ROBOT LOOP",true);
        ApplyTheme();
        refresh.Tick += (_,_) => UpdateStatus(); refresh.Start(); KeyDown += OnCaptureKey;
        Shown += (_,_) => {
            Theme.ApplyWindowChrome(this);
            if (!enableHotkeys) return;
            if (!RegisterBinds()) {
                settings.RecordKey=Keys.F6; settings.PlayKey=Keys.F7; recordBind.Text="F6"; playBind.Text="F7";
                if (!RegisterBinds()) MessageBox.Show(this,"Горячие клавиши заняты. Выберите другие в настройках.",Text);
            }
            hotkeys=new GlobalHotkeys(Handle) { RecordKey=settings.RecordKey, PlayKey=settings.PlayKey };
            try { hotkeys.Start(); Diagnostics.Write("application 1.2.3 started; dedicated keyboard worker active"); }
            catch(Exception ex) { hotkeys.Dispose(); hotkeys=null; MessageBox.Show(this,$"Прямой перехват клавиш недоступен; используются обычные бинды.\n{ex.Message}",Text); }
        };
        UpdateStatus();
    }
    private static TerminalLabel Label(string text,int x,int y,int w,int h,float size,string? role=null)
        => new() { Text=text, Bounds=new Rectangle(x,y,w,h), Font=Theme.Mono(size), Tag=role, BackColor=Color.Transparent };
    private void SetButton(TerminalButton button,string text,int x,int y,int w,int h,bool accent)
    {
        button.Text=text; button.SetBounds(x,y,w,h); button.Accent=accent; button.Font=Theme.Mono(14); button.CornerRadius=10; button.Cursor=Cursors.Hand; Controls.Add(button);
    }
    internal void ApplyTheme()
    {
        void Apply(Control control) {
            control.BackColor=control is CardPanel || control.Parent is CardPanel ? Theme.Card : Theme.Bg;
            control.ForeColor=control.Tag as string == "accent" ? Theme.Accent : control.Tag as string == "muted" ? Theme.Muted : Theme.Text;
            foreach(Control child in control.Controls) Apply(child);
            control.Invalidate();
        }
        Apply(this); Theme.ApplyWindowChrome(this); themeButton.Text=Theme.Light ? "Тёмная" : "Светлая";
    }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Theme.Bg);
    private bool RegisterBinds()
    {
        Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2);
        bool first = Register(1, settings.RecordKey), second = Register(2, settings.PlayKey);
        if (first && second) {
            if (hotkeys is not null) { hotkeys.RecordKey = settings.RecordKey; hotkeys.PlayKey = settings.PlayKey; }
            return true;
        }
        Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2); return false;
    }
    private bool Register(int id, Keys key)
    {
        uint mods = 0x4000;
        if (key.HasFlag(Keys.Control)) mods |= 2;
        if (key.HasFlag(Keys.Alt)) mods |= 1;
        if (key.HasFlag(Keys.Shift)) mods |= 4;
        uint code = (uint)(key & Keys.KeyCode);
        return code != 0 && Native.RegisterHotKey(Handle, id, mods, code);
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg is Native.WM_HOTKEY or Native.WM_LOOP_HOTKEY && capturing is null && !closing)
        {
            if (m.WParam == 1) ToggleRecord();
            if (m.WParam == 2) TogglePlayback();
        }
        base.WndProc(ref m);
    }
    private async void ToggleRecord(bool fromButton = false)
    {
        if (capturing is not null || engine.Playing) return;
        if (pendingRecord) { recordRequest++; pendingRecord = false; UpdateStatus(); return; }
        if (engine.Recording) engine.StopRecording(fromButton);
        else
        {
            int request = ++recordRequest;
            pendingRecord = true; UpdateStatus();
            await Task.Delay(350);
            if (!pendingRecord || closing || request != recordRequest) return;
            pendingRecord = false;
            try { engine.StartRecording(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Ошибка записи"); }
        }
        UpdateStatus();
    }
    private void TogglePlayback()
    {
        if (capturing is not null || engine.Recording || pendingRecord) return;
        if (engine.Playing) engine.StopPlayback(); else engine.StartPlayback((double)pause.Value);
        UpdateStatus();
    }
    private void UpdateStatus()
    {
        bool busy = engine.Recording || engine.Playing || pendingRecord;
        recordBind.Enabled = playBind.Enabled = !busy;
        pause.Enabled = pauseButton.Enabled = !engine.Recording && !pendingRecord;
        pauseButton.Text = $"{pause.Value:0.#} сек  →";
        record.Enabled = !engine.Playing; play.Enabled = !engine.Recording && !pendingRecord && engine.Count > 0;
        record.Text = engine.Recording || pendingRecord ? "Завершить" : "Записать";
        record.Arrow = engine.Recording || pendingRecord ? "■" : "↗";
        play.Text = engine.Playing ? "Остановить" : "Повторить"; play.Arrow = engine.Playing ? "■" : "↻";
        status.Text = pendingRecord ? "Подготовка." : engine.Recording ? "Идёт запись." : engine.Playing
            ? engine.WaitRemaining > 0 ? "Ждём повтор." : "Повторяем." : "Готов к записи.";
        details.Text = engine.Recording ? $"Движения и клики · {Settings.KeyName(settings.RecordKey)} — завершить" : engine.Playing
            ? $"Цикл {engine.Cycles+1:00} · {Settings.KeyName(settings.PlayKey)} — остановить"
            : $"{Settings.KeyName(settings.RecordKey)} — запись · {Settings.KeyName(settings.PlayKey)} — повтор";
        infoTitle.Text = engine.Playing && engine.WaitRemaining > 0 ? "ДО СЛЕДУЮЩЕГО ЦИКЛА" : engine.Recording ? "ТЕКУЩАЯ ЗАПИСЬ" : "СОХРАНЁННАЯ ЗАПИСЬ";
        counter.Text = engine.Playing && engine.WaitRemaining > 0 ? MiniForm.FormatTime(engine.NextRepeatRemaining)
            : engine.Count == 0 ? "Пока нет записи" : $"{engine.Count:N0} событий · {engine.Duration/1000:F1} сек";
        badge.Text = engine.Recording ? "● REC" : engine.Playing ? engine.WaitRemaining > 0 ? "◌ ПАУЗА" : "● LOOP" : "■ ГОТОВ";
        if (engine.Error is not null)
        {
            status.Text = "Ошибка ввода."; badge.Text = "! СБОЙ";
            if (reportedError != engine.Error)
            {
                reportedError = engine.Error;
                errorTip.SetToolTip(details, engine.Error); errorTip.SetToolTip(counter, engine.Error);
                Diagnostics.Write("playback failed: " + engine.Error);
            }
            details.Text = "Ошибка ввода — подробности при наведении";
        }
        else if (reportedError is not null)
        {
            reportedError = null; errorTip.RemoveAll();
        }
        mini?.UpdateTime(engine.Playing ? engine.NextRepeatRemaining : (double)pause.Value,
            engine.Recording || pendingRecord, engine.Error is not null);
    }
    private void EnterMini()
    {
        if (capturing is not null) CancelCapture();
        if (mini is null || mini.IsDisposed)
        {
            mini = new MiniForm(() => pause.Value, value => pause.Value = value, RestoreMain);
            mini.Icon = Icon;
        }
        var screen = Screen.FromControl(this).WorkingArea;
        mini.Location = new Point(Math.Clamp(Left, screen.Left, Math.Max(screen.Left, screen.Right - mini.Width)),
            Math.Clamp(Top, screen.Top, Math.Max(screen.Top, screen.Bottom - mini.Height)));
        UpdateStatus(); mini.Show(); Hide();
    }
    private void RestoreMain()
    {
        if (closing) return;
        mini?.Hide(); Show(); WindowState = FormWindowState.Normal; Activate();
    }
    private void BeginBindCapture(Button button, Keys previous)
    {
        if (capturing is not null) CancelCapture();
        Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2);
        capturing = previous; captureButton = button; button.Text = "Нажмите…";
        if (hotkeys is not null) hotkeys.Suspended = true;
        button.Focus();
    }
    private void CancelCapture()
    {
        if (captureButton is not null && capturing is Keys previous) captureButton.Text = Settings.KeyName(previous);
        capturing = null; captureButton = null; RegisterBinds();
        if (hotkeys is not null) hotkeys.Suspended = false;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (capturing is not null)
        {
            ApplyCapturedKey(keyData); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    private void OnCaptureKey(object? sender, KeyEventArgs e)
    {
        if (capturing is null) return;
        e.Handled = true; e.SuppressKeyPress = true; ApplyCapturedKey(e.KeyData);
    }
    private void ApplyCapturedKey(Keys key)
    {
        var code = key & Keys.KeyCode;
        if (code is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
        if (code == Keys.Escape) { CancelCapture(); return; }
        bool isRecord = captureButton == recordBind;
        Keys old = capturing!.Value;
        if (key == (isRecord ? settings.PlayKey : settings.RecordKey))
        { MessageBox.Show(this, "Для записи и повтора нужны разные клавиши.", Text); return; }
        if (isRecord) settings.RecordKey = key; else settings.PlayKey = key;
        if (!RegisterBinds())
        {
            if (isRecord) settings.RecordKey = old; else settings.PlayKey = old;
            Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2);
            MessageBox.Show(this, "Эта комбинация уже занята. Выберите другую.", Text); return;
        }
        captureButton!.Text = Settings.KeyName(key); capturing = null; captureButton = null; SaveSettings();
        if (hotkeys is not null) hotkeys.Suspended = false;
    }
    private void SaveSettings()
    {
        try { settings.Save(); }
        catch (Exception ex) { MessageBox.Show(this, $"Не удалось сохранить настройки: {ex.Message}", Text); }
    }
    protected override void OnDeactivate(EventArgs e)
    { if (capturing is not null) CancelCapture(); base.OnDeactivate(e); }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        closing = true; pendingRecord = false; refresh.Stop();
        Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2);
        hotkeys?.Dispose();
        mini?.Dispose();
        engine.Dispose(); pause.Dispose(); refresh.Dispose(); errorTip.Dispose();
        Diagnostics.Write("application closed"); base.OnFormClosing(e);
    }
}
