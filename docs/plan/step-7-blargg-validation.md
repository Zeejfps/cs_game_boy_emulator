# Step 7 — Validate against Blargg's `cpu_instrs`

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §6.1.

Blargg's `cpu_instrs.gb` is the canonical correctness gate for a DMG CPU.
It expects a CPU that's already past the boot ROM, so initialize register
state to the standard post-boot DMG values before jumping to `0x0100`.

## Tasks

- [ ] Get the test ROM running end-to-end
- [ ] Initialize post-boot register state (DMG) when skipping the boot ROM (§6.1)
  - [ ] `PC = 0x0100`
  - [ ] `SP = 0xFFFE`
  - [ ] `AF = 0x01B0`  (A=0x01, F = Z|H|C, N clear)
  - [ ] `BC = 0x0013`
  - [ ] `DE = 0x00D8`
  - [ ] `HL = 0x014D`
  - [ ] `IME = 0`
- [ ] Run each `cpu_instrs` sub-test and fix failures
  - [ ] 01 — special
  - [ ] 02 — interrupts
  - [ ] 03 — op sp,hl
  - [ ] 04 — op r,imm
  - [ ] 05 — op rp
  - [ ] 06 — ld r,r
  - [ ] 07 — jr,jp,call,ret,rst
  - [ ] 08 — misc instrs
  - [ ] 09 — op r,r
  - [ ] 10 — bit ops
  - [ ] 11 — op a,(hl)
- [ ] Run `halt_bug.gb` to confirm the HALT-bug edge case from step 5
- [ ] Run `instr_timing.gb` once cycle accounting is trusted

## Exit criteria

- All 11 `cpu_instrs` sub-tests print "Passed".
- `halt_bug.gb` passes (validates the IME=0 + pending interrupt branch from
  step 5).
- `instr_timing.gb` passes — proves the cycle counts in
  `lr35902-opcode-tables.md` are wired up correctly.
