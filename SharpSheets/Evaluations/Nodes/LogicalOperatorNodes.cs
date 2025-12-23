using SharpSheets.Evaluations.Types;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class BooleanBinaryOperatorNode : BinaryOperatorNode {
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;
		protected abstract string OperatorName { get; }
		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public BooleanBinaryOperatorNode(EvaluationContext context) : base(context) { }

		public sealed override EvaluationType GetReturnType() {
			EvaluationType firstType = First.GetReturnType();
			EvaluationType secondType = Second.GetReturnType();
			if (BoolEvaluationType.IsBool(firstType) && BoolEvaluationType.IsBool(secondType)) {
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				throw new EvaluationTypeException($"Cannot evaluate logical {OperatorName} for operands of type {firstType} and {secondType}.");
			}
		}

		protected static EvaluationValue MakeResult(IEnvironment environment, bool result) {
			return new EvaluationValue(result, environment.Context.GetType<BoolEvaluationType>());
		}

		protected EvaluationTypeException MakeCalculationError(EvaluationValue a) {
			return new EvaluationTypeException($"Invalid operand for logical {OperatorName} of type {a.Type}.");
		}
		protected EvaluationTypeException MakeCalculationError(EvaluationValue a, EvaluationValue b) {
			return new EvaluationTypeException($"Cannot evaluate logical {OperatorName} for operands of type {a.Type} and {b.Type}.");
		}
	}

	public class ANDNode : BooleanBinaryOperatorNode {
		public sealed override int Precedence { get; } = 7;
		public override string Symbol { get; } = "&";
		protected override string OperatorName { get; } = "AND";

		public ANDNode(EvaluationContext context) : base(context) { }

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = First.Evaluate(environment);

			if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
				if (aBool) {
					EvaluationValue b = Second.Evaluate(environment);
					if (BoolEvaluationType.TryGetBool(b, out bool bBool)) {
						return MakeResult(environment, aBool && bBool);
					}
					else {
						throw MakeCalculationError(a, b);
					}
				}
				else {
					return MakeResult(environment, false);
				}
			}
			else {
				throw MakeCalculationError(a);
			}
		}

		protected override BinaryOperatorNode Empty() {
			return new ANDNode(Context);
		}
	}

	public class XORNode : BooleanBinaryOperatorNode {
		public sealed override int Precedence { get; } = 8;
		public override string Symbol { get; } = "^";
		protected override string OperatorName { get; } = "XOR";

		public XORNode(EvaluationContext context) : base(context) { }

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = First.Evaluate(environment);
			EvaluationValue b = Second.Evaluate(environment);

			if (BoolEvaluationType.TryGetBool(a, out bool aBool) && BoolEvaluationType.TryGetBool(b, out bool bBool)) {
				return MakeResult(environment, aBool ^ bBool);
			}
			else {
				throw MakeCalculationError(a, b);
			}
		}

		protected override BinaryOperatorNode Empty() {
			return new XORNode(Context);
		}
	}

	public class ORNode : BooleanBinaryOperatorNode {
		public sealed override int Precedence { get; } = 9;
		public override string Symbol { get; } = "|";
		protected override string OperatorName { get; } = "OR";

		public ORNode(EvaluationContext context) : base(context) { }

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = First.Evaluate(environment);

			if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
				if (aBool) {
					return MakeResult(environment, true);
					
				}
				else {
					EvaluationValue b = Second.Evaluate(environment);
					if (BoolEvaluationType.TryGetBool(b, out bool bBool)) {
						return MakeResult(environment, bBool);
					}
					else {
						throw MakeCalculationError(a, b);
					}
				}
			}
			else {
				throw MakeCalculationError(a);
			}
		}

		protected override BinaryOperatorNode Empty() {
			return new ORNode(Context);
		}
	}

}
