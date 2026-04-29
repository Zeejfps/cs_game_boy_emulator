namespace GameboyEmulator.Core.LR35902;

[Flags]
public enum CpuFlags : byte
{
    None = 0,
    C = 1 << 4,
    H = 1 << 5,
    N = 1 << 6,
    Z = 1 << 7,
    All = Z | N | H | C,
}
