using System;
using Typeface = System.Windows.Media.Typeface;
using System.Linq;
using GeboPdf.Fonts;
using SharpSheets.Canvas.Text;
using SharpSheets.Fonts;
using SharpSheets.PDFs;
using System.Windows.Media;
using System.Windows;
using SharpSheets.Canvas;

namespace SharpEditor {

	public class TypefaceGrouping {

		private struct TypefaceInfo {
			public readonly Typeface typeface;
			public readonly PdfFont pdfFont;
			public readonly FontPath? origin;

			public TypefaceInfo(Typeface typeface, PdfFont pdfFont, FontPath? origin) {
				this.typeface = typeface;
				this.pdfFont = pdfFont;
				this.origin = origin;
			}
		}

		private TypefaceInfo regular;
		private TypefaceInfo? bold;
		private TypefaceInfo? italic;
		private TypefaceInfo? bolditalic;

		//public Typeface Regular { get; set; }
		//public Typeface Bold { get { return bold ?? Regular; } set { bold = value; } }
		//public Typeface Italic { get { return italic ?? Regular; } set { italic = value; } }
		//public Typeface BoldItalic { get { return bolditalic ?? Regular; } set { bolditalic = value; } }

		/*
		public Typeface this[TextFormat format] {
			get { return GetFont(format); }
			set { SetFont(format, value); }
		}
		*/

		public Typeface GetTypeface(TextFormat format) {
			if (format == TextFormat.REGULAR) {
				return regular.typeface;
			}
			else if (format == TextFormat.BOLD) {
				return bold?.typeface ?? regular.typeface;
			}
			else if (format == TextFormat.ITALIC) {
				return italic?.typeface ?? regular.typeface;
			}
			else { // format == TextFormat.BOLDITALIC
				return bolditalic?.typeface ?? regular.typeface;
			}
		}

		private PdfFont GetPdfFont(TextFormat format) {
			if (format == TextFormat.REGULAR) {
				return regular.pdfFont;
			}
			else if (format == TextFormat.BOLD) {
				return bold?.pdfFont ?? regular.pdfFont;
			}
			else if (format == TextFormat.ITALIC) {
				return italic?.pdfFont ?? regular.pdfFont;
			}
			else { // format == TextFormat.BOLDITALIC
				return bolditalic?.pdfFont ?? regular.pdfFont;
			}
		}

		private void SetFont(TextFormat format, Typeface typeface, PdfFont pdfFont, FontPath? origin) {
			TypefaceInfo typefaceInfo = new TypefaceInfo(typeface, pdfFont, origin);
			if (format == TextFormat.REGULAR) {
				regular = typefaceInfo;
			}
			else if (format == TextFormat.BOLD) {
				bold = typefaceInfo;
			}
			else if (format == TextFormat.ITALIC) {
				italic = typefaceInfo;
			}
			else { // format == TextFormat.BOLDITALIC
				bolditalic = typefaceInfo;
			}
		}

		public void SetFont(TextFormat format, FontPath origin) {
			GlyphTypeface glyphs = new GlyphTypeface(new Uri(origin.Path));
			FontFamily family = new FontFamily(origin.Path + "#" + glyphs.FamilyNames.First().Value);
			Typeface typeface = new Typeface(family, glyphs.Style, glyphs.Weight, glyphs.Stretch);
			PdfFont pdfFont = FontGraphicsRegistry.CreateFont(origin); // PdfEncodings.WINANSI
			SetFont(format, typeface, pdfFont, origin);
		}

		// For opening a font from a Stream?
		// https://stackoverflow.com/a/65662363/11002708

		public void SetNull(TextFormat format) {
			if (format == TextFormat.REGULAR) {
				//regular = null; // Throw error?
			}
			else if (format == TextFormat.BOLD) {
				bold = null;
			}
			else if (format == TextFormat.ITALIC) {
				italic = null;
			}
			else { // format == TextFormat.BOLDITALIC
				bolditalic = null;
			}
		}

		/*
		public IEnumerator<Typeface> GetEnumerator() {
			yield return Regular;
			if (bold != null) { yield return bold; }
			if (italic != null) { yield return italic; }
			if (bolditalic != null) { yield return bolditalic; }
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
		*/

		public FontPathGrouping GetFonts() {
			return new FontPathGrouping(regular.origin, bold?.origin, italic?.origin, bolditalic?.origin);
		}

		/*
		private IEnumerable<PdfFont> GetPdfFonts() {
			yield return regular.pdfFont;
			if (bold?.pdfFont != null) { yield return bold?.pdfFont; }
			if (italic?.pdfFont != null) { yield return italic?.pdfFont; }
			if (bolditalic?.pdfFont != null) { yield return bolditalic?.pdfFont; }
		}
		*/

		public float GetAscent(string text, TextFormat format, float fontsize) {
			return GetPdfFont(format).GetAscent(text, fontsize);
		}

		public float GetDescent(string text, TextFormat format, float fontsize) {
			return GetPdfFont(format).GetDescent(text, fontsize);
		}

		public float GetWidth(string text, TextFormat format, float fontsize) {
			return GetPdfFont(format).GetWidth(text, fontsize);
		}

		public TypefaceGrouping() {
			FontFamily defaultFontFamily = new FontFamily("Arial");

			PdfFont regularDefault = FontGraphicsRegistry.GetRegularDefault();
			PdfFont boldDefault = FontGraphicsRegistry.GetBoldDefault();
			PdfFont italicDefault = FontGraphicsRegistry.GetItalicDefault();
			PdfFont boldItalicDefault = FontGraphicsRegistry.GetBoldItalicDefault();

			this.SetFont(
				TextFormat.REGULAR,
				new Typeface(defaultFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
				regularDefault, null);
			this.SetFont(
				TextFormat.BOLD,
				new Typeface(defaultFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
				boldDefault, null);
			this.SetFont(
				TextFormat.ITALIC,
				new Typeface(defaultFontFamily, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal),
				italicDefault, null);
			this.SetFont(
				TextFormat.BOLDITALIC,
				new Typeface(defaultFontFamily, FontStyles.Italic, FontWeights.Bold, FontStretches.Normal),
				boldItalicDefault, null);
		}

		public TypefaceGrouping(TypefaceGrouping source) {
			this.regular = source.regular;
			this.bold = source.bold;
			this.italic = source.italic;
			this.bolditalic = source.bolditalic;
		}
	}

}
