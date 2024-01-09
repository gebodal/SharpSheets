using System;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public class LengthFunction : AbstractFunction {

		public static readonly LengthFunction Instance = new LengthFunction();
		private LengthFunction() { }

		public override EvaluationName Name { get; } = "len";
		public override string? Description { get; } = "Returns the integer length of the argument. For arrays this is the number of entries, for strings the number of characters, real values return the floor, and bools return 1 for true and 0 for false.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			return EvaluationType.INT;
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if(a is null) {
				throw new EvaluationCalculationException("Cannot take length of null value.");
			}
			else if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
				return aInt;
			}
			else if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return (int)aFloat;
			}
			else if (a is bool aBool) {
				return aBool ? 1 : 0;
			}
			else if (a is string aStr) {
				return aStr.Length;
			}
			else if (a.GetType().IsArray && a is Array aArr && aArr.Rank == 1) {
				return aArr.Length;
			}
			else {
				throw new EvaluationTypeException($"Length not defined for value of type {a.GetType().Name}.");
			}
		}
	}

	public class ExistsFunction : AbstractFunction {

		public static readonly ExistsFunction Instance = new ExistsFunction();
		private ExistsFunction() { }

		public override EvaluationName Name { get; } = "exists";
		public override string? Description { get; } = "Returns true if the argument evaluates to a non-null value (i.e. the variables are defined, and have valid values), otherwise false.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			return EvaluationType.BOOL;
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			try {
				object? a = args[0].Evaluate(environment);
				if (a is string aString) {
					return aString.Length > 0;
				}
				else {
					return a != null;
				}
			}
			catch (UndefinedVariableException) {
				return false;
			}
		}
	}

	public class TryFunction : AbstractFunction {

		public static readonly TryFunction Instance = new TryFunction();
		private TryFunction() { }

		public override EvaluationName Name { get; } = "try";
		public override string? Description { get; } = "Returns true if the argument evaluates and produces a non-null value (i.e. all variables exist and the result is a valid value), otherwise false.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			return EvaluationType.BOOL;
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			try {
				object? a = args[0].Evaluate(environment);
				if (a is string aString) {
					return aString.Length > 0;
				}
				else {
					return a != null;
				}
			}
			catch (EvaluationException) {
				return false;
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
				new EnvironmentFunctionArg("start", EvaluationType.INT, null),
				new EnvironmentFunctionArg("end", EvaluationType.INT, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("end", EvaluationType.INT, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			return EvaluationType.INT.MakeArray();
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object[] argVals = EvaluationTypes.VerifyArray(args.Select(a => a.Evaluate(environment)).ToArray());

			if (argVals.Length == 2) {
				if (EvaluationTypes.TryGetIntegral(argVals[0], out int start) && EvaluationTypes.TryGetIntegral(argVals[1], out int end)) {
					int[] values = new int[Math.Max(0, end - start)];
					int counter = start;
					for (int i = 0; i < values.Length; i++) {
						values[i] = counter;
						counter++;
					}
					return values;
				}
			}
			else if (argVals.Length == 1) {
				if (EvaluationTypes.TryGetIntegral(argVals[0], out int end)) {
					int[] values = new int[Math.Max(0, end)];
					for (int i = 0; i < end; i++) {
						values[i] = i;
					}
					return values;
				}
			}

			throw new EvaluationCalculationException("Invalid arguments to range function. Must provide 1 or 2 integer values for [start,]end of range.");
		}
	}

}
