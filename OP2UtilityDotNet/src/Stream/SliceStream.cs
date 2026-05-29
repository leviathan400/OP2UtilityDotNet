using System.IO;

namespace OP2UtilityDotNet.Streams
{
	public class SliceStream : Stream
	{
		private Stream _BaseStream;
		private long _DataOffset;
		private long _DataLength;
		private long _Position;


		public SliceStream(Stream baseStream, long dataLength) : this(baseStream, baseStream.Position, dataLength)
		{
		}

		public SliceStream(Stream baseStream, long dataOffset, long dataLength)
		{
			// Allow dataOffset == baseStream.Length so a zero-length slice at
			// the end of the stream is legal (e.g. an empty entry stored at the
			// very end of an archive).
			if (dataOffset < 0 || dataOffset > baseStream.Length)
				throw new System.ArgumentOutOfRangeException(nameof(dataOffset));
			// dataLength is also bounded by baseStream.Length to prevent the
			// dataOffset + dataLength addition below from overflowing (e.g.
			// dataLength == long.MaxValue with dataOffset == 1).
			if (dataLength < 0 || dataLength > baseStream.Length)
				throw new System.ArgumentOutOfRangeException(nameof(dataLength));
			if (dataOffset + dataLength > baseStream.Length)
				throw new System.ArgumentOutOfRangeException(nameof(dataOffset) + " + " + nameof(dataLength), "Slice extends past end of stream.");

			_BaseStream = baseStream;
			_DataOffset = dataOffset;
			_DataLength = dataLength;
		}

		public override bool CanRead => _BaseStream.CanRead;
		public override bool CanSeek => _BaseStream.CanSeek;
		public override bool CanWrite => _BaseStream.CanWrite;

		public override long Length => _DataLength;

		public override long Position { get => _Position; set => Seek(value, SeekOrigin.Begin); }

		public override void Flush()
		{
			_BaseStream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			// Stream.Read contract: read up to `count` bytes; returning 0 means
			// end-of-stream. Clamp `count` to whatever the slice has left
			// instead of bailing with 0, otherwise callers reading in chunks
			// larger than the remaining slice see a spurious EOF.
			if (_Position >= _DataLength)
			{
				return 0;
			}
			if (_Position + count > _DataLength)
			{
				count = (int)(_DataLength - _Position);
			}

			// Set base stream position
			long basePosition = _BaseStream.Position;
			_BaseStream.Position = _DataOffset + _Position;

			int bytesRead;

			try
			{
				// Read
				bytesRead = _BaseStream.Read(buffer, offset, count);
				_Position += bytesRead;
			}
			finally
			{
				// Restore base stream position
				_BaseStream.Position = basePosition;
			}

			return bytesRead;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin:
					_Position = offset;
					break;

				case SeekOrigin.Current:
					_Position = _Position + offset;
					break;

				case SeekOrigin.End:
					// Standard Stream contract: SeekOrigin.End computes
					// (Length + offset). Callers pass a non-positive offset to
					// land inside the slice; offset == 0 lands at EOF.
					_Position = _DataLength + offset;
					break;
			}

			if (_Position < 0)
				_Position = 0;

			// EOF is a legal Position (one past the last byte). Clamp to
			// _DataLength, not _DataLength - 1, otherwise the final byte is
			// unreachable and zero-length slices cannot be seeked at all.
			if (_Position > _DataLength)
				_Position = _DataLength;

			return _Position;
		}

		public override void SetLength(long value)
		{
			long newLength = value;

			if (_DataOffset + newLength > _BaseStream.Length)
			{
				newLength = _BaseStream.Length - _DataOffset;
			}

			_DataLength = newLength;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (_Position + count > _DataLength)
			{
				throw new EndOfStreamException();
			}

			// Set base stream position
			long basePosition = _BaseStream.Position;
			_BaseStream.Position = _DataOffset + _Position;

			try
			{
				// Write
				_BaseStream.Write(buffer, offset, count);
				_Position += count;
			}
			finally
			{
				// Restore base stream position
				_BaseStream.Position = basePosition;
			}
		}
	}
}
