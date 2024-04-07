using System.Collections.Generic;
using System.Linq;
using SharpSheets.Shapes;
using System.Collections;
using SharpSheets.Utilities;
using SharpSheets.Parsing;

namespace SharpSheets.Documentation {

	public class ConstructorContext : IContext {

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

		public IContext? GetNamedChild(string name, bool local, IContext? origin) => null;

		private readonly Dictionary<string, string> properties;
		private readonly Dictionary<string, bool> flags;
		private readonly List<string> entries;

		public ConstructorContext(ConstructorDetails constructor, IDictionary<string, object> propertyValues) {
			properties = new Dictionary<string, string>(SharpDocuments.StringComparer);
			flags = new Dictionary<string, bool>();
			entries = new List<string>();

			foreach(KeyValuePair<string, object> propertyEntry in propertyValues) {
				properties[propertyEntry.Key] = ValueParsing.ToString(propertyEntry.Value);
			}

			foreach (ArgumentDetails arg in constructor.Arguments) {
				if (arg.Type.DisplayType.TryGetGenericTypeDefinition() == typeof(List<>)) { // Use DisplayType here to be sure...
					if (arg.ExampleValue is IEnumerable exampleEnumerable) {
						entries = exampleEnumerable.Cast<object>().Select(v => ValueParsing.ToString(v)).ToList();
					}
				}
				else if (arg.Type.DisplayType.TryGetGenericTypeDefinition() == typeof(Numbered<>)) {
					if (arg.ExampleValue is INumbered exampleNumbered) {
						foreach (KeyValuePair<int, object?> entry in exampleNumbered) {
							properties[arg.Name + (entry.Key + 1).ToString()] = ValueParsing.ToString(entry.Value);
						}
					}
				}
				else if (arg.Type.DisplayType == typeof(bool)) {
					if (arg.ExampleValue is bool exampleBool) {
						flags[arg.Name] = exampleBool;
					}
				}
				else if (arg.Implied != null) {
					//Console.WriteLine("Implied: " + arg.Implied);
					if (typeof(IShape).IsAssignableFrom(arg.Type.DisplayType) && arg.ExampleValue != null) {
						properties[arg.Name + "." + arg.Implied] = arg.ExampleValue.GetType().Name;
					}
				}
				else if (arg.ExampleValue != null) {
					properties[arg.Name] = ValueParsing.ToString(arg.ExampleValue);
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

		public bool GetFlag(string flag, bool local, IContext? origin, out DocumentSpan? location) {
			if (flags.TryGetValue(flag, out bool value)) {
				location = DocumentSpan.Imaginary;
				return value;
			}
			else {
				location = null;
				return false;
			}
		}

		public string? GetProperty(string key, bool local, IContext? origin, string? defaultValue, out DocumentSpan? location) {
			if (properties.TryGetValue(key, out string? value) && value is not null) {
				location = DocumentSpan.Imaginary;
				return value;
			}
			else {
				location = null;
				return defaultValue;
			}
		}

		public bool HasFlag(string flag, bool local, IContext? origin, out DocumentSpan? location) {
			bool hasFlag = flags.ContainsKey(flag);
			location = hasFlag ? DocumentSpan.Imaginary : (DocumentSpan?)null;
			return hasFlag;
		}

		public bool HasProperty(string key, bool local, IContext? origin, out DocumentSpan? location) {
			bool hasProperty = properties.TryGetValue(key, out string? value) && value is not null;
			location = hasProperty ? DocumentSpan.Imaginary : (DocumentSpan?)null;
			return hasProperty;
		}

		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) {
			return entries.Select(e => new ContextValue<string>(DocumentSpan.Imaginary, e));
		}
	}

}