using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public interface IInterruptsRegisters
{
    InterruptType RequestedInterrupts { get; set; }
}
