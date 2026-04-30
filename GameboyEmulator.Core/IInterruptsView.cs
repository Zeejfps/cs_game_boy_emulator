using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public interface IInterruptsView
{
    InterruptType ReadRequestedInterrupts();
    void WriteRequestedInterrupts(InterruptType requestedInterrupts);

    InterruptType ReadEnabledInterrupts();
    void WriteEnabledInterrupts(InterruptType enabledInterrupts);
}