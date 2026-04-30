namespace GameBoyEmulator.Core;

public static class MbcFactory
{
    private const int CartTypeAddress = 0x0147;

    public static IMbc Create(byte[] rom)
    {
        if (rom.Length < 0x0150)
            throw new ArgumentException("ROM is smaller than the cartridge header", nameof(rom));

        var cartType = rom[CartTypeAddress];
        return cartType switch
        {
            0x00 => new RomOnlyMbc(rom),
            _ => throw new NotSupportedException($"Cartridge type 0x{cartType:X2} is not supported")
        };
    }
}
