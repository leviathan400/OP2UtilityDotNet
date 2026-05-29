using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OP2UtilityDotNet.OP2Map;

namespace UnitTest.src.OP2Map
{
	[TestClass]
	public class MapHeader_Test
	{
		[TestMethod]
		public void SizeInBytes_IsCompileTimeConstant()
		{
			// Static access on the type (not an instance) compiles only if
			// SizeInBytes is a const/static member. This guards against
			// regressing it back to an instance field.
			Assert.AreEqual(20, MapHeader.SizeInBytes);

			System.Reflection.FieldInfo field = typeof(MapHeader).GetField(
				nameof(MapHeader.SizeInBytes),
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
			Assert.IsNotNull(field, "SizeInBytes should be a static field");
			Assert.IsTrue(field.IsLiteral && !field.IsInitOnly, "SizeInBytes should be declared const");
		}

		[TestMethod]
		public void VersionTagValid()
		{
			MapHeader mapHeader = new MapHeader();

			Assert.IsTrue(mapHeader.VersionTagValid());

			mapHeader.versionTag = MapHeader.MinMapVersion + 1;
			Assert.IsTrue(mapHeader.VersionTagValid());

			mapHeader.versionTag = MapHeader.MinMapVersion - 1;
			Assert.IsFalse(mapHeader.VersionTagValid());
		}

		[TestMethod]
		public void WidthInTiles()
		{
			MapHeader mapHeader = new MapHeader();
			mapHeader.lgWidthInTiles = 5;
			mapHeader.heightInTiles = 32;

			Assert.AreEqual(32u, mapHeader.WidthInTiles());
		}

		[TestMethod]
		public void TileCount()
		{
			MapHeader mapHeader = new MapHeader();
			mapHeader.lgWidthInTiles = 5;
			mapHeader.heightInTiles = 32;

			Assert.AreEqual(32u * 32u, mapHeader.TileCount());
		}
	}
}
