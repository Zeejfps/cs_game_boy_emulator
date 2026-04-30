using GameboyEmulator.Core.LR35902;

namespace GameboyEmulator.Core.Tests;

// Test harness for Blargg's `cpu_instrs` sub-tests. Composes the real Mmu with
// ROM-only MBC, char-capturing serial, and a simple cycle-counted timer so the
// runner can scrape "Passed"/"Failed" out of the serial output.
public sealed class BlarggMmu : IMemoryBus
{
    private readonly Mmu _mmu;
    private readonly BlarggTimer _timer;
    private readonly BlarggSerial _serial;

    public BlarggMmu(byte[] rom)
    {
        var interrupts = new Interrupts();
        var mbc = new BlarggMbc(rom);
        _timer = new BlarggTimer(interrupts);
        _serial = new BlarggSerial(interrupts);
        _mmu = new Mmu(
            mbc,
            new NullPpu(),
            new NullJoypad(),
            _timer,
            new NullApu(),
            _serial,
            interrupts);
    }

    public string SerialOutput => _serial.Output;

    public void Tick(int tStates) => _timer.Tick(tStates);

    public byte Read(ushort address) => _mmu.Read(address);
    public void Write(ushort address, byte value) => _mmu.Write(address, value);
    public ushort ReadWord(ushort address) => _mmu.ReadWord(address);
    public void WriteWord(ushort address, ushort value) => _mmu.WriteWord(address, value);
}
