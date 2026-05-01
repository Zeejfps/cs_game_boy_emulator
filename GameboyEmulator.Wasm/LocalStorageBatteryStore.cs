using System.Runtime.InteropServices.JavaScript;
using GameBoyEmulator.Core;

namespace GameBoyEmulator.Wasm;

public sealed partial class LocalStorageBatteryStore : IBatteryStore
{
    private const string KeyPrefix = "gb-save:";

    public byte[]? Load(string key)
    {
        var encoded = Get(KeyPrefix + key);
        if (string.IsNullOrEmpty(encoded))
            return null;
        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public void Save(string key, ReadOnlySpan<byte> data)
    {
        var encoded = Convert.ToBase64String(data);
        Set(KeyPrefix + key, encoded);
    }

    [JSImport("globalThis.__gbBatteryGet")]
    private static partial string? Get(string key);

    [JSImport("globalThis.__gbBatterySet")]
    private static partial void Set(string key, string value);
}
