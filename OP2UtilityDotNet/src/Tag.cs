using System;
using System.IO;

namespace OP2UtilityDotNet
{
	public class Tag
	{
		public const int SizeInBytes = 4;

		private byte[] text = new byte[4];


		public byte[] GetBytes()
		{
			byte[] result = new byte[4];
			Array.Copy(text, result, result.Length);
			return result;
		}

		// Allow default construction
		public Tag()
		{
		}

		// Allow construction from string literals
		public Tag(string text)
		{
			for (int i=0; i < text.Length && i < this.text.Length; ++i)
				this.text[i] = (byte)text[i];
		}

		// Allow construction from other Tag objects
		public Tag(Tag tag)
		{
			Array.Copy(tag.text, text, tag.text.Length);
		}

		public Tag(byte[] bytes)
		{
			int length = bytes.Length < text.Length ? bytes.Length : text.Length;
			Array.Copy(bytes, text, length);
		}

		public void Serialize(BinaryWriter writer)
		{
			Serialize(writer.BaseStream);
		}

		public void Serialize(Stream writer)
		{
			writer.Write(text, 0, text.Length);
		}

		public Tag(BinaryReader reader) : this(reader.BaseStream)
		{
		}

		public Tag(Stream reader)
		{
			Array.Clear(text, 0, text.Length);
			reader.Read(text, 0, text.Length);
		}

		// Equality and inequality comparable
		public override bool Equals(object obj)
		{
			Tag rhs = obj as Tag;

			return this == rhs;
		}

		public override int GetHashCode()
		{
			// Hash the byte content, not the array reference, so equal Tags
			// hash equally (required by the Equals/GetHashCode contract).
			return (text[0] << 24) | (text[1] << 16) | (text[2] << 8) | text[3];
		}

		public static bool operator ==(Tag lhs, Tag rhs)
		{
			if (ReferenceEquals(lhs, rhs))
				return true;

			if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null))
				return false;

			if (lhs.text.Length != rhs.text.Length)
				return false;

			for (int i=0; i < lhs.text.Length; ++i)
			{
				if (lhs.text[i] != rhs.text[i])
					return false;
			}

			return true;
		}

		public static bool operator !=(Tag lhs, Tag rhs)
		{
			return !(lhs == rhs);
		}

		public override string ToString()
		{
			return System.Text.Encoding.ASCII.GetString(text);
		}
	}
}
