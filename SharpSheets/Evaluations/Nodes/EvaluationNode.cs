using System.Collections.Generic;
using SharpSheets.Colors;
using SharpSheets.Utilities;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class EvaluationNode {

		public abstract bool IsConstant { get; }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		public abstract EvaluationType ReturnType { get; }

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public abstract object? Evaluate(IEnvironment environment);

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// /// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public abstract EvaluationNode Simplify();

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		/// <exception cref="EvaluationProcessingException"></exception>
		public abstract EvaluationNode Clone();

		//public abstract void Print(int indent, IEnvironment environment);

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		private static EvaluationNode Validate(EvaluationNode node) {
			_ = node.ReturnType;
			return node.Simplify();
		}

		public static EvaluationNode operator *(EvaluationNode a, EvaluationNode b) {
			return Validate(new MultiplicationNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator /(EvaluationNode a, EvaluationNode b) {
			return Validate(new DivisionNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator %(EvaluationNode a, EvaluationNode b) {
			return Validate(new RemainderNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator +(EvaluationNode a, EvaluationNode b) {
			return Validate(new AdditionNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator -(EvaluationNode a, EvaluationNode b) {
			return Validate(new SubtractNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator -(EvaluationNode a) {
			return Validate(new MinusOperator() { Operand = a.Clone() });
		}
		public static EvaluationNode operator <(EvaluationNode a, EvaluationNode b) {
			return Validate(new LessThanNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator >(EvaluationNode a, EvaluationNode b) {
			return Validate(new GreaterThanNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator <=(EvaluationNode a, EvaluationNode b) {
			return Validate(new LessThanEqualNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator >=(EvaluationNode a, EvaluationNode b) {
			return Validate(new GreaterThanEqualNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator ==(EvaluationNode a, EvaluationNode b) {
			return Validate(new EqualityNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator !=(EvaluationNode a, EvaluationNode b) {
			return Validate(new InequalityNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator &(EvaluationNode a, EvaluationNode b) {
			return Validate(new ANDNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator ^(EvaluationNode a, EvaluationNode b) {
			return Validate(new XORNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator |(EvaluationNode a, EvaluationNode b) {
			return Validate(new ORNode() { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator !(EvaluationNode a) {
			return Validate(new NegateOperator() { Operand = a.Clone() });
		}

		public static implicit operator EvaluationNode(float value) {
			return new ConstantNode(value);
		}
		public static implicit operator EvaluationNode(UFloat value) {
			return new ConstantNode(value);
		}
		public static implicit operator EvaluationNode(int value) {
			return new ConstantNode(value);
		}
		public static implicit operator EvaluationNode(uint value) {
			return new ConstantNode(value);
		}
		public static implicit operator EvaluationNode(bool value) {
			return new ConstantNode(value);
		}
		public static implicit operator EvaluationNode(string value) {
			return new ConstantNode(value);
		}
		public static implicit operator EvaluationNode(Color value) {
			return new ConstantNode(value);
		}

		public override bool Equals(object? obj) {
			return base.Equals(obj);
		}
		public override int GetHashCode() {
			return base.GetHashCode();
		}

		public abstract IEnumerable<EvaluationName> GetVariables();

		public override sealed string ToString() {
			return GetRepresentation();
		}

		protected abstract string GetRepresentation();

		protected static string GetDataTypeName(object? data) {
			return data?.GetType().Name ?? "null";
		}
	}

}
