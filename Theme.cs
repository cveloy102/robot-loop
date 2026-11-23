using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace RobotLoop;

internal static class Theme
{
    private static readonly PrivateFontCollection Fonts = new();
    private static readonly Dictionary<string, FontFamily> Families = [];
    public static bool Light { get; set; }
    public static Color Bg => ColorTranslator.FromHtml(Light ? "#F5F2EB" : "#101010");
    public static Color Card => ColorTranslator.FromHtml(Light ? "#EEEBE4" : "#181818");
    public static Color Plate => ColorTranslator.FromHtml(Light ? "#E7E2D8" : "#242220");
    public static Color Text => ColorTranslator.FromHtml(Light ? "#191918" : "#E8E4D9");
    public static Color Muted => ColorTranslator.FromHtml(Light ? "#68645D" : "#AAA79F");
    public static Color Divider => ColorTranslator.FromHtml(Light ? "#D5D0C7" : "#353535");
    public static Color Accent => ColorTranslator.FromHtml(Light ? "#CF332D" : "#F0443E");
    public static Color OnAccent => Light ? Color.White : Bg;
    [DllImport("gdi32.dll")] private static extern nint AddFontMemResourceEx(nint data, uint size, nint reserved, ref uint count);

    static Theme()
    {
        foreach (string name in new[] { "inter_display_black", "inter_medium", "jetbrains_mono_medium" })
        {
            using var stream = typeof(Theme).Assembly.GetManifestResourceStream($"RobotLoop.Fonts.{name}.ttf");
            if (stream is null) continue;
            using var bytes = new MemoryStream(); stream.CopyTo(bytes);
            var data = bytes.ToArray();
            // Font memory is retained for the process lifetime, as required by GDI/GDI+.
            var memory = Marshal.AllocHGlobal(data.Length); Marshal.Copy(data, 0, memory, data.Length);
            uint count = 0; AddFontMemResourceEx(memory, (uint)data.Length, 0, ref count);
            using var individual = new PrivateFontCollection(); individual.AddMemoryFont(memory, data.Length);
            Fonts.AddMemoryFont(memory, data.Length);
            Families[name] = Fonts.Families.First(f => f.Name == individual.Families[0].Name);
        }
    }
    public static Font Mono(float pixels) => FontFor("jetbrains_mono_medium", pixels);
    public static Font Display(float pixels) => FontFor("inter_display_black", pixels);
    public static Font Sans(float pixels) => FontFor("inter_medium", pixels);
    private static Font FontFor(string key, float pixels)
    {
        FontFamily family = Families.GetValueOrDefault(key) ?? FontFamily.GenericSansSerif;
        var style = family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
        return new Font(family, pixels, style, GraphicsUnit.Pixel);
    }
    public static GraphicsPath Round(RectangleF bounds, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        p.AddArc(bounds.Right-d, bounds.Top, d, d, 270, 90);
        p.AddArc(bounds.Right-d, bounds.Bottom-d, d, d, 0, 90);
        p.AddArc(bounds.Left, bounds.Bottom-d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    public static void ApplyWindowChrome(Form form)
    {
        if (!form.IsHandleCreated) return;
        // Only request compositor corners/colors. Custom chrome has no native
        // caption or extended glass frame and uses no binary clipping region.
        void Set(int attribute, int value) => DwmSetWindowAttribute(form.Handle,attribute,ref value,4);
        Set(20, Light ? 0 : 1); Set(33, 2);
        Set(38,1); // Opaque application surfaces, no automatic Mica/Acrylic.
        Set(34, ColorTranslator.ToWin32(Divider));
        if(form is not ChromeForm)
        { Set(35, ColorTranslator.ToWin32(Bg)); Set(36, ColorTranslator.ToWin32(Text)); }
    }
}

internal sealed class CardPanel : Panel
{
    public CardPanel() { DoubleBuffered = true; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new RectangleF(.5f, .5f, Width-1, Height-1), 16 * DeviceDpi / 96f);
        using var brush = new SolidBrush(Theme.Card); e.Graphics.FillPath(brush, path);
        using var pen = new Pen(Theme.Divider); e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class TerminalLabel : Label
{
    public TerminalLabel() { DoubleBuffered=true; }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent is CardPanel ? Theme.Card : Theme.Bg);
    protected override void OnPaint(PaintEventArgs e)
    {
        var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis;
        if(TextAlign==ContentAlignment.MiddleCenter) flags|=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ForeColor,flags);
    }
}

internal sealed class TerminalButton : Button
{
    [DefaultValue(false)] public bool Accent { get; set; }
    [DefaultValue(false)] public bool Plain { get; set; }
    [DefaultValue(null)] public string? Arrow { get; set; }
    [DefaultValue(14)] public int CornerRadius { get; set; } = 14;
    private bool hovered, pressed;
    public TerminalButton() { DoubleBuffered = true; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize=0; TabStop = true; Cursor = Cursors.Hand; SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true); }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnMouseEnter(EventArgs e) { hovered=true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered=pressed=false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed=e.Button==MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed=false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1), (Plain ? 5 : CornerRadius) * DeviceDpi / 96f);
        Color fill = Accent ? Theme.Accent : Plain ? Theme.Bg : Theme.Card;
        Color ink = Accent ? Theme.OnAccent : Theme.Text;
        if (!Enabled) { fill = Theme.Card; ink = Theme.Muted; }
        else if (!Accent && (hovered || pressed)) fill = Theme.Plate;
        using var brush = new SolidBrush(fill); e.Graphics.FillPath(brush,path);
        using var pen = new Pen(Theme.Divider);
        if (!Plain) e.Graphics.DrawPath(pen,path);
        if (Focused && ShowFocusCues)
        {
            using var focusPath=Theme.Round(new RectangleF(3,3,Width-6,Height-6), Math.Max(2,CornerRadius-3)*DeviceDpi/96f);
            using var focusPen=new Pen(Accent ? Theme.OnAccent : Theme.Accent);
            e.Graphics.DrawPath(focusPen,focusPath);
        }
        int gutter = (int)(12 * DeviceDpi / 96f);
        var rect = new Rectangle(gutter, 0, Width-gutter*2, Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, rect, ink,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
            (Arrow is null ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
        if (Arrow is not null) TextRenderer.DrawText(e.Graphics, Arrow, Font, rect, ink,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }
}

internal class ChromeForm : Form
{
    protected ChromeForm()
    {
        FormBorderStyle=FormBorderStyle.None; DoubleBuffered=true; MaximizeBox=false;
    }
    protected override CreateParams CreateParams
    {
        get
        {
            var p=base.CreateParams;
            // This is a genuinely borderless HWND, not a native caption hidden
            // with WM_NCCALCSIZE. Native frame painting must never replace our header.
            p.Style&=~(0x00C00000|0x00040000|0x00080000|0x00010000|0x00020000);
            p.ExStyle&=~0x00000001;
            return p;
        }
    }
    protected override void OnHandleCreated(EventArgs e) {base.OnHandleCreated(e);Theme.ApplyWindowChrome(this);}
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Theme.Bg);
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0xA3 || m.Msg==0x112 && ((int)m.WParam&0xFFF0)==0xF030){m.Result=0;return;}
        base.WndProc(ref m);
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(keyData==(Keys.Alt|Keys.F4)){Close();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected void AddCaption(string title,bool allowMinimize)
    {
        var header=new CaptionPanel{Bounds=new Rectangle(1,1,ClientSize.Width-2,39),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,BackColor=Theme.Bg};
        var label=new TerminalLabel{Text=title,Bounds=new Rectangle(14,10,ClientSize.Width-120,24),Font=Theme.Sans(14),ForeColor=Theme.Text};
        header.Controls.Add(label);
        void Drag(object? sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,2,0);}}
        header.MouseDown+=Drag;label.MouseDown+=Drag;
        var close=new CaptionButton{CloseGlyph=true,Bounds=new Rectangle(header.Width-44,0,44,38),Anchor=AnchorStyles.Top|AnchorStyles.Right,AccessibleName="Закрыть"};
        close.Click+=(_,_)=>Close();header.Controls.Add(close);
        if(allowMinimize){var minimize=new CaptionButton{Bounds=new Rectangle(header.Width-88,0,44,38),Anchor=AnchorStyles.Top|AnchorStyles.Right,AccessibleName="Свернуть"};minimize.Click+=(_,_)=>WindowState=FormWindowState.Minimized;header.Controls.Add(minimize);}
        Controls.Add(header);header.BringToFront();
    }
}

internal sealed class CaptionPanel : Panel
{
    public CaptionPanel(){DoubleBuffered=true;}
    protected override void OnPaintBackground(PaintEventArgs e)=>e.Graphics.Clear(Theme.Bg);
}

internal sealed class CaptionButton : Button
{
    [DefaultValue(false)] public bool CloseGlyph{get;set;}
    private bool hovered;
    public CaptionButton(){DoubleBuffered=true;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;TabStop=true;}
    protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaintBackground(PaintEventArgs e){}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(hovered?CloseGlyph?Theme.Accent:Theme.Plate:Theme.Bg);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float x=Width/2f,y=Height/2f,r=5*DeviceDpi/96f;
        using var pen=new Pen(hovered&&CloseGlyph?Theme.OnAccent:Theme.Muted,1.2f*DeviceDpi/96f);
        if(CloseGlyph){e.Graphics.DrawLine(pen,x-r,y-r,x+r,y+r);e.Graphics.DrawLine(pen,x+r,y-r,x-r,y+r);}
        else e.Graphics.DrawLine(pen,x-r,y,x+r,y);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),Theme.Accent,Theme.Bg);
    }
}

// One complete settings row is the hit target. Its divider belongs to the same
// control and always follows its actual scaled height.
internal sealed class SettingRowButton : Button
{
    [DefaultValue("")] public string Heading { get; set; } = "";
    [DefaultValue(true)] public bool Divider { get; set; } = true;
    private bool hovered;
    public SettingRowButton() { DoubleBuffered=true; FlatStyle=FlatStyle.Flat; Cursor=Cursors.Hand; Font=Theme.Mono(14); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); AccessibleName=$"{Heading}: {Text}"; Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hovered=true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered=false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(hovered && Enabled ? Theme.Plate : Theme.Card);
        int padding=(int)Math.Round(2*DeviceDpi/96f);
        int half=(Width-12)/2;
        const TextFormatFlags flags=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis;
        using var headingFont=Theme.Sans(14*DeviceDpi/96f);
        TextRenderer.DrawText(e.Graphics,Heading,headingFont,new Rectangle(padding,0,half,Height-1),Theme.Muted,flags);
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(half+8,0,Width-half-8-padding,Height-1),Enabled?Theme.Text:Theme.Muted,flags|TextFormatFlags.Right);
        using var pen=new Pen(Theme.Divider);
        if(Divider) e.Graphics.DrawLine(pen,0,Height-1,Width,Height-1);
        if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-2,-3),Theme.Accent,Theme.Card);
    }
}

internal sealed class StatusHeadline : Control
{
    public StatusHeadline() { DoubleBuffered = true; }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Theme.Text, flags);
        int width = TextRenderer.MeasureText(e.Graphics, Text, Font, Size.Empty, flags).Width;
        TextRenderer.DrawText(e.Graphics, "_", Font, new Rectangle(width-1,0,Math.Max(0,Width-width+1),Height), Theme.Accent,flags);
    }
}
