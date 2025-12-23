using SharpSheets.Evaluations.Types;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public sealed class FieldAccessNode : UnaryOperatorNode {
		public override int Precedence { get; } = 0;
		public override Associativity Associativity { get; } = Associativity.LEFT;
		public override string Symbol { get; }

		public EvaluationName Field { get; }

		public FieldAccessNode(EvaluationName field, EvaluationContext context) : base(context) {
			this.Field = field;
			this.Symbol = "." + Field;
		}

		public override EvaluationType GetReturnType() {
			if (Operand is TypeLiteralNode typeNode) {
				EvaluationType typeLiteral = typeNode.TypeValue;
				TypeField typeField = typeLiteral.GetStaticField(Field) ?? throw new EvaluationTypeException($"{typeLiteral} type does not have a static field named {Field}.");
				return typeField.Type;
			}
			else {
				EvaluationType type = Operand.GetReturnType();
				TypeField typeField = type.GetField(Field) ?? throw new EvaluationTypeException($"{type} does not have a field named {Field}.");
				return typeField.Type;
			}
		}

		protected override UnaryOperatorNode Empty() {
			return new FieldAccessNode(Field, Context);
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			if (Operand is TypeLiteralNode typeNode) {
				EvaluationType typeLiteral = typeNode.TypeValue;
				TypeField? typeField = typeLiteral.GetStaticField(Field) ?? throw new EvaluationCalculationException($"{typeLiteral} type does not have a static field named {Field}.");
				return typeField.GetValue(new EvaluationValue(typeLiteral, typeNode.GetReturnType()));
			}
			else {
				EvaluationValue subject = Operand.Evaluate(environment);
				TypeField? field = subject.Type.GetField(Field) ?? throw new EvaluationCalculationException($"{subject.Type} does not have a field named {Field}.");
				if (subject.Value == null) {
					throw new EvaluationCalculationException($"Cannot access field on a null value.");
				}
				return field.GetValue(subject);
			}
		}

		public override IEnumerable<EvaluationName> GetVariables() => Operand.GetVariables();

	}

	public class IndexerNode : BinaryOperatorNode {

		public sealed override int Precedence { get; } = 0;
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;

		public override string Symbol { get; } = "[]";

		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public IndexerNode(EvaluationContext context) : base(context) { }

		public override EvaluationType GetReturnType() {
			EvaluationType argType = First.GetReturnType();
			EvaluationType indexType = Second.GetReturnType();

			return argType.IndexerResult(indexType) ?? throw new EvaluationTypeException($"Cannot index into value of type {argType} using {indexType}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue subject = First.Evaluate(environment);
			EvaluationValue index = Second.Evaluate(environment);

			return subject.Type.Indexer(subject, index) ?? throw new EvaluationTypeException($"Cannot index into value of type {subject.Type} using {index.Type}.");
		}

		protected override BinaryOperatorNode Empty() {
			return new IndexerNode(Context);
		}

		protected override string GetRepresentation() {
			string left = First.ToString();
			string right = Second.ToString();
			if (First is OperatorNode leftNode && (leftNode.Precedence >= this.Precedence || (this.Precedence == leftNode.Precedence && this.Associativity != Associativity.LEFT))) {
				left = "(" + left + ")";
			}
			return left + "[" + right + "]";
		}
	}

	public class IndexerSliceNode : TernaryOperatorNode {

		public override int Precedence => 0;
		public override Associativity Associativity => Associativity.LEFT;

		public override int[] CalculationOrder => new int[] { 2, 1, 0 };

		public IndexerSliceNode(EvaluationContext context) : base(context) { }

		public override EvaluationType GetReturnType() {
			EvaluationType argType = First.GetReturnType();
			EvaluationType index1Type = Second.GetReturnType();
			EvaluationType index2Type = Third.GetReturnType();

			return argType.IndexerSliceResult(index1Type, index2Type) ?? throw new EvaluationTypeException($"Cannot slice value of type {argType} using indexes of type ({index1Type}:{index2Type}).");
		}

		public override Type OpeningType => throw new InvalidOperationException();
		internal override void AssignOpening(OperatorNode openingNode) => throw new InvalidOperationException();

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue subject = First.Evaluate(environment);
			EvaluationValue index1 = Second.Evaluate(environment);
			EvaluationValue index2 = Third.Evaluate(environment);

			return subject.Type.IndexerSlice(subject, index1, index2) ?? throw new EvaluationTypeException($"Cannot slice value of type {subject.Type} using indexes of type ({index1.Type}:{index2.Type}).");
		}

		protected override TernaryOperatorNode Empty() {
			return new IndexerSliceNode(Context);
		}

		protected override string GetRepresentation() {
			string subject = First.ToString();
			string left = Second.ToString();
			string right = Third.ToString();
			if (First is OperatorNode leftNode && (leftNode.Precedence >= this.Precedence || (this.Precedence == leftNode.Precedence && this.Associativity != Associativity.LEFT))) {
				subject = "(" + subject + ")";
			}
			if (Second is not ValueNode) {
				left = "(" + left + ")";
			}
			if (Third is not ValueNode) {
				right = "(" + right + ")";
			}
			return subject + "[" + left + ":" + right + "]";
		}
	}

}
