using GeboPdf.Fonts.TrueType;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SharpSheets.Fonts {

	public class FontPath : IEquatable<FontPath> {

		/// <summary>
		/// The system path for this font.
		/// </summary>
		public string Path { get; }

		/// <summary>
		/// If the font file contains multiple fonts (TrueType or OpenType Collection), then this
		/// will be the zero-based index of that font in the file, otherwise -1.
		/// </summary>
		public int FontIndex { get; }

		public EmbeddingFlags EmbeddingFlags { get; }

		public bool IsKnownEmbeddable { get { return EmbeddingFlags.IsKnownEmbeddable(); } }

		public FontPath(string path, int index, EmbeddingFlags embeddingFlags) {
			this.Path = System.IO.Path.GetFullPath(path); // TODO Check that path exists?
			this.FontIndex = index;
			this.EmbeddingFlags = embeddingFlags;
		}

		public bool Equals(FontPath? other) {
			if(other is null) { return false; }
			return this.Path == other.Path && this.FontIndex == other.FontIndex;
		}

		public override bool Equals(object? obj) {
			return Equals(obj as FontPath);
		}
		public override int GetHashCode() {
			return HashCode.Combine(Path, FontIndex);
		}
		public override string ToString() {
			return $"font:{Path}[{FontIndex}]";
		}

		public static bool Equals(FontPath? a, FontPath? b) {
			if(a is null) {
				return b is null;
			}
			return a.Equals(b);
		}

	}

	public class FontPathGrouping : IEquatable<FontPathGrouping> {

		public FontPath? Regular { get; }
		public FontPath? Bold { get; }
		public FontPath? Italic { get; }
		public FontPath? BoldItalic { get; }

		public FontPath? this[TextFormat format] { get { return GetFontPath(format); } }

		public FontPath? GetFontPath(TextFormat format) {
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

		public FontPathGrouping(FontPath? regularPath, FontPath? boldPath, FontPath? italicPath, FontPath? boldItalicPath) {
			Regular = regularPath;
			Bold = boldPath;
			Italic = italicPath;
			BoldItalic = boldItalicPath;
		}

		/*
		public FontPathGrouping1(FontPathGrouping1 source) {
			Regular = source.Regular;
			Bold = source.Bold;
			Italic = source.Italic;
			BoldItalic = source.BoldItalic;
		}
		*/

		public bool Equals(FontPathGrouping? other) {
			if(other is null) { return false; }
			return FontPath.Equals(this.Regular, other.Regular)
				&& FontPath.Equals(this.Bold, other.Bold)
				&& FontPath.Equals(this.Italic, other.Italic)
				&& FontPath.Equals(this.BoldItalic, other.BoldItalic);
		}

		public override bool Equals(object? obj) {
			return Equals(obj as FontPathGrouping);
		}

		public override int GetHashCode() {
			return HashCode.Combine(Regular, Bold, Italic, BoldItalic);
		}

	}

}
