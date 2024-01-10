using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using SharpSheets.Shapes;
using SharpSheets.Widgets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Xml;
using SharpSheets.Utilities;
using SharpEditor.Highlighting;
using System.Diagnostics.CodeAnalysis;

namespace SharpEditor.DataManagers {

	public class SharpEditorPalette {

		public static void Initialise() { } // Dummy method to force static initialisation

		static SharpEditorPalette() {
			LoadCharacterSheetHighlightings();
			LoadCardConfigHighlightings();
			LoadCardSubjectHighlighting();
			LoadBoxMarkupHighlighting();

			AssignHighlightingColors();
		}

		public static IHighlightingDefinition CharacterSheetHighlighting { get; private set; }
		public static IHighlightingDefinition CardConfigHighlighting { get; private set; }
		public static IHighlightingDefinition CardSubjectHighlighting { get; private set; }
		public static IHighlightingDefinition BoxMarkupHighlighting { get; private set; }

		public static Brush RectBrush { get; private set; }
		public static Brush ArgBrush { get; private set; }
		public static Brush StyleBrush { get; private set; }
		public static Brush TypeBrush { get; private set; }
		public static Brush ClassBrush { get; private set; }
		public static Brush DefaultValueBrush { get; private set; }
		public static Brush DefinitionBrush { get; private set; }

		public static Brush DefinitionNameBrush { get; private set; }
		public static Brush DefinitionTypeBrush { get; private set; }

		public static Brush EnvironmentNameBrush => DefinitionNameBrush;
		public static Brush EnvironmentTypeBrush => DefinitionTypeBrush;

		public static Brush MarkupElementBrush { get; private set; }
		public static Brush MarkupAttributeBrush { get; private set; }
		public static Brush MarkupPunctuationBrush { get; private set; }
		public static Brush MarkupBaseBrush { get; private set; }

		[MemberNotNull(nameof(CharacterSheetHighlighting))]
		static void LoadCharacterSheetHighlightings() {
			CharacterSheetHighlighting = LoadSharpConfigHighlighting("CharacterSheet", 2);
		}

		[MemberNotNull(nameof(CardConfigHighlighting))]
		static void LoadCardConfigHighlightings() {
			CardConfigHighlighting = LoadSharpConfigHighlighting("CardConfig", 4);
		}

		/*
		private static HighlightingRule characterSheetStyleRule;

		[MemberNotNull(nameof(characterSheetStyleRule), nameof(CharacterSheetHighlighting))]
		static void LoadCharacterSheetHighlightings() {
			// Load our custom highlighting definition
			IHighlightingDefinition customHighlighting;
			using (Stream s = typeof(SharpEditorWindow).Assembly.GetManifestResourceStream("SharpEditor.Highlighting.SharpConfigHighlighting.xshd")!) {
				if (s == null)
					throw new InvalidOperationException("Could not find embedded resource");
				using (XmlReader reader = new HighlightingReader(s, "CharacterSheet")) {
					customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
				}
			}

			//IList<HighlightingRule> rules = customHighlighting.MainRuleSet.Rules;
			//HighlightingRuleSet basicRules = customHighlighting.GetNamedRuleSet("BasicRules");
			HighlightingRuleSet propertyRules = customHighlighting.GetNamedRuleSet("PropertyRules");
			Dictionary<string, HighlightingColor> colors = customHighlighting.NamedHighlightingColors.ToDictionary(c => c.Name);

			Regex GetStyleListRegex() {
				string[] styleList = SharpEditorRegistries.ShapeFactoryInstance.GetAllNames().Distinct().OrderByDescending(n => n).Select(n => Regex.Escape(n)).ToArray();
				//Console.WriteLine("styleList = " + string.Join(", ", styleList));
				return new Regex(string.Format(@"(?<=\:)\s*({0})\s*(?=\#|$)", string.Join("|", styleList)), RegexOptions.IgnoreCase);
			}
			characterSheetStyleRule = new HighlightingRule {
				Color = colors["Style"],
				Regex = GetStyleListRegex()
			};
			SharpEditorRegistries.MarkupRegistry.RegistryChanged += delegate {
				// This should never need to be removed during the program lifetime, so adding an anonymous function is fine
				characterSheetStyleRule.Regex = GetStyleListRegex();
			};
			//customHighlighting.MainRuleSet.Spans.Last().RuleSet.Rules.Insert(0, characterSheetStyleRule);

			//basicRules.Rules.Insert(0, characterSheetStyleRule);
			//propertyRules.Rules.Insert(0, characterSheetStyleRule);

			customHighlighting.MainRuleSet.Spans[2].RuleSet.Rules.Insert(0, characterSheetStyleRule);

			string[] rectSetupList = WidgetFactory.WidgetSetupConstructor.Arguments.Select(a => a.Name.ToLowerInvariant()).Distinct().OrderByDescending(a => a).ToArray();
			HighlightingSpan rectSetupSpanRule = new HighlightingSpan {
				StartColor = colors["RectSetup"],
				SpanColorIncludesStart = true,
				StartExpression = new Regex(string.Format(@"^\s*\@?({0})\s*(?=:)", string.Join("|", rectSetupList)), RegexOptions.IgnoreCase),
				EndExpression = new Regex(@"$"),
				RuleSet = propertyRules
			};
			customHighlighting.MainRuleSet.Spans.Insert(2, rectSetupSpanRule); // Insert after comment spans

			//HighlightingManager.Instance.RegisterHighlighting("SharpSheets", new string[] { ".ssc" }, customHighlighting);
			CharacterSheetHighlighting = customHighlighting;
		}

		private static HighlightingRule cardDefinitionStyleRule;

		[MemberNotNull(nameof(cardDefinitionStyleRule), nameof(CardDefinitionHighlighting))]
		static void LoadCardDefinitionHighlightings() {
			// Load our custom highlighting definition
			IHighlightingDefinition customHighlighting;
			using (Stream s = typeof(SharpEditorWindow).Assembly.GetManifestResourceStream("SharpEditor.Highlighting.SharpConfigHighlighting.xshd")!) {
				if (s == null)
					throw new InvalidOperationException("Could not find embedded resource");
				using (XmlReader reader = new HighlightingReader(s, "CardDefinition")) {
					customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
				}
			}

			//IList<HighlightingRule> rules = customHighlighting.MainRuleSet.Rules;
			IList<HighlightingRule> basicRules = customHighlighting.GetNamedRuleSet("BasicRules").Rules;
			Dictionary<string, HighlightingColor> colors = customHighlighting.NamedHighlightingColors.ToDictionary(c => c.Name);

			//HighlightingRule rectRule = new HighlightingRule { Color = colors["Rect"] };
			//string[] rectList = WidgetFactory.GetAllNames()
			//	//.Append("page")
			//	.Concat(CardDefinitionConstructors.Select(c => c.Name))
			//	.Concat(CardRectConstructors.Select(c => c.Name))
			//	.ToArray();
			//rectRule.Regex = new Regex(string.Format(@"^\s*({0})\s*(?=:?\s*(#|$))", string.Join("|", rectList)), RegexOptions.IgnoreCase);
			//rules.Insert(0, rectRule);

			Regex GetStyleListRegex() {
				string[] styleList = SharpEditorRegistries.ShapeFactoryInstance.GetAllNames().Distinct().OrderByDescending(n => n).Select(n => Regex.Escape(n)).ToArray();
				return new Regex(string.Format(@"(?<=\:)\s*({0})\s*(?=#|$)", string.Join("|", styleList)), RegexOptions.IgnoreCase);
			}
			cardDefinitionStyleRule = new HighlightingRule {
				Color = colors["Style"],
				Regex = GetStyleListRegex()
			};
			SharpEditorRegistries.MarkupRegistry.RegistryChanged += delegate {
				// This should never need to be removed during the program lifetime, so adding an anonymous function is fine
				cardDefinitionStyleRule.Regex = GetStyleListRegex();
			};
			//basicRules.Insert(0, cardDefinitionStyleRule);
			customHighlighting.MainRuleSet.Spans[4].RuleSet.Rules.Insert(0, cardDefinitionStyleRule);

			//HighlightingManager.Instance.RegisterHighlighting("CardDefinitions", new string[] { ".scd" }, customHighlighting);
			CardDefinitionHighlighting = customHighlighting;
		}
		*/

		private static IHighlightingDefinition LoadSharpConfigHighlighting(string style, int styleRuleRulesetIndex) {
			// Load our custom highlighting definition
			IHighlightingDefinition customHighlighting;
			using (Stream s = typeof(SharpEditorWindow).Assembly.GetManifestResourceStream("SharpEditor.Highlighting.SharpConfigHighlighting.xshd")!) {
				if (s == null)
					throw new InvalidOperationException("Could not find embedded resource");
				using (XmlReader reader = new HighlightingReader(s, style)) {
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
				Color = colors["Style"],
				Regex = GetStyleListRegex()
			};
			SharpEditorRegistries.MarkupRegistry.RegistryChanged += delegate {
				// This should never need to be removed during the program lifetime, so adding an anonymous function is fine
				styleRule.Regex = GetStyleListRegex();
			};

			customHighlighting.MainRuleSet.Spans[styleRuleRulesetIndex].RuleSet.Rules.Insert(0, styleRule);

			string[] rectSetupList = WidgetFactory.WidgetSetupConstructor.Arguments.Select(a => a.Name.ToLowerInvariant()).Distinct().OrderByDescending(a => a).ToArray();
			HighlightingSpan rectSetupSpanRule = new HighlightingSpan {
				StartColor = colors["RectSetup"],
				SpanColorIncludesStart = true,
				StartExpression = new Regex(string.Format(@"^\s*\@?({0})\s*(?=:)", string.Join("|", rectSetupList)), RegexOptions.IgnoreCase),
				EndExpression = new Regex(@"$"),
				RuleSet = propertyRules
			};
			customHighlighting.MainRuleSet.Spans.Insert(1, rectSetupSpanRule); // Insert after multi-line comment spans

			return customHighlighting;
		}

		[MemberNotNull(nameof(CardSubjectHighlighting))]
		static void LoadCardSubjectHighlighting() {
			// Card subject highlighting
			IHighlightingDefinition customHighlighting;
			using (Stream s = typeof(SharpEditorWindow).Assembly.GetManifestResourceStream("SharpEditor.Highlighting.CardSubjectHighlighting.xshd")!) {
				if (s == null)
					throw new InvalidOperationException("Could not find embedded resource");
				using (XmlReader reader = new XmlTextReader(s)) {
					customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
				}
			}

			//HighlightingManager.Instance.RegisterHighlighting("CardSubjects", new string[] { ".scs" }, customHighlighting);
			CardSubjectHighlighting = customHighlighting;
		}

		[MemberNotNull(nameof(BoxMarkupHighlighting))]
		static void LoadBoxMarkupHighlighting() {
			// Box markup highlighting
			IHighlightingDefinition customHighlighting;
			using (Stream s = typeof(SharpEditorWindow).Assembly.GetManifestResourceStream("SharpEditor.Highlighting.SBMLHighlighting.xshd")!) {
				if (s == null)
					throw new InvalidOperationException("Could not find embedded resource");
				using (XmlReader reader = new XmlTextReader(s)) {
					customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
				}
			}

			//HighlightingManager.Instance.RegisterHighlighting("CardSubjects", new string[] { ".scs" }, customHighlighting);
			BoxMarkupHighlighting = customHighlighting;
		}

		[MemberNotNull(nameof(RectBrush), nameof(ArgBrush), nameof(StyleBrush), nameof(TypeBrush), nameof(ClassBrush), nameof(DefaultValueBrush), nameof(DefinitionBrush), nameof(DefinitionNameBrush), nameof(DefinitionTypeBrush), nameof(MarkupElementBrush), nameof(MarkupAttributeBrush), nameof(MarkupPunctuationBrush), nameof(MarkupBaseBrush))]
		static void AssignHighlightingColors() {
			RectBrush = new SolidColorBrush(CharacterSheetHighlighting.GetNamedColor("Rect").Foreground.GetColor(null)!.Value);
			ArgBrush = new SolidColorBrush(CardConfigHighlighting.GetNamedColor("Condition").Foreground.GetColor(null)!.Value); // new SolidColorBrush(Colors.Magenta);
			StyleBrush = new SolidColorBrush(CharacterSheetHighlighting.GetNamedColor("Style").Foreground.GetColor(null)!.Value);
			TypeBrush = new SolidColorBrush(Colors.Gold);
			ClassBrush = new SolidColorBrush(Colors.OrangeRed);
			DefaultValueBrush = new SolidColorBrush(Colors.Gray);
			DefinitionBrush = new SolidColorBrush(CardConfigHighlighting.GetNamedColor("Def").Foreground.GetColor(null)!.Value);

			DefinitionNameBrush = new SolidColorBrush(CardConfigHighlighting.GetNamedColor("DefinitionName").Foreground.GetColor(null)!.Value);
			DefinitionTypeBrush = new SolidColorBrush(CardConfigHighlighting.GetNamedColor("DefinitionType").Foreground.GetColor(null)!.Value);

			MarkupElementBrush = new SolidColorBrush(BoxMarkupHighlighting.GetNamedColor("TagName").Foreground.GetColor(null)!.Value);
			MarkupAttributeBrush = new SolidColorBrush(BoxMarkupHighlighting.GetNamedColor("AttributeName").Foreground.GetColor(null)!.Value);
			MarkupPunctuationBrush = new SolidColorBrush(BoxMarkupHighlighting.GetNamedColor("Punctuation").Foreground.GetColor(null)!.Value);
			MarkupBaseBrush = new SolidColorBrush(BoxMarkupHighlighting.GetNamedColor("BaseColor").Foreground.GetColor(null)!.Value);
		}

		public static Brush GetTypeBrush(Type? type) {
			if (typeof(SharpWidget).IsAssignableFrom(type)) {
				return RectBrush;
			}
			else if (typeof(IShape).IsAssignableFrom(type)) {
				return StyleBrush;
			}
			else {
				return TypeBrush; // StyleBrush; // TypeBrush?
			}
		}

		public static Brush? GetValueBrush(Type type) {
			if (typeof(SharpWidget).IsAssignableFrom(type)) {
				return RectBrush;
			}
			else if (typeof(IShape).IsAssignableFrom(type)) {
				return StyleBrush;
			}
			else {
				return null;
			}
		}

	}

}
