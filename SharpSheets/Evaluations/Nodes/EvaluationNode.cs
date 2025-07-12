using System.Collections.Generic;
using SharpSheets.Colors;
using SharpSheets.Utilities;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class EvaluationNode {

		public abstract bool IsConstant { get; }

		public EvaluationContext Context { get; }

		protected EvaluationNode(EvaluationContext context) {
			this.Context = context;
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		public abstract EvaluationType GetReturnType();

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public abstract EvaluationValue Evaluate(IEnvironment environment);

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
			_ = node.GetReturnType();
			return node.Simplify();
		}

		public static EvaluationNode operator *(EvaluationNode a, EvaluationNode b) {
			return Validate(new MultiplicationNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator /(EvaluationNode a, EvaluationNode b) {
			return Validate(new DivisionNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator %(EvaluationNode a, EvaluationNode b) {
			return Validate(new RemainderNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator +(EvaluationNode a, EvaluationNode b) {
			return Validate(new AdditionNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator -(EvaluationNode a, EvaluationNode b) {
			return Validate(new SubtractNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator -(EvaluationNode a) {
			return Validate(new MinusOperator(a.Context) { Operand = a.Clone() });
		}
		public static EvaluationNode operator <(EvaluationNode a, EvaluationNode b) {
			return Validate(new LessThanNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator >(EvaluationNode a, EvaluationNode b) {
			return Validate(new GreaterThanNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator <=(EvaluationNode a, EvaluationNode b) {
			return Validate(new LessThanEqualNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator >=(EvaluationNode a, EvaluationNode b) {
			return Validate(new GreaterThanEqualNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator ==(EvaluationNode a, EvaluationNode b) {
			return Validate(new EqualityNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator !=(EvaluationNode a, EvaluationNode b) {
			return Validate(new InequalityNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator &(EvaluationNode a, EvaluationNode b) {
			return Validate(new ANDNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator ^(EvaluationNode a, EvaluationNode b) {
			return Validate(new XORNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator |(EvaluationNode a, EvaluationNode b) {
			return Validate(new ORNode(a.Context) { First = a.Clone(), Second = b.Clone() });
		}
		public static EvaluationNode operator !(EvaluationNode a) {
			return Validate(new NegateOperator(a.Context) { Operand = a.Clone() });
		}


		public static implicit operator EvaluationNode(EvaluationValue value) {
			return new ConstantNode(value);
		}
		/*
		public static implicit operator EvaluationNode(float value) {
			return new ConstantNode(new EvaluationValue(value, EvaluationTypes.FLOAT));
		}
		public static implicit operator EvaluationNode(UFloat value) {
			return new ConstantNode(new EvaluationValue(value, EvaluationTypes.UFLOAT));
		}
		public static implicit operator EvaluationNode(int value) {
			return new ConstantNode(new EvaluationValue(value, EvaluationTypes.INT));
		}
		public static implicit operator EvaluationNode(uint value) {
			return new ConstantNode(new EvaluationValue(value, EvaluationTypes.UINT));
		}
		public static implicit operator EvaluationNode(bool value) {
			return new ConstantNode(new EvaluationValue(value, EvaluationTypes.BOOL));
		}
		public static implicit operator EvaluationNode(string value) {
			return new ConstantNode(new EvaluationValue(value, EvaluationTypes.STRING));
		}
		*/
		/*
		public static implicit operator EvaluationNode(Color value) {
			return new ConstantNode(value);
		}
		*/

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

	}

}
