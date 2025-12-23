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

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null), true)
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null), true)
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			return GetReturnElementType(context, args).MakeArray(); // EvaluationType.Array(arrayType);
		}

		private static EvaluationType GetReturnElementType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType[] returnTypes = args.Select(a => a.GetReturnType()).Distinct().ToArray();

			if (returnTypes.Length == 0) {
				throw new EvaluationTypeException("Unknown return type for array create function (contains no elements).");
			}

			EvaluationType arrayElemType = returnTypes[0];
			bool badTypes = false;
			for (int i = 1; i < returnTypes.Length; i++) {
				if (context.TryGetLeastUpperBoundType(arrayElemType, returnTypes[i], out EvaluationType? compatible)) {
					arrayElemType = compatible;
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

			if (arrayElemType.DataType == null) {
				throw new EvaluationTypeException($"Cannot create array of dynamic type {arrayElemType}.");
			}

			return arrayElemType;
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationType elemType = GetReturnElementType(environment.Context, args);
			
			List<EvaluationValue> results = new List<EvaluationValue>();

			for (int i = 0; i < args.Length; i++) {
				EvaluationValue arg = args[i].Evaluate(environment);
				EvaluationValue castArg = elemType.Cast(arg) ?? throw new EvaluationTypeException($"Cannot cast value of type {arg.Type} to {elemType}.");
				results.Add(castArg);
			}

			return ArrayEvaluationType.MakeArray(elemType, results);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static EvaluationNode MakeArrayCreateNode(EvaluationContext context, params EvaluationNode[] arguments) {
			EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, context);
			node.SetArguments(arguments);
			return node.Simplify();
		}
	}

	public class ArrayConcatenateFunction : AbstractFunction {

		public static readonly ArrayConcatenateFunction Instance = new ArrayConcatenateFunction();
		private ArrayConcatenateFunction() { }

		public override EvaluationName Name { get; } = "concat";
		public override string? Description { get; } = "Concat all array arguments into a single array. The arguments must be of compatible types.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("array", null, null), true)
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("array", null, null), true)
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			return GetResultElementType(context, args).MakeArray();
		}

		private static EvaluationType GetResultElementType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType[] returnTypes = args.Select(a => a.GetReturnType()).Distinct().ToArray();

			if (returnTypes.Length == 0) {
				throw new EvaluationTypeException("Unknown return type for array concat function (no arguments provided).");
			}

			EvaluationType? resultElemType;
			if (returnTypes[0].IterationResult() is EvaluationType firstArgIterType) {
				resultElemType = firstArgIterType;
				for (int i = 1; i < returnTypes.Length; i++) {
					EvaluationType? argElemType = returnTypes[i].IterationResult();
					if (argElemType is null) {
						resultElemType = null;
						break;
					}

					if (context.TryGetLeastUpperBoundType(resultElemType, argElemType, out EvaluationType? compatible)) {
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

			return resultElemType;
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationType resultElemType = GetResultElementType(environment.Context, args);

			List<EvaluationValue> results = new List<EvaluationValue>();

			for (int i = 0; i < args.Length; i++) {
				EvaluationValue arg = args[i].Evaluate(environment);

				foreach(EvaluationValue item in arg.Type.Iteration(arg) ?? throw new EvaluationTypeException($"Cannot iterate through value of type {arg.Type}")) {
					EvaluationValue castItem = resultElemType.Cast(item) ?? throw new EvaluationTypeException($"Cannot cast item of type {item.Type} to compatible type {resultElemType}.");
					results.Add(castItem);
				}
			}

			return ArrayEvaluationType.MakeArray(resultElemType, results);
		}
	}

	public class ArrayContainsFunction : AbstractFunction {

		public static readonly ArrayContainsFunction Instance = new ArrayContainsFunction();
		private ArrayContainsFunction() { }

		public override EvaluationName Name { get; } = "contains";
		public override string? Description { get; } = "Returns true of the array (or tuple) contains the value, otherwise false. Alternatively, if two strings are provided, returns true of the first string contains the second.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("arrayOrTuple", null, null),
				new EnvironmentFunctionArg("value", null, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("text", EvaluationTypes.STRING, null),
				new EnvironmentFunctionArg("substring", EvaluationTypes.STRING, null)
				)
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("arrayOrTuple", null, null),
					new EnvironmentFunctionArg("value", null, null)
					),
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("text", context.GetType<StringEvaluationType>(), null),
					new EnvironmentFunctionArg("substring", context.GetType<StringEvaluationType>(), null)
					)
			);
		}

		public sealed override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].GetReturnType();
			EvaluationType arg2Type = args[1].GetReturnType();
			if (arg1Type.IterationResult() is EvaluationType iterableType && EvaluationOps.EqualResult(arg2Type, iterableType) is EvaluationType equalsType && BoolEvaluationType.IsBool(equalsType)) {
				return context.GetType<BoolEvaluationType>();
			}
			else if (StringEvaluationType.AllString(arg1Type, arg2Type)) {
				return context.GetType<BoolEvaluationType>();
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for operands of type {arg1Type} and {arg2Type}.");
			}
		}

		private static EvaluationValue MakeResult(IEnvironment environment, bool result) {
			return new EvaluationValue(result, environment.GetType<BoolEvaluationType>());
		}

		public bool EvaluateBool(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);
			EvaluationValue b = args[1].Evaluate(environment);

			if (StringEvaluationType.TryGetString(a, out string? text) && StringEvaluationType.TryGetString(b, out string? searchTerm)) {
				return text.Contains(searchTerm);
			}
			else if (a.Type.Iteration(a)?.ToArray() is EvaluationValue[] iterables) {
				for (int i = 0; i < iterables.Length; i++) {
					EvaluationValue? equals = EvaluationOps.Equal(b, iterables[i]);
					if (equals.HasValue && BoolEvaluationType.TryGetBool(equals.Value, out bool isMatch) && isMatch) {
						return true;
					}
				}

				return false;
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for operands of type {a.Type} and {b.Type}.");
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			return MakeResult(environment, EvaluateBool(environment, args));
		}
	}

	public abstract class ArrayLogicalRedictionFunction : AbstractSingleArgFunction {

		//protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("arrayOrTuple", EvaluationTypes.BOOL.MakeArray(), null);
		protected override string? Warning => null;

		protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
			return new EnvironmentFunctionArg("arrayOrTuple", context.GetType<BoolEvaluationType>().MakeArray(), null);
		}

		public sealed override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
			EvaluationType argType = arg.GetReturnType();
			if (argType.IterationResult() is EvaluationType iterableType && BoolEvaluationType.IsBool(iterableType)) {
				return context.GetType<BoolEvaluationType>();
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for operands of type {argType}.");
			}
		}

		protected abstract bool Default { get; }
		protected abstract bool GotResult(bool value, out bool result);

		protected static EvaluationValue MakeResult(IEnvironment environment, bool result) {
			return new EvaluationValue(result, environment.GetType<BoolEvaluationType>());
		}

		public bool EvaluateBool(IEnvironment environment, EvaluationNode arg) {
			EvaluationValue a = arg.Evaluate(environment);

			if (a.Type.Iteration(a)?.ToArray() is EvaluationValue[] values) {
				for (int i = 0; i < values.Length; i++) {
					if (BoolEvaluationType.TryGetBool(values[i], out bool b)) {
						if (GotResult(b, out bool result)) { return result; }
					}
					else {
						throw new EvaluationTypeException($"Invalid element of type {values[i].Type} in {Name}.");
					}
				}

				return Default;
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for operands of type {a.Type}.");
			}
		}

		public sealed override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			return MakeResult(environment, EvaluateBool(environment, arg));
		}
	}

	public class ArrayAllFunction : ArrayLogicalRedictionFunction {

		public static readonly ArrayAllFunction Instance = new ArrayAllFunction();
		private ArrayAllFunction() { }

		public override EvaluationName Name { get; } = "all";
		public override string? Description { get; } = "Returns true if every entry of the array is true, otherwise false.";

		protected override bool Default { get; } = true;
		protected override bool GotResult(bool value, out bool result) {
			if (!value) {
				result = false;
				return true;
			}
			else {
				result = false;
				return false;
			}
		}
	}

	public class ArrayAnyFunction : ArrayLogicalRedictionFunction {

		public static readonly ArrayAnyFunction Instance = new ArrayAnyFunction();
		private ArrayAnyFunction() { }

		public override EvaluationName Name { get; } = "any";
		public override string? Description { get; } = "Returns true is any entry of the array is true, otherwise false.";

		protected override bool Default { get; } = false;
		protected override bool GotResult(bool value, out bool result) {
			if (value) {
				result = true;
				return true;
			}
			else {
				result = false;
				return false;
			}
		}
	}

	public class ArraySortFunction : AbstractFunction {

		public static readonly ArraySortFunction Instance = new ArraySortFunction();
		private ArraySortFunction() { }

		public override EvaluationName Name { get; } = "sort";
		public override string? Description { get; } = "Returns a sorted copy of the array (or tuple). If a keys array is provided, it must be the same length as the array to be sorted, and the sorting will use the sorted ordering of the keys array.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("arrayOrTuple", null, null),
				new EnvironmentFunctionArg("keys", null, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("arrayOrTuple", null, null)
				)
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("arrayOrTuple", null, null),
				new EnvironmentFunctionArg("keys", null, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("arrayOrTuple", null, null)
				)
		);
		}

		private static bool IsSortable(EvaluationContext context, EvaluationType elemType) {
			return EvaluationOps.LessThanResult(elemType, elemType) is EvaluationType lessThanType
				&& BoolEvaluationType.IsBool(lessThanType)
				&& EvaluationOps.GreaterThanResult(elemType, elemType) is EvaluationType greaterThanType
				&& BoolEvaluationType.IsBool(greaterThanType);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			if (!(args.Length == 1 || args.Length == 2)) {
				throw new EvaluationTypeException($"{Name} must take one or two iterable arguments (values [, keys ])."); // Better exception type?
			}

			EvaluationType[] argTypes = args.Select(a => a.GetReturnType()).ToArray();

			EvaluationType[] elemTypes = new EvaluationType[argTypes.Length];
			for(int i=0; i<argTypes.Length; i++) {
				elemTypes[i] = argTypes[i].IterationResult() ?? throw new EvaluationTypeException($"{Name} arguments must be iterable, not {string.Join(", ", argTypes.Select(a => a.ToString()))}.");
			}

			if (argTypes.Length == 1) {
				if (!IsSortable(context, elemTypes[0])) {
					throw new EvaluationTypeException($"Provided values of type {elemTypes[0]} are not sortable.");
				}
			}
			else { // argTypes.Length == 2
				if (!IsSortable(context, elemTypes[1])) {
					throw new EvaluationTypeException($"Provided keys of type {elemTypes[1]} are not sortable.");
				}
			}

			EvaluationType resultElemType = elemTypes[0];
			return resultElemType.MakeArray();
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue arg1 = args[0].Evaluate(environment);

			if(arg1.Type.IterationResult() is EvaluationType resultElemType && arg1.Type.Iteration(arg1)?.ToArray() is EvaluationValue[] array) {

				if (args.Length > 1) {
					EvaluationValue arg2 = args[1].Evaluate(environment);
					if (arg2.Type.Iteration(arg2)?.ToArray() is EvaluationValue[] keys) {
						if (keys.Length != array.Length) {
							throw new EvaluationCalculationException($"Length of values ({array.Length}) does not match length of keys ({keys.Length}) in {Name}.");
						}
						Array.Sort(keys, array, new EvaluationValueComparer(environment.Context));
					}
					else {
						throw new EvaluationTypeException($"Invalid keys of type {arg2.Type} to {Name}.");
					}
				}
				else {
					Array.Sort(array, new EvaluationValueComparer(environment.Context));
				}

				return ArrayEvaluationType.MakeArray(resultElemType, array);
			}
			else {
				throw new EvaluationTypeException($"Invalid values of type {arg1.Type} to {Name}.");
			}
		}

		private class EvaluationValueComparer : IComparer<EvaluationValue> {

			public EvaluationContext Context { get; }

			public EvaluationValueComparer(EvaluationContext context) {
				this.Context = context;
			}

			private static bool GetBool(EvaluationValue? value) {
				if (value.HasValue && BoolEvaluationType.TryGetBool(value.Value, out bool result)) {
					return result;
				}

				throw new EvaluationTypeException($"Required comparison operations for sorting not supported.");
			}

			public int Compare(EvaluationValue x, EvaluationValue y) {
				if(GetBool(EvaluationOps.LessThan(x, y))) {
					return -1;
				}
				else if(GetBool(EvaluationOps.GreaterThan(x, y))) {
					return 1;
				}
				else {
					return 0;
				}
			}
		}
	}

	public class ArrayReverseFunction : AbstractSingleArgFunction {

		public static readonly ArrayReverseFunction Instance = new ArrayReverseFunction();
		private ArrayReverseFunction() { }

		public override EvaluationName Name { get; } = "reverse";
		public override string? Description { get; } = "Returns a reversed copy of the array (no sorting is performed).";

		//protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("arrayOrTuple", null, null);
		protected override string? Warning => null;

		protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
			return new EnvironmentFunctionArg("arrayOrTuple", null, null);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
			EvaluationType argType = arg.GetReturnType();

			if(argType.IterationResult() is EvaluationType iterationType) {
				return iterationType.MakeArray();
			}
			else {
				throw new EvaluationTypeException($"Arguments for reverse must be iterable type, not {argType}.");
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			EvaluationValue argValue = arg.Evaluate(environment);

			if(argValue.Type.IterationResult() is EvaluationType iterationType && argValue.Type.Iteration(argValue)?.ToArray() is EvaluationValue[] iterables) {
				return ArrayEvaluationType.MakeArray(iterationType, iterables.Reverse().ToArray());
			}
			else {
				throw new EvaluationTypeException($"Invalid values argument of type {argValue.Type} to {Name}.");
			}
		}

	}

}
