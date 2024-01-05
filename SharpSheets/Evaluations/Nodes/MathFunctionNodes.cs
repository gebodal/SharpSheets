using SharpSheets.Utilities;
using System;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class AbstractMinMaxNode : VariableArgsFunctionNode {

		protected abstract double BaseValue { get; }
		protected abstract bool IsPreferred(double val, double runningBest);

		public override EvaluationType ReturnType {
			get {
				if (Arguments.Length == 0) {
					throw new EvaluationTypeException($"{Name} must take a non-zero number of arguments.");
				}

				EvaluationType[] returnTypes = Arguments.Select(a => a.ReturnType).Distinct().ToArray();

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
		}

		public override object Evaluate(IEnvironment environment) {
			object[] args = EvaluationTypes.VerifyArray(Arguments.Select(a => a.Evaluate(environment)).ToArray());

			bool fromArray = false;
			if (args.Length == 1 && EvaluationTypes.TryGetArray(args[0], out Array? array)) {
				args = array.Cast<object>().ToArray();
				fromArray = true;
			}

			if (args.Length == 0) {
				if (fromArray) {
					throw new EvaluationCalculationException($"Cannot take {Name} of array with zero elements.");
				}
				else {
					throw new EvaluationCalculationException($"Cannot take {Name} of zero arguments.");
				}
			}

			bool badTypes = false;

			object result = args[0];
			double runningBest = BaseValue;
			for (int i = 0; i < args.Length; i++) {
				bool preferred = false;
				double val;
				if (args[i] is int aInt) { val = aInt; }
				else if (args[i] is uint aUInt) { val = aUInt; }
				else if (args[i] is float aFloat) { val = aFloat; }
				else if (args[i] is UFloat aUFloat) { val = aUFloat; }
				else {
					badTypes = true;
					break;
				}

				preferred = IsPreferred(val, runningBest);


				if (preferred) {
					runningBest = val;
					result = args[i];
				}
			}

			if (badTypes) {
				Type[] types = args.Select(a => a.GetType()).Distinct().ToArray();
				string s = (types.Length > 1) ? "s" : "";
				if (fromArray) {
					throw new EvaluationTypeException($"Cannot take {Name} of array with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
				}
				else {
					throw new EvaluationTypeException($"Cannot take {Name} of arguments with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
				}
			}

			EvaluationType returnType = ReturnType;

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

	public class MinVarNode : AbstractMinMaxNode {
		public sealed override string Name { get; } = "min";

		protected override double BaseValue { get { return double.MaxValue; } }
		protected override bool IsPreferred(double val, double runningBest) {
			return val < runningBest;
		}

		protected override VariableArgsFunctionNode MakeEmptyBase() {
			return new MinVarNode();
		}
	}

	public class MaxVarNode : AbstractMinMaxNode {
		public sealed override string Name { get; } = "max";

		protected override double BaseValue { get { return double.MinValue; } }
		protected override bool IsPreferred(double val, double runningBest) {
			return val > runningBest;
		}

		protected override VariableArgsFunctionNode MakeEmptyBase() {
			return new MaxVarNode();
		}
	}

	public class SumNode : VariableArgsFunctionNode {

		public override string Name { get; } = "sum";

		public override EvaluationType ReturnType {
			get {
				if (Arguments.Length == 0) {
					throw new EvaluationTypeException($"{Name} must take a non-zero number of arguments.");
				}

				EvaluationType[] returnTypes = Arguments.Select(a => a.ReturnType).Distinct().ToArray();

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
		}

		public override object Evaluate(IEnvironment environment) {
			object[] args = EvaluationTypes.VerifyArray(Arguments.Select(a => a.Evaluate(environment)).ToArray());

			bool fromArray = false;
			if (args.Length == 1 && EvaluationTypes.TryGetArray(args[0], out Array? array)) {
				args = array.Cast<object>().ToArray();
				fromArray = true;
			}

			if (args.Length == 0) {
				if (fromArray) {
					throw new EvaluationCalculationException($"Cannot take {Name} of array with zero elements.");
				}
				else {
					throw new EvaluationCalculationException($"Cannot take {Name} of zero arguments.");
				}
			}

			bool badTypes = false;
			double total = 0.0;
			foreach (object a in args) {
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
				Type[] types = args.Select(a => a.GetType()).Distinct().ToArray();
				string s = (types.Length > 1) ? "s" : "";
				if (fromArray) {
					throw new EvaluationTypeException($"Cannot take {Name} of array with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
				}
				else {
					throw new EvaluationTypeException($"Cannot take {Name} of arguments with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
				}
			}

			EvaluationType returnType = ReturnType;

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

		protected override VariableArgsFunctionNode MakeEmptyBase() {
			return new SumNode();
		}
	}

	public class FloorNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				return argType.IsReal() ? EvaluationType.INT : throw new EvaluationTypeException($"Floor not defined for value of type {argType}.");
			}
		}
		public sealed override string Name { get; } = "floor";

		public override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

			if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
				return aInt;
			}
			else if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return (int)Math.Floor(aFloat);
			}
			else {
				throw new EvaluationTypeException($"Floor not defined for value of type {GetDataTypeName(a)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new FloorNode();
		}
	}

	public class CeilingNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				return argType.IsReal() ? EvaluationType.INT : throw new EvaluationTypeException($"Ceiling not defined for value of type {argType}.");
			}
		}
		public sealed override string Name { get; } = "ceil";

		public override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

			if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
				return aInt;
			}
			else if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return (int)Math.Ceiling(aFloat);
			}
			else {
				throw new EvaluationTypeException($"Ceiling not defined for value of type {GetDataTypeName(a)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new CeilingNode();
		}
	}

	public abstract class MathematicalFunction : SingleArgFunctionNode {
		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				return argType.IsReal() ? EvaluationType.FLOAT : throw new EvaluationTypeException($"Mathematical functions are not defined for value of type {argType}.");
			}
		}

		protected abstract double Calculate(float argument);

		public sealed override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

			if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return (float)Calculate(aFloat);
			}
			else {
				throw new EvaluationTypeException($"Mathematical functions are not defined for value of type {GetDataTypeName(a)}.");
			}
		}
	}

	public class SquareRootNode : MathematicalFunction {
		public override string Name { get; } = "sqrt";
		protected override double Calculate(float argument) => Math.Sqrt(argument);
		protected override FunctionNode Empty() { return new SquareRootNode(); }
	}

	public class SinNode : MathematicalFunction {
		public override string Name { get; } = "sin";
		protected override double Calculate(float argument) => Math.Sin(argument);
		protected override FunctionNode Empty() { return new SinNode(); }
	}
	public class CosNode : MathematicalFunction {
		public override string Name { get; } = "cos";
		protected override double Calculate(float argument) => Math.Cos(argument);
		protected override FunctionNode Empty() { return new CosNode(); }
	}
	public class TanNode : MathematicalFunction {
		public override string Name { get; } = "tan";
		protected override double Calculate(float argument) => Math.Tan(argument);
		protected override FunctionNode Empty() { return new TanNode(); }
	}

	public class AsinNode : MathematicalFunction {
		public override string Name { get; } = "asin";
		protected override double Calculate(float argument) => Math.Asin(argument);
		protected override FunctionNode Empty() { return new AsinNode(); }
	}
	public class AcosNode : MathematicalFunction {
		public override string Name { get; } = "acos";
		protected override double Calculate(float argument) => Math.Acos(argument);
		protected override FunctionNode Empty() { return new AcosNode(); }
	}
	public class AtanNode : MathematicalFunction {
		public override string Name { get; } = "atan";
		protected override double Calculate(float argument) => Math.Atan(argument);
		protected override FunctionNode Empty() { return new AtanNode(); }
	}
	public class Atan2Node : FunctionNode {
		public override string Name { get; } = "atan2";
		public override EvaluationNode[] Arguments { get; } = new EvaluationNode[2];
		public override bool IsConstant { get { return Arguments[0].IsConstant && Arguments[1].IsConstant; } }

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType arg1Type = Arguments[0].ReturnType;
				EvaluationType arg2Type = Arguments[1].ReturnType;
				return (arg1Type.IsReal() && arg2Type.IsReal()) ? EvaluationType.FLOAT : throw new EvaluationTypeException($"Atan2 not defined for operands of type {arg1Type} and {arg2Type}.");
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? a = Arguments[0].Evaluate(environment);
			object? b = Arguments[1].Evaluate(environment);

			if (EvaluationTypes.TryGetReal(a, out float afloat) && EvaluationTypes.TryGetReal(b, out float bfloat)) {
				return (float)Math.Atan2(afloat, bfloat);
			}
			else {
				throw new EvaluationTypeException($"Atan2 not defined for operands of type {GetDataTypeName(a)} and {GetDataTypeName(b)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new Atan2Node();
		}
	}

	public class SinhNode : MathematicalFunction {
		public override string Name { get; } = "sinh";
		protected override double Calculate(float argument) => Math.Sinh(argument);
		protected override FunctionNode Empty() { return new SinhNode(); }
	}
	public class CoshNode : MathematicalFunction {
		public override string Name { get; } = "cosh";
		protected override double Calculate(float argument) => Math.Cosh(argument);
		protected override FunctionNode Empty() { return new CoshNode(); }
	}
	public class TanhNode : MathematicalFunction {
		public override string Name { get; } = "tanh";
		protected override double Calculate(float argument) => Math.Tanh(argument);
		protected override FunctionNode Empty() { return new TanhNode(); }
	}

	public class LerpNode : FunctionNode {
		public override string Name { get; } = "lerp";
		public override EvaluationNode[] Arguments { get; } = new EvaluationNode[3];
		public override bool IsConstant { get { return Arguments[0].IsConstant && Arguments[1].IsConstant && Arguments[2].IsConstant; } }

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType arg1Type = Arguments[0].ReturnType;
				EvaluationType arg2Type = Arguments[1].ReturnType;
				EvaluationType arg3Type = Arguments[2].ReturnType;
				return (arg1Type.IsReal() && arg2Type.IsReal() && arg3Type.IsReal()) ? EvaluationType.FLOAT : throw new EvaluationTypeException($"lerp not defined for operands of type {arg1Type}, {arg2Type}, and {arg3Type}.");
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? aObj = Arguments[0].Evaluate(environment);
			object? bObj = Arguments[1].Evaluate(environment);
			object? tObj = Arguments[2].Evaluate(environment);

			if (EvaluationTypes.TryGetReal(aObj, out float a) && EvaluationTypes.TryGetReal(bObj, out float b) && EvaluationTypes.TryGetReal(tObj, out float t)) {
				return a + (b - a) * t;
			}
			else {
				throw new EvaluationTypeException($"lerp not defined for operands of type {GetDataTypeName(aObj)}, {GetDataTypeName(bObj)}, and {GetDataTypeName(tObj)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new LerpNode();
		}
	}

}
