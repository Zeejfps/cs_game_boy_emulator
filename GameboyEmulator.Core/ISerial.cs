namespace GameboyEmulator.Core;

public interface ISerial
{
    void WriteData(byte value);
    void WriteControl(byte value);
    byte ReadData();
    byte ReadControl();
}
