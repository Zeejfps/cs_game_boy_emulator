using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core;

public interface IInterruptsRequester
{
    void Request(InterruptType kind);
}
