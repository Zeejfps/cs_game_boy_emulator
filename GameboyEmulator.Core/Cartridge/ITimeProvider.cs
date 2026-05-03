namespace GameBoyEmulator.Core.Cartridge;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
}
