using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopBc()
    {
        Rbc = ReadWord(Sp);
        Sp += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushBc()
    {
        Tick(4);
        Sp -= 2;
        Write((ushort)(Sp + 1), Rb);
        Write(Sp, Rc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopDe()
    {
        Rde = ReadWord(Sp);
        Sp += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushDe()
    {
        Tick(4);
        Sp -= 2;
        Write((ushort)(Sp + 1), Rd);
        Write(Sp, Re);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopHl()
    {
        Rhl = ReadWord(Sp);
        Sp += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushHl()
    {
        Tick(4);
        Sp -= 2;
        Write((ushort)(Sp + 1), Rh);
        Write(Sp, Rl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopAf()
    {
        Flags = (CpuFlags)Read(Sp);
        Ra = Read((ushort)(Sp + 1));
        Sp += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushAf()
    {
        Tick(4);
        Sp -= 2;
        Write((ushort)(Sp + 1), Ra);
        Write(Sp, (byte)Flags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdSpHl()
    {
        Sp = Rhl;
        Tick(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHlSpR8()
    {
        var r8 = (sbyte)Fetch();
        Rhl = AddSpSigned(r8);
        Tick(4);
    }
}
