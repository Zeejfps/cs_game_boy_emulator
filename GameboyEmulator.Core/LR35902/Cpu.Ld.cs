using System.Runtime.CompilerServices;

namespace GameboyEmulator.Core.LR35902;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBb() { return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBc() { Rb = Rc; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBd() { Rb = Rd; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBe() { Rb = Re; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBh() { Rb = Rh; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBl() { Rb = Rl; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBa() { Rb = Ra; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdBm() { Rb = _mmu.Read(Rhl); return 8; }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCb() { Rc = Rb; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCc() { return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCd() { Rc = Rd; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCe() { Rc = Re; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCh() { Rc = Rh; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCl() { Rc = Rl; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCa() { Rc = Ra; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdCm() { Rc = _mmu.Read(Rhl); return 8; }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDb() { Rd = Rb; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDc() { Rd = Rc; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDd() { return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDe() { Rd = Re; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDh() { Rd = Rh; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDl() { Rd = Rl; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDa() { Rd = Ra; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdDm() { Rd = _mmu.Read(Rhl); return 8; }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdEb() { Re = Rb; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdEc() { Re = Rc; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdEd() { Re = Rd; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdEe() { return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdEh() { Re = Rh; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdEl() { Re = Rl; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdEa() { Re = Ra; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdEm() { Re = _mmu.Read(Rhl); return 8; }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHb() { Rh = Rb; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHc() { Rh = Rc; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHd() { Rh = Rd; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHe() { Rh = Re; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHh() { return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHl() { Rh = Rl; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHa() { Rh = Ra; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdHm() { Rh = _mmu.Read(Rhl); return 8; }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdLb() { Rl = Rb; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdLc() { Rl = Rc; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdLd() { Rl = Rd; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdLe() { Rl = Re; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdLh() { Rl = Rh; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdLl() { return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdLa() { Rl = Ra; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdLm() { Rl = _mmu.Read(Rhl); return 8; }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAb() { Ra = Rb; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAc() { Ra = Rc; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAd() { Ra = Rd; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAe() { Ra = Re; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAh() { Ra = Rh; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAl() { Ra = Rl; return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAa() { return 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdAm() { Ra = _mmu.Read(Rhl); return 8; }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdMb() { _mmu.Write(Rhl, Rb); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdMc() { _mmu.Write(Rhl, Rc); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdMd() { _mmu.Write(Rhl, Rd); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdMe() { _mmu.Write(Rhl, Re); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdMh() { _mmu.Write(Rhl, Rh); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdMl() { _mmu.Write(Rhl, Rl); return 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LdMa() { _mmu.Write(Rhl, Ra); return 8; }
}
