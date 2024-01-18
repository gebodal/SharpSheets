using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public class ArrayCreateFunction : AbstractFunction {

		public static readonly ArrayCreateFunction Instance = new ArrayCreateFunction();
		private ArrayCreateFunction() { }

		public override EvaluationName Name { get; } = "array";
		public override string? Description { get; } = "Creates an array from the arguments. The arguments must be of compatible types.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null), true)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType[] returnTypes = args.Select(a => a.ReturnType).Distinct().ToArray();

			if (returnTypes.Length == 0) {
				throw new EvaluationTypeException("Unknown return type for array create function (contains no elements).");
			}

			EvaluationType arrayType = returnTypes[0];
			bool badTypes = false;
			for (int i = 1; i < returnTypes.Length; i++) {
				if(EvaluationTypes.TryGetCompatibleType(arrayType, returnTypes[i], out EvaluationType? compatible)) {
					arrayType = compatible;
				}
				else {
					badTypes = true;
					break;
				}
			}

			if (badTypes) {
				string s = (returnTypes.Length > 1) ? "s" : "";
				throw new EvaluationTypeException($"Cannot create an array from arguments with type{s}: " + string.Join(", ", returnTypes.Select(t => t.ToString())));
			}

			if (arrayType.DataType == null) {
				throw new EvaluationTypeException($"Cannot create array of dynamic type {arrayType}.");
			}

			return arrayType.MakeArray(); // EvaluationType.Array(arrayType);
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationType returnType = GetReturnType(args);
			EvaluationType elemType = returnType.ElementType ?? throw new EvaluationTypeException($"Invalid return type for {nameof(ArrayCreateFunction)}.");
			Type? elementType = (returnType.ElementType?.DataType) ?? throw new EvaluationCalculationException($"Cannot construct array of type {returnType.ElementType}.");
			
			List<object?> results = new List<object?>();

			for (int i = 0; i < args.Length; i++) {
				object? arg = args[i].Evaluate(environment);
				results.Add(EvaluationTypes.GetCompatibleValue(elemType, arg));
			}

			return EvaluationTypes.MakeArray(elementType, results);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static EvaluationNode MakeArrayCreateNode(params EvaluationNode[] arguments) {
			EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance);
			node.SetArgumentCount(arguments.Length);
			for (int i = 0; i < arguments.Length; i++) {
				node.Arguments[i] = arguments[i];
			}
			return node.Simplify();
		}
	}

	public class ArrayConcatenateFunction : AbstractFunction {

		public static readonly ArrayConcatenateFunction Instance = new ArrayConcatenateFunction();
		private ArrayConcatenateFunction() { }

		public override EvaluationName Name { get; } = "concat";
		public override string? Description { get; } = "Concat all array arguments into a single array. The arguments must be of compatible types.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("array", null, null), true)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType[] returnTypes = args.Select(a => a.ReturnType).Distinct().ToArray();

			if (returnTypes.Length == 0) {
				throw new EvaluationTypeException("Unknown return type for array concat function (no arguments provided).");
			}

			EvaluationType? resultElemType;
			if (returnTypes[0].IsArray) {
				resultElemType = returnTypes[0].ElementType!;
				for (int i = 1; i < returnTypes.Length; i++) {
					EvaluationType? argElemType = returnTypes[i].ElementType;
					if(argElemType is null) {
						resultElemType = null;
						break;
					}

					if(EvaluationTypes.TryGetCompatibleType(resultElemType, argElemType, out EvaluationType? compatible)) {
						resultElemType = compatible;
					}
					else {
						resultElemType = null;
						break;
					}
				}
			}
			else {
				resultElemType = null;
			}

			if (resultElemType is null) {
				string s = (returnTypes.Length > 1) ? "s" : "";
				throw new EvaluationTypeException($"Cannot create an array from arguments with type{s}: " + string.Join(", ", returnTypes.Select(t => t.ToString())));
			}

			if (resultElemType.DataType == null) {
				throw new EvaluationTypeException($"Cannot create array of dynamic type {resultElemType}.");
			}

			return resultElemType.MakeArray();
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationType returnType = GetReturnType(args);
			EvaluationType elemType = returnType.ElementType ?? throw new EvaluationTypeException($"Invalid return type for {nameof(ArrayConcatenateFunction)}.");
			Type? elementType = (returnType.ElementType?.DataType) ?? throw new EvaluationCalculationException($"Cannot construct array of type {returnType.ElementType}.");

			List<object?> results = new List<object?>();

			for (int i = 0; i < args.Length; i++) {
				object? arg = args[i].Evaluate(environment);

				if(arg is Array arrayArg) {
					foreach(object? val in arrayArg) {
						results.Add(EvaluationTypes.GetCompatibleValue(elemType, val));
					}
				}
				else {
					throw new EvaluationCalculationException($"Cannot concatenate non-array result to array of type {returnType}.");
				}
			}

			return EvaluationTypes.MakeArray(elementType, results);
		}
	}

	public class ArrayContainsFunction : AbstractFunction {

		public static readonly ArrayContainsFunction Instance = new ArrayContainsFunction();
		private ArrayContainsFunction() { }

		public override EvaluationName Name { get; } = "contains";
		public override string? Description { get; } = "Returns true of the array (or tuple) returns the value, otherwise false. Alternatively, if two strings are provided, returns true of the first string contains the second.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("arrayOrTuple", null, null),
				new EnvironmentFunctionArg("value", null, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("text", EvaluationType.STRING, null),
				new EnvironmentFunctionArg("substring", EvaluationType.STRING, null)
				)
		);

		public sealed override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].ReturnType;
			EvaluationType arg2Type = args[1].ReturnType;
			if ((arg1Type.IsArray || arg1Type.IsTuple) && arg1Type.ElementType == arg2Type) {
				return EvaluationType.BOOL;
			}
			else if (arg1Type == EvaluationType.STRING && arg2Type == EvaluationType.STRING) {
				return EvaluationType.BOOL;
			}
			else {
				throw new EvaluationTypeException($"Contains not defined for operands of type {arg1Type} and {arg2Type}.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);
			object? b = args[1].Evaluate(environment);

			if (a is not null && EvaluationTypes.TryGetArray(a, out Array? values)) {
				try {
					return Array.IndexOf(values, b) != -1;
				}
				catch (RankException e) {
					throw new EvaluationTypeException($"Contains only defined for arrays of rank 1 (got rank {values.Rank}).", e);
				}
			}
			else if(a is string text && b is string searchTerm) {
				return text.Contains(searchTerm);
			}
			else {
				throw new EvaluationTypeException($"Contains not defined for operands of type {EvaluationUtils.GetDataTypeName(a)} and {EvaluationUtils.GetDataTypeName(b)}.");
			}
		}
	}

	public class ArrayAllFunction : AbstractFunction {

		public static readonly ArrayAllFunction Instance = new ArrayAllFunction();
		private ArrayAllFunction() { }

		public override EvaluationName Name { get; } = "all";
		public override string? Description { get; } = "Returns true if every entry of the array is true, otherwise false.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("arrayOrTuple", EvaluationType.BOOL.MakeArray(), null))
		);

		public sealed override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			if ((argType.IsArray || argType.IsTuple) && argType.ElementType == EvaluationType.BOOL) {
				return EvaluationType.BOOL;
			}
			else {
				throw new EvaluationTypeException($"All not defined for operands of type {argType}.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (a is not null && EvaluationTypes.TryGetArray(a, out Array? values)) {
				foreach(object i in values) {
					if(i is bool b) {
						if(!b) { return false; }
					}
					else {
						throw new EvaluationTypeException($"Invalid input entry of type {i?.GetType()?.Name ?? "null"}.");
					}
				}

				return true;
			}
			else {
				throw new EvaluationTypeException($"All not defined for operands of type {EvaluationUtils.GetDataTypeName(a)}.");
			}
		}
	}

	public class ArrayAnyFunction : AbstractFunction {

		public static readonly ArrayAnyFunction Instance = new ArrayAnyFunction();
		private ArrayAnyFunction() { }

		public override EvaluationName Name { get; } = "any";
		public override string? Description { get; } = "Returns true is any entry of the array is true, otherwise false.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("arrayOrTuple", EvaluationType.BOOL.MakeArray(), null))
		);

		public sealed override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			if ((argType.IsArray || argType.IsTuple) && argType.ElementType == EvaluationType.BOOL) {
				return EvaluationType.BOOL;
			}
			else {
				throw new EvaluationTypeException($"Any not defined for operands of type {argType}.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (a is not null && EvaluationTypes.TryGetArray(a, out Array? values)) {
				foreach (object i in values) {
					if (i is bool b) {
						if (b) { return true; }
					}
					else {
						throw new EvaluationTypeException($"Invalid input entry of type {i?.GetType()?.Name ?? "null"}.");
					}
				}

				return false;
			}
			else {
				throw new EvaluationTypeException($"\"{Name}\" not defined for operands of type {EvaluationUtils.GetDataTypeName(a)}.");
			}
		}
	}

	public class ArraySortFunction : AbstractFunction {

		public static readonly ArraySortFunction Instance = new ArraySortFunction();
		private ArraySortFunction() { }

		public override EvaluationName Name { get; } = "sort";
		public override string? Description { get; } = "Returns a sorted copy of the array (or tuple). If a keys array is provided, it must be the same length as the array to be sorted, and the sorting will use the sorted ordering of the keys array.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("arrayOrTuple", null, null),
				new EnvironmentFunctionArg("keys", null, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("arrayOrTuple", null, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			if (!(args.Length == 1 || args.Length == 2)) {
				throw new EvaluationTypeException($"Sort must take one or two array/tuple arguments (values [, keys ])."); // Better exception type?
			}
			if (!args.All(a => a.ReturnType.IsArray || a.ReturnType.IsTuple)) {
				throw new EvaluationTypeException($"Sort arguments must be arrays or tuples, not {string.Join(", ", args.Select(a => a.ReturnType.ToString()))}.");
			}
			EvaluationType argType = args[0].ReturnType;
			if (argType.IsTuple) {
				argType = argType.ElementType.MakeArray();
			}
			return argType;
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? arg1 = args[0].Evaluate(environment);

			if(arg1 is not null && EvaluationTypes.TryGetArray(arg1, out  Array? array)) {
				try {
					if (args.Length > 1) {
						object? arg2 = args[1].Evaluate(environment);
						if (arg2 is not null && EvaluationTypes.TryGetArray(arg2, out Array? keys)) {
							if(keys.Length != array.Length) {
								throw new EvaluationCalculationException($"Length of values ({array.Length}) does not match length of keys ({keys.Length}).");
							}
							Array.Sort(keys, array);
						}
						else {
							throw new EvaluationTypeException($"Invalid keys entry of type {arg2?.GetType()?.Name ?? "null"} to sort.");
						}
					}
					else {
						Array.Sort(array);
					}

					return array;
				}
				catch(InvalidOperationException e) {
					EvaluationType? elemType = args[0].ReturnType.ElementType;
					throw new EvaluationCalculationException($"Could not sort values (sorting not implemented for {elemType?.ToString() ?? "unknown type"}).", e);
				}
				catch(RankException e) {
					throw new EvaluationTypeException($"Sort only defined for arrays of rank 1 (got rank {array.Rank}).", e);
				}
			}
			else {
				throw new EvaluationTypeException($"Invalid values argument of type {arg1?.GetType()?.Name ?? "null"} to sort.");
			}
		}
	}

	public class ArrayReverseFunction : AbstractFunction {

		public static readonly ArrayReverseFunction Instance = new ArrayReverseFunction();
		private ArrayReverseFunction() { }

		public override EvaluationName Name { get; } = "reverse";
		public override string? Description { get; } = "Returns a reversed copy of the array (no sorting is performed).";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("arrayOrTuple", null, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			if (argType.IsTuple) {
				return argType.ElementType.MakeArray();
			}
			else if (!argType.IsArray) {
				throw new EvaluationTypeException($"Arguments for reverse must be an array or tuple, not {argType}.");
			}
			else {
				return argType;
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? arg = args[0].Evaluate(environment);

			if (arg is not null && EvaluationTypes.TryGetArray(arg, out Array? array)) {
				List<object?> results = new List<object?>();

				for (int i = array.Length - 1; i >= 0; i--) {
					results.Add(array.GetValue(i));
				}

				Type arrayType = (args[0].ReturnType.ElementType?.DataType) ?? throw new EvaluationTypeException("Cannot resolve array system type.");
				
				return EvaluationTypes.MakeArray(arrayType, results);
			}
			else {
				throw new EvaluationTypeException($"Invalid values argument of type {arg?.GetType()?.Name ?? "null"} to {Name}.");
			}
		}

	}

}
