using GameBoyEmulator.Core;

namespace GameBoyEmulator.Wasm;

// One-cart-at-a-time buffer for battery-backed cartridge RAM. The host loads
// bytes in via Emulator.LoadRom and reads them back out via GetSaveData;
// the MBC stores its dirty RAM here when it flushes. Storage policy (where
// the bytes ultimately live) is the host's problem, not ours.
public sealed class InMemoryBatteryStore : IBatteryStore
{
    public byte[]? Bytes { get; set; }

    public byte[]? Load(string key) => Bytes;

    public void Save(string key, ReadOnlySpan<byte> data) => Bytes = data.ToArray();
}
