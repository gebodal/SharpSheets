using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Definitions;
using SharpSheets.Evaluations;
using SharpSheets.Exceptions;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Cards.CardSubjects {

	[System.Diagnostics.DebuggerDisplay("# {Name} (@{TitleLocation.Line})")]
	public class CardSubjectBuilder {

		public DocumentSpan Location => Name.Location;
		public ContextValue<string> Name { get; }
		public CardSetConfig CardSetConfig { get; }

		public readonly List<CardSectionBuilder> sections;

		private ContextProperty<string>[]? setupProperties;

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		public CardSubjectBuilder(ContextValue<string> name, CardSetConfig cardSetConfig) {
			if (string.IsNullOrEmpty(name.Value)) {
				throw new ArgumentException("Card subject must have a name.");
			}

			this.Name = name;
			this.CardSetConfig = cardSetConfig;

			sections = new List<CardSectionBuilder>();

			setupProperties = null;
		}

		private CardSubject? BuildBase(CardSubjectSet subjectSet, List<SharpParsingException> errors) {

			List<(BoolExpression condition, SharpParsingException[] propertyErrors)> missingDefinitions = new List<(BoolExpression, SharpParsingException[])>();

			foreach (Conditional<CardConfig> cardConfig in CardSetConfig.cardConfigs) {
				if (cardConfig.Value.definitions.Count == 0 && cardConfig.Condition.IsTrue) {
					// Is this ever going to actually happen? Should something try and prevent it?
					return new CardSubject(subjectSet, Name, cardConfig.Value, DefinitionEnvironment.Empty);
				}
				else {
					IVariableBox subjectVariables = VariableBoxes.Concat(
						BasisEnvironment.Instance,
						CardSubjectEnvironments.GetVariables(cardConfig.Value));

					DefinitionEnvironment? subjectProperties = SubjectValueParsing.ParseValues(this.Name.Location, setupProperties ?? Array.Empty<ContextProperty<string>>(), subjectVariables, cardConfig.Value.definitions, out List<SharpParsingException> propertyErrors);
					if (subjectProperties != null) {
						try {
							CardSubject builtSubject = new CardSubject(subjectSet, Name, cardConfig.Value, subjectProperties);
							if (cardConfig.Evaluate(builtSubject.Environment)) {
								errors.AddRange(propertyErrors);
								return builtSubject;
							}
						}
						catch (Exception) { // TODO Just EvaluationException?
							errors.AddRange(propertyErrors); // Something went wrong trying to build subject. This should be reported.
						}
					}
					missingDefinitions.Add((cardConfig.Condition, propertyErrors.ToArray()));
				}
			}

			errors.Add(CreateUnresolvedDefinitionException(missingDefinitions.Select(i => (i.condition, i.propertyErrors.OfType<MissingDefinitionException>().ToArray())).ToArray()));
			// This is not ideal, can we think of a better option?
			errors.AddRange(missingDefinitions.SelectMany(i => i.propertyErrors));
			return null;
		}

		private SharpParsingException CreateUnresolvedDefinitionException(IList<(BoolExpression condition, MissingDefinitionException[] missing)> missingDefinitions) {
			if (missingDefinitions.Count > 0) {
				string errorMessage = $"No matching card configuration found. Must provide values for {(missingDefinitions.Count > 1 ? "one of " : "")}the following: " +
					string.Join("; ", missingDefinitions.Select(
						m =>
						"[" + string.Join(", ", m.missing.Select(e => e.Missing.name)) + "]"
						+ (!m.condition.IsTrue ? (" and match " + m.condition.ToString()) : "")
						)
					);

				return new SharpParsingException(Location, errorMessage);
			}
			else {
				return new SharpParsingException(Location, "No matching card configuration found.");
			}
		}

		public CardSubject? Build(CardSubjectSet subjectSet, out List<SharpParsingException> errors) {
			errors = new List<SharpParsingException>();

			CardSubject? subject = BuildBase(subjectSet, errors);

			if (subject == null) {
				return null;
			}

			foreach (CardSectionBuilder sectionBuilder in sections) {
				CardSection? section = sectionBuilder.Build(subject, out List<SharpParsingException> sectionErrors);
				errors.AddRange(sectionErrors);

				if (section != null) {
					subject.AddSection(section);
				}
			}

			return subject;
		}

		public static CardSubjectBuilder Reopen(CardSubject subject) {
			throw new NotImplementedException(); // TODO Implement
		}

		public void SetProperties(IEnumerable<ContextProperty<string>> properties) {
			this.setupProperties = properties.ToArray();
		}
	}

	[System.Diagnostics.DebuggerDisplay("## {Title} ({Note}) (@{TitleLocation.Line})")]
	public class CardSectionBuilder {

		public DocumentSpan Location { get; }

		public CardSubjectBuilder Subject { get; }
		public ContextValue<string> Heading { get; }
		public ContextValue<string> Note { get; }

		public List<CardFeatureBuilder> Features { get; }

		private readonly ContextValue<string>? details;

		private CardSectionBuilder(DocumentSpan location, CardSubjectBuilder subject, ContextValue<string>? heading, ContextValue<string>? note, ContextValue<string>? details) {
			this.Location = location;
			this.Subject = subject;
			this.Heading = heading.HasValue ? new ContextValue<string>(heading.Value.Location, heading.Value.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");
			//this.Heading = !string.IsNullOrEmpty(heading.Value) ? new ContextValue<string>(heading.Location, heading.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");
			this.Note = note.HasValue ? new ContextValue<string>(note.Value.Location, note.Value.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");
			this.details = details;
			this.Features = new List<CardFeatureBuilder>();
		}

		public static CardSectionBuilder Create(DocumentSpan location, CardSubjectBuilder subject, ContextValue<string>? heading, ContextValue<string>? note, ContextValue<string>? details) {
			return new CardSectionBuilder(location, subject, heading, note, details);
		}

		public static CardSectionBuilder Reopen(CardSection section) {
			throw new NotImplementedException(); // TODO Implement
		}

		/*
		private static IEnvironment GetBuildEnvironment(CardSubject subject, string title, string note) {
			return subject.Environment.AppendEnvironment(new Dictionary<EvaluationName, object> { { "title", title ?? "" }, { "note", note ?? "" } });
		}
		*/

		private CardSection? BuildBase(CardSubject subject, List<SharpParsingException> errors) {
			foreach (Conditional<AbstractCardSectionConfig> section in subject.CardConfig.cardSections.Concat(subject.CardConfig.cardSetConfig.cardSections).Where(s => s.Value != null && (s.Value is not DynamicCardSectionConfig dynamic || !dynamic.AlwaysInclude))) {
				if (section.Value.definitions.Count == 0 && section.Condition.IsTrue) {
					return new CardSection(Location, section.Value, subject, Heading, Note, DefinitionEnvironment.Empty);
				}
				else {
					IVariableBox sectionVariables = CardSectionEnvironments.GetVariables(section.Value);

					DefinitionEnvironment? sectionDetails = SubjectValueParsing.ParseValues(details, sectionVariables, section.Value.definitions, out List<SharpParsingException> sectionErrors);
					if (sectionDetails != null) {
						try {
							CardSection builtSection = new CardSection(Location, section.Value, subject, Heading, Note, sectionDetails);
							if (section.Evaluate(builtSection.Environment)) {
								errors.AddRange(sectionErrors);
								return builtSection;
							}
						}
						catch (Exception) { // TODO Just EvaluationException?
							errors.AddRange(sectionErrors); // Something went wrong trying to build section. This should be reported.
						}
					}
				}
			}

			errors.Add(new SharpParsingException(Location, "No matching section configuration found."));
			return null;
		}

		public CardSection? Build(CardSubject subject, out List<SharpParsingException> errors) {
			errors = new List<SharpParsingException>();

			CardSection? section = null;
			try {
				section = BuildBase(subject, errors);
			}
			catch(EvaluationException e) {
				errors.Add(new SharpParsingException(Location, e.Message, e));
			}

			if(section == null) {
				return null;
			}

			foreach (CardFeatureBuilder featureBuilder in Features) {
				CardFeature? feature;
				List<SharpParsingException> featureErrors;
				if (section.SectionConfig is DynamicCardSectionConfig dynamicSectionConfig) {
					feature = featureBuilder.BuildDynamic(section, dynamicSectionConfig, out featureErrors);
				}
				else {
					feature = featureBuilder.BuildSimple(section, out featureErrors);
				}

				if (feature != null) {
					section.AddFeature(feature);
				}
				else if (featureErrors.Count == 0) {
					errors.Add(new SharpParsingException(featureBuilder.Location, "Could not build feature."));
				}

				errors.AddRange(featureErrors);
			}

			return section;
		}

		public CardFeatureBuilder Add(DocumentSpan titleLocation, ContextValue<string>? title, ContextValue<string>? note, ContextValue<string>? details, ContextValue<string>? text, bool isListItem) {
			CardFeatureBuilder newFeature = CardFeatureBuilder.Create(titleLocation, this, title, note, details, text, isListItem, Features.Count); // TODO Why do I need to recreate the text ContextValue?!
			newFeature.IsMultiLine = false;
			Features.Add(newFeature);
			return newFeature;
		}

		public CardFeatureBuilder AddEmpty(DocumentSpan titleLocation, ContextValue<string> title, ContextValue<string>? note, ContextValue<string>? details) {
			CardFeatureBuilder newFeature = CardFeatureBuilder.Create(titleLocation, this, title, note, details, null, false, Features.Count);
			newFeature.IsMultiLine = true;
			Features.Add(newFeature);
			return newFeature;
		}

		public void AppendToLast(ContextValue<string> text) {
			CardFeatureBuilder lastEntry = Features.Last();
			lastEntry.IsMultiLine = true;
			if (string.IsNullOrEmpty(lastEntry.text.Value)) {
				lastEntry.text = text;
			}
			else {
				int startOffset = lastEntry.text.Location.Offset;
				int startLine = lastEntry.text.Location.Line;
				int startColumn = lastEntry.text.Location.Column;
				int endOffset = Math.Max(startOffset, text.Location.Offset + Math.Max(text.Location.Length, 0));
				int totalLength = endOffset - startOffset;
				DocumentSpan fullSpan = new DocumentSpan(startOffset, startLine, startColumn, totalLength);
				lastEntry.text = new ContextValue<string>(fullSpan, string.Join(" ", lastEntry.text.Value, text.Value));
			}
		}
	}

	[System.Diagnostics.DebuggerDisplay("### {Title} ({Note}) (@{TitleLocation.Line})")]
	public class CardFeatureBuilder {

		public DocumentSpan Location { get; }

		public CardSectionBuilder Section { get; }
		public ContextValue<string> Title { get; }
		public ContextValue<string> Note { get; }

		public ContextValue<string> text;

		public bool IsMultiLine { get; set; }
		public bool IsListItem { get; }
		public int Index { get; }

		private readonly ContextValue<string> details;

		private CardFeatureBuilder(DocumentSpan location, CardSectionBuilder section, ContextValue<string>? title, ContextValue<string>? note, ContextValue<string>? details, ContextValue<string>? text, bool isListItem, int index) {
			this.Location = location;
			this.Section = section;
			this.Title = title.HasValue ? new ContextValue<string>(title.Value.Location, title.Value.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");
			this.Note = note.HasValue ? new ContextValue<string>(note.Value.Location, note.Value.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");

			this.text = new ContextValue<string>(text?.Location ?? location, text?.Value ?? "");

			this.details = details ?? new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");

			this.IsListItem = isListItem;
			this.Index = index;
		}

		public static CardFeatureBuilder Create(DocumentSpan location, CardSectionBuilder section, ContextValue<string>? title, ContextValue<string>? note, ContextValue<string>? details, ContextValue<string>? text, bool isListItem, int index) {
			return new CardFeatureBuilder(location, section, title, note, details, text, isListItem, index);
		}

		public static CardFeatureBuilder Reopen(CardFeature feature) {
			throw new NotImplementedException(); // TODO Implement
		}

		/*
		private static IEnvironment GetBuildEnvironment(CardSection section, string title, string note, string text, bool isListItem) {
			return section.Environment.AppendEnvironment(new Dictionary<EvaluationName, object> { { "title", title ?? "" }, { "note", note ?? "" }, { "text", text ?? "" }, { "listitem", isListItem } });
		}
		*/

		public CardFeature? BuildDynamic(CardSection section, DynamicCardSectionConfig sectionConfig, out List<SharpParsingException> errors) {
			// Need to use RegexFormats to properly format text at some point
			// return formatting.Apply(new RichString(textStr)).Formatted;
			//new CardFeatureConcrete(Line, FeatureConfig, section, Title, Note, )

			errors = new List<SharpParsingException>();

			foreach (Conditional<CardFeatureConfig> feature in sectionConfig.cardFeatures) {
				if (feature.Value.definitions.Count == 0 && feature.Condition.IsTrue) {
					IVariableBox textParseVariables = CardFeatureEnvironments.GetTextVariables(feature.Value);
					ContextValue<TextExpression> textExpr = ParseText(textParseVariables, out List<SharpParsingException> textErrors);
					errors.AddRange(textErrors);
					return new CardFeature(Location, feature.Value, section, Title, Note, textExpr, DefinitionEnvironment.Empty, IsMultiLine, IsListItem, Index);
				}
				else {
					IVariableBox featureEnvironment1 = CardFeatureEnvironments.GetVariables(feature.Value);
					DefinitionEnvironment? featureDetails = SubjectValueParsing.ParseValues(details, featureEnvironment1, feature.Value.definitions, out List<SharpParsingException> featureErrors);
					if (featureDetails != null) {
						try {
							IVariableBox textParseVariables = CardFeatureEnvironments.GetTextVariables(feature.Value);
							ContextValue<TextExpression> textExpr = ParseText(textParseVariables, out List<SharpParsingException> textErrors);

							CardFeature builtFeature = new CardFeature(Location, feature.Value, section, Title, Note, textExpr, featureDetails, IsMultiLine, IsListItem, Index);
							if (feature.Evaluate(builtFeature.Environment)) {
								errors.AddRange(featureErrors);
								errors.AddRange(textErrors);
								return builtFeature;
							}
						}
						catch (Exception) {
							// Something went wrong trying to build feature. This should be reported.
							errors.AddRange(featureErrors);
						}
					}
				}
			}

			//errors.Add(new SharpParsingException(Location, "Could not find matching feature configuration."));
			errors.Add(new UnknownFeatureConfigException(Location, sectionConfig));

			return null;
		}

		public CardFeature BuildSimple(CardSection section, out List<SharpParsingException> errors) {
			// Need to use RegexFormats to properly format text at some point
			// return formatting.Apply(new RichString(textStr)).Formatted;
			//new CardFeatureConcrete(Line, FeatureConfig, section, Title, Note, )

			errors = new List<SharpParsingException>();

			// This is the case for Text and Table sections
			IVariableBox textParseVariables = CardFeatureEnvironments.GetTextVariables(section);
			ContextValue<TextExpression> textExpr = ParseText(textParseVariables, out List<SharpParsingException> textErrors);
			errors.AddRange(textErrors);

			return new CardFeature(Location, null, section, Title, Note, textExpr, DefinitionEnvironment.Empty, IsMultiLine, IsListItem, Index);
		}

		private ContextValue<TextExpression> ParseText(IVariableBox variables, out List<SharpParsingException> errors) {
			errors = new List<SharpParsingException>();
			TextExpression textExpr;
			try {
				textExpr = Interpolation.Parse(text.Value, variables, false);
			}
			catch (EvaluationException e) {
				errors.Add(new SharpParsingException(Location, e.Message, e));
				textExpr = new TextExpression(text.Value);
			}
			catch (FormatException e) {
				errors.Add(new SharpParsingException(Location, e.Message, e));
				textExpr = new TextExpression(text.Value);
			}
			return new ContextValue<TextExpression>(text.Location, textExpr);
		}
	}

}