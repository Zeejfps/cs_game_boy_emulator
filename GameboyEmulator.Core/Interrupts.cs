using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public sealed class Interrupts : IInterrupts, IInterruptsView
{
    private InterruptType _requestedInterrupts;
    private InterruptType _enabledInterrupts;

    public void Request(InterruptType kind)
    {
        _requestedInterrupts |= kind;
    }

    public void Clear(InterruptType kind)
    {
        _requestedInterrupts &= ~kind;
    }

    public bool IsRequested(InterruptType kind)
    {
        return (_requestedInterrupts & kind) != 0;
    }

    public InterruptType GetPending()
    {
        return _requestedInterrupts & _enabledInterrupts & InterruptType.All;
    }

    public InterruptType ReadRequestedInterrupts()
    {
        return _requestedInterrupts;
    }

    public void WriteRequestedInterrupts(InterruptType requestedInterrupts)
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
