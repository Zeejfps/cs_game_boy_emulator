using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public class TimerTests
{
    private sealed class FakeInterruptBus : IInterruptsBus
    {
        private InterruptType _requested;
        private InterruptType _enabled;

        public int TimerCount { get; private set; }

        public void Request(InterruptType kind)
        {
            _requested |= kind;
            if (kind == InterruptType.Timer)
                TimerCount++;
        }

        public void Clear(InterruptType kind) => _requested &= ~kind;

        public bool IsRequested(InterruptType kind) => (_requested & kind) != 0;

        public InterruptType GetPending() => _requested & _enabled;

        public InterruptType ReadRequestedInterrupts() => _requested;
        public void WriteRequestedInterrupts(InterruptType requestedInterrupts) => _requested = requestedInterrupts;

        public InterruptType ReadEnabledInterrupts() => _enabled;
        public void WriteEnabledInterrupts(InterruptType enabledInterrupts) => _enabled = enabledInterrupts;
    }

    private readonly FakeInterruptBus _interrupts = new();
    private readonly Timer _timer;

    public TimerTests()
    {
        _timer = new Timer(_interrupts);
    }

    [Fact]
    public void Div_IncrementsEvery256TStates()
    {
        _timer.Tick(255);
        Assert.Equal(0, _timer.ReadDiv());

        _timer.Tick(1);
        Assert.Equal(1, _timer.ReadDiv());

        _timer.Tick(256);
        Assert.Equal(2, _timer.ReadDiv());
    }

    [Fact]
    public void WriteDiv_ResetsInternalCounter()
    {
        _timer.Tick(1000);
        Assert.NotEqual(0, _timer.ReadDiv());

        _timer.WriteDiv(0xAB);

        Assert.Equal(0, _timer.ReadDiv());
    }

    [Fact]
    public void ReadTac_UnusedBitsReadAsOne()
    {
        _timer.WriteTac(0x05);

        Assert.Equal(0xFD, _timer.ReadTac());
    }

    [Theory]
    [InlineData(0x04, 1024)] // 4096 Hz
    [InlineData(0x05, 16)]   // 262144 Hz
    [InlineData(0x06, 64)]   // 65536 Hz
    [InlineData(0x07, 256)]  // 16384 Hz
    public void Tima_IncrementsAtTacFrequency(byte tac, int periodTStates)
    {
        _timer.WriteTac(tac);

        _timer.Tick(periodTStates - 1);
        Assert.Equal(0, _timer.ReadTima());

        _timer.Tick(1);
        Assert.Equal(1, _timer.ReadTima());

        _timer.Tick(periodTStates);
        Assert.Equal(2, _timer.ReadTima());
    }

    [Fact]
    public void Tima_DoesNotIncrementWhenTimerDisabled()
    {
        _timer.WriteTac(0x01); // freq=01 but enable bit=0

        _timer.Tick(10_000);

        Assert.Equal(0, _timer.ReadTima());
    }

    [Fact]
    public void TimaOverflow_ReloadsFromTmaAndFiresInterruptAfter4TStates()
    {
        _timer.WriteTma(0x42);
        _timer.WriteTima(0xFF);
        _timer.WriteTac(0x05); // every 16 T

        _timer.Tick(16);

        // Overflow has occurred but reload is delayed 4 T-states.
        Assert.Equal(0, _timer.ReadTima());
        Assert.Equal(0, _interrupts.TimerCount);

        _timer.Tick(3);
        Assert.Equal(0, _timer.ReadTima());
        Assert.Equal(0, _interrupts.TimerCount);

        _timer.Tick(1);
        Assert.Equal(0x42, _timer.ReadTima());
        Assert.Equal(1, _interrupts.TimerCount);
    }

    [Fact]
    public void WritingTimaDuringReloadWindow_CancelsReloadAndInterrupt()
    {
        _timer.WriteTma(0x42);
        _timer.WriteTima(0xFF);
        _timer.WriteTac(0x05);

        _timer.Tick(16); // overflow → reload pending
        Assert.Equal(0, _timer.ReadTima());

        _timer.WriteTima(0x99); // cancel
        _timer.Tick(10);

        Assert.Equal(0x99, _timer.ReadTima() & 0xFF);
        Assert.Equal(0, _interrupts.TimerCount);
    }

    [Fact]
    public void WriteDivWithSelectedBitHigh_TriggersTimaIncrement()
    {
        // TAC=01 selects bit 3. Tick 8 T-states so bit 3 of the counter is 1
        // but no falling edge has fired yet (bit 3 transitions at 16-T period).
        _timer.WriteTac(0x05);
        _timer.Tick(8);
        Assert.Equal(0, _timer.ReadTima());

        _timer.WriteDiv(0); // resets counter → bit 3 falls 1→0 → spurious TIMA++

        Assert.Equal(1, _timer.ReadTima());
    }

    [Fact]
    public void DisablingTacWhenSelectedBitHigh_TriggersTimaIncrement()
    {
        _timer.WriteTac(0x05); // freq=01, enabled
        _timer.Tick(8);        // bit 3 high, no edge yet
        Assert.Equal(0, _timer.ReadTima());

        _timer.WriteTac(0x01); // disable → signal goes high→low → TIMA++

        Assert.Equal(1, _timer.ReadTima());
    }
}
