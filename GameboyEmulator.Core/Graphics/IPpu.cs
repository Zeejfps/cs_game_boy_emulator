namespace GameBoyEmulator.Core.Graphics;

public interface IPpu
{
    void WriteVram(ushort address, byte value);
    byte ReadVram(ushort address);
    void WriteOam(ushort address, byte value);
    void WriteOam(ReadOnlySpan<byte> data);
    byte ReadOam(ushort address);
    void WriteRegister(ushort address, byte value);
    byte ReadRegister(ushort address);
    PpuMode Mode { get; }
}
