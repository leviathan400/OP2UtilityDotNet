using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace OP2UtilityDotNet.Archive
{
	public class ClmFile : ArchiveFile
	{
		public ClmFile(string filename) : base(filename)
		{
			FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
			try
			{
				clmFileReader = new BinaryReader(fs);
				m_ArchiveFileSize = clmFileReader.BaseStream.Length;
				ReadHeader();
			}
			catch
			{
				// If header parsing throws (malformed archive), dispose the
				// underlying stream so the file handle is not leaked.
				if (clmFileReader != null) {
					clmFileReader.Dispose();
				} else {
					fs.Dispose();
				}
				throw;
			}
		}
		
		// Returns the internal file name of the packed file corresponding to index.
		// Throws an error if packed file index is not valid.
		public override string GetName(int index)
		{
			VerifyIndexInBounds(index);

			return indexEntries[index].GetFilename();
		}

		// Returns the size of the internal file corresponding to index
		public override int GetSize(int index)
		{
			VerifyIndexInBounds(index);

			return indexEntries[index].dataLength;
		}

		// Extracts the internal file corresponding to index
		public override void ExtractFileToStream(int index, Stream stream)
		{
			VerifyIndexInBounds(index);
			IndexEntry indexEntry = indexEntries[index];

			WaveHeader header = WaveHeader.Create(clmHeader.waveFormat, (uint)indexEntry.dataLength);

			try
			{
				using (BinaryWriter waveFileWriter = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
				{
					header.Serialize(waveFileWriter);

					clmFileReader.BaseStream.Seek(indexEntry.dataOffset, SeekOrigin.Begin);

					byte[] slice = clmFileReader.ReadBytes(indexEntry.dataLength);

					waveFileWriter.Write(slice);
				}
			}
			catch (System.Exception e)
			{
				throw new System.Exception("Error attempting to extracted uncompressed file " + index + ". Internal Error Message: " + e);
			}
		}

		// Opens a stream containing packed audio PCM data
		public override Stream OpenStream(int index)
		{
			VerifyIndexInBounds(index);
			IndexEntry indexEntry = indexEntries[index];

			return new Streams.SliceStream(clmFileReader.BaseStream, indexEntry.dataOffset, indexEntry.dataLength);
		}

		// Creates a new Archive file with the file name archiveFilename. The
		// files listed in the container filesToPack are packed into the archive.
		// Automatically strips file name extensions from filesToPack.
		// Returns nonzero if successful and zero otherwise.
		public static void CreateArchive(string archiveFilename, List<string> filesToPack)
		{
			// Sort files alphabetically based on the filename only (not including the full path).
			// Packed files must be locatable by a binary search of their filename.
			filesToPack.Sort(new Comparison<string>((s1, s2) => Path.GetFileName(s1).CompareTo(Path.GetFileName(s2))));

			List<FileStream> filesToPackReaders = new List<FileStream>();

			// Opens all files for packing. If there is a problem opening a file, an exception is raised.
			foreach (string filename in filesToPack)
			{
				filesToPackReaders.Add(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));
			}

			// Initialize vectors with default values for the number of files to pack.
			// Allows directly reading data into the vector using a Reader.
			List<WaveFormatEx> waveFormats = new List<WaveFormatEx>(filesToPack.Count);
			List<IndexEntry> indexEntries = new List<IndexEntry>(filesToPack.Count);

			ReadAllWaveHeaders(filesToPackReaders, waveFormats, indexEntries);

			// Check if all wave formats are the same
			CompareWaveFormats(waveFormats, filesToPack);

			List<string> names = GetNamesFromPaths(filesToPack);
			names = StripFilenameExtensions(names);

			foreach (string name in names)
			{
				if (name.Length > 8)
				{
					throw new System.Exception("Filename " + name + " for packing into archive " + archiveFilename + " must be at most 8 characters in length excluding the extension");
				}
			}

			// Allowing duplicate names when packing may cause unintended results during search and file extraction.
			VerifySortedContainerHasNoDuplicateNames(names);

			// Write the archive header and copy files into the archive
			WriteArchive(archiveFilename, filesToPackReaders, indexEntries, names, PrepareWaveFormat(waveFormats));

			// Dispose of our streams
			foreach (FileStream readers in filesToPackReaders)
			{
				readers.Close();
			}
		}

		// A null terminator (\0) is automatically assigned to the end of the string when placing it within the std::array
		private static readonly ReadOnlyCollection<byte> standardFileVersion = new ReadOnlyCollection<byte>(System.Text.Encoding.ASCII.GetBytes("OP2 Clump File Version 1.0\x01A\0\0\0\0\0"));
		private static readonly ReadOnlyCollection<byte> standardUnknown = new ReadOnlyCollection<byte>(new byte[6] { 0, 0, 0, 0, 1, 0 });
	
		private class ClmHeader
		{
			public const int SizeInBytes = 32 + WaveFormatEx.SizeInBytes + 6 + 4;

			public byte[] fileVersion = new byte[32];
			public WaveFormatEx waveFormat = new WaveFormatEx();
			public byte[] unknown = new byte[6];
			public uint packedFilesCount;


			public ClmHeader(byte[] fileVersion, WaveFormatEx waveFormat, byte[] unknown, uint packedFilesCount)
			{
				this.fileVersion = fileVersion;
				this.waveFormat = waveFormat;
				this.unknown = unknown;
				this.packedFilesCount = packedFilesCount;
			}

			public bool CheckFileVersion()
			{
				if (fileVersion.Length != standardFileVersion.Count)
					return false;

				for (int i=0; i < fileVersion.Length; ++i)
				{
					if (fileVersion[i] != standardFileVersion[i])
						return false;
				}

				return true;
			}

			public bool CheckUnknown()
			{
				if (unknown.Length != standardUnknown.Count)
					return false;

				for (int i=0; i < unknown.Length; ++i)
				{
					if (unknown[i] != standardUnknown[i])
						return false;
				}

				return true;
			}

			// Exception raised if invalid version
			public void VerifyFileVersion()
			{
				if (!CheckFileVersion())
				{
					throw new System.Exception("CLM file version is incorrect.");
				}
			}
			public void VerifyUnknown() // Exception raised if invalid version
			{
				if (!CheckUnknown())
				{
					throw new System.Exception("Unknown field in CLM header is incorrect.");
				}
			}

			public void Serialize(BinaryWriter writer)
			{
				writer.Write(fileVersion);
				waveFormat.Serialize(writer);
				writer.Write(unknown);
				writer.Write(packedFilesCount);
			}

			public ClmHeader(BinaryReader reader)
			{
				fileVersion = reader.ReadBytes(32);
				waveFormat = new WaveFormatEx(reader);
				unknown = reader.ReadBytes(6);
				packedFilesCount = reader.ReadUInt32();
			}

			public static ClmHeader MakeHeader(WaveFormatEx waveFormat, uint packedFilesCount)
			{
				return new ClmHeader(standardFileVersion.ToArray(),  waveFormat, standardUnknown.ToArray(), packedFilesCount);
			}
		};

		//static_assert(42 + sizeof(WaveFormatEx) == sizeof(ClmHeader), "ClmHeader is an unexpected size");

		private class IndexEntry
		{
			public const int SizeInBytes = 8 + 8;

			public byte[] filename = new byte[8];
			public uint dataOffset;
			public int dataLength;

			public string GetFilename()
			{
				// Find the first instance of the null terminator and return only this portion of the filename.
				return System.Text.Encoding.ASCII.GetString(filename).TrimEnd('\0');
			}

			public IndexEntry()
			{
			}

			public void Serialize(BinaryWriter writer)
			{
				writer.Write(filename);
				writer.Write(dataOffset);
				writer.Write(dataLength);
			}

			public IndexEntry(BinaryReader reader)
			{
				filename = reader.ReadBytes(8);
				dataOffset = reader.ReadUInt32();
				dataLength = reader.ReadInt32();
			}
		};

		//static_assert(16 == sizeof(IndexEntry), "ClmFile::IndexEntry is an unexpected size");

		// Reads in the header when the volume is first opened and does some
		// basic error checking on the header.
		// Throws an error is problems encountered while reading the header.
		private void ReadHeader()
		{
			clmHeader = new ClmHeader(clmFileReader);

			try
			{
				clmHeader.VerifyFileVersion();
				clmHeader.VerifyUnknown();
			}
			catch (System.Exception e)
			{
				throw new System.Exception("Invalid clm header read from file " + m_ArchiveFilename + ". " + e);
			}

			m_Count = (int)clmHeader.packedFilesCount;

			indexEntries = new List<IndexEntry>(m_Count);
			for (int i=0; i < m_Count; ++i)
				indexEntries.Add(new IndexEntry(clmFileReader));
		}

		// Private functions for packing files

		// Reads the beginning of each file and verifies it is formatted as a WAVE file. Locates
		// the WaveFormatEx structure and start of data. The WaveFormat is stored in the waveFormats container.
		// The current stream position is set to the start of the data chunk.
		// Note: This function assumes that all stream positions are initially set to the beginning
		//  of the file. When reading the wave file header, it does not seek to the file start.
		private static void ReadAllWaveHeaders(List<FileStream> filesToPackReaders, List<WaveFormatEx> waveFormats, List<IndexEntry> indexEntries)
		{
			RiffHeader header;

			// Read in all the headers and find start of data
			for (int i = 0; i < filesToPackReaders.Count; ++i)
			{
				using (BinaryReader reader = new BinaryReader(filesToPackReaders[i], System.Text.Encoding.ASCII, true))
				{
					// Read the file header
					filesToPackReaders[i].Seek(0, SeekOrigin.Begin);
					header = new RiffHeader(reader);
					if (header.riffTag != CommonTags.tagRIFF || header.waveTag != CommonTags.tagWAVE)
					{
						throw new System.Exception("Error reading header from file " + filesToPackReaders[i].Name);
					}

					// Check that the file size makes sense (matches with header chunk length + 8)
					if (header.chunkSize + 8 != filesToPackReaders[i].Length)
					{
						throw new System.Exception("Chunk size does not match file length in " + filesToPackReaders[i].Name);
					}

					// Find the format tag
					FindChunk(CommonTags.tagFMT_, reader);
					// Read in the wave format
					WaveFormatEx waveFormat = new WaveFormatEx(reader);
					waveFormat.cbSize = 0;
					waveFormats.Add(waveFormat);

					// Find the start of the data and record length
					IndexEntry entry = new IndexEntry();
					entry.dataLength = (int)FindChunk(CommonTags.tagDATA, reader);
					indexEntries.Add(entry);
					// Note: Current stream position is set to the start of the wave data
				}
			}
		}
		
		// Searches through the wave file to find the given chunk length
		// The current stream position is set the the first byte after the chunk header
		// Returns the chunk length if found or -1 otherwise
		private static uint FindChunk(Tag chunkTag, BinaryReader seekableStreamReader)
		{
			long fileSize = seekableStreamReader.BaseStream.Length;

			if (fileSize < RiffHeader.SizeInBytes + ChunkHeader.SizeInBytes)
			{
				throw new System.Exception("There is not enough space in the file to represent the Riff Header and Chunk Header");
			}

			// Seek to beginning of first internal chunk (provided it exists)
			// Note: this seeks past the initial format tag (such as RIFF and WAVE)
			uint currentPosition = RiffHeader.SizeInBytes;
			seekableStreamReader.BaseStream.Seek(currentPosition, SeekOrigin.Begin);

			ChunkHeader header;
			do
			{
				header = new ChunkHeader(seekableStreamReader);

				// Check if this is the right header
				if (header.formatTag == chunkTag)
				{
					return header.length;
				}

				// If not the right header, skip to next header
				currentPosition += header.length + ChunkHeader.SizeInBytes;
				seekableStreamReader.BaseStream.Seek(currentPosition, SeekOrigin.Begin);
			} while (currentPosition < fileSize);

			throw new System.Exception("Unable to find the tag " + chunkTag);
		}

		// Compares wave format structures in the waveFormats container
		// If 2 wave formats are discovered of different type, an error is thrown.
		private static void CompareWaveFormats(List<WaveFormatEx> waveFormats, List<string> filesToPack)
		{
			for (int i = 0; i < waveFormats.Count; ++i)
			{
				if (!waveFormats[0].Equals(waveFormats[i]))
				{
					throw new System.Exception("Files " + filesToPack[0] + " and " + filesToPack[i] +
						" contain differnt wave formats. Clm files cannot contain 2 wave files with different formats.");
				}
			}
		}

		private static void WriteArchive(string archiveFilename,
			List<FileStream> filesToPackReaders,
			List<IndexEntry> indexEntries,
			List<string> names,
			WaveFormatEx waveFormat)
		{
			// ClmFile cannot contain more than 32 bit size internal file count.
			ClmHeader header = ClmHeader.MakeHeader(waveFormat, (uint)names.Count);

			using (FileStream fs = new FileStream(archiveFilename, FileMode.Create, FileAccess.Write, FileShare.None))
			using (BinaryWriter clmFileWriter = new BinaryWriter(fs))
			{
				header.Serialize(clmFileWriter);

				// Prepare and write Archive Index
				PrepareIndex(ClmHeader.SizeInBytes, names, indexEntries);
				for (int i = 0; i < indexEntries.Count; ++i)
				{
					indexEntries[i].Serialize(clmFileWriter);
				}

				// Copy files into the archive
				for (int i = 0; i < header.packedFilesCount; ++i)
				{
					using (BinaryReader reader = new BinaryReader(filesToPackReaders[i], System.Text.Encoding.ASCII, true))
					{
						reader.BaseStream.Seek(0, SeekOrigin.Begin);
						clmFileWriter.Write(reader.ReadBytes((int)reader.BaseStream.Length));
					}
				}
			}
		}

		private static void PrepareIndex(int headerSize, List<string> names, List<IndexEntry> indexEntries)
		{
			ulong offset = (ulong)headerSize + (ulong)names.Count * IndexEntry.SizeInBytes;
			for (int i = 0; i < names.Count; ++i)
			{
				// Copy the filename into the entry
				byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(names[i]);
				
				indexEntries[i].filename = new byte[indexEntries[i].filename.Length];
				System.Array.Copy(nameBytes, indexEntries[i].filename, nameBytes.Length < indexEntries[i].filename.Length ? nameBytes.Length : indexEntries[i].filename.Length);
				
				if (offset + (ulong)indexEntries[i].dataLength > uint.MaxValue)
				{
					throw new System.Exception("Index Entry offset is too large to create CLM file");
				}

				// Set the offset of the file
				indexEntries[i].dataOffset = (uint)offset;
				offset += (ulong)indexEntries[i].dataLength;
			}
		}
		private static List<string> StripFilenameExtensions(List<string> paths)
		{
			List<string> strippedExtensions = new List<string>();

			foreach (string path in paths)
			{
				strippedExtensions.Add(Path.GetFileNameWithoutExtension(path));
			}

			return strippedExtensions;
		}

		private static WaveFormatEx PrepareWaveFormat(List<WaveFormatEx> waveFormats)
		{
			if (waveFormats.Count == 0)
			{
				return new WaveFormatEx
				(
					1, // WAVE_FORMAT_PCM
					1, // mono
					22050, // 22.05KHz
					44100, // nSamplesPerSec * nBlockAlign
					2, // 2 bytes/sample = nChannels * wBitsPerSample / 8
					16,
					0
				);
			}

			return waveFormats[0];
		}

		public override void Dispose()
		{
			clmFileReader.Dispose();
		}

		private BinaryReader clmFileReader;
		private ClmHeader clmHeader;
		private List<IndexEntry> indexEntries;
	};
}
