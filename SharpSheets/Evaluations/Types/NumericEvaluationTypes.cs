using SharpSheets.Utilities;
using SharpSheets.Parsing;
using System.Globalization;
using SharpSheets.Evaluations.Nodes;
using System.Text.RegularExpressions;

namespace SharpSheets.Evaluations.Types {

	public sealed partial class FloatEvaluationType : SingleDataType<float> {

		public override string Name { get; } = "float";

		public FloatEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MINVALUE", $"The most negative float value, {float.MinValue}.", this, t => new EvaluationValue(float.MinValue, this)));
			AddStaticField(new TypeField("MAXVALUE", $"The largest positive float value, {float.MaxValue}.", this, t => new EvaluationValue(float.MaxValue, this)));

			AddMethod(new NumericClampMethod(this));
		}

		protected override float ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseFloat(text);
		}

		protected override string GetEvaluationSingleString(float value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override float DefaultValueDataSingle() {
			return 0f;
		}

		public static bool IsReal(EvaluationType type) {
			return type is FloatEvaluationType || UFloatEvaluationType.IsPositiveReal(type) || IntEvaluationType.IsIntegral(type);
		}

		public static bool AllReal(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsReal(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetFloat(EvaluationValue value, out float number) {
			if (value.Type is FloatEvaluationType && value.Value is float floatVal) {
				number = floatVal;
				return true;
			}
			else if (UFloatEvaluationType.TryGetUFloat(value, out UFloat uFloatVal)) {
				number = uFloatVal;
				return true;
			}
			else if (IntEvaluationType.TryGetInt(value, out int intVal)) {
				number = intVal;
				return true;
			}

			number = 0f;
			return false;
		}

		public override IEnvironmentFunction GetTypeFunction() => FloatCastFunction.Instance;

		public override bool CanImplicitCastFrom(EvaluationType other) => IsReal(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetFloat(other, out float value)) {
				return new EvaluationValue(value, this);
			}
			else {
				return null;
			}
		}

		private EvaluationValue? UnaryArithmetic(EvaluationValue operand, Func<float, float> operation) {
			if (TryGetFloat(operand, out float operandVal)) {
				return new EvaluationValue(operation(operandVal), this);
			}
			else {
				return null;
			}
		}

		private FloatEvaluationType? BinaryArithmeticResult(EvaluationType other) {
			if (IsReal(other)) { // Another float-like
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperation(EvaluationValue left, EvaluationValue right, Func<float, float, float> operation) {
			if (TryGetFloat(left, out float leftVal) && TryGetFloat(right, out float rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperation(EvaluationValue left, EvaluationValue right, Func<float, float, bool> operation) {
			if (TryGetFloat(left, out float leftVal) && TryGetFloat(right, out float rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		private BoolEvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsReal(other)) { // Another float-like
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => this;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => +a);

		public override EvaluationType? NegResult() => this;
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => -a);

		public override EvaluationType? AddResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RAddResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a + b);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a + b);

		public override EvaluationType? SubResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RSubResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a - b);
		public override EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a - b);

		public override EvaluationType? MulResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RMulResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a * b);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a * b);

		public override EvaluationType? DivResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RDivResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a / b);
		public override EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a / b);

		public override EvaluationType? ModResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RModResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a % b);
		public override EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a % b);

		public override EvaluationType? PowResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RPowResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => (float)Math.Pow(a, b));
		public override EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => (float)Math.Pow(a, b));

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a < b);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a <= b);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a > b);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a >= b);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a != b);

		public partial class FloatCastFunction : AbstractFunction {

			public static readonly FloatCastFunction Instance = new FloatCastFunction();
			private FloatCastFunction() { }

			public override EvaluationName Name { get; } = "float";
			public override string? Description { get; } = "Converts the argument into a floating point number. Strings will be parsed, bools and integers will be cast, and floating point numbers will be unchanged.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null))
				);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType argType = args[0].GetReturnType();
				if (IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType)) {
					return context.GetType<FloatEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to float.");
				}
			}

			private static EvaluationValue MakeResult(float result, EvaluationContext context) {
				return new EvaluationValue(result, context.GetType<FloatEvaluationType>());
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue a = args[0].Evaluate(environment);

				if (TryGetFloat(a, out float aFloat)) {
					return MakeResult(aFloat, environment.Context);
				}
				else if (IntEvaluationType.TryGetInt(a, out int aInt)) {
					return MakeResult(aInt, environment.Context);
				}
				else if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
					return MakeResult(aBool ? 1f : 0f, environment.Context);
				}
				else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
					return MakeResult(Parse(aStr), environment.Context);
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {a.Type} to float.");
				}
			}

			[GeneratedRegex(@"^\s*[\-\+]?\s*([0-9]+(\.[0-9]*)?|\.[0-9]+)\s*$")]
			private static partial Regex FloatRegex();
			[GeneratedRegex(@"^\s*(?<numer>[\-\+]?\s*([0-9]+(\.[0-9]*)?|\.[0-9]+))\s*\/\s*(?<denom>([0-9]+(\.[0-9]*)?|\.[0-9]+))\s*")]
			private static partial Regex FracRegex();
			/// <summary></summary>
			/// <exception cref="EvaluationCalculationException"></exception>
			private static float Parse(string str) {
				try {
					Match match;
					if (FloatRegex().IsMatch(str)) {
						return float.Parse(str.Replace(" ", ""));
					}
					else if ((match = FracRegex().Match(str)).Success) {
						float numer = float.Parse(match.Groups["numer"].Value.Replace(" ", ""));
						float denom = float.Parse(match.Groups["denom"].Value.Replace(" ", ""));
						return numer / denom;
					}
				}
				catch (FormatException) { }

				throw new EvaluationCalculationException($"Provided string is not a valid float: \"{str}\"");
			}

			/// <summary></summary>
			/// <exception cref="EvaluationCalculationException"></exception>
			/// <exception cref="EvaluationTypeException"></exception>
			/// <exception cref="EvaluationProcessingException"></exception>
			public static EvaluationNode MakeFloatCastNode(EvaluationNode argument) {
				EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, argument.Context);
				node.SetArguments(argument);
				return node.Simplify();
			}
		}
	}

	public sealed class UFloatEvaluationType : SingleDataType<UFloat> {

		public override string Name { get; } = "ufloat";

		public UFloatEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MAXVALUE", $"The largest positive ufloat value, {UFloat.MaxValue}.", this, t => new EvaluationValue(UFloat.MaxValue, this)));

			AddMethod(new NumericClampMethod(this));
		}

		protected override UFloat ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseUFloat(text);
		}

		protected override string GetEvaluationSingleString(UFloat value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override UFloat DefaultValueDataSingle() {
			return UFloat.Zero;
		}

		public static bool IsPositiveReal(EvaluationType type) {
			return type is UFloatEvaluationType || UIntEvaluationType.IsPositiveIntegral(type);
		}

		public static bool AllPositiveReal(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsPositiveReal(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetUFloat(EvaluationValue value, out UFloat number) {
			if (value.Type is UFloatEvaluationType && value.Value is UFloat uFloatVal) {
				number = uFloatVal;
				return true;
			}
			else if (UIntEvaluationType.TryGetUInt(value, out uint uintVal)) {
				number = new UFloat(uintVal);
				return true;
			}

			number = UFloat.Zero;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsPositiveReal(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetUFloat(other, out UFloat value)) {
				return new EvaluationValue(value, this);
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? UnaryArithmetic<T>(EvaluationValue operand, Func<UFloat, T> operation, EvaluationType resultType) {
			if (TryGetUFloat(operand, out UFloat operandVal)) {
				return new EvaluationValue(operation(operandVal), resultType);
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryArithmeticResult(EvaluationType other, bool closed) {
			if (IsPositiveReal(other)) {
				return closed ? this : Context.GetType<FloatEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationClosed(EvaluationValue left, EvaluationValue right, Func<UFloat, UFloat, UFloat> operation) {
			if (TryGetUFloat(left, out UFloat leftVal) && TryGetUFloat(right, out UFloat rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationNotClosed(EvaluationValue left, EvaluationValue right, Func<UFloat, UFloat, float> operation) {
			if (TryGetUFloat(left, out UFloat leftVal) && TryGetUFloat(right, out UFloat rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<FloatEvaluationType>());
			}
			else {
				return null;
			}
		}

		private BoolEvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsPositiveReal(other)) { // Another UFloat-like
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<UFloat, UFloat, bool> operation) {
			if (TryGetUFloat(left, out UFloat leftVal) && TryGetUFloat(right, out UFloat rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => this;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => a, this);

		public override EvaluationType? NegResult() => Context.GetType<FloatEvaluationType>();
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => -a.Value, Context.GetType<FloatEvaluationType>());

		public override EvaluationType? AddResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RAddResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a + b);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a + b);

		public override EvaluationType? SubResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RSubResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		public override EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => a - b);
		public override EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => a - b);

		public override EvaluationType? MulResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RMulResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a * b);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a * b);

		public override EvaluationType? DivResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RDivResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a / b);
		public override EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a / b);

		public override EvaluationType? ModResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RModResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		// Closed or not closed?
		public override EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => a % b);
		public override EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => a % b);

		public override EvaluationType? PowResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RPowResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		// Closed or not closed?
		public override EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (float)Math.Pow(a, b));
		public override EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (float)Math.Pow(a, b));

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a < b);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a <= b);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a > b);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a >= b);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a != b);
	}

	public sealed class IntEvaluationType : SingleDataType<int> {

		public override string Name { get; } = "int";

		public IntEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MINVALUE", $"The most negative int value, {int.MinValue}.", this, t => new EvaluationValue(int.MinValue, this)));
			AddStaticField(new TypeField("MAXVALUE", $"The largest positive int value, {int.MaxValue}.", this, t => new EvaluationValue(int.MaxValue, this)));

			AddMethod(new NumericClampMethod(this));
		}

		protected override int ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseInt(text);
		}

		protected override string GetEvaluationSingleString(int value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override int DefaultValueDataSingle() {
			return 0;
		}

		public static bool IsIntegral(EvaluationType type) {
			return type is IntEvaluationType || UIntEvaluationType.IsPositiveIntegral(type);
		}

		public static bool AllIntegral(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsIntegral(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetInt(EvaluationValue value, out int number) {
			if (value.Type is IntEvaluationType && value.Value is int intVal) {
				number = intVal;
				return true;
			}
			else if (UIntEvaluationType.TryGetUInt(value, out uint uintVal)) {
				number = (int)uintVal;
				return true;
			}

			number = 0;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsIntegral(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetInt(other, out int value)) {
				return new EvaluationValue(value, this);
			}
			else {
				return null;
			}
		}

		public override IEnvironmentFunction? GetTypeFunction() => IntCastFunction.Instance;

		private EvaluationValue? UnaryArithmetic(EvaluationValue operand, Func<int, int> operation) {
			if (TryGetInt(operand, out int operandVal)) {
				return new EvaluationValue(operation(operandVal), this);
			}
			else {
				return null;
			}
		}

		private IntEvaluationType? BinaryArithmeticResult(EvaluationType other) {
			if (IsIntegral(other)) { // Another int-like
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationAny(EvaluationValue left, EvaluationValue right, Func<int, int, int> operation) {
			if (TryGetInt(left, out int leftVal) && TryGetInt(right, out int rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		private BoolEvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsIntegral(other)) { // Another float-like
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<int, int, bool> operation) {
			if (TryGetInt(left, out int leftVal) && TryGetInt(right, out int rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => this;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => +a);

		public override EvaluationType? NegResult() => this;
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => -a);

		public override EvaluationType? AddResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RAddResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a + b);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a + b);

		public override EvaluationType? SubResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RSubResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a - b);
		public override EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a - b);

		public override EvaluationType? MulResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RMulResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a * b);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a * b);

		public override EvaluationType? DivResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RDivResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a / b);
		public override EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a / b);

		public override EvaluationType? ModResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RModResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a % b);
		public override EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a % b);

		public override EvaluationType? PowResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RPowResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => (int)Math.Pow(a, b));
		public override EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => (int)Math.Pow(a, b));

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a < b);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a <= b);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a > b);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a >= b);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a != b);

		public class IntCastFunction : AbstractFunction {

			public static readonly IntCastFunction Instance = new IntCastFunction();
			private IntCastFunction() { }

			public override EvaluationName Name { get; } = "int";
			public override string? Description { get; } = "Converts the argument into an integer. Strings will be parsed, floats will be rounded down, bools cast, and integers unchanged.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null))
				);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType argType = args[0].GetReturnType();
				if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType)) {
					return context.GetType<IntEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to integer.");
				}
			}

			private static EvaluationValue MakeResult(int result, EvaluationContext context) {
				return new EvaluationValue(result, context.GetType<IntEvaluationType>());
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue a = args[0].Evaluate(environment);

				if (TryGetInt(a, out int aInt)) {
					return MakeResult(aInt, environment.Context);
				}
				else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
					return MakeResult((int)aFloat, environment.Context);
				}
				else if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
					return MakeResult(aBool ? 1 : 0, environment.Context);
				}
				else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
					if (int.TryParse(aStr, out int parsed)) {
						return MakeResult(parsed, environment.Context);
					}
					else {
						throw new EvaluationCalculationException($"Provided string is not a valid int: \"{aStr}\"");
					}
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {a.Type} to integer.");
				}
			}

			public static EvaluationNode MakeIntCastNode(EvaluationNode argument) {
				EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, argument.Context);
				node.SetArguments(argument);
				return node.Simplify();
			}
		}
	}

	public sealed class UIntEvaluationType : SingleDataType<uint> {

		public override string Name { get; } = "uint";

		public UIntEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MAXVALUE", $"The largest positive uint value, {uint.MaxValue}.", this, t => new EvaluationValue(uint.MaxValue, this)));

			AddMethod(new NumericClampMethod(this));
		}

		protected override uint ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseUInt(text);
		}

		protected override string GetEvaluationSingleString(uint value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override uint DefaultValueDataSingle() {
			return 0U;
		}

		public static bool IsPositiveIntegral(EvaluationType type) {
			return type is UIntEvaluationType || BoolEvaluationType.IsBool(type);
		}

		public static bool AllPositiveIntegral(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsPositiveIntegral(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetUInt(EvaluationValue value, out uint number) {
			if (value.Type is UIntEvaluationType && value.Value is uint uintVal) {
				number = uintVal;
				return true;
			}
			else if (BoolEvaluationType.TryGetBool(value, out bool boolean)) {
				number = boolean ? 1u : 0u;
				return true;
			}

			number = 0U;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsPositiveIntegral(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetUInt(other, out uint value)) {
				return new EvaluationValue(value, this);
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? UnaryArithmetic<T>(EvaluationValue operand, Func<uint, T> operation, EvaluationType resultType) {
			if (TryGetUInt(operand, out uint operandVal)) {
				return new EvaluationValue(operation(operandVal), resultType);
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryArithmeticResult(EvaluationType other, bool closed) {
			if (IsPositiveIntegral(other)) {
				return closed ? this : Context.GetType<IntEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationClosed(EvaluationValue left, EvaluationValue right, Func<uint, uint, uint> operation) {
			if (TryGetUInt(left, out uint leftVal) && TryGetUInt(right, out uint rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationNotClosed(EvaluationValue left, EvaluationValue right, Func<uint, uint, int> operation) {
			if (TryGetUInt(left, out uint leftVal) && TryGetUInt(right, out uint rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<IntEvaluationType>());
			}
			else {
				return null;
			}
		}

		private BoolEvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsPositiveIntegral(other)) { // Another uint-like
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<uint, uint, bool> operation) {
			if (TryGetUInt(left, out uint leftVal) && TryGetUInt(right, out uint rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => this;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => a, this);

		public override EvaluationType? NegResult() => Context.GetType<IntEvaluationType>();
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => (int)(-a), Context.GetType<IntEvaluationType>());

		public override EvaluationType? AddResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RAddResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a + b);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a + b);

		public override EvaluationType? SubResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RSubResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		public override EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)a - (int)b);
		public override EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)a - (int)b);

		public override EvaluationType? MulResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RMulResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a * b);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a * b);

		public override EvaluationType? DivResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RDivResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a / b);
		public override EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a / b);

		public override EvaluationType? ModResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RModResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		// Closed or not closed?
		public override EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)a % (int)b);
		public override EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)a % (int)b);

		public override EvaluationType? PowResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RPowResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		// Closed or not closed?
		public override EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)Math.Pow(a, b));
		public override EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)Math.Pow(a, b));

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a < b);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a <= b);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a > b);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a >= b);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a != b);
	}

	public sealed class BoolEvaluationType : SingleDataType<bool> {

		public override string Name { get; } = "bool";

		public BoolEvaluationType(EvaluationContext context) : base(context) { }

		protected override bool ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseBool(text);
		}

		protected override string GetEvaluationSingleString(bool value) {
			return value ? "true" : "false";
		}

		protected override bool DefaultValueDataSingle() {
			return false;
		}

		public static bool IsBool(EvaluationType other) {
			return other is BoolEvaluationType;
		}

		public static bool AllBool(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsBool(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetBool(EvaluationValue value, out bool boolean) {
			if (value.Type is BoolEvaluationType && value.Value is bool boolVal) {
				boolean = boolVal;
				return true;
			}

			boolean = false;
			return false;
		}

		public override IEnvironmentFunction GetTypeFunction() => BoolCastFunction.Instance;

		public override bool CanImplicitCastFrom(EvaluationType other) => IsBool(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetBool(other, out bool value)) {
				return new EvaluationValue(value, this);
			}
			else {
				return null;
			}
		}

		public static bool TryCastBool(EvaluationValue value, out bool boolean) {
			if (TryGetBool(value, out bool boolVal)) {
				boolean = boolVal;
				return true;
			}
			else if (IntEvaluationType.TryGetInt(value, out int intVal)) {
				boolean = intVal != 0;
				return true;
			}
			else if (FloatEvaluationType.TryGetFloat(value, out float floatVal)) {
				boolean = floatVal != 0f;
				return true;
			}

			boolean = false;
			return false;
		}

		private EvaluationValue? UnaryOperationAny(EvaluationValue operand, Func<bool, bool> operation) {
			if (TryGetBool(operand, out bool value)) {
				return new EvaluationValue(operation(value), this);
			}
			else {
				return null;
			}
		}

		private BoolEvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsBool(other)) { // Another bool
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<bool, bool, bool> operation) {
			if (TryGetBool(left, out bool leftVal) && TryGetBool(right, out bool rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? InvertResult() => this;
		public override EvaluationValue? Invert(EvaluationValue operand) => UnaryOperationAny(operand, (a) => !a);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a != b);

		/*
		public override EvaluationType? AndResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? And(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a && b);

		public override EvaluationType? OrResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Or(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a || b);

		public override EvaluationType? XorResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Xor(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a ^ b);
		*/

		public class BoolCastFunction : AbstractFunction {

			public static readonly BoolCastFunction Instance = new BoolCastFunction();
			private BoolCastFunction() { }

			public override EvaluationName Name { get; } = "bool";
			public override string? Description { get; } = "Converts the argument into a boolean value. Strings will be parsed, floats and integers will be compared to zero, and bools will be unchanged.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null))
				);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType argType = args[0].GetReturnType();
				if (FloatEvaluationType.IsReal(argType) || IsBool(argType) || StringEvaluationType.IsString(argType)) {
					return context.GetType<BoolEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to boolean.");
				}
			}

			private static EvaluationValue MakeResult(bool result, EvaluationContext context) {
				return new EvaluationValue(result, context.GetType<BoolEvaluationType>());
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue a = args[0].Evaluate(environment);

				if (TryGetBool(a, out bool aBool)) {
					return MakeResult(aBool, environment.Context);
				}
				else if (IntEvaluationType.TryGetInt(a, out int aInt)) {
					return MakeResult(aInt != 0, environment.Context);
				}
				else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
					return MakeResult(aFloat != 0f, environment.Context);
				}
				else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
					if (bool.TryParse(aStr, out bool parsed)) {
						return MakeResult(parsed, environment.Context);
					}
					else {
						throw new EvaluationCalculationException($"Provided string is not a valid boolean: \"{aStr}\"");
					}
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {a.Type} to boolean.");
				}
			}
		}

	}

	public class NumericClampMethod : AbstractMethod {
		public override EvaluationName Name { get; } = "clamp";
		public override string? Description { get; } = "Returns the current value clamped between a given minimum and maximum value.";

		public NumericClampMethod(EvaluationType receiverType) : base(receiverType) { }

		public override EnvironmentFunctionArguments GetArguments() {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("min", ReceiverType, null),
					new EnvironmentFunctionArg("max", ReceiverType, null)
				)
			);
		}

		public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode[] args) {
			EvaluationType receiverType = receiver.GetReturnType();
			if (ReceiverType != receiverType) {
				throw new EvaluationTypeException($"Invalid receiver type for {ReceiverType}.{Name}: {receiverType}.");
			}

			EvaluationType[] argTypes = args.Select(a => a.GetReturnType()).ToArray();
			if (argTypes.Length == 2 && argTypes.All(t => ReceiverType.CanImplicitCastFrom(t))) {
				EvaluationType? lessThanType = EvaluationOps.LessThanEqualResult(ReceiverType, argTypes[0]);
				EvaluationType? greaterThanType = EvaluationOps.GreaterThanEqualResult(ReceiverType, argTypes[1]);
				if (lessThanType is not null && BoolEvaluationType.IsBool(lessThanType) && greaterThanType is not null && BoolEvaluationType.IsBool(greaterThanType)) {
					return ReceiverType;
				}
			}

			throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for arguments of types {string.Join(", ", argTypes.Select(t => t.ToString()))}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode[] args) {
			EvaluationValue value = receiver.Evaluate(environment);
			EvaluationValue[] argValues = args.Evaluate(environment);

			if (argValues.Length == 2 && ReceiverType.Cast(value) is EvaluationValue castValue) {
				EvaluationValue? lessThan = EvaluationOps.LessThan(castValue, argValues[0]);
				if (!lessThan.HasValue || !BoolEvaluationType.TryGetBool(lessThan.Value, out bool lessThanBool)) {
					throw new EvaluationCalculationException($"Invalid first argument for {ReceiverType}.{Name}.");
				}

				if (lessThanBool) {
					if (ReceiverType.Cast(argValues[0]) is EvaluationValue minVal) {
						return minVal;
					}
					else {
						throw new EvaluationCalculationException($"Invalid first argument for {ReceiverType}.{Name}.");
					}
				}

				EvaluationValue? greaterThan = EvaluationOps.GreaterThan(castValue, argValues[1]);
				if (!greaterThan.HasValue || !BoolEvaluationType.TryGetBool(greaterThan.Value, out bool greaterThanBool)) {
					throw new EvaluationCalculationException($"Invalid second argument for {ReceiverType}.{Name}.");
				}

				if (greaterThanBool) {
					if (ReceiverType.Cast(argValues[1]) is EvaluationValue maxVal) {
						return maxVal;
					}
					else {
						throw new EvaluationCalculationException($"Invalid first argument for {ReceiverType}.{Name}.");
					}
				}

				return castValue;
			}
			else {
				throw new EvaluationCalculationException($"Cannot calculate {ReceiverType}.{Name} for value of type {value.Type} with arguments {string.Join(", ", argValues.Select(v => v.Type.ToString()))}.");
			}
		}

	}

}
