using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu : ICpu
{
    private CpuFlags _flags;
    public CpuFlags Flags
    {
        get => _flags;
        set => _flags = value & (CpuFlags)0xF0;
    }
    public ushort Pc { get; set; }
    public ushort Sp { get; set; }
    public byte Ra { get; set; }
    public byte Rb { get; set; }
    public byte Rc { get; set; }
    public byte Rd { get; set; }
    public byte Re { get; set; }
    public byte Rh { get; set; }
    public byte Rl { get; set; }
    public bool InterruptMasterEnable { get; set; }
    public bool IsWaitingForInterrupt { get; internal set; }
    public bool IsSleeping { get; private set; }
    
    private ushort Rbc
    {
        get => (ushort)((Rb << 8) | Rc);
        set { Rb = (byte)(value >> 8); Rc = (byte)(value & 0xFF); }
    }
    
    private ushort Rde
    {
        get => (ushort)((Rd << 8) | Re);
        set { Rd = (byte)(value >> 8); Re = (byte)(value & 0xFF); }
    }
    
    private ushort Rhl
    {
        get => (ushort)((Rh << 8) | Rl);
        set { Rh = (byte)(value >> 8); Rl = (byte)(value & 0xFF); }
    }

    private int _enableInterruptsTimer;
    private bool _haltBugPending;
    private readonly IMemoryBus _mmu;
    private readonly IBusClock _busClock;
    private readonly IInterrupts _interrupts;

    public Cpu(IMemoryBus mmu, IBusClock busClock, IInterrupts interrupts)
    {
        _mmu = mmu;
        _interrupts = interrupts;
        _busClock = busClock;
    }

    public void Reset()
    {
        Flags = default;
        Pc = 0;
        Sp = 0;
        Ra = Rb = Rc = Rd = Re = Rh = Rl = 0;
        InterruptMasterEnable = false;
        IsWaitingForInterrupt = false;
        IsSleeping = false;
        _enableInterruptsTimer = 0;
        _haltBugPending = false;
    }

    // Used by ROM-level test harnesses that jump straight
    // to 0x0100.
    public void SkipBoot()
    {
        Pc = 0x0100;
        Sp = 0xFFFE;
        Ra = 0x01;
        Flags = CpuFlags.Z | CpuFlags.H | CpuFlags.C;
        Rbc = 0x0013;
        Rde = 0x00D8;
        Rhl = 0x014D;
        InterruptMasterEnable = false;
        IsWaitingForInterrupt = false;
        IsSleeping = false;
        _enableInterruptsTimer = 0;
        _haltBugPending = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int Step()
    {
        var cycles = Dispatch();
        UpdateInterruptTimer();
        return cycles;
    }
    
    private byte Read(ushort address)
    {
        _busClock.Tick(4);
        return _mmu.Read(address);
    }
    
    private ushort ReadWord(ushort address)
    {
        var lo = Read(address);
        var hi = Read((ushort)(address + 1));
        return (ushort)((hi << 8) | lo);
    }

    private void Write(ushort address, byte value)
    {
        _busClock.Tick(4);
        _mmu.Write(address, value);
    }
    
    private void WriteWord(ushort address, ushort value)
    {
        var lo = (byte)(value & 0xFF);
        var hi = (byte)(value >> 8);
        Write(address, lo);
        Write((ushort)(address + 1), hi);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int Dispatch()
    {
        if (IsSleeping)
        {
            // Joypad interrupt request wakes from IsSleeping. The wake step itself
            // does not dispatch and does not fetch — the next Step() prologue
            // handles dispatch (if IME=1) or normal fetch.
            if (_interrupts.IsRequested(InterruptType.Joypad))
            {
                IsSleeping = false;
            }
            return 4;
        }

        var pending = _interrupts.GetPending();
        if (IsWaitingForInterrupt && pending == InterruptType.None)
            return 4;
        
        IsWaitingForInterrupt = false;

        if (InterruptMasterEnable && pending != InterruptType.None)
            return ServicePendingInterrupt(pending);

        var opcode = Fetch();
        return Execute(opcode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateInterruptTimer()
    {
        if (_enableInterruptsTimer <= 0)
            return;

        _enableInterruptsTimer--;
        if (_enableInterruptsTimer == 0)
            InterruptMasterEnable = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int Execute(byte opcode) => opcode switch
    {
        // NOP
        0x00 => Nop(),

        // LD r,n8
        0x06 => LdBn(),
        0x0E => LdCn(),
        0x16 => LdDn(),
        0x1E => LdEn(),
        0x26 => LdHn(),
        0x2E => LdLn(),
        0x3E => LdAn(),
        0x36 => LdMn(),

        // LD B,r
        0x40 => LdBb(),
        0x41 => LdBc(),
        0x42 => LdBd(),
        0x43 => LdBe(),
        0x44 => LdBh(),
        0x45 => LdBl(),
        0x46 => LdBm(),
        0x47 => LdBa(),

        // LD C,r
        0x48 => LdCb(),
        0x49 => LdCc(),
        0x4A => LdCd(),
        0x4B => LdCe(),
        0x4C => LdCh(),
        0x4D => LdCl(),
        0x4E => LdCm(),
        0x4F => LdCa(),

        // LD D,r
        0x50 => LdDb(),
        0x51 => LdDc(),
        0x52 => LdDd(),
        0x53 => LdDe(),
        0x54 => LdDh(),
        0x55 => LdDl(),
        0x56 => LdDm(),
        0x57 => LdDa(),

        // LD E,r
        0x58 => LdEb(),
        0x59 => LdEc(),
        0x5A => LdEd(),
        0x5B => LdEe(),
        0x5C => LdEh(),
        0x5D => LdEl(),
        0x5E => LdEm(),
        0x5F => LdEa(),

        // LD H,r
        0x60 => LdHb(),
        0x61 => LdHc(),
        0x62 => LdHd(),
        0x63 => LdHe(),
        0x64 => LdHh(),
        0x65 => LdHl(),
        0x66 => LdHm(),
        0x67 => LdHa(),

        // LD L,r
        0x68 => LdLb(),
        0x69 => LdLc(),
        0x6A => LdLd(),
        0x6B => LdLe(),
        0x6C => LdLh(),
        0x6D => LdLl(),
        0x6E => LdLm(),
        0x6F => LdLa(),

        // LD A,r
        0x78 => LdAb(),
        0x79 => LdAc(),
        0x7A => LdAd(),
        0x7B => LdAe(),
        0x7C => LdAh(),
        0x7D => LdAl(),
        0x7E => LdAm(),
        0x7F => LdAa(),

        // LD (HL),r
        0x70 => LdMb(),
        0x71 => LdMc(),
        0x72 => LdMd(),
        0x73 => LdMe(),
        0x74 => LdMh(),
        0x75 => LdMl(),
        0x76 => Halt(),
        0x77 => LdMa(),

        // LD A,(rr)
        0x0A => LdABc(),
        0x1A => LdADe(),

        // LD (rr),A
        0x02 => LdBcA(),
        0x12 => LdDeA(),

        // LD rr,n16
        0x01 => LdBcNn(),
        0x11 => LdDeNn(),
        0x21 => LdHlNn(),
        0x31 => LdSpNn(),

        // ADD HL,rr
        0x09 => AddHlBc(),
        0x19 => AddHlDe(),
        0x29 => AddHlHl(),
        0x39 => AddHlSp(),

        // INC rr
        0x03 => IncBc(),
        0x13 => IncDe(),
        0x23 => IncHl(),
        0x33 => IncSp(),

        // DEC rr
        0x0B => DecBc(),
        0x1B => DecDe(),
        0x2B => DecHl(),
        0x3B => DecSp(),

        // Stack operations
        0xC1 => PopBc(),
        0xD1 => PopDe(),
        0xE1 => PopHl(),
        0xF1 => PopAf(),

        0xC5 => PushBc(),
        0xD5 => PushDe(),
        0xE5 => PushHl(),
        0xF5 => PushAf(),

        // Conditional returns
        0xC0 => RetNz(),
        0xC8 => RetZ(),
        0xD0 => RetNc(),
        0xD8 => RetC(),

        // ADD
        0x80 => AddB(),
        0x81 => AddC(),
        0x82 => AddD(),
        0x83 => AddE(),
        0x84 => AddH(),
        0x85 => AddL(),
        0x86 => AddM(),
        0x87 => AddA(),

        // ADC
        0x88 => AdcB(),
        0x89 => AdcC(),
        0x8A => AdcD(),
        0x8B => AdcE(),
        0x8C => AdcH(),
        0x8D => AdcL(),
        0x8E => AdcM(),
        0x8F => AdcA(),

        // SUB
        0x90 => SubB(),
        0x91 => SubC(),
        0x92 => SubD(),
        0x93 => SubE(),
        0x94 => SubH(),
        0x95 => SubL(),
        0x96 => SubM(),
        0x97 => SubA(),

        // SBC
        0x98 => SbcB(),
        0x99 => SbcC(),
        0x9A => SbcD(),
        0x9B => SbcE(),
        0x9C => SbcH(),
        0x9D => SbcL(),
        0x9E => SbcM(),
        0x9F => SbcA(),

        // AND
        0xA0 => AndB(),
        0xA1 => AndC(),
        0xA2 => AndD(),
        0xA3 => AndE(),
        0xA4 => AndH(),
        0xA5 => AndL(),
        0xA6 => AndM(),
        0xA7 => AndA(),

        // XOR
        0xA8 => XorB(),
        0xA9 => XorC(),
        0xAA => XorD(),
        0xAB => XorE(),
        0xAC => XorH(),
        0xAD => XorL(),
        0xAE => XorM(),
        0xAF => XorA(),

        // OR
        0xB0 => OrB(),
        0xB1 => OrC(),
        0xB2 => OrD(),
        0xB3 => OrE(),
        0xB4 => OrH(),
        0xB5 => OrL(),
        0xB6 => OrM(),
        0xB7 => OrA(),

        // CP
        0xB8 => CpB(),
        0xB9 => CpC(),
        0xBA => CpD(),
        0xBB => CpE(),
        0xBC => CpH(),
        0xBD => CpL(),
        0xBE => CpM(),
        0xBF => CpA(),

        // Unconditional return, call, JP HL
        0xC9 => Ret(),
        0xCD => Call(),
        0xE9 => JpHl(),

        // Jumps
        0xC3 => Jp(),
        0xC2 => JpNz(),
        0xCA => JpZ(),
        0xD2 => JpNc(),
        0xDA => JpC(),

        // Conditional calls
        0xC4 => CallNz(),
        0xCC => CallZ(),
        0xD4 => CallNc(),
        0xDC => CallC(),

        // Restarts
        0xC7 => Rst0(),
        0xCF => Rst1(),
        0xD7 => Rst2(),
        0xDF => Rst3(),
        0xE7 => Rst4(),
        0xEF => Rst5(),
        0xF7 => Rst6(),
        0xFF => Rst7(),

        // Interrupt control
        0xF3 => Di(),
        0xFB => Ei(),

        // Immediate arithmetic / logic
        0xC6 => AddN(),
        0xCE => AdcN(),
        0xD6 => SubN(),
        0xDE => SbcN(),
        0xE6 => AndN(),
        0xEE => XorN(),
        0xF6 => OrN(),
        0xFE => CpN(),

        // Rotate / special accumulator
        0x07 => Rlca(),
        0x0F => Rrca(),
        0x17 => Rla(),
        0x1F => Rra(),
        0x27 => Daa(),
        0x2F => Cpl(),
        0x37 => Scf(),
        0x3F => Ccf(),

        // INC r
        0x04 => IncB(),
        0x0C => IncC(),
        0x14 => IncD(),
        0x1C => IncE(),
        0x24 => IncH(),
        0x2C => IncL(),
        0x34 => IncM(),
        0x3C => IncA(),

        // DEC r
        0x05 => DecB(),
        0x0D => DecC(),
        0x15 => DecD(),
        0x1D => DecE(),
        0x25 => DecH(),
        0x2D => DecL(),
        0x35 => DecM(),
        0x3D => DecA(),

        0xF9 => LdSpHl(),

        // LR35902 Replace opcodes — load/store
        0x22 => LdHlIncA(),
        0x2A => LdAHlInc(),
        0x32 => LdHlDecA(),
        0x3A => LdAHlDec(),
        0xEA => LdA16A(),
        0xFA => LdAA16(),
        0xE0 => LdhA8A(),
        0xF0 => LdhAA8(),
        0xE2 => LdCA(),
        0xF2 => LdAC(),
        0x08 => LdA16Sp(),

        // SP arithmetic
        0xE8 => AddSpR8(),
        0xF8 => LdHlSpR8(),

        // Relative jumps
        0x18 => Jr(),
        0x20 => JrNz(),
        0x28 => JrZ(),
        0x30 => JrNc(),
        0x38 => JrC(),

        // Deferred to later steps
        0x10 => Stop(),
        0xCB => CbPrefix(),
        0xD9 => Reti(),

        // Illegal opcodes
        0xD3 => Illegal(0xD3),
        0xDB => Illegal(0xDB),
        0xDD => Illegal(0xDD),
        0xE3 => Illegal(0xE3),
        0xE4 => Illegal(0xE4),
        0xEB => Illegal(0xEB),
        0xEC => Illegal(0xEC),
        0xED => Illegal(0xED),
        0xF4 => Illegal(0xF4),
        0xFC => Illegal(0xFC),
        0xFD => Illegal(0xFD),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Illegal(byte opcode)
    {
        throw new InvalidOperationException($"Illegal LR35902 opcode 0x{opcode:X2}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Nop()
    {
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Fetch()
    {
        if (_haltBugPending)
        {
            _haltBugPending = false;
            return _mmu.Read(Pc);
        }
        return _mmu.Read(Pc++);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort FetchWord()
    {
        var lo = Fetch();
        var hi = Fetch();
        return (ushort)((hi << 8) | lo);
    }
}