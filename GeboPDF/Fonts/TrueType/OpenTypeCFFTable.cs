using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	public class OpenTypeCFFTable {

		public readonly Type2Glyph[] glyphs;

		public OpenTypeCFFTable(Type2Glyph[] glyphs) {
			this.glyphs = glyphs;
		}

		public static OpenTypeCFFTable Read(FontFileReader fontReader, long offset) {
			
			CFFReader reader = new CFFReader(fontReader.BaseStream, offset);
			reader.Position = 0;

			byte majorVersion = reader.ReadCard8();
			/* byte minorVersion */ _ = reader.ReadCard8();
			if (majorVersion != 1) { // Should be able to ignore minor version increments safely
				throw new FormatException("Unknown CFF version.");
			}

			byte headerSize = reader.ReadCard8();
			byte offSize = reader.ReadOffSize();

			reader.Position = headerSize;

			CFFIndexData nameIndex = reader.ReadIndex();
			if (nameIndex.Count != 1) {
				throw new FormatException($"CFF Name INDEX must have exactly 1 entry (got {nameIndex.Count}).");
			}
			reader.Position = nameIndex.end;

			CFFIndexData topDictIndex = reader.ReadIndex();
			if(nameIndex.Count != topDictIndex.Count) {
				throw new FormatException($"Number of entries in Top DICT INDEX ({topDictIndex.Count}) must match Name INDEX ({nameIndex.Count}).");
			}
			reader.Position = topDictIndex.end;

			CFFIndexData stringIndex = reader.ReadIndex();
			reader.Position = stringIndex.end;

			CFFIndexData globalSubrIndex = reader.ReadIndex();
			reader.Position = globalSubrIndex.end;

			(CFFIndexData charStringsIndex, CFFIndexData? subrsIndex) = GetTopDictData(reader, topDictIndex.offsets[0], topDictIndex.lengths[0]);

			Type2Glyph[] glyphs = new Type2Glyph[charStringsIndex.Count];
			for(int i=0; i<charStringsIndex.Count; i++) {
				//Console.WriteLine($"Read glyph {i}");
				glyphs[i] = Type2CharStrings.ReadCharString(reader, charStringsIndex.offsets[i], charStringsIndex.lengths[i], globalSubrIndex, subrsIndex);
			}

			return new OpenTypeCFFTable(glyphs);
		}

		private static readonly ushort charstringTypeOperator = 0x0c06; // Bytes 12,6
		private static readonly ushort charStringsOperator = 17;
		private static readonly ushort privateDictOperator = 18;
		private static readonly ushort privateDictSubrsOperator = 19;

		private static (CFFIndexData charStringsIndex, CFFIndexData? subrsIndex) GetTopDictData(CFFReader reader, long topDictOffset, long topDictLength) {

			reader.Position = topDictOffset;

			Dictionary<ushort, object[]> topDict = reader.ReadDictionary(topDictLength);

			object[]? charstringTypeOperands = topDict.GetValueOrDefault(charstringTypeOperator);
			/*
			if(charstringTypeOperands is null) {
				throw new FormatException("Could not find CharstringType entry in CFF Top DICT.");
			}
			*/
			if(charstringTypeOperands is not null && (charstringTypeOperands.Length != 1 || charstringTypeOperands[0] is not long charstringType || charstringType != 2)) {
				throw new FormatException($"CharstringType must be 2 for OpenType CFF table.");
			}

			object[]? charStringsOperands = topDict.GetValueOrDefault(charStringsOperator);
			if (charStringsOperands is null) {
				throw new FormatException("Could not find CharStrings entry in CFF Top DICT.");
			}
			else if(charStringsOperands.Length != 1 || charStringsOperands[0] is not long) {
				throw new FormatException($"CharStrings operand is not valid offset value.");
			}
			long charStringsOffset = (long)charStringsOperands[0];

			reader.Position = charStringsOffset;
			CFFIndexData charStringsIndex = reader.ReadIndex();

			CFFIndexData? subrsIndex = null;
			object[]? privateDictOperands = topDict.GetValueOrDefault(privateDictOperator);
			if (privateDictOperands is not null) {
				if (privateDictOperands.Length == 2 && privateDictOperands[0] is long privateDictSize && privateDictOperands[1] is long privateDictOffset) {
					reader.Position = privateDictOffset;
					Dictionary<ushort, object[]> privateDict = reader.ReadDictionary(privateDictSize);

					object[]? subrsOperands = privateDict.GetValueOrDefault(privateDictSubrsOperator);
					if (subrsOperands is not null) {
						if (subrsOperands.Length == 1 && subrsOperands[0] is long subrsLocalOffset) {
							reader.Position = privateDictOffset + subrsLocalOffset;
							subrsIndex = reader.ReadIndex();
						}
						else {
							throw new FormatException($"Private DICT Subrs operand is not a valid offset value.");
						}
					}
				}
				else {
					throw new FormatException($"Invalid Private DICT operands in Top DICT.");
				}
			}

			return (charStringsIndex, subrsIndex);
		}

		private static void ReadCharString(CFFReader reader, long offset, long length, CFFIndexData globalSubrIndex, CFFIndexData? subrsIndex) {
			reader.Position = offset;


		}

	}

	public class CFFReader {

		//private readonly Stream stream;
		private readonly BinaryReader reader;

		public Stream BaseStream => reader.BaseStream;

		private readonly long zeroOffset;
		public long Position {
			get { return reader.BaseStream.Position - zeroOffset; }
			set { reader.BaseStream.Position = zeroOffset + value; }
		}

		public CFFReader(Stream stream, long zeroOffset) {
			//this.stream = stream;
			if (!stream.CanRead || !stream.CanSeek) {
				throw new ArgumentException("Input stream for CFFReader must be readable and seekable.");
			}
			this.reader = new BinaryReader(stream);
			this.zeroOffset = zeroOffset;
		}

		/*** Helper Functions ***/

		internal byte ReadByte() {
			return reader.ReadByte();
		}

		internal byte[] ReadBytes(int count) {
			byte[] b = reader.ReadBytes(count);
			if (b.Length != count) {
				throw new EndOfStreamException();
			}
			if (BitConverter.IsLittleEndian) {
				Array.Reverse(b); // Account for Big-endian
			}
			return b;
		}

		private byte[] ReadBytes(int count, int pad) {
			byte[] b = reader.ReadBytes(count);
			if (b.Length != count) {
				throw new EndOfStreamException();
			}

			byte[] bPad = new byte[pad + count];
			for (int i = 0; i < pad; i++) {
				bPad[i] = 0;
			}
			for (int i = 0; i < count; i++) {
				bPad[pad + i] = b[i];
			}

			if (BitConverter.IsLittleEndian) {
				Array.Reverse(bPad); // Account for Big-endian
			}
			return bPad;
		}

		/*** Card8 ***/
		public byte ReadCard8() {
			return reader.ReadByte();
		}

		public byte[] ReadCard8(int count) {
			byte[] array = new byte[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadCard8();
			}
			return array;
		}

		public void SkipCard8(int count) {
			reader.ReadBytes(count);
		}

		/*** Card16 ***/
		public ushort ReadCard16() {
			return BitConverter.ToUInt16(ReadBytes(2), 0);
		}

		public ushort[] ReadCard16(int count) {
			ushort[] array = new ushort[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadCard16();
			}
			return array;
		}

		public void SkipCard16(int count) {
			reader.ReadBytes(count * 2);
		}

		/*** SID ***/
		public ushort ReadSID() {
			return BitConverter.ToUInt16(ReadBytes(2), 0); // Should only have values 0-64999
		}

		public ushort[] ReadSID(int count) {
			ushort[] array = new ushort[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadSID();
			}
			return array;
		}

		public void SkipSID(int count) {
			reader.ReadBytes(count * 2);
		}

		/*** OffSize ***/
		public byte ReadOffSize() {
			return reader.ReadByte(); // Should only have values 1-4
		}

		public byte[] ReadOffSize(int count) {
			byte[] array = new byte[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadOffSize();
			}
			return array;
		}

		public void SkipOffSize(int count) {
			reader.ReadBytes(count);
		}

		/*** Offset ***/
		public uint ReadOffset(byte offSize) {
			if (offSize == 1) {
				return reader.ReadByte();
			}
			else if (offSize == 2) {
				return BitConverter.ToUInt16(ReadBytes(2), 0);
			}
			else if (offSize == 3) {
				return BitConverter.ToUInt32(ReadBytes(3, 1), 0);
			}
			else if (offSize == 4) {
				return BitConverter.ToUInt32(ReadBytes(4), 0);
			}
			else {
				throw new FormatException("Invalid OffSize value (must be 1-4).");
			}
		}

		public uint[] ReadOffset(byte offSize, int count) {
			uint[] array = new uint[count];
			for (int i = 0; i < count; i++) {
				array[i] = ReadOffset(offSize);
			}
			return array;
		}

		/*** DICT Operands ***/

		private long ReadIntDictOperand(byte b0) {
			if (b0 >= 32 && b0 <= 246) {
				return b0 - 139;
			}
			else if (b0 >= 247 && b0 <= 250) {
				byte b1 = reader.ReadByte();
				return (b0 - 247) * 256 + b1 + 108;
			}
			else if (b0 >= 251 && b0 <= 254) {
				byte b1 = reader.ReadByte();
				return -(b0 - 251) * 256 - b1 - 108;
			}
			else if (b0 == 28) {
				/*
				byte b1 = reader.ReadByte();
				byte b2 = reader.ReadByte();
				return (b1 << 8) | b2;
				*/
				return BitConverter.ToInt16(ReadBytes(2), 0);
			}
			else if (b0 == 29) {
				/*
				byte b1 = reader.ReadByte();
				byte b2 = reader.ReadByte();
				byte b3 = reader.ReadByte();
				byte b4 = reader.ReadByte();
				return ((long)b1 << 24) | ((long)b2 << 16) | ((long)b3 << 8) | b4;
				*/
				return BitConverter.ToInt32(ReadBytes(4), 0);
			}
			else {
				throw new FormatException("Invalid leading byte for CFF integer dictionary operand.");
			}
		}

		public long ReadIntDictOperand() {
			byte b0 = reader.ReadByte();
			return ReadIntDictOperand(b0);
		}

		public double ReadRealDictOperand(byte b0) {
			if (b0 == 30) {

				// This approach seems inefficient and hacky, but I can't be bothered right now.
				StringBuilder sb = new StringBuilder();

				bool continueFlag = true;

				while (continueFlag) {
					byte[] nibbles = ReadNibbles();

					for (int n = 0; continueFlag && n < nibbles.Length; n++) {
						switch (nibbles[n]) {
							case 0xf:
								continueFlag = false;
								break;
							case 0xa:
								sb.Append('.');
								break;
							case 0xb:
								sb.Append('E');
								break;
							case 0xc:
								sb.Append("E-");
								break;
							case 0xd:
								throw new FormatException("Invalid nibble value for CFF real dictionary operand.");
							case 0xe:
								sb.Append('-');
								break;
							default:
								sb.Append(nibbles[n]);
								break;
						}
					}
				}

				try {
					return double.Parse(sb.ToString());
				}
				catch (SystemException e) {
					throw new FormatException("Error parsing CFF real dictionary operand.", e);
				}
			}
			else {
				throw new FormatException("Invalid leading byte for CFF real dictionary operand.");
			}
		}

		public double ReadRealDictOperand() {
			byte b0 = reader.ReadByte();
			return ReadRealDictOperand(b0);
		}

		private byte[] ReadNibbles() {
			byte b = reader.ReadByte();
			byte nibble1 = (byte)((b >> 4) & 0x0f);
			byte nibble2 = (byte)(b & 0x0f);
			return new byte[] { nibble1, nibble2 };
		}

		public Dictionary<ushort, object[]> ReadDictionary(long length) {

			long endPos = Position + length;

			List<(ushort operatorCode, object[] operands)> values = new List<(ushort, object[])>();
			List<object> operands = new List<object>();

			while (Position < endPos) {
				byte b0 = reader.ReadByte();

				if (operands.Count > 48) {
					throw new FormatException("Too many operands encountered in CFF DICT.");
				}

				if (b0 <= 21) {
					// Operator
					ushort fullCode;
					if (b0 == 12) {
						byte b1 = reader.ReadByte();
						fullCode = (ushort)((b0 << 8) | b1);
					}
					else {
						fullCode = b0;
					}
					values.Add((fullCode, operands.ToArray()));
					operands.Clear();
				}
				else if (b0 == 30) {
					// Real number
					operands.Add(ReadRealDictOperand(b0));
				}
				else if (b0 == 28 || b0 == 29 || (b0 >= 32 && b0 <= 254)) {
					// Integer number
					operands.Add(ReadIntDictOperand(b0));
				}
				else {
					throw new FormatException("Invalid start byte for CFF DICT value.");
				}
			}

			if(operands.Count > 0) {
				throw new FormatException("Incomplete CFF DICT.");
			}

			return values.ToDictionary(e => e.operatorCode, e => e.operands);
		}

		/*** INDEX ***/

		public CFFIndexData ReadIndex() {
			ushort count = ReadCard16();

			if (count == 0) {
				return new CFFIndexData(Array.Empty<long>(), Array.Empty<uint>(), Position);
			}

			byte offSize = ReadOffSize();

			uint[] offsets = new uint[count + 1]; // +1 so that size of last entry can be calculated
			uint[] lengths = new uint[count];

			for (int i = 0; i <= count; i++) {
				offsets[i] = ReadOffset(offSize);
				if (i > 0) {
					lengths[i - 1] = offsets[i] - offsets[i - 1];
				}
			}

			long offsetZero = Position - 1;
			long[] finalOffsets = new long[count];

			for (int i = 0; i < count; i++) {
				finalOffsets[i] = offsetZero + offsets[i];
			}

			return new CFFIndexData(finalOffsets, lengths, offsetZero + offsets[^1]);
		}

	}

	public class CFFIndexData {

		public readonly long[] offsets; // These are absolute positions within the stream
		public readonly uint[] lengths;
		public readonly long end; // First byte after the end of index data

		public int Count => lengths.Length;

		public CFFIndexData(long[] offsets, uint[] lengths, long end) {
			this.offsets = offsets;
			this.lengths = lengths;
			this.end = end;
		}

	}

	public class Type2Glyph {

		public IReadOnlyList<short> Xs => xs;
		public IReadOnlyList<short> Ys => ys;
		public IReadOnlyList<bool> OnCurve => onCurve;
		public IReadOnlyList<ushort> EndPtsOfContours => endPtsOfContours;

		private readonly List<short> xs;
		private readonly List<short> ys;
		private readonly List<bool> onCurve;
		private readonly List<ushort> endPtsOfContours;

		public int PointCount => xs.Count;

		internal Type2Glyph() {
			this.xs = new List<short>();
			this.ys = new List<short>();
			this.onCurve = new List<bool>();
			this.endPtsOfContours = new List<ushort>();
		}

		internal void AddPoint(short x, short y, bool onCurve) {
			xs.Add(x);
			ys.Add(y);
			this.onCurve.Add(onCurve);
			//Console.WriteLine($"Add point {(onCurve ? "on" : "off")} curve");
		}

		internal void EndContour() {
			if (endPtsOfContours.Count == 0 && xs.Count > 0) {
				endPtsOfContours.Add((ushort)(xs.Count - 1));
			}
			else if (endPtsOfContours.Count > 0 && endPtsOfContours[^1] < xs.Count - 1) {
				endPtsOfContours.Add((ushort)(xs.Count - 1));
			}
		}
	}

	internal static class Type2CharStrings {

		public static Type2Glyph ReadCharString(CFFReader reader, long offset, long length, CFFIndexData globalSubrIndex, CFFIndexData? subrsIndex) {
			reader.Position = offset;

			Type2Glyph glyph = new Type2Glyph();
			List<object> stack = new List<object>();
			object[] transientArray = new object[32];
			bool hadStackClear = false;
			int stemCount = 0;
			short currentX = 0, currentY = 0;

			ReadCharString(reader, length, globalSubrIndex, subrsIndex, glyph, stack, transientArray, ref hadStackClear, ref stemCount, ref currentX, ref currentY, 0, out _);

			//Console.WriteLine("ENDGLYPH\n");

			return glyph;
		}

		public static void ReadCharString(CFFReader reader, long length, CFFIndexData globalSubrIndex, CFFIndexData? subrsIndex, Type2Glyph glyph, List<object> stack, object[] transientArray, ref bool hadStackClear, ref int stemCount, ref short currentX, ref short currentY, int subroutineDepth, out bool encounteredEndChar) {
			encounteredEndChar = false;
			long endPos = reader.Position + length;

			void ProcessWidth() {
				stack.RemoveAt(0);
				//Console.WriteLine($"Width found, stack count now {stack.Count}");
			}

			short GetStack(int idx) {
				return stack[idx] switch {
					long longVal => (short)longVal,
					float floatVal => (short)floatVal,
					_ => throw new FormatException("Invalid stack value.")
				};
			}
			object PopStack() {
				object val = stack[^1];
				stack.RemoveAt(stack.Count - 1);
				return val;
			}
			bool ConvertBool(object val) {
				return val switch {
					long longVal => longVal != 0,
					float floatVal => floatVal != 0f,
					_ => throw new FormatException("Invalid stack logical value.")
				};
			}
			double ConvertDouble(object val) {
				return val switch {
					long longVal => (double)longVal,
					float floatVal => (double)floatVal,
					_ => throw new FormatException("Invalid stack numeric value.")
				};
			}

			//bool hadStackClear = false;
			//int stemCount = 0;

			int GetStemBytes(int sCount) {
				return (sCount / 8) + ((sCount < 8 || sCount % 8 > 0) ? 1 : 0);
			}

			//short currentX = 0, currentY = 0;

			//while(reader.Position < endPos) {
			while (true) {

				byte b0 = reader.ReadByte();

				if (b0 == 28) {
					// 16-bit 2s complement integer
					stack.Add((long)BitConverter.ToInt16(reader.ReadBytes(2), 0));
				}
				else if (b0 == 255) {
					// 32-bit 2s complement number
					int fixed1616 = BitConverter.ToInt32(reader.ReadBytes(4), 0); // Big-Endian
					stack.Add((float)(fixed1616 / 65536.0f));
				}
				else if (b0 >= 32 && b0 <= 246) {
					// Integer
					stack.Add((long)(b0 - 139));
				}
				else if (b0 >= 247 && b0 <= 250) {
					// Integer
					byte b1 = reader.ReadByte();
					stack.Add((long)((b0 - 247) * 256 + b1 + 108));
				}
				else if (b0 >= 251 && b0 <= 254) {
					// Integer
					byte b1 = reader.ReadByte();
					stack.Add((long)(-(b0 - 251) * 256 - b1 - 108));
				}
				else {
					// Operators: 0-11, (12,...), 13-18, 19, 20, 21-27, 29-31
					ushort fullCode;
					if (b0 == 12) { // Escape operator
						byte b1 = reader.ReadByte();
						fullCode = (ushort)((b0 << 8) | b1);
					}
					else {
						fullCode = b0;
					}

					bool clearStack = true;

					/*
					Console.Write("Op ");
					if (b0 == 12) {
						Console.Write($"12 {fullCode & 0xff}");
					}
					else {
						Console.Write($"{fullCode}");
					}
					Console.WriteLine($" [{fullCode:X4}] ({subroutineDepth}) : " + (stack.Count > 0 ? ($"({stack.Count}) " + string.Join(", ", stack.Select(i => i.ToString()))) : "EMPTY"));
					*/

					// Path construction Operators
					if (fullCode == rmovetoOp) { // rmoveto
						if (!hadStackClear && stack.Count > 2) { ProcessWidth(); } // May take width argument
						glyph.EndContour();
						currentX += GetStack(0);
						currentY += GetStack(1);
						glyph.AddPoint(currentX, currentY, true);
					}
					else if (fullCode == hmovetoOp) { // hmoveto
						if (!hadStackClear && stack.Count > 1) { ProcessWidth(); } // May take width argument
						glyph.EndContour();
						currentX += GetStack(0);
						glyph.AddPoint(currentX, currentY, true);
					}
					else if (fullCode == vmovetoOp) { // vmoveto
						if (!hadStackClear && stack.Count > 1) { ProcessWidth(); } // May take width argument
						glyph.EndContour();
						currentY += GetStack(0);
						glyph.AddPoint(currentX, currentY, true);
					}
					else if (fullCode == rlinetoOp) { // rlineto
						for (int i = 0; i < stack.Count; i += 2) {
							currentX += GetStack(i);
							currentY += GetStack(i + 1);
							glyph.AddPoint(currentX, currentY, true);
						}
					}
					else if (fullCode == hlinetoOp) { // hlineto
						for (int i = 0; i < stack.Count; i++) {
							if (i % 2 == 0) {
								currentX += GetStack(i);
							}
							else {
								currentY += GetStack(i);
							}
							glyph.AddPoint(currentX, currentY, true);
						}
					}
					else if (fullCode == vlinetoOp) { // vlineto
						for (int i = 0; i < stack.Count; i++) {
							if (i % 2 == 0) {
								currentY += GetStack(i);
							}
							else {
								currentX += GetStack(i);
							}
							glyph.AddPoint(currentX, currentY, true);
						}
					}
					else if (fullCode == rrcurvetoOp) { // rrcurveto
						for (int i = 0; i < stack.Count; i += 6) {
							currentX += GetStack(i);
							currentY += GetStack(i + 1);
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(i + 2);
							currentY += GetStack(i + 3);
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(i + 4);
							currentY += GetStack(i + 5);
							glyph.AddPoint(currentX, currentY, true);
						}
					}
					else if (fullCode == hhcurvetoOp) { // hhcurveto
						int starti;
						if (stack.Count % 2 == 1) { // Odd case
							starti = 1;
							currentY += GetStack(0); // dy1?
						}
						else {
							starti = 0;
						}
						for (int i = starti; i < stack.Count; i += 4) {
							// Is this right?
							currentX += GetStack(i); // dxa
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(i + 1); // dxb
							currentY += GetStack(i + 2); // dyb
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(i + 3); // dxc
							glyph.AddPoint(currentX, currentY, true);
						}
					}
					else if (fullCode == hvcurvetoOp) { // hvcurveto
						int evenCount = stack.Count - (stack.Count % 2); // Remove trailing odd-indexed value if present
						bool hasOdd = stack.Count % 2 == 1;
						if (evenCount % 8 == 0) {
							for (int i = 0; i < evenCount; i += 8) {
								currentX += GetStack(i); // dxa
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 1); // dxb
								currentY += GetStack(i + 2); // dyb
								glyph.AddPoint(currentX, currentY, false);
								currentY += GetStack(i + 3); // dyc
								glyph.AddPoint(currentX, currentY, true);
								currentY += GetStack(i + 4); // dyd
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 5); // dxe
								currentY += GetStack(i + 6); // dye
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 7); // dxf
								if (i + 8 < evenCount) { // Not the last one
									glyph.AddPoint(currentX, currentY, true);
								}
							}

							if (hasOdd) {
								currentY += GetStack(stack.Count - 1); // dyf?
							}
							glyph.AddPoint(currentX, currentY, true);
						}
						else if(stack.Count == 4 || stack.Count == 5) {
							currentX += GetStack(0); // dx1
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(1); // dx2
							currentY += GetStack(2); // dy2
							glyph.AddPoint(currentX, currentY, false);
							currentY += GetStack(3); // dy3
							if (hasOdd) {
								currentX += GetStack(stack.Count - 1); // dxf?
							}
							glyph.AddPoint(currentX, currentY, true);
						}
						else {
							currentX += GetStack(0); // dx1
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(1); // dx2
							currentY += GetStack(2); // dy2
							glyph.AddPoint(currentX, currentY, false);
							currentY += GetStack(3); // dy3
							glyph.AddPoint(currentX, currentY, true);

							for (int i = 4; i < evenCount; i += 8) {
								currentY += GetStack(i); // dya
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 1); // dxb
								currentY += GetStack(i + 2); // dyb
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 3); // dxc
								glyph.AddPoint(currentX, currentY, true);
								currentX += GetStack(i + 4); // dxd
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 5); // dxe
								currentY += GetStack(i + 6); // dye
								glyph.AddPoint(currentX, currentY, false);
								currentY += GetStack(i + 7); // dyf
								if (i + 8 < evenCount) { // Not the last one
									glyph.AddPoint(currentX, currentY, true);
								}
							}

							if (stack.Count > 4) {
								if (hasOdd) {
									currentX += GetStack(stack.Count - 1); // dxf?
								}
								glyph.AddPoint(currentX, currentY, true);
							}
						}
					}
					else if (fullCode == rcurvelineOp) { // rcurveline
						for (int i = 0; i < stack.Count - 2; i += 6) {
							currentX += GetStack(i);
							currentY += GetStack(i + 1);
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(i + 2);
							currentY += GetStack(i + 3);
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(i + 4);
							currentY += GetStack(i + 5);
							glyph.AddPoint(currentX, currentY, true);
						}

						currentX += GetStack(stack.Count - 2);
						currentY += GetStack(stack.Count - 1);
						glyph.AddPoint(currentX, currentY, true);
					}
					else if (fullCode == rlinecurveOp) { // rlinecurve
						for (int i = 0; i < stack.Count - 6; i += 2) {
							currentX += GetStack(i);
							currentY += GetStack(i + 1);
							glyph.AddPoint(currentX, currentY, true);
						}

						currentX += GetStack(stack.Count - 6);
						currentY += GetStack(stack.Count - 5);
						glyph.AddPoint(currentX, currentY, false);
						currentX += GetStack(stack.Count - 4);
						currentY += GetStack(stack.Count - 3);
						glyph.AddPoint(currentX, currentY, false);
						currentX += GetStack(stack.Count - 2);
						currentY += GetStack(stack.Count - 1);
						glyph.AddPoint(currentX, currentY, true);
					}
					else if (fullCode == vhcurvetoOp) { // vhcurveto
						int evenCount = stack.Count - (stack.Count % 2); // Remove trailing odd-indexed value if present
						bool hasOdd = stack.Count % 2 == 1;
						if (evenCount % 8 == 0) {
							for (int i = 0; i < evenCount; i += 8) {
								currentY += GetStack(i); // dya
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 1); // dxb
								currentY += GetStack(i + 2); // dyb
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 3); // dxc
								glyph.AddPoint(currentX, currentY, true);
								currentX += GetStack(i + 4); // dxd
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 5); // dxe
								currentY += GetStack(i + 6); // dye
								glyph.AddPoint(currentX, currentY, false);
								currentY += GetStack(i + 7); // dyf
								if (i + 8 < evenCount) { // Not the last one
									glyph.AddPoint(currentX, currentY, true);
								}
							}

							if (hasOdd) {
								currentX += GetStack(stack.Count - 1); // dxf?
							}
							glyph.AddPoint(currentX, currentY, true);
						}
						else if(stack.Count == 4 || stack.Count == 5) {
							currentY += GetStack(0); // dy1
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(1); // dx2
							currentY += GetStack(2); // dy2
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(3); // dx3
							if (hasOdd) {
								currentY += GetStack(stack.Count - 1); // dyf?
							}
							glyph.AddPoint(currentX, currentY, true);
						}
						else {
							currentY += GetStack(0); // dy1
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(1); // dx2
							currentY += GetStack(2); // dy2
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(3); // dx3
							glyph.AddPoint(currentX, currentY, true);

							for (int i = 4; i < evenCount; i += 8) {
								currentX += GetStack(i); // dxa
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 1); // dxb
								currentY += GetStack(i + 2); // dyb
								glyph.AddPoint(currentX, currentY, false);
								currentY += GetStack(i + 3); // dyc
								glyph.AddPoint(currentX, currentY, true);
								currentY += GetStack(i + 4); // dyd
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 5); // dxe
								currentY += GetStack(i + 6); // dye
								glyph.AddPoint(currentX, currentY, false);
								currentX += GetStack(i + 7); // dxf
								if (i + 8 < evenCount) { // Not the last one
									glyph.AddPoint(currentX, currentY, true);
								}
							}

							if (stack.Count > 4) {
								if (hasOdd) {
									currentY += GetStack(stack.Count - 1); // dyf?
								}
								glyph.AddPoint(currentX, currentY, true);
							}
						}
					}
					else if (fullCode == vvcurvetoOp) { // vvcurveto
						int starti;
						if (stack.Count % 2 == 1) { // Odd case
							starti = 1;
							currentX += GetStack(0); // dx1?
						}
						else {
							starti = 0;
						}
						for (int i = starti; i < stack.Count; i += 4) {
							// Is this right?
							currentY += GetStack(i); // dya
							glyph.AddPoint(currentX, currentY, false);
							currentX += GetStack(i + 1); // dxb
							currentY += GetStack(i + 2); // dyb
							glyph.AddPoint(currentX, currentY, false);
							currentY += GetStack(i + 3); // dyc
							glyph.AddPoint(currentX, currentY, true);
						}
					}
					else if (fullCode == flexOp) { // flex - Ignore stack[12] (fd)
						currentX += GetStack(0); // dx1
						currentY += GetStack(1); // dy1
						glyph.AddPoint(currentX, currentY, false); // First control
						currentX += GetStack(2); // dx2
						currentY += GetStack(3); // dy2
						glyph.AddPoint(currentX, currentY, false); // First neighbour contrl
						currentX += GetStack(4); // dx3
						currentY += GetStack(5); // dy3
						glyph.AddPoint(currentX, currentY, true); // Joining point
						currentX += GetStack(6); // dx4
						currentY += GetStack(7); // dy4
						glyph.AddPoint(currentX, currentY, false); // Second neighbour control
						currentX += GetStack(8); // dx5
						currentY += GetStack(9); // dy5
						glyph.AddPoint(currentX, currentY, false); // Last control
						currentX += GetStack(10); // dx6
						currentY += GetStack(11); // dy6
						glyph.AddPoint(currentX, currentY, true); // End point
					}
					else if (fullCode == hflexOp) { // hflex
						short startY = currentY;

						currentX += GetStack(0); // dx1
						glyph.AddPoint(currentX, startY, false); // First control
						currentX += GetStack(1); // dx2
						currentY += GetStack(2); // dy2
						glyph.AddPoint(currentX, currentY, false); // First neighbour contrl
						currentX += GetStack(3); // dx3
						glyph.AddPoint(currentX, currentY, true); // Joining point
						currentX += GetStack(4); // dx4
						glyph.AddPoint(currentX, currentY, false); // Second neighbour control
						currentX += GetStack(5); // dx5
						glyph.AddPoint(currentX, startY, false); // Last control
						currentX += GetStack(6); // dx6
						glyph.AddPoint(currentX, startY, true); // End point

						currentY = startY;
					}
					else if (fullCode == hflex1Op) { // hflex1
						short startY = currentY;

						currentX += GetStack(0); // dx1
						currentY += GetStack(1); // dy1
						glyph.AddPoint(currentX, currentY, false); // First control
						currentX += GetStack(2); // dx2
						currentY += GetStack(3); // dy2
						glyph.AddPoint(currentX, currentY, false); // First neighbour contrl
						currentX += GetStack(4); // dx3
						glyph.AddPoint(currentX, currentY, true); // Joining point
						currentX += GetStack(5); // dx4
						glyph.AddPoint(currentX, currentY, false); // Second neighbour control
						currentX += GetStack(6); // dx5
						currentY += GetStack(7); // dy5
						glyph.AddPoint(currentX, currentY, false); // Last control
						currentX += GetStack(8); // dx6
						glyph.AddPoint(currentX, startY, true); // End point

						currentY = startY;
					}
					else if (fullCode == flex1Op) { // flex1
						short startX = currentX;
						short startY = currentY;

						currentX += GetStack(0); // dx1
						currentY += GetStack(1); // dy1
						glyph.AddPoint(currentX, currentY, false); // First control
						currentX += GetStack(2); // dx2
						currentY += GetStack(3); // dy2
						glyph.AddPoint(currentX, currentY, false); // First neighbour contrl
						currentX += GetStack(4); // dx3
						currentY += GetStack(5); // dy3
						glyph.AddPoint(currentX, currentY, true); // Joining point
						currentX += GetStack(6); // dx4
						currentY += GetStack(7); // dy4
						glyph.AddPoint(currentX, currentY, false); // Second neighbour control
						currentX += GetStack(8); // dx5
						currentY += GetStack(9); // dy5
						glyph.AddPoint(currentX, currentY, false); // Last control

						short d6 = GetStack(10); // d6

						short dx = 0, dy = 0;
						for (int i = 0; i < 10; i += 2) {
							dx += GetStack(i); // dxi
							dy += GetStack(i + 1); // dyi
						}

						if (Math.Abs(dx) > Math.Abs(dy)) {
							currentX += d6;
							currentY = startY;
						}
						else {
							currentX = startX;
							currentY += d6;
						}

						glyph.AddPoint(currentX, currentY, true); // End point
					}

					// Finishing Path Operators
					else if (fullCode == endcharOp) { // endchar
						if (!hadStackClear && stack.Count % 2 == 1) { ProcessWidth(); } // May take width argument
						glyph.EndContour();
						encounteredEndChar = true;
						break;
					}

					// Hint operators
					else if (fullCode == hstemOp) { // hstem
						if (!hadStackClear && stack.Count % 2 == 1) { ProcessWidth(); } // May take width argument
						stemCount += stack.Count / 2;
					}
					else if (fullCode == vstemOp) { // vstem
						if (!hadStackClear && stack.Count % 2 == 1) { ProcessWidth(); } // May take width argument
						stemCount += stack.Count / 2;
					}
					else if (fullCode == hstemhmOp) { // hstemhm
						if (!hadStackClear && stack.Count % 2 == 1) { ProcessWidth(); } // May take width argument
						stemCount += stack.Count / 2;
					}
					else if (fullCode == vstemhmOp) { // vstemhm
						if (!hadStackClear && stack.Count % 2 == 1) { ProcessWidth(); } // May take width argument
						stemCount += stack.Count / 2;
					}
					else if (fullCode == hintmaskOp) { // hintmask
						if (!hadStackClear && stack.Count > 0) { ProcessWidth(); } // May take width argument
						stemCount += stack.Count / 2;
						int numBytes = GetStemBytes(stemCount);
						//Console.WriteLine($"Hint mask read {numBytes} bytes (stem count {stemCount})");
						_ = reader.ReadBytes(numBytes);
					}
					else if (fullCode == cntrmaskOp) { // cntrmask
						if (!hadStackClear && stack.Count > 0) { ProcessWidth(); } // May take width argument
						stemCount += stack.Count / 2;
						int numBytes = GetStemBytes(stemCount);
						//Console.WriteLine($"Centre mask read {numBytes} bytes (stem count {stemCount})");
						_ = reader.ReadBytes(numBytes);
					}

					// Arithmetic Operators
					else if (fullCode == absOp) { // abs
						object val = PopStack();
						if (val is long longVal) {
							val = Math.Abs(longVal);
						}
						else if (val is float floatVal) {
							val = Math.Abs(floatVal);
						}
						stack.Add(val);
						clearStack = false;
					}
					else if (fullCode == addOp) { // add
						object num2 = PopStack();
						object num1 = PopStack();
						if (num1 is long long1 && num2 is long long2) {
							stack.Add(long1 + long2);
						}
						else if (num1 is float float1 && num2 is float float2) {
							stack.Add(float1 + float2);
						}
						else if (num1 is long long1f && num2 is float float2l) {
							stack.Add(long1f + float2l);
						}
						else if (num1 is float float1l && num2 is long long2f) {
							stack.Add(float1l + long2f);
						}
						else {
							throw new FormatException("Invalid value for add operator.");
						}
						clearStack = false;
					}
					else if (fullCode == subOp) { // sub
						object num2 = PopStack();
						object num1 = PopStack();
						if (num1 is long long1 && num2 is long long2) {
							stack.Add(long1 - long2);
						}
						else if (num1 is float float1 && num2 is float float2) {
							stack.Add(float1 - float2);
						}
						else if (num1 is long long1f && num2 is float float2l) {
							stack.Add(long1f - float2l);
						}
						else if (num1 is float float1l && num2 is long long2f) {
							stack.Add(float1l - long2f);
						}
						else {
							throw new FormatException("Invalid value for sub operator.");
						}
						clearStack = false;
					}
					else if (fullCode == divOp) { // div
						object num2 = PopStack();
						object num1 = PopStack();
						if (num1 is long long1 && num2 is long long2) {
							stack.Add(long1 / long2);
						}
						else if (num1 is float float1 && num2 is float float2) {
							stack.Add(float1 / float2);
						}
						else if (num1 is long long1f && num2 is float float2l) {
							stack.Add(long1f / float2l);
						}
						else if (num1 is float float1l && num2 is long long2f) {
							stack.Add(float1l / long2f);
						}
						else {
							throw new FormatException("Invalid value for div operator.");
						}
						clearStack = false;
					}
					else if (fullCode == negOp) { // neg
						object val = PopStack();
						if (val is long longVal) {
							val = -longVal;
						}
						else if (val is float floatVal) {
							val = -floatVal;
						}
						stack.Add(val);
						clearStack = false;
					}
					else if (fullCode == randomOp) { // random
						Random rng = new Random();
						stack.Add((float)(1.0 - rng.NextDouble())); // (0, 1]
						clearStack = false;
					}
					else if (fullCode == mulOp) { // mul
						object num2 = PopStack();
						object num1 = PopStack();
						if (num1 is long long1 && num2 is long long2) {
							stack.Add(long1 * long2);
						}
						else if (num1 is float float1 && num2 is float float2) {
							stack.Add(float1 * float2);
						}
						else if (num1 is long long1f && num2 is float float2l) {
							stack.Add(long1f * float2l);
						}
						else if (num1 is float float1l && num2 is long long2f) {
							stack.Add(float1l * long2f);
						}
						else {
							throw new FormatException("Invalid value for mul operator.");
						}
						clearStack = false;
					}
					else if (fullCode == sqrtOp) { // sqrt
						object val = PopStack();
						if (val is long longVal) {
							val = (float)Math.Sqrt(longVal);
						}
						else if (val is float floatVal) {
							val = (float)Math.Sqrt(floatVal);
						}
						stack.Add(val);
						clearStack = false;
					}
					else if (fullCode == dropOp) { // drop
						PopStack();
						clearStack = false;
					}
					else if (fullCode == exchOp) { // exch
						object num2 = PopStack();
						object num1 = PopStack();
						stack.Add(num2);
						stack.Add(num1);
						clearStack = false;
					}
					else if (fullCode == indexOp) { // index
						int idx = (int)((long)PopStack());
						object numi;
						if (idx < 0) {
							numi = stack[^1];
						}
						else {
							numi = stack[^(idx + 1)];
						}
						stack.Add(numi);
						clearStack = false;
					}
					else if (fullCode == rollOp) { // roll
						int j = (int)((long)PopStack());
						int n = (int)((long)PopStack());
						object[] rollingVals = stack.ToArray()[^n..];
						for (int i = 0; i < n; i++) {
							stack[^(n + i)] = rollingVals[(i - j) % n]; // Is that right?
						}
						clearStack = false;
					}
					else if (fullCode == dupOp) { // dup
						stack.Add(stack[^1]);
						clearStack = false;
					}

					// Storage Operators
					else if (fullCode == putOp) { // put
						long i = (long)PopStack(); // Is this the right order of pops?
						object val = PopStack();
						transientArray[i] = val;
						clearStack = false;
					}
					else if (fullCode == getOp) { // get
						long i = (long)PopStack();
						stack.Add(transientArray[i]);
						clearStack = false;
					}

					// Conditional Operators
					else if (fullCode == andOp) { // and
						object num2 = PopStack();
						object num1 = PopStack();
						stack.Add(ConvertBool(num1) && ConvertBool(num2));
						clearStack = false;
					}
					else if (fullCode == orOp) { // or
						object num2 = PopStack();
						object num1 = PopStack();
						stack.Add(ConvertBool(num1) || ConvertBool(num2));
						clearStack = false;
					}
					else if (fullCode == notOp) { // not
						object num1 = PopStack();
						stack.Add(!ConvertBool(num1));
						clearStack = false;
					}
					else if (fullCode == eqOp) { // eq
						object num2 = PopStack();
						object num1 = PopStack();
						if (num1 is long long1 && num2 is long long2) {
							stack.Add(long1 == long2);
						}
						else if (num1 is float float1 && num2 is float float2) {
							stack.Add(float1 == float2);
						}
						else if (num1 is long long1f && num2 is float float2l) {
							stack.Add(long1f == float2l);
						}
						else if (num1 is float float1l && num2 is long long2f) {
							stack.Add(float1l == long2f);
						}
						else {
							throw new FormatException("Invalid value for mul operator.");
						}
						clearStack = false;
					}
					else if (fullCode == ifelseOp) { // ifelse
						double v2 = ConvertDouble(PopStack());
						double v1 = ConvertDouble(PopStack());
						object s2 = PopStack();
						object s1 = PopStack();

						if(v1 <= v2) {
							stack.Add(s1);
						}
						else {
							stack.Add(s2);
						}

						clearStack = false;
					}

					// Subroutine Operators
					else if (fullCode == callsubrOp || fullCode == callgsubrOp) { // callsubr or callgsubr
						CFFIndexData subroutineIndexToUse;

						if (fullCode == callsubrOp) {
							if (subrsIndex is null) {
								throw new FormatException("Local subroutine call but no local subroutine table provided.");
							}
							subroutineIndexToUse = subrsIndex;
						}
						else {
							subroutineIndexToUse = globalSubrIndex;
						}

						long subr = (long)PopStack();
						if (subroutineIndexToUse.Count < 1240) {
							subr += 107;
						}
						else if (subroutineIndexToUse.Count < 33900) {
							subr += 1131;
						}
						else {
							subr += 32768;
						}

						long subrPos = subroutineIndexToUse.offsets[subr];
						uint subrLength = subroutineIndexToUse.lengths[subr];

						long previousPos = reader.Position;
						reader.Position = subrPos; // Set position to start of subroutine

						ReadCharString(reader, length, globalSubrIndex, subrsIndex, glyph, stack, transientArray, ref hadStackClear, ref stemCount, ref currentX, ref currentY, subroutineDepth + 1, out encounteredEndChar);

						reader.Position = previousPos; // Return to where we were

						if (encounteredEndChar) {
							break;
						}

						clearStack = false;
					}
					else if (fullCode == returnOp) { // return
						if (subroutineDepth < 1) {
							throw new FormatException("Cannot return, not currently executing subroutine.");
						}

						break;

						//clearStack = false;
					}

					// Deprecated
					else if (fullCode == dotsectionOp) {
						// Deprecated operator
						// Treated as no-op by Adobe, so we will too
					}

					// Fallback
					else {
						throw new FormatException($"Unrecognised Type2 CharString operator code ({fullCode:X4}).");
					}

					if (clearStack) {
						stack.Clear();
						hadStackClear = true;
					}
				}

			}

			//Console.WriteLine("End call");
		}

		private const ushort hstemOp = 0x1;
		private const ushort vstemOp = 0x3;
		private const ushort vmovetoOp = 0x4;
		private const ushort rlinetoOp = 0x5;
		private const ushort hlinetoOp = 0x6;
		private const ushort vlinetoOp = 0x7;
		private const ushort rrcurvetoOp = 0x8;
		private const ushort callsubrOp = 0x0a;
		private const ushort returnOp = 0x0b;
		private const ushort endcharOp = 0x0e;
		private const ushort hstemhmOp = 0x12;
		private const ushort hintmaskOp = 0x13;
		private const ushort cntrmaskOp = 0x14;
		private const ushort rmovetoOp = 0x15;
		private const ushort hmovetoOp = 0x16;
		private const ushort vstemhmOp = 0x17;
		private const ushort rcurvelineOp = 0x18;
		private const ushort rlinecurveOp = 0x19;
		private const ushort vvcurvetoOp = 0x1a;
		private const ushort hhcurvetoOp = 0x1b;
		private const ushort callgsubrOp = 0x1d;
		private const ushort vhcurvetoOp = 0x1e;
		private const ushort hvcurvetoOp = 0x1f;
		private const ushort andOp = 0x0c03;
		private const ushort orOp = 0x0c04;
		private const ushort notOp = 0x0c05;
		private const ushort absOp = 0x0c09;
		private const ushort addOp = 0x0c0a;
		private const ushort subOp = 0x0c0b;
		private const ushort divOp = 0x0c0c;
		private const ushort negOp = 0x0c0e;
		private const ushort eqOp = 0x0c0f;
		private const ushort dropOp = 0x0c12;
		private const ushort putOp = 0x0c14;
		private const ushort getOp = 0x0c15;
		private const ushort ifelseOp = 0x0c16;
		private const ushort randomOp = 0x0c17;
		private const ushort mulOp = 0x0c18;
		private const ushort sqrtOp = 0x0c1a;
		private const ushort dupOp = 0x0c1b;
		private const ushort exchOp = 0x0c1c;
		private const ushort indexOp = 0x0c1d;
		private const ushort rollOp = 0x0c1e;
		private const ushort hflexOp = 0x0c22;
		private const ushort flexOp = 0x0c23;
		private const ushort hflex1Op = 0x0c24;
		private const ushort flex1Op = 0x0c25;

		// Deprecated
		private const ushort dotsectionOp = 0x0c00;

	}

}
