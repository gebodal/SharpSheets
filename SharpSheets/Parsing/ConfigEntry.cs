using SharpSheets.Utilities;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpSheets.Parsing {

	public class ConfigEntry : IContext {

		public string SimpleName { get { return type; } }
		public DocumentSpan Location { get; }
		public int Depth { get { return parent != null ? parent.Depth + 1 : 0; } }

		public IContext? Parent { get { return parent; } }
		public IEnumerable<IContext> Children { get { return children; } }
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren { get { return namedChildren.Select(kv => new KeyValuePair<string, IContext>(kv.Key, kv.Value.Child)); } }

		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		public string DetailedName { get { return (parent != null ? $"<{parent.children.IndexOf(this)}>" : "") + $"{SimpleName}({Depth})"; } }
		public string FullName { get { return (parent != null ? parent.FullName + "." : "") + DetailedName; } }

		//public readonly int indent;
		//public int Indent { get { return Location.Column; } }
		private readonly string type;

		// TODO Child/Content indent, int? so that it can be unassigned

		private readonly Dictionary<string, ConfigProperty<string>> properties;
		private readonly Dictionary<string, ConfigValue<bool>> flags;
		private readonly List<ContextValue<string>> entries;
		private readonly List<ContextValue<string>> definitions;
		private readonly HashSet<int> overwrittenLines;

		private readonly ConfigEntry? parent;
		private readonly List<ConfigEntry> children;
		private readonly Dictionary<string, ConfigNamedChild> namedChildren;

		public ConfigEntry(ConfigEntry? parent, DocumentSpan location, string type) {
			this.parent = parent;
			this.Location = location;
			this.type = type;

			children = new List<ConfigEntry>();
			namedChildren = new Dictionary<string, ConfigNamedChild>(SharpDocuments.StringComparer);
			properties = new Dictionary<string, ConfigProperty<string>>(SharpDocuments.StringComparer);
			flags = new Dictionary<string, ConfigValue<bool>>(SharpDocuments.StringComparer);
			entries = new List<ContextValue<string>>();
			definitions = new List<ContextValue<string>>();
			overwrittenLines = new HashSet<int>();
		}

		public void SetProperty(DocumentSpan location, string name, DocumentSpan valueLocation, string value, bool localOnly) {
			if (properties.TryGetValue(name, out ConfigProperty<string> property) && property.LocalOnly == localOnly) {
				overwrittenLines.Add(property.Location.Line);
			}

			this.properties[name] = new ConfigProperty<string>(location, name, valueLocation, value, localOnly);
		}

		public void AddFlag(DocumentSpan location, string flag, bool localOnly) {
			if (flags.TryGetValue(flag, out ConfigValue<bool> flagValue) && flagValue.LocalOnly == localOnly) {
				overwrittenLines.Add(flagValue.Location.Line);
			}

			bool value;
			if (flag.StartsWith("!")) {
				value = false;
				flag = flag.Substring(1);
			}
			else {
				value = true;
			}

			this.flags[flag] = new ConfigValue<bool>(location, value, localOnly);
		}

		public void AddEntry(DocumentSpan location, string entry) {
			this.entries.Add(new ContextValue<string>(location, entry));
		}

		public void AddDefinition(DocumentSpan location, string definition) {
			this.definitions.Add(new ContextValue<string>(location, definition));
		}

		public virtual void AddChild(ConfigEntry child) {
			children.Add(child);
		}

		public virtual void AddNamedChild(ConfigEntry child, bool localOnly) {
			namedChildren[child.type] = new ConfigNamedChild(child, localOnly);
		}

		public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
			if (properties.TryGetValue(key, out ConfigProperty<string> value) && (isLocalRequest || !value.LocalOnly)) {
				property = value.Value;
				location = value.Location;
				return true;
			}
			else {
				property = null;
				location = null;
				return false;
			}
		}

		public bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location) {
			if (flags.TryGetValue(key, out ConfigValue<bool> value) && (isLocalRequest || !value.LocalOnly)) {
				flag = value.Value;
				location = value.Location;
				return true;
			}
			else {
				flag = false;
				location = null;
				return false;
			}
		}

		public bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild) {
			if (namedChildren.TryGetValue(name, out ConfigNamedChild value) && (isLocalRequest || !value.LocalOnly)) {
				namedChild = value.Child;
				return true;
			}
			else {
				namedChild = null;
				return false;
			}
		}

		public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) {
			foreach(ConfigProperty<string> property in properties.Values) {
				yield return new ContextProperty<string>(property.Location, property.Name, property.ValueLocation, property.Value);
			}
		}

		public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) {
			foreach (KeyValuePair<string, ConfigValue<bool>> flagEntry in flags) {
				yield return new ContextProperty<bool>(flagEntry.Value.Location, flagEntry.Key, flagEntry.Value.Location, flagEntry.Value.Value);
			}
		}

		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) {
			return entries;
		}

		public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) {
			return definitions;
		}

		private readonly struct ConfigValue<T> {
			public DocumentSpan Location { get; }
			public T Value { get; }
			public bool LocalOnly { get; }
			public ConfigValue(DocumentSpan location, T value, bool localOnly) {
				this.Location = location;
				this.Value = value;
				this.LocalOnly = localOnly;
			}
		}

		private readonly struct ConfigProperty<T> {
			public DocumentSpan Location { get; }
			public string Name { get; }
			public DocumentSpan ValueLocation { get; }
			public T Value { get; }
			public bool LocalOnly { get; }
			public ConfigProperty(DocumentSpan location, string name, DocumentSpan valueLocation, T value, bool localOnly) {
				this.Location = location;
				this.Name = name;
				this.ValueLocation = valueLocation;
				this.Value = value;
				this.LocalOnly = localOnly;
			}
		}

		private readonly struct ConfigNamedChild {
			public ConfigEntry Child { get; }
			public bool LocalOnly { get; }
			public ConfigNamedChild(ConfigEntry child, bool localOnly) {
				this.Child = child;
				this.LocalOnly = localOnly;
			}
		}

		public override string ToString() {
			return FullName;
		}

	}

}
