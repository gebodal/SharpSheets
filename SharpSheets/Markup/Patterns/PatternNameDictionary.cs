using SharpSheets.Parsing;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Markup.Patterns {

	public readonly struct PatternName {
		public IReadOnlyList<string> Parts { get { return parts ?? Array.Empty<string>(); } }
		public int Length { get { return parts != null ? parts.Length : 0; } }
		private readonly string[] parts;

		private PatternName(string[] parts) {
			this.parts = parts;
		}

		public static PatternName Parse(string name) {
			string[] parts = name.Split('.');
			return new PatternName(parts);
		}

		public override string ToString() {
			return string.Join(".", Parts);
		}
	}

	public class PatternNameDictionary<T> { //: IDictionary<PatternName, T> {

		public T this[PatternName name] {
			get {
				return Get(name);
			}
			set {
				Set(name, value, true);
			}
		}

		public int Count => values.Count;

		public IEnumerable<T> Values => values.GetValues();
		public IEnumerable<PatternName> Keys(HashSet<string> reservedKeys) => values.GetKeys(null, reservedKeys).Select(k => PatternName.Parse(k));
		public IEnumerable<PatternName> MinimalKeys(HashSet<string> reservedKeys) => values.GetMinimalKeys(null, reservedKeys).Select(k => PatternName.Parse(k));
		public IEnumerable<PatternName> ValidKeys(HashSet<string> reservedKeys) => values.GetValidKeys(null, reservedKeys).Select(k => PatternName.Parse(k));

		public IEnumerable<string> Names(HashSet<string> reservedKeys) => values.GetKeys(null, reservedKeys);
		public IEnumerable<string> MinimalNames(HashSet<string> reservedKeys) => values.GetMinimalKeys(null, reservedKeys);
		public IEnumerable<string> ValidNames(HashSet<string> reservedKeys) => values.GetValidKeys(null, reservedKeys);

		private PartNode values;

		public PatternNameDictionary() {
			values = new PartNode(null, null);
		}

		public void Clear() {
			values = new PartNode(null, null); // Is this sufficient?
		}

		public T Get(PatternName name) {
			if (name.Length == 0) {
				throw new ArgumentException("Invalid pattern name.");
			}

			PartNode finalNode = values;
			for (int i = name.Length - 1; i >= 0; i--) {
				if (finalNode.children.TryGetValue(name.Parts[i], out PartNode? newFinal)) {
					finalNode = newFinal;
				}
				else {
					//throw new KeyNotFoundException(string.Join(".", name.Parts.Skip(i)) + " not found.");
					throw new KeyNotFoundException(name + " not found.");
				}
			}

			if (finalNode.leafValueSet) {
				return finalNode.leafValue!;
			}
			else if(finalNode.values.Count == 1) {
				return finalNode.values[0];
			}
			else if (finalNode.values.Count > 1) {
				throw new KeyNotFoundException(name + " is not a sufficiently specific name.");
			}
			else {
				throw new KeyNotFoundException(name + " not found.");
			}
		}

		public bool TryGetValue(PatternName name, [MaybeNullWhen(false)] out T value) {
			if (name.Length == 0) {
				throw new ArgumentException("Invalid pattern name.");
			}

			PartNode finalNode = values;
			for (int i = name.Length - 1; i >= 0; i--) {
				if (finalNode.children.TryGetValue(name.Parts[i], out PartNode? newFinal)) {
					finalNode = newFinal;
				}
				else {
					value = default;
					return false;
				}
			}

			if (finalNode.leafValueSet) {
				value = finalNode.leafValue!;
				return true;
			}
			else if (finalNode.values.Count == 1) {
				value = finalNode.values[0];
				return true;
			}
			else {
				value = default;
				return false;
			}
		}

		private void Set(PatternName name, T value, bool overwrite) {
			if (name.Length == 0) {
				throw new ArgumentException("Invalid pattern name.");
			}

			PartNode? finalNode = values;

			for (int i = name.Length - 1; i >= 0; i--) {
				if (finalNode.children.TryGetValue(name.Parts[i], out PartNode? newFinal)) {
					finalNode = newFinal;
				}
				else {
					PartNode newNode = new PartNode(finalNode, name.Parts[i]);
					finalNode.children.Add(name.Parts[i], newNode);
					finalNode = newNode;
				}
			}

			if (finalNode.leafValueSet && !overwrite) {
				throw new ArgumentException("A value for " + name + " already exists.");
			}

			finalNode.leafValue = value;
			finalNode.leafValueSet = true;

			while (finalNode != null) {
				finalNode.values.Add(value);
				finalNode = finalNode.parent;
			}
		}

		public void Add(PatternName name, T value) {
			Set(name, value, false);
		}

		public bool Remove(PatternName name) {
			if (name.Length == 0) {
				throw new ArgumentException("Invalid pattern name.");
			}

			PartNode? finalNode = values;
			for (int i = name.Length - 1; i >= 0; i--) {
				if (finalNode.children.TryGetValue(name.Parts[i], out PartNode? newFinal)) {
					finalNode = newFinal;
				}
				else {
					return false;
				}
			}

			if (finalNode.leafValueSet) {
				T value = finalNode.leafValue!;
				finalNode.leafValue = default;
				finalNode.leafValueSet = false;

				while (finalNode != null) {
					finalNode.values.Remove(value);
					finalNode = finalNode.parent;
				}

				return true;
			}
			else {
				return false;
			}
		}

		public bool ContainsKey(PatternName name) {
			if (name.Length == 0) {
				return false;
			}

			PartNode finalNode = values;
			for (int i = name.Length - 1; i >= 0; i--) {
				if (finalNode.children.TryGetValue(name.Parts[i], out PartNode? newFinal)) {
					finalNode = newFinal;
				}
				else {
					return false;
				}
			}

			return finalNode.values.Count == 1;
		}

		private class PartNode {

			public readonly PartNode? parent;
			public readonly string? key;
			public readonly Dictionary<string, PartNode> children;
			public readonly List<T> values;
			public T? leafValue;
			public bool leafValueSet = false;

			public bool HasValues => values.Count > 0;
			public int Count { get { return (leafValueSet ? 1 : 0) + children.Values.Select(c => c.Count).Sum(); } }

			public PartNode(PartNode? parent, string? key) {
				this.parent = parent;
				this.key = key;
				this.children = new Dictionary<string, PartNode>(SharpDocuments.StringComparer);
				this.values = new List<T>();
			}

			private string? GetNodeKey(string? suffixKey) {
				if (key == null && suffixKey == null) {
					return null;
				}
				else if (key == null) {
					return suffixKey;
				}
				else if (suffixKey == null) {
					return key;
				}
				else {
					return key + "." + suffixKey;
				}
			}

			public IEnumerable<T> GetValues() {
				if (leafValueSet) {
					yield return leafValue!;
				}

				foreach (PartNode child in children.Values) {
					foreach (T value in child.GetValues()) {
						yield return value;
					}
				}
			}

			public IEnumerable<string> GetKeys(string? suffixKey, HashSet<string> reservedKeys) {
				string? nodeKey = GetNodeKey(suffixKey);

				if (leafValueSet && nodeKey is not null && !reservedKeys.Contains(nodeKey)) {
					//yield return key;
					yield return nodeKey;
				}

				foreach (PartNode child in children.Values) {
					foreach (string childKey in child.GetKeys(nodeKey, reservedKeys)) {
						//yield return childKey + (key != null ? "." + key : "");
						yield return childKey;
					}
				}
			}

			public IEnumerable<string> GetMinimalKeys(string? suffixKey, HashSet<string> reservedKeys) {
				string? nodeKey = GetNodeKey(suffixKey);

				if (values.Count == 1 && nodeKey != null && !reservedKeys.Contains(nodeKey)) {
					//yield return key;
					yield return nodeKey;
				}
				else {
					foreach (PartNode child in children.Values) {
						foreach (string childKey in child.GetMinimalKeys(nodeKey, reservedKeys)) {
							//yield return childKey + (key != null ? "." + key : "");
							yield return childKey;
						}
					}
				}
			}

			public IEnumerable<string> GetValidKeys(string? suffixKey, HashSet<string> reservedKeys) {
				string? nodeKey = GetNodeKey(suffixKey);

				if (values.Count == 1 && nodeKey != null && !reservedKeys.Contains(nodeKey)) {
					//yield return key;
					yield return nodeKey;
				}

				foreach (PartNode child in children.Values) {
					foreach (string childKey in child.GetValidKeys(nodeKey, reservedKeys)) {
						//yield return childKey + (key != null ? "." + key : "");
						yield return childKey;
					}
				}
			}
		}

	}

}
