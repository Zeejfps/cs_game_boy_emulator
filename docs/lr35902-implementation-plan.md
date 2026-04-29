# LR35902 Implementation Plan

A checklist-driven plan for converting the existing Intel 8080 core in
`GameboyEmulator.Core/Intel8080/` into the Game Boy's LR35902 (Sharp SM83) CPU.
Based on [`8080-to-LR35902.md`](8080-to-LR35902.md) §6 ("Suggested order of work").

Cross-references in this doc point at sections of the conversion guide
(`8080-to-LR35902.md`) and the opcode tables (`lr35902-opcode-tables.md`).

---

## Step 1 — Flag layout and ALU helpers

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

## Step 2 — Strip 8080-only plumbing

- [ ] Delete IN/OUT support
  - [ ] Remove `IIOBus.cs`
  - [ ] Remove `Cpu.Io.cs`
  - [ ] Remove the `IIOBus` field/parameter from `Cpu`
- [ ] Remove XCHG / XTHL from `Cpu.Special.cs` (keep the file)
- [ ] Remove the legacy interrupt-injection plumbing
  - [ ] `_isInterruptPending`
  - [ ] `_pendingInterruptOpcode`
  - [ ] Public `Interrupt(byte)` API
- [ ] Remove "undocumented alias" entries from the `Execute` switch
  - [ ] `0x08, 0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0xCB, 0xD9, 0xDD, 0xED, 0xFD`
- [ ] Remove or rewrite the existing 8080 dispatch arms whose opcode bytes are reused
  - [ ] `0x22 Shld`, `0x2A Lhld`, `0x32 StA`, `0x3A LdA`
  - [ ] `0xE0 Rpo`, `0xE2 Jpo`, `0xE8 Rpe`, `0xEA Jpe`
  - [ ] `0xF0 Rp`, `0xF2 Jp`, `0xF8 Rm`, `0xFA Jm`
- [ ] Delete (or rename + rewrite) the per-opcode files now reused
  - [ ] `Cpu.Lhld.cs`, `Cpu.Shld.cs`, `Cpu.LdA.cs`, `Cpu.StA.cs`

## Step 3 — Implement the 21 "Replace" opcodes (§1, `lr35902-opcode-tables.md` §1)

Each opcode byte survives but encodes a new LR35902 instruction.

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
- [ ] Misc
  - [ ] `0x10` → `STOP` (implemented in step 5)
  - [ ] `0xCB` → CB prefix dispatch (implemented in step 4)
  - [ ] `0xD9` → `RETI` (implemented in step 5)
- [ ] Mark the 11 illegal opcodes as faulting
  - [ ] `0xD3, 0xDB, 0xDD, 0xE3, 0xE4, 0xEB, 0xEC, 0xED, 0xF4, 0xFC, 0xFD`

## Step 4 — CB-prefix path and the 11 CB operations (§2)

- [ ] Add a `0xCB`-prefix dispatch entry in the main `Execute` switch
- [ ] Decode CB opcodes via an `(operation, operand)` decoder, **not** 256 case arms
  - [ ] Operand index: `B, C, D, E, H, L, (HL), A` (0–7)
  - [ ] Operation groups by opcode hi-nibble + bit field
- [ ] Implement the 11 operations
  - [ ] `RLC r/(HL)` — Z=result, N=0, H=0, C=shifted-out
  - [ ] `RRC r/(HL)`
  - [ ] `RL  r/(HL)`
  - [ ] `RR  r/(HL)`
  - [ ] `SLA r/(HL)`
  - [ ] `SRA r/(HL)`
  - [ ] `SWAP r/(HL)` — swap nibbles, Z=result, N=H=C=0
  - [ ] `SRL r/(HL)`
  - [ ] `BIT n,r/(HL)` — Z=!bit, N=0, H=1, C unchanged
  - [ ] `RES n,r/(HL)` — no flags
  - [ ] `SET n,r/(HL)` — no flags
- [ ] Cycle accounting
  - [ ] Register-form CB ops = 8 T (4 T prefix + 4 T op)
  - [ ] `(HL)` form = 16 T (read+modify+write), except `BIT n,(HL)` = 12 T (read only)
  - [ ] Do **not** double-count the 4 T prefix fetch — it's already in those totals

## Step 5 — Interrupt model, EI/DI/RETI, HALT, STOP (§4)

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

## Step 6 — DAA (§3.2)

- [ ] Replace the 8080 (additive-only) DAA with the N-aware version
  - [ ] If `N==0` (last op was add):
    - [ ] If `H` or `(A & 0x0F) > 9`: `A += 0x06`
    - [ ] If `C` or `A > 0x9F`: `A += 0x60; C = 1`
  - [ ] If `N==1` (last op was sub):
    - [ ] If `H`: `A = (A - 0x06) & 0xFF`
    - [ ] If `C`: `A -= 0x60`
  - [ ] After: `Z = (A == 0)`; `H = 0`; `C` as set above; `N` preserved
- [ ] Verify every ADD/ADC/SUB/SBC/INC/DEC handler sets H and N correctly (DAA depends on it)

## Step 7 — Validate against Blargg's `cpu_instrs`

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
