using System;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public class LengthFunction : AbstractSingleArgFunction {

		public static readonly LengthFunction Instance = new LengthFunction();
		private LengthFunction() { }

		public override EvaluationName Name { get; } = "len";
		public override string? Description { get; } = "Returns the integer length of the argument. For arrays this is the number of entries, for strings the number of characters, real values return the floor, and bools return 1 for true and 0 for false.";

		protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("value", null, null);
		protected override string? Warning => null;

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode arg) {
			EvaluationType argType = arg.GetReturnType(typeSystem);
			if (IntEvaluationType.IsIntegral(argType)
				|| FloatEvaluationType.IsReal(argType)
				|| StringEvaluationType.IsString(argType)
				|| BoolEvaluationType.IsBool(argType)
				|| argType is CollectionEvaluationType) {
				return EvaluationTypes.INT;
			}
			else {
				throw new EvaluationTypeException($"Cannot take length of {argType} value.");
			}
		}

		private static EvaluationValue MakeResult(int result) {
			return new EvaluationValue(result, IntEvaluationType.Instance);
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			EvaluationValue a = arg.Evaluate(environment);

			if(a.Value is null) {
				throw new EvaluationCalculationException("Cannot take length of null value.");
			}
			else if (IntEvaluationType.TryGetInt(a, out int aInt)) {
				return MakeResult(aInt);
			}
			else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
				return MakeResult((int)aFloat);
			}
			else if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
				return MakeResult(aBool ? 1 : 0);
			}
			else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
				return MakeResult(aStr.Length);
			}
			else if (a.Type.Iteration(a) is IEnumerable<EvaluationType> iterable) {
				return MakeResult(iterable.Count());
			}
			else {
				throw new EvaluationTypeException($"Length not defined for value of type {a.Type}.");
			}
		}
	}

	public class ExistsFunction : AbstractSingleArgFunction {

		public static readonly ExistsFunction Instance = new ExistsFunction();
		private ExistsFunction() { }

		public override EvaluationName Name { get; } = "exists";
		public override string? Description { get; } = "Returns true if the argument evaluates to a non-null value (i.e. the variables are defined, and have valid values), otherwise false.";

		protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("value", null, null);
		protected override string? Warning => null;

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode arg) {
			return EvaluationTypes.BOOL;
		}

		private static EvaluationValue MakeResult(bool result) {
			return new EvaluationValue(result, BoolEvaluationType.Instance);
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			try {
				EvaluationValue a = arg.Evaluate(environment);
				if (StringEvaluationType.TryGetString(a, out string? aString)) {
					return MakeResult(aString.Length > 0);
				}
				else {
					return MakeResult(a.Value != null);
				}
			}
			catch (UndefinedVariableException) {
				return MakeResult(false);
			}
		}
	}

	public class TryFunction : AbstractSingleArgFunction {

		public static readonly TryFunction Instance = new TryFunction();
		private TryFunction() { }

		public override EvaluationName Name { get; } = "try";
		public override string? Description { get; } = "Returns true if the argument evaluates and produces a non-null value (i.e. all variables exist and the result is a valid value), otherwise false.";

		protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("value", null, null);
		protected override string? Warning => null;

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode arg) {
			return EvaluationTypes.BOOL;
		}

		private static EvaluationValue MakeResult(bool result) {
			return new EvaluationValue(result, BoolEvaluationType.Instance);
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			try {
				EvaluationValue a = arg.Evaluate(environment);
				if (StringEvaluationType.TryGetString(a, out string? aString)) {
					return MakeResult(aString.Length > 0);
				}
				else {
					return MakeResult(a.Value != null);
				}
			}
			catch (UndefinedVariableException) {
				return MakeResult(false);
			}
		}
	}

	public class RangeFunction : AbstractFunction {

		public static readonly RangeFunction Instance = new RangeFunction();
		private RangeFunction() { }

		public override EvaluationName Name { get; } = "range";
		public override string? Description { get; } = "Returns an array of integers that runs from start (or 0, if start is not provided) to end (exclusive, meaning the final value will be end-1). If (end-start) or end are less than 0, an empty array will be returned.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null, // "Invalid number of arguments provided to range function. May accept 1 or 2 arguments, {count} provided."
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("start", EvaluationTypes.INT, null),
				new EnvironmentFunctionArg("end", EvaluationTypes.INT, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("end", EvaluationTypes.INT, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode[] args) {
			return EvaluationTypes.INT.MakeArray();
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue[] argVals = args.Select(a => a.Evaluate(environment)).ToArray();

			if (argVals.Length == 2) {
				if (IntEvaluationType.TryGetInt(argVals[0], out int start) && IntEvaluationType.TryGetInt(argVals[1], out int end)) {
					EvaluationValue[] values = new EvaluationValue[Math.Max(0, end - start)];
					int counter = start;
					for (int i = 0; i < values.Length; i++) {
						values[i] = new EvaluationValue(counter, EvaluationTypes.INT);
						counter++;
					}
					return ArrayEvaluationType.MakeArray(EvaluationTypes.INT, values);
				}
			}
			else if (argVals.Length == 1) {
				if (IntEvaluationType.TryGetInt(argVals[0], out int end)) {
					EvaluationValue[] values = new EvaluationValue[Math.Max(0, end)];
					for (int i = 0; i < end; i++) {
						values[i] = new EvaluationValue(i, EvaluationTypes.INT);
					}
					return ArrayEvaluationType.MakeArray(EvaluationTypes.INT, values);
				}
			}

			throw new EvaluationCalculationException("Invalid arguments to range function. Must provide 1 or 2 integer values for [start,]end of range.");
		}
	}

}
