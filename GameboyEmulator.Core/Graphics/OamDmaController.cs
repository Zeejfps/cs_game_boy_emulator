using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Graphics;

// IMemoryBus decorator that owns OAM DMA.
//
// CPU-facing reads/writes flow through this controller:
//   - Writes to 0xFF46 are intercepted and start a transfer.
//   - Reads of 0xFF46 return the source page.
//   - While a transfer is active, reads/writes of regions that share the
//     bus DMA is currently using return 0xFF / are dropped — modeling the
//     real DMG bus contention. OAM is always inaccessible during DMA
//     because DMA is writing to it. HRAM and I/O stay reachable on either
//     bus, which is why every game's DMA-wait loop lives in HRAM.
//
// Startup timing: writing FF46 schedules a 2-M-cycle (8T) startup window
// before the new DMA actually engages OAM. During that window OAM stays
// accessible (fresh DMA) or stays locked by the previous DMA (restart),
// per Mooneye's oam_dma_start spec: M=0 is the FF46 write, M=1 is "nothing"
// (still accessible), M=2 is when the new DMA starts and OAM lock kicks in.
//
// The controller's own source reads call _inner.Read directly, bypassing the
// block — same as DMA being the bus master in hardware.
public sealed class OamDmaController : IBus
{
    private const ushort DmaRegister = 0xFF46;
    private const ushort IoStart = 0xFF00;
    private const ushort OamStart = 0xFE00;
    private const ushort OamEnd = 0xFEA0;
    private const int OamSize = 0xA0;
    private const int TicksPerByte = 4;
    private const int StartupTicks = 8;

    private readonly IBus _inner;
    private readonly IPpu _ppu;

    private bool _active;
    private byte _sourcePage;
    private bool _sourceVideoBus;
    private int _byteIndex;
    private int _pendingTicks;

    private bool _startupPending;
    private int _startupTicks;
    private byte _pendingSourcePage;
    private bool _pendingSourceVideoBus;

    public OamDmaController(IBus inner, IPpu ppu)
    {
        _inner = inner;
        _ppu = ppu;
    }

    public byte Read(ushort address)
    {
        if (address == DmaRegister)
            return _startupPending ? _pendingSourcePage : _sourcePage;
        if (_active && IsBusBlocked(address))
            return 0xFF;
        return _inner.Read(address);
    }

    public void Write(ushort address, byte value)
    {
        if (address == DmaRegister)
        {
            Start(value);
            return;
        }
        if (_active && IsBusBlocked(address))
        {
            return;
        }
        _inner.Write(address, value);
    }

    public void Tick(int ticks)
    {
        // The current (old) DMA, if any, keeps running through its full
        // ticks budget regardless of any pending startup. The new DMA's
        // startup window runs in parallel.
        if (_active)
        {
            _pendingTicks += ticks;

            while (_active && _pendingTicks >= TicksPerByte)
            {
                _pendingTicks -= TicksPerByte;
                if (_byteIndex < OamSize)
                {
                    // DMG quirk: source pages $E0-$FF read echo-of-WRAM
                    // ($C0-$DF), not OAM/IO/HRAM. Mooneye's
                    // oam_dma/sources-GS verifies this for $FE and $FF in
                    // particular — without the mask DMA from $FE would
                    // copy OAM into itself.
                    var page = _sourcePage >= 0xE0 ? (byte)(_sourcePage - 0x20) : _sourcePage;
                    var src = (ushort)((page << 8) | _byteIndex);
                    var value = _inner.Read(src);
                    _ppu.WriteOam((ushort)_byteIndex, value);
                    _byteIndex++;
                    if (_byteIndex >= OamSize)
                        _active = false;
                }
            }
        }

        if (_startupPending)
        {
            var consume = Math.Min(_startupTicks, ticks);
            _startupTicks -= consume;
            if (_startupTicks == 0)
            {
                // New DMA takes over after its startup window. Resets
                // byte index to 0 — any progress the old DMA made during
                // these 8T is discarded since OAM is about to be filled
                // by the new transfer anyway.
                _startupPending = false;
                _active = true;
                _sourcePage = _pendingSourcePage;
                _sourceVideoBus = _pendingSourceVideoBus;
                _byteIndex = 0;
                _pendingTicks = 0;
            }
        }
    }

    public void Reset()
    {
        _active = false;
        _sourcePage = 0;
        _sourceVideoBus = false;
        _byteIndex = 0;
        _pendingTicks = 0;
        _startupPending = false;
        _startupTicks = 0;
        _pendingSourcePage = 0;
        _pendingSourceVideoBus = false;
    }

    // OAM is always blocked while DMA runs (DMA owns it for writes).
    // HRAM and I/O sit off the main fetch buses so they stay accessible.
    // Otherwise an address is blocked iff it shares the bus DMA's source
    // is currently driving: video bus = $8000-$9FFF (VRAM); external bus =
    // everything else below $FE00.
    private bool IsBusBlocked(ushort address)
    {
        if (address >= OamStart && address < OamEnd) return true;
        if (address >= IoStart) return false;
        var addressVideoBus = address >= 0x8000 && address < 0xA000;
        return addressVideoBus == _sourceVideoBus;
    }

    private void Start(byte sourcePage)
    {
        _pendingSourcePage = sourcePage;
        _pendingSourceVideoBus = sourcePage >= 0x80 && sourcePage < 0xA0;
        _startupPending = true;
        _startupTicks = StartupTicks;
    }
}
