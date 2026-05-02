using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdABc() { Ra = Read(Rbc); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdADe() { Ra = Read(Rde); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBcA() { Write(Rbc, Ra); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDeA() { Write(Rde, Ra); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHlIncA()
    {
        var hl = Rhl;
        Write(hl, Ra);
        Rhl = (ushort)(hl + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAHlInc()
    {
        var hl = Rhl;
        Ra = Read(hl);
        Rhl = (ushort)(hl + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHlDecA()
    {
        var hl = Rhl;
        Write(hl, Ra);
        Rhl = (ushort)(hl - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAHlDec()
    {
        var hl = Rhl;
        Ra = Read(hl);
        Rhl = (ushort)(hl - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdA16A()
    {
        var address = FetchWord();
        Write(address, Ra);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAA16()
    {
        var address = FetchWord();
        Ra = Read(address);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdhA8A()
    {
        var offset = Fetch();
        Write((ushort)(0xFF00 + offset), Ra);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdhAA8()
    {
        var offset = Fetch();
        Ra = Read((ushort)(0xFF00 + offset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCA() { Write((ushort)(0xFF00 + Rc), Ra); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAC() { Ra = Read((ushort)(0xFF00 + Rc)); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdA16Sp()
    {
        var address = FetchWord();
        WriteWord(address, Sp);
    }
}
