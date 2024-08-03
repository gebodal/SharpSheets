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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Fonts {

	public static class FontPathRegistry {

		public static string[] GetSystemFontsDirs() {
			List<string> fontsDirs = new List<string>();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				if (Environment.GetEnvironmentVariable("windir") is string windir) {
					fontsDirs.Add(Path.Combine(windir, "fonts"));
				}
				if (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) is string localAppData) {
					// https://superuser.com/questions/1597642/where-are-user-specific-font-files-stored
					fontsDirs.Add(Path.Combine(localAppData, "Microsoft", "Windows", "Fonts"));
				}
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				// https://support.apple.com/en-gb/guide/font-book/fntbk1004/mac
				fontsDirs.Add("/System/Library/Fonts"); // Better way of getting this?
				fontsDirs.Add("/Library/Fonts"); // Better way of getting this?
				if (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is string homeDir) { // Should get home directory
					fontsDirs.Add(Path.Combine(homeDir, "Library", "Fonts"));
				}
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) {
				// https://www.baeldung.com/linux/find-installed-fonts-command-line
				fontsDirs.Add("/usr/share/fonts"); // Better way of getting this?
				fontsDirs.Add("/usr/local/share/fonts"); // Better way of getting this?
				if (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is string homeDir) { // Should get home directory
					fontsDirs.Add(Path.Combine(homeDir, ".local", "share", "fonts"));
					fontsDirs.Add(Path.Combine(homeDir, ".fonts"));
				}
			}

			return fontsDirs.Where(d => Directory.Exists(d)).ToArray();
		}

		private static readonly EnumerationOptions fontFileEnumerateOpt = new EnumerationOptions() {
			RecurseSubdirectories = true
		};
		private static IEnumerable<string> GetSourceFontFiles(params string[] sources) {
			return sources.Where(d => Directory.Exists(d))
				.SelectMany(d => Directory.EnumerateFiles(d, "*.???", fontFileEnumerateOpt))
				.Where(PathIsFontFile)
				.Distinct();
		}

		public static bool FontPathIsCollection(string path) {
			return path.EndsWith(StringComparison.InvariantCultureIgnoreCase, ".ttc", ".otc");
		}

		public static bool PathIsFontFile(string path) {
			return path.EndsWith(StringComparison.InvariantCultureIgnoreCase, ".ttf", ".ttc", ".otf", ".otc");
		}

		public static readonly IReadOnlyCollection<string> FontFileExtensions = new string[] { ".ttf", ".ttc", ".otf", ".otc" };

		#region FontPath Registry

		public static FontPathGrouping? FindFontFamily(string familyName) {
			if (!AllRegistered) {
				RegisterAll(); // Now FamilyRegistry/FontRegistry is definitely not null
			}

			if(CustomFamilyRegistry.Values.FirstOrDefault(r=>r.ContainsKey(familyName))?.TryGetValue(familyName, out Dictionary<TextFormat, FontPath>? customRegister) ?? false) {
				return MakeGrouping(customRegister);
			}
			else if (FamilyRegistry.TryGetValue(familyName, out Dictionary<TextFormat, FontPath>? register)) {
				return MakeGrouping(register);
			}
			else if(FontRegistry.TryGetValue(familyName, out FontPath? path)) {
				return new FontPathGrouping(path, null, null, null);
			}
			else {
				return null;
			}
		}

		private static FontPathGrouping MakeGrouping(Dictionary<TextFormat, FontPath> register) {
			return new FontPathGrouping(
				register.GetValueOrFallback(TextFormat.REGULAR, null),
				register.GetValueOrFallback(TextFormat.BOLD, null),
				register.GetValueOrFallback(TextFormat.ITALIC, null),
				register.GetValueOrFallback(TextFormat.BOLDITALIC, null)
				);
		}

		public static FontPath? FindFontPath(string fontName) {
			if (!AllRegistered) {
				RegisterAll(); // Now FamilyRegistry/FontRegistry is definitely not null
			}

			if (CustomFontRegistry.Values.FirstOrDefault(r => r.ContainsKey(fontName))?.TryGetValue(fontName, out FontPath? custom) ?? false) {
				return custom;
			}
			else if (FontRegistry.TryGetValue(fontName, out FontPath? singleton)) {
				return singleton;
			}
			else if (FindFontFamily(fontName) is FontPathGrouping grouping) {
				return grouping.Regular ?? grouping.Bold ?? grouping.Italic ?? grouping.BoldItalic;
			}
			else {
				return null;
			}
		}

		public static string? FindFontName(FontPath fontpath) {
			if (!AllRegistered) {
				RegisterAll(); // Now FamilyRegistry/FontRegistry is definitely not null
			}

			return CustomFontRegistry.SelectMany(kv => kv.Value).Concat(FontRegistry).FirstOrDefault(kv => FontPath.Equals(kv.Value, fontpath)).Key;
		}

		public static IEnumerable<string> GetAllRegisteredFamilies() {
			if (!AllRegistered) {
				RegisterAll(); // Now FamilyRegistry/FontRegistry is definitely not null
			}
			return CustomFamilyRegistry.Values.SelectMany(r => r.Keys).Concat(FamilyRegistry.Keys).Distinct();
		}
		public static IEnumerable<string> GetAllRegisteredFonts() {
			if (!AllRegistered) {
				RegisterAll(); // Now FamilyRegistry/FontRegistry is definitely not null
			}
			return CustomFontRegistry.Values.SelectMany(r => r.Keys).Concat(FontRegistry.Keys).Distinct();
		}

		[MemberNotNullWhen(true, nameof(FamilyRegistry), nameof(FontRegistry))]
		private static bool AllRegistered { get; set; }
		private static Dictionary<string, Dictionary<TextFormat, FontPath>>? FamilyRegistry;
		private static Dictionary<string, FontPath>? FontRegistry;

		[MemberNotNull(nameof(FamilyRegistry), nameof(FontRegistry))]
		public static void RegisterAll() {
			(FamilyRegistry, FontRegistry) = ReadFontSources(GetSystemFontsDirs());
			AllRegistered = true;
		}

		private static readonly Dictionary<string, Dictionary<string, Dictionary<TextFormat, FontPath>>> CustomFamilyRegistry = new Dictionary<string, Dictionary<string, Dictionary<TextFormat, FontPath>>>();
		private static readonly Dictionary<string, Dictionary<string, FontPath>> CustomFontRegistry = new Dictionary<string, Dictionary<string, FontPath>>();

		public static bool AddFontSource(string path) {
			if (!Directory.Exists(path)) { return false; } // Throw exception?

			CustomFamilyRegistry.Remove(path);
			CustomFontRegistry.Remove(path);

			(Dictionary<string, Dictionary<TextFormat, FontPath>> families, Dictionary<string, FontPath> fonts) = ReadFontSources(path);

			//Console.WriteLine($"Register {fonts.Count} fonts from {path}: {string.Join(", ", fonts.Values)}");

			if(families.Count > 0 || fonts.Count > 0) {
				CustomFamilyRegistry.Add(path, families);
				CustomFontRegistry.Add(path, fonts);
				return true;
			}
			else {
				return false;
			}
		}

		public static bool RemoveFontSource(string path) {
			return CustomFamilyRegistry.Remove(path) | CustomFontRegistry.Remove(path);
		}

		private static (Dictionary<string, Dictionary<TextFormat, FontPath>> families, Dictionary<string, FontPath> fonts) ReadFontSources(params string[] sources) {

			Dictionary<string, Dictionary<TextFormat, FontPath>> families = new Dictionary<string, Dictionary<TextFormat, FontPath>>();
			Dictionary<string, FontPath> fonts = new Dictionary<string, FontPath>();

			foreach (string filePath in GetSourceFontFiles(sources)) {
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

						//Console.WriteLine($"Family name:    {familyName}");

						if (!families.ContainsKey(familyName)) {
							families.Add(familyName, new Dictionary<TextFormat, FontPath>());
						}

						TextFormat? format = GetTextFormat(fontFile);
						if (format.HasValue) {
							Dictionary<TextFormat, FontPath> family = families[familyName];
							if (!family.ContainsKey(format.Value)) {
								family[format.Value] = fontPath;
							}
						}

						// Register the font as singletons as well, for direct access
						// Save under font full name, and under a constructed font face name
						// e.g. "Times New Roman" and "Times New Roman Regular"
						// TODO Does this first one make sense?

						string? fullName = GetFullName(fontFile);
						if (fullName is not null && !fonts.ContainsKey(fullName)) {
							//Console.WriteLine($"Full name:      {fullName}");
							fonts.Add(fullName, fontPath);
						}

						string? faceName = GetFaceName(fontFile);
						if (faceName is not null) {
							string singletonName = (familyName + " " + faceName).Trim();
							//Console.WriteLine($"Singleton name: {singletonName}");
							if (!fonts.ContainsKey(singletonName)) {
								fonts.Add(singletonName, fontPath);
							}
						}
					}
				}
				catch (FormatException) {
					//Console.WriteLine("\tBadly formatted font file: " + filePath);
				}
				catch (IOException) {
					//Console.WriteLine("\tCould not read font file: " + filePath);
				}
			}

			string[] registryKeys = families.Keys.ToArray();
			foreach (string key in registryKeys) {
				if (families[key].Count == 0) {
					families.Remove(key);
				}
				/*
				else if (!FamilyRegistry[key].ContainsKey(TextFormat.REGULAR)) {
					// TODO Should we add any existing font path as the regular here?
				}
				*/
			}

			return (families, fonts);
		}

		#endregion FontPath Registry

		#region FontFile Utils

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

		public static TrueTypeFontFileData OpenFontFile(FontPath path) {
			TrueTypeFontFileData fontFile;

			if (path.FontIndex >= 0) {
				TrueTypeFontFileData[] collectionFiles = TrueTypeCollectionData.Open(path.Path).fonts;
				fontFile = collectionFiles[path.FontIndex];
			}
			else {
				fontFile = TrueTypeFontFileData.Open(path.Path);
			}

			return fontFile;
		}

		private static string? GetFamilyName(TrueTypeFontFileData fontFile) {
			////return glyphs.FamilyNames.Select(kv => kv.Value).Distinct().FirstOrDefault();
			//return fontFile.name.nameRecords[NameID.FontFamily].FirstOrDefault()?.name;
			return GetFontText(fontFile, NameID.FontFamily);
		}

		private static string? GetFaceName(TrueTypeFontFileData fontFile) {
			return GetFontText(fontFile, NameID.FontSubfamily);
		}

		private static string? GetFullName(TrueTypeFontFileData fontFile) {
			return GetFontText(fontFile, NameID.FullName);
		}

		private static string? GetFontText(TrueTypeFontFileData fontData, NameID nameID) {
			if (fontData.name.nameRecords.TryGetValue(nameID, out TrueTypeName[]? nameRecords)) {
				return GetFontName(nameRecords);
			}
			else {
				return null;
			}
		}

		private static string? GetFontName(TrueTypeName[] names) {
			// TODO This approach can cause issues if SharpSheets is loaded on a non-English system
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
				else if(string.Equals(name.name, "Oblique", StringComparison.OrdinalIgnoreCase)) {
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
