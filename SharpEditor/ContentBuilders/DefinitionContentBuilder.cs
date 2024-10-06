using SharpSheets.Cards.Definitions;
using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpEditor.DataManagers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia;
using Avalonia.Layout;

namespace SharpEditor.ContentBuilders {

	// FrameworkElement -> Control

	public static class DefinitionContentBuilder {

		public static Inline GetDefinitionNameInline(EvaluationName name) {
			return new Run(name.ToString()) { Foreground = SharpEditorPalette.DefinitionNameBrush };
		}

		public static IEnumerable<Inline> GetDefinitionNameInlines(Definition definition, bool noBreak = true) {
			yield return GetDefinitionNameInline(definition.name);
			foreach (EvaluationName alias in definition.aliases) {
				yield return new Run(noBreak ? SharpValueHandler.NO_BREAK_SPACED_PIPE : " | ") { };
				yield return GetDefinitionNameInline(alias);
			}
		}

		public static string GetDefinitionTypeName(DefinitionType definitionType) {
			if (definitionType is CategoricalType) {
				return "categorical";
			}
			else if (definitionType is MulticategoryType) {
				return "multicategory";
			}
			else {
				return definitionType.ReturnType.ToString(); // TODO Is this sufficient?
			}
		}

		public static Control MakeDefinitionElement(Definition definition, IEnvironment? environment, Thickness textMargin, Thickness indentedMargin) {
			StackPanel definitionPanel = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock definitionBlock = BaseContentBuilder.GetContentTextBlock(textMargin);
			definitionBlock.Inlines?.AddRange(GetDefinitionHeaderInlines(definition, environment));
			definitionPanel.Children.Add(definitionBlock);

			if (!string.IsNullOrWhiteSpace(definition.description)) {
				definitionPanel.Children.Add(BaseContentBuilder.GetContentTextBlock(definition.description, indentedMargin));
			}

			if (definition.Type is RegexType regexType) {
				definitionPanel.Children.Add(BaseContentBuilder.GetContentTextBlock("Must match: " + regexType.Pattern.ToString(), indentedMargin));
			}
			else if (definition.Type is IntegerRange intRangeType) {
				definitionPanel.Children.Add(BaseContentBuilder.GetContentTextBlock($"Must be in range: {intRangeType.Start}-{intRangeType.End} (inclusive)", indentedMargin));
			}
			else if (definition.Type is FloatRange floatRangeType) {
				definitionPanel.Children.Add(BaseContentBuilder.GetContentTextBlock($"Must be in range: {floatRangeType.Start}-{floatRangeType.End} (inclusive)", indentedMargin));
			}
			else if (definition.Type is CategoricalType categorical) {
				definitionPanel.Children.Add(BaseContentBuilder.GetContentTextBlock("Must be one of the following: " + string.Join(", ", categorical.Categories), indentedMargin));
			}
			else if (definition.Type is MulticategoryType multicategory) {
				definitionPanel.Children.Add(BaseContentBuilder.GetContentTextBlock("Must be one or more of the following: " + string.Join(", ", multicategory.Categories), indentedMargin));
			}

			return definitionPanel;
		}

		public static IEnumerable<Inline> GetDefinitionHeaderInlines(Definition definition, IEnvironment? environment) {
			foreach (Inline nameInline in GetDefinitionNameInlines(definition)) {
				yield return nameInline;
			}

			if (definition is FunctionDefinition function) {
				yield return new Run("(") { };

				bool first = true;
				foreach (EnvironmentVariableInfo arg in function.Arguments) {
					if (first) { first = false; }
					else { yield return new Run(", ") { }; }
					yield return new Run(arg.Name.ToString()) { Foreground = SharpEditorPalette.DefinitionNameBrush };
					yield return new Run(":" + SharpValueHandler.NO_BREAK_SPACE) { };
					yield return new Run(arg.EvaluationType.ToString()) { Foreground = SharpEditorPalette.DefinitionTypeBrush };
				}

				yield return new Run(")") { };
			}

			yield return new Run(":" + SharpValueHandler.NO_BREAK_SPACE) { };
			yield return new Run(GetDefinitionTypeName(definition.Type)) { Foreground = SharpEditorPalette.DefinitionTypeBrush };

			//bool printedValue = false;
			if (environment != null) {
				object? result = null;
				if (environment.TryGetValue(definition.name, out object? value)) {
					result = value;
				}
				else if (environment.TryGetNode(definition.name, out EvaluationNode? node)) {
					try {
						result = node.Evaluate(environment);
					}
					catch (Exception) {
						result = node;
					}
				}

				if (result != null) {
					yield return new Run(SharpValueHandler.NO_BREAK_SPACED_EQUALS) { };
					yield return BaseContentBuilder.GetValueInline(definition.Type.ReturnType.DisplayType, result, false);
					//printedValue = true;
				}
			}

			/*
			if (!printedValue) {
				if(definition is FallbackDefinition fallbackDefinition) {
					yield return new Run(SharpValueHandler.NO_BREAK_SPACED_EQUALS) { Foreground = SharpEditorPalette.DefaultValueBrush };
					yield return BaseContentBuilder.GetValueInline(definition.Type.ReturnType.DisplayType, fallbackDefinition.Evaluation, true);
				}
				else if(definition is CalculatedDefinition calculatedDefinition) {
					yield return new Run(SharpValueHandler.NO_BREAK_SPACED_EQUALS) { };
					yield return BaseContentBuilder.GetValueInline(definition.Type.ReturnType.DisplayType, calculatedDefinition.Evaluation, false);
				}
			}
			*/
		}

		public static IEnumerable<TextBlock> MakeDefinitionEntries(IEnumerable<Definition> definitions, IEnvironment? evaluationEnvironment, Thickness textMargin) {
			return definitions.Select(d => MakeDefinitionEntries(d, evaluationEnvironment, textMargin));
		}

		public static TextBlock MakeDefinitionEntries(Definition definition, IEnvironment? evaluationEnvironment, Thickness textMargin) {
			TextBlock definitionBlock = BaseContentBuilder.GetContentTextBlock(textMargin);

			definitionBlock.Inlines?.AddRange(GetDefinitionNameInlines(definition));

			if (evaluationEnvironment != null) {
				Inline resultText = GetResultInline(definition, evaluationEnvironment);

				definitionBlock.Inlines?.Add(new Run(": ") { });
				definitionBlock.Inlines?.Add(resultText);
			}

			return definitionBlock;
		}

		public static TextBlock MakeDefinitionsBlock(IEnumerable<Definition> definitions, IEnvironment? evaluationEnvironment, Thickness textMargin) {
			TextBlock definitionsBlock = BaseContentBuilder.GetContentTextBlock(textMargin);

			bool first = true;

			foreach (Definition definition in definitions) {
				if (first) {
					first = false;
				}
				else {
					definitionsBlock.Inlines?.Add(new Run(", ") { });
				}

				//definitionsBlock.Inlines?.Add(GetDefinitionNameInline(definition.name));
				definitionsBlock.Inlines?.AddRange(GetDefinitionNameInlines(definition, false));

				if (evaluationEnvironment != null) {
					Inline resultText = GetResultInline(definition, evaluationEnvironment);

					definitionsBlock.Inlines?.Add(new Run(": ") { });
					definitionsBlock.Inlines?.Add(resultText);
				}
			}

			return definitionsBlock;
		}

		private static Inline GetResultInline(Definition definition, IEnvironment evaluationEnvironment) {
			object? result = null;
			if (evaluationEnvironment.TryGetValue(definition.name, out object? value)) {
				result = value;
			}
			else if (evaluationEnvironment.TryGetNode(definition.name, out EvaluationNode? node)) {
				result = node.Evaluate(evaluationEnvironment); // Can this ever fail?
			}

			Inline resultText;
			if (result is Array a) {
				if (a.Length > 0) {
					resultText = BaseContentBuilder.GetValueInline(definition.Type.ReturnType.DisplayType, a, false);
				}
				else {
					resultText = new Run("empty") { Foreground = SharpEditorPalette.DefaultValueBrush };
				}
			}
			else if (result is string textResult) {
				if (textResult.Length > 0) {
					resultText = new Run(textResult) { };
				}
				else {
					resultText = new Run("empty") { Foreground = SharpEditorPalette.DefaultValueBrush };
				}
			}
			else if (result != null) {
				resultText = BaseContentBuilder.GetValueInline(definition.Type.ReturnType.DisplayType, result, false);
			}
			else {
				resultText = new Run("null") { Foreground = SharpEditorPalette.DefaultValueBrush };
			}

			return resultText;
		}

	}

}
