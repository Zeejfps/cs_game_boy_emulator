namespace GameBoyEmulator.Core.Graphics;

public interface IPpu
{
    void WriteVram(ushort address, byte value);
    byte ReadVram(ushort address);
    ReadOnlySpan<byte> ReadVramRange(ushort address, int length);
    void WriteOam(ushort address, byte value);
    void WriteOam(ReadOnlySpan<byte> data);
    byte ReadOam(ushort address);
    void WriteRegister(ushort address, byte value);
    byte ReadRegister(ushort address);
}
