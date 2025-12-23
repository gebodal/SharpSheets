using SharpSheets.Utilities;

namespace SharpSheets.Evaluations.Types {

	public sealed class MetaEvaluationType : EvaluationType {

		public override string Name { get; } = "type";

		public override Type DataType { get; } = typeof(EvaluationType);

		public MetaEvaluationType(EvaluationContext context) : base(context) { }

		protected override object ParseValueData(string text, DirectoryPath source) {
			if (Context.TryGetType(text, out EvaluationType? type)) {
				return type;
			}
			else {
				throw new FormatException($"Unrecognized evaluation type name: \"{text}\".");
			}
		}

		public override string GetEvaluationString(EvaluationValue value) {
			return value.Value is EvaluationType evalType ? evalType.Name : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
		}

		protected override object? DefaultValueData() {
			return default(Type);
		}

		protected override bool EqualTypeData(EvaluationType other) {
			return Name == other.Name
				&& DataType == other.DataType;
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(Name, DataType);
		}
	}

}
