namespace GameboyEmulator.Core;

public interface IApu
{
    void WriteRegister(ushort address, byte value);
    byte ReadRegister(ushort address);
}