using System;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class ArithmeticOperatorNode : BinaryOperatorNode {
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;

		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public override EvaluationType ReturnType {
			get {
				EvaluationType firstType = First.ReturnType;
				EvaluationType secondType = Second.ReturnType;
				if (firstType.IsReal() && secondType.IsReal()) {
					return (firstType.IsIntegral() && secondType.IsIntegral()) ? EvaluationType.INT : EvaluationType.FLOAT;
				}
				else if (AcceptString && firstType == EvaluationType.STRING && secondType == EvaluationType.STRING) {
					return EvaluationType.STRING;
				}
				else {
					throw new EvaluationTypeException($"Cannot perform arithmetic operation on operands of type {firstType} and {secondType}.");
				}
			}
		}

		protected abstract int Calculate(int a, int b);
		protected abstract float Calculate(float a, float b);
		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		protected virtual string Calculate(string a, string b) { throw new EvaluationCalculationException($"Binary {Symbol} not implemented for string operands."); }
		protected virtual bool AcceptString { get; } = false;

		public sealed override object Evaluate(IEnvironment environment) {
			object? a = First.Evaluate(environment);
			object? b = Second.Evaluate(environment);

			if(EvaluationTypes.TryGetIntegral(a, out int aint1) && EvaluationTypes.TryGetIntegral(b, out int bint1)) {
				return Calculate(aint1, bint1);
			}
			else if(EvaluationTypes.TryGetReal(a, out float afloat1) && EvaluationTypes.TryGetReal(b, out float bfloat1)) {
				return Calculate(afloat1, bfloat1);
			}
			else if (AcceptString && a is string aString && b is string bString) {
				return Calculate(aString, bString);
			}
			else {
				throw new EvaluationTypeException($"Cannot perform binary {Symbol} for operands of type {EvaluationUtils.GetDataTypeName(a)} and {EvaluationUtils.GetDataTypeName(b)}.");
			}
		}
	}

	public class ExponentNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 2;
		public override string Symbol { get; } = "**";

		protected override float Calculate(float a, float b) {
			return (float)Math.Pow(a, b);
		}

		protected override int Calculate(int a, int b) {
			return (int)Math.Pow(a, b);
		}

		protected override BinaryOperatorNode Empty() {
			return new ExponentNode();
		}
	}

	public class DivisionNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 3;
		public override string Symbol { get; } = "/";

		protected override float Calculate(float a, float b) {
			return a / b;
		}

		protected override int Calculate(int a, int b) {
			return a / b;
		}

		protected override BinaryOperatorNode Empty() {
			return new DivisionNode();
		}
	}

	public class MultiplicationNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 3;
		public override string Symbol { get; } = "*";

		protected override float Calculate(float a, float b) {
			return a * b;
		}

		protected override int Calculate(int a, int b) {
			return a * b;
		}

		protected override BinaryOperatorNode Empty() {
			return new MultiplicationNode();
		}
	}

	public class RemainderNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 3;
		public override string Symbol { get; } = "%";

		protected override float Calculate(float a, float b) {
			return a % b;
		}

		protected override int Calculate(int a, int b) {
			return a % b;
		}

		protected override BinaryOperatorNode Empty() {
			return new RemainderNode();
		}
	}

	public class AdditionNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 4;
		public override string Symbol { get; } = "+";

		protected override float Calculate(float a, float b) {
			return a + b;
		}

		protected override int Calculate(int a, int b) {
			return a + b;
		}

		protected override bool AcceptString { get; } = true;
		protected override string Calculate(string a, string b) {
			return a + b;
		}

		protected override BinaryOperatorNode Empty() {
			return new AdditionNode();
		}
	}

	public class SubtractNode : ArithmeticOperatorNode {
		public sealed override int Precedence { get; } = 4;
		public override string Symbol { get; } = "-";

		protected override float Calculate(float a, float b) {
			return a - b;
		}

		protected override int Calculate(int a, int b) {
			return a - b;
		}

		protected override BinaryOperatorNode Empty() {
			return new SubtractNode();
		}
	}

}
