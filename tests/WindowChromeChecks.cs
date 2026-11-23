using System.Runtime.InteropServices;
using RobotLoop;

internal static class WindowChromeChecks
{
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd,int index);
    internal static void Run()
    {
        Exception? error=null;
        var thread=new Thread(()=>{
            try { CheckWindows(); }
            catch(Exception ex){error=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(error is not null)throw new Exception("Native window chrome regression",error);
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach(Control child in root.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
    private static void Verify(Form form,string stage,int captionCount)
    {
        Application.DoEvents();
        long style=GetWindowLongPtr(form.Handle,-16);
        const long nativeCaptionBits=0x00C00000|0x00040000|0x00010000|0x00020000;
        if((style&nativeCaptionBits)!=0)throw new Exception($"{stage}: native caption styles returned: {style:X}");
        var buttons=Descendants(form).OfType<RobotLoop.CaptionButton>().ToArray();
        if(buttons.Length!=captionCount)throw new Exception($"{stage}: expected {captionCount} caption buttons");
        foreach(var button in buttons)
            if(!button.Visible||!button.Parent!.ClientRectangle.Contains(button.Bounds))
                throw new Exception($"{stage}: missing or clipped {button.AccessibleName}");
        Console.WriteLine($"PASS: {stage}: no native frame; {captionCount} visible caption buttons");
    }
    private static void CheckWindows()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();
        using var main=new MainForm(enableHotkeys:false);
        main.Show();Verify(main,"startup",2);
        main.Hide();Application.DoEvents();main.Show();Verify(main,"returned from hidden window",2);
        typeof(Control).GetMethod("RecreateHandle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(main,null);
        Verify(main,"native handle recreated",2);
        foreach(bool light in new[]{true,false,true})
        {
            Theme.Light=light;main.ApplyTheme();Verify(main,light?"light theme":"dark theme",2);
            main.WindowState=FormWindowState.Minimized;Application.DoEvents();
            main.WindowState=FormWindowState.Normal;Verify(main,"restored from taskbar",2);
            using var dialog=new IntervalDialog(59.5m);dialog.Show(main);Verify(dialog,"interval dialog",1);dialog.Close();
            Verify(main,"after dialog close",2);
        }
        main.Scale(new SizeF(1.25f,1.25f));Verify(main,"125% scaled layout",2);
        main.Scale(new SizeF(1.2f,1.2f));Verify(main,"150% scaled layout",2);
        using var mini=new MiniForm(()=>60,_=>{},()=>{});mini.Show();Verify(mini,"mini timer",0);
        main.Close();
    }
}
