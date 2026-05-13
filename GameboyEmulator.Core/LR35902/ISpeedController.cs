namespace GameBoyEmulator.Core.LR35902;

// MMU dispatches 0xFF4D r/w through this so the CPU (which owns the actual
// speed-switch logic via STOP) doesn't have to be a forward dependency of
// the MMU. The CPU implements this interface.
public interface ISpeedController
{
    byte ReadKey1();
    void WriteKey1(byte value);
}
