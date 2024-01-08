using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Evaluations {

	public static class EvaluationUtils {

		public static string GetDataTypeName(object? data) {
			return data?.GetType().Name ?? "null";
		}

	}

}
