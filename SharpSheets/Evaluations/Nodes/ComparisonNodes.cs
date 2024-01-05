using System;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class RealValueComparisonOperatorNode : BinaryOperatorNode {
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;

		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType firstType = First.ReturnType;
				EvaluationType secondType = Second.ReturnType;
				if (firstType.IsReal() && secondType.IsReal()) {
					return EvaluationType.BOOL;
				}
				else {
					throw new EvaluationTypeException($"Cannot evaluate {Symbol} operator for operands of type {firstType} and {secondType}.");
				}
			}
		}

		protected abstract bool Compare(float a, float b);

		public sealed override object Evaluate(IEnvironment environment) {
			object? a = First.Evaluate(environment);
			object? b = Second.Evaluate(environment);

			if (EvaluationTypes.TryGetReal(a, out float afloat1) && EvaluationTypes.TryGetReal(b, out float bfloat1)) {
				return Compare(afloat1, bfloat1);
			}
			else {
				throw new EvaluationCalculationException($"Cannot evaluate {Symbol} operator for operands of type {GetDataTypeName(a)} and {GetDataTypeName(b)}.");
			}
		}
	}

	public class LessThanNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = "<";

		protected override bool Compare(float a, float b) {
			return a < b;
		}

		protected override BinaryOperatorNode Empty() {
			return new LessThanNode();
		}
	}

	public class GreaterThanNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = ">";

		protected override bool Compare(float a, float b) {
			return a > b;
		}

		protected override BinaryOperatorNode Empty() {
			return new GreaterThanNode();
		}
	}

	public class LessThanEqualNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = "<=";

		protected override bool Compare(float a, float b) {
			return a <= b;
		}

		protected override BinaryOperatorNode Empty() {
			return new LessThanEqualNode();
		}
	}

	public class GreaterThanEqualNode : RealValueComparisonOperatorNode {
		public sealed override int Precedence { get; } = 5;
		public override string Symbol { get; } = ">=";

		protected override bool Compare(float a, float b) {
			return a >= b;
		}

		protected override BinaryOperatorNode Empty() {
			return new GreaterThanEqualNode();
		}
	}

	public abstract class AbstractEqualityNode : BinaryOperatorNode {
		public sealed override EvaluationType ReturnType { get; } = EvaluationType.BOOL;
		public sealed override int Precedence { get; } = 6;
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;
		public sealed override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		protected bool ArgumentsEqual(IEnvironment environment) {
			try {
				object? a = First.Evaluate(environment);
				object? b = Second.Evaluate(environment);

				// TODO Should this be using TryGetIntegral/TryGetReal?
				if (a is float aFloat && b is int bInt) {
					return aFloat == bInt;
				}
				else if (a is int aInt && b is float bFloat) {
					return aInt == bFloat;
				}
				else if (First.ReturnType.IsEnum || Second.ReturnType.IsEnum) {
					if (a is Enum aEnumVal && b is Enum bEnumVal) {
						return aEnumVal == bEnumVal;
					}
					else if (a is string aStringVal && b is string bStringVal) {
						return StringComparer.InvariantCultureIgnoreCase.Equals(aStringVal, bStringVal);
					}
					if (a is Enum aEnum && b is string bString) {
						return StringComparer.InvariantCultureIgnoreCase.Equals(aEnum.ToString(), bString);
					}
					else if (a is string aString && b is Enum bEnum) {
						return StringComparer.InvariantCultureIgnoreCase.Equals(aString, bEnum.ToString());
					}
					else {
						throw new EvaluationCalculationException($"Invalid types {GetDataTypeName(a)} and {GetDataTypeName(b)} for equality evaluation.");
					}
				}
				else if (a is not null && b is not null) {
					return a.Equals(b);
				}
				else {
					return a is null && b is null;
				}
			}
			catch(EvaluationTypeException e) {
				throw new EvaluationCalculationException("Could not evaluate equality.", e); // This should really never occur.
			}
		}
	}

	public class EqualityNode : AbstractEqualityNode {
		public override string Symbol { get; } = "==";

		public override object Evaluate(IEnvironment environment) {
			return ArgumentsEqual(environment);
		}

		protected override BinaryOperatorNode Empty() {
			return new EqualityNode();
		}
	}

	public class InequalityNode : AbstractEqualityNode {
		public override string Symbol { get; } = "!=";

		public override object Evaluate(IEnvironment environment) {
			return !ArgumentsEqual(environment);
		}

		protected override BinaryOperatorNode Empty() {
			return new InequalityNode();
		}
	}

}
