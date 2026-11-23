using System.Runtime.InteropServices;
using System.Diagnostics;

namespace RobotLoop;

// Only the two assigned shortcuts are consumed. Other key events are forwarded.
internal sealed class GlobalHotkeys : IDisposable
{
    private readonly nint window;
    private readonly Native.HookProc callback;
    private readonly HotkeyLatch latch = new();
    private nint hook, foreground;
    private Thread? worker;
    private Control? dispatcher;
    private volatile bool suspended;
    private int recordKey, playKey, renewals;
    private long lastRenewal;
    public bool Suspended { get => suspended; set => suspended = value; }
    public Keys RecordKey { get => (Keys)Volatile.Read(ref recordKey); set => Volatile.Write(ref recordKey, (int)value); }
    public Keys PlayKey { get => (Keys)Volatile.Read(ref playKey); set => Volatile.Write(ref playKey, (int)value); }
    internal int Renewals => Volatile.Read(ref renewals);
    internal int WorkerThreadId { get; private set; }

    public GlobalHotkeys(nint window)
    { this.window = window; callback = OnKeyboard; }

    public void Start()
    {
        if (worker is not null) throw new InvalidOperationException("Перехват уже запущен.");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        worker = new Thread(() => {
            WorkerThreadId = Environment.CurrentManagedThreadId;
            try
            {
                using var control = new Control();
                _ = control.Handle;
                dispatcher = control;
                if (!Renew()) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                using var timer = new System.Windows.Forms.Timer { Interval = 100 };
                timer.Tick += (_, _) => Maintain();
                timer.Start(); ready.SetResult();
                Application.Run();
            }
            catch (Exception ex)
            {
                ready.TrySetException(ex);
                Diagnostics.Write("keyboard worker failed: " + ex.Message);
            }
            finally
            {
                if (hook != 0) { Native.UnhookWindowsHookEx(hook); hook = 0; }
                dispatcher = null;
            }
        }) { IsBackground = true, Name = "RobotLoop.Keyboard" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        ready.Task.GetAwaiter().GetResult();
    }

    private void Maintain()
    {
        var current = Native.GetForegroundWindow();
        bool changed = current != foreground;
        if (changed || Stopwatch.GetElapsedTime(lastRenewal).TotalSeconds >= 5)
        {
            // Windows can silently remove a timed-out hook, even without a focus change.
            if (!Renew()) Diagnostics.Write("keyboard hook renewal failed: " + Marshal.GetLastWin32Error());
            if (changed) latch.ReleaseMissingKeys(Held);
        }
    }

    private bool Renew()
    {
        var replacement = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (replacement == 0) return false;
        if (hook != 0) Native.UnhookWindowsHookEx(hook);
        hook = replacement; foreground = Native.GetForegroundWindow();
        lastRenewal = Stopwatch.GetTimestamp(); Interlocked.Increment(ref renewals);
        return true;
    }

    private nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0 && (int)message is 0x100 or 0x101 or 0x104 or 0x105)
        {
            var key = Marshal.PtrToStructure<Native.KeyboardHook>(data);
            bool down = (int)message is 0x100 or 0x104;
            Keys modifiers = Keys.None;
            if (Held(Keys.ControlKey)) modifiers |= Keys.Control;
            if (Held(Keys.ShiftKey)) modifiers |= Keys.Shift;
            if (Held(Keys.Menu)) modifiers |= Keys.Alt;
            bool windows = Held(Keys.LWin) || Held(Keys.RWin);
            int action = latch.Handle((Keys)key.Key, down, modifiers, Suspended || windows, RecordKey, PlayKey, out bool consumed);
            if (action != 0) Native.PostMessage(window, Native.WM_LOOP_HOTKEY, action, 0);
            if (consumed) return 1;
        }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    private static bool Held(Keys key) => (Native.GetAsyncKeyState((int)key) & 0x8000) != 0;
    public void Dispose()
    {
        var control = dispatcher;
        try { if (control is not null && !control.IsDisposed) control.BeginInvoke((Action)Application.ExitThread); }
        catch (InvalidOperationException) { }
        if (worker is not null && worker.ManagedThreadId != Environment.CurrentManagedThreadId)
            worker.Join();
    }
}

internal sealed class HotkeyLatch
{
    private readonly HashSet<Keys> consumedKeys = [];
    private readonly Dictionary<Keys, long> lastDown = [];
    internal int Handle(Keys key, bool down, Keys modifiers, bool suspended, Keys record, Keys play, out bool consumed)
        => HandleAt(key, down, modifiers, suspended, record, play, Environment.TickCount64, out consumed);

    internal int HandleAt(Keys key, bool down, Keys modifiers, bool suspended, Keys record, Keys play, long now, out bool consumed)
    {
        if (!down) { lastDown.Remove(key); consumed = consumedKeys.Remove(key); return 0; }
        if (consumedKeys.Contains(key))
        {
            long previous = lastDown.GetValueOrDefault(key);
            lastDown[key] = now;
            // A missing key-up must not disable this shortcut for the rest of the session.
            // Normal auto-repeat keeps refreshing this timestamp while a key is held.
            if (now - previous < 2000) { consumed = true; return 0; }
            consumedKeys.Remove(key);
        }
        var chord = key | modifiers;
        int action = suspended ? 0 : chord == record ? 1 : chord == play ? 2 : 0;
        consumed = action != 0;
        if (consumed) { consumedKeys.Add(key); lastDown[key] = now; }
        return action;
    }
    internal void ReleaseMissingKeys(Func<Keys, bool> isHeld) => consumedKeys.RemoveWhere(key => !isHeld(key));
}
