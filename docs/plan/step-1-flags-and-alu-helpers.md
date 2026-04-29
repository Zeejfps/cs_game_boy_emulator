# Step 1 — Flag layout and ALU helpers

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §3.1, §3.3, §3.4.

This step is intentionally first: rewriting `CpuFlags` will break every ALU
handler in the project, and the helpers added here are what those handlers
get rewritten on top of. Expect the build to be red until step 1 is done.

## Tasks

- [ ] Rewrite `CpuFlags.cs` to the LR35902 layout `Z N H C 0 0 0 0` (§3.1)
  - [ ] Remove `S` (Sign) and `P` (Parity) constants and any helpers that touch them
  - [ ] Rename `AC` → `H` (Half-Carry); add `N` (Subtract)
  - [ ] Set bit values: `C = 1<<4`, `H = 1<<5`, `N = 1<<6`, `Z = 1<<7`
  - [ ] Ensure low 4 bits of F are forced to 0 on read and ignored on write (relevant to `POP AF`)
- [ ] Add flag helpers on `Cpu` (§3.4)
  - [ ] `SetZ(byte)`, `SetN(bool)`, `SetH(bool)`, `SetC(bool)`
  - [ ] `SetFlags(byte result, bool n, bool h, bool c)` for ALU ops
  - [ ] `Add8(byte a, byte b, bool carryIn)` — returns result, sets Z/N/H/C
  - [ ] `Sub8(byte a, byte b, bool carryIn)` — returns result, sets Z/N/H/C
  - [ ] `AddHL(ushort rr)` — bit-11 H, bit-15 C, N=0, Z preserved
  - [ ] `AddSpSigned(sbyte r8)` — low-byte 4-bit H / 8-bit C, Z=0, N=0
- [ ] Update every ALU handler in `ALU.cs` to use the new helpers
  - [ ] INC r / INC (HL): Z, N=0, H (C unchanged)
  - [ ] DEC r / DEC (HL): Z, N=1, H (C unchanged)
  - [ ] ADD/ADC: Z, N=0, H, C
  - [ ] SUB/SBC/CP: Z, N=1, H, C
  - [ ] AND: Z, N=0, **H=1**, C=0
  - [ ] OR/XOR: Z, N=0, H=0, C=0
  - [ ] Accumulator rotates `0x07/0x0F/0x17/0x1F`: **Z=0**, N=0, H=0, C
  - [ ] CPL: N=1, H=1 (other flags unchanged)
  - [ ] SCF: C=1, N=0, H=0
  - [ ] CCF: C^=1, N=0, H=0

## Exit criteria

- `CpuFlags` only exposes `Z, N, H, C` (plus `None`/`All`).
- Project compiles; every existing ALU handler routes its flag math through
  the new helpers rather than setting flag bits inline.
- Spot-check: `INC B` from `B=0x0F` produces `B=0x10` with `Z=0, N=0, H=1`,
  C unchanged.
