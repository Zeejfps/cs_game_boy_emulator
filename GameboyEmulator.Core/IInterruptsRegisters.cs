namespace GameBoyEmulator.Core;

public interface IInterruptsRegisters
{
    byte InterruptFlag { get; set; }
    byte InterruptEnable { get; set; }
}
