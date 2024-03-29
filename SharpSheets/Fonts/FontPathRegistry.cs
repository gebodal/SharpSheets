﻿using GeboPdf.Fonts;
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

	public static class FontPathRegistry {

		public static string GetSystemFontsDir() {
			// TODO Make more robust (i.e. system agnostic)
			string? windir = Environment.GetEnvironmentVariable("windir");
			return windir != null ? Path.Combine(windir, "fonts") : "";
		}

		public static bool FontPathIsCollection(string path) {
			return path.EndsWith(StringComparison.InvariantCultureIgnoreCase, ".ttc", ".otc");
		}

		#region FontPath Registry

		public static FontPathGrouping? FindFontFamily(string fontName) {
			if (!AllRegistered) {
				RegisterAll(); // Now FamilyRegistry/FontRegistry is definitely not null
			}

			if (FamilyRegistry.TryGetValue(fontName, out Dictionary<TextFormat, FontPath>? register)) {
				return new FontPathGrouping(
					register.GetValueOrFallback(TextFormat.REGULAR, null),
					register.GetValueOrFallback(TextFormat.BOLD, null),
					register.GetValueOrFallback(TextFormat.ITALIC, null),
					register.GetValueOrFallback(TextFormat.BOLDITALIC, null)
					);
			}
			else {
				return null;
			}
		}

		public static FontPath? FindFontPath(string fontName) {
			if (!AllRegistered) {
				RegisterAll(); // Now FamilyRegistry/FontRegistry is definitely not null
			}

			if (FontRegistry.TryGetValue(fontName, out FontPath? singleton)) {
				return singleton;
			}
			else {
				return null;
			}
		}

		public static IEnumerable<string> GetAllRegisteredFamilies() {
			if (!AllRegistered) {
				RegisterAll(); // Now FamilyRegistry/FontRegistry is definitely not null
			}
			return FamilyRegistry.Keys;
		}
		public static IEnumerable<string> GetAllRegisteredFonts() {
			if (!AllRegistered) {
				RegisterAll(); // Now FamilyRegistry/FontRegistry is definitely not null
			}
			return FontRegistry.Keys;
		}

		[MemberNotNullWhen(true, nameof(FamilyRegistry), nameof(FontRegistry))]
		private static bool AllRegistered { get; set; }
		private static Dictionary<string, Dictionary<TextFormat, FontPath>>? FamilyRegistry;
		private static Dictionary<string, FontPath>? FontRegistry;

		[MemberNotNull(nameof(FamilyRegistry), nameof(FontRegistry))]
		public static void RegisterAll() {

			FamilyRegistry = new Dictionary<string, Dictionary<TextFormat, FontPath>>();
			FontRegistry = new Dictionary<string, FontPath>();

			foreach (string filePath in Directory.GetFiles(GetSystemFontsDir()).Where(f => f.EndsWith(StringComparison.InvariantCultureIgnoreCase, ".ttf", ".ttc", ".otf", ".otc"))) {
				//Console.WriteLine(filePath);
				try {
					TrueTypeFontFileData[] fontFiles = OpenFontFile(filePath, out bool isFontCollection);

					for (int fontIndex = 0; fontIndex < fontFiles.Length; fontIndex++) {
						TrueTypeFontFileData fontFile = fontFiles[fontIndex];
						FontPath fontPath = new FontPath(filePath, isFontCollection ? fontIndex : -1, fontFile.EmbeddingFlags);

						//Console.WriteLine("\t" + GetFamilyName(fontFile));
						//Console.WriteLine("\t" + GetFamilyName(fontFile) + " " + GetFaceName(fontFile));
						//Console.WriteLine("\t" + GetFullName(fontFile));
						//Console.WriteLine("\t" + (GetTextFormat(fontFile)?.ToString() ?? "Unknown"));

						string? familyName = GetFamilyName(fontFile);

						if (familyName is null) {
							continue;
						}

						if (!FamilyRegistry.ContainsKey(familyName)) {
							FamilyRegistry.Add(familyName, new Dictionary<TextFormat, FontPath>());
						}

						TextFormat? format = GetTextFormat(fontFile);
						if (format.HasValue) {
							Dictionary<TextFormat, FontPath> family = FamilyRegistry[familyName];
							if (!family.ContainsKey(format.Value)) {
								family[format.Value] = fontPath;
							}
						}

						// Register the font as singletons as well, for direct access
						// Save under font full name, and under a constructed font face name
						// e.g. "Times New Roman" and "Times New Roman Regular"
						// TODO Does this first one make sense?

						string? fullName = GetFullName(fontFile);
						if (fullName is not null && !FontRegistry.ContainsKey(fullName)) {
							FontRegistry.Add(fullName, fontPath);
						}

						string? faceName = GetFaceName(fontFile);
						if (faceName is not null) {
							string singletonName = (familyName + " " + faceName).Trim();
							if (!FontRegistry.ContainsKey(singletonName)) {
								FontRegistry.Add(singletonName, fontPath);
							}
						}
					}
				}
				catch (FormatException) {
					//Console.WriteLine("\tBadly formatted file.");
				}
				catch (IOException) {

				}
			}

			string[] registryKeys = FamilyRegistry.Keys.ToArray();
			foreach (string key in registryKeys) {
				if (FamilyRegistry[key].Count == 0) {
					FamilyRegistry.Remove(key);
				}
				/*
				else if (!FamilyRegistry[key].ContainsKey(TextFormat.REGULAR)) {
					// TODO Should we add any existing font path as the regular here?
				}
				*/
			}

			AllRegistered = true;
		}

		public static TrueTypeFontFileData[] OpenFontFile(string path, out bool isFontCollection) {
			TrueTypeFontFileData[] fontFiles;

			if (FontPathIsCollection(path)) {
				fontFiles = TrueTypeCollectionData.Open(path).fonts;
				isFontCollection = true;
			}
			else {
				fontFiles = new TrueTypeFontFileData[] { TrueTypeFontFileData.Open(path) };
				isFontCollection = false;
			}

			return fontFiles;
		}

		#endregion FontPath Registry

		#region FontFile Utils

		private static string? GetFamilyName(TrueTypeFontFileData fontFile) {
			////return glyphs.FamilyNames.Select(kv => kv.Value).Distinct().FirstOrDefault();
			//return fontFile.name.nameRecords[NameID.FontFamily].FirstOrDefault()?.name;
			return GetFontName(fontFile.name.nameRecords[NameID.FontFamily]);
		}

		private static string? GetFaceName(TrueTypeFontFileData fontFile) {
			return GetFontName(fontFile.name.nameRecords[NameID.FontSubfamily]);
		}

		private static string? GetFullName(TrueTypeFontFileData fontFile) {
			return GetFontName(fontFile.name.nameRecords[NameID.FullName]);
		}

		private static string? GetFontName(TrueTypeName[] names) {
			string? name = names.Where(n => n.cultureInfo != null && n.cultureInfo.TwoLetterISOLanguageName == CultureInfo.CurrentCulture.TwoLetterISOLanguageName).FirstOrDefault()?.name;
			if (name is null) {
				name = names.FirstOrDefault()?.name;
			}
			return name;
		}

		private static TextFormat? GetTextFormat(TrueTypeFontFileData fontFile) {
			MacStyle macStyle = fontFile.head.macStyle;
			FontSelectionFlags? fsSelection = fontFile.os2?.fsSelection;

			bool italic = fsSelection.HasValue ? ((fsSelection.Value & FontSelectionFlags.ITALIC) == FontSelectionFlags.ITALIC) : ((macStyle & MacStyle.Italic) == MacStyle.Italic);
			bool bold = fsSelection.HasValue ? ((fsSelection.Value & FontSelectionFlags.BOLD) == FontSelectionFlags.BOLD) : ((macStyle & MacStyle.Bold) == MacStyle.Bold);
			//bool regular = fsSelection.HasValue ? ((fsSelection.Value & FontSelectionFlags.REGULAR) == FontSelectionFlags.REGULAR) : (macStyle == MacStyle.None);

			if ((!bold || !italic) && fontFile.name.nameRecords[NameID.FontSubfamily].FirstOrDefault(r => r.cultureInfo is CultureInfo ci && ci.TwoLetterISOLanguageName == "en") is TrueTypeName name) {
				if (string.Equals(name.name, "Bold", StringComparison.OrdinalIgnoreCase)) { // This a sensible choice?
					bold = true;
				}
				/*
				else if (string.Equals(name.name, "Regular", StringComparison.OrdinalIgnoreCase)) { // This a sensible choice?
					bold = false;
				}
				*/

				if (string.Equals(name.name, "Italic", StringComparison.OrdinalIgnoreCase)) { // This a sensible choice?
					italic = true;
				}
			}

			if (!italic && bold) { // regular && bold ??
				return TextFormat.BOLD;
			}
			else if (italic && !bold) {
				return TextFormat.ITALIC;
			}
			else if (italic && bold) {
				return TextFormat.BOLDITALIC;
			}
			else if (!italic && !bold) {
				return TextFormat.REGULAR;
			}
			else {
				return null;
			}
		}

		#endregion FontFile Utils

	}

}
