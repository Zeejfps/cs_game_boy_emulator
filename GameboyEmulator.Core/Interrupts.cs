using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core;

public sealed class Interrupts : IInterruptRegisters, IInterruptBus
{
    private byte _flag;
    private byte _enable;

    public byte InterruptFlag
    {
        get => (byte)(_flag | 0xE0);
        set => _flag = value;
    }

    public byte InterruptEnable
    {
        get => _enable;
        set => _enable = value;
    }

    public void Write(InterruptType kind)
    {
        _flag |= (byte)kind;
    }
}
