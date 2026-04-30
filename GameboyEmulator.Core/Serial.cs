using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class Serial : ISerial
{
    private readonly IInterrupts _interrupts;
    private byte _data;
    private byte _control;

    public Serial(IInterrupts interrupts)
    {
        _interrupts = interrupts;
    }

    public void WriteData(byte value) => _data = value;

    public void WriteControl(byte value)
    {
        _control = value;
        if (value == 0x81)
        {
            Console.Write((char)_data);
            _data = 0xFF;
            _control = 0x01;
            _interrupts.Request(InterruptType.Serial);
        }
    }

    public byte ReadData() => _data;
    public byte ReadControl() => (byte)(_control | 0x7E);
}
