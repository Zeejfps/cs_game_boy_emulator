namespace GameBoyEmulator.Core;

public interface IBatteryStore
{
    byte[]? Load(string key);
    void Save(string key, ReadOnlySpan<byte> data);
}
