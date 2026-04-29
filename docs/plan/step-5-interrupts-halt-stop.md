# Step 5 — Interrupt model, EI/DI/RETI, HALT, STOP

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §4.

The Game Boy doesn't use the 8080's "inject an opcode on the bus" interrupt
model. Instead, the CPU polls `IE & IF` at the top of each step, and dispatch
is a fixed push-PC + jump-to-vector sequence. EI has a one-instruction delay;
RETI does not. HALT has a real hardware bug that test ROMs check for.

## Tasks

- [ ] Replace `InterruptEnabled` with a real **IME** flag plus memory-mapped registers
  - [ ] `IE` at `0xFFFF`
  - [ ] `IF` at `0xFF0F`
- [ ] Move interrupt requests onto the bus
  - [ ] Provide `IRequestInterrupt(InterruptType)` (or let peripherals OR into `IF` directly)
  - [ ] Wire the 5 sources: VBlank (0x40), LCD STAT (0x48), Timer (0x50), Serial (0x58), Joypad (0x60)
- [ ] Add interrupt dispatch at the top of `Step()`
  - [ ] If `IME && (IE & IF & 0x1F) != 0`: pick highest-priority bit, clear that bit in `IF`, clear `IME`, push PC, jump to vector
  - [ ] Charge 20 T (5 M-cycles) for dispatch
- [ ] EI / DI semantics
  - [ ] `DI`: clear IME immediately
  - [ ] `EI`: set IME after the **next** instruction (re-use the existing `_enableInterruptsTimer`; verify it counts exactly 1 instruction)
- [ ] `RETI` (`0xD9`): pop PC, set IME=1 **immediately** (no one-instruction delay)
- [ ] HALT
  - [ ] `IME=1` + interrupt pending: resume and service the interrupt (8080-style)
  - [ ] `IME=0` + `(IE & IF & 0x1F) != 0` at HALT: trigger the **HALT bug** — next byte after HALT is read twice (PC fails to increment for one fetch); required for `halt_bug.gb`
  - [ ] `IME=0` + no interrupt pending: halt normally; resume on any enabled-interrupt pending, **without** dispatching
- [ ] STOP (`0x10 0x00`)
  - [ ] Consume the second byte without executing it
  - [ ] Set a `Stopped` flag; exit on joypad input
  - [ ] CGB speed-switch path is out of scope for DMG

## Exit criteria

- `IE` and `IF` are real memory-mapped registers and the only mechanism by
  which interrupts are signalled.
- `EI; <op>; HALT` orders correctly — the instruction immediately after `EI`
  executes with IME still 0; IME becomes 1 just before the next fetch.
- `RETI` re-enables interrupts in the same step that pops PC (no delay).
- All three HALT branches (IME=1 pending, IME=0 pending = HALT bug, IME=0
  not pending) are exercised; the HALT bug double-reads the byte after HALT.
- STOP swallows the trailing `0x00` and parks the CPU until joypad input.
