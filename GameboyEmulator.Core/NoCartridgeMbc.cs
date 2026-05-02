namespace GameBoyEmulator.Core;

public sealed class NoCartridgeMbc : IMbc
{
    public void WriteBank0(ushort address, byte value) { }
    public void WriteBankN(ushort address, byte value) { }
    public void WriteExternalRam(ushort address, byte value) { }

    public byte ReadBank0(ushort address) => 0xFF;
    public byte ReadBankN(ushort address) => 0xFF;
    public byte ReadExternalRam(ushort address) => 0xFF;

    public void Flush() { }
}
