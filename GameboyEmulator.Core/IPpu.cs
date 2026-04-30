namespace GameboyEmulator.Core;

public interface IPpu
{
    void Write(ushort address, byte value);
    byte Read(ushort address);
}