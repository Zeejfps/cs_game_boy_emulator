# Step 3 — Implement the 21 "Replace" opcodes

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §1,
[`lr35902-opcode-tables.md`](../lr35902-opcode-tables.md) §1.

These are the opcode bytes that survived from the 8080 byte-for-byte but now
encode different LR35902 instructions. This is where the bulk of the *new*
behavior in the primary opcode table lives.

`0x10` (STOP), `0xCB` (CB prefix), and `0xD9` (RETI) are listed here for
completeness but their full implementation is deferred — `0xCB` to step 4
and `0x10` / `0xD9` to step 5.

## Snapshot of the relevant source after step 2

- The dispatch switch in `Cpu.cs` is a non-exhaustive switch expression with
  no default arm. Every byte that was removed in step 2 (the 21 Replace
  bytes minus the four still wired to old handlers, plus the 11 illegal
  bytes) currently throws `SwitchExpressionException` at runtime. Step 3
  closes the gap so the primary table is total.
- `Cpu.LoadStore.cs` exists and currently holds `LdAb`/`LdAd`/`StAb`/`StAd`
  (the 8080 LDAX/STAX handlers, which are valid LR35902 ops at
  `0x0A`/`0x1A`/`0x02`/`0x12`). The new memory-access Replace ops
  (`0x22`/`0x2A`/`0x32`/`0x3A`/`0xEA`/`0xFA`/`0xE0`/`0xF0`/`0xE2`/`0xF2`/`0x08`)
  go in this file.
- `Cpu.Branch.cs` holds the existing `Jmp`, `Jnz/Jz/Jnc/Jc`, `Cnz/Cz/Cnc/Cc`,
  `Rnz/Rz/Rnc/Rcy`, `Ret`, `Call`, `Pchl`, `Rst*`. The 8080 sign/parity
  branch stubs were deleted in step 2, so the file has no `NotImplemented`
  stubs left. The new `JR` family (`0x18, 0x20, 0x28, 0x30, 0x38`) goes here.
- `Cpu.Stack.cs` holds the push/pop pairs and `Sphl` (`0xF9`, becomes
  `LD SP,HL`). The new `0xF8 LD HL,SP+r8` goes here. `0xE8 ADD SP,r8` is a
  16-bit-arithmetic op against SP — put it in `Cpu.Alu.RegPair.cs`
  alongside `Dad*` so it shares neighborhood with the other 16-bit ALU.
- `Cpu.Alu.cs` already has `AddSpSigned(sbyte r8)` from step 1; both
  `0xE8` and `0xF8` use it.
- `Cpu.Interrupts.cs` holds `Di` and `Ei`. `0xD9 RETI` and `0x10 STOP` stubs
  go here — they are interrupt-/halt-adjacent and step 5 fills them in.
- `Cpu.cs` itself already has the `Halted` flag and `_enableInterruptsTimer`.
  Step 3 adds a `Stopped` flag (or equivalent) only if the STOP stub
  needs it; otherwise leave that to step 5.

### Renames deferred from step 1

Step 1 explicitly deferred these handler renames to step 3 (the 8080
mnemonic stayed while the flag math was rewritten):

- `Inr*` → `Inc*`
- `Dcr*` → `Dec*`
- `Dad*` → `AddHl*`
- `Cma`  → `Cpl`
- `Stc`  → `Scf`
- `Cmc`  → `Ccf`
- `PushPsw` → `PushAf`
- `PopPsw`  → `PopAf`

Renames are mechanical (handler + dispatch arm + tests). Do them in their
own commit before adding the new opcodes so the diff stays readable.

## Tasks

### Loads / stores reusing 8080 byte slots (`Cpu.LoadStore.cs`)

- [x] `0x22` → `LD (HL+),A` — store A at `(HL)`, then `HL++`. 8 T.
- [x] `0x2A` → `LD A,(HL+)` — load A from `(HL)`, then `HL++`. 8 T.
- [x] `0x32` → `LD (HL-),A` — store A at `(HL)`, then `HL--`. 8 T.
- [x] `0x3A` → `LD A,(HL-)` — load A from `(HL)`, then `HL--`. 8 T.
- [x] `0xEA` → `LD (a16),A` — fetch 16-bit immediate, write A there. 16 T.
- [x] `0xFA` → `LD A,(a16)` — fetch 16-bit immediate, read A from there. 16 T.
- [x] `0xE0` → `LDH (a8),A` — write A to `0xFF00 + fetched a8`. 12 T.
- [x] `0xF0` → `LDH A,(a8)` — read A from `0xFF00 + fetched a8`. 12 T.
- [x] `0xE2` → `LD (C),A` — write A to `0xFF00 + C`. **One-byte instruction**
      (no immediate). 8 T.
- [x] `0xF2` → `LD A,(C)` — read A from `0xFF00 + C`. One-byte. 8 T.
- [x] `0x08` → `LD (a16),SP` — fetch 16-bit immediate, write SP (little-endian)
      there. 20 T.

### Stack / SP arithmetic

- [x] `0xE8` → `ADD SP,r8` (`Cpu.Alu.RegPair.cs`) — `SP = AddSpSigned(r8)`.
      Flags Z=0, N=0, H from bit-3 of low byte, C from bit-7 of low byte. 16 T.
- [x] `0xF8` → `LD HL,SP+r8` (`Cpu.Stack.cs`) — `HL = AddSpSigned(r8)` (do not
      mutate SP; the helper sets the same flag rules as 0xE8). 12 T.

### Relative jumps (`Cpu.Branch.cs`)

- [x] `0x18` → `JR r8` — unconditional signed 8-bit relative jump. 12 T.
- [x] `0x20` → `JR NZ,r8` — 12 T taken, 8 T not taken.
- [x] `0x28` → `JR Z,r8`  — 12 T / 8 T.
- [x] `0x30` → `JR NC,r8` — 12 T / 8 T.
- [x] `0x38` → `JR C,r8`  — 12 T / 8 T.

Conditional `JR` must always fetch the displacement (PC advances by 1) before
deciding whether to add it to PC, so the not-taken path still consumes the
operand byte.

### Stub the deferred opcodes so dispatch is total

- [x] `0x10` → `Stop()` in `Cpu.Interrupts.cs` —
      `throw new NotImplementedException("STOP — wired in step 5")`.
      Dispatch arm only; step 5 replaces with the real implementation
      (consume trailing `0x00`, set `Stopped`, exit on joypad input).
- [x] `0xCB` → `CbPrefix()` —
      `throw new NotImplementedException("CB prefix — wired in step 4")`.
      Place next to the dispatch switch in `Cpu.cs` (step 4 replaces the
      body with the prefix decoder).
- [x] `0xD9` → `Reti()` in `Cpu.Interrupts.cs` —
      `throw new NotImplementedException("RETI — wired in step 5")`.

### Illegal opcodes — make the dispatch total and faulting

- [x] Add a single `Illegal(byte opcode)` helper that throws
      `InvalidOperationException($"Illegal LR35902 opcode 0x{opcode:X2}")`.
- [x] Wire dispatch arms for all 11 illegal bytes:
      `0xD3, 0xDB, 0xDD, 0xE3, 0xE4, 0xEB, 0xEC, 0xED, 0xF4, 0xFC, 0xFD`,
      each calling `Illegal(<byte>)`.
- [x] ~~Add a `_ => Illegal(opcode)` default arm to the `Execute` switch so
      the expression is exhaustive and every unmapped byte (from a future
      decoding bug) also faults instead of throwing
      `SwitchExpressionException`.~~ **Deviation:** with all 256 bytes
      mapped explicitly the C# compiler rejects `_ => Illegal(opcode)` as
      an unreachable pattern (CS8510). The exhaustiveness guarantee is
      already enforced statically by the compiler in this configuration,
      so the catch-all was dropped. If a future change removes any
      explicit arm, the compiler will warn that the switch is
      non-exhaustive — at which point the catch-all should be reinstated.

### Renames deferred from step 1

- [x] Rename `Inr*` → `Inc*`, `Dcr*` → `Dec*`, `Dad*` → `AddHl*`,
      `Cma` → `Cpl`, `Stc` → `Scf`, `Cmc` → `Ccf`,
      `PushPsw` → `PushAf`, `PopPsw` → `PopAf`. Update dispatch arms and
      tests to match. (No behavior change — pure rename.)

### Tests

- [x] Add focused tests for each new opcode in the appropriate existing
      test file (`CpuMovTests` for the load/store ops and `LD HL,SP+r8`,
      `CpuBranchTests` for `JR`, `CpuStackTests` or a new
      `CpuArithmeticTests` row for `ADD SP,r8`). Cover at minimum:
  - [x] `LD (HL+),A` / `LD A,(HL+)` increment HL and wrap from `0xFFFF` → `0x0000`.
  - [x] `LD (HL-),A` / `LD A,(HL-)` decrement HL and wrap from `0x0000` → `0xFFFF`.
  - [x] `LDH` / `LD (C),A` use the `0xFF00 + offset` zero-page mapping.
  - [x] `LD (a16),SP` writes both bytes (little-endian).
  - [x] `JR cc` taken vs not-taken cycle counts (12 / 8) and that the
        operand is consumed in both cases.
  - [x] `JR r8` displacement is signed (negative branch works).
  - [x] `ADD SP,r8` and `LD HL,SP+r8` flag rules: Z=0, N=0, H from bit-3
        of low byte, C from bit-7 of low byte; signed displacement applied
        to a 16-bit `SP` produces the correct unsigned result.
- [x] Add a test that executing each of the 11 illegal opcodes throws
      `InvalidOperationException` (one parameterized test row per byte).
- [x] Add a test that `0x10` / `0xCB` / `0xD9` throw
      `NotImplementedException` until their respective steps land. (Can be
      a single parameterized test; lets step 4 / step 5 see those tests
      flip from "throws" to "passes".)

## Exit criteria

- The primary opcode dispatch is total — every byte 0x00–0xFF either runs
  an LR35902 instruction, calls `Illegal(...)` (the 11 illegal bytes; the
  catch-all default arm was dropped — see deviation note in the Illegal
  opcodes task), or routes to a step-4/step-5 stub.
- No path in `Execute` can throw `SwitchExpressionException` anymore.
- Conditional `JR` instructions take the variable cycle count documented in
  `lr35902-opcode-tables.md` §1 (taken vs. not-taken).
- `LDH` and `LD (C),A` use the `0xFF00 + offset` zero-page mapping.
- `0xE8` / `0xF8` route through `AddSpSigned` and produce identical flag
  results (Z=0, N=0, H, C as documented).
- `0x10` / `0xCB` / `0xD9` are reachable from dispatch and throw
  `NotImplementedException` (they fault explicitly rather than silently
  doing nothing, so the gap is visible until step 4 / step 5 lands).
- Renames from the "deferred from step 1" list are done; no handler in
  `GameboyEmulator.Core/LR35902/` still uses an 8080-only mnemonic
  (`Inr`/`Dcr`/`Dad`/`Cma`/`Stc`/`Cmc`/`PushPsw`/`PopPsw`).
- Test suite is green, including new coverage for the 21 Replace opcodes
  and the 11 illegal opcodes.

## Out of scope (explicitly)

- Cycle-count corrections on the **Keep**/**Modify** opcodes whose handlers
  still return 8080 T-states (e.g. `Jmp` returns 10 instead of 16,
  `MoveBc` returns 5 instead of 4). The 21 new handlers added here use
  LR35902 T-states; the broader cycle-count audit lives in
  [`step-3.1-cycle-count-audit.md`](step-3.1-cycle-count-audit.md), which
  runs immediately after step 3.
- The actual STOP / RETI / CB-prefix bodies (steps 4 and 5).
- Memory-mapped `IE`/`IF` registers and the interrupt service routine
  (step 5).
