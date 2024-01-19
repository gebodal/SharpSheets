using GeboPdf.Fonts;
using GeboPdf.Fonts.TrueType;
using SharpSheets.Canvas.Text;
using SharpSheets.PDFs;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SharpSheets.Fonts {

	public static class FontGraphicsRegistry {

		public static PdfFont GetPdfFont(FontPath font) {
			if (!knownFonts.ContainsKey(font)) {
				PdfFont pdfFont = CreateFont(font);
				knownFonts.Add(font, pdfFont);
			}
			return knownFonts[font];
		}

		public static PdfFont GetPdfFont(FontPath? font, TextFormat format) {
			if (font is null) {
				return defaultFonts.GetPdfFont(format);
			}
			else {
				if (!knownFonts.ContainsKey(font)) {
					PdfFont pdfFont = CreateFont(font);
					knownFonts.Add(font, pdfFont);
				}
				return knownFonts[font];
			}
		}

		public static PdfFont GetPdfFont(FontPathGrouping fonts, TextFormat format) {
			if (fonts is null) {
				return defaultFonts.GetPdfFont(format);
			}

			FontPath? font = fonts[format] ?? fonts[TextFormat.REGULAR];

			if (font is null) {
				return defaultFonts.GetPdfFont(format);
			}
			else {
				if (!knownFonts.ContainsKey(font)) {
					PdfFont pdfFont = CreateFont(font);
					knownFonts.Add(font, pdfFont);
				}
				return knownFonts[font];
			}
		}

		private static PdfFont CreateFont(FontPath fontPath) {
			if (fontPath.FontIndex >= 0) {
				throw new ArgumentException("Font collections not surrently supported (requested font is part of a font collection file).");
			}
			return CIDFontFactory.CreateFont(fontPath.Path);
		}

		public static PdfFont GetRegularDefault() {
			return defaultFonts.GetPdfFont(TextFormat.REGULAR);
		}
		public static PdfFont GetBoldDefault() {
			return defaultFonts.GetPdfFont(TextFormat.BOLD);
		}
		public static PdfFont GetItalicDefault() {
			return defaultFonts.GetPdfFont(TextFormat.ITALIC);
		}
		public static PdfFont GetBoldItalicDefault() {
			return defaultFonts.GetPdfFont(TextFormat.BOLDITALIC);
		}

		private static readonly Dictionary<FontPath, PdfFont> knownFonts;
		private static readonly GeboFontPathGrouping defaultFonts;

		static FontGraphicsRegistry() {
			knownFonts = new Dictionary<FontPath, PdfFont>();

			defaultFonts = new GeboFontPathGrouping();
			defaultFonts.SetFont(TextFormat.REGULAR, PdfStandardFonts.Helvetica);
			defaultFonts.SetFont(TextFormat.BOLD, PdfStandardFonts.HelveticaBold);
			defaultFonts.SetFont(TextFormat.ITALIC, PdfStandardFonts.HelveticaOblique);
			defaultFonts.SetFont(TextFormat.BOLDITALIC, PdfStandardFonts.HelveticaBoldOblique);
		}

	}
}
