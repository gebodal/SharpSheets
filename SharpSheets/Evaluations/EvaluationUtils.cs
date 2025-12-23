using SharpSheets.Evaluations.Nodes;
using SharpSheets.Evaluations.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Evaluations {

	public static class EvaluationUtils {

		/*
		public static string GetDataTypeName(object? data) {
			return data?.GetType().Name ?? "null";
		}
		*/

		public static EvaluationValue[] Evaluate(this IEnumerable<EvaluationNode> nodes, IEnvironment environment) {
			return nodes.Select(n => n.Evaluate(environment)).ToArray();
		}

	}

}
