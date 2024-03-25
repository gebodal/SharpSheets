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

		#region Font Page

		public static FrameworkElement GetFontsContents(DocumentationWindow window) {
			StackPanel contentsStack = new StackPanel() { Margin = ParagraphMargin };

			foreach (string fontName in FontPathRegistry.GetAllRegisteredFonts().OrderBy(n => n)) {
				ClickableRun fontClickable = new ClickableRun(fontName);
				fontClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(new FontName(fontName));
				contentsStack.Children.Add(new TextBlock(fontClickable) { Margin = new Thickness(0, 1, 0, 1) });
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

			string[] aliases = FontPathRegistry.GetAllRegisteredFonts()
				.Where(f => f != fontName.Name && path.Equals(FontPathRegistry.FindFontPath(f)))
				.OrderBy(f => f).ToArray();

			TrueTypeFontFileData fontData;
			TrueTypePostTable? postTable;
			OpenTypeGlyphSubstitutionTable? gsubTable;
			OpenTypeLayoutTagSet layoutTags;
			IReadOnlyDictionary<uint, ushort> cidMap;
			Uri fontUri;

			try {
				fontData = FontPathRegistry.OpenFontFile(path);

				TrueTypeCMapTable? cmap;

				if (path.FontIndex >= 0) {
					postTable = TrueTypeCollection.OpenPost(path.Path, path.FontIndex);
					gsubTable = TrueTypeCollection.OpenGSUB(path.Path, path.FontIndex);
					layoutTags = TrueTypeCollection.ReadOpenTypeTags(path.Path, path.FontIndex);

					cmap = TrueTypeCollection.OpenCmap(path.Path, path.FontIndex);
				}
				else {
					postTable = TrueTypeFontFile.OpenPost(path.Path);
					gsubTable = TrueTypeFontFile.OpenGSUB(path.Path);
					layoutTags = TrueTypeFontFile.ReadOpenTypeTags(path.Path);

					cmap = TrueTypeFontFile.OpenCmap(path.Path);
				}

				cidMap = cmap is null ? new Dictionary<uint, ushort>() : CIDFontFactory.GetCmapDict(cmap);
				fontUri = TypefaceGrouping.GetFontUri(path);
			}
			catch (FormatException) {
				return MakeErrorContent("Could not read font file.");
			}
			catch (System.IO.IOException) {
				return MakeErrorContent("Error while reading font file.");
			}

			IReadOnlyDictionary<ushort, string> glyphNames = postTable?.glyphNames ?? new Dictionary<ushort, string>();

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock nameTitleBlock = GetContentTextBlock(fontName.Name, TextBlockMargin, 2.0);
			stack.Children.Add(nameTitleBlock);

			if (GetFontText(fontData, NameID.SampleText, NameID.FontFamily) is string sampleText) {
				GlyphTypeface glyphTypeface = new GlyphTypeface(fontUri);
				FrameworkElement sampleElem = MakeGlyphsBox(glyphTypeface, Array.Empty<ushort>(), sampleText.Select(c => cidMap.GetValueOrDefault(c, (ushort)0)).ToArray(), Array.Empty<ushort>());
				sampleElem.HorizontalAlignment = HorizontalAlignment.Left;
				sampleElem.Margin = IndentedMargin;
				stack.Children.Add(sampleElem);
			}

			StackPanel fontProperties = new StackPanel() { Orientation = Orientation.Vertical, Margin = IndentedMargin };

			if (aliases.Length > 0) {
				fontProperties.Children.Add(GetFontPropertyBlock("Aliases", string.Join(", ", aliases)));
			}
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

			WrapPanel panel = new WrapPanel() { Orientation = Orientation.Horizontal }.AddMargin(ParagraphMargin);
			PopulateGlyphPanel(panel, fontData, cidMap, fontUri, glyphNames);
			stack.Children.Add(panel);

			if (layoutTags.ScriptTags.Count > 0 || layoutTags.FeatureTags.Count > 0) {
				stack.Children.Add(MakeSeparator());

				if (layoutTags.ScriptTags.Count > 0) {
					stack.Children.Add(GetContentTextBlock("OpenType Scripts", TextBlockMargin, 1.5));
					stack.Children.Add(MakeScriptsTable(fontName.Name, layoutTags, window));
				}
				if (layoutTags.FeatureTags.Count > 0) {
					stack.Children.Add(GetContentTextBlock("OpenType Features", TextBlockMargin, 1.5));

					stack.Children.Add(MakeFeaturesTable(layoutTags.FeatureTags));
				}
			}

			return stack;
		}

		private static UIElement MakeScriptsTable(string fontName, OpenTypeLayoutTagSet layoutTags, DocumentationWindow window) {

			TextBlock[] tableHeader = new TextBlock[] {
				GetContentTextBlock("Tag", TextBlockMargin),
				GetContentTextBlock("Script", TextBlockMargin),
				GetContentTextBlock("Language Systems", TextBlockMargin)
			};

			List<TextBlock[]> tableRows = new List<TextBlock[]>();

			foreach ((string scriptTag, IReadOnlySet<string> langSysSet) in layoutTags.ScriptTags) {
				TextBlock scriptTagBlock = GetContentTextBlock(scriptTag.TrimEnd(), TextBlockMargin);
				TextBlock scriptNameBlock = GetContentTextBlock(TextBlockMargin);
				scriptNameBlock.Inlines.Add(MakeOpenTypeFeaturesLink(OpenTypeLayoutTags.ScriptTagsRegistry.GetValueOrDefault(scriptTag, "Unknown Script"), fontName, scriptTag, null, window));

				List<ClickableRun> langSysLinks = new List<ClickableRun>();
				foreach (string langSysTag in langSysSet) {
					string content = $"{OpenTypeLayoutTags.LangSysTagsRegistry.GetValueOrDefault(langSysTag, "Unknown")} ({langSysTag.TrimEnd()})";

					langSysLinks.Add(MakeOpenTypeFeaturesLink(content, fontName, scriptTag, langSysTag, window));
				}
				langSysLinks = langSysLinks.OrderBy(l => l.Text).ToList();

				List<Inline> langSysInlines = new List<Inline>();
				for(int i=0; i<langSysLinks.Count; i++) {
					if(i > 0) { langSysInlines.Add(new LineBreak()); }
					langSysInlines.Add(langSysLinks[i]);
				}

				TextBlock langSysBlock = GetContentTextBlock(TextBlockMargin);
				langSysBlock.Inlines.AddRange(langSysInlines);

				tableRows.Add(new TextBlock[] { scriptTagBlock, scriptNameBlock, langSysBlock });
			}

			return CreateTable(tableHeader, ToRank2Array(tableRows.ToArray()));
		}

		private static ClickableRun MakeOpenTypeFeaturesLink(string content, string fontName, string scriptTag, string? langSysTag, DocumentationWindow window) {
			ClickableRun clickable = new ClickableRun(content);
			clickable.MouseLeftButtonDown += window.MakeNavigationDelegate(new OpenTypeFontSetting(fontName, scriptTag, langSysTag));
			return clickable;
		}

		private static UIElement MakeFeaturesTable(IEnumerable<string> featureTags) {
			return CreateTable(
				new TextBlock[] { MakeTableHeaderBlock("Tag"), MakeTableHeaderBlock("Feature") },
				ToRank2Array(featureTags
					.OrderBy(t => t)
					.Select(t => new TextBlock[] {
						GetContentTextBlock(t, TextBlockMargin),
						GetContentTextBlock(OpenTypeLayoutTags.FeatureTagsRegistry.GetValueOrDefault(t, "Unknown Feature"), TextBlockMargin)
					}).ToArray())
				);
		}

		#endregion Font Page

		#region Font Family Page

		public static FrameworkElement GetFontFamiliesContents(DocumentationWindow window) {
			StackPanel contentsStack = new StackPanel() { Margin = ParagraphMargin };

			foreach (string familyName in FontPathRegistry.GetAllRegisteredFamilies().OrderBy(n => n)) {
				ClickableRun familyClickable = new ClickableRun(familyName);
				familyClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(new FontFamilyName(familyName));
				contentsStack.Children.Add(new TextBlock(familyClickable) { Margin = new Thickness(0, 1, 0, 1) });
			}

			return contentsStack;
		}

		public static DocumentationPage GetFontFamilyPage(FontFamilyName familyName, DocumentationWindow window) {
			if (familyName is null) {
				return MakeErrorPage("Invalid font family name.");
			}

			return MakePage(GetFontFamilyPageContent(familyName, window), familyName.Name, () => GetFontFamilyPageContent(familyName, window));
		}

		private static UIElement GetFontFamilyPageContent(FontFamilyName familyName, DocumentationWindow window) {
			if (familyName is null) {
				return MakeErrorContent("Invalid font family name.");
			}

			FontPathGrouping? family = FontPathRegistry.FindFontFamily(familyName.Name);

			if (family is null) {
				return MakeErrorContent("Could not find font family.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock nameTitleBlock = GetContentTextBlock(familyName.Name, TextBlockMargin, 2.0);
			stack.Children.Add(nameTitleBlock);

			stack.Children.Add(GetContentTextBlock("The following fonts are available within this family:", ParagraphMargin));

			void ListFont(FontPath? path, string format) {

				if (path is null) {
					TextBlock missingBlock = GetContentTextBlock($"No {format} font for this family.", new Thickness(0, 1, 0, 1)).AddMargin(IndentedMargin);
					missingBlock.FontStyle = FontStyles.Italic;
					stack.Children.Add(missingBlock);
					return;
				}

				string? fontName = FontPathRegistry.FindFontName(path);

				if (fontName is null) {
					TextBlock errorBlock = GetContentTextBlock($"Could not find {format} font name for this family.", new Thickness(0, 1, 0, 1)).AddMargin(IndentedMargin);
					errorBlock.FontStyle = FontStyles.Italic;
					stack.Children.Add(errorBlock);
					return;
				}

				ClickableRun fontClickable = new ClickableRun(fontName);
				fontClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(new FontName(fontName));
				stack.Children.Add(new TextBlock(fontClickable) { Margin = new Thickness(0, 1, 0, 1) }.AddMargin(IndentedMargin));

				// Font example graphic
				TrueTypeFontFileData fontData;
				IReadOnlyDictionary<uint, ushort> cidMap;
				Uri fontUri;

				try {
					fontData = FontPathRegistry.OpenFontFile(path);

					TrueTypeCMapTable? cmap;
					if (path.FontIndex >= 0) {
						cmap = TrueTypeCollection.OpenCmap(path.Path, path.FontIndex);
					}
					else {
						cmap = TrueTypeFontFile.OpenCmap(path.Path);
					}
					cidMap = cmap is null ? new Dictionary<uint, ushort>() : CIDFontFactory.GetCmapDict(cmap);

					fontUri = TypefaceGrouping.GetFontUri(path);
				}
				catch (Exception) {
					return;
				}

				GlyphTypeface glyphTypeface = new GlyphTypeface(fontUri);
				FrameworkElement sampleElem = MakeGlyphsBox(glyphTypeface, Array.Empty<ushort>(), fontName.Select(c => cidMap.GetValueOrDefault(c, (ushort)0)).ToArray(), Array.Empty<ushort>());
				sampleElem.HorizontalAlignment = HorizontalAlignment.Left;
				sampleElem.Margin = IndentedMargin;
				sampleElem.MouseLeftButtonDown += window.MakeNavigationDelegate(new FontName(fontName));
				stack.Children.Add(sampleElem);
			}

			ListFont(family.Regular, "Regular");
			ListFont(family.Bold, "Bold");
			ListFont(family.Italic, "Italic");
			ListFont(family.BoldItalic, "Bold Italic");

			return stack;
		}

		#endregion Font Family Page

		#region OpenType Features Page

		public static DocumentationPage GetOpenTypeFeaturesPage(OpenTypeFontSetting fontSetting, DocumentationWindow window) {
			if (fontSetting is null) {
				return MakeErrorPage("Invalid OpenType font setting.");
			}

			return MakePage(GetOpenTypeFeaturesPageContent(fontSetting, window), fontSetting.FontName, () => GetOpenTypeFeaturesPageContent(fontSetting, window));
		}

		private static UIElement GetOpenTypeFeaturesPageContent(OpenTypeFontSetting fontSetting, DocumentationWindow window) {
			if (fontSetting is null) {
				return MakeErrorContent("Invalid OpenType font setting.");
			}

			FontPath? path = FontPathRegistry.FindFontPath(fontSetting.FontName);

			if (path is null) {
				return MakeErrorContent("Could not find font path.");
			}

			TrueTypePostTable? postTable;
			OpenTypeGlyphSubstitutionTable? gsubTable;
			IReadOnlySet<string>? features;
			IReadOnlyDictionary<uint, ushort> cidMap;
			Uri fontUri;

			try {
				TrueTypeCMapTable? cmap;

				if (path.FontIndex >= 0) {
					postTable = TrueTypeCollection.OpenPost(path.Path, path.FontIndex);
					gsubTable = TrueTypeCollection.OpenGSUB(path.Path, path.FontIndex);
					cmap = TrueTypeCollection.OpenCmap(path.Path, path.FontIndex);
				}
				else {
					postTable = TrueTypeFontFile.OpenPost(path.Path);
					gsubTable = TrueTypeFontFile.OpenGSUB(path.Path);
					cmap = TrueTypeFontFile.OpenCmap(path.Path);
				}

				if (gsubTable is not null) {
					features = OpenTypeLayoutHelpers.GetFeatures(fontSetting.ScriptTag, fontSetting.LangSysTag, gsubTable.ScriptListTable, gsubTable.FeatureListTable);
				}
				else {
					features = null;
				}

				cidMap = cmap is null ? new Dictionary<uint, ushort>() : CIDFontFactory.GetCmapDict(cmap);
				fontUri = TypefaceGrouping.GetFontUri(path);
			}
			catch (FormatException) {
				return MakeErrorContent("Could not read font file.");
			}

			IReadOnlyDictionary<ushort, string> glyphNames = postTable?.glyphNames ?? new Dictionary<ushort, string>();

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock nameTitleBlock = GetContentTextBlock(fontSetting.FontName, TextBlockMargin, 2.0);
			stack.Children.Add(nameTitleBlock);

			StackPanel fontProperties = new StackPanel() { Orientation = Orientation.Vertical, Margin = IndentedMargin };

			fontProperties.Children.Add(GetFontPropertyBlock("Script", $"{OpenTypeLayoutTags.ScriptTagsRegistry.GetValueOrDefault(fontSetting.ScriptTag, "Unknown Script")} ({fontSetting.ScriptTag.Trim()})"));
			string langSys;
			if (fontSetting.LangSysTag is not null) {
				langSys = $"{OpenTypeLayoutTags.LangSysTagsRegistry.GetValueOrDefault(fontSetting.LangSysTag, "Unknown Language")} ({fontSetting.LangSysTag.Trim()})";
			}
			else {
				langSys = "Using default language system.";
			}
			fontProperties.Children.Add(GetFontPropertyBlock("Language", langSys));

			stack.Children.Add(fontProperties.AddMargin(ParagraphSpacingMargin));

			if (gsubTable is not null && features is not null && features.Count > 0) {
				stack.Children.Add(MakeFeaturesTable(features.Where(DisplayedFeature)));

				PopulateSubstitutions(stack, fontSetting.ScriptTag, fontSetting.LangSysTag, features, gsubTable, cidMap, fontUri, glyphNames);
			}

			return stack;
		}

		#endregion OpenType Features Page

		#region Utilities

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

		private static readonly int MaxGlyphCount = 8000;
		private static readonly int GlyphLoadChunkSize = 100;

		private async static void PopulateGlyphPanel(WrapPanel panel, TrueTypeFontFileData fontData, IReadOnlyDictionary<uint, ushort> cidMap, Uri fontUri, IReadOnlyDictionary<ushort, string> glyphNames) {
			SortedDictionary<ushort, uint> gidToUnicode = CMapWriter.GetGIDToUnicodeMap(cidMap);
			HashSet<ushort> unicodeKnown = new HashSet<ushort>(gidToUnicode.Keys);

			GlyphTypeface glyphTypeface = new GlyphTypeface(fontUri);

			List<UIElement> elementsToAdd = new List<UIElement>();

			int panelCount = 0;
			foreach (ushort glyphIdx in unicodeKnown.OrderBy(i => gidToUnicode[i]).Concat(Range(fontData.numGlyphs).Where(i => !unicodeKnown.Contains(i)))) {

				FrameworkElement glyphElement = MakeGlyphBox(glyphTypeface, glyphIdx, gidToUnicode, glyphNames).AddMargin(new Thickness(1));

				elementsToAdd.Add(glyphElement);

				if (elementsToAdd.Count > GlyphLoadChunkSize) {
					await panel.Dispatcher.InvokeAsync(() => {
						foreach (UIElement element in elementsToAdd) {
							panel.Children.Add(element);
						}
						elementsToAdd.Clear();
					}, System.Windows.Threading.DispatcherPriority.Background);
					elementsToAdd = new List<UIElement>();
				}
				
				panelCount++;

				if (panelCount >= MaxGlyphCount) {
					TextBlock warningBlock = new TextBlock() {
						Text = $"... and {fontData.numGlyphs - panelCount} more glyphs.",
						Margin = new Thickness(10, 10, 0, 0)
					};
					elementsToAdd.Add(warningBlock);
					break;
				}
			}

			if (elementsToAdd.Count > 0) {
				await panel.Dispatcher.InvokeAsync(() => {
					foreach (UIElement element in elementsToAdd) {
						panel.Children.Add(element);
					}
				}, System.Windows.Threading.DispatcherPriority.Background);
			}
		}

		private static FrameworkElement MakeGlyphBox(GlyphTypeface glyphTypeface, ushort glyphIdx, IReadOnlyDictionary<ushort, uint> gidToUnicode, IReadOnlyDictionary<ushort, string> glyphNames) {
			Border border = new Border {
				Width = GlyphElementSize,
				Height = GlyphElementSize,
				BorderBrush = Brushes.White,
				Background = Brushes.White
				//BorderThickness = new Thickness(1),
			};

			double glyphHeight = GlyphFontSize * glyphTypeface.Baseline;
			double glyphWidth = GlyphFontSize * (glyphTypeface.AdvanceWidths.TryGetValue(glyphIdx, out double val) ? val : 0.0);

			// Calculate the margin to center the Path within the Border
			double horizontalOffset = (border.Width - glyphWidth) / 2;
			double verticalOffset = (border.Height + glyphHeight) / 2;

			Geometry geometry = glyphTypeface.GetGlyphOutline(glyphIdx, GlyphFontSize, GlyphFontSize);
			geometry.Transform = new TranslateTransform(horizontalOffset, verticalOffset);
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

			return border;
		}

		private static FrameworkElement MakeGlyphsBox(GlyphTypeface glyphTypeface, ushort[] backtrack, ushort[] glyphIdxs, ushort[] lookahead) {
			
			double glyphHeight = GlyphFontSize * glyphTypeface.Baseline;

			double runWidth = 0.0;
			double[] glyphWidths = new double[backtrack.Length + glyphIdxs.Length + lookahead.Length];

			GeometryGroup beforeGroup = new GeometryGroup() { FillRule = FillRule.Nonzero };
			for (int b = 0; b < backtrack.Length; b++) {
				double width = GlyphFontSize * (glyphTypeface.AdvanceWidths.TryGetValue(backtrack[b], out double val) ? val : 0.0);

				Geometry geometry = glyphTypeface.GetGlyphOutline(backtrack[b], GlyphFontSize, GlyphFontSize);
				geometry.Transform = new TranslateTransform(runWidth, 0.0);

				runWidth += width;
				glyphWidths[b] = width;

				beforeGroup.Children.Add(geometry);
			}
			GeometryGroup middleGroup = new GeometryGroup() { FillRule = FillRule.Nonzero };
			for (int g=0; g<glyphIdxs.Length; g++) {
				double width = GlyphFontSize * (glyphTypeface.AdvanceWidths.TryGetValue(glyphIdxs[g], out double val) ? val : 0.0);

				Geometry geometry = glyphTypeface.GetGlyphOutline(glyphIdxs[g], GlyphFontSize, GlyphFontSize);
				geometry.Transform = new TranslateTransform(runWidth, 0.0);

				runWidth += width;
				glyphWidths[backtrack.Length + g] = width;

				middleGroup.Children.Add(geometry);
			}
			GeometryGroup afterGroup = new GeometryGroup() { FillRule = FillRule.Nonzero };
			for (int a = 0; a < lookahead.Length; a++) {
				double width = GlyphFontSize * (glyphTypeface.AdvanceWidths.TryGetValue(lookahead[a], out double val) ? val : 0.0);

				Geometry geometry = glyphTypeface.GetGlyphOutline(lookahead[a], GlyphFontSize, GlyphFontSize);
				geometry.Transform = new TranslateTransform(runWidth, 0.0);

				runWidth += width;
				glyphWidths[backtrack.Length + glyphIdxs.Length + a] = width;

				afterGroup.Children.Add(geometry);
			}

			double borderWidth = GlyphElementSize + runWidth - (glyphWidths.Length > 0 ? glyphWidths[0] + glyphWidths[^1] : 0.0) / 2.0;

			double horizontalOffset = (GlyphElementSize - (glyphWidths.Length > 0 ? glyphWidths[0] : 0.0)) / 2.0;
			double verticalOffset = (GlyphElementSize + glyphHeight) / 2;

			beforeGroup.Transform = new TranslateTransform(horizontalOffset, verticalOffset);
			middleGroup.Transform = new TranslateTransform(horizontalOffset, verticalOffset);
			afterGroup.Transform = new TranslateTransform(horizontalOffset, verticalOffset);

			string tooltip = $"Glyph{(glyphIdxs.Length != 1 ? "s" : "")} {string.Join(", ", glyphIdxs)}";
			if (backtrack.Length > 0) {
				tooltip = $"Backtrack {string.Join(", ", backtrack)}\n" + tooltip;
			}
			if (lookahead.Length > 0) {
				tooltip += $"\nLookahead {string.Join(", ", lookahead)}";
			}

			DrawingGroup drawings = new DrawingGroup();
			drawings.Children.Add(new GeometryDrawing(Brushes.Gray, null, beforeGroup));
			drawings.Children.Add(new GeometryDrawing(Brushes.Gray, null, afterGroup));
			drawings.Children.Add(new GeometryDrawing(Brushes.Black, null, middleGroup)); // So it's on top

			DrawingElement elem = new DrawingElement(drawings);

			Border border = new Border {
				Width = borderWidth,
				Height = GlyphElementSize,
				BorderBrush = Brushes.White,
				Background = Brushes.White,
				//BorderThickness = new Thickness(1),
				ToolTip = tooltip,
				Child = elem
			};

			return border;
		}

		private static readonly HashSet<string> DisplayedSubstitutionFeatures = new HashSet<string>() {
			"smcp", "c2sc", "pcap", "c2pc", "unic", "ital", "ordn", // bicameral (casing) features
			"lnum", "onum", "pnum", "tnum", "frac", "afrc", "dnom", "numr", "sinf", "zero", "mgrk", "ssty", // Mathematics and digit features
			"aalt", "swsh", "cswh", "calt", "hist", "locl", "rand", "nalt", "salt", "subs", "sups", "titl", "clig", "dlig", "hlig", "liga" // Ligature and alternative form features
		};
		private static bool DisplayedFeature(string featureTag) {
			return featureTag.Length == 4 && (
				DisplayedSubstitutionFeatures.Contains(featureTag)
				|| (featureTag.StartsWith("ss") && char.IsDigit(featureTag[2]) && char.IsDigit(featureTag[3]))
				|| (featureTag.StartsWith("cv") && char.IsDigit(featureTag[2]) && char.IsDigit(featureTag[3])));
		}

		private async static void PopulateSubstitutions(StackPanel stack, string scriptTag, string? langSysTag, IReadOnlySet<string> features, OpenTypeGlyphSubstitutionTable gsubTable, IReadOnlyDictionary<uint, ushort> cidMap, Uri fontUri, IReadOnlyDictionary<ushort, string> glyphNames) {
			SortedDictionary<ushort, uint> gidToUnicode = CMapWriter.GetGIDToUnicodeMap(cidMap);

			GlyphTypeface glyphTypeface = new GlyphTypeface(fontUri);

			UIElement GetGlyphsBox(ushort[] before, ushort[] gids, ushort[] after) {
				return MakeGlyphsBox(glyphTypeface, before, gids, after);
			}

			UIElement GetGlyphsBoxSingle(ushort[] gids) {
				return MakeGlyphsBox(glyphTypeface, Array.Empty<ushort>(), gids, Array.Empty<ushort>());
			}

			UIElement GetContents(UIElement[] initial, UIElement[] final, char connection, string? note) {
				WrapPanel glyphsPanel = new WrapPanel() { Orientation = Orientation.Horizontal };
				glyphsPanel.Children.AddRange(initial);
				if (!string.IsNullOrEmpty(note)) {
					glyphsPanel.Children.Add(new TextBlock() {
						Text = note,
						Margin = new Thickness(5, 5, 0, 5),
						VerticalAlignment = VerticalAlignment.Center,
						HorizontalAlignment = HorizontalAlignment.Center,
						FontStyle = FontStyles.Italic
					});
				}
				TextBlock connectionBlock = new TextBlock() {
					Text = connection.ToString(), // "\u2192",
					Margin = new Thickness(5),
					VerticalAlignment = VerticalAlignment.Center,
					HorizontalAlignment = HorizontalAlignment.Center
				};
				connectionBlock.MakeFontSizeRelative(2);
				glyphsPanel.Children.Add(connectionBlock);
				glyphsPanel.Children.AddRange(final);

				return new Border() {
					Margin = new Thickness(10, 4, 10, 4),
					Padding = new Thickness(4),
					BorderThickness = new Thickness(0),
					Background = Brushes.Gray,
					Child = glyphsPanel
				};
			}

			UIElement GetGlyphs2(ushort[] initial, ushort[] final, char connection, string? note) {
				WrapPanel glyphsPanel = new WrapPanel() { Orientation = Orientation.Horizontal };
				glyphsPanel.Children.AddRange(MakeGlyphSet(glyphTypeface, gidToUnicode, glyphNames, initial));
				if (!string.IsNullOrEmpty(note)) {
					glyphsPanel.Children.Add(new TextBlock() {
						Text = note,
						Margin = new Thickness(5, 5, 0, 5),
						VerticalAlignment = VerticalAlignment.Center,
						HorizontalAlignment = HorizontalAlignment.Center,
						FontStyle = FontStyles.Italic
					});
				}
				TextBlock connectionBlock = new TextBlock() {
					Text = connection.ToString(), // "\u2192",
					Margin = new Thickness(5),
					VerticalAlignment = VerticalAlignment.Center,
					HorizontalAlignment = HorizontalAlignment.Center
				};
				connectionBlock.MakeFontSizeRelative(2);
				glyphsPanel.Children.Add(connectionBlock);
				glyphsPanel.Children.AddRange(MakeGlyphSet(glyphTypeface, gidToUnicode, glyphNames, final));

				return new Border() {
					Margin = new Thickness(10, 4, 10, 4),
					Padding = new Thickness(4),
					BorderThickness = new Thickness(0),
					Background = Brushes.Gray,
					Child = glyphsPanel
				};
			}

			GlyphComparer comparer = new GlyphComparer(gidToUnicode);

			stack.Children.Add(GetContentTextBlock("Substitutions", TextBlockMargin, 1.5));

			foreach ((string feature, GlyphSubstitutionLookupSet lookups) in GetFeatures(scriptTag, langSysTag, features, gsubTable)) {

				stack.Children.Add(GetContentTextBlock($"{feature.TrimEnd()}\u2002\u2013\u2002{OpenTypeLayoutTags.FeatureTagsRegistry.GetValueOrDefault(feature, "Unknown Feature")}", TextBlockMargin, 1.5));

				WrapPanel subsPanel = new WrapPanel() { Orientation = Orientation.Horizontal, Margin = ParagraphMargin };
				stack.Children.Add(subsPanel);

				List<UIElement> elementsToAdd = new List<UIElement>();

				foreach (ISubstitutionSubtable subTable in lookups.Lookups.SelectMany(l => l.Subtables)) {
					if (subTable is OpenTypeSingleSubstitutionSubtable type1SingleSub) {
						foreach ((ushort initial, ushort final) in type1SingleSub.GetExamples().OrderBy(e => e.initial, comparer)) {
							elementsToAdd.Add(GetGlyphs2(new ushort[] { initial }, new ushort[] { final }, '\u2192', null));
						}
					}
					else if (subTable is OpenTypeMultipleSubstitutionSubtable type2MultiSub) {
						foreach ((ushort initial, ushort[] final) in type2MultiSub.GetExamples()) {
							elementsToAdd.Add(GetGlyphs2(new ushort[] { initial }, final, '\u2192', null));
						}
					}
					else if (subTable is OpenTypeAlternateSubstitutionSubtable type3AltSub) {
						foreach ((ushort initial, ushort[] final) in type3AltSub.GetExamples()) {
							//stack.Children.Add(GetContentTextBlock($"Choose {initial} -> {string.Join(", ", final)}", TextBlockMargin));

							elementsToAdd.Add(GetGlyphs2(new ushort[] { initial }, final, '\u21d2', null)); // \u21a0
						}
					}
					else if (subTable is OpenTypeLigatureSubstitutionSubtable type4LigaSub) {
						foreach ((ushort[] initial, ushort final) in type4LigaSub.GetExamples()) {
							elementsToAdd.Add(GetGlyphs2(initial, new ushort[] { final }, '\u2192', null));
						}
					}
					// TODO Missing contextual substitution tables
					else if (subTable is OpenTypeContextualSubstitutionSubtable type5ContextSub) {
						foreach ((ushort[] initial, ushort[] final) in type5ContextSub.GetExamples()) {
							//subsPanel.Children.Add(GetGlyphs2(initial, final, "e.g. \u2192"));
							elementsToAdd.Add(GetContents(new UIElement[] { GetGlyphsBoxSingle(initial) }, new UIElement[] { GetGlyphsBoxSingle(final) }, '\u2192', "e.g."));
						}
					}
					else if (subTable is OpenTypeChainedContextsSubstitutionSubtable type6ChainContextSub) {
						foreach ((ushort[] backtrack, ushort[] initial, ushort[] lookahead, ushort[] final) in type6ChainContextSub.GetExamples()) {
							//subsPanel.Children.Add(GetGlyphs2(initial, final, "e.g. \u2192"));
							elementsToAdd.Add(GetContents(new UIElement[] { GetGlyphsBox(backtrack, initial, lookahead) }, new UIElement[] { GetGlyphsBox(backtrack, final, lookahead) }, '\u2192', "e.g."));
						}
					}
					else if (subTable is OpenTypeReverseChainingContextualSingleSubstitutionSubtable type8ReverseChainSub) {
						foreach ((ushort[] initial, ushort[] final) in type8ReverseChainSub.GetExamples()) {
							//subsPanel.Children.Add(GetGlyphs2(initial, final, "e.g. \u2192"));
							elementsToAdd.Add(GetContents(new UIElement[] { GetGlyphsBoxSingle(initial) }, new UIElement[] { GetGlyphsBoxSingle(final) }, '\u2192', "e.g."));
						}
					}

					if (elementsToAdd.Count > GlyphLoadChunkSize) {
						await subsPanel.Dispatcher.InvokeAsync(() => {
							foreach (UIElement element in elementsToAdd) {
								subsPanel.Children.Add(element);
							}
							elementsToAdd.Clear();
						}, System.Windows.Threading.DispatcherPriority.Background);
						elementsToAdd = new List<UIElement>();
					}
				}

				if (elementsToAdd.Count > 0) {
					await subsPanel.Dispatcher.InvokeAsync(() => {
						foreach (UIElement element in elementsToAdd) {
							subsPanel.Children.Add(element);
						}
					}, System.Windows.Threading.DispatcherPriority.Background);
				}
			}
		}

		private static IEnumerable<FrameworkElement> MakeGlyphSet(GlyphTypeface glyphTypeface, IReadOnlyDictionary<ushort, uint> gidToUnicode, IReadOnlyDictionary<ushort, string> glyphNames, params ushort[] glyphs) {
			for (int i = 0; i < glyphs.Length; i++) {
				yield return MakeGlyphBox(glyphTypeface, glyphs[i], gidToUnicode, glyphNames);
			}
		}

		private static IEnumerable<KeyValuePair<string, GlyphSubstitutionLookupSet>> GetFeatures(string scriptTag, string? langSysTag, IReadOnlySet<string> features, OpenTypeGlyphSubstitutionTable gsubTable) {
			foreach (string feature in features.Where(DisplayedFeature).OrderBy(t => t)) {
				if (gsubTable.GetLookups(new OpenTypeLayoutTags(scriptTag, langSysTag, new string[] { feature })) is GlyphSubstitutionLookupSet lookups) {
					yield return new KeyValuePair<string, GlyphSubstitutionLookupSet>(feature, lookups);
				}
			}
		}

		private class GlyphComparer : IComparer<ushort> {

			private readonly IReadOnlyDictionary<ushort, uint> gidToUnicode;

			public GlyphComparer(IReadOnlyDictionary<ushort, uint> gidToUnicode) {
				this.gidToUnicode = gidToUnicode;
			}

			public int Compare(ushort x, ushort y) {
				bool hasX = gidToUnicode.TryGetValue(x, out uint xUnicode);
				bool hasY = gidToUnicode.TryGetValue(y, out uint yUnicode);

				if (hasX && hasY) {
					return xUnicode.CompareTo(yUnicode);
				}
				else if (!hasX && !hasY) {
					return x.CompareTo(y);
				}
				else if (hasX) {
					return -1;
				}
				else {
					return 1;
				}
			}

		}

		private static IEnumerable<ushort> Range(ushort count) {
			for (ushort i = 0; i < count; i++) {
				yield return i;
			}
		}

		private static Grid CreateTable(TextBlock[] headers, TextBlock?[,] data) {
			Grid grid = new Grid() { HorizontalAlignment = HorizontalAlignment.Center };

			ColumnDefinition[] colDefs = new ColumnDefinition[data.GetLength(1)];
			for (int c = 0; c < data.GetLength(1); c++) {
				colDefs[c] = new ColumnDefinition() { Width = GridLength.Auto };
				grid.ColumnDefinitions.Add(colDefs[c]);
			}

			RowDefinition headerRowDef = new RowDefinition();
			grid.RowDefinitions.Add(headerRowDef);
			for (int c = 0; c < headers.Length && c < data.GetLength(1); c++) {
				TextBlock cellContent = headers[c].AddMargin(TableCellBlockMargin);

				Grid.SetRow(cellContent, 0);
				Grid.SetColumn(cellContent, c);

				grid.Children.Add(cellContent);
			}

			for (int r = 0; r < data.GetLength(0); r++) {
				RowDefinition rowDef = new RowDefinition();
				grid.RowDefinitions.Add(rowDef);

				for (int c = 0; c < data.GetLength(1); c++) {
					TextBlock cellContent = (data[r, c] ?? new TextBlock()).AddMargin(TableCellBlockMargin);

					Grid.SetRow(cellContent, r + 1); // +1 for header row
					Grid.SetColumn(cellContent, c);

					grid.Children.Add(cellContent);
				}
			}

			return grid;
		}

		private static TextBlock MakeTableHeaderBlock(string text) {
			TextBlock cellContent = GetContentTextBlock(TextBlockMargin);
			cellContent.Text = text;
			cellContent.FontWeight = FontWeights.Bold;
			return cellContent;
		}

		private static T?[,] ToRank2Array<T>(T[][] source) where T : class {
			T?[,] array = new T?[source.Length, source.MaxOrFallback(r => r.Length, 0)];
			for (int r = 0; r < source.Length; r++) {
				int c = 0;
				for (; c < source[r].Length; c++) {
					array[r, c] = source[r][c];
				}
				for (; c < array.GetLength(1); c++) {
					array[r, c] = null;
				}
			}
			return array;
		}

		#endregion Utilities

		#region Unicode Names

		private static readonly IReadOnlyDictionary<uint, string> UnicodeNames;
		private static readonly IReadOnlyDictionary<(uint start, uint end), string> UnicodeRangeNames;

		private static bool TryGetUnicodeName(uint codepoint, [MaybeNullWhen(false)] out string name) {
			if (UnicodeNames.TryGetValue(codepoint, out string? charName)) {
				name = charName;
				return true;
			}

			foreach (((uint start, uint end), string rangeName) in UnicodeRangeNames) {
				if (codepoint >= start && codepoint <= end) {
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

		#endregion Unicode Names

	}

	public class FontName {

		public string Name { get; }

		public FontName(string name) {
			Name = name;
		}

	}

	public class FontFamilyName {

		public string Name { get; }

		public FontFamilyName(string name) {
			Name = name;
		}

	}

	public class OpenTypeFontSetting {
		
		public string FontName { get; }
		public string ScriptTag { get; }
		public string? LangSysTag { get; }

		public OpenTypeFontSetting(string fontName, string scriptTag, string? langSysTag) {
			FontName = fontName;
			ScriptTag = scriptTag;
			LangSysTag = langSysTag;
		}

	}

}