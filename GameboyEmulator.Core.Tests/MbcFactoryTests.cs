using GameBoyEmulator.Core.Cartridge;

namespace GameBoyEmulator.Core.Tests;

public class MbcFactoryTests
{
    [Theory]
    [InlineData(0x80, true)]    // DMG-compatible CGB cart
    [InlineData(0xC0, true)]    // CGB-only cart
    [InlineData(0x00, false)]   // DMG-only cart
    [InlineData(0x40, false)]   // bit 7 clear — not a CGB indicator
    public void IsCgbCartridge_ReadsFlagAtOffset0x143(byte flagByte, bool expected)
    {
        var rom = new byte[0x8000];
        rom[0x0143] = flagByte;

        Assert.Equal(expected, MbcFactory.IsCgbCartridge(rom));
    }

    [Fact]
    public void IsCgbCartridge_ReturnsFalseForUndersizedRom()
    {
        var rom = new byte[0x100];
        Assert.False(MbcFactory.IsCgbCartridge(rom));
    }
}
