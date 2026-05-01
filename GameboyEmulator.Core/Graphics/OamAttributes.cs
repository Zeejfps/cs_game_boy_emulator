namespace GameBoyEmulator.Core.Graphics;

[Flags]
public enum OamAttributes : byte
{
    None       = 0,
    Palette    = 1 << 4, // DMG: 0=OBP0, 1=OBP1
    XFlip      = 1 << 5,
    YFlip      = 1 << 6,
    BgPriority = 1 << 7, // 1=BG colors 1-3 hide sprite
}
