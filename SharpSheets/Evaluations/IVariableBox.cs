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
		bool IsEmpty { get; }

		bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo);
		bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo);
		bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node);

		IEnumerable<EnvironmentVariableInfo> GetVariables();
		IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos();
	}

	public static class VariableBoxUtils {

		public static bool TryGetReturnType(this IVariableBox variables, EvaluationName key, [MaybeNullWhen(false)] out EvaluationType returnType) {
			if(variables.TryGetVariableInfo(key, out EnvironmentVariableInfo? variableInfo)) {
				returnType = variableInfo.EvaluationType;
				return true;
			}
			else {
				returnType = null;
				return false;
			}
		}

		public static IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes(this IVariableBox variables) {
			foreach (EnvironmentVariableInfo info in variables.GetVariables()) {
				yield return new KeyValuePair<EvaluationName, EvaluationType>(info.Name, info.EvaluationType);
			}
		}

		public static IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> GetNodes(this IVariableBox variables) {
			foreach (EvaluationName key in variables.GetVariables().Select(i => i.Name)) {
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
			else if (variables.TryGetVariableInfo(key, out EnvironmentVariableInfo? variableInfo)) {
				return new VariableNode(variableInfo.Name, variableInfo.EvaluationType);
			}

			// If all else fails
			throw new UndefinedVariableException(key);
		}

		public static bool IsVariable(this IVariableBox variables, EvaluationName key) {
			return variables.TryGetVariableInfo(key, out _);
		}

		public static bool IsFunction(this IVariableBox variables, EvaluationName name) {
			return variables.TryGetFunctionInfo(name, out _);
		}

	}

	public static class VariableBoxes {

		public static readonly IVariableBox Empty = new ConcatenatedVariableBox(Array.Empty<IVariableBox>());

		private class ConcatenatedVariableBox : IVariableBox {
			public readonly IVariableBox[] variableBoxes;

			public bool IsEmpty => variableBoxes.All(b => b.IsEmpty);

			public ConcatenatedVariableBox(IEnumerable<IVariableBox> variableBoxes) {
				this.variableBoxes = variableBoxes.Where(b => !b.IsEmpty).ToArray();
			}

			public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
				for (int i = 0; i < variableBoxes.Length; i++) {
					if (variableBoxes[i].TryGetVariableInfo(key, out EnvironmentVariableInfo? result)) {
						variableInfo = result;
						return true;
					}
				}

				variableInfo = null;
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

			public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
				for (int i = 0; i < variableBoxes.Length; i++) {
					if (variableBoxes[i].TryGetFunctionInfo(name, out IEnvironmentFunctionInfo? result)) {
						functionInfo = result;
						return true;
					}
				}

				functionInfo = null;
				return false;
			}

			public IEnumerable<EnvironmentVariableInfo> GetVariables() {
				return variableBoxes.SelectMany(e => e.GetVariables()).DistinctBy(i => i.Name);
			}

			public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() {
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
			IEnumerable<EnvironmentVariableInfo> variables,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes,
			IEnumerable<IEnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(variables, nodes, functions);
		}

		public static IVariableBox Create(
			IEnumerable<EnvironmentVariableInfo> variables,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return new SimpleVariableBox(variables, nodes, null);
		}

		public static IVariableBox Create(
			IEnumerable<EnvironmentVariableInfo> variables,
			IEnumerable<IEnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(variables, null, functions);
		}

		public static IVariableBox Create(
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes,
			IEnumerable<IEnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(null, nodes, functions);
		}

		public static IVariableBox Create(IEnumerable<EnvironmentVariableInfo> variables) {
			return new SimpleVariableBox(variables, null, null);
		}

		public static IVariableBox Create(IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return new SimpleVariableBox(null, nodes, null);
		}

		public static IVariableBox Create(IEnumerable<IEnvironmentFunctionInfo> functions) {
			return new SimpleVariableBox(null, null, functions);
		}

		public static IVariableBox Single(EnvironmentVariableInfo info) {
			return new SimpleVariableBox(info.Yield(), null, null);
		}

		#endregion

		private class SimpleVariableBox : IVariableBox {
			private readonly Dictionary<EvaluationName, EnvironmentVariableInfo> variables;
			private readonly Dictionary<EvaluationName, EvaluationNode> nodes;
			private readonly Dictionary<EvaluationName, IEnvironmentFunctionInfo> functions;

			public bool IsEmpty => variables.Count == 0 && nodes.Count == 0 && functions.Count == 0;

			public SimpleVariableBox(IEnumerable<EnvironmentVariableInfo>? variables, IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes, IEnumerable<IEnvironmentFunctionInfo>? functions) {
				this.variables = variables?.ToDictionaryAllowRepeats(i => i.Name, true) ?? new Dictionary<EvaluationName, EnvironmentVariableInfo>();
				this.nodes = nodes?.ToDictionaryAllowRepeats(true) ?? new Dictionary<EvaluationName, EvaluationNode>();
				this.functions = functions?.ToDictionaryAllowRepeats(i => i.Name, true) ?? new Dictionary<EvaluationName, IEnvironmentFunctionInfo>();
			}

			public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
				if (variables.TryGetValue(key, out EnvironmentVariableInfo? info)) {
					variableInfo = info;
					return true;
				}
				else if (nodes.TryGetValue(key, out EvaluationNode? node)) {
					try {
						variableInfo = new EnvironmentVariableInfo(key, node.ReturnType, null);
						return true;
					}
					catch (EvaluationTypeException) { } // Should we throw something here? throw new UndefinedVariableException(key);
				}

				variableInfo = null;
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

			public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
				if(functions.TryGetValue(name, out IEnvironmentFunctionInfo? result)) {
					functionInfo = result;
					return true;
				}
				else {
					functionInfo = null;
					return false;
				}
			}

			public IEnumerable<EnvironmentVariableInfo> GetVariables() {
				return variables.Values;
			}

			public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() {
				return functions.Values;
			}

		}

	}

}
