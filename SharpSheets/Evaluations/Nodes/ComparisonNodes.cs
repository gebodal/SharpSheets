using SharpSheets.Evaluations.Types;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class RealValueComparisonOperatorNode : BinaryOperatorNode {
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;

		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public RealValueComparisonOperatorNode(EvaluationContext context) : base(context) { }

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
			return new EvaluationTypeException($"Cannot perform {Symbol} comparison on operands of type {firstType} and {secondType}.");
		}

		protected EvaluationTypeException MakeCalculationError(EvaluationValue a, EvaluationValue b) {
			return new EvaluationTypeException($"Cannot perform binary {Symbol} for operands of type {a.Type} and {b.Type}.");
		}
	}

	public class LessThanNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = "<";

		public LessThanNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.LessThanResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.LessThan(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new LessThanNode(Context);
		}
	}

	public class GreaterThanNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = ">";

		public GreaterThanNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.GreaterThanResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.GreaterThan(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new GreaterThanNode(Context);
		}
	}

	public class LessThanEqualNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = "<=";

		public LessThanEqualNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.LessThanEqualResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.LessThanEqual(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new LessThanEqualNode(Context);
		}
	}

	public class GreaterThanEqualNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = ">=";

		public GreaterThanEqualNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.GreaterThanEqualResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.GreaterThanEqual(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new GreaterThanEqualNode(Context);
		}
	}

	public abstract class AbstractEqualityNode : BinaryOperatorNode {
		public sealed override int Precedence { get; } = 6;
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;
		public sealed override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public AbstractEqualityNode(EvaluationContext context) : base(context) { }

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
			return new EvaluationTypeException($"Cannot perform {Symbol} comparison on operands of type {firstType} and {secondType}.");
		}

		protected EvaluationTypeException MakeCalculationError(EvaluationValue a, EvaluationValue b) {
			return new EvaluationTypeException($"Cannot perform binary {Symbol} for operands of type {a.Type} and {b.Type}.");
		}
	}

	public class EqualityNode : AbstractEqualityNode {
		public override string Symbol { get; } = "==";

		public EqualityNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.EqualResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.Equal(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new EqualityNode(Context);
		}
	}

	public class InequalityNode : AbstractEqualityNode {
		public override string Symbol { get; } = "!=";

		public InequalityNode(EvaluationContext context) : base(context) { }

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second) {
			return EvaluationOps.NotEqualResult(first, second);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b) {
			return EvaluationOps.NotEqual(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new InequalityNode(Context);
		}
	}

}
