using SharpSheets.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using SharpSheets.Parsing;
using SharpSheets.Evaluations.Nodes;

namespace SharpSheets.Evaluations.Types {

	public sealed class StringEvaluationType : SingleDataType<string> {

		public override string Name { get; } = "str";

		public StringEvaluationType(EvaluationContext context) : base(context) {
			AddField(new TypeField("length", "The length of (number of characters in) this string.", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(((string)value.Value!).Length, value.Type.Context.GetType<IntEvaluationType>())));
			AddMethod(new StringRepeatMethod(this));
			AddMethod(new StringJoinMethod(this));
			AddMethod(new StringContainsMethod(this));
		}

		protected override string ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseString(text);
		}

		protected override string GetEvaluationSingleString(string value) {
			//return $"\"{value}\"";
			return value;
		}

		protected override string DefaultValueDataSingle() {
			return string.Empty;
		}

		public static bool IsString(EvaluationType other) {
			return other is StringEvaluationType;
		}

		public static bool AllString(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsString(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetString(EvaluationValue value, [NotNullWhen(true)] out string? str) {
			if (value.Type is StringEvaluationType && value.Value is string stringVal) {
				str = stringVal;
				return true;
			}

			str = null;
			return false;
		}

		public override IEnvironmentFunction GetTypeFunction() => StringCastFunction.Instance;

		public override bool CanImplicitCastFrom(EvaluationType other) => IsString(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetString(other, out string? value)) {
				return new EvaluationValue(value, this);
			}
			else {
				return null;
			}
		}

		private StringEvaluationType? AddResultAny(EvaluationType other) {
			if (IsString(other)) { // Another string
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? AddAny(EvaluationValue left, EvaluationValue right) {
			if (TryGetString(left, out string? leftVal) && TryGetString(right, out string? rightVal)) {
				return new EvaluationValue(leftVal + rightVal, this);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? AddResult(EvaluationType right) => AddResultAny(right);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => AddAny(left, right);
		public override EvaluationType? RAddResult(EvaluationType left) => AddResultAny(left);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => AddAny(left, right);

		private StringEvaluationType? MulResultAny(EvaluationType other) {
			if (IntEvaluationType.IsIntegral(other)) { // An int-like
				return this;
			}
			else {
				return null;
			}
		}

		private static string RepeatString(string str, int count) {
			return new StringBuilder(str.Length * count).Insert(0, str, Math.Max(0, count)).ToString();
		}

		public override EvaluationType? MulResult(EvaluationType right) => MulResultAny(right);
		public override EvaluationType? RMulResult(EvaluationType left) => MulResultAny(left);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) {
			if (TryGetString(left, out string? stringVal) && IntEvaluationType.TryGetInt(right, out int intVal)) {
				return new EvaluationValue(RepeatString(stringVal, intVal), this);
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) {
			if (IntEvaluationType.TryGetInt(left, out int intVal) && TryGetString(right, out string? stringVal)) {
				return new EvaluationValue(RepeatString(stringVal, intVal), this);
			}
			else {
				return null;
			}
		}

		private BoolEvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsString(other)) { // Another string
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<string, string, bool> operation) {
			if (TryGetString(left, out string? leftVal) && TryGetString(right, out string? rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => string.CompareOrdinal(a, b) < 0);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => string.CompareOrdinal(a, b) <= 0);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => string.CompareOrdinal(a, b) > 0);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => string.CompareOrdinal(a, b) >= 0);


		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a.Equals(b));

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => !a.Equals(b));

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (IntEvaluationType.IsIntegral(index)) {
				return this;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if(TryGetString(subject, out string? subjectVal) && IntEvaluationType.TryGetInt(index, out int indexVal)) {
				int indexFinal = EvaluationTypeHelpers.GetIndex(indexVal, subjectVal.Length);
				return new EvaluationValue(subjectVal[indexFinal].ToString(), this);
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
			if (TryGetString(subject, out string? subjectVal) && IntEvaluationType.TryGetInt(start, out int startVal) && IntEvaluationType.TryGetInt(end, out int endVal)) {
				EvaluationTypeHelpers.GetSliceIndexes(startVal, endVal, subjectVal.Length, out int startFinal, out int endFinal);
				return new EvaluationValue(subjectVal[startFinal..endFinal].ToString(), this);
			}
			else {
				return null;
			}
		}

		public class StringCastFunction : AbstractFunction {

			public static readonly StringCastFunction Instance = new StringCastFunction();
			private StringCastFunction() { }

			public override EvaluationName Name { get; } = "str";
			public override string? Description { get; } = "Converts the argument into a string value. Real values will be serialised as decimal numbers, bools as true/false, enums as the value name, and strings will be unchanged.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<IntEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("enumVal", null, null))
				);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType argType = args[0].GetReturnType();
				if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType) || EnumEvaluationType.IsEnum(argType)) {
					return context.GetType<StringEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to string.");
				}
			}

			private static EvaluationValue MakeResult(string result, EvaluationContext context) {
				return new EvaluationValue(result, context.GetType<StringEvaluationType>());
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue a = args[0].Evaluate(environment);

				if (FloatEvaluationType.IsReal(a.Type) || BoolEvaluationType.IsBool(a.Type) || StringEvaluationType.IsString(a.Type)) {
					return MakeResult(a.Value?.ToString() ?? "", environment.Context); // Sensible fallback?
				}
				else if (EnumEvaluationType.IsEnum(a.Type)) {
					if (a.Value is null) {
						throw new EvaluationCalculationException("Cannot convert null enum value to string.");
					}
					return MakeResult((a.ToString() ?? throw new EvaluationCalculationException("Could not resolve enum name.")).ToUpperInvariant(), environment.Context);
				}
				else {
					throw new EvaluationCalculationException($"Cannot cast variable of type {a.Type} to string.");
				}
			}

			/// <summary></summary>
			/// <exception cref="EvaluationCalculationException"></exception>
			/// <exception cref="EvaluationTypeException"></exception>
			/// <exception cref="EvaluationProcessingException"></exception>
			public static EvaluationNode MakeStringCastNode(EvaluationNode argument) {
				EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, argument.Context);
				node.SetArguments(argument);
				return node.Simplify();
			}
		}

		public class StringRepeatMethod : AbstractSingleArgMethod {
			public override EvaluationName Name { get; } = "repeat";
			public override string? Description { get; } = "Repeat the string content a given number of times.";

			protected override EnvironmentFunctionArg GetArgument() {
				return new EnvironmentFunctionArg("count", ReceiverType.Context.GetType<IntEvaluationType>(), "The number of times the string should be repeated.");
			}

			protected override string? Warning { get; } = null;

			public StringRepeatMethod(StringEvaluationType receiverType) : base(receiverType) { }

			public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode arg) {
				EvaluationType receiverType = receiver.GetReturnType();
				EvaluationType argType = arg.GetReturnType();
				return (IsString(receiverType) && IntEvaluationType.IsIntegral(argType)) ? receiver.Context.GetType<StringEvaluationType>() : throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {argType}.");
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode arg) {
				EvaluationValue r = receiver.Evaluate(environment);
				EvaluationValue a = arg.Evaluate(environment);

				if (TryGetString(r, out string? str) && IntEvaluationType.TryGetInt(a, out int count)) {
					return new EvaluationValue(string.Join("", str.Yield().Repeat(count)), environment.GetType<StringEvaluationType>());
				}
				else {
					throw new EvaluationTypeException($"Mathematical functions are not defined for value of type {a.Type}.");
				}
			}
		}

		public class StringJoinMethod : AbstractSingleArgMethod {
			public override EvaluationName Name { get; } = "join";
			public override string? Description { get; } = "Join the provided values together using the current string as a separator.";

			protected override EnvironmentFunctionArg GetArgument() {
				return new EnvironmentFunctionArg("values", ReceiverType.Context.GetType<StringEvaluationType>().MakeArray(), "The string values to be joined together.");
			}

			protected override string? Warning { get; } = null;

			public StringJoinMethod(StringEvaluationType receiverType) : base(receiverType) { }

			public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode arg) {
				EvaluationType receiverType = receiver.GetReturnType();
				EvaluationType argType = arg.GetReturnType();
				return (IsString(receiverType) && argType.IterationResult() is EvaluationType argIterType && IsString(argIterType)) ? receiver.Context.GetType<StringEvaluationType>() : throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {argType}.");
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode arg) {
				EvaluationValue r = receiver.Evaluate(environment);
				EvaluationValue a = arg.Evaluate(environment);

				if (TryGetString(r, out string? separator) && a.Type.Iteration(a)?.ToArray() is EvaluationValue[] values) {
					string[] parts = values.Select(v => TryGetString(v, out string? s) ? s : throw new EvaluationCalculationException($"Invalid data type for {Name} argument.")).ToArray();
					string result = string.Join(separator, parts);
					return new EvaluationValue(result, environment.GetType<StringEvaluationType>());
				}
				else {
					throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {a.Type}.");
				}
			}
		}

		public class StringContainsMethod : AbstractSingleArgMethod {
			public override EvaluationName Name { get; } = "contains";
			public override string? Description { get; } = "Returns true of the string contains the provided substring, otherwise false.";

			protected override EnvironmentFunctionArg GetArgument() {
				return new EnvironmentFunctionArg("substring", ReceiverType.Context.GetType<StringEvaluationType>(), "The substring to check for.");
			}

			protected override string? Warning { get; } = null;

			public StringContainsMethod(StringEvaluationType receiverType) : base(receiverType) { }

			public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode arg) {
				EvaluationType receiverType = receiver.GetReturnType();
				EvaluationType argType = arg.GetReturnType();
				if (IsString(receiverType) && IsString(argType)) {
					return ReceiverType.Context.GetType<BoolEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {argType}.");
				}
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode arg) {
				EvaluationValue str = receiver.Evaluate(environment);
				EvaluationValue value = arg.Evaluate(environment);

				if (TryGetString(str, out string? baseStr) && TryGetString(value, out string? substring)) {
					return str.Type.Context.MakeValue<BoolEvaluationType>(baseStr.Contains(substring));
				}
				else {
					throw new EvaluationCalculationException($"Cannot calculate {ReceiverType}.{Name} for argument of type {value.Type}.");
				}
			}

		}

	}

}
