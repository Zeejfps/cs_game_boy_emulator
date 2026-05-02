using GameBoyEmulator.Core.Graphics;

namespace GameBoyEmulator.Core.Tests;

public class MmuTests
{
    private readonly FakeMbc _mbc = new();
    private readonly FakePpu _ppu = new();
    private readonly FakeJoypad _joypad = new();
    private readonly FakeTimer _timer = new();
    private readonly FakeApu _apu = new();
    private readonly FakeSerial _serial = new();
    private readonly Interrupts _interrupts = new();
    private readonly Mmu _mmu;

    public MmuTests()
    {
        _mmu = new Mmu(_mbc, _ppu, _joypad, _timer, _apu, _serial, _interrupts);
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

    [Fact]
    public void Write_Vram_BlockedDuringPpuDrawing()
    {
        _ppu.Mode = PpuMode.Drawing;
        _mmu.Write(0x8000, 0x42);
        Assert.Null(_ppu.LastVramWrite);
    }

    [Fact]
    public void Read_Vram_ReturnsFFDuringPpuDrawing()
    {
        _ppu.Mode = PpuMode.Drawing;
        _ppu.VramReadStub = _ => 0x42;
        Assert.Equal(0xFF, _mmu.Read(0x8000));
    }

    [Theory]
    [InlineData(PpuMode.OamScan)]
    [InlineData(PpuMode.Drawing)]
    public void Write_Oam_BlockedDuringPpuOamScanOrDrawing(PpuMode mode)
    {
        _ppu.Mode = mode;
        _mmu.Write(0xFE00, 0x77);
        Assert.Null(_ppu.LastOamWrite);
    }

    [Theory]
    [InlineData(PpuMode.OamScan)]
    [InlineData(PpuMode.Drawing)]
    public void Read_Oam_ReturnsFFDuringPpuOamScanOrDrawing(PpuMode mode)
    {
        _ppu.Mode = mode;
        _ppu.OamReadStub = _ => 0x77;
        Assert.Equal(0xFF, _mmu.Read(0xFE00));
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
    public void Write_JoypadRegister_DispatchesToJoypad()
    {
        _mmu.Write(0xFF00, 0x20);

        Assert.Equal((byte)0x20, _joypad.LastSelect);
    }

    [Fact]
    public void Write_TimerDiv_DispatchesToTimer()
    {
        _mmu.Write(0xFF04, 0xAB);

        Assert.Equal((byte)0xAB, _timer.LastDivWrite);
        Assert.Null(_timer.LastTimaWrite);
        Assert.Null(_timer.LastTmaWrite);
        Assert.Null(_timer.LastTacWrite);
    }

    [Fact]
    public void Write_TimerTima_DispatchesToTimer()
    {
        _mmu.Write(0xFF05, 0xCD);

        Assert.Equal((byte)0xCD, _timer.LastTimaWrite);
        Assert.Null(_timer.LastDivWrite);
        Assert.Null(_timer.LastTmaWrite);
        Assert.Null(_timer.LastTacWrite);
    }

    [Fact]
    public void Write_TimerTma_DispatchesToTimer()
    {
        _mmu.Write(0xFF06, 0xEF);

        Assert.Equal((byte)0xEF, _timer.LastTmaWrite);
        Assert.Null(_timer.LastDivWrite);
        Assert.Null(_timer.LastTimaWrite);
        Assert.Null(_timer.LastTacWrite);
    }

    [Fact]
    public void Write_TimerTac_DispatchesToTimer()
    {
        _mmu.Write(0xFF07, 0x07);

        Assert.Equal((byte)0x07, _timer.LastTacWrite);
        Assert.Null(_timer.LastDivWrite);
        Assert.Null(_timer.LastTimaWrite);
        Assert.Null(_timer.LastTmaWrite);
    }

    [Theory]
    [InlineData((ushort)0x0000)]
    [InlineData((ushort)0x1234)]
    [InlineData((ushort)0x3FFF)]
    public void Read_RomBank0_DispatchesToMbcWithOriginalAddress(ushort address)
    {
        _mbc.Bank0ReadStub = _ => 0xAB;

        var result = _mmu.Read(address);

        Assert.Equal(address, _mbc.LastBank0ReadAddress);
        Assert.Equal(0xAB, result);
    }

    [Theory]
    [InlineData((ushort)0x4000)]
    [InlineData((ushort)0x5555)]
    [InlineData((ushort)0x7FFF)]
    public void Read_RomBankN_DispatchesToMbcWithOriginalAddress(ushort address)
    {
        _mbc.BankNReadStub = _ => 0xCD;

        var result = _mmu.Read(address);

        Assert.Equal(address, _mbc.LastBankNReadAddress);
        Assert.Equal(0xCD, result);
    }

    [Theory]
    [InlineData((ushort)0x8000, (ushort)0x0000)]
    [InlineData((ushort)0x8123, (ushort)0x0123)]
    [InlineData((ushort)0x9FFF, (ushort)0x1FFF)]
    public void Read_VRam_DispatchesToPpuWithOffsetAddress(ushort address, ushort expectedOffset)
    {
        _ppu.VramReadStub = _ => 0x42;

        var result = _mmu.Read(address);

        Assert.Equal(expectedOffset, _ppu.LastVramReadAddress);
        Assert.Equal(0x42, result);
    }

    [Theory]
    [InlineData((ushort)0xA000, (ushort)0x0000)]
    [InlineData((ushort)0xABCD, (ushort)0x0BCD)]
    [InlineData((ushort)0xBFFF, (ushort)0x1FFF)]
    public void Read_ExternalRam_DispatchesToMbcWithOffsetAddress(ushort address, ushort expectedOffset)
    {
        _mbc.ExternalRamReadStub = _ => 0x99;

        var result = _mmu.Read(address);

        Assert.Equal(expectedOffset, _mbc.LastExternalRamReadAddress);
        Assert.Equal(0x99, result);
    }

    [Theory]
    [InlineData((ushort)0xFE00, (ushort)0x0000)]
    [InlineData((ushort)0xFE50, (ushort)0x0050)]
    [InlineData((ushort)0xFE9F, (ushort)0x009F)]
    public void Read_Oam_DispatchesToPpuWithOffsetAddress(ushort address, ushort expectedOffset)
    {
        _ppu.OamReadStub = _ => 0x77;

        var result = _mmu.Read(address);

        Assert.Equal(expectedOffset, _ppu.LastOamReadAddress);
        Assert.Equal(0x77, result);
    }

    [Theory]
    [InlineData((ushort)0xC000)]
    [InlineData((ushort)0xCDEF)]
    [InlineData((ushort)0xDFFF)]
    public void Read_WRam_RoundTripsValueWritten(ushort address)
    {
        _mmu.Write(address, 0x11);

        Assert.Equal(0x11, _mmu.Read(address));
    }

    [Theory]
    [InlineData((ushort)0xE000, (ushort)0xC000)]
    [InlineData((ushort)0xEFAB, (ushort)0xCFAB)]
    [InlineData((ushort)0xFDFF, (ushort)0xDDFF)]
    public void Read_EchoRam_ReturnsValueWrittenToCorrespondingWRamAddress(ushort echoAddress, ushort wramAddress)
    {
        _mmu.Write(wramAddress, 0x55);

        Assert.Equal(0x55, _mmu.Read(echoAddress));
    }

    [Theory]
    [InlineData((ushort)0xFEA0)]
    [InlineData((ushort)0xFED0)]
    [InlineData((ushort)0xFEFF)]
    public void Read_UnusableRegion_ReturnsFF(ushort address)
    {
        Assert.Equal(0xFF, _mmu.Read(address));
    }

    [Theory]
    [InlineData((ushort)0xFF80)]
    [InlineData((ushort)0xFFC0)]
    [InlineData((ushort)0xFFFE)]
    public void Read_HRam_RoundTripsValueWritten(ushort address)
    {
        _mmu.Write(address, 0x44);

        Assert.Equal(0x44, _mmu.Read(address));
    }

    [Fact]
    public void Read_InterruptEnable_RoundTripsValueWritten()
    {
        _mmu.Write(0xFFFF, 0x1F);

        Assert.Equal(0x1F, _mmu.Read(0xFFFF));
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

        public ushort? LastBank0ReadAddress { get; private set; }
        public ushort? LastBankNReadAddress { get; private set; }
        public ushort? LastExternalRamReadAddress { get; private set; }

        public Func<ushort, byte> Bank0ReadStub { get; set; } = _ => 0;
        public Func<ushort, byte> BankNReadStub { get; set; } = _ => 0;
        public Func<ushort, byte> ExternalRamReadStub { get; set; } = _ => 0;

        public void WriteBank0(ushort address, byte value) => LastBank0Write = (address, value);
        public void WriteBankN(ushort address, byte value) => LastBankNWrite = (address, value);
        public void WriteExternalRam(ushort address, byte value) => LastExternalRamWrite = (address, value);

        public byte ReadBank0(ushort address) { LastBank0ReadAddress = address; return Bank0ReadStub(address); }
        public byte ReadBankN(ushort address) { LastBankNReadAddress = address; return BankNReadStub(address); }
        public byte ReadExternalRam(ushort address) { LastExternalRamReadAddress = address; return ExternalRamReadStub(address); }

        public void Flush() { }
    }

    private sealed class FakeJoypad : IJoypad
    {
        public byte? LastSelect { get; private set; }
        public byte ReadStub { get; set; }
        public void Select(byte value) => LastSelect = value;
        public byte Read() => ReadStub;
        public void SetButton(JoypadButton button, bool pressed) { }
        public void Reset() { }
    }

    private sealed class FakeApu : IApu
    {
        public (ushort address, byte value)? LastRegisterWrite { get; private set; }
        public void WriteRegister(ushort address, byte value) => LastRegisterWrite = (address, value);
        public byte ReadRegister(ushort address) => 0;
    }

    private sealed class FakeTimer : ITimer
    {
        public byte? LastDivWrite { get; private set; }
        public byte? LastTimaWrite { get; private set; }
        public byte? LastTmaWrite { get; private set; }
        public byte? LastTacWrite { get; private set; }

        public byte DivReadStub { get; set; }
        public byte TimaReadStub { get; set; }
        public byte TmaReadStub { get; set; }
        public byte TacReadStub { get; set; }

        public void WriteDiv(byte value) => LastDivWrite = value;
        public void WriteTima(byte value) => LastTimaWrite = value;
        public void WriteTma(byte value) => LastTmaWrite = value;
        public void WriteTac(byte value) => LastTacWrite = value;

        public byte ReadDiv() => DivReadStub;
        public byte ReadTima() => TimaReadStub;
        public byte ReadTma() => TmaReadStub;
        public byte ReadTac() => TacReadStub;
    }

    private sealed class FakeSerial : ISerial
    {
        public byte? LastDataWrite { get; private set; }
        public byte? LastControlWrite { get; private set; }
        public void WriteData(byte value) => LastDataWrite = value;
        public void WriteControl(byte value) => LastControlWrite = value;
        public byte ReadData() => 0xFF;
        public byte ReadControl() => 0xFF;
    }

    private sealed class FakePpu : IPpu
    {
        public List<(ushort address, byte value)> VramWrites { get; } = new();
        public List<(ushort address, byte value)> OamWrites { get; } = new();

        public (ushort address, byte value)? LastVramWrite => VramWrites.Count == 0 ? null : VramWrites[^1];
        public (ushort address, byte value)? LastOamWrite => OamWrites.Count == 0 ? null : OamWrites[^1];

        public ushort? LastVramReadAddress { get; private set; }
        public ushort? LastOamReadAddress { get; private set; }

        public Func<ushort, byte> VramReadStub { get; set; } = _ => 0;
        public Func<ushort, byte> OamReadStub { get; set; } = _ => 0;

        public void WriteVram(ushort address, byte value) => VramWrites.Add((address, value));
        public void WriteOam(ushort address, byte value) => OamWrites.Add((address, value));
        public void WriteOam(ReadOnlySpan<byte> data) => LastOamBulkWrite = data.ToArray();
        public void WriteRegister(ushort address, byte value) { }

        public byte[]? LastOamBulkWrite { get; private set; }

        public byte ReadVram(ushort address) { LastVramReadAddress = address; return VramReadStub(address); }
        public byte ReadOam(ushort address) { LastOamReadAddress = address; return OamReadStub(address); }
        public byte ReadRegister(ushort address) => 0;
        public PpuMode Mode { get; set; } = PpuMode.HBlank;
    }
}
