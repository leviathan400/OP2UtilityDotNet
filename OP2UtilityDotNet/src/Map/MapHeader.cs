
using System.IO;

namespace OP2UtilityDotNet.OP2Map
{
	// Outpost 2 map and save file header
	public class MapHeader
	{
		public const int SizeInBytes = 20;

		public MapHeader() { }

		// Minimum map version tag that Outpost 2 will accept without error
		public const uint MinMapVersion = 0x1010;

		// Default map version tag used by original Outpost 2 maps
		public const uint CurrentMapVersion = 0x1011;

		// The map's version tag must be >= MinMapVersion or Outpost 2 will abort loading the map.
		public uint versionTag = CurrentMapVersion;

		// True if file represents a saved game instead of a map file.
		public int bSavedGame;

		// Width of map in tiles in base 2 Logarithm representation.
		// Outpost 2 maps must be representable by a base 2 Logarithm.
		// See function WidthInTiles.
		public uint lgWidthInTiles;

		// Height of map in tiles. Will be rounded to a power of 2 by Outpost 2.
		public uint heightInTiles;

		// Number of tile sets on map.
		public uint tilesetCount;

		// Map Width in Tiles.
		public uint WidthInTiles()
		{
			return (uint)(1 << (int)lgWidthInTiles); // Use bitwise left shift to translate base 2 logarithm.
		}

		public bool VersionTagValid()
		{
			return versionTag >= MinMapVersion;
		}

		// Total number of tiles on map.
		public uint TileCount()
		{
			return heightInTiles << (int)lgWidthInTiles;
		}

		public void Serialize(BinaryWriter writer)
		{
			writer.Write(versionTag);
			writer.Write(bSavedGame);
			writer.Write(lgWidthInTiles);
			writer.Write(heightInTiles);
			writer.Write(tilesetCount);
		}

		public MapHeader(BinaryReader reader)
		{
			versionTag = reader.ReadUInt32();
			bSavedGame = reader.ReadInt32();
			lgWidthInTiles = reader.ReadUInt32();
			heightInTiles = reader.ReadUInt32();
			tilesetCount = reader.ReadUInt32();
		}
	}

	//static_assert(20 == sizeof(MapHeader), "MapHeader is an unexpected size");
}
