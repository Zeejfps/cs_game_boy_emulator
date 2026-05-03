using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopBc()
    {
        Rbc = ReadWordFromBus(Sp);
        Sp += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushBc()
    {
        AdvanceClock(4);
        Sp -= 2;
        WriteToBus((ushort)(Sp + 1), Rb);
        WriteToBus(Sp, Rc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopDe()
    {
        Rde = ReadWordFromBus(Sp);
        Sp += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushDe()
    {
        AdvanceClock(4);
        Sp -= 2;
        WriteToBus((ushort)(Sp + 1), Rd);
        WriteToBus(Sp, Re);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopHl()
    {
        Rhl = ReadWordFromBus(Sp);
        Sp += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushHl()
    {
        AdvanceClock(4);
        Sp -= 2;
        WriteToBus((ushort)(Sp + 1), Rh);
        WriteToBus(Sp, Rl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopAf()
    {
        Flags = (CpuFlags)ReadFromBus(Sp);
        Ra = ReadFromBus((ushort)(Sp + 1));
        Sp += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushAf()
    {
        AdvanceClock(4);
        Sp -= 2;
        WriteToBus((ushort)(Sp + 1), Ra);
        WriteToBus(Sp, (byte)Flags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdSpHl()
    {
        Sp = Rhl;
        AdvanceClock(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHlSpR8()
    {
        var r8 = (sbyte)Fetch();
        Rhl = AddSpSigned(r8);
        AdvanceClock(4);
    }
}
