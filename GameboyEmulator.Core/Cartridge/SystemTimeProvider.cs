namespace GameBoyEmulator.Core.Cartridge;

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
