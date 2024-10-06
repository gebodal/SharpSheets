using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Definitions;
using SharpSheets.Evaluations;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Cards.CardSubjects {

	public static class CardSubjectWriter {

		private static string GetName(Definition definition) {
			if (definition.aliases.Length > 0) {
				return definition.aliases[0].ToString().ToTitleCase();
			}
			else {
				return definition.name.ToString().ToTitleCase();
			}
		}

		public static string ToText(this CardSubject subject) {
			StringBuilder result = new StringBuilder();
			result.Append($"# {subject.Name.Value}");

			bool firstProperty = true;
			foreach ((Definition definition, ContextProperty<object> property) in subject.Properties.ContextProperties) {
				if (firstProperty) {
					firstProperty = false;
					result.Append('\n');
				}
				result.Append($"\n{GetName(definition)}: {DefinitionType.ValueToString(property.Value)}");
			}

			foreach (CardSegment segment in subject) {
				result.Append("\n\n");
				result.Append(segment.ToText());
			}

			return result.ToString();
		}

		public static string GetExampleText(this CardConfig config) {
			string name = config.Name ?? "Name";
			List<ConstantDefinition> definitions = config.definitions.OfType<ConstantDefinition>().ToList();

			StringBuilder result = new StringBuilder();
			result.Append($"# {name}");

			bool firstProperty = true;
			foreach (ConstantDefinition definition in definitions) {
				object? exampleValue = null;
				if (definition.ExampleValue is not null) {
					exampleValue = definition.ExampleValue;
				}
				else if(definition.Type.ReturnType.DataType.IsValueType) {
					exampleValue = Activator.CreateInstance(definition.Type.ReturnType.DataType);
				}

				if (firstProperty) {
					firstProperty = false;
					result.Append('\n');
				}
				result.Append($"\n{GetName(definition)}: {(exampleValue is not null ? DefinitionType.ValueToString(exampleValue) : "")}");
			}

			return result.ToString();
		}

		public static string ToText(this CardSegment segment) {
			StringBuilder result = new StringBuilder();

			if (!string.IsNullOrEmpty(segment.Heading.Value) || !string.IsNullOrEmpty(segment.Note.Value) || segment.Details.ContextProperties.Count > 0 || segment.Subject.IndexOf(segment) != 0) {
				result.Append($"##");
				if (!string.IsNullOrEmpty(segment.Heading.Value)) {
					result.Append(" " + segment.Heading.Value);
				}
				if (!string.IsNullOrEmpty(segment.Note.Value)) {
					result.Append(" (" + segment.Note.Value + ")");
				}
				if (segment.Details.ContextProperties.Count > 0) {
					result.Append(" [");
					bool first = true;
					foreach ((Definition definition, ContextProperty<object> detail) in segment.Details.ContextProperties) {
						if (first) { first = false; }
						else { result.Append(' '); }

						if (detail.Value is bool boolean) {
							result.Append((boolean ? "" : "!") + (definition.AllNames.Select(n => n.ToString()).FirstOrDefault(n => !n.Contains(' ')) ?? definition.name));
						}
						else {
							result.Append($"{GetName(definition)}: {DefinitionType.ValueToString(detail.Value)}");
						}
					}
					result.Append(']');
				}
			}

			foreach (CardFeature feature in segment) {
				if (result.Length > 0) {
					result.Append("\n\n");
				}
				result.Append(feature.ToText());
			}

			return result.ToString();
		}

		public static string ToText(this CardFeature feature) {
			StringBuilder result = new StringBuilder();

			if (!string.IsNullOrEmpty(feature.Title.Value) || !string.IsNullOrEmpty(feature.Note.Value) || feature.Details.ContextProperties.Count > 0) {
				if (feature.IsMultiLine) { result.Append($"###"); }
				if (!string.IsNullOrEmpty(feature.Title.Value)) {
					//result.Append(" " + Title);
					result.Append(feature.Title.Value);
				}
				if (!string.IsNullOrEmpty(feature.Note.Value)) {
					result.Append(" (" + feature.Note.Value + ")");
				}
				if (feature.Details.ContextProperties.Count > 0) {
					result.Append(" [");
					bool first = true;
					foreach ((Definition definition, ContextProperty<object> detail) in feature.Details.ContextProperties) {
						if (first) { first = false; }
						else { result.Append(", "); }

						if (detail.Value is bool boolean) {
							result.Append((boolean ? "" : "!") + (definition.AllNames.Select(n => n.ToString()).FirstOrDefault(n => !n.Contains(' ')) ?? definition.name));
						}
						else {
							result.Append($"{GetName(definition)}: {DefinitionType.ValueToString(detail.Value)}");
						}
					}
					result.Append(']');
				}

				if (feature.IsMultiLine) { result.Append('\n'); }
				else { result.Append(": "); }
			}

			if (feature.IsListItem) { result.Append("+ "); }

			result.Append(feature.Text.Value.ToString());

			return result.ToString().TrimStart();
		}

	}

}
