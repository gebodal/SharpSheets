using SharpSheets.Canvas.Text;
using SharpSheets.Parsing;

namespace SharpSheets.Fonts {

	public class FontSetting : ISharpArgSupplemented, IEquatable<FontSetting> {

		public readonly FontPath Path;
		public readonly FontTags? Tags;

		/// <summary>
		/// Constructor for FontSetting.
		/// </summary>
		/// <param name="path">Font path to use for this font setting.</param>
		/// <param name="tags">Font tags to use for this font. This can include script,
		/// language system, and feature tags (if they are supported by the font
		/// specified).</param>
		public FontSetting(FontPath path, FontTags? tags = null) {
			Path = path;
			Tags = tags;
		}

		public static bool Equals(FontSetting? a, FontSetting? b) {
			if(a is null) { return b is null; }
			return FontPath.Equals(a.Path, b?.Path) && FontTags.Equals(a.Tags, b?.Tags);
		}

		public bool Equals(FontSetting? other) {
			return Equals(this, other);
		}

		public override bool Equals(object? obj) {
			return Equals(obj as FontSetting);
		}

		public override int GetHashCode() {
			return HashCode.Combine(Path, Tags);
		}

	}

	public class FontSettingGrouping : IEquatable<FontSettingGrouping> {

		public FontSetting? Regular { get; }
		public FontSetting? Bold { get; }
		public FontSetting? Italic { get; }
		public FontSetting? BoldItalic { get; }

		public FontSetting? this[TextFormat format] { get { return GetFontPath(format); } }

		public FontSetting? GetFontPath(TextFormat format) {
			if (format == TextFormat.REGULAR) {
				return Regular;
			}
			else if (format == TextFormat.BOLD) {
				return Bold;
			}
			else if (format == TextFormat.ITALIC) {
				return Italic;
			}
			else { // format == TextFormat.BOLDITALIC
				return BoldItalic;
			}
		}

		public FontSettingGrouping(FontSetting? regularPath, FontSetting? boldPath, FontSetting? italicPath, FontSetting? boldItalicPath) {
			Regular = regularPath;
			Bold = boldPath;
			Italic = italicPath;
			BoldItalic = boldItalicPath;
		}

		public FontSettingGrouping(FontSettingGrouping source) {
			Regular = source.Regular;
			Bold = source.Bold;
			Italic = source.Italic;
			BoldItalic = source.BoldItalic;
		}

		public bool Equals(FontSettingGrouping? other) {
			if (other is null) { return false; }
			return FontSetting.Equals(this.Regular, other.Regular)
				&& FontSetting.Equals(this.Bold, other.Bold)
				&& FontSetting.Equals(this.Italic, other.Italic)
				&& FontSetting.Equals(this.BoldItalic, other.BoldItalic);
		}

		public override bool Equals(object? obj) {
			return Equals(obj as FontSettingGrouping);
		}

		public override int GetHashCode() {
			return HashCode.Combine(Regular, Bold, Italic, BoldItalic);
		}

	}

}
