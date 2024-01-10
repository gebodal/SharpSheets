namespace SharpSheets.Evaluations.Nodes {

	public abstract class BooleanBinaryOperatorNode : BinaryOperatorNode {
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;
		protected abstract string OperatorName { get; }
		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType firstType = First.ReturnType;
				EvaluationType secondType = Second.ReturnType;
				if (firstType == EvaluationType.BOOL && secondType == EvaluationType.BOOL) {
					return EvaluationType.BOOL;
				}
				else {
					throw new EvaluationTypeException($"Cannot evaluate logical {OperatorName} for operands of type {firstType} and {secondType}.");
				}
			}
		}

		protected abstract bool Compare(bool a, bool b);

		public sealed override object Evaluate(IEnvironment environment) {
			object? a = First.Evaluate(environment);
			object? b = Second.Evaluate(environment);

			if (a is bool aBool && b is bool bBool) {
				return Compare(aBool, bBool);
			}
			else {
				throw new EvaluationTypeException($"Cannot evaluate logical {OperatorName} for operands of type {EvaluationUtils.GetDataTypeName(a)} and {EvaluationUtils.GetDataTypeName(b)}.");
			}
		}
	}

	public class ANDNode : BooleanBinaryOperatorNode {
		public sealed override int Precedence { get; } = 7;
		public override string Symbol { get; } = "&";
		protected override string OperatorName { get; } = "AND";

		protected override bool Compare(bool a, bool b) {
			return a && b;
		}

		protected override BinaryOperatorNode Empty() {
			return new ANDNode();
		}
	}

	public class XORNode : BooleanBinaryOperatorNode {
		public sealed override int Precedence { get; } = 8;
		public override string Symbol { get; } = "^";
		protected override string OperatorName { get; } = "XOR";

		protected override bool Compare(bool a, bool b) {
			return a ^ b;
		}

		protected override BinaryOperatorNode Empty() {
			return new XORNode();
		}
	}

	public class ORNode : BooleanBinaryOperatorNode {
		public sealed override int Precedence { get; } = 9;
		public override string Symbol { get; } = "|";
		protected override string OperatorName { get; } = "OR";

		protected override bool Compare(bool a, bool b) {
			return a || b;
		}

		protected override BinaryOperatorNode Empty() {
			return new ORNode();
		}
	}

}
