using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using RobotLoop;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
static FieldInfo Field(string name) => typeof(MacroEngine).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
static List<MouseEvent> Events(MacroEngine engine) => (List<MouseEvent>)Field("events").GetValue(engine)!;
static void Feed(MacroEngine engine, int message, uint data = 0, uint flags = 0)
{
    nint memory = Marshal.AllocHGlobal(Marshal.SizeOf<Native.MouseHook>());
    try
    {
        Marshal.StructureToPtr(new Native.MouseHook { Position = new Native.Point { X = -100, Y = 220 }, MouseData = data, Flags = flags }, memory, false);
        typeof(MacroEngine).GetMethod("Hook", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(engine, [0, (nint)message, memory]);
    }
    finally { Marshal.FreeHGlobal(memory); }
}
static async Task Until(Func<bool> condition, int milliseconds = 3000)
{
    var timer = Stopwatch.StartNew();
    while (!condition())
    {
        if (timer.ElapsedMilliseconds > milliseconds) throw new TimeoutException();
        await Task.Delay(5);
    }
}

var latch = new HotkeyLatch();
int Key(Keys key, bool down = true, Keys mods = Keys.None, bool suspended = false)
    => latch.Handle(key, down, mods, suspended, Keys.F6, Keys.Control | Keys.F7, out _);
Check(Key(Keys.F6) == 1 && Key(Keys.F6) == 0, "record shortcut fires once while held");
Key(Keys.F6, false);
Check(Key(Keys.F6) == 1, "second press of same shortcut can stop recording");
Key(Keys.F6, false);
Check(Key(Keys.F7) == 0 && Key(Keys.F7, mods: Keys.Control) == 2, "shortcut modifiers matched exactly");
Key(Keys.F7, false);
Check(Key(Keys.F6, suspended: true) == 0, "shortcuts suspended during bind editing");
Key(Keys.F6); latch.ReleaseMissingKeys(_ => false);
Check(Key(Keys.F6) == 1, "focus change clears keys released outside hook");
latch.Handle(Keys.F6, false, Keys.None, true, Keys.F6, Keys.F7, out bool consumedUp);
Check(consumedUp, "shortcut release consumed even after changing mode");
latch.Handle(Keys.A, true, Keys.None, false, Keys.F6, Keys.F7, out bool consumedOther);
Check(!consumedOther, "unassigned keys forwarded");
Check(MiniForm.FormatTime(65.24) == "01:05.3", "mini timer preserves minutes and rounds countdown up");
Check(MiniForm.FormatTime(3601) == "01:00:01" && MiniForm.FormatTime(86400) == "24:00:00", "mini timer supports hour-long pauses");
Check(MiniForm.FormatTime(-1) == "00:00.0", "mini timer does not show negative time");
var missingRelease = new HotkeyLatch();
int At(long time) => missingRelease.HandleAt(Keys.F6, true, Keys.None, false, Keys.F6, Keys.F7, time, out _);
Check(At(0) == 1 && At(2500) == 1, "shortcut recovers from a missed key-up without a focus change");
var heldRepeat = new HotkeyLatch();
Check(heldRepeat.HandleAt(Keys.F6, true, Keys.None, false, Keys.F6, Keys.F7, 0, out _) == 1, "held shortcut starts once");
for (int time = 500; time <= 20000; time += 500)
    if (heldRepeat.HandleAt(Keys.F6, true, Keys.None, false, Keys.F6, Keys.F7, time, out _) != 0)
        throw new Exception("Held key repeated an action");
Check(true, "long-held key does not toggle repeatedly");
Check(Marshal.SizeOf<Native.Input>() == (IntPtr.Size == 8 ? 40 : 28), "Windows INPUT ABI");
using (var engine = new MacroEngine((_, _, _, _, _) => { }))
{
    Field("<Recording>k__BackingField").SetValue(engine, true);
    ((Stopwatch)Field("recordingClock").GetValue(engine)!).Start();
    Feed(engine, 0x200, flags: 1);
    Check(engine.Count == 0, "injected input is ignored");
    Feed(engine, 0x200); Feed(engine, 0x20A, unchecked((uint)(ushort)(short)-120) << 16);
    Feed(engine, 0x20B, 2u << 16); Feed(engine, 0x20C, 2u << 16);
    Check(engine.Count == 4 && Events(engine)[0].X == -100, "movement and negative monitor coordinates");
    Check(Events(engine)[1].Data == unchecked((uint)-120), "signed wheel delta");
    Check(Events(engine)[2].Data == 2 && Events(engine)[3].Flag == Native.XUP, "side mouse buttons");
    Feed(engine, 0x201); Feed(engine, 0x202);
    engine.StopRecording(true);
    Check(engine.Count == 4, "stop button click removed from recording");
}
var sent = new ConcurrentQueue<uint>();
using (var engine = new MacroEngine((_, _, flag, _, _) => sent.Enqueue(flag)))
{
    Events(engine).Add(new MouseEvent(0, 0, 0, Native.LEFTDOWN, 0));
    Field("duration").SetValue(engine, 60d);
    engine.StartPlayback(0.15);
    await Until(() => engine.Cycles >= 1);
    Check(engine.WaitRemaining > 0, "countdown between cycles");
    Check(sent.ToArray().SequenceEqual(new[] { Native.LEFTDOWN, Native.LEFTUP }), "unmatched mouse down released at end of cycle");
    await Until(() => engine.Cycles >= 2);
    engine.StopPlayback(); await Until(() => !engine.Playing);
    int count = sent.Count; await Task.Delay(200);
    Check(sent.Count == count && engine.Error is null, "multiple cycles and cancellation during pause");
}
sent.Clear();
using (var engine = new MacroEngine((_, _, flag, _, _) => sent.Enqueue(flag)))
{
    Events(engine).Add(new MouseEvent(0, 0, 0, Native.RIGHTDOWN, 0));
    Field("duration").SetValue(engine, 5000d);
    engine.StartPlayback(0);
    await Until(() => !sent.IsEmpty);
    engine.StopPlayback(); await Until(() => !engine.Playing);
    Check(sent.ToArray().SequenceEqual(new[] { Native.RIGHTDOWN, Native.RIGHTUP }), "stop during playback releases held button");
}
using (var engine = new MacroEngine((_, _, _, _, _) => throw new InvalidOperationException("blocked")))
{
    Events(engine).Add(new MouseEvent(0, 0, 0, Native.MOVE, 0));
    engine.StartPlayback(0); await Until(() => !engine.Playing);
    Check(engine.Error == "blocked", "input failure stops playback");
}
using (var engine = new MacroEngine((_, _, _, _, _) => { }))
{
    Events(engine).Add(new MouseEvent(0, 0, 0, Native.MOVE, 0));
    Field("duration").SetValue(engine, 10d);
    engine.StartPlayback(10);
    await Until(() => engine.WaitRemaining > 5);
    Check(engine.NextRepeatRemaining > 5, "mini countdown shows time to next cycle");
    engine.RepeatPauseSeconds = 0;
    await Until(() => engine.Cycles >= 2, 1000);
    Check(engine.Playing, "editing pause applies during the current countdown without restarting playback");
    engine.StopPlayback(); await Until(() => !engine.Playing);
}
Console.WriteLine("All checks passed. No real mouse input was sent.");
if(args.Contains("--ui-chrome")) WindowChromeChecks.Run();

if (args.Contains("--soak"))
{
    using var hotkeys = new GlobalHotkeys(0); // No assigned keys; never consume real input.
    int ownerThread = Environment.CurrentManagedThreadId;
    hotkeys.Start();
    Check(hotkeys.WorkerThreadId != ownerThread, "keyboard hook owns a separate message loop");
    var hookField = typeof(GlobalHotkeys).GetField("hook", BindingFlags.NonPublic | BindingFlags.Instance)!;
    Check(Native.UnhookWindowsHookEx((nint)hookField.GetValue(hotkeys)!), "simulate Windows removing the keyboard hook");
    int before = hotkeys.Renewals;
    // The owning thread does not pump window messages during this pause.
    Thread.Sleep(6200);
    Check(hotkeys.Renewals > before, "hook recovered while owning thread was blocked");

    long outputs = 0;
    using var soak = new MacroEngine((_, _, _, _, _) => Interlocked.Increment(ref outputs));
    Events(soak).Add(new MouseEvent(0, 0, 0, Native.LEFTDOWN, 0));
    Events(soak).Add(new MouseEvent(4, 0, 0, Native.LEFTUP, 0));
    Field("duration").SetValue(soak, 8d);
    soak.StartPlayback(0);
    var elapsed = Stopwatch.StartNew();
    for (int round = 0; round < 6; round++)
    {
        await Task.Delay(10000);
        GC.Collect();
        Check(soak.Playing && soak.Error is null, $"continuous playback after {elapsed.Elapsed.TotalSeconds:F0}s ({soak.Cycles} cycles)");
    }
    soak.StopPlayback(); await Until(() => !soak.Playing);
    Check(outputs == soak.Cycles * 2L || outputs == soak.Cycles * 2L + 2, "every mouse down has a matching release after long playback");
    Check(hotkeys.Renewals >= before + 10, "keyboard hook keeps renewing during long playback");
    Console.WriteLine("Long-run checks passed; no real mouse input was sent.");
}
