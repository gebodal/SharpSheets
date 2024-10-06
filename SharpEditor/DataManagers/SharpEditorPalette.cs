﻿using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using SharpSheets.Shapes;
using SharpSheets.Widgets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using SharpSheets.Utilities;
using SharpEditor.Highlighting;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Cards.CardConfigs;
using Avalonia.Media;
using SharpEditor.Utilities;

namespace SharpEditor.DataManagers {

	public static class SharpEditorPalette {

		private static ConfigName ConfigName => new ConfigName(SharpEditorData.GetEditorName() + "_Highlighting", ".json");

		public static void Initialise() { } // Dummy method to force static initialisation

		static SharpEditorPalette() {
			LoadCurrentHighlightingColors();

			LoadAll();
		}

		[MemberNotNull(nameof(CharacterSheetHighlighting))]
		[MemberNotNull(nameof(CardConfigHighlighting))]
		[MemberNotNull(nameof(CardSubjectHighlighting))]
		[MemberNotNull(nameof(BoxMarkupHighlighting))]
		[MemberNotNull(nameof(WidgetBrush), nameof(MetaPropertyBrush),
			nameof(ShapeStyleBrush), nameof(TypeBrush),
			nameof(DefaultValueBrush), nameof(DefinitionNameBrush), nameof(DefinitionTypeBrush),
			nameof(MarkupElementBrush), nameof(MarkupAttributeBrush), nameof(MarkupPunctuationBrush),
			nameof(CardSubjectTitleBrush), nameof(CardSegmentTitleBrush), nameof(CardFeatureTitleBrush), nameof(CardFeatureListBrush))]
		private static void LoadAll() {
			LoadCharacterSheetHighlightings();
			LoadCardConfigHighlightings();
			LoadCardSubjectHighlighting();
			LoadBoxMarkupHighlighting();

			AssignHighlightingColors();
		}

		public static event EventHandler? HighlightColorChanged;

		private static Dictionary<string, HighlightData> highlightingColors;
		public static IReadOnlyDictionary<string, HighlightData> HighlightingColors => highlightingColors;

		public static IHighlightingDefinition CharacterSheetHighlighting { get; private set; }
		public static IHighlightingDefinition CardConfigHighlighting { get; private set; }
		public static IHighlightingDefinition CardSubjectHighlighting { get; private set; }
		public static IHighlightingDefinition BoxMarkupHighlighting { get; private set; }

		public static HighlightData UnusedData => HighlightingColors["Editor_Unused"]; // This feels janky?

		public static Brush WidgetBrush { get; private set; }
		public static Brush MetaPropertyBrush { get; private set; }
		public static Brush ShapeStyleBrush { get; private set; }
		public static Brush TypeBrush { get; private set; }
		public static Brush DefaultValueBrush { get; private set; }

		public static Brush DefinitionNameBrush { get; private set; }
		public static Brush DefinitionTypeBrush { get; private set; }

		public static Brush EnvironmentNameBrush => DefinitionNameBrush;
		public static Brush EnvironmentTypeBrush => DefinitionTypeBrush;

		public static Brush MarkupElementBrush { get; private set; }
		public static Brush MarkupAttributeBrush { get; private set; }
		public static Brush MarkupPunctuationBrush { get; private set; }

		public static Brush CardSubjectTitleBrush { get; private set; }
		public static Brush CardSegmentTitleBrush { get; private set; }
		public static Brush CardFeatureTitleBrush { get; private set; }
		public static Brush CardFeatureListBrush { get; private set; }


		[MemberNotNull(nameof(highlightingColors))]
		private static void LoadCurrentHighlightingColors() {
			highlightingColors = LoadDefaultHighlightingColors();

			IReadOnlyDictionary<string, HighlightData>? colorsConfig = SharpConfigManager.Load<Dictionary<string, HighlightData>>(ConfigName, out bool latest);
			if (colorsConfig is not null) {
				SetColors(colorsConfig, false);
			}
			else if (latest && colorsConfig is null) {
				SharpConfigManager.SaveBackup(ConfigName);
				SaveCurrentHighlightingColors();
			}
		}

		private static void SaveCurrentHighlightingColors() {
			SharpConfigManager.Save(highlightingColors, ConfigName);
		}

		public static Dictionary<string, HighlightData> LoadDefaultHighlightingColors() {
			Dictionary<string, HighlightData> defaultColors = new Dictionary<string, HighlightData>();

			using (Stream s = ResourceUtilities.GetResourceStream(typeof(SharpEditorData).Assembly, "HighlightingColors.xml") ?? throw new InvalidOperationException("Could not find embedded resource")) {
				using (XmlTextReader reader = new XmlTextReader(s)) {
					while (reader.Read()) {
						if (reader.NodeType == XmlNodeType.Element && reader.Name == "Color") {
							string? name = reader.GetAttribute("name");
							string? foreground = reader.GetAttribute("foreground");
							string? fontWeight = reader.GetAttribute("fontWeight");
							string? fontStyle = reader.GetAttribute("fontStyle");

							if (name is not null && foreground is not null)
								defaultColors.Add(name,
									new HighlightData(
										Color.Parse(foreground),
										fontWeight is not null ? Enum.Parse<FontWeight>(fontWeight, true) : FontWeight.Normal,
										fontStyle is not null ? Enum.Parse<FontStyle>(fontStyle, true) : FontStyle.Normal
									));
						}
					}
				}
			}

			return defaultColors;
		}

		public static IReadOnlyDictionary<string, HighlightData> GetColors() {
			return HighlightingColors;
		}

		public static void SetColors(IReadOnlyDictionary<string, HighlightData> colors, bool reloadHighlighting = true) {
			bool changed = false;
			foreach ((string n, HighlightData d) in colors) {
				// Only copy over recognized color names
				if (highlightingColors.TryGetValue(n, out HighlightData? existing) && existing != d) {
					highlightingColors[n] = d;
					changed = true;
				}
			}

			if (changed) {
				SaveCurrentHighlightingColors();
			}

			if (changed && reloadHighlighting) {
				// Reload color palette
				LoadAll();

				HighlightColorChanged?.Invoke(null, new EventArgs());
			}
		}

		private static Dictionary<string, HighlightingColor> MakeColorDict(IReadOnlyDictionary<string, HighlightData> data) {
			Dictionary<string, HighlightingColor> result = new Dictionary<string, HighlightingColor>();

			foreach ((string name, HighlightData highlight) in data) {
				result[name] = new HighlightingColor() {
					Foreground = new SimpleHighlightingBrush(highlight.Color),
					FontWeight = highlight.FontWeight,
					FontStyle = highlight.FontStyle
				};
			}

			return result;
		}

		[MemberNotNull(nameof(CharacterSheetHighlighting))]
		private static void LoadCharacterSheetHighlightings() {
			CharacterSheetHighlighting = LoadSharpConfigHighlighting("CharacterSheet", 3);
		}

		[MemberNotNull(nameof(CardConfigHighlighting))]
		private static void LoadCardConfigHighlightings() {
			CardConfigHighlighting = LoadSharpConfigHighlighting("CardConfig", 5);
		}

		private static IHighlightingDefinition LoadSharpConfigHighlighting(string style, int styleRuleRulesetIndex) {
			// Load our custom highlighting definition
			IHighlightingDefinition customHighlighting;
			using (Stream s = ResourceUtilities.GetResourceStream(typeof(SharpEditorData).Assembly, "SharpConfigHighlighting.xshd") ?? throw new InvalidOperationException("Could not find embedded resource")) {
				using (XmlReader reader = new HighlightingReader(s, style, MakeColorDict(HighlightingColors))) {
					customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
				}
			}

			HighlightingRuleSet propertyRules = customHighlighting.GetNamedRuleSet("PropertyRules");
			Dictionary<string, HighlightingColor> colors = customHighlighting.NamedHighlightingColors.ToDictionary(c => c.Name);

			static Regex GetStyleListRegex() {
				string[] styleList = SharpEditorRegistries.ShapeFactoryInstance.GetAllNames().Distinct().OrderByDescending(n => n).Select(n => Regex.Escape(n)).ToArray();
				//Console.WriteLine("styleList = " + string.Join(", ", styleList));
				return new Regex(string.Format(@"(?<=\:)\s*({0})\s*(?=\#|$)", string.Join("|", styleList)), RegexOptions.IgnoreCase);
			}
			HighlightingRule styleRule = new HighlightingRule {
				Color = colors["Config_ShapeStyle"],
				Regex = GetStyleListRegex()
			};
			SharpEditorRegistries.MarkupRegistry.RegistryChanged += delegate {
				// This should never need to be removed during the program lifetime, so adding an anonymous function is fine
				styleRule.Regex = GetStyleListRegex();
			};

			customHighlighting.MainRuleSet.Spans[styleRuleRulesetIndex].RuleSet.Rules.Insert(0, styleRule);

			string[] rectSetupList = WidgetFactory.WidgetSetupConstructor.Arguments.Select(a => a.Name.ToLowerInvariant()).Distinct().OrderByDescending(a => a).ToArray();
			HighlightingSpan rectSetupSpanRule = new HighlightingSpan {
				StartColor = colors["Config_WidgetSetup"],
				SpanColorIncludesStart = true,
				StartExpression = new Regex(string.Format(@"^\s*\@?({0})\s*(?=:\s*(?!\s|\#|$))", string.Join("|", rectSetupList)), RegexOptions.IgnoreCase),
				EndExpression = new Regex(@"$"),
				RuleSet = propertyRules
			};
			customHighlighting.MainRuleSet.Spans.Insert(1, rectSetupSpanRule); // Insert after multi-line comment spans

			return customHighlighting;
		}

		[MemberNotNull(nameof(CardSubjectHighlighting))]
		private static void LoadCardSubjectHighlighting() {
			// Card subject highlighting
			IHighlightingDefinition customHighlighting;
			using (Stream s = ResourceUtilities.GetResourceStream(typeof(SharpEditorData).Assembly, "CardSubjectHighlighting.xshd") ?? throw new InvalidOperationException("Could not find embedded resource")) {
				using (XmlReader reader = new HighlightingReader(s, null, MakeColorDict(HighlightingColors))) {
					customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
				}
			}

			//HighlightingManager.Instance.RegisterHighlighting("CardSubjects", new string[] { ".scs" }, customHighlighting);
			CardSubjectHighlighting = customHighlighting;
		}

		[MemberNotNull(nameof(BoxMarkupHighlighting))]
		private static void LoadBoxMarkupHighlighting() {
			// Box markup highlighting
			IHighlightingDefinition customHighlighting;
			using (Stream s = ResourceUtilities.GetResourceStream(typeof(SharpEditorData).Assembly, "SBMLHighlighting.xshd") ?? throw new InvalidOperationException("Could not find embedded resource")) {
				using (XmlReader reader = new HighlightingReader(s, null, MakeColorDict(HighlightingColors))) {
					customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
				}
			}

			//HighlightingManager.Instance.RegisterHighlighting("CardSubjects", new string[] { ".scs" }, customHighlighting);
			BoxMarkupHighlighting = customHighlighting;
		}

		[MemberNotNull(nameof(WidgetBrush), nameof(MetaPropertyBrush),
			nameof(ShapeStyleBrush), nameof(TypeBrush), nameof(DefaultValueBrush),
			nameof(DefinitionNameBrush), nameof(DefinitionTypeBrush),
			nameof(MarkupElementBrush), nameof(MarkupAttributeBrush), nameof(MarkupPunctuationBrush),
			nameof(CardSubjectTitleBrush), nameof(CardSegmentTitleBrush), nameof(CardFeatureTitleBrush), nameof(CardFeatureListBrush))]
		private static void AssignHighlightingColors() {
			WidgetBrush = new SolidColorBrush(highlightingColors["Config_Widget"].Color);
			MetaPropertyBrush = new SolidColorBrush(highlightingColors["Config_MetaProperty"].Color);
			ShapeStyleBrush = new SolidColorBrush(highlightingColors["Config_ShapeStyle"].Color);
			TypeBrush = new SolidColorBrush(highlightingColors["Documentation_Type"].Color);
			DefaultValueBrush = new SolidColorBrush(highlightingColors["Documentation_DefaultValue"].Color);
			
			DefinitionNameBrush = new SolidColorBrush(highlightingColors["Config_DefinitionName"].Color);
			DefinitionTypeBrush = new SolidColorBrush(highlightingColors["Config_DefinitionType"].Color);

			MarkupElementBrush = new SolidColorBrush(highlightingColors["XML_TagName"].Color);
			MarkupAttributeBrush = new SolidColorBrush(highlightingColors["XML_AttributeName"].Color);
			MarkupPunctuationBrush = new SolidColorBrush(highlightingColors["XML_Punctuation"].Color);

			CardSubjectTitleBrush = new SolidColorBrush(highlightingColors["CardSubject_CardTitle"].Color);
			CardSegmentTitleBrush = new SolidColorBrush(highlightingColors["CardSubject_SegmentTitle"].Color);
			CardFeatureTitleBrush = new SolidColorBrush(highlightingColors["CardSubject_FeatureTitle"].Color);
			CardFeatureListBrush = new SolidColorBrush(highlightingColors["CardSubject_ListMarker"].Color);
		}

		public static Brush GetTypeBrush(Type? type) {
			if (typeof(SharpWidget).IsAssignableFrom(type)) {
				return WidgetBrush;
			}
			else if (typeof(IShape).IsAssignableFrom(type)) {
				return ShapeStyleBrush;
			}
			else if (typeof(ICardConfigComponent).IsAssignableFrom(type)) {
				return WidgetBrush;
			}
			else {
				return TypeBrush; // StyleBrush; // TypeBrush?
			}
		}

		public static Brush? GetValueBrush(Type type) {
			if (typeof(SharpWidget).IsAssignableFrom(type)) {
				return WidgetBrush;
			}
			else if (typeof(IShape).IsAssignableFrom(type)) {
				return ShapeStyleBrush;
			}
			else if (typeof(ICardConfigComponent).IsAssignableFrom(type)) {
				return WidgetBrush;
			}
			else {
				return null;
			}
		}

	}

	public class HighlightData : IEquatable<HighlightData> {

		public Color Color { get; }
		public FontWeight FontWeight { get; }
		public FontStyle FontStyle { get; }

		public HighlightData(Color color, FontWeight fontWeight = FontWeight.Normal, FontStyle fontStyle = FontStyle.Normal) {
			this.Color = color;
			this.FontWeight = fontWeight;
			this.FontStyle = fontStyle;
		}

		public bool Equals(HighlightData? other) {
			if(other is null) { return false; }
			return Color == other.Color && FontWeight == other.FontWeight && FontStyle == other.FontStyle;
		}

		public override bool Equals(object? obj) {
			return Equals(obj as HighlightData);
		}

		public override int GetHashCode() {
			return HashCode.Combine(Color, FontWeight, FontStyle);
		}

		public static bool operator ==(HighlightData left, HighlightData right) {
			return left.Equals(right);
		}
		public static bool operator !=(HighlightData left, HighlightData right) {
			return !left.Equals(right);
		}
	}

}
