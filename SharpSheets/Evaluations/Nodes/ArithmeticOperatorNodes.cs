using System;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class ArithmeticOperatorNode : BinaryOperatorNode {
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;

		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public ArithmeticOperatorNode(EvaluationContext context) : base(context) { }

		public sealed override EvaluationType GetReturnType() {
			EvaluationType firstType = First.GetReturnType();
			EvaluationType secondType = Second.GetReturnType();

			return ResultType(firstType, secondType) ?? throw MakeTypeError(firstType, secondType);
		}

		protected abstract EvaluationType? ResultType(EvaluationType first, EvaluationType second);

		public sealed override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = First.Evaluate(environment);
			EvaluationValue b = Second.Evaluate(environment);

			return Evaluate(a, b) ?? throw MakeCalculationError(a, b);
		}

		protected abstract EvaluationValue? Evaluate(EvaluationValue first, EvaluationValue second);

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

		public ExponentNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.PowResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.Pow(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new ExponentNode(Context);
		}
	}

	public class DivisionNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 3;
		public override string Symbol { get; } = "/";

		public DivisionNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.DivResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.PerformDiv(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new DivisionNode(Context);
		}
	}

	public class MultiplicationNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 3;
		public override string Symbol { get; } = "*";

		public MultiplicationNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.MulResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.Mul(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new MultiplicationNode(Context);
		}
	}

	public class RemainderNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 3;
		public override string Symbol { get; } = "%";

		public RemainderNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.ModResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.Mod(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new RemainderNode(Context);
		}
	}

	public class AdditionNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 4;
		public override string Symbol { get; } = "+";

		public AdditionNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.AddResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.Add(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new AdditionNode(Context);
		}
	}

	public class SubtractNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 4;
		public override string Symbol { get; } = "-";

		public SubtractNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.SubResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.Sub(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new SubtractNode(Context);
		}
	}

}
