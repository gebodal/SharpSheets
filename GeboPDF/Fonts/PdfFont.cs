using GeboPdf.Documents;
using GeboPdf.Fonts.TrueType;
using GeboPdf.Objects;
using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts {

	public abstract class PdfFont : IPdfDocumentContents {

		public PdfIndirectReference FontReference { get { return PdfIndirectReference.Create(FontDictionary); } }

		protected abstract AbstractPdfDictionary FontDictionary { get; }

		public IEnumerable<PdfObject> CollectObjects() {
			yield return FontDictionary;
			foreach(PdfObject fontObject in CollectFontObjects()) {
				yield return fontObject;
			}
		}

		public abstract IEnumerable<PdfObject> CollectFontObjects();

		public abstract byte[] GetBytes(string text);

		public abstract int GetWidth(string text);
		public abstract int GetAscent(string text);
		public abstract int GetDescent(string text);
		public abstract int GetKerning(char left, char right);

		public float GetWidth(string text, float fontsize) {
			return (GetWidth(text) * fontsize) / 1000f;
		}
		public float GetAscent(string text, float fontsize) {
			return (GetAscent(text) * fontsize) / 1000f;
		}
		public float GetDescent(string text, float fontsize) {
			return (GetDescent(text) * fontsize) / 1000f;
		}
		public float GetKerning(char left, char right, float fontsize) {
			return (GetKerning(left, right) * fontsize) / 1000f;
		}

		public float GetWidthWithKerning(string text) {
			float width = GetWidth(text);
			for (int i = 1; i < text.Length; i++) {
				width += GetKerning(text[i - 1], text[i]);
			}
			return width;
		}
		public float GetWidthWithKerning(string text, float fontsize) {
			return (GetWidthWithKerning(text) * fontsize) / 1000f;
		}

		public override int GetHashCode() => FontDictionary.GetHashCode();
		
		public override bool Equals(object? obj) {
			if(ReferenceEquals(this, obj)) {
				return true;
			}
			else if (obj is PdfFont other) {
				return FontDictionary.Equals(other.FontDictionary);
			}
			return false;
		}

		public static bool operator ==(PdfFont a, PdfFont b) {
			if (a is null) { return b is null; }
			else { return a.Equals(b); }
		}
		public static bool operator !=(PdfFont a, PdfFont b) {
			if (a is null) { return b is not null; }
			else { return !a.Equals(b); }
		}

	}

	public class PdfStandardFont : PdfFont {

		private static readonly Dictionary<PdfStandardFonts, PdfName> standardFontNames = new Dictionary<PdfStandardFonts, PdfName> {
			{ PdfStandardFonts.TimesRoman, new PdfName("Times-Roman") },
			{ PdfStandardFonts.TimesBold, new PdfName("Times-Bold") },
			{ PdfStandardFonts.TimesItalic, new PdfName("Times-Italic") },
			{ PdfStandardFonts.TimesBoldItalic, new PdfName("Times-BoldItalic") },
			{ PdfStandardFonts.Helvetica, new PdfName("Helvetica") },
			{ PdfStandardFonts.HelveticaBold, new PdfName("Helvetica-Bold") },
			{ PdfStandardFonts.HelveticaOblique, new PdfName("Helvetica-Oblique") },
			{ PdfStandardFonts.HelveticaBoldOblique, new PdfName("Helvetica-BoldOblique") },
			{ PdfStandardFonts.Courier, new PdfName("Courier") },
			{ PdfStandardFonts.CourierBold, new PdfName("Courier-Bold") },
			{ PdfStandardFonts.CourierOblique, new PdfName("Courier-Oblique") },
			{ PdfStandardFonts.CourierBoldOblique, new PdfName("Courier-BoldOblique") },
			{ PdfStandardFonts.Symbol, new PdfName("Symbol") },
			{ PdfStandardFonts.ZapfDingbats, new PdfName("ZapfDingbats") }
		};

		public static readonly Dictionary<PdfStandardFonts, Fonts.AfmFile> standardFontMetrics;

		public static readonly PdfStandardFont ZapfDingbats;

		static PdfStandardFont() {
			standardFontMetrics = new Dictionary<PdfStandardFonts, Fonts.AfmFile>();

			foreach (string afmResource in typeof(PdfFont).Assembly.GetManifestResourceNames().Where(r => r.EndsWith(".afm"))) {
				using (Stream afmStream = typeof(PdfFont).Assembly.GetManifestResourceStream(afmResource)!) {

					Fonts.AfmFile afmFile = Fonts.AfmFile.ReadFile(afmStream);
					string[] nameParts = afmResource.Split('.');
					PdfStandardFonts font = (PdfStandardFonts)Enum.Parse(typeof(PdfStandardFonts), nameParts[^2].Replace("-", ""));

					standardFontMetrics[font] = afmFile;
				}
			}

			ZapfDingbats = new PdfStandardFont(PdfStandardFonts.ZapfDingbats, null); // new PdfName("StandardEncoding")
		}


		private readonly PdfDictionary dict;
		protected override AbstractPdfDictionary FontDictionary { get { return dict; } }

		private readonly Fonts.AfmFile afmFile;

		public PdfStandardFont(PdfStandardFonts font, PdfName? encoding) { // TODO Accept PdfEncoding!
			dict = new PdfDictionary() {
				{ PdfNames.Type, PdfNames.Font },
				{ PdfNames.Subtype, PdfNames.Type1 },
				{ PdfNames.BaseFont, standardFontNames[font] }
			};
			if (encoding != null) {
				dict.Add(PdfNames.Encoding, encoding);
			}

			this.afmFile = standardFontMetrics[font];
		}

		public override IEnumerable<PdfObject> CollectFontObjects() {
			yield break;
		}

		public override byte[] GetBytes(string text) {
			// TODO Need to implement encoding here
			byte[] bytes = new byte[text.Length];
			for (int i = 0; i < text.Length; i++) {
				bytes[i] = (byte)((int)text[i]);
			}
			return bytes;
		}

		public override int GetWidth(string text) {
			int width = 0;
			for (int i = 0; i < text.Length; i++) {
				// TODO Do we need to consider kerning here?
				uint c = (uint)text[i];
				if (afmFile.metrics.TryGetValue(c, out CharMetric cMetric)) {
					width += cMetric.width;
				}
				else {
					width += 0;
				}
			}
			return width;
		}

		public override int GetAscent(string text) {
			if(text.Length == 0) {
				return 0;
			}

			int ascent = int.MinValue;
			for (int i = 0; i < text.Length; i++) {
				uint c = (uint)text[i];
				if (afmFile.metrics.TryGetValue(c, out CharMetric cMetric) && cMetric.bBox.ury > ascent) {
					ascent = cMetric.bBox.ury;
				}
			}
			return ascent;
		}

		public override int GetDescent(string text) {
			if (text.Length == 0) {
				return 0;
			}

			int descent = int.MaxValue;
			for (int i = 0; i < text.Length; i++) {
				uint c = (uint)text[i];
				if (afmFile.metrics.TryGetValue(c, out CharMetric cMetric) && cMetric.bBox.lly < descent) {
					descent = cMetric.bBox.lly;
				}
			}
			return descent;
		}

		public override int GetKerning(char left, char right) {
			return 0;
		}

	}

	public enum PdfStandardFonts {
		TimesRoman,
		TimesBold,
		TimesItalic,
		TimesBoldItalic,
		Helvetica,
		HelveticaBold,
		HelveticaOblique,
		HelveticaBoldOblique,
		Courier,
		CourierBold,
		CourierOblique,
		CourierBoldOblique,
		Symbol,
		ZapfDingbats
	}

	public class PdfType0Font : PdfFont {

		private readonly PdfDictionary dict;
		protected override AbstractPdfDictionary FontDictionary { get { return dict; } }

		private readonly Type2CIDFont cidFont;
		private readonly AbstractPdfStream? toUnicode;

		private readonly Dictionary<uint, ushort> unicodeToGID;
		private readonly int[] advanceWidths;
		private readonly int[] ascents;
		private readonly int[] descents;
		private readonly Dictionary<uint, int> kerning;

		public PdfType0Font(Type2CIDFont cidFont, Dictionary<uint,ushort> unicodeToGID, int[] advanceWidths, int[] ascents, int[] descents, Dictionary<uint, int> kerning, AbstractPdfStream? toUnicode) {
			this.cidFont = cidFont;
			this.toUnicode = toUnicode;
			this.unicodeToGID = unicodeToGID;
			this.advanceWidths = advanceWidths;
			this.ascents = ascents;
			this.descents = descents;
			this.kerning = kerning;

			PdfArray descendantFonts = new PdfArray(this.cidFont.FontDictionaryReference);

			// If the descendant is a Type 0 CIDFont, this name should be the concatenation of the CIDFont’s BaseFont name, a hyphen, and the CMap name given in the Encoding entry (or the CMapName entry in the CMap). If the descendant is a Type 2 CIDFont, this name should be the same as the CIDFont’s BaseFontname.
			PdfName fontName = cidFont.FontName;

			dict = new PdfDictionary() {
				{ PdfNames.Type, PdfNames.Font },
				{ PdfNames.Subtype, PdfNames.Type0 },
				{ PdfNames.BaseFont, fontName },
				{ PdfNames.Encoding, new PdfName("Identity-H") },
				{ PdfNames.DescendantFonts, descendantFonts }
			};

			if (this.toUnicode != null) {
				dict.Add(PdfNames.ToUnicode, PdfIndirectReference.Create(this.toUnicode));
			}
		}

		public override IEnumerable<PdfObject> CollectFontObjects() {
			foreach(PdfObject cidFontObj in cidFont.CollectObjects()) {
				yield return cidFontObj;
			}

			if (toUnicode != null) {
				yield return toUnicode;
			}
		}

		public override byte[] GetBytes(string text) {
			List<byte> bytes = new List<byte>();
			foreach (uint codePoint in GetCodePoints(text)) {
				ushort gid;
				if(!unicodeToGID.TryGetValue(codePoint, out gid)) {
					gid = (ushort)0;
				}
				byte[] glyphBytes = BitConverter.GetBytes(gid);
				if (BitConverter.IsLittleEndian) {
					Array.Reverse(glyphBytes);
				}
				bytes.AddRange(glyphBytes);
			}
			return bytes.ToArray();
		}

		private static IEnumerable<uint> GetCodePoints(string text) {
			for (var i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1) {
				uint codepoint = (uint)char.ConvertToUtf32(text, i);
				yield return codepoint;
			}
		}

		public override int GetWidth(string text) {
			int width = 0;
			for (int i = 0; i < text.Length; i++) {
				// What do we do about kerning here?
				uint c = (uint)text[i];
				ushort gid = unicodeToGID.GetValueOrDefault(c, (ushort)0);
				width += advanceWidths[gid];
			}
			return width;
		}

		public override int GetAscent(string text) {
			int ascent = 0;
			for (int i = 0; i < text.Length; i++) {
				uint c = (uint)text[i];
				ushort gid = unicodeToGID.GetValueOrDefault(c, (ushort)0);
				if (i == 0 || ascents[gid] > ascent) {
					ascent = ascents[gid];
				}
			}
			return ascent;
		}

		public override int GetDescent(string text) {
			int descent = 0;
			for (int i = 0; i < text.Length; i++) {
				uint c = (uint)text[i];
				ushort gid = unicodeToGID.GetValueOrDefault(c, (ushort)0);
				if (i == 0 || descents[gid] < descent) {
					descent = descents[gid];
				}
			}
			return descent;
		}

		public override int GetKerning(char left, char right) {
			ushort leftGID = unicodeToGID.GetValueOrDefault(left, (ushort)0);
			ushort rightGID = unicodeToGID.GetValueOrDefault(right, (ushort)0);
			uint pair = TrueTypeKerningTable.CombineGlyphs(leftGID, rightGID);
			return kerning.GetValueOrDefault(pair, 0);
		}

	}

	public class Type2CIDFont : IPdfDocumentContents {

		private static readonly AbstractPdfDictionary CIDSystemInfoIdentityDictionary = new PdfDictionary() {
			{ PdfNames.Registry, new PdfTextString("Adobe") },
			{ PdfNames.Ordering, new PdfTextString("Identity") },
			{ PdfNames.Supplement, new PdfInt(0) }
		};

		private readonly PdfDictionary dict;
		public PdfIndirectReference FontDictionaryReference { get; }

		public PdfName FontName { get; }
		private readonly FontDescriptor fontDescriptor;

		public Type2CIDFont(string fontName, FontDescriptor fontDescriptor, int defaultWidth, AbstractPdfArray widths) {
			this.fontDescriptor = fontDescriptor;

			this.FontName = new PdfName(fontName);

			dict = new PdfDictionary() {
				{ PdfNames.Type, PdfNames.Font },
				{ PdfNames.Subtype, PdfNames.CIDFontType2 },
				{ PdfNames.BaseFont, this.FontName }, // The PostScript name of the CIDFont. For Type 0 CIDFonts, this is usually the value of the CIDFontName entry in the CIDFont program. For Type 2 CIDFonts, it is derived the same way as for a simple TrueType font; see Section 5.5.2, “TrueType Fonts.” In either case, the name can have a subset prefix if appropriate; see Section 5.5.3, “Font Subsets.”
				{ PdfNames.CIDSystemInfo, CIDSystemInfoIdentityDictionary },
				{ PdfNames.FontDescriptor, this.fontDescriptor.DictionaryReference },
				{ PdfNames.DefaultWidth, new PdfInt(defaultWidth) },
				{ PdfNames.Widths, widths }
			};

			FontDictionaryReference = PdfIndirectReference.Create(dict);
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return dict;
			foreach(PdfObject fontDescriptorObj in fontDescriptor.CollectObjects()) {
				yield return fontDescriptorObj;
			}
		}
	}

	public class FontDescriptor : IPdfDocumentContents {

		private readonly PdfDictionary dict;
		public PdfIndirectReference DictionaryReference { get; }

		private readonly AbstractPdfStream fontFile;

		public FontDescriptor(string fontName, AbstractPdfStream fontFile, FontDescriptorFlags flags, PdfRectangle fontBBox, float italicAngle, float ascent, float descent, float capHeight, float stemV) {
			this.fontFile = fontFile;
			
			dict = new PdfDictionary() {
				{ PdfNames.Type, PdfNames.FontDescriptor },
				{ PdfNames.FontName, new PdfName(fontName) },
				{ PdfNames.FontFile2, PdfIndirectReference.Create(this.fontFile) },
				{ PdfNames.Flags, new PdfInt((int)((uint)flags)) },
				{ PdfNames.FontBBox, fontBBox },
				{ PdfNames.ItalicAngle, new PdfFloat(italicAngle) },
				{ PdfNames.Ascent, new PdfFloat(ascent) },
				{ PdfNames.Descent, new PdfFloat(descent) },
				{ PdfNames.CapHeight, new PdfFloat(capHeight) },
				{ PdfNames.StemV, new PdfFloat(stemV) }
				// CIDSet?
			};

			DictionaryReference = PdfIndirectReference.Create(dict);
		}

		public IEnumerable<PdfObject> CollectObjects() {
			yield return dict;
			yield return fontFile;
		}

	}

	[Flags]
	public enum FontDescriptorFlags : uint {
		None = 0,
		/// <summary>
		/// All glyphs have the same width (as opposed to proportional or variable-pitch fonts, which have different widths). (Bit 1)
		/// </summary>
		FixedPitch = 1 << 0,
		/// <summary>
		/// Glyphs have serifs, which are short strokes drawn at an angle on the top and bottom of glyph stems. (Sans serif fonts do not have serifs.) (Bit 2)
		/// </summary>
		Serif = 1 << 1,
		/// <summary>
		/// Font contains glyphs outside the Adobe standard Latin character set. This flag and the Nonsymbolic flag cannot both be set or both be clear (see below). (Bit 3)
		/// </summary>
		Symbolic = 1 << 2,
		/// <summary>
		/// Glyphs resemble cursive handwriting. (Bit 4)
		/// </summary>
		Script = 1 << 3,
		/// <summary>
		/// Font uses the Adobe standard Latin character set or a subset of it (see below). (Bit 6)
		/// </summary>
		Nonsymbolic = 1 << 5,
		/// <summary>
		/// Glyphs have dominant vertical strokes that are slanted. (Bit 7)
		/// </summary>
		Italic = 1 << 6,
		/// <summary>
		/// Font contains no lowercase letters; typically used for display purposes, such as for titles or headlines. (Bit 17)
		/// </summary>
		AllCap = 1 << 16,
		/// <summary>
		/// Font contains both uppercase and lowercase letters. The uppercase letters are similar to those in the regular version of the same typeface family. The glyphs for the lowercase letters have the same shapes as the corresponding uppercase letters, but they are sized and their proportions adjusted so that they have the same size and stroke weight as lowercase glyphs in the same typeface family. (Bit 18)
		/// </summary>
		SmallCap = 1 << 17,
		/// <summary>
		/// The ForceBold flag determines whether bold glyphs are painted with extra pixels even at very small text sizes. (Bit 19)
		/// </summary>
		ForceBold = 1 << 18
	}

	public static class FontDescriptorFlagsUtils {

		public static bool IsValid(this FontDescriptorFlags flags) {
			bool symbolic = (flags & FontDescriptorFlags.Symbolic) == FontDescriptorFlags.Symbolic;
			bool nonsymbolic = (flags & FontDescriptorFlags.Nonsymbolic) == FontDescriptorFlags.Nonsymbolic;

			return symbolic ^ nonsymbolic;
		}

	}

	public class TrueTypeFontProgramStream : AbstractPdfStream {

		private readonly MemoryStream fontProgram;
		private readonly bool openType;

		public TrueTypeFontProgramStream(MemoryStream fontProgram, bool openType) {
			this.fontProgram = fontProgram;
			this.openType = openType;
		}

		public override bool AllowEncoding { get; } = true;

		public override MemoryStream GetStream() {
			return fontProgram;
		}

		public override int Count {
			get {
				int count = 1;
				if (openType) {
					count += 1;
				}
				return count;
			}
		}

		public override IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() {
			yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Length1, new PdfInt(fontProgram.Length));

			if (openType) {
				yield return new KeyValuePair<PdfName, PdfObject>(PdfNames.Subtype, PdfNames.OpenType);
			}

			// Metadata?
		}
	}

}
