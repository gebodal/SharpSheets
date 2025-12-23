using SharpSheets.Utilities;

namespace SharpSheets.Evaluations.Types {

	public abstract class SingleDataType<T> : EvaluationType where T : notnull {

		public sealed override Type DataType { get; } = typeof(T);

		protected SingleDataType(EvaluationContext context) : base(context) { }

		protected sealed override bool EqualTypeData(EvaluationType other) {
			return Name == other.Name
				&& DataType == other.DataType;
		}

		protected sealed override int GetTypeHashCode() {
			return HashCode.Combine(Name, DataType);
		}

		/// <exception cref="FormatException"/>
		protected abstract T ParseValueDataSingle(string text, DirectoryPath source);
		protected override sealed object ParseValueData(string text, DirectoryPath source) {
			return ParseValueDataSingle(text, source);
		}

		protected abstract string GetEvaluationSingleString(T value);
		public override string GetEvaluationString(EvaluationValue value) {
			return value.Value is T data ? GetEvaluationSingleString(data) : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
		}

		protected abstract T DefaultValueDataSingle();

		protected sealed override object? DefaultValueData() {
			return DefaultValueDataSingle();
		}

	}

}
