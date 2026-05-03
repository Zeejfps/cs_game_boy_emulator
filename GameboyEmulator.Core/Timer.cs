using System.Runtime.CompilerServices;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class Timer : ITimer
{
    // Bit of the 16-bit internal counter that drives TIMA per TAC mode.
    //   TAC=00 → 4096 Hz   (bit 9, period 1024 T)
    //   TAC=01 → 262144 Hz (bit 3, period 16   T)
    //   TAC=10 → 65536 Hz  (bit 5, period 64   T)
    //   TAC=11 → 16384 Hz  (bit 7, period 256  T)
    private static readonly byte[] TimaBitIndex = [9, 3, 5, 7];

    private readonly IInterrupts _interrupts;

    private ushort _counter;
    private byte _tima;
    private byte _tma;
    private byte _tac;
    private byte _timaBitIndex = TimaBitIndex[0]; // matches TAC=0 default
    private bool _isTimaEnabled;

    private bool _prevSignal;
    private int _reloadDelay;
    private bool _justReloaded;

    // Bit 12 of the internal counter falls 1->0 at exactly 512 Hz, which is
    // the APU frame sequencer's clock. WriteDiv (which resets the counter
    // to 0) can also cause a falling edge if bit 12 was set — that's the
    // documented "DIV reset glitches APU" behavior, used by some games to
    // phase-shift envelopes.
    private bool _prevApuBit;
    public Action? OnApuFrameSequencerTick { get; set; }

    public Timer(IInterrupts interrupts)
    {
        _interrupts = interrupts;
    }

    public byte ReadDiv() => (byte)(_counter >> 8);
    public byte ReadTima() => _tima;
    public byte ReadTma() => _tma;
    public byte ReadTac() => (byte)(_tac | 0xF8);

    public void WriteDiv(byte _)
    {
        _counter = 0;
        DetectTimaEdge();
        DetectApuEdge();
    }

    public void WriteTima(byte value)
    {
        // At the exact T-cycle the reload fires, a TIMA write is ignored —
        // TIMA keeps the just-loaded TMA value.
        if (_justReloaded)
            return;
        // Otherwise a write inside the 4-T reload window cancels the pending
        // reload+IRQ; outside the window it's a normal write.
        _reloadDelay = 0;
        _tima = value;
    }

    public void WriteTma(byte value)
    {
        _tma = value;
        // A TMA write at the exact T-cycle the reload fires updates the
        // value the reload just loaded: TIMA picks up the new TMA.
        if (_justReloaded)
            _tima = value;
    }

    public void WriteTac(byte value)
    {
        _tac = (byte)(value & 0x07);
        _timaBitIndex = TimaBitIndex[_tac & 0x03];
        _isTimaEnabled = (_tac & 0x04) != 0;
        DetectTimaEdge();
    }

    public void Tick(int tStates)
    {
        for (var i = 0; i < tStates; i++)
        {
            _counter++;
            _justReloaded = false;

            if (_reloadDelay > 0 && --_reloadDelay == 0)
            {
                _tima = _tma;
                _interrupts.Request(InterruptType.Timer);
                _justReloaded = true;
            }

            DetectTimaEdge();
            DetectApuEdge();
        }
    }

    private void DetectTimaEdge()
    {
        var signal = ComputeTimaSignal();

        if (_prevSignal && !signal)
            IncrementTima();

        _prevSignal = signal;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ComputeTimaSignal()
    {
        return _isTimaEnabled && ((_counter >> _timaBitIndex) & 1) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementTima()
    {
        if (_tima == 0xFF)
        {
            _tima = 0;
            _reloadDelay = 4;
        }
        else
        {
            _tima++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DetectApuEdge()
    {
        var bit = ((_counter >> 12) & 1) != 0;
        if (_prevApuBit && !bit)
            OnApuFrameSequencerTick?.Invoke();
        _prevApuBit = bit;
    }

    public void Reset()
    {
        _counter = 0;
        _tima = 0;
        _tma = 0;
        _tac = 0;
        _timaBitIndex = TimaBitIndex[0];
        _isTimaEnabled = false;
        _prevSignal = false;
        _prevApuBit = false;
        _reloadDelay = 0;
        _justReloaded = false;
    }
}
