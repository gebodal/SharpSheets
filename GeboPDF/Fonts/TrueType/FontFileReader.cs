using GeboPdf.Utilities;
using System;
using System.IO;
using System.Text;

namespace GeboPdf.Fonts.TrueType {

	public class FontFileReader {

		//private readonly Stream stream;
		private readonly BinaryReader reader;

		public Stream BaseStream => reader.BaseStream;

		public long Position {
			get { return reader.BaseStream.Position; }
			set { reader.BaseStream.Position = value; }
		}

		public long Length {
			get { return reader.BaseStream.Length; }
		}

		public FontFileReader(Stream stream) {
			//this.stream = stream;
			if(!stream.CanRead || !stream.CanSeek) {
				throw new ArgumentException("Input stream for FontFileReader must be readable and seekable.");
			}
			this.reader = new BinaryReader(stream);
		}

		/*** ASCII Strings ***/
		public string ReadASCIIString(int numBytes) {
			byte[] bytes = reader.ReadBytes(numBytes);
			return Encoding.ASCII.GetString(bytes);
		}

		/*** UTF16 Strings ***/
		public string ReadUTF16BEString(int numBytes) {
			byte[] bytes = reader.ReadBytes(numBytes);
			return Encoding.BigEndianUnicode.GetString(bytes);
		}

		/*** Helper Functions ***/

		private byte[] ReadBytes(int count) {
			byte[] b = reader.ReadBytes(count);
			if (b.Length != count) {
				throw new EndOfStreamException();
			}
			if (BitConverter.IsLittleEndian) {
				Array.Reverse(b); // Ensure Big-endian
			}
			return b;
		}

		/*** UInt8 ***/
		public byte ReadUInt8() {
			return reader.ReadByte();
		}

		public byte[] ReadUInt8(int count) {
			byte[] array = new byte[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadUInt8();
			}
			return array;
		}

		public void SkipUInt8(int count) {
			reader.ReadBytes(count);
		}

		/*** Int8 ***/
		public sbyte ReadInt8() {
			return reader.ReadSByte();
		}

		public sbyte[] ReadInt8(int count) {
			sbyte[] array = new sbyte[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadInt8();
			}
			return array;
		}

		public void SkipInt8(int count) {
			reader.ReadBytes(count);
		}

		/*** UInt16 ***/
		public ushort ReadUInt16() {
			return BitConverter.ToUInt16(ReadBytes(2), 0); // Big-Endian
		}

		public ushort[] ReadUInt16(int count) {
			ushort[] array = new ushort[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadUInt16();
			}
			return array;
		}

		public void SkipUInt16(int count) {
			reader.ReadBytes(count * 2);
		}

		/*** Int16 ***/
		public short ReadInt16() {
			return BitConverter.ToInt16(ReadBytes(2), 0); // Big-Endian
		}

		public short[] ReadInt16(int count) {
			short[] array = new short[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadInt16();
			}
			return array;
		}

		public void SkipInt16(int count) {
			reader.ReadBytes(count * 2);
		}

		/*** UFWord and FWord ***/
		public ushort ReadUFWord() => ReadUInt16();
		public ushort[] ReadUFWord(int count) => ReadUInt16(count);
		public void SkipUFWord(int count) => SkipUInt16(count);
		public short ReadFWord() => ReadInt16();
		public short[] ReadFWord(int count) => ReadInt16(count);
		public void SkipFWord(int count) => SkipInt16(count);

		/*** UInt32 ***/
		public uint ReadUInt32() {
			//return BitConverter.ToUInt32(ReadReverseBytes(4), 0); // Big-Endian
			byte[] bytes = ReadBytes(4);
			return BitConverter.ToUInt32(bytes, 0);
		}

		public uint[] ReadUInt32(int count) {
			uint[] array = new uint[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadUInt32();
			}
			return array;
		}

		public void SkipUInt32(int count) {
			reader.ReadBytes(count * 4);
		}

		/*** Int32 ***/
		public int ReadInt32() {
			return BitConverter.ToInt32(ReadBytes(4), 0); // Big-Endian
		}

		public int[] ReadInt32(int count) {
			int[] array = new int[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadInt32();
			}
			return array;
		}

		public void SkipInt32(int count) {
			reader.ReadBytes(count * 4);
		}

		/*** UInt64 ***/
		public ulong ReadUInt64() {
			return BitConverter.ToUInt64(ReadBytes(8), 0); // Big-Endian
		}

		public ulong[] ReadUInt64(int count) {
			ulong[] array = new ulong[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadUInt64();
			}
			return array;
		}

		public void SkipUInt64(int count) {
			reader.ReadBytes(count * 8);
		}

		/*** Int64 ***/
		public long ReadInt64() {
			return BitConverter.ToInt32(ReadBytes(8), 0); // Big-Endian
		}

		public long[] ReadInt64(int count) {
			long[] array = new long[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadInt64();
			}
			return array;
		}

		public void SkipInt64(int count) {
			reader.ReadBytes(count * 6);
		}

		/*** Fixed (16.16 float) ***/
		public float ReadFixed() {
			int fixed1616 = BitConverter.ToInt32(ReadBytes(4), 0); // Big-Endian
			return fixed1616 / 65536.0f;
		}

		public float[] ReadFixed(int count) {
			float[] array = new float[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadFixed();
			}
			return array;
		}

		public void SkipFixed(int count) {
			reader.ReadBytes(count * 4);
		}

		/*** 2.14 float ***/
		public float ReadF2Dot14() {
			ushort bytes = ReadUInt16();
			return HexWriter.ConvertF2Dot14(bytes);
		}

		public float[] ReadF2Dot14(int count) {
			float[] array = new float[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadF2Dot14();
			}
			return array;
		}

		public void SkipF2Dot14(int count) {
			SkipUInt16(count);
		}

		/*** Offset16 ***/
		public ushort? ReadOffset16() {
			ushort offset = ReadUInt16();
			return offset > 0 ? offset : null;
		}

		public ushort?[] ReadOffset16(int count) {
			ushort?[] array = new ushort?[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadOffset16();
			}
			return array;
		}

		public void SkipOffset16(int count) {
			SkipUInt16(count);
		}

		/*** Offset32 ***/
		public uint? ReadOffset32() {
			uint offset = ReadUInt32();
			return offset > 0 ? offset : null;
		}

		public uint?[] ReadOffset32(int count) {
			uint?[] array = new uint?[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadOffset32();
			}
			return array;
		}

		public void SkipOffset32(int count) {
			SkipUInt32(count);
		}

	}

	public static class FontFileReaderUtils {

		public static T ReadFrom<T>(this FontFileReader reader, long offset, Func<FontFileReader, long, T> parser) {
			long position = reader.Position;
			T result = parser(reader, offset);
			reader.Position = position;
			return result;
		}

		public static T? ReadFrom<T>(this FontFileReader reader, long? offset, Func<FontFileReader, long, T> parser) {
			if (offset.HasValue) {
				return reader.ReadFrom(offset.Value, parser);
			}
			else {
				return default;
			}
		}

		public static T ReadFromOffset16<T>(this FontFileReader reader, long referenceOffset, Func<FontFileReader, long, T> parser) {
			ushort valueOffset = reader.ReadUInt16();
			return reader.ReadFrom(referenceOffset + valueOffset, parser);
		}

		public static T ReadFromOffset32<T>(this FontFileReader reader, long referenceOffset, Func<FontFileReader, long, T> parser) {
			uint valueOffset = reader.ReadUInt32();
			return reader.ReadFrom(referenceOffset + valueOffset, parser);
		}

		public static T[] ReadFrom<T>(this FontFileReader reader, long referenceOffset, ushort[] offsets, Func<FontFileReader, long, T> parser) {
			T[] array = new T[offsets.Length];
			for (int i = 0; i < offsets.Length; i++) {
				array[i] = reader.ReadFrom(referenceOffset + offsets[i], parser);
			}
			return array;
		}

		public static T[] ReadFrom<T>(this FontFileReader reader, long referenceOffset, uint[] offsets, Func<FontFileReader, long, T> parser) {
			T[] array = new T[offsets.Length];
			for (int i = 0; i < offsets.Length; i++) {
				array[i] = reader.ReadFrom(referenceOffset + offsets[i], parser);
			}
			return array;
		}

		public static T?[] ReadFrom<T>(this FontFileReader reader, long referenceOffset, ushort?[] offsets, Func<FontFileReader, long, T> parser) {
			T?[] array = new T?[offsets.Length];
			for (int i = 0; i < offsets.Length; i++) {
				if (offsets[i].HasValue) {
					array[i] = reader.ReadFrom(referenceOffset + offsets[i]!.Value, parser);
				}
				else {
					array[i] = default;
				}
			}
			return array;
		}

		public static T?[] ReadFrom<T>(this FontFileReader reader, long referenceOffset, uint?[] offsets, Func<FontFileReader, long, T> parser) {
			T?[] array = new T?[offsets.Length];
			for (int i = 0; i < offsets.Length; i++) {
				if (offsets[i].HasValue) {
					array[i] = reader.ReadFrom(referenceOffset + offsets[i]!.Value, parser);
				}
				else {
					array[i] = default;
				}
			}
			return array;
		}

	}

}
