using SharpSheets.Utilities;

namespace SharpSheets.Evaluations.Nodes {

	public class PlusOperator : UnaryOperatorNode {
		public sealed override int Precedence { get; } = 1;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol { get; } = "+";

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		public override EvaluationType ReturnType {
			get {
				EvaluationType operandType = Operand.ReturnType;
				return operandType.IsReal() ? operandType : throw new EvaluationTypeException($"Cannot take positive value of type {operandType}.");
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? a = Operand.Evaluate(environment);

			if (a is int aint) {
				return +aint;
			}
			else if (a is float afloat) {
				return +afloat;
			}
			else if (a is uint auint) {
				return +auint;
			}
			else if (a is UFloat aufloat) {
				return aufloat;
			}
			else {
				throw new EvaluationTypeException($"Cannot take positive value of type {GetDataTypeName(a)}.");
			}
		}

		protected override UnaryOperatorNode Empty() {
			return new PlusOperator();
		}
	}

	public class MinusOperator : UnaryOperatorNode {
		public sealed override int Precedence { get; } = 1;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol { get; } = "-";

		public override EvaluationType ReturnType {
			get {
				EvaluationType operandType = Operand.ReturnType;
				if (operandType.IsReal()) {
					return operandType.IsIntegral() ? EvaluationType.INT : EvaluationType.FLOAT;
				}
				else {
					throw new EvaluationTypeException($"Cannot take negative value of type {operandType}.");
				}
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? a = Operand.Evaluate(environment);

			if (EvaluationTypes.TryGetIntegral(a, out int aint)) {
				return -aint;
			}
			else if (EvaluationTypes.TryGetReal(a, out float afloat)) {
				return -afloat;
			}
			else {
				throw new EvaluationTypeException($"Cannot take negative value of type {GetDataTypeName(a)}.");
			}
		}

		protected override UnaryOperatorNode Empty() {
			return new MinusOperator();
		}
	}

	public class NegateOperator : UnaryOperatorNode {
		public sealed override int Precedence { get; } = 1;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol { get; } = "!";

		public override EvaluationType ReturnType {
			get {
				EvaluationType operandType = Operand.ReturnType;
				if (operandType == EvaluationType.BOOL) {
					return operandType;
				}
				else {
					throw new EvaluationTypeException($"Cannot negate value of type {operandType}.");
				}
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? a = Operand.Evaluate(environment);

			if (a is bool aBool) {
				return !aBool;
			}
			else {
				throw new EvaluationTypeException($"Cannot negate value of type {GetDataTypeName(a)}.");
			}
		}

		protected override UnaryOperatorNode Empty() {
			return new NegateOperator();
		}
	}

}
