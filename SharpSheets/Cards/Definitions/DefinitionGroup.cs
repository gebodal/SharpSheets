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

		private readonly List<ValueDefinition> valueDefinitions;
		private readonly List<FunctionDefinition> functionDefinitions;
		private readonly Dictionary<EvaluationName, ValueDefinition> variableAliasLookup;
		private readonly Dictionary<EvaluationName, FunctionDefinition> functionAliasLookup;
		private readonly Dictionary<EvaluationName, EnvironmentVariableInfo> variableInfos;

		public int Count { get { return valueDefinitions.Count + functionDefinitions.Count + (fallback?.Count ?? 0); } }

		public DefinitionGroup(DefinitionGroup? fallback) {
			this.fallback = fallback;
			valueDefinitions = new List<ValueDefinition>();
			functionDefinitions = new List<FunctionDefinition>();
			variableAliasLookup = new Dictionary<EvaluationName, ValueDefinition>();
			functionAliasLookup = new Dictionary<EvaluationName, FunctionDefinition>();
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
			if(definition is ValueDefinition valueDefinition) {
				AddValue(valueDefinition);
			}
			else if(definition is FunctionDefinition functionDefinition) {
				AddFunction(functionDefinition);
			}
			else {
				throw new InvalidOperationException($"Unrecognized {nameof(Definition)} type: {definition.GetType().Name}");
			}
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException">A <see cref="ValueDefinition"/> with a matching name or alias already exists in the collection.</exception>
		private void AddValue(ValueDefinition definition) {
			List<EvaluationName> alreadyAdded = new List<EvaluationName>();
			foreach (EvaluationName alias in definition.AllNames) {
				if (ConflictingVariable(alias)) {
					alreadyAdded.Add(alias);
				}
			}
			if (alreadyAdded.Count > 0) {
				throw new InvalidOperationException("Definition names already registered: " + string.Join(", ", alreadyAdded)); // Better error?
			}

			valueDefinitions.Add(definition);

			variableAliasLookup.Add(definition.name, definition);
			variableInfos.Add(definition.name, new EnvironmentVariableInfo(definition.name, definition.Type.ReturnType, definition.description));
			foreach (EvaluationName alias in definition.aliases) {
				variableAliasLookup.Add(alias, definition);
				variableInfos.Add(alias, new EnvironmentVariableInfo(alias, definition.Type.ReturnType, definition.description));
			}
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException">A <see cref="FunctionDefinition"/> with a matching name already exists in the collection.</exception>
		private void AddFunction(FunctionDefinition definition) {
			List<EvaluationName> alreadyAdded = new List<EvaluationName>();
			foreach (EvaluationName alias in definition.AllNames) {
				if (ConflictingFunction(alias)) {
					alreadyAdded.Add(alias);
				}
			}
			if (alreadyAdded.Count > 0) {
				throw new InvalidOperationException("Definition names already registered: " + string.Join(", ", alreadyAdded)); // Better error?
			}

			functionDefinitions.Add(definition);

			functionAliasLookup.Add(definition.name, definition);
			foreach (EvaluationName alias in definition.aliases) {
				functionAliasLookup.Add(alias, definition);
			}
		}

		/// <summary>
		/// Return the <see cref="Definition"/> specified by the provided alias. Return <see langword="false"/> if no such <see cref="Definition"/> exists.
		/// </summary>
		/// <param name="key">Alias of the definition to find.</param>
		/// <param name="definition"></param>
		/// <returns></returns>
		public bool TryGetDefinition(EvaluationName key, [MaybeNullWhen(false)] out Definition definition) {
			if(variableAliasLookup.TryGetValue(key, out ValueDefinition? valueDefinition)) {
				definition = valueDefinition;
				return true;
			}
			else if (functionAliasLookup.TryGetValue(key, out FunctionDefinition? functionDefinition)) {
				definition = functionDefinition;
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

		private bool ConflictingVariable(EvaluationName alias) {
			if (variableAliasLookup.ContainsKey(alias)) {
				return true;
			}
			else if(fallback != null) {
				return fallback.ConflictingVariable(alias);
			}
			else {
				return false;
			}
		}

		private bool ConflictingFunction(EvaluationName name) {
			if (functionAliasLookup.ContainsKey(name)) {
				return true;
			}
			else if (fallback != null) {
				return fallback.ConflictingFunction(name);
			}
			else {
				return false;
			}
		}

		public bool Conflicting(Definition other) {
			if (other is ValueDefinition valueDefinition) {
				if (ConflictingVariable(valueDefinition.name)) {
					return true;
				}

				foreach (EvaluationName alias in valueDefinition.aliases) {
					if (ConflictingVariable(alias)) {
						return true;
					}
				}

				return false;
			}
			else if(other is FunctionDefinition functionDefinition) {
				if (ConflictingFunction(functionDefinition.name)) {
					return true;
				}

				foreach (EvaluationName alias in functionDefinition.aliases) {
					if (ConflictingFunction(alias)) {
						return true;
					}
				}

				return false;
			}
			else {
				throw new InvalidOperationException($"Unrecognized {nameof(Definition)} type: {other.GetType().Name}");
			}
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
			if (variableAliasLookup.TryGetValue(key, out ValueDefinition? definition)) {
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

		public IEnumerable<EnvironmentVariableInfo> GetVariables() {
			return variableInfos.Values.ConcatOrNothing(fallback?.GetVariables()).DistinctBy(i => i.Name);
		}

		public bool TryGetFunctionDefinition(EvaluationName name, [MaybeNullWhen(false)] out FunctionDefinition function) {
			if (functionAliasLookup.TryGetValue(name, out FunctionDefinition? func)) {
				function = func;
				return true;
			}
			else if (fallback != null && fallback.TryGetFunctionDefinition(name, out function)) {
				return true;
			}
			else {
				function = null;
				return false;
			}
		}

		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
			if(TryGetFunctionDefinition(name, out FunctionDefinition? function)) {
				functionInfo = function;
				return true;
			}
			else {
				functionInfo = null;
				return false;
			}
		}

		public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() {
			return functionAliasLookup.Values.DistinctBy(f => f.name);
		}

		public IEnumerator<Definition> GetEnumerator() {
			if (fallback != null) {
				return fallback.Concat(valueDefinitions, functionDefinitions).GetEnumerator();
			}
			else {
				return valueDefinitions.Concat<Definition>(functionDefinitions).GetEnumerator();
			}
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
	}
}
