using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Parsing;
using SharpSheets.Canvas.Text;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Cards.Definitions {

	public static class CardSetConfigEnvironments {

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSetConfigEnvironments() {
			BaseDefinitions = new DefinitionGroup();
		}
	}

	public static class CardConfigEnvironments {

		public static readonly DefinitionGroup BaseDefinitions;

		static CardConfigEnvironments() {
			BaseDefinitions = new DefinitionGroup();
		}
	}

	public static class CardSubjectEnvironments {

		static readonly Definition nameDefinition = new ConstantDefinition(
			"name", Array.Empty<EvaluationName>(),
			"The subject name text.",
			EvaluationType.STRING);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSubjectEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				nameDefinition
			};
		}

		public static IVariableBox GetVariables(ICardSectionParent parent) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, parent.Variables, BaseDefinitions); // Do we actually have to append BaseDefinitions these here...?
		}

		public static IVariableBox GetVariables(CardSetConfig cardSetConfig) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, cardSetConfig.Variables, BaseDefinitions); // Do we actually have to append BaseDefinitions these here...?
		}

		public static IVariableBox GetVariables(CardConfig cardConfig) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, cardConfig.Variables, BaseDefinitions); // Do we actually have to append BaseDefinitions these here...?
		}

		public static IEnvironment GetDryRun(CardConfig cardConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(cardConfig)));
		}

		public static DefinitionEnvironment MakeBaseEnvironment(ContextValue<string> name) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ nameDefinition, new ContextValue<object>(name.Location, name.Value) }
			});
		}

	}

	public static class CardOutlinesEnvironments {

		static readonly Definition cardnumDefinition = new ConstantDefinition(
			"card", new EvaluationName[] { "cardnum" },
			"The index of the current card being drawn in the current layout. This is zero-indexed, meaning that the first card has an index of 0.",
			EvaluationType.INT);
		static readonly Definition cardcountDefinition = new ConstantDefinition(
			"cardcount", new EvaluationName[] { "totalcards" },
			"The total number of cards in the current card layout.",
			EvaluationType.INT);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardOutlinesEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				cardnumDefinition,
				cardcountDefinition
			};
		}

		public static IVariableBox GetVariables(CardSetConfig cardSetConfig) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, CardSubjectEnvironments.GetVariables(cardSetConfig), BaseDefinitions);
		}

		public static IVariableBox GetVariables(CardConfig cardConfig) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, CardSubjectEnvironments.GetVariables(cardConfig), BaseDefinitions);
		}

		public static IEnvironment GetDryRun(CardSetConfig cardSetConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(cardSetConfig)));
		}

		public static IEnvironment GetDryRun(CardConfig cardConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(cardConfig)));
		}

		public static DefinitionEnvironment GetEnvironment(int card, int totalCards) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ cardnumDefinition, new ContextValue<object>(DocumentSpan.Imaginary, card) },
				{ cardcountDefinition, new ContextValue<object>(DocumentSpan.Imaginary, totalCards) }
			});
		}

	}

	public static class CardSectionEnvironments {

		static readonly Definition headingDefinition = new ConstantDefinition(
			"heading", Array.Empty<EvaluationName>(),
			"The section heading text, without note or details.",
			EvaluationType.STRING);
		static readonly Definition noteDefinition = new FallbackDefinition(
			"subheading", Array.Empty<EvaluationName>(),
			"The section subheading text (which may be empty).",
			EvaluationType.STRING,
			new ConstantNode(""));

		static readonly Definition featureCountDefinition = new ConstantDefinition(
			"featurecount", new EvaluationName[] { "totalfeatures" },
			"The total number of features in the current card section.",
			EvaluationType.INT);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSectionEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				headingDefinition,
				noteDefinition,
				featureCountDefinition
			};
		}

		public static IVariableBox GetVariables(AbstractCardSectionConfig sectionConfig) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				CardSubjectEnvironments.GetVariables(sectionConfig.parent),
				sectionConfig.Variables,
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetVariables(ICardSectionParent parent) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				CardSubjectEnvironments.GetVariables(parent),
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IEnvironment GetDryRun(AbstractCardSectionConfig sectionConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(sectionConfig)));
		}

		/*
		public static DefinitionEnvironment MakeBaseEnvironment(ContextValue<string> heading, ContextValue<string> note, int numFeatures) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ headingDefinition, new ContextValue<object>(heading.Location, heading.Value) },
				{ noteDefinition, new ContextValue<object>(note.Location, note.Value) },
				{ featureCountDefinition, new ContextValue<object>(DocumentSpan.Imaginary, numFeatures) }
			});
		}

		public static DefinitionEnvironment GetBuildEnvironment(ContextValue<string> heading, ContextValue<string> note, int numFeatures) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ headingDefinition, new ContextValue<object>(heading.Location, heading.Value ?? "") },
				{ noteDefinition, new ContextValue<object>(note.Location, note.Value ?? "") },
				{ featureCountDefinition, new ContextValue<object>(DocumentSpan.Imaginary, numFeatures) }
			});
		}
		*/

		public static IEnvironment MakeBaseEnvironment(CardSection section) {
			return new CardSectionEnvironment(section);
		}

		private class CardSectionEnvironment : AbstractDataEnvironment {
			private readonly CardSection section;

			public CardSectionEnvironment(CardSection section) {
				this.section = section;
			}

			public override bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
				return BaseDefinitions.TryGetVariableInfo(key, out variableInfo);
			}

			public override bool TryGetValue(EvaluationName key, out object? value) {
				if (BaseDefinitions.TryGetDefinition(key, out Definition? definition)) {
					value = definition.name.ToString() switch {
						"heading" => section.Heading.Value,
						"subheading" => section.Note.Value,
						"featurecount" => section.Count,
						_ => throw new InvalidOperationException("Unknown card section definition.")
					};
					return true;
				}
				else {
					value = null;
					return false;
				}
			}

			public override IEnumerable<EnvironmentVariableInfo> GetVariables() {
				return BaseDefinitions.GetVariables();
			}
		}

	}

	public static class CardSectionOutlineEnvironments {

		static readonly Definition partnumDefinition = new ConstantDefinition(
			"partnum", Array.Empty<EvaluationName>(),
			"The index of the current section part/segment being drawn in the current card section. This is zero-indexed, meaning that the first part has an index of 0.",
			EvaluationType.INT);
		static readonly Definition partcountDefinition = new ConstantDefinition(
			"partcount", new EvaluationName[] { "totalparts" },
			"The total number of section parts/segments in the current section for the current card layout.",
			EvaluationType.INT);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSectionOutlineEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				partnumDefinition,
				partcountDefinition
			};
		}

		public static IVariableBox GetVariables(AbstractCardSectionConfig sectionConfig) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, CardSectionEnvironments.GetVariables(sectionConfig), BaseDefinitions);
		}

		public static IEnvironment GetDryRun(AbstractCardSectionConfig sectionConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(sectionConfig)));
		}

		public static DefinitionEnvironment GetEnvironment(int partnum, int totalParts) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ partnumDefinition, new ContextValue<object>(DocumentSpan.Imaginary, partnum) },
				{ partcountDefinition, new ContextValue<object>(DocumentSpan.Imaginary, totalParts) }
			});
		}
	}

	public static class CardFeatureEnvironments {

		static readonly Definition titleDefinition = new ConstantDefinition(
			"title", Array.Empty<EvaluationName>(),
			"The feature title text (without note or details).",
			EvaluationType.STRING);
		static readonly Definition noteDefinition = new FallbackDefinition(
			"subtitle", Array.Empty<EvaluationName>(),
			"The feature subtitle text (which may be empty).",
			EvaluationType.STRING,
			new ConstantNode(""));
		static readonly Definition textDefinition = new ConstantDefinition(
			"text", Array.Empty<EvaluationName>(),
			"The feature text content.",
			EvaluationType.STRING);
		static readonly Definition listItemDefinition = new ConstantDefinition(
			"listitem", Array.Empty<EvaluationName>(),
			"A flag indicating if the current feature is an item in a list.",
			EvaluationType.BOOL);
		
		static readonly Definition featureNumDefinition = new ConstantDefinition(
			"featureNum", Array.Empty<EvaluationName>(),
			"The index of the current feature being drawn in the current card section. This is zero-indexed, meaning that the first feature has an index of 0.",
			EvaluationType.INT);

		public static readonly DefinitionGroup BaseDefinitions;
		public static readonly DefinitionGroup BaseTextDefinitions;

		public static readonly StringExpression TextExpression = new StringExpression(new VariableNode(new EvaluationName("text"), EvaluationType.STRING));

		static CardFeatureEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				titleDefinition,
				noteDefinition,
				textDefinition,
				listItemDefinition,
				featureNumDefinition
			};

			BaseTextDefinitions = new DefinitionGroup() {
				titleDefinition,
				noteDefinition
			};
		}

		public static IVariableBox GetVariables(CardFeatureConfig featureConfig) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				CardSectionEnvironments.GetVariables(featureConfig.cardSectionConfig),
				featureConfig.Variables,
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetVariables(ICardSectionParent parent) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				CardSectionEnvironments.GetVariables(parent),
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IEnvironment GetDryRun(CardFeatureConfig featureConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(featureConfig)));
		}

		public static DefinitionEnvironment MakeBaseEnvironment(CardFeature feature) {
			return DefinitionEnvironment.Create(
					new Dictionary<Definition, ContextValue<object>> {
						{ titleDefinition, new ContextValue<object>(feature.Title.Location, feature.Title.Value) },
						{ noteDefinition, new ContextValue<object>(feature.Note.Location, feature.Note.Value) },
						{ listItemDefinition, new ContextValue<object>(DocumentSpan.Imaginary, feature.IsListItem) },
						{ featureNumDefinition, new ContextValue<object>(DocumentSpan.Imaginary, feature.Index) }
					},
					new Dictionary<Definition, ContextValue<EvaluationNode>> {
						{ textDefinition, new ContextValue<EvaluationNode>(feature.Text.Location, new FormattedFeatureTextNode(feature.Text.Value, feature.RegexFormats)) }
					}
				);
		}

		public static IVariableBox GetTextVariables(CardFeatureConfig featureConfig) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				CardSectionEnvironments.GetVariables(featureConfig.cardSectionConfig),
				featureConfig.Variables,
				BaseTextDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetTextVariables(CardSection section) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				section.Environment,
				BaseTextDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetTextVariables(ICardSectionParent parent) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				parent.Variables,
				BaseTextDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static DefinitionEnvironment GetTextEnvironment(CardFeature feature) { // bool isListItem
			return DefinitionEnvironment.Create(
					new Dictionary<Definition, ContextValue<object>> {
						{ titleDefinition, new ContextValue<object>(feature.Title.Location, feature.Title.Value ?? "") }, // { titleDefinition, new ContextValue<object>(new DocumentSpan(-1), title ?? "") },
						{ noteDefinition, new ContextValue<object>(feature.Note.Location, feature.Note.Value ?? "") } // { noteDefinition, new ContextValue<object>(new DocumentSpan(-1), note ?? "") }
						// { listItemDefinition, new ContextValue<object>(new DocumentSpan(-1), isListItem) }
					}
				);
		}

		private class FormattedFeatureTextNode : EvaluationNode {
			public override bool IsConstant => text.IsConstant;
			public override EvaluationType ReturnType => EvaluationType.STRING;

			private readonly TextExpression text;
			private readonly RegexFormats formats;

			public FormattedFeatureTextNode(TextExpression text, RegexFormats formats) {
				this.text = text;
				this.formats = formats;
			}

			public override EvaluationNode Clone() => this;
			public override EvaluationNode Simplify() => this;

			public override object Evaluate(IEnvironment environment) {
				if (text != null) {
					/*
					RichString evalText = (RichString)this.text.Evaluate(environment);
					if (formats != null) {
						evalText = formats.Apply(evalText);
					}
					return evalText.Formatted;
					*/
					string rawText = this.text.Evaluate(environment);
					RichString richText = StringParsing.ParseRich(rawText);
					if (formats != null) {
						richText = formats.Apply(richText);
					}
					return StringParsing.EscapeRich(richText);
				}
				else {
					return "";
				}
			}

			public override IEnumerable<EvaluationName> GetVariables() {
				return text?.GetVariables() ?? Enumerable.Empty<EvaluationName>();
			}

			protected override string GetRepresentation() { throw new NotSupportedException(); }

		}

	}

	public static class CardEnvironmentUtils {

		public static EnvironmentVariableInfo GetVariableInfo(this Definition definition) {
			return new EnvironmentVariableInfo(definition.name, definition.Type.ReturnType, definition.description);
		}

		public static IEnumerable<EnvironmentVariableInfo> GetVariableInfos(this Definition definition) {
			yield return new EnvironmentVariableInfo(definition.name, definition.Type.ReturnType, definition.description);
			foreach (EvaluationName alias in definition.aliases) {
				yield return new EnvironmentVariableInfo(alias, definition.Type.ReturnType, definition.description);
			}
		}

	}

}
