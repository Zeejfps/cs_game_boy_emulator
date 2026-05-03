using GameBoyEmulator.Core.LR35902;

namespace GameBoyEmulator.Core.Graphics;

// IMemoryBus decorator that owns OAM DMA.
//
// CPU-facing reads/writes flow through this controller:
//   - Writes to 0xFF46 are intercepted and start a transfer.
//   - Reads of 0xFF46 return the source page.
//   - While a transfer is active, every other read below HRAM (< 0xFF80)
//     returns 0xFF — modeling the real DMG bus contention. HRAM stays
//     reachable, which is why every game's DMA-wait loop lives there.
//
// The controller's own source reads call _inner.Read directly, bypassing the
// block — same as DMA being the bus master in hardware.
public sealed class OamDmaController : IBus
{
    private const ushort DmaRegister = 0xFF46;
    private const ushort HramStart = 0xFF80;
    private const int OamSize = 0xA0;
    private const int TicksPerByte = 4;
    private const int SetupTicks = 4;

    private readonly IBus _inner;
    private readonly IPpu _ppu;

    private bool _active;
    private byte _sourcePage;
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
        if (_active && address < HramStart)
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
        if (_active && address < HramStart)
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
            var src = (ushort)((_sourcePage << 8) | _byteIndex);
            var value = _inner.Read(src);
            _ppu.WriteOam((ushort)_byteIndex, value);
            _byteIndex++;
            if (_byteIndex >= OamSize)
                _active = false;
        }
    }

    public void Reset()
    {
        _active = false;
        _sourcePage = 0;
        _byteIndex = 0;
        _pendingTicks = 0;
        _setupTicks = 0;
    }

    private void Start(byte sourcePage)
    {
        _sourcePage = sourcePage;
        _active = true;
        _byteIndex = 0;
        _pendingTicks = 0;
        _setupTicks = SetupTicks;
    }
}
