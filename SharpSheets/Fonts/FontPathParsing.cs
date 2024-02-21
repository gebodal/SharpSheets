using GeboPdf.Fonts.TrueType;
using SharpSheets.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Fonts {

	public static class FontPathParsing {

		#region FontPath Parsing

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static FontPath Parse(string fontStr, string sourceDir) {
			if (string.IsNullOrWhiteSpace(fontStr)) {
				throw new FormatException("Empty font string provided.");
			}

			FontPath? normalized = NormaliseFontPath(fontStr, sourceDir);

			if (normalized != null) {
				return normalized;
			}
			else {
				throw new FormatException($"Not a valid font name: \"{fontStr}\"");
			}
		}

		private static FontPath? NormaliseFontPath(string proposedPath, string sourceDir) {
			if(FontPathRegistry.FindFontPath(proposedPath) is FontPath fontPath) {
				return fontPath;
			}
			else if (FontPathRegistry.FindFontFamily(proposedPath) is FontPathGrouping grouping) {
				return grouping.Regular;
			}
			else if (TryFontPath(System.IO.Path.Combine(sourceDir, proposedPath), out string? fromSourcePath)) {
				return GetFontPath(fromSourcePath);
			}
			else if (TryFontPath(System.IO.Path.Combine(FontPathRegistry.GetSystemFontsDir(), proposedPath), out string? fromFontsDirPath)) {
				return GetFontPath(fromFontsDirPath);
			}
			else {
				return null;
			}
		}

		private static bool TryFontPath(string path, [MaybeNullWhen(false)] out string finalPath) {
			if (File.Exists(path)) {
				finalPath = path;
				return true;
			}

			if (!System.IO.Path.HasExtension(path)) {
				string[] currentDirMatches = Directory.GetFiles(System.IO.Path.GetDirectoryName(path) ?? "", System.IO.Path.GetFileNameWithoutExtension(path) + ".*", SearchOption.TopDirectoryOnly);
				if (currentDirMatches.Length > 0) {
					// Prefer a TrueType or OpenType font file, but default to the first
					finalPath = currentDirMatches.FirstOrDefault(m => m.EndsWith(StringComparison.InvariantCultureIgnoreCase, ".ttf", ".ttc", ".otf", ".otc")) ?? currentDirMatches[0];
					return true;
				}
			}
			// else // If it has an extension and doesn't exist, no need for else, just fail

			finalPath = null;
			return false;
		}

		private static FontPath GetFontPath(string path) {
			TrueTypeFontFileData[] fontFiles = FontPathRegistry.OpenFontFile(path, out bool isFontCollection);

			if(fontFiles.Length < 1) {
				throw new FormatException("Invalid font file specified (could not find any font data).");
			}

			return new FontPath(path, isFontCollection ? 0 : -1, fontFiles[0].EmbeddingFlags);
		}

		#endregion

		#region FontPathGrouping Parsing

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static FontPathGrouping ParseGrouping(string[] fontStr, string sourceDir) {
			//string[] parts = fontStr.SplitAndTrim(',');

			if (fontStr.Length == 0) {
				throw new FormatException("Empty font string provided.");
			}
			else if (fontStr.Length == 1 && FontPathRegistry.FindFontFamily(fontStr[0]) is FontPathGrouping grouping) {
				return grouping;
			}

			FontPath[] normalizedParts = fontStr.Select(p => Parse(p, sourceDir)).ToArray();

			/*
			if (normalizedParts.Where(p => p != null).Count() == 0 && parts.Length == 1) {
				// This is just a single font family name
				return FontPathUtils.FindFontFamily(fontStr);
			}
			*/

			return FromArray(normalizedParts);
		}

		/*
		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static FontPathGrouping1 MakeFrom(string[] fonts, string sourceDir) {
			if (fonts.Length == 0) {
				throw new FormatException("No fonts provided.");
			}

			FontPath[] normalized = fonts.Select(f => Parse(f, sourceDir)).ToArray();

			return FromArray(normalized);
		}
		*/

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		private static FontPathGrouping FromArray(params FontPath[] fontNames) {
			if (fontNames.Length == 1) {
				return new FontPathGrouping(fontNames[0], null, null, null);
			}
			else if (fontNames.Length == 2) {
				return new FontPathGrouping(fontNames[0], fontNames[1], null, null);
			}
			else if (fontNames.Length == 3) {
				return new FontPathGrouping(fontNames[0], fontNames[1], fontNames[2], null);
			}
			else if (fontNames.Length >= 4) {
				return new FontPathGrouping(fontNames[0], fontNames[1], fontNames[2], fontNames[3]);
			}
			else {
				throw new FormatException("Empty font array.");
			}
		}

		#endregion

	}

}
