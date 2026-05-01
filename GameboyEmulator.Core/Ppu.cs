using System.Runtime.CompilerServices;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public enum PpuMode : byte
{
    HBlank  = 0,
    VBlank  = 1,
    OamScan = 2,
    Drawing = 3,
}

public sealed class Ppu : IPpu
{
    public const int ScreenWidth = 160;
    public const int ScreenHeight = 144;

    private const int VramSize = 0x2000;
    private const int OamSize = 0xA0;

    private const int DotsPerLine    = 456;
    private const int LinesPerFrame  = 154;
    private const int VisibleLines   = 144;
    private const int OamScanEndDot  = 80;

    private const byte LcdcBgEnableMask   = 0x01; // LCDC bit 0
    private const byte LcdcObjEnableMask  = 0x02; // LCDC bit 1
    private const byte LcdcObjSizeMask    = 0x04; // LCDC bit 2 (0=8x8, 1=8x16)
    private const byte LcdcBgTileMapMask  = 0x08; // LCDC bit 3 (0=0x9800, 1=0x9C00)
    private const byte LcdcTileDataMask   = 0x10; // LCDC bit 4 (0=signed/0x9000, 1=unsigned/0x8000)
    private const byte LcdcWinEnableMask  = 0x20; // LCDC bit 5
    private const byte LcdcWinTileMapMask = 0x40; // LCDC bit 6 (0=0x9800, 1=0x9C00)
    private const byte LcdEnableMask  = 0x80; // LCDC bit 7

    private const byte StatHBlankIrq  = 0x08; // STAT bit 3
    private const byte StatVBlankIrq  = 0x10; // STAT bit 4
    private const byte StatOamIrq     = 0x20; // STAT bit 5
    private const byte StatLycIrq     = 0x40; // STAT bit 6

    private const byte OamAttrPalette = 0x10; // bit 4: 0=OBP0, 1=OBP1
    private const byte OamAttrXFlip   = 0x20; // bit 5
    private const byte OamAttrYFlip   = 0x40; // bit 6
    private const byte OamAttrBgPrio  = 0x80; // bit 7: 1=BG colors 1-3 hide sprite

    private const int MaxSpritesPerLine = 10;
    
    private const ushort LcdcAddress = 0xFF40;
    private const ushort StatAddress = 0xFF41;
    private const ushort ScyAddress  = 0xFF42;
    private const ushort ScxAddress  = 0xFF43;
    private const ushort LyAddress   = 0xFF44;
    private const ushort LycAddress  = 0xFF45;
    private const ushort BgpAddress  = 0xFF47;
    private const ushort Obp0Address = 0xFF48;
    private const ushort Obp1Address = 0xFF49;
    private const ushort WyAddress   = 0xFF4A;
    private const ushort WxAddress   = 0xFF4B;

    private readonly byte[] _vram = new byte[VramSize];
    private readonly byte[] _oam = new byte[OamSize];
    private readonly byte[] _frameBuffer = new byte[ScreenWidth * ScreenHeight];

    private byte _lcdc;
    private byte _statSources; // bits 6,5,4,3 — interrupt source enables
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

    // Cached LCDC-derived state. Refreshed only in WriteLcdc.
    // Defaults match LCDC = 0 (everything off).
    private bool _isDrawingEnabled;
    private bool _isBackgroundDrawingEnabled;
    private bool _isObjectDrawingEnabled;
    private bool _isWindowDrawingEnabled;
    private Memory<byte> _bgTileMap;
    private Memory<byte> _windowTileMap;
    private Memory<byte> _bgTilePixels;
    private byte _bgTileFlipBit;

    private PpuMode _mode;
    private int _dot;
    private bool _statLine; // previous OR-of-sources, for stat-blocking edge detection

    // Window state.
    // _wyTriggered latches the first time LY == WY in a frame; persists until frame end.
    // _windowLineCounter (WLY) only advances on scanlines that actually pushed window pixels.
    private bool _wyTriggered;
    private bool _inWindow;
    private bool _windowRenderedThisLine;
    private byte _windowLineCounter;

    private readonly IInterrupts _interrupts;

    private readonly Memory<byte> _tileMap0;
    private readonly Memory<byte> _tileMap1;
    // BG/window tile data windows. Unified addressing: addr = (id ^ flip) << 4
    //   unsigned: _tilePixels0 (blocks 0+1), flip 0x00
    //   signed:   _tilePixels1 (blocks 1+2), flip 0x80 — swaps the two halves
    private readonly Memory<byte> _tilePixels0;
    private readonly Memory<byte> _tilePixels1;

    public ReadOnlyMemory<byte> FrameBuffer => _frameBuffer;

    public event Action? FrameCompleted;

    
    public Ppu(IInterrupts interrupts)
    {
        _interrupts = interrupts;
        _tileMap0 = _vram.AsMemory(0x1800, 1024);
        _tileMap1 = _vram.AsMemory(0x1C00, 1024);
        _tilePixels0 = _vram.AsMemory(0x0000, 4096);
        _tilePixels1 = _vram.AsMemory(0x0800, 4096);
        _mode = PpuMode.HBlank;
    }
    
    public void Step(int tStates)
    {
        if (!_isDrawingEnabled) return;
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

    private byte _scanSpriteIndex;
    private int _spriteCount;
    private readonly Sprite[] _sprites = new Sprite[MaxSpritesPerLine];

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
                        Attributes = _oam[address + 3],
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
        UpdateStatLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterDrawingMode()
    {
        _mode = PpuMode.Drawing;
        _fetcherState = BgPixelsFetcherState.GetTile;
        _fetcherX = 0;
        _bgFifoCount = 0;
        _lcdX = 0;
        _lcdDiscard = (byte)(_scx & 0x07);
        _fetchingSprite = false;
        _inWindow = false;
        _windowRenderedThisLine = false;
        if (_ly == _wy) _wyTriggered = true;
        _spriteFifoLow = 0;
        _spriteFifoHigh = 0;
        _spriteFifoPalette = 0;
        _spriteFifoBgPriority = 0;
        _spriteFifoCount = 0;
        _nextSpriteIndex = 0;
        SortSpritesByX();
        UpdateStatLine();
    }

    // Stable insertion sort by X ascending; preserves OAM-index order on ties
    // so DMG priority (lower X wins, OAM-index breaks tie) falls out naturally
    // from "first sprite fetched fills the FIFO slot first."
    private void SortSpritesByX()
    {
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
        UpdateStatLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterVBlankMode()
    {
        _mode = PpuMode.VBlank;
        _interrupts.Request(InterruptType.VBlank);
        FrameCompleted?.Invoke();
        UpdateStatLine();
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
            EnterOamScanMode();
            return;
        }
        if (_ly == VisibleLines)
        {
            EnterVBlankMode();
            return;
        }
        if (_ly > VisibleLines)
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
            // Fetcher advances one step every 2 dots; pusher advances every dot.
            if ((_dot & 1) == 1) FetcherTick();
            LcdControllerTick();
            _dot++;
            tStates--;
        }
        return tStates;
    }

    #region BgPixelsFetcher
    
    private BgPixelsFetcherState _fetcherState;
    private byte _fetcherX;
    private byte _fetcherTileId;
    private byte _fetcherTileLow;
    private byte _fetcherTileHigh;

    private byte _bgFifoLow;
    private byte _bgFifoHigh;
    private byte _bgFifoCount;

    private byte _lcdX;
    private byte _lcdDiscard;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FetcherTick()
    {
        if (_fetchingSprite) { SpriteFetcherTick(); return; }
        switch (_fetcherState)
        {
            case BgPixelsFetcherState.GetTile:          BgPixelsFetcher_GetTile();          break;
            case BgPixelsFetcherState.GetTilePixelsLow: BgPixelsFetcher_GetTilePixelsLow(); break;
            case BgPixelsFetcherState.GetTilePixelsHigh:BgPixelsFetcher_GetTilePixelsHigh();break;
            case BgPixelsFetcherState.Push:             BgPixelsFetcher_Push();             break;
            default: throw new ArgumentOutOfRangeException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelsFetcher_Push()
    {
        if (_bgFifoCount != 0) return;
        _bgFifoLow = _fetcherTileLow;
        _bgFifoHigh = _fetcherTileHigh;
        _bgFifoCount = 8;
        _fetcherX++;
        _fetcherState = BgPixelsFetcherState.GetTile;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelsFetcher_GetTilePixelsLow()
    {
        _fetcherTileLow = _bgTilePixels.Span[BgTileRowOffset()];
        _fetcherState = BgPixelsFetcherState.GetTilePixelsHigh;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelsFetcher_GetTilePixelsHigh()
    {
        _fetcherTileHigh = _bgTilePixels.Span[BgTileRowOffset() + 1];
        _fetcherState = BgPixelsFetcherState.Push;
    }

    // Offset within _bgTilePixels for the current tile id and row.
    // _bgTileFlipBit (0x00 unsigned, 0x80 signed) swaps the two halves of the
    // window so a single base + xor handles both addressing modes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BgTileRowOffset()
    {
        var rowY = _inWindow ? (_windowLineCounter & 0x07) : ((_ly + _scy) & 0x07);
        return ((_fetcherTileId ^ _bgTileFlipBit) << 4) | (rowY << 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelsFetcher_GetTile()
    {
        ReadOnlySpan<byte> tileMap;
        int tileX, tileY;
        if (_inWindow)
        {
            tileMap = _windowTileMap.Span;
            tileX = _fetcherX & 0x1F;
            tileY = _windowLineCounter >> 3;
        }
        else
        {
            tileMap = _bgTileMap.Span;
            tileX = ((_scx >> 3) + _fetcherX) & 0x1F;
            tileY = ((_ly + _scy) & 0xFF) >> 3;
        }
        _fetcherTileId = tileMap[(tileY << 5) | tileX];
        _fetcherState = BgPixelsFetcherState.GetTilePixelsLow;
    }

    #endregion

    #region SpriteFetcher

    private bool _fetchingSprite;
    private SpriteFetcherState _spriteFetcherState;
    private Sprite _activeSprite;
    private byte _spriteFetcherTileLow;
    private byte _spriteFetcherTileHigh;
    private byte _nextSpriteIndex;

    // Sprite FIFO: MSB = next pixel popped, mirroring BG FIFO layout.
    // Color is two bitplanes; palette and bg-priority are one bit per slot.
    private byte _spriteFifoLow;
    private byte _spriteFifoHigh;
    private byte _spriteFifoPalette;
    private byte _spriteFifoBgPriority;
    private byte _spriteFifoCount;

    // Pick the next sprite whose visible column is at or before the current
    // _lcdX. Lower X wins (sprites are sorted); X==0 / X>=168 are off-screen.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryStartSpriteFetch()
    {
        if (!_isObjectDrawingEnabled) return false;
        while (_nextSpriteIndex < _spriteCount)
        {
            var s = _sprites[_nextSpriteIndex];
            if (s.X == 0 || s.X >= 168) { _nextSpriteIndex++; continue; }
            if (s.X > _lcdX + 8) return false;
            _activeSprite = s;
            _nextSpriteIndex++;
            _fetchingSprite = true;
            _spriteFetcherState = SpriteFetcherState.GetTile;
            // Real hardware aborts the BG fetch in flight; the BG FIFO is left
            // alone but the fetcher restarts at GetTile.
            _fetcherState = BgPixelsFetcherState.GetTile;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SpriteFetcherTick()
    {
        switch (_spriteFetcherState)
        {
            case SpriteFetcherState.GetTile:           _spriteFetcherState = SpriteFetcherState.GetTilePixelsLow; break;
            case SpriteFetcherState.GetTilePixelsLow:  SpriteFetcher_GetTilePixelsLow();  break;
            case SpriteFetcherState.GetTilePixelsHigh: SpriteFetcher_GetTilePixelsHigh(); break;
            default: throw new ArgumentOutOfRangeException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SpriteTileRowAddress()
    {
        var row = _ly - (_activeSprite.Y - 16);
        if ((_activeSprite.Attributes & OamAttrYFlip) != 0)
            row = _spriteHeight - 1 - row;
        var tileId = _activeSprite.TileId;
        if (_spriteHeight == 16)
        {
            if (row < 8) tileId = (byte)(tileId & 0xFE);
            else { tileId = (byte)(tileId | 0x01); row -= 8; }
        }
        // Sprites always use the unsigned 0x8000 base; in our VRAM layout that's offset 0.
        return (tileId << 4) + (row << 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SpriteFetcher_GetTilePixelsLow()
    {
        _spriteFetcherTileLow = _vram[SpriteTileRowAddress()];
        _spriteFetcherState = SpriteFetcherState.GetTilePixelsHigh;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SpriteFetcher_GetTilePixelsHigh()
    {
        _spriteFetcherTileHigh = _vram[SpriteTileRowAddress() + 1];
        MergeIntoSpriteFifo();
        _fetchingSprite = false;
    }

    // Merge the 8 fetched sprite pixels into the sprite FIFO. Existing opaque
    // pixels (color != 0) are preserved — earlier-fetched sprites win on overlap,
    // which gives DMG priority for free since we fetch in (X asc, OAM idx) order.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MergeIntoSpriteFifo()
    {
        var low = _spriteFetcherTileLow;
        var high = _spriteFetcherTileHigh;
        if ((_activeSprite.Attributes & OamAttrXFlip) != 0)
        {
            low = ReverseBits(low);
            high = ReverseBits(high);
        }

        // Sprites with X<8 are partially off the left edge: their visible pixels
        // start at tile-pixel (8 - X) and land in the leading X FIFO slots (MSB end).
        // Shift the tile bytes left by (8 - X) and restrict the merge to the same bits.
        var x = _activeSprite.X;
        var shift = x < 8 ? 8 - x : 0;
        var newLow = (byte)(low << shift);
        var newHigh = (byte)(high << shift);
        var pixelMask = (byte)(0xFF << shift);

        var paletteByte = (_activeSprite.Attributes & OamAttrPalette) != 0 ? (byte)0xFF : (byte)0x00;
        var bgPrioByte  = (_activeSprite.Attributes & OamAttrBgPrio)  != 0 ? (byte)0xFF : (byte)0x00;

        // Slots already holding an opaque sprite pixel (color != 0) within the
        // currently-occupied portion of the FIFO are preserved.
        var occupiedMask = _spriteFifoCount == 0 ? (byte)0 : (byte)(0xFF << (8 - _spriteFifoCount));
        var existingOpaque = (byte)((_spriteFifoLow | _spriteFifoHigh) & occupiedMask);
        var writeMask = (byte)(pixelMask & ~existingOpaque);

        _spriteFifoLow        = (byte)((_spriteFifoLow        & ~writeMask) | (newLow       & writeMask));
        _spriteFifoHigh       = (byte)((_spriteFifoHigh       & ~writeMask) | (newHigh      & writeMask));
        _spriteFifoPalette    = (byte)((_spriteFifoPalette    & ~writeMask) | (paletteByte  & writeMask));
        _spriteFifoBgPriority = (byte)((_spriteFifoBgPriority & ~writeMask) | (bgPrioByte   & writeMask));
        if (_spriteFifoCount < 8) _spriteFifoCount = 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ReverseBits(byte b)
    {
        b = (byte)(((b & 0xF0) >> 4) | ((b & 0x0F) << 4));
        b = (byte)(((b & 0xCC) >> 2) | ((b & 0x33) << 2));
        b = (byte)(((b & 0xAA) >> 1) | ((b & 0x55) << 1));
        return b;
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LcdControllerTick()
    {
        if (_fetchingSprite) return;

        // Window activation: once per scanline, when the screen X reaches WX-7
        // and the WY-condition has latched. This drops any in-flight BG pixels
        // (window pixels are not subject to SCX discard) and restarts the
        // fetcher against the window tilemap with WLY as the row source.
        if (!_inWindow
            && _isWindowDrawingEnabled
            && _wyTriggered
            && _wx <= 166
            && _wx >= 7
            && _lcdX == _wx - 7)
        {
            _inWindow = true;
            _windowRenderedThisLine = true;
            _bgFifoCount = 0;
            _fetcherState = BgPixelsFetcherState.GetTile;
            _fetcherX = 0;
            _lcdDiscard = 0;
            return;
        }

        if (_bgFifoCount == 0) return;

        // SCX-discard pixels are popped from the BG FIFO without writing to the
        // framebuffer. Sprite triggering must wait until discard completes —
        // sprites are positioned in screen space, and _lcdX is still 0 here.
        if (_lcdDiscard != 0)
        {
            _bgFifoLow  = (byte)(_bgFifoLow  << 1);
            _bgFifoHigh = (byte)(_bgFifoHigh << 1);
            _bgFifoCount--;
            _lcdDiscard--;
            return;
        }

        // Trigger any sprite at this column before popping. The fetch freezes
        // the pusher; multiple sprites at the same X chain naturally because
        // _lcdX doesn't advance until the FIFO actually pops a pixel.
        if (TryStartSpriteFetch()) return;

        var bgPixel = (((_bgFifoHigh >> 7) & 1) << 1) | ((_bgFifoLow >> 7) & 1);
        _bgFifoLow  = (byte)(_bgFifoLow  << 1);
        _bgFifoHigh = (byte)(_bgFifoHigh << 1);
        _bgFifoCount--;

        var spPixel = 0;
        var spPalette = 0;
        var spBgPrio = 0;
        if (_spriteFifoCount > 0)
        {
            spPixel   = (((_spriteFifoHigh >> 7) & 1) << 1) | ((_spriteFifoLow >> 7) & 1);
            spPalette = (_spriteFifoPalette    >> 7) & 1;
            spBgPrio  = (_spriteFifoBgPriority >> 7) & 1;
            _spriteFifoLow        = (byte)(_spriteFifoLow        << 1);
            _spriteFifoHigh       = (byte)(_spriteFifoHigh       << 1);
            _spriteFifoPalette    = (byte)(_spriteFifoPalette    << 1);
            _spriteFifoBgPriority = (byte)(_spriteFifoBgPriority << 1);
            _spriteFifoCount--;
        }

        if (!_isBackgroundDrawingEnabled) bgPixel = 0;

        byte color;
        if (spPixel != 0 && _isObjectDrawingEnabled && (spBgPrio == 0 || bgPixel == 0))
        {
            var palette = spPalette == 0 ? _obp0 : _obp1;
            color = (byte)((palette >> (spPixel << 1)) & 0x03);
        }
        else
        {
            color = (byte)((_bgp >> (bgPixel << 1)) & 0x03);
        }

        _frameBuffer[_ly * ScreenWidth + _lcdX] = color;
        _lcdX++;

        if (_lcdX == ScreenWidth) EnterHBlankMode();
    }
    
    private void UpdateStatLine()
    {
        var line =
            ((_statSources & StatLycIrq)    != 0 && _ly == _lyc)         ||
            ((_statSources & StatOamIrq)    != 0 && _mode == PpuMode.OamScan) ||
            ((_statSources & StatVBlankIrq) != 0 && _mode == PpuMode.VBlank)  ||
            ((_statSources & StatHBlankIrq) != 0 && _mode == PpuMode.HBlank);

        if (line && !_statLine)
            _interrupts.Request(InterruptType.LcdStat);

        _statLine = line;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteLcdc(byte value)
    {
        var wasDrawingEnabled = _isDrawingEnabled;
        
        _isDrawingEnabled = (value & LcdEnableMask) != 0;
        _isBackgroundDrawingEnabled = (value & LcdcBgEnableMask) != 0;
        _isObjectDrawingEnabled = (value & LcdcObjEnableMask) != 0;
        _isWindowDrawingEnabled = (value & LcdcWinEnableMask) != 0;
        _spriteHeight = (value & LcdcObjSizeMask) != 0 ? (byte)16 : (byte)8;
        _bgTileMap = (value & LcdcBgTileMapMask) != 0 ? _tileMap1 : _tileMap0;
        _windowTileMap = (value & LcdcWinTileMapMask) != 0 ? _tileMap1 : _tileMap0;
        
        var unsignedTileData = (value & LcdcTileDataMask) != 0;
        _bgTilePixels  = unsignedTileData ? _tilePixels0         : _tilePixels1;
        _bgTileFlipBit = (byte)(unsignedTileData ? 0x0 : 0x80);
        _lcdc = value;
        
        if (wasDrawingEnabled && !_isDrawingEnabled)
        {
            _ly = 0;
            _dot = 0;
            _mode = PpuMode.HBlank;
            _statLine = false;
            _wyTriggered = false;
            _windowLineCounter = 0;
            _inWindow = false;
            _windowRenderedThisLine = false;
        }
        else if (!wasDrawingEnabled && _isDrawingEnabled)
        {
            _ly = 0;
            _dot = 0;
            _wyTriggered = false;
            _windowLineCounter = 0;
            EnterOamScanMode();
        }
    }

    public void WriteVram(ushort address, byte value)
    {
        if (_mode == PpuMode.Drawing) return;
        _vram[address] = value;
    }

    public byte ReadVram(ushort address)
    {
        if (_mode == PpuMode.Drawing) return 0xFF;
        return _vram[address];
    }

    public ReadOnlySpan<byte> ReadVramRange(ushort address, int length) => _vram.AsSpan(address, length);

    public byte ReadOam(ushort address)
    {
        if (_mode is PpuMode.OamScan or PpuMode.Drawing) return 0xFF;
        return _oam[address];
    }

    public void WriteOam(ushort address, byte value)
    {
        if (_mode is PpuMode.OamScan or PpuMode.Drawing) return;
        _oam[address] = value;
    }

    // DMA path: PPU bus restrictions don't apply — DMA itself drives OAM.
    public void WriteOam(ReadOnlySpan<byte> data) => data.CopyTo(_oam);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case LcdcAddress: WriteLcdc(value); break;
            case StatAddress: _statSources = (byte)(value & 0x78); break;
            case ScyAddress:  _scy = value; break;
            case ScxAddress:  _scx = value; break;
            case LyAddress:   /* read-only */ break;
            case LycAddress:  _lyc = value; break;
            case BgpAddress:  _bgp = value; break;
            case Obp0Address: _obp0 = value; break;
            case Obp1Address: _obp1 = value; break;
            case WyAddress:   _wy = value; break;
            case WxAddress:   _wx = value; break;
        }
    }

    public byte ReadRegister(ushort address)
    {
        return address switch
        {
            LcdcAddress => _lcdc,
            StatAddress => (byte)(0x80 | _statSources | (_ly == _lyc ? 0x04 : 0x00) | (byte)_mode),
            ScyAddress  => _scy,
            ScxAddress  => _scx,
            LyAddress   => _ly,
            LycAddress  => _lyc,
            BgpAddress  => _bgp,
            Obp0Address => _obp0,
            Obp1Address => _obp1,
            WyAddress   => _wy,
            WxAddress   => _wx,
            _ => 0xFF
        };
    }

    readonly struct Sprite
    {
        public byte Y { get; init; }
        public byte X { get; init; }
        public byte TileId { get; init; }
        public byte Attributes { get; init; }
        public byte OamIndex { get; init; }
    }
}
