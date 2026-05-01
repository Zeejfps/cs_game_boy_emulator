using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.Graphics;

// Sprite fetcher and sprite-FIFO merge. State fields live in Ppu.cs.
public sealed partial class Ppu
{
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
    private void SpriteFetcher_Tick()
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
}
