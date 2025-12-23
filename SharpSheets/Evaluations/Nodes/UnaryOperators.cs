using SharpSheets.Evaluations.Types;

namespace SharpSheets.Evaluations.Nodes {

	public class PlusOperator : UnaryOperatorNode {
		public sealed override int Precedence { get; } = 1;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol { get; } = "+";

		public PlusOperator(EvaluationContext context) : base(context) { }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		public override EvaluationType GetReturnType() {
			EvaluationType operandType = Operand.GetReturnType();
			return operandType.PosResult() ?? throw new EvaluationTypeException($"Cannot take positive value of type {operandType}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = Operand.Evaluate(environment);
			return a.Type.Pos(a) ?? throw new EvaluationTypeException($"Cannot take positive value of type {a.Type}.");
		}

		protected override UnaryOperatorNode Empty() {
			return new PlusOperator(Context);
		}
	}

	public class MinusOperator : UnaryOperatorNode {
		public sealed override int Precedence { get; } = 1;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol { get; } = "-";

		public MinusOperator(EvaluationContext context) : base(context) { }

		public override EvaluationType GetReturnType() {
			EvaluationType operandType = Operand.GetReturnType();
			return operandType.NegResult() ?? throw new EvaluationTypeException($"Cannot take negative value of type {operandType}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = Operand.Evaluate(environment);
			return a.Type.Neg(a) ?? throw new EvaluationTypeException($"Cannot take negative value of type {a.Type}.");
		}

		protected override UnaryOperatorNode Empty() {
			return new MinusOperator(Context);
		}
	}

	public class NegateOperator : UnaryOperatorNode {
		public sealed override int Precedence { get; } = 1;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol { get; } = "!";

		public NegateOperator(EvaluationContext context) : base(context) { }

		public override EvaluationType GetReturnType() {
			EvaluationType operandType = Operand.GetReturnType();
			return operandType.InvertResult() ?? throw new EvaluationTypeException($"Cannot negate value of type {operandType}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = Operand.Evaluate(environment);
			return a.Type.Invert(a) ?? throw new EvaluationTypeException($"Cannot negate value of type {a.Type}.");
		}

		protected override UnaryOperatorNode Empty() {
			return new NegateOperator(Context);
		}
	}

}
