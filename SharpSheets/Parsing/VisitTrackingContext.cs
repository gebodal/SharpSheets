using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Parsing {

	public class VisitTrackingContext : IContext {

		private readonly IContext original;
		private readonly List<VisitTrackingContext> children;
		private readonly Dictionary<string, VisitTrackingContext> namedChildren;

		private readonly VisitTrackingContext? parent;
		public IContext? Parent => parent ?? original.Parent;
		public IEnumerable<IContext> Children => children;
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren => namedChildren.Select(kv => new KeyValuePair<string, IContext>(kv.Key, kv.Value));

		public string SimpleName => original.SimpleName;
		public string DetailedName => original.DetailedName;
		public string FullName => original.FullName;

		public DocumentSpan Location => original.Location;
		public int Depth => original.Depth;

		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		private Dictionary<int, HashSet<int>> propertyVisits; // Key: line of property, Value: Set of lines it was called from
		private Dictionary<int, HashSet<int>> flagVisits; // Key: line of flag, Value: Set of lines it was called from
		private HashSet<int> entryVisits;
		private HashSet<int> definitionVisits;
		private Dictionary<int, HashSet<int>> namedChildVisits; // Key: line of named child, Value: Set of lines it was called from

		public VisitTrackingContext(VisitTrackingContext? parent, IContext original) {
			this.original = original;
			this.parent = parent;

			this.children = new List<VisitTrackingContext>();
			foreach(IContext originalChild in original.Children) {
				this.children.Add(new VisitTrackingContext(this, originalChild));
			}

			this.namedChildren = new Dictionary<string, VisitTrackingContext>();
			foreach((string childName, IContext originalNamedChild) in original.NamedChildren) {
				namedChildren[childName] = new VisitTrackingContext(this, originalNamedChild);
			}

			RefreshVisited();
		}

		public VisitTrackingContext(IContext original) : this(null, original) { }

		public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) {
			foreach (ContextProperty<string> localProperty in original.GetLocalProperties(origin)) {
				VisitProperty(localProperty.Location, origin);
				yield return localProperty;
			}
		}

		public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) {
			foreach (ContextProperty<bool> localFlag in original.GetLocalFlags(origin)) {
				VisitFlag(localFlag.Location, origin);
				yield return localFlag;
			}
		}

		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) {
			VisitEntries(origin);
			return original.GetEntries(origin);
		}

		public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) {
			VisitDefinitions(origin);
			return original.GetDefinitions(origin);
		}

		public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
			if(original.TryGetLocalProperty(key, origin, isLocalRequest, out property, out location)) {
				if (location.HasValue) { VisitProperty(location.Value, origin); }
				return true;
			}
			else {
				property = null;
				location = null;
				return false;
			}
		}

		public bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location) {
			if (original.TryGetLocalFlag(key, origin, isLocalRequest, out flag, out location)) {
				if (location.HasValue) { VisitFlag(location.Value, origin); }
				return true;
			}
			else {
				flag = false;
				location = null;
				return false;
			}
		}

		public bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild) {
			if (original.TryGetLocalNamedChild(name, origin, isLocalRequest, out _) && namedChildren.TryGetValue(name, out VisitTrackingContext? trackingNamedChild)) {
				namedChild = trackingNamedChild;
				VisitNamedChild(trackingNamedChild, origin);
				return true;
			}
			else {
				namedChild = null;
				return false;
			}
		}


		[MemberNotNull(nameof(propertyVisits), nameof(flagVisits), nameof(entryVisits), nameof(definitionVisits), nameof(namedChildVisits))]
		public void RefreshVisited() {
			propertyVisits = new Dictionary<int, HashSet<int>>();
			flagVisits = new Dictionary<int, HashSet<int>>();
			entryVisits = new HashSet<int>();
			definitionVisits = new HashSet<int>();
			namedChildVisits = new Dictionary<int, HashSet<int>>();

			foreach (VisitTrackingContext child in children) {
				child.RefreshVisited();
			}
			foreach (VisitTrackingContext namedChild in namedChildren.Values) {
				namedChild.RefreshVisited();
			}
		}

		private void VisitProperty(DocumentSpan propertyLocation, IContext? origin) {
			if (origin != null) {
				if (!propertyVisits.ContainsKey(propertyLocation.Line)) { propertyVisits[propertyLocation.Line] = new HashSet<int>(); }
				propertyVisits[propertyLocation.Line].Add(origin.Location.Line);
			}
		}
		private void VisitFlag(DocumentSpan flagLocation, IContext? origin) {
			if (origin != null) {
				if (!flagVisits.ContainsKey(flagLocation.Line)) { flagVisits[flagLocation.Line] = new HashSet<int>(); }
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
		private void VisitNamedChild(VisitTrackingContext namedChild, IContext? origin) {
			if (origin != null) {
				if (!namedChildVisits.ContainsKey(namedChild.Location.Line)) { namedChildVisits[namedChild.Location.Line] = new HashSet<int>(); }
				namedChildVisits[namedChild.Location.Line].Add(origin.Location.Line);
			}
		}

		public HashSet<int> GetUsedLines() {
			HashSet<int> used = new HashSet<int>();

			used.UnionWith(propertyVisits.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key));
			used.UnionWith(flagVisits.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key));

			if (entryVisits.Count > 0) { used.UnionWith(original.GetEntries(null).Select(e => e.Location.Line)); }
			if (definitionVisits.Count > 0) { used.UnionWith(original.GetDefinitions(null).Select(e => e.Location.Line)); }

			foreach (VisitTrackingContext child in children) {
				used.UnionWith(child.GetUsedLines());
			}
			foreach (VisitTrackingContext namedChild in namedChildren.Values) {
				used.UnionWith(namedChild.GetUsedLines());
			}

			if(used.Count > 0) {
				used.Add(Location.Line);
			}

			return used;
		}

		// Line owners: Key: line number, Value: all lines which reference that line
		private void CalculateLineOwnership(LineOwnership lineOwners) {
			foreach ((int propertyLine, HashSet<int> propertyVisits) in propertyVisits) {
				lineOwners.Add(propertyLine, propertyVisits);
			}
			foreach ((int flagLine, HashSet<int> flagVisits) in flagVisits) {
				lineOwners.Add(flagLine, flagVisits);
			}

			if (entryVisits.Count > 0) {
				foreach (ContextValue<string> entry in original.GetEntries(null)) {
					lineOwners.Add(entry.Location.Line, entryVisits);
				}
			}
			if (definitionVisits.Count > 0) {
				foreach (ContextValue<string> definition in original.GetDefinitions(null)) {
					lineOwners.Add(definition.Location.Line, definitionVisits);
				}
			}

			foreach ((int namedChildLine, HashSet<int> namedChildVisits) in namedChildVisits) {
				lineOwners.Add(namedChildLine, namedChildVisits);
			}

			foreach (VisitTrackingContext child in children) {
				child.CalculateLineOwnership(lineOwners);
			}
			foreach (VisitTrackingContext namedChild in namedChildren.Values) {
				namedChild.CalculateLineOwnership(lineOwners);
			}
		}
		public IReadOnlyLineOwnership CalculateLineOwnership() {
			LineOwnership lineOwners = new LineOwnership();
			CalculateLineOwnership(lineOwners);
			return lineOwners;
		}

	}

}
