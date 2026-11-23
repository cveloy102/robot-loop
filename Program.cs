namespace RobotLoop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args is ["--render-review", var output])
        {
            Directory.CreateDirectory(output);
            using (var mono = Theme.Mono(13))
            using (var display = Theme.Display(32))
                File.WriteAllText(Path.Combine(output,"fonts.txt"),$"Mono: {mono.Name} {mono.Style}; Display: {display.Name} {display.Style}\n" + string.Join("\n",typeof(Theme).Assembly.GetManifestResourceNames()));
            using var form = new MainForm(enableHotkeys: false);
            form.Show();
            foreach (bool light in new[] { false, true })
            {
                Theme.Light=light; form.ApplyTheme(); Application.DoEvents();
                using var bitmap=new Bitmap(form.Width,form.Height);
                form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));
                bitmap.Save(Path.Combine(output,light ? "native-light.png" : "native-dark.png"));
                using var dialog=new IntervalDialog(59.5m);
                dialog.Show(form); Application.DoEvents();
                using var dialogBitmap=new Bitmap(dialog.Width,dialog.Height);
                dialog.DrawToBitmap(dialogBitmap,new Rectangle(0,0,dialog.Width,dialog.Height));
                dialogBitmap.Save(Path.Combine(output,light ? "dialog-light.png" : "dialog-dark.png"));
                dialog.Close();
            }
            using var mini=new MiniForm(()=>60,_=>{},()=>{});
            mini.Show(); mini.UpdateTime(42,false,false); Application.DoEvents();
            using var miniBitmap=new Bitmap(mini.Width,mini.Height);
            mini.DrawToBitmap(miniBitmap,mini.ClientRectangle);
            miniBitmap.Save(Path.Combine(output,"native-mini.png"));
            return;
        }
        using var mutex = new Mutex(true, "RobotLoop.SingleInstance", out bool first);
        if (!first) { MessageBox.Show("Robot Loop уже запущен.", "Robot Loop"); return; }
        Application.Run(new MainForm());
    }
}
