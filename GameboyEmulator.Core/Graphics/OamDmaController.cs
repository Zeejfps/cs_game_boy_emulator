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
    private const int SetupTicks = 4;

    private readonly IBus _inner;
    private readonly IPpu _ppu;

    private bool _active;
    private byte _sourcePage;
    private bool _sourceVideoBus;
    private int _byteIndex;
    private int _pendingTicks;
    private int _setupTicks;

    public OamDmaController(IBus inner, IPpu ppu)
    {
        _inner = inner;
        _ppu = ppu;
    }

    public byte Read(ushort address)
    {
        if (address == DmaRegister)
            return _sourcePage;
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
        if (!_active) return;

        _pendingTicks += ticks;

        // 4-T bus-arbitration setup before the first byte transfers.
        if (_setupTicks > 0)
        {
            var consume = Math.Min(_setupTicks, _pendingTicks);
            _setupTicks -= consume;
            _pendingTicks -= consume;
        }

        while (_active && _pendingTicks >= TicksPerByte)
        {
            _pendingTicks -= TicksPerByte;
            if (_byteIndex < OamSize)
            {
                // DMG quirk: source pages $E0-$FF read echo-of-WRAM
                // ($C0-$DF), not OAM/IO/HRAM. Mooneye's
                // oam_dma/sources-GS verifies this for $FE and $FF in
                // particular — without the mask DMA from $FE would copy
                // OAM into itself.
                var page = _sourcePage >= 0xE0 ? (byte)(_sourcePage - 0x20) : _sourcePage;
                var src = (ushort)((page << 8) | _byteIndex);
                var value = _inner.Read(src);
                _ppu.WriteOam((ushort)_byteIndex, value);
                _byteIndex++;
            }
            else
            {
                // One drain M-cycle past the final transfer so the OAM
                // lock persists through the M-cycle that contained that
                // last write. Mooneye's add_sp_e/jp/etc. timing tests
                // align an OAM read to land on exactly that cycle.
                _active = false;
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
        _setupTicks = 0;
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
        _sourcePage = sourcePage;
        _sourceVideoBus = sourcePage >= 0x80 && sourcePage < 0xA0;
        _active = true;
        _byteIndex = 0;
        _pendingTicks = 0;
        _setupTicks = SetupTicks;
    }
}
