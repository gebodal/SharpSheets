namespace SharpSheets.Evaluations.Types {

	public readonly struct EvaluationValue {
		public readonly object? Value;
		public readonly EvaluationType Type;

		public EvaluationValue(object? value, EvaluationType type) {
			Value = value;
			Type = type;
		}

		public override string ToString() {
			return (Value?.ToString() ?? "") + $" {{{Type}}}";
		}

		public string ToEvaluationString() {
			return Type.GetEvaluationString(this);
		}

	}

}
