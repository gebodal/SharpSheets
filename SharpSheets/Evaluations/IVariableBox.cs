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
		bool TryGetReturnType(EvaluationName key, [MaybeNullWhen(false)] out EvaluationType returnType);
		bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionInfo functionInfo);
		bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node);

		IEnumerable<EvaluationName> GetVariables();
		IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos();
	}

	public static class VariableBoxUtils {

		/// <summary>
		/// 
		/// </summary>
		/// <param name="variables"></param>
		/// <returns></returns>
		/// <exception cref="UndefinedVariableException"></exception>
		public static IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes(this IVariableBox variables) {
			foreach (EvaluationName key in variables.GetVariables()) {
				if (variables.TryGetReturnType(key, out EvaluationType? returnType)) {
					yield return new KeyValuePair<EvaluationName, EvaluationType>(key, returnType);
				}
				else if (variables.TryGetNode(key, out EvaluationNode? node)) {
					yield return new KeyValuePair<EvaluationName, EvaluationType>(key, node.ReturnType);
				}
				else {
					throw new UndefinedVariableException(key);
				}
			}
		}

		public static IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> GetNodes(this IVariableBox variables) {
			foreach (EvaluationName key in variables.GetVariables()) {
				if (variables.TryGetNode(key, out EvaluationNode? node)) {
					yield return new KeyValuePair<EvaluationName, EvaluationNode>(key, node);
				}
			}
		}

		/// <summary></summary>
		/// <exception cref="UndefinedVariableException"></exception>
		public static EvaluationNode GetNode(this IVariableBox variables, EvaluationName key) {
			if (variables.TryGetNode(key, out EvaluationNode? node)) {
				try {
					return node.Clone();
				}
				catch (EvaluationProcessingException) { }
			}
			else if (variables.TryGetReturnType(key, out EvaluationType? returnType)) {
				return new VariableNode(key, returnType);
			}

			// If all else fails
			throw new UndefinedVariableException(key);
		}

		public static bool IsVariable(this IVariableBox variables, EvaluationName key) {
			return variables.TryGetReturnType(key, out _);
		}

	}

	public static class VariableBoxes {

		public static readonly IVariableBox Empty = new ConcatenatedVariableBox(Array.Empty<IVariableBox>());

		private class ConcatenatedVariableBox : IVariableBox {
			public readonly IVariableBox[] variableBoxes;

			public ConcatenatedVariableBox(IEnumerable<IVariableBox> variableBoxes) {
				this.variableBoxes = variableBoxes.ToArray();
			}

			public bool TryGetReturnType(EvaluationName key, [MaybeNullWhen(false)] out EvaluationType returnType) {
				for (int i = 0; i < variableBoxes.Length; i++) {
					if (variableBoxes[i].TryGetReturnType(key, out EvaluationType? result)) {
						returnType = result;
						return true;
					}
				}

				returnType = null;
				return false;
			}

			public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
				for (int i = 0; i < variableBoxes.Length; i++) {
					if (variableBoxes[i].TryGetNode(key, out EvaluationNode? result)) {
						node = result;
						return true;
					}
				}

				node = null;
				return false;
			}

			public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionInfo functionInfo) {
				for (int i = 0; i < variableBoxes.Length; i++) {
					if (variableBoxes[i].TryGetFunctionInfo(name, out EnvironmentFunctionInfo? result)) {
						functionInfo = result;
						return true;
					}
				}

				functionInfo = null;
				return false;
			}

			public IEnumerable<EvaluationName> GetVariables() {
				return variableBoxes.SelectMany(e => e.GetVariables()).Distinct();
			}

			public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
				return variableBoxes.SelectMany(e => e.GetFunctionInfos()).DistinctBy(i => i.Name);
			}

		}

		#region Appending Variable Boxes

		public static IVariableBox AppendVariables(this IVariableBox source, IVariableBox other) {
			if (source is ConcatenatedVariableBox concatFirst && other is ConcatenatedVariableBox concatSecond) {
				return new ConcatenatedVariableBox(concatFirst.variableBoxes.Concat(concatSecond.variableBoxes));
			}
			else if (source is ConcatenatedVariableBox concatSource) {
				return new ConcatenatedVariableBox(concatSource.variableBoxes.Append(other));
			}
			else if (other is ConcatenatedVariableBox concatOther) {
				return new ConcatenatedVariableBox(source.Yield().Concat(concatOther.variableBoxes));
			}
			else {
				return new ConcatenatedVariableBox(new IVariableBox[] { source, other });
			}
		}

		public static IVariableBox Concat(params IVariableBox[] variableBoxes) {
			return new ConcatenatedVariableBox(variableBoxes);
		}

		#endregion

	}

	public static class SimpleVariableBoxes {

		#region SimpleVariableBox creation methods

		public static IVariableBox Create(
			IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes,
			IEnumerable<EnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(returnTypes, nodes, functions);
		}

		public static IVariableBox Create(
			IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return new SimpleVariableBox(returnTypes, nodes, null);
		}

		public static IVariableBox Create(
			IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes,
			IEnumerable<EnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(returnTypes, null, functions);
		}

		public static IVariableBox Create(
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes,
			IEnumerable<EnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(null, nodes, functions);
		}

		public static IVariableBox Create(IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> returnTypes) {
			return new SimpleVariableBox(returnTypes, null, null);
		}

		public static IVariableBox Create(IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return new SimpleVariableBox(null, nodes, null);
		}

		public static IVariableBox Create(IEnumerable<EnvironmentFunctionInfo> functions) {
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

			public bool TryGetReturnType(EvaluationName key, [MaybeNullWhen(false)] out EvaluationType returnType) {
				if (returnTypes.TryGetValue(key, out EvaluationType? type)) {
					returnType = type;
					return true;
				}
				else if (nodes.TryGetValue(key, out EvaluationNode? node)) {
					try {
						returnType = node.ReturnType;
						return true;
					}
					catch (EvaluationTypeException) { } // Should we throw something here? throw new UndefinedVariableException(key);
				}

				returnType = null;
				return false;
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

			public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionInfo functionInfo) {
				if(functions.TryGetValue(name, out EnvironmentFunctionInfo? result)) {
					functionInfo = result;
					return true;
				}
				else {
					functionInfo = null;
					return false;
				}
			}

			public IEnumerable<EvaluationName> GetVariables() {
				return returnTypes.Keys.Concat(nodes.Keys).Distinct();
			}

			public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
				return functions.Values;
			}

		}

	}

}
