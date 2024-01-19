using System;
using System.Collections.Generic;

namespace SharpSheets.Evaluations.Nodes {

	public class NullCoalescingNode : BinaryOperatorNode {
		public sealed override int Precedence { get; } = 10;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol { get; } = "??";
		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public override EvaluationType ReturnType {
			get {
				EvaluationType firstType = First.ReturnType;
				EvaluationType secondType = Second.ReturnType;
				if (EvaluationTypes.TryGetCompatibleType(firstType, secondType, out EvaluationType? compatible)) {
					return compatible;
				}
				else {
					throw new EvaluationTypeException($"Operands must have the same return type ({firstType} != {secondType}).");
				}
			}
		}

		protected override BinaryOperatorNode Empty() {
			return new NullCoalescingNode();
		}

		public override object? Evaluate(IEnvironment environment) {

			EvaluationType firstType = First.ReturnType;
			EvaluationType secondType = Second.ReturnType;
			if (!EvaluationTypes.TryGetCompatibleType(firstType, secondType, out EvaluationType? compatible)) {
				throw new EvaluationTypeException($"Operands must have the same return type ({firstType} != {secondType}).");
			}

			object? a = null;
			try {
				a = EvaluationTypes.GetCompatibleValue(compatible, First.Evaluate(environment));
			}
			catch (EvaluationException) { }

			if (a != null) {
				return a;
			}
			else {
				return EvaluationTypes.GetCompatibleValue(compatible, Second.Evaluate(environment));
			}
		}
	}

	public class ConditionalOperatorNode : TernaryOperatorNode {
		public override int Precedence { get; } = 11;
		public override Associativity Associativity { get; } = Associativity.RIGHT;
		public override Type OpeningType { get; } = typeof(ConditionalOpenNode);
		public override int[] CalculationOrder { get; } = new int[] { 2, 1, 0 };

		public override EvaluationType ReturnType {
			get {
				EvaluationType conditionType = First.ReturnType;
				if (conditionType != EvaluationType.BOOL) {
					throw new EvaluationTypeException($"Condition for conditional operator must evaluate to a boolean.");
				}
				EvaluationType consequentType = Second.ReturnType;
				EvaluationType alternativeType = Third.ReturnType;
				if (EvaluationTypes.TryGetCompatibleType(consequentType, alternativeType, out EvaluationType? compatibleType)) {
					return compatibleType;
				}
				else {
					throw new EvaluationTypeException($"Expressions must have the same return type ({consequentType} != {alternativeType}).");
				}
			}
		}

		internal override void AssignOpening(OperatorNode openingNode) { }

		protected override TernaryOperatorNode Empty() {
			return new ConditionalOperatorNode();
		}

		public override object? Evaluate(IEnvironment environment) {
			bool condition = (bool)(First.Evaluate(environment) ?? throw new EvaluationCalculationException("Cannot evaluate condition."));

			if (EvaluationTypes.TryGetCompatibleType(Second.ReturnType, Third.ReturnType, out EvaluationType? compatibleType)) {
				if (condition) {
					return EvaluationTypes.GetCompatibleValue(compatibleType, Second.Evaluate(environment));
				}
				else {
					return EvaluationTypes.GetCompatibleValue(compatibleType, Third.Evaluate(environment));
				}
			}
			else {
				throw new EvaluationTypeException($"Expressions must have the same return type ({Second.ReturnType} != {Third.ReturnType}).");
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
			public override EvaluationType ReturnType => throw new NotImplementedException();
			public sealed override int Operands { get { return 0; } }
			public sealed override int Precedence { get; } = 11;
			public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
			public override object Evaluate(IEnvironment environment) { throw new NotImplementedException(); }
			public override EvaluationNode Simplify() { throw new NotImplementedException(); }
			public override EvaluationNode Clone() { return new ConditionalOpenNode(); }
			public override IEnumerable<EvaluationName> GetVariables() { throw new NotImplementedException(); }
			//public override void Print(int indent, IEnvironment environment) { throw new NotImplementedException(); }
			protected override string GetRepresentation() { throw new NotImplementedException(); }
		}
	}

}
