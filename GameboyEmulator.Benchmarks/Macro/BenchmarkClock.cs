using GameBoyEmulator.Core;

namespace GameBoyEmulator.Benchmarks.Macro;

// IClock that emulates exactly ticksPerStep T-cycles per Step() call by setting
// Frequency to the GB CPU clock (so GameBoy's _cyclesPerTick == 1.0 exactly) and
// advancing the timestamp by ticksPerStep per tick. ticksPerStep must stay below
// GameBoy's MaxCatchUpCycles (~419,430) or cycles get silently dropped.
public sealed class BenchmarkClock : IClock
{
    public event Action? Ticked;
    public long Frequency => 4_194_304;
    public long GetTimestamp() => _timestamp;

    private long _timestamp;
    private readonly long _ticksPerStep;

    public BenchmarkClock(long ticksPerStep = 70_224)
    {
        if (ticksPerStep <= 0 || ticksPerStep > 400_000)
            throw new ArgumentOutOfRangeException(nameof(ticksPerStep),
                "Must be in (0, 400_000]; GameBoy's MaxCatchUpCycles caps at ~419,430.");
        _ticksPerStep = ticksPerStep;
    }

    public long Step()
    {
        _timestamp += _ticksPerStep;
        Ticked?.Invoke();
        return _ticksPerStep;
    }
}
