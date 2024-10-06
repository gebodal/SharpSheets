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

		//public static readonly DefinitionGroup BaseDefinitions;
		public static DefinitionGroup BaseDefinitions => CardSubjectEnvironments.BaseDefinitions;

		static CardSetConfigEnvironments() {
			//BaseDefinitions = new DefinitionGroup();
		}
	}

	public static class CardConfigEnvironments {

		//public static readonly DefinitionGroup BaseDefinitions;
		public static DefinitionGroup BaseDefinitions => CardSubjectEnvironments.BaseDefinitions;

		static CardConfigEnvironments() {
			//BaseDefinitions = new DefinitionGroup();
		}
	}

	public static class CardSubjectEnvironments {

		public static readonly Definition nameDefinition = new ConstantDefinition(
			"name", Array.Empty<EvaluationName>(),
			"The subject name text.",
			EvaluationType.STRING,
			null);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSubjectEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				nameDefinition
			};
		}

		public static IVariableBox GetVariables(ICardSegmentParent parent) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, parent.Variables, BaseDefinitions); // Do we actually have to append BaseDefinitions these here...?
		}

		public static IVariableBox GetVariables(CardSetConfig cardSetConfig) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, cardSetConfig.Variables, BaseDefinitions); // Do we actually have to append BaseDefinitions these here...?
		}

		public static IVariableBox GetVariables(CardConfig cardConfig) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, cardConfig.Variables, BaseDefinitions); // Do we actually have to append BaseDefinitions these here...?
		}

		public static IEnvironment GetDryRun(CardConfig cardConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(cardConfig), cardConfig.Variables));
		}

		public static DefinitionEnvironment MakeBaseEnvironment(ContextValue<string> name) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ nameDefinition, new ContextValue<object>(name.Location, name.Value) }
			});
		}

	}

	public static class CardOutlinesEnvironments {

		public static readonly Definition cardnumDefinition = new ConstantDefinition(
			"card", new EvaluationName[] { "cardnum" },
			"The index of the current card being drawn in the current layout. This is zero-indexed, meaning that the first card has an index of 0.",
			EvaluationType.INT,
			null);
		public static readonly Definition cardcountDefinition = new ConstantDefinition(
			"cardcount", new EvaluationName[] { "totalcards" },
			"The total number of cards in the current card layout.",
			EvaluationType.INT,
			null);

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
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(cardSetConfig), cardSetConfig.Variables));
		}

		public static IEnvironment GetDryRun(CardConfig cardConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(cardConfig), cardConfig.Variables));
		}

		public static DefinitionEnvironment GetEnvironment(int card, int totalCards) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ cardnumDefinition, new ContextValue<object>(DocumentSpan.Imaginary, card) },
				{ cardcountDefinition, new ContextValue<object>(DocumentSpan.Imaginary, totalCards) }
			});
		}

	}

	public static class CardSegmentEnvironments {

		public static readonly Definition headingDefinition = new ConstantDefinition(
			"heading", Array.Empty<EvaluationName>(),
			"The segment heading text, without note or details.",
			EvaluationType.STRING,
			null);
		public static readonly Definition noteDefinition = new FallbackDefinition(
			"subheading", Array.Empty<EvaluationName>(),
			"The segment subheading text (which may be empty).",
			EvaluationType.STRING,
			new ConstantNode(""));

		public static readonly Definition featureCountDefinition = new ConstantDefinition(
			"featurecount", new EvaluationName[] { "totalfeatures" },
			"The total number of features in the current card segment.",
			EvaluationType.INT,
			null);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSegmentEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				headingDefinition,
				noteDefinition,
				featureCountDefinition
			};
		}

		public static IVariableBox GetVariables(AbstractCardSegmentConfig segmentConfig) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				CardSubjectEnvironments.GetVariables(segmentConfig.parent),
				segmentConfig.Variables,
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetVariables(ICardSegmentParent parent) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				CardSubjectEnvironments.GetVariables(parent),
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IEnvironment GetDryRun(AbstractCardSegmentConfig segmentConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(segmentConfig), segmentConfig.Variables));
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

		public static IEnvironment MakeBaseEnvironment(CardSegment segment) {
			return new CardSegmentEnvironment(segment);
		}

		private class CardSegmentEnvironment : AbstractDataEnvironment {
			private readonly CardSegment segment;

			public CardSegmentEnvironment(CardSegment segment) {
				this.segment = segment;
			}

			public override bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
				return BaseDefinitions.TryGetVariableInfo(key, out variableInfo);
			}

			public override bool TryGetValue(EvaluationName key, out object? value) {
				if (BaseDefinitions.TryGetDefinition(key, out Definition? definition)) {
					value = definition.name.ToString() switch {
						"heading" => segment.Heading.Value,
						"subheading" => segment.Note.Value,
						"featurecount" => segment.Count,
						_ => throw new InvalidOperationException("Unknown card segment definition.")
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

	public static class CardSegmentOutlineEnvironments {

		public static readonly Definition partnumDefinition = new ConstantDefinition(
			"partnum", Array.Empty<EvaluationName>(),
			"The index of the current segment part/segment being drawn in the current card segment. This is zero-indexed, meaning that the first part has an index of 0.",
			EvaluationType.INT,
			null);
		public static readonly Definition partcountDefinition = new ConstantDefinition(
			"partcount", new EvaluationName[] { "totalparts" },
			"The total number of segment parts/segments in the current segment for the current card layout.",
			EvaluationType.INT,
			null);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSegmentOutlineEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				partnumDefinition,
				partcountDefinition
			};
		}

		public static IVariableBox GetVariables(AbstractCardSegmentConfig segmentConfig) {
			return VariableBoxes.Concat(BasisEnvironment.Instance, CardSegmentEnvironments.GetVariables(segmentConfig), BaseDefinitions);
		}

		public static IEnvironment GetDryRun(AbstractCardSegmentConfig segmentConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(segmentConfig), segmentConfig.Variables));
		}

		public static DefinitionEnvironment GetEnvironment(int partnum, int totalParts) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ partnumDefinition, new ContextValue<object>(DocumentSpan.Imaginary, partnum) },
				{ partcountDefinition, new ContextValue<object>(DocumentSpan.Imaginary, totalParts) }
			});
		}
	}

	public static class CardFeatureEnvironments {

		public static readonly Definition titleDefinition = new ConstantDefinition(
			"title", Array.Empty<EvaluationName>(),
			"The feature title text (without note or details).",
			EvaluationType.STRING,
			null);
		public static readonly Definition noteDefinition = new FallbackDefinition(
			"subtitle", Array.Empty<EvaluationName>(),
			"The feature subtitle text (which may be empty).",
			EvaluationType.STRING,
			new ConstantNode(""));
		public static readonly Definition textDefinition = new ConstantDefinition(
			"text", Array.Empty<EvaluationName>(),
			"The feature text content.",
			EvaluationType.STRING,
			null);
		public static readonly Definition listItemDefinition = new ConstantDefinition(
			"listitem", Array.Empty<EvaluationName>(),
			"A flag indicating if the current feature is an item in a list.",
			EvaluationType.BOOL,
			null);

		public static readonly Definition featureNumDefinition = new ConstantDefinition(
			"featureNum", Array.Empty<EvaluationName>(),
			"The index of the current feature being drawn in the current card segment. This is zero-indexed, meaning that the first feature has an index of 0.",
			EvaluationType.INT,
			null);

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
				noteDefinition,
				listItemDefinition,
				featureNumDefinition
			};
		}

		public static IVariableBox GetVariables(CardFeatureConfig featureConfig) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				CardSegmentEnvironments.GetVariables(featureConfig.cardSegmentConfig),
				featureConfig.Variables,
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetVariables(ICardSegmentParent parent) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				CardSegmentEnvironments.GetVariables(parent),
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IEnvironment GetDryRun(CardFeatureConfig featureConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(featureConfig), featureConfig.Variables));
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
				CardSegmentEnvironments.GetVariables(featureConfig.cardSegmentConfig),
				featureConfig.definitions, // definitions used here otherwise we repeat non-text variables
				BaseTextDefinitions
				);
		}

		public static IVariableBox GetTextVariables(CardSegment segment) {
			return VariableBoxes.Concat(
				BasisEnvironment.Instance,
				segment.Environment,
				BaseTextDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetTextVariables(ICardSegmentParent parent) {
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
						{ noteDefinition, new ContextValue<object>(feature.Note.Location, feature.Note.Value ?? "") }, // { noteDefinition, new ContextValue<object>(new DocumentSpan(-1), note ?? "") }
						{ listItemDefinition, new ContextValue<object>(DocumentSpan.Imaginary, feature.IsListItem) },
						{ featureNumDefinition, new ContextValue<object>(DocumentSpan.Imaginary, feature.Index) }
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
