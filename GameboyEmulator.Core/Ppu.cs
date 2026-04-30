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
    private readonly byte[] _bgColorIds = new byte[ScreenWidth];
    private readonly SpriteEntry[] _spriteBuffer = new SpriteEntry[MaxSpritesPerLine];
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

    private PpuMode _mode;
    private int _dot;
    private bool _statLine; // previous OR-of-sources, for stat-blocking edge detection

    private readonly IInterrupts _interrupts;

    public Ppu(IInterrupts interrupts)
    {
        _interrupts = interrupts;
    }

    public ReadOnlyMemory<byte> FrameBuffer => _frameBuffer;

    public event Action? FrameCompleted;

    public void Step(int tStates)
    {
        if ((_lcdc & LcdEnableMask) == 0) return;
        for (var i = 0; i < tStates; i++) StepDot();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StepDot()
    {
        _dot++;

        if (_ly < VisibleLines)
        {
            switch (_dot)
            {
                case OamScanEndDot:
                    _mode = PpuMode.Drawing;
                    break;
                case DrawingEndDot:
                    _mode = PpuMode.HBlank;
                    RenderScanline(_ly);
                    break;
            }
        }

        if (_dot == DotsPerLine)
        {
            _dot = 0;
            _ly++;
            AdvanceLine();
        }

        UpdateStatLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdvanceLine()
    {
        switch (_ly)
        {
            case VisibleLines:
                _mode = PpuMode.VBlank;
                _interrupts.Request(InterruptType.VBlank);
                FrameCompleted?.Invoke();
                break;
            case LinesPerFrame:
                _ly = 0;
                _mode = PpuMode.OamScan;
                break;
            case < VisibleLines:
                _mode = PpuMode.OamScan;
                break;
        }
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RenderScanline(byte line)
    {
        var rowBase = line * ScreenWidth;

        if ((_lcdc & LcdcBgEnable) == 0)
        {
            Array.Clear(_frameBuffer, rowBase, ScreenWidth);
            Array.Clear(_bgColorIds, 0, ScreenWidth);
        }
        else
        {
            var tileMapOffset = (_lcdc & LcdcBgTileMap) != 0 ? TileMap1Offset : TileMap0Offset;
            var (tileDataBase, flipBit) = (_lcdc & LcdcTileData) != 0
                ? (TileDataUnsignedBase, TileDataUnsignedFlip)
                : (TileDataSignedBase,   TileDataSignedFlip);

            var worldY = (byte)(_scy + line);
            var tileRow = worldY >> 3;
            var pixelRow = worldY & 7;

            var tileCol = _scx >> 3;
            var startBit = _scx & 7;            // pixels of the first tile to skip

            var x = 0;
            while (x < ScreenWidth)
            {
                var tileIndex = _vram[tileMapOffset + tileRow * 32 + tileCol];
                var rowAddr = tileDataBase + ((tileIndex ^ flipBit) << 4) + pixelRow * 2;
                var lo = _vram[rowAddr];
                var hi = _vram[rowAddr + 1];

                var end = Math.Min(8, startBit + ScreenWidth - x);
                for (var b = startBit; b < end; b++)
                {
                    var bit = 7 - b;
                    var colorId = (((hi >> bit) & 1) << 1) | ((lo >> bit) & 1);
                    _bgColorIds[x] = (byte)colorId;
                    _frameBuffer[rowBase + x++] = (byte)((_bgp >> (colorId * 2)) & 0x3);
                }

                startBit = 0;
                tileCol = (tileCol + 1) & 31;   // wrap horizontally across the 32-tile map
            }
        }

        if ((_lcdc & LcdcObjEnable) != 0)
            RenderSprites(line, rowBase);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RenderSprites(byte line, int rowBase)
    {
        var height = (_lcdc & LcdcObjSize) != 0 ? 16 : 8;

        // OAM scan: collect first 10 Y-overlapping sprites, insertion-sorted into
        // descending priority (losers first). OAM index increases monotonically,
        // so an equal-X new entry is always the bigger loser and goes earlier.
        _spriteCount = 0;
        for (var i = 0; i < 40; i++)
        {
            var oamBase = i * 4;
            var spriteY = _oam[oamBase];
            var rowInSprite = line - (spriteY - 16);
            if (rowInSprite < 0 || rowInSprite >= height) continue;

            var attr = _oam[oamBase + 3];
            if ((attr & OamAttrYFlip) != 0)
                rowInSprite = height - 1 - rowInSprite;

            var entry = new SpriteEntry
            {
                X = _oam[oamBase + 1],
                Tile = _oam[oamBase + 2],
                Attr = attr,
                OamIndex = (byte)i,
                LineOffset = (byte)rowInSprite,
            };

            var pos = 0;
            while (pos < _spriteCount && _spriteBuffer[pos].X > entry.X) pos++;
            for (var j = _spriteCount; j > pos; j--)
                _spriteBuffer[j] = _spriteBuffer[j - 1];
            _spriteBuffer[pos] = entry;
            _spriteCount++;

            if (_spriteCount == MaxSpritesPerLine) break;
        }

        for (var s = 0; s < _spriteCount; s++)
        {
            ref var spr = ref _spriteBuffer[s];

            int tileIndex, rowInTile;
            if (height == 16)
            {
                // 8x16: bit 0 of tile index is forced; LineOffset already has Y-flip applied.
                tileIndex = spr.LineOffset < 8 ? spr.Tile & 0xFE : spr.Tile | 0x01;
                rowInTile = spr.LineOffset & 7;
            }
            else
            {
                tileIndex = spr.Tile;
                rowInTile = spr.LineOffset;
            }

            // Sprites always use unsigned 0x8000 base, regardless of LCDC bit 4.
            var rowAddr = (tileIndex << 4) + rowInTile * 2;
            var lo = _vram[rowAddr];
            var hi = _vram[rowAddr + 1];

            var palette = (spr.Attr & OamAttrPalette) != 0 ? _obp1 : _obp0;
            var xFlip   = (spr.Attr & OamAttrXFlip) != 0;
            var bgPrio  = (spr.Attr & OamAttrBgPrio) != 0;
            var baseX   = spr.X - 8;

            for (var p = 0; p < 8; p++)
            {
                var screenX = baseX + p;
                if ((uint)screenX >= ScreenWidth) continue;

                var bit = xFlip ? p : 7 - p;
                var colorId = (((hi >> bit) & 1) << 1) | ((lo >> bit) & 1);
                if (colorId == 0) continue;
                if (bgPrio && _bgColorIds[screenX] != 0) continue;

                _frameBuffer[rowBase + screenX] = (byte)((palette >> (colorId * 2)) & 0x3);
            }
        }
    }

    private struct SpriteEntry
    {
        public byte X;
        public byte Tile;
        public byte Attr;
        public byte OamIndex;
        public byte LineOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteLcdc(byte value)
    {
        var wasOn = (_lcdc & LcdEnableMask) != 0;
        var nowOn = (value & LcdEnableMask) != 0;
        _lcdc = value;
        if (wasOn && !nowOn)
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
}
