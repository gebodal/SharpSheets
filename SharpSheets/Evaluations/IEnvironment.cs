using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Utilities;

namespace SharpSheets.Evaluations {

	// TODO Would it be helpful to distinguish between Environment (static/immutable) and EnvironmentBuilder (mutable and unstable)?

	/*
	 * The environment function documentation should return a collection of possible argument lists.
	 * Each list may contain 0 or more arguments.
	 * The final argument may be marked as "nargs", or similar, to indicate that it can be repeated.
	 * An unknown argument type can be indicated with a null? Or with a Union type?
	 * The environment function object should have a way of validating its own arguments (like the current ReturnType).
	 * 
	 * The AppendEnvironment mechanism should be changed to create ConcatenatedEnvironment objects.
	 * These can themselves be concatenated together to produce single objects.
	 * This can improve readability (no more tree structures), and avoid weird issues of extracting environment contents.
	 * 
	 * Environments should rely less on SimpleEnvironment objects.
	 * Maybe remove that as an option in the main codebase? (For testing only?)
	 * Make some custom Environment classes for storing particular types of Environment data.
	 * Do the environments need simplifying to make this work?
	 * Remove `IsVariable` and `IsFunction`, and just have `TryGet...` methods (this requires fewer methods overall).
	 */

	/// <summary>
	/// IEnvironment objects should be immutable once created.
	/// This object promises that all keys in GetVariables() are available as nodes or values.
	/// </summary>
	public interface IEnvironment : IVariableBox {
		bool TryGetValue(EvaluationName key, out object? value);
		bool TryGetFunction(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionDefinition functionDefinition);
	}

	public static class EnvironmentUtils {

		/// <summary>
		/// 
		/// </summary>
		/// <param name="environment"></param>
		/// <returns></returns>
		/// <exception cref="UndefinedVariableException"></exception>
		public static IEnumerable<KeyValuePair<EvaluationName, object?>> GetValues(this IEnvironment environment) {
			foreach (EvaluationName key in environment.GetVariables()) {
				if (environment.TryGetValue(key, out object? value)) {
					yield return new KeyValuePair<EvaluationName, object?>(key, value);
				}
				else {
					throw new UndefinedVariableException(key);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="environment"></param>
		/// <returns></returns>
		/// <exception cref="UndefinedFunctionException"></exception>
		public static IEnumerable<EnvironmentFunctionDefinition> GetFunctions(this IEnvironment environment) {
			foreach (EnvironmentFunctionInfo functionInfo in environment.GetFunctionInfos()) {
				if (environment.TryGetFunction(functionInfo.Name, out EnvironmentFunctionDefinition? functionDefinition)) {
					yield return functionDefinition;
				}
				else {
					throw new UndefinedFunctionException(functionInfo.Name);
				}
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="UndefinedVariableException"></exception>
		public static object? GetValue(this IEnvironment environment, EvaluationName key) {
			if (environment.TryGetValue(key, out object? value)) {
				return value;
			}
			else if (environment.TryGetNode(key, out EvaluationNode? node)) {
				return node.Evaluate(environment);
			}
			else {
				throw new UndefinedVariableException(key);
			}
		}

		public static EnvironmentFunctionDefinition GetFunction(this IEnvironment environment, EvaluationName name) {
			if (environment.TryGetFunction(name, out EnvironmentFunctionDefinition? functionDefinition)) {
				return functionDefinition;
			}

			// If all else fails
			throw new UndefinedFunctionException(name);
		}

	}

	public static class Environments {

		public static readonly IEnvironment Empty = new ConcatenatedEnvironment(Array.Empty<IEnvironment>());

		private class ConcatenatedEnvironment : IEnvironment {
			public readonly IEnvironment[] environments;

			public ConcatenatedEnvironment(IEnumerable<IEnvironment> environments) {
				this.environments = environments.ToArray();
			}

			public bool TryGetValue(EvaluationName key, out object? value) {
				for (int i = 0; i < environments.Length; i++) {
					if (environments[i].TryGetValue(key, out object? result)) {
						value = result;
						return true;
					}
				}

				value = null;
				return false;
			}

			public bool TryGetReturnType(EvaluationName key, [MaybeNullWhen(false)] out EvaluationType returnType) {
				for (int i = 0; i < environments.Length; i++) {
					if (environments[i].TryGetReturnType(key, out EvaluationType? result)) {
						returnType = result;
						return true;
					}
				}

				returnType = null;
				return false;
			}

			public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
				for (int i = 0; i < environments.Length; i++) {
					if (environments[i].TryGetNode(key, out EvaluationNode? result)) {
						node = result;
						return true;
					}
				}

				node = null;
				return false;
			}

			public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionInfo functionInfo) {
				for (int i = 0; i < environments.Length; i++) {
					if (environments[i].TryGetFunctionInfo(name, out EnvironmentFunctionInfo? result)) {
						functionInfo = result;
						return true;
					}
				}

				functionInfo = null;
				return false;
			}

			public bool TryGetFunction(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionDefinition functionDefinition) {
				for (int i = 0; i < environments.Length; i++) {
					if (environments[i].TryGetFunction(name, out EnvironmentFunctionDefinition? result)) {
						functionDefinition = result;
						return true;
					}
				}

				functionDefinition = null;
				return false;
			}

			public IEnumerable<EvaluationName> GetVariables() {
				return environments.SelectMany(e => e.GetVariables()).Distinct();
			}

			public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
				return environments.SelectMany(e => e.GetFunctionInfos()).DistinctBy(i => i.Name);
			}

		}

		#region Appending Environments

		public static IEnvironment AppendEnvironment(this IEnvironment source, IEnvironment other) {
			if (source is ConcatenatedEnvironment concatFirst && other is ConcatenatedEnvironment concatSecond) {
				return new ConcatenatedEnvironment(concatFirst.environments.Concat(concatSecond.environments));
			}
			else if (source is ConcatenatedEnvironment concatSource) {
				return new ConcatenatedEnvironment(concatSource.environments.Append(other));
			}
			else if (other is ConcatenatedEnvironment concatOther) {
				return new ConcatenatedEnvironment(source.Yield().Concat(concatOther.environments));
			}
			else {
				return new ConcatenatedEnvironment(new IEnvironment[] { source, other });
			}
		}

		public static IEnvironment Concat(params IEnvironment[] environments) {
			return new ConcatenatedEnvironment(environments);
		}

		#endregion

	}

	public static class SimpleEnvironments {

		#region SimpleEnvironment creation methods

		public static IEnvironment CreateAndInferType(
			IEnumerable<KeyValuePair<EvaluationName, object>>? values,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes,
			IEnumerable<EnvironmentFunctionDefinition>? functions,
			IVariableBox? variables) {
			return new SimpleEnvironment(SimpleEnvironment.MakeValuesDictionary(values), nodes, functions, variables);
		}

		public static IEnvironment Create(
			IEnumerable<KeyValuePair<EvaluationName, (object?, EvaluationType)>>? values,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes,
			IEnumerable<EnvironmentFunctionDefinition>? functions,
			IVariableBox? variables) {
			return new SimpleEnvironment(values, nodes, functions, variables);
		}

		public static IEnvironment CreateAndInferType(IEnumerable<KeyValuePair<EvaluationName, object>> values) {
			return new SimpleEnvironment(SimpleEnvironment.MakeValuesDictionary(values), null, null, null);
		}

		public static IEnvironment Create(IEnumerable<KeyValuePair<EvaluationName, (object?, EvaluationType)>> values) {
			return new SimpleEnvironment(values, null, null, null);
		}

		public static IEnvironment Create(IEnumerable<KeyValuePair<EvaluationName, (object?, EvaluationType)>> values, IVariableBox variables) {
			return new SimpleEnvironment(values, null, null, variables);
		}

		public static IEnvironment Create(IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return new SimpleEnvironment(null, nodes, null, null);
		}

		public static IEnvironment Create(IEnumerable<EnvironmentFunctionDefinition> functions) {
			return new SimpleEnvironment(null, null, functions, null);
		}

		public static IEnvironment ToEnvironment(this IVariableBox variables) {
			return new SimpleEnvironment(null, null, null, variables);
		}

		public static IEnvironment Single(EvaluationName key, object? value, EvaluationType type) {
			return new SimpleEnvironment(new KeyValuePair<EvaluationName, (object?, EvaluationType)>(key, (value, type)).Yield(), null, null, null);
		}

		#endregion

		private class SimpleEnvironment : IEnvironment {
			private readonly IReadOnlyDictionary<EvaluationName, (object? value, EvaluationType type)> values;
			private readonly IReadOnlyDictionary<EvaluationName, EnvironmentFunctionDefinition> functions;
			private readonly IReadOnlyDictionary<EvaluationName, EvaluationNode> nodes;
			private readonly IVariableBox variables1;

			public SimpleEnvironment(IEnumerable<KeyValuePair<EvaluationName, (object? value, EvaluationType type)>>? values, IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes, IEnumerable<EnvironmentFunctionDefinition>? functions, IVariableBox? variables) {
				this.values = values?.ToDictionaryAllowRepeats(true) ?? new Dictionary<EvaluationName, (object?, EvaluationType)>();
				this.functions = functions?.ToDictionaryAllowRepeats(d => d.Name, true) ?? new Dictionary<EvaluationName, EnvironmentFunctionDefinition>();
				this.nodes = nodes?.ToDictionaryAllowRepeats(true) ?? new Dictionary<EvaluationName, EvaluationNode>();
				this.variables1 = variables ?? VariableBoxes.Empty;
			}

			public static Dictionary<EvaluationName, (object? value, EvaluationType type)>? MakeValuesDictionary(IEnumerable<KeyValuePair<EvaluationName, object>>? values) {
				if(values is null) { return null; }
				return values.ToDictionaryAllowRepeats<KeyValuePair<EvaluationName, object>, EvaluationName, (object?, EvaluationType)>(
					kv => kv.Key,
					kv => (kv.Value, EvaluationType.FromData(kv.Value)),
					true);
			}

			public bool TryGetValue(EvaluationName key, out object? value) {
				if (values.TryGetValue(key, out (object? value, EvaluationType returnType) entry)) {
					value = entry.value;
					return true;
				}
				else {
					value = null;
					return false;
				}
			}

			public bool TryGetReturnType(EvaluationName key, [MaybeNullWhen(false)] out EvaluationType returnType) {
				if (values.TryGetValue(key, out (object? value, EvaluationType returnType) entry)) {
					returnType = entry.returnType;
					return true;
				}
				else if(nodes.TryGetValue(key, out EvaluationNode? node)) {
					returnType = node.ReturnType;
					return true;
				}
				else {
					return variables1.TryGetReturnType(key, out returnType);
				}
			}

			public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
				if(nodes.TryGetValue(key, out node)) {
					return true;
				}
				if (!values.ContainsKey(key)) {
					return variables1.TryGetNode(key, out node);
				}
				else {
					node = null;
					return false;
				}
			}

			public bool TryGetFunction(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionDefinition functionDefinition) {
				if (functions.TryGetValue(name, out EnvironmentFunctionDefinition? func)) {
					functionDefinition = func;
					return true;
				}
				else if (variables1.TryGetFunctionInfo(name, out EnvironmentFunctionInfo? varablesFunctionInfo) && varablesFunctionInfo is EnvironmentFunctionDefinition variablesFunctionDefinition) {
					functionDefinition = variablesFunctionDefinition;
					return true;
				}
				else {
					functionDefinition = null;
					return false;
				}
			}

			public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionInfo functionInfo) {
				if(TryGetFunction(name, out EnvironmentFunctionDefinition? functionDefinition)) {
					functionInfo = functionDefinition;
					return true;
				}
				else if(variables1.TryGetFunctionInfo(name, out EnvironmentFunctionInfo? varablesFunctionInfo)) {
					functionInfo = varablesFunctionInfo;
					return true;
				}
				else {
					functionInfo= null;
					return false;
				}
			}

			public IEnumerable<EvaluationName> GetVariables() {
				return values.Keys.Concat(nodes.Keys).Concat(variables1.GetVariables()).Distinct();
			}

			public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
				return functions.Values.Concat(variables1.GetFunctionInfos()).DistinctBy(f => f.Name);
			}

		}

	}

}
