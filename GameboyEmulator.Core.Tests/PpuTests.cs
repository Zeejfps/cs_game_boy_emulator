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
    private const ushort OBP0 = 0xFF48;
    private const ushort OBP1 = 0xFF49;
    private const ushort WY   = 0xFF4A;
    private const ushort WX   = 0xFF4B;

    private const byte LcdOn   = 0x80;
    private const byte BgOn    = 0x01;
    private const byte ObjOn   = 0x02;           // LCDC bit 1
    private const byte TallSprites = 0x04;       // LCDC bit 2
    private const byte UnsignedTileData = 0x10;  // LCDC bit 4
    private const byte WindowOn    = 0x20;       // LCDC bit 5
    private const byte WindowMap1  = 0x40;       // LCDC bit 6
    private const byte IdentityBgp = 0xE4;       // 0→0 1→1 2→2 3→3

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

        // Mode 3 with no sprites/SCX/window is ~173 dots in our pipeline; step
        // a comfortable margin past that to reliably land in HBlank.
        _ppu.Step(200);
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
    public void VBlankInterrupt_FiresOnFirstDotOfLine144()
    {
        // Stop one dot before LY=144 begins.
        _ppu.Step(DotsPerLine * 143 + 455);
        Assert.Equal(143, _ppu.ReadRegister(LY));
        Assert.Equal(0, _interrupts.VBlankCount);

        // The very next dot crosses into LY=144 and must fire VBlank.
        _ppu.Step(1);
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
    public void Scanline_IsExactly456Dots()
    {
        // One dot before the boundary, LY must still be 0.
        _ppu.Step(455);
        Assert.Equal(0, _ppu.ReadRegister(LY));

        // One more dot crosses the boundary to LY=1.
        _ppu.Step(1);
        Assert.Equal(1, _ppu.ReadRegister(LY));
    }

    [Fact]
    public void VBlank_LastsExactly10Scanlines()
    {
        // Step to the start of VBlank (LY=144).
        _ppu.Step(DotsPerLine * 144);
        Assert.Equal(144, _ppu.ReadRegister(LY));
        Assert.Equal(PpuMode.VBlank, Mode(_ppu.ReadRegister(STAT)));

        // 10 full VBlank scanlines later, LY wraps to 0 and a new frame begins.
        _ppu.Step(DotsPerLine * 10);
        Assert.Equal(0, _ppu.ReadRegister(LY));
        Assert.Equal(PpuMode.OamScan, Mode(_ppu.ReadRegister(STAT)));
    }

    [Fact]
    public void Frame_IsExactly70224Dots()
    {
        const int FrameDots = 456 * 154; // 70,224

        // One dot before the boundary, we should still be on the last VBlank line.
        _ppu.Step(FrameDots - 1);
        Assert.Equal(153, _ppu.ReadRegister(LY));
        Assert.Equal(PpuMode.VBlank, Mode(_ppu.ReadRegister(STAT)));

        // The next dot wraps the frame.
        _ppu.Step(1);
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

    // VRAM/OAM bus locking is now enforced by the MMU (see MmuTests). The PPU
    // exposes raw access; DMA, which bypasses the MMU, naturally bypasses the
    // mode-block too.

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

    // ──────────────────────────── STAT bug ──────────────────────────────

    [Fact]
    public void StatBug_WriteStatWhenSourceActive_FiresSpuriousInterrupt()
    {
        // Step into HBlank on line 0.
        _ppu.Step(80 + 200);
        Assert.Equal(PpuMode.HBlank, Mode(_ppu.ReadRegister(STAT)));

        var before = _interrupts.LcdStatCount;

        // Enable the HBlank source while already in HBlank — the STAT bug
        // glitches the internal line low then re-evaluates, producing a rising edge.
        _ppu.WriteRegister(STAT, 0x08);

        Assert.Equal(before + 1, _interrupts.LcdStatCount);
    }

    [Fact]
    public void StatBug_WriteStatWhenNoSourceActive_NoSpuriousInterrupt()
    {
        // Step into HBlank on line 0.
        _ppu.Step(80 + 200);
        Assert.Equal(PpuMode.HBlank, Mode(_ppu.ReadRegister(STAT)));

        var before = _interrupts.LcdStatCount;

        // Enable OAM source (mode 2), but we're in HBlank (mode 0) — no source
        // evaluates true, so the line stays low after re-evaluation.
        _ppu.WriteRegister(STAT, 0x20);

        Assert.Equal(before, _interrupts.LcdStatCount);
    }

    [Fact]
    public void HBlankInterrupt_FiresOnceWhenEnteringMode0()
    {
        // Enable Mode 0 (HBlank) source only. We start at line 0, dot 0 in OamScan,
        // so no source is active and the STAT line is low. Step into HBlank and
        // verify a single IRQ on the rising edge.
        _ppu.WriteRegister(STAT, 0x08);

        var before = _interrupts.LcdStatCount;
        _ppu.Step(80 + 200); // through OamScan + Drawing into HBlank

        Assert.Equal(PpuMode.HBlank, Mode(_ppu.ReadRegister(STAT)));
        Assert.Equal(before + 1, _interrupts.LcdStatCount);
    }

    [Fact]
    public void VBlankStatInterrupt_FiresOnceWhenEnteringMode1()
    {
        // Enable Mode 1 (VBlank) STAT source only. This is distinct from the
        // VBlank vector (0x40) — bit 4 of STAT routes mode-1 entry to the LCD
        // STAT IRQ.
        _ppu.WriteRegister(STAT, 0x10);

        // Step to first dot of line 144.
        _ppu.Step(DotsPerLine * 144);

        Assert.Equal(PpuMode.VBlank, Mode(_ppu.ReadRegister(STAT)));
        Assert.Equal(1, _interrupts.LcdStatCount);
    }

    [Fact]
    public void OamInterrupt_FiresOnceWhenEnteringMode2()
    {
        // Enable Mode 2 (OAM scan) source only. Line 0 starts in OamScan, so
        // the source is already true at t=0; no rising edge expected yet.
        _ppu.WriteRegister(STAT, 0x20);
        var afterEnable = _interrupts.LcdStatCount;

        // Cross into HBlank, then back into OamScan on line 1 — that crossing
        // is the rising edge we care about.
        _ppu.Step(DotsPerLine);

        Assert.Equal(1, _ppu.ReadRegister(LY));
        Assert.Equal(PpuMode.OamScan, Mode(_ppu.ReadRegister(STAT)));
        Assert.Equal(afterEnable + 1, _interrupts.LcdStatCount);
    }

    [Fact]
    public void LycWrite_MatchingCurrentLy_FiresStatInterrupt()
    {
        // Enable LYC interrupt source.
        _ppu.WriteRegister(STAT, 0x40);

        // Advance to LY=5.
        _ppu.Step(DotsPerLine * 5);
        Assert.Equal(5, _ppu.ReadRegister(LY));

        var before = _interrupts.LcdStatCount;

        // Write LYC to match current LY — should trigger immediately.
        _ppu.WriteRegister(LYC, 5);

        Assert.Equal(before + 1, _interrupts.LcdStatCount);
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

        // Render line 0. Step well past mode 3 to ensure all 160 pixels are
        // pushed before the assert.
        _ppu.Step(80 + 200);

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

    // ────────────────────────── Window rendering ─────────────────────────

    [Fact]
    public void Window_RendersTilesFromWindowTileMap()
    {
        // Tile 1 = solid color 2 (high plane only).
        for (var i = 0; i < 16; i += 2)
        {
            _ppu.WriteVram((ushort)(16 + i), 0x00);     // low plane = 0
            _ppu.WriteVram((ushort)(16 + i + 1), 0xFF);  // high plane = 1
        }

        // Place tile 1 at window tile map 1 position (0,0).
        _ppu.WriteVram(0x1C00, 1);

        _ppu.WriteRegister(LCDC, LcdOn | BgOn | UnsignedTileData | WindowOn | WindowMap1);
        _ppu.WriteRegister(BGP, IdentityBgp);
        _ppu.WriteRegister(WY, 0);
        _ppu.WriteRegister(WX, 7); // window starts at screen X=0

        // Render line 0.
        _ppu.Step(DotsPerLine);

        var row = _ppu.FrameBuffer.Span;
        for (var x = 0; x < 8; x++)
            Assert.Equal(2, row[x]);
    }

    [Fact]
    public void Window_LineCounterAdvancesOnlyOnActiveLines()
    {
        // Tile 0, row 0 = color 3 (both planes set), row 1 = color 1 (low plane only).
        _ppu.WriteVram(0x0000, 0xFF); // row 0, low
        _ppu.WriteVram(0x0001, 0xFF); // row 0, high
        _ppu.WriteVram(0x0002, 0xFF); // row 1, low
        _ppu.WriteVram(0x0003, 0x00); // row 1, high

        // Window tile map 1 entry (0,0) = tile 0.
        _ppu.WriteVram(0x1C00, 0);

        // Disable LCD so the WY-latch (set at Mode 2 entry) is cleared before
        // we configure WY=2; otherwise the constructor's LCD-on with default WY=0
        // would have already latched the trigger at LY=0.
        _ppu.WriteRegister(LCDC, 0);
        _ppu.WriteRegister(WY, 2); // window starts at screen line 2
        _ppu.WriteRegister(WX, 7);
        _ppu.WriteRegister(LCDC, LcdOn | BgOn | UnsignedTileData | WindowOn | WindowMap1);
        _ppu.WriteRegister(BGP, IdentityBgp);

        // Complete lines 0-1 (no window) + line 2 drawing (window line counter 0 → tile row 0).
        _ppu.Step(DotsPerLine * 3);

        var fb = _ppu.FrameBuffer.Span;
        Assert.Equal(3, fb[2 * 160]); // line 2, pixel 0 = color 3 (tile row 0)

        // Complete line 3 (window line counter 1 → tile row 1).
        _ppu.Step(DotsPerLine);

        fb = _ppu.FrameBuffer.Span;
        Assert.Equal(1, fb[3 * 160]); // line 3, pixel 0 = color 1 (tile row 1)
    }

    // ──────────────────────── Sprite rendering ───────────────────────────

    [Fact]
    public void Sprite8x16_RendersBothHalves()
    {
        // Tile 0 (top half) = color 1 (low plane only).
        for (var i = 0; i < 16; i += 2)
        {
            _ppu.WriteVram((ushort)i, 0xFF);
            _ppu.WriteVram((ushort)(i + 1), 0x00);
        }
        // Tile 1 (bottom half) = color 2 (high plane only).
        for (var i = 0; i < 16; i += 2)
        {
            _ppu.WriteVram((ushort)(16 + i), 0x00);
            _ppu.WriteVram((ushort)(16 + i + 1), 0xFF);
        }

        // OAM entry 0: Y=16 (screen Y=0), X=8 (screen X=0), tile 0, no flags.
        _ppu.WriteRegister(LCDC, 0x00); // LCD off to write OAM
        _ppu.WriteOam(0, 16);  // Y
        _ppu.WriteOam(1, 8);   // X
        _ppu.WriteOam(2, 0);   // tile ID
        _ppu.WriteOam(3, 0);   // attributes

        _ppu.WriteRegister(LCDC, LcdOn | BgOn | UnsignedTileData | ObjOn | TallSprites);
        _ppu.WriteRegister(BGP, IdentityBgp);
        _ppu.WriteRegister(OBP0, IdentityBgp);

        // Render line 0 (top half of sprite).
        _ppu.Step(DotsPerLine);
        Assert.Equal(1, _ppu.FrameBuffer.Span[0]); // color 1

        // Render through line 8 (bottom half of sprite).
        _ppu.Step(DotsPerLine * 8);
        Assert.Equal(2, _ppu.FrameBuffer.Span[8 * 160]); // color 2
    }

    [Fact]
    public void SpritePriority_LowerXWinsOnDmg()
    {
        // Tile 1 = color 1 (low plane), tile 2 = color 2 (high plane).
        for (var i = 0; i < 16; i += 2)
        {
            _ppu.WriteVram((ushort)(16 + i), 0xFF);
            _ppu.WriteVram((ushort)(16 + i + 1), 0x00);
        }
        for (var i = 0; i < 16; i += 2)
        {
            _ppu.WriteVram((ushort)(32 + i), 0x00);
            _ppu.WriteVram((ushort)(32 + i + 1), 0xFF);
        }

        // Sprite A (OAM 0): X=10, tile 2 (color 2) — covers screen cols 2-9.
        // Sprite B (OAM 1): X=8, tile 1 (color 1) — covers screen cols 0-7.
        // Overlap at cols 2-7. Lower X (sprite B, X=8) should win.
        _ppu.WriteRegister(LCDC, 0x00);
        _ppu.WriteOam(0, 16); _ppu.WriteOam(1, 10); _ppu.WriteOam(2, 2); _ppu.WriteOam(3, 0);
        _ppu.WriteOam(4, 16); _ppu.WriteOam(5, 8);  _ppu.WriteOam(6, 1); _ppu.WriteOam(7, 0);

        _ppu.WriteRegister(LCDC, LcdOn | BgOn | UnsignedTileData | ObjOn);
        _ppu.WriteRegister(BGP, IdentityBgp);
        _ppu.WriteRegister(OBP0, IdentityBgp);

        _ppu.Step(DotsPerLine);

        var row = _ppu.FrameBuffer.Span;
        Assert.Equal(1, row[2]); // overlap zone: lower-X sprite (color 1) wins
        Assert.Equal(1, row[7]); // still in overlap
        Assert.Equal(2, row[8]); // only sprite A here (color 2)
    }

    [Fact]
    public void SpritePartiallyOffScreenLeft_OnlyVisiblePortionRenders()
    {
        // Tile 1 = color 3 (both planes set).
        for (var i = 0; i < 16; i++)
            _ppu.WriteVram((ushort)(16 + i), 0xFF);

        // Sprite at X=4: left 4 pixels clipped, right 4 visible at screen cols 0-3.
        _ppu.WriteRegister(LCDC, 0x00);
        _ppu.WriteOam(0, 16); // Y
        _ppu.WriteOam(1, 4);  // X
        _ppu.WriteOam(2, 1);  // tile ID
        _ppu.WriteOam(3, 0);  // attributes

        _ppu.WriteRegister(LCDC, LcdOn | BgOn | UnsignedTileData | ObjOn);
        _ppu.WriteRegister(BGP, IdentityBgp);
        _ppu.WriteRegister(OBP0, IdentityBgp);

        _ppu.Step(DotsPerLine);

        var row = _ppu.FrameBuffer.Span;
        for (var x = 0; x < 4; x++)
            Assert.Equal(3, row[x]); // visible portion of sprite
        Assert.Equal(0, row[4]); // no sprite here — BG color 0
    }
}
