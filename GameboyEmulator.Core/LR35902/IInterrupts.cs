namespace GameBoyEmulator.Core.LR35902;

public interface IInterrupts
{
    void Request(InterruptType kind);
    void Clear(InterruptType kind);
    bool IsRequested(InterruptType kind);
    InterruptType GetPending(); 
}
