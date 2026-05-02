# dmg-acid2

The `DmgAcid2Tests` test class loads the dmg-acid2 ROM from this directory at
runtime. The ROM and reference image are not checked in; fetch them from the
canonical source and drop them here.

Source: <https://github.com/mattcurrie/dmg-acid2>

## Files expected

```
TestRoms/dmg-acid2/
  dmg-acid2.gb            # the test ROM
  reference-dmg.bin       # 23040 bytes (160 × 144), one byte per pixel
                          # with the post-palette color index 0..3
                          # (0 = white, 1 = light grey, 2 = dark grey, 3 = black)
```

## Producing `reference-dmg.bin`

The upstream repo ships a PNG (`img/reference-dmg.png`). To convert to the raw
indexed format the test expects:

```sh
# imagemagick:
convert reference-dmg.png -depth 8 -define quantum:format=floating-point gray:- \
  | python3 -c '
import sys
buf = sys.stdin.buffer.read()
# Map shade thresholds (white→0, ltgrey→1, dkgrey→2, black→3)
out = bytearray(b"\x00" if b > 200 else b"\x01" if b > 130 else b"\x02" if b > 60 else b"\x03" for b in buf)
sys.stdout.buffer.write(out)
' > reference-dmg.bin
```

Or any equivalent: load the PNG, map each pixel's grey value to 0..3, write
160 × 144 bytes.

## Running

`dotnet test --filter "FullyQualifiedName~DmgAcid2Tests"` — the test fails
with a "file not found" message if either file is missing.
