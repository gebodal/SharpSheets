using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
