using System.Diagnostics.CodeAnalysis;
using System.Text;
using SharpSheets.Utilities;
using System.Collections;
using SharpSheets.Parsing;
using System.Collections.Specialized;
using System.Globalization;
using SharpSheets.Evaluations.Nodes;
using System.Text.RegularExpressions;

namespace SharpSheets.Evaluations.Types {

	public abstract class CollectionEvaluationType : EvaluationType {

		public EvaluationType ElementType { get; }

		protected CollectionEvaluationType(EvaluationContext context, EvaluationType elementType) : base(context) {
			this.ElementType = elementType;
		}

		public override string Name {
			get {
				StringBuilder sb = new StringBuilder();
				EvaluationType baseType = this;
				while(baseType is CollectionEvaluationType subCollection) {
					baseType = subCollection.ElementType;
					sb.Append(subCollection.GetMyCollectionBrackets());
				}
				return baseType.Name + sb.ToString();

				//return ElementType.Name + GetCollectionBrackets();
			}
		}

		/*
		private string GetCollectionBrackets() {
			return GetMyCollectionBrackets() + (ElementType is CollectionEvaluationType subCollection ? subCollection.GetCollectionBrackets() : "");
		}
		*/

		protected abstract string GetMyCollectionBrackets();

		public override bool CanImplicitCastFrom(EvaluationType other) => base.CanImplicitCastFrom(other); // All collection casting requires more information
		public abstract bool CanImplicitCastFrom(EvaluationType other, [NotNullWhen(true)] out EvaluationType? lubType);

	}

	public abstract class SequentialCollectionEvaluationType : CollectionEvaluationType {

		protected SequentialCollectionEvaluationType(EvaluationContext context, EvaluationType elementType) : base(context, elementType) { }

		protected int GetRank() {
			return 1 + (ElementType is SequentialCollectionEvaluationType sequentialElem ? sequentialElem.GetRank() : 0);
		}

	}

	public class ArrayEvaluationType : SequentialCollectionEvaluationType {

		public override Type DataType { get; }

		internal ArrayEvaluationType(EvaluationContext context, EvaluationType elementType) : base(context, elementType) {
			this.DataType = this.ElementType.DataType.MakeArrayType();

			AddField(new TypeField("length", "The number of elements in the array.", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(((Array)value.Value!).Length, value.Type.Context.GetType<IntEvaluationType>())));
			AddMethod(new ArrayContainsMethod(this));
		}

		protected override string GetMyCollectionBrackets() {
			return "[]";
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			int rank = GetRank();

			if (rank > ValueParsers.MaxArrayOrTupleRank) { throw new FormatException($"Cannot parse array with rank {rank} (max {ValueParsers.MaxArrayOrTupleRank})."); }

			return MakeArray(ElementType, StringParsing.SplitOnUnescaped(text, ValueParsers.GetArrayDelimiter(rank)).Select(v => ElementType.ParseValue(v.Trim(), source)).ToArray()).Value!;
		}

		public override string GetEvaluationString(EvaluationValue value) {
			return TryGetArray(value, out Array? array) ? ValueSerialization.ToString(array) : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
		}

		protected override object? DefaultValueData() {
			return MakeArray(ElementType, Array.Empty<EvaluationValue>()).Value;
		}

		private static bool TryGetArray(EvaluationValue value, [MaybeNullWhen(false)] out Array array) {
			if (value.Value is Array objArray) {
				array = objArray;
				return true;
			}
			else {
				array = null;
				return false;
			}
		}

		public static EvaluationValue MakeArray(EvaluationType elementType, IList<EvaluationValue> values) {
			return MakeArrayFromData(elementType, values.Select(v => v.Value).ToArray());
		}

		public static EvaluationValue MakeArrayFromData(EvaluationType elementType, object?[] values) {
			Array final = Array.CreateInstance(elementType.DataType, values.Length);
			Array.Copy(values, final, final.Length);
			return new EvaluationValue(final, elementType.MakeArray());
		}

		public override bool CanImplicitCastFrom(EvaluationType other, [NotNullWhen(true)] out EvaluationType? lubType) {
			if (other is SequentialCollectionEvaluationType collection && Context.TryGetLeastUpperBoundType(ElementType, collection.ElementType, out EvaluationType? lubElement)) {
				lubType = lubElement.MakeArray();
				return true;
			}
			else {
				lubType = null;
				return false;
			}
		}

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (other.Type.Iteration(other)?.ToArray() is EvaluationValue[] otherValues) {
				EvaluationValue[] result = new EvaluationValue[otherValues.Length];
				for (int i = 0; i < otherValues.Length; i++) {
					result[i] = ElementType.Cast(otherValues[i]) ?? throw new EvaluationTypeException($"Cannot cast element of type {otherValues[i].Type} to {ElementType}.");
				}
				return MakeArray(ElementType, result);
			}
			else {
				return null;
			}
		}

		// TODO Implement?
		/*
		public override EvaluationType? AddResult(EvaluationType right) => null;
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => null;
		public override EvaluationType? RAddResult(EvaluationType left) => null;
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => null;
		*/

		private EvaluationType? MulResultAny(EvaluationType other) {
			if (IntEvaluationType.IsIntegral(other)) {
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? PerformMul(EvaluationValue arrayValue, EvaluationValue intValue) {
			if (IntEvaluationType.TryGetInt(intValue, out int multiplier) && arrayValue.Type.Iteration(arrayValue)?.ToArray() is EvaluationValue[] values) {
				/*
				for (int v = 0; v < values.Length; v++) {
					values[v] = ElementType.Cast(values[v]) ?? throw new EvaluationTypeException($"Cannot convert element value of type {values[v].Type} to {ElementType}.");
				}
				*/

				int repeats = Math.Max(0, multiplier);
				List<EvaluationValue> result = new List<EvaluationValue>();
				for (int r = 0; r < repeats; r++) {
					result.AddRange(values);
				}
				return MakeArray(ElementType, result);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? MulResult(EvaluationType right) => MulResultAny(right);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => PerformMul(left, right);
		public override EvaluationType? RMulResult(EvaluationType left) => MulResultAny(left);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => PerformMul(right, left);

		// TODO Implement?
		/*
		public override EvaluationType? EqualResult(EvaluationType other) => null
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => null;

		public override EvaluationType? NotEqualResult(EvaluationType other) => null;
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => null;
		*/

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (IntEvaluationType.IsIntegral(index)) {
				return ElementType;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if (TryGetArray(subject, out Array? array) && IntEvaluationType.TryGetInt(index, out int indexVal)) {
				int indexFinal = EvaluationTypeHelpers.GetIndex(indexVal, array.Length);
				try {
					return new EvaluationValue(array.GetValue(indexFinal), ElementType);
				}
				catch (IndexOutOfRangeException) {
					throw new EvaluationCalculationException($"Index out of range for array indexer ({indexVal} not in {array.Length}).");
				}
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IndexerSliceResult(EvaluationType start, EvaluationType end) {
			if (IntEvaluationType.IsIntegral(start) && IntEvaluationType.IsIntegral(end)) {
				return this;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? IndexerSlice(EvaluationValue subject, EvaluationValue start, EvaluationValue end) {
			if (TryGetArray(subject, out Array? array) && IntEvaluationType.TryGetInt(start, out int startVal) && IntEvaluationType.TryGetInt(end, out int endVal)) {
				EvaluationTypeHelpers.GetSliceIndexes(startVal, endVal, array.Length, out int startFinal, out int endFinal);

				EvaluationValue[] result = new EvaluationValue[endFinal - startFinal];
				for (int idx = startFinal; idx < endFinal; idx++) {
					result[idx - startFinal] = new EvaluationValue(array.GetValue(idx), ElementType);
				}

				return MakeArray(ElementType, result);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IterationResult() => ElementType;
		public override IEnumerable<EvaluationValue>? Iteration(EvaluationValue subject) {
			if (TryGetArray(subject, out Array? array)) {
				List<EvaluationValue> result = new List<EvaluationValue>();
				foreach (object? i in array) {
					result.Add(new EvaluationValue(i, ElementType));
				}
				return result;
			}
			else {
				return null;
			}
		}

		protected override bool EqualTypeData(EvaluationType other) {
			// This should be sufficient
			return DataType == other.DataType;
		}

		protected override int GetTypeHashCode() {
			return DataType.GetHashCode();
		}

		public class ArrayContainsMethod : AbstractSingleArgMethod {
			public override EvaluationName Name { get; } = "contains";
			public override string? Description { get; } = "Returns true of the array contains the provided value, otherwise false.";

			protected override EnvironmentFunctionArg GetArgument() {
				return new EnvironmentFunctionArg("value", arrayReceiver.ElementType, "The value to check for presence in the array.");
			}

			protected override string? Warning { get; } = null;

			private readonly ArrayEvaluationType arrayReceiver;

			public ArrayContainsMethod(ArrayEvaluationType receiverType) : base(receiverType) {
				arrayReceiver = receiverType;
			}

			public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode arg) {
				EvaluationType receiverType = receiver.GetReturnType();
				EvaluationType argType = arg.GetReturnType();
				if (receiverType.IterationResult() is EvaluationType iterableType && EvaluationOps.EqualResult(iterableType, argType) is EvaluationType equalsType && BoolEvaluationType.IsBool(equalsType)) {
					return ReceiverType.Context.GetType<BoolEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {argType}.");
				}
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode arg) {
				EvaluationValue iterable = receiver.Evaluate(environment);
				EvaluationValue value = arg.Evaluate(environment);

				if (EvaluationOps.Iteration(iterable)?.ToArray() is EvaluationValue[] values) {
					for (int i = 0; i < values.Length; i++) {
						EvaluationValue equals = EvaluationOps.Equal(values[i], value) ?? throw new EvaluationCalculationException($"Cannot compares values of types {values[i].Type} and {value.Type}.");
						if (BoolEvaluationType.TryGetBool(equals, out bool isMatch) && isMatch) {
							return iterable.Type.Context.MakeValue<BoolEvaluationType>(true);
						}
					}
					return iterable.Type.Context.MakeValue<BoolEvaluationType>(false);
				}
				else {
					throw new EvaluationCalculationException($"Cannot calculate {ReceiverType}.{Name} for argument of type {value.Type}.");
				}
			}

		}

	}

	public class TupleEvaluationType : SequentialCollectionEvaluationType {

		public override Type DataType { get; }

		public int ElementCount { get; }

		internal TupleEvaluationType(EvaluationContext context, EvaluationType elementType, int elementCount) : base(context, elementType) {
			this.DataType = this.ElementType.DataType.MakeArrayType();

			this.ElementCount = elementCount;

			AddField(new TypeField("length", "The number of elements in the tuple.", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(elementCount, value.Type.Context.GetType<IntEvaluationType>())));
		}

		protected override string GetMyCollectionBrackets() {
			return $"[{ElementCount}]";
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			int rank = GetRank();

			if (rank > ValueParsers.MaxArrayOrTupleRank) { throw new FormatException($"Cannot parse tuple with rank {rank} (max {ValueParsers.MaxArrayOrTupleRank})."); }

			string[] parts = StringParsing.SplitOnUnescaped(text, ValueParsers.GetArrayDelimiter(rank));

			if (parts.Length != ElementCount) { throw new FormatException($"Invalid number of elements for tuple of length {ElementCount} (got {parts.Length})."); }

			return MakeTuple(ElementType, parts.Select(v => ElementType.ParseValue(v.Trim(), source)).ToArray()).Value!;
		}

		public override string GetEvaluationString(EvaluationValue value) {
			return TryGetTuple(value, out Array? tupleValues) ? ValueSerialization.ToString(tupleValues) : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
		}

		protected override object? DefaultValueData() {
			EvaluationValue[] defaults = new EvaluationValue[ElementCount];
			EvaluationValue defaultElemValue = ElementType.DefaultValue();
			for (int i = 0; i < ElementCount; i++) {
				defaults[i] = defaultElemValue;
			}
			return MakeTuple(ElementType, defaults).Value;
		}

		private static bool TryGetTuple(EvaluationValue value, [MaybeNullWhen(false)] out Array array) {
			if (value.Value is Array objArray) {
				array = objArray;
				return true;
			}
			else {
				array = null;
				return false;
			}
		}

		public static EvaluationValue MakeTuple(EvaluationType elementType, IList<EvaluationValue> values) {
			return MakeTupleFromData(elementType, values.Select(v => v.Value).ToArray());
		}

		public static EvaluationValue MakeTupleFromData(EvaluationType elementType, object?[] values) {
			Array final = Array.CreateInstance(elementType.DataType, values.Length);
			Array.Copy(values, final, final.Length);
			return new EvaluationValue(final, elementType.MakeTuple(values.Length));
		}

		public override bool CanImplicitCastFrom(EvaluationType other, [NotNullWhen(true)] out EvaluationType? lubType) {
			if (other is TupleEvaluationType otherTuple && ElementCount == otherTuple.ElementCount && Context.TryGetLeastUpperBoundType(ElementType, otherTuple.ElementType, out EvaluationType? lubElement)) {
				lubType = lubElement.MakeTuple(ElementCount);
				return true;
			}
			else {
				lubType = null;
				return false;
			}
		}

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (other.Type.Iteration(other)?.ToArray() is EvaluationValue[] otherValues && otherValues.Length == ElementCount) {
				EvaluationValue[] result = new EvaluationValue[otherValues.Length];
				for (int i = 0; i < otherValues.Length; i++) {
					result[i] = ElementType.Cast(otherValues[i]) ?? throw new EvaluationTypeException($"Cannot cast element of type {otherValues[i].Type} to {ElementType}.");
				}
				return MakeTuple(ElementType, result);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (IntEvaluationType.IsIntegral(index)) {
				return ElementType;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if (TryGetTuple(subject, out Array? tupleData) && IntEvaluationType.TryGetInt(index, out int indexVal)) {
				if (tupleData.Length != ElementCount) { throw new EvaluationTypeException($"Invalid tuple data (expected {ElementCount} entries, found {tupleData.Length})."); }
				int indexFinal = EvaluationTypeHelpers.GetIndex(indexVal, ElementCount);
				return new EvaluationValue(tupleData.GetValue(indexFinal), ElementType);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IterationResult() => ElementType;
		public override IEnumerable<EvaluationValue>? Iteration(EvaluationValue subject) {
			if (TryGetTuple(subject, out Array? tupleData)) {
				if (tupleData.Length != ElementCount) { throw new EvaluationTypeException($"Invalid tuple data (expected {ElementCount} entries, found {tupleData.Length})."); }
				EvaluationValue[] values = new EvaluationValue[ElementCount];
				for (int i = 0; i < ElementCount; i++) {
					values[i] = new EvaluationValue(tupleData.GetValue(i), ElementType);
				}
				return values;
			}
			else {
				return null;
			}
		}

		protected override bool EqualTypeData(EvaluationType other) {
			return other is TupleEvaluationType otherTuple
				&& ElementCount == otherTuple.ElementCount
				&& DataType == other.DataType;
		}

		protected override int GetTypeHashCode() {
			return DataType.GetHashCode();
		}

	}

	public class DictionaryEvaluationType : CollectionEvaluationType {

		public EvaluationType KeyType { get; }

		public override Type DataType { get; } = typeof(OrderedDictionary);

		internal DictionaryEvaluationType(EvaluationContext context, EvaluationType keyType, EvaluationType elementType) : base(context, elementType) {
			this.KeyType = keyType;

			AddField(new TypeField("keys", "An array of the keys of this dictionary.", KeyType.MakeArray(), value => ArrayEvaluationType.MakeArrayFromData(KeyType, ((IDictionary)value.Value!).Keys.Cast<object>().ToArray())));
			AddField(new TypeField("values", "An array of the values of this dictionary.", ElementType.MakeArray(), value => ArrayEvaluationType.MakeArrayFromData(ElementType, ((IDictionary)value.Value!).Values.Cast<object?>().ToArray())));
			AddField(new TypeField("count", "The number of entries in the dictionary.", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(((IDictionary)value.Value!).Count, value.Type.Context.GetType<IntEvaluationType>())));
		}

		protected override string GetMyCollectionBrackets() {
			return $"[{KeyType.Name}]";
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			string[] parts = Escaping.SplitUnescaped(text, ',');

			List<(EvaluationValue key, EvaluationValue value)> entries = new List<(EvaluationValue, EvaluationValue)>();

			foreach (string part in parts) {
				string[] keyValue = Escaping.SplitUnescaped(part, ':', 2, StringSplitOptions.TrimEntries);

				if (keyValue.Length != 2) { throw new FormatException("Invalid dictionary entry literal value."); }

				EvaluationValue key = KeyType.ParseValue(keyValue[0], source);
				EvaluationValue value = ElementType.ParseValue(keyValue[1], source);

				entries.Add((key, value));
			}

			return MakeDictionary(KeyType, ElementType, entries).Value!;
		}

		private static readonly char[] dictPartsEscapedChars = new char[] { ',' };
		private static readonly char[] dictEntryEscapedChars = new char[] { ':' };
		public override string GetEvaluationString(EvaluationValue value) {
			if (value.Type is DictionaryEvaluationType dictType) {
				List<string> parts = new List<string>();
				foreach (EvaluationValue keyValue in dictType.Iteration(value) ?? Enumerable.Empty<EvaluationValue>()) {
					EvaluationValue valueValue = dictType.Indexer(value, keyValue) ?? throw new EvaluationCalculationException($"Cannot extract value from dictionary ({Name}).");
					parts.Add($"{Escaping.Escape(keyValue.ToEvaluationString(), dictEntryEscapedChars)}: {Escaping.Escape(valueValue.ToEvaluationString(), dictEntryEscapedChars)}");
				}
				return string.Join(", ", parts.Select(static p => Escaping.Escape(p, dictPartsEscapedChars)));
			}
			else {
				throw new EvaluationCalculationException($"Invalid data for {Name}.");
			}
		}

		protected override object? DefaultValueData() {
			return MakeDictionary(KeyType, ElementType, Array.Empty<(EvaluationValue, EvaluationValue)>()).Value;
		}

		private static bool TryGetDictionary(EvaluationValue value, [MaybeNullWhen(false)] out IDictionary dict, [MaybeNullWhen(false)] out DictionaryEvaluationType dictType) {
			if (value.Type is DictionaryEvaluationType dictEvalType && value.Value is IDictionary dictObj) {
				dict = dictObj;
				dictType = dictEvalType;
				return true;
			}
			else {
				dict = null;
				dictType = null;
				return false;
			}
		}

		public static EvaluationValue MakeDictionary(EvaluationType keyType, EvaluationType elementType, IList<(EvaluationValue key, EvaluationValue value)> entries) {
			EvaluationType dictType = new DictionaryEvaluationType(keyType.Context, keyType, elementType);
			IDictionary dict = new OrderedDictionary();

			foreach ((EvaluationValue key, EvaluationValue value) in entries) {
				dict.Add(
					key.Value ?? throw new EvaluationCalculationException("Invalid null value provided for dictionary key."),
					value.Value
					);
			}

			return new EvaluationValue(dict, dictType);
		}

		public override bool CanImplicitCastFrom(EvaluationType other, [NotNullWhen(true)] out EvaluationType? lubType) {
			if (other is DictionaryEvaluationType dict
				&& Context.TryGetLeastUpperBoundType(KeyType, dict.KeyType, out EvaluationType? lubKey)
				&& Context.TryGetLeastUpperBoundType(ElementType, dict.ElementType, out EvaluationType? lubElement)
				) {

				lubType = new DictionaryEvaluationType(Context, lubKey, lubElement);
				return true;
			}
			else {
				lubType = null;
				return false;
			}
		}

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetDictionary(other, out IDictionary? dict, out DictionaryEvaluationType? dictType)) {
				List<(EvaluationValue key, EvaluationValue value)> result = new List<(EvaluationValue key, EvaluationValue value)>();

				foreach (object key in dict.Keys) {
					result.Add((
						KeyType.Cast(new EvaluationValue(key, dictType.KeyType)) ?? throw new EvaluationTypeException($"Cannot cast dictionary key of type {dictType.KeyType} to {KeyType}."),
						ElementType.Cast(new EvaluationValue(dict[key], dictType.ElementType)) ?? throw new EvaluationTypeException($"Cannot cast dictionary value of type {dictType.ElementType} to {ElementType}.")
						));
				}

				return MakeDictionary(KeyType, ElementType, result);
			}
			else {
				return null;
			}
		}

		// TODO Implement?
		/*
		public override EvaluationType? EqualResult(EvaluationType other) => null
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => null;

		public override EvaluationType? NotEqualResult(EvaluationType other) => null;
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => null;
		*/

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (KeyType.CanImplicitCastFrom(index)) {
				return ElementType;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if (TryGetDictionary(subject, out IDictionary? dict, out _) && KeyType.Cast(index) is EvaluationValue indexCast) {
				object key = indexCast.Value ?? throw new EvaluationCalculationException("Invalid null dictionary key.");
				object? value = dict.Contains(key) ? dict[key] : throw new EvaluationCalculationException($"No value for key in dictionary: {key}");
				return new EvaluationValue(value, ElementType);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IterationResult() => KeyType;
		public override IEnumerable<EvaluationValue>? Iteration(EvaluationValue subject) {
			if (TryGetDictionary(subject, out IDictionary? dict, out _)) {
				List<EvaluationValue> result = new List<EvaluationValue>();
				foreach (object key in dict.Keys) {
					result.Add(new EvaluationValue(key, KeyType));
				}
				return result;
			}
			else {
				return null;
			}
		}

		protected override bool EqualTypeData(EvaluationType other) {
			return other is DictionaryEvaluationType otherDict
				&& otherDict.KeyType == KeyType
				&& otherDict.ElementType == ElementType;
		}

		protected override int GetTypeHashCode() {
			return DataType.GetHashCode();
		}

	}

}
