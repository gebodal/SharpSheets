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
	public class CardSubject : ICardDocumentEntity, IEnumerable<CardSection> {

		string IDocumentEntity.SimpleName { get { return Name.Value; } }
		string IDocumentEntity.DetailedName { get { return $"<{SubjectSet.IndexOf(this)}>{(this as IDocumentEntity).SimpleName}"; } }
		string IDocumentEntity.FullName { get { return $"{(SubjectSet as IDocumentEntity).FullName}.{(this as IDocumentEntity).DetailedName}"; } }
		IDocumentEntity IDocumentEntity.Parent { get { return SubjectSet; } }
		IEnumerable<IDocumentEntity> IDocumentEntity.Children { get { return sections; } }
		public DocumentSpan Location { get { return Name.Location; } }
		public int Depth => 1;

		public CardSubjectSet SubjectSet { get; }

		public CardConfig CardConfig { get; }
		public ContextValue<string> Name { get; }

		public int Count { get { return sections.Count; } }
		public CardSection this[int sectionIdx] { get { return sections[sectionIdx]; } }
		public int IndexOf(CardSection section) { return sections.IndexOf(section); }

		private readonly List<CardSection> sections;

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

			this.sections = new List<CardSection>();

			this.Properties = subjectProperties;

			this.SubjectDefinitions = new DefinitionGroup(CardSubjectEnvironments.BaseDefinitions, this.Properties);
			this.Environment = CardSubjectEnvironments.MakeBaseEnvironment(this.Name).AppendEnvironment(this.Properties);
		}

		public void AddSection(CardSection section) {
			sections.Add(section);
		}

		public CardSubject WithOrigin(CardSubjectSet subjectSet, DocumentSpan location) {
			CardSubject newSubject = new CardSubject(subjectSet, new ContextValue<string>(location, Name.Value), CardConfig, Properties);
			foreach (CardSection section in sections) {
				newSubject.AddSection(section.WithOrigin(DocumentSpan.Imaginary, newSubject));
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

		public IEnumerator<CardSection> GetEnumerator() {
			return sections.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		/*
		public static IEnvironment GetDryRun(CardConfig cardConfig) {
			return new DryRunEnvironment(cardConfig.AppendVariables(new Dictionary<EvaluationName, EvaluationType> { { "name", EvaluationType.STRING } }));
		}
		*/

		public string ToText() {
			StringBuilder result = new StringBuilder();
			result.Append($"# {Name.Value}");

			bool firstProperty = true;
			foreach (KeyValuePair<Definition, ContextProperty<object>> property in Properties.ContextProperties) {
				if (firstProperty) {
					firstProperty = false;
					result.Append("\n");
				}
				result.Append($"\n{property.Key.name}: {DefinitionType.ValueToString(property.Value.Value)}");
			}

			foreach (CardSection section in sections) {
				result.Append("\n\n");
				result.Append(section.ToText());
			}

			return result.ToString();
		}
	}

	[System.Diagnostics.DebuggerDisplay("## {Heading} (@{Location.Line})")]
	public class CardSection : ICardDocumentEntity, IEnumerable<CardFeature> {

		string IDocumentEntity.SimpleName { get { return Heading.Value; } }
		string IDocumentEntity.DetailedName { get { return $"<{Subject.IndexOf(this)}>{(this as IDocumentEntity).SimpleName}"; } }
		string IDocumentEntity.FullName { get { return $"{(Subject as IDocumentEntity).FullName}.{(this as IDocumentEntity).DetailedName}"; } }
		IDocumentEntity IDocumentEntity.Parent { get { return Subject; } }
		IEnumerable<IDocumentEntity> IDocumentEntity.Children { get { return features; } }
		public DocumentSpan Location { get; }
		public int Depth => 2;

		public AbstractCardSectionConfig SectionConfig { get; }
		public CardSubject Subject { get; }
		public ContextValue<string> Heading { get; }
		public ContextValue<string> Note { get; }

		public int Count { get { return features.Count; } }
		public CardFeature this[int featureIdx] { get { return features[featureIdx]; } }
		public int IndexOf(CardFeature feature) { return features.IndexOf(feature); }

		private readonly List<CardFeature> features;

		public DefinitionGroup SectionDefinitions { get; }
		public DefinitionEnvironment Details { get; }
		public IEnvironment Environment { get; }

		DefinitionGroup ICardDocumentEntity.Definitions => SectionDefinitions;
		DefinitionEnvironment ICardDocumentEntity.Properties => Details;

		public CardSection(DocumentSpan location, AbstractCardSectionConfig sectionConfig, CardSubject subject, ContextValue<string> heading, ContextValue<string> note, DefinitionEnvironment details) {
			this.Location = location;
			this.SectionConfig = sectionConfig;
			this.Subject = subject;
			this.Heading = new ContextValue<string>(heading.Location, heading.Value ?? "");
			this.Note = new ContextValue<string>(note.Location, note.Value ?? "");

			this.features = new List<CardFeature>();

			this.Details = details;

			this.SectionDefinitions = new DefinitionGroup(CardSectionEnvironments.BaseDefinitions, this.Details);
			this.Environment = Environments.Concat(
				subject.Environment,
				CardSectionEnvironments.MakeBaseEnvironment(this),
				this.Details
				);
		}

		public void AddFeature(CardFeature feature) {
			features.Add(feature);
		}

		public CardSection WithOrigin(DocumentSpan location, CardSubject newSubject) {
			CardSection newSection = new CardSection(location, SectionConfig, newSubject, new ContextValue<string>(location, Heading.Value), new ContextValue<string>(location, Note.Value), Details);
			foreach (CardFeature feature in features) {
				newSection.AddFeature(feature.WithOrigin(DocumentSpan.Imaginary, newSection));
			}
			return newSection;
		}

		/*
		public bool IsVariable(string key) => sectionEnvironment.IsVariable(key);
		public EvaluationType GetReturnType(string key) => sectionEnvironment.GetReturnType(key);
		public bool TryGetNode(string key, out EvaluationNode node) => sectionEnvironment.TryGetNode(key, out node);
		public bool TryGetFunctionInfo(string name, out EnvironmentFunctionInfo functionInfo) => sectionEnvironment.TryGetFunctionInfo(name, out functionInfo);
		public EnvironmentFunction GetFunction(string name) => sectionEnvironment.GetFunction(name);
		public IEnumerable<string> GetVariables() => sectionEnvironment.GetVariables();
		public object this[string key] => sectionEnvironment[key];
		*/

		public IEnumerator<CardFeature> GetEnumerator() {
			return features.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		/*
		public static IEnvironment GetDryRun(CardSectionConfig sectionConfig) {
			return new DryRunEnvironment(
				sectionConfig.AppendVariables(
					new Dictionary<EvaluationName, EvaluationType> { 
						{ "title", EvaluationType.STRING },
						{ "note", EvaluationType.STRING }
					}
					)
				);
		}
		*/

		public string ToText() {
			StringBuilder result = new StringBuilder();

			if (!string.IsNullOrEmpty(Heading.Value) || !string.IsNullOrEmpty(Note.Value) || Details.ContextProperties.Count > 0 || Subject.IndexOf(this) != 0) {
				result.Append($"##");
				if (!string.IsNullOrEmpty(Heading.Value)) {
					result.Append(" " + Heading.Value);
				}
				if (!string.IsNullOrEmpty(Note.Value)) {
					result.Append(" (" + Note.Value + ")");
				}
				if(Details.ContextProperties.Count > 0) {
					result.Append(" [");
					bool first = true;
					foreach(KeyValuePair<Definition, ContextProperty<object>> detail in Details.ContextProperties) {
						if (first) { first = false; }
						else { result.Append(" "); }

						if(detail.Value.Value is bool boolean) {
							result.Append((boolean ? "" : "!") + (detail.Key.AllNames.Select(n => n.ToString()).FirstOrDefault(n => !n.Contains(" ")) ?? detail.Key.name));
						}
						else {
							result.Append($"{detail.Key.name}: {DefinitionType.ValueToString(detail.Value.Value)}");
						}
					}
					result.Append("]");
				}
			}

			foreach(CardFeature feature in features) {
				if (result.Length > 0) {
					result.Append("\n\n");
				}
				result.Append(feature.ToText());
			}

			return result.ToString();
		}
	}

	[System.Diagnostics.DebuggerDisplay("### {Title} (@{Location.Line})")]
	public class CardFeature : ICardDocumentEntity {

		string IDocumentEntity.SimpleName { get { return Title.Value; } }
		string IDocumentEntity.DetailedName { get { return $"<{Section.IndexOf(this)}>{(this as IDocumentEntity).SimpleName}"; } }
		string IDocumentEntity.FullName { get { return $"{(Section as IDocumentEntity).FullName}.{(this as IDocumentEntity).DetailedName}"; } }
		IDocumentEntity IDocumentEntity.Parent { get { return Section; } }
		IEnumerable<IDocumentEntity> IDocumentEntity.Children { get { return Enumerable.Empty<IDocumentEntity>(); } }
		public DocumentSpan Location { get; }
		public int Depth => 3;

		public CardFeatureConfig? FeatureConfig { get; }
		public CardSection Section { get; }
		public ContextValue<string> Title { get; } // TextExpression?
		public ContextValue<string> Note { get; } // TextExpression?
		public ContextValue<TextExpression> Text { get; }
		public bool IsMultiLine { get; }
		public bool IsListItem { get; }
		public int Index { get; }

		public RegexFormats RegexFormats => FeatureConfig?.RegexFormats ?? Section.SectionConfig.regexFormats;

		public DefinitionEnvironment TextEnvironment { get; }

		public DefinitionGroup FeatureDefinitions { get; }
		public DefinitionEnvironment Details { get; }
		public IEnvironment Environment { get; }

		DefinitionGroup ICardDocumentEntity.Definitions => FeatureDefinitions;
		DefinitionEnvironment ICardDocumentEntity.Properties => Details;

		public CardFeature(DocumentSpan location, CardFeatureConfig? featureConfig, CardSection section, ContextValue<string> title, ContextValue<string> note, ContextValue<TextExpression> text, DefinitionEnvironment details, bool isMultiLine, bool isListItem, int index) {
			this.Location = location;
			this.FeatureConfig = featureConfig;
			this.Section = section;
			this.Title = new ContextValue<string>(title.Location, title.Value ?? "");
			this.Note = new ContextValue<string>(note.Location, note.Value ?? "");
			this.Text = new ContextValue<TextExpression>(text.Location, text.Value ?? new TextExpression(""));
			this.IsMultiLine = isMultiLine;
			this.IsListItem = isListItem;
			this.Index = index;

			this.Details = details;

			this.FeatureDefinitions = new DefinitionGroup(CardFeatureEnvironments.BaseDefinitions, this.Details);
			this.Environment = Environments.Concat(
				section.Environment,
				CardFeatureEnvironments.MakeBaseEnvironment(this),
				this.Details
				);

			this.TextEnvironment = CardFeatureEnvironments.GetTextEnvironment(this);
		}

		public CardFeature WithOrigin(DocumentSpan location, CardSection newSection) {
			CardFeature newFeature = new CardFeature(location, FeatureConfig, newSection, new ContextValue<string>(location, Title.Value), new ContextValue<string>(location, Note.Value), new ContextValue<TextExpression>(location, Text.Value), Details, IsMultiLine, IsListItem, Index);
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

		public string ToText() {
			StringBuilder result = new StringBuilder();

			if (!string.IsNullOrEmpty(Title.Value) || !string.IsNullOrEmpty(Note.Value) || Details.ContextProperties.Count > 0) {
				if (IsMultiLine) { result.Append($"###"); }
				if (!string.IsNullOrEmpty(Title.Value)) {
					//result.Append(" " + Title);
					result.Append(Title.Value);
				}
				if (!string.IsNullOrEmpty(Note.Value)) {
					result.Append(" (" + Note.Value + ")");
				}
				if (Details.ContextProperties.Count > 0) {
					result.Append(" [");
					bool first = true;
					foreach (KeyValuePair<Definition, ContextProperty<object>> detail in Details.ContextProperties) {
						if (first) { first = false; }
						else { result.Append(", "); }

						if (detail.Value.Value is bool boolean) {
							result.Append((boolean ? "" : "!") + (detail.Key.AllNames.Select(n => n.ToString()).FirstOrDefault(n => !n.Contains(" ")) ?? detail.Key.name));
						}
						else {
							result.Append($"{detail.Key.name}: {DefinitionType.ValueToString(detail.Value.Value)}");
						}
					}
					result.Append("]");
				}

				if (IsMultiLine) { result.Append("\n"); }
				else { result.Append(": "); }
			}

			if (IsListItem) { result.Append("+ "); }

			result.Append(Text.Value.ToString());

			return result.ToString().TrimStart();
		}
	}
}