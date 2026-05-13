namespace GameBoyEmulator.Core.Graphics;

[Flags]
public enum OamAttributes : byte
{
    None         = 0,
    CgbPalette   = 0x07,   // CGB: OBJ palette index 0..7 (bits 0..2)
    CgbVramBank  = 1 << 3, // CGB: 0=bank 0, 1=bank 1 for tile data
    Palette      = 1 << 4, // DMG: 0=OBP0, 1=OBP1
    XFlip        = 1 << 5,
    YFlip        = 1 << 6,
    BgPriority   = 1 << 7, // 1=BG colors 1-3 hide sprite
}
