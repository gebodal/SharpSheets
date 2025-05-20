namespace SharpSheets.Evaluations.Nodes {

	public abstract class BooleanBinaryOperatorNode : BinaryOperatorNode {
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;
		protected abstract string OperatorName { get; }
		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public sealed override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem) {
			EvaluationType firstType = First.GetReturnType(typeSystem);
			EvaluationType secondType = Second.GetReturnType(typeSystem);
			if (BoolEvaluationType.IsBool(firstType) && BoolEvaluationType.IsBool(secondType)) {
				return EvaluationTypes.BOOL;
			}
			else {
				throw new EvaluationTypeException($"Cannot evaluate logical {OperatorName} for operands of type {firstType} and {secondType}.");
			}
		}

		protected static EvaluationValue MakeResult(bool result) {
			return new EvaluationValue(result, EvaluationTypes.BOOL);
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

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = First.Evaluate(environment);

			if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
				if (aBool) {
					EvaluationValue b = Second.Evaluate(environment);
					if (BoolEvaluationType.TryGetBool(b, out bool bBool)) {
						return MakeResult(aBool && bBool);
					}
					else {
						throw MakeCalculationError(a, b);
					}
				}
				else {
					return MakeResult(false);
				}
			}
			else {
				throw MakeCalculationError(a);
			}
		}

		protected override BinaryOperatorNode Empty() {
			return new ANDNode();
		}
	}

	public class XORNode : BooleanBinaryOperatorNode {
		public sealed override int Precedence { get; } = 8;
		public override string Symbol { get; } = "^";
		protected override string OperatorName { get; } = "XOR";

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = First.Evaluate(environment);
			EvaluationValue b = Second.Evaluate(environment);

			if (BoolEvaluationType.TryGetBool(a, out bool aBool) && BoolEvaluationType.TryGetBool(b, out bool bBool)) {
				return new EvaluationValue(aBool ^ bBool, EvaluationTypes.BOOL);
			}
			else {
				throw MakeCalculationError(a, b);
			}
		}

		protected override BinaryOperatorNode Empty() {
			return new XORNode();
		}
	}

	public class ORNode : BooleanBinaryOperatorNode {
		public sealed override int Precedence { get; } = 9;
		public override string Symbol { get; } = "|";
		protected override string OperatorName { get; } = "OR";

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue a = First.Evaluate(environment);

			if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
				if (aBool) {
					return MakeResult(true);
					
				}
				else {
					EvaluationValue b = Second.Evaluate(environment);
					if (BoolEvaluationType.TryGetBool(b, out bool bBool)) {
						return MakeResult(bBool);
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
			return new ORNode();
		}
	}

}
