namespace GameBoyEmulator.Core.LR35902;

public interface ISystemClock
{
    void Advance(int ticks);

    // STOP-based CGB speed switch routes here so the CPU doesn't depend on the
    // concrete SystemClock. DMG and CGB-normal pass false; CGB double-speed
    // passes true. Halves the bus-domain (PPU/APU) tick rate.
    void SetDoubleSpeed(bool doubleSpeed);
}
