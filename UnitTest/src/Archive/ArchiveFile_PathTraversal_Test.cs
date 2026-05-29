using Microsoft.VisualStudio.TestTools.UnitTesting;
using OP2UtilityDotNet.Archive;
using System;
using System.IO;
using System.Text;

namespace UnitTest.src.Archive
{
	/// <summary>
	/// Regression tests for the path-traversal hardening in
	/// <see cref="ArchiveFile.ExtractAllFiles(string)"/>.
	///
	/// A malicious or malformed .vol/.clm archive could previously contain an
	/// entry whose name included directory separators, a rooted path, or
	/// <c>..</c> traversal segments. <c>ExtractAllFiles</c> combined that name
	/// with the user-supplied destination directory via <c>Path.Combine</c>,
	/// which does no sanitization, so the file would be written outside the
	/// destination directory.
	/// </summary>
	[TestClass]
	public class ArchiveFile_PathTraversal_Test
	{
		private const string ExtractDirectory = "./PathTraversalTestExtract";

		[TestInitialize]
		public void Setup()
		{
			if (Directory.Exists(ExtractDirectory)) {
				Directory.Delete(ExtractDirectory, true);
			}
			Directory.CreateDirectory(ExtractDirectory);
		}

		[TestCleanup]
		public void Cleanup()
		{
			if (Directory.Exists(ExtractDirectory)) {
				Directory.Delete(ExtractDirectory, true);
			}
		}

		[TestMethod]
		public void ExtractAllFiles_RejectsParentDirectoryTraversal()
		{
			using (MockArchiveFile archive = new MockArchiveFile(@"..\evil.txt")) {
				Assert.ThrowsException<Exception>(() => archive.ExtractAllFiles(ExtractDirectory));
			}

			// Nothing should have been written anywhere.
			Assert.IsFalse(File.Exists("evil.txt"));
			Assert.IsFalse(File.Exists(@"..\evil.txt"));
		}

		[TestMethod]
		public void ExtractAllFiles_RejectsSubdirectoryComponent()
		{
			using (MockArchiveFile archive = new MockArchiveFile(@"sub\file.txt")) {
				Assert.ThrowsException<Exception>(() => archive.ExtractAllFiles(ExtractDirectory));
			}

			Assert.IsFalse(Directory.Exists(Path.Combine(ExtractDirectory, "sub")));
		}

		[TestMethod]
		public void ExtractAllFiles_RejectsForwardSlashSubdirectory()
		{
			// Forward slash is a valid path separator on Windows too.
			using (MockArchiveFile archive = new MockArchiveFile("sub/file.txt")) {
				Assert.ThrowsException<Exception>(() => archive.ExtractAllFiles(ExtractDirectory));
			}

			Assert.IsFalse(Directory.Exists(Path.Combine(ExtractDirectory, "sub")));
		}

		[TestMethod]
		public void ExtractAllFiles_RejectsRootedPath()
		{
			// An absolute path would, under the old implementation, be returned
			// as-is from Path.Combine and written to an attacker-chosen
			// location.
			string maliciousPath = Path.Combine(Path.GetTempPath(), "op2_extract_evil.txt");
			if (File.Exists(maliciousPath)) {
				File.Delete(maliciousPath);
			}

			using (MockArchiveFile archive = new MockArchiveFile(maliciousPath)) {
				Assert.ThrowsException<Exception>(() => archive.ExtractAllFiles(ExtractDirectory));
			}

			Assert.IsFalse(File.Exists(maliciousPath));
		}

		[TestMethod]
		public void ExtractAllFiles_AcceptsPlainFilename()
		{
			using (MockArchiveFile archive = new MockArchiveFile("safe.txt")) {
				archive.ExtractAllFiles(ExtractDirectory);
			}

			string expectedPath = Path.Combine(ExtractDirectory, "safe.txt");
			Assert.IsTrue(File.Exists(expectedPath));
			Assert.AreEqual("payload", File.ReadAllText(expectedPath));
		}

		/// <summary>
		/// Minimal <see cref="ArchiveFile"/> subclass that holds a single
		/// entry with a configurable name. Lets us exercise the sanitization
		/// path in <c>ExtractAllFiles</c> without having to craft a real
		/// malformed .vol/.clm on disk.
		/// </summary>
		private sealed class MockArchiveFile : ArchiveFile
		{
			private readonly string _entryName;
			private readonly byte[] _entryData = Encoding.ASCII.GetBytes("payload");

			public MockArchiveFile(string entryName) : base("MockArchive")
			{
				_entryName = entryName;
				m_Count = 1;
			}

			public override string GetName(int index) { VerifyIndexInBounds(index); return _entryName; }
			public override int GetSize(int index) { VerifyIndexInBounds(index); return _entryData.Length; }

			public override void ExtractFileToStream(int index, System.IO.Stream stream)
			{
				VerifyIndexInBounds(index);
				stream.Write(_entryData, 0, _entryData.Length);
			}

			public override System.IO.Stream OpenStream(int index)
			{
				VerifyIndexInBounds(index);
				return new MemoryStream(_entryData, false);
			}

			public override void Dispose() { }
		}
	}
}
