using SharpSheets.Documentation;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SharpEditor.DataManagers;
using static SharpEditor.ContentBuilders.BaseContentBuilder;
using static SharpEditor.Documentation.DocumentationBuilders.BaseDocumentationBuilder;
using SharpSheets.Fonts;
using GeboPdf.Fonts.TrueType;
using System.Linq;
using System.Globalization;
using SharpEditor.ContentBuilders;
using System.Windows.Navigation;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Shapes;
using SharpSheets.Utilities;
using System.Collections.Generic;
using GeboPdf.Fonts;
using SharpEditor.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace SharpEditor.Documentation.DocumentationBuilders {

	public static class FontPageBuilder {

		public static Thickness TableCellBlockMargin { get; } = new Thickness(10, 0, 10, 0);

		public static FrameworkElement GetFontsContents(DocumentationWindow window) {
			StackPanel contentsStack = new StackPanel() { Margin = ParagraphMargin };

			foreach (string fontName in FontPathRegistry.GetAllRegisteredFonts().OrderBy(n => n)) {
				ClickableRun fontClickable = new ClickableRun(fontName);
				fontClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(new FontName(fontName));
				contentsStack.Children.Add(new TextBlock(fontClickable) { Margin = new Thickness(0, 1, 0, 1) });
				//contentsStack.Children.Add(new TextBlock() { Text = fontName, Margin = new Thickness(0, 1, 0, 1) });
			}

			return contentsStack;
		}

		public static FrameworkElement GetFontFamiliesContents(DocumentationWindow window) {
			StackPanel contentsStack = new StackPanel() { Margin = ParagraphMargin };

			foreach (string familyName in FontPathRegistry.GetAllRegisteredFamilies().OrderBy(n => n)) {
				//ClickableRun familyClickable = new ClickableRun(familyName);
				//familyClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(constructor, () => refreshAction?.Invoke(constructor.FullName));
				//contentsStack.Children.Add(new TextBlock(familyClickable) { Margin = new Thickness(0, 1, 0, 1) });
				contentsStack.Children.Add(new TextBlock() { Text = familyName, Margin = new Thickness(0, 1, 0, 1) });
			}

			return contentsStack;
		}

		public static DocumentationPage GetFontPage(FontName fontName, DocumentationWindow window) {
			if (fontName is null) {
				return MakeErrorPage("Invalid font name.");
			}

			return MakePage(GetFontPageContent(fontName, window), fontName.Name, () => GetFontPageContent(fontName, window));
		}

		private static UIElement GetFontPageContent(FontName fontName, DocumentationWindow window) {
			if (fontName is null) {
				return MakeErrorContent("Invalid font name.");
			}

			FontPath? path = FontPathRegistry.FindFontPath(fontName.Name);

			if (path is null) {
				return MakeErrorContent("Could not find font path.");
			}

			TrueTypeFontFileData fontData;
			TrueTypePostTable? postTable;
			OpenTypeLayoutTagSet layoutTags;
			IReadOnlyDictionary<uint, ushort> cidMap;
			Uri fontUri;

			try {
				fontData = FontPathRegistry.OpenFontFile(path);

				TrueTypeCMapTable? cmap;

				if (path.FontIndex >= 0) {
					postTable = TrueTypeCollection.OpenPost(path.Path, path.FontIndex);
					layoutTags = TrueTypeCollection.ReadOpenTypeTags(path.Path, path.FontIndex);

					cmap = TrueTypeCollection.OpenCmap(path.Path, path.FontIndex);

					fontUri = new Uri("file://" + path.Path + (path.FontIndex >= 0 ? $"#{path.FontIndex}" : ""));
				}
				else {
					postTable = TrueTypeFontFile.OpenPost(path.Path);
					layoutTags = TrueTypeFontFile.ReadOpenTypeTags(path.Path);

					cmap = TrueTypeFontFile.OpenCmap(path.Path);

					fontUri = new Uri(path.Path);
				}

				cidMap = cmap is null ? new Dictionary<uint, ushort>() : CIDFontFactory.GetCmapDict(cmap);
			}
			catch (FormatException) {
				return MakeErrorContent("Could not read font file.");
			}

			IReadOnlyDictionary<ushort, string> glyphNames = postTable?.glyphNames ?? new Dictionary<ushort, string>();

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock nameTitleBlock = GetContentTextBlock(fontName.Name, TextBlockMargin, 2.0);
			stack.Children.Add(nameTitleBlock);

			StackPanel fontProperties = new StackPanel() { Orientation = Orientation.Vertical, Margin = IndentedMargin };

			if (GetFontText(fontData, NameID.PreferredFamily, NameID.FontFamily) is string family) {
				fontProperties.Children.Add(GetFontPropertyBlock("Font Family", family));
			}
			if (GetFontText(fontData, NameID.PreferredSubfamily, NameID.FontSubfamily) is string subfamily) {
				fontProperties.Children.Add(GetFontPropertyBlock("Font Subfamily", subfamily));
			}
			if (GetFontText(fontData, NameID.Designer) is string designer) {
				fontProperties.Children.Add(GetFontPropertyBlock("Designer", designer));
			}
			if (GetFontText(fontData, NameID.ManufacturerName) is string manufacturer) {
				fontProperties.Children.Add(GetFontPropertyBlock("Manufacturer", manufacturer));
			}
			if (GetFontText(fontData, NameID.NameTableVersion) is string version) {
				fontProperties.Children.Add(GetFontPropertyBlock("Version", GetVersionNumber(version)));
			}
			if (GetFontText(fontData, NameID.TrademarkNotice) is string trademark) {
				fontProperties.Children.Add(GetFontPropertyBlock("Trademark Notice", trademark));
			}
			fontProperties.Children.Add(GetFontPropertyBlock("Glyph Layout", fontData.OutlineLayout.ToString()));
			fontProperties.Children.Add(GetFontPropertyBlock("Embedding Notices", MakeEmbeddingNotice(fontData.EmbeddingFlags)));
			fontProperties.Children.Add(GetFontPropertyBlock("OpenType", $"{layoutTags.FeatureTags.Count} OpenType features detected."));

			fontProperties.Children.Add(GetFontPropertyBlock("Glyph Count", $"{fontData.numGlyphs}"));

			if (fontProperties.Children.Count > 0) {
				stack.Children.Add(fontProperties.AddMargin(ParagraphSpacingMargin));
			}

			if (GetFontText(fontData, NameID.Description) is string description) {
				stack.Children.Add(GetContentTextBlock("Description", TextBlockMargin, 1.5));
				stack.Children.Add(GetContentTextBlock(description, ParagraphMargin).AddMargin(ParagraphSpacingMargin));
			}

			string? license = GetFontText(fontData, NameID.LicenseDescription);
			string? licenseURL = GetFontText(fontData, NameID.LicenseInformationURL);
			if(license is not null || licenseURL is not null) {
				stack.Children.Add(GetContentTextBlock("License", TextBlockMargin, 1.5));
			}
			if (license is not null) {
				stack.Children.Add(GetContentTextBlock(license, ParagraphMargin));
			}
			if (licenseURL is not null) {
				stack.Children.Add(GetFontPropertyBlock("License URL", licenseURL, true));
			}

			stack.Children.Add(MakeSeparator());

			stack.Children.Add(MakeGlyphPanel(fontData, cidMap, fontUri, glyphNames).AddMargin(ParagraphMargin));

			if (layoutTags.ScriptTags.Count > 0 || layoutTags.FeatureTags.Count > 0) {
				stack.Children.Add(MakeSeparator());

				if (layoutTags.ScriptTags.Count > 0) {
					stack.Children.Add(GetContentTextBlock("OpenType Scripts", TextBlockMargin, 1.5));
					stack.Children.Add(
						CreateTable(
							new string[] { "Tag", "Script", "Language Systems" },
							ToRank2Array(
								layoutTags.ScriptTags
									.OrderBy(t => t.Key)
									.Select(t => new string[] { t.Key, OpenTypeLayoutTags.ScriptTagsRegistry.GetValueOrDefault(t.Key, "Unknown Script"), string.Join("\n", t.Value.Select(ls => $"{OpenTypeLayoutTags.LangSysTagsRegistry.GetValueOrDefault(ls, "Unknown")} ({ls.TrimEnd()})").OrderBy(s => s)) })
									.ToArray()
								)
							)
						);
				}
				if (layoutTags.FeatureTags.Count > 0) {
					stack.Children.Add(GetContentTextBlock("OpenType Features", TextBlockMargin, 1.5));
					stack.Children.Add(CreateTable(new string[] { "Tag", "Feature" }, ToRank2Array(layoutTags.FeatureTags.OrderBy(t => t).Select(t => new string[] { t, OpenTypeLayoutTags.FeatureTagsRegistry.GetValueOrDefault(t, "Unknown Feature") }).ToArray())));
				}
			}

			return stack;
		}

		private static string? GetFontText(TrueTypeFontFileData fontData, NameID nameID) {
			if (fontData.name.nameRecords.TryGetValue(nameID, out TrueTypeName[]? nameRecords)) {
				return GetFontName(nameRecords);
			}
			else {
				return null;
			}
		}

		private static string? GetFontText(TrueTypeFontFileData fontData, params NameID[] nameIDwithFallbacks) {
			for (int i = 0; i < nameIDwithFallbacks.Length; i++) {
				if (GetFontText(fontData, nameIDwithFallbacks[i]) is string name) {
					return name;
				}
			}
			return null;
		}

		private static string? GetFontName(TrueTypeName[] names) {
			return names
				.Where(n => n.cultureInfo != null && n.cultureInfo.TwoLetterISOLanguageName == CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
				.FirstOrDefault()?.name ?? names.FirstOrDefault()?.name;
		}

		private static TextBlock GetFontPropertyBlock(string property, string value, bool hyperlink = false) {
			TextBlock block = GetContentTextBlock(TextBlockMargin);
			block.Inlines.Add(new Run(property + ": ") { FontStyle = FontStyles.Italic });
			if (hyperlink) {
				try {
					Uri? uri = new Uri((Regex.IsMatch(value, @"^https?\:\/\/") ? "" : "http://") + value);
					Hyperlink link = new Hyperlink(new Run(value)) {
						NavigateUri = uri
					};
					link.RequestNavigate += RequestNavigateHyperlink;
					block.Inlines.Add(link);
				}
				catch (UriFormatException) {
					block.Inlines.Add(new Run(value));
				}
			}
			else {
				block.Inlines.Add(new Run(value));
			}
			return block;
		}

		private static void RequestNavigateHyperlink(object sender, RequestNavigateEventArgs e) {
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
			e.Handled = true;
		}

		private static readonly Regex versionRegex = new Regex(@"^(?:version)\s*([0-9\.]+)$", RegexOptions.IgnoreCase);

		private static string GetVersionNumber(string version) {
			Match match = versionRegex.Match(version);
			if (match.Success) {
				return match.Groups[1].Value;
			}
			else {
				return version;
			}
		}

		private static string MakeEmbeddingNotice(EmbeddingFlags flags) {
			if (flags.IsLicensed()) {
				return "This font indicates that it is licensed, and you must contact the legal owner before embedding it in a file.";
			}
			else if (flags.IsKnownEmbeddable()) {
				return "This font indicates that it may be embedded in a file.";
			}
			else {
				return "The embedding restrictions of this font are unclear.";
			}
		}

		private static readonly double GlyphElementSize = 50;
		private static readonly double GlyphFontSize = 30;

		private static FrameworkElement MakeGlyphPanel(TrueTypeFontFileData fontData, IReadOnlyDictionary<uint, ushort> cidMap, Uri fontUri, IReadOnlyDictionary<ushort, string> glyphNames) {
			try {
				WrapPanel panel = new WrapPanel() { Orientation = Orientation.Horizontal };

				SortedDictionary<ushort, uint> gidToUnicode = CMapWriter.GetGIDToUnicodeMap(cidMap);
				HashSet<ushort> unicodeKnown = new HashSet<ushort>(gidToUnicode.Keys);

				GlyphTypeface glyphTypeface = new GlyphTypeface(fontUri);
				double glyphHeight = GlyphFontSize * glyphTypeface.Baseline;
				//double baseline = GlyphFontSize * glyphTypeface.Baseline;

				int panelCount = 0;
				foreach (ushort glyphIdx in unicodeKnown.OrderBy(i => gidToUnicode[i]).Concat(Range(fontData.numGlyphs).Where(i => !unicodeKnown.Contains(i)))) {

					Border border = new Border {
						Width = GlyphElementSize,
						Height = GlyphElementSize,
						BorderBrush = Brushes.White,
						Background = Brushes.White,
						//BorderThickness = new Thickness(1),
						Margin = new Thickness(1)
					};

					double glyphWidth = GlyphFontSize * (glyphTypeface.AdvanceWidths.TryGetValue(glyphIdx, out double val) ? val : 0.0);

					// Calculate the margin to center the Path within the Border
					double horizontalMargin = (border.Width - glyphWidth) / 2;
					double verticalMargin = (border.Height - glyphHeight) / 2;

					Geometry geometry = glyphTypeface.GetGlyphOutline(glyphIdx, GlyphFontSize, GlyphFontSize);
					geometry.Transform = new TranslateTransform(horizontalMargin, border.Height - verticalMargin);
					border.Child = new Path() { Data = geometry, Fill = Brushes.Black };

					string tooltip = $"Glyph {glyphIdx}";
					if (gidToUnicode.TryGetValue(glyphIdx, out uint unicode)) {
						tooltip += $"\nUnicode U+{unicode.ToString(unicode <= 0xFFFF ? "X4" : "X8")}";
						if (TryGetUnicodeName(unicode, out string? unicodeName)) {
							tooltip += $"\nUnicode name: {unicodeName}";
						}
					}
					if (glyphNames.TryGetValue(glyphIdx, out string? glyphName) && glyphName.Trim() is string trimmedGlyphName && !string.IsNullOrWhiteSpace(trimmedGlyphName)) {
						tooltip += $"\nName: {trimmedGlyphName}";
					}

					border.ToolTip = tooltip;

					panel.Children.Add(border);

					panelCount++;

					if (panelCount >= 8000) {
						TextBlock warningBlock = new TextBlock() {
							Text = $"... and {fontData.numGlyphs - panelCount} more glyphs."
						};
						panel.Children.Add(warningBlock);
						break;
					}
				}

				return panel;
			}
			catch(Exception ex) {
				return new TextBlock() {
					Text = ex.Message + "\n" + (ex.StackTrace ?? "Unknown origin.")
				};
			}
		}

		private static IEnumerable<ushort> Range(ushort count) {
			for (ushort i = 0; i < count; i++) {
				yield return i;
			}
		}

		private static Grid CreateTable(string[] headers, string[,] data) {
			Grid grid = new Grid() { HorizontalAlignment = HorizontalAlignment.Center };

			ColumnDefinition[] colDefs = new ColumnDefinition[data.GetLength(1)];
			for (int c = 0; c < data.GetLength(1); c++) {
				colDefs[c] = new ColumnDefinition() { Width = GridLength.Auto };
				grid.ColumnDefinitions.Add(colDefs[c]);
			}

			RowDefinition headerRowDef = new RowDefinition();
			grid.RowDefinitions.Add(headerRowDef);
			for (int c = 0; c < headers.Length && c < data.GetLength(1); c++) {
				TextBlock cellContent = GetContentTextBlock(TextBlockMargin).AddMargin(TableCellBlockMargin);
				cellContent.Text = headers[c];
				cellContent.FontWeight = FontWeights.Bold;

				Grid.SetRow(cellContent, 0);
				Grid.SetColumn(cellContent, c);

				grid.Children.Add(cellContent);
			}

			for (int r = 0; r < data.GetLength(0); r++) {
				RowDefinition rowDef = new RowDefinition();
				grid.RowDefinitions.Add(rowDef);

				for (int c = 0; c < data.GetLength(1); c++) {
					TextBlock cellContent = GetContentTextBlock(TextBlockMargin).AddMargin(TableCellBlockMargin);
					cellContent.Text = data[r, c];

					Grid.SetRow(cellContent, r + 1); // +1 for header row
					Grid.SetColumn(cellContent, c);

					grid.Children.Add(cellContent);
				}
			}

			return grid;
		}

		private static string[,] DictionaryToArray(IReadOnlyDictionary<string, string> dictionary) {
			string[,] array = new string[dictionary.Count, 2];
			int index = 0;
			foreach (var kvp in dictionary) {
				array[index, 0] = kvp.Key;
				array[index, 1] = kvp.Value;
				index++;
			}
			return array;
		}

		private static string[,] ToRank2Array(string[][] source) {
			string[,] array = new string[source.Length, source.Max(r => r.Length)];
			for (int r = 0; r < source.Length; r++) {
				int c = 0;
				for (; c < source[r].Length; c++) {
					array[r, c] = source[r][c];
				}
				for (; c < array.GetLength(1); c++) {
					array[r, c] = "";
				}
			}
			return array;
		}

		private static readonly IReadOnlyDictionary<uint, string> UnicodeNames;
		private static readonly IReadOnlyDictionary<(uint start, uint end), string> UnicodeRangeNames;

		private static bool TryGetUnicodeName(uint codepoint, [MaybeNullWhen(false)] out string name) {
			if(UnicodeNames.TryGetValue(codepoint, out string? charName)) {
				name = charName;
				return true;
			}

			foreach(((uint start, uint end), string rangeName) in UnicodeRangeNames) {
				if(codepoint >= start && codepoint <= end) {
					name = $"{rangeName} {codepoint - start:X}";
					return true;
				}
			}

			name = null;
			return false;
		}

		static FontPageBuilder() {
			Dictionary<uint, string> unicodeNames = new Dictionary<uint, string>();
			Dictionary<string, uint> firsts = new Dictionary<string, uint>();
			Dictionary<string, uint> lasts = new Dictionary<string, uint>();

			string unicodeResource = ResourceUtilities.GetResource(typeof(FontPageBuilder).Assembly, "UnicodeData15-1-0.txt") ?? throw new InvalidOperationException("Could not find Unicode data file.");

			using (System.IO.Stream stream = typeof(FontPageBuilder).Assembly.GetManifestResourceStream(unicodeResource)!)
			using (System.IO.StreamReader reader = new System.IO.StreamReader(stream)) {
				while (reader.Peek() >= 0) {
					string? line = reader.ReadLine()?.Trim();

					if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) {
						continue;
					}

					string[] parts = line.Split(';');
					
					uint codePoint = uint.Parse(parts[0], NumberStyles.HexNumber);
					string characterName = parts[1];

					if (characterName.EndsWith(", First>")) {
						firsts[characterName[1..^8]] = codePoint;
					}
					else if (characterName.EndsWith(", Last>")) {
						lasts[characterName[1..^7]] = codePoint;
					}
					else {
						if (characterName.StartsWith('<') && characterName.EndsWith('>') && !string.IsNullOrWhiteSpace(parts[10])) {
							unicodeNames[codePoint] = parts[10]; // Use Unicode 1.0 name if the current name is vague
						}
						else {
							unicodeNames[codePoint] = characterName;
						}
					}
				}
			}

			Dictionary<(uint start, uint end), string> unicodeRangeNames = new Dictionary<(uint, uint), string>();
			foreach ((string group, uint firstCodePoint) in firsts) {
				if(lasts.TryGetValue(group, out uint lastCodePoint)) {
					unicodeRangeNames[(firstCodePoint, lastCodePoint)] = group;
				}
			}

			UnicodeNames = unicodeNames;
			UnicodeRangeNames = unicodeRangeNames;
		}

	}

	public class FontName {

		public string Name { get; }

		public FontName(string name) {
			Name = name;
		}

	}

}