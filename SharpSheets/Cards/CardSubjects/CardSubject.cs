using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Definitions;
using SharpSheets.Evaluations;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpSheets.Cards.CardSubjects {

	public class CardSubjectDocument : IDocumentEntity, IEnumerable<CardSubjectSet> {
		public string SimpleName { get; } = "CardSubjectCollection";
		public string DetailedName => SimpleName;
		public string FullName => SimpleName;

		public DocumentSpan Location { get; } = DocumentSpan.Imaginary;
		public int Depth { get; } = -1;

		public IDocumentEntity? Parent { get; } = null;

		public IEnumerable<IDocumentEntity> Children { get { return subjectSets; } }

		private readonly List<CardSubjectSet> subjectSets;
		private readonly List<ContextValue<CardSetConfig>> configurations;

		public int Count { get { return subjectSets.Count; } }
		public CardSubjectSet this[int subjectSetIdx] { get { return subjectSets[subjectSetIdx]; } }
		public int IndexOf(CardSubjectSet subjectSet) { return subjectSets.IndexOf(subjectSet); }

		public CardSubjectDocument() {
			subjectSets = new List<CardSubjectSet>();
			configurations = new List<ContextValue<CardSetConfig>>();
		}

		public void AddSubjectSet(CardSubjectSet subjectSet) {
			subjectSets.Add(subjectSet);
		}

		public void AddConfiguration(DocumentSpan location, CardSetConfig config) {
			configurations.Add(new ContextValue<CardSetConfig>(location, config));
		}

		public IEnumerable<CardSubject> AllSubjects() {
			return subjectSets.SelectMany(s => s);
		}

		public IEnumerable<ContextValue<CardSetConfig>> GetConfigurations() {
			return configurations;
		}

		public IEnumerator<CardSubjectSet> GetEnumerator() => ((IEnumerable<CardSubjectSet>)subjectSets).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)subjectSets).GetEnumerator();
	}

	public class CardSubjectSet : IDocumentEntity, IEnumerable<CardSubject> {
		public string SimpleName { get; } = "CardSubjectCollection";
		public string DetailedName => SimpleName;
		public string FullName => SimpleName;

		public DocumentSpan Location { get; }
		public int Depth { get; } = 0;

		public IDocumentEntity Parent { get; }

		public IEnumerable<IDocumentEntity> Children { get { return subjects; } }

		private readonly List<CardSubject> subjects;

		public int Count { get { return subjects.Count; } }
		public CardSubject this[int subjectIdx] { get { return subjects[subjectIdx]; } }
		public int IndexOf(CardSubject subject) { return subjects.IndexOf(subject); }

		public CardSubjectSet(CardSubjectDocument parent, DocumentSpan location) {
			this.Parent = parent;
			this.Location = location;
			subjects = new List<CardSubject>();
		}

		public void AddSubject(CardSubject subject) {
			subjects.Add(subject);
		}

		public IEnumerator<CardSubject> GetEnumerator() => ((IEnumerable<CardSubject>)subjects).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)subjects).GetEnumerator();
	}

	public interface ICardDocumentEntity : IDocumentEntity {
		DefinitionGroup Definitions { get; }
		DefinitionEnvironment Properties { get; }
		IEnvironment Environment { get; }
	}

	[System.Diagnostics.DebuggerDisplay("# {Name} (@{Location.Line})")]
	public class CardSubject : ICardDocumentEntity, IEnumerable<CardSegment> {

		string IDocumentEntity.SimpleName { get { return Name.Value; } }
		string IDocumentEntity.DetailedName { get { return $"<{SubjectSet.IndexOf(this)}>{(this as IDocumentEntity).SimpleName}"; } }
		string IDocumentEntity.FullName { get { return $"{(SubjectSet as IDocumentEntity).FullName}.{(this as IDocumentEntity).DetailedName}"; } }
		IDocumentEntity IDocumentEntity.Parent { get { return SubjectSet; } }
		IEnumerable<IDocumentEntity> IDocumentEntity.Children { get { return segments; } }
		public DocumentSpan Location { get { return Name.Location; } }
		public int Depth => 1;

		public CardSubjectSet SubjectSet { get; }

		public CardConfig CardConfig { get; }
		public ContextValue<string> Name { get; }

		public int Count { get { return segments.Count; } }
		public CardSegment this[int segmentIdx] { get { return segments[segmentIdx]; } }
		public int IndexOf(CardSegment segment) { return segments.IndexOf(segment); }

		private readonly List<CardSegment> segments;

		public DefinitionGroup SubjectDefinitions { get; }
		public DefinitionEnvironment Properties { get; }
		public IEnvironment Environment { get; }

		DefinitionGroup ICardDocumentEntity.Definitions => SubjectDefinitions;

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		public CardSubject(CardSubjectSet subjectSet, ContextValue<string> name, CardConfig cardConfig, DefinitionEnvironment subjectProperties) {
			if (string.IsNullOrEmpty(name.Value)) {
				throw new ArgumentException("CardSubject must have a name.");
			}

			this.SubjectSet = subjectSet;

			this.Name = name;
			this.CardConfig = cardConfig;

			this.segments = new List<CardSegment>();

			this.Properties = subjectProperties;

			this.SubjectDefinitions = new DefinitionGroup(CardSubjectEnvironments.BaseDefinitions, this.Properties);
			this.Environment = CardSubjectEnvironments.MakeBaseEnvironment(this.Name).AppendEnvironment(this.Properties);
		}

		public void AddSegment(CardSegment segment) {
			segments.Add(segment);
		}

		public CardSubject WithOrigin(CardSubjectSet subjectSet, DocumentSpan location) {
			CardSubject newSubject = new CardSubject(subjectSet, new ContextValue<string>(location, Name.Value), CardConfig, Properties);
			foreach (CardSegment segment in segments) {
				newSubject.AddSegment(segment.WithOrigin(DocumentSpan.Imaginary, newSubject));
			}
			return newSubject;
		}

		/*
		public IEnumerable<ContextProperty<object>> GetProperties() {
			return subjectProperties?.GetDocumentProperties() ?? Enumerable.Empty<ContextProperty<object>>();
		}
		*/

		/*
		public bool IsVariable(string key) => subjectEnvironment.IsVariable(key);
		public EvaluationType GetReturnType(string key) => subjectEnvironment.GetReturnType(key);
		public bool TryGetNode(string key, out EvaluationNode node) => subjectEnvironment.TryGetNode(key, out node);
		public bool TryGetFunctionInfo(string name, out EnvironmentFunctionInfo functionInfo) => subjectEnvironment.TryGetFunctionInfo(name, out functionInfo);
		public EnvironmentFunction GetFunction(string name) => subjectEnvironment.GetFunction(name);
		public IEnumerable<string> GetVariables() => subjectEnvironment.GetVariables();
		public object this[string key] => subjectEnvironment[key];
		*/

		public IEnumerator<CardSegment> GetEnumerator() {
			return segments.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		/*
		public static IEnvironment GetDryRun(CardConfig cardConfig) {
			return new DryRunEnvironment(cardConfig.AppendVariables(new Dictionary<EvaluationName, EvaluationType> { { "name", EvaluationType.STRING } }));
		}
		*/

	}

	[System.Diagnostics.DebuggerDisplay("## {Heading} (@{Location.Line})")]
	public class CardSegment : ICardDocumentEntity, IEnumerable<CardFeature> {

		string IDocumentEntity.SimpleName { get { return Heading.Value; } }
		string IDocumentEntity.DetailedName { get { return $"<{Subject.IndexOf(this)}>{(this as IDocumentEntity).SimpleName}"; } }
		string IDocumentEntity.FullName { get { return $"{(Subject as IDocumentEntity).FullName}.{(this as IDocumentEntity).DetailedName}"; } }
		IDocumentEntity IDocumentEntity.Parent { get { return Subject; } }
		IEnumerable<IDocumentEntity> IDocumentEntity.Children { get { return features; } }
		public DocumentSpan Location { get; }
		public int Depth => 2;

		public AbstractCardSegmentConfig SegmentConfig { get; }
		public CardSubject Subject { get; }
		public ContextValue<string> Heading { get; }
		public ContextValue<string> Note { get; }

		public int Count { get { return features.Count; } }
		public CardFeature this[int featureIdx] { get { return features[featureIdx]; } }
		public int IndexOf(CardFeature feature) { return features.IndexOf(feature); }

		private readonly List<CardFeature> features;

		public DefinitionGroup SegmentDefinitions { get; }
		public DefinitionEnvironment Details { get; }
		public IEnvironment Environment { get; }

		DefinitionGroup ICardDocumentEntity.Definitions => SegmentDefinitions;
		DefinitionEnvironment ICardDocumentEntity.Properties => Details;

		public CardSegment(DocumentSpan location, AbstractCardSegmentConfig segmentConfig, CardSubject subject, ContextValue<string> heading, ContextValue<string> note, DefinitionEnvironment details) {
			this.Location = location;
			this.SegmentConfig = segmentConfig;
			this.Subject = subject;
			this.Heading = new ContextValue<string>(heading.Location, heading.Value ?? "");
			this.Note = new ContextValue<string>(note.Location, note.Value ?? "");

			this.features = new List<CardFeature>();

			this.Details = details;

			this.SegmentDefinitions = new DefinitionGroup(CardSegmentEnvironments.BaseDefinitions, this.Details);
			this.Environment = Environments.Concat(
				subject.Environment,
				CardSegmentEnvironments.MakeBaseEnvironment(this),
				this.Details
				);
		}

		public void AddFeature(CardFeature feature) {
			features.Add(feature);
		}

		public CardSegment WithOrigin(DocumentSpan location, CardSubject newSubject) {
			CardSegment newSegment = new CardSegment(location, SegmentConfig, newSubject, new ContextValue<string>(location, Heading.Value), new ContextValue<string>(location, Note.Value), Details);
			foreach (CardFeature feature in features) {
				newSegment.AddFeature(feature.WithOrigin(DocumentSpan.Imaginary, newSegment));
			}
			return newSegment;
		}

		public IEnumerator<CardFeature> GetEnumerator() {
			return features.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

	}

	[System.Diagnostics.DebuggerDisplay("### {Title} (@{Location.Line})")]
	public class CardFeature : ICardDocumentEntity {

		string IDocumentEntity.SimpleName { get { return Title.Value; } }
		string IDocumentEntity.DetailedName { get { return $"<{Segment.IndexOf(this)}>{(this as IDocumentEntity).SimpleName}"; } }
		string IDocumentEntity.FullName { get { return $"{(Segment as IDocumentEntity).FullName}.{(this as IDocumentEntity).DetailedName}"; } }
		IDocumentEntity IDocumentEntity.Parent { get { return Segment; } }
		IEnumerable<IDocumentEntity> IDocumentEntity.Children { get { return Enumerable.Empty<IDocumentEntity>(); } }
		public DocumentSpan Location { get; }
		public int Depth => 3;

		public CardFeatureConfig? FeatureConfig { get; }
		public CardSegment Segment { get; }
		public ContextValue<string> Title { get; } // TextExpression?
		public ContextValue<string> Note { get; } // TextExpression?
		public ContextValue<TextExpression> Text { get; }
		public bool IsMultiLine { get; }
		public bool IsListItem { get; }
		public int Index { get; }

		public RegexFormats RegexFormats => FeatureConfig?.RegexFormats ?? Segment.SegmentConfig.regexFormats;

		public DefinitionEnvironment TextEnvironment { get; }

		public DefinitionGroup FeatureDefinitions { get; }
		public DefinitionEnvironment Details { get; }
		public IEnvironment Environment { get; }

		DefinitionGroup ICardDocumentEntity.Definitions => FeatureDefinitions;
		DefinitionEnvironment ICardDocumentEntity.Properties => Details;

		public CardFeature(DocumentSpan location, CardFeatureConfig? featureConfig, CardSegment segment, ContextValue<string> title, ContextValue<string> note, ContextValue<TextExpression> text, DefinitionEnvironment details, bool isMultiLine, bool isListItem, int index) {
			this.Location = location;
			this.FeatureConfig = featureConfig;
			this.Segment = segment;
			this.Title = new ContextValue<string>(title.Location, title.Value ?? "");
			this.Note = new ContextValue<string>(note.Location, note.Value ?? "");
			this.Text = new ContextValue<TextExpression>(text.Location, text.Value ?? new TextExpression(""));
			this.IsMultiLine = isMultiLine;
			this.IsListItem = isListItem;
			this.Index = index;

			this.Details = details;

			this.FeatureDefinitions = new DefinitionGroup(CardFeatureEnvironments.BaseDefinitions, this.Details);
			this.Environment = Environments.Concat(
				segment.Environment,
				CardFeatureEnvironments.MakeBaseEnvironment(this),
				this.Details
				);

			this.TextEnvironment = CardFeatureEnvironments.GetTextEnvironment(this);
		}

		public CardFeature WithOrigin(DocumentSpan location, CardSegment newSegment) {
			CardFeature newFeature = new CardFeature(location, FeatureConfig, newSegment, new ContextValue<string>(location, Title.Value), new ContextValue<string>(location, Note.Value), new ContextValue<TextExpression>(location, Text.Value), Details, IsMultiLine, IsListItem, Index);
			return newFeature;
		}

		/*
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
					RichString evalText = this.text.Evaluate(environment);
					if (formats != null) {
						evalText = formats.Apply(evalText);
					}
					return evalText.Formatted;
				}
				else {
					return "";
				}
			}

			public override IEnumerable<EvaluationName> GetVariables() {
				return text.GetVariables();
			}

		}

		public static IEnvironment GetDryRun(CardFeatureConfig featureConfig) {
			return new DryRunEnvironment(
				featureConfig.AppendVariables(
					new Dictionary<EvaluationName, EvaluationType> {
						{ "title", EvaluationType.STRING },
						{ "note", EvaluationType.STRING },
						{ "text", EvaluationType.STRING }
					}
					)
				);
		}
		*/

	}
}