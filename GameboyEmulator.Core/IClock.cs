namespace GameBoyEmulator.Core;

public interface IClock
{
    event Action? Ticked;
    long Frequency { get; }
    long GetTimestamp();
}