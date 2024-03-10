using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	public class TrueTypeCollection {

		public readonly TrueTypeFontFile[] fonts;

		public uint NumFonts => (uint)fonts.Length;

		public TrueTypeCollection(TrueTypeFontFile[] fonts) {
			this.fonts = fonts;
		}

		public static TrueTypeCollection Open(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				TrueTypeCollection fontFile = TrueTypeCollection.Open(fontReader);
				//fontFileStream.Close();
				return fontFile;
			}
		}

		internal static uint[] ReadHeader(FontFileReader reader) {
			// TODO Need a check here that we are actually reading a font collection file

			byte[] ttcTag = reader.ReadUInt8(4);
			ushort majorVersion = reader.ReadUInt16();
			ushort minorVersion = reader.ReadUInt16();
			uint numFonts = reader.ReadUInt32();

			uint[] tableDirectoryOffsets = new uint[numFonts];

			for (int i = 0; i < numFonts; i++) {
				tableDirectoryOffsets[i] = reader.ReadUInt32();
			}

			return tableDirectoryOffsets;
		}

		public static TrueTypeCollection Open(FontFileReader reader) {
			
			uint[] tableDirectoryOffsets = ReadHeader(reader);

			TrueTypeFontFile[] fonts = new TrueTypeFontFile[tableDirectoryOffsets.Length];

			for (int i = 0; i < tableDirectoryOffsets.Length; i++) {
				reader.Position = tableDirectoryOffsets[i];

				fonts[i] = TrueTypeFontFile.Open(reader);
			}

			return new TrueTypeCollection(fonts);
		}

		internal static IReadOnlyDictionary<string, TrueTypeFontTable> ReadFontTables(FontFileReader reader, int fontIndex) {
			uint[] tableDirectoryOffsets = ReadHeader(reader);

			if (fontIndex < 0 || fontIndex >= tableDirectoryOffsets.Length) {
				throw new ArgumentOutOfRangeException(nameof(fontIndex), fontIndex, $"This collection contains only {tableDirectoryOffsets.Length} fonts.");
			}

			reader.Position = tableDirectoryOffsets[fontIndex];
			IReadOnlyDictionary<string, TrueTypeFontTable> tables = TrueTypeFontFile.ReadHeader(reader,
				out _, out _, out _, out _);

			return tables;
		}

		internal static T? OpenTable<T>(string fontProgramPath, int fontIndex, string tableLabel, Func<FontFileReader, long, T> parser) where T : class {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return OpenTable(fontReader, fontIndex, tableLabel, parser);
			}
		}

		internal static T? OpenTable<T>(FontFileReader reader, int fontIndex, string tableLabel, Func<FontFileReader, long, T> parser) where T : class {
			IReadOnlyDictionary<string, TrueTypeFontTable> tables = ReadFontTables(reader, fontIndex);

			if (tables.TryGetValue(tableLabel, out TrueTypeFontTable? table)) {
				return parser(reader, table.offset);
			}
			else {
				return null;
			}
		}

		public static TrueTypeCMapTable? OpenCmap(string fontProgramPath, int fontIndex) {
			return OpenTable(fontProgramPath, fontIndex, "cmap", TrueTypeCMapTable.Read);
		}
		public static TrueTypeCMapTable? OpenCmap(FontFileReader reader, int fontIndex) {
			return OpenTable(reader, fontIndex, "cmap", TrueTypeCMapTable.Read);
		}

		public static TrueTypePostTable? OpenPost(string fontProgramPath, int fontIndex) {
			return OpenTable(fontProgramPath, fontIndex, "post", TrueTypePostTable.Read);
		}
		public static TrueTypePostTable? OpenPost(FontFileReader reader, int fontIndex) {
			return OpenTable(reader, fontIndex, "post", TrueTypePostTable.Read);
		}

		public static OpenTypeGlyphSubstitutionTable? OpenGSUB(string fontProgramPath, int fontIndex) {
			return OpenTable(fontProgramPath, fontIndex, "GSUB", OpenTypeGlyphSubstitutionTable.Read);
		}
		public static OpenTypeGlyphSubstitutionTable? OpenGSUB(FontFileReader reader, int fontIndex) {
			return OpenTable(reader, fontIndex, "GSUB", OpenTypeGlyphSubstitutionTable.Read);
		}

		public static OpenTypeLayoutTagSet ReadOpenTypeTags(string fontProgramPath, int fontIndex) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return ReadOpenTypeTags(fontReader, fontIndex);
			}
		}

		public static OpenTypeLayoutTagSet ReadOpenTypeTags(FontFileReader reader, int fontIndex) {
			IReadOnlyDictionary<string, TrueTypeFontTable> tables = ReadFontTables(reader, fontIndex);

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

	public class TrueTypeCollectionData {

		public readonly TrueTypeFontFileData[] fonts;

		public uint NumFonts => (uint)fonts.Length;

		public TrueTypeCollectionData(TrueTypeFontFileData[] fonts) {
			this.fonts = fonts;
		}

		public static TrueTypeCollectionData Open(string fontProgramPath) {
			using (FileStream fontFileStream = new FileStream(fontProgramPath, FileMode.Open, FileAccess.Read)) {
				FontFileReader fontReader = new FontFileReader(fontFileStream);
				return Open(fontReader);
			}
		}

		public static TrueTypeCollectionData Open(FontFileReader reader) {

			uint[] tableDirectoryOffsets = TrueTypeCollection.ReadHeader(reader);

			TrueTypeFontFileData[] fonts = new TrueTypeFontFileData[tableDirectoryOffsets.Length];

			for (int i = 0; i < tableDirectoryOffsets.Length; i++) {
				reader.Position = tableDirectoryOffsets[i];

				fonts[i] = TrueTypeFontFileData.Open(reader);
			}

			return new TrueTypeCollectionData(fonts);
		}

	}

}
