using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OP2UtilityDotNet.Streams;

namespace UnitTest.src.Stream
{
	[TestClass]
	public class SliceReader_Test
	{
		[TestMethod]
		public void SliceIsBoundsChecked()
		{
			using (FileStream fs = new FileStream("src/Stream/data/SimpleStream.txt", FileMode.Open))
			{
				SliceStream stream = null;

				// Proects against overflow of the stream position
				Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream = new SliceStream(fs, 1, long.MaxValue));
				Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream = new SliceStream(fs, long.MaxValue, 1));
				
				// Protects against overstepping the bounds of the source stream
				Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream = new SliceStream(fs, 0, fs.Length + 1));
				Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream = new SliceStream(fs, 1, fs.Length));
				Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream = new SliceStream(fs, fs.Length, 1));
			}
		}

		[TestMethod]
		public void SliceCanBeSliced()
		{
			using (FileStream fs = new FileStream("src/Stream/data/SimpleStream.txt", FileMode.Open))
			{
				SliceStream slice = new SliceStream(fs, 1, fs.Length - 2);
				
				// Creating a 0 byte (empty) slice works
				new SliceStream(slice, 0, 0);
				//new SliceStream(slice, slice.Length, 0);

				// Creating a full sized slice works
				new SliceStream(slice, 0, slice.Length);

				// Slicing in works
				new SliceStream(slice, 0, slice.Length - 1);  // Chop end
				new SliceStream(slice, 1, slice.Length - 1);  // Chop beginning
				new SliceStream(slice, 1, slice.Length - 2);  // Chop both sides
			}
		}

		[TestMethod]
		public void SliceOfSliceIsBoundsChecked()
		{
			using (FileStream fs = new FileStream("src/Stream/data/SimpleStream.txt", FileMode.Open))
			{
				SliceStream slice = new SliceStream(fs, 1, fs.Length - 2);

				// Protects against overstepping the bounds of the original slice
				Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SliceStream(slice, 0, slice.Length + 1));
				Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SliceStream(slice, 1, slice.Length));
				Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SliceStream(slice, slice.Length, 1));
			}
		}

		[TestMethod]
		public void SliceOfSliceMatchesCorrectOffset1Param()
		{
			using (FileStream fs = new FileStream("src/Stream/data/SimpleStream.txt", FileMode.Open))
			{
				fs.Seek(1, SeekOrigin.Begin);
				SliceStream slice1 = new SliceStream(fs, fs.Length - 2);
				slice1.Seek(1, SeekOrigin.Begin);
				SliceStream slice2 = new SliceStream(slice1, slice1.Length - 2);

				fs.Seek(2, SeekOrigin.Begin);
				slice1.Seek(1, SeekOrigin.Begin);

				int data, data1, data2;
				data = fs.ReadByte();
				data1 = slice1.ReadByte();
				data2 = slice2.ReadByte();

				// Reads through different slices at corresponding offsets should match up
				Assert.AreEqual(data, data1);
				Assert.AreEqual(data, data2);
			}
		}

		[TestMethod]
		public void EmptySliceAtEndOfStreamIsAllowed()
		{
			// A zero-length slice positioned exactly at the end of the
			// underlying stream is legitimate (e.g. an empty archive entry
			// stored at the very end of the file).
			using (FileStream fs = new FileStream("src/Stream/data/SimpleStream.txt", FileMode.Open))
			{
				SliceStream slice = new SliceStream(fs, fs.Length, 0);
				Assert.AreEqual(0L, slice.Length);
				Assert.AreEqual(0L, slice.Position);
				Assert.AreEqual(0, slice.Read(new byte[4], 0, 4));
			}
		}

		[TestMethod]
		public void ReadClampsToRemainingBytes()
		{
			// Stream.Read contract: read up to `count` bytes. The previous
			// implementation returned 0 (spurious EOF) when the requested
			// count was larger than the bytes remaining in the slice.
			using (FileStream fs = new FileStream("src/Stream/data/SimpleStream.txt", FileMode.Open))
			{
				SliceStream slice = new SliceStream(fs, 0, fs.Length);

				byte[] buffer = new byte[fs.Length + 16];
				int bytesRead = slice.Read(buffer, 0, buffer.Length);

				Assert.AreEqual(fs.Length, bytesRead);
				Assert.AreEqual(fs.Length, slice.Position);

				// A second read at EOF should return 0.
				Assert.AreEqual(0, slice.Read(buffer, 0, 1));
			}
		}

		[TestMethod]
		public void SeekFromEndUsesPositiveOffsetConvention()
		{
			// SeekOrigin.End computes (Length + offset). offset == 0 lands at
			// EOF; a negative offset moves back into the slice.
			using (FileStream fs = new FileStream("src/Stream/data/SimpleStream.txt", FileMode.Open))
			{
				SliceStream slice = new SliceStream(fs, 0, fs.Length);

				Assert.AreEqual(slice.Length, slice.Seek(0, SeekOrigin.End));
				Assert.AreEqual(slice.Length - 1, slice.Seek(-1, SeekOrigin.End));

				// Final byte must be readable after seeking to length-1.
				slice.Seek(-1, SeekOrigin.End);
				int lastByte = slice.ReadByte();
				Assert.AreNotEqual(-1, lastByte);
			}
		}

		[TestMethod]
		public void SliceOfSliceMatchesCorrectOffset2Param()
		{
			using (FileStream fs = new FileStream("src/Stream/data/SimpleStream.txt", FileMode.Open))
			{
				SliceStream slice1 = new SliceStream(fs, 1, fs.Length - 2);
				SliceStream slice2 = new SliceStream(slice1, 1, slice1.Length - 2);

				fs.Seek(2, SeekOrigin.Begin);
				slice1.Seek(1, SeekOrigin.Begin);

				int data, data1, data2;
				data = fs.ReadByte();
				data1 = slice1.ReadByte();
				data2 = slice2.ReadByte();

				// Reads through different slices at corresponding offsets should match up
				Assert.AreEqual(data, data1);
				Assert.AreEqual(data, data2);
			}
		}
	}
}
