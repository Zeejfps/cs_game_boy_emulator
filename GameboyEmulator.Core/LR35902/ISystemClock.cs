namespace GameBoyEmulator.Core.LR35902;

public interface ISystemClock
{
    void Advance(int ticks);
}
