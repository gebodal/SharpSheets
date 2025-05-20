using System;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class ArithmeticOperatorNode : BinaryOperatorNode {
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;

		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public sealed override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem) {
			EvaluationType firstType = First.GetReturnType(typeSystem);
			EvaluationType secondType = Second.GetReturnType(typeSystem);

			return ResultType(firstType, secondType, typeSystem) ?? throw MakeTypeError(firstType, secondType);
		}

		protected abstract EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem);

		public sealed override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = First.Evaluate(environment);
			EvaluationValue b = Second.Evaluate(environment);

			return Evaluate(a, b, environment.TypeSystem) ?? throw MakeCalculationError(a, b);
		}

		protected abstract EvaluationValue? Evaluate(EvaluationValue first, EvaluationValue second, EvaluationTypeSystem typeSystem);

		protected EvaluationTypeException MakeTypeError(EvaluationType firstType, EvaluationType secondType) {
			return new EvaluationTypeException($"Cannot perform {Symbol} operation on operands of type {firstType} and {secondType}.");
		}

		protected EvaluationTypeException MakeCalculationError(EvaluationValue a, EvaluationValue b) {
			return new EvaluationTypeException($"Cannot perform binary {Symbol} for operands of type {a.Type} and {b.Type}.");
		}
	}

	public class ExponentNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 2;
		public override string Symbol { get; } = "**";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.PowResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.Pow(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new ExponentNode();
		}
	}

	public class DivisionNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 3;
		public override string Symbol { get; } = "/";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.DivResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.PerformDiv(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new DivisionNode();
		}
	}

	public class MultiplicationNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 3;
		public override string Symbol { get; } = "*";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.MulResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.Mul(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new MultiplicationNode();
		}
	}

	public class RemainderNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 3;
		public override string Symbol { get; } = "%";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.ModResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.Mod(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new RemainderNode();
		}
	}

	public class AdditionNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 4;
		public override string Symbol { get; } = "+";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.AddResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.Add(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new AdditionNode();
		}
	}

	public class SubtractNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 4;
		public override string Symbol { get; } = "-";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.SubResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.Sub(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new SubtractNode();
		}
	}

}
