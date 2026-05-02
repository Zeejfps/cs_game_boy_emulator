namespace GameBoyEmulator.Core.Cartridge;

public interface IBatteryStore
{
    byte[]? Load(string key);
    void Save(string key, ReadOnlySpan<byte> data);
}
