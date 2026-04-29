# Step 3 — Implement the 21 "Replace" opcodes

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §1,
[`lr35902-opcode-tables.md`](../lr35902-opcode-tables.md) §1.

These are the opcode bytes that survived from the 8080 byte-for-byte but now
encode different LR35902 instructions. This is where the bulk of the *new*
behavior in the primary opcode table lives.

`0x10` (STOP), `0xCB` (CB prefix), and `0xD9` (RETI) are listed here for
completeness but their full implementation is deferred — `0xCB` to step 4
and `0x10` / `0xD9` to step 5.

## Tasks

- [ ] Loads / stores reusing 8080 byte slots
  - [ ] `0x22` → `LD (HL+),A`
  - [ ] `0x2A` → `LD A,(HL+)`
  - [ ] `0x32` → `LD (HL-),A`
  - [ ] `0x3A` → `LD A,(HL-)`
  - [ ] `0xEA` → `LD (a16),A`
  - [ ] `0xFA` → `LD A,(a16)`
  - [ ] `0xE0` → `LDH (a8),A`
  - [ ] `0xF0` → `LDH A,(a8)`
  - [ ] `0xE2` → `LD (C),A`   (i.e. `LD (0xFF00+C),A`)
  - [ ] `0xF2` → `LD A,(C)`
  - [ ] `0x08` → `LD (a16),SP`
- [ ] Stack / SP arithmetic
  - [ ] `0xE8` → `ADD SP,r8` (Z=0, N=0, H bit-3 of low byte, C bit-7 of low byte)
  - [ ] `0xF8` → `LD HL,SP+r8` (same flag rules as `ADD SP,r8`)
- [ ] Relative jumps
  - [ ] `0x18` → `JR r8`
  - [ ] `0x20` → `JR NZ,r8`
  - [ ] `0x28` → `JR Z,r8`
  - [ ] `0x30` → `JR NC,r8`
  - [ ] `0x38` → `JR C,r8`
- [ ] Stub the deferred opcodes so dispatch is total
  - [ ] `0x10` → `STOP`  (full impl in step 5)
  - [ ] `0xCB` → CB prefix dispatch  (full impl in step 4)
  - [ ] `0xD9` → `RETI`  (full impl in step 5)
- [ ] Mark the 11 illegal opcodes as faulting
  - [ ] `0xD3, 0xDB, 0xDD, 0xE3, 0xE4, 0xEB, 0xEC, 0xED, 0xF4, 0xFC, 0xFD`

## Exit criteria

- The primary opcode dispatch is total — every byte 0x00–0xFF either runs an
  LR35902 instruction, faults (illegal opcode), or routes to a step-4/step-5
  stub.
- Conditional `JR` instructions take the variable cycle count documented in
  `lr35902-opcode-tables.md` §1 (taken vs. not-taken).
- `LDH` and `LD (C),A` use the `0xFF00 + offset` zero-page mapping.
