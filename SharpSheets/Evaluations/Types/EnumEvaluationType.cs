using SharpSheets.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Evaluations.Types {

	public class EnumEvaluationType : EvaluationType {

		public override string Name { get; }

		public Type SystemType { get; }

		public override Type DataType { get; } = typeof(string);

		private readonly IReadOnlyDictionary<string, string> enumNames;
		private readonly string defaultEnumName;
		private readonly int enumNamesHash;

		public IEnumerable<string> EnumNames => enumNames.Keys;

		internal EnumEvaluationType(EvaluationContext context, string name, IEnumerable<string> enumValues, Type systemType) : base(context) {
			this.Name = name;

			this.SystemType = systemType;

			string[] enumNamesVals = enumValues.Select(s => s.ToUpperInvariant()).Distinct().ToArray();

			if(enumNamesVals.Length == 0) { throw new ArgumentException("Enum evaluation type must be provided with at least one enum value name.", nameof(enumValues)); }

			this.defaultEnumName = enumNamesVals[0];
			this.enumNames = enumNamesVals.ToDictionary(s => s, StringComparer.InvariantCultureIgnoreCase);
			enumNamesHash = GetEnumNamesHashCode(this.enumNames.Keys);

			foreach (string enumName in this.enumNames.Keys.Order()) {
				AddStaticField(new TypeField(enumName, $"The {enumName} enum value for {name}.", this, t => new EvaluationValue(enumName, this)));
			}
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			if (enumNames.TryGetValue(text.Trim(), out string? foundName)) {
				return foundName;
			}
			else {
				throw new FormatException($"Invalid literal for enum of type {Name}: \"{text}\".");
			}
		}

		public override string GetEvaluationString(EvaluationValue value) {
			return TryGetEnumValue(value, out string? enumValue) ? enumValue : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
		}

		protected override object? DefaultValueData() {
			return defaultEnumName;
		}

		public static EnumEvaluationType FromSystemType<T>(EvaluationContext context) where T : Enum {
			return new EnumEvaluationType(context, typeof(T).Name, Enum.GetNames(typeof(T)), typeof(T));
		}
		public static EnumEvaluationType FromSystemType(EvaluationContext context, Type type) {
			if (!type.IsEnum) { throw new ArgumentException("System type must be an enum type.", nameof(type)); }
			return new EnumEvaluationType(context, type.Name, Enum.GetNames(type), type);
		}

		public static bool IsEnum<T>(EvaluationType type) where T : System.Enum {
			return type is EnumEvaluationType enumEvalType && enumEvalType.SystemType == typeof(T);
		}

		public static bool IsEnum(EvaluationType type) {
			return type is EnumEvaluationType;
		}

		public static bool IsEnum(EvaluationType type, [NotNullWhen(true)] out EnumEvaluationType? enumType) {
			enumType = type as EnumEvaluationType;
			return enumType is not null;
		}

		public bool IsEnumValueDefined(string name) {
			return enumNames.ContainsKey(name);
		}

		public static bool TryGetEnumValue<T>(EvaluationValue value, [NotNullWhen(true)] out T? enumValue) where T : struct {
			if (value.Value is T enumData) {
				enumValue = enumData;
				return true;
			}
			else if (value.Value is string stringData && Enum.TryParse(stringData, true, out T parsed)) {
				enumValue = parsed;
				return true;
			}
			else {
				enumValue = default;
				return false;
			}
		}

		public bool TryGetEnumValue(EvaluationValue value, [NotNullWhen(true)] out string? enumValue) {
			if(value.Value is Enum enumData && enumData.GetType() == SystemType) {
				enumValue = enumData.ToString();
				return true;
			}
			else if(value.Value is string stringData && enumNames.TryGetValue(stringData, out string? enumName)) {
				enumValue = enumName;
				return true;
			}
			else {
				enumValue = null;
				return false;
			}
		}

		public bool TryGetEnumValue(EvaluationValue value, [NotNullWhen(true)] out Enum? enumValue) {
			if (value.Value is Enum enumData && enumData.GetType() == SystemType) {
				enumValue = enumData;
				return true;
			}
			else if (value.Value is string stringData && Enum.TryParse(SystemType, stringData, true, out object? parsed) && parsed is Enum parsedEnum) {
				enumValue = parsedEnum;
				return true;
			}
			else {
				enumValue = null;
				return false;
			}
		}

		// No implicit casting for enums
		//public override bool CanImplicitCastFrom(EvaluationType other) => false;
		//public override EvaluationValue? Cast(EvaluationValue other) => null; // Should this convert string values if possible?

		private EvaluationType? EqualityResultAny(EvaluationType other) {
			if (other == this) { // Is the same as us
				return Context.GetType<BoolEvaluationType>();
			}
			else if(StringEvaluationType.IsString(other)) { // Is a string value
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<string, string, bool> operation) {
			if (TryGetEnumValue(left, out string? leftVal) && TryGetEnumValue(right, out string? rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		private static bool EnumValuesEqual(string left, string right) {
			return string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase);
		}

		public override EvaluationType? EqualResult(EvaluationType other) => EqualityResultAny(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, EnumValuesEqual);

		public override EvaluationType? NotEqualResult(EvaluationType other) => EqualityResultAny(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, EnumValuesEqual);

		protected override bool EqualTypeData(EvaluationType other) {
			return other is EnumEvaluationType otherEnum
				&& Name == otherEnum.Name
				&& SystemType == otherEnum.SystemType
				&& EnumNames.SequenceEqual(otherEnum.EnumNames);
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(Name, SystemType, enumNamesHash);
		}

		public static int GetEnumNamesHashCode(IEnumerable<string> names) {
			unchecked {
				int hash = 0;
				foreach (string s in names.Order()) {
					int h = s?.GetHashCode() ?? 0;
					hash ^= (h ^ int.MinValue) * -0x61C88647; // 0x9E3779B9 as signed
				}
				return hash;
			}
		}

	}

}
