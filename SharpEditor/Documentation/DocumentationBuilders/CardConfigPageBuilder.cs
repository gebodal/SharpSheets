using SharpEditor.ContentBuilders;
using System;
using System.Collections.Generic;
using SharpSheets.Cards.Definitions;
using SharpSheets.Cards.CardConfigs;
using SharpEditor.DataManagers;
using static SharpEditor.ContentBuilders.BaseContentBuilder;
using static SharpEditor.Documentation.DocumentationBuilders.BaseDocumentationBuilder;
using SharpSheets.Evaluations;
using System.Linq;
using SharpEditor.Utilities;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using SharpEditor.Windows;

namespace SharpEditor.Documentation.DocumentationBuilders {

	// UIElement -> Control
	// FrameworkElement -> Control

	public static class CardConfigPageBuilder {

		public static DocumentationPage GetCardSetConfigPage(CardSetConfig cardSetConfig, DocumentationWindow window, Func<CardSetConfig?>? refreshAction) {
			if (cardSetConfig == null) {
				return MakeErrorPage("Invalid card configuration.");
			}

			return MakePage(GetCardSetConfigPageContent(cardSetConfig, window), cardSetConfig.name, () => GetCardSetConfigPageContent(refreshAction?.Invoke(), window));
		}

		private static Control GetCardSetConfigPageContent(CardSetConfig? cardSetConfig, DocumentationWindow window) {
			if (cardSetConfig == null) {
				return MakeErrorContent("Invalid card configuration.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = MakeTitleBlock(MakeCardConfigHeaderInlines(cardSetConfig), 0, TextWrapping.NoWrap);
			Grid headerGrid = MakeExternalLinkHeader(headerBlock, "Open Configuration File...", out Button configSourceButton, window);
			configSourceButton.Click += delegate { SharpEditorWindow.Instance?.OpenEditorDocument(cardSetConfig.origin.Path, true); };
			stack.Children.Add(headerGrid);

			if (!string.IsNullOrWhiteSpace(cardSetConfig.description)) {
				stack.Children.Add(MakeDescriptionBlock(cardSetConfig.description));
			}
			stack.Children.Add(MakeSeparator());
			

			if(cardSetConfig.cardConfigs.Count == 1 && cardSetConfig.cardConfigs.First() is Conditional<CardConfig> singleCard && singleCard.Condition.IsTrue && string.IsNullOrWhiteSpace(singleCard.Value.description)) {
				// Singular card config or dummy card config
				List<Definition> cardDefinitions = singleCard.Value.definitions.ToList(); // The card will reference back to the setConfig definitions
				if (cardDefinitions.Count > 0) {
					stack.Children.Add(GetConfigDefinitionList(cardDefinitions, 3).AddMargin(SectionMargin));
				}

				List<Conditional<AbstractCardSegmentConfig>> segments = GetDisplaySegments(singleCard.Value.AllCardSegments).ToList();
				if (segments.Any()) {
					stack.Children.Add(MakeSeparator());
					stack.Children.Add(MakeTitleBlock("List of Card Segments", 1));
					stack.Children.AddRange(GetSegmentElements(segments, 2, true, window));
				}
			}
			else {
				// Multiple card configs

				if (cardSetConfig.definitions.Count > 0) {
					stack.Children.Add(GetConfigDefinitionList(cardSetConfig.definitions, 3).AddMargin(SectionMargin));
				}

				foreach (Conditional<CardConfig> card in cardSetConfig.cardConfigs) {
					stack.Children.Add(MakeSeparator());

					TextBlock segmentHeaderBlock = MakeTitleBlock(2);
					if (!string.IsNullOrWhiteSpace(card.Value.name)) {
						segmentHeaderBlock.Inlines?.Add(new Run("[Card] ") { Foreground = SharpEditorPalette.DefaultValueBrush });
						segmentHeaderBlock.Inlines?.Add(new Run(card.Value.name) { Foreground = SharpEditorPalette.WidgetBrush });
					}
					else {
						segmentHeaderBlock.Inlines?.Add(new Run("[Unnamed Card]") { Foreground = SharpEditorPalette.DefaultValueBrush });
					}
					stack.Children.Add(segmentHeaderBlock);

					if (!string.IsNullOrWhiteSpace(card.Value.description)) {
						stack.Children.Add(MakeDescriptionBlock(card.Value.description));
					}
					if (!card.Condition.IsConstant) {
						stack.Children.Add(MakeConditionBlock(card.Condition));
					}

					// TODO Need to add card definitions here (but exclude cardSetConfig definitions!)
					List<Definition> cardOnlyDefs = card.Value.definitions.GetLocalDefinitions().ToList();
					if (cardOnlyDefs.Count > 0) {
						Control cardDefsElem = GetConfigDefinitionList(cardOnlyDefs, 5).AddMargin(TextBlockMargin);
						cardDefsElem.AddIndent(10);
						stack.Children.Add(cardDefsElem);
					}

					List<Conditional<AbstractCardSegmentConfig>> segments = GetDisplaySegments(card.Value.cardSegments).ToList();
					if (segments.Count > 0) {
						stack.Children.Add(MakeTitleBlock("List of Card Segments", 3));
						stack.Children.AddRange(GetSegmentElements(segments, 4, false, window));
					}
				}

				List<Conditional<AbstractCardSegmentConfig>> commonSegments = GetDisplaySegments(cardSetConfig.cardSetSegments).ToList();
				if (commonSegments.Count > 0) {
					stack.Children.Add(MakeSeparator());
					stack.Children.Add(MakeTitleBlock("List of Common Segments", 1));
					stack.Children.AddRange(GetSegmentElements(commonSegments, 2, true, window));
				}
			}

			return stack;
		}

		private static IEnumerable<Conditional<AbstractCardSegmentConfig>> GetDisplaySegments(IEnumerable<Conditional<AbstractCardSegmentConfig>> segments) {
			foreach (Conditional<AbstractCardSegmentConfig> segment in segments) {
				if (!(segment.Value is DynamicCardSegmentConfig dynamic && dynamic.AlwaysInclude)) {
					yield return segment;
				}
			}
		}

		private static IEnumerable<Control> GetSegmentElements(IEnumerable<Conditional<AbstractCardSegmentConfig>> segments, int titleLevel, bool includeSeparator, DocumentationWindow window) {
			bool first = true;
			foreach (Conditional<AbstractCardSegmentConfig> segment in segments) {
				if (!(segment.Value is DynamicCardSegmentConfig dynamic && dynamic.AlwaysInclude)) {
					if (first) {
						first = false;
					}
					else if (includeSeparator) {
						yield return MakeSeparator(horizontalPadding: 30);
					}
					yield return MakeCardSegmentElement(segment, titleLevel, window).AddMargin(SectionMargin);
				}
			}
		}

		private static IEnumerable<Inline> MakeCardConfigHeaderInlines(CardSetConfig cardSetConfig) {
			yield return new Run(cardSetConfig.name) { Foreground = SharpEditorPalette.WidgetBrush };
		}

		private static void SeparateDefinitions(IEnumerable<Definition> allDefinitions,
			out List<ConstantDefinition> required,
			out List<FallbackDefinition> optional,
			out List<ValueDefinition> available,
			out List<FunctionDefinition> functions
		) {
			required = new List<ConstantDefinition>();
			optional = new List<FallbackDefinition>();
			available = new List<ValueDefinition>();
			functions = new List<FunctionDefinition>();
			
			foreach (Definition definition in allDefinitions) {
				if(definition is ConstantDefinition constant) {
					required.Add(constant);
				}
				else if(definition is FallbackDefinition fallback) {
					optional.Add(fallback);
				}
				else if(definition is ValueDefinition value) {
					available.Add(value);
				}
				else if(definition is FunctionDefinition function) {
					functions.Add(function);
				}
			}
		}

		public static Control MakeCardSegmentElement(Conditional<AbstractCardSegmentConfig> segment, int titleLevel, DocumentationWindow window) {
			StackPanel segmentPanel = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock segmentHeaderBlock = MakeTitleBlock(titleLevel);
			if (!string.IsNullOrWhiteSpace(segment.Value.name)) {
				segmentHeaderBlock.Inlines?.Add(new Run("[Segment] ") { Foreground = SharpEditorPalette.DefaultValueBrush });
				segmentHeaderBlock.Inlines?.Add(new Run(segment.Value.name) { Foreground = SharpEditorPalette.WidgetBrush });
			}
			else {
				segmentHeaderBlock.Inlines?.Add(new Run("[Unnamed Segment]") { Foreground = SharpEditorPalette.DefaultValueBrush });
			}
			segmentPanel.Children.Add(segmentHeaderBlock);

			if (!string.IsNullOrWhiteSpace(segment.Value.description)) {
				segmentPanel.Children.Add(MakeDescriptionBlock(segment.Value.description));
			}
			if (!segment.Condition.IsConstant) {
				segmentPanel.Children.Add(MakeConditionBlock(segment.Condition));
			}
			if (segment.Value.definitions.Count > 0) {
				Control defElem = GetConfigDefinitionList(segment.Value.definitions, titleLevel+2).AddMargin(TextBlockMargin);
				defElem.AddIndent(10);
				segmentPanel.Children.Add(defElem);
			}

			if (segment.Value is DynamicCardSegmentConfig dynamicSegment) {
				if (dynamicSegment.cardFeatures.Count > 0) {
					segmentPanel.Children.Add(MakeTitleBlock("Segment Features", titleLevel+1));

					foreach (Conditional<CardFeatureConfig> feature in dynamicSegment.cardFeatures) {
						segmentPanel.Children.Add(MakeCardFeatureElements(feature, titleLevel+2, window));
					}
				}
			}

			return segmentPanel;
		}

		public static Control MakeCardFeatureElements(Conditional<CardFeatureConfig> feature, int titleLevel, DocumentationWindow window) {
			StackPanel featurePanel = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock featureHeaderBlock = MakeTitleBlock(titleLevel);
			if (!string.IsNullOrWhiteSpace(feature.Value.name)) {
				featureHeaderBlock.Inlines?.Add(new Run("[Feature] ") { Foreground = SharpEditorPalette.DefaultValueBrush });
				featureHeaderBlock.Inlines?.Add(new Run(feature.Value.name) { Foreground = SharpEditorPalette.WidgetBrush });
			}
			else {
				featureHeaderBlock.Inlines?.Add(new Run("[Unnamed Feature]") { Foreground = SharpEditorPalette.DefaultValueBrush });
			}
			featurePanel.Children.Add(featureHeaderBlock);

			if (!string.IsNullOrWhiteSpace(feature.Value.description)) {
				featurePanel.Children.Add(MakeDescriptionBlock(feature.Value.description));
			}

			if (!feature.Condition.IsConstant) {
				featurePanel.Children.Add(MakeConditionBlock(feature.Condition));
			}

			if (feature.Value.definitions.Count > 0) {
				Control defElem = GetConfigDefinitionList(feature.Value.definitions, titleLevel + 1).AddMargin(TextBlockMargin);
				defElem.AddIndent(10);
				featurePanel.Children.Add(defElem);
			}

			return featurePanel;
		}

		private static TextBlock MakeDescriptionBlock(string description) {
			return GetContentTextBlock(description, IndentedMargin);
		}

		private static TextBlock MakeConditionBlock(BoolExpression condition) {
			TextBlock conditionBlock = GetContentTextBlock(IndentedMargin);
			conditionBlock.Inlines?.Add(new Run("Condition: ") { Foreground = SharpEditorPalette.DefaultValueBrush });
			conditionBlock.Inlines?.Add(new Run(condition.ToString()) { Foreground = SharpEditorPalette.DefaultValueBrush });
			return conditionBlock;
		}

		private static Control MakeValueDefinitionElement(ValueDefinition definition) {
			Control defElem = DefinitionContentBuilder.MakeDefinitionElement(definition, null, TextBlockMargin, IndentedMargin);
			defElem.Margin = ParagraphSpacingMargin;
			return defElem;
		}

		private static Control MakeFunctionDefinitionElement(FunctionDefinition definition) {
			Control defElem = DefinitionContentBuilder.MakeDefinitionElement(definition, null, TextBlockMargin, IndentedMargin);
			defElem.Margin = ParagraphSpacingMargin;
			return defElem;
		}

		private static Control GetConfigDefinitionList(IEnumerable<Definition> definitions, int titleLevel) {
			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			SeparateDefinitions(definitions,
					out List<ConstantDefinition> required,
					out List<FallbackDefinition> optional,
					out List<ValueDefinition> available,
					out List<FunctionDefinition> functions);

			if (required.Count > 0) {
				TextBlock attributesHeaderBlock = MakeTitleBlock("Required Attributes", titleLevel);
				stack.Children.Add(attributesHeaderBlock);

				foreach (ConstantDefinition definition in required) {
					Control defElem = MakeValueDefinitionElement(definition);
					defElem.AddIndent(10);
					stack.Children.Add(defElem);
				}
			}

			if (optional.Count > 0) {
				TextBlock attributesHeaderBlock = MakeTitleBlock("Optional Attributes", titleLevel);
				stack.Children.Add(attributesHeaderBlock);

				foreach (FallbackDefinition definition in optional) {
					Control defElem = MakeValueDefinitionElement(definition);
					defElem.AddIndent(10);
					stack.Children.Add(defElem);
				}
			}

			if (available.Count > 0) {
				TextBlock attributesHeaderBlock = MakeTitleBlock("Available Variables", titleLevel);
				stack.Children.Add(attributesHeaderBlock);

				foreach (ValueDefinition definition in available) {
					Control defElem = MakeValueDefinitionElement(definition);
					defElem.AddIndent(10);
					stack.Children.Add(defElem);
				}
			}

			if (functions.Count > 0) {
				TextBlock attributesHeaderBlock = MakeTitleBlock("Available Functions", titleLevel);
				stack.Children.Add(attributesHeaderBlock);

				foreach (FunctionDefinition definition in functions) {
					Control defElem = MakeFunctionDefinitionElement(definition);
					defElem.AddIndent(10);
					stack.Children.Add(defElem);
				}
			}

			return stack;
		}

	}

}
