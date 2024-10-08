using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Cards.Definitions {

	public class DefinitionEnvironment : IEnvironment, IEnumerable<Definition> {

		public static readonly DefinitionEnvironment Empty = new DefinitionEnvironment(new DefinitionGroup(), null, null, null, null);

		private readonly DefinitionGroup definitions;
		private readonly Dictionary<Definition, ContextValue<object>> contextValues;
		private readonly Dictionary<Definition, ContextValue<EvaluationNode>> contextNodes;
		private readonly Dictionary<Definition, ContextProperty<object>> propertyValues;
		private readonly Dictionary<Definition, ContextProperty<EvaluationNode>> propertyNodes;

		public IReadOnlyDictionary<Definition, ContextValue<object>> ContextValues => contextValues;
		public IReadOnlyDictionary<Definition, ContextProperty<object>> ContextProperties => propertyValues;

		public bool IsEmpty => definitions.Count == 0 && contextValues.Count == 0 && contextNodes.Count == 0 && propertyValues.Count == 0 && propertyNodes.Count == 0;

		private DefinitionEnvironment(
			DefinitionGroup definitions,
			Dictionary<Definition, ContextValue<object>>? contextValues,
			Dictionary<Definition, ContextValue<EvaluationNode>>? contextNodes,
			Dictionary<Definition, ContextProperty<object>>? propertyValues,
			Dictionary<Definition, ContextProperty<EvaluationNode>>? propertyNodes
			) {

			this.definitions = definitions;

			this.contextValues = contextValues ?? new Dictionary<Definition, ContextValue<object>>();
			this.contextNodes = contextNodes ?? new Dictionary<Definition, ContextValue<EvaluationNode>>();

			this.propertyValues = propertyValues ?? new Dictionary<Definition, ContextProperty<object>>();
			this.propertyNodes = propertyNodes ?? new Dictionary<Definition, ContextProperty<EvaluationNode>>();
		}

		public static DefinitionEnvironment Create(DefinitionGroup definitions, Dictionary<Definition, ContextProperty<object>> values, Dictionary<Definition, ContextProperty<EvaluationNode>> nodes) {
			return new DefinitionEnvironment(definitions, null, null, values, nodes);
		}

		public static DefinitionEnvironment Create(Dictionary<Definition, ContextProperty<object>> values) {
			return new DefinitionEnvironment(new DefinitionGroup(values.Keys), null, null, values, null);
		}

		public static DefinitionEnvironment Create(Dictionary<Definition, ContextProperty<object>> values, Dictionary<Definition, ContextProperty<EvaluationNode>> nodes) {
			Dictionary<Definition, ContextProperty<object>> finalvalues = values ?? new Dictionary<Definition, ContextProperty<object>>();
			Dictionary<Definition, ContextProperty<EvaluationNode>> finalnodes = nodes ?? new Dictionary<Definition, ContextProperty<EvaluationNode>>();

			DefinitionGroup definitions = new DefinitionGroup(finalvalues.Keys.Concat(finalnodes.Keys).Distinct());

			return new DefinitionEnvironment(definitions, null, null, finalvalues, finalnodes);
		}

		public static DefinitionEnvironment Create(Dictionary<Definition, ContextValue<object>> values) {
			return new DefinitionEnvironment(new DefinitionGroup(values.Keys), values, null, null, null);
		}

		public static DefinitionEnvironment Create(Dictionary<Definition, ContextValue<object>> values, Dictionary<Definition, ContextValue<EvaluationNode>> nodes) {
			Dictionary<Definition, ContextValue<object>> finalvalues = values ?? new Dictionary<Definition, ContextValue<object>>();
			Dictionary<Definition, ContextValue<EvaluationNode>> finalnodes = nodes ?? new Dictionary<Definition, ContextValue<EvaluationNode>>();

			DefinitionGroup definitions = new DefinitionGroup(finalvalues.Keys.Concat(finalnodes.Keys).Distinct());

			return new DefinitionEnvironment(definitions, finalvalues, finalnodes, null, null);
		}

		public IEnumerable<EnvironmentVariableInfo> GetVariables() {
			return definitions.GetVariables();
		}

		public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
			return definitions.TryGetVariableInfo(key, out variableInfo);
		}

		public bool TryGetValue(EvaluationName key, out object? value) {
			if (definitions.TryGetDefinition(key, out Definition? definition)) {
				if (contextValues.TryGetValue(definition, out ContextValue<object> context)) {
					value = context.Value;
					return true;
				}
				else if (propertyValues.TryGetValue(definition, out ContextProperty<object> property)) {
					value = property.Value;
					return true;
				}
			}

			value = null;
			return false;
		}

		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
			if (definitions.TryGetDefinition(key, out Definition? definition)) {
				if (definition is CalculatedDefinition calculated) {
					node = calculated.Evaluation;
					return true;
				}
				else if (contextNodes.TryGetValue(definition, out ContextValue<EvaluationNode> context)) {
					node = context.Value;
					return true;
				}
				else if (propertyNodes.TryGetValue(definition, out ContextProperty<EvaluationNode> property)) {
					node = property.Value;
					return true;
				}
				else if (definition is FallbackDefinition fallback && !contextValues.ContainsKey(fallback) && !propertyValues.ContainsKey(fallback)) {
					node = fallback.Evaluation;
					return true;
				}
			}

			node = null;
			return false;
		}

		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
			return definitions.TryGetFunctionInfo(name, out functionInfo);
		}
		public bool TryGetFunction(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionEvaluator functionEvaluator) {
			if (definitions.TryGetFunctionDefinition(name, out FunctionDefinition? function)) {
				functionEvaluator = function;
				return true;
			}
			else {
				functionEvaluator = null;
				return false;
			}
		}
		public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() {
			return definitions.GetFunctionInfos();
		}

		#region IEnumerable
		public IEnumerator<Definition> GetEnumerator() {
			return definitions.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
		#endregion

		public DefinitionEnvironment AppendDefinitionEnvironment(DefinitionEnvironment other) {
			return new DefinitionEnvironment(
				new DefinitionGroup(this.definitions.Where(d => !other.definitions.Conflicting(d)).Concat(other.definitions)),
				this.contextValues.Where(kv => !other.definitions.Conflicting(kv.Key)).Concat(other.contextValues).ToDictionary(),
				this.contextNodes.Where(kv => !other.definitions.Conflicting(kv.Key)).Concat(other.contextNodes).ToDictionary(),
				this.propertyValues.Where(kv => !other.definitions.Conflicting(kv.Key)).Concat(other.propertyValues).ToDictionary(),
				this.propertyNodes.Where(kv => !other.definitions.Conflicting(kv.Key)).Concat(other.propertyNodes).ToDictionary()
				);
		}

		public IEnumerable<KeyValuePair<Definition, ContextProperty<object>>> GetPropertyValues() {
			return propertyValues;
		}
		public IEnumerable<KeyValuePair<Definition, ContextProperty<EvaluationNode>>> GetPropertyNodes() {
			return propertyNodes;
		}
	}

}
