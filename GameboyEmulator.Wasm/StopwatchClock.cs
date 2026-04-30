using System.Diagnostics;
using GameBoyEmulator.Core;

namespace GameBoyEmulator.Wasm;

public sealed class StopwatchClock : IClock
{
    public event Action? Ticked;
    public long Frequency => Stopwatch.Frequency;
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    public void Tick() => Ticked?.Invoke();
}
