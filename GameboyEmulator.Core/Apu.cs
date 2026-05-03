namespace GameBoyEmulator.Core;

// Minimal silent APU. Doesn't generate audio, but exposes register reads
// with the correct unused-bit masks and an NR52 that reflects the master
// enable (bit 7) the game wrote, with channel-active bits (0-3) reported
// as 0 — i.e. "no channel currently playing." Games that gate logic on
// "is the APU on?" or "is a channel still busy?" need this to be honest;
// returning 0xFF for everything (the previous stub) tells the game every
// channel is busy forever, which can hang sound-driver state machines and
// scripts that wait on SFX completion.
//
// When NR52 bit 7 is cleared, all sound register writes (other than NR52
// itself and wave RAM) are ignored, matching DMG behavior.
public sealed class Apu : IApu
{
    private const ushort Nr10 = 0xFF10;
    private const ushort Nr52 = 0xFF26;
    private const ushort WaveRamStart = 0xFF30;
    private const ushort WaveRamEnd = 0xFF3F;

    // Read-only mask applied to each FF10-FF26 register: bit set = bit reads
    // back as 1 regardless of what was written. Mirrors the standard DMG
    // unused-bit map. Indices are address - 0xFF10.
    private static readonly byte[] ReadMask =
    [
        0x80, 0x3F, 0x00, 0xFF, 0xBF, // FF10-FF14 (channel 1)
        0xFF, 0x3F, 0x00, 0xFF, 0xBF, // FF15-FF19 (FF15 unused, channel 2)
        0x7F, 0xFF, 0x9F, 0xFF, 0xBF, // FF1A-FF1E (channel 3)
        0xFF, 0xFF, 0x00, 0x00, 0xBF, // FF1F-FF23 (FF1F unused, channel 4)
        0x00, 0x00, 0x70,             // FF24-FF26 (NR50, NR51, NR52)
    ];

    private readonly byte[] _regs = new byte[ReadMask.Length];
    private readonly byte[] _waveRam = new byte[16];
    private bool _powered;

    public void WriteRegister(ushort address, byte value)
    {
        if (address >= WaveRamStart && address <= WaveRamEnd)
        {
            _waveRam[address - WaveRamStart] = value;
            return;
        }

        if (address < Nr10 || address > Nr52)
            return;

        if (address == Nr52)
        {
            // Only bit 7 is writable; turning the APU off zeros the
            // other registers (DMG quirk — wave RAM is preserved).
            var nowOn = (value & 0x80) != 0;
            if (!nowOn && _powered)
                Array.Clear(_regs);
            _powered = nowOn;
            _regs[Nr52 - Nr10] = (byte)(nowOn ? 0x80 : 0x00);
            return;
        }

        // While powered off, writes to NR10-NR25 are ignored on DMG. NR11/21/31/41
        // length-load writes are accepted on CGB but not DMG; modeling the strict
        // DMG behavior is fine here.
        if (!_powered)
            return;

        _regs[address - Nr10] = value;
    }

    public byte ReadRegister(ushort address)
    {
        if (address >= WaveRamStart && address <= WaveRamEnd)
            return _waveRam[address - WaveRamStart];

        if (address < Nr10 || address > Nr52)
            return 0xFF;

        var idx = address - Nr10;
        return (byte)(_regs[idx] | ReadMask[idx]);
    }
}
