using SharpSheets.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Fonts {

	public class FontArgument : ISharpArgSupplemented {

		public readonly FontSettingGrouping Fonts;

		public class FontSettingArg : ISharpArgSupplemented {
			public readonly FontPath? Path;
			public readonly FontTags? Tags;

			/// <summary>
			/// 
			/// </summary>
			/// <param name="path">Font path to use for this font setting.</param>
			/// <param name="tags">Font tags to use for this font. This can include script,
			/// language system, and feature tags (if they are supported by the font
			/// specified).</param>
			public FontSettingArg(FontPath? path = null, FontTags? tags = null) {
				Path = path;
				Tags = tags;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fonts"></param>
		/// <param name="tags">Font tags to use as defaults for this font grouping. This can
		/// include script, language system, and feature tags (if they are supported by the font
		/// specified).</param>
		/// <param name="regular">Standard font to use for text without formatting.</param>
		/// <param name="bold">Font to use for bold text.</param>
		/// <param name="italic">Font to use for italic text.</param>
		/// <param name="bolditalic">Font to use for bold-italic text.</param>
		public FontArgument(
				FontPathGrouping? fonts = null,
				FontTags? tags = null,
				FontSettingArg? regular = null,
				FontSettingArg? bold = null,
				FontSettingArg? italic = null,
				FontSettingArg? bolditalic = null
			) {

			FontPath? regularPath = regular?.Path ?? fonts?.Regular;
			FontPath? boldPath = bold?.Path ?? fonts?.Bold;
			FontPath? italicPath = italic?.Path ?? fonts?.Italic;
			FontPath? bolditalicPath = bolditalic?.Path ?? fonts?.BoldItalic;

			FontSetting? regularFont = regularPath is not null ? new FontSetting(regularPath, regular?.Tags ?? tags) : null;
			FontSetting? boldFont = boldPath is not null ? new FontSetting(boldPath, bold?.Tags ?? tags) : null;
			FontSetting? italicFont = italicPath is not null ? new FontSetting(italicPath, italic?.Tags ?? tags) : null;
			FontSetting? bolditalicFont = bolditalicPath is not null ? new FontSetting(bolditalicPath, bolditalic?.Tags ?? tags) : null;

			Fonts = new FontSettingGrouping(regularFont, boldFont, italicFont, bolditalicFont);
		}

	}

	/*
	public static class FontArguments {

		public static FontSettingGrouping FinalFonts(FontGrouping? fonts, FontSettingCollection? fontOverrides) {
			return new FontSettingGrouping(
				fontOverrides?.regular ?? fonts?.Regular,
				fontOverrides?.bold ?? fonts?.Bold,
				fontOverrides?.italic ?? fonts?.Italic,
				fontOverrides?.bolditalic ?? fonts?.BoldItalic
				);
		}

		public class FontGrouping : ISharpArgSupplemented {

			public FontSetting? Regular => GetSetting(fontPaths?.Regular);
			public FontSetting? Bold => GetSetting(fontPaths?.Bold);
			public FontSetting? Italic => GetSetting(fontPaths?.Italic);
			public FontSetting? BoldItalic => GetSetting(fontPaths?.BoldItalic);

			public readonly FontPathGrouping? fontPaths;
			public readonly FontTags? tags;

			/// <summary>
			/// Constructor for FontGrouping.
			/// </summary>
			/// <param name="fonts">Font paths for this grouping.</param>
			/// <param name="tags">Font tags to use for this font grouping. This can include script,
			/// language system, and feature tags (if they are supported by the font
			/// specified).</param>
			public FontGrouping(FontPathGrouping? fonts = null, FontTags? tags = null) {
				this.fontPaths = fonts;
				this.tags = tags;
			}

			private FontSetting? GetSetting(FontPath? path) {
				if (path is null) { return null; }
				else { return new FontSetting(path, tags); }
			}

		}

		public class FontSettingCollection : ISharpArgsGrouping {
			public readonly FontSetting? regular;
			public readonly FontSetting? bold;
			public readonly FontSetting? italic;
			public readonly FontSetting? bolditalic;

			/// <summary>
			/// Constructor for FontSettingCollection.
			/// </summary>
			/// <param name="regular">Standard font to use for text without formatting.</param>
			/// <param name="bold">Font to use for bold text.</param>
			/// <param name="italic">Font to use for italic text.</param>
			/// <param name="bolditalic">Font to use for bold-italic text.</param>
			public FontSettingCollection(FontSetting? regular = null, FontSetting? bold = null, FontSetting? italic = null, FontSetting? bolditalic = null) {
				this.regular = regular;
				this.bold = bold;
				this.italic = italic;
				this.bolditalic = bolditalic;
			}
		}

	}
	*/

}
