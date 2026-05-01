namespace GameBoyEmulator.Core;

public interface IBatteryStore
{
    byte[]? Load(string key);
    void Save(string key, ReadOnlySpan<byte> data);
}

public sealed class NullBatteryStore : IBatteryStore
{
    public byte[]? Load(string key) => null;
    public void Save(string key, ReadOnlySpan<byte> data) { }
}
