using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core;

public interface IInterruptController
{
    byte InterruptFlag { get; set; }
    byte InterruptEnable { get; set; }
    void Request(InterruptType kind);
}
