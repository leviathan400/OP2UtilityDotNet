# OP2UtilityDotNet

A C# .NET library for working with Outpost 2 game files, including maps (.map), saved games (.op2), and archive files (.vol, .clm).

This is the C# .NET port of OP2Utility for C++, providing equivalent functionality with a modern .NET API.

## Installation

Reference the DLL or drop the source files directly into your C# or VB.NET projects.

All classes are available in the `OP2UtilityDotNet` namespace.

## Features

- **Archive Management**: Read and write VOL and CLM archive files
- **Map Handling**: Load, modify, and save Outpost 2 map files
- **Bitmap Processing**: Work with Outpost 2's custom bitmap formats
- **Sprite/Animation Support**: Handle sprite sheets and animation data
- **Resource Management**: Unified interface for accessing files from disk or archives

## Main Classes and Functions

### ResourceManager
Unified resource access from both loose files and archives.

```csharp
ResourceManager(string archiveDirectory)
byte[] GetResource(string filename, bool accessArchives = true)
Stream GetResourceStream(string filename, bool accessArchives = true)
List<string> GetAllFilenames(string searchPattern)
string FindContainingArchivePath(string filename)
```

### Archive Files

#### VolFile
Handle .vol archive files.

```csharp
VolFile(string filename)
static void CreateArchive(string filename, List<string> filesToPack)
int GetCount()
string GetName(int index)
bool Contains(string filename)
Stream OpenStream(int index)
void ExtractFile(int index, string filename)
void ExtractAllFiles(string directory)
```

#### ClmFile
Handle .clm (audio) archive files - similar API to VolFile.

### Map

Load, modify, and save Outpost 2 map files (.map) and saved games (.op2).

```csharp
static Map ReadMap(string filename)
static Map ReadSavedGame(string filename)
void Write(string filename)

// Properties
List<Tile> tiles
Rect clipRect
List<TilesetSource> tilesetSources
List<TileMapping> tileMappings
List<TerrainType> terrainTypes
List<TileGroup> tileGroups

// Methods
uint WidthInTiles()
uint HeightInTiles()
CellType GetCellType(int x, int y)
void SetCellType(CellType cellType, int x, int y)
bool GetLavaPossible(int x, int y)
void SetLavaPossible(bool lavaPossible, int x, int y)
int GetTilesetIndex(int x, int y)
int GetImageIndex(int x, int y)
```

### BitmapFile

Read and write indexed color bitmap files (1, 2, 4, and 8-bit).

### ArtFile

Handle Outpost 2 sprite/animation files.

### TilesetLoader

Load Outpost 2 tilesets (custom PBMP format or standard BMP).

## Utility Structs and Classes

- **Point32, Point16**: 2D point structures
- **Rect**: Rectangle structure with serialization support
- **Color**: RGBA color structure
- **Palette**: 256-color palette
- **Tag**: 4-character tag for file format identification
- **BitTwiddle**: Utility functions for bit manipulation

## Namespaces

- **OP2UtilityDotNet**: Core classes and utilities
- **OP2UtilityDotNet.Archive**: VOL and CLM archive handling
- **OP2UtilityDotNet.OP2Map**: Map and saved game file handling
- **OP2UtilityDotNet.Bitmap**: Bitmap file processing
- **OP2UtilityDotNet.Sprite**: Sprite and animation support
- **OP2UtilityDotNet.Streams**: Stream utilities

## Example Usage

### C# Example

```csharp
using OP2UtilityDotNet.OP2Map;

Map map = Map.ReadMap("test.map");

Console.WriteLine("Map size: " + map.WidthInTiles() + " x " + map.HeightInTiles());
Console.WriteLine("Number of tile sets: " + map.tilesetSources.Count);
```

### VB.NET Example

```vb
Imports OP2UtilityDotNet.OP2Map

Dim currentMap As Map = Map.ReadMap("test.map")

Console.WriteLine("Map size: " & currentMap.WidthInTiles() & " x " & currentMap.HeightInTiles())
Console.WriteLine("Number of tile sets: " & currentMap.tilesetSources.Count)
```

## Related Projects

- [OP2Utility](https://github.com/OutpostUniverse/OP2Utility) - The original C++ implementation

## Links

- [OP2 Mission Editor](https://forum.outpost2.net/index.php/topic,6318.0.html) - Outpost Universe forum thread

