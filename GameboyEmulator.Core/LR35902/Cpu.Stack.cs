using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopBc()
    {
        Rbc = _mmu.ReadWord(Sp);
        Sp += 2;
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PushBc()
    {
        Sp -= 2;
        _mmu.Write((ushort)(Sp + 1), Rb);
        _mmu.Write(Sp, Rc);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopDe()
    {
        Rde = _mmu.ReadWord(Sp);
        Sp += 2;
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PushDe()
    {
        Sp -= 2;
        _mmu.Write((ushort)(Sp + 1), Rd);
        _mmu.Write(Sp, Re);
        return 16;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopHl()
    {
        Rhl = _mmu.ReadWord(Sp);
        Sp += 2;
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PushHl()
    {
        Sp -= 2;
        _mmu.Write((ushort)(Sp + 1), Rh);
        _mmu.Write(Sp, Rl);
        return 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PopAf()
    {
        Flags = (CpuFlags)_mmu.Read(Sp);
        Ra = _mmu.Read((ushort)(Sp + 1));
        Sp += 2;
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PushAf()
    {
        Sp -= 2;
        _mmu.Write((ushort)(Sp + 1), Ra);
        _mmu.Write(Sp, (byte)Flags);
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
