using System;
using System.Collections.Generic;

namespace GeboPdf.Fonts.TrueType {

	public class TrueTypeCMapTable {

		public readonly ushort verson;
		public readonly ushort numSubtables;

		public readonly Dictionary<CMapTableIdentifier, TrueTypeCMapSubtable> subtables;

		internal TrueTypeCMapTable(ushort verson, ushort numSubtables, Dictionary<CMapTableIdentifier, TrueTypeCMapSubtable> subtables) {
			this.verson = verson;
			this.numSubtables = numSubtables;
			this.subtables = subtables;
		}

		internal static TrueTypeCMapTable Read(FontFileReader reader, long offset) {
			reader.Position = offset;

			ushort version = reader.ReadUInt16();
			ushort numSubtables = reader.ReadUInt16();

			Dictionary<CMapTableIdentifier, TrueTypeCMapSubtable> subtables = new Dictionary<CMapTableIdentifier, TrueTypeCMapSubtable>();

			for (int i = 0; i < numSubtables; i++) {
				ushort platformID = reader.ReadUInt16();
				ushort encodingID = reader.ReadUInt16();
				uint subtableOffset = reader.ReadUInt32();

				long old = reader.Position;

				TrueTypeCMapSubtable? subtable = TrueTypeCMapSubtable.Read(reader, offset + subtableOffset, platformID, encodingID, subtableOffset);
				if (subtable != null) {
					subtables[new CMapTableIdentifier(subtable.platformID, subtable.encodingID, subtable.language)] = subtable;
				}

				reader.Position = old;
			}

			return new TrueTypeCMapTable(version, numSubtables, subtables);
		}

	}

	public readonly struct CMapTableIdentifier {

		public readonly ushort platformID;
		public readonly ushort encodingID;
		public readonly ushort languageID;

		public CMapTableIdentifier(ushort platformID, ushort encodingID, ushort languageID) {
			this.platformID = platformID;
			this.encodingID = encodingID;
			this.languageID = languageID;
		}

		public override bool Equals(object? obj) {
			if (obj is CMapTableIdentifier other) {
				return platformID == other.platformID && encodingID == other.encodingID && languageID == other.languageID;
			}
			else {
				return false;
			}
		}

		public override int GetHashCode() {
			return HashCode.Combine(platformID, encodingID, languageID);
		}

		public static bool operator ==(CMapTableIdentifier left, CMapTableIdentifier right) {
			return left.Equals(right);
		}

		public static bool operator !=(CMapTableIdentifier left, CMapTableIdentifier right) {
			return !(left == right);
		}
	}

	public class TrueTypeCMapSubtable {

		public readonly ushort platformID;
		public readonly ushort encodingID;
		public readonly uint subtableOffset;
		public readonly ushort format;
		public readonly ushort language;
		public readonly IReadOnlyDictionary<uint, ushort> cidMap;

		internal TrueTypeCMapSubtable(ushort platformID, ushort encodingID, uint subtableOffset, ushort format, ushort language, IReadOnlyDictionary<uint, ushort> cidMap) {
			this.platformID = platformID;
			this.encodingID = encodingID;
			this.subtableOffset = subtableOffset;
			this.format = format;
			this.language = language;
			this.cidMap = cidMap;
		}

		internal static TrueTypeCMapSubtable? Read(FontFileReader reader, long offset, ushort platformID, ushort encodingID, uint subtableOffset) {
			reader.Position = offset;

			ushort format = reader.ReadUInt16();

			ushort language;
			Dictionary<uint, ushort>? cidMap;

			if (format == 0) {
				cidMap = Read0(reader, out language);
			}
			else if (format == 2) {
				cidMap = Read2(reader, out language);
			}
			else if (format == 4) {
				cidMap = Read4(reader, out language);
			}
			else if (format == 6) {
				cidMap = Read6(reader, out language);
			}
			else if (format == 8) {
				cidMap = Read8(reader, out uint language32);
				language = (ushort)language32;
			}
			else if (format == 10) {
				cidMap = Read10(reader, out uint language32);
				language = (ushort)language32;
			}
			else if (format == 12) {
				cidMap = Read12(reader, out uint language32);
				language = (ushort)language32;
			}
			else if (format == 14) {
				cidMap = Read14(reader, out language);
			}
			else {
				throw new FormatException("Unsupported cmap subtable format: " + format);
			}

			if (cidMap != null) {
				return new TrueTypeCMapSubtable(platformID, encodingID, subtableOffset, format, language, cidMap);
			}
			else {
				return null;
			}
		}

		private static Dictionary<uint, ushort> Read0(FontFileReader reader, out ushort language) {
			ushort length = reader.ReadUInt16();
			if (length != 262) { throw new FormatException($"Invalid length for cmap subtable of format 0 ({length})."); }

			language = reader.ReadUInt16();

			Dictionary<uint, ushort> cidMap = new Dictionary<uint, ushort>();
			for (uint i = 0; i < 256; i++) {
				byte glyphIdx = reader.ReadUInt8();
				cidMap.Add(i, glyphIdx);
			}

			return cidMap;
		}

		private static Dictionary<uint, ushort>? Read2(FontFileReader reader, out ushort language) {
			/*
			ushort length = reader.ReadUInt16();
			language = reader.ReadUInt16();
			
			ushort[] subHeaderKeys = new ushort[256];
			int numSubHeaders = 0;
			for (int i = 0; i < 256; i++) {
				subHeaderKeys[i] = reader.ReadUInt16();

				if(subHeaderKeys[i] > numSubHeaders) {
					numSubHeaders = subHeaderKeys[i];
				}
			}
			numSubHeaders = 1 + numSubHeaders / 8;

			SubHeader subHeaders[];
			uint16 glyphIdArray[];

			Dictionary<uint, ushort> cidMap = new Dictionary<uint, ushort>();
			return cidMap;
			*/
			language = 0;
			return null;
		}

		private static Dictionary<uint, ushort> Read4(FontFileReader reader, out ushort language) {

			/*long offset*/ _ = reader.Position;

			ushort length = reader.ReadUInt16();
			language = reader.ReadUInt16();
			ushort segCountX2 = reader.ReadUInt16();
			ushort segCount = (ushort)(segCountX2 / 2);

			//Console.WriteLine($"Read4: {offset} {length} {offset+length} {reader.Length} / {segCountX2} {segCount}");

			//ushort searchRange = reader.ReadUInt16();
			//ushort entrySelector = reader.ReadUInt16();
			//ushort rangeShift = reader.ReadUInt16();
			reader.SkipUInt16(3);

			ushort[] endCodes = new ushort[segCount];
			for (int i = 0; i < segCount; i++) {
				endCodes[i] = reader.ReadUInt16();
			}

			reader.SkipUInt16(1); //ushort reservedPad = reader.ReadUInt16();

			ushort[] startCodes = new ushort[segCount];
			for (int i = 0; i < segCount; i++) {
				startCodes[i] = reader.ReadUInt16();
			}

			short[] idDeltas = new short[segCount];
			for (int i = 0; i < segCount; i++) {
				idDeltas[i] = reader.ReadInt16();
			}

			ushort[] idRangeOffsets = new ushort[segCount];
			for (int i = 0; i < segCount; i++) {
				idRangeOffsets[i] = reader.ReadUInt16();
			}

			int glyphIndexArrayLength = length - 16 - segCount * 2 * 4;
			ushort[] glyphIndexArray = new ushort[glyphIndexArrayLength];
			for (int i = 0; i < glyphIndexArrayLength; i++) {
				glyphIndexArray[i] = reader.ReadUInt16();
			}

			Dictionary<uint, ushort> cidMap = new Dictionary<uint, ushort>();

			for (int seg = 0; seg < segCount; seg++) {

				ushort idRangeOffset = idRangeOffsets[seg];

				for (uint code = startCodes[seg]; code <= endCodes[seg]; code++) {

					//Console.WriteLine($"Code {code} (start {startCodes[seg]}, end {endCodes[seg]})");

					ushort glyphIdx;
					if (idRangeOffset == 0) {
						glyphIdx = (ushort)((code + idDeltas[seg]) % 65536);
					}
					else {
						//glyphIdx = glyphIndexArray[idRangeOffset - segCount + (code - startCodes[seg])];
						long glyphIndexArrayIdx = seg - segCount + idRangeOffset / 2 + (code - startCodes[seg]); // https://stackoverflow.com/a/61804360/11002708
						if (glyphIndexArrayIdx >= 0 && glyphIndexArrayIdx < glyphIndexArrayLength) {
							glyphIdx = glyphIndexArray[glyphIndexArrayIdx];
						}
						else {
							glyphIdx = 0;
						}

						if (glyphIdx != 0) {
							glyphIdx = (ushort)((glyphIdx + idDeltas[seg]) % 65536);
						}
					}

					cidMap.Add(code, glyphIdx);
				}

			}

			return cidMap;
		}

		private static Dictionary<uint, ushort> Read6(FontFileReader reader, out ushort language) {
			reader.SkipUInt16(1); //ushort length = reader.ReadUInt16();
			language = reader.ReadUInt16();

			ushort firstCode = reader.ReadUInt16();
			ushort entryCount = reader.ReadUInt16();

			Dictionary<uint, ushort> cidMap = new Dictionary<uint, ushort>();

			for (uint i = 0; i < entryCount; i++) {
				ushort glyphIndex = reader.ReadUInt16();
				cidMap.Add((uint)(firstCode + i), glyphIndex);
			}

			return cidMap;
		}

		private static Dictionary<uint, ushort> Read8(FontFileReader reader, out uint language) {
			reader.SkipUInt16(1); //ushort reserved = reader.ReadUInt16();
			reader.SkipUInt32(1); //uint length = reader.ReadUInt32();
			language = reader.ReadUInt32();

			byte[] is32 = new byte[65536];
			for (int i = 0; i < is32.Length; i++) {
				is32[i] = reader.ReadUInt8();
			}

			uint nGroups = reader.ReadUInt32();

			Dictionary<uint, ushort> cidMap = new Dictionary<uint, ushort>();

			for (ulong g = 0; g < nGroups; g++) {
				uint startCharCode = reader.ReadUInt32();
				uint endCharCode = reader.ReadUInt32();
				uint startGlyphCode = reader.ReadUInt32();

				uint numChars = endCharCode - startCharCode + 1;

				for (ulong c = 0; c < numChars; c++) {
					uint charCode = (uint)(startCharCode + c);
					ushort glyphIdx = (ushort)(startGlyphCode + c);

					cidMap.Add(charCode, glyphIdx);
				}
			}

			return cidMap;
		}

		private static Dictionary<uint, ushort> Read10(FontFileReader reader, out uint language) {
			reader.SkipUInt16(1); //ushort reserved = reader.ReadUInt16();
			reader.SkipUInt32(1); //uint length = reader.ReadUInt32();
			language = reader.ReadUInt32();

			uint startCharCode = reader.ReadUInt32();
			uint numChars = reader.ReadUInt32();

			Dictionary<uint, ushort> cidMap = new Dictionary<uint, ushort>();

			for (uint i = 0; i < numChars; i++) {
				ushort glyphIndex = reader.ReadUInt16();
				cidMap.Add(startCharCode + i, glyphIndex);
			}

			return cidMap;
		}

		private static Dictionary<uint, ushort> Read12(FontFileReader reader, out uint language) {
			reader.SkipUInt16(1); //ushort reserved = reader.ReadUInt16();
			reader.SkipUInt32(1); //uint length = reader.ReadUInt32();
			language = reader.ReadUInt32();
			uint nGroups = reader.ReadUInt32();

			Dictionary<uint, ushort> cidMap = new Dictionary<uint, ushort>();

			for (uint g = 0; g < nGroups; g++) {
				uint startCharCode = reader.ReadUInt32();
				uint endCharCode = reader.ReadUInt32();
				uint startGlyphCode = reader.ReadUInt32();

				uint numChars = endCharCode - startCharCode + 1;

				for (uint c = 0; c < numChars; c++) {
					uint charCode = startCharCode + c;
					ushort glyphIndex = (ushort)(startGlyphCode + c);
					cidMap.Add(charCode, glyphIndex);
				}
			}

			return cidMap;
		}

		private static Dictionary<uint, ushort>? Read14(FontFileReader reader, out ushort language) {
			/*
			Dictionary<uint, ushort> cidMap = new Dictionary<uint, ushort>();
			return cidMap;
			*/
			language = 0;
			return null;
		}

	}

}
