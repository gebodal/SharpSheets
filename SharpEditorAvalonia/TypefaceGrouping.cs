using Avalonia.Media;
using Avalonia.Media.Fonts;
using GeboPdf.Fonts;
using SharpSheets.Canvas.Text;
using SharpSheets.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditorAvalonia {

	public class TypefaceGrouping {

		private readonly struct TypefaceInfo {
			public readonly GlyphTypeface typeface;
			public readonly PdfGlyphFont pdfFont;
			public readonly FontSetting? origin;

			public TypefaceInfo(GlyphTypeface typeface, PdfGlyphFont pdfFont, FontSetting? origin) {
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

		public GlyphTypeface GetTypeface(TextFormat format) {
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

		public PdfGlyphFont GetPdfFont(TextFormat format) {
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

		private void SetFont(TextFormat format, GlyphTypeface typeface, PdfGlyphFont pdfFont, FontSetting? origin) {
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

		public static Uri GetFontUri(FontPath path) {
			return new Uri("file://" + path.Path + (path.FontIndex >= 0 ? $"#{path.FontIndex}" : ""));
		}

		public void SetFont(TextFormat format, FontSetting origin) {

			FontFamilyLoader.LoadFontAssets(new Uri("font.otf"));
			Avalonia.Media.Gly
			//new FontFamily()
			//new Avalonia.Media.Typeface()
			GlyphTypeface glyphs = new GlyphTypeface(GetFontUri(origin.Path));
			PdfGlyphFont pdfFont = FontGraphicsRegistry.GetPdfFont(origin); // PdfEncodings.WINANSI
			SetFont(format, glyphs, pdfFont, origin);
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

		public FontSettingGrouping GetFonts() {
			return new FontSettingGrouping(regular.origin, bold?.origin, italic?.origin, bolditalic?.origin);
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

		public float GetWidth(char c, TextFormat format, float fontsize) {
			return GetPdfFont(format).GetWidth(c.ToString(), fontsize);
		}
		/*
		public float GetKerning(char left, char right, TextFormat format, float fontsize) {
			return GetPdfFont(format).GetKerning(left, right, fontsize);
		}
		*/

		private static readonly GlyphTypeface regularGlyphs;
		private static readonly GlyphTypeface boldGlyphs;
		private static readonly GlyphTypeface italicGlyphs;
		private static readonly GlyphTypeface bolditalicGlyphs;

		static TypefaceGrouping() {
			regularGlyphs = new GlyphTypeface(FontGraphicsRegistry.GetRegularDefaultUri());
			boldGlyphs = new GlyphTypeface(FontGraphicsRegistry.GetBoldDefaultUri());
			italicGlyphs = new GlyphTypeface(FontGraphicsRegistry.GetItalicDefaultUri());
			bolditalicGlyphs = new GlyphTypeface(FontGraphicsRegistry.GetBoldItalicDefaultUri());
		}

		public TypefaceGrouping() {
			PdfGlyphFont regularDefault = FontGraphicsRegistry.GetRegularDefault();
			PdfGlyphFont boldDefault = FontGraphicsRegistry.GetBoldDefault();
			PdfGlyphFont italicDefault = FontGraphicsRegistry.GetItalicDefault();
			PdfGlyphFont boldItalicDefault = FontGraphicsRegistry.GetBoldItalicDefault();

			this.SetFont(TextFormat.REGULAR, regularGlyphs, regularDefault, null);
			this.SetFont(TextFormat.BOLD, boldGlyphs, boldDefault, null);
			this.SetFont(TextFormat.ITALIC, italicGlyphs, italicDefault, null);
			this.SetFont(TextFormat.BOLDITALIC, bolditalicGlyphs, boldItalicDefault, null);
		}

		public TypefaceGrouping(TypefaceGrouping source) {
			this.regular = source.regular;
			this.bold = source.bold;
			this.italic = source.italic;
			this.bolditalic = source.bolditalic;
		}
	}

}
