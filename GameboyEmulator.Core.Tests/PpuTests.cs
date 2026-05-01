using GameBoyEmulator.Core.Graphics;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public class PpuTests
{
    private const ushort LCDC = 0xFF40;
    private const ushort STAT = 0xFF41;
    private const ushort SCY  = 0xFF42;
    private const ushort SCX  = 0xFF43;
    private const ushort LY   = 0xFF44;
    private const ushort LYC  = 0xFF45;
    private const ushort BGP  = 0xFF47;

    private const byte LcdOn   = 0x80;
    private const byte BgOn    = 0x01;
    private const byte UnsignedTileData = 0x10; // LCDC bit 4
    private const byte IdentityBgp      = 0xE4; // 0→0 1→1 2→2 3→3

    private const int DotsPerLine = 456;

    private sealed class FakeInterruptBus : IInterrupts
    {
        private InterruptType _requested;
        private InterruptType _enabled;

        public int VBlankCount  { get; private set; }
        public int LcdStatCount { get; private set; }

        public void Request(InterruptType kind)
        {
            _requested |= kind;
            if ((kind & InterruptType.VBlank)  != 0) VBlankCount++;
            if ((kind & InterruptType.LcdStat) != 0) LcdStatCount++;
        }

        public void Clear(InterruptType kind) => _requested &= ~kind;
        public bool IsRequested(InterruptType kind) => (_requested & kind) != 0;
        public InterruptType GetPending() => _requested & _enabled;

        public InterruptType ReadRequestedInterrupts() => _requested;
        public void WriteRequestedInterrupts(InterruptType v) => _requested = v;
        public InterruptType ReadEnabledInterrupts() => _enabled;
        public void WriteEnabledInterrupts(InterruptType v) => _enabled = v;
    }

    private readonly FakeInterruptBus _interrupts = new();
    private readonly Ppu _ppu;

    public PpuTests()
    {
        _ppu = new Ppu(_interrupts);
        _ppu.WriteRegister(LCDC, LcdOn);
    }

    private static PpuMode Mode(byte stat) => (PpuMode)(stat & 0x03);

    // ──────────────────────────── Mode timing ────────────────────────────

    [Fact]
    public void ScanlineModes_FollowOamScanThenDrawingThenHBlank()
    {
        // Advance to start of line 1 so we begin in OamScan cleanly.
        _ppu.Step(DotsPerLine);
        Assert.Equal(PpuMode.OamScan, Mode(_ppu.ReadRegister(STAT)));

        _ppu.Step(80);
        Assert.Equal(PpuMode.Drawing, Mode(_ppu.ReadRegister(STAT)));

        _ppu.Step(172);
        Assert.Equal(PpuMode.HBlank, Mode(_ppu.ReadRegister(STAT)));
    }

    [Fact]
    public void Ly_AdvancesEachScanline()
    {
        _ppu.Step(DotsPerLine);
        Assert.Equal(1, _ppu.ReadRegister(LY));

        _ppu.Step(DotsPerLine * 5);
        Assert.Equal(6, _ppu.ReadRegister(LY));
    }

    [Fact]
    public void EnteringLine144_FiresVBlankAndSetsVBlankMode()
    {
        _ppu.Step(DotsPerLine * 144);

        Assert.Equal(144, _ppu.ReadRegister(LY));
        Assert.Equal(PpuMode.VBlank, Mode(_ppu.ReadRegister(STAT)));
        Assert.Equal(1, _interrupts.VBlankCount);
    }

    [Fact]
    public void Frame_WrapsBackToZeroAfter154Lines()
    {
        _ppu.Step(DotsPerLine * 154);

        Assert.Equal(0, _ppu.ReadRegister(LY));
        Assert.Equal(PpuMode.OamScan, Mode(_ppu.ReadRegister(STAT)));
        Assert.Equal(1, _interrupts.VBlankCount);
    }

    [Fact]
    public void TickIsNoOpWhenLcdOff()
    {
        _ppu.WriteRegister(LCDC, 0x00);

        _ppu.Step(DotsPerLine * 200);

        Assert.Equal(0, _ppu.ReadRegister(LY));
        Assert.Equal(0, _interrupts.VBlankCount);
    }

    [Fact]
    public void TurningLcdOff_ResetsLyAndMode()
    {
        _ppu.Step(DotsPerLine * 50 + 100);
        Assert.NotEqual(0, _ppu.ReadRegister(LY));

        _ppu.WriteRegister(LCDC, 0x00);

        Assert.Equal(0, _ppu.ReadRegister(LY));
        Assert.Equal(PpuMode.HBlank, Mode(_ppu.ReadRegister(STAT)));
    }

    // ──────────────────────────── STAT register ──────────────────────────

    [Fact]
    public void Stat_Bit7AlwaysReadsOne()
    {
        _ppu.WriteRegister(STAT, 0x00);

        Assert.Equal(0x80, _ppu.ReadRegister(STAT) & 0x80);
    }

    [Fact]
    public void Stat_OnlyAllowsWritingInterruptSourceBits()
    {
        // Drop LCD off so we're in HBlank (mode 0) for the live-state assertion below.
        _ppu.WriteRegister(LCDC, 0x00);
        _ppu.WriteRegister(STAT, 0xFF);

        // Bits 3-6 latch; bits 0-2 reflect live state (mode 0, LY==LYC since both 0); bit 7 reads 1.
        var stat = _ppu.ReadRegister(STAT);
        Assert.Equal(0x78, stat & 0x78); // sources retained
        Assert.Equal(0x00, stat & 0x03); // mode bits not from write
    }

    [Fact]
    public void Stat_LycCoincidenceFlagReflectsLyEqualsLyc()
    {
        _ppu.WriteRegister(LYC, 0);
        Assert.Equal(0x04, _ppu.ReadRegister(STAT) & 0x04);

        _ppu.WriteRegister(LYC, 5);
        Assert.Equal(0x00, _ppu.ReadRegister(STAT) & 0x04);

        _ppu.Step(DotsPerLine * 5);
        Assert.Equal(0x04, _ppu.ReadRegister(STAT) & 0x04);
    }

    [Fact]
    public void Ly_IsReadOnly()
    {
        _ppu.Step(DotsPerLine * 3);
        Assert.Equal(3, _ppu.ReadRegister(LY));

        _ppu.WriteRegister(LY, 0x42);

        Assert.Equal(3, _ppu.ReadRegister(LY));
    }

    // ─────────────────────── VRAM/OAM bus locking ────────────────────────

    [Fact]
    public void Vram_IsBlockedDuringDrawing()
    {
        _ppu.WriteVram(0x0000, 0xAB);

        // Step into Drawing on line 1.
        _ppu.Step(DotsPerLine + 80);
        Assert.Equal(PpuMode.Drawing, Mode(_ppu.ReadRegister(STAT)));

        Assert.Equal(0xFF, _ppu.ReadVram(0x0000));
        _ppu.WriteVram(0x0000, 0x99);

        // Leave Drawing — write was dropped.
        _ppu.Step(172);
        Assert.Equal(0xAB, _ppu.ReadVram(0x0000));
    }

    [Fact]
    public void Oam_IsBlockedDuringOamScanAndDrawing()
    {
        // Drop LCD off so we land in HBlank and the seed write goes through.
        _ppu.WriteRegister(LCDC, 0x00);
        _ppu.WriteOam(0, 0xAB);
        _ppu.WriteRegister(LCDC, LcdOn);

        Assert.Equal(PpuMode.OamScan, Mode(_ppu.ReadRegister(STAT)));
        Assert.Equal(0xFF, _ppu.ReadOam(0));

        _ppu.Step(80);
        Assert.Equal(PpuMode.Drawing, Mode(_ppu.ReadRegister(STAT)));
        Assert.Equal(0xFF, _ppu.ReadOam(0));

        _ppu.Step(172);
        Assert.Equal(PpuMode.HBlank, Mode(_ppu.ReadRegister(STAT)));
        Assert.Equal(0xAB, _ppu.ReadOam(0));
    }

    [Fact]
    public void DmaOamWrite_BypassesBusLocking()
    {
        // Get into OamScan, where the per-byte write would be blocked.
        _ppu.Step(DotsPerLine);
        Assert.Equal(PpuMode.OamScan, Mode(_ppu.ReadRegister(STAT)));

        var data = new byte[0xA0];
        for (var i = 0; i < data.Length; i++) data[i] = (byte)i;
        _ppu.WriteOam(data);

        // Leave OamScan/Drawing to read OAM back.
        _ppu.Step(80 + 172);
        Assert.Equal(0x42, _ppu.ReadOam(0x42));
    }

    // ───────────────────────── STAT IRQ behavior ─────────────────────────

    [Fact]
    public void LycInterrupt_FiresOnceWhenLyEntersLyc()
    {
        _ppu.WriteRegister(LYC, 5);
        _ppu.WriteRegister(STAT, 0x40); // enable LYC source

        _ppu.Step(DotsPerLine * 5);

        Assert.Equal(5, _ppu.ReadRegister(LY));
        Assert.Equal(1, _interrupts.LcdStatCount);
    }

    [Fact]
    public void StatBlocking_HandoffBetweenSourcesProducesNoNewIrq()
    {
        // HBlank + LYC sources both enabled at LYC=5. While LY=4 is in HBlank
        // the STAT line is high from the HBlank source. When line 5 starts
        // (OamScan + LYC=LY), the HBlank source falls but LYC takes over,
        // so the line stays high — no new rising edge should be seen.
        _ppu.WriteRegister(LYC, 5);
        _ppu.WriteRegister(STAT, 0x48);

        // Park mid-HBlank of LY=4. Latch the count at that point.
        _ppu.Step(DotsPerLine * 4 + 80 + 172 + 100);
        Assert.Equal(PpuMode.HBlank, Mode(_ppu.ReadRegister(STAT)));
        var before = _interrupts.LcdStatCount;

        // Step across into LY=5 / OamScan.
        _ppu.Step(DotsPerLine - (80 + 172 + 100));

        Assert.Equal(5, _ppu.ReadRegister(LY));
        Assert.Equal(PpuMode.OamScan, Mode(_ppu.ReadRegister(STAT)));
        Assert.Equal(before, _interrupts.LcdStatCount);
    }

    // ─────────────────────────── BG rendering ────────────────────────────

    [Fact]
    public void BgDisabled_ProducesZeroRow()
    {
        _ppu.WriteRegister(LCDC, LcdOn); // BG disabled (bit 0 clear)
        _ppu.WriteRegister(BGP, IdentityBgp);

        // Render line 0.
        _ppu.Step(80 + 172);

        var row = _ppu.FrameBuffer.Span;
        for (var x = 0; x < 160; x++)
            Assert.Equal(0, row[x]);
    }

    [Fact]
    public void BgRender_SimpleSolidTilePaintsAcrossScanline()
    {
        // Tile 0 = solid color 3: every byte = 0xFF (both planes set).
        for (var i = 0; i < 16; i++)
            _ppu.WriteVram((ushort)i, 0xFF);

        // Tile map 0 (offset 0x1800) is already all zeros = tile index 0.
        _ppu.WriteRegister(LCDC, (byte)(LcdOn | UnsignedTileData | BgOn));
        _ppu.WriteRegister(BGP, IdentityBgp);

        // Render line 0.
        _ppu.Step(80 + 172);

        var row = _ppu.FrameBuffer.Span;
        for (var x = 0; x < 160; x++)
            Assert.Equal(3, row[x]);
    }

    [Fact]
    public void BgRender_PaletteMapsColorIds()
    {
        // Tile 0 = color 1 (only low plane set).
        for (var i = 0; i < 16; i += 2) _ppu.WriteVram((ushort)i, 0xFF);

        _ppu.WriteRegister(LCDC, (byte)(LcdOn | UnsignedTileData | BgOn));
        _ppu.WriteRegister(BGP, 0b11_10_01_00); // 0→0, 1→1, 2→2, 3→3 (identity), but explicit
        _ppu.Step(80 + 172);
        Assert.Equal(1, _ppu.FrameBuffer.Span[0]);

        // Remap color 1 → shade 3.
        _ppu.WriteRegister(BGP, 0b00_00_11_00); // 0→0, 1→3, 2→0, 3→0
        _ppu.Step(DotsPerLine - (80 + 172) + 80 + 172); // advance to next line and re-render
        Assert.Equal(3, _ppu.FrameBuffer.Span[160]); // line 1, pixel 0
    }

    [Fact]
    public void BgRender_RespectsScx()
    {
        // Two tiles: tile 0 = all color 0, tile 1 = all color 3.
        for (var i = 0; i < 16; i++)
            _ppu.WriteVram((ushort)(16 + i), 0xFF); // tile 1

        // Tile map 0 entry (0,0) already 0; place tile 1 at (0,1).
        _ppu.WriteVram(0x1800 + 1, 1);

        _ppu.WriteRegister(LCDC, (byte)(LcdOn | UnsignedTileData | BgOn));
        _ppu.WriteRegister(BGP, IdentityBgp);
        _ppu.WriteRegister(SCX, 8); // shift left by one tile

        _ppu.Step(80 + 172);

        // Now tile 1 should occupy the leftmost 8 pixels.
        var row = _ppu.FrameBuffer.Span;
        for (var x = 0; x < 8; x++)
            Assert.Equal(3, row[x]);
        Assert.Equal(0, row[8]); // tile 2 at world col 2 is empty
    }
}
