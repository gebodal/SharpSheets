using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Utilities;

namespace SharpSheets.Evaluations {

	/// <summary>
	/// IVariableBox objects should be immutable once created.
	/// This object makes no promises that the keys in GetVariables() are all available as nodes.
	/// But all variables and functions must have return types and function information available.
	/// </summary>
	public interface IVariableBox {
		bool IsVariable(EvaluationName key);
		bool IsFunction(EvaluationName name);

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		/// <exception cref="UndefinedVariableException"></exception>
		EvaluationType GetReturnType(EvaluationName key);

		/// <summary></summary>
		/// <param name="name"></param>
		/// <returns></returns>
		/// <exception cref="UndefinedFunctionException"></exception>
		EnvironmentFunctionInfo GetFunctionInfo(EvaluationName name);

		bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node);

		IEnumerable<EvaluationName> GetVariables();

		IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes();
		IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> GetNodes();
		IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos();
	}

	public static class VariableBoxes {

		public static readonly IVariableBox Empty = new SimpleVariableBox(null, null, null);

		#region SimpleVariableBox creation methods

		public static IVariableBox Simple(
			IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes,
			IEnumerable<EnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(returnTypes, nodes, functions);
		}

		public static IVariableBox Simple(
			IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return new SimpleVariableBox(returnTypes, nodes, null);
		}

		public static IVariableBox Simple(
			IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes,
			IEnumerable<EnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(returnTypes, null, functions);
		}

		public static IVariableBox Simple(
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes,
			IEnumerable<EnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(null, nodes, functions);
		}

		public static IVariableBox Simple(IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes) {
			return new SimpleVariableBox(returnTypes, null, null);
		}

		public static IVariableBox Simple(IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return new SimpleVariableBox(null, nodes, null);
		}

		public static IVariableBox Simple(IEnumerable<EnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(null, null, functions);
		}

		#endregion

		private class SimpleVariableBox : IVariableBox {
			private readonly Dictionary<EvaluationName, EvaluationType> returnTypes;
			private readonly Dictionary<EvaluationName, EvaluationNode> nodes;
			private readonly Dictionary<EvaluationName, EnvironmentFunctionInfo> functions;

			public SimpleVariableBox(IEnumerable<KeyValuePair<EvaluationName, EvaluationType>>? returnTypes, IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes, IEnumerable<EnvironmentFunctionInfo>? functions) {
				this.returnTypes = returnTypes?.ToDictionaryAllowRepeats(true) ?? new Dictionary<EvaluationName, EvaluationType>();
				this.nodes = nodes?.ToDictionaryAllowRepeats(true) ?? new Dictionary<EvaluationName, EvaluationNode>();
				this.functions = functions?.ToDictionaryAllowRepeats(i => i.Name, true) ?? new Dictionary<EvaluationName, EnvironmentFunctionInfo>();
			}

			public bool IsVariable(EvaluationName key) {
				return returnTypes.ContainsKey(key) || nodes.ContainsKey(key);
			}

			public EvaluationType GetReturnType(EvaluationName key) {
				if (returnTypes.TryGetValue(key, out EvaluationType? type)) {
					return type;
				}
				else if (nodes.TryGetValue(key, out EvaluationNode? node)) {
					try {
						return node.ReturnType;
					}
					catch (EvaluationTypeException) {
						throw new UndefinedVariableException(key);
					}
				}
				else {
					throw new UndefinedVariableException(key);
				}
			}

			public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
				if (nodes.TryGetValue(key, out node)) {
					return true;
				}
				else {
					node = null;
					return false;
				}
			}

			public bool IsFunction(EvaluationName name) {
				return functions.ContainsKey(name);
			}

			public EnvironmentFunctionInfo GetFunctionInfo(EvaluationName name) {
				if(functions.TryGetValue(name, out EnvironmentFunctionInfo? functionInfo)) {
					return functionInfo;
				}
				else {
					throw new UndefinedFunctionException(name);
				}
			}

			public IEnumerable<EvaluationName> GetVariables() {
				return returnTypes.Keys.Concat(nodes.Keys).Distinct();
			}

			public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes() {
				return returnTypes.Concat(nodes.ToDictionary(kv => kv.Key, kv => kv.Value.ReturnType)).Distinct();
			}
			public IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> GetNodes() {
				return nodes;
			}
			public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
				return functions.Values;
			}
		}

		#region Appending VariableBox methods

		public static IVariableBox AppendVariables(this IVariableBox variables,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationType>>? returnTypes,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes,
			IEnumerable<EnvironmentFunctionInfo>? functions) {

			return new SimpleVariableBox(
				returnTypes != null ? variables.GetReturnTypes().Concat(returnTypes) : variables.GetReturnTypes(),
				nodes != null ? variables.GetNodes().Concat(nodes) : variables.GetNodes(),
				functions != null ? variables.GetFunctionInfos().Concat(functions) : variables.GetFunctionInfos()
				);
		}

		public static IVariableBox AppendVariables(this IVariableBox variables, IVariableBox other) {
			return AppendVariables(variables, other.GetReturnTypes(), other.GetNodes(), other.GetFunctionInfos());
		}

		public static IVariableBox AppendVariables(this IVariableBox variables,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return AppendVariables(variables, returnTypes, nodes, null);
		}

		public static IVariableBox AppendVariables(this IVariableBox variables,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes,
			IEnumerable<EnvironmentFunctionInfo> functions) {
			return AppendVariables(variables, returnTypes, null, functions);
		}

		public static IVariableBox AppendVariables(this IVariableBox variables,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes,
			IEnumerable<EnvironmentFunctionInfo> functions) {
			return AppendVariables(variables, null, nodes, functions);
		}

		public static IVariableBox AppendVariables(this IVariableBox variables, IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes) {
			return AppendVariables(variables, returnTypes, null, null);
		}

		public static IVariableBox AppendVariables(this IVariableBox variables, IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return AppendVariables(variables, null, nodes, null);
		}

		public static IVariableBox AppendVariables(this IVariableBox variables, IEnumerable<EnvironmentFunctionInfo> functions) {
			return AppendVariables(variables, null, null, functions);
		}

		#endregion

		#region Helper Functions

		/// <summary></summary>
		/// <exception cref="UndefinedVariableException"></exception>
		public static EvaluationNode GetNode(this IVariableBox variables, EvaluationName key) {
			if (variables.TryGetNode(key, out EvaluationNode? node)) {
				try {
					return node.Clone();
				}
				catch (EvaluationProcessingException) {
					throw new UndefinedVariableException(key);
				}
			}
			else {
				return new VariableNode(key, variables.GetReturnType(key));
			}
		}

		#endregion
	}

}
