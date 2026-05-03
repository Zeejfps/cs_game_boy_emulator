using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Add(byte value) { Ra = Add8(Ra, value, false); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Adc(byte value) { Ra = Add8(Ra, value, (Flags & CpuFlags.C) != 0); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddB() => Add(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddC() => Add(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddD() => Add(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddE() => Add(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddH() => Add(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddL() => Add(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddA() => Add(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddM() { Ra = Add8(Ra, ReadFromBus(Rhl), false); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdcB() => Adc(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdcC() => Adc(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdcD() => Adc(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdcE() => Adc(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdcH() => Adc(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdcL() => Adc(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdcA() => Adc(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdcM() { Ra = Add8(Ra, ReadFromBus(Rhl), (Flags & CpuFlags.C) != 0); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Sub(byte value) { Ra = Sub8(Ra, value, false); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SubB() => Sub(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SubC() => Sub(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SubD() => Sub(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SubE() => Sub(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SubH() => Sub(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SubL() => Sub(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SubA() => Sub(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SubM() { Ra = Sub8(Ra, ReadFromBus(Rhl), false); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Sbc(byte value) { Ra = Sub8(Ra, value, (Flags & CpuFlags.C) != 0); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SbcB() => Sbc(Rb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SbcC() => Sbc(Rc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SbcD() => Sbc(Rd);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SbcE() => Sbc(Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SbcH() => Sbc(Rh);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SbcL() => Sbc(Rl);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SbcA() => Sbc(Ra);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SbcM() { Ra = Sub8(Ra, ReadFromBus(Rhl), (Flags & CpuFlags.C) != 0); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Inc(byte value)
    {
        var carry = (Flags & CpuFlags.C) != 0;
        var result = Add8(value, 1, false);
        SetC(carry);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncB() { Rb = Inc(Rb); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncC() { Rc = Inc(Rc); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncD() { Rd = Inc(Rd); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncE() { Re = Inc(Re); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncH() { Rh = Inc(Rh); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncL() { Rl = Inc(Rl); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncA() { Ra = Inc(Ra); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncM() { WriteToBus(Rhl, Inc(ReadFromBus(Rhl))); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Dec(byte value)
    {
        var carry = (Flags & CpuFlags.C) != 0;
        var result = Sub8(value, 1, false);
        SetC(carry);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecB() { Rb = Dec(Rb); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecC() { Rc = Dec(Rc); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecD() { Rd = Dec(Rd); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecE() { Re = Dec(Re); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecH() { Rh = Dec(Rh); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecL() { Rl = Dec(Rl); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecA() { Ra = Dec(Ra); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecM() { WriteToBus(Rhl, Dec(ReadFromBus(Rhl))); }
}
