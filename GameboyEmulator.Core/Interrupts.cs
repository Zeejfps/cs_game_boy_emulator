using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class Interrupts : IInterruptsRegisters, IInterruptsBus
{
    public InterruptType RequestedInterrupts { get; set; }

    public void Request(InterruptType kind)
    {
        RequestedInterrupts |= kind;
    }
}
