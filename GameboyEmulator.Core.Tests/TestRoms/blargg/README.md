# Blargg test ROMs

The `BlarggCpuInstrTests` test class loads the freely-redistributable Blargg
test ROMs from this directory at runtime. They are not currently checked in;
fetch them from the canonical archive and drop them here.

Source: <https://github.com/retrio/gb-test-roms>

Expected layout (matches the `[InlineData]` paths in `BlarggCpuInstrTests.cs`):

```
TestRoms/blargg/
  cpu_instrs/individual/
    01-special.gb
    02-interrupts.gb
    03-op sp,hl.gb
    04-op r,imm.gb
    05-op rp.gb
    06-ld r,r.gb
    07-jr,jp,call,ret,rst.gb
    08-misc instrs.gb
    09-op r,r.gb
    10-bit ops.gb
    11-op a,(hl).gb
  halt_bug.gb
  instr_timing.gb
```

The `.gb` files are tracked as binary in `.gitattributes`. The csproj copies
`TestRoms/**/*.gb` to the test output directory on build.

The Blargg tests are tagged `[Trait("Category", "Blargg")]` so CI can opt in
or out:

```
dotnet test --filter Category=Blargg
dotnet test --filter Category!=Blargg
```
