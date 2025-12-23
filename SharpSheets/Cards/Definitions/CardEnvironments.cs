using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Parsing;
using SharpSheets.Canvas.Text;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Evaluations.Types;

namespace SharpSheets.Cards.Definitions {

	public static class CardEnvironments {
		public static readonly EvaluationContext Context = EvaluationContext.Create().Build();
		public static readonly IEnvironment Basis = BasisEnvironment.MakeInstance(Context);

		public static readonly IntEvaluationType INT = Context.GetType<IntEvaluationType>();
		public static readonly UIntEvaluationType UINT = Context.GetType<UIntEvaluationType>();
		public static readonly FloatEvaluationType FLOAT = Context.GetType<FloatEvaluationType>();
		public static readonly UFloatEvaluationType UFLOAT = Context.GetType<UFloatEvaluationType>();
		public static readonly BoolEvaluationType BOOL = Context.GetType<BoolEvaluationType>();
		public static readonly StringEvaluationType STRING = Context.GetType<StringEvaluationType>();
	}

	public static class CardSetConfigEnvironments {
		public static DefinitionGroup BaseDefinitions => CardSubjectEnvironments.BaseDefinitions;
	}

	public static class CardConfigEnvironments {
		public static DefinitionGroup BaseDefinitions => CardSubjectEnvironments.BaseDefinitions;
	}

	public static class CardSubjectEnvironments {

		public static readonly Definition nameDefinition = new ConstantDefinition(
			"name", Array.Empty<EvaluationName>(),
			"The subject name text.",
			CardEnvironments.STRING,
			null);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSubjectEnvironments() {
			BaseDefinitions = new DefinitionGroup(CardEnvironments.Context) {
				nameDefinition
			};
		}

		public static IVariableBox GetVariables(ICardSegmentParent parent) {
			return VariableBoxes.Concat(CardEnvironments.Basis, parent.Variables, BaseDefinitions); // Do we actually have to append BaseDefinitions these here...?
		}

		public static IVariableBox GetVariables(CardSetConfig cardSetConfig) {
			return VariableBoxes.Concat(CardEnvironments.Basis, cardSetConfig.Variables, BaseDefinitions); // Do we actually have to append BaseDefinitions these here...?
		}

		public static IVariableBox GetVariables(CardConfig cardConfig) {
			return VariableBoxes.Concat(CardEnvironments.Basis, cardConfig.Variables, BaseDefinitions); // Do we actually have to append BaseDefinitions these here...?
		}

		public static IEnvironment GetDryRun(CardConfig cardConfig) {
			return CardEnvironments.Basis.AppendEnvironment(new DryRunEnvironment(GetVariables(cardConfig), cardConfig.Variables));
		}

		public static DefinitionEnvironment MakeBaseEnvironment(ContextValue<string> name) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<EvaluationValue>> {
				{ nameDefinition, new ContextValue<EvaluationValue>(name.Location, new EvaluationValue(name.Value, nameDefinition.Type.ReturnType)) }
			});
		}

	}

	public static class CardOutlinesEnvironments {

		public static readonly Definition cardnumDefinition = new ConstantDefinition(
			"card", new EvaluationName[] { "cardnum" },
			"The index of the current card being drawn in the current layout. This is zero-indexed, meaning that the first card has an index of 0.",
			CardEnvironments.INT,
			null);
		public static readonly Definition cardcountDefinition = new ConstantDefinition(
			"cardcount", new EvaluationName[] { "totalcards" },
			"The total number of cards in the current card layout.",
			CardEnvironments.INT,
			null);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardOutlinesEnvironments() {
			BaseDefinitions = new DefinitionGroup(CardEnvironments.Context) {
				cardnumDefinition,
				cardcountDefinition
			};
		}

		public static IVariableBox GetVariables(CardSetConfig cardSetConfig) {
			return VariableBoxes.Concat(CardEnvironments.Basis, CardSubjectEnvironments.GetVariables(cardSetConfig), BaseDefinitions);
		}

		public static IVariableBox GetVariables(CardConfig cardConfig) {
			return VariableBoxes.Concat(CardEnvironments.Basis, CardSubjectEnvironments.GetVariables(cardConfig), BaseDefinitions);
		}

		public static IEnvironment GetDryRun(CardSetConfig cardSetConfig) {
			return CardEnvironments.Basis.AppendEnvironment(new DryRunEnvironment(GetVariables(cardSetConfig), cardSetConfig.Variables));
		}

		public static IEnvironment GetDryRun(CardConfig cardConfig) {
			return CardEnvironments.Basis.AppendEnvironment(new DryRunEnvironment(GetVariables(cardConfig), cardConfig.Variables));
		}

		public static DefinitionEnvironment GetEnvironment(int card, int totalCards) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<EvaluationValue>> {
				{ cardnumDefinition, new ContextValue<EvaluationValue>(DocumentSpan.Imaginary, new EvaluationValue(card, cardnumDefinition.Type.ReturnType)) },
				{ cardcountDefinition, new ContextValue<EvaluationValue>(DocumentSpan.Imaginary, new EvaluationValue(totalCards, cardcountDefinition.Type.ReturnType)) }
			});
		}

	}

	public static class CardSegmentEnvironments {

		public static readonly Definition headingDefinition = new ConstantDefinition(
			"heading", Array.Empty<EvaluationName>(),
			"The segment heading text, without note or details.",
			CardEnvironments.STRING,
			null);
		public static readonly Definition noteDefinition = new FallbackDefinition(
			"subheading", Array.Empty<EvaluationName>(),
			"The segment subheading text (which may be empty).",
			CardEnvironments.STRING,
			new ConstantNode("", CardEnvironments.STRING));

		public static readonly Definition featureCountDefinition = new ConstantDefinition(
			"featurecount", new EvaluationName[] { "totalfeatures" },
			"The total number of features in the current card segment.",
			CardEnvironments.INT,
			null);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSegmentEnvironments() {
			BaseDefinitions = new DefinitionGroup(CardEnvironments.Context) {
				headingDefinition,
				noteDefinition,
				featureCountDefinition
			};
		}

		public static IVariableBox GetVariables(AbstractCardSegmentConfig segmentConfig) {
			return VariableBoxes.Concat(
				CardEnvironments.Basis,
				CardSubjectEnvironments.GetVariables(segmentConfig.parent),
				segmentConfig.Variables,
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetVariables(ICardSegmentParent parent) {
			return VariableBoxes.Concat(
				CardEnvironments.Basis,
				CardSubjectEnvironments.GetVariables(parent),
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IEnvironment GetDryRun(AbstractCardSegmentConfig segmentConfig) {
			return CardEnvironments.Basis.AppendEnvironment(new DryRunEnvironment(GetVariables(segmentConfig), segmentConfig.Variables));
		}

		public static IEnvironment MakeBaseEnvironment(CardSegment segment) {
			return new CardSegmentEnvironment(segment);
		}

		private class CardSegmentEnvironment : AbstractDataEnvironment {
			private readonly CardSegment segment;

			public override bool IsEmpty { get; } = false;

			public CardSegmentEnvironment(CardSegment segment) : base(CardEnvironments.Context) {
				this.segment = segment;
			}

			public override bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
				return BaseDefinitions.TryGetVariableInfo(key, out variableInfo);
			}

			public override bool TryGetValue(EvaluationName key, [NotNullWhen(true)] out EvaluationValue? value) {
				if (BaseDefinitions.TryGetDefinition(key, out Definition? definition)) {
					value = definition.name.ToString() switch {
						"heading" => new EvaluationValue(segment.Heading.Value, CardEnvironments.STRING),
						"subheading" => new EvaluationValue(segment.Note.Value, CardEnvironments.STRING),
						"featurecount" => new EvaluationValue(segment.Count, CardEnvironments.INT),
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
			CardEnvironments.INT,
			null);
		public static readonly Definition partcountDefinition = new ConstantDefinition(
			"partcount", new EvaluationName[] { "totalparts" },
			"The total number of segment parts/segments in the current segment for the current card layout.",
			CardEnvironments.INT,
			null);

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSegmentOutlineEnvironments() {
			BaseDefinitions = new DefinitionGroup(CardEnvironments.Context) {
				partnumDefinition,
				partcountDefinition
			};
		}

		public static IVariableBox GetVariables(AbstractCardSegmentConfig segmentConfig) {
			return VariableBoxes.Concat(CardEnvironments.Basis, CardSegmentEnvironments.GetVariables(segmentConfig), BaseDefinitions);
		}

		public static IEnvironment GetDryRun(AbstractCardSegmentConfig segmentConfig) {
			return CardEnvironments.Basis.AppendEnvironment(new DryRunEnvironment(GetVariables(segmentConfig), segmentConfig.Variables));
		}

		public static DefinitionEnvironment GetEnvironment(int partnum, int totalParts) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<EvaluationValue>> {
				{ partnumDefinition, new ContextValue<EvaluationValue>(DocumentSpan.Imaginary, new EvaluationValue(partnum, partnumDefinition.Type.ReturnType)) },
				{ partcountDefinition, new ContextValue<EvaluationValue>(DocumentSpan.Imaginary, new EvaluationValue(totalParts, partcountDefinition.Type.ReturnType)) }
			});
		}
	}

	public static class CardFeatureEnvironments {

		public static readonly Definition titleDefinition = new ConstantDefinition(
			"title", Array.Empty<EvaluationName>(),
			"The feature title text (without note or details).",
			CardEnvironments.STRING,
			null);
		public static readonly Definition noteDefinition = new FallbackDefinition(
			"subtitle", Array.Empty<EvaluationName>(),
			"The feature subtitle text (which may be empty).",
			CardEnvironments.STRING,
			new ConstantNode("", CardEnvironments.STRING));
		public static readonly Definition textDefinition = new ConstantDefinition(
			"text", Array.Empty<EvaluationName>(),
			"The feature text content.",
			CardEnvironments.STRING,
			null);
		public static readonly Definition listItemDefinition = new ConstantDefinition(
			"listitem", Array.Empty<EvaluationName>(),
			"A flag indicating if the current feature is an item in a list.",
			CardEnvironments.BOOL,
			null);

		public static readonly Definition featureNumDefinition = new ConstantDefinition(
			"featureNum", Array.Empty<EvaluationName>(),
			"The index of the current feature being drawn in the current card segment. This is zero-indexed, meaning that the first feature has an index of 0.",
			CardEnvironments.INT,
			null);

		public static readonly DefinitionGroup BaseDefinitions;
		public static readonly DefinitionGroup BaseTextDefinitions;

		public static readonly StringExpression TextExpression = new StringExpression(new VariableNode(new EvaluationName("text"), CardEnvironments.STRING));

		static CardFeatureEnvironments() {
			BaseDefinitions = new DefinitionGroup(CardEnvironments.Context) {
				titleDefinition,
				noteDefinition,
				textDefinition,
				listItemDefinition,
				featureNumDefinition
			};

			BaseTextDefinitions = new DefinitionGroup(CardEnvironments.Context) {
				titleDefinition,
				noteDefinition,
				listItemDefinition,
				featureNumDefinition
			};
		}

		public static IVariableBox GetVariables(CardFeatureConfig featureConfig) {
			return VariableBoxes.Concat(
				CardEnvironments.Basis,
				CardSegmentEnvironments.GetVariables(featureConfig.cardSegmentConfig),
				featureConfig.Variables,
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetVariables(ICardSegmentParent parent) {
			return VariableBoxes.Concat(
				CardEnvironments.Basis,
				CardSegmentEnvironments.GetVariables(parent),
				BaseDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IEnvironment GetDryRun(CardFeatureConfig featureConfig) {
			return CardEnvironments.Basis.AppendEnvironment(new DryRunEnvironment(GetVariables(featureConfig), featureConfig.Variables));
		}

		public static DefinitionEnvironment MakeBaseEnvironment(CardFeature feature) {
			return DefinitionEnvironment.Create(
					new Dictionary<Definition, ContextValue<EvaluationValue>> {
						{ titleDefinition, new ContextValue<EvaluationValue>(feature.Title.Location, new EvaluationValue(feature.Title.Value, titleDefinition.Type.ReturnType)) },
						{ noteDefinition, new ContextValue<EvaluationValue>(feature.Note.Location, new EvaluationValue(feature.Note.Value, noteDefinition.Type.ReturnType)) },
						{ listItemDefinition, new ContextValue<EvaluationValue>(DocumentSpan.Imaginary, new EvaluationValue(feature.IsListItem, listItemDefinition.Type.ReturnType)) },
						{ featureNumDefinition, new ContextValue<EvaluationValue>(DocumentSpan.Imaginary, new EvaluationValue(feature.Index, featureNumDefinition.Type.ReturnType)) }
					},
					new Dictionary<Definition, ContextValue<EvaluationNode>> {
						{ textDefinition, new ContextValue<EvaluationNode>(feature.Text.Location, new FormattedFeatureTextNode(feature.Text.Value, feature.RegexFormats)) }
					}
				);
		}

		public static IVariableBox GetTextVariables(CardFeatureConfig featureConfig) {
			return VariableBoxes.Concat(
				CardEnvironments.Basis,
				CardSegmentEnvironments.GetVariables(featureConfig.cardSegmentConfig),
				featureConfig.definitions, // definitions used here otherwise we repeat non-text variables
				BaseTextDefinitions
				);
		}

		public static IVariableBox GetTextVariables(CardSegment segment) {
			return VariableBoxes.Concat(
				CardEnvironments.Basis,
				segment.Environment,
				BaseTextDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static IVariableBox GetTextVariables(ICardSegmentParent parent) {
			return VariableBoxes.Concat(
				CardEnvironments.Basis,
				parent.Variables,
				BaseTextDefinitions // Do we actually have to append BaseDefinitions these here...?
				);
		}

		public static DefinitionEnvironment GetTextEnvironment(CardFeature feature) { // bool isListItem
			return DefinitionEnvironment.Create(
					new Dictionary<Definition, ContextValue<EvaluationValue>> {
						{ titleDefinition, new ContextValue<EvaluationValue>(feature.Title.Location, new EvaluationValue(feature.Title.Value ?? "", titleDefinition.Type.ReturnType)) }, // { titleDefinition, new ContextValue<object>(new DocumentSpan(-1), title ?? "") },
						{ noteDefinition, new ContextValue<EvaluationValue>(feature.Note.Location, new EvaluationValue(feature.Note.Value ?? "", noteDefinition.Type.ReturnType)) }, // { noteDefinition, new ContextValue<object>(new DocumentSpan(-1), note ?? "") }
						{ listItemDefinition, new ContextValue<EvaluationValue>(DocumentSpan.Imaginary, new EvaluationValue(feature.IsListItem, listItemDefinition.Type.ReturnType)) },
						{ featureNumDefinition, new ContextValue<EvaluationValue>(DocumentSpan.Imaginary, new EvaluationValue(feature.Index, featureNumDefinition.Type.ReturnType)) }
					}
				);
		}

		private class FormattedFeatureTextNode : EvaluationNode {
			public override bool IsConstant => text.IsConstant;
			public override EvaluationType GetReturnType() => Context.GetType<StringEvaluationType>();

			private readonly TextExpression text;
			private readonly RegexFormats formats;

			public FormattedFeatureTextNode(TextExpression text, RegexFormats formats) : base(text.Context) {
				this.text = text;
				this.formats = formats;
			}

			public override EvaluationNode Clone() => this;
			public override EvaluationNode Simplify() => this;

			private EvaluationValue MakeValue(string value) => new EvaluationValue(value, Context.GetType<StringEvaluationType>());

			public override EvaluationValue Evaluate(IEnvironment environment) {
				if (text != null) {
					string rawText = this.text.Evaluate(environment);
					RichString richText = StringParsing.ParseRich(rawText);
					if (formats != null) {
						richText = formats.Apply(richText);
					}
					return MakeValue(StringParsing.EscapeRich(richText));
				}
				else {
					return MakeValue("");
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
