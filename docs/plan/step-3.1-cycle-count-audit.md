# Step 3.1 — Cycle-count audit for Keep / Modify opcodes

References: [`lr35902-opcode-tables.md`](../lr35902-opcode-tables.md) §1.

Most existing handlers were ported from the 8080 implementation and still
return **8080 T-state counts**. The 21 Replace handlers added in step 3
already use LR35902 counts; this step brings every other handler in line so
that `Step()` returns a faithful T-state budget end-to-end. We fix this now
(rather than after step 7) because the PPU and timer integration in later
steps both depend on cycle accuracy — wrong cycles make every subsequent
bug hard to reason about.

## Snapshot of current vs. target cycles

Current return values come from the post-step-3 source. Target values are
from `lr35902-opcode-tables.md` §1. Rows where current already matches the
target are omitted.

### `Cpu.cs`

| Opcode | Handler | Current | Target | Notes |
|--------|---------|--------:|-------:|-------|
| 0x76   | `Hlt`   | 7       | 4      | LR35902 HALT is 4 T. |

### `Cpu.Mov.cs`

| Opcode range | Handler shape   | Current | Target |
|--------------|-----------------|--------:|-------:|
| reg ↔ reg (0x40–0x7F minus the `(HL)` row/col and 0x76) | `MoveXy` | 5 | 4 |
| `(HL)` source: 0x46/0x4E/0x56/0x5E/0x66/0x6E/0x7E | `MoveXm` | 7 | 8 |
| `(HL)` dest: 0x70/0x71/0x72/0x73/0x74/0x75/0x77 | `MoveMx` | 7 | 8 |

### `Cpu.Mvi.cs`

| Opcode | Handler | Current | Target |
|--------|---------|--------:|-------:|
| 0x06/0x0E/0x16/0x1E/0x26/0x2E/0x3E | `MviB`…`MviA` | 7 | 8 |
| 0x36 | `MviM` | 10 | 12 |

### `Cpu.LoadStore.cs`

| Opcode | Handler | Current | Target |
|--------|---------|--------:|-------:|
| 0x0A   | `LdAb`  | 7 | 8 |
| 0x1A   | `LdAd`  | 7 | 8 |
| 0x02   | `StAb`  | 7 | 8 |
| 0x12   | `StAd`  | 7 | 8 |

### `Cpu.Lxi.cs`

| Opcode | Handler | Current | Target |
|--------|---------|--------:|-------:|
| 0x01/0x11/0x21/0x31 | `LxiB/D/H/Sp` | 10 | 12 |

### `Cpu.Alu.RegPair.cs`

| Opcode | Handler | Current | Target |
|--------|---------|--------:|-------:|
| 0x03/0x13/0x23/0x33 | `InxB/D/H/Sp` (renamed `IncBc`/`IncDe`/`IncHl`/`IncSp` if step 3 did the rename) | 5 | 8 |
| 0x0B/0x1B/0x2B/0x3B | `DcxB/D/H/Sp` (likewise `DecBc`…) | 5 | 8 |
| 0x09/0x19/0x29/0x39 | `Dad*` (→ `AddHl*`) | 10 | 8 |

### `Cpu.Alu.Arithmetic.cs`

| Opcode | Handler | Current | Target |
|--------|---------|--------:|-------:|
| 0x04/0x0C/0x14/0x1C/0x24/0x2C/0x3C | `Inr*` (→ `Inc*`) | 5 | 4 |
| 0x05/0x0D/0x15/0x1D/0x25/0x2D/0x3D | `Dcr*` (→ `Dec*`) | 5 | 4 |
| 0x34 | `InrM` (→ `IncHlInd`) | 10 | 12 |
| 0x35 | `DcrM` (→ `DecHlInd`) | 10 | 12 |
| 0x86/0x8E/0x96/0x9E | `AddM/AdcM/SubM/SbbM` | 7 | 8 |

### `Cpu.Alu.Logic.cs`

| Opcode | Handler | Current | Target |
|--------|---------|--------:|-------:|
| 0xA6/0xAE/0xB6/0xBE | `AnaM/XraM/OraM/CmpM` | 7 | 8 |

### `Cpu.Alu.Immediate.cs`

| Opcode | Handler | Current | Target |
|--------|---------|--------:|-------:|
| 0xC6/0xD6/0xE6/0xF6/0xCE/0xDE/0xEE/0xFE | `Adi/Sui/Ani/Ori/Aci/Sbi/Xri/Cpi` | 7 | 8 |

### `Cpu.Stack.cs`

| Opcode | Handler | Current | Target |
|--------|---------|--------:|-------:|
| 0xC1/0xD1/0xE1/0xF1 | `PopB/D/H/Psw` (→ `PopBc/De/Hl/Af`) | 10 | 12 |
| 0xC5/0xD5/0xE5/0xF5 | `PushB/D/H/Psw` (→ `PushBc/De/Hl/Af`) | 11 | 16 |
| 0xF9 | `Sphl` (→ `LdSpHl`) | 5 | 8 |

### `Cpu.Branch.cs`

| Opcode | Handler | Current | Target |
|--------|---------|--------:|-------:|
| 0xC0/0xC8/0xD0/0xD8 taken | `Rnz/Rz/Rnc/Rcy` | 11 | 20 |
| 0xC0/0xC8/0xD0/0xD8 not taken | `Rnz/Rz/Rnc/Rcy` | 5 | 8 |
| 0xC9 | `Ret` | 10 | 16 |
| 0xC3 | `Jmp` (→ `Jp`) | 10 | 16 |
| 0xC2/0xCA/0xD2/0xDA | `Jnz/Jz/Jnc/Jc` | **always 10 (bug)** | 16 taken / 12 not taken |
| 0xCD | `Call` | 17 | 24 |
| 0xC4/0xCC/0xD4/0xDC taken | `Cnz/Cz/Cnc/Cc` | 17 | 24 |
| 0xC4/0xCC/0xD4/0xDC not taken | `Cnz/Cz/Cnc/Cc` | 11 | 12 |
| 0xE9 | `Pchl` (→ `JpHl`) | 5 | 4 |
| 0xC7…0xFF (RST 0–7) | `Rst*` | 11 | 16 |

The `Jnz/Jz/Jnc/Jc` row is a pre-existing **correctness bug**, not just a
T-state mismatch — currently the same cycle count is returned regardless of
whether the branch was taken. Fix it by returning `16` on the taken arm and
`12` on the fall-through arm, mirroring the structure already used by the
conditional CALLs.

### Already correct (do not touch)

- `Cpu.cs::Nop` — 4 ✓
- `Cpu.Alu.Arithmetic.cs` register ADD/ADC/SUB/SBB (0x80–0x97 except `M`) — 4 ✓
- `Cpu.Alu.Logic.cs` register ANA/XRA/ORA/CMP (0xA0–0xBF except `M`) — 4 ✓
- `Cpu.Alu.Special.cs` RLCA/RRCA/RLA/RRA/DAA/CPL/SCF/CCF — 4 ✓
- `Cpu.Interrupts.cs` `Di`/`Ei` — 4 ✓
- All 21 step-3 Replace handlers (added with LR35902 counts).

## Tasks

### Code changes

- [ ] Update each handler in the diff tables above to its target T-state.
      Group commits per-file so a regression bisect points at the right
      handler.
- [ ] Fix the `Jnz/Jz/Jnc/Jc` taken-vs-not-taken bug (16 / 12) — restructure
      to match the `Cnz`-family early-return shape rather than a single
      `return 10` after the `if`.
- [ ] Cross-check every conditional handler (`Rnz/Rz/Rnc/Rcy`, `Cnz/Cz/Cnc/Cc`,
      `Jnz/Jz/Jnc/Jc`, and the `JR cc` family from step 3) returns the
      correct T-state on **both** the taken and not-taken paths.

### Tests

The existing test suite asserts the **old** 8080 cycle counts in many
places. Sweep cycle assertions before changing the production values so a
green build can be restored quickly.

- [ ] Grep each `*Tests.cs` for `Step()`/`Execute(...)` cycle-count
      assertions. Update every numeric expectation to the target column
      above.
- [ ] Add focused regression tests for the conditional jump cycle bug:
  - [ ] `JNZ` taken returns 16, not-taken returns 12.
  - [ ] Same for `JZ`, `JNC`, `JC`.
- [ ] Add a small "T-state coverage" test that runs one representative
      opcode per category (NOP, MOV r,r, MOV r,(HL), MVI, LXI, INX, DCR,
      ADD A,r, ADD A,(HL), ADI, RST, JP, JP cc taken/not, CALL, CALL cc
      taken/not, RET, RET cc taken/not, PUSH, POP, ADD HL,rr, LDH (a8),A,
      JR cc taken/not) through `Step()` and asserts the published T-state
      count. Cheap insurance against future drift.

## Exit criteria

- Every handler in `GameboyEmulator.Core/LR35902/` returns the T-state
  count documented in `lr35902-opcode-tables.md` §1.
- Conditional jumps (`JP cc`, `JR cc`, `RET cc`, `CALL cc`) return the
  correct *different* counts on the taken vs. not-taken paths — verified
  by tests, not just by inspection.
- The `Jnz/Jz/Jnc/Jc` bug is fixed; an unconditional regression test
  guards against re-introducing it.
- The full test suite is green against the new counts.
- No handler still returns an 8080-only value (5, 7, 10 where the LR35902
  expects 4, 8, 12, etc.). A `grep "return 5;"` / `"return 7;"` /
  `"return 10;"` over `LR35902/` returns only intentional matches (e.g.
  not-taken branch returns of 12 are fine — `return 5;` should not appear
  at all in handler bodies after this step).
