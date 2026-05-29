using OP2UtilityDotNet.Bitmap;
using OP2UtilityDotNet.Streams;
using System;
using System.IO;

namespace OP2UtilityDotNet.Sprite
{
	public class OP2BmpLoader : IDisposable
	{
		public OP2BmpLoader(string bmpFilename, string artFilename)
		{
			FileStream fs = new FileStream(bmpFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
			bmpReader = new BinaryReader(fs);
			try
			{
				artFile = ArtFile.Read(artFilename);
			}
			catch
			{
				bmpReader.Dispose();
				throw;
			}
		}

		public OP2BmpLoader(string bmpFilename, Stream artStream)
		{
			FileStream fs = new FileStream(bmpFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
			bmpReader = new BinaryReader(fs);
			try
			{
				artFile = ArtFile.Read(artStream);
			}
			catch
			{
				bmpReader.Dispose();
				throw;
			}
		}

		public OP2BmpLoader(string bmpFilename, ArtFile artFile)
		{
			FileStream fs = new FileStream(bmpFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
			bmpReader = new BinaryReader(fs);

			this.artFile = artFile;
		}

		public void ExtractImage(int index, string filenameOut)
		{
			using (FileStream fs = new FileStream(filenameOut, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				WriteToStream(fs, index);
			}
		}

		public MemoryStream GetImageStream(int index)
		{
			MemoryStream stream = new MemoryStream();

			WriteToStream(stream, index);

			stream.Seek(0, SeekOrigin.Begin);

			return stream;
		}

		private void WriteToStream(Stream destination, int index)
		{
			BitmapFile bmpFile = GetImage(index);
			bmpFile.Serialize(destination);
		}

		public OP2BitmapFile GetImage(int index)
		{
			artFile.VerifyImageIndexInBounds(index);

			ImageMeta imageMeta = artFile.imageMetas[index];

			Color[] palette;
			if (imageMeta.type.bShadow != 0)
			{
				// Shadow graphic uses a 2 color palette
				palette = new Color[2];
				System.Array.Copy(artFile.palettes[imageMeta.paletteIndex].colors, palette, palette.Length);
			}
			else
			{
				// Normal graphic
				palette = new Color[artFile.palettes[imageMeta.paletteIndex].colors.Length];
				System.Array.Copy(artFile.palettes[imageMeta.paletteIndex].colors, palette, palette.Length);
			}

			// Palette length is always 256 for OP2's master BMP
			uint pixelOffset = (uint)(imageMeta.pixelDataOffset + 14 + ImageHeader.SizeInBytes + 256 * Color.SizeInBytes);

			int height = (int)System.Math.Abs(imageMeta.height);
			int pitch = ImageHeader.CalculatePitch(imageMeta.GetBitCount(), (int)imageMeta.width);

			SliceStream pixels = GetPixels(pixelOffset, (uint)(height * pitch));

			byte[] pixelContainer = new byte[height * pitch];
			pixels.Read(pixelContainer, 0, pixelContainer.Length);

			// Outpost 2 stores pixels in normal raster scan order (top-down). This requires a negative height for BMP file format.
			if (imageMeta.height > uint.MaxValue) {
				throw new System.Exception("Image height is too large to fit in standard bitmap file format.");
			}

			return new OP2BitmapFile(imageMeta, palette, pixelContainer);
		}

		// Bmp loader for Outpost 2 specific BMP file
		// Contains many images in pixels section with a default palette. 
		// Actual palette data and range of pixels to form each image is contained in the .prt file.
		private BinaryReader bmpReader;
		private ArtFile artFile;

		private SliceStream GetPixels(uint startingIndex, uint length)
		{
			return new SliceStream(bmpReader.BaseStream, startingIndex, length);
		}

		public void Dispose()
		{
			if (bmpReader != null)
			{
				bmpReader.Dispose();
				bmpReader = null;
			}
		}
	}
}
