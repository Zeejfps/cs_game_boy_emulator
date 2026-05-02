namespace GameBoyEmulator.Core.Cartridge;

public interface IMbc
{
    void WriteBank0(ushort address, byte value);
    void WriteBankN(ushort address, byte value);
    byte ReadBank0(ushort address);
    byte ReadBankN(ushort address);
    void WriteExternalRam(ushort address, byte value);
    byte ReadExternalRam(ushort address);
    void Flush();
}
