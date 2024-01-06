using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Cards.Definitions {

	/*
	public interface IDefinitionGroup : IEnumerable<Definition> {
		bool IsDefinition(EvaluationName key);
		Definition? GetDefinition(EvaluationName key);
	}

	public class DefinitionGroupImmutable : IDefinitionGroup, IVariableBox {

		private readonly List<Definition> definitions;
		private readonly Dictionary<EvaluationName, Definition> aliasLookup;
		private readonly Dictionary<EvaluationName, EvaluationType> returnTypes;

		public int Count { get { return definitions.Count; } }

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		public DefinitionGroupImmutable(IEnumerable<Definition> definitions) {
			this.definitions = new List<Definition>();
			this.aliasLookup = new Dictionary<EvaluationName, Definition>();
			this.returnTypes = new Dictionary<EvaluationName, EvaluationType>();

			foreach (Definition definition in definitions) {
				Add(definition);
			}
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		private void Add(Definition definition) {
			List<EvaluationName> alreadyAdded = new List<EvaluationName>();
			foreach (EvaluationName alias in definition.AllNames) {
				if (aliasLookup.ContainsKey(alias)) {
					alreadyAdded.Add(alias);
				}
			}
			if (alreadyAdded.Count > 0) {
				throw new ArgumentException("Definition names already registered: " + string.Join(", ", alreadyAdded)); // Better error?
			}

			definitions.Add(definition);

			aliasLookup.Add(definition.name, definition);
			returnTypes.Add(definition.name, definition.Type.ReturnType);
			foreach (EvaluationName alias in definition.aliases) {
				aliasLookup.Add(alias, definition);
				returnTypes.Add(alias, definition.Type.ReturnType);
			}
		}

		public Definition? GetDefinition(EvaluationName key) {
			return aliasLookup.GetValueOrFallback(key, null); // TODO Should this be more strict, and throw an exception when unrecognised key provided?
		}

		public bool IsDefinition(EvaluationName key) {
			return aliasLookup.ContainsKey(key);
		}

		public bool IsVariable(EvaluationName key) {
			return aliasLookup.ContainsKey(key);
		}

		public EvaluationType GetReturnType(EvaluationName key) {
			if (returnTypes.TryGetValue(key, out EvaluationType? type)) {
				return type;
			}
			else {
				throw new UndefinedVariableException(key);
			}
		}

		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
			if (aliasLookup.TryGetValue(key, out Definition? definition)) {
				if (definition is CalculatedDefinition calculated) {
					node = calculated.Evaluation;
					return true;
				}
				//else if(definition is FallbackDefinition fallback) {
				//	node = fallback.Evaluation; // TODO Is this right?
				//	return true;
				//}
			}

			node = null;
			return false;
		}

		public IEnumerable<EvaluationName> GetVariables() => aliasLookup.Keys;

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes() {
			return returnTypes;
		}
		public IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> GetNodes() {
			foreach (Definition definition in definitions) {
				if (definition is CalculatedDefinition calculated) {
					foreach (KeyValuePair<EvaluationName, EvaluationNode> node in calculated.AllNames.ToDictionary(n => n, n => calculated.Evaluation)) {
						yield return node;
					}
				}
				// TODO What to do about fallback definitions?
				// else if(definition is FallbackDefinition fallback)
			}
		}

		public bool IsFunction(EvaluationName name) {
			// This will need updating if we end up implementing user defined functions
			return false;
		}
		public EnvironmentFunctionInfo GetFunctionInfo(EvaluationName name) {
			// This will need updating if we end up implementing user defined functions
			throw new UndefinedFunctionException(name);
		}
		public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
			// This will need updating if we end up implementing user defined functions
			return Enumerable.Empty<EnvironmentFunctionInfo>();
		}

		public IEnumerator<Definition> GetEnumerator() {
			return definitions.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
	}

	public class DefinitionGroupBuilder : IVariableBox, IEnumerable<Definition> {

		// TODO Need to implement some pattern so that we can have an immutable form of this grouping

		private readonly List<Definition> definitions;
		private readonly Dictionary<EvaluationName, Definition> aliasLookup;
		private readonly Dictionary<EvaluationName, EvaluationType> returnTypes;

		public int Count { get { return definitions.Count; } }

		public DefinitionGroupBuilder() {
			definitions = new List<Definition>();
			aliasLookup = new Dictionary<EvaluationName, Definition>();
			returnTypes = new Dictionary<EvaluationName, EvaluationType>();
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		public DefinitionGroupBuilder(IEnumerable<Definition> definitions) : this() {
			foreach (Definition definition in definitions) {
				Add(definition);
			}
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		public void Add(Definition definition) {
			List<EvaluationName> alreadyAdded = new List<EvaluationName>();
			foreach (EvaluationName alias in definition.AllNames) {
				if (aliasLookup.ContainsKey(alias)) {
					alreadyAdded.Add(alias);
				}
			}
			if (alreadyAdded.Count > 0) {
				throw new ArgumentException("Definition names already registered: " + string.Join(", ", alreadyAdded)); // Better error?
			}

			definitions.Add(definition);

			aliasLookup.Add(definition.name, definition);
			returnTypes.Add(definition.name, definition.Type.ReturnType);
			foreach (EvaluationName alias in definition.aliases) {
				aliasLookup.Add(alias, definition);
				returnTypes.Add(alias, definition.Type.ReturnType);
			}
		}

		public Definition? GetDefinition(EvaluationName key) {
			return aliasLookup.GetValueOrFallback(key, null); // TODO Should this be more strict, and throw an exception when unrecognised key provided?
		}

		public bool IsVariable(EvaluationName key) {
			return aliasLookup.ContainsKey(key);
		}

		public bool Conflicting(Definition other) {
			if (aliasLookup.ContainsKey(other.name)) {
				return true;
			}

			foreach (EvaluationName alias in other.aliases) {
				if (aliasLookup.ContainsKey(alias)) {
					return true;
				}
			}

			return false;
		}

		public EvaluationType GetReturnType(EvaluationName key) {
			if (returnTypes.TryGetValue(key, out EvaluationType? type)) {
				return type;
			}
			else {
				throw new UndefinedVariableException(key);
			}
		}

		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
			if (aliasLookup.TryGetValue(key, out Definition? definition)) {
				if (definition is CalculatedDefinition calculated) {
					node = calculated.Evaluation;
					return true;
				}
				//else if(definition is FallbackDefinition fallback) {
				//	node = fallback.Evaluation; // TODO Is this right?
				//	return true;
				//}
			}

			node = null;
			return false;
		}

		public IEnumerable<EvaluationName> GetVariables() => aliasLookup.Keys;

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes() {
			return returnTypes;
		}
		public IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> GetNodes() {
			foreach (Definition definition in definitions) {
				if (definition is CalculatedDefinition calculated) {
					foreach (KeyValuePair<EvaluationName, EvaluationNode> node in calculated.AllNames.ToDictionary(n => n, n => calculated.Evaluation)) {
						yield return node;
					}
				}
				// TODO What to do about fallback definitions?
				// else if(definition is FallbackDefinition fallback)
			}
		}

		public bool IsFunction(EvaluationName name) {
			// This will need updating if we end up implementing user defined functions
			return false;
		}
		public EnvironmentFunctionInfo GetFunctionInfo(EvaluationName name) {
			// This will need updating if we end up implementing user defined functions
			throw new UndefinedFunctionException(name);
		}
		public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
			// This will need updating if we end up implementing user defined functions
			return Enumerable.Empty<EnvironmentFunctionInfo>();
		}

		public IEnumerator<Definition> GetEnumerator() {
			return definitions.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
	}
	*/
}
