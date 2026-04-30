using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core;

public interface IInterruptBus
{
    void Write(InterruptType kind);
}
