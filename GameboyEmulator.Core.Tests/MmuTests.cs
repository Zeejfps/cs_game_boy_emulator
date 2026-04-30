namespace GameboyEmulator.Core.Tests;

public class MmuTests
{
    private readonly FakeMbc _mbc = new();
    private readonly FakePpu _ppu = new();
    private readonly Mmu _mmu;

    public MmuTests()
    {
        _mmu = new Mmu(_mbc, _ppu);
    }

    [Theory]
    [InlineData((ushort)0x0000)]
    [InlineData((ushort)0x1234)]
    [InlineData((ushort)0x3FFF)]
    public void Write_RomBank0_DispatchesToMbcWithOriginalAddress(ushort address)
    {
        _mmu.Write(address, 0xAB);

        Assert.Equal((address, (byte)0xAB), _mbc.LastBank0Write);
        Assert.Null(_mbc.LastBankNWrite);
        Assert.Null(_mbc.LastExternalRamWrite);
    }

    [Theory]
    [InlineData((ushort)0x4000)]
    [InlineData((ushort)0x5555)]
    [InlineData((ushort)0x7FFF)]
    public void Write_RomBankN_DispatchesToMbcWithOriginalAddress(ushort address)
    {
        _mmu.Write(address, 0xCD);

        Assert.Equal((address, (byte)0xCD), _mbc.LastBankNWrite);
        Assert.Null(_mbc.LastBank0Write);
    }

    [Theory]
    [InlineData((ushort)0x8000, (ushort)0x0000)]
    [InlineData((ushort)0x8123, (ushort)0x0123)]
    [InlineData((ushort)0x9FFF, (ushort)0x1FFF)]
    public void Write_VRam_DispatchesToPpuWithOffsetAddress(ushort address, ushort expectedOffset)
    {
        _mmu.Write(address, 0x42);

        Assert.Equal((expectedOffset, (byte)0x42), _ppu.LastVramWrite);
        Assert.Null(_ppu.LastOamWrite);
    }

    [Theory]
    [InlineData((ushort)0xA000, (ushort)0x0000)]
    [InlineData((ushort)0xABCD, (ushort)0x0BCD)]
    [InlineData((ushort)0xBFFF, (ushort)0x1FFF)]
    public void Write_ExternalRam_DispatchesToMbcWithOffsetAddress(ushort address, ushort expectedOffset)
    {
        _mmu.Write(address, 0x99);

        Assert.Equal((expectedOffset, (byte)0x99), _mbc.LastExternalRamWrite);
    }

    [Theory]
    [InlineData((ushort)0xFE00, (ushort)0x0000)]
    [InlineData((ushort)0xFE50, (ushort)0x0050)]
    [InlineData((ushort)0xFE9F, (ushort)0x009F)]
    public void Write_Oam_DispatchesToPpuWithOffsetAddress(ushort address, ushort expectedOffset)
    {
        _mmu.Write(address, 0x77);

        Assert.Equal((expectedOffset, (byte)0x77), _ppu.LastOamWrite);
        Assert.Null(_ppu.LastVramWrite);
    }

    [Theory]
    [InlineData((ushort)0xC000)]
    [InlineData((ushort)0xCDEF)]
    [InlineData((ushort)0xDFFF)]
    public void Write_WRam_DoesNotDispatchToMbcOrPpu(ushort address)
    {
        _mmu.Write(address, 0x11);

        AssertNoExternalDispatch();
    }

    [Theory]
    [InlineData((ushort)0xE000)]
    [InlineData((ushort)0xEFAB)]
    [InlineData((ushort)0xFDFF)]
    public void Write_EchoRam_DoesNotDispatchToMbcOrPpu(ushort address)
    {
        _mmu.Write(address, 0x22);

        AssertNoExternalDispatch();
    }

    [Theory]
    [InlineData((ushort)0xFEA0)]
    [InlineData((ushort)0xFED0)]
    [InlineData((ushort)0xFEFF)]
    public void Write_UnusableRegion_IsSilentlyIgnored(ushort address)
    {
        _mmu.Write(address, 0xFF);

        AssertNoExternalDispatch();
    }

    [Theory]
    [InlineData((ushort)0xFF00)]
    [InlineData((ushort)0xFF40)]
    [InlineData((ushort)0xFF7F)]
    public void Write_IoRegisters_DoesNotDispatchToMbcOrPpu(ushort address)
    {
        _mmu.Write(address, 0x33);

        AssertNoExternalDispatch();
    }

    [Theory]
    [InlineData((ushort)0xFF80)]
    [InlineData((ushort)0xFFC0)]
    [InlineData((ushort)0xFFFE)]
    public void Write_HRam_DoesNotDispatchToMbcOrPpu(ushort address)
    {
        _mmu.Write(address, 0x44);

        AssertNoExternalDispatch();
    }

    [Fact]
    public void Write_InterruptEnable_DoesNotDispatchToMbcOrPpu()
    {
        _mmu.Write(0xFFFF, 0x1F);

        AssertNoExternalDispatch();
    }

    [Fact]
    public void WriteWord_WritesLittleEndian()
    {
        _mmu.WriteWord(0x8000, 0xBEEF);

        Assert.Equal(2, _ppu.VramWrites.Count);
        Assert.Equal(((ushort)0x0000, (byte)0xEF), _ppu.VramWrites[0]);
        Assert.Equal(((ushort)0x0001, (byte)0xBE), _ppu.VramWrites[1]);
    }

    [Fact]
    public void WriteWord_AcrossRegionBoundary_DispatchesEachByteSeparately()
    {
        _mmu.WriteWord(0x7FFF, 0xBEEF);

        Assert.Equal(((ushort)0x7FFF, (byte)0xEF), _mbc.LastBankNWrite);
        Assert.Equal(((ushort)0x0000, (byte)0xBE), _ppu.LastVramWrite);
    }

    private void AssertNoExternalDispatch()
    {
        Assert.Null(_mbc.LastBank0Write);
        Assert.Null(_mbc.LastBankNWrite);
        Assert.Null(_mbc.LastExternalRamWrite);
        Assert.Null(_ppu.LastVramWrite);
        Assert.Null(_ppu.LastOamWrite);
    }

    private sealed class FakeMbc : IMbc
    {
        public (ushort address, byte value)? LastBank0Write { get; private set; }
        public (ushort address, byte value)? LastBankNWrite { get; private set; }
        public (ushort address, byte value)? LastExternalRamWrite { get; private set; }

        public void WriteBank0(ushort address, byte value) => LastBank0Write = (address, value);
        public void WriteBankN(ushort address, byte value) => LastBankNWrite = (address, value);
        public void WriteExternalRam(ushort address, byte value) => LastExternalRamWrite = (address, value);

        public byte ReadBank0(ushort address) => 0;
        public byte ReadBankN(ushort address) => 0;
        public byte ReadExternalRam(ushort address) => 0;
    }

    private sealed class FakePpu : IPpu
    {
        public List<(ushort address, byte value)> VramWrites { get; } = new();
        public List<(ushort address, byte value)> OamWrites { get; } = new();

        public (ushort address, byte value)? LastVramWrite => VramWrites.Count == 0 ? null : VramWrites[^1];
        public (ushort address, byte value)? LastOamWrite => OamWrites.Count == 0 ? null : OamWrites[^1];

        public void WriteVram(ushort address, byte value) => VramWrites.Add((address, value));
        public void WriteOam(ushort address, byte value) => OamWrites.Add((address, value));
        public void WriteRegister(ushort address, byte value) { }

        public byte ReadVram(ushort address) => 0;
        public byte ReadOam(ushort address) => 0;
        public byte ReadRegister(ushort address) => 0;
    }
}
