using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public interface IInterruptsBus
{
    void Request(InterruptType kind);
}
