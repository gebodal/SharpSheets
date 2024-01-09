using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Parsing;
using SharpSheets.Canvas.Text;
using System.Collections.Generic;
using System.Linq;

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

		static readonly Definition nameDefinition = new ConstantDefinition("name", Array.Empty<EvaluationName>(), "Card name description", EvaluationType.STRING);

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

		public static DefinitionEnvironment GetBuildEnvironment(ContextValue<string> name) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ nameDefinition, new ContextValue<object>(name.Location, name.Value ?? "") }
			});
		}

	}

	public static class CardOutlinesEnvironments {

		static readonly Definition cardnumDefinition = new ConstantDefinition("card", new EvaluationName[] { "cardnum" }, "Card index description", EvaluationType.INT);
		static readonly Definition cardcountDefinition = new ConstantDefinition("cardcount", new EvaluationName[] { "totalcards" }, "Total cardcount description", EvaluationType.INT);

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

		static readonly Definition headingDefinition = new ConstantDefinition("heading", Array.Empty<EvaluationName>(), "Section heading description", EvaluationType.STRING);
		static readonly Definition noteDefinition = new FallbackDefinition("note", Array.Empty<EvaluationName>(), "Section note description", EvaluationType.STRING, new ConstantNode(""));

		public static readonly DefinitionGroup BaseDefinitions;

		static CardSectionEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				headingDefinition,
				noteDefinition
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

		public static IEnvironment GetDryRun(AbstractCardSectionConfig sectionConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(sectionConfig)));
		}

		public static DefinitionEnvironment MakeBaseEnvironment(ContextValue<string> heading, ContextValue<string> note) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ headingDefinition, new ContextValue<object>(heading.Location, heading.Value) },
				{ noteDefinition, new ContextValue<object>(note.Location, note.Value) }
			});
		}

		public static DefinitionEnvironment GetBuildEnvironment(ContextValue<string> heading, ContextValue<string> note) {
			return DefinitionEnvironment.Create(new Dictionary<Definition, ContextValue<object>> {
				{ headingDefinition, new ContextValue<object>(heading.Location, heading.Value ?? "") },
				{ noteDefinition, new ContextValue<object>(note.Location, note.Value ?? "") }
			});
		}

	}

	public static class CardSectionOutlineEnvironments {

		static readonly Definition partnumDefinition = new ConstantDefinition("partnum", Array.Empty<EvaluationName>(), "Section part index description", EvaluationType.INT);
		static readonly Definition partcountDefinition = new ConstantDefinition("partcount", new EvaluationName[] { "totalparts" }, "Total partcount description", EvaluationType.INT);

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

		static readonly Definition titleDefinition = new ConstantDefinition("title", Array.Empty<EvaluationName>(), "Feature title description", EvaluationType.STRING);
		static readonly Definition noteDefinition = new FallbackDefinition("note", Array.Empty<EvaluationName>(), "Feature note description", EvaluationType.STRING, new ConstantNode(""));
		static readonly Definition textDefinition = new ConstantDefinition("text", Array.Empty<EvaluationName>(), "Feature text description", EvaluationType.STRING);
		static readonly Definition listItemDefinition = new ConstantDefinition("listitem", Array.Empty<EvaluationName>(), "Feature list item description", EvaluationType.BOOL);
		
		static readonly Definition featureNumDefinition = new ConstantDefinition("featureNum", Array.Empty<EvaluationName>(), "Feature number description", EvaluationType.INT);

		public static readonly DefinitionGroup BaseDefinitions;

		public static readonly StringExpression TextExpression = new StringExpression(new VariableNode(new EvaluationName("text"), EvaluationType.STRING));

		static CardFeatureEnvironments() {
			BaseDefinitions = new DefinitionGroup() {
				titleDefinition,
				noteDefinition,
				textDefinition,
				listItemDefinition,
				featureNumDefinition
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

		public static IEnvironment GetDryRun(CardFeatureConfig featureConfig) {
			return BasisEnvironment.Instance.AppendEnvironment(new DryRunEnvironment(GetVariables(featureConfig)));
		}

		public static DefinitionEnvironment MakeBaseEnvironment(ContextValue<string> title, ContextValue<string> note, ContextValue<TextExpression> text, RegexFormats regexFormats, bool isListItem, int index) {
			return DefinitionEnvironment.Create(
					new Dictionary<Definition, ContextValue<object>> {
						{ titleDefinition, new ContextValue<object>(title.Location, title.Value) },
						{ noteDefinition, new ContextValue<object>(note.Location, note.Value) },
						{ listItemDefinition, new ContextValue<object>(DocumentSpan.Imaginary, isListItem) },
						{ featureNumDefinition, new ContextValue<object>(DocumentSpan.Imaginary, index) }
					},
					new Dictionary<Definition, ContextValue<EvaluationNode>> {
						{ textDefinition, new ContextValue<EvaluationNode>(text.Location, new FormattedFeatureTextNode(text.Value, regexFormats)) }
					}
				);
		}

		public static DefinitionEnvironment GetTextEnvironment(ContextValue<string> title, ContextValue<string> note) { // bool isListItem
			return DefinitionEnvironment.Create(
					new Dictionary<Definition, ContextValue<object>> {
						{ titleDefinition, new ContextValue<object>(title.Location, title.Value ?? "") }, // { titleDefinition, new ContextValue<object>(new DocumentSpan(-1), title ?? "") },
						{ noteDefinition, new ContextValue<object>(note.Location, note.Value ?? "") } // { noteDefinition, new ContextValue<object>(new DocumentSpan(-1), note ?? "") }
						// { listItemDefinition, new ContextValue<object>(new DocumentSpan(-1), isListItem) }
					}
				);
		}

		public static DefinitionEnvironment GetBuildEnvironment(ContextValue<string> title, ContextValue<string> note, ContextValue<TextExpression> text, RegexFormats regexFormats, bool isListItem, int index) {
			return DefinitionEnvironment.Create(
					new Dictionary<Definition, ContextValue<object>> {
						{ titleDefinition, new ContextValue<object>(title.Location, title.Value ?? "") },
						{ noteDefinition, new ContextValue<object>(note.Location, note.Value ?? "") },
						{ listItemDefinition, new ContextValue<object>(DocumentSpan.Imaginary, isListItem) },
						{ featureNumDefinition, new ContextValue<object>(DocumentSpan.Imaginary, index) }
					},
					new Dictionary<Definition, ContextValue<EvaluationNode>> {
						{ textDefinition, new ContextValue<EvaluationNode>(text.Location, new FormattedFeatureTextNode(text.Value, regexFormats)) }
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

}
