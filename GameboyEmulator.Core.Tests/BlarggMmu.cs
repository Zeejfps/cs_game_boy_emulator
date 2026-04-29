using System.Text;
using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

// Test bus for running Blargg's `cpu_instrs` sub-tests. Models the DMG memory
// map subset those ROMs touch (ROM-only image, WRAM/HRAM/OAM/VRAM, IF/IE), plus
// a minimal cycle-counted timer and a serial-port capture so the runner can
// scrape "Passed"/"Failed" out of the output. Not cycle-accurate; deliberately
// omits MBC banking, OAM DMA, the DIV-edge TIMA increment, and the TIMA reload
// delay — see `docs/plan/step-7-blargg-validation.md` for what's intentionally
// out of scope.
public sealed class BlarggMmu : IMemoryBus
{
    private readonly byte[] _rom = new byte[0x8000];
    private readonly byte[] _vram = new byte[0x2000];
    private readonly byte[] _wram = new byte[0x2000];
    private readonly byte[] _oam = new byte[0xA0];
    private readonly byte[] _hram = new byte[0x7F];
    private readonly byte[] _io = new byte[0x80];
    private byte _ie;

    private readonly StringBuilder _serial = new();
    private ushort _internalCounter;
    private int _timaAccumulator;

    public BlarggMmu(byte[] rom)
    {
        if (rom.Length != 0x8000)
            throw new ArgumentException(
                $"BlarggMmu requires a 32 KB ROM-only image; got {rom.Length} bytes.",
                nameof(rom));
        Array.Copy(rom, _rom, 0x8000);
    }

    public string SerialOutput => _serial.ToString();

    public byte Read(ushort address)
    {
        if (address < 0x8000) return _rom[address];
        if (address < 0xA000) return _vram[address - 0x8000];
        if (address < 0xC000) return 0xFF; // External RAM not modeled.
        if (address < 0xE000) return _wram[address - 0xC000];
        if (address < 0xFE00) return _wram[address - 0xE000]; // Echo of 0xC000-0xDDFF.
        if (address < 0xFEA0) return _oam[address - 0xFE00];
        if (address < 0xFF00) return 0xFF; // Unusable.
        if (address < 0xFF80) return ReadIo(address);
        if (address < 0xFFFF) return _hram[address - 0xFF80];
        return _ie;
    }

    public void Write(ushort address, byte value)
    {
        if (address < 0x8000) return; // ROM-only; ignore writes (no MBC).
        if (address < 0xA000) { _vram[address - 0x8000] = value; return; }
        if (address < 0xC000) return;
        if (address < 0xE000) { _wram[address - 0xC000] = value; return; }
        if (address < 0xFE00) { _wram[address - 0xE000] = value; return; }
        if (address < 0xFEA0) { _oam[address - 0xFE00] = value; return; }
        if (address < 0xFF00) return;
        if (address < 0xFF80) { WriteIo(address, value); return; }
        if (address < 0xFFFF) { _hram[address - 0xFF80] = value; return; }
        _ie = value;
    }

    public ushort ReadWord(ushort address)
    {
        var lo = Read(address);
        var hi = Read((ushort)(address + 1));
        return (ushort)((hi << 8) | lo);
    }

    public void WriteWord(ushort address, ushort value)
    {
        Write(address, (byte)(value & 0xFF));
        Write((ushort)(address + 1), (byte)(value >> 8));
    }

    public void Tick(int tStates)
    {
        _internalCounter = (ushort)(_internalCounter + tStates);

        var tac = _io[IoRegisters.TimerControlAddress - 0xFF00];
        if ((tac & 0x04) == 0)
            return;

        var period = (tac & 0x03) switch
        {
            0 => 1024,
            1 => 16,
            2 => 64,
            _ => 256,
        };

        _timaAccumulator += tStates;
        while (_timaAccumulator >= period)
        {
            _timaAccumulator -= period;
            ref var tima = ref _io[IoRegisters.TimerCounterAddress - 0xFF00];
            if (tima == 0xFF)
            {
                tima = _io[IoRegisters.TimerModuloAddress - 0xFF00];
                var ifAddr = IoRegisters.InterruptFlagAddress - 0xFF00;
                _io[ifAddr] = (byte)(_io[ifAddr] | 0x04);
            }
            else
            {
                tima++;
            }
        }
    }

    private byte ReadIo(ushort address)
    {
        return address switch
        {
            IoRegisters.DividerAddress => (byte)(_internalCounter >> 8),
            _ => _io[address - 0xFF00],
        };
    }

    private void WriteIo(ushort address, byte value)
    {
        switch (address)
        {
            case IoRegisters.DividerAddress:
                _internalCounter = 0;
                return;

            case IoRegisters.SerialControlAddress:
                _io[address - 0xFF00] = value;
                if (value == 0x81)
                {
                    _serial.Append((char)_io[IoRegisters.SerialDataAddress - 0xFF00]);
                    _io[address - 0xFF00] = 0x01; // Clear "transfer complete" bit.
                    var ifAddr = IoRegisters.InterruptFlagAddress - 0xFF00;
                    _io[ifAddr] = (byte)(_io[ifAddr] | 0x08);
                }
                return;

            default:
                _io[address - 0xFF00] = value;
                return;
        }
    }
}
