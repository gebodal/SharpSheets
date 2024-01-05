using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	// -2) Open brace: (, [?
	// -1) Close brace: ), ]?
	// 0) Primary: x.y, f(x), a[i]
	// 1) Unary: +x, -x, !x
	// 2) Exponent: x ** y
	// 3) Multiplicative: x * y, x / y, x % y
	// 4) Additive: x + y, x – y
	// 5) Relational: x < y, x > y, x <= y, x >= y
	// 6) Equality: x == y, x != y
	// 7) Logical AND: x & y
	// 8) Logical XOR: x ^ y
	// 9) Logical OR: x | y
	// 10) Null-coalescing operator: x ?? y (right-associative)
	// 11) Conditional operator: c ? t : f
	// 12) Comprehensions: for...in...if
	// 13) Indexer slice ":": a[1:-1]

	public enum Associativity { LEFT, RIGHT }

	public abstract class OperatorNode : EvaluationNode {
		public abstract int Operands { get; }
		public abstract int Precedence { get; }
		public abstract Associativity Associativity { get; }
	}

	public abstract class UnaryOperatorNode : OperatorNode {
		public sealed override bool IsConstant { get { return Operand.IsConstant; } }
		public sealed override int Operands { get { return 1; } }
		public abstract string Symbol { get; }

		private EvaluationNode? _operand;
		public EvaluationNode Operand {
			get { return _operand ?? throw new EvaluationCalculationException("Operator not initialized."); }
			set { _operand = value; }
		}

		protected abstract UnaryOperatorNode Empty();

		public sealed override EvaluationNode Clone() {
			UnaryOperatorNode empty = Empty();
			empty.Operand = Operand.Clone();
			return empty;
		}

		public override EvaluationNode Simplify() {
			if (IsConstant) {
				return new ConstantNode(Evaluate(Environments.Empty), ReturnType);
			}
			else {
				UnaryOperatorNode empty = Empty();
				empty.Operand = Operand.Simplify();
				return empty;
			}
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			return Operand.GetVariables();
		}

		protected override string GetRepresentation() {
			string operand = Operand.ToString();
			if (Operand is OperatorNode node && node.Precedence >= this.Precedence) {
				operand = "(" + operand + ")";
			}

			// Is this associativity thing always true?
			if (this.Associativity == Associativity.LEFT) {
				return operand + Symbol;
			}
			else {
				return Symbol + operand;
			}
		}

		/*
		public sealed override void Print(int indent, IEnvironment environment) {
			Console.WriteLine(new string(' ', indent * 4) + $"Unary {Symbol}:");
			Operand.Print(indent + 1, environment);
		}
		*/
	}

	public abstract class BinaryOperatorNode : OperatorNode {
		public sealed override bool IsConstant { get { return First.IsConstant && Second.IsConstant; } }
		public sealed override int Operands { get; } = 2;
		public abstract int[] CalculationOrder { get; }
		public abstract string Symbol { get; }

		private EvaluationNode? _first;
		public EvaluationNode First {
			get { return _first ?? throw new EvaluationCalculationException("Operator not initialized."); }
			set { _first = value; }
		}
		private EvaluationNode? _second;
		public EvaluationNode Second {
			get { return _second ?? throw new EvaluationCalculationException("Operator not initialized."); }
			set { _second = value; }
		}

		public EvaluationNode this[int arg] {
			get {
				if (arg == 0) { return First; }
				else if (arg == 1) { return Second; }
				else { throw new EvaluationProcessingException("Argument index out of bounds."); }
			}
			set {
				if (arg == 0) { First = value; }
				else if (arg == 1) { Second = value; }
				else { throw new EvaluationProcessingException("Argument index out of bounds."); }
			}
		}

		protected abstract BinaryOperatorNode Empty();

		public sealed override EvaluationNode Clone() {
			BinaryOperatorNode empty = Empty();
			empty.First = First.Clone();
			empty.Second = Second.Clone();
			return empty;
		}

		public sealed override EvaluationNode Simplify() {
			if (IsConstant) {
				return new ConstantNode(Evaluate(Environments.Empty), ReturnType);
			}
			else {
				BinaryOperatorNode empty = Empty();
				empty.First = First.Simplify();
				empty.Second = Second.Simplify();
				return empty;
			}
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			return First.GetVariables().Concat(Second.GetVariables()).Distinct();
		}

		protected override string GetRepresentation() {
			string left = First.ToString();
			string right = Second.ToString();
			if (First is OperatorNode leftNode && (leftNode.Precedence >= this.Precedence || (this.Precedence == leftNode.Precedence && this.Associativity != Associativity.LEFT))) {
				left = "(" + left + ")";
			}
			if (Second is OperatorNode rightNode && (rightNode.Precedence >= this.Precedence || (this.Precedence == rightNode.Precedence && this.Associativity != Associativity.RIGHT))) {
				right = "(" + right + ")";
			}
			return left + " " + Symbol + " " + right;
		}

		/*
		public sealed override void Print(int indent, IEnvironment environment) {
			Console.WriteLine(new string(' ', indent * 4) + $"Binary {Symbol}:");
			First.Print(indent + 1, environment);
			Second.Print(indent + 1, environment);
		}
		*/
	}

	public abstract class TernaryOperatorNode : OperatorNode {
		public sealed override int Operands { get; } = 3;
		public sealed override bool IsConstant => First.IsConstant && Second.IsConstant && Third.IsConstant;
		public abstract int[] CalculationOrder { get; }

		public abstract Type OpeningType { get; }

		private EvaluationNode? _first;
		public EvaluationNode First {
			get { return _first ?? throw new EvaluationCalculationException("Operator not initialized."); }
			set { _first = value; }
		}
		private EvaluationNode? _second;
		public EvaluationNode Second {
			get { return _second ?? throw new EvaluationCalculationException("Operator not initialized."); }
			set { _second = value; }
		}
		private EvaluationNode? _third;
		public EvaluationNode Third {
			get { return _third ?? throw new EvaluationCalculationException("Operator not initialized."); }
			set { _third = value; }
		}

		public EvaluationNode this[int arg] {
			get {
				if (arg == 0) { return First; }
				else if (arg == 1) { return Second; }
				else if (arg == 2) { return Third; }
				else { throw new EvaluationProcessingException("Argument index out of bounds."); }
			}
			set {
				if (arg == 0) { First = value; }
				else if (arg == 1) { Second = value; }
				else if (arg == 2) { Third = value; }
				else { throw new EvaluationProcessingException("Argument index out of bounds."); }
			}
		}

		internal abstract void AssignOpening(OperatorNode openingNode);

		protected abstract TernaryOperatorNode Empty();

		public override EvaluationNode Clone() {
			TernaryOperatorNode empty = Empty();
			empty.First = First.Clone();
			empty.Second = Second.Clone();
			empty.Third = Third.Clone();
			return empty;
		}

		public override EvaluationNode Simplify() {
			if (IsConstant) {
				return new ConstantNode(Evaluate(Environments.Empty), ReturnType);
			}
			else {
				TernaryOperatorNode empty = Empty();
				empty.First = First.Simplify();
				empty.Second = Second.Simplify();
				empty.Third = Third.Simplify();
				return empty;
			}
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			return First.GetVariables().Concat(Second.GetVariables(), Third.GetVariables()).Distinct();
		}

		/*
		public override void Print(int indent, IEnvironment environment) {
			Console.WriteLine(new string(' ', indent * 4) + this.GetType().Name + ":");
			First.Print(indent + 1, environment);
			Second.Print(indent + 1, environment);
			Third.Print(indent + 1, environment);
		}
		*/
	}

}
