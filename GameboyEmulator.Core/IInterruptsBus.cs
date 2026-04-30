using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public interface IInterruptsBus
{
    void Write(InterruptType kind);
}
