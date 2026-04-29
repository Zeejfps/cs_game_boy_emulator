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
        return 7;
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
        return 7;
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
        return 7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Sbb(byte value)
    {
        Ra = Sub8(Ra, value, (Flags & CpuFlags.C) != 0);
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbbB() => Sbb(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbbC() => Sbb(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbbD() => Sbb(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbbE() => Sbb(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbbH() => Sbb(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbbL() => Sbb(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbbA() => Sbb(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SbbM()
    {
        Ra = Sub8(Ra, _mmu.Read(Rhl), (Flags & CpuFlags.C) != 0);
        return 7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Inr(byte value)
    {
        var carry = (Flags & CpuFlags.C) != 0;
        var result = Add8(value, 1, false);
        SetC(carry);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InrB() { Rb = Inr(Rb); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InrC() { Rc = Inr(Rc); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InrD() { Rd = Inr(Rd); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InrE() { Re = Inr(Re); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InrH() { Rh = Inr(Rh); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InrL() { Rl = Inr(Rl); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InrA() { Ra = Inr(Ra); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InrM()
    {
        _mmu.Write(Rhl, Inr(_mmu.Read(Rhl)));
        return 10;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Dcr(byte value)
    {
        var carry = (Flags & CpuFlags.C) != 0;
        var result = Sub8(value, 1, false);
        SetC(carry);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcrB() { Rb = Dcr(Rb); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcrC() { Rc = Dcr(Rc); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcrD() { Rd = Dcr(Rd); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcrE() { Re = Dcr(Re); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcrH() { Rh = Dcr(Rh); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcrL() { Rl = Dcr(Rl); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcrA() { Ra = Dcr(Ra); return 5; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DcrM()
    {
        _mmu.Write(Rhl, Dcr(_mmu.Read(Rhl)));
        return 10;
    }
}
