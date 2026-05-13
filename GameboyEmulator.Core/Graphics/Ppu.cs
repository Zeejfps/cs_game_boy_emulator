using System.Runtime.CompilerServices;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Graphics;

public sealed partial class Ppu : IPpu
{
    public const int ScreenWidth = 160;
    public const int ScreenHeight = 144;
    
    private const int DotsPerLine    = 456;
    private const int LinesPerFrame  = 154;
    // OAM scan is logically 80 dots, but the mode register has 1 dot of read
    // latency on real hardware: a STAT read on the exact cycle of mode 2→3
    // transition still sees mode 2. Modeling this directly is fiddly because
    // it interacts with our "Tick advances state, then Read" ordering — so
    // we offset the transition by 1 dot here, which produces the same
    // observable timing for STAT polls (Mooneye's intr_2_mode3_timing,
    // intr_2_0_timing, intr_2_oam_ok_timing).
    private const int OamScanEndDot  = 81;

    private const int MaxSpritesPerLine = 10;

    // 2 banks × 8 KB. Bank 0 holds tile data + tile maps as on DMG; bank 1
    // holds an alternate copy of the tile data plus CGB BG attribute bytes at
    // the same offsets as the tile maps. _vramBank picks the bank for CPU
    // r/w through 0x8000-0x9FFF; PPU rendering reads both banks regardless.
    private readonly byte[] _vram = new byte[0x4000];
    private readonly byte[] _oam = new byte[0xA0];
    private readonly byte[] _frameBuffer = new byte[ScreenWidth * ScreenHeight];

    // RGBA output, pre-resolved through the palette tables. This is what the
    // host paints to the canvas — paletted byte[] above is retained for tests
    // and debug, which still want to inspect "which of the 4 shades is here".
    private readonly uint[] _rgbFrameBuffer = new uint[ScreenWidth * ScreenHeight];

    // 8 palettes × 4 colors. DMG uses only palette 0 in each table; the BGP /
    // OBP0 / OBP1 registers are pre-resolved into entries on write so the hot
    // pixel loop reads RGBA directly. CGB will populate all 32 entries from
    // BCPS/BCPD and OCPS/OCPD palette RAM in a later phase.
    private readonly uint[] _bgPaletteTable = new uint[32];
    private readonly uint[] _objPaletteTable = new uint[32];

    // Classic Game Boy green tint, formerly applied at paint time by the JS
    // frontend. Stored as 0xAABBGGRR — a uint written to little-endian memory
    // lands as R,G,B,A in byte order, exactly what canvas ImageData expects.
    private static readonly uint[] DmgShades =
    {
        0xFFD0F8E0u, // shade 0 — lightest (R=0xE0 G=0xF8 B=0xD0)
        0xFF70C088u, // shade 1
        0xFF566834u, // shade 2
        0xFF201808u, // shade 3 — darkest
    };

    private bool _isCgb;

    // CGB register state. _vramBank picks which 8 KB half of VRAM the CPU
    // sees through 0x8000-0x9FFF; PPU rendering always reads both banks. The
    // BCPS/OCPS bytes store the auto-increment flag (bit 7) and an index
    // (bits 0..5) into the 64-byte palette RAM. Full palette RAM lands in
    // Phase 5 — these fields are wired now so MMU dispatch is complete.
    private byte _vramBank;
    private byte _bcps;
    private byte _ocps;
    private readonly byte[] _bgPaletteRam = new byte[64];
    private readonly byte[] _objPaletteRam = new byte[64];

    private LcdControl _lcdc;
    private StatFlags _statSources; // bits 6,5,4,3 — interrupt source enables
    private byte _scy;
    private byte _scx;
    private byte _ly;
    private byte _lyc;
    private byte _bgp;
    private byte _obp0;
    private byte _obp1;
    private byte _wy;
    private byte _wx;
    private byte _spriteHeight;

    // LCDC-derived state. Enable bits and sprite height are live (refreshed in
    // WriteLcdc) since the predicates that read them run every dot. The BG/window
    // fetcher fields are latched at EnterDrawingMode so mid-scanline LCDC writes
    // don't disturb in-flight fetches — real hardware behaves this way.
    private bool _isLcdEnabled;
    private bool _isBackgroundDrawingEnabled;
    private bool _isObjectDrawingEnabled;
    private bool _isWindowDrawingEnabled;
    // Latched at EnterDrawingMode: starting offsets within `_vram` (bank 0) for
    // the BG tile map, window tile map, and tile-data window. The fetcher
    // adds the per-tile attribute's bank bit (×0x2000) to read tile data from
    // bank 1 in CGB mode; tile maps themselves always come from bank 0.
    private int _bgTileMapBase;
    private int _windowTileMapBase;
    private int _bgTilePixelsBase;
    private byte _bgTileFlipBit;

    private PpuMode _mode;
    private int _dot;
    private bool _statLine; // previous OR-of-sources, for stat-blocking edge detection

    // Latched LY=LYC comparison. Updated only while the comparison clock runs
    // (PPU on); preserved across LCD off so STAT bit 2 retains its last value
    // and changing LYC while the LCD is off does not affect the bit.
    private bool _lycMatch;
    // Set when the LCD is enabled; while true, STAT reports mode 0 instead of
    // mode 2 during the first OAM scan (well-known DMG quirk). Cleared when
    // the PPU advances into Drawing for the first time after enable.
    private bool _firstScanlineAfterEnable;

    // Mode-3 startup delay: real DMG holds the BG pixel pusher idle for ~6
    // dots after entering Drawing while the fetcher warms up. This is what
    // makes the canonical mode-3 length 172 dots (SCX=0, no sprites). Without
    // it our pipeline emits the first pixel ~5-6 dots too early and OAM
    // becomes accessible too soon after the mode-2 STAT IRQ.
    private int _drawingStall;

    // Window state.
    // _wyTriggered latches the first time LY == WY in a frame; persists until frame end.
    // _windowLineCounter (WLY) only advances on scanlines that actually pushed window pixels.
    private bool _wyTriggered;
    private bool _inWindow;
    private bool _windowRenderedThisLine;
    private byte _windowLineCounter;

    private readonly IInterrupts _interrupts;

    // Tile map / tile data offsets within bank 0 of `_vram`. The fetcher does
    // its own bank arithmetic so we stay in raw `_vram` indices throughout:
    //   tile maps: 0x1800 + 1 KB / 0x1C00 + 1 KB
    //   tile data: 0x0000 + 4 KB (unsigned) or 0x0800 + 4 KB (signed; uses
    //              `_bgTileFlipBit = 0x80` to XOR the tile-id high bit so a
    //              single base+xor handles both addressing modes).
    private const int TileMap0Base = 0x1800;
    private const int TileMap1Base = 0x1C00;
    private const int TilePixels0Base = 0x0000;
    private const int TilePixels1Base = 0x0800;

    // OAM scan state.
    private byte _scanSpriteIndex;
    private int _spriteCount;
    private readonly Sprite[] _sprites = new Sprite[MaxSpritesPerLine];

    // BG fetcher / pusher state. Methods live in Ppu.BgFetcher.cs.
    private BgPixelsFetcherState _fetcherState;
    private byte _fetcherX;
    private byte _fetcherTileId;
    // CGB BG attribute byte for the tile currently being fetched. Bits:
    //   0..2 palette index, 3 VRAM bank, 5 X-flip, 6 Y-flip, 7 BG-OBJ priority.
    // DMG mode reads it as 0 because bank 1 is all zeroes (CPU never wrote there).
    private byte _fetcherTileAttr;
    private byte _fetcherTileLow;
    private byte _fetcherTileHigh;

    private BgFifo _bgFifo;

    private byte _lcdX;
    private byte _lcdDiscard;

    // Sprite fetcher state. Methods live in Ppu.SpriteFetcher.cs.
    private bool _fetchingSprite;
    private SpriteFetcherState _spriteFetcherState;
    private Sprite _activeSprite;
    private byte _spriteFetcherTileLow;
    private byte _spriteFetcherTileHigh;
    // 1 bit per OAM-scanned sprite (max 10) — flips on when that sprite has
    // either been fetched or skipped as off-screen. Replaces the DMG-only
    // sorted-walk index so CGB's OAM-priority traversal can run.
    private int _spriteTriggeredMask;

    private SpriteFifo _spriteFifo;

    public ReadOnlyMemory<byte> FrameBuffer => _frameBuffer;
    public ReadOnlyMemory<uint> RgbFrameBuffer => _rgbFrameBuffer;
    public event Action? FrameCompleted;
    // Fires on the Drawing → HBlank transition. CGB HDMA's H-Blank mode is
    // wired here so the controller can transfer 16 bytes per scanline.
    public Action? OnHBlankEntry;

    public Ppu(IInterrupts interrupts)
    {
        _interrupts = interrupts;
        _mode = PpuMode.HBlank;
        // Seed palette tables to a known state so frames before any BGP/OBP
        // write (e.g. boot ROM running) don't paint uninitialized memory.
        RebuildDmgBgPalette();
        RebuildDmgObjPalette(0);
        RebuildDmgObjPalette(1);
    }

    public void SetCgbMode(bool isCgb)
    {
        _isCgb = isCgb;
    }

    // Translates the DMG BGP register (2 bits per shade-slot, 4 slots) into
    // entries 0..3 of the BG palette table. Hot path then does a single
    // lookup per pixel instead of unpacking BGP every dot.
    private void RebuildDmgBgPalette()
    {
        _bgPaletteTable[0] = DmgShades[(_bgp >> 0) & 0x03];
        _bgPaletteTable[1] = DmgShades[(_bgp >> 2) & 0x03];
        _bgPaletteTable[2] = DmgShades[(_bgp >> 4) & 0x03];
        _bgPaletteTable[3] = DmgShades[(_bgp >> 6) & 0x03];
    }

    // DMG OBP0 → entries 0..3 of OBJ table; OBP1 → entries 4..7.
    // (Sprite color 0 is always transparent, but we still write it so debug
    // tooling sees a consistent table.)
    private void RebuildDmgObjPalette(int which)
    {
        var reg = which == 0 ? _obp0 : _obp1;
        var baseIdx = which * 4;
        _objPaletteTable[baseIdx + 0] = DmgShades[(reg >> 0) & 0x03];
        _objPaletteTable[baseIdx + 1] = DmgShades[(reg >> 2) & 0x03];
        _objPaletteTable[baseIdx + 2] = DmgShades[(reg >> 4) & 0x03];
        _objPaletteTable[baseIdx + 3] = DmgShades[(reg >> 6) & 0x03];
    }

    public void Step(int tStates)
    {
        if (!_isLcdEnabled) return;
        while (tStates > 0)
        {
            tStates = _mode switch
            {
                PpuMode.OamScan => StepOamScan(tStates),
                PpuMode.Drawing => StepDrawing(tStates),
                PpuMode.HBlank  => StepHBlank(tStates),
                PpuMode.VBlank  => StepVBlank(tStates),
                _ => 0,
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int StepOamScan(int tStates)
    {
        var lineY = _ly + 16;
        while (tStates > 0 && _dot < OamScanEndDot)
        {
            // Sprite check completes once per 2-dot pair; do it on the second T-state.
            if ((_dot & 1) == 1)
            {
                var address = _scanSpriteIndex * 4;
                var spriteY = _oam[address];
                if (lineY >= spriteY && lineY < spriteY + _spriteHeight && _spriteCount < MaxSpritesPerLine)
                {
                    _sprites[_spriteCount] = new Sprite
                    {
                        Y = spriteY,
                        X = _oam[address + 1],
                        TileId = _oam[address + 2],
                        Attributes = (OamAttributes)_oam[address + 3],
                        OamIndex = _scanSpriteIndex
                    };
                    _spriteCount++;
                }
                _scanSpriteIndex++;
            }
            _dot++;
            tStates--;
        }
        if (_dot >= OamScanEndDot) EnterDrawingMode();
        return tStates;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterOamScanMode()
    {
        _mode = PpuMode.OamScan;
        _scanSpriteIndex = 0;
        _spriteCount = 0;
        // WY-condition latch is checked at the start of Mode 2 on real hardware.
        if (_ly == _wy) _wyTriggered = true;
        UpdateStatLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterDrawingMode()
    {
        _mode = PpuMode.Drawing;
        _firstScanlineAfterEnable = false;
        _drawingStall = 6;
        LatchBgFetcherLcdc();
        _fetcherState = BgPixelsFetcherState.GetTile;
        _fetcherX = 0;
        _bgFifo.Clear();
        _lcdX = 0;
        _lcdDiscard = (byte)(_scx & 0x07);
        _fetchingSprite = false;
        _inWindow = false;
        _windowRenderedThisLine = false;
        _spriteFifo = default;
        _spriteTriggeredMask = 0;
        SortSpritesByX();
        UpdateStatLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LatchBgFetcherLcdc()
    {
        var unsignedTileData = _lcdc.HasFlag(LcdControl.UseUnsignedTileAddressing);
        _bgTileMapBase     = _lcdc.HasFlag(LcdControl.BackgroundUsesTileMap1) ? TileMap1Base : TileMap0Base;
        _windowTileMapBase = _lcdc.HasFlag(LcdControl.WindowUsesTileMap1)     ? TileMap1Base : TileMap0Base;
        _bgTilePixelsBase  = unsignedTileData ? TilePixels0Base : TilePixels1Base;
        _bgTileFlipBit = (byte)(unsignedTileData ? 0x0 : 0x80);
    }

    // Stable insertion sort by X ascending; preserves OAM-index order on ties
    // so DMG priority (lower X wins, OAM-index breaks tie) falls out naturally
    // from "first sprite fetched fills the FIFO slot first."
    //
    // CGB hardware uses OAM-index priority unconditionally (lowest index wins),
    // which is already the scan order from `_sprites[]`, so we skip the sort.
    // OPRI (0xFF6C) can flip CGB back to DMG semantics; not modeled here.
    private void SortSpritesByX()
    {
        if (_isCgb) return;
        for (var i = 1; i < _spriteCount; i++)
        {
            var sprite = _sprites[i];
            var j = i;
            while (j > 0 && _sprites[j - 1].X > sprite.X)
            {
                _sprites[j] = _sprites[j - 1];
                j--;
            }
            _sprites[j] = sprite;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterHBlankMode()
    {
        _mode = PpuMode.HBlank;
        // HDMA fires before STAT — the H-Blank transfer must write VRAM
        // before any side effect that could trigger another VRAM access.
        OnHBlankEntry?.Invoke();
        UpdateStatLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterVBlankMode()
    {
        _mode = PpuMode.VBlank;
        _interrupts.Request(InterruptType.VBlank);
        FrameCompleted?.Invoke();

        // DMG quirk: at the LY 143→144 transition, the OAM (mode 2) STAT
        // source is asserted on the same cycle as the VBlank IRQ, even though
        // the visible mode is 1 (VBlank). If mode 2 IRQ is enabled this fires
        // a STAT IRQ at exactly the cycle VBlank is requested. The line then
        // settles to its VBlank-mode value at the next UpdateStatLine call.
        var line =
            (_statSources.HasFlag(StatFlags.LycIrq)    && _lycMatch) ||
            _statSources.HasFlag(StatFlags.OamIrq)                   ||
            _statSources.HasFlag(StatFlags.VBlankIrq);
        if (line && !_statLine)
            _interrupts.Request(InterruptType.LcdStat);
        _statLine = line;
    }

    private int StepHBlank(int tStates)
    {
        var dotsLeft = DotsPerLine - _dot;
        if (tStates < dotsLeft)
        {
            _dot += tStates;
            return 0;
        }
        _dot = DotsPerLine;
        EndOfLine();
        return tStates - dotsLeft;
    }

    private int StepVBlank(int tStates)
    {
        var dotsLeft = DotsPerLine - _dot;
        if (tStates < dotsLeft)
        {
            _dot += tStates;
            return 0;
        }
        _dot = DotsPerLine;
        EndOfLine();
        return tStates - dotsLeft;
    }

    private void EndOfLine()
    {
        _dot = 0;
        if (_windowRenderedThisLine) _windowLineCounter++;
        _ly++;
        if (_ly >= LinesPerFrame)
        {
            _ly = 0;
            _wyTriggered = false;
            _windowLineCounter = 0;
        }
        _lycMatch = _ly == _lyc;
        if (_ly == 0)
        {
            EnterOamScanMode();
            return;
        }
        if (_ly == ScreenHeight)
        {
            EnterVBlankMode();
            return;
        }
        if (_ly > ScreenHeight)
        {
            UpdateStatLine();
            return;
        }
        EnterOamScanMode();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int StepDrawing(int tStates)
    {
        while (tStates > 0 && _mode == PpuMode.Drawing)
        {
            if (_drawingStall > 0)
            {
                _drawingStall--;
                _dot++;
                tStates--;
                continue;
            }
            // Fetcher advances one step every 2 dots; pusher advances every dot.
            if ((_dot & 1) == 1) BgPixelFetcher_Tick();
            LcdControllerTick();
            _dot++;
            tStates--;
        }
        return tStates;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LcdControllerTick()
    {
        if (_fetchingSprite) return;

        // Window activation: once per scanline, when the screen X reaches WX-7
        // and the WY-condition has latched. This drops any in-flight BG pixels
        // (window pixels are not subject to SCX discard) and restarts the
        // fetcher against the window tilemap with WLY as the row source.
        // WX in 0..6 puts the window's leading pixels off-screen to the left:
        // activate at LCD X=0 and reuse _lcdDiscard to drop (7 - WX) window
        // pixels so the first visible window pixel lands at column 0.
        if (!_inWindow
            && _isWindowDrawingEnabled
            && _wyTriggered
            && _wx <= 166
            && (_wx >= 7 ? _lcdX == _wx - 7 : _lcdX == 0))
        {
            _inWindow = true;
            _windowRenderedThisLine = true;
            _bgFifo.Clear();
            _fetcherState = BgPixelsFetcherState.GetTile;
            _fetcherX = 0;
            _lcdDiscard = _wx < 7 ? (byte)(7 - _wx) : (byte)0;
            return;
        }

        if (_bgFifo.IsEmpty) return;

        // SCX-discard pixels are popped from the BG FIFO without writing to the
        // framebuffer. Sprite triggering must wait until discard completes —
        // sprites are positioned in screen space, and _lcdX is still 0 here.
        if (_lcdDiscard != 0)
        {
            _bgFifo.DropOne();
            _lcdDiscard--;
            return;
        }

        // Trigger any sprite at this column before popping. The fetch freezes
        // the pusher; multiple sprites at the same X chain naturally because
        // _lcdX doesn't advance until the FIFO actually pops a pixel.
        if (TryStartSpriteFetch()) return;

        // Read BG attribute before Pop — Pop only consumes color, attribute is
        // shared across all 8 pixels of the FIFO load.
        var bgAttr = _bgFifo.Attribute;
        var bgPixel = _bgFifo.Pop();

        var spPixel = 0;
        var spPalette = 0;
        var spBgPrio = 0;
        if (_spriteFifo.Count > 0)
            (spPixel, spPalette, spBgPrio) = _spriteFifo.Pop();

        // DMG: LCDC.0 = 0 hides BG entirely (forces color 0). CGB doesn't
        // disable BG this way — LCDC.0 there is master priority, handled in
        // the compositor (Phase 5 task 24).
        if (!_isCgb && !_isBackgroundDrawingEnabled) bgPixel = 0;

        // BG/OBJ priority. Sprite wins if:
        //   - it's enabled, opaque, and either
        //   - DMG: the priority bit is clear, or BG color is 0; or
        //   - CGB: LCDC.0 (BG master priority) is off, or BG color is 0, or
        //          neither the BG-attr priority nor the OBJ-priority bit is set.
        var bgAttrPrio = (bgAttr & 0x80) != 0;
        bool spriteWins;
        if (spPixel == 0 || !_isObjectDrawingEnabled)
        {
            spriteWins = false;
        }
        else if (!_isCgb)
        {
            spriteWins = spBgPrio == 0 || bgPixel == 0;
        }
        else if (!_isBackgroundDrawingEnabled)
        {
            // CGB: master-priority off — sprites always over BG.
            spriteWins = true;
        }
        else if (bgPixel == 0)
        {
            spriteWins = true;
        }
        else
        {
            spriteWins = spBgPrio == 0 && !bgAttrPrio;
        }

        byte color;
        uint rgb;
        if (spriteWins)
        {
            var palette = spPalette == 0 ? _obp0 : _obp1;
            color = (byte)((palette >> (spPixel << 1)) & 0x03);
            rgb = _objPaletteTable[(spPalette << 2) + spPixel];
        }
        else
        {
            color = (byte)((_bgp >> (bgPixel << 1)) & 0x03);
            // CGB: bgAttr bits 0..2 pick one of 8 palettes; DMG: bgAttr is 0
            // (bank 1 is unwritable in DMG mode), so this collapses to entries
            // 0..3 of palette 0 — the DMG-shade slots BGP populated.
            var bgPaletteIdx = bgAttr & 0x07;
            rgb = _bgPaletteTable[(bgPaletteIdx << 2) | bgPixel];
        }

        var pixelIdx = _ly * ScreenWidth + _lcdX;
        _frameBuffer[pixelIdx] = color;
        _rgbFrameBuffer[pixelIdx] = rgb;
        _lcdX++;

        if (_lcdX == ScreenWidth) EnterHBlankMode();
    }

    private void UpdateStatLine()
    {
        var line =
            (_statSources.HasFlag(StatFlags.LycIrq)    && _lycMatch)                ||
            (_statSources.HasFlag(StatFlags.OamIrq)    && _mode == PpuMode.OamScan) ||
            (_statSources.HasFlag(StatFlags.VBlankIrq) && _mode == PpuMode.VBlank)  ||
            (_statSources.HasFlag(StatFlags.HBlankIrq) && _mode == PpuMode.HBlank);

        if (line && !_statLine)
            _interrupts.Request(InterruptType.LcdStat);

        _statLine = line;
    }

    // Raw VRAM/OAM access. PPU-mode bus restrictions are enforced at the MMU
    // (CPU side); DMA writes go through these directly because DMA is the bus
    // master and isn't subject to those restrictions.
    public void WriteVram(ushort address, byte value) => _vram[(_vramBank << 13) | address] = value;
    public byte ReadVram(ushort address) => _vram[(_vramBank << 13) | address];
    public byte ReadOam(ushort address) => _oam[address];
    public void WriteOam(ushort address, byte value) => _oam[address] = value;
    public void WriteOam(ReadOnlySpan<byte> data) => data.CopyTo(_oam);

    public PpuMode Mode => _mode;
}