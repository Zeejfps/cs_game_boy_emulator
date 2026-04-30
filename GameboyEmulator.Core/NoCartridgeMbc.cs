namespace GameBoyEmulator.Core;

public sealed class NoCartridgeMbc : IMbc
{
    private static readonly byte[] OpenBus = CreateOpenBus();

    public void WriteBank0(ushort address, byte value) { }
    public void WriteBankN(ushort address, byte value) { }
    public void WriteExternalRam(ushort address, byte value) { }

    public byte ReadBank0(ushort address) => 0xFF;
    public byte ReadBankN(ushort address) => 0xFF;
    public byte ReadExternalRam(ushort address) => 0xFF;

    public ReadOnlySpan<byte> ReadBank0Range(ushort address, int length) => OpenBus.AsSpan(0, length);
    public ReadOnlySpan<byte> ReadBankNRange(ushort address, int length) => OpenBus.AsSpan(0, length);
    public ReadOnlySpan<byte> ReadExternalRamRange(ushort address, int length) => OpenBus.AsSpan(0, length);

    private static byte[] CreateOpenBus()
    {
        var buffer = new byte[0xA0];
        Array.Fill(buffer, (byte)0xFF);
        return buffer;
    }
}
