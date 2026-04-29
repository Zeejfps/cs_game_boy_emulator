# Step 4 — CB-prefix path and the 11 CB operations

References: [`8080-to-LR35902.md`](../8080-to-LR35902.md) §2,
[`lr35902-opcode-tables.md`](../lr35902-opcode-tables.md) §2.

The CB prefix introduces 256 new opcodes, but they are highly regular —
8 operands × N operations laid out by hi-nibble + bit field. Decode them
parametrically; do **not** write 256 case arms.

## Tasks

- [ ] Add a `0xCB`-prefix dispatch entry in the main `Execute` switch
- [ ] Decode CB opcodes via an `(operation, operand)` decoder
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

## Exit criteria

- A single CB dispatch routine drives all 256 sub-opcodes off
  `(operation, operand)` decoded from one byte.
- Flags match the matrix in §3.3 / §2 of the conversion guide:
  shifts/rotates write `Z=result, N=0, H=0, C=shifted-out`; `BIT` writes
  `Z=!bit, N=0, H=1, C=unchanged`; `RES`/`SET` touch no flags.
- Cycle counts: `8` for register operands, `16` for `(HL)`, `12` for
  `BIT n,(HL)` — all **inclusive** of the 4 T prefix fetch.
