using SharpSheets.Utilities;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class AbstractMinMaxFunction : AbstractFunction {

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

		protected abstract EvaluationType? IsPreferredResult(EvaluationType type);
		protected abstract EvaluationValue? IsPreferred(EvaluationValue val, EvaluationValue runningBest, EvaluationType resultType);

		protected bool IsComparable(EvaluationType type) {
			EvaluationType? comparisonType = IsPreferredResult(type);
			if (comparisonType is not null && BoolEvaluationType.IsBool(comparisonType)) {
				return true;
			}
			else {
				return false;
			}
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			if (args.Length == 0) {
				throw new EvaluationTypeException($"{Name} must take a non-zero number of arguments.");
			}

			EvaluationType[] returnTypes = args.Select(a => a.GetReturnType()).Distinct().ToArray();

			if (args.Length == 1 && returnTypes[0].IterationResult() is EvaluationType iterationType) {
				if (IsComparable(iterationType)) {
					return iterationType;
				}
				else {
					throw new EvaluationTypeException($"{iterationType} does not support the necessary comparison operations for {Name} function.");
				}
			}
			else if (context.TryGetLeastUpperBoundType(returnTypes, out EvaluationType? compatible)) {
				if (IsComparable(compatible)) {
					return compatible;
				}
				else {
					throw new EvaluationTypeException($"{compatible} does not support the necessary comparison operations for {Name} function.");
				}
			}
			else {
				throw new EvaluationTypeException($"Cannot take {Name} of operands with type{(returnTypes.Length > 1 ? "s" : "")}: " + string.Join(", ", returnTypes.Select(t => t.ToString())));
			}
		}

		private bool IsPreferredCalculation(EvaluationValue val, EvaluationValue runningBest, EvaluationType resultType) {
			if (IsPreferred(val, runningBest, resultType) is EvaluationValue preferredValue && BoolEvaluationType.TryGetBool(preferredValue, out bool preferred)) {
				return preferred;
			}
			else {
				throw new EvaluationTypeException($"Cannot compare values of types {val.Type} and {runningBest.Type} for {Name} function.");
			}
		}

		protected EvaluationValue GetPreferred(EvaluationValue[] values, EvaluationType resultType) {
			EvaluationValue result = values[0];

			for (int i = 1; i < values.Length; i++) {
				if (IsPreferredCalculation(values[i], result, resultType)) {
					result = values[i];
				}
			}

			return result;
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue[] argVals = args.Select(a => a.Evaluate(environment)).ToArray();

			if (argVals.Length == 0) {
				throw new EvaluationCalculationException($"Cannot take {Name} of zero arguments.");
			}

			if (argVals.Length == 1 && argVals[0].Type.IterationResult() is EvaluationType resultType && argVals[0].Type.Iteration(argVals[0])?.ToArray() is EvaluationValue[] iterables) {
				if (iterables.Length == 0) {
					throw new EvaluationCalculationException($"Cannot take {Name} of array with zero elements.");
				}

				EvaluationValue result1 = GetPreferred(iterables, resultType);

				return result1;
			}
			else if (environment.Context.TryGetLeastUpperBoundType(argVals.Select(v => v.Type), out EvaluationType? compatible)) {
				EvaluationValue result2 = GetPreferred(argVals, compatible);

				return result2;
			}
			else {
				EvaluationType[] types = argVals.Select(a => a.Type).Distinct().ToArray();
				string s = (types.Length > 1) ? "s" : "";
				throw new EvaluationTypeException($"Cannot take {Name} of arguments with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
			}
		}

	}

	public class MinVarFunction : AbstractMinMaxFunction {
		public static readonly MinVarFunction Instance = new MinVarFunction();
		private MinVarFunction() { }

		public sealed override EvaluationName Name { get; } = "min";
		public override string? Description { get; } = "Returns the minimum of the arguments, which must be of a type which support less than comparison. The return type will be the common type between the arguments.";

		protected override EvaluationType? IsPreferredResult(EvaluationType type) {
			return type.LessThanResult(type);
		}

		protected override EvaluationValue? IsPreferred(EvaluationValue val, EvaluationValue runningBest, EvaluationType resultType) {
			return resultType.LessThan(val, runningBest);
		}
	}

	public class MaxVarFunction : AbstractMinMaxFunction {
		public static readonly MaxVarFunction Instance = new MaxVarFunction();
		private MaxVarFunction() { }

		public sealed override EvaluationName Name { get; } = "max";
		public override string? Description { get; } = "Returns the maximum of the arguments, which must be of a type which support greater than comparison. The return type will be the common type between the arguments.";

		protected override EvaluationType? IsPreferredResult(EvaluationType type) {
			return type.GreaterThanResult(type);
		}

		protected override EvaluationValue? IsPreferred(EvaluationValue val, EvaluationValue runningBest, EvaluationType resultType) {
			return resultType.GreaterThan(val, runningBest);
		}
	}

	public class SumFunction : AbstractFunction {

		public static readonly SumFunction Instance = new SumFunction();
		private SumFunction() { }

		public override EvaluationName Name { get; } = "sum";
		public override string? Description { get; } = "Returns the sum of the arguments, which must be of a type which supports addition. The return type will be the common type between the arguments.";

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

		protected static bool TryIsSummable(EvaluationContext context, EvaluationType type, [NotNullWhen(true)] out EvaluationType? sumType) {
			EvaluationType? addType = type.AddResult(type);
			if (addType is not null && context.TryGetLeastUpperBoundType(type, addType, out EvaluationType? finalSumType)) {
				sumType = finalSumType;
				return true;
			}
			else {
				sumType = null;
				return false;
			}
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			if (args.Length == 0) {
				throw new EvaluationTypeException($"{Name} must take a non-zero number of arguments.");
			}

			EvaluationType[] returnTypes = args.Select(a => a.GetReturnType()).Distinct().ToArray();

			if (args.Length == 1 && returnTypes[0].IterationResult() is EvaluationType iterationType) {
				if (TryIsSummable(context, iterationType, out EvaluationType? resultType)) {
					return resultType;
				}
				else {
					throw new EvaluationTypeException($"{iterationType} does not support the necessary addition operations for {Name} function.");
				}
			}
			else if (context.TryGetLeastUpperBoundType(returnTypes, out EvaluationType? compatible)) {
				if (TryIsSummable(context, compatible, out EvaluationType? resultType)) {
					return resultType;
				}
				else {
					throw new EvaluationTypeException($"{compatible} does not support the necessary addition operations for {Name} function.");
				}
			}
			else {
				throw new EvaluationTypeException($"Cannot take {Name} of operands with type{(returnTypes.Length > 1 ? "s" : "")}: " + string.Join(", ", returnTypes.Select(t => t.ToString())));
			}
		}

		protected EvaluationValue PerformSum(EvaluationValue[] values, EvaluationType resultType) {
			EvaluationValue sum = values[0];

			for (int i = 1; i < values.Length; i++) {
				sum = resultType.Add(sum, values[i]) ?? throw new EvaluationTypeException($"Cannot add values of types {sum.Type} and {values[i].Type} in {Name} function.");
			}

			return resultType.Cast(sum) ?? throw new EvaluationTypeException($"Cannot convert sum value of type {sum.Type} into compatible type {resultType}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue[] argVals = args.Select(a => a.Evaluate(environment)).ToArray();

			if (argVals.Length == 0) {
				throw new EvaluationCalculationException($"Cannot take {Name} of zero arguments.");
			}

			if (argVals.Length == 1 && argVals[0].Type.IterationResult() is EvaluationType resultType && argVals[0].Type.Iteration(argVals[0])?.ToArray() is EvaluationValue[] iterables) {
				if (iterables.Length == 0) {
					throw new EvaluationCalculationException($"Cannot take {Name} of array with zero elements.");
				}

				EvaluationValue result1 = PerformSum(iterables, resultType);

				return result1;
			}
			else if (environment.Context.TryGetLeastUpperBoundType(argVals.Select(v => v.Type), out EvaluationType? compatible)) { // TODO Not sure if this is right... don't we need to check Add result type?
				EvaluationValue result2 = PerformSum(argVals, compatible);

				return result2;
			}
			else {
				EvaluationType[] types = argVals.Select(a => a.Type).Distinct().ToArray();
				string s = (types.Length > 1) ? "s" : "";
				throw new EvaluationTypeException($"Cannot take {Name} of arguments with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
			}
		}
	}

	public class FloorFunction : AbstractSingleArgFunction {

		public static readonly FloorFunction Instance = new FloorFunction();
		private FloorFunction() { }

		public override EvaluationName Name { get; } = "floor";
		public override string? Description { get; } = "Returns the integer floor of the argument.";

		//protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("value", EvaluationTypes.FLOAT, null);
		protected override string? Warning => null;

		protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
			return new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
			EvaluationType argType = arg.GetReturnType();
			return FloatEvaluationType.IsReal(argType) ? context.GetType<IntEvaluationType>() : throw new EvaluationTypeException($"{Name} not defined for value of type {argType}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			EvaluationValue a = arg.Evaluate(environment);

			if (IntEvaluationType.TryGetInt(a, out int aInt)) {
				return new EvaluationValue(aInt, environment.GetType<IntEvaluationType>());
			}
			else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
				return new EvaluationValue((int)Math.Floor(aFloat), environment.GetType<IntEvaluationType>());
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {a.Type}.");
			}
		}
	}

	public class CeilingFunction : AbstractSingleArgFunction {

		public static readonly CeilingFunction Instance = new CeilingFunction();
		private CeilingFunction() { }

		public override EvaluationName Name { get; } = "ceil";
		public override string? Description { get; } = "Returns the integer ceiling of the argument.";

		//protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("value", EvaluationTypes.FLOAT, null);
		protected override string? Warning => null;

		protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
			return new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
			EvaluationType argType = arg.GetReturnType();
			return FloatEvaluationType.IsReal(argType) ? context.GetType<IntEvaluationType>() : throw new EvaluationTypeException($"{Name} not defined for value of type {argType}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			EvaluationValue a = arg.Evaluate(environment);

			if (IntEvaluationType.TryGetInt(a, out int aInt)) {
				return new EvaluationValue(aInt, environment.GetType<IntEvaluationType>());
			}
			else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
				return new EvaluationValue((int)Math.Ceiling(aFloat), environment.GetType<IntEvaluationType>());
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {a.Type}.");
			}
		}
	}

	public class AbsoluteFunction : AbstractFunction {

		public static readonly AbsoluteFunction Instance = new AbsoluteFunction();
		private AbsoluteFunction() { }

		public override EvaluationName Name { get; } = "abs";
		public override string? Description { get; } = "Returns the absolute value of the argument.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.FLOAT, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.INT, null))
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<IntEvaluationType>(), null))
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType argType = args[0].GetReturnType();
			if (IntEvaluationType.IsIntegral(argType)) {
				return context.GetType<UIntEvaluationType>();
			}
			else if (FloatEvaluationType.IsReal(argType)) {
				return context.GetType<UFloatEvaluationType>();
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {argType}.");
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);

			if (IntEvaluationType.TryGetInt(a, out int aInt)) {
				return new EvaluationValue((uint)Math.Abs(aInt), environment.GetType<UIntEvaluationType>());
			}
			else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
				return new EvaluationValue(new UFloat(Math.Abs(aFloat)), environment.GetType<UFloatEvaluationType>());
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {a.Type}.");
			}
		}
	}

	public abstract class MathematicalFunction : AbstractSingleArgFunction {

		//protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("x", EvaluationTypes.FLOAT, null);
		protected override string? Warning => null;

		protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
			return new EnvironmentFunctionArg("x", context.GetType<FloatEvaluationType>(), null);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
			EvaluationType argType = arg.GetReturnType();
			return FloatEvaluationType.IsReal(argType) ? context.GetType<FloatEvaluationType>() : throw new EvaluationTypeException($"{Name} is not defined for value of type {argType}.");
		}

		protected abstract double Calculate(float argument);

		public sealed override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			EvaluationValue a = arg.Evaluate(environment);

			if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
				return new EvaluationValue((float)Calculate(aFloat), environment.GetType<FloatEvaluationType>());
			}
			else {
				throw new EvaluationTypeException($"Mathematical functions are not defined for value of type {a.Type}.");
			}
		}
	}

	public class SquareRootFunction : MathematicalFunction {
		public static readonly SquareRootFunction Instance = new SquareRootFunction();
		private SquareRootFunction() { }

		public override EvaluationName Name { get; } = "sqrt";
		public override string? Description { get; } = "Returns the square root of the argument.";

		protected override double Calculate(float argument) => Math.Sqrt(argument);
	}

	public class SinFunction : MathematicalFunction {
		public static readonly SinFunction Instance = new SinFunction();
		private SinFunction() { }

		public override EvaluationName Name { get; } = "sin";
		public override string? Description { get; } = "Returns the sine of the argument.";

		protected override double Calculate(float argument) => Math.Sin(argument);
	}
	public class CosFunction : MathematicalFunction {
		public static readonly CosFunction Instance = new CosFunction();
		private CosFunction() { }

		public override EvaluationName Name { get; } = "cos";
		public override string? Description { get; } = "Returns the cosine of the argument.";

		protected override double Calculate(float argument) => Math.Cos(argument);
	}
	public class TanFunction : MathematicalFunction {
		public static readonly TanFunction Instance = new TanFunction();
		private TanFunction() { }

		public override EvaluationName Name { get; } = "tan";
		public override string? Description { get; } = "Returns the tangent of the argument.";

		protected override double Calculate(float argument) => Math.Tan(argument);
	}

	public class AsinFunction : MathematicalFunction {
		public static readonly AsinFunction Instance = new AsinFunction();
		private AsinFunction() { }

		public override EvaluationName Name { get; } = "asin";
		public override string? Description { get; } = "Returns the arcsine of the argument.";

		protected override double Calculate(float argument) => Math.Asin(argument);
	}
	public class AcosFunction : MathematicalFunction {
		public static readonly AcosFunction Instance = new AcosFunction();
		private AcosFunction() { }

		public override EvaluationName Name { get; } = "acos";
		public override string? Description { get; } = "Returns the arccosine of the argument.";

		protected override double Calculate(float argument) => Math.Acos(argument);
	}
	public class AtanFunction : MathematicalFunction {
		public static readonly AtanFunction Instance = new AtanFunction();
		private AtanFunction() { }

		public override EvaluationName Name { get; } = "atan";
		public override string? Description { get; } = "Returns the arctangent of the argument.";

		protected override double Calculate(float argument) => Math.Atan(argument);
	}

	public class Atan2Function : AbstractFunction {

		public static readonly Atan2Function Instance = new Atan2Function();
		private Atan2Function() { }

		public override EvaluationName Name { get; } = "atan2";
		public override string? Description { get; } = "The two-argument arctangent function.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("y", EvaluationTypes.FLOAT, null),
				new EnvironmentFunctionArg("x", EvaluationTypes.FLOAT, null)
				)
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("y", context.GetType<FloatEvaluationType>(), null),
					new EnvironmentFunctionArg("x", context.GetType<FloatEvaluationType>(), null)
				)
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].GetReturnType();
			EvaluationType arg2Type = args[1].GetReturnType();
			return (FloatEvaluationType.IsReal(arg1Type) && FloatEvaluationType.IsReal(arg2Type)) ? context.GetType<FloatEvaluationType>() : throw new EvaluationTypeException($"Atan2 not defined for operands of type {arg1Type} and {arg2Type}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);
			EvaluationValue b = args[1].Evaluate(environment);

			if (FloatEvaluationType.TryGetFloat(a, out float afloat) && FloatEvaluationType.TryGetFloat(b, out float bfloat)) {
				return new EvaluationValue((float)Math.Atan2(afloat, bfloat), environment.GetType<FloatEvaluationType>());
			}
			else {
				throw new EvaluationTypeException($"Atan2 not defined for operands of type {a.Type} and {b.Type}.");
			}
		}
	}

	public class SinhFunction : MathematicalFunction {
		public static readonly SinhFunction Instance = new SinhFunction();
		private SinhFunction() { }

		public override EvaluationName Name { get; } = "sinh";
		public override string? Description { get; } = "Returns the hyperbolic sine of the argument.";

		protected override double Calculate(float argument) => Math.Sinh(argument);
	}
	public class CoshFunction : MathematicalFunction {
		public static readonly CoshFunction Instance = new CoshFunction();
		private CoshFunction() { }

		public override EvaluationName Name { get; } = "cosh";
		public override string? Description { get; } = "Returns the hyperbolic cosine of the argument.";

		protected override double Calculate(float argument) => Math.Cosh(argument);
	}
	public class TanhFunction : MathematicalFunction {
		public static readonly TanhFunction Instance = new TanhFunction();
		private TanhFunction() { }

		public override EvaluationName Name { get; } = "tanh";
		public override string? Description { get; } = "Returns the hyperbolic tangent of the argument.";

		protected override double Calculate(float argument) => Math.Tanh(argument);
	}

	public class LerpFunction : AbstractFunction {

		public static readonly LerpFunction Instance = new LerpFunction();
		private LerpFunction() { }

		public override EvaluationName Name { get; } = "lerp";
		public override string? Description { get; } = "Returns the linear interpolation of the values a and b at \"time\" t.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("a", EvaluationTypes.FLOAT, null),
				new EnvironmentFunctionArg("b", EvaluationTypes.FLOAT, null),
				new EnvironmentFunctionArg("t", EvaluationTypes.FLOAT, null)
				)
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("a", context.GetType<FloatEvaluationType>(), null),
					new EnvironmentFunctionArg("b", context.GetType<FloatEvaluationType>(), null),
					new EnvironmentFunctionArg("t", context.GetType<FloatEvaluationType>(), null)
				)
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].GetReturnType();
			EvaluationType arg2Type = args[1].GetReturnType();
			EvaluationType arg3Type = args[2].GetReturnType();
			return FloatEvaluationType.AllReal(arg1Type, arg2Type, arg3Type) ? context.GetType<FloatEvaluationType>() : throw new EvaluationTypeException($"lerp not defined for operands of type {arg1Type}, {arg2Type}, and {arg3Type}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue aVal = args[0].Evaluate(environment);
			EvaluationValue bVal = args[1].Evaluate(environment);
			EvaluationValue tVal = args[2].Evaluate(environment);

			if (FloatEvaluationType.TryGetFloat(aVal, out float a) && FloatEvaluationType.TryGetFloat(bVal, out float b) && FloatEvaluationType.TryGetFloat(tVal, out float t)) {
				return new EvaluationValue(MathUtils.Lerp(a, b, t), environment.GetType<FloatEvaluationType>());
			}
			else {
				throw new EvaluationTypeException($"lerp not defined for operands of type {aVal.Type}, {bVal.Type}, and {tVal.Type}.");
			}
		}
	}

}
