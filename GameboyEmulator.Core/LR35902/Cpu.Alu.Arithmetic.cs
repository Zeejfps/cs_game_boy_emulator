using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Add(byte value)
    {
        Ra = Add8(Ra, value, false);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Adc(byte value)
    {
        Ra = Add8(Ra, value, (Flags & CpuFlags.C) != 0);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddB() => Add(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddC() => Add(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddD() => Add(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddE() => Add(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddH() => Add(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddL() => Add(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddA() => Add(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddM()
    {
        Ra = Add8(Ra, _mmu.Read(Rhl), false);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcB() => Adc(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcC() => Adc(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcD() => Adc(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcE() => Adc(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcH() => Adc(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcL() => Adc(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcA() => Adc(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AdcM()
    {
        Ra = Add8(Ra, _mmu.Read(Rhl), (Flags & CpuFlags.C) != 0);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Sub(byte value)
    {
        Ra = Sub8(Ra, value, false);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubB() => Sub(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubC() => Sub(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubD() => Sub(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubE() => Sub(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubH() => Sub(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubL() => Sub(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubA() => Sub(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SubM()
    {
        Ra = Sub8(Ra, _mmu.Read(Rhl), false);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Sbc(byte value)
    {
        Ra = Sub8(Ra, value, (Flags & CpuFlags.C) != 0);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcB() => Sbc(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcC() => Sbc(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcD() => Sbc(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcE() => Sbc(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcH() => Sbc(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcL() => Sbc(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcA() => Sbc(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbcM()
    {
        Ra = Sub8(Ra, _mmu.Read(Rhl), (Flags & CpuFlags.C) != 0);
        return 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Inc(byte value)
    {
        var carry = (Flags & CpuFlags.C) != 0;
        var result = Add8(value, 1, false);
        SetC(carry);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncB() { Rb = Inc(Rb); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncC() { Rc = Inc(Rc); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncD() { Rd = Inc(Rd); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncE() { Re = Inc(Re); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncH() { Rh = Inc(Rh); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncL() { Rl = Inc(Rl); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncA() { Ra = Inc(Ra); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IncM()
    {
        _mmu.Write(Rhl, Inc(_mmu.Read(Rhl)));
        return 12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Dec(byte value)
    {
        var carry = (Flags & CpuFlags.C) != 0;
        var result = Sub8(value, 1, false);
        SetC(carry);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecB() { Rb = Dec(Rb); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecC() { Rc = Dec(Rc); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecD() { Rd = Dec(Rd); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecE() { Re = Dec(Re); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecH() { Rh = Dec(Rh); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecL() { Rl = Dec(Rl); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecA() { Ra = Dec(Ra); return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecM()
    {
        _mmu.Write(Rhl, Dec(_mmu.Read(Rhl)));
        return 12;
    }
}
