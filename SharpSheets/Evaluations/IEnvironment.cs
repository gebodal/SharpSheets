using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Utilities;

namespace SharpSheets.Evaluations {

	// TODO Would it be helpful to distinguish between Environment (static/immutable) and EnvironmentBuilder (mutable and unstable)?

	/// <summary>
	/// IEnvironment objects should be immutable once created.
	/// This object promises that all keys in GetVariables() are available as nodes or values.
	/// </summary>
	public interface IEnvironment : IVariableBox {
		//object this[string key] { get; }
		//object GetValue(string key);
		bool TryGetValue(EvaluationName key, out object? value);

		/// <summary></summary>
		/// <param name="name"></param>
		/// <returns></returns>
		/// <exception cref="UndefinedFunctionException"></exception>
		EnvironmentFunction GetFunction(EvaluationName name);

		IEnumerable<KeyValuePair<EvaluationName, object?>> GetValues();
		IEnumerable<EnvironmentFunctionDefinition> GetFunctions();
	}

	public static class Environments {

		// TODO Need to clear up this naming confusion
		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="UndefinedVariableException"></exception>
		public static object? GetVariable(this IEnvironment environment, EvaluationName key) {
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

		public static readonly IEnvironment Empty = new SimpleEnvironment(null, null, null, null);

		#region SimpleEnvironment creation methods

		public static IEnvironment Simple(
			IEnumerable<KeyValuePair<EvaluationName, object>>? values,
			IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes,
			IEnumerable<EnvironmentFunctionDefinition>? functions,
			IVariableBox? variables) {
			return new SimpleEnvironment(SimpleEnvironment.MakeValuesDictionary(values), nodes, functions, variables);
		}

		public static IEnvironment Simple(IEnumerable<KeyValuePair<EvaluationName, object>> values) {
			return new SimpleEnvironment(SimpleEnvironment.MakeValuesDictionary(values), null, null, null);
		}

		public static IEnvironment Simple(IEnumerable<KeyValuePair<EvaluationName, (object?, EvaluationType)>> values) {
			return new SimpleEnvironment(values, null, null, null);
		}

		public static IEnvironment Simple(IEnumerable<KeyValuePair<EvaluationName, (object?, EvaluationType)>> values, IVariableBox variables) {
			return new SimpleEnvironment(values, null, null, variables);
		}

		public static IEnvironment Simple(IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return new SimpleEnvironment(null, nodes, null, null);
		}

		public static IEnvironment Simple(IEnumerable<EnvironmentFunctionDefinition> functions) {
			return new SimpleEnvironment(null, null, functions, null);
		}

		public static IEnvironment ToEnvironment(this IVariableBox variables) {
			return new SimpleEnvironment(null, null, null, variables);
		}

		#endregion

		private class SimpleEnvironment : IEnvironment {
			private readonly Dictionary<EvaluationName, (object? value, EvaluationType type)> values;
			//private readonly Dictionary<EvaluationName, EvaluationType> returnTypes;
			private readonly Dictionary<EvaluationName, EnvironmentFunctionDefinition> functions;
			private readonly IVariableBox variables;

			public SimpleEnvironment(IEnumerable<KeyValuePair<EvaluationName, (object? value, EvaluationType type)>>? values, IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes, IEnumerable<EnvironmentFunctionDefinition>? functions, IVariableBox? variables) {
				/*
				this.values = values?.ToDictionaryAllowRepeats(true) ?? new Dictionary<EvaluationName, object>();
				this.returnTypes = this.values.ToDictionaryAllowRepeats(
					kv => kv.Key,
					kv => EvaluationType.FromData(kv.Value),
					true);
				*/
				this.values = values?.ToDictionaryAllowRepeats(true) ?? new Dictionary<EvaluationName, (object?, EvaluationType)>();

				this.functions = functions?.ToDictionaryAllowRepeats(d => d.Name, true) ?? new Dictionary<EvaluationName, EnvironmentFunctionDefinition>();

				IVariableBox envVariables = variables ?? VariableBoxes.Empty;
				if (nodes != null) {
					envVariables = envVariables.AppendVariables(nodes);
				}
				this.variables = envVariables;
			}

			public static Dictionary<EvaluationName, (object? value, EvaluationType type)>? MakeValuesDictionary(IEnumerable<KeyValuePair<EvaluationName, object>>? values) {
				if(values is null) { return null; }
				return values.ToDictionaryAllowRepeats<KeyValuePair<EvaluationName, object>, EvaluationName, (object?, EvaluationType)>(
					kv => kv.Key,
					kv => (kv.Value, EvaluationType.FromData(kv.Value)),
					true);
			}

			public static Dictionary<EvaluationName, (object? value, EvaluationType type)> GetValuesDictionary(IEnvironment other) {
				return other.GetValues().ToDictionaryAllowRepeats<KeyValuePair<EvaluationName, object?>, EvaluationName, (object?, EvaluationType)>(
					kv => kv.Key,
					kv => (kv.Value, other.GetReturnType(kv.Key)),
					true);
			}

			/*
			public object this[string key] {
				get {
					if (values.ContainsKey(key)) { return values[key]; }
					else { throw new UndefinedVariableException(key); }
				}
			}
			*/

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

			public bool IsVariable(EvaluationName key) {
				return values.ContainsKey(key) || variables.TryGetNode(key, out _);
			}

			public EvaluationType GetReturnType(EvaluationName key) {
				/*
				if (returnTypes.TryGetValue(key, out EvaluationType returnType)) {
					return returnType;
				}
				else if(variables.TryGetNode(key, out EvaluationNode node)) {
					return node.ReturnType;
				}
				else {
					throw new UndefinedVariableException(key);
				}
				*/
				if (values.TryGetValue(key, out (object? value, EvaluationType returnType) entry)) {
					return entry.returnType;
				}
				else {
					return variables.GetReturnType(key);
				}
			}

			public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
				if (!values.ContainsKey(key)) {
					return variables.TryGetNode(key, out node);
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
				if (functions.TryGetValue(name, out EnvironmentFunctionDefinition? function)) {
					return function;
				}
				else {
					throw new UndefinedFunctionException(name);
				}
			}

			public EnvironmentFunction GetFunction(EvaluationName name) {
				if(functions.TryGetValue(name, out EnvironmentFunctionDefinition? func)) {
					return func.Evaluator;
				}
				else {
					throw new UndefinedFunctionException(name);
				}
			}

			public IEnumerable<EvaluationName> GetVariables() {
				return values.Keys.Concat(variables.GetVariables()).Distinct();
			}

			public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes() {
				return GetVariables().ToDictionary(v => v, v => GetReturnType(v));
			}
			public IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> GetNodes() {
				return variables.GetNodes();
			}
			public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
				return functions.Values;
			}

			public IEnumerable<KeyValuePair<EvaluationName, object?>> GetValues() {
				return values.Select(i => new KeyValuePair<EvaluationName, object?>(i.Key, i.Value.value));
			}
			public IEnumerable<EnvironmentFunctionDefinition> GetFunctions() {
				return functions.Values;
			}
		}

		#region Appending Environment methods

		private static IEnvironment AppendEnvironment(this IEnvironment source, IEnumerable<KeyValuePair<EvaluationName, (object?, EvaluationType)>>? values, IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes, IEnumerable<EnvironmentFunctionDefinition>? functions, IVariableBox? variables) {
			return new SimpleEnvironment(
				source.GetValues().Select(kv => new KeyValuePair<EvaluationName, (object?, EvaluationType)>(kv.Key, (kv.Value, source.GetReturnType(kv.Key))))
					.Concat(values ?? Enumerable.Empty<KeyValuePair<EvaluationName, (object?, EvaluationType)>>()),
				nodes,
				functions != null ? source.GetFunctions().Concat(functions) : source.GetFunctions(),
				source.AppendVariables(variables ?? VariableBoxes.Empty));
		}

		public static IEnvironment AppendEnvironment(this IEnvironment source, IEnumerable<KeyValuePair<EvaluationName, object>>? values, IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>>? nodes, IEnumerable<EnvironmentFunctionDefinition>? functions, IVariableBox? variables) {
			return AppendEnvironment(source, values != null ? SimpleEnvironment.MakeValuesDictionary(values) : null, nodes, functions, variables);
		}

		public static IEnvironment AppendEnvironment(this IEnvironment source, IEnvironment other) {
			return AppendEnvironment(source, SimpleEnvironment.GetValuesDictionary(other), null, other.GetFunctions(), other);
		}

		public static IEnvironment AppendEnvironment(this IEnvironment source, IEnumerable<KeyValuePair<EvaluationName, object>> values) {
			return AppendEnvironment(source, values, null, null, null);
		}

		public static IEnvironment AppendEnvironment(this IEnvironment source, IEnumerable<KeyValuePair<EvaluationName, (object? value, EvaluationType type)>> values) {
			return AppendEnvironment(source, values, null, null, null);
		}

		public static IEnvironment AppendEnvironment(this IEnvironment source, IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> nodes) {
			return AppendEnvironment(source, Enumerable.Empty<KeyValuePair<EvaluationName, (object?, EvaluationType)>>(), nodes, null, null);
		}

		#endregion

	}

	/// <summary>
	/// To be used only as a temporary environment. Creation of this object involves no
	/// additional processing of component IEnvironment contents, and so is faster to construct,
	/// but the access methods will likely be slower, as they may call methods on both components.
	/// If the environment is expected to be long-lived and frequently accessed, use
	/// <see cref="Environments.AppendEnvironment(IEnvironment, IEnvironment)"/> method.
	/// </summary>
	public class FallbackEnvironment : IEnvironment {

		private readonly IEnvironment original;
		private readonly IEnvironment appended;

		private FallbackEnvironment(IEnvironment original, IEnvironment appended) {
			this.original = original;
			this.appended = appended;
		}

		public static IEnvironment Create(IEnvironment original, IEnvironment appended) {
			return new FallbackEnvironment(original, appended);
		}

		public bool IsVariable(EvaluationName key) {
			return original.IsVariable(key) || appended.IsVariable(key);
		}

		public bool TryGetValue(EvaluationName key, out object? value) {
			if(appended.TryGetValue(key, out value)) {
				return true;
			}
			else {
				return original.TryGetValue(key, out value);
			}
		}

		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
			if (appended.TryGetNode(key, out node)) {
				return true;
			}
			else {
				return original.TryGetNode(key, out node);
			}
		}

		public EvaluationType GetReturnType(EvaluationName key) {
			if (appended.IsVariable(key)) {
				return appended.GetReturnType(key);
			}
			else {
				return original.GetReturnType(key);
			}
		}

		public IEnumerable<KeyValuePair<EvaluationName, object?>> GetValues() {
			return original.GetValues().Concat(appended.GetValues()).ToDictionaryAllowRepeats(true);
		}

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> GetNodes() {
			return original.GetNodes().Concat(appended.GetNodes()).ToDictionaryAllowRepeats(true);
		}

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes() {
			return original.GetReturnTypes().Concat(appended.GetReturnTypes()).ToDictionaryAllowRepeats(true);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return original.GetVariables().Concat(appended.GetVariables()).Distinct();
		}

		public bool IsFunction(EvaluationName name) {
			return original.IsFunction(name) || appended.IsFunction(name);
		}

		public EnvironmentFunction GetFunction(EvaluationName name) {
			if (appended.IsFunction(name)) {
				return appended.GetFunction(name);
			}
			else {
				return original.GetFunction(name);
			}
		}

		public EnvironmentFunctionInfo GetFunctionInfo(EvaluationName name) {
			if (appended.IsFunction(name)) {
				return appended.GetFunctionInfo(name);
			}
			else {
				return original.GetFunctionInfo(name);
			}
		}

		public IEnumerable<EnvironmentFunctionDefinition> GetFunctions() {
			return original.GetFunctions().Concat(appended.GetFunctions());
		}

		public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
			return original.GetFunctionInfos().Concat(appended.GetFunctionInfos());
		}

	}

}
