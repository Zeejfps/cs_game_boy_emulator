using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public interface IInterruptBus
{
    void Write(InterruptType kind);
}
