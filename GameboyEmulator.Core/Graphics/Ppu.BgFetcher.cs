using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.Graphics;

// BG / window pixel fetcher. State fields live in Ppu.cs.
public sealed partial class Ppu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BgPixelFetcher_Tick()
    {
        if (_fetchingSprite) { SpriteFetcher_Tick(); return; }
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
}
