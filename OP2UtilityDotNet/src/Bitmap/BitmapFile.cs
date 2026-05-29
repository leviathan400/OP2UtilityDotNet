using System.IO;
using System.Linq;

namespace OP2UtilityDotNet.Bitmap
{
	public enum ScanLineOrientation
	{
		TopDown,
		BottomUp
	}

	// BitmapFile only supports indexed palette BMPs. 
	// Writing and reading bitmaps is restricted to the subset of 1, 2 and 8 bit BMPs.
	public class BitmapFile
	{
		public BmpHeader bmpHeader;
		public ImageHeader imageHeader;
		public Color[] palette; // Bitmap files store palette colors in BGR format
		public byte[] pixels;


		public void SetPixel(int x, int y, int paletteIndex)
		{
			int pitch = ImageHeader.CalculatePitch(imageHeader.bitCount, imageHeader.width);
			
			int row = y*pitch;							// The row of the pixel to set
			int bitOffset = x * imageHeader.bitCount;	// The bit offset in the row
			int byteOffset = bitOffset / 8;				// The byte offset in the row
			int index = row + byteOffset;				// The index to set

			// The bit offset relative to the byte offset
			// The pixels are stored with most-significant first
			bitOffset %= 8;
			bitOffset = 8 - (bitOffset + imageHeader.bitCount);

			// A mask for the bits to set for the byte at the "index" in the pixel array
			int mask = ~(~0 << imageHeader.bitCount) << bitOffset;

			// NOTE:
			// We are assuming that the pixel will not fall between bytes (e.g. 2 bits on index 1 and 2 bits on index 2).
			// This is a safe assumption for 1, 2, 4, and 8 bits per pixel.
			pixels[index] &= (byte)~mask; // Clear masked bits
			pixels[index] |= (byte)((paletteIndex << bitOffset) & mask); // Insert masked palette index
		}

		// Returns raw pixel in BGR format
		public Color GetRawPixel(int x, int y)
		{
			return palette[GetPixelPaletteIndex(x,y)];
		}

		//Returns pixel in RGB format
		public Color GetPixel(int x, int y)
		{
			Color color = palette[GetPixelPaletteIndex(x,y)];
			return new Color(color.blue, color.green, color.red, color.alpha);
		}

		public int GetPixelPaletteIndex(int x, int y)
		{
			int pitch = ImageHeader.CalculatePitch(imageHeader.bitCount, imageHeader.width);
			
			int row = y*pitch;							// The row of the pixel to get
			int bitOffset = x * imageHeader.bitCount;	// The bit offset in the row
			int byteOffset = bitOffset / 8;				// The byte offset in the row
			int index = row + byteOffset;				// The index to get

			// The bit offset relative to the byte offset
			// The pixels are stored with most-significant first
			bitOffset %= 8;
			bitOffset = 8 - (bitOffset + imageHeader.bitCount);

			// A mask for the bits to get for the byte at the "index" in the pixel array
			int mask = ~(~0 << imageHeader.bitCount) << bitOffset;

			// NOTE:
			// We are assuming that the pixel will not fall between bytes (e.g. 2 bits on index 1 and 2 bits on index 2).
			// This is a safe assumption for 1, 2, 4, and 8 bits per pixel.
			int paletteIndex = pixels[index] & (byte)mask; // Clear unmasked bits
			return paletteIndex >> bitOffset;
		}

		public static bool PeekIsBitmap(Stream reader)
		{
			byte[] fileSignature = new byte[2];
			int readCount = reader.Read(fileSignature, 0, fileSignature.Length);
			reader.Position -= readCount;

			return fileSignature.SequenceEqual(BmpHeader.FileSignature);
		}


		public BitmapFile() { }

		/// <summary>
		/// Create default indexed bitmap.
		/// </summary>
		public BitmapFile(ushort bitCount, uint width, int height)
		{
			VerifyIndexedImageForSerialization(bitCount);
			
			// Create headers and default palette
			imageHeader = ImageHeader.Create((int)width, (int)height, bitCount);
			palette = new Color[imageHeader.CalcMaxIndexedPaletteSize()];
			pixels = new byte[imageHeader.CalculatePitch() * System.Math.Abs(height)];

			int pixelOffset = BmpHeader.SizeInBytes + ImageHeader.SizeInBytes + palette.Length * Color.SizeInBytes;
			int bitmapFileSize = pixelOffset + pixels.Length * sizeof(byte);

			//if (bitmapFileSize > uint.MaxValue) {
			//	throw new System.Exception("Maximum size of a bitmap file has been exceeded");
			//}

			bmpHeader = BmpHeader.Create((uint)bitmapFileSize, (uint)pixelOffset);
		}

		/// <summary>
		/// Create default indexed bitmap with palette.
		/// </summary>
		public BitmapFile(ushort bitCount, uint width, int height, Color[] palette) : this(bitCount, width, height)
		{
			if (palette.Length > 1 << bitCount)
			{
				throw new System.Exception("Unable to create bitmap. Provided palette length is greater than provided bit count.");
			}

			// Expand palette, if needed
			int maxSize = ImageHeader.CalcMaxIndexedPaletteSize(bitCount);
			if (palette.Length != maxSize)
			{
				Color[] newPalette = new Color[maxSize];
				System.Array.Copy(palette, newPalette, palette.Length < newPalette.Length ? palette.Length : newPalette.Length);

				palette = newPalette;
			}

			// Create headers
			int pixelOffset = BmpHeader.SizeInBytes + ImageHeader.SizeInBytes + palette.Length * Color.SizeInBytes;
			int fileSize = pixelOffset + ImageHeader.CalculatePitch(bitCount, (int)width) * System.Math.Abs(height);

			bmpHeader = BmpHeader.Create((uint)fileSize, (uint)pixelOffset);
			imageHeader = ImageHeader.Create((int)width, height, bitCount);

			// Store data
			this.palette = palette;
		}

		/// <summary>
		/// Create default indexed bitmap with palette and pixels.
		/// </summary>
		public BitmapFile(ushort bitCount, uint width, int height, Color[] palette, byte[] indexedPixels) : this(bitCount, width, height, palette)
		{
			pixels = indexedPixels;
			VerifyPixelSizeMatchesImageDimensionsWithPitch(bitCount, (int)width, height, indexedPixels.Length);
		}

		public static BitmapFile CreateDefaultIndexed(ushort bitCount, uint width, int height)
		{
			return new BitmapFile(bitCount, width, height);
		}

		// BMP Reader only supports Indexed Color palettes (1, 2, and 8 bit BMPs).
		public static BitmapFile ReadIndexed(string filename)
		{
			using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return ReadIndexed(fs);
			}
		}

		public static BitmapFile ReadIndexed(Stream seekableStream)
		{
			using (BinaryReader seekableReader = new BinaryReader(seekableStream, System.Text.Encoding.ASCII, true))
			{
				return ReadIndexed(seekableReader);
			}
		}

		public static BitmapFile ReadIndexed(BinaryReader seekableReader)
		{
			BitmapFile bitmapFile = new BitmapFile();
			bitmapFile.bmpHeader = ReadBmpHeader(seekableReader);
			bitmapFile.imageHeader = ReadImageHeader(seekableReader);

			ReadPalette(seekableReader, bitmapFile);
			ReadPixels(seekableReader, bitmapFile);

			return bitmapFile;
		}

		// BMP Writer only supporting Indexed Color palettes (1, 2, and 8 bit BMPs).
		// @indexedPixels: Must include padding to fill each image row out to the next 4 byte memory border (pitch).
		public void Serialize(string filename)
		{
			using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				Serialize(fs);
			}
		}

		public void Serialize(Stream stream)
		{
			// Test all properties that are auto-generated as correct when writing bitmap piecemeal
			if (imageHeader.compression != BmpCompression.Uncompressed) {
				throw new System.Exception("Unable to write compressed bitmap files");
			}

			Validate();

			using (BinaryWriter fileWriter = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
			{
				WriteIndexed(fileWriter, imageHeader.bitCount, imageHeader.width, imageHeader.height, palette, pixels);
			}
		}

		private void WriteIndexed(BinaryWriter seekableWriter, ushort bitCount, int width, int height, Color[] palette, byte[] indexedPixels)
		{
			VerifyIndexedImageForSerialization(bitCount);
			VerifyIndexedPaletteSizeDoesNotExceedBitCount(bitCount, palette.Length);
			VerifyPixelSizeMatchesImageDimensionsWithPitch(bitCount, width, height, indexedPixels.Length);

			int maxSize = ImageHeader.CalcMaxIndexedPaletteSize(bitCount);
			if (palette.Length != maxSize)
			{
				Color[] newPalette = new Color[maxSize];
				System.Array.Copy(palette, newPalette, palette.Length < newPalette.Length ? palette.Length : newPalette.Length);

				palette = newPalette;
			}

			WriteHeaders(seekableWriter, bitCount, width, height, palette);

			for (int i=0; i < palette.Length; ++i)
				palette[i].Serialize(seekableWriter);

			WritePixels(seekableWriter, indexedPixels, width, height, bitCount);
		}
		
		public void VerifyIndexedPaletteSizeDoesNotExceedBitCount()
		{
			VerifyIndexedPaletteSizeDoesNotExceedBitCount(imageHeader.bitCount, palette.Length);
		}
		public static void VerifyIndexedPaletteSizeDoesNotExceedBitCount(ushort bitCount, int paletteSize)
		{
			if (paletteSize > ImageHeader.CalcMaxIndexedPaletteSize(bitCount)) {
				throw new System.Exception("Too many colors listed on the indexed palette");
			}
		}

		// Check the pixel count is correct and already includes dummy pixels out to next 4 byte boundary.
		// @width: Width in pixels. Do not include the pitch in width.
		// @pixelsWithPitchSize: Number of pixels including padding pixels to next 4 byte boundary.
		public void VerifyPixelSizeMatchesImageDimensionsWithPitch()
		{
			VerifyPixelSizeMatchesImageDimensionsWithPitch(imageHeader.bitCount, imageHeader.width, imageHeader.height, pixels.Length);
		}

		public static void VerifyPixelSizeMatchesImageDimensionsWithPitch(ushort bitCount, int width, int height, int pixelsWithPitchSize)
		{
			if (pixelsWithPitchSize < ImageHeader.CalculatePitch(bitCount, width) * System.Math.Abs(height)) {
				throw new System.Exception("The size of pixels does not match the image's height time pitch");
			}
		}

		public void Validate()
		{
			bmpHeader.VerifyFileSignature();
			imageHeader.Validate();

			VerifyIndexedPaletteSizeDoesNotExceedBitCount();
			VerifyPixelSizeMatchesImageDimensionsWithPitch();
		}

		public ScanLineOrientation GetScanLineOrientation()
		{
			return imageHeader.height < 0 ? ScanLineOrientation.TopDown : ScanLineOrientation.BottomUp;
		}

		public int AbsoluteHeight()
		{
			return System.Math.Abs(imageHeader.height);
		}

		public void SwapRedAndBlue()
		{
			for (int i=0; i < palette.Length; ++i)
			{
				palette[i].SwapRedAndBlue();
			}
		}

		public void InvertScanLines()
		{
			imageHeader.height *= -1;

			byte[] invertedPixels = new byte[pixels.Length];
			
			int pitch = imageHeader.CalculatePitch();
			for (int row = AbsoluteHeight()-1, destIndex=0; row >= 0; --row)
			{
				int srcIndex = row * pitch;
				int copyLength = pitch;
				System.Array.Copy(pixels, srcIndex, invertedPixels, destIndex, copyLength);
				destIndex += copyLength;
			}

			pixels = invertedPixels;
		}

		private static void VerifyIndexedImageForSerialization(ushort bitCount)
		{
			if (!ImageHeader.IsIndexedImage(bitCount)) {
				throw new System.Exception("Unable to read/write a non-indexed bitmap file. Bit count is " + bitCount + " but must be 8 or less");
			}
		}

		// Read
		private static BmpHeader ReadBmpHeader(BinaryReader seekableReader)
		{
			BmpHeader bmpHeader = new BmpHeader(seekableReader);

			bmpHeader.VerifyFileSignature();

			if (bmpHeader.size < seekableReader.BaseStream.Length) {
				throw new System.Exception("Bitmap file size exceed length of stream.");
			}

			return bmpHeader;
		}
		private static ImageHeader ReadImageHeader(BinaryReader seekableReader)
		{
			ImageHeader imageHeader = new ImageHeader(seekableReader);

			imageHeader.Validate();

			VerifyIndexedImageForSerialization(imageHeader.bitCount);

			return imageHeader;
		}

		private static void ReadPalette(BinaryReader seekableReader, BitmapFile bitmapFile)
		{
			if (bitmapFile.imageHeader.usedColorMapEntries != 0) {
				bitmapFile.palette = new Color[bitmapFile.imageHeader.usedColorMapEntries];
			}
			else {
				bitmapFile.palette = new Color[bitmapFile.imageHeader.CalcMaxIndexedPaletteSize()];
			}

			for (int i = 0; i < bitmapFile.palette.Length; ++i)
			{
				bitmapFile.palette[i] = new Color(seekableReader);
			}
		}

		private static void ReadPixels(BinaryReader seekableReader, BitmapFile bitmapFile)
		{
			// Both size and pixelOffset are unsigned values read from the
			// file. If pixelOffset > size the subtraction wraps to a near
			// 4 GB value and the subsequent allocation OOMs. Validate both
			// the relationship and that the resulting block actually fits
			// inside the underlying stream before allocating.
			if (bitmapFile.bmpHeader.pixelOffset > bitmapFile.bmpHeader.size) {
				throw new System.Exception("Bitmap pixelOffset is greater than the declared file size.");
			}

			uint pixelContainerSize = bitmapFile.bmpHeader.size - bitmapFile.bmpHeader.pixelOffset;

			long remainingInStream = seekableReader.BaseStream.Length - seekableReader.BaseStream.Position;
			if (pixelContainerSize > remainingInStream) {
				throw new System.Exception("Bitmap pixel block exceeds the remaining bytes in the stream.");
			}

			VerifyPixelSizeMatchesImageDimensionsWithPitch(bitmapFile.imageHeader.bitCount, bitmapFile.imageHeader.width, bitmapFile.imageHeader.height, (int)pixelContainerSize);

			bitmapFile.pixels = new byte[pixelContainerSize];

			for (int i=0; i < bitmapFile.pixels.Length; ++i)
				bitmapFile.pixels[i] = seekableReader.ReadByte();
		}

		// Write
		private static void WriteHeaders(BinaryWriter seekableWriter, ushort bitCount, int width, int height, Color[] palette)
		{
			int pixelOffset = BmpHeader.SizeInBytes + ImageHeader.SizeInBytes + palette.Length * Color.SizeInBytes;
			int fileSize = pixelOffset + ImageHeader.CalculatePitch(bitCount, width) * System.Math.Abs(height);

			//if (fileSize > uint.MaxValue) {
			//	throw new System.Exception("Bitmap size is too large to save to disk.");
			//}

			BmpHeader bmpHeader = BmpHeader.Create((uint)fileSize, (uint)pixelOffset);
			ImageHeader imageHeader = ImageHeader.Create(width, height, bitCount);

			bmpHeader.Serialize(seekableWriter);
			imageHeader.Serialize(seekableWriter);
		}
		private static void WritePixels(BinaryWriter seekableWriter, byte[] pixels, int width, int height, ushort bitCount)
		{
			height = System.Math.Abs(height);

			int pitch = ImageHeader.CalculatePitch(bitCount, width);
			int bytesOfPixelsPerRow = ImageHeader.CalcPixelByteWidth(bitCount, width);
			byte[] padding = new byte[pitch - bytesOfPixelsPerRow];

			//for (int i = 0; i+bytesOfPixelsPerRow <= pixels.Length; i += pitch) {
			for (int y=0; y < height; ++y)
			{
				seekableWriter.Write(pixels, y*pitch, bytesOfPixelsPerRow);
				seekableWriter.Write(padding);
			}
		}

		public override bool Equals(object obj)
		{
			BitmapFile rhs = obj as BitmapFile;

			return this == rhs;
		}

		public override int GetHashCode()
		{
			return bmpHeader.GetHashCode() + imageHeader.GetHashCode();
		}

		public static bool operator ==(BitmapFile lhs, BitmapFile rhs)
		{
			if (ReferenceEquals(lhs, rhs))
				return true;

			if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null))
				return false;

			if (lhs.palette.Length != rhs.palette.Length)
				return false;

			if (lhs.pixels.Length != rhs.pixels.Length)
				return false;

			for (int i = 0; i < lhs.palette.Length; ++i)
			{
				if (lhs.palette[i] != rhs.palette[i])
					return false;
			}

			for (int i = 0; i < lhs.pixels.Length; ++i)
			{
				if (lhs.pixels[i] != rhs.pixels[i])
					return false;
			}

			return lhs.bmpHeader == rhs.bmpHeader
				&& lhs.imageHeader == rhs.imageHeader;
		}
		public static bool operator !=(BitmapFile lhs, BitmapFile rhs)
		{
			return !(lhs == rhs);
		}

		public BitmapFile Clone()
		{
			BitmapFile bmp = new BitmapFile();
			bmp.bmpHeader = bmpHeader.Clone();
			bmp.imageHeader = imageHeader.Clone();
			bmp.palette = new Color[palette.Length];
			System.Array.Copy(palette, bmp.palette, palette.Length);
			bmp.pixels = new byte[pixels.Length];
			System.Array.Copy(pixels, bmp.pixels, pixels.Length);

			return bmp;
		}
	}
}
