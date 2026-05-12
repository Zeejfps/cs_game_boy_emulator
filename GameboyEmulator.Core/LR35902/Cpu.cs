using System.Runtime.CompilerServices;

namespace GameBoyEmulator.Core.LR35902;

public sealed partial class Cpu : ICpu, ISpeedController
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
    public bool IsWaitingForInterrupt { get; private set; }
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
    private bool _isCgb;
    // KEY1 (0xFF4D). Bit 7 = current speed (0=normal, 1=double),
    // bit 0 = prepare-switch (set by game, cleared by STOP). Bits 1..6 read 1.
    // STOP-based speed-switch handling arrives in Phase 4.
    private byte _key1;
    private readonly IBus _bus;
    private readonly ISystemClock _systemClock;
    private readonly IInterrupts _interrupts;

    public Cpu(IBus bus, ISystemClock systemClock, IInterrupts interrupts)
    {
        _bus = bus;
        _interrupts = interrupts;
        _systemClock = systemClock;
    }

    public void SetCgbMode(bool isCgb)
    {
        _isCgb = isCgb;
    }

    // MMU forwards 0xFF4D r/w here. DMG-mode gating happens at the MMU
    // dispatch — these methods assume the caller already decided to call them.
    public byte ReadKey1() => (byte)(_key1 | 0x7E);

    public void WriteKey1(byte value)
    {
        // Only bit 0 (prepare-switch) is writable. Bit 7 flips on STOP (Phase 4).
        _key1 = (byte)((_key1 & 0x80) | (value & 0x01));
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
        _key1 = 0;
        // Power-cycle returns the bus clock to normal speed. Without this,
        // a CGB game that left the system in double-speed before power-off
        // would resume with the bus running half-rate.
        _systemClock.SetDoubleSpeed(false);
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
    public void Step()
    {
        Dispatch();
        UpdateInterruptTimer();
    }
    
    private byte ReadFromBus(ushort address)
    {
        AdvanceClock(4);
        return _bus.Read(address);
    }
    
    private ushort ReadWordFromBus(ushort address)
    {
        var lo = ReadFromBus(address);
        var hi = ReadFromBus((ushort)(address + 1));
        return (ushort)((hi << 8) | lo);
    }

    private void WriteToBus(ushort address, byte value)
    {
        AdvanceClock(4);
        _bus.Write(address, value);
    }
    
    private void WriteWordToBus(ushort address, ushort value)
    {
        var lo = (byte)(value & 0xFF);
        var hi = (byte)(value >> 8);
        WriteToBus(address, lo);
        WriteToBus((ushort)(address + 1), hi);
    }

    private void AdvanceClock(int cycles)
    {
        _systemClock.Advance(cycles);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Dispatch()
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
            AdvanceClock(4);
            return;
        }

        var pending = _interrupts.GetPending();
        if (IsWaitingForInterrupt && pending == InterruptType.None)
        {
            AdvanceClock(4);
            return;
        }

        IsWaitingForInterrupt = false;

        if (InterruptMasterEnable && pending != InterruptType.None)
        {
            ServicePendingInterrupt();
            return;
        }

        var opcode = Fetch();
        Execute(opcode);
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
    private void Execute(byte opcode)
    {
        switch (opcode)
        {
            // NOP
            case 0x00: Nop(); break;

            // LD r,n8
            case 0x06: LdBn(); break;
            case 0x0E: LdCn(); break;
            case 0x16: LdDn(); break;
            case 0x1E: LdEn(); break;
            case 0x26: LdHn(); break;
            case 0x2E: LdLn(); break;
            case 0x3E: LdAn(); break;
            case 0x36: LdMn(); break;

            // LD B,r
            case 0x40: LdBb(); break;
            case 0x41: LdBc(); break;
            case 0x42: LdBd(); break;
            case 0x43: LdBe(); break;
            case 0x44: LdBh(); break;
            case 0x45: LdBl(); break;
            case 0x46: LdBm(); break;
            case 0x47: LdBa(); break;

            // LD C,r
            case 0x48: LdCb(); break;
            case 0x49: LdCc(); break;
            case 0x4A: LdCd(); break;
            case 0x4B: LdCe(); break;
            case 0x4C: LdCh(); break;
            case 0x4D: LdCl(); break;
            case 0x4E: LdCm(); break;
            case 0x4F: LdCa(); break;

            // LD D,r
            case 0x50: LdDb(); break;
            case 0x51: LdDc(); break;
            case 0x52: LdDd(); break;
            case 0x53: LdDe(); break;
            case 0x54: LdDh(); break;
            case 0x55: LdDl(); break;
            case 0x56: LdDm(); break;
            case 0x57: LdDa(); break;

            // LD E,r
            case 0x58: LdEb(); break;
            case 0x59: LdEc(); break;
            case 0x5A: LdEd(); break;
            case 0x5B: LdEe(); break;
            case 0x5C: LdEh(); break;
            case 0x5D: LdEl(); break;
            case 0x5E: LdEm(); break;
            case 0x5F: LdEa(); break;

            // LD H,r
            case 0x60: LdHb(); break;
            case 0x61: LdHc(); break;
            case 0x62: LdHd(); break;
            case 0x63: LdHe(); break;
            case 0x64: LdHh(); break;
            case 0x65: LdHl(); break;
            case 0x66: LdHm(); break;
            case 0x67: LdHa(); break;

            // LD L,r
            case 0x68: LdLb(); break;
            case 0x69: LdLc(); break;
            case 0x6A: LdLd(); break;
            case 0x6B: LdLe(); break;
            case 0x6C: LdLh(); break;
            case 0x6D: LdLl(); break;
            case 0x6E: LdLm(); break;
            case 0x6F: LdLa(); break;

            // LD A,r
            case 0x78: LdAb(); break;
            case 0x79: LdAc(); break;
            case 0x7A: LdAd(); break;
            case 0x7B: LdAe(); break;
            case 0x7C: LdAh(); break;
            case 0x7D: LdAl(); break;
            case 0x7E: LdAm(); break;
            case 0x7F: LdAa(); break;

            // LD (HL),r
            case 0x70: LdMb(); break;
            case 0x71: LdMc(); break;
            case 0x72: LdMd(); break;
            case 0x73: LdMe(); break;
            case 0x74: LdMh(); break;
            case 0x75: LdMl(); break;
            case 0x76: Halt(); break;
            case 0x77: LdMa(); break;

            // LD A,(rr)
            case 0x0A: LdABc(); break;
            case 0x1A: LdADe(); break;

            // LD (rr),A
            case 0x02: LdBcA(); break;
            case 0x12: LdDeA(); break;

            // LD rr,n16
            case 0x01: LdBcNn(); break;
            case 0x11: LdDeNn(); break;
            case 0x21: LdHlNn(); break;
            case 0x31: LdSpNn(); break;

            // ADD HL,rr
            case 0x09: AddHlBc(); break;
            case 0x19: AddHlDe(); break;
            case 0x29: AddHlHl(); break;
            case 0x39: AddHlSp(); break;

            // INC rr
            case 0x03: IncBc(); break;
            case 0x13: IncDe(); break;
            case 0x23: IncHl(); break;
            case 0x33: IncSp(); break;

            // DEC rr
            case 0x0B: DecBc(); break;
            case 0x1B: DecDe(); break;
            case 0x2B: DecHl(); break;
            case 0x3B: DecSp(); break;

            // Stack operations
            case 0xC1: PopBc(); break;
            case 0xD1: PopDe(); break;
            case 0xE1: PopHl(); break;
            case 0xF1: PopAf(); break;

            case 0xC5: PushBc(); break;
            case 0xD5: PushDe(); break;
            case 0xE5: PushHl(); break;
            case 0xF5: PushAf(); break;

            // Conditional returns
            case 0xC0: RetNz(); break;
            case 0xC8: RetZ(); break;
            case 0xD0: RetNc(); break;
            case 0xD8: RetC(); break;

            // ADD
            case 0x80: AddB(); break;
            case 0x81: AddC(); break;
            case 0x82: AddD(); break;
            case 0x83: AddE(); break;
            case 0x84: AddH(); break;
            case 0x85: AddL(); break;
            case 0x86: AddM(); break;
            case 0x87: AddA(); break;

            // ADC
            case 0x88: AdcB(); break;
            case 0x89: AdcC(); break;
            case 0x8A: AdcD(); break;
            case 0x8B: AdcE(); break;
            case 0x8C: AdcH(); break;
            case 0x8D: AdcL(); break;
            case 0x8E: AdcM(); break;
            case 0x8F: AdcA(); break;

            // SUB
            case 0x90: SubB(); break;
            case 0x91: SubC(); break;
            case 0x92: SubD(); break;
            case 0x93: SubE(); break;
            case 0x94: SubH(); break;
            case 0x95: SubL(); break;
            case 0x96: SubM(); break;
            case 0x97: SubA(); break;

            // SBC
            case 0x98: SbcB(); break;
            case 0x99: SbcC(); break;
            case 0x9A: SbcD(); break;
            case 0x9B: SbcE(); break;
            case 0x9C: SbcH(); break;
            case 0x9D: SbcL(); break;
            case 0x9E: SbcM(); break;
            case 0x9F: SbcA(); break;

            // AND
            case 0xA0: AndB(); break;
            case 0xA1: AndC(); break;
            case 0xA2: AndD(); break;
            case 0xA3: AndE(); break;
            case 0xA4: AndH(); break;
            case 0xA5: AndL(); break;
            case 0xA6: AndM(); break;
            case 0xA7: AndA(); break;

            // XOR
            case 0xA8: XorB(); break;
            case 0xA9: XorC(); break;
            case 0xAA: XorD(); break;
            case 0xAB: XorE(); break;
            case 0xAC: XorH(); break;
            case 0xAD: XorL(); break;
            case 0xAE: XorM(); break;
            case 0xAF: XorA(); break;

            // OR
            case 0xB0: OrB(); break;
            case 0xB1: OrC(); break;
            case 0xB2: OrD(); break;
            case 0xB3: OrE(); break;
            case 0xB4: OrH(); break;
            case 0xB5: OrL(); break;
            case 0xB6: OrM(); break;
            case 0xB7: OrA(); break;

            // CP
            case 0xB8: CpB(); break;
            case 0xB9: CpC(); break;
            case 0xBA: CpD(); break;
            case 0xBB: CpE(); break;
            case 0xBC: CpH(); break;
            case 0xBD: CpL(); break;
            case 0xBE: CpM(); break;
            case 0xBF: CpA(); break;

            // Unconditional return, call, JP HL
            case 0xC9: Ret(); break;
            case 0xCD: Call(); break;
            case 0xE9: JpHl(); break;

            // Jumps
            case 0xC3: Jp(); break;
            case 0xC2: JpNz(); break;
            case 0xCA: JpZ(); break;
            case 0xD2: JpNc(); break;
            case 0xDA: JpC(); break;

            // Conditional calls
            case 0xC4: CallNz(); break;
            case 0xCC: CallZ(); break;
            case 0xD4: CallNc(); break;
            case 0xDC: CallC(); break;

            // Restarts
            case 0xC7: Rst0(); break;
            case 0xCF: Rst1(); break;
            case 0xD7: Rst2(); break;
            case 0xDF: Rst3(); break;
            case 0xE7: Rst4(); break;
            case 0xEF: Rst5(); break;
            case 0xF7: Rst6(); break;
            case 0xFF: Rst7(); break;

            // Interrupt control
            case 0xF3: Di(); break;
            case 0xFB: Ei(); break;

            // Immediate arithmetic / logic
            case 0xC6: AddN(); break;
            case 0xCE: AdcN(); break;
            case 0xD6: SubN(); break;
            case 0xDE: SbcN(); break;
            case 0xE6: AndN(); break;
            case 0xEE: XorN(); break;
            case 0xF6: OrN(); break;
            case 0xFE: CpN(); break;

            // Rotate / special accumulator
            case 0x07: Rlca(); break;
            case 0x0F: Rrca(); break;
            case 0x17: Rla(); break;
            case 0x1F: Rra(); break;
            case 0x27: Daa(); break;
            case 0x2F: Cpl(); break;
            case 0x37: Scf(); break;
            case 0x3F: Ccf(); break;

            // INC r
            case 0x04: IncB(); break;
            case 0x0C: IncC(); break;
            case 0x14: IncD(); break;
            case 0x1C: IncE(); break;
            case 0x24: IncH(); break;
            case 0x2C: IncL(); break;
            case 0x34: IncM(); break;
            case 0x3C: IncA(); break;

            // DEC r
            case 0x05: DecB(); break;
            case 0x0D: DecC(); break;
            case 0x15: DecD(); break;
            case 0x1D: DecE(); break;
            case 0x25: DecH(); break;
            case 0x2D: DecL(); break;
            case 0x35: DecM(); break;
            case 0x3D: DecA(); break;

            case 0xF9: LdSpHl(); break;

            // LR35902 Replace opcodes — load/store
            case 0x22: LdHlIncA(); break;
            case 0x2A: LdAHlInc(); break;
            case 0x32: LdHlDecA(); break;
            case 0x3A: LdAHlDec(); break;
            case 0xEA: LdA16A(); break;
            case 0xFA: LdAA16(); break;
            case 0xE0: LdhA8A(); break;
            case 0xF0: LdhAA8(); break;
            case 0xE2: LdCA(); break;
            case 0xF2: LdAC(); break;
            case 0x08: LdA16Sp(); break;

            // SP arithmetic
            case 0xE8: AddSpR8(); break;
            case 0xF8: LdHlSpR8(); break;

            // Relative jumps
            case 0x18: Jr(); break;
            case 0x20: JrNz(); break;
            case 0x28: JrZ(); break;
            case 0x30: JrNc(); break;
            case 0x38: JrC(); break;

            // Deferred to later steps
            case 0x10: Stop(); break;
            case 0xCB: CbPrefix(); break;
            case 0xD9: Reti(); break;

            // Illegal opcodes
            case 0xD3:
            case 0xDB:
            case 0xDD:
            case 0xE3:
            case 0xE4:
            case 0xEB:
            case 0xEC:
            case 0xED:
            case 0xF4:
            case 0xFC:
            case 0xFD:
                Illegal(opcode);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Illegal(byte opcode)
    {
        throw new InvalidOperationException($"Illegal LR35902 opcode 0x{opcode:X2}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Nop() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Fetch()
    {
        if (_haltBugPending)
        {
            _haltBugPending = false;
            return ReadFromBus(Pc);
        }
        return ReadFromBus(Pc++);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort FetchWord()
    {
        var lo = Fetch();
        var hi = Fetch();
        return (ushort)((hi << 8) | lo);
    }
}