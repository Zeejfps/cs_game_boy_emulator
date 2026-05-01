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
    private const int DrawingEndDot  = 80 + 172;

    private const byte LcdcBgEnable   = 0x01; // LCDC bit 0
    private const byte LcdcObjEnable  = 0x02; // LCDC bit 1
    private const byte LcdcObjSize    = 0x04; // LCDC bit 2 (0=8x8, 1=8x16)
    private const byte LcdcBgTileMap  = 0x08; // LCDC bit 3 (0=0x9800, 1=0x9C00)
    private const byte LcdcTileData   = 0x10; // LCDC bit 4 (0=signed/0x9000, 1=unsigned/0x8000)
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

    private const ushort TileMap0Offset = 0x1800; // 0x9800 - 0x8000
    private const ushort TileMap1Offset = 0x1C00; // 0x9C00 - 0x8000

    // Unified addressing: addr = base + ((index ^ flip) << 4)
    //   unsigned mode: base 0x0000, flip 0x00 → block 0+1
    //   signed   mode: base 0x0800, flip 0x80 → block 1+2 (0x80 swaps the halves)
    private const ushort TileDataUnsignedBase = 0x0000;
    private const byte   TileDataUnsignedFlip = 0x00;
    private const ushort TileDataSignedBase   = 0x0800;
    private const byte   TileDataSignedFlip   = 0x80;

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
    private int _spriteCount;

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
    private bool _isLcdEnabled;
    private byte _spriteHeight;

    private PpuMode _mode;
    private int _dot;
    private bool _statLine; // previous OR-of-sources, for stat-blocking edge detection

    private readonly IInterrupts _interrupts;

    private readonly Memory<byte> _tileMap0;
    private readonly Memory<byte> _tileMap1;
    private readonly Memory<byte> _tilePixels0;
    private readonly Memory<byte> _tilePixels1;

    private Memory<byte> _bgTileMap;
    private Memory<byte> _bgTilePixels;
    private byte _bgTileFlipBit;

    public ReadOnlyMemory<byte> FrameBuffer => _frameBuffer;

    public event Action? FrameCompleted;

    
    public Ppu(IInterrupts interrupts)
    {
        _interrupts = interrupts;
        _tileMap0 = _vram.AsMemory(0x1800, 1024);
        _tileMap1 = _vram.AsMemory(0x1C00, 1024);
        _tilePixels0 = _vram.AsMemory(0x0000, 4096);
        _tilePixels1 = _vram.AsMemory(0x0800, 4096);
        _mode = PpuMode.OamScan;
    }
    
    public void Step(int tStates)
    {
        if (!_isLcdEnabled) return;
        switch (_mode)
        {
            case PpuMode.HBlank:
                break;
            case PpuMode.VBlank:
                break;
            case PpuMode.OamScan:
                StepOamScan(tStates);
                break;
            case PpuMode.Drawing:
                StepDrawing(tStates);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    private int _remainderTStates;
    private byte _scanSpriteIndex;
    private readonly Sprite[] _sprites = new Sprite[MaxSpritesPerLine];

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void StepOamScan(int tStates)
    {
        var totalStates = tStates + _remainderTStates;
        var lineY = _ly + 16;
        while (totalStates >= 2)
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
            totalStates -= 2;
            _dot += 2;
            if (_dot >= 80)
            {
                _mode = PpuMode.Drawing;
                break;
            }
        }
        _remainderTStates = totalStates;
    }

    private void StepDrawing(int tStates)
    {
        StepFetcher(tStates);
        StepLcd(tStates);
    }
    
    private FetcherState _fetcherState;
    
    private void StepFetcher(int tStates)
    {
        var totalTStates = tStates + _remainderTStates;
        while (totalTStates >= 2)
        {
            switch (_fetcherState)
            {
                case FetcherState.GetTile:
                    Fetcher_GetTile();
                    break;
                case FetcherState.GetTilePixelsLow:
                    Fetcher_GetTilePixelsLow();
                    break;
                case FetcherState.GetTilePixelsHigh:
                    Fetcher_GetTilePixelsHigh();
                    break;
                case FetcherState.Push:
                    Fetcher_Push();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            totalTStates -= 2;
        }
        _remainderTStates = totalTStates;
    }

    private void Fetcher_Push()
    {
        throw new NotImplementedException();
    }

    private void Fetcher_GetTilePixelsHigh()
    {
        throw new NotImplementedException();
    }

    private void Fetcher_GetTilePixelsLow()
    {
        throw new NotImplementedException();
    }

    private void Fetcher_GetTile()
    {
        throw new NotImplementedException();
    }

    private void StepLcd(int tStates)
    {
        
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
        var wasEnabled = _isLcdEnabled;
        _isLcdEnabled = (value & LcdEnableMask) != 0;
        _spriteHeight = (value & LcdcObjSize) != 0 ? (byte)16 : (byte)8;
        _lcdc = value;
        if (wasEnabled && !_isLcdEnabled)
        {
            _ly = 0;
            _dot = 0;
            _mode = PpuMode.HBlank;
            _statLine = false;
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
