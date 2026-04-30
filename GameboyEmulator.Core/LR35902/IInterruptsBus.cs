namespace GameBoyEmulator.Core.LR35902;

public interface IInterruptsBus
{
    InterruptType ReadRequestedInterrupt();
    void WriteRequestedInterrupt(InterruptType requestedInterrupts);
    
    InterruptType ReadEnabledInterrupts();
    void WriteEnabledInterrupts(InterruptType enabledInterrupts);
}
