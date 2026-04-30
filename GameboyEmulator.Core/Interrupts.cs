using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class Interrupts : IInterruptsBus, IInterruptsRequester
{
    private InterruptType _requestedInterrupts;
    private InterruptType _enabledInterrupts;

    public void Request(InterruptType kind)
    {
        _requestedInterrupts |= kind;
    }

    public InterruptType ReadRequestedInterrupt()
    {
        return _requestedInterrupts;
    }

    public void WriteRequestedInterrupt(InterruptType requestedInterrupts)
    {
        _requestedInterrupts =  requestedInterrupts;
    }

    public InterruptType ReadEnabledInterrupts()
    {
        return _enabledInterrupts;
    }

    public void WriteEnabledInterrupts(InterruptType enabledInterrupts)
    {
        _enabledInterrupts = enabledInterrupts;
    }
}
