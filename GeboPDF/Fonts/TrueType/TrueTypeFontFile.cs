using System;
using System.Collections.Generic;
using System.IO;

namespace GeboPdf.Fonts.TrueType {

	public class TrueTypeFontFile {

		public readonly uint scalerType;

		public readonly ushort numTables;
		public readonly ushort searchRange;
		public readonly ushort entrySelector;
		public readonly ushort rangeShift;

		public readonly IReadOnlyDictionary<string, TrueTypeFontTable> tables;

		public readonly ushort numGlyphs;

		public readonly TrueTypeHeadTable head;
		public readonly TrueTypeNameTable name;
		public readonly TrueTypeHorizontalHeaderTable hhea;
		public readonly TrueTypeIndexToLocationTable? loca;
		public readonly TrueTypeGlyphTable? glyf;
		public readonly TrueTypeHorizontalMetricsTable hmtx;
		public readonly TrueTypeCMapTable cmap;
		public readonly TrueTypeOS2Table? os2;

		public EmbeddingFlags EmbeddingFlags => os2?.fsType ?? EmbeddingFlags.EditableEmbedding;

		private TrueTypeFontFile(
				uint scalerType, ushort numTables, ushort searchRange, ushort entrySelector, ushort rangeShift,
				IReadOnlyDictionary<string, TrueTypeFontTable> tables,
				ushort numGlyphs,
				TrueTypeHeadTable head,
				TrueTypeNameTable name,
				TrueTypeHorizontalHeaderTable hhea,
				TrueTypeIndexToLocationTable? loca,
				TrueTypeGlyphTable? glyf,
				TrueTypeHorizontalMetricsTable hmtx,
				TrueTypeCMapTable cmap,
				TrueTypeOS2Table? os2
			) {

			this.scalerType = scalerType;
			this.numTables = numTables;
			this.searchRange = searchRange;
			this.entrySelector = entrySelector;
			this.rangeShift = rangeShift;
			this.tables = tables;

			this.numGlyphs = numGlyphs;

			this.head = head;
			this.name = name;
			this.hhea = hhea;
			this.loca = loca;
			this.glyf = glyf;
			this.hmtx = hmtx;
			this.cmap = cmap;
			this.os2 = os2;
		}

		public static TrueTypeFontFile Open(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return Open(fontReader);
			}
		}

		public static TrueTypeFontFile Open(FontFileReader reader) {

			uint scalerType = reader.ReadUInt32();

			ushort numTables = reader.ReadUInt16();
			ushort searchRange = reader.ReadUInt16();
			ushort entrySelector = reader.ReadUInt16();
			ushort rangeShift = reader.ReadUInt16();

			Dictionary<string, TrueTypeFontTable> tables = new Dictionary<string, TrueTypeFontTable>();

			for (ushort i = 0; i < numTables; i++) {
				string tag = reader.ReadASCIIString(4);
				uint checksum = reader.ReadUInt32();
				uint offset = reader.ReadUInt32();
				uint length = reader.ReadUInt32();

				/*
				if(offset + length >= reader.Length) {
					throw new FormatException($"Invalid font table ({tag}) length provided (longer than font file).");
				}
				*/

				/*
				if (tag != "head") {
					long old = reader.Position;
					reader.Position = offset;
					uint sum = 0U;
					uint nlongs = (length + 3U) / 4U; // ((length + 3) / 4) | 0;
					while (nlongs-- > 0U) {
						////sum = (sum + reader.ReadUInt32() & 0xffffffff) >> 0;
						//sum = sum + reader.ReadUInt32() & 0xffffffff;
						sum += reader.ReadUInt32();
					}

					reader.Position = old;

					if (sum != checksum) {
						//throw new InvalidDataException("Checksum does not match for table: " + tag);
						Console.WriteLine($"Checksum does not match for table {tag}");
					}
				}
				*/

				tables.Add(tag, new TrueTypeFontTable(tag, checksum, offset, length));
			}

			///////////// Head table
			if (!tables.TryGetValue("head", out TrueTypeFontTable? headTable)) {
				throw new FormatException("No head table.");
			}
			TrueTypeHeadTable head = TrueTypeHeadTable.Read(reader, headTable.offset);

			///////////// Name table
			if (!tables.TryGetValue("name", out TrueTypeFontTable? nameTable)) {
				throw new FormatException("No name table.");
			}
			TrueTypeNameTable name = TrueTypeNameTable.Read(reader, nameTable.offset);

			///////////// maxp table
			if (!tables.TryGetValue("maxp", out TrueTypeFontTable? maxpTable)) {
				throw new FormatException("No maxp table.");
			}
			reader.Position = maxpTable.offset;
			/*Fixed version =*/ reader.ReadFixed();
			ushort numGlyphs = reader.ReadUInt16();

			///////////// Horizontal header table
			if (!tables.TryGetValue("hhea", out TrueTypeFontTable? hheaTable)) {
				throw new FormatException("No hhea table.");
			}
			TrueTypeHorizontalHeaderTable hhea = TrueTypeHorizontalHeaderTable.Read(reader, hheaTable.offset);

			////// TrueType loca and glyf tables
			TrueTypeIndexToLocationTable? loca = null;
			TrueTypeGlyphTable? glyf = null;
			if (tables.ContainsKey("loca") || tables.ContainsKey("glyf")) {
				///////////// Location to Index table
				if (!tables.TryGetValue("loca", out TrueTypeFontTable? locaTable)) {
					throw new FormatException("No loca table.");
				}
				loca = TrueTypeIndexToLocationTable.Read(reader, locaTable.offset, numGlyphs, head.indexToLocFormat);

				///////////// Glyph Data table
				if (!tables.TryGetValue("glyf", out TrueTypeFontTable? glyfTable)) {
					throw new FormatException("No glyf table.");
				}
				glyf = TrueTypeGlyphTable.Read(reader, glyfTable.offset, loca);
			}

			///////////// Horizontal Metrics table
			if (!tables.TryGetValue("hmtx", out TrueTypeFontTable? hmtxTable)) {
				throw new FormatException("No hmtx table.");
			}
			TrueTypeHorizontalMetricsTable hmtx = TrueTypeHorizontalMetricsTable.Read(reader, hmtxTable.offset, numGlyphs, hhea.numOfLongHorMetrics);

			///////////// CMap table
			if (!tables.TryGetValue("cmap", out TrueTypeFontTable? cmapTable)) {
				throw new FormatException("No cmap table.");
			}
			TrueTypeCMapTable cmap = TrueTypeCMapTable.Read(reader, cmapTable.offset);

			///////////// OS/2 table
			TrueTypeOS2Table? os2;
			if (tables.TryGetValue("OS/2", out TrueTypeFontTable? os2Table)) {
				os2 = TrueTypeOS2Table.Read(reader, os2Table.offset, os2Table.length);
			}
			else {
				os2 = null;
			}

			return new TrueTypeFontFile(
				scalerType, numTables, searchRange, entrySelector, rangeShift,
				tables,
				numGlyphs,
				head, name, hhea, loca, glyf, hmtx, cmap, os2);
		}

	}

	public class TrueTypeFontTable {

		public readonly string tag;
		public readonly uint checksum;
		public readonly uint offset;
		public readonly uint length;

		internal TrueTypeFontTable(string tag, uint checksum, uint offset, uint length) {
			this.tag = tag;
			this.checksum = checksum;
			this.offset = offset;
			this.length = length;
		}
	}

	public class TrueTypeFontFileData {

		public readonly ushort numGlyphs;

		public readonly TrueTypeHeadTable head;
		public readonly TrueTypeNameTable name;
		public readonly TrueTypeOS2Table? os2;

		public EmbeddingFlags EmbeddingFlags => os2?.fsType ?? EmbeddingFlags.EditableEmbedding;

		public TrueTypeFontFileData(ushort numGlyphs, TrueTypeHeadTable head, TrueTypeNameTable name, TrueTypeOS2Table? os2) {
			this.numGlyphs = numGlyphs;
			this.head = head;
			this.name = name;
			this.os2 = os2;
		}

		public static TrueTypeFontFileData Open(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return Open(fontReader);
			}
		}

		public static TrueTypeFontFileData Open(FontFileReader reader) {

			reader.SkipUInt32(1); // scalerType
			ushort numTables = reader.ReadUInt16();
			reader.SkipUInt16(3); // searchRange, entrySelector, rangeShift

			Dictionary<string, TrueTypeFontTable> tables = new Dictionary<string, TrueTypeFontTable>();

			for (ushort i = 0; i < numTables; i++) {
				string tag = reader.ReadASCIIString(4);
				uint checksum = reader.ReadUInt32();
				uint offset = reader.ReadUInt32();
				uint length = reader.ReadUInt32();

				tables.Add(tag, new TrueTypeFontTable(tag, checksum, offset, length));
			}

			///////////// Head table
			if (!tables.TryGetValue("head", out TrueTypeFontTable? headTable)) {
				throw new FormatException("No head table.");
			}
			TrueTypeHeadTable head = TrueTypeHeadTable.Read(reader, headTable.offset);

			///////////// Name table
			if (!tables.TryGetValue("name", out TrueTypeFontTable? nameTable)) {
				throw new FormatException("No name table.");
			}
			TrueTypeNameTable name = TrueTypeNameTable.Read(reader, nameTable.offset);

			///////////// maxp table
			if (!tables.TryGetValue("maxp", out TrueTypeFontTable? maxpTable)) {
				throw new FormatException("No maxp table.");
			}
			reader.Position = maxpTable.offset;
			/*Fixed version =*/
			reader.ReadFixed();
			ushort numGlyphs = reader.ReadUInt16();

			///////////// OS/2 table
			TrueTypeOS2Table? os2;
			if (tables.TryGetValue("OS/2", out TrueTypeFontTable? os2Table)) {
				os2 = TrueTypeOS2Table.Read(reader, os2Table.offset, os2Table.length);
			}
			else {
				os2 = null;
			}

			return new TrueTypeFontFileData(
				numGlyphs,
				head, name, os2);
		}
	}

}
