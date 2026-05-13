using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public class HdmaControllerTests
{
    // 64 KB byte-array-backed bus so source reads and VRAM writes can be
    // verified through a single flat array. The IPpu surface aliases VRAM
    // writes back into the same Memory[0x8000+address] slot so existing
    // tests can keep asserting on _bus.Memory.
    private sealed class FlatBus : IBus, IPpu
    {
        public readonly byte[] Memory = new byte[0x10000];
        public byte Read(ushort address) => Memory[address];
        public void Write(ushort address, byte value) => Memory[address] = value;
        public void WriteVram(ushort address, byte value) => Memory[0x8000 + address] = value;
        public byte ReadVram(ushort address) => Memory[0x8000 + address];
        public void WriteOam(ushort address, byte value) { }
        public void WriteOam(ReadOnlySpan<byte> data) { }
        public byte ReadOam(ushort address) => 0xFF;
        public void WriteRegister(ushort address, byte value) { }
        public byte ReadRegister(ushort address) => 0xFF;
        public PpuMode Mode => PpuMode.HBlank;
    }

    private readonly FlatBus _bus = new();
    private readonly HdmaController _hdma;

    public HdmaControllerTests()
    {
        _hdma = new HdmaController(_bus, _bus);
    }

    [Fact]
    public void GeneralDma_CopiesRequestedBlocksImmediately()
    {
        // 32 bytes of sentinel data at 0x4000 — copy to VRAM at 0x8100.
        for (var i = 0; i < 32; i++) _bus.Memory[0x4000 + i] = (byte)(0x10 + i);

        _hdma.WriteRegister(0xFF51, 0x40); // src high
        _hdma.WriteRegister(0xFF52, 0x00); // src low
        _hdma.WriteRegister(0xFF53, 0x01); // dst high (within VRAM page)
        _hdma.WriteRegister(0xFF54, 0x00); // dst low
        // length = (1+1)*16 = 32 bytes, bit 7 = 0 → general DMA
        _hdma.WriteRegister(0xFF55, 0x01);

        for (var i = 0; i < 32; i++)
            Assert.Equal((byte)(0x10 + i), _bus.Memory[0x8100 + i]);

        // After completion HDMA5 reads 0xFF.
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF55));
        Assert.False(_hdma.IsHBlankActive);
    }

    [Fact]
    public void HBlankDma_TransfersOneBlockPerHBlank()
    {
        // 64 bytes (4 blocks) at 0x5000 → 0x8200
        for (var i = 0; i < 64; i++) _bus.Memory[0x5000 + i] = (byte)(0x80 + i);

        _hdma.WriteRegister(0xFF51, 0x50);
        _hdma.WriteRegister(0xFF52, 0x00);
        _hdma.WriteRegister(0xFF53, 0x02);
        _hdma.WriteRegister(0xFF54, 0x00);
        // length = (3+1)*16 = 64 bytes, bit 7 = 1 → H-Blank DMA
        _hdma.WriteRegister(0xFF55, 0x83);

        // Before the first HBlank, nothing in VRAM yet.
        Assert.Equal(0x00, _bus.Memory[0x8200]);
        Assert.True(_hdma.IsHBlankActive);
        // HDMA5 read: bit 7 = 0 (active), 3 blocks remaining (length-1).
        Assert.Equal(0x03, _hdma.ReadRegister(0xFF55));

        _hdma.OnHBlank();
        for (var i = 0; i < 16; i++) Assert.Equal((byte)(0x80 + i), _bus.Memory[0x8200 + i]);
        Assert.Equal(0x02, _hdma.ReadRegister(0xFF55)); // 2 blocks left, active

        _hdma.OnHBlank();
        _hdma.OnHBlank();
        _hdma.OnHBlank(); // final block

        for (var i = 0; i < 64; i++) Assert.Equal((byte)(0x80 + i), _bus.Memory[0x8200 + i]);
        Assert.False(_hdma.IsHBlankActive);
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF55));
    }

    [Fact]
    public void HBlankDma_CancellationStopsTransferAndPreservesRemaining()
    {
        // 4 blocks armed
        _hdma.WriteRegister(0xFF51, 0x60);
        _hdma.WriteRegister(0xFF52, 0x00);
        _hdma.WriteRegister(0xFF53, 0x03);
        _hdma.WriteRegister(0xFF54, 0x00);
        _hdma.WriteRegister(0xFF55, 0x83);

        // Transfer one block, then cancel.
        _hdma.OnHBlank();
        _hdma.WriteRegister(0xFF55, 0x00); // bit 7 = 0 while active → cancel

        Assert.False(_hdma.IsHBlankActive);
        // Cancelled with 48 bytes (3 blocks) remaining: bit 7 = 1, lower = 2.
        Assert.Equal(0x82, _hdma.ReadRegister(0xFF55));

        // Further HBlanks should not copy anything.
        var snapshot = _bus.Memory[0x8310]; // 16 bytes into where the *2nd* block would have landed
        _hdma.OnHBlank();
        Assert.Equal(snapshot, _bus.Memory[0x8310]);
    }

    [Fact]
    public void HDMA5_ReadIsFFWhenIdle()
    {
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF55));
    }

    [Fact]
    public void HDMA1_to_4_AlwaysReadFF()
    {
        _hdma.WriteRegister(0xFF51, 0x12);
        _hdma.WriteRegister(0xFF52, 0x34);
        _hdma.WriteRegister(0xFF53, 0x56);
        _hdma.WriteRegister(0xFF54, 0x78);
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF51));
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF52));
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF53));
        Assert.Equal(0xFF, _hdma.ReadRegister(0xFF54));
    }

    // HDMA is the bus master — its writes to VRAM are not subject to the
    // CPU-side mode-3 lockout. Wire HDMA up to a real MMU+PPU and verify a
    // general transfer triggered while the PPU is in Drawing mode still
    // lands its bytes in VRAM. Without the bypass, _bus.Write at 0x8000-0x9FFF
    // silently drops while PPU.Mode == Drawing.
    [Fact]
    public void GeneralDma_BypassesVramLockoutDuringDrawingMode()
    {
        var interrupts = new Interrupts();
        var ppu = new Graphics.Ppu(interrupts);
        var mbc = new MmuTests_HdmaFixture.NoopMbc();
        var mmu = new Mmu(
            mbc,
            ppu,
            new MmuTests_HdmaFixture.NoopJoypad(),
            new MmuTests_HdmaFixture.NoopTimer(),
            new MmuTests_HdmaFixture.NoopApu(),
            new MmuTests_HdmaFixture.NoopSerial(),
            interrupts);
        mmu.SetCgbMode(true);
        var hdma = new HdmaController(mmu, ppu);
        mmu.SetHdmaController(hdma);

        // Seed 16 bytes of source data into WRAM (always readable).
        for (var i = 0; i < 16; i++)
            mmu.Write((ushort)(0xC000 + i), (byte)(0xA0 + i));

        // Drive the PPU into Drawing mode. LCD must be enabled (it is via
        // Ppu default LCDC=0x91 path in tests that touch WriteRegister; we
        // re-enable explicitly to be safe), then step past OAM scan.
        ppu.WriteRegister(0xFF40, 0x91);
        ppu.Step(85); // > 80 dots: past OAM scan, into Drawing
        Assert.Equal(Graphics.PpuMode.Drawing, ppu.Mode);

        // Trigger a 16-byte general DMA from WRAM into VRAM at 0x8000.
        mmu.Write(0xFF51, 0xC0); // src high
        mmu.Write(0xFF52, 0x00); // src low
        mmu.Write(0xFF53, 0x00); // dst high (VRAM offset 0)
        mmu.Write(0xFF54, 0x00); // dst low
        mmu.Write(0xFF55, 0x00); // length 1 block (16 bytes), bit 7 = 0

        // After the transfer, the destination bytes must be present in
        // PPU VRAM bank 0 even though the PPU was in Drawing mode.
        for (var i = 0; i < 16; i++)
            Assert.Equal((byte)(0xA0 + i), ppu.ReadVram((ushort)i));
    }
}

internal static class MmuTests_HdmaFixture
{
    internal sealed class NoopMbc : Cartridge.IMbc
    {
        public void WriteBank0(ushort a, byte v) { }
        public void WriteBankN(ushort a, byte v) { }
        public void WriteExternalRam(ushort a, byte v) { }
        public byte ReadBank0(ushort a) => 0xFF;
        public byte ReadBankN(ushort a) => 0xFF;
        public byte ReadExternalRam(ushort a) => 0xFF;
        public void Flush() { }
    }
    internal sealed class NoopJoypad : IJoypad
    {
        public byte Read() => 0xFF;
        public void Select(byte v) { }
        public void SetButton(JoypadButton b, bool p) { }
        public void Reset() { }
    }
    internal sealed class NoopTimer : ITimer
    {
        public byte ReadDiv() => 0;
        public byte ReadTima() => 0;
        public byte ReadTma() => 0;
        public byte ReadTac() => 0;
        public void WriteDiv(byte v) { }
        public void WriteTima(byte v) { }
        public void WriteTma(byte v) { }
        public void WriteTac(byte v) { }
    }
    internal sealed class NoopApu : IApu
    {
        public byte ReadRegister(ushort a) => 0xFF;
        public void WriteRegister(ushort a, byte v) { }
        public void Step(int t) { }
        public void OnFrameSequencerTick() { }
        public int DrainAudio(Span<float> dest) => 0;
    }
    internal sealed class NoopSerial : ISerial
    {
        public byte ReadData() => 0xFF;
        public byte ReadControl() => 0xFF;
        public void WriteData(byte v) { }
        public void WriteControl(byte v) { }
    }
}
