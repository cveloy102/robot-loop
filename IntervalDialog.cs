using System.Globalization;

namespace RobotLoop;

internal sealed class IntervalDialog : ChromeForm
{
    internal decimal Value { get; private set; }
    internal IntervalDialog(decimal current)
    {
        Value=current; Text="Пауза между повторами"; AutoScaleMode=AutoScaleMode.Dpi;
        AutoScaleDimensions=new SizeF(96,96); ClientSize=new Size(320,214);
        FormBorderStyle=FormBorderStyle.None; MaximizeBox=false; MinimizeBox=false;
        ShowInTaskbar=false; TopMost=true; StartPosition=FormStartPosition.CenterParent;
        BackColor=Theme.Bg; ForeColor=Theme.Text; Font=Theme.Sans(14);
        var label=new TerminalLabel {Text="Секунды между циклами",Bounds=new Rectangle(24,20,272,24),ForeColor=Theme.Text};
        var input=new TextBox {Text=current.ToString(CultureInfo.CurrentCulture),Bounds=new Rectangle(24,52,272,36),Font=Theme.Mono(20),BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,AccessibleName="Пауза в секундах"};
        var error=new TerminalLabel {Bounds=new Rectangle(24,94,272,34),ForeColor=Theme.Accent,Font=Theme.Sans(12)};
        var save=new TerminalButton {Text="Сохранить",Accent=true,Font=Theme.Mono(14),Bounds=new Rectangle(24,144,148,44),CornerRadius=8};
        var cancel=new TerminalButton {Text="Отмена",Font=Theme.Mono(14),Bounds=new Rectangle(180,144,116,44),CornerRadius=8,DialogResult=DialogResult.Cancel};
        Controls.AddRange([label,input,error,save,cancel]); AcceptButton=save; CancelButton=cancel;
        foreach(Control control in Controls)control.Top+=40;
        ClientSize=new Size(320,254);AddCaption("Пауза повтора",false);
        save.Click+=(_,_)=>{
            string text=input.Text.Trim().Replace(".",CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            if(!decimal.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out var value)||value<0||value>86400)
            { error.Text="Введите число от 0 до 86400."; input.Focus(); return; }
            Value=decimal.Round(value,1); DialogResult=DialogResult.OK;
        };
        Shown+=(_,_)=>{Theme.ApplyWindowChrome(this);Activate();BringToFront();input.Focus();input.SelectAll();};
    }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Theme.Bg);
}
