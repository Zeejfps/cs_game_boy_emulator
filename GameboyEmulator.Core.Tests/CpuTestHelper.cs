using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Tests;

public static class CpuExtensions
{
    public static Cpu WriteState(this Cpu cpu, CpuState state)
    {
        cpu.Flags = state.Flags;
        cpu.Pc = state.Pc;
        cpu.Sp = state.Sp;
        cpu.Ra = state.Ra;
        cpu.Rb = state.Rb;
        cpu.Rc = state.Rc;
        cpu.Rd = state.Rd;
        cpu.Re = state.Re;
        cpu.Rh = state.Rh;
        cpu.Rl = state.Rl;
        return cpu;
    }

    public static CpuState ReadState(this Cpu cpu)
    {
        return CpuState.FromCpu(cpu);
    }
}

public class FakeMmu : IBus
{
    private readonly byte[] _ram = new byte[64 * 1024];
    public Interrupts Interrupts { get; } = new();

    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case 0xFF0F:
                Interrupts.WriteRequestedInterrupts((InterruptType)value);
                return;
            case 0xFFFF:
                Interrupts.WriteEnabledInterrupts((InterruptType)value);
                return;
            default:
                _ram[address] = value;
                return;
        }
    }

    public void WriteWord(ushort address, ushort value)
    {
        Write(address, (byte)(value & 0xFF));
        Write((ushort)(address + 1), (byte)(value >> 8));
    }

    public byte Read(ushort address) => address switch
    {
        0xFF0F => (byte)Interrupts.ReadRequestedInterrupts(),
        0xFFFF => (byte)Interrupts.ReadEnabledInterrupts(),
        _ => _ram[address],
    };

    public ushort ReadWord(ushort address) =>
        (ushort)((Read((ushort)(address + 1)) << 8) | Read(address));
}
