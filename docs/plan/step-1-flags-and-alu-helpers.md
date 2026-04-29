# Step 1 — Flag layout, ALU partial reorganization, and ALU helpers

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §3.1, §3.3, §3.4.

This step is intentionally first: rewriting `CpuFlags` will break every ALU
handler in the project, and the helpers added here are what those handlers
get rewritten on top of. Expect the build to be red until step 1 is done.

## File reorganization

Today's ALU code is spread across `Cpu.Arithmetic.cs`, `Cpu.Logic.cs`,
`Cpu.Immediate.cs`, `Cpu.Special.cs`, and `Cpu.RegPair.cs`. Consolidate them
under a `Cpu.Alu.*` partial-class group so the new helpers have a clear home:

- [ ] **New** `Cpu.Alu.cs` — shared flag helpers (`SetZ/SetN/SetH/SetC`,
  `SetFlags`, `Add8`, `Sub8`, `AddHL`, `AddSpSigned`). No opcode handlers.
- [ ] Rename `Cpu.Arithmetic.cs` → `Cpu.Alu.Arithmetic.cs` (ADD/ADC/SUB/SBB, INR/DCR)
- [ ] Rename `Cpu.Logic.cs` → `Cpu.Alu.Logic.cs` (ANA/XRA/ORA/CMP)
- [ ] Rename `Cpu.Immediate.cs` → `Cpu.Alu.Immediate.cs` (ADI/ACI/SUI/SBI/ANI/XRI/ORI/CPI)
- [ ] Rename `Cpu.Special.cs` → `Cpu.Alu.Special.cs` (rotates, CMA, STC, CMC, DAA)
- [ ] Rename `Cpu.RegPair.cs` → `Cpu.Alu.RegPair.cs` (DAD; INX/DCX stay here too)

8080 mnemonics (`Inr`, `Dcr`, `Dad`, `Cma`, `Stc`, `Cmc`, `PushPsw`, `PopPsw`)
stay for now — step 3 renames them to `Inc`/`Dec`/`AddHl`/`Cpl`/`Scf`/`Ccf`/
`PushAf`/`PopAf`. This step only swaps the flag math underneath them.

## Tasks

### Flag layout (`CpuFlags.cs`)

- [ ] Rewrite to the LR35902 layout `Z N H C 0 0 0 0` (§3.1)
  - [ ] Remove `S` (Sign), `P` (Parity), and `A` (Aux Carry) constants
  - [ ] Add `H` (Half-Carry) and `N` (Subtract)
  - [ ] Bit values: `C = 1<<4`, `H = 1<<5`, `N = 1<<6`, `Z = 1<<7`
  - [ ] `All = Z | N | H | C` (`0xF0` — note: changes from 8080's `0xD5`)

### F-register masking (`Cpu.cs`)

- [ ] Replace the `Flags` auto-property with a manual property that masks the
  low nibble on both read and write: `set => _flags = value & (CpuFlags)0xF0;`
  This is the canonical place for "low 4 bits of F are always 0" — `POP AF` /
  `PUSH AF` (currently `PopPsw` / `PushPsw`) inherit it for free.

### ALU helpers (`Cpu.Alu.cs`)

- [ ] `SetZ(byte)`, `SetN(bool)`, `SetH(bool)`, `SetC(bool)`
- [ ] `SetFlags(byte result, bool n, bool h, bool c)` for ALU ops
- [ ] `Add8(byte a, byte b, bool carryIn)` — returns result, sets Z/N/H/C
- [ ] `Sub8(byte a, byte b, bool carryIn)` — returns result, sets Z/N/H/C
- [ ] `AddHL(ushort rr)` — bit-11 H, bit-15 C, N=0, Z preserved
- [ ] `AddSpSigned(sbyte r8)` — low-byte 4-bit H / 8-bit C, Z=0, N=0
  (helper only; opcodes `0xE8`/`0xF8` are wired in step 3)

### Wire handlers through helpers

- [ ] `Cpu.Alu.Arithmetic.cs`
  - [ ] ADD/ADC → `Add8` (Z, N=0, H, C)
  - [ ] SUB/SBB → `Sub8` (Z, N=1, H, C)
  - [ ] INR (→ INC) → `Add8(value, 1, false)` but **preserve C** (Z, N=0, H)
  - [ ] DCR (→ DEC) → `Sub8(value, 1, false)` but **preserve C** (Z, N=1, H)
- [ ] `Cpu.Alu.Logic.cs`
  - [ ] ANA: Z, N=0, **H=1**, C=0 — drop the 8080 AC-quirk `((a|b)&0x08)`
  - [ ] XRA/ORA: Z, N=0, H=0, C=0
  - [ ] CMP → `Sub8` and discard the result (Z, N=1, H, C)
- [ ] `Cpu.Alu.Immediate.cs` — route through the same helpers as the register forms
- [ ] `Cpu.Alu.Special.cs`
  - [ ] RLC/RRC/RAL/RAR (`0x07/0x0F/0x17/0x1F`): **Z=0**, N=0, H=0, C from rotated bit
  - [ ] CMA (→ CPL): N=1, H=1, Z and C unchanged
  - [ ] STC (→ SCF): C=1, N=0, H=0
  - [ ] CMC (→ CCF): C^=1, N=0, H=0
  - [ ] **DAA: stub to `throw new NotImplementedException()`** — depends on
    flags S/P/A that no longer exist; rewritten in step 6
- [ ] `Cpu.Alu.RegPair.cs`
  - [ ] DAD (→ ADD HL): route through `AddHL` (currently only sets C; needs H too)

### Cleanup

- [ ] Delete `Parity()` static helper from the old `Cpu.Arithmetic.cs` —
  unreachable after the rewrite (LR35902 has no parity flag and DAA
  doesn't use it)
- [ ] Delete `ComputeAddFlags`, `ComputeSubFlags`, `ComputeAnaFlags`,
  `ComputeLogicalFlags`, `ComputeInrDcrFlags` once every caller is migrated

### Tests

The 8080 test suite (`CpuArithmeticTests`, `CpuLogicTests`, `CpuBranchTests`,
`CpuMovTests`, `CpuStackTests`, `CpuIoTests`) references `CpuFlags.S/P/A`
heavily and will not compile once those constants are gone.

- [ ] **Pick a strategy** (and record it here):
  - **(A)** Port the still-relevant tests to LR35902 flag expectations as part
    of this step. Recommended for `CpuArithmeticTests` and `CpuLogicTests`
    since their flag expectations change with the new helpers.
  - **(B)** Comment out / `[Skip]` the failing test classes and re-port them
    incrementally as opcodes are rewritten in step 3. Faster path to a green
    build, at the cost of zero coverage during steps 2–3.
- [ ] `CpuIoTests` will be deleted in step 2 along with IN/OUT — no need to
  port.
- [ ] `CpuMovTests` / `CpuStackTests` use `CpuFlags.All` only as an opaque
  "everything set" sentinel for non-flag-mutating instructions; updating them
  is mechanical (`CpuFlags.All` still exists, just with a different bit
  pattern).

## Semantics notes (call out in PR description)

- `CpuFlags.All` silently changes from `S|Z|A|P|C` (0xD5) to `Z|N|H|C` (0xF0).
  Code reading `(byte)CpuFlags.All` will get a different value — search for
  any such cast before merging.
- 8080's ANA "AC quirk" (H derived from `((a|b)&0x08)`) is intentionally
  dropped; LR35902 hard-sets H=1 on AND.
- 8080 SUB's `(a&0xF) < (b&0xF) + borrow` half-carry is equivalent to
  LR35902's `((a&0xF) - (b&0xF) - carryIn) < 0`. Behavior is preserved; only
  the framing changes.

## Exit criteria

- `CpuFlags` exposes only `Z, N, H, C` (plus `None`, `All`).
- `Cpu.Alu.cs` holds the shared flag helpers; every per-category ALU handler
  routes its flag math through them rather than setting flag bits inline.
- F-register low nibble is masked to zero on read/write (verify via
  `PUSH AF` / `POP AF` round-trip).
- `Daa()` throws `NotImplementedException`; all other handlers compile and
  execute.
- Test-strategy decision above is executed: either the ported tests pass, or
  the deferred test classes are clearly marked skipped with a TODO pointing
  at step 3.
- Spot-checks (manual or as new tests):
  - `INC B` with `B=0x0F` → `B=0x10`, `Z=0 N=0 H=1`, C unchanged.
  - `SUB` with `A=0x10, val=0x01` → `A=0x0F`, `Z=0 N=1 H=1 C=0`.
  - `ADD HL,HL` with `HL=0x0FFF` → `HL=0x1FFE`, `N=0 H=1 C=0`, Z unchanged.
  - `POP AF` of stack value `0x12FF` → `A=0x12, F=0xF0`.
