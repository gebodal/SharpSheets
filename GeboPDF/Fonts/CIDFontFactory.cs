using GeboPdf.Fonts;
using GeboPdf.Fonts.TrueType;
using GeboPdf.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GeboPdf.Fonts {

	public static class CIDFontFactory {

		// Very crude caching - should be improved
		private static readonly Dictionary<string, MemoryStream> fontStreams = new Dictionary<string, MemoryStream>();

		public static PdfType0Font CreateFont(string fontPath, OpenTypeLayoutTags layoutTags) {
			if (!fontStreams.ContainsKey(fontPath)) {
				byte[] fontBytes = File.ReadAllBytes(fontPath);
				MemoryStream memoryStream = new MemoryStream(fontBytes, false);
				fontStreams.Add(fontPath, memoryStream);
			}

			return CreateFont(fontStreams[fontPath], fontPath, layoutTags);
		}

		public static PdfType0Font CreateFont(string fontUri, Stream stream, OpenTypeLayoutTags layoutTags) {
			MemoryStream memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			memoryStream.Position = 0;

			return CreateFont(memoryStream, fontUri, layoutTags);
		}

		public static PdfType0Font CreateFont(string fontPath, int fontIndex, OpenTypeLayoutTags layoutTags) {
			string fontKey = fontPath + $"#{fontIndex}";

			if (!fontStreams.ContainsKey(fontKey)) {
				using (FileStream fileStream = new FileStream(fontPath, FileMode.Open, FileAccess.Read)) {
					MemoryStream fontStream = new MemoryStream();
					TrueTypeCollection.ExtractFont(fileStream, fontIndex, fontStream);

					fontStreams.Add(fontKey, fontStream);
				}
			}

			return CreateFont(fontStreams[fontKey], fontKey, layoutTags);
		}

		public static PdfType0Font CreateFont(string fontUri, int fontIndex, Stream stream, OpenTypeLayoutTags layoutTags) {
			MemoryStream memoryStream = new MemoryStream();
			TrueTypeCollection.ExtractFont(stream, fontIndex, memoryStream);
			memoryStream.Position = 0;

			return CreateFont(memoryStream, fontUri, layoutTags);
		}

		private static PdfType0Font CreateFont(MemoryStream memoryStream, string fontUri, OpenTypeLayoutTags layoutTags) {

			TrueTypeFontFile fontFile = ReadFontFile(memoryStream);

			IReadOnlyDictionary<uint, ushort> cmap = GetCmapDict(fontFile.cmap);

			//OpenTypeLayoutTags layoutTags = new OpenTypeLayoutTags("latn", null, new HashSet<string>() { "liga", "kern", "dlig" });

			GlyphSubstitutionLookupSet? gsubLookups = fontFile.gsub?.GetLookups(layoutTags);
			GlyphPositioningLookupSet? gposLookups = fontFile.gpos?.GetLookups(layoutTags);

			(ushort[] advanceWidths, short[] ascents, short[] descents, Dictionary<uint, short>? kerning) = GetMetrics(fontFile);

			PdfType0Font pdfFont = new PdfType0Font(
				fontUri, memoryStream,
				cmap,
				advanceWidths, ascents, descents, kerning,
				gsubLookups, gposLookups, fontFile.UnitsPerEm);

			return pdfFont;
		}

		private static readonly IReadOnlySet<string> reqTabs = new HashSet<string>() {
			"head", "hhea", "loca", "maxp", "cvt ", "prep", "glyf", "hmtx", "fpgm",
			"CFF ", "cmap"
		};

		public static PdfType0FontDictionary CreateFontDictionary(MemoryStream memoryStream, FontGlyphUsage fontUsage) {

			TrueTypeFontFile fontFile = ReadFontFile(memoryStream);

			bool openType = fontFile.tables.ContainsKey("CFF ");

			string fontName = GetFontName(fontFile);

			IReadOnlyDictionary<uint, ushort> cmap = GetCmapDict(fontFile.cmap);

			MemoryStream fontStream;
			if (fontUsage.All) {
				fontStream = memoryStream;
			}
			else {
				fontStream = new MemoryStream();
				TrueTypeFontTable[] tables = fontFile.tables.Values.Where(t => reqTabs.Contains(t.tag)).OrderBy(t => t.offset).ToArray();
				FontFileWriter.WriteHeaderAndTables(memoryStream, fontFile.scalerType, tables, fontStream);
			}

			TrueTypeFontProgramStream fontProgram = new TrueTypeFontProgramStream(fontStream, openType);

			FontDescriptorFlags flags = GetFlags(fontFile);
			PdfRectangle fontBBox = GetBBox(fontFile);
			float italicAngle = GetItalicAngle(fontFile);
			float ascent = GetAscent(fontFile);
			float descent = GetDescent(fontFile);
			float capHeight = GetCapHeight(fontFile);
			float stemV = GetStemV(fontFile);

			FontDescriptor fontDescriptor = new FontDescriptor(fontName, fontProgram, flags, fontBBox, italicAngle, ascent, descent, capHeight, stemV);

			IReadOnlySet<(ushort, ushort[])> mappings = GetMappings(fontUsage);

			PdfCmapStream toUnicode = CMapWriter.CreateToUnicode(cmap, mappings, fontName);

			int defaultWidth = GetDefaultWidth(fontFile);
			PdfArray widths = GetWidths(fontFile);

			Type2CIDFont cidFont = new Type2CIDFont(fontName, fontDescriptor, defaultWidth, widths);

			PdfType0FontDictionary pdfFontDictionary = new PdfType0FontDictionary(cidFont, toUnicode);

			return pdfFontDictionary;
		}

		private static TrueTypeFontFile ReadFontFile(MemoryStream memoryStream) {
			TrueTypeFontFile fontFile;
			lock (memoryStream) { // TODO Slightly crude attempt at thread safety
				memoryStream.Position = 0;
				FontFileReader fontReader = new FontFileReader(memoryStream);
				fontFile = TrueTypeFontFile.Open(fontReader);
			}
			return fontFile;
		}

		private static string GetFontName(TrueTypeFontFile fontFile) {
			string name;

			if (fontFile.name.nameRecords.TryGetValue(NameID.PostscriptName, out TrueTypeName[]? postScriptNames)) {
				name = GetName(postScriptNames).name;
			}
			else if (fontFile.name.nameRecords.TryGetValue(NameID.FullName, out TrueTypeName[]? fullNames)) {
				name = GetName(fullNames).name;
			}
			else if(fontFile.name.nameRecords.TryGetValue(NameID.FontFamily, out TrueTypeName[]? familyNames)) {
				string synthesised = GetName(familyNames).name;

				if (fontFile.name.nameRecords.TryGetValue(NameID.FontSubfamily, out TrueTypeName[]? subfamilyNames)) {
					synthesised = synthesised + "," + GetName(subfamilyNames).name;
				}

				name = synthesised;
			}
			else {
				throw new ArgumentException("Could not find or synthesise valid font name.");
			}

			return Regex.Replace(name, @"\s+", "");
		}

		public static TrueTypeCMapSubtable GetCmap(TrueTypeCMapTable cmap) {
			if (cmap.subtables.FirstOrDefault(kv => kv.Key.platformID == 0 && (kv.Key.encodingID == 3 || kv.Key.encodingID == 4)) is KeyValuePair<CMapTableIdentifier, TrueTypeCMapSubtable> unicode && unicode.Value != null) {
				return unicode.Value;
			}
			else if (cmap.subtables.FirstOrDefault(kv => kv.Key.platformID == 3 && (kv.Key.encodingID == 0 || kv.Key.encodingID == 1)) is KeyValuePair<CMapTableIdentifier, TrueTypeCMapSubtable> windows && windows.Value != null) {
				return windows.Value;
			}
			else {
				throw new ArgumentException("Could not find valid cmap table.");
			}
		}

		public static IReadOnlyDictionary<uint, ushort> GetCmapDict(TrueTypeCMapTable cmap) {
			TrueTypeCMapSubtable cmapSubtable = GetCmap(cmap);
			IReadOnlyDictionary<uint, ushort> cmapDict = cmapSubtable.cidMap;
			if (cmapSubtable.platformID == 3 && cmapSubtable.encodingID == 0) { // Windows Symbol table
				if (cmapDict.All(kv => kv.Key >= 0xF000)) {
					cmapDict = cmapDict.ToDictionary(kv => (uint)(kv.Key - 0xF000), kv => kv.Value);
				}
			}
			return cmapDict;
		}

		public static TrueTypeName GetName(TrueTypeName[] names) {
			// All of this is very Anglocentric. Can we do any localization?
			if (names.FirstOrDefault(kv => ((int)kv.platformID) == 0 && (kv.platformSpecificID == 3 || kv.platformSpecificID == 4)) is TrueTypeName unicode) {
				return unicode;
			}
			else if (names.FirstOrDefault(kv => ((int)kv.platformID) == 1 && kv.platformSpecificID == 0 && kv.languageID == 0) is TrueTypeName macintosh) {
				return macintosh;
			}
			else if (names.FirstOrDefault(kv => ((int)kv.platformID) == 3 && (kv.platformSpecificID == 0 || kv.platformSpecificID == 1) && kv.cultureInfo is not null && kv.cultureInfo.TwoLetterISOLanguageName.ToLower() == "en") is TrueTypeName windows) {
				// Is this working now?
				return windows;
			}
			else {
				return names.First();
			}
		}

		private static FontDescriptorFlags GetFlags(TrueTypeFontFile fontFile) {
			FontDescriptorFlags flags = FontDescriptorFlags.Symbolic;

			// TODO Implement flags?

			if (!flags.IsValid()) {
				throw new ArgumentException("Invalid flags.");
			}

			return flags;
		}

		private static int ProcessShort(short value, TrueTypeFontFile fontFile) {
			return (int)(1000 * (value / (double)fontFile.UnitsPerEm));
		}

		private static int ProcessUShort(ushort value, TrueTypeFontFile fontFile) {
			return (int)(1000 * ((int)value / (double)fontFile.UnitsPerEm));
		}

		private static PdfRectangle GetBBox(TrueTypeFontFile fontFile) {
			return PdfRectangle.FromCorners(
				ProcessShort(fontFile.head.xMin, fontFile),
				ProcessShort(fontFile.head.yMin, fontFile),
				ProcessShort(fontFile.head.xMax, fontFile),
				ProcessShort(fontFile.head.yMax, fontFile)
				);
		}

		private static float GetItalicAngle(TrueTypeFontFile fontFile) {
			// TODO Implement?
			return 0f;
		}

		private static float GetStemV(TrueTypeFontFile fontFile) {
			// TODO Implement better?
			return 85; // https://stackoverflow.com/a/35543715/11002708
		}

		private static float GetAscent(TrueTypeFontFile fontFile) {
			short ascent;
			if (fontFile.os2 != null && fontFile.os2.sTypoAscender.HasValue) {
				ascent = fontFile.os2.sTypoAscender.Value;
			}
			else {
				ascent = fontFile.head.yMax;
			}
			return ProcessShort(ascent, fontFile);
		}

		private static float GetDescent(TrueTypeFontFile fontFile) {
			short descent;
			if (fontFile.os2 != null && fontFile.os2.sTypoDescender.HasValue) {
				descent = fontFile.os2.sTypoDescender.Value;
			}
			else {
				descent = fontFile.head.yMin;
			}
			return ProcessShort(descent, fontFile);
		}

		private static float GetCapHeight(TrueTypeFontFile fontFile) {
			// TODO Better implementation?
			return GetAscent(fontFile);
		}

		private static int GetDefaultWidth(TrueTypeFontFile fontFile) {
			return ProcessUShort(fontFile.hhea.advanceWidthMax, fontFile);
		}

		private static PdfArray GetWidths(TrueTypeFontFile fontFile) {
			PdfArray array = new PdfArray();

			int numGlyphs = fontFile.hmtx.advanceWidths.Length;
			ushort[] advanceWidths = fontFile.hmtx.advanceWidths;

			void AddExplicitWidths(int start, int endExcl) {
				array.Add(new PdfInt(start));
				PdfArray widths = new PdfArray();
				for (int j = start; j < endExcl; j++) {
					widths.Add(new PdfInt(ProcessUShort(advanceWidths[j], fontFile)));
				}
				array.Add(widths);
			}

			int head = 0;
			for (int i = 0; i < numGlyphs;) {
				int sameCount = 0;
				while (i + sameCount + 1 < numGlyphs && advanceWidths[i] == advanceWidths[i + sameCount + 1]) {
					sameCount++;
				}

				if (sameCount > 2) {
					if (i > head) {
						AddExplicitWidths(head, i);
					}

					array.Add(new PdfInt(i));
					array.Add(new PdfInt(i + sameCount));
					array.Add(new PdfInt(ProcessUShort(advanceWidths[i], fontFile)));

					i += sameCount;
					head = i;
				}
				else {
					i++;
				}
			}

			if (head < numGlyphs) {
				AddExplicitWidths(head, numGlyphs);
			}

			return array;
		}

		private static (ushort[] advanceWidths, short[] ascents, short[] descents, Dictionary<uint, short>? kerning) GetMetrics(TrueTypeFontFile fontFile) {

			ushort[] advanceWidths = new ushort[fontFile.numGlyphs];
			short[] ascents = new short[fontFile.numGlyphs];
			short[] descents = new short[fontFile.numGlyphs];

			for (int i = 0; i < fontFile.numGlyphs; i++) {
				// Advance width
				advanceWidths[i] = fontFile.hmtx.advanceWidths[i];

				// Ascent & Descent
				short yMax, yMin;
				if (fontFile.glyf != null) {
					yMax = fontFile.glyf.glyphs[i].yMax;
					yMin = fontFile.glyf.glyphs[i].yMin;
				}
				else if (fontFile.os2 != null) {
					yMax = fontFile.os2.sTypoAscender ?? fontFile.head.yMax;
					yMin = fontFile.os2.sTypoDescender ?? fontFile.head.yMax;
				}
				else {
					yMax = fontFile.head.yMax;
					yMin = fontFile.head.yMax;
				}
				ascents[i] = yMax;
				descents[i] = yMin;
			}

			Dictionary<uint, short>? kerning = null;
			if (fontFile.kern is not null) {
				kerning = new Dictionary<uint, short>();

				foreach (uint pair in fontFile.kern.Subtables.SelectMany(s => s.Values.Keys).Distinct().OrderBy(p => p)) {

					short kernValue = 0;

					for (int s = 0; s < fontFile.kern.Subtables.Length; s++) {
						TrueTypeKerningSubtable subtable = fontFile.kern.Subtables[s];
						// TODO Only deals with horizontal writing
						if(subtable.Coverage.IsKerningValues() && subtable.Coverage.IsHorizontal() && !subtable.Coverage.IsCrossStream()) {
							if (subtable.Values.TryGetValue(pair, out short value)) {
								if (subtable.Coverage.IsOverride()) {
									kernValue = value;
								}
								else {
									kernValue += value;
								}
							}
						}
					}

					kerning[pair] = kernValue;
				}
			}

			return (advanceWidths, ascents, descents, kerning);
		}

		public static IReadOnlySet<(ushort gid, ushort[] originals)> GetMappings(FontGlyphUsage fontUsage) {
			return new HashSet<(ushort gid, ushort[] originals)>(
				fontUsage.Mappings
					.Where(m => m.glyphs.Length == 1)
					.Select(m => (m.glyphs[0], m.original)),
				GlyphMappingComparer.Instance);
		}

	}

}
