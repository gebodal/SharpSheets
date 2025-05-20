using System;
using System.Collections.Generic;

namespace SharpSheets.Evaluations.Nodes {

	public class NullCoalescingNode : BinaryOperatorNode {
		public sealed override int Precedence { get; } = 10;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol { get; } = "??";
		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem) {
			EvaluationType firstType = First.GetReturnType(typeSystem);
			EvaluationType secondType = Second.GetReturnType(typeSystem);
			if (typeSystem.TryGetLeastUpperBoundType(firstType, secondType, out EvaluationType? compatible)) {
				return compatible;
			}
			else {
				throw new EvaluationTypeException($"Operands must have compatible return types ({firstType} != {secondType}).");
			}
		}

		protected override BinaryOperatorNode Empty() {
			return new NullCoalescingNode();
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationType firstType = First.GetReturnType(environment);
			EvaluationType secondType = Second.GetReturnType(environment);
			if (!environment.TryGetLeastUpperBoundType(firstType, secondType, out EvaluationType? compatible)) {
				throw new EvaluationTypeException($"Operands must have compatible return types ({firstType} != {secondType}).");
			}

			EvaluationValue? result = null;
			try {
				result = First.Evaluate(environment);
			}
			catch (EvaluationException) { }

			if (!result.HasValue) {
				result = Second.Evaluate(environment);
			}

			return compatible.Cast(result.Value) ?? throw new EvaluationCalculationException($"Cannot convert from {result.Value.Type} to {compatible}.");
		}
	}

	public class ConditionalOperatorNode : TernaryOperatorNode {
		public override int Precedence { get; } = 11;
		public override Associativity Associativity { get; } = Associativity.RIGHT;
		public override Type OpeningType { get; } = typeof(ConditionalOpenNode);
		public override int[] CalculationOrder { get; } = new int[] { 2, 1, 0 };

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem) {
			EvaluationType conditionType = First.GetReturnType(typeSystem);
			if (!BoolEvaluationType.IsBool(conditionType)) { // Could we be more lenient here? (And therefore below when evaluating?)
				throw new EvaluationTypeException($"Condition for conditional operator must evaluate to a boolean.");
			}
			EvaluationType consequentType = Second.GetReturnType(typeSystem);
			EvaluationType alternativeType = Third.GetReturnType(typeSystem);
			if (typeSystem.TryGetLeastUpperBoundType(consequentType, alternativeType, out EvaluationType? compatibleType)) {
				return compatibleType;
			}
			else {
				throw new EvaluationTypeException($"Expressions must have compatible return types ({consequentType} != {alternativeType}).");
			}
		}

		internal override void AssignOpening(OperatorNode openingNode) { }

		protected override TernaryOperatorNode Empty() {
			return new ConditionalOperatorNode();
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			if(!BoolEvaluationType.TryGetBool(First.Evaluate(environment), out bool condition)) {
				throw new EvaluationCalculationException("Cannot evaluate condition.");
			}

			EvaluationValue consequent = Second.Evaluate(environment);
			EvaluationValue alternative = Third.Evaluate(environment);

			if (environment.TryGetLeastUpperBoundType(consequent.Type, alternative.Type, out EvaluationType? compatibleType)) {
				if (condition) {
					return compatibleType.Cast(consequent) ?? throw new EvaluationCalculationException($"Could not convert {consequent.Type} to {compatibleType}.");
				}
				else {
					return compatibleType.Cast(alternative) ?? throw new EvaluationCalculationException($"Could not convert {alternative.Type} to {compatibleType}.");
				}
			}
			else {
				throw new EvaluationTypeException($"Expressions must have compatible return types ({consequent.Type} != {alternative.Type}).");
			}
		}

		protected override string GetRepresentation() {
			string condition = First.ToString();
			string consequent = Second.ToString();
			string alternative = Third.ToString();
			if (First is OperatorNode conditionNode && conditionNode.Precedence >= this.Precedence) {
				condition = "(" + condition + ")";
			}
			if (Second is OperatorNode consequentNode && consequentNode.Precedence >= this.Precedence) {
				consequent = "(" + consequent + ")";
			}
			if (Third is OperatorNode alternativeNode && alternativeNode.Precedence >= this.Precedence) {
				alternative = "(" + alternative + ")";
			}
			return condition + " ? " + consequent + " : " + alternative;
		}

		internal class ConditionalOpenNode : OperatorNode {
			public override bool IsConstant => throw new NotImplementedException();
			public override EvaluationType GetReturnType(EvaluationTypeSystem _) => throw new NotImplementedException();
			public sealed override int Operands { get { return 0; } }
			public sealed override int Precedence { get; } = 11;
			public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
			public override EvaluationValue Evaluate(IEnvironment environment) { throw new NotImplementedException(); }
			public override EvaluationNode Simplify(EvaluationTypeSystem typeSystem) { throw new NotImplementedException(); }
			public override EvaluationNode Clone() { return new ConditionalOpenNode(); }
			public override IEnumerable<EvaluationName> GetVariables() { throw new NotImplementedException(); }
			//public override void Print(int indent, IEnvironment environment) { throw new NotImplementedException(); }
			protected override string GetRepresentation() { throw new NotImplementedException(); }
		}
	}

}
