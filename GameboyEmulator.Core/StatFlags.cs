namespace GameBoyEmulator.Core;

// Bits 0-1 (mode) are not represented here; they come from PpuMode.
[Flags]
public enum StatFlags : byte
{
    None       = 0,
    LycEqualLy = 1 << 2,
    HBlankIrq  = 1 << 3,
    VBlankIrq  = 1 << 4,
    OamIrq     = 1 << 5,
    LycIrq     = 1 << 6,
    Unused     = 1 << 7, // always reads 1
    Sources    = HBlankIrq | VBlankIrq | OamIrq | LycIrq,
}
