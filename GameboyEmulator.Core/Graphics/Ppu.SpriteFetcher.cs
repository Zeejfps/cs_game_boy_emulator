using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.Graphics;

// Sprite fetcher and sprite-FIFO merge. State fields live in Ppu.cs.
public sealed partial class Ppu
{
    // Pick the next sprite whose visible column is at or before the current
    // _lcdX. DMG: sprites are X-sorted, so the first not-yet-triggered ready
    // sprite wins (lower X, OAM-index breaks ties from the stable sort).
    // CGB: sprites stay in OAM-scan order; among ready ones, lowest OAM index
    // wins regardless of X. The _spriteTriggeredMask tracks which sprites
    // have already fired so we don't refetch.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryStartSpriteFetch()
    {
        if (!_isObjectDrawingEnabled) return false;

        var bestSlot = -1;
        var bestOam = 0xFF;
        for (var i = 0; i < _spriteCount; i++)
        {
            if ((_spriteTriggeredMask & (1 << i)) != 0) continue;
            var s = _sprites[i];
            if (s.X == 0 || s.X >= 168)
            {
                // Off-screen sprites are skipped permanently so the scan
                // doesn't keep re-evaluating them every dot.
                _spriteTriggeredMask |= 1 << i;
                continue;
            }
            if (s.X > _lcdX + 8) continue;
            if (!_isCgb)
            {
                // DMG: sprites are X-sorted; first ready slot wins.
                bestSlot = i;
                break;
            }
            // CGB: scan all to find the lowest OAM index among ready sprites.
            if (s.OamIndex < bestOam)
            {
                bestSlot = i;
                bestOam = s.OamIndex;
            }
        }

        if (bestSlot < 0) return false;

        _spriteTriggeredMask |= 1 << bestSlot;
        _activeSprite = _sprites[bestSlot];
        _fetchingSprite = true;
        _spriteFetcherState = SpriteFetcherState.GetTile;
        // Real hardware aborts the BG fetch in flight; the BG FIFO is left
        // alone but the fetcher restarts at GetTile.
        _fetcherState = BgPixelsFetcherState.GetTile;
        return true;
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
        if (_activeSprite.Attributes.HasFlag(OamAttributes.YFlip))
            row = _spriteHeight - 1 - row;
        var tileId = _activeSprite.TileId;
        if (_spriteHeight == 16)
        {
            if (row < 8) tileId = (byte)(tileId & 0xFE);
            else { tileId = (byte)(tileId | 0x01); row -= 8; }
        }
        // Sprites always use the unsigned 0x8000 base. CGB attribute bit 3
        // picks VRAM bank for tile data (+0x2000); DMG mode never sets it.
        var bankBase = _isCgb && _activeSprite.Attributes.HasFlag(OamAttributes.CgbVramBank) ? 0x2000 : 0;
        return bankBase + (tileId << 4) + (row << 1);
    }
    
    // Merge the 8 fetched sprite pixels into the sprite FIFO. Existing opaque
    // pixels (color != 0) are preserved — earlier-fetched sprites win on overlap,
    // which gives DMG priority for free since we fetch in (X asc, OAM idx) order.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MergeIntoSpriteFifo()
    {
        var low = _spriteFetcherTileLow;
        var high = _spriteFetcherTileHigh;
        if (_activeSprite.Attributes.HasFlag(OamAttributes.XFlip))
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

        // Palette is a 3-bit field. CGB pulls bits 0..2 of the attribute byte;
        // DMG only uses bit 4 (OBP0/OBP1) which we map to palette index 0/1 so
        // the same lookup `_objPaletteTable[palette*4 + color]` works for both.
        var attr = (byte)_activeSprite.Attributes;
        int paletteIdx;
        if (_isCgb)
            paletteIdx = attr & 0x07;
        else
            paletteIdx = (attr & (byte)OamAttributes.Palette) != 0 ? 1 : 0;
        var p0Byte = (paletteIdx & 0x01) != 0 ? (byte)0xFF : (byte)0x00;
        var p1Byte = (paletteIdx & 0x02) != 0 ? (byte)0xFF : (byte)0x00;
        var p2Byte = (paletteIdx & 0x04) != 0 ? (byte)0xFF : (byte)0x00;
        var bgPrioByte = (attr & (byte)OamAttributes.BgPriority) != 0 ? (byte)0xFF : (byte)0x00;

        // DMG rule: slots already holding an opaque sprite pixel are preserved
        // — earlier-fetched sprites win on overlap. Since DMG sprites fetch in
        // (X asc, OAM-index asc) order, "first-fetched wins" gives the correct
        // DMG priority for free.
        //
        // CGB rule: lowest OAM index wins regardless of fetch order. We track
        // each slot's source OAM index in _spriteFifo.OamIndices and overwrite
        // when the new sprite has a lower index. Empty slots take whatever
        // comes; new transparent pixels never overwrite opaque ones either way.
        var occupiedMask = _spriteFifo.Count == 0 ? (byte)0 : (byte)(0xFF << (8 - _spriteFifo.Count));
        var existingOpaque = (byte)((_spriteFifo.Low | _spriteFifo.High) & occupiedMask);
        byte writeMask;
        var newOam = _activeSprite.OamIndex;
        if (!_isCgb)
        {
            writeMask = (byte)(pixelMask & ~existingOpaque);
        }
        else
        {
            // Per-slot decision: write where (existing transparent OR
            // newOam < existingOam) AND the new sprite covers the slot.
            byte mask = 0;
            for (var s = 0; s < 8; s++)
            {
                var slotBit = (byte)(0x80 >> s);
                if ((pixelMask & slotBit) == 0) continue;
                if ((existingOpaque & slotBit) == 0)
                {
                    mask |= slotBit;
                    continue;
                }
                var existingOam = (byte)(_spriteFifo.OamIndices >> ((7 - s) * 8));
                if (newOam < existingOam) mask |= slotBit;
            }
            writeMask = mask;
        }

        _spriteFifo.Low        = (byte)((_spriteFifo.Low        & ~writeMask) | (newLow      & writeMask));
        _spriteFifo.High       = (byte)((_spriteFifo.High       & ~writeMask) | (newHigh     & writeMask));
        _spriteFifo.Palette0   = (byte)((_spriteFifo.Palette0   & ~writeMask) | (p0Byte      & writeMask));
        _spriteFifo.Palette1   = (byte)((_spriteFifo.Palette1   & ~writeMask) | (p1Byte      & writeMask));
        _spriteFifo.Palette2   = (byte)((_spriteFifo.Palette2   & ~writeMask) | (p2Byte      & writeMask));
        _spriteFifo.BgPriority = (byte)((_spriteFifo.BgPriority & ~writeMask) | (bgPrioByte  & writeMask));

        // Record OAM index for slots we just wrote so future merges can compare.
        if (writeMask != 0)
        {
            var wm = writeMask;
            ulong oamUpdates = 0;
            ulong clearMask = 0;
            for (var s = 0; s < 8; s++)
            {
                var slotBit = (byte)(0x80 >> s);
                if ((wm & slotBit) == 0) continue;
                var slotShift = (7 - s) * 8;
                oamUpdates |= (ulong)newOam << slotShift;
                clearMask |= 0xFFUL << slotShift;
            }
            _spriteFifo.OamIndices = (_spriteFifo.OamIndices & ~clearMask) | oamUpdates;
        }
        if (_spriteFifo.Count < 8) _spriteFifo.Count = 8;
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
