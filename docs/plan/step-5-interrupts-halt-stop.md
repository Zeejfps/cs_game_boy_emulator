# Step 5 — Interrupt model, EI/DI/RETI, HALT, STOP

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §4.

The Game Boy doesn't use the 8080's "inject an opcode on the bus" interrupt
model. Instead, the CPU polls `IE & IF` at the top of each step, and dispatch
is a fixed push-PC + jump-to-vector sequence. EI has a one-instruction delay;
RETI does not. HALT has a real hardware bug that test ROMs check for.

## Snapshot of the relevant source after step 4

- `Cpu.cs` exposes a `bool InterruptEnabled` property and a `bool Halted`
  property (private setter). `Reset()` currently sets `InterruptEnabled =
  true` and `Halted = false`. Post-boot DMG state is `IME = 0`, so step 5
  must change the reset value as part of the rename.
- `_enableInterruptsTimer` is decremented by `UpdateInterruptTimer()` at the
  bottom of each `Step()` and `Ei()` sets it to **2**. With the current
  ordering (Execute → UpdateInterruptTimer), 2 is the value that produces
  exactly one full instruction between EI and IME becoming effective:
  EI's own step decrements 2→1; the next instruction's step decrements 1→0
  and sets IME, so the *third* fetch is the first one that sees IME=1.
  Keep the value at 2 — the conversion guide's "set to 1" remark assumes a
  different ordering and is wrong for our pipeline. (The §4.2 cross-check
  is on the externally observable EI delay, not the literal counter value.)
- `Step()` currently short-circuits with `if (Halted) { UpdateInterruptTimer();
  return 4; }` *before* `Fetch()`. Step 5 inserts interrupt dispatch into the
  same prologue; the `Halted` short-circuit needs to coexist with it (waking
  up on a pending enabled interrupt, with or without dispatch depending on
  IME).
- `Cpu.Interrupts.cs` already hosts `Di()` / `Ei()` and `NotImplementedException`
  stubs for `Stop()` (`0x10`) and `Reti()` (`0xD9`). `Hlt()` lives in `Cpu.cs`
  (called from the `0x76` arm of the `Execute` switch) and currently just sets
  `Halted = true` and returns 4 — move it into `Cpu.Interrupts.cs` alongside
  the other CPU-control handlers so the new HALT-bug logic stays adjacent.
- `IMemoryBus` exposes `Read` / `Write` / `ReadWord` / `WriteWord`. `FakeMmu`
  in the test harness is a flat 64 KB array, so writing/reading `0xFFFF` (IE)
  and `0xFF0F` (IF) just works in tests. Real MMIO wiring (the actual `IE`/
  `IF` register objects on a production bus) is **out of scope for this step
  — see the bottom of this file**; the bus already routes those addresses to
  RAM and that's all the CPU needs.
- `TestDeferredOpcodeThrowsUntilNextStep` in `CpuArithmeticTests.cs` still
  asserts that `0x10` and `0xD9` throw `NotImplementedException`. After step
  5 lands, neither throws, so the entire theory should be deleted (no rows
  are left — `0xCB` was removed in step 4).

## Tasks

### Rename + reset

- [x] Rename `InterruptEnabled` → `InterruptMasterEnable` (public property on `Cpu`).
      Update every reference in `Cpu.Interrupts.cs`, `Cpu.cs`, and any test
      that reads/writes the flag. The new name matches the spec language
      ("Interrupt Master Enable") and avoids confusion with `IE` (the
      register at `0xFFFF`).
- [x] Change `Reset()` so `InterruptMasterEnable = false` (post-boot DMG state). Add a brief
      comment that this matches `8080-to-LR35902.md` §6.1.

### IE / IF as memory-mapped registers

- [x] Treat `0xFFFF` (IE) and `0xFF0F` (IF) as **plain bus reads** from the
      CPU's perspective — the CPU never holds its own copies. Every
      poll/clear goes through `_mmu.Read` / `_mmu.Write`.
      **Addition:** the literal addresses are exposed as named constants
      on a new `IoRegisters` static class (`InterruptEnableAddress = 0xFFFF`,
      `InterruptFlagAddress = 0xFF0F`) so call sites read in English. Future
      MMIO addresses (timer, LCD, joypad, …) can land alongside them.
- [x] Define a small `InterruptType` enum in `GameboyEmulator.Core/LR35902/`
      mirroring the 5 sources (VBlank=0, LcdStat=1, Timer=2, Serial=3,
      Joypad=4). Used by both the dispatch logic and any future
      `IRequestInterrupt` helper.
      **Deviation:** implemented as `[Flags] enum : byte` with bit-mask
      values (`VBlank = 1 << 0`, `LcdStat = 1 << 1`, … `Joypad = 1 << 4`)
      plus `None = 0` and `All = 0x1F` aliases, instead of plain bit
      indices. The flag form composes naturally with `IE`/`IF` register
      reads (which *are* bit masks), eliminates the `1 << bit` shifting
      at every call site, and lets the helpers below take/return
      `InterruptType` directly. A future `IRequestInterrupt` would write
      `IF |= source` rather than `IF |= 1 << (int)source` — the cast is
      gone.
- [x] Do **not** add an `IRequestInterrupt(InterruptType)` method on `Cpu`
      in this step. Peripherals are out of scope; whoever owns the bus can
      OR into `IF` directly when the time comes. (Keeping the CPU surface
      small avoids a stub method nobody calls.)

### Interrupt dispatch in Step()

- [x] Add a `ServicePendingInterrupt()` helper. When invoked it:
      reads `IE` and `IF`, computes `pending = IE & IF & 0x1F`, picks the
      lowest set bit (highest priority — VBlank wins over Joypad), clears
      that bit in `IF` (read-modify-write through the bus), clears `InterruptMasterEnable`,
      pushes `Pc` (high byte first via `_mmu.Write`, matching the rest of
      the stack handlers), sets `Pc = 0x40 + bit*8`, and returns `20`.
      **Addition — interrupt helpers in `Cpu.Interrupts.cs`:**
      `GetPendingInterrupts()` returns `(IE & IF & All)` cast to
      `InterruptType`. `IsInterruptRequested(InterruptType)` checks a
      single bit in `IF`. `ClearInterruptRequest(InterruptType)` does the
      read-modify-write that clears the bit in `IF`.
      `GetHighestPriority(InterruptType)` resolves priority via an explicit
      VBlank → LcdStat → Timer → Serial → Joypad chain (no bit-index
      arithmetic). `GetInterruptVector(InterruptType)` is a switch
      expression returning `0x40`/`0x48`/`0x50`/`0x58`/`0x60`. Call sites
      now read `if (pending != InterruptType.None)`,
      `if (IsInterruptRequested(InterruptType.Joypad))`,
      `Pc = GetInterruptVector(serviced)` etc., with no `1 << bit` or
      `& 0x10` masks at the call sites.
- [x] Restructure the top of `Step()` so the prologue runs in this order:
      1. If `IsWaitingForInterrupt`, check `(IE & IF & 0x1F) != 0`. If yes:
         clear `IsWaitingForInterrupt` and fall through to step 2. If no:
         `UpdateInterruptTimer()` and return 4 (CPU keeps idling at 4 T
         per step).
      2. If `InterruptMasterEnable && (IE & IF & 0x1F) != 0`: call `ServicePendingInterrupt()`,
         then `UpdateInterruptTimer()`, return 20.
      3. Otherwise: `Fetch()` + `Execute()` + `UpdateInterruptTimer()` as
         today.
      Note: when wake-from-HALT happens with `InterruptMasterEnable == 0`, control flows to
      step 3 and the next opcode runs normally (no dispatch). With
      `InterruptMasterEnable == 1`, step 2 fires and dispatch happens *before* the next
      opcode, which matches the spec.

### EI / DI / RETI

- [x] `Di()`: clear `InterruptMasterEnable` immediately, **also clear `_enableInterruptsTimer`**
      (a pending EI is cancelled by an immediate DI). Return 4.
- [x] `Ei()`: keep `_enableInterruptsTimer = 2` per the snapshot note above.
      Return 4.
- [x] `Reti()` (`0xD9`): pop PC (mirror the existing `Ret()` handler in
      `Cpu.Branch.cs`), set `InterruptMasterEnable = true` *immediately* (no timer). Return
      16. Do **not** also set `_enableInterruptsTimer` — RETI is the
      no-delay path.

### HALT

- [x] Move `Hlt()` from `Cpu.cs` into `Cpu.Interrupts.cs`. Make `Halted`'s
      setter `internal` (or keep `private set` and assign through a private
      backing field — the partial class can reach it either way).
      **Deviation:** the property is named `IsWaitingForInterrupt` (not
      `Halted`). It reads more naturally at the call site
      (`if (IsWaitingForInterrupt)`) and reflects the *behavior* — the CPU
      is parked waiting for any enabled interrupt to become pending —
      rather than the opcode mnemonic. Setter is `internal`.
- [x] Add a `bool _haltBugPending` field. The next `Fetch()` after it's
      set reads `_mmu.Read(Pc)` *without* incrementing `Pc`, and clears
      the flag. The byte that follows HALT is therefore read twice — once
      with the bug fetch, once on the subsequent normal fetch.
- [x] In `Hlt()`:
      - If `InterruptMasterEnable`: set `IsWaitingForInterrupt = true`.
        (Wake-and-dispatch is handled by the `Step()` prologue.)
      - If `!InterruptMasterEnable` and `(IE & IF & 0x1F) != 0`: do **not** halt. Set
        `_haltBugPending = true` and return 4. The next instruction will
        fetch the byte after HALT twice.
      - If `!InterruptMasterEnable` and no interrupt pending: set
        `IsWaitingForInterrupt = true`. The prologue's wake-without-dispatch
        path handles resume.
      - Always return 4.
- [x] Modify `Fetch()` to honour `_haltBugPending`: if set, read at `Pc`
      without incrementing, then clear the flag. (The hot path adds one
      branch — keep `[MethodImpl(AggressiveInlining)]`.)

### STOP

- [x] Implement `Stop()` (`0x10`): consume the second byte via `Fetch()`
      and discard it (per spec the encoder writes `10 00`, but real
      hardware ignores whatever byte follows — don't assert it's `0x00`).
      Set a new `bool Stopped { get; private set; }` flag on `Cpu`.
      Return 4.
      **Deviation:** the property is named `IsSleeping` (not `Stopped`),
      mirroring the HALT rename. Reads as `if (IsSleeping)` at the call
      site and conveys "CPU is parked in deep sleep, only joypad wakes it"
      better than the opcode mnemonic.
- [x] Add a `Stopped` short-circuit in `Step()` (above the `Halted`
      check): if stopped, `UpdateInterruptTimer()` and return 4. CPU
      stays parked. *(Now `IsSleeping` short-circuit above the
      `IsWaitingForInterrupt` check.)*
- [x] Wake from STOP is driven by the joypad. Since joypad MMIO isn't
      wired yet, expose a single internal escape hatch — either
      `internal void WakeFromStop()` on `Cpu` or have the prologue check
      "joypad bit (4) of IF set" and clear `Stopped` if so. Pick the
      `IF` bit-4 check: it's the same mechanism the real hardware uses,
      and once joypad MMIO lands in a later step it Just Works without
      another change to the CPU. **Do not** dispatch the joypad
      interrupt as part of waking — clearing `IsSleeping` is enough; the
      normal prologue will dispatch on the next step if `InterruptMasterEnable` is set.
- [x] CGB speed-switch path is explicitly out of scope for DMG.

## Tests

Add a new `CpuInterruptTests.cs` next to the other `Cpu*Tests.cs` files
(same `CpuTestBase` plumbing). Coverage to hit the load-bearing parts of
the model — not exhaustive multiplication of (IME × IF bits × HALT state).

- [x] **Dispatch basics**: with `InterruptMasterEnable=true`, `Mmu.Write(0xFFFF, 0x01)` (IE
      VBlank), `Mmu.Write(0xFF0F, 0x01)` (IF VBlank), and any opcode at
      `Pc`, `Step()` returns 20, `Pc == 0x0040`, `InterruptMasterEnable == false`,
      `Mmu.Read(0xFF0F) == 0x00` (bit cleared), and the SP-stacked word
      equals the original `Pc`. Repeat the same shape for STAT/Timer/
      Serial/Joypad to confirm vectors `0x48`/`0x50`/`0x58`/`0x60`.
      *(Implemented as a single `[Theory]` `DispatchJumpsToVectorAndClearsBit`
      parametrized over all 5 vectors.)*
- [x] **Priority**: with VBlank and Timer both pending, the lower bit
      (VBlank) wins; only its `IF` bit is cleared, Timer stays asserted.
- [x] **No dispatch when InterruptMasterEnable=false**: `InterruptMasterEnable=false`, both IE and IF set →
      `Step()` runs the opcode at `Pc` normally, `IF` is unchanged.
- [x] **No dispatch when (IE & IF) == 0**: `InterruptMasterEnable=true` but no overlap →
      runs the opcode normally.
- [x] **EI delay**: program `EI; NOP; NOP` at `Pc`. After step 1 (EI),
      `InterruptMasterEnable` is still false. After step 2 (NOP), `InterruptMasterEnable` is still false
      (the timer ticks but doesn't fire until end-of-step). After step
      3 (NOP), `InterruptMasterEnable` is true. Pin this exact ordering — it's the spec.
      **Deviation:** the snapshot above pins "third *fetch* is the first
      that sees IME=1" — with our Execute → UpdateInterruptTimer ordering,
      that means the timer hits 0 at the end of step 2, so observing the
      flag *after* `Step()` returns yields IME=true at end of step 2. The
      "still false" in this bullet is internally inconsistent with the
      snapshot. Implemented as `EiHasOneInstructionDelay` (asserts
      IME=false after step 1, IME=true after step 2 — matching the
      snapshot semantics), plus a separate
      `InstructionAfterEiIsNotPreempted` test that pins the externally
      observable property: with `EI; INC A; NOP` and IE/IF pre-asserted,
      the `INC A` runs (not pre-empted by dispatch); dispatch only fires
      on the *third* step.
- [x] **DI cancels pending EI**: `EI; DI; NOP` → after step 3, `InterruptMasterEnable`
      stays false.
- [x] **RETI**: push a target address, set `Pc` at a `0xD9` opcode,
      `InterruptMasterEnable=false`. After `Step()`, `Pc` equals the popped address, `InterruptMasterEnable`
      is true *immediately* (without an instruction delay), step returns
      16.
- [x] **HALT, IME=1, interrupt pending**: `Pc` at `0x76`, IE=IF=0x01,
      `InterruptMasterEnable=true`. First `Step()` is the HALT itself
      (returns 4, sets `IsWaitingForInterrupt`). Second `Step()` services
      the interrupt: returns 20, `Pc == 0x0040`,
      `IsWaitingForInterrupt == false`. (We don't combine HALT and
      dispatch into a single step — the snapshot's prologue clears
      `IsWaitingForInterrupt` first, then dispatches.)
      **Deviation:** as written, this setup makes the prologue dispatch
      *before* the HALT runs (IME=1 + pending overlap is checked before
      `Fetch()`), so the HALT instruction never executes. The realistic
      sequence — and the one that matches real hardware — is HALT runs
      with IF=0, then a peripheral asserts IF later. The implemented
      `HaltImeOnePendingInterrupt` test follows that flow: IF=0 at HALT
      time (step 1: HALT, returns 4, `IsWaitingForInterrupt=true`), then
      `Mmu.Write(IoRegisters.InterruptFlagAddress, 0x01)` (step 2:
      prologue clears `IsWaitingForInterrupt` and dispatches, returns 20).
- [x] **HALT, IME=0, interrupt pending → HALT bug**: `Pc=0x100`, IE=IF=
      `0x01`, `InterruptMasterEnable=false`. Place `0x76` at 0x100 and `0x3C` (INC A) at
      0x101. After `Step()` (HALT itself): `IsWaitingForInterrupt == false`,
      `Pc == 0x101`, A unchanged. After next `Step()`: A incremented and
      `Pc == 0x101` *still* (the bug fetch didn't advance). After third
      `Step()`: A incremented again, `Pc == 0x102`. (Two INCs from one
      written byte is the canonical halt_bug.gb signature.)
- [x] **HALT, IME=0, no interrupt**: `Pc` at `0x76`, IE=IF=0. First
      `Step()` halts (returns 4). Subsequent `Step()`s return 4 without
      advancing `Pc`. Set `IF=0x01` with IE=0x01 → next `Step()` clears
      `IsWaitingForInterrupt` and runs the opcode after HALT (does *not*
      dispatch, because IME=0). Step returns whatever that opcode
      normally costs, not 20.
- [x] **STOP**: `Pc` at `0x10 0x00 0x3C` (STOP, padding, INC A). First
      `Step()` returns 4, `IsSleeping == true`, `Pc` advanced past both
      bytes. Subsequent `Step()`s return 4 without advancing `Pc`. Set
      IF bit 4 (joypad) → next `Step()` clears `IsSleeping`; the step
      after runs `INC A`.
- [x] **DI clears IME**: `InterruptMasterEnable=true`, run `DI` → `InterruptMasterEnable=false`, returns 4.
- [x] **Reset post-boot**: `Reset()` leaves `InterruptMasterEnable == false`.
- [x] Delete `TestDeferredOpcodeThrowsUntilNextStep` from
      `CpuArithmeticTests.cs` — both `0x10` and `0xD9` now have real
      handlers and the theory is empty.
- [x] Add rows to `CpuTStateCoverageTests.cs`:
      - `DI` → 4 T (`0xF3`)
      - `EI` → 4 T (`0xFB`)
      - `HALT` → 4 T (`0x76`)
      - `RETI` → 16 T (`0xD9`, with a stacked return address set up via
        `Sp` + `Mmu.Write`)
      - "Interrupt dispatch" → 20 T. This one doesn't fit the existing
        "execute one opcode" shape because the cycles are spent in the
        prologue, not in `Execute()`. Either extend the theory with an
        `InterruptMasterEnable=true, IE=IF=0x01` row that asserts `Step()` returns 20, or
        add a dedicated `Fact`. A dedicated `Fact` is cleaner — the
        existing `[InlineData]` shape doesn't carry IE/IF/IME setup.
      *(DI/EI/HALT added as `[InlineData]` rows; RETI and interrupt
      dispatch added as dedicated `[Fact]`s — `RetiReturns16TStates` and
      `InterruptDispatchReturns20TStates` — since both need state setup
      the existing theory shape doesn't carry.)*

## Out of scope (explicitly)

- A real `IE`/`IF` MMIO peripheral. The CPU only needs `_mmu.Read`/`Write`
  at `0xFFFF` and `0xFF0F`; the production bus already routes those
  addresses to RAM. Wiring IE/IF as their own register objects (with the
  upper 3 bits of IF reading as 1, etc.) is a bus-level concern for a
  later step.
- `IRequestInterrupt(InterruptType)` on `Cpu`. No peripheral needs it
  yet; adding the method now is dead surface.
- Joypad input (button matrix at `0xFF00`). The STOP wake mechanism is
  defined in terms of `IF` bit 4, so when joypad MMIO arrives later it
  drops in without further CPU changes.
- CGB double-speed mode (the `STOP` half of speed switching).
- DAA (`0x27`) — still throws `NotImplementedException`; rewritten in
  step 6.

## Exit criteria

- `InterruptMasterEnable` replaces `InterruptEnabled` everywhere; `Reset()` leaves `InterruptMasterEnable ==
  false` (post-boot DMG state).
- `IE` (`0xFFFF`) and `IF` (`0xFF0F`) are read and written through the
  bus only. The CPU holds no shadow copy.
- `EI; <op>; <op>` orders correctly: the instruction immediately after
  `EI` executes with `InterruptMasterEnable` still false; `InterruptMasterEnable` becomes true just before
  the second post-EI fetch. `DI` between EI and the delayed enable
  cancels the pending enable.
- `RETI` re-enables `InterruptMasterEnable` in the same step that pops `Pc`, and returns
  16 T.
- Interrupt dispatch costs 20 T, clears the serviced bit in `IF`, clears
  `InterruptMasterEnable`, pushes `Pc`, and jumps to `0x40 + bit*8`. Priority is
  lowest-bit-wins.
- All three HALT branches are exercised: IME=1 pending (halt then
  dispatch on the next step), IME=0 pending (HALT bug — the byte after
  HALT is read twice), IME=0 not pending (sleep until any enabled
  interrupt becomes pending, then resume *without* dispatch).
- `STOP` consumes its trailing byte, parks the CPU, and wakes on `IF`
  bit 4.
- `0x10` and `0xD9` no longer throw; `TestDeferredOpcodeThrowsUntilNextStep`
  is deleted.
- Test suite is green, including the new `CpuInterruptTests` coverage
  and the added rows in `CpuTStateCoverageTests`.
