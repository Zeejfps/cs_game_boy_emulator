namespace GameBoyEmulator.Core;

public interface IMbc
{
    void WriteBank0(ushort address, byte value);
    void WriteBankN(ushort address, byte value);
    byte ReadBank0(ushort address);
    byte ReadBankN(ushort address);
    ReadOnlySpan<byte> ReadBank0Range(ushort address, int length);
    ReadOnlySpan<byte> ReadBankNRange(ushort address, int length);
    void WriteExternalRam(ushort address, byte value);
    byte ReadExternalRam(ushort address);
    ReadOnlySpan<byte> ReadExternalRamRange(ushort address, int length);
}
