using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBb() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBc() { Rb = Rc; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBd() { Rb = Rd; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBe() { Rb = Re; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBh() { Rb = Rh; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBl() { Rb = Rl; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBa() { Rb = Ra; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdBm() { Rb = ReadFromBus(Rhl); }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCb() { Rc = Rb; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCc() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCd() { Rc = Rd; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCe() { Rc = Re; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCh() { Rc = Rh; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCl() { Rc = Rl; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCa() { Rc = Ra; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdCm() { Rc = ReadFromBus(Rhl); }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDb() { Rd = Rb; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDc() { Rd = Rc; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDd() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDe() { Rd = Re; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDh() { Rd = Rh; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDl() { Rd = Rl; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDa() { Rd = Ra; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdDm() { Rd = ReadFromBus(Rhl); }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdEb() { Re = Rb; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdEc() { Re = Rc; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdEd() { Re = Rd; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdEe() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdEh() { Re = Rh; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdEl() { Re = Rl; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdEa() { Re = Ra; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdEm() { Re = ReadFromBus(Rhl); }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHb() { Rh = Rb; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHc() { Rh = Rc; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHd() { Rh = Rd; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHe() { Rh = Re; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHh() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHl() { Rh = Rl; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHa() { Rh = Ra; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdHm() { Rh = ReadFromBus(Rhl); }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdLb() { Rl = Rb; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdLc() { Rl = Rc; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdLd() { Rl = Rd; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdLe() { Rl = Re; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdLh() { Rl = Rh; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdLl() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdLa() { Rl = Ra; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdLm() { Rl = ReadFromBus(Rhl); }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAb() { Ra = Rb; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAc() { Ra = Rc; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAd() { Ra = Rd; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAe() { Ra = Re; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAh() { Ra = Rh; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAl() { Ra = Rl; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAa() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdAm() { Ra = ReadFromBus(Rhl); }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdMb() { WriteToBus(Rhl, Rb); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdMc() { WriteToBus(Rhl, Rc); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdMd() { WriteToBus(Rhl, Rd); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdMe() { WriteToBus(Rhl, Re); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdMh() { WriteToBus(Rhl, Rh); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdMl() { WriteToBus(Rhl, Rl); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LdMa() { WriteToBus(Rhl, Ra); }
}
