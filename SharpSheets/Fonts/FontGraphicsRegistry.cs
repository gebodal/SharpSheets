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

		public static PdfGlyphFont GetPdfFont(FontSetting font) {
			if (!knownFonts.ContainsKey(font)) {
				PdfGlyphFont pdfFont = CreateFont(font.Path, font.Tags);
				knownFonts.Add(font, pdfFont);
			}
			return knownFonts[font];
		}

		/*
		public static PdfGlyphFont GetPdfFont(FontPath? font, TextFormat format) {
			if (font is null) {
				return defaultFonts[format];
			}
			else {
				if (!knownFonts.ContainsKey(font)) {
					PdfGlyphFont pdfFont = CreateFont(font);
					knownFonts.Add(font, pdfFont);
				}
				return knownFonts[font];
			}
		}
		*/

		public static PdfGlyphFont GetPdfFont(FontSettingGrouping fonts, TextFormat format) {
			if (fonts is null) {
				return defaultFonts[format];
			}

			FontSetting? font = fonts[format] ?? fonts[TextFormat.REGULAR];

			if (font is null || font.Path is null) {
				return defaultFonts[format];
			}
			else {
				if (!knownFonts.ContainsKey(font)) {
					PdfGlyphFont pdfFont = CreateFont(font.Path, font.Tags);
					knownFonts.Add(font, pdfFont);
				}
				return knownFonts[font];
			}
		}

		private static PdfGlyphFont CreateFont(FontPath fontPath, FontTags? tags) {
			if (fontPath.FontIndex >= 0) {
				throw new ArgumentException("Font collections not surrently supported (requested font is part of a font collection file).");
			}
			return CIDFontFactory.CreateFont(fontPath.Path, GetOpenTypeTags(tags));
		}
		private static PdfGlyphFont CreateFont(string fontUri, Stream fontStream, FontTags? fontTags) {
			return CIDFontFactory.CreateFont(fontUri, fontStream, GetOpenTypeTags(fontTags));
		}

		private static OpenTypeLayoutTags GetOpenTypeTags(FontTags? tags) {
			if (tags is null) { tags = FontTags.Default; }
			return new OpenTypeLayoutTags(tags.ScriptTag, tags.LangSysTag, tags.FeatureTags);
		}

		public static PdfGlyphFont GetRegularDefault() {
			return defaultFonts[TextFormat.REGULAR];
		}
		public static PdfGlyphFont GetBoldDefault() {
			return defaultFonts[TextFormat.BOLD];
		}
		public static PdfGlyphFont GetItalicDefault() {
			return defaultFonts[TextFormat.ITALIC];
		}
		public static PdfGlyphFont GetBoldItalicDefault() {
			return defaultFonts[TextFormat.BOLDITALIC];
		}

		public static Uri GetRegularDefaultUri() {
			return defaultFontUris[TextFormat.REGULAR];
		}
		public static Uri GetBoldDefaultUri() {
			return defaultFontUris[TextFormat.BOLD];
		}
		public static Uri GetItalicDefaultUri() {
			return defaultFontUris[TextFormat.ITALIC];
		}
		public static Uri GetBoldItalicDefaultUri() {
			return defaultFontUris[TextFormat.BOLDITALIC];
		}

		private static readonly Dictionary<FontSetting, PdfGlyphFont> knownFonts;

		private static readonly IReadOnlyDictionary<TextFormat, PdfGlyphFont> defaultFonts;
		private static readonly IReadOnlyDictionary<TextFormat, Uri> defaultFontUris;

		static FontGraphicsRegistry() {
			knownFonts = new Dictionary<FontSetting, PdfGlyphFont>();

			System.Reflection.Assembly assembly = typeof(FontGraphicsRegistry).Assembly;

			Dictionary<TextFormat, PdfGlyphFont>  defaultFontsDict = new Dictionary<TextFormat, PdfGlyphFont>();
			defaultFonts = defaultFontsDict;

			Dictionary<TextFormat, Uri>  defaultFontUrisDict = new Dictionary<TextFormat, Uri>();
			defaultFontUris = defaultFontUrisDict;

			//defaultFonts.SetFont(TextFormat.REGULAR, PdfStandardFonts.Helvetica);
			//defaultFonts.SetFont(TextFormat.BOLD, PdfStandardFonts.HelveticaBold);
			//defaultFonts.SetFont(TextFormat.ITALIC, PdfStandardFonts.HelveticaOblique);
			//defaultFonts.SetFont(TextFormat.BOLDITALIC, PdfStandardFonts.HelveticaBoldOblique);

			string appDataFolder = SharpSheetsData.GetAssemblyDataDir(); // TODO Is this a good place?
			string fontDataFolder = Path.Combine(appDataFolder, "fonts");

			if(!Directory.Exists(fontDataFolder)) {
				Directory.CreateDirectory(fontDataFolder);
			}

			foreach ((TextFormat fontFormat, string fontFileName) in defaultFontFiles) {

				string resourceName = GetResourceName(assembly, fontFileName);
				string diskPath = Path.Combine(fontDataFolder, fontFileName);

				if (!File.Exists(diskPath)) {
					using (Stream fontResourceStream = assembly.GetManifestResourceStream(resourceName)!) { // We know this resource exists
						using (FileStream fileStream = File.Create(diskPath)) {
							fontResourceStream.CopyTo(fileStream);
						}
					}
				}

				defaultFontUrisDict[fontFormat] = new Uri(diskPath);

				using (Stream stream = assembly.GetManifestResourceStream(resourceName)!) { // We know this resource exists
					PdfGlyphFont defaultFont = CreateFont(diskPath, stream, FontTags.Default);

					defaultFontsDict[fontFormat] = defaultFont;
				}
			}
		}

		private static readonly IReadOnlyDictionary<TextFormat, string> defaultFontFiles = new Dictionary<TextFormat, string>() {
			{ TextFormat.REGULAR, "LiberationSans-Regular.ttf" },
			{ TextFormat.BOLD, "LiberationSans-Bold.ttf" },
			{ TextFormat.ITALIC, "LiberationSans-Italic.ttf" },
			{ TextFormat.BOLDITALIC, "LiberationSans-BoldItalic.ttf" }
		};

		private static string GetResourceName(System.Reflection.Assembly assembly, string filename) {
			return assembly.GetManifestResourceNames()
				.FirstOrDefault(str => str.EndsWith(filename)) ?? throw new InvalidOperationException($"Could not find embedded resource {filename}");
		}

	}

	/*
	public static class SharpGeboFonts {

		private static readonly Dictionary<FontPath, PdfFont> registry = new Dictionary<FontPath, PdfFont>();
		private static readonly Dictionary<PdfStandardFonts, PdfFont> standardRegistry = new Dictionary<PdfStandardFonts, PdfFont>();

		public static PdfFont GetFont(FontPath path) {
			if (registry.TryGetValue(path, out PdfFont? existing)) {
				return existing;
			}

			PdfFont font = CIDFontFactory.CreateFont(path.Path);
			registry.Add(path, font);
			return font;
		}

		public static PdfFont GetFont(PdfStandardFonts standardFont) {
			if (standardRegistry.TryGetValue(standardFont, out PdfFont? existing)) {
				return existing;
			}

			PdfFont font = new PdfStandardFont(standardFont, new GeboPdf.Objects.PdfName("WinAnsiEncoding")); // TODO Encoding???
			standardRegistry.Add(standardFont, font);
			return font;
		}

	}
	*/

	public class PdfFontPathGrouping {

		private (FontSetting? setting, PdfGlyphFont font) regular;
		private (FontSetting? setting, PdfGlyphFont font)? bold;
		private (FontSetting? setting, PdfGlyphFont font)? italic;
		private (FontSetting? setting, PdfGlyphFont font)? bolditalic;

		public PdfGlyphFont GetPdfFont(TextFormat format) {
			if (format == TextFormat.REGULAR) {
				return regular.font;
			}
			else if (format == TextFormat.BOLD) {
				return bold?.font ?? regular.font;
			}
			else if (format == TextFormat.ITALIC) {
				return italic?.font ?? regular.font;
			}
			else { // format == TextFormat.BOLDITALIC
				return bolditalic?.font ?? regular.font;
			}
		}

		private void SetFont(TextFormat format, FontSetting? origin, PdfGlyphFont font) {
			(FontSetting?, PdfGlyphFont) info = (origin, font);
			if (format == TextFormat.REGULAR) {
				regular = info;
			}
			else if (format == TextFormat.BOLD) {
				bold = info;
			}
			else if (format == TextFormat.ITALIC) {
				italic = info;
			}
			else { // format == TextFormat.BOLDITALIC
				bolditalic = info;
			}
		}

		public void SetFont(TextFormat format, FontSetting origin) {
			PdfGlyphFont pdfFont = FontGraphicsRegistry.GetPdfFont(origin);
			SetFont(format, origin, pdfFont);
		}

		/*
		public void SetFont(TextFormat format, PdfStandardFonts standardFont) {
			PdfFont pdfFont = SharpGeboFonts.GetFont(standardFont);
			SetFont(format, null, pdfFont);
		}
		*/

		public void SetFont(TextFormat format, PdfGlyphFont font) {
			SetFont(format, null, font);
		}

		public void SetNull(TextFormat format) {
			if (format == TextFormat.REGULAR) {
				//regular = null; // Throw error?
				SetFont(TextFormat.REGULAR, FontGraphicsRegistry.GetRegularDefault()); // Better?
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

		public FontSettingGrouping GetFonts() {
			return new FontSettingGrouping(regular.setting, bold?.setting, italic?.setting, bolditalic?.setting);
		}

		public PdfFontPathGrouping() {
			SetFont(TextFormat.REGULAR, FontGraphicsRegistry.GetRegularDefault());
			SetFont(TextFormat.BOLD, FontGraphicsRegistry.GetBoldDefault());
			SetFont(TextFormat.ITALIC, FontGraphicsRegistry.GetItalicDefault());
			SetFont(TextFormat.BOLDITALIC, FontGraphicsRegistry.GetBoldItalicDefault());
		}

		public PdfFontPathGrouping(PdfFontPathGrouping source) {
			this.regular = source.regular;
			this.bold = source.bold;
			this.italic = source.italic;
			this.bolditalic = source.bolditalic;
		}
	}

}
