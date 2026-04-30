using System.Text;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

internal sealed class BlarggSerial : ISerial
{
    private readonly IInterruptBus _interrupts;
    private readonly StringBuilder _output = new();
    private byte _data;
    private byte _control;

    public BlarggSerial(IInterruptBus interrupts)
    {
        _interrupts = interrupts;
    }

    public string Output => _output.ToString();

    public void WriteData(byte value) => _data = value;

    public void WriteControl(byte value)
    {
        _control = value;
        if (value == 0x81)
        {
            _output.Append((char)_data);
            _control = 0x01;
            _interrupts.Write(InterruptType.Serial);
        }
    }

    public byte ReadData() => _data;
    public byte ReadControl() => _control;
}
