namespace GameBoyEmulator.Core.LR35902;

public interface IInterruptsBus
{
    void Request(InterruptType kind);
    void Clear(InterruptType kind);
    bool IsRequested(InterruptType kind);
    InterruptType GetPending();

    InterruptType ReadRequestedInterrupts();
    void WriteRequestedInterrupts(InterruptType requestedInterrupts);

    InterruptType ReadEnabledInterrupts();
    void WriteEnabledInterrupts(InterruptType enabledInterrupts);
}
