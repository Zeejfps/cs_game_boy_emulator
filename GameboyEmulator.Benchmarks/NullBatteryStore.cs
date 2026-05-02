using GameBoyEmulator.Core;
using GameBoyEmulator.Core.Cartridge;

namespace GameBoyEmulator.Benchmarks;

internal sealed class NullBatteryStore : IBatteryStore
{
    public byte[]? Load(string key) => null;
    public void Save(string key, ReadOnlySpan<byte> data) { }
}
