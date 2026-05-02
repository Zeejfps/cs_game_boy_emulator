using GameBoyEmulator.Core.Cartridge;

namespace GameBoyEmulator.Core.Tests;

internal sealed class BlarggMbc : IMbc
{
    private readonly byte[] _rom;

    public BlarggMbc(byte[] rom)
    {
        if (rom.Length != 0x8000)
            throw new ArgumentException(
                $"BlarggMbc requires a 32 KB ROM-only image; got {rom.Length} bytes.",
                nameof(rom));
        _rom = rom;
    }

    public void WriteBank0(ushort address, byte value) { }
    public void WriteBankN(ushort address, byte value) { }
    public void WriteExternalRam(ushort address, byte value) { }

    public byte ReadBank0(ushort address) => _rom[address];
    public byte ReadBankN(ushort address) => _rom[address];
    public byte ReadExternalRam(ushort address) => 0xFF;

    public void Flush() { }
}
