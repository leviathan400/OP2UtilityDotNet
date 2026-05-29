# Changes

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
