namespace GameboyEmulator.Core.LR35902;

public interface IIOBus
{
    void WritePort(byte port, byte value);
    byte ReadPort(byte port);
}