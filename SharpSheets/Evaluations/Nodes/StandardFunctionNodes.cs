using System;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public class LengthFunction : AbstractSingleArgFunction {

		public static readonly LengthFunction Instance = new LengthFunction();
		private LengthFunction() { }

		public override EvaluationName Name { get; } = "len";
		public override string? Description { get; } = "Returns the integer length of the argument. For arrays this is the number of entries, for strings the number of characters, real values return the floor, and bools return 1 for true and 0 for false.";

		//protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("value", null, null);
		protected override string? Warning => null;

		protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
			return new EnvironmentFunctionArg("value", null, null);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
			EvaluationType argType = arg.GetReturnType();
			if (IntEvaluationType.IsIntegral(argType)
				|| FloatEvaluationType.IsReal(argType)
				|| StringEvaluationType.IsString(argType)
				|| BoolEvaluationType.IsBool(argType)
				|| argType.IterationResult() is not null) {
				return argType.Context.GetType<IntEvaluationType>();
			}
			else {
				throw new EvaluationTypeException($"Cannot take length of {argType} value.");
			}
		}

		private static EvaluationValue MakeResult(IEnvironment environment, int result) {
			return new EvaluationValue(result, environment.Context.GetType<IntEvaluationType>());
		}

		private static int EvaluateInt(IEnvironment environment, EvaluationNode arg) {
			EvaluationValue a = arg.Evaluate(environment);

			if (a.Value is null) {
				throw new EvaluationCalculationException("Cannot take length of null value.");
			}
			else if (IntEvaluationType.TryGetInt(a, out int aInt)) {
				return aInt;
			}
			else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
				return (int)aFloat;
			}
			else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
				return aStr.Length;
			}
			else if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
				return aBool ? 1 : 0;
			}
			else if (a.Type.Iteration(a) is IEnumerable<EvaluationValue> iterable) {
				return iterable.Count();
			}
			else {
				//Console.WriteLine($"Arg type {arg.GetReturnType()}, value type: {a.Type}, iteration result: {a.Type.IterationResult()}, iteration: {a.Type.Iteration(a)}");
				throw new EvaluationTypeException($"Length not defined for value of type {a.Type}.");
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			return MakeResult(environment, EvaluateInt(environment, arg));
		}
	}

	public class ExistsFunction : AbstractSingleArgFunction {

		public static readonly ExistsFunction Instance = new ExistsFunction();
		private ExistsFunction() { }

		public override EvaluationName Name { get; } = "exists";
		public override string? Description { get; } = "Returns true if the argument evaluates to a non-null value (i.e. the variables are defined, and have valid values), otherwise false.";

		//protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("value", null, null);
		protected override string? Warning => null;

		protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
			return new EnvironmentFunctionArg("value", null, null);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
			return context.GetType<BoolEvaluationType>();
		}

		private static EvaluationValue MakeResult(IEnvironment environment, bool result) {
			return new EvaluationValue(result, environment.Context.GetType<BoolEvaluationType>());
		}

		private static bool EvaluateBool(IEnvironment environment, EvaluationNode arg) {
			try {
				EvaluationValue a = arg.Evaluate(environment);
				if (StringEvaluationType.TryGetString(a, out string? aString)) {
					return aString.Length > 0;
				}
				else {
					return a.Value != null;
				}
			}
			catch (UndefinedVariableException) {
				return false;
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			return MakeResult(environment, EvaluateBool(environment, arg));
		}
	}

	public class TryFunction : AbstractSingleArgFunction {

		public static readonly TryFunction Instance = new TryFunction();
		private TryFunction() { }

		public override EvaluationName Name { get; } = "try";
		public override string? Description { get; } = "Returns true if the argument evaluates and produces a non-null value (i.e. all variables exist and the result is a valid value), otherwise false.";

		//protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("value", null, null);
		protected override string? Warning => null;

		protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
			return new EnvironmentFunctionArg("value", null, null);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
			return context.GetType<BoolEvaluationType>();
		}

		private static EvaluationValue MakeResult(IEnvironment environment, bool result) {
			return new EvaluationValue(result, environment.Context.GetType<BoolEvaluationType>());
		}

		public static bool EvaluateBool(IEnvironment environment, EvaluationNode arg) {
			try {
				EvaluationValue a = arg.Evaluate(environment);
				if (StringEvaluationType.TryGetString(a, out string? aString)) {
					return aString.Length > 0;
				}
				else {
					return a.Value != null;
				}
			}
			catch (UndefinedVariableException) {
				return false;
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			return MakeResult(environment, EvaluateBool(environment, arg));
		}
	}

	public class RangeFunction : AbstractFunction {

		public static readonly RangeFunction Instance = new RangeFunction();
		private RangeFunction() { }

		public override EvaluationName Name { get; } = "range";
		public override string? Description { get; } = "Returns an array of integers that runs from start (or 0, if start is not provided) to end (exclusive, meaning the final value will be end-1). If (end-start) or end are less than 0, an empty array will be returned.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null, // "Invalid number of arguments provided to range function. May accept 1 or 2 arguments, {count} provided."
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("start", EvaluationTypes.INT, null),
				new EnvironmentFunctionArg("end", EvaluationTypes.INT, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("end", EvaluationTypes.INT, null)
				)
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			EvaluationType intType = context.GetType<IntEvaluationType>();

			return new EnvironmentFunctionArguments(null, // "Invalid number of arguments provided to range function. May accept 1 or 2 arguments, {count} provided."
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("start", intType, null),
					new EnvironmentFunctionArg("end", intType, null)
					),
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("end", intType, null)
					)
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			return context.GetType<IntEvaluationType>().MakeArray();
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue[] argVals = args.Select(a => a.Evaluate(environment)).ToArray();

			if (argVals.Length == 2) {
				if (IntEvaluationType.TryGetInt(argVals[0], out int start) && IntEvaluationType.TryGetInt(argVals[1], out int end)) {
					EvaluationValue[] values = new EvaluationValue[Math.Max(0, end - start)];
					int counter = start;
					for (int i = 0; i < values.Length; i++) {
						values[i] = new EvaluationValue(counter, environment.Context.GetType<IntEvaluationType>());
						counter++;
					}
					return ArrayEvaluationType.MakeArray(environment.Context.GetType<IntEvaluationType>(), values);
				}
			}
			else if (argVals.Length == 1) {
				if (IntEvaluationType.TryGetInt(argVals[0], out int end)) {
					EvaluationValue[] values = new EvaluationValue[Math.Max(0, end)];
					for (int i = 0; i < end; i++) {
						values[i] = new EvaluationValue(i, environment.Context.GetType<IntEvaluationType>());
					}
					return ArrayEvaluationType.MakeArray(environment.Context.GetType<IntEvaluationType>(), values);
				}
			}

			throw new EvaluationCalculationException("Invalid arguments to range function. Must provide 1 or 2 integer values for [start,]end of range.");
		}
	}

}
