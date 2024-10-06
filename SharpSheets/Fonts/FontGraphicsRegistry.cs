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
			lock (knownFonts) {
				if (!knownFonts.ContainsKey(font)) {
					PdfGlyphFont pdfFont = CreateFont(font.Path, font.Tags);
					knownFonts.Add(font, pdfFont);
				}
			}
			return knownFonts[font];
		}

		public static TrueTypeFontFileOutlines GetFontOutlines(FontPath font) {
			lock (knownOutlines) {
				if (!knownOutlines.ContainsKey(font)) {
					TrueTypeFontFileOutlines outlines = CreateOutlines(font);
					knownOutlines.Add(font, outlines);
				}
			}
			return knownOutlines[font];
		}

		public static TrueTypeFontFileOutlines GetFontOutlines(FontSetting font) {
			return GetFontOutlines(font.Path);
		}

		public static PdfGlyphFont GetPdfFont(FontSettingGrouping fonts, TextFormat format) {
			if (fonts is null) {
				return defaultFonts[format];
			}

			FontSetting? font = fonts[format] ?? fonts[TextFormat.REGULAR];

			if (font is null || font.Path is null) {
				return defaultFonts[format];
			}
			else {
				lock (knownFonts) {
					if (!knownFonts.ContainsKey(font)) {
						PdfGlyphFont pdfFont = CreateFont(font.Path, font.Tags);
						knownFonts.Add(font, pdfFont);
					}
				}
				return knownFonts[font];
			}
		}

		private static PdfGlyphFont CreateFont(FontPath fontPath, FontTags? tags) {
			if (fontPath.FontIndex >= 0) {
				return CIDFontFactory.CreateFont(fontPath.Path, fontPath.FontIndex, GetOpenTypeTags(tags));
			}
			else {
				return CIDFontFactory.CreateFont(fontPath.Path, GetOpenTypeTags(tags));
			}
		}
		private static PdfGlyphFont CreateFont(string fontUri, Stream fontStream, FontTags? fontTags) {
			return CIDFontFactory.CreateFont(fontUri, fontStream, GetOpenTypeTags(fontTags));
		}

		private static TrueTypeFontFileOutlines CreateOutlines(FontPath fontPath) {
			if (fontPath.FontIndex >= 0) {
				return TrueTypeCollectionOutlines.Open(fontPath.Path, fontPath.FontIndex);
			}
			else {
				return TrueTypeFontFileOutlines.Open(fontPath.Path);
			}
		}
		private static TrueTypeFontFileOutlines CreateOutlines(string fontUri, Stream fontStream) {
			FontFileReader reader = new FontFileReader(fontStream);
			return TrueTypeFontFileOutlines.Open(reader);
		}

		private static OpenTypeLayoutTags GetOpenTypeTags(FontTags? tags) {
			if (tags is null) { tags = FontTags.Default; }
			return new OpenTypeLayoutTags(MakeTag(tags.ScriptTag), MakeTag(tags.LangSysTag), tags.FeatureTags.Select(t => MakeTag(t)));
		}

		[return: NotNullIfNotNull(nameof(tag))]
		private static string? MakeTag(string? tag) {
			if (tag is not null && tag.Length < 4) {
				return tag + "    "[..(4 - tag.Length)];
			}
			else {
				return tag;
			}
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

		public static TrueTypeFontFileOutlines GetRegularDefaultOutlines() {
			return defaultFontOutlines[TextFormat.REGULAR];
		}
		public static TrueTypeFontFileOutlines GetBoldDefaultOutlines() {
			return defaultFontOutlines[TextFormat.BOLD];
		}
		public static TrueTypeFontFileOutlines GetItalicDefaultOutlines() {
			return defaultFontOutlines[TextFormat.ITALIC];
		}
		public static TrueTypeFontFileOutlines GetBoldItalicDefaultOutlines() {
			return defaultFontOutlines[TextFormat.BOLDITALIC];
		}

		private static readonly Dictionary<FontSetting, PdfGlyphFont> knownFonts;
		private static readonly Dictionary<FontPath, TrueTypeFontFileOutlines> knownOutlines;

		private static readonly IReadOnlyDictionary<TextFormat, PdfGlyphFont> defaultFonts;
		private static readonly IReadOnlyDictionary<TextFormat, TrueTypeFontFileOutlines> defaultFontOutlines;

		static FontGraphicsRegistry() {
			knownFonts = new Dictionary<FontSetting, PdfGlyphFont>();
			knownOutlines = new Dictionary<FontPath, TrueTypeFontFileOutlines>();

			System.Reflection.Assembly assembly = typeof(FontGraphicsRegistry).Assembly;

			Dictionary<TextFormat, PdfGlyphFont> defaultFontsDict = new Dictionary<TextFormat, PdfGlyphFont>();
			defaultFonts = defaultFontsDict;

			Dictionary<TextFormat, TrueTypeFontFileOutlines>  defaultFontOutlinesDict = new Dictionary<TextFormat, TrueTypeFontFileOutlines>();
			defaultFontOutlines = defaultFontOutlinesDict;

			foreach ((TextFormat fontFormat, string fontFileName) in defaultFontFiles) {

				string resourceName = GetResourceName(assembly, fontFileName);
				string resourceUri = "MANIFEST_RESOURCE:::" + resourceName;

				using (Stream stream = assembly.GetManifestResourceStream(resourceName)!) { // We know this resource exists
					PdfGlyphFont defaultFont = CreateFont(resourceUri, stream, FontTags.Default);

					defaultFontsDict[fontFormat] = defaultFont;
				}

				using (Stream stream = assembly.GetManifestResourceStream(resourceName)!) { // We know this resource exists
					TrueTypeFontFileOutlines defaultFontOutlines = CreateOutlines(resourceUri, stream);

					defaultFontOutlinesDict[fontFormat] = defaultFontOutlines;
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
