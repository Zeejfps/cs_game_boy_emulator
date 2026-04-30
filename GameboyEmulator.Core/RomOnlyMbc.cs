namespace GameBoyEmulator.Core;

public sealed class RomOnlyMbc : IMbc
{
    private const int RomSize = 0x8000;

    private readonly byte[] _rom;

    public RomOnlyMbc(byte[] rom)
    {
        if (rom.Length != RomSize)
            throw new ArgumentException($"ROM-only cartridges must be {RomSize} bytes, got {rom.Length}", nameof(rom));
        _rom = rom;
    }

    public void WriteBank0(ushort address, byte value) { }
    public void WriteBankN(ushort address, byte value) { }
    public void WriteExternalRam(ushort address, byte value) { }

    public byte ReadBank0(ushort address) => _rom[address];
    public byte ReadBankN(ushort address) => _rom[address];
    public byte ReadExternalRam(ushort address) => 0xFF;

    public ReadOnlySpan<byte> ReadBank0Range(ushort address, int length) => _rom.AsSpan(address, length);
    public ReadOnlySpan<byte> ReadBankNRange(ushort address, int length) => _rom.AsSpan(address, length);
    public ReadOnlySpan<byte> ReadExternalRamRange(ushort address, int length) => ReadOnlySpan<byte>.Empty;
}
