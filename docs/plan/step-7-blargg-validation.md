# Step 7 — Validate against Blargg's `cpu_instrs`

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §6.1 (post-boot
state), §4 (interrupts; the timer model the test ROMs lean on).

Blargg's `cpu_instrs` is the canonical correctness gate for a DMG CPU.
It assumes the boot ROM has just finished, jumps from `0x0100`, and
reports progress by writing bytes to the serial port. Step 7 is the
first step that runs *real ROM code* against our core, so most of the
work is **infrastructure** — none of it exists yet — not opcode fixes.

## Snapshot of the relevant source after step 6

- `Cpu.Reset()` zeroes every register and the flag set. The comment
  "Post-boot DMG state per §6.1" applies only to the `IME=false` line —
  PC/SP/AF/BC/DE/HL are still all zero. To skip the boot ROM the test
  runner needs to apply §6.1 explicitly; either extend `Reset()` with a
  `bool skipBoot` knob or add a `SkipBoot()` helper. Existing unit tests
  rely on the all-zero default, so the new path must be opt-in.
- `IMemoryBus` is the only bus surface the CPU sees. The only existing
  implementation is `FakeMmu` in `CpuTestHelper.cs` — a flat 64 KB
  byte array. That's fine for unit tests but useless for Blargg: the
  ROMs program the timer (`0xFF04-0xFF07`), write progress bytes to
  the serial port (`0xFF01`/`0xFF02`), and depend on `IF`/`IE` being
  honored under load. We need a new test-only `BlarggMmu` that models
  the DMG address map, drives a small timer, and captures serial output.
- `IoRegisters` currently only names `InterruptEnableAddress` (`0xFFFF`)
  and `InterruptFlagAddress` (`0xFF0F`). Step 7 adds the serial and
  timer addresses so the new bus references them by name.
- `CpuInterruptTests` already pins EI delay, RETI semantics, all three
  HALT branches (including the bug), STOP wake, and dispatch priority —
  exactly what `02-interrupts.gb` exercises. Step 7's role for
  interrupts is integration-level: confirm that a *real* timer raises
  `IF` bit 2, that dispatch clears it, and that the EI-delay and
  HALT-bug paths survive when the surrounding code path isn't a
  hand-written test fixture.
- The combined `cpu_instrs.gb` is 64 KB and uses MBC1. The 11 individual
  sub-test ROMs (`01-special.gb` … `11-op a,(hl).gb`) are 32 KB and
  ROM-only — no banking required. Running them individually is the
  right granularity here: failures are localized to a single sub-test,
  and we avoid implementing MBC1 just to satisfy step 7.
- Blargg's pass/fail signalling is "write progress text to the serial
  port; spin forever". A runner detects completion by scanning the
  captured serial buffer for `"Passed"` / `"Failed"` (or by hitting a
  hard cycle budget).

## Tasks

### Skip-boot register init

- [ ] Add a way to bring the CPU up in the §6.1 post-boot DMG state
      without running a boot ROM. Either:
      - extend `Reset()` with a `bool skipBoot = false` parameter, or
      - add a separate `SkipBoot()` method.
      Either way, the new path sets `PC=0x0100`, `SP=0xFFFE`,
      `A=0x01`, `Flags = Z|H|C` (so `AF=0x01B0`, `N=0`),
      `BC=0x0013`, `DE=0x00D8`, `HL=0x014D`, `IME=false`,
      `IsWaitingForInterrupt=false`, `IsSleeping=false`,
      `_enableInterruptsTimer=0`, `_haltBugPending=false`.
- [ ] Tighten the existing `// Post-boot DMG state per ... §6.1.`
      comment in `Reset()` — currently it sits above the IME line
      but visually applies to all the zeroed register lines, which
      is wrong. Either move the comment so it scopes only to IME, or
      replace it with a one-liner pointing at `SkipBoot()` for the
      real post-boot state.
- [ ] Unit-test the post-boot path: `CpuSkipBootMatchesDmg` asserts
      `Pc==0x0100`, `Sp==0xFFFE`, `Ra==0x01`, `Flags==(Z|H|C)`,
      `Rbc==0x0013`, `Rde==0x00D8`, `Rhl==0x014D`,
      `InterruptMasterEnable==false`.

### `IoRegisters` additions

- [ ] Add named addresses for the registers the Blargg harness touches:
      ```
      SerialDataAddress    = 0xFF01  // SB
      SerialControlAddress = 0xFF02  // SC
      DividerAddress       = 0xFF04  // DIV
      TimerCounterAddress  = 0xFF05  // TIMA
      TimerModuloAddress   = 0xFF06  // TMA
      TimerControlAddress  = 0xFF07  // TAC
      ```
      No CPU-side code references them yet; they exist for the test
      MMU and for whatever production bus replaces it later.

### `BlarggMmu` test bus

Lives next to `FakeMmu` in `CpuTestHelper.cs` (or a new
`BlarggMmu.cs` if it gets bulky). Implements `IMemoryBus` with the
DMG memory map for the small subset Blargg's ROMs use:

- [ ] Address routing:
      - `0x0000-0x7FFF` → ROM image (read-only; writes are ignored —
        the sub-test ROMs are ROM-only and never write here, but
        ignoring rather than asserting matches real hardware).
      - `0x8000-0x9FFF` → VRAM (plain RAM is fine; nothing reads it).
      - `0xC000-0xDFFF` → WRAM.
      - `0xE000-0xFDFF` → echo of `0xC000-0xDDFF` (mirror).
      - `0xFE00-0xFE9F` → OAM (plain RAM).
      - `0xFEA0-0xFEFF` → returns `0xFF` on read; writes ignored.
      - `0xFF00-0xFF7F` → I/O (see below; default `0xFF` on read,
        ignored on write for unimplemented addresses).
      - `0xFF80-0xFFFE` → HRAM.
      - `0xFFFF` → IE.
- [ ] `WriteWord` / `ReadWord` go through the byte path so the I/O
      hooks fire. Don't shortcut to a raw array slot.
- [ ] Constructor takes a `byte[]` ROM image and copies the first
      32 KB into `0x0000-0x7FFF`. Validates the image is exactly
      32 KB (the sub-tests we run are all ROM-only) and throws
      otherwise — no MBC fallback in this step.

### Serial capture (`0xFF01` / `0xFF02`)

- [ ] In the test MMU, when `0x81` is written to `SerialControlAddress`:
      append the current `SerialDataAddress` byte to a `StringBuilder`,
      then store `0x01` at `SerialControlAddress` (clear bit 7 —
      "transfer complete"). Expose the captured text via a
      `string SerialOutput` property.
- [ ] Optionally raise the serial interrupt (`IF |= 0x08`) on each
      "transmit". Blargg's ROMs don't enable IE bit 3, so it's a
      no-op for them, but it's spec-correct and costs one line.

### Timer model (`0xFF04`-`0xFF07`)

The Blargg `02-interrupts` and `instr_timing` ROMs need TIMA actually
firing. A minimal cycle-counted model is enough; the well-known
hardware quirks (DIV-edge TIMA bug, the 4-T reload delay, the TAC
write-glitch) are out of scope here.

- [ ] Internal 16-bit `_internalCounter` ticked by T-states from the
      test runner. `DIV` reads as the high byte of `_internalCounter`
      (i.e. ticks once per 256 T). Any write to `DIV` resets
      `_internalCounter` to 0.
- [ ] When `TAC` bit 2 is set, TIMA increments at the rate selected
      by TAC's low two bits: `00`→1024 T, `01`→16 T, `10`→64 T,
      `11`→256 T. On TIMA overflow (255 → wrap), reload from TMA
      and set `IF |= 0x04` (timer interrupt request) via the I/O
      hook on `InterruptFlagAddress`.
- [ ] `TIMA`/`TMA`/`TAC` reads/writes go through the I/O block in
      the MMU; values are stored as plain bytes.
- [ ] `Tick(int tStates)` is called by the runner after each
      `Cpu.Step()` so the timer advances in lockstep with the CPU.

### Test runner

- [ ] Add a `TestRoms/blargg/` directory under
      `GameboyEmulator.Core.Tests/` and check in (or document where
      to fetch) the freely-redistributable Blargg binaries:
      - `cpu_instrs/individual/01-special.gb` … `11-op a,(hl).gb`
      - `halt_bug.gb`
      - `instr_timing.gb`
      Add `*.gb binary` to `.gitattributes` so git treats them
      correctly.
- [ ] In `GameboyEmulator.Core.Tests.csproj`, copy the ROMs to the
      test output dir:
      ```xml
      <ItemGroup>
        <None Update="TestRoms\**\*.gb">
          <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
      </ItemGroup>
      ```
- [ ] Add a `BlarggCpuInstrTests` test class with a `[Theory]` over
      the 11 sub-test filenames. Each iteration:
      1. Reads the ROM bytes from disk.
      2. Constructs `BlarggMmu` and `Cpu`, calls `cpu.SkipBoot()`.
      3. Steps in a tight loop: `var t = cpu.Step(); mmu.Tick(t);
         total += t;`.
      4. After each step, scan `mmu.SerialOutput` for `"Passed"` or
         `"Failed"`. Stop on either, or when `total` exceeds the
         budget (~250 M T-states is comfortable headroom for the
         slowest sub-tests; pick a single shared constant).
      5. Assert the final output contains `"Passed"`. On failure,
         include the *full* captured buffer in the assertion message
         so the xUnit failure report is self-contained.
- [ ] Add separate `[Fact]`s `BlarggHaltBug` and `BlarggInstrTiming`
      using the same harness.
- [ ] Tag the new tests with `[Trait("Category", "Blargg")]`. They're
      slower than the unit tests and depend on checked-in binaries —
      a category lets CI opt in/out without filename-pattern hacks.

### Triage notes

The Blargg sub-tests print which sub-routine and which opcode group
failed; capturing the serial buffer is usually enough to localize.
Likely failure modes given what step 1-6 already covered:

- `01-special` — almost entirely DAA. Step 6 unit-tested the table;
  if this fails, suspect a flag *read* path (DAA misreading H/N/C
  set by some adjacent op).
- `02-interrupts` — EI delay, RETI, HALT, IF clear-on-dispatch, and
  the timer interrupt firing under load. If unit tests pass and this
  fails, the timer model is the most likely culprit.
- `03 op sp,hl` / `08 misc instrs` — `LD (a16),SP`, `LD HL,SP+r8`,
  `ADD SP,r8`. Watch the half-carry/carry rules on `ADD SP,r8`: they
  use *low-byte unsigned* arithmetic on `SP` against the signed
  immediate. Common emulator bug.
- `04 op r,imm` / `05 op rp` / `06 ld r,r` / `09 op r,r` /
  `11 op a,(hl)` — broad ALU/move coverage. Failures here are
  usually a typo in one opcode arm, not a structural bug.
- `07 jr,jp,call,ret,rst` — branch + stack timing. `CpuTStateCoverage`
  pins single-instruction cycles; this exercises the conditional
  cycle counts (taken vs not-taken) in sequence.
- `10 bit ops` — entire CB table.

### Implementation plan housekeeping

- [ ] In `docs/lr35902-implementation-plan.md`, tick steps 1-6 (and
      step 3.1) in the top-level checklist — they're all done. Step 7
      is in progress; leave it unchecked until exit criteria below
      are green.

## Out of scope (explicitly)

- MBC1/2/3/5 banking. Step 7 uses ROM-only sub-tests. The combined
  `cpu_instrs.gb` (which needs MBC1) is *not* a goal.
- PPU / LCD. The test ROMs write tile data and tilemap entries but
  do not read VRAM — no display is needed to determine pass/fail.
- APU and joypad MMIO.
- A production-grade memory bus (bus contention, OAM DMA, accurate
  unmapped-region read patterns, MBC1 multicart quirks). The
  `BlarggMmu` is a test artefact.
- Cycle-accurate timer (DIV-edge TIMA increment, TIMA reload delay,
  TAC write glitches). The simple model passes `02-interrupts` and
  `instr_timing` for the ranges Blargg checks; full hardware
  accuracy is for whichever later step adds Mooneye-GB coverage.
- Mooneye-GB / dmg-acid2 / Wilbert Pol's tests. They cover PPU and
  edge-case CPU behavior — out of scope until the PPU lands.
- A real boot ROM. `SkipBoot()` is a permanent feature; running the
  DMG boot ROM is its own (much later) task.

## Exit criteria

- `Cpu.SkipBoot()` (or equivalent) leaves the CPU in the §6.1
  post-boot DMG state and is unit-tested.
- `BlarggMmu` implements the DMG memory map subset above, captures
  serial writes, and pumps a working timer that raises `IF` bit 2.
- All 11 `cpu_instrs` sub-tests print `"Passed"` via the captured
  serial buffer (`01-special` … `11-op a,(hl)`).
- `halt_bug.gb` prints `"Passed"` — confirms the IME=0 + pending
  interrupt branch from step 5 survives a real ROM.
- `instr_timing.gb` prints `"Passed"` — confirms the cycle counts
  in `lr35902-opcode-tables.md` are wired into `Execute()`
  correctly, including the variable-cycle conditional branches and
  the timer pump.
- The Blargg ROMs are checked in (or fetched at build time) and
  copied to the test output directory.
- The full test suite — including `[Trait("Category", "Blargg")]` —
  is green.
- `docs/lr35902-implementation-plan.md` reflects steps 1-7 as
  complete.
