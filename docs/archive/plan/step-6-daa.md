# Step 6 — DAA

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §3.2.

The 8080 DAA only "fixes" additive BCD results. The LR35902 DAA inspects the
**N** flag and corrects either an add or a subtract. This is why every
ADD/ADC/SUB/SBC/INC/DEC handler must already be setting H and N correctly
(step 1) — DAA reads them as input.

## Snapshot of the relevant source after step 5

- `Cpu.Alu.Special.cs` hosts `Daa()`, currently a single
  `throw new NotImplementedException("DAA rewrites in step 6")`. The opcode
  table in `Cpu.cs` already routes `0x27 => Daa()`, so step 6 is purely a
  body rewrite — no dispatch wiring needed.
- `Cpu.Alu.cs` exposes the helpers DAA needs: `SetZ(byte)`, `SetH(bool)`,
  `SetC(bool)`, plus direct `_flags` access. `Ra` is the accumulator
  property; `Flags` reads back the current flag set as `CpuFlags`.
  N is preserved by simply not touching it.
- `Add8` (in `Cpu.Alu.cs`) sets `N=false`, plus correct `H` and `C`.
  `Sub8` sets `N=true`, plus correct `H` and `C`. Both go through
  `SetFlags(result, n, h, c)` so the inputs DAA cares about are already
  trustworthy. `Inc`/`Dec` (in `Cpu.Alu.Arithmetic.cs`) save C, call
  `Add8`/`Sub8`, then restore C — they leave N and H set correctly. No
  per-handler audit of ADD/ADC/SUB/SBC/INC/DEC is needed; the helpers are
  the single source of truth and they're already right.
- `TestDaaThrowsUntilStep6` in `CpuArithmeticTests.cs` (around line 697)
  pins the current "throws" behavior. After step 6 lands, this test is
  obsolete and gets deleted.
- `CpuTStateCoverageTests.cs` has no `0x27` row today. DAA is a 4 T
  instruction — add the row alongside the other rotate/special accumulator
  ops.

## Tasks

### Implement DAA

- [x] Replace the throw in `Cpu.Alu.Special.cs::Daa()` with the N-aware
      algorithm from §3.2:
      ```
      if (Flags & N) == 0:                       // last op was add
          if H or (Ra & 0x0F) > 9: correction |= 0x06
          if C or  Ra        > 0x99: correction |= 0x60; setC = true
          Ra += correction
      else:                                      // last op was sub
          if H: correction |= 0x06
          if C: correction |= 0x60
          Ra -= correction
      Z = (Ra == 0); H = 0; N preserved; C as set above (sub branch leaves C unchanged).
      ```
      Use the additive threshold `Ra > 0x99` (equivalent to the §3.2
      pseudocode's `Ra > 0x9F` once the low-nibble correction is folded
      in — both give the canonical `01-special` behavior; pick whichever
      reads cleaner). Compute the correction first, then mutate `Ra`
      once. Do **not** clear `N` — the spec says `N` is preserved.
- [x] Return `4` T.

### Tests

Add a `Daa` test block in `CpuArithmeticTests.cs` (alongside the existing
`Add`/`Sub`/`Inc`/`Dec` blocks). Cover the load-bearing branches without
exhaustively enumerating BCD inputs.

- [x] **Add path, no carries needed**: `A=0x12`, `N=0`, `H=0`, `C=0` →
      `A=0x12`, flags `Z=0, N=0, H=0, C=0`.
- [x] **Add path, low-nibble correction**: `A=0x0A`, `N=0`, `H=0`, `C=0` →
      `A=0x10` (low nibble `>9` triggers `+0x06`), `C=0`, `H=0`.
- [x] **Add path, half-carry correction**: `A=0x10`, `N=0`, `H=1`, `C=0` →
      `A=0x16` (`H=1` triggers `+0x06` even though low nibble is fine).
- [x] **Add path, high-nibble correction sets C**: `A=0xA0`, `N=0`, `H=0`,
      `C=0` → `A=0x00`, `C=1`, `Z=1`, `H=0`. (Pins both the `>0x99`
      branch and the `Z` recompute on overflow.)
- [x] **Add path, carry-in forces high correction**: `A=0x10`, `N=0`,
      `H=0`, `C=1` → `A=0x70`, `C=1`. (Earlier ADD overflowed to a
      pre-existing `C=1`; DAA must keep `C=1`.)
- [x] **Sub path, no correction**: `A=0x42`, `N=1`, `H=0`, `C=0` →
      `A=0x42`, `N=1`, `H=0`, `C=0`.
- [x] **Sub path, half-borrow correction**: `A=0x06`, `N=1`, `H=1`, `C=0`
      → `A=0x00` (`-0x06`), `Z=1`, `N=1`, `H=0`, `C=0`.
- [x] **Sub path, full-borrow correction**: `A=0x00`, `N=1`, `H=0`, `C=1`
      → `A=0xA0` (`-0x60`), `N=1`, `H=0`, `C=1`. (Sub branch never
      *clears* C.)
- [x] **Sub path, both corrections**: `A=0x00`, `N=1`, `H=1`, `C=1` →
      `A=0x9A` (`-0x66`), `N=1`, `H=0`, `C=1`.
- [x] **End-to-end add+DAA**: program `LD A,0x15; ADD A,0x27; DAA`
      (`0x3E 0x15 0xC6 0x27 0x27`). After three steps `A=0x42`,
      `N=0`, `H=0`, `C=0` (15 + 27 = 42 BCD).
- [x] **End-to-end sub+DAA**: program `LD A,0x42; SUB A,0x15; DAA`
      (`0x3E 0x42 0xD6 0x15 0x27`). After three steps `A=0x27`,
      `N=1`, `H=0`, `C=0` (42 − 15 = 27 BCD). Pins that the SUB sets
      `N=1` and DAA reads it.
- [x] **H is always cleared**: same setup as the half-borrow row but
      assert `(Flags & CpuFlags.H) == 0` explicitly.

- [x] Delete `TestDaaThrowsUntilStep6` from `CpuArithmeticTests.cs`.
- [x] Add `DAA → 4 T (0x27)` row to the
      `StepReturnsExpectedTStates` theory in `CpuTStateCoverageTests.cs`.

## Out of scope (explicitly)

- Auditing each ADD/ADC/SUB/SBC/INC/DEC handler. Step 1 already routed
  these through `Add8`/`Sub8`, which set N and H from a single place.
  Trusting the helpers (and the existing flag tests) is sufficient — DAA
  doesn't need a fresh audit.
- Blargg ROM execution. `01-special` and `09-op r,r` validation lives in
  step 7; step 6 is unit-test green only.

## Exit criteria

- `Daa()` no longer throws. The opcode at `0x27` runs as a 4 T
  instruction.
- DAA produces correct BCD results for both additive and subtractive
  prior operations across the unit tests above.
- After DAA: `H == 0` always, `Z` reflects the corrected `A`, `N` is
  preserved from the prior operation, `C` is set per §3.2 (the add path
  may set or leave C; the sub path never clears C).
- `TestDaaThrowsUntilStep6` is deleted; `CpuTStateCoverageTests` covers
  `0x27`.
- Test suite is green.
