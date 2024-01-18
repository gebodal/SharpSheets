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

		public readonly List<CardSegmentBuilder> segments;

		private ContextProperty<string>[]? setupProperties;

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		public CardSubjectBuilder(ContextValue<string> name, CardSetConfig cardSetConfig) {
			if (string.IsNullOrEmpty(name.Value)) {
				throw new ArgumentException("Card subject must have a name.");
			}

			this.Name = name;
			this.CardSetConfig = cardSetConfig;

			segments = new List<CardSegmentBuilder>();

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

			foreach (CardSegmentBuilder segmentBuilder in segments) {
				CardSegment? segment = segmentBuilder.Build(subject, out List<SharpParsingException> segmentErrors);
				errors.AddRange(segmentErrors);

				if (segment != null) {
					subject.AddSegment(segment);
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
	public class CardSegmentBuilder {

		public DocumentSpan Location { get; }

		public CardSubjectBuilder Subject { get; }
		public ContextValue<string> Heading { get; }
		public ContextValue<string> Note { get; }

		public List<CardFeatureBuilder> Features { get; }

		private readonly ContextValue<string>? details;

		private CardSegmentBuilder(DocumentSpan location, CardSubjectBuilder subject, ContextValue<string>? heading, ContextValue<string>? note, ContextValue<string>? details) {
			this.Location = location;
			this.Subject = subject;
			this.Heading = heading.HasValue ? new ContextValue<string>(heading.Value.Location, heading.Value.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");
			//this.Heading = !string.IsNullOrEmpty(heading.Value) ? new ContextValue<string>(heading.Location, heading.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");
			this.Note = note.HasValue ? new ContextValue<string>(note.Value.Location, note.Value.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");
			this.details = details;
			this.Features = new List<CardFeatureBuilder>();
		}

		public static CardSegmentBuilder Create(DocumentSpan location, CardSubjectBuilder subject, ContextValue<string>? heading, ContextValue<string>? note, ContextValue<string>? details) {
			return new CardSegmentBuilder(location, subject, heading, note, details);
		}

		public static CardSegmentBuilder Reopen(CardSegment segment) {
			throw new NotImplementedException(); // TODO Implement
		}

		/*
		private static IEnvironment GetBuildEnvironment(CardSubject subject, string title, string note) {
			return subject.Environment.AppendEnvironment(new Dictionary<EvaluationName, object> { { "title", title ?? "" }, { "note", note ?? "" } });
		}
		*/

		private CardSegment? BuildBase(CardSubject subject, List<SharpParsingException> errors) {
			foreach (Conditional<AbstractCardSegmentConfig> segment in subject.CardConfig.AllCardSegments.Where(s => s.Value != null && (s.Value is not DynamicCardSegmentConfig dynamic || !dynamic.AlwaysInclude))) {
				if (segment.Value.definitions.Count == 0 && segment.Condition.IsTrue) {
					return new CardSegment(Location, segment.Value, subject, Heading, Note, DefinitionEnvironment.Empty);
				}
				else {
					IVariableBox segmentVariables = CardSegmentEnvironments.GetVariables(segment.Value);

					DefinitionEnvironment? segmentDetails = SubjectValueParsing.ParseValues(details, segmentVariables, segment.Value.definitions, out List<SharpParsingException> segmentErrors);
					if (segmentDetails != null) {
						try {
							CardSegment builtSegment = new CardSegment(Location, segment.Value, subject, Heading, Note, segmentDetails);
							if (segment.Evaluate(builtSegment.Environment)) {
								errors.AddRange(segmentErrors);
								return builtSegment;
							}
						}
						catch (Exception) { // TODO Just EvaluationException?
							errors.AddRange(segmentErrors); // Something went wrong trying to build segment. This should be reported.
						}
					}
				}
			}

			errors.Add(new SharpParsingException(Location, "No matching segment configuration found."));
			return null;
		}

		public CardSegment? Build(CardSubject subject, out List<SharpParsingException> errors) {
			errors = new List<SharpParsingException>();

			CardSegment? segment = null;
			try {
				segment = BuildBase(subject, errors);
			}
			catch(EvaluationException e) {
				errors.Add(new SharpParsingException(Location, e.Message, e));
			}

			if(segment == null) {
				return null;
			}

			foreach (CardFeatureBuilder featureBuilder in Features) {
				CardFeature? feature;
				List<SharpParsingException> featureErrors;
				if (segment.SegmentConfig is DynamicCardSegmentConfig dynamicSegmentConfig) {
					feature = featureBuilder.BuildDynamic(segment, dynamicSegmentConfig, out featureErrors);
				}
				else {
					feature = featureBuilder.BuildSimple(segment, out featureErrors);
				}

				if (feature != null) {
					segment.AddFeature(feature);
				}
				else if (featureErrors.Count == 0) {
					errors.Add(new SharpParsingException(featureBuilder.Location, "Could not build feature."));
				}

				errors.AddRange(featureErrors);
			}

			return segment;
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

		public CardSegmentBuilder Segment { get; }
		public ContextValue<string> Title { get; }
		public ContextValue<string> Note { get; }

		public ContextValue<string> text;

		public bool IsMultiLine { get; set; }
		public bool IsListItem { get; }
		public int Index { get; }

		private readonly ContextValue<string> details;

		private CardFeatureBuilder(DocumentSpan location, CardSegmentBuilder segment, ContextValue<string>? title, ContextValue<string>? note, ContextValue<string>? details, ContextValue<string>? text, bool isListItem, int index) {
			this.Location = location;
			this.Segment = segment;
			this.Title = title.HasValue ? new ContextValue<string>(title.Value.Location, title.Value.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");
			this.Note = note.HasValue ? new ContextValue<string>(note.Value.Location, note.Value.Value ?? "") : new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");

			this.text = new ContextValue<string>(text?.Location ?? location, text?.Value ?? "");

			this.details = details ?? new ContextValue<string>(new DocumentSpan(location.Offset, location.Line, location.Column, 0), "");

			this.IsListItem = isListItem;
			this.Index = index;
		}

		public static CardFeatureBuilder Create(DocumentSpan location, CardSegmentBuilder segment, ContextValue<string>? title, ContextValue<string>? note, ContextValue<string>? details, ContextValue<string>? text, bool isListItem, int index) {
			return new CardFeatureBuilder(location, segment, title, note, details, text, isListItem, index);
		}

		public static CardFeatureBuilder Reopen(CardFeature feature) {
			throw new NotImplementedException(); // TODO Implement
		}

		/*
		private static IEnvironment GetBuildEnvironment(CardSegment segment, string title, string note, string text, bool isListItem) {
			return segment.Environment.AppendEnvironment(new Dictionary<EvaluationName, object> { { "title", title ?? "" }, { "note", note ?? "" }, { "text", text ?? "" }, { "listitem", isListItem } });
		}
		*/

		public CardFeature? BuildDynamic(CardSegment segment, DynamicCardSegmentConfig segmentConfig, out List<SharpParsingException> errors) {
			// Need to use RegexFormats to properly format text at some point
			// return formatting.Apply(new RichString(textStr)).Formatted;
			//new CardFeatureConcrete(Line, FeatureConfig, segment, Title, Note, )

			errors = new List<SharpParsingException>();

			foreach (Conditional<CardFeatureConfig> feature in segmentConfig.cardFeatures) {
				if (feature.Value.definitions.Count == 0 && feature.Condition.IsTrue) {
					IVariableBox textParseVariables = CardFeatureEnvironments.GetTextVariables(feature.Value);
					ContextValue<TextExpression> textExpr = ParseText(textParseVariables, out List<SharpParsingException> textErrors);
					errors.AddRange(textErrors);
					return new CardFeature(Location, feature.Value, segment, Title, Note, textExpr, DefinitionEnvironment.Empty, IsMultiLine, IsListItem, Index);
				}
				else {
					IVariableBox featureEnvironment1 = CardFeatureEnvironments.GetVariables(feature.Value);
					DefinitionEnvironment? featureDetails = SubjectValueParsing.ParseValues(details, featureEnvironment1, feature.Value.definitions, out List<SharpParsingException> featureErrors);
					if (featureDetails != null) {
						try {
							IVariableBox textParseVariables = CardFeatureEnvironments.GetTextVariables(feature.Value);
							ContextValue<TextExpression> textExpr = ParseText(textParseVariables, out List<SharpParsingException> textErrors);

							CardFeature builtFeature = new CardFeature(Location, feature.Value, segment, Title, Note, textExpr, featureDetails, IsMultiLine, IsListItem, Index);
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
			errors.Add(new UnknownFeatureConfigException(Location, segmentConfig));

			return null;
		}

		public CardFeature BuildSimple(CardSegment segment, out List<SharpParsingException> errors) {
			// Need to use RegexFormats to properly format text at some point
			// return formatting.Apply(new RichString(textStr)).Formatted;
			//new CardFeatureConcrete(Line, FeatureConfig, segment, Title, Note, )

			errors = new List<SharpParsingException>();

			// This is the case for Text and Table segments
			IVariableBox textParseVariables = CardFeatureEnvironments.GetTextVariables(segment);
			ContextValue<TextExpression> textExpr = ParseText(textParseVariables, out List<SharpParsingException> textErrors);
			errors.AddRange(textErrors);

			return new CardFeature(Location, null, segment, Title, Note, textExpr, DefinitionEnvironment.Empty, IsMultiLine, IsListItem, Index);
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