using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

internal sealed class BlarggTimer : ITimer
{
    private readonly IInterruptBus _interrupts;
    private ushort _internalCounter;
    private int _timaAccumulator;
    private byte _tima;
    private byte _tma;
    private byte _tac;

    public BlarggTimer(IInterruptBus interrupts)
    {
        _interrupts = interrupts;
    }

    public void WriteDiv(byte value) => _internalCounter = 0;
    public void WriteTima(byte value) => _tima = value;
    public void WriteTma(byte value) => _tma = value;
    public void WriteTac(byte value) => _tac = value;

    public byte ReadDiv() => (byte)(_internalCounter >> 8);
    public byte ReadTima() => _tima;
    public byte ReadTma() => _tma;
    public byte ReadTac() => _tac;

    public void Tick(int tStates)
    {
        _internalCounter = (ushort)(_internalCounter + tStates);

        if ((_tac & 0x04) == 0)
            return;

        var period = (_tac & 0x03) switch
        {
            0 => 1024,
            1 => 16,
            2 => 64,
            _ => 256,
        };

        _timaAccumulator += tStates;
        while (_timaAccumulator >= period)
        {
            _timaAccumulator -= period;
            if (_tima == 0xFF)
            {
                _tima = _tma;
                _interrupts.Write(InterruptType.Timer);
            }
            else
            {
                _tima++;
            }
        }
    }
}
