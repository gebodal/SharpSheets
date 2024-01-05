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
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren { get { return namedChildren.Select(kv => new KeyValuePair<string, IContext>(kv.Key, kv.Value)); } }

		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		public string DetailedName { get { return (parent != null ? $"<{parent.children.IndexOf(this)}>" : "") + $"{SimpleName}({Depth})"; } }
		public string FullName { get { return (parent != null ? parent.FullName + "." : "") + DetailedName; } }

		//public readonly int indent;
		//public int Indent { get { return Location.Column; } }
		private readonly string type;
		private readonly List<Regex> namedChildrenTypes;

		// TODO Child/Content indent, int? so that it can be unassigned

		private readonly Dictionary<string, ConfigProperty<string>> properties;
		private readonly Dictionary<string, ConfigValue<bool>> flags;
		private readonly List<ContextValue<string>> entries;
		private readonly List<ContextValue<string>> definitions;
		private readonly HashSet<int> overwrittenLines;

		private readonly ConfigEntry? parent;
		private readonly List<ConfigEntry> children;
		private readonly Dictionary<string, ConfigEntry> namedChildren;

		private Dictionary<int, HashSet<int>> propertyVisits; // Key: line of property, Value: Set of lines it was called from
		private Dictionary<int, HashSet<int>> flagVisits; // Key: line of flag, Value: Set of lines it was called from
		private HashSet<int> entryVisits;
		private HashSet<int> definitionVisits;

		private Dictionary<int, HashSet<int>> AllVisits { get { return propertyVisits.Concat(flagVisits).ToDictionary(); } }

		public ConfigEntry(ConfigEntry? parent, DocumentSpan location, string type, IEnumerable<Regex> namedChildrenTypes) {
			this.parent = parent;
			this.Location = location;
			this.type = type;
			this.namedChildrenTypes = new List<Regex>(namedChildrenTypes);

			children = new List<ConfigEntry>();
			namedChildren = new Dictionary<string, ConfigEntry>(SharpDocuments.StringComparer);
			properties = new Dictionary<string, ConfigProperty<string>>(SharpDocuments.StringComparer);
			flags = new Dictionary<string, ConfigValue<bool>>(SharpDocuments.StringComparer);
			entries = new List<ContextValue<string>>();
			definitions = new List<ContextValue<string>>();
			overwrittenLines = new HashSet<int>();

			RefreshVisited();
		}

		[MemberNotNull(nameof(propertyVisits), nameof(flagVisits), nameof(entryVisits), nameof(definitionVisits))]
		public void RefreshVisited() {
			//Console.WriteLine(new string(' ', Math.Max(0,indent*4)) + $"Refresh Visited: {type}");
			propertyVisits = properties.ToDictionary(kv => kv.Value.Location.Line, kv => new HashSet<int>());
			flagVisits = flags.ToDictionary(kv => kv.Value.Location.Line, kv => new HashSet<int>());
			entryVisits = new HashSet<int>();
			definitionVisits = new HashSet<int>();

			foreach (ConfigEntry child in children) {
				child.RefreshVisited();
			}
			foreach (ConfigEntry namedChild in namedChildren.Values) {
				namedChild.RefreshVisited();
			}
		}

		private void VisitProperty(DocumentSpan propertyLocation, IContext? origin) {
			if (origin != null) {
				propertyVisits[propertyLocation.Line].Add(origin.Location.Line);
			}
		}
		private void VisitFlag(DocumentSpan flagLocation, IContext? origin) {
			if (origin != null) {
				flagVisits[flagLocation.Line].Add(origin.Location.Line);
			}
		}
		private void VisitEntries(IContext? origin) {
			if (origin != null) {
				entryVisits.Add(origin.Location.Line);
			}
		}
		private void VisitDefinitions(IContext? origin) {
			if (origin != null) {
				definitionVisits.Add(origin.Location.Line);
			}
		}

		/*
		public HashSet<int> GetUnusedLines() {
			HashSet<int> unused = new HashSet<int>(overwrittenLines);
			unused.UnionWith(propertyVisits.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key));
			unused.UnionWith(flagVisits.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key));
			if (entryVisits.Count == 0) { unused.UnionWith(entries.Select(e => e.Location.Line)); }
			if (definitionVisits.Count == 0) { unused.UnionWith(definitions.Select(e => e.Location.Line)); }

			foreach (ConfigEntry child in children) {
				unused.UnionWith(child.GetUnusedLines());
			}
			foreach (ConfigEntry namedChild in namedChildren.Values) {
				unused.UnionWith(namedChild.GetUnusedLines());
			}

			return unused;
		}
		*/
		public HashSet<int> GetUsedLines() {
			HashSet<int> used = new HashSet<int>() { Location.Line };
			used.UnionWith(propertyVisits.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key));
			used.UnionWith(flagVisits.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key));
			if (entryVisits.Count > 0) { used.UnionWith(entries.Select(e => e.Location.Line)); }
			if (definitionVisits.Count > 0) { used.UnionWith(definitions.Select(e => e.Location.Line)); }

			foreach (ConfigEntry child in children) {
				used.UnionWith(child.GetUsedLines());
			}
			foreach (ConfigEntry namedChild in namedChildren.Values) {
				used.UnionWith(namedChild.GetUsedLines());
			}

			return used;
		}

		// Line owners: Key: line number, Value: all lines which reference that line
		private void CalculateLineOwnership(Dictionary<int, HashSet<int>> lineOwners) {
			foreach (KeyValuePair<int, HashSet<int>> entry in AllVisits) {
				lineOwners[entry.Key] = entry.Value; // Should this be checking if there is already a set present...?
			}
			if (entryVisits.Count > 0) {
				foreach (ContextValue<string> entry in entries) {
					lineOwners[entry.Location.Line] = entryVisits; // Should this be checking if there is already a set present...?
				}
			}
			if (definitionVisits.Count > 0) {
				foreach (ContextValue<string> definition in definitions) {
					lineOwners[definition.Location.Line] = definitionVisits; // Should this be checking if there is already a set present...?
				}
			}

			foreach (ConfigEntry child in children) {
				child.CalculateLineOwnership(lineOwners);
			}
			foreach (ConfigEntry namedChild in namedChildren.Values) {
				namedChild.CalculateLineOwnership(lineOwners);
			}
		}
		public Dictionary<int, HashSet<int>> CalculateLineOwnership() {
			Dictionary<int, HashSet<int>> lineOwners = new Dictionary<int, HashSet<int>>();
			CalculateLineOwnership(lineOwners);
			return lineOwners;
		}

		public void SetProperty(DocumentSpan location, string name, DocumentSpan valueLocation, string value) {
			bool localOnly = false;
			if (name.StartsWith("@")) {
				name = name.Substring(1);
				localOnly = true;
			}

			if (properties.TryGetValue(name, out ConfigProperty<string> property) && property.LocalOnly == localOnly) {
				overwrittenLines.Add(property.Location.Line);
			}

			this.properties[name] = new ConfigProperty<string>(location, name, valueLocation, value, localOnly);
		}

		public void AddFlag(DocumentSpan location, string flag) {
			bool localOnly = false;
			if (flag.StartsWith("@")) {
				flag = flag.Substring(1);
				localOnly = true;
			}

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
			if (namedChildrenTypes.Any(r => r.IsMatch(child.type))) {
				//namedChildren.Add(child.type, child);
				namedChildren[child.type] = child;
			}
			else {
				children.Add(child);
			}
		}

		public IContext? GetNamedChild(string name) {
			return namedChildren.GetValueOrFallback(name, null);
		}

		private string? GetPropertyRecurse(string key, IContext? origin, string? defaultValue, out DocumentSpan? location, bool forceLocal, bool firstCall) {
			if (properties.TryGetValue(key, out ConfigProperty<string> value) && (firstCall || !value.LocalOnly)) {
				VisitProperty(value.Location, origin);
				location = value.Location;
				return value.Value;
			}
			else if (!forceLocal && parent != null) {
				return parent.GetPropertyRecurse(key, origin, defaultValue, out location, forceLocal, false);
			}
			else {
				location = null;
				return defaultValue;
			}
		}
		public string? GetProperty(string key, bool local, IContext? origin, string? defaultValue, out DocumentSpan? location) {
			return GetPropertyRecurse(key, origin, defaultValue, out location, local, true);
		}

		private bool HasPropertyRecurse(string key, IContext? origin, out DocumentSpan? location, bool forceLocal, bool firstCall) {
			bool hasProperty = properties.TryGetValue(key, out ConfigProperty<string> value);
			if (hasProperty && (firstCall || !value.LocalOnly)) {
				VisitProperty(value.Location, origin);
				location = value.Location;
			}
			else if (!forceLocal && parent != null) {
				hasProperty = parent.HasPropertyRecurse(key, origin, out location, forceLocal, false);
			}
			else {
				location = null;
			}
			return hasProperty;
		}
		public bool HasProperty(string key, bool local, IContext? origin, out DocumentSpan? location) {
			return HasPropertyRecurse(key, origin, out location, local, true);
		}

		private bool GetFlagRecurse(string flag, IContext? origin, out DocumentSpan? location, bool forceLocal, bool firstCall) {
			if (flags.TryGetValue(flag, out ConfigValue<bool> value) && (firstCall || !value.LocalOnly)) {
				VisitFlag(value.Location, origin);
				location = value.Location;
				return value.Value;
			}
			else if (!forceLocal && parent != null) {
				return parent.GetFlag(flag, false, origin, out location);
			}
			else {
				location = null;
				return false;
			}
		}
		public bool GetFlag(string flag, bool local, IContext? origin, out DocumentSpan? location) {
			return GetFlagRecurse(flag, origin, out location, local, true);
		}

		private bool HasFlagRecurse(string flag, IContext? origin, out DocumentSpan? location, bool forceLocal, bool firstCall) {
			bool hasFlag = flags.TryGetValue(flag, out ConfigValue<bool> value);
			if (hasFlag && (firstCall || !value.LocalOnly)) {
				VisitFlag(value.Location, origin);
				location = value.Location;
			}
			else if (!forceLocal && parent != null) {
				hasFlag = parent.HasFlagRecurse(flag, origin, out location, forceLocal, false);
			}
			else {
				location = null;
			}
			return hasFlag;
		}
		public bool HasFlag(string flag, bool local, IContext? origin, out DocumentSpan? location) {
			return HasFlagRecurse(flag, origin, out location, local, true);
		}

		public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) {
			foreach(ConfigProperty<string> property in properties.Values) {
				VisitProperty(property.Location, origin);
				yield return new ContextProperty<string>(property.Location, property.Name, property.ValueLocation, property.Value);
			}
		}

		public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) {
			foreach (KeyValuePair<string, ConfigValue<bool>> flagEntry in flags) {
				VisitFlag(flagEntry.Value.Location, origin);
				yield return new ContextProperty<bool>(flagEntry.Value.Location, flagEntry.Key, flagEntry.Value.Location, flagEntry.Value.Value);
			}
		}

		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) {
			VisitEntries(origin);
			return entries;
		}

		public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) {
			VisitDefinitions(origin);
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

		public override string ToString() {
			return FullName;
		}
	}

}
