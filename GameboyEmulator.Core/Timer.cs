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

    private readonly IInterruptsBus _interrupts;

    private ushort _counter;
    private byte _tima;
    private byte _tma;
    private byte _tac;
    private byte _timaBitIndex = TimaBitIndex[0]; // matches TAC=0 default
    private bool _isTimaEnabled;

    private bool _prevSignal;
    private int _reloadDelay;

    public Timer(IInterruptsBus interrupts)
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
    }

    public void WriteTima(byte value)
    {
        // A write inside the 4-T reload window cancels the pending reload+IRQ.
        _reloadDelay = 0;
        _tima = value;
    }

    public void WriteTma(byte value) => _tma = value;

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

            if (_reloadDelay > 0 && --_reloadDelay == 0)
            {
                _tima = _tma;
                _interrupts.Request(InterruptType.Timer);
            }

            DetectTimaEdge();
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
}
