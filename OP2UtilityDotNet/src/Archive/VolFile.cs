using System.Collections.Generic;
using System.IO;

namespace OP2UtilityDotNet.Archive
{
	public class VolFile : ArchiveFile
	{
		// Volume section header tags
		private static readonly Tag TagVOL_ = new Tag("VOL "); // Volume file tag
		private static readonly Tag TagVOLH = new Tag("volh"); // Header tag
		private static readonly Tag TagVOLS = new Tag("vols"); // Filename table tag
		private static readonly Tag TagVOLI = new Tag("voli"); // Index table tag
		private static readonly Tag TagVBLK = new Tag("VBLK"); // Packed file tag

		public VolFile(string filename) : base(filename)
		{
			FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
			try
			{
				archiveFileReader = new BinaryReader(fs);
				m_ArchiveFileSize = archiveFileReader.BaseStream.Length;

				ReadVolHeader();
			}
			catch
			{
				// If header parsing throws (malformed archive), dispose the
				// underlying stream so the file handle is not leaked.
				if (archiveFileReader != null) {
					archiveFileReader.Dispose();
				} else {
					fs.Dispose();
				}
				throw;
			}
		}

		// Internal file status
		//std::size_t GetIndex(const std::string& name) override;
		public override string GetName(int index)
		{
			VerifyIndexInBounds(index);

			return m_StringTable[index];
		}

		public CompressionType GetCompressionCode(int index)
		{
			VerifyIndexInBounds(index);

			return m_IndexEntries[index].compressionType;
		}

		public override int GetSize(int index)
		{
			VerifyIndexInBounds(index);

			return m_IndexEntries[index].fileSize;
		}

		// Extraction
		public override void ExtractFileToStream(int index, Stream stream)
		{
			VerifyIndexInBounds(index);
			IndexEntry indexEntry = m_IndexEntries[index];

			if (indexEntry.compressionType == CompressionType.Uncompressed)
			{
				ExtractFileUncompressed(index, stream);
			}
			else if (indexEntry.compressionType == CompressionType.LZH)
			{
				ExtractFileLzh(index, stream);
			}
			else
			{
				throw new System.Exception("Compression type is not supported.");
			}
		}

		// Opens a stream containing a packed file
		public override Stream OpenStream(int index)
		{
			SectionHeader sectionHeader = GetSectionHeader(index);

			return new Streams.SliceStream(archiveFileReader.BaseStream, archiveFileReader.BaseStream.Position, sectionHeader.length);
		}

		// Create a new archive with the files specified in filesToPack
		public static void CreateArchive(string volumeFilename, IEnumerable<string> filesToPack)
		{
			List<string> filesToPackSorted = new List<string>(filesToPack);

			// Sort files alphabetically based on the filename only (not including the full path).
			// Packed files must be locatable by a binary search of their filename.
			filesToPackSorted.Sort(new System.Comparison<string>((s1, s2) => Path.GetFileName(s1).CompareTo(Path.GetFileName(s2))));

			CreateVolumeInfo volInfo = new CreateVolumeInfo();

			volInfo.filesToPack = filesToPackSorted;
			volInfo.names = GetNamesFromPaths(filesToPackSorted);

			// Allowing duplicate names when packing may cause unintended results during binary search and file extraction.
			VerifySortedContainerHasNoDuplicateNames(volInfo.names);

			// Open input files and prepare header and indexing info
			PrepareHeader(volInfo, volumeFilename);

			WriteVolume(volumeFilename, volInfo);

			// Dispose of open readers
			foreach (BinaryReader reader in volInfo.fileStreamReaders)
			{
				reader.Dispose();
			}
		}

		private int GetFileOffset(int index)
		{
			return (int)(m_IndexEntries[index].dataBlockOffset + SectionHeader.SizeInBytes);
		}

		private int GetFilenameOffset(int index)
		{
			return (int)m_IndexEntries[index].filenameOffset;
		}

		private void ExtractFileUncompressed(int index, Stream destination)
		{
			try
			{
				// Calling GetSectionHeader moves the streamReader's position to just past the SectionHeader
				SectionHeader sectionHeader = GetSectionHeader(index);
				byte[] buffer = archiveFileReader.ReadBytes((int)sectionHeader.length);
				destination.Write(buffer, 0, buffer.Length);
			}
			catch (System.Exception e)
			{
				throw new System.Exception("Error attempting to extracted uncompressed file " + index + ". Internal Error Message: " + e);
			}
		}

		private void ExtractFileLzh(int index, Stream destination)
		{
			try
			{
				// Calling GetSectionHeader moves the streamReader's position to just past the SectionHeader
				SectionHeader sectionHeader = GetSectionHeader(index);

				// Load data into temporary memory buffer
				uint length = sectionHeader.length;
				byte[] buffer = new byte[length];
				archiveFileReader.Read(buffer, 0, buffer.Length);

				HuffLZ decompressor = new HuffLZ(new BitStreamReader(buffer, length));

				int len;

				do
				{
					MemoryStream decompressedBuffer = decompressor.GetInternalBuffer(out len);
					destination.Write(decompressedBuffer.ToArray(), 0, len);
				} while (len > 0);
			}
			catch (System.Exception e)
			{
				throw new System.Exception("Error attempting to extract LZH compressed file " + index + ". Internal Error Message: " + e);
			}
		}

		private class IndexEntry
		{
			public const int SizeInBytes = 14;

			public uint filenameOffset;
			public uint dataBlockOffset;
			public int fileSize;
			public CompressionType compressionType;


			public IndexEntry() { }

			public void Serialize(BinaryWriter writer)
			{
				writer.Write(filenameOffset);
				writer.Write(dataBlockOffset);
				writer.Write(fileSize);
				writer.Write((ushort)compressionType);
			}

			public IndexEntry(BinaryReader reader)
			{
				filenameOffset = reader.ReadUInt32();
				dataBlockOffset = reader.ReadUInt32();
				fileSize = reader.ReadInt32();
				compressionType = (CompressionType)reader.ReadUInt16();
			}
		}

		//static_assert(14 == sizeof(IndexEntry), "VolFile::IndexEntry is an unexpected size");

		// Specify boundary padding for a volume file section
		private enum VolPadding
		{
			TwoByte = 0,
			FourByte = 1
		}

		private class SectionHeader
		{
			public const int SizeInBytes = Tag.SizeInBytes + 4;

			public SectionHeader()
			{
			}
			public SectionHeader(Tag tag, uint length, VolPadding padding = VolPadding.FourByte)
			{
				this.tag = tag;
				this.length = length;
				this.padding = padding;
			}

			public Tag tag;
			private uint _length;

			public uint length
			{
				get
				{
					return _length & (~0x80000000);
				}
				set
				{
					_length &= 0x80000000;
					_length |= value & (~0x80000000);
				}
			}
			public VolPadding padding
			{
				get
				{
					return (VolPadding)(_length >> 31);
				}
				set
				{
					_length &= ~0x80000000;
					_length |= (uint)value << 31;
				}
			}


			public void Serialize(BinaryWriter writer)
			{
				tag.Serialize(writer);
				writer.Write(_length);
			}

			public SectionHeader(BinaryReader reader)
			{
				tag = new Tag(reader);
				_length = reader.ReadUInt32();
			}
		};

		//static_assert(8 == sizeof(SectionHeader), "VolFile::SectionHeader is an unexpected size");

		private class CreateVolumeInfo
		{
			public List<IndexEntry> indexEntries = new List<IndexEntry>();
			public List<BinaryReader> fileStreamReaders = new List<BinaryReader>();
			public List<string> filesToPack = new List<string>();
			public List<string> names = new List<string>();
			public uint stringTableLength;
			public uint indexTableLength;
			public uint paddedStringTableLength;
			public uint paddedIndexTableLength;

			public int fileCount()
			{
				return filesToPack.Count;
			}
		}

		// Reads a tag in the .vol file and returns the length of that section.
		// If tag does not match what is in the file or if the length is invalid then an error is thrown.
		private uint ReadTag(Tag tagName)
		{
			SectionHeader tag = new SectionHeader(archiveFileReader);

			if (tag.tag != tagName) {
				throw new System.Exception("The tag " + tagName +
					" was not found in the proper position in volume " + m_ArchiveFilename);
			}

			if (tag.padding == VolPadding.TwoByte) {
				throw new System.Exception("The tag " + tagName +
					" from volume " + m_ArchiveFilename +
					" uses 2 byte padding, which is not supported. Only 4 byte padding is supported.");
			}

			return tag.length;
		}

		// Reads the header structure of the .vol file and sets up indexing/structure variables
		// Returns true is the header structure is valid and false otherwise
		private void ReadVolHeader()
		{
			// Make sure file is big enough to contain header tag
			if (archiveFileReader.BaseStream.Length < SectionHeader.SizeInBytes) {
				throw new System.Exception("The volume file " + m_ArchiveFilename + " is not large enough to contain the 'VOL ' section header");
			}

			m_HeaderLength = ReadTag(TagVOL_);

			// Make sure the file is large enough to contain the header
			if (archiveFileReader.BaseStream.Length < m_HeaderLength + SectionHeader.SizeInBytes) {
				throw new System.Exception("The volume file " + m_ArchiveFilename + " is not large enough to contain the volh section header");
			}

			uint volhSize = ReadTag(TagVOLH);
			if (volhSize != 0) {
				throw new System.Exception("The length associated with tag volh is not zero in volume " + m_ArchiveFilename);
			}

			m_StringTableLength = ReadTag(TagVOLS);

			if (m_HeaderLength < m_StringTableLength + SectionHeader.SizeInBytes * 2 + sizeof(uint)) {
				throw new System.Exception("The string table does not fit in the header of volume " + m_ArchiveFilename);
			}

			ReadStringTable();

			m_IndexTableLength = ReadTag(TagVOLI);
			m_IndexEntryCount = m_IndexTableLength / IndexEntry.SizeInBytes;

			if (m_IndexTableLength > 0) {
				m_IndexEntries = new List<IndexEntry>((int)m_IndexEntryCount);

				for (int i = 0; i < m_IndexEntryCount; ++i)
				{
					m_IndexEntries.Add(new IndexEntry(archiveFileReader));
				}
			}

			if (m_HeaderLength < m_StringTableLength + m_IndexTableLength + 24) {
				throw new System.Exception("The index table does not fit in the header of volume " + m_ArchiveFilename);
			}

			CountValidEntries();
		}

		private void ReadStringTable()
		{
			uint actualStringTableLength = archiveFileReader.ReadUInt32();

			// Validate the reported inner length fits within the outer
			// string-table length (header + 4 bytes for the length field
			// itself). Without this check the unsigned subtraction below
			// can wrap on a malformed file and produce a multi-GB seek.
			if ((ulong)actualStringTableLength + 4UL > m_StringTableLength) {
				throw new System.Exception("The string table inner length exceeds the declared string table length in volume " + m_ArchiveFilename);
			}

			byte[] charBuffer = archiveFileReader.ReadBytes((int)actualStringTableLength);

			m_StringTable.Add("");
			for (int i = 0; i < charBuffer.Length; ++i)
			{
				if (charBuffer[i] == '\0') {
					m_StringTable.Add("");
					continue;
				}

				m_StringTable[m_StringTable.Count - 1] += System.Convert.ToChar(charBuffer[i]);
			}

			m_StringTable.RemoveAt(m_StringTable.Count-1);

			// Seek to the end of padding at end of StringTable
			archiveFileReader.BaseStream.Seek(m_StringTableLength - actualStringTableLength - 4, SeekOrigin.Current);
		}

		private void CountValidEntries()
		{
			// Count the number of valid entries
			int packedFileCount = 0;
			for (; packedFileCount < m_IndexEntryCount; ++packedFileCount)
			{
				// Make sure entry is valid
				if (m_IndexEntries[packedFileCount].filenameOffset == uint.MaxValue) {
					break;
				}
			}
			m_Count = packedFileCount;
		}
		private SectionHeader GetSectionHeader(int index)
		{
			VerifyIndexInBounds(index);

			archiveFileReader.BaseStream.Seek(m_IndexEntries[index].dataBlockOffset, SeekOrigin.Begin);

			SectionHeader sectionHeader = new SectionHeader(archiveFileReader);

			//Volume Block
			if (sectionHeader.tag != TagVBLK) {
				throw new System.Exception("Archive file " + m_ArchiveFilename +
					" is missing VBLK tag for requested file at index " + index);
			}

			return sectionHeader;
		}

		private static void WriteVolume(string filename, CreateVolumeInfo volInfo)
		{
			foreach (string path in volInfo.filesToPack) {
				if (string.Compare(filename, path, true) == 0) // NOTE: Case insensitive comparison
				{
					throw new System.Exception("Cannot include a volume being overwritten in new volume " + filename);
				}
			}

			using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
			using (BinaryWriter volWriter = new BinaryWriter(fs))
			{
				WriteHeader(volWriter, volInfo);
				WriteFiles(volWriter, volInfo);
			}
		}

		private static void WriteFiles(BinaryWriter volWriter, CreateVolumeInfo volInfo)
		{
			// Write each file header and contents
			for (int i = 0; i < volInfo.fileCount(); ++i)
			{
				new SectionHeader(TagVBLK, (uint)volInfo.indexEntries[i].fileSize).Serialize(volWriter);

				try {
					volInfo.fileStreamReaders[i].BaseStream.Seek(0, SeekOrigin.Begin);
					volWriter.Write(volInfo.fileStreamReaders[i].ReadBytes((int)volInfo.fileStreamReaders[i].BaseStream.Length));
					
					// Add padding after the file, ensuring it ends on a 4 byte boundary
					// Use a bitmask to quickly calculate the modulo 4 (remainder) of fileSize
					byte[] padding = new byte[4];
					volWriter.Write(padding, 0, (-volInfo.indexEntries[i].fileSize) & 3);
				}
				catch (System.Exception e) {
					throw new System.Exception("Unable to pack file " + volInfo.names[i] + ". Internal error: " + e);
				}
			}
		}

		private static void WriteHeader(BinaryWriter volWriter, CreateVolumeInfo volInfo)
		{
			// Write the header
			new SectionHeader(TagVOL_, volInfo.paddedStringTableLength + volInfo.paddedIndexTableLength + 24).Serialize(volWriter);

			new SectionHeader(TagVOLH, 0).Serialize(volWriter);

			// Write the string table
			new SectionHeader(TagVOLS, volInfo.paddedStringTableLength).Serialize(volWriter);

			volWriter.Write(volInfo.stringTableLength);

			// Write out all internal file name strings (including NULL terminator)
			for (int i = 0; i < volInfo.fileCount(); ++i) {
				// Account for the null terminator in the size.
				byte[] stringBuffer = System.Text.Encoding.ASCII.GetBytes(volInfo.names[i] + "\0");
				volWriter.Write(stringBuffer, 0, stringBuffer.Length);
			}

			byte[] padding = new byte[4]; // Pad with 0 bytes
			volWriter.Write(padding, 0, (int)(volInfo.paddedStringTableLength - (volInfo.stringTableLength + 4)));

			// Write the index table
			new SectionHeader(TagVOLI, volInfo.indexTableLength).Serialize(volWriter);

			int bytesWritten = 0;
			foreach (IndexEntry entry in volInfo.indexEntries)
			{
				if (bytesWritten >= volInfo.indexTableLength)
					break;

				entry.Serialize(volWriter);
				bytesWritten += IndexEntry.SizeInBytes;
			}

			volWriter.Write(padding, 0, (int)(volInfo.paddedIndexTableLength - volInfo.indexTableLength));
		}

		private static void PrepareHeader(CreateVolumeInfo volInfo, string volumeFilename)
		{
			OpenAllInputFiles(volInfo, volumeFilename);

			volInfo.stringTableLength = 0;

			// Get file sizes and calculate length of string table
			for (int i = 0; i < volInfo.fileCount(); ++i)
			{
				IndexEntry indexEntry = new IndexEntry();

				long fileSize = volInfo.fileStreamReaders[i].BaseStream.Length;
				// IndexEntry.fileSize is a 32-bit signed value (matching the
				// public int GetSize API). Capping at int.MaxValue rather
				// than uint.MaxValue prevents files between 2 GB and 4 GB
				// from round-tripping as negative sizes.
				if (fileSize > int.MaxValue) {
					throw new System.Exception("File " + volInfo.filesToPack[i] +
						" is too large to fit inside a volume archive. Writing volume " + volumeFilename + " aborted.");
				}

				indexEntry.fileSize = (int)(fileSize);
				indexEntry.filenameOffset = volInfo.stringTableLength;
				indexEntry.compressionType = CompressionType.Uncompressed;

				volInfo.indexEntries.Add(indexEntry);

				// Add length of internal filename plus null terminator to string table length.
				if (volInfo.stringTableLength + (ulong)volInfo.names[i].Length + 1 > uint.MaxValue) {
					throw new System.Exception("String table length is too long to create volume " + volumeFilename);
				}

				volInfo.stringTableLength += (uint)(volInfo.names[i].Length) + 1;
			}

			// Calculate size of index table
			if ((ulong)volInfo.fileCount() * IndexEntry.SizeInBytes > uint.MaxValue) {
				throw new System.Exception("Index table length is too long to create volume " + volumeFilename);
			}
			volInfo.indexTableLength = (uint)volInfo.fileCount() * IndexEntry.SizeInBytes;

			// Calculate the zero padded length of the string table and index table
			volInfo.paddedStringTableLength = (uint)((volInfo.stringTableLength + 7) & ~3);
			volInfo.paddedIndexTableLength = (uint)((volInfo.indexTableLength + 3) & ~3);

			if (volInfo.indexEntries.Count == 0) {
				return;
			}

			volInfo.indexEntries[0].dataBlockOffset = volInfo.paddedStringTableLength + volInfo.paddedIndexTableLength + 32;

			// Calculate offsets to the files
			for (int i = 1; i < volInfo.fileCount(); ++i)
			{
				IndexEntry previousIndex = volInfo.indexEntries[i - 1];
				volInfo.indexEntries[i].dataBlockOffset = (uint)((previousIndex.dataBlockOffset + previousIndex.fileSize + 11) & ~3);
			}
		}

		private static void OpenAllInputFiles(CreateVolumeInfo volInfo, string volumeFilename)
		{
			volInfo.fileStreamReaders.Clear();

			foreach (string filename in volInfo.filesToPack) {
				try {
					FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
					volInfo.fileStreamReaders.Add(new BinaryReader(fs));
				}
				catch (System.Exception e) {
					throw new System.Exception("Error attempting to open " + filename +
						" for reading into volume " + volumeFilename + ". Internal Error: " + e);
				}
			}
		}

		public override void Dispose()
		{
			archiveFileReader.Dispose();
		}

		private BinaryReader archiveFileReader;
		private uint m_IndexEntryCount;
		private List<string> m_StringTable = new List<string>();
		private uint m_HeaderLength;
		private uint m_StringTableLength;
		private uint m_IndexTableLength;
		private List<IndexEntry> m_IndexEntries = new List<IndexEntry>();
	}
}
