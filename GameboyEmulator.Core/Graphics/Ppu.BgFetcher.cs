using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.Graphics;

// BG / window pixel fetcher. State fields live in Ppu.cs.
public sealed partial class Ppu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelFetcher_Tick()
    {
        if (_fetchingSprite)
        {
            SpriteFetcher_Tick(); 
            return;
        }
        
        switch (_fetcherState)
        {
            case BgPixelsFetcherState.GetTile:          
                BgPixelsFetcher_GetTile();         
                break;
            case BgPixelsFetcherState.GetTilePixelsLow: 
                BgPixelsFetcher_GetTilePixelsLow(); 
                break;
            case BgPixelsFetcherState.GetTilePixelsHigh:
                BgPixelsFetcher_GetTilePixelsHigh();
                break;
            case BgPixelsFetcherState.Push:             
                BgPixelsFetcher_Push(); 
                break;
            default: 
                throw new ArgumentOutOfRangeException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelsFetcher_Push()
    {
        if (!_bgFifo.IsEmpty) return;
        _bgFifo.Push(_fetcherTileLow, _fetcherTileHigh, _fetcherTileAttr);
        _fetcherX++;
        _fetcherState = BgPixelsFetcherState.GetTile;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelsFetcher_GetTilePixelsLow()
    {
        var b = _vram[BgTileRowVramOffset()];
        // CGB X-flip is implemented by reversing each fetched byte so the FIFO
        // pop order — which always reads MSB first — produces flipped pixels.
        if ((_fetcherTileAttr & 0x20) != 0) b = ReverseBits(b);
        _fetcherTileLow = b;
        _fetcherState = BgPixelsFetcherState.GetTilePixelsHigh;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelsFetcher_GetTilePixelsHigh()
    {
        var b = _vram[BgTileRowVramOffset() + 1];
        if ((_fetcherTileAttr & 0x20) != 0) b = ReverseBits(b);
        _fetcherTileHigh = b;
        _fetcherState = BgPixelsFetcherState.Push;
    }

    // VRAM offset for the current tile/row, accounting for: tile-data addressing
    // mode (`_bgTileFlipBit` XORs the tile-id high bit to swap signed/unsigned
    // halves of the 6 KB tile-data window), CGB Y-flip (attribute bit 6), and
    // CGB tile-data VRAM bank (attribute bit 3 → +0x2000 to land in bank 1).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BgTileRowVramOffset()
    {
        var inWindow = _inWindow && _isWindowDrawingEnabled;
        var rowY = inWindow ? (_windowLineCounter & 0x07) : ((_ly + _scy) & 0x07);
        if ((_fetcherTileAttr & 0x40) != 0) rowY = 7 - rowY;

        var bankBase = (_fetcherTileAttr & 0x08) << 10; // bit 3 → +0x2000
        var tileDataBase = _bgTilePixelsBase;
        return bankBase + tileDataBase + ((_fetcherTileId ^ _bgTileFlipBit) << 4) + (rowY << 1);
    }

    // Window-vs-BG is decided per fetch on the live LCDC.WindowEnable bit, not
    // the activation latch alone — clearing the bit mid-window stops further
    // window fetches and the fetcher reverts to BG (with whatever _fetcherX
    // window left it at, matching hardware's glitchy resume).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelsFetcher_GetTile()
    {
        int tileX, tileY, mapOffset;
        if (_inWindow && _isWindowDrawingEnabled)
        {
            tileX = _fetcherX & 0x1F;
            tileY = _windowLineCounter >> 3;
            mapOffset = _windowTileMapBase + (tileY << 5) + tileX;
        }
        else
        {
            tileX = ((_scx >> 3) + _fetcherX) & 0x1F;
            tileY = ((_ly + _scy) & 0xFF) >> 3;
            mapOffset = _bgTileMapBase + (tileY << 5) + tileX;
        }
        // Bank 0 holds the tile-id (unchanged from DMG). Bank 1 holds the CGB
        // attribute byte at the same offset; in DMG mode the CPU can't write
        // bank 1 so it stays zero, which falls back to "palette 0, bank 0,
        // no flips, no priority" — i.e. DMG semantics.
        _fetcherTileId = _vram[mapOffset];
        _fetcherTileAttr = _vram[0x2000 + mapOffset];
        _fetcherState = BgPixelsFetcherState.GetTilePixelsLow;
    }
}
