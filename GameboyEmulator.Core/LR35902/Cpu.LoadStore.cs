using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdABc() { Ra = ReadFromBus(Rbc); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdADe() { Ra = ReadFromBus(Rde); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBcA() { WriteToBus(Rbc, Ra); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDeA() { WriteToBus(Rde, Ra); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHlIncA()
    {
        var hl = Rhl;
        WriteToBus(hl, Ra);
        Rhl = (ushort)(hl + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAHlInc()
    {
        var hl = Rhl;
        Ra = ReadFromBus(hl);
        Rhl = (ushort)(hl + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHlDecA()
    {
        var hl = Rhl;
        WriteToBus(hl, Ra);
        Rhl = (ushort)(hl - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAHlDec()
    {
        var hl = Rhl;
        Ra = ReadFromBus(hl);
        Rhl = (ushort)(hl - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdA16A()
    {
        var address = FetchWord();
        WriteToBus(address, Ra);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAA16()
    {
        var address = FetchWord();
        Ra = ReadFromBus(address);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdhA8A()
    {
        var offset = Fetch();
        WriteToBus((ushort)(0xFF00 + offset), Ra);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdhAA8()
    {
        var offset = Fetch();
        Ra = ReadFromBus((ushort)(0xFF00 + offset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCA() { WriteToBus((ushort)(0xFF00 + Rc), Ra); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAC() { Ra = ReadFromBus((ushort)(0xFF00 + Rc)); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdA16Sp()
    {
        var address = FetchWord();
        WriteWordToBus(address, Sp);
    }
}
