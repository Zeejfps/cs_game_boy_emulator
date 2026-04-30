# PPU Background Tile Fetch

How the Game Boy PPU turns VRAM into background pixels. Reference for the BG path of `RenderScanline`.

## VRAM is two things stuck together

VRAM is `0x8000-0x9FFF` (8 KB). It holds two completely different kinds of data:

```
0x8000 ┌──────────────────────────────────────┐ ─┐
       │                                      │  │
       │  Tile data block 0 (128 tiles)       │  │
       │  16 bytes per tile × 128 = 0x800     │  │
0x8800 ├──────────────────────────────────────┤  │
       │                                      │  │  TILE DATA (PATTERNS)
       │  Tile data block 1 (128 tiles)       │  │  6 KB total
       │                                      │  │  Up to 384 distinct 8×8 patterns
0x9000 ├──────────────────────────────────────┤  │
       │                                      │  │
       │  Tile data block 2 (128 tiles)       │  │
       │                                      │  │
0x9800 ├──────────────────────────────────────┤ ─┤
       │  BG tile MAP 0 (32×32 indices)       │  │  TILE MAPS (LAYOUTS)
0x9C00 ├──────────────────────────────────────┤  │  2 KB total
       │  BG tile MAP 1 (32×32 indices)       │  │
0xA000 └──────────────────────────────────────┘ ─┘
```

- **Tile data** = the 8×8 *patterns* you can draw (think: a sprite sheet).
- **Tile map** = a 32×32 grid telling the PPU *which pattern to put where* on the background.

You can have the same tile pattern drawn 50 times across the screen — only one copy in tile data, fifty references in the tile map.

## The tile map: 1 byte per cell

Each map is a flat 32×32 array of bytes. Each byte is a **tile index** — "go look up tile #N in the tile data."

```
Tile map (32 × 32 = 1024 bytes)

         col 0   col 1   col 2  ...  col 31
row 0  ┌───────┬───────┬───────┬───┬───────┐
       │ 0x47  │ 0x47  │ 0x12  │...│ 0x00  │
row 1  ├───────┼───────┼───────┼───┼───────┤
       │ 0x47  │ 0x88  │ 0x88  │...│ 0x00  │
row 2  ├───────┼───────┼───────┼───┼───────┤
       │  ...                              │
       │                                   │
row 31 │ 0x00  │ 0x00  │ 0x00  │...│ 0x00  │
       └───────┴───────┴───────┴───┴───────┘

Each cell covers an 8×8-pixel area on the background.
32 cells × 8 = 256 pixels wide. Same vertically.
That's the 256×256 "world" the screen scrolls inside.
```

To find which map cell a world pixel `(worldX, worldY)` falls into:

```
tileCol  = worldX / 8   (which cell column)
tileRow  = worldY / 8   (which cell row)
pixelCol = worldX % 8   (which pixel column inside the cell)
pixelRow = worldY % 8   (which pixel row inside the cell)

tileIndex = vram[mapOffset + tileRow * 32 + tileCol]
```

## A single tile: 16 bytes, 2 bits per pixel

A tile is 8×8 pixels. Each pixel can be color 0/1/2/3 (2 bits). Naively that's 64×2 = 128 bits = 16 bytes — but they're laid out in a peculiar **planar** form:

```
A tile occupies 16 bytes.
2 bytes per row × 8 rows = 16.

Row 0 → bytes 0,1
Row 1 → bytes 2,3
Row 2 → bytes 4,5
...
Row 7 → bytes 14,15

For each row:
  even byte = LOW bit-plane of that row's 8 pixels
  odd  byte = HIGH bit-plane of that row's 8 pixels

Pixel n's color id = (high_byte.bit(7-n) << 1) | low_byte.bit(7-n)

Note: pixel 0 is in BIT 7, pixel 7 is in BIT 0  ← always trips people up
```

Worked example. Suppose row 3 of some tile is the bytes `0x3C` and `0x42`:

```
  bit:    7  6  5  4  3  2  1  0       (bit position in the byte)
  pixel:  0  1  2  3  4  5  6  7       (pixel column, left → right)

  lo = 0x3C =  0  0  1  1  1  1  0  0
  hi = 0x42 =  0  1  0  0  0  0  1  0

  cid     =  0  2  1  1  1  1  2  0    ← (hi<<1) | lo, per column
```

The two-bit-plane layout is hardware-friendly because the PPU fetches the low and high bytes in two consecutive memory accesses.

## Putting the map and data together

To draw pixel `(worldX, worldY)`:

```
              worldX, worldY
              (e.g. SCX+x, SCY+LY)
                  │
                  ▼
       ┌──────────┴──────────┐
       │                     │
   tileCol = X/8         pixelCol = X%8
   tileRow = Y/8         pixelRow = Y%8
       │                     │
       ▼                     │
  TILE MAP                   │
  byte at [row*32 + col]     │
       │                     │
       ▼                     │
  tileIndex (0-255)          │
       │                     │
       ▼                     │
  TILE DATA                  │
  16 bytes for this tile     │
       │                     │
       ▼                     │
  pick row → 2 bytes (lo, hi)│
       │                     │
       └──────────┬──────────┘
                  ▼
          extract bit (7-pixelCol)
          from each byte → cid 0..3
                  │
                  ▼
              BGP palette
                  │
                  ▼
           final shade 0..3 → framebuffer
```

## The two addressing modes (LCDC bit 4)

The Game Boy supports up to **384 unique tiles** in tile data (3 blocks of 128). But a tile map index is only **1 byte**, so it can only address 256 of them at a time. The two modes pick *which* 256 tiles are addressable:

```
Tile data layout (constant — always at 0x8000-0x97FF):

0x8000 ┌──────────────┐ block 0 (tiles 0..127)
0x8800 ├──────────────┤ block 1 (tiles 128..255 / shared)
0x9000 ├──────────────┤ block 2
0x9800 └──────────────┘

LCDC bit 4 = 1  ("$8000 method", unsigned)
   index 0   → 0x8000   ─┐
   index 127 → 0x87F0    │  blocks 0 + 1
   index 128 → 0x8800    │
   index 255 → 0x8FF0   ─┘
   addr = 0x8000 + index * 16

LCDC bit 4 = 0  ("$8800 method", signed)
   index  0  → 0x9000   ─┐
   index  127→ 0x97F0    │  blocks 2 + 1   ← block 1 is shared!
   index -128→ 0x8800    │
   index -1  → 0x8FF0   ─┘
   addr = 0x9000 + (sbyte)index * 16
```

**Block 1 (`0x8800-0x8FFF`) is shared by both modes.** That's deliberate — it lets a game store BG-only tiles in block 2, sprite-only tiles in block 0, and shared tiles in block 1, with both BG (using signed mode) and sprites (always unsigned from `0x8000`) able to reach the shared set.

In VRAM offset terms (subtract `0x8000`):

```
LCDC bit 4 = 1:  addr = 0x0000 +         index  * 16  (index unsigned 0..255)
LCDC bit 4 = 0:  addr = 0x1000 + (sbyte) index  * 16  (index signed -128..127)
```

**Unified branch-free formula.** Both modes can be expressed as a single arithmetic expression:

```
addr = base + ((index ^ flip) << 4)

   unsigned mode: base = 0x0000, flip = 0x00   → blocks 0+1
   signed   mode: base = 0x0800, flip = 0x80   → blocks 1+2
```

Why it works: in signed mode the high bit of `index` flips meaning (128-255 become *lower* addresses than 0-127). XOR'ing with `0x80` swaps the upper and lower halves of the 0..255 range, after which a plain unsigned multiply produces the right layout — you just need a different base to land in the right region of VRAM.

Verify against the table above:

| Mode | index | `index ^ flip` | `<<4` | `+base` | VRAM offset |
|---|---|---|---|---|---|
| unsigned | 0   | 0   | 0x000 | 0x000 | 0x0000 ✓ |
| unsigned | 255 | 255 | 0xFF0 | 0xFF0 | 0x0FF0 ✓ |
| signed   | 0   | 128 | 0x800 | 0x1000 | 0x1000 ✓ |
| signed   | 127 | 255 | 0xFF0 | 0x17F0 | 0x17F0 ✓ |
| signed   | 128 | 0   | 0x000 | 0x0800 | 0x0800 ✓ |
| signed   | 255 | 127 | 0x7F0 | 0xFF0  | 0x0FF0 ✓ |

In the code, `base` and `flip` are picked once per scanline:

```csharp
var (tileDataBase, flipBit) = (_lcdc & LcdcTileData) != 0
    ? (TileDataUnsignedBase, TileDataUnsignedFlip)
    : (TileDataSignedBase,   TileDataSignedFlip);

// per tile:
var rowAddr = tileDataBase + ((tileIndex ^ flipBit) << 4) + pixelRow * 2;
```

No branch in the inner loop, no signed cast, just XOR + shift + add.

## A scanline as a movie

Concrete example: `LY = 10`, `SCY = 5`, `SCX = 12`, LCDC bit 3 = 0 (map at `0x9800`), bit 4 = 1 (unsigned data).

```
worldY = (5 + 10) & 0xFF      = 15
tileRow = 15 / 8              = 1
pixelRow = 15 % 8             = 7        ← bottom row of the tile

Hoisted out of inner loop. Now per-pixel:

x = 0:
  worldX  = (12 + 0) & 0xFF   = 12
  tileCol = 12 / 8            = 1
  tileIndex = vram[0x1800 + 1*32 + 1]   = vram[0x1821]   = (some byte, say 0x4A)
  tileAddr = 0x0000 + 0x4A*16           = 0x04A0
  rowAddr  = 0x04A0 + 7*2               = 0x04AE
  lo = vram[0x04AE], hi = vram[0x04AF]
  bit = 7 - (12 & 7) = 7 - 4 = 3
  cid = ((hi >> 3) & 1) << 1 | ((lo >> 3) & 1)

x = 1:
  worldX  = 13
  tileCol = 1                ← same tile as before!
  ... same tile fetch, just different bit ...
  bit = 7 - (13 & 7) = 7 - 5 = 2

(x=0..3 are all in tileCol 1; the same lo/hi could be reused)

x = 4:
  worldX  = 16
  tileCol = 2                ← new tile
  tileIndex = vram[0x1800 + 1*32 + 2] = vram[0x1822]
  ... new lo/hi fetched ...
```

In the simple loop the same `lo`/`hi` is fetched for 8 consecutive pixels. An optimized version fetches once per 8 pixels and shifts through — usually 4-8× faster.

## Mental images

**The "world" and the screen window into it:**

```
    ┌──────────────── 256 px ──────────────┐
    │                                      │
    │        BG world (32x32 tiles)        │
    │                                      │
    │      ┌──── 160 ────┐                 │
    │      │             │                 │
    │  SCY │   SCREEN    │                 │  256 px
    │      │             │                 │
    │      └─────────────┘                 │
    │       SCX                            │
    │                                      │
    └──────────────────────────────────────┘
    The world wraps — go past the right edge and you reappear on the left.
```

**A tile's bytes → its pixels:**

```
16 bytes:  L0 H0 L1 H1 L2 H2 ... L7 H7
            └──┘  └──┘  └──┘     └──┘
            row 0 row 1 row 2    row 7

For each row, two bytes combine bit-by-bit:
   pixel 0 ← bit 7    pixel 4 ← bit 3
   pixel 1 ← bit 6    pixel 5 ← bit 2
   pixel 2 ← bit 5    pixel 6 ← bit 1
   pixel 3 ← bit 4    pixel 7 ← bit 0

cid(pixel n) = (H bit (7-n)) * 2 + (L bit (7-n))
```

## Where this is used

- **Background** — what's described above. `RenderScanline` in `Ppu.cs`.
- **Window** — same machinery, separate tile-map selection bit (LCDC bit 6) and its own `WX`/`WY` origin instead of `SCX`/`SCY`. No wraparound — it's a rectangle pinned to a position.
- **Sprites** — reuse the **tile data** half (always unsigned, base `0x8000`). Positions and attributes come from OAM, not from a tile map. 8×16 mode pairs two consecutive tile indices vertically.
