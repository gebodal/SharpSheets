using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeboPdf.Utilities;
using GeboPDF.Fonts.TrueType;

namespace GeboPdf.Fonts.TrueType {

	public class FontFileWriter {

		private readonly BinaryWriter _writer;

		public Stream BaseStream => _writer.BaseStream;

		public long Position {
			get { return _writer.BaseStream.Position; }
			set { _writer.BaseStream.Position = value; }
		}

		public long Length {
			get { return _writer.BaseStream.Length; }
		}

		public FontFileWriter(Stream stream) {
			if (!stream.CanWrite || !stream.CanSeek) {
				throw new ArgumentException("Input stream for FontFileWriterStream must be writable and seekable.");
			}
			this._writer = new BinaryWriter(stream);
		}

		public uint CheckSum(uint length) {
			return TrueTypeUtils.CheckSum(BaseStream, length);
		}

		/*
		private uint checksum = 0;
		private uint runningCount = 0;
		private long byteCount = 0;
		public void ResetChecksum() {
			checksum = 0;
			runningCount = 0;
			byteCount = 0;
		}
		public uint GetChecksum() {
			return checksum + (runningCount << (8 * (int)(byteCount % 4)));
		}

		private void IncrementChecksum(byte b) {
			runningCount = (runningCount << 8) + b;
			byteCount++;
			if (byteCount % 4 == 0) {
				checksum += runningCount;
				runningCount = 0;
			}
		}
		*/

		private void Write(Span<byte> bytes) {
			/*
			for (int i=0; i<bytes.Length; i++) {
				IncrementChecksum(bytes[i]);
			}
			*/
			_writer.Write(bytes);
		}
		private void Write(byte b) {
			//IncrementChecksum(b);
			_writer.Write(b);
		}

		public void Take(FontFileReader source, int numBytes) {
			Write(source.BaseStream.Read(numBytes));
		}

		/*** ASCII Strings ***/
		public void WriteASCIIString(string ascii) {
			Write(Encoding.ASCII.GetBytes(ascii));
		}

		/*** UTF16 Strings ***/
		public void WriteUTF16BEString(string utf16be) {
			Write(Encoding.BigEndianUnicode.GetBytes(utf16be));
		}

		/*** UInt8 ***/
		public void WriteUInt8(byte value) {
			Write(value);
		}

		public void WriteUInt8(byte[] values) {
			Write(values);
		}

		public void TakeUInt8(FontFileReader source, int count) {
			Take(source, count);
		}

		/*** Int8 ***/
		public void WriteInt8(sbyte value) {
			Write(unchecked((byte)value));
		}

		public void WriteInt8(sbyte[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteInt8(values[i]);
			}
		}

		public void TakeInt8(FontFileReader source, int count) {
			Take(source, count);
		}

		/*** UInt16 ***/
		public void WriteUInt16(ushort value) {
			Span<byte> bytes = stackalloc byte[2];
			BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
			Write(bytes); // Big-Endian
		}

		public void WriteUInt16(ushort[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteUInt16(values[i]);
			}
		}

		public void TakeUInt16(FontFileReader source, int count) {
			Take(source, count * 2);
		}

		/*** Int16 ***/
		public void WriteInt16(short value) {
			Span<byte> bytes = stackalloc byte[2];
			BinaryPrimitives.WriteInt16BigEndian(bytes, value);
			Write(bytes); // Big-Endian
		}

		public void WriteInt16(short[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteInt16(values[i]);
			}
		}

		public void TakeInt16(FontFileReader source, int count) {
			Take(source, count * 2);
		}

		/*** UFWord and FWord ***/
		public void WriteUFWord(ushort value) => WriteUInt16(value);
		public void WriteUFWord(ushort[] values) => WriteUInt16(values);
		public void WriteFWord(short value) => WriteInt16(value);
		public void WriteFWord(short[] values) => WriteInt16(values);

		public void TakeUFWord(FontFileReader source, int count) => TakeUInt16(source, count);
		public void TakeFWord(FontFileReader source, int count) => TakeInt16(source, count);

		/*** UInt32 ***/
		public void WriteUInt32(uint value) {
			Span<byte> bytes = stackalloc byte[4];
			BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
			Write(bytes); // Big-Endian
		}

		public void WriteUInt32(uint[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteUInt32(values[i]);
			}
		}

		public void TakeUInt32(FontFileReader source, int count) {
			Take(source, count * 4);
		}

		/*** Int32 ***/
		public void WriteInt32(int value) {
			Span<byte> bytes = stackalloc byte[4];
			BinaryPrimitives.WriteInt32BigEndian(bytes, value);
			Write(bytes); // Big-Endian
		}

		public void WriteInt32(int[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteInt32(values[i]);
			}
		}

		public void TakeInt32(FontFileReader source, int count) {
			Take(source, count * 4);
		}

		/*** UInt64 ***/
		public void WriteUInt64(ulong value) {
			Span<byte> bytes = stackalloc byte[8];
			BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
			Write(bytes); // Big-Endian
		}

		public void WriteUInt64(ulong[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteUInt64(values[i]);
			}
		}

		public void TakeUInt64(FontFileReader source, int count) {
			Take(source, count * 8);
		}

		/*** Int64 ***/
		public void WriteInt64(long value) {
			Span<byte> bytes = stackalloc byte[8];
			BinaryPrimitives.WriteInt64BigEndian(bytes, value);
			Write(bytes); // Big-Endian
		}

		public void WriteInt64(long[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteInt64(values[i]);
			}
		}

		public void TakeInt64(FontFileReader source, int count) {
			Take(source, count * 8);
		}

		/*** Fixed (16.16 float) ***/
		public void WriteFixed(float value) {
			if (float.IsNaN(value) || float.IsInfinity(value)) {
				throw new ArgumentOutOfRangeException(nameof(value), "Cannot encode NaN or Infinity as 16.16 fixed.");
			}

			// Scale using double for extra precision, then round to nearest integer.
			double scaled = Math.Round((double)value * 65536.0, MidpointRounding.ToEven);
			int fixed1616 = (int)long.Clamp((long)scaled, int.MinValue, int.MaxValue);

			WriteInt32(fixed1616);
		}

		public void WriteFixed(float[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteFixed(values[i]);
			}
		}

		public void TakeFixed(FontFileReader source, int count) {
			TakeInt32(source, count);
		}

		/*** 2.14 float ***/
		public void WriteF2Dot14(float value) {
			WriteUInt16(HexWriter.ConvertF2Dot14(value));
		}

		public void WriteF2Dot14(float[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteF2Dot14(values[i]);
			}
		}

		public void TakeF2Dot14(FontFileReader source, int count) {
			TakeUInt16(source, count);
		}

		/*** Offset16 ***/
		public void WriteOffset16(ushort? value) {
			WriteUInt16(value ?? 0);
		}

		public void WriteOffset16(ushort?[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteOffset16(values[i]);
			}
		}

		public void TakeOffset16(FontFileReader source, int count) {
			TakeUInt16(source, count);
		}

		/*** Offset32 ***/
		public void WriteOffset32(uint? value) {
			WriteUInt32(value ?? 0);
		}

		public void WriteOffset32(uint?[] values) {
			for (int i = 0; i < values.Length; i++) {
				WriteOffset32(values[i]);
			}
		}

		public void TakeOffset32(FontFileReader source, int count) {
			TakeUInt32(source, count);
		}

		/*** Long alignment helpers ***/
		public void EnsureLongAlignedFrom(long position) {
			if (position > Position) {
				throw new InvalidOperationException("Cannot long align from subsequent position.");
			}
			else if (position == Position) {
				return; // Trivially aligned
			}

			long currentLength = Position - position;
			long totalLength = 4 * (((currentLength - 1) / 4) + 1);
			long paddingLength = totalLength - currentLength;

			for (long p = 0; p < paddingLength; p++) {
				Write(0); // Pad with zeros for 4-byte-alignment
			}
		}

		/*** Table writing helpers ***/

		public static int FontDirectorySize(int numTables) {
			return 12 + 16 * numTables; // 12 bytes for the front matter, and 16 bytes per table record
		}

		public void WriteOffsetSubtable(uint scalerType, int numTables) {
			ushort maxPow2 = (ushort)Math.Pow(2, (int)Math.Log2(numTables)); // Highest power of 2 <= tables.Length
			ushort searchRange = (ushort)(maxPow2 * 16);
			ushort entrySelector = (ushort)Math.Log2(maxPow2);
			ushort rangeShift = (ushort)(numTables * 16 - searchRange);

			// Offset subtable (12 bytes)
			WriteUInt32(scalerType);
			WriteUInt16((ushort)numTables);
			WriteUInt16(searchRange);
			WriteUInt16(entrySelector);
			WriteUInt16(rangeShift);
		}

		/*
		public void WriteTableDirectory(TrueTypeFontTable[] tables) {
			for (int i = 0; i < tables.Length; i++) {
				//Console.WriteLine($"table {i} '{tables[i].tag}', checksum {tables[i].checksum}, offset {tables[i].offset}, length {tables[i].length}");
				WriteASCIIString(tables[i].tag);
				WriteUInt32(tables[i].checksum);
				WriteUInt32(tables[i].offset);
				WriteUInt32(tables[i].length);
			}
		}
		*/

		public void WriteTableDirectoryRecalculateCheckSum(TrueTypeFontTable[] tables) {
			for (int i = 0; i < tables.Length; i++) {
				//Console.WriteLine($"table {i} '{tables[i].tag}', checksum {tables[i].checksum}, offset {tables[i].offset}, length {tables[i].length}, end {tables[i].offset + tables[i].length}/{BaseStream.Length}");
				long initialPos = Position;
				Position = tables[i].offset;
				uint checkSum = CheckSum(tables[i].length);
				Position = initialPos;

				//Console.WriteLine($"table {i} '{tables[i].tag}', checksum {checkSum}, offset {tables[i].offset}, length {tables[i].length}");
				WriteASCIIString(tables[i].tag);
				WriteUInt32(checkSum);
				WriteUInt32(tables[i].offset);
				WriteUInt32(tables[i].length);
			}
		}

		public void Checksumming(TrueTypeFontTable headTable, long startPos, long wholeLength) {
			Position = startPos;
			uint wholeCheckSum = CheckSum((uint)wholeLength);

			TrueTypeHeadTable.UpdateCheckSumAdjustmentValue(this, headTable.offset, 0xB1B0AFBA - wholeCheckSum);
		}

		public void WriteFontDirectoryAndTables(Stream source, uint scalerType, TrueTypeFontTable[] tables) {

			uint initialOffset = (uint)Position;

			uint[] totalLengths = new uint[tables.Length];
			for (int i = 0; i < tables.Length; i++) {
				totalLengths[i] = 4 * (((tables[i].length - 1) / 4) + 1); // Tables must be 4-byte-aligned (long aligned)
			}

			uint[] newOffsets = new uint[tables.Length];
			newOffsets[0] = initialOffset + (uint)FontDirectorySize(tables.Length);
			for (int i = 1; i < tables.Length; i++) {
				newOffsets[i] = newOffsets[i - 1] + totalLengths[i - 1];
			}

			TrueTypeFontTable[] newTables = new TrueTypeFontTable[tables.Length];
			for (int i = 0; i < tables.Length; i++) {
				newTables[i] = new TrueTypeFontTable(tables[i].tag, tables[i].checksum, newOffsets[i], tables[i].length);
			}

			Position += FontDirectorySize(tables.Length);

			/*
			 * Strong assumption made here that the data in the tables will be contiguous
			 * and not interleaved. However, as detected such interleaving would require
			 * a full implementation of all font subtables, this will have to do for now.
			 */

			// Write actual table contents
			for (int i = 0; i < tables.Length; i++) {
				CopyTable(source, tables[i]);
			}
			long finalPosition = Position;

			TrueTypeFontTable? headTable = tables.FirstOrDefault(t => t.tag == "head");
			if (headTable is not null) {
				TrueTypeHeadTable.UpdateCheckSumAdjustmentValue(this, headTable.offset, 0);
			}

			// Write the Font Directory subtables
			Position = initialOffset; // Return to start
			// - Offset subtable (12 bytes)
			WriteOffsetSubtable(scalerType, tables.Length);
			// - Table directory (with newly calculated offsets)
			WriteTableDirectoryRecalculateCheckSum(newTables);

			if (headTable is not null) {
				Checksumming(headTable, initialOffset, finalPosition - initialOffset);
			}

			// Return to end, just to be safe
			Position = finalPosition;
		}

		public void CopyTable(Stream source, TrueTypeFontTable table) {
			source.Position = table.offset;
			source.CopyTo((int)table.length, BaseStream);

			uint totalLength = 4 * (((table.length - 1) / 4) + 1);
			uint paddingLength = totalLength - table.length;

			for (uint p = 0; p < paddingLength; p++) {
				BaseStream.WriteByte(0); // Pad tables with zeros for 4-byte-alignment
			}
		}
	}

}
