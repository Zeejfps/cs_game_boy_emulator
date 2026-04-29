# Step 1 — Flag layout, ALU partial reorganization, and ALU helpers

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §3.1, §3.3, §3.4.

This step is intentionally first: rewriting `CpuFlags` will break every ALU
handler in the project, and the helpers added here are what those handlers
get rewritten on top of. Expect the build to be red until step 1 is done.

## File reorganization

Today's ALU code is spread across `Cpu.Arithmetic.cs`, `Cpu.Logic.cs`,
`Cpu.Immediate.cs`, `Cpu.Special.cs`, and `Cpu.RegPair.cs`. Consolidate them
under a `Cpu.Alu.*` partial-class group so the new helpers have a clear home:

- [x] **New** `Cpu.Alu.cs` — shared flag helpers (`SetZ/SetN/SetH/SetC`,
  `SetFlags`, `Add8`, `Sub8`, `AddHL`, `AddSpSigned`). No opcode handlers.
- [x] Rename `Cpu.Arithmetic.cs` → `Cpu.Alu.Arithmetic.cs` (ADD/ADC/SUB/SBB, INR/DCR)
- [x] Rename `Cpu.Logic.cs` → `Cpu.Alu.Logic.cs` (ANA/XRA/ORA/CMP)
- [x] Rename `Cpu.Immediate.cs` → `Cpu.Alu.Immediate.cs` (ADI/ACI/SUI/SBI/ANI/XRI/ORI/CPI)
- [x] Rename `Cpu.Special.cs` → `Cpu.Alu.Special.cs` (rotates, CMA, STC, CMC, DAA)
- [x] Rename `Cpu.RegPair.cs` → `Cpu.Alu.RegPair.cs` (DAD; INX/DCX stay here too)

8080 mnemonics (`Inr`, `Dcr`, `Dad`, `Cma`, `Stc`, `Cmc`, `PushPsw`, `PopPsw`)
stay for now — step 3 renames them to `Inc`/`Dec`/`AddHl`/`Cpl`/`Scf`/`Ccf`/
`PushAf`/`PopAf`. This step only swaps the flag math underneath them.

## Tasks

### Flag layout (`CpuFlags.cs`)

- [x] Rewrite to the LR35902 layout `Z N H C 0 0 0 0` (§3.1)
  - [x] Remove `S` (Sign), `P` (Parity), and `A` (Aux Carry) constants
  - [x] Add `H` (Half-Carry) and `N` (Subtract)
  - [x] Bit values: `C = 1<<4`, `H = 1<<5`, `N = 1<<6`, `Z = 1<<7`
  - [x] `All = Z | N | H | C` (`0xF0` — note: changes from 8080's `0xD5`)

### F-register masking (`Cpu.cs`)

- [x] Replace the `Flags` auto-property with a manual property that masks the
  low nibble on both read and write: `set => _flags = value & (CpuFlags)0xF0;`
  This is the canonical place for "low 4 bits of F are always 0" — `POP AF` /
  `PUSH AF` (currently `PopPsw` / `PushPsw`) inherit it for free.

### ALU helpers (`Cpu.Alu.cs`)

- [x] `SetZ(byte)`, `SetN(bool)`, `SetH(bool)`, `SetC(bool)`
- [x] `SetFlags(byte result, bool n, bool h, bool c)` for ALU ops
- [x] `Add8(byte a, byte b, bool carryIn)` — returns result, sets Z/N/H/C
- [x] `Sub8(byte a, byte b, bool carryIn)` — returns result, sets Z/N/H/C
- [x] `AddHL(ushort rr)` — bit-11 H, bit-15 C, N=0, Z preserved
- [x] `AddSpSigned(sbyte r8)` — low-byte 4-bit H / 8-bit C, Z=0, N=0
  (helper only; opcodes `0xE8`/`0xF8` are wired in step 3)

### Wire handlers through helpers

- [x] `Cpu.Alu.Arithmetic.cs`
  - [x] ADD/ADC → `Add8` (Z, N=0, H, C)
  - [x] SUB/SBB → `Sub8` (Z, N=1, H, C)
  - [x] INR (→ INC) → `Add8(value, 1, false)` but **preserve C** (Z, N=0, H)
  - [x] DCR (→ DEC) → `Sub8(value, 1, false)` but **preserve C** (Z, N=1, H)
- [x] `Cpu.Alu.Logic.cs`
  - [x] ANA: Z, N=0, **H=1**, C=0 — drop the 8080 AC-quirk `((a|b)&0x08)`
  - [x] XRA/ORA: Z, N=0, H=0, C=0
  - [x] CMP → `Sub8` and discard the result (Z, N=1, H, C)
- [x] `Cpu.Alu.Immediate.cs` — route through the same helpers as the register forms
- [x] `Cpu.Alu.Special.cs`
  - [x] RLC/RRC/RAL/RAR (`0x07/0x0F/0x17/0x1F`): **Z=0**, N=0, H=0, C from rotated bit
  - [x] CMA (→ CPL): N=1, H=1, Z and C unchanged
  - [x] STC (→ SCF): C=1, N=0, H=0
  - [x] CMC (→ CCF): C^=1, N=0, H=0
  - [x] **DAA: stub to `throw new NotImplementedException()`** — depends on
    flags S/P/A that no longer exist; rewritten in step 6
- [x] `Cpu.Alu.RegPair.cs`
  - [x] DAD (→ ADD HL): route through `AddHL` (currently only sets C; needs H too)

### Cleanup

- [x] Delete `Parity()` static helper from the old `Cpu.Arithmetic.cs` —
  unreachable after the rewrite (LR35902 has no parity flag and DAA
  doesn't use it)
- [x] Delete `ComputeAddFlags`, `ComputeSubFlags`, `ComputeAnaFlags`,
  `ComputeLogicalFlags`, `ComputeInrDcrFlags` once every caller is migrated

### Tests

The 8080 test suite (`CpuArithmeticTests`, `CpuLogicTests`, `CpuBranchTests`,
`CpuMovTests`, `CpuStackTests`, `CpuIoTests`) references `CpuFlags.S/P/A`
heavily and will not compile once those constants are gone.

- [x] **Strategy chosen: A for ALU tests, partial port for CpuBranchTests.**
  - `CpuArithmeticTests` and `CpuLogicTests` ported to LR35902 flag
    expectations. `TestDaa` replaced with `TestDaaThrowsUntilStep6`. New
    `TestIncBExitCriteria`, `TestSubExitCriteria`, `TestDadH` cover the
    spot-checks from the exit criteria.
  - `CpuBranchTests`: parameterized rows for the 8080-only sign/parity
    conditions (RP/RM/RPE/RPO, JP/JM/JPE/JPO, CP/CM/CPE/CPO) deleted; the
    handlers throw `NotImplementedException` and step 3 will repurpose the
    opcodes. The Z/C-conditional rows and the unconditional jump/call/ret/rst
    tests are kept and pass.
  - `CpuMovTests` / `CpuStackTests` / `CpuIoTests` compile unchanged
    (`CpuFlags.All` and `Z`/`C` constants are still in scope). Added
    `TestPopAfMasksLowNibble` to `CpuStackTests` for the F-mask spot-check.
- [x] `CpuIoTests` will be deleted in step 2 along with IN/OUT — no need to
  port. (Compiles unchanged for now; deferred to step 2.)
- [x] `CpuMovTests` / `CpuStackTests` use `CpuFlags.All` only as an opaque
  "everything set" sentinel for non-flag-mutating instructions; updating them
  is mechanical (`CpuFlags.All` still exists, just with a different bit
  pattern). (No changes needed; both compile and pass against the new layout.)

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

- [x] `CpuFlags` exposes only `Z, N, H, C` (plus `None`, `All`).
- [x] `Cpu.Alu.cs` holds the shared flag helpers; every per-category ALU
  handler routes its flag math through them rather than setting flag bits
  inline.
- [x] F-register low nibble is masked to zero on read/write (verified via
  `TestPopAfMasksLowNibble`).
- [x] `Daa()` throws `NotImplementedException`; all other handlers compile
  and execute (verified via `TestDaaThrowsUntilStep6`).
- [x] Test-strategy decision above is executed: ALU tests ported and passing,
  branch S/P-condition rows dropped (handlers throw `NotImplementedException`
  to be repurposed in step 3). Suite is green: 355 passed, 0 failed.
- [x] Spot-checks (covered by new tests):
  - [x] `INC B` with `B=0x0F` → `B=0x10`, `Z=0 N=0 H=1`, C unchanged
    (`TestIncBExitCriteria`).
  - [x] `SUB` with `A=0x10, val=0x01` → `A=0x0F`, `Z=0 N=1 H=1 C=0`
    (`TestSubExitCriteria`).
  - [x] `ADD HL,HL` with `HL=0x0FFF` → `HL=0x1FFE`, `N=0 H=1 C=0`,
    Z unchanged (`TestDadH`).
  - [x] `POP AF` of stack value `0x12FF` → `A=0x12, F=0xF0`
    (`TestPopAfMasksLowNibble`).
