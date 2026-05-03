using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncBc() { Rbc = (ushort)(Rbc + 1); AdvanceClock(4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncDe() { Rde = (ushort)(Rde + 1); AdvanceClock(4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncHl() { Rhl = (ushort)(Rhl + 1); AdvanceClock(4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncSp() { Sp = (ushort)(Sp + 1); AdvanceClock(4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecBc() { Rbc = (ushort)(Rbc - 1); AdvanceClock(4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecDe() { Rde = (ushort)(Rde - 1); AdvanceClock(4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecHl() { Rhl = (ushort)(Rhl - 1); AdvanceClock(4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecSp() { Sp = (ushort)(Sp - 1); AdvanceClock(4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHl(ushort value) { AddHL(value); AdvanceClock(4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHlBc() => AddHl(Rbc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHlDe() => AddHl(Rde);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHlHl() => AddHl(Rhl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHlSp() => AddHl(Sp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddSpR8()
    {
        var r8 = (sbyte)Fetch();
        Sp = AddSpSigned(r8);
        AdvanceClock(8);
    }
}
