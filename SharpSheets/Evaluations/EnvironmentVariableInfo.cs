using SharpSheets.Evaluations.Types;

namespace SharpSheets.Evaluations {

	public class EnvironmentVariableInfo {
		public EvaluationName Name { get; }
		public EvaluationType EvaluationType { get; }
		public string? Description { get; }

		public EnvironmentVariableInfo(EvaluationName name, EvaluationType evaluationType, string? description) {
			Name = name;
			EvaluationType = evaluationType;
			Description = description;
		}
	}

}
