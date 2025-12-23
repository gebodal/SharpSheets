using GeboPdf.Fonts;
using GeboPdf.Fonts.TrueType;
using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPDF.Fonts {

	public static class FontSubsetting {

		private static readonly IReadOnlySet<string> reqTabs = new HashSet<string>() {
			"head", "hhea", "loca", "maxp", "cvt ", "prep", "glyf", "hmtx", "fpgm",
			"CFF ", "cmap"
		};

		public static MemoryStream ExtractRequiredTables(Stream source, TrueTypeFontFile fontFile) {
			MemoryStream fontStream = new MemoryStream();
			TrueTypeFontTable[] tables = fontFile.tables.Values.Where(t => reqTabs.Contains(t.tag)).OrderBy(t => t.offset).ToArray();
			FontFileWriter writer = new FontFileWriter(fontStream);
			writer.WriteFontDirectoryAndTables(source, fontFile.scalerType, tables);
			return fontStream;
		}

		public static (MemoryStream fontStream, PdfCmapStream encoding) Subset(Stream source, TrueTypeFontFile fontFile, string fontName, FontGlyphUsage fontUsage, TrueTypeCMapSubtable cmap) {

			MemoryStream fontStream = new MemoryStream();

			int reqTablesCount = fontFile.tables.Values.Where(t => reqTabs.Contains(t.tag)).Count();
			int fontDirectorySize = FontFileWriter.FontDirectorySize(reqTablesCount);

			List<TrueTypeFontTable> tables = new List<TrueTypeFontTable>();

			FontFileReader reader = new FontFileReader(source);
			FontFileWriter writer = new FontFileWriter(fontStream);

			// Need to skip Font Directory bytes?
			writer.Position += fontDirectorySize;

			// Optional font program tables
			if (fontFile.tables.TryGetValue("cvt ", out TrueTypeFontTable? cvt)) {
				tables.Add(new TrueTypeFontTable("cvt ", cvt.checksum, (uint)writer.Position, cvt.length));
				writer.CopyTable(source, cvt);
			}
			if (fontFile.tables.TryGetValue("prep", out TrueTypeFontTable? prep)) {
				tables.Add(new TrueTypeFontTable("prep", prep.checksum, (uint)writer.Position, prep.length));
				writer.CopyTable(source, prep);
			}
			if (fontFile.tables.TryGetValue("fpgm", out TrueTypeFontTable? fpgm)) {
				tables.Add(new TrueTypeFontTable("fpgm", fpgm.checksum, (uint)writer.Position, fpgm.length));
				writer.CopyTable(source, fpgm);
			}

			IndexToLocFormat locaFormat;
			List<ushort> remappings;
			if (fontFile.tables.TryGetValue("glyf", out TrueTypeFontTable? glyf)) {
				if (fontFile.loca is null || !fontFile.tables.TryGetValue("maxp", out TrueTypeFontTable? maxp)) {
					throw new FormatException("Missing required tables for valid 'glyf' based font.");
				}
				// Write glyf
				// Write loca
				// Write maxp
				SubsetGlyf(reader, writer, fontUsage, glyf.offset, fontFile.loca, out locaFormat, maxp, tables, out remappings);
			}
			else if(fontFile.tables.TryGetValue("CFF ", out TrueTypeFontTable? cff)) {
				// Write CFF
				// Write loca? <- is this used here?
				// Write maxp
				SubsetCFF(reader, writer, fontUsage, cff.offset, out locaFormat, tables, out remappings);
			}
			else {
				throw new FormatException("Can only subset 'glyf' and 'CFF ' based fonts.");
			}

			// Write htmx
			(ushort[] advanceWidths, short[] leftSideBearings) = GetHorizontalMetrics(fontFile, remappings);
			tables.Add(new TrueTypeHorizontalMetricsTable(advanceWidths, leftSideBearings).Write(writer, out ushort numOfLongHorMetrics));

			// Write hhea (with updated numOfLongHorMetrics)
			tables.Add(fontFile.hhea.Write(writer, numOfLongHorMetrics));

			// Write cmap
			Dictionary<uint, ushort> newCidMap = GetNewCIDMap(remappings, cmap);
			tables.Add(TrueTypeCMapTable.WriteFormat12(writer, newCidMap));

			// Required header table
			TrueTypeFontTable headTable = fontFile.head.Write(writer, 0, locaFormat);
			tables.Add(headTable);

			if (tables.Count != reqTablesCount) {
				throw new InvalidOperationException("Something went wrong.");
			}

			long finalLength = writer.Position;

			// Font Directory
			fontStream.Position = 0;
			writer.WriteOffsetSubtable(fontFile.scalerType, tables.Count);
			writer.WriteTableDirectoryRecalculateCheckSum(tables.ToArray());
			writer.Checksumming(headTable, 0, finalLength);

			PdfCmapStream encoding = CMapWriter.CreateGIDRemappingEncoding(remappings, fontName);

			return (fontStream, encoding);
		}

		private static void SubsetGlyf(FontFileReader reader, FontFileWriter writer, FontGlyphUsage fontUsage, long glyfTableOffset, TrueTypeIndexToLocationTable loca, out IndexToLocFormat locaFormat, TrueTypeFontTable maxp, List<TrueTypeFontTable> tables, out List<ushort> remappings) {

			HashSet<ushort> requiredGlyphs = new HashSet<ushort>();
			remappings = new List<ushort>(); // List of old IDs in new IDs order

			// Must include .notdef glyph as glyph 0
			requiredGlyphs.Add(0);
			remappings.Add(0);

			foreach (ushort i in fontUsage.Glyphs) {
				foreach (ushort used in TrueTypeGlyphContourTable.GetUsedGlyphs(reader, glyfTableOffset, loca, i)) {
					if (requiredGlyphs.Add(used)) {
						// If the glyph has been newly added
						remappings.Add(used);
					}
				}
			}

			remappings.Sort();
			Dictionary<ushort, ushort> remappingDict = new Dictionary<ushort, ushort>();
			for (ushort newGID = 0; newGID < remappings.Count; newGID++) {
				ushort oldGID = remappings[newGID];
				remappingDict[oldGID] = newGID;
			}


			uint[] offsets = new uint[remappings.Count];
			uint[] lengths = new uint[remappings.Count];

			long newGlyfTableOffset = writer.Position;

			// Write 'glyf'
			for (ushort newGID = 0; newGID < remappings.Count; newGID++) {
				ushort oldGID = remappings[newGID];
				uint length = loca.lengths[oldGID];

				offsets[newGID] = (uint)(writer.Position - newGlyfTableOffset);
				lengths[newGID] = length;

				// Actually copy glyph data
				reader.Position = glyfTableOffset + loca.offsets[oldGID];
				TrueTypeGlyphContourTable.CopyGlyphData(reader, writer, length, remappingDict);
			}

			tables.Add(new TrueTypeFontTable("glyf", 0, (uint)newGlyfTableOffset, (uint)(writer.Position - newGlyfTableOffset)));

			// Write 'loca'
			TrueTypeIndexToLocationTable locaSubset = new TrueTypeIndexToLocationTable(offsets, lengths);
			locaFormat = locaSubset.OptimalFormat();
			tables.Add(locaSubset.Write(writer, locaFormat));

			// Write 'maxp'
			TrueTypeMaximumProfileTable maxpTable = TrueTypeMaximumProfileTable.Read(reader, maxp.offset);
			tables.Add(maxpTable.Write(writer, (ushort)remappings.Count)); // Rewrite with new numGlyphs
		}

		private static (ushort[] advanceWidths, short[] leftSideBearings) GetHorizontalMetrics(TrueTypeFontFile fontFile, List<ushort> remappings) {
			ushort[] advanceWidths = new ushort[remappings.Count];
			short[] leftSideBearings = new short[remappings.Count];
			for (ushort newGID = 0; newGID < remappings.Count; newGID++) {
				ushort oldGID = remappings[newGID];
				advanceWidths[newGID] = fontFile.hmtx.advanceWidths[oldGID];
				leftSideBearings[newGID] = fontFile.hmtx.leftSideBearings[oldGID];
			}
			return (advanceWidths, leftSideBearings);
		}

		private static Dictionary<uint, ushort> GetNewCIDMap(List<ushort> remappings, TrueTypeCMapSubtable cmap) {

			Dictionary<uint, ushort> newCidMap = new Dictionary<uint, ushort>();

			Dictionary<ushort, ushort> glyphRemapping = new Dictionary<ushort, ushort>(); // oldGID -> newGID
			for(ushort newGID = 0; newGID < remappings.Count; newGID++) {
				ushort oldGID = remappings[newGID];
				glyphRemapping[oldGID] = newGID;
			}

			foreach ((uint charCode, ushort oldGlyphCode) in cmap.cidMap) {
				if (glyphRemapping.TryGetValue(oldGlyphCode, out ushort newGlyphCode)) {
					newCidMap[charCode] = newGlyphCode;
				}
			}

			return newCidMap;
		}

		private static void SubsetCFF(FontFileReader reader, FontFileWriter writer, FontGlyphUsage fontUsage, long cffTableOffset, out IndexToLocFormat locaFormat, List<TrueTypeFontTable> tables, out List<ushort> remappings) {
			throw new FormatException("Need to implement 'CFF ' subsetting logic."); // FormatException so that this triggers catch block in CIDFontFactory (hacky, I know, but hopefully temporary)
			//throw new NotImplementedException("Need to implement 'CFF ' subsetting logic.");
		}

	}

}
