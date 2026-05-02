using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopBc()
    {
        Rbc = ReadWord(Sp);
        Sp += 2;
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PushBc()
    {
        Sp -= 2;
        Write((ushort)(Sp + 1), Rb);
        Write(Sp, Rc);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopDe()
    {
        Rde = ReadWord(Sp);
        Sp += 2;
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PushDe()
    {
        Sp -= 2;
        Write((ushort)(Sp + 1), Rd);
        Write(Sp, Re);
        return 16;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopHl()
    {
        Rhl = ReadWord(Sp);
        Sp += 2;
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PushHl()
    {
        Sp -= 2;
        Write((ushort)(Sp + 1), Rh);
        Write(Sp, Rl);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopAf()
    {
        Flags = (CpuFlags)Read(Sp);
        Ra = Read((ushort)(Sp + 1));
        Sp += 2;
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PushAf()
    {
        Sp -= 2;
        Write((ushort)(Sp + 1), Ra);
        Write(Sp, (byte)Flags);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdSpHl()
    {
        Sp = Rhl;
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHlSpR8()
    {
        var r8 = (sbyte)Fetch();
        Rhl = AddSpSigned(r8);
        return 12;
    }
}
