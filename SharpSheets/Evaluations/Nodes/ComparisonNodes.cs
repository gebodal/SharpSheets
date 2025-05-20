using System;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class RealValueComparisonOperatorNode : BinaryOperatorNode {
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
			return new EvaluationTypeException($"Cannot perform {Symbol} comparison on operands of type {firstType} and {secondType}.");
		}

		protected EvaluationTypeException MakeCalculationError(EvaluationValue a, EvaluationValue b) {
			return new EvaluationTypeException($"Cannot perform binary {Symbol} for operands of type {a.Type} and {b.Type}.");
		}
	}

	public class LessThanNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = "<";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.LessThanResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.LessThan(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new LessThanNode();
		}
	}

	public class GreaterThanNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = ">";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.GreaterThanResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.GreaterThan(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new GreaterThanNode();
		}
	}

	public class LessThanEqualNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = "<=";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.LessThanEqualResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.LessThanEqual(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new LessThanEqualNode();
		}
	}

	public class GreaterThanEqualNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = ">=";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.GreaterThanEqualResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.GreaterThanEqual(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new GreaterThanEqualNode();
		}
	}

	public abstract class AbstractEqualityNode : BinaryOperatorNode {
		public sealed override int Precedence { get; } = 6;
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;
		public sealed override int[] CalculationOrder { get; } = new int[] { 1, 0 };

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
			return new EvaluationTypeException($"Cannot perform {Symbol} comparison on operands of type {firstType} and {secondType}.");
		}

		protected EvaluationTypeException MakeCalculationError(EvaluationValue a, EvaluationValue b) {
			return new EvaluationTypeException($"Cannot perform binary {Symbol} for operands of type {a.Type} and {b.Type}.");
		}
	}

	public class EqualityNode : AbstractEqualityNode {
		public override string Symbol { get; } = "==";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.EqualResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.Equal(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new EqualityNode();
		}
	}

	public class InequalityNode : AbstractEqualityNode {
		public override string Symbol { get; } = "!=";

		protected override EvaluationType? ResultType(EvaluationType first, EvaluationType second, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.NotEqualResult(first, second, typeSystem);
		}

		protected override EvaluationValue? Evaluate(EvaluationValue a, EvaluationValue b, EvaluationTypeSystem typeSystem) {
			return EvaluationOps.NotEqual(a, b, typeSystem);
		}

		protected override BinaryOperatorNode Empty() {
			return new InequalityNode();
		}
	}

}
