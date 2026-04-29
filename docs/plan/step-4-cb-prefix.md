# Step 4 — CB-prefix path and the 11 CB operations

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §2,
[`lr35902-opcode-tables.md`](../lr35902-opcode-tables.md) §2.

The CB prefix introduces 256 new opcodes, but they are highly regular —
8 operands × N operations laid out by `op >> 3` (operation group + bit
field) and `op & 7` (operand). Decode them parametrically; do **not**
write 256 case arms.

## Snapshot of the relevant source after step 3 / 3.1

- `0xCB` is already wired in the main `Execute` switch (`Cpu.cs`) to a
  stub `CbPrefix()` that throws `NotImplementedException("CB prefix —
  wired in step 4")`. Step 4 replaces the body, not the dispatch arm.
- The accumulator rotate handlers `Rlc` / `Rrc` / `Ral` / `Rar` in
  `Cpu.Alu.Special.cs` already implement the `0x07` / `0x0F` / `0x17` /
  `0x1F` primary opcodes (RLCA/RRCA/RLA/RRA). They set `Z=0` per the
  LR35902 spec for the accumulator forms. **The CB-prefix variants set
  `Z=result` instead** and must not reuse those handlers — write fresh
  helpers under different names (e.g. `CbRlc(byte)` returning the new
  byte and updating flags) so the two flag-rule sets stay separate.
- Flag helpers `SetZ(byte)` / `SetN(bool)` / `SetH(bool)` / `SetC(bool)`
  and `SetFlags(result, n, h, c)` already exist in `Cpu.Alu.cs`. Memory
  access uses `_mmu.Read(addr)` / `_mmu.Write(addr, value)`.
- A test in `CpuArithmeticTests.cs::TestDeferredOpcodeThrowsUntilNextStep`
  currently asserts that `0xCB` throws `NotImplementedException`. After
  step 4 lands, `0xCB` runs the sub-opcode, so the `[InlineData(0xCB)]`
  row must be removed from that theory.

## Tasks

### Decoder

- [ ] Create `Cpu.Cb.cs` (new file in `GameboyEmulator.Core/LR35902/`)
      and put the CB dispatch + operation helpers there. Replace the
      `CbPrefix()` body in `Cpu.cs` with a call into this file (or move
      the method outright — pick one and keep `Cpu.cs` thin).
- [ ] Inside `CbPrefix()`:
  - [ ] `Fetch()` the sub-opcode byte (the `0xCB` byte itself was
        already consumed by `Step()`'s outer `Fetch()` before dispatch).
  - [ ] Compute `op = subOpcode >> 3` (0..31) and `operand = subOpcode & 7`
        (0=B, 1=C, 2=D, 3=E, 4=H, 5=L, 6=(HL), 7=A).
  - [ ] Read the operand value via a small helper (switch on `operand`,
        returning `Rb` / `Rc` / … / `_mmu.Read(Rhl)` / `Ra`).
  - [ ] Apply the operation by switching on `op`:
        `0..7` → shift/rotate/swap group (8 ops by `op` value),
        `8..15` → `BIT (op-8), r`, `16..23` → `RES (op-16), r`,
        `24..31` → `SET (op-24), r`.
  - [ ] For `BIT`, return early after reading — no write-back.
  - [ ] For all other operations, write the new value back via the
        operand-writer (mirror of the reader: register set or
        `_mmu.Write(Rhl, value)`).
  - [ ] Return the cycle count: register operand → `8`; `(HL)` operand
        → `16`, except `BIT n,(HL)` → `12`. The 4 T prefix fetch is
        **included** in those totals; `CbPrefix` is responsible for
        the entire 2-byte instruction's T-state budget — do **not**
        add 4 T on top.

### Operation helpers (one method per operation, parametric over the byte)

Each shift/rotate/swap helper takes the current operand value, computes
the new value, sets `Z=result, N=0, H=0` and `C` per the table below,
and returns the new byte. `BIT`/`RES`/`SET` take the bit index too.

- [ ] `RLC` — bit 7 → C and bit 0; `C = old bit7`.
- [ ] `RRC` — bit 0 → C and bit 7; `C = old bit0`.
- [ ] `RL`  — rotate left through C; `C = old bit7`.
- [ ] `RR`  — rotate right through C; `C = old bit0`.
- [ ] `SLA` — shift left, bit 0 = 0; `C = old bit7`.
- [ ] `SRA` — arithmetic shift right (bit 7 preserved); `C = old bit0`.
- [ ] `SWAP` — swap nibbles; `Z=result, N=H=C=0`.
- [ ] `SRL` — logical shift right (bit 7 = 0); `C = old bit0`.
- [ ] `BIT n` — `Z = !((value >> n) & 1)`, `N=0`, `H=1`, `C` unchanged.
- [ ] `RES n` — `value & ~(1 << n)`, no flag changes.
- [ ] `SET n` — `value | (1 << n)`,  no flag changes.

### Tests

Add a new `CpuCbTests.cs` next to the other `Cpu*Tests.cs` files
(same `CpuTestBase` plumbing as the rest of the suite). Coverage to
hit the load-bearing parts of the decoder, not all 256 opcodes:

- [ ] One operand-coverage test per operation group: pick a single
      sub-opcode that exercises the operation against B (or any
      register), verify the result and the four flag bits. 11 tests.
- [ ] Operand decoding: a parameterized test that runs `RLC` against
      each of the 8 operand slots (B, C, D, E, H, L, (HL), A) and
      asserts the right register/memory location was written.
- [ ] `(HL)` cycle accounting: `RLC (HL)` returns 16, `BIT 0,(HL)`
      returns 12, `RES 0,(HL)` returns 16, `SET 0,(HL)` returns 16.
- [ ] Register cycle accounting: `RLC B` returns 8; one row per
      operation group is plenty.
- [ ] Flag-rule regressions worth pinning explicitly:
  - [ ] `SWAP A` with `A=0` sets `Z=1`, clears N/H/C.
  - [ ] `BIT n,r` leaves `C` untouched (run with `C=1` preset, assert
        still `C=1` after).
  - [ ] `RES`/`SET` leave all four flags untouched.
  - [ ] CB `RLC B` with `B=0x00` sets `Z=1` (distinguishes from the
        accumulator `RLCA` form, which always clears Z).
- [ ] Remove the `[InlineData(0xCB)]` row from
      `CpuArithmeticTests.cs::TestDeferredOpcodeThrowsUntilNextStep`.
      `0x10` and `0xD9` stay (they land in step 5).
- [ ] Add `0xCB` rows to the T-state coverage test in
      `CpuTStateCoverageTests.cs` (one register-form CB op = 8 T,
      one `(HL)` form = 16 T, `BIT n,(HL)` = 12 T).

## Out of scope (explicitly)

- `DAA` (`0x27`) currently throws `NotImplementedException` — it is
  rewritten in step 6, not here.
- `STOP` (`0x10`) and `RETI` (`0xD9`) stay as `NotImplementedException`
  stubs until step 5.
- The interrupt service routine and `IE`/`IF` MMIO (step 5).

## Exit criteria

- A single CB dispatch routine drives all 256 sub-opcodes off
  `(operation, operand)` decoded from one byte; no per-opcode case
  arm tables.
- Flags match the matrix in §3.3 / §2 of the conversion guide:
  shifts/rotates write `Z=result, N=0, H=0, C=shifted-out`; `SWAP`
  writes `Z=result, N=0, H=0, C=0`; `BIT` writes `Z=!bit, N=0, H=1,
  C=unchanged`; `RES`/`SET` touch no flags.
- The CB-prefix `RLC` / `RRC` / `RL` / `RR` operations set `Z=result`
  and remain distinct from the accumulator handlers `Rlc`/`Rrc`/`Ral`/
  `Rar` (which set `Z=0`).
- Cycle counts: `8` for register operands, `16` for `(HL)`, `12` for
  `BIT n,(HL)` — all **inclusive** of the 4 T prefix fetch (i.e. what
  `CbPrefix` returns directly; the outer `Step()` does not add to it).
- The `0xCB` row is removed from
  `TestDeferredOpcodeThrowsUntilNextStep`; `0x10` and `0xD9` still
  throw `NotImplementedException`.
- Test suite is green, including the new `CpuCbTests` coverage and the
  added CB rows in `CpuTStateCoverageTests`.
