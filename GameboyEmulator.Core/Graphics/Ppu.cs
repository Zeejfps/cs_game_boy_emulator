using System.Runtime.CompilerServices;
using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Graphics;

public sealed partial class Ppu : IPpu
{
    public const int ScreenWidth = 160;
    public const int ScreenHeight = 144;
    
    private const int DotsPerLine    = 456;
    private const int LinesPerFrame  = 154;
    private const int OamScanEndDot  = 80;

    private const int MaxSpritesPerLine = 10;

    private readonly byte[] _vram = new byte[0x2000];
    private readonly byte[] _oam = new byte[0xA0];
    private readonly byte[] _frameBuffer = new byte[ScreenWidth * ScreenHeight];

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

    // OAM scan state.
    private byte _scanSpriteIndex;
    private int _spriteCount;
    private readonly Sprite[] _sprites = new Sprite[MaxSpritesPerLine];

    // BG fetcher / pusher state. Methods live in Ppu.BgFetcher.cs.
    private BgPixelsFetcherState _fetcherState;
    private byte _fetcherX;
    private byte _fetcherTileId;
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
    private byte _nextSpriteIndex;

    private SpriteFifo _spriteFifo;

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
        _nextSpriteIndex = 0;
        SortSpritesByX();
        UpdateStatLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LatchBgFetcherLcdc()
    {
        var unsignedTileData = _lcdc.HasFlag(LcdControl.UseUnsignedTileAddressing);
        _bgTileMap     = _lcdc.HasFlag(LcdControl.BackgroundUsesTileMap1) ? _tileMap1 : _tileMap0;
        _windowTileMap = _lcdc.HasFlag(LcdControl.WindowUsesTileMap1) ? _tileMap1 : _tileMap0;
        _bgTilePixels  = unsignedTileData ? _tilePixels0 : _tilePixels1;
        _bgTileFlipBit = (byte)(unsignedTileData ? 0x0 : 0x80);
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

        var bgPixel = _bgFifo.Pop();

        var spPixel = 0;
        var spPalette = 0;
        var spBgPrio = 0;
        if (_spriteFifo.Count > 0)
            (spPixel, spPalette, spBgPrio) = _spriteFifo.Pop();

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
            (_statSources.HasFlag(StatFlags.LycIrq)    && _ly == _lyc)              ||
            (_statSources.HasFlag(StatFlags.OamIrq)    && _mode == PpuMode.OamScan) ||
            (_statSources.HasFlag(StatFlags.VBlankIrq) && _mode == PpuMode.VBlank)  ||
            (_statSources.HasFlag(StatFlags.HBlankIrq) && _mode == PpuMode.HBlank);

        if (line && !_statLine)
            _interrupts.Request(InterruptType.LcdStat);

        _statLine = line;
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
}