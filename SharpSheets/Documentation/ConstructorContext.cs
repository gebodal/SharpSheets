using System.Collections.Generic;
using System.Linq;
using SharpSheets.Shapes;
using System.Collections;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Documentation {

	public class BuilderContext : IContext {

		public IContext? Parent { get; } = null;
		public IEnumerable<IContext> Children => Enumerable.Empty<IContext>();
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren => Enumerable.Empty<KeyValuePair<string, IContext>>();

		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		public string SimpleName => "";
		public string DetailedName => "";
		public string FullName => "";

		public DocumentSpan Location => DocumentSpan.Imaginary;
		public int Depth => 0;

		public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) => Enumerable.Empty<ContextValue<string>>();

		private readonly Dictionary<string, string> properties;
		private readonly Dictionary<string, bool> flags;
		private readonly List<string> entries;

		public BuilderContext(BuilderDetails builder, IDictionary<string, object> propertyValues) {
			properties = new Dictionary<string, string>(SharpDocuments.StringComparer);
			flags = new Dictionary<string, bool>();
			entries = new List<string>();

			foreach(KeyValuePair<string, object> propertyEntry in propertyValues) {
				properties[propertyEntry.Key] = ValueSerialization.ToString(propertyEntry.Value);
			}

			foreach (ArgumentDetails arg in builder.Arguments) {
				if (arg.Type.IsEntried) { // Use DisplayType here to be sure...
					if (arg.ExampleValue is IEnumerable exampleEnumerable) {
						entries = exampleEnumerable.Cast<object>().Select(v => ValueSerialization.ToString(v)).ToList();
					}
				}
				else if (arg.Type.IsNumbered) {
					if (arg.ExampleValue is INumbered exampleNumbered) {
						foreach (KeyValuePair<int, object?> entry in exampleNumbered) {
							properties[arg.Name + (entry.Key + 1).ToString()] = ValueSerialization.ToString(entry.Value);
						}
					}
				}
				else if (arg.Type.DisplayType.IsBool) {
					if (arg.ExampleValue is bool exampleBool) {
						flags[arg.Name] = exampleBool;
					}
				}
				else if (arg.Implied != null) {
					//Console.WriteLine("Implied: " + arg.Implied);
					if (typeof(IShape).IsAssignableFrom(arg.Type.DisplayType.SystemType) && arg.ExampleValue != null) {
						properties[arg.Name + "." + arg.Implied] = arg.ExampleValue.GetType().Name;
					}
				}
				else if (arg.ExampleValue != null) {
					properties[arg.Name] = ValueSerialization.ToString(arg.ExampleValue);
				}
			}

			if (!properties.ContainsKey("name")) {
				properties["name"] = "NAME"; // Good idea?
			}
		}

		public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) {
			return properties
				.Select(kv => new ContextProperty<string>(DocumentSpan.Imaginary, kv.Key, DocumentSpan.Imaginary, kv.Value));
		}
		
		public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) {
			return flags
				.Select(kv => new ContextProperty<bool>(DocumentSpan.Imaginary, kv.Key, DocumentSpan.Imaginary, kv.Value));
		}

		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) {
			return entries.Select(e => new ContextValue<string>(DocumentSpan.Imaginary, e));
		}

		public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
			if (properties.TryGetValue(key, out string? value)) {
				property = value;
				location = DocumentSpan.Imaginary;
				return true;
			}
			else {
				property = null;
				location = null;
				return false;
			}
		}

		public bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location) {
			if (flags.TryGetValue(key, out bool value)) {
				flag = value;
				location = DocumentSpan.Imaginary;
				return true;
			}
			else {
				flag = false;
				location = null;
				return false;
			}
		}

		public bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild) {
			namedChild = null;
			return false;
		}
	}

}