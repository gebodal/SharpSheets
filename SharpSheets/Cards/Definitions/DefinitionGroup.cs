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
		private readonly Dictionary<EvaluationName, EnvironmentVariableInfo> variableInfos;

		public int Count { get { return definitions.Count + (fallback?.Count ?? 0); } }

		public DefinitionGroup(DefinitionGroup? fallback) {
			this.fallback = fallback;
			definitions = new List<Definition>();
			aliasLookup = new Dictionary<EvaluationName, Definition>();
			variableInfos = new Dictionary<EvaluationName, EnvironmentVariableInfo>();
		}

		public DefinitionGroup() : this(null) { }

		/// <summary></summary>
		/// <exception cref="InvalidOperationException">Duplicate name or alias encountered in <paramref name="definitions"/>.</exception>
		public DefinitionGroup(IEnumerable<Definition> definitions, params IEnumerable<Definition>[] other) : this(null) {
			foreach (Definition definition in definitions.Concat(other.SelectMany(ds => ds))) {
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
			variableInfos.Add(definition.name, new EnvironmentVariableInfo(definition.name, definition.Type.ReturnType, definition.description));
			foreach (EvaluationName alias in definition.aliases) {
				aliasLookup.Add(alias, definition);
				variableInfos.Add(alias, new EnvironmentVariableInfo(alias, definition.Type.ReturnType, definition.description));
			}
		}

		/// <summary>
		/// Return the <see cref="Definition"/> specified by the provided alias. Return <see langword="false"/> if no such <see cref="Definition"/> exists.
		/// </summary>
		/// <param name="key">Alias of the definition to find.</param>
		/// <param name="definition"></param>
		/// <returns></returns>
		public bool TryGetDefinition(EvaluationName key, [MaybeNullWhen(false)] out Definition definition) {
			if(aliasLookup.TryGetValue(key, out definition)) {
				return true;
			}
			else if (fallback != null) {
				return fallback.TryGetDefinition(key, out definition);
			}
			else {
				definition = null;
				return false;
			}
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

		public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
			if (variableInfos.TryGetValue(key, out EnvironmentVariableInfo? info)) {
				variableInfo = info;
				return true;
			}
			else if (fallback != null && fallback.TryGetVariableInfo(key, out variableInfo)) {
				return true;
			}
			else {
				variableInfo = null;
				return false;
			}
		}

		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
			if (aliasLookup.TryGetValue(key, out Definition? definition)) {
				if (definition is CalculatedDefinition calculated) {
					node = calculated.Evaluation;
					return true;
				}
				// TODO What to do about fallback definitions?
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

		public IEnumerable<EnvironmentVariableInfo> GetVariables() => variableInfos.Values.ConcatOrNothing(fallback?.GetVariables()).Distinct();

		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
			// This will need updating if we end up implementing user defined functions
			functionInfo = null;
			return false;
		}
		public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() {
			// This will need updating if we end up implementing user defined functions
			return Enumerable.Empty<IEnvironmentFunctionInfo>().ConcatOrNothing(fallback?.GetFunctionInfos());
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
