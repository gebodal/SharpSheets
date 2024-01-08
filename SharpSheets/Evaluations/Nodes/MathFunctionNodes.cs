using SharpSheets.Utilities;
using System;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class AbstractMinMaxFunction : AbstractFunction {

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null), true)
		);

		protected abstract double BaseValue { get; }
		protected abstract bool IsPreferred(double val, double runningBest);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			if (args.Length == 0) {
				throw new EvaluationTypeException($"{Name} must take a non-zero number of arguments.");
			}

			EvaluationType[] returnTypes = args.Select(a => a.ReturnType).Distinct().ToArray();

			if (returnTypes.Length == 1 && (returnTypes[0].IsArray || returnTypes[0].IsTuple)) {
				if (returnTypes[0].ElementType!.IsReal()) {
					return returnTypes[0].ElementType!;
				}
				else {
					throw new EvaluationTypeException($"Cannot take {Name} of array with type: {returnTypes[0]}");
				}
			}

			try {
				return EvaluationTypes.FindCommonNumericType(returnTypes) ?? throw new EvaluationTypeException($"{Name} must take a non-zero number of arguments.");
			}
			catch (EvaluationTypeException) {
				throw new EvaluationTypeException($"Cannot take {Name} of operands with type{(returnTypes.Length > 1 ? "s" : "")}: " + string.Join(", ", returnTypes.Select(t => t.ToString())));
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object[] argVals = EvaluationTypes.VerifyArray(args.Select(a => a.Evaluate(environment)).ToArray());

			bool fromArray = false;
			if (argVals.Length == 1 && EvaluationTypes.TryGetArray(argVals[0], out Array? array)) {
				argVals = array.Cast<object>().ToArray();
				fromArray = true;
			}

			if (argVals.Length == 0) {
				if (fromArray) {
					throw new EvaluationCalculationException($"Cannot take {Name} of array with zero elements.");
				}
				else {
					throw new EvaluationCalculationException($"Cannot take {Name} of zero arguments.");
				}
			}

			bool badTypes = false;

			object result = argVals[0];
			double runningBest = BaseValue;
			for (int i = 0; i < argVals.Length; i++) {
				bool preferred = false;
				double val;
				if (argVals[i] is int aInt) { val = aInt; }
				else if (argVals[i] is uint aUInt) { val = aUInt; }
				else if (argVals[i] is float aFloat) { val = aFloat; }
				else if (argVals[i] is UFloat aUFloat) { val = aUFloat; }
				else {
					badTypes = true;
					break;
				}

				preferred = IsPreferred(val, runningBest);


				if (preferred) {
					runningBest = val;
					result = argVals[i];
				}
			}

			if (badTypes) {
				Type[] types = argVals.Select(a => a.GetType()).Distinct().ToArray();
				string s = (types.Length > 1) ? "s" : "";
				if (fromArray) {
					throw new EvaluationTypeException($"Cannot take {Name} of array with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
				}
				else {
					throw new EvaluationTypeException($"Cannot take {Name} of arguments with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
				}
			}

			EvaluationType returnType = GetReturnType(args);

			if(returnType.DataType == typeof(int)) {
				if(EvaluationTypes.TryGetIntegral(result, out int intResult)) {
					return intResult;
				}
			}
			else if (returnType.DataType == typeof(float)) {
				if (EvaluationTypes.TryGetReal(result, out float floatResult)) {
					return floatResult;
				}
			}
			else {
				return result;
			}

			throw new EvaluationCalculationException($"Could not calculate {Name} for provided values.");
		}

	}

	public class MinVarFunction : AbstractMinMaxFunction {
		public static readonly MinVarFunction Instance = new MinVarFunction();
		private MinVarFunction() { }

		public sealed override EvaluationName Name { get; } = "min";
		public override string? Description { get; } = null;

		protected override double BaseValue { get { return double.MaxValue; } }
		protected override bool IsPreferred(double val, double runningBest) {
			return val < runningBest;
		}
	}

	public class MaxVarFunction : AbstractMinMaxFunction {
		public static readonly MaxVarFunction Instance = new MaxVarFunction();
		private MaxVarFunction() { }

		public sealed override EvaluationName Name { get; } = "max";
		public override string? Description { get; } = null;

		protected override double BaseValue { get { return double.MinValue; } }
		protected override bool IsPreferred(double val, double runningBest) {
			return val > runningBest;
		}
	}

	public class SumFunction : AbstractFunction {

		public static readonly SumFunction Instance = new SumFunction();
		private SumFunction() { }

		public override EvaluationName Name { get; } = "sum";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null), true)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			if (args.Length == 0) {
				throw new EvaluationTypeException($"{Name} must take a non-zero number of arguments.");
			}

			EvaluationType[] returnTypes = args.Select(a => a.ReturnType).Distinct().ToArray();

			if (returnTypes.Length == 1 && (returnTypes[0].IsArray || returnTypes[0].IsTuple)) {
				if (returnTypes[0].ElementType!.IsReal()) {
					return returnTypes[0].ElementType!;
				}
				else {
					throw new EvaluationTypeException($"Cannot take {Name} of array with type: {returnTypes[0]}");
				}
			}

			try {
				return EvaluationTypes.FindCommonNumericType(returnTypes) ?? throw new EvaluationTypeException($"{Name} must take a non-zero number of arguments.");
			}
			catch (EvaluationTypeException) {
				throw new EvaluationTypeException($"Cannot take {Name} of operands with type{(returnTypes.Length > 1 ? "s" : "")}: " + string.Join(", ", returnTypes.Select(t => t.ToString())));
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object[] argVals = EvaluationTypes.VerifyArray(args.Select(a => a.Evaluate(environment)).ToArray());

			bool fromArray = false;
			if (argVals.Length == 1 && EvaluationTypes.TryGetArray(argVals[0], out Array? array)) {
				argVals = array.Cast<object>().ToArray();
				fromArray = true;
			}

			if (argVals.Length == 0) {
				if (fromArray) {
					throw new EvaluationCalculationException($"Cannot take {Name} of array with zero elements.");
				}
				else {
					throw new EvaluationCalculationException($"Cannot take {Name} of zero arguments.");
				}
			}

			bool badTypes = false;
			double total = 0.0;
			foreach (object a in argVals) {
				if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
					total += aInt;
				}
				else if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
					total += aFloat;
				}
				else {
					badTypes = true;
					break;
				}
			}

			if (badTypes) {
				Type[] types = argVals.Select(a => a.GetType()).Distinct().ToArray();
				string s = (types.Length > 1) ? "s" : "";
				if (fromArray) {
					throw new EvaluationTypeException($"Cannot take {Name} of array with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
				}
				else {
					throw new EvaluationTypeException($"Cannot take {Name} of arguments with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
				}
			}

			EvaluationType returnType = GetReturnType(args);

			if (returnType.DataType == typeof(uint)) {
				return (uint)total;
			}
			else if (returnType.DataType == typeof(UFloat)) {
				return new UFloat((float)total);
			}
			else if (returnType.DataType == typeof(int)) {
				return (int)total;

			}
			else {
				return (float)total;
			}
		}
	}

	public class FloorFunction : AbstractFunction {

		public static readonly FloorFunction Instance = new FloorFunction();
		private FloorFunction() { }

		public override EvaluationName Name { get; } = "floor";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationType.FLOAT, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			return argType.IsReal() ? EvaluationType.INT : throw new EvaluationTypeException($"{Name} not defined for value of type {argType}.");
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
				return aInt;
			}
			else if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return (int)Math.Floor(aFloat);
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {EvaluationUtils.GetDataTypeName(a)}.");
			}
		}
	}

	public class CeilingFunction : AbstractFunction {

		public static readonly CeilingFunction Instance = new CeilingFunction();
		private CeilingFunction() { }

		public override EvaluationName Name { get; } = "ceil";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationType.FLOAT, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			return argType.IsReal() ? EvaluationType.INT : throw new EvaluationTypeException($"{Name} not defined for value of type {argType}.");
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
				return aInt;
			}
			else if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return (int)Math.Ceiling(aFloat);
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {EvaluationUtils.GetDataTypeName(a)}.");
			}
		}
	}

	public abstract class MathematicalFunction : AbstractFunction {

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("x", EvaluationType.FLOAT, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			return argType.IsReal() ? EvaluationType.FLOAT : throw new EvaluationTypeException($"{Name} is not defined for value of type {argType}.");
		}

		protected abstract double Calculate(float argument);

		public sealed override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return (float)Calculate(aFloat);
			}
			else {
				throw new EvaluationTypeException($"Mathematical functions are not defined for value of type {EvaluationUtils.GetDataTypeName(a)}.");
			}
		}
	}

	public class SquareRootFunction : MathematicalFunction {
		public static readonly SquareRootFunction Instance = new SquareRootFunction();
		private SquareRootFunction() { }

		public override EvaluationName Name { get; } = "sqrt";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Sqrt(argument);
	}

	public class SinFunction : MathematicalFunction {
		public static readonly SinFunction Instance = new SinFunction();
		private SinFunction() { }

		public override EvaluationName Name { get; } = "sin";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Sin(argument);
	}
	public class CosFunction : MathematicalFunction {
		public static readonly CosFunction Instance = new CosFunction();
		private CosFunction() { }

		public override EvaluationName Name { get; } = "cos";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Cos(argument);
	}
	public class TanFunction : MathematicalFunction {
		public static readonly TanFunction Instance = new TanFunction();
		private TanFunction() { }

		public override EvaluationName Name { get; } = "tan";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Tan(argument);
	}

	public class AsinFunction : MathematicalFunction {
		public static readonly AsinFunction Instance = new AsinFunction();
		private AsinFunction() { }

		public override EvaluationName Name { get; } = "asin";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Asin(argument);
	}
	public class AcosFunction : MathematicalFunction {
		public static readonly AcosFunction Instance = new AcosFunction();
		private AcosFunction() { }

		public override EvaluationName Name { get; } = "acos";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Acos(argument);
	}
	public class AtanFunction : MathematicalFunction {
		public static readonly AtanFunction Instance = new AtanFunction();
		private AtanFunction() { }

		public override EvaluationName Name { get; } = "atan";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Atan(argument);
	}

	public class Atan2Function : AbstractFunction {

		public static readonly Atan2Function Instance = new Atan2Function();
		private Atan2Function() { }

		public override EvaluationName Name { get; } = "atan2";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("y", EvaluationType.FLOAT, null),
				new EnvironmentFunctionArg("x", EvaluationType.FLOAT, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].ReturnType;
			EvaluationType arg2Type = args[1].ReturnType;
			return (arg1Type.IsReal() && arg2Type.IsReal()) ? EvaluationType.FLOAT : throw new EvaluationTypeException($"Atan2 not defined for operands of type {arg1Type} and {arg2Type}.");
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);
			object? b = args[1].Evaluate(environment);

			if (EvaluationTypes.TryGetReal(a, out float afloat) && EvaluationTypes.TryGetReal(b, out float bfloat)) {
				return (float)Math.Atan2(afloat, bfloat);
			}
			else {
				throw new EvaluationTypeException($"Atan2 not defined for operands of type {EvaluationUtils.GetDataTypeName(a)} and {EvaluationUtils.GetDataTypeName(b)}.");
			}
		}
	}

	public class SinhFunction : MathematicalFunction {
		public static readonly SinhFunction Instance = new SinhFunction();
		private SinhFunction() { }

		public override EvaluationName Name { get; } = "sinh";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Sinh(argument);
	}
	public class CoshFunction : MathematicalFunction {
		public static readonly CoshFunction Instance = new CoshFunction();
		private CoshFunction() { }

		public override EvaluationName Name { get; } = "cosh";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Cosh(argument);
	}
	public class TanhFunction : MathematicalFunction {
		public static readonly TanhFunction Instance = new TanhFunction();
		private TanhFunction() { }

		public override EvaluationName Name { get; } = "tanh";
		public override string? Description { get; } = null;

		protected override double Calculate(float argument) => Math.Tanh(argument);
	}

	public class LerpFunction : AbstractFunction {

		public static readonly LerpFunction Instance = new LerpFunction();
		private LerpFunction() { }

		public override EvaluationName Name { get; } = "lerp";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("a", EvaluationType.FLOAT, null),
				new EnvironmentFunctionArg("b", EvaluationType.FLOAT, null),
				new EnvironmentFunctionArg("t", EvaluationType.FLOAT, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].ReturnType;
			EvaluationType arg2Type = args[1].ReturnType;
			EvaluationType arg3Type = args[2].ReturnType;
			return (arg1Type.IsReal() && arg2Type.IsReal() && arg3Type.IsReal()) ? EvaluationType.FLOAT : throw new EvaluationTypeException($"lerp not defined for operands of type {arg1Type}, {arg2Type}, and {arg3Type}.");
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? aObj = args[0].Evaluate(environment);
			object? bObj = args[1].Evaluate(environment);
			object? tObj = args[2].Evaluate(environment);

			if (EvaluationTypes.TryGetReal(aObj, out float a) && EvaluationTypes.TryGetReal(bObj, out float b) && EvaluationTypes.TryGetReal(tObj, out float t)) {
				return MathUtils.Lerp(a, b, t);
			}
			else {
				throw new EvaluationTypeException($"lerp not defined for operands of type {EvaluationUtils.GetDataTypeName(aObj)}, {EvaluationUtils.GetDataTypeName(bObj)}, and {EvaluationUtils.GetDataTypeName(tObj)}.");
			}
		}
	}

}
