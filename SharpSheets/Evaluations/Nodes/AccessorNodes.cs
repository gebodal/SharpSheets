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

		public FieldAccessNode(EvaluationName field) {
			this.Field = field;
			this.Symbol = "." + Field;
		}

		public override EvaluationType ReturnType {
			get {
				EvaluationType type = Operand.ReturnType;
				TypeField? field = type.GetField(Field);
				if (field == null) {
					throw new EvaluationTypeException($"{type} does not have a field named {Field}.");
				}
				return field.Type;
			}
		}

		protected override UnaryOperatorNode Empty() {
			return new FieldAccessNode(Field);
		}

		public override object? Evaluate(IEnvironment environment) {
			EvaluationType type = Operand.ReturnType;
			TypeField? field = type.GetField(Field);
			if (field == null) {
				throw new EvaluationCalculationException($"{type} does not have a field named {Field}.");
			}
			object? subject = Operand.Evaluate(environment);
			if (subject == null) {
				throw new EvaluationCalculationException($"Cannot access field on a null value.");
			}
			return field.GetValue(subject);
		}

		public override IEnumerable<EvaluationName> GetVariables() => Operand.GetVariables();

	}

	/*
	public class IndexerNode : UnaryOperatorNode {

		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Operand.ReturnType;
				if (argType == EvaluationType.STRING) {
					return argType;
				}
				else if (argType.IsArray) {
					return argType.ElementType;
				}
				else {
					throw new EvaluationTypeException($"Cannot index into value of type {argType}.");
				}
			}
		}

		public sealed override int Precedence { get; } = 0;
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;

		public override string Symbol {
			get {
				if (!range) {
					return $"[{start.Value}]";
				}
				else {
					string s = start.HasValue ? start.Value.ToString() : "0";
					string e = end.HasValue ? end.Value.ToString() : "end";

					return $"[{s}:{e}]";
				}
			}
		}

		// TODO These should probably be EvaluationNodes themselves?
		private readonly int? start;
		private readonly int? end;
		private readonly bool range;

		public IndexerNode(int? start, int? end, bool range) {
			this.start = start;
			this.end = end;
			this.range = range;
		}

		public override object Evaluate(IEnvironment environment) {
			object a = Operand.Evaluate(environment);

			if (a is string aString) {
				if (!range) {
					return aString[start.Value].ToString();
				}
				else {
					int s = start ?? 0;
					int e = end ?? aString.Length;

					// TODO This isn't working quite right yet?

					if (s < 0) { s += aString.Length; }
					if (e < 0) { e += aString.Length; }
					if (s < 0 || e < 0 || s > aString.Length || e > aString.Length) {
						throw new EvaluationCalculationException("Invalid index.");
					}

					if (start.HasValue && !end.HasValue) {
						return aString.Substring(s);
					}
					else if (!start.HasValue && end.HasValue) {
						return aString.Substring(0, e);
					}
					else {
						return aString.Substring(s, e - s);
					}
				}
			}
			else if (a is Array aArr) {
				if (aArr.Rank != 1) {
					throw new EvaluationProcessingException($"Can only index into rank 1 arrays (rank {aArr.Rank} provided).");
				}
				int length = aArr.Length;
				if (!range) {
					return aArr.GetValue(start.Value);
				}
				else {
					int s = start ?? 0;
					int e = end ?? length;

					// TODO This isn't working quite right yet?

					if (s < 0) { s += length; }
					if (e < 0) { e += length; }
					if (s < 0 || e < 0 || s > length || e > length) {
						throw new EvaluationCalculationException("Invalid index.");
					}

					if (start.HasValue && !end.HasValue) {
						Array result = Array.CreateInstance(aArr.GetType().GetElementType(), length - s);
						Array.Copy(aArr, s, result, 0, result.Length);
						return result;
					}
					else if (!start.HasValue && end.HasValue) {
						Array result = Array.CreateInstance(aArr.GetType().GetElementType(), e);
						Array.Copy(aArr, result, result.Length);
						return result;
					}
					else {
						Array result = Array.CreateInstance(aArr.GetType().GetElementType(), e - s);
						Array.Copy(aArr, s, result, 0, result.Length);
						return result;
					}
				}
			}
			else {
				throw new EvaluationTypeException($"Cannot index into value of type {a.GetType().Name}.");
			}
		}

		protected override UnaryOperatorNode Empty() {
			return new IndexerNode(start, end, range);
		}
	}
	*/

	public class IndexerNode : BinaryOperatorNode {

		public override EvaluationType ReturnType {
			get {
				EvaluationType indexType = Second.ReturnType;
				if (!indexType.IsIntegral()) {
					throw new EvaluationTypeException($"Index value must be an integer, not {indexType}.");
				}

				EvaluationType argType = First.ReturnType;
				if (argType == EvaluationType.STRING) {
					return argType;
				}
				else if (argType.IsArray || argType.IsTuple) {
					return argType.ElementType;
				}
				else {
					throw new EvaluationTypeException($"Cannot index into value of type {argType}.");
				}
			}
		}

		public sealed override int Precedence { get; } = 0;
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;

		public override string Symbol { get; } = "[]";

		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		private static int GetIndex(int index, int length) {
			if (index < 0) index += length;
			return index;
		}

		public override object? Evaluate(IEnvironment environment) {
			object? subject = First.Evaluate(environment);

			if(!EvaluationTypes.TryGetIntegral(Second.Evaluate(environment), out int index)) {
				throw new EvaluationCalculationException("Cannot evaluate index value to integer.");
			}

			if(subject is null) {
				throw new EvaluationCalculationException("Cannot index into null value.");
			}
			else if (subject is string aString) {
				index = GetIndex(index, aString.Length);
				if (index < 0 || index > aString.Length - 1) {
					throw new EvaluationCalculationException($"Index is outside of string content.");
				}
				return aString[index].ToString();
			}
			else if (subject is Array aArr) {
				if (aArr.Rank != 1) {
					throw new EvaluationCalculationException($"Can only index into rank 1 arrays (rank {aArr.Rank} provided).");
				}
				index = GetIndex(index, aArr.Length);
				if (index < 0 || index > aArr.Length - 1) {
					throw new EvaluationCalculationException($"Index is outside of array bounds.");
				}
				return aArr.GetValue(index);
			}
			else if (TupleUtils.IsTupleObject(subject, out Type? tupleType)) {
				int length = TupleUtils.GetTupleLength(tupleType);
				index = GetIndex(index, length);
				if (index < 0 || index > length - 1) {
					throw new EvaluationCalculationException($"Index is outside of tuple bounds.");
				}
				return TupleUtils.Index(subject, index);
			}
			else {
				throw new EvaluationTypeException($"Cannot index into value of type {subject.GetType().Name}.");
			}
		}

		protected override BinaryOperatorNode Empty() {
			return new IndexerNode();
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

		public override EvaluationType ReturnType {
			get {
				EvaluationType index1Type = Second.ReturnType;
				EvaluationType index2Type = Third.ReturnType;
				if (!index1Type.IsIntegral() || !index2Type.IsIntegral()) {
					string typeString = string.Join(", ", new string[] { index1Type.ToString(), index2Type.ToString() });
					throw new EvaluationTypeException($"Index values must be an integers, not [{typeString}].");
				}

				EvaluationType argType = First.ReturnType;
				if (argType == EvaluationType.STRING || argType.IsArray) {
					return argType;
				}
				else if (argType.IsTuple) {
					return argType.ElementType.MakeArray();
				}
				else {
					throw new EvaluationTypeException($"Cannot index into value of type {argType}.");
				}
			}
		}

		public override int Precedence => 0;
		public override Associativity Associativity => Associativity.LEFT;

		public override int[] CalculationOrder => new int[] { 2, 1, 0 };

		public override Type OpeningType => throw new InvalidOperationException();
		internal override void AssignOpening(OperatorNode openingNode) => throw new InvalidOperationException();

		private static void GetIndexes(int a, int b, int length, out int start, out int end) {
			if (a < 0) a += length;
			if (b < 0) b += length;

			start = a.Clamp(0, length);
			end = b.Clamp(start, length);
		}

		public override object Evaluate(IEnvironment environment) {
			object? subject = First.Evaluate(environment);

			if (!EvaluationTypes.TryGetIntegral(Second.Evaluate(environment), out int index1)) {
				throw new EvaluationCalculationException("Cannot evaluate start index value to integer.");
			}
			if (!EvaluationTypes.TryGetIntegral(Third.Evaluate(environment), out int index2)) {
				throw new EvaluationCalculationException("Cannot evaluate end index value to integer.");
			}

			if (subject is null) {
				throw new EvaluationCalculationException("Cannot index into null value.");
			}
			else if (subject is string aString) {
				GetIndexes(index1, index2, aString.Length, out index1, out index2);

				return aString.Substring(index1, index2 - index1);
			}
			else if (subject is Array aArr) {
				if (aArr.Rank != 1) {
					throw new EvaluationProcessingException($"Can only index into rank 1 arrays (rank {aArr.Rank} provided).");
				}

				GetIndexes(index1, index2, aArr.Length, out index1, out index2);

				List<object?> results = new List<object?>();

				for (int i = index1; i < index2; i++) {
					object? entry = aArr.GetValue(i);
					results.Add(entry);
				}

				return EvaluationTypes.MakeArray(aArr.GetType().GetElementType()!, results);
			}
			else if (TupleUtils.IsTupleObject(subject, out Type? tupleType)) {
				int length = TupleUtils.GetTupleLength(tupleType);

				GetIndexes(index1, index2, length, out index1, out index2);

				List<object?> results = new List<object?>();

				for (int i = index1; i < index2; i++) {
					object entry = TupleUtils.Index(subject, i);
					results.Add(entry);
				}

				return EvaluationTypes.MakeArray(First.ReturnType.ElementType!.DataType, results);
			}
			else {
				throw new EvaluationTypeException($"Cannot index into value of type {subject.GetType().Name}.");
			}
		}

		protected override TernaryOperatorNode Empty() {
			return new IndexerSliceNode();
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
