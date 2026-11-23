using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RobotLoop;

internal record MouseEvent(double At, int X, int Y, uint Flag, uint Data);

internal sealed class MacroEngine : IDisposable
{
    private readonly List<MouseEvent> events = [];
    private readonly Stopwatch recordingClock = new();
    private readonly Native.HookProc callback;
    private readonly Action<int, int, uint, uint, Rectangle> send;
    private nint hook;
    private double duration;
    private CancellationTokenSource? cancellation;
    private Task? playback;
    public bool Recording { get; private set; }
    public bool Playing => playback is { IsCompleted: false };
    public int Count => events.Count;
    public double Duration => Recording ? recordingClock.Elapsed.TotalMilliseconds : duration;
    public int Cycles { get; private set; }
    public double WaitRemaining => Math.Max(0, (Interlocked.Read(ref waitUntil) - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency);
    private long waitUntil;
    private long cycleEnds;
    private double repeatPauseSeconds;
    public double RepeatPauseSeconds
    {
        get => Volatile.Read(ref repeatPauseSeconds);
        set => Volatile.Write(ref repeatPauseSeconds, Math.Clamp(value, 0, 86400));
    }
    public double NextRepeatRemaining => Interlocked.Read(ref cycleEnds) is long end && end != 0
        ? Math.Max(0, (end - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency + RepeatPauseSeconds)
        : WaitRemaining;
    public string? Error { get; private set; }

    public MacroEngine(Action<int, int, uint, uint, Rectangle>? sender = null)
    { callback = Hook; send = sender ?? Native.Send; }
    public void StartRecording()
    {
        if (Playing || Recording) return;
        hook = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
        if (hook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        events.Clear(); duration = 0; Error = null; Cycles = 0;
        recordingClock.Restart(); Recording = true;
    }
    public void StopRecording(bool discardStopClick = false)
    {
        if (!Recording) return;
        duration = recordingClock.Elapsed.TotalMilliseconds;
        Recording = false; recordingClock.Stop();
        Native.UnhookWindowsHookEx(hook); hook = 0;
        if (discardStopClick)
        {
            int index = events.FindLastIndex(e => e.Flag == Native.LEFTDOWN);
            if (index >= 0)
            {
                duration = events[index].At;
                events.RemoveRange(index, events.Count - index);
            }
        }
    }
    private nint Hook(int code, nint message, nint data)
    {
        if (code >= 0 && Recording)
        {
            var item = Marshal.PtrToStructure<Native.MouseHook>(data);
            if ((item.Flags & 1) == 0)
            {
                uint flag = (int)message switch {
                    0x200 => Native.MOVE, 0x201 => Native.LEFTDOWN, 0x202 => Native.LEFTUP,
                    0x204 => Native.RIGHTDOWN, 0x205 => Native.RIGHTUP, 0x207 => Native.MIDDLEDOWN,
                    0x208 => Native.MIDDLEUP, 0x20A => Native.WHEEL, 0x20B => Native.XDOWN,
                    0x20C => Native.XUP, 0x20E => Native.HWHEEL, _ => 0
                };
                if (flag != 0)
                {
                    // Sample movement while retaining every click and wheel event.
                    double now = recordingClock.Elapsed.TotalMilliseconds;
                    if (flag != Native.MOVE || events.Count == 0 || now - events[^1].At >= 8)
                        events.Add(new MouseEvent(now, item.Position.X, item.Position.Y, flag,
                            flag is Native.WHEEL or Native.HWHEEL ? unchecked((uint)(int)(short)(item.MouseData >> 16)) : item.MouseData >> 16));
                }
            }
        }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    public void StartPlayback(double pauseSeconds)
    {
        if (Recording || Playing || events.Count == 0) return;
        var snapshot = events.ToArray();
        var desktop = SystemInformation.VirtualScreen;
        var length = duration;
        RepeatPauseSeconds = pauseSeconds;
        cancellation?.Dispose(); cancellation = new CancellationTokenSource();
        var token = cancellation.Token; Error = null; Cycles = 0;
        playback = Task.Run(async () => {
            var held = new HashSet<(uint Up, uint Data)>();
            int x = snapshot[0].X, y = snapshot[0].Y;
            try
            {
                Interlocked.Exchange(ref cycleEnds, Stopwatch.GetTimestamp() + (long)((350 + length) / 1000 * Stopwatch.Frequency));
                await Task.Delay(350, token);
                while (true)
                {
                    var clock = Stopwatch.StartNew();
                    Interlocked.Exchange(ref cycleEnds, Stopwatch.GetTimestamp() + (long)(length / 1000 * Stopwatch.Frequency));
                    foreach (var e in snapshot)
                    {
                        await WaitUntil(clock, e.At, token);
                        token.ThrowIfCancellationRequested();
                        send(e.X, e.Y, e.Flag, e.Data, desktop); x = e.X; y = e.Y;
                        TrackButton(held, e);
                    }
                    await WaitUntil(clock, length, token);
                    Release(held, x, y, desktop);
                    Cycles++;
                    var pauseClock = Stopwatch.StartNew();
                    Interlocked.Exchange(ref waitUntil, Stopwatch.GetTimestamp() + (long)(RepeatPauseSeconds * Stopwatch.Frequency));
                    Interlocked.Exchange(ref cycleEnds, 0);
                    do
                    {
                        double remaining = RepeatPauseSeconds - pauseClock.Elapsed.TotalSeconds;
                        Interlocked.Exchange(ref waitUntil, Stopwatch.GetTimestamp() + (long)(Math.Max(0, remaining) * Stopwatch.Frequency));
                        if (remaining <= 0) break;
                        await Task.Delay(TimeSpan.FromSeconds(Math.Min(0.05, remaining)), token);
                    } while (true);
                    // Yield even for a zero-length recording with no pause.
                    await Task.Delay(1, token);
                    Interlocked.Exchange(ref waitUntil, 0);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Error = ex.Message; }
            finally { Release(held, x, y, desktop); Interlocked.Exchange(ref waitUntil, 0); Interlocked.Exchange(ref cycleEnds, 0); }
        });
    }
    private static async Task WaitUntil(Stopwatch clock, double at, CancellationToken token)
    {
        while (at - clock.Elapsed.TotalMilliseconds > 0)
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, at - clock.Elapsed.TotalMilliseconds)), token);
    }
    private static void TrackButton(HashSet<(uint Up, uint Data)> held, MouseEvent e)
    {
        uint up = e.Flag switch { Native.LEFTDOWN or Native.LEFTUP => Native.LEFTUP,
            Native.RIGHTDOWN or Native.RIGHTUP => Native.RIGHTUP, Native.MIDDLEDOWN or Native.MIDDLEUP => Native.MIDDLEUP,
            Native.XDOWN or Native.XUP => Native.XUP, _ => 0 };
        if (up == 0) return;
        var key = (up, up == Native.XUP ? e.Data : 0);
        if (e.Flag == up) held.Remove(key); else held.Add(key);
    }
    private void Release(HashSet<(uint Up, uint Data)> held, int x, int y, Rectangle desktop)
    {
        foreach (var button in held)
            try { send(x, y, button.Up, button.Data, desktop); } catch { }
        held.Clear();
    }
    public void StopPlayback() => cancellation?.Cancel();
    public void Dispose()
    {
        StopRecording(); StopPlayback();
        playback?.GetAwaiter().GetResult(); cancellation?.Dispose();
    }
}
