using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace GeboPdf.Fonts.TrueType {


	[Flags]
	public enum TrueTypeHeadFlags : ushort {
		/// <summary>
		/// y value of 0 specifies baseline
		/// </summary>
		Y0Baseline = 1 << 0,
		/// <summary>
		/// x position of left most black bit is LSB
		/// </summary>
		LeftmostXposLSB = 1 << 1,
		/// <summary>
		/// scaled point size and actual point size will differ (i.e. 24 point glyph differs from 12 point glyph scaled by factor of 2)
		/// </summary>
		ScaledSizeDiffers = 1 << 2,
		/// <summary>
		/// use integer scaling instead of fractional
		/// </summary>
		IntegerScaling = 1 << 3,
		/// <summary>
		/// Instructions may alter advance width (the advance widths might not scale linearly).
		/// Used by the Microsoft implementation of the TrueType scaler.
		/// </summary>
		ScaledWidthDiffers = 1 << 4,
		/// <summary>
		/// This bit should be set in fonts that are intended to be laid out vertically,
		/// and in which the glyphs have been drawn such that an x-coordinate of 0 corresponds to the desired vertical baseline.
		/// </summary>
		VerticalX0Baseline = 1 << 5,
		/// <summary>
		/// This bit should be set if the font requires layout for correct linguistic rendering (e.g. Arabic fonts).
		/// </summary>
		RequiresLayout = 1 << 7,
		/// <summary>
		/// This bit should be set for an AAT font which has one or more metamorphosis effects designated as happening by default.
		/// </summary>
		AATMetamorphosis = 1 << 8,
		/// <summary>
		/// This bit should be set if the font contains any strong right-to-left glyphs.
		/// </summary>
		ContainsRightToLeft = 1 << 9,
		/// <summary>
		/// This bit should be set if the font contains Indic-style rearrangement effects.
		/// </summary>
		IndicRearrangement = 1 << 10,
		/// <summary>
		/// This bit should be set if the glyphs in the font are simply generic symbols for code point ranges, such as for a last resort font.
		/// </summary>
		LastResort = 1 << 14
	}

	[Flags]
	public enum MacStyle : ushort {
		/// <summary>
		/// None.
		/// </summary>
		None = 0,
		/// <summary>
		/// Bold.
		/// </summary>
		Bold = 1 << 0,
		/// <summary>
		/// Italic.
		/// </summary>
		Italic = 1 << 1,
		/// <summary>
		/// Underline.
		/// </summary>
		Underline = 1 << 2,
		/// <summary>
		/// Outline.
		/// </summary>
		Outline = 1 << 3,
		/// <summary>
		/// Shadow.
		/// </summary>
		Shadow = 1 << 4,
		/// <summary>
		/// Condensed (narrow).
		/// </summary>
		Condensed = 1 << 5,
		/// <summary>
		/// Extended.
		/// </summary>
		Extended = 1 << 6
	}

	public enum FontDirectionHint : short {
		/// <summary>
		/// Mixed directional glyphs
		/// </summary>
		Mixed = 0,
		/// <summary>
		/// Only strongly left to right glyphs
		/// </summary>
		OnlyStrongLeftRight = 1,
		/// <summary>
		/// Like 1 but also contains neutrals
		/// </summary>
		LeftRightNeutrals = 2,
		/// <summary>
		/// Only strongly right to left glyphs
		/// </summary>
		OnlyStrongRightLeft = -1,
		/// <summary>
		/// Like -1 but also contains neutrals
		/// </summary>
		RightLeftNeutrals = -2
	}

	public enum IndexToLocFormat : short {
		Short = 0,
		Long = 1
	}

	public class TrueTypeHeadTable {

		public readonly float version;
		public readonly float fontRevision;
		public readonly uint checkSumAdjustment;
		public readonly uint magicNumber;
		public readonly TrueTypeHeadFlags flags;
		public readonly ushort unitsPerEm;
		public readonly long created;
		public readonly long modified;
		public readonly short xMin;
		public readonly short yMin;
		public readonly short xMax;
		public readonly short yMax;
		public readonly MacStyle macStyle;
		public readonly ushort lowestRecPPEM;
		public readonly FontDirectionHint fontDirectionHint;
		public readonly IndexToLocFormat indexToLocFormat;
		public readonly short glyphDataFormat;

		internal TrueTypeHeadTable(
				float version, float fontRevision,
				uint checkSumAdjustment, uint magicNumber,
				TrueTypeHeadFlags flags,
				ushort unitsPerEm,
				long created, long modified,
				short xMin, short yMin, short xMax, short yMax,
				MacStyle macStyle,
				ushort lowestRecPPEM,
				FontDirectionHint fontDirectionHint,
				IndexToLocFormat indexToLocFormat, short glyphDataFormat
			) {

			this.version = version;
			this.fontRevision = fontRevision;
			this.checkSumAdjustment = checkSumAdjustment;
			this.magicNumber = magicNumber;
			this.flags = flags;
			this.unitsPerEm = unitsPerEm;
			this.created = created;
			this.modified = modified;
			this.xMin = xMin;
			this.yMin = yMin;
			this.xMax = xMax;
			this.yMax = yMax;
			this.macStyle = macStyle;
			this.lowestRecPPEM = lowestRecPPEM;
			this.fontDirectionHint = fontDirectionHint;
			this.indexToLocFormat = indexToLocFormat;
			this.glyphDataFormat = glyphDataFormat;
		}

		internal static TrueTypeHeadTable Read(FontFileReader reader, long offset) {
			reader.Position = offset;

			float version = reader.ReadFixed();
			float fontRevision = reader.ReadFixed();
			uint checkSumAdjustment = reader.ReadUInt32();
			uint magicNumber = reader.ReadUInt32();
			TrueTypeHeadFlags flags = (TrueTypeHeadFlags)reader.ReadUInt16();
			ushort unitsPerEm = reader.ReadUInt16();
			long created = reader.ReadInt64();
			long modified = reader.ReadInt64();
			short xMin = reader.ReadFWord();
			short yMin = reader.ReadFWord();
			short xMax = reader.ReadFWord();
			short yMax = reader.ReadFWord();
			MacStyle macStyle = (MacStyle)reader.ReadUInt16();
			ushort lowestRecPPEM = reader.ReadUInt16();
			FontDirectionHint fontDirectionHint = (FontDirectionHint)reader.ReadInt16();
			IndexToLocFormat indexToLocFormat = (IndexToLocFormat)reader.ReadInt16();
			short glyphDataFormat = reader.ReadInt16();

			return new TrueTypeHeadTable(
				version, fontRevision,
				checkSumAdjustment, magicNumber,
				flags,
				unitsPerEm,
				created, modified,
				xMin, yMin, xMax, yMax,
				macStyle,
				lowestRecPPEM,
				fontDirectionHint,
				indexToLocFormat, glyphDataFormat
			);
		}
	}

	public enum PlatformID : ushort {
		/// <summary>
		/// Indicates Unicode version.
		/// </summary>
		Unicode = 0,
		/// <summary>
		/// QuickDraw Script Manager code.
		/// </summary>
		Macintosh = 1,
		/// <summary>
		/// Microsoft encoding.
		/// </summary>
		Microsoft = 3
	}

	public enum NameID : ushort {
		/// <summary>
		/// Copyright notice.
		/// </summary>
		CopyrightNotice = 0,
		/// <summary>
		/// Font Family.
		/// </summary>
		FontFamily = 1,
		/// <summary>
		/// Font Subfamily.
		/// </summary>
		FontSubfamily = 2,
		/// <summary>
		/// Unique subfamily identification.
		/// </summary>
		UniqueSubfamilyIdentification = 3,
		/// <summary>
		/// Full name of the font.
		/// </summary>
		FullName = 4,
		/// <summary>
		/// Version of the name table
		/// </summary>
		NameTableVersion = 5,
		/// <summary>
		/// PostScript name of the font. All PostScript names in a font must be identical. They may not be longer than 63 characters and the characters used are restricted to the set of printable ASCII characters (U+0021 through U+007E), less the ten characters '[', ']', '(', ')', '{', '}', '<', '>', '/', and '%'.
		/// </summary>
		PostscriptName = 6,
		/// <summary>
		/// Trademark notice.
		/// </summary>
		TrademarkNotice = 7,
		/// <summary>
		/// Manufacturer name.
		/// </summary>
		ManufacturerName = 8,
		/// <summary>
		/// Designer; name of the designer of the typeface.
		/// </summary>
		Designer = 9,
		/// <summary>
		/// Description; description of the typeface. Can contain revision information, usage recommendations, history, features, and so on.
		/// </summary>
		Description = 10,
		/// <summary>
		/// URL of the font vendor (with procotol, e.g., http://, ftp://). If a unique serial number is embedded in the URL, it can be used to register the font.
		/// </summary>
		FontVendorURL = 11,
		/// <summary>
		/// URL of the font designer (with protocol, e.g., http://, ftp://)
		/// </summary>
		FontDesignerURL = 12,
		/// <summary>
		/// License description; description of how the font may be legally used, or different example scenarios for licensed use. This field should be written in plain language, not legalese.
		/// </summary>
		LicenseDescription = 13,
		/// <summary>
		/// License information URL, where additional licensing information can be found.
		/// </summary>
		LicenseInformationURL = 14,
		/// <summary>
		/// Reserved
		/// </summary>
		Reserved = 15,
		/// <summary>
		/// Preferred Family. In Windows, the Family name is displayed in the font menu, and the Subfamily name is presented as the Style name. For historical reasons, font families have contained a maximum of four styles, but font designers may group more than four fonts to a single family. The Preferred Family and Preferred Subfamily IDs allow font designers to include the preferred family/subfamily groupings. These IDs are only present if they are different from IDs 1 and 2.
		/// </summary>
		PreferredFamily = 16,
		/// <summary>
		/// Preferred Subfamily. In Windows, the Family name is displayed in the font menu, and the Subfamily name is presented as the Style name. For historical reasons, font families have contained a maximum of four styles, but font designers may group more than four fonts to a single family. The Preferred Family and Preferred Subfamily IDs allow font designers to include the preferred family/subfamily groupings. These IDs are only present if they are different from IDs 1 and 2.
		/// </summary>
		PreferredSubfamily = 17,
		/// <summary>
		/// Compatible Full (macOS only). In QuickDraw, the menu name for a font is constructed using the FOND resource.This usually matches the Full Name.If you want the name of the font to appear differently than the Full Name, you can insert the Compatible Full Name in ID 18. This name is not used by macOS itself, but may be used by application developers (e.g., Adobe).
		/// </summary>
		CompatibleFull = 18,
		/// <summary>
		/// Sample text. This can be the font name, or any other text that the designer thinks is the best sample text to show what the font looks like.
		/// </summary>
		SampleText = 19,
		// 20–24 Defined by OpenType.
		/// <summary>
		/// Variations PostScript Name Prefix. If present in a variable font, it may be used as the family prefix in the algorithm to generate PostScript names for variation fonts. See Adobe Technical Note #5902: “PostScript Name Generation for Variation Fonts” for details.
		/// </summary>
		VariationsPostScriptNamePrefix = 25
	}

	[System.Diagnostics.DebuggerDisplay("TrueTypeName({name}, {nameID}, {platformID}, {platformSpecificID}, {languageID}, {cultureInfo})")]
	public class TrueTypeName {

		public readonly PlatformID platformID;
		public readonly ushort platformSpecificID;
		public readonly ushort languageID;
		public readonly NameID nameID;

		public readonly string name;
		public readonly CultureInfo? cultureInfo;

		internal TrueTypeName(PlatformID platformID, ushort platformSpecificID, ushort languageID, NameID nameID, string name, CultureInfo? cultureInfo) {
			this.platformID = platformID;
			this.platformSpecificID = platformSpecificID;
			this.languageID = languageID;
			this.nameID = nameID;
			this.name = name;
			this.cultureInfo = cultureInfo;
		}
	}

	public class TrueTypeNameTable {

		public readonly ushort format;
		//public readonly Dictionary<string, TrueTypeName> nameRecords;
		public readonly Dictionary<NameID, TrueTypeName[]> nameRecords;

		public IEnumerable<TrueTypeName> AllNameRecords => nameRecords.Values.SelectMany(vs => vs);

		internal TrueTypeNameTable(ushort format, IEnumerable<TrueTypeName> nameRecords) {
			this.format = format;
			//this.nameRecords = nameRecords.ToDictionary(r => $"{r.platformID}|{r.platformSpecificID}|{r.languageID}|{r.nameID}");

			this.nameRecords = nameRecords.GroupBy(r => r.nameID).ToDictionary(rg => rg.Key, rg => rg.ToArray());
		}

		internal static TrueTypeNameTable Read(FontFileReader reader, long offset) {
			List<TrueTypeName> names = new List<TrueTypeName>();

			reader.Position = offset;

			ushort format = reader.ReadUInt16();
			ushort count = reader.ReadUInt16();
			ushort stringOffset = reader.ReadUInt16();

			for (int i = 0; i < count; i++) {
				ushort[] nameRecord = reader.ReadUInt16(6);

				long old = reader.Position;
				reader.Position = offset + stringOffset + nameRecord[5];

				TrueTypeName name = TrueTypeNameEncodings.Decode(reader, nameRecord[0], nameRecord[1], nameRecord[2], nameRecord[3], nameRecord[4]);

				reader.Position = old;

				names.Add(name);
			}

			return new TrueTypeNameTable(format, names);
		}

	}

	public static class TrueTypeNameEncodings {

		public static TrueTypeName Decode(FontFileReader reader, ushort platformID, ushort platformSpecificID, ushort languageID, ushort nameID, ushort length) {
			string name;

			if (platformID == (ushort)PlatformID.Unicode || platformID == (ushort)PlatformID.Microsoft) {
				name = reader.ReadUTF16BEString(length);
			}
			else {
				name = reader.ReadASCIIString(length);
			}

			name = name.Replace("\r\n", "\n").Replace("\r", "").Trim();

			CultureInfo? cultureInfo;
			if (platformID == (ushort)PlatformID.Microsoft) {
				cultureInfo = new CultureInfo(languageID);
			}
			else {
				cultureInfo = null;
			}

			return new TrueTypeName((PlatformID)platformID, platformSpecificID, languageID, (NameID)nameID, name, cultureInfo);
		}

	}

	public class TrueTypeHorizontalHeaderTable {

		public readonly float version;
		public readonly short ascent;
		public readonly short descent;
		public readonly short lineGap;
		public readonly ushort advanceWidthMax;
		public readonly short minLeftSideBearing;
		public readonly short minRightSideBearing;
		public readonly short xMaxExtent;
		public readonly short caretSlopeRise;
		public readonly short caretSlopeRun;
		public readonly short caretOffset;
		public readonly short metricDataFormat;
		public readonly ushort numOfLongHorMetrics;

		internal TrueTypeHorizontalHeaderTable(
				float version,
				short ascent, short descent, short lineGap,
				ushort advanceWidthMax,
				short minLeftSideBearing, short minRightSideBearing,
				short xMaxExtent,
				short caretSlopeRise, short caretSlopeRun, short caretOffset,
				short metricDataFormat,
				ushort numOfLongHorMetrics
			) {

			this.version = version;
			this.ascent = ascent;
			this.descent = descent;
			this.lineGap = lineGap;
			this.advanceWidthMax = advanceWidthMax;
			this.minLeftSideBearing = minLeftSideBearing;
			this.minRightSideBearing = minRightSideBearing;
			this.xMaxExtent = xMaxExtent;
			this.caretSlopeRise = caretSlopeRise;
			this.caretSlopeRun = caretSlopeRun;
			this.caretOffset = caretOffset;
			this.metricDataFormat = metricDataFormat;
			this.numOfLongHorMetrics = numOfLongHorMetrics;
		}

		internal static TrueTypeHorizontalHeaderTable Read(FontFileReader reader, long offset) {

			reader.Position = offset;

			float version = reader.ReadFixed();
			short ascent = reader.ReadFWord();
			short descent = reader.ReadFWord();
			short lineGap = reader.ReadFWord();
			ushort advanceWidthMax = reader.ReadUFWord();
			short minLeftSideBearing = reader.ReadFWord();
			short minRightSideBearing = reader.ReadFWord();
			short xMaxExtent = reader.ReadFWord();
			short caretSlopeRise = reader.ReadInt16();
			short caretSlopeRun = reader.ReadInt16();
			short caretOffset = reader.ReadFWord();
			reader.SkipInt16(4); //short[] reserved = reader.ReadInt16(4);
			short metricDataFormat = reader.ReadInt16();
			ushort numOfLongHorMetrics = reader.ReadUInt16();

			return new TrueTypeHorizontalHeaderTable(
				version,
				ascent, descent, lineGap,
				advanceWidthMax,
				minLeftSideBearing, minRightSideBearing,
				xMaxExtent,
				caretSlopeRise, caretSlopeRun, caretOffset,
				metricDataFormat,
				numOfLongHorMetrics
			);
		}

	}

	public class TrueTypeIndexToLocationTable {

		public readonly uint[] offsets;
		public readonly uint[] lengths;

		internal TrueTypeIndexToLocationTable(uint[] offsets, uint[] lengths) {
			this.offsets = offsets;
			this.lengths = lengths;
		}

		internal static TrueTypeIndexToLocationTable Read(FontFileReader reader, long offset, ushort numGlyphs, IndexToLocFormat format) {
			reader.Position = offset;

			uint[] offsets = new uint[numGlyphs];
			uint[] lengths = new uint[numGlyphs];

			for (int i = 0; i <= numGlyphs; i++) {
				uint glyphOffset;
				if (format == IndexToLocFormat.Short) {
					glyphOffset = 2U * reader.ReadUInt16();
				}
				else {
					glyphOffset = reader.ReadUInt32();
				}

				if (i < numGlyphs) {
					offsets[i] = glyphOffset;
				}

				if (i > 0) {
					lengths[i - 1] = glyphOffset - offsets[i - 1];
				}
			}

			return new TrueTypeIndexToLocationTable(offsets, lengths);
		}

	}

	public class TrueTypeHorizontalMetricsTable {

		public readonly ushort[] advanceWidths;
		public readonly short[] leftSideBearings;

		internal TrueTypeHorizontalMetricsTable(ushort[] advanceWidths, short[] leftSideBearings) {
			this.advanceWidths = advanceWidths;
			this.leftSideBearings = leftSideBearings;
		}

		internal static TrueTypeHorizontalMetricsTable Read(FontFileReader reader, long offset, ushort numGlyphs, ushort numOfLongHorMetrics) {

			if (numOfLongHorMetrics < 1) {
				throw new FormatException($"numOfLongHorMetrics must be greater than 0 ({numOfLongHorMetrics} provided).");
			}

			reader.Position = offset;

			ushort[] advanceWidths = new ushort[numGlyphs];
			short[] leftSideBearings = new short[numGlyphs];

			int i = 0;
			while (i < numOfLongHorMetrics && i < numGlyphs) {
				ushort advanceWidth = reader.ReadUInt16();
				short leftSideBearing = reader.ReadInt16();

				advanceWidths[i] = advanceWidth;
				leftSideBearings[i] = leftSideBearing;

				i++;
			}
			ushort finalAdvanceWidth = advanceWidths[i - 1];
			while (i < numGlyphs) {
				short leftSideBearing = reader.ReadFWord();

				advanceWidths[i] = finalAdvanceWidth;
				leftSideBearings[i] = leftSideBearing;

				i++;
			}

			return new TrueTypeHorizontalMetricsTable(advanceWidths, leftSideBearings);
		}

	}

	[Flags]
	public enum FontSelectionFlags : ushort {
		/// <summary>
		/// Font contains italic or oblique glyphs, otherwise they are upright.
		/// </summary>
		ITALIC = 1 << 0,
		/// <summary>
		/// glyphs are underscored.
		/// </summary>
		UNDERSCORE = 1 << 1,
		/// <summary>
		/// glyphs have their foreground and background reversed.
		/// </summary>
		NEGATIVE = 1 << 2,
		/// <summary>
		/// Outline (hollow) glyphs, otherwise they are solid.
		/// </summary>
		OUTLINED = 1 << 3,
		/// <summary>
		/// glyphs are overstruck.
		/// </summary>
		STRIKEOUT = 1 << 4,
		/// <summary>
		/// glyphs are emboldened.
		/// </summary>
		BOLD = 1 << 5,
		/// <summary>
		/// glyphs are in the standard weight/style for the font.
		/// </summary>
		REGULAR = 1 << 6,
		/// <summary>
		/// If set, it is strongly recommended that applications use OS/2.sTypoAscender - OS/2.sTypoDescender + OS/2.sTypoLineGap as the default line spacing for this font.
		/// </summary>
		USE_TYPO_METRICS = 1 << 7,
		/// <summary>
		/// The font has 'name' table strings consistent with a weight/width/slope family without requiring use of name IDs 21 and 22. (Please see more detailed description below.)
		/// </summary>
		WWS = 1 << 8,
		/// <summary>
		/// Font contains oblique glyphs.
		/// </summary>
		OBLIQUE = 1 << 9
	}

	// TODO EmbeddingFlags has some extra stipulations on which bits can be set.
	// A Validation() method is probably required.
	[Flags]
	public enum EmbeddingFlags : ushort {
		/// <summary>
		/// Licensed (protected) font; should not be 1 if bits 2 or 3 are one.
		/// Fonts that have only this bit set must not be modified, embedded,
		/// or exchanged in any manner without first obtaining permission of
		/// the legal owner.
		/// </summary>
		Licensed = 1 << 1,
		/// <summary>
		/// Preview and print embedding; should not be 1 if bits 1 or 3 are one.
		/// Fonts that have only this bit set may be embedded in documents and
		/// temporarily loaded on the remote system. Documents containing such
		/// fonts must be opened “read-only;” no edits can be applied to the
		/// document.
		/// </summary>
		PreviewAndPrintEmbedding = 1 << 2,
		/// <summary>
		/// Editable embedding; should not be 1 if bits 1 or 2 are one. Fonts
		/// that have only this bit set may be embedded in documents and
		/// temporarily loaded on the remote system. Documents containing such
		/// fonts may be editable.
		/// </summary>
		EditableEmbedding = 1 << 3,
		/// <summary>
		/// No subsetting. When this bit is set, the font may not be subsetted
		/// prior to embedding. Other embedding restrictions specified in
		/// bits 1–3 and 9 also apply.
		/// </summary>
		NoSubsetting = 1 << 8,
		/// <summary>
		/// Bitmap embedding only. When this bit is set, only bitmaps contained
		/// in the font may be embedded. No outline data may be embedded. Other
		/// embedding restrictions specified in bits 1–3 and 8 also apply.
		/// </summary>
		BitmapEmbeddingOnly = 1 << 9
	}

	public static class EmbeddingFlagsUtils {

		private static readonly EmbeddingFlags LegalityBits = EmbeddingFlags.Licensed | EmbeddingFlags.PreviewAndPrintEmbedding | EmbeddingFlags.EditableEmbedding;
		private static readonly EmbeddingFlags KnownEmbeddableBits = EmbeddingFlags.PreviewAndPrintEmbedding | EmbeddingFlags.EditableEmbedding;

		/// <summary>
		/// Indicates if the font is a licensed font.
		/// </summary>
		/// <param name="flags">The <see cref="EmbeddingFlags"/> value to consider.</param>
		/// <returns>
		/// <see langword="true"/> if the <see cref="EmbeddingFlags.Licensed"/> bit is set to 1,
		/// otherwise <see langword="false"/>.
		/// </returns>
		public static bool IsLicensed(this EmbeddingFlags flags) {
			return (flags & EmbeddingFlags.Licensed) == EmbeddingFlags.Licensed;
		}

		/// <summary>
		/// Indicates if the font allows embedding for preview and printing.
		/// </summary>
		/// <param name="flags">The <see cref="EmbeddingFlags"/> value to consider.</param>
		/// <returns>
		/// <see langword="true"/> if the <see cref="EmbeddingFlags.PreviewAndPrintEmbedding"/> bit is set to 1,
		/// and the other legality bits are set to 0 (<see cref="EmbeddingFlags.Licensed"/>
		/// and <see cref="EmbeddingFlags.EditableEmbedding"/>), otherwise <see langword="false"/>.
		/// </returns>
		public static bool IsPreviewAndPrintEmbedding(this EmbeddingFlags flags) {
			return (flags & LegalityBits) == EmbeddingFlags.PreviewAndPrintEmbedding;
		}

		/// <summary>
		/// Indicates if the font allows editable embedding.
		/// </summary>
		/// <param name="flags">The <see cref="EmbeddingFlags"/> value to consider.</param>
		/// <returns>
		/// <see langword="true"/> if the <see cref="EmbeddingFlags.EditableEmbedding"/> bit is set to 1,
		/// and the other legality bits are set to 0 (<see cref="EmbeddingFlags.Licensed"/>
		/// and <see cref="EmbeddingFlags.PreviewAndPrintEmbedding"/>), otherwise <see langword="false"/>.
		/// </returns>
		public static bool IsEditableEmbedding(this EmbeddingFlags flags) {
			return (flags & LegalityBits) == EmbeddingFlags.EditableEmbedding;
		}

		/// <summary>
		/// Indicates if the font allows subsetting when embedding.
		/// </summary>
		/// <param name="flags">The <see cref="EmbeddingFlags"/> value to consider.</param>
		/// <returns>
		/// <see langword="true"/> if the <see cref="EmbeddingFlags.NoSubsetting"/> bit is set to 0,
		/// otherwise <see langword="false"/>.
		/// </returns>
		public static bool SubsettingAllowed(this EmbeddingFlags flags) {
			return (flags & EmbeddingFlags.NoSubsetting) != EmbeddingFlags.NoSubsetting;
		}

		/// <summary>
		/// Indicates if the font only allows bitmap embedding.
		/// </summary>
		/// <param name="flags">The <see cref="EmbeddingFlags"/> value to consider.</param>
		/// <returns>
		/// <see langword="true"/> if the <see cref="EmbeddingFlags.BitmapEmbeddingOnly"/> bit is set to 1,
		/// otherwise <see langword="false"/>.
		/// </returns>
		public static bool IsBitmapEmbeddingOnly(this EmbeddingFlags flags) {
			return (flags & EmbeddingFlags.BitmapEmbeddingOnly) != EmbeddingFlags.BitmapEmbeddingOnly;
		}

		/// <summary>
		/// Indicates if the font is not licensed, and is known to allow embedding of any kind.
		/// </summary>
		/// <param name="flags">The <see cref="EmbeddingFlags"/> value to consider.</param>
		/// <returns>
		/// <see langword="true"/> if either the <see cref="EmbeddingFlags.PreviewAndPrintEmbedding"/>
		/// or <see cref="EmbeddingFlags.EditableEmbedding"/> flags are set to 1 and the
		/// <see cref="EmbeddingFlags.Licensed"/> flag is set to 0, otherwise <see langword="false"/>.
		/// </returns>
		public static bool IsKnownEmbeddable(this EmbeddingFlags flags) {
			return (flags & KnownEmbeddableBits) > 0 && !flags.IsLicensed();
		}

	}

	public class TrueTypeOS2Table {

		/// <summary> table version number(set to 0) </summary>
		public readonly ushort version;
		/// <summary> average weighted advance width of lower case letters and space </summary>
		public readonly short xAvgCharWidth;
		/// <summary> visual weight(degree of blackness or thickness) of stroke in glyphs </summary>
		public readonly ushort usWeightClass;
		/// <summary> relative change from the normal aspect ratio(width to height ratio) as specified by a font designer for the glyphs in the font </summary>
		public readonly ushort usWidthClass;
		/// <summary> characteristics and properties of this font(set undefined bits to zero) </summary>
		public readonly EmbeddingFlags fsType;
		/// <summary> recommended horizontal size in pixels for subscripts </summary>
		public readonly short ySubscriptXSize;
		/// <summary> recommended vertical size in pixels for subscripts </summary>
		public readonly short ySubscriptYSize;
		/// <summary> recommended horizontal offset for subscripts </summary>
		public readonly short ySubscriptXOffset;
		/// <summary> recommended vertical offset form the baseline for subscripts </summary>
		public readonly short ySubscriptYOffset;
		/// <summary> recommended horizontal size in pixels for superscripts </summary>
		public readonly short ySuperscriptXSize;
		/// <summary> recommended vertical size in pixels for superscripts </summary>
		public readonly short ySuperscriptYSize;
		/// <summary> recommended horizontal offset for superscripts </summary>
		public readonly short ySuperscriptXOffset;
		/// <summary> recommended vertical offset from the baseline for superscripts </summary>
		public readonly short ySuperscriptYOffset;
		/// <summary> width of the strikeout stroke </summary>
		public readonly short yStrikeoutSize;
		/// <summary> position of the strikeout stroke relative to the baseline </summary>
		public readonly short yStrikeoutPosition;
		/// <summary> classification of font - family design. </summary>
		public readonly short sFamilyClass;
		/// <summary> 10 byte series of number used to describe the visual characteristics of a given typeface </summary>
		public readonly byte[] panose;
		/// <summary> Field is split into two bit fields of 96 and 36 bits each. The low 96 bits are used to specify the Unicode blocks encompassed by the font file.The high 32 bits are used to specify the character or script sets covered by the font file.Bit assignments are pending.Set to 0 </summary>
		public readonly uint[] ulUnicodeRange;
		/// <summary> four character identifier for the font vendor </summary>
		public readonly sbyte[] achVendID;
		/// <summary> 2-byte bit field containing information concerning the nature of the font patterns </summary>
		public readonly FontSelectionFlags fsSelection;
		/// <summary> The minimum Unicode index in this font. </summary>
		public readonly ushort fsFirstCharIndex;
		/// <summary> The maximum Unicode index in this font. </summary>
		public readonly ushort fsLastCharIndex;

		/// <summary>
		/// The typographic ascender for this font. This field should be combined with the sTypoDescender and sTypoLineGap values to determine default line spacing. 
		/// </summary>
		public readonly short? sTypoAscender;
		/// <summary>
		/// The typographic descender for this font. This field should be combined with the sTypoAscender and sTypoLineGap values to determine default line spacing. 
		/// </summary>
		public readonly short? sTypoDescender;
		/// <summary>
		/// The typographic line gap for this font. This field should be combined with the sTypoAscender and sTypoDescender values to determine default line spacing. 
		/// </summary>
		public readonly short? sTypoLineGap;
		/// <summary>
		/// The “Windows ascender” metric. This should be used to specify the height above the baseline for a clipping region. 
		/// </summary>
		public readonly ushort? usWinAscent;
		/// <summary>
		/// : 	The “Windows descender” metric. This should be used to specify the vertical extent below the baseline for a clipping region.
		/// </summary>
		public readonly ushort? usWinDescent;

		internal TrueTypeOS2Table(ushort version,
				short xAvgCharWidth,
				ushort usWeightClass, ushort usWidthClass,
				EmbeddingFlags fsType,
				short ySubscriptXSize, short ySubscriptYSize, short ySubscriptXOffset, short ySubscriptYOffset,
				short ySuperscriptXSize, short ySuperscriptYSize, short ySuperscriptXOffset, short ySuperscriptYOffset,
				short yStrikeoutSize, short yStrikeoutPosition,
				short sFamilyClass,
				byte[] panose,
				uint[] ulUnicodeRange,
				sbyte[] achVendID,
				FontSelectionFlags fsSelection,
				ushort fsFirstCharIndex, ushort fsLastCharIndex,
				short? sTypoAscender,
				short? sTypoDescender,
				short? sTypoLineGap,
				ushort? usWinAscent,
				ushort? usWinDescent
			) {

			this.version = version;
			this.xAvgCharWidth = xAvgCharWidth;
			this.usWeightClass = usWeightClass;
			this.usWidthClass = usWidthClass;
			this.fsType = fsType;
			this.ySubscriptXSize = ySubscriptXSize;
			this.ySubscriptYSize = ySubscriptYSize;
			this.ySubscriptXOffset = ySubscriptXOffset;
			this.ySubscriptYOffset = ySubscriptYOffset;
			this.ySuperscriptXSize = ySuperscriptXSize;
			this.ySuperscriptYSize = ySuperscriptYSize;
			this.ySuperscriptXOffset = ySuperscriptXOffset;
			this.ySuperscriptYOffset = ySuperscriptYOffset;
			this.yStrikeoutSize = yStrikeoutSize;
			this.yStrikeoutPosition = yStrikeoutPosition;
			this.sFamilyClass = sFamilyClass;
			this.panose = panose;
			this.ulUnicodeRange = ulUnicodeRange;
			this.achVendID = achVendID;
			this.fsSelection = fsSelection;
			this.fsFirstCharIndex = fsFirstCharIndex;
			this.fsLastCharIndex = fsLastCharIndex;

			// Below this point, the values may not actually be present
			this.sTypoAscender = sTypoAscender;
			this.sTypoDescender =sTypoDescender;
			this.sTypoLineGap =sTypoLineGap;
			this.usWinAscent =usWinAscent;
			this.usWinDescent =usWinDescent;
		}

		internal static TrueTypeOS2Table Read(FontFileReader reader, long offset, long length) {

			reader.Position = offset;

			ushort version = reader.ReadUInt16();
			short xAvgCharWidth = reader.ReadInt16();
			ushort usWeightClass = reader.ReadUInt16();
			ushort usWidthClass = reader.ReadUInt16();
			EmbeddingFlags fsType = (EmbeddingFlags)reader.ReadUInt16();
			short ySubscriptXSize = reader.ReadInt16();
			short ySubscriptYSize = reader.ReadInt16();
			short ySubscriptXOffset = reader.ReadInt16();
			short ySubscriptYOffset = reader.ReadInt16();
			short ySuperscriptXSize = reader.ReadInt16();
			short ySuperscriptYSize = reader.ReadInt16();
			short ySuperscriptXOffset = reader.ReadInt16();
			short ySuperscriptYOffset = reader.ReadInt16();
			short yStrikeoutSize = reader.ReadInt16();
			short yStrikeoutPosition = reader.ReadInt16();
			short sFamilyClass = reader.ReadInt16();
			byte[] panose = reader.ReadUInt8(10);
			uint[] ulUnicodeRange = reader.ReadUInt32(4);
			sbyte[] achVendID = reader.ReadInt8(4);
			FontSelectionFlags fsSelection = (FontSelectionFlags)reader.ReadUInt16();
			ushort fsFirstCharIndex = reader.ReadUInt16();
			ushort fsLastCharIndex = reader.ReadUInt16();

			// Below this point, these entries may not be defined, depending on whether the true OS/2 table is available
			short? sTypoAscender = null;
			short? sTypoDescender = null;
			short? sTypoLineGap = null;
			ushort? usWinAscent = null;
			ushort? usWinDescent = null;
			if(length > 68L) {
				sTypoAscender = reader.ReadInt16();
				sTypoDescender = reader.ReadInt16();
				sTypoLineGap = reader.ReadInt16();
				usWinAscent = reader.ReadUInt16();
				usWinDescent = reader.ReadUInt16();
			}

			return new TrueTypeOS2Table(version,
				xAvgCharWidth,
				usWeightClass, usWidthClass,
				fsType,
				ySubscriptXSize, ySubscriptYSize, ySubscriptXOffset, ySubscriptYOffset,
				ySuperscriptXSize, ySuperscriptYSize, ySuperscriptXOffset, ySuperscriptYOffset,
				yStrikeoutSize, yStrikeoutPosition,
				sFamilyClass,
				panose,
				ulUnicodeRange,
				achVendID,
				fsSelection,
				fsFirstCharIndex, fsLastCharIndex,
				// These ones may be undefined
				sTypoAscender, sTypoDescender, sTypoLineGap,
				usWinAscent, usWinDescent);
		}

	}

	public class TrueTypeGlyphData {
		public readonly short xMin;
		public readonly short yMin;
		public readonly short xMax;
		public readonly short yMax;

		public TrueTypeGlyphData(short xMin, short yMin, short xMax, short yMax) {
			this.xMin = xMin;
			this.yMin = yMin;
			this.xMax = xMax;
			this.yMax = yMax;
		}
	}

	public class TrueTypeGlyphTable {

		public readonly TrueTypeGlyphData[] glyphs;

		internal TrueTypeGlyphTable(TrueTypeGlyphData[] glyphs) {
			this.glyphs = glyphs;
		}

		internal static TrueTypeGlyphTable Read(FontFileReader reader, long offset, TrueTypeIndexToLocationTable loca) {

			//reader.Position = offset;

			TrueTypeGlyphData[] glyphs = new TrueTypeGlyphData[loca.lengths.Length];

			for (int i=0; i<loca.offsets.Length; i++) {
				reader.Position = offset + loca.offsets[i];

				reader.SkipInt16(1);// numberOfContours
				short xMin = reader.ReadFWord();
				short yMin = reader.ReadFWord();
				short xMax = reader.ReadFWord();
				short yMax = reader.ReadFWord();

				glyphs[i] = new TrueTypeGlyphData(xMin, yMin, xMax, yMax);
			}

			return new TrueTypeGlyphTable(glyphs);
		}

	}

}
