namespace GameboyEmulator.Core;

public interface IPpu
{
    void WriteVram(ushort address, byte value);
    byte ReadVram(ushort address);
    void WriteOam(ushort address, byte value);
    byte ReadOam(ushort address);
    void WriteRegister(ushort address, byte value);
    byte ReadRegister(ushort address);
}
