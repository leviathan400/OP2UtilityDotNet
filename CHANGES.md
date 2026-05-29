# Changes

## 1.4.1

### Security
- **Malformed bitmap OOM** (`BitmapFile.ReadPixels`): A crafted BMP with
  `pixelOffset > size` would underflow the unsigned subtraction to ~4 GB and
  trigger an OOM allocation. The relationship is now validated and the
  resulting block is checked against the remaining bytes in the stream.
- **Malformed volume seek wrap** (`VolFile.ReadStringTable`): A crafted `.vol`
  with an inner string-table length larger than its container would underflow
  an unsigned subtraction to a multi-GB seek offset. Now validated before
  use.

### Bug fixes
- **`SliceStream.Read`**: Now reads up to `count` bytes, clamping to the
  remaining slice length. Previously returned 0 (spurious EOF) when the
  caller requested more bytes than remained.
- **`SliceStream.Seek`** with `SeekOrigin.End`: Now uses the standard
  `Length + offset` convention (callers pass a non-positive offset).
  Previously subtracted, reversing direction. Position is also clamped to
  `Length` (a valid EOF position) rather than `Length - 1`, so the final
  byte is reachable and zero-length slices behave correctly.
- **`SliceStream` constructor**: Now permits a zero-length slice positioned
  at the end of the underlying stream (e.g. an empty archive entry at EOF).
- **Resource leaks on malformed input**: `VolFile`, `ClmFile`, and the
  `OP2BmpLoader(string, string)` / `OP2BmpLoader(string, Stream)`
  constructors now dispose the underlying `FileStream` if header parsing
  throws. Previously the file handle was leaked.
- **`VolFile.CreateArchive` file-size limit**: Lowered the per-file cap
  from `uint.MaxValue` to `int.MaxValue` to match the signed
  `IndexEntry.fileSize` field and the `int GetSize` API. Files between 2 GB
  and 4 GB no longer round-trip as negative sizes.

## 1.4.0

### Security
- **Archive extraction path traversal** (`ArchiveFile.ExtractAllFiles`): A
  malicious or malformed `.vol`/`.clm` archive could contain entry names with
  directory separators or `..` segments, allowing extraction to write files
  outside the destination directory. Entry names are now restricted to plain
  filenames, and the resolved output path is verified to stay within the
  destination directory.

### Bug fixes
- **`MapHeader.SizeInBytes`**: Changed from a writable instance field to
  `const int`, matching the convention used by every other `*.SizeInBytes`
  constant in the library.
- **`Tag.GetHashCode`**: Previously returned the identity hash of the internal
  byte array, so two `Tag` instances with equal content produced different
  hash codes. This violated the `Equals`/`GetHashCode` contract and broke
  `HashSet<Tag>` and `Dictionary<Tag, T>`. Now hashes the four bytes of
  content.

## 1.3.0
- Fix for bmp files with excess padding.
- Added new unit tests for new features.
- Removed incorrect int cast.
- Fixed `SwapPaletteRedAndBlue` for `TilesetLoader`.
- Added `Clone` method to `BitmapFile`. Added constructor that takes palette
  but not pixels.
- Added `Clone` method to `BmpHeader`.
- Added V4 and V5 image headers. Added clone method to `ImageHeader`.
- Updated to match OP2Utility C++.
- `ArchiveFile`: Added convenience methods for extracting to streams/memory
  by name.
- `Tag`: Added generic stream methods and new constructor.
- Added `OP2BitmapFile`.
- Changed `GetPixel` method formats.
- Fixed issue with `SetPixel`/`GetPixel`.
- Added `SetPixel`, `GetPixel`, and `GetPixelPaletteIndex` to `BitmapFile`.
