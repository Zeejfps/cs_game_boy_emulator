namespace GameBoyEmulator.Core;

public interface IInterruptRegisters
{
    byte InterruptFlag { get; set; }
    byte InterruptEnable { get; set; }
}
