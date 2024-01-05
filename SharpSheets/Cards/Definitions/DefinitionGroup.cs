using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Cards.Definitions {

	public class DefinitionGroup : IVariableBox, IEnumerable<Definition> {

		// TODO Need to implement some pattern so that we can have an immutable form of this grouping

		private readonly DefinitionGroup? fallback;

		private readonly List<Definition> definitions;
		private readonly Dictionary<EvaluationName, Definition> aliasLookup;
		private readonly Dictionary<EvaluationName, EvaluationType> returnTypes;

		public int Count { get { return definitions.Count + (fallback?.Count ?? 0); } }

		public DefinitionGroup(DefinitionGroup? fallback) {
			this.fallback = fallback;
			definitions = new List<Definition>();
			aliasLookup = new Dictionary<EvaluationName, Definition>();
			returnTypes = new Dictionary<EvaluationName, EvaluationType>();
		}

		public DefinitionGroup() : this(null) { }

		/// <summary></summary>
		/// <exception cref="InvalidOperationException">Duplicate name or alias encountered in <paramref name="definitions"/>.</exception>
		public DefinitionGroup(IEnumerable<Definition> definitions) : this(null) {
			foreach (Definition definition in definitions) {
				Add(definition);
			}
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException">A <see cref="Definition"/> with a matching name or alias already exists in the collection.</exception>
		public void Add(Definition definition) {
			List<EvaluationName> alreadyAdded = new List<EvaluationName>();
			foreach (EvaluationName alias in definition.AllNames) {
				if (Conflicting(alias)) {
					alreadyAdded.Add(alias);
				}
			}
			if (alreadyAdded.Count > 0) {
				throw new InvalidOperationException("Definition names already registered: " + string.Join(", ", alreadyAdded)); // Better error?
			}

			definitions.Add(definition);

			aliasLookup.Add(definition.name, definition);
			returnTypes.Add(definition.name, definition.Type.ReturnType);
			foreach (EvaluationName alias in definition.aliases) {
				aliasLookup.Add(alias, definition);
				returnTypes.Add(alias, definition.Type.ReturnType);
			}
		}

		/// <summary>
		/// Return the <see cref="Definition"/> specified by the provided alias. Return <see langword="null"/> if no such <see cref="Definition"/> exists.
		/// </summary>
		/// <param name="key">Alias of the definition to find.</param>
		/// <returns></returns>
		public Definition? GetDefinition(EvaluationName key) {
			if(aliasLookup.TryGetValue(key, out Definition? definition)) {
				return definition;
			}
			else if (fallback != null) {
				return fallback.GetDefinition(key);
			}
			else {
				return null; // TODO Should this be more strict, and throw an exception when unrecognised key provided?
			}
		}

		public bool IsVariable(EvaluationName key) {
			return aliasLookup.ContainsKey(key) || (fallback != null && fallback.IsVariable(key));
		}

		private bool Conflicting(EvaluationName alias) {
			if (aliasLookup.ContainsKey(alias)) {
				return true;
			}
			else if(fallback != null) {
				return fallback.Conflicting(alias);
			}
			else {
				return false;
			}
		}

		public bool Conflicting(Definition other) {
			if (Conflicting(other.name)) {
				return true;
			}

			foreach (EvaluationName alias in other.aliases) {
				if (Conflicting(alias)) {
					return true;
				}
			}

			return false;
		}

		public EvaluationType GetReturnType(EvaluationName key) {
			if (returnTypes.TryGetValue(key, out EvaluationType? type)) {
				return type;
			}
			else if (fallback != null) {
				return fallback.GetReturnType(key);
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
			else if (fallback != null) {
				return fallback.TryGetNode(key, out node);
			}

			node = null;
			return false;
		}

		public IEnumerable<EvaluationName> GetVariables() => aliasLookup.Keys.ConcatOrNothing(fallback?.GetVariables()).Distinct();

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes() {
			return returnTypes.ConcatOrNothing(fallback?.GetReturnTypes()).Distinct();
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
			if (fallback != null) {
				foreach (KeyValuePair<EvaluationName, EvaluationNode> fallbackEntry in fallback.GetNodes()) {
					yield return fallbackEntry;
				}
			}
		}

		public bool IsFunction(EvaluationName name) {
			// This will need updating if we end up implementing user defined functions
			return false || (fallback != null && fallback.IsFunction(name));
		}
		public EnvironmentFunctionInfo GetFunctionInfo(EvaluationName name) {
			// This will need updating if we end up implementing user defined functions
			throw new UndefinedFunctionException(name);
		}
		public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
			// This will need updating if we end up implementing user defined functions
			return Enumerable.Empty<EnvironmentFunctionInfo>().ConcatOrNothing(fallback?.GetFunctionInfos());
		}

		public IEnumerator<Definition> GetEnumerator() {
			if (fallback != null) {
				return fallback.definitions.Concat(definitions).GetEnumerator();
			}
			else {
				return definitions.GetEnumerator();
			}
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
	}
}
