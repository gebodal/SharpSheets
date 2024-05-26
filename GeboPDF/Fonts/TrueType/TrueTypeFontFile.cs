using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeboPdf.Fonts.TrueType {

	public enum GlyphOutlineLayout { Unknown, TrueType, OpenType }

	public class TrueTypeFontFile {

		public readonly uint scalerType;

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
		public readonly TrueTypePostTable? post;

		public readonly TrueTypeKerningTable? kern;


		public readonly OpenTypeGlyphDefinitionTable? gdef;
		public readonly OpenTypeGlyphSubstitutionTable? gsub;
		public readonly OpenTypeGlyphPositioningTable? gpos;

		public EmbeddingFlags EmbeddingFlags => os2?.fsType ?? EmbeddingFlags.EditableEmbedding;

		public ushort UnitsPerEm => head.unitsPerEm;

		public GlyphOutlineLayout OutlineLayout { get; }

		private TrueTypeFontFile(
				uint scalerType,
				IReadOnlyDictionary<string, TrueTypeFontTable> tables,
				ushort numGlyphs,
				TrueTypeHeadTable head,
				TrueTypeNameTable name,
				TrueTypeHorizontalHeaderTable hhea,
				TrueTypeIndexToLocationTable? loca,
				TrueTypeGlyphTable? glyf,
				TrueTypeHorizontalMetricsTable hmtx,
				TrueTypeCMapTable cmap,
				TrueTypeOS2Table? os2,
				TrueTypePostTable? post,
				TrueTypeKerningTable? kern,
				OpenTypeGlyphDefinitionTable? gdef,
				OpenTypeGlyphSubstitutionTable? gsub,
				OpenTypeGlyphPositioningTable? gpos,
				GlyphOutlineLayout glyphOutlineLayout
			) {

			this.scalerType = scalerType;
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
			this.post = post;

			this.kern = kern;

			this.gdef = gdef;
			this.gsub = gsub;
			this.gpos = gpos;

			this.OutlineLayout = glyphOutlineLayout;
		}

		public static TrueTypeFontFile Open(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return Open(fontReader);
			}
		}

		internal static TrueTypeFontTable[] ReadHeader(FontFileReader reader,
			out uint scalerType, out ushort searchRange, out ushort entrySelector, out ushort rangeShift) {

			scalerType = reader.ReadUInt32();

			ushort numTables = reader.ReadUInt16();
			searchRange = reader.ReadUInt16();
			entrySelector = reader.ReadUInt16();
			rangeShift = reader.ReadUInt16();

			TrueTypeFontTable[] tables = new TrueTypeFontTable[numTables];

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
				tables[i] = new TrueTypeFontTable(tag, checksum, offset, length);
			}

			return tables;
		}

		internal static IReadOnlyDictionary<string, TrueTypeFontTable> ReadHeaderDict(FontFileReader reader,
			out uint scalerType, out ushort searchRange, out ushort entrySelector, out ushort rangeShift) {

			TrueTypeFontTable[] tables = ReadHeader(reader,
				out scalerType, out searchRange, out entrySelector, out rangeShift);

			return tables.ToDictionary(t => t.tag);
		}

		public static TrueTypeFontFile Open(FontFileReader reader) {

			IReadOnlyDictionary<string, TrueTypeFontTable> tables = ReadHeaderDict(reader,
				out uint scalerType, out _, out _, out _);

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

			///////////// PostScript table
			TrueTypePostTable? post;
			if (tables.TryGetValue("post", out TrueTypeFontTable? postTable)) {
				post = TrueTypePostTable.Read(reader, postTable.offset);
			}
			else {
				post = null;
			}

			///////////// Kerning table
			TrueTypeKerningTable? kern = null;
			if (tables.TryGetValue("kern", out TrueTypeFontTable? kernTable)) {
				kern = TrueTypeKerningTable.Read(reader, kernTable.offset);
			}

			///////////// GSUB table
			OpenTypeGlyphDefinitionTable? gdef = null;
			if (tables.TryGetValue("GDEF", out TrueTypeFontTable? gdefTable)) {
				gdef = OpenTypeGlyphDefinitionTable.Read(reader, gdefTable.offset);
			}

			///////////// GSUB table
			OpenTypeGlyphSubstitutionTable? gsub = null;
			if (tables.TryGetValue("GSUB", out TrueTypeFontTable? gsubTable)) {
				gsub = OpenTypeGlyphSubstitutionTable.Read(reader, gsubTable.offset);
			}

			///////////// GPOS table
			OpenTypeGlyphPositioningTable? gpos = null;
			if (tables.TryGetValue("GPOS", out TrueTypeFontTable? gposTable)) {
				gpos = OpenTypeGlyphPositioningTable.Read(reader, gposTable.offset);
			}

			GlyphOutlineLayout outlineLayout = GetGlyphOutlineLayout(tables);

			return new TrueTypeFontFile(
				scalerType,
				tables,
				numGlyphs,
				head, name, hhea, loca, glyf, hmtx, cmap, os2, post,
				kern,
				gdef, gsub, gpos,
				outlineLayout);
		}

		public static GlyphOutlineLayout GetGlyphOutlineLayout(IReadOnlyDictionary<string, TrueTypeFontTable> tables) {
			if(tables.ContainsKey("CFF ")) {
				return GlyphOutlineLayout.OpenType;
			}
			else if (tables.ContainsKey("glyf")) {
				return GlyphOutlineLayout.TrueType;
			}
			else {
				return GlyphOutlineLayout.Unknown;
			}
		}

		internal static T? OpenTable<T>(string fontProgramPath, string tableLabel, Func<FontFileReader, long, T> parser) where T : class {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return OpenTable(fontReader, tableLabel, parser);
			}
		}

		internal static T? OpenTable<T>(FontFileReader reader, string tableLabel, Func<FontFileReader, long, T> parser) where T : class {
			IReadOnlyDictionary<string, TrueTypeFontTable> tables = ReadHeaderDict(reader,
				out _, out _, out _, out _);

			if (tables.TryGetValue(tableLabel, out TrueTypeFontTable? table)) {
				return parser(reader, table.offset);
			}
			else {
				return null;
			}
		}

		public static TrueTypeCMapTable? OpenCmap(string fontProgramPath) {
			return OpenTable(fontProgramPath, "cmap", TrueTypeCMapTable.Read);
		}
		public static TrueTypeCMapTable? OpenCmap(FontFileReader reader) {
			return OpenTable(reader, "cmap", TrueTypeCMapTable.Read);
		}

		public static TrueTypePostTable? OpenPost(string fontProgramPath) {
			return OpenTable(fontProgramPath, "post", TrueTypePostTable.Read);
		}
		public static TrueTypePostTable? OpenPost(FontFileReader reader) {
			return OpenTable(reader, "post", TrueTypePostTable.Read);
		}

		public static OpenTypeGlyphSubstitutionTable? OpenGSUB(string fontProgramPath) {
			return OpenTable(fontProgramPath, "GSUB", OpenTypeGlyphSubstitutionTable.Read);
		}
		public static OpenTypeGlyphSubstitutionTable? OpenGSUB(FontFileReader reader) {
			return OpenTable(reader, "GSUB", OpenTypeGlyphSubstitutionTable.Read);
		}

		public static OpenTypeLayoutTagSet ReadOpenTypeTags(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return ReadOpenTypeTags(fontReader);
			}
		}

		public static OpenTypeLayoutTagSet ReadOpenTypeTags(FontFileReader reader) {
			IReadOnlyDictionary<string, TrueTypeFontTable> tables = ReadHeaderDict(reader,
				out _, out _, out _, out _);

			OpenTypeLayoutTagSet tags = OpenTypeLayoutTagSet.Empty();

			if (tables.TryGetValue("GSUB", out TrueTypeFontTable? gsub)) {
				OpenTypeLayoutTagSet gsubTags = OpenTypeGlyphSubstitutionTable.ReadTags(reader, gsub.offset);
				tags.UnionWith(gsubTags);
			}
			if (tables.TryGetValue("GPOS", out TrueTypeFontTable? gpos)) {
				OpenTypeLayoutTagSet gposTags = OpenTypeGlyphPositioningTable.ReadTags(reader, gpos.offset);
				tags.UnionWith(gposTags);
			}

			return tags;
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
		public GlyphOutlineLayout OutlineLayout { get; }

		public ushort UnitsPerEm => head.unitsPerEm;

		public TrueTypeFontFileData(ushort numGlyphs, TrueTypeHeadTable head, TrueTypeNameTable name, TrueTypeOS2Table? os2, GlyphOutlineLayout glyphOutlineLayout) {
			this.numGlyphs = numGlyphs;
			this.head = head;
			this.name = name;
			this.os2 = os2;
			this.OutlineLayout = glyphOutlineLayout;
		}

		public static TrueTypeFontFileData Open(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return Open(fontReader);
			}
		}

		public static TrueTypeFontFileData Open(FontFileReader reader) {

			IReadOnlyDictionary<string, TrueTypeFontTable> tables = TrueTypeFontFile.ReadHeaderDict(reader,
				out _, out _, out _, out _);

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

			GlyphOutlineLayout outlineLayout = TrueTypeFontFile.GetGlyphOutlineLayout(tables);

			return new TrueTypeFontFileData(
				numGlyphs,
				head, name, os2,
				outlineLayout);
		}
	}

	public class TrueTypeFontFileOutlines {

		public readonly ushort numGlyphs;

		public readonly TrueTypeHeadTable head;
		public readonly TrueTypeIndexToLocationTable? loca;
		public readonly TrueTypeGlyphContourTable? glyf;
		public readonly OpenTypeCFFTable? cff;

		public ushort UnitsPerEm => head.unitsPerEm;

		public TrueTypeFontFileOutlines(ushort numGlyphs, TrueTypeHeadTable head, TrueTypeIndexToLocationTable? loca, TrueTypeGlyphContourTable? glyf, OpenTypeCFFTable? cff) {
			this.numGlyphs = numGlyphs;
			this.head = head;
			this.loca = loca;
			this.glyf = glyf;
			this.cff = cff;
		}

		public static TrueTypeFontFileOutlines Open(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return Open(fontReader);
			}
		}

		public static TrueTypeFontFileOutlines Open(FontFileReader reader) {

			IReadOnlyDictionary<string, TrueTypeFontTable> tables = TrueTypeFontFile.ReadHeaderDict(reader,
				out _, out _, out _, out _);

			///////////// Head table
			if (!tables.TryGetValue("head", out TrueTypeFontTable? headTable)) {
				throw new FormatException("No head table.");
			}
			TrueTypeHeadTable head = TrueTypeHeadTable.Read(reader, headTable.offset);

			///////////// maxp table
			if (!tables.TryGetValue("maxp", out TrueTypeFontTable? maxpTable)) {
				throw new FormatException("No maxp table.");
			}
			reader.Position = maxpTable.offset;
			/*Fixed version =*/
			reader.ReadFixed();
			ushort numGlyphs = reader.ReadUInt16();

			////// TrueType loca and glyf tables
			TrueTypeIndexToLocationTable? loca = null;
			TrueTypeGlyphContourTable? glyf = null;
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
				glyf = TrueTypeGlyphContourTable.Read(reader, glyfTable.offset, loca);
			}

			///////////// OS/2 table
			OpenTypeCFFTable? cff = null;
			if (tables.TryGetValue("CFF ", out TrueTypeFontTable? cffTable)) {
				cff = OpenTypeCFFTable.Read(reader, cffTable.offset);
			}

			return new TrueTypeFontFileOutlines(
				numGlyphs,
				head, loca, glyf,
				cff);
		}
	}

}
