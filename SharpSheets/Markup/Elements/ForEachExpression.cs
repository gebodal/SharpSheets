using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using System.Text.RegularExpressions;

namespace SharpSheets.Markup.Elements {

	public class ForEachExpression {

		public EnvironmentVariableInfo Variable { get; }
		public EvaluationType ReturnType { get; }
		readonly EvaluationNode arrayExpr;

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		public ForEachExpression(EvaluationName variable, EvaluationNode arrayExpr) {
			EvaluationType arrayExprType = arrayExpr.GetReturnType();

			if (!(arrayExprType.IterationResult() is EvaluationType arrayElementType)) {
				throw new EvaluationTypeException("Expression must produce an array or tuple.");
			}

			this.arrayExpr = arrayExpr;
			this.ReturnType = arrayElementType;
			this.Variable = new EnvironmentVariableInfo(variable, ReturnType, null);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public IEnumerable<EvaluationValue> Evaluate(IEnvironment environment) {
			EvaluationValue eval = arrayExpr.Evaluate(environment);
			if (eval.Type.Iteration(eval) is IEnumerable<EvaluationValue> iters) {
				foreach (EvaluationValue value in iters) {
					yield return value;
				}
			}
			else {
				throw new EvaluationCalculationException($"Invalid type received from for-each expression: {eval.Type.Name}");
			}
		}

		/// <summary>
		/// Evaluate the environments produced by this for-each expression, optionally concatenating each result to the original environment.
		/// </summary>
		/// <param name="environment"> The original environment, using which the for-each expression will be evaluated. </param>
		/// <param name="includeOriginal"> Flag indicating if the original environment should be concatenated onto the returned environments.
		/// Concatenating each result to the original environment if true, otherwise returning each resulting item as a IEnvironment with a single value. </param>
		/// <returns></returns>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public IEnumerable<IEnvironment> EvaluateEnvironments(IEnvironment environment, bool includeOriginal) {
			EvaluationValue eval = arrayExpr.Evaluate(environment);
			if (eval.Type.Iteration(eval) is IEnumerable<EvaluationValue> iters) {
				foreach (EvaluationValue value in iters) {
					IEnvironment variableEnv = Environments.Single(Variable, value);
					if (includeOriginal) {
						yield return variableEnv.AppendEnvironment(environment);
					}
					else {
						yield return variableEnv;
					}
				}
			}
			else {
				throw new EvaluationCalculationException("Could not resolve for-each expression.");
			}
		}

		private static readonly Regex forEachRegex = new Regex(@"^(?<variable>[a-z][a-z0-9]*)\s+in\s+(?<expr>.+)$", RegexOptions.IgnoreCase);

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		/// <exception cref="EvaluationException"></exception>
		public static ForEachExpression Parse(string text, IVariableBox variables) {
			Match match = forEachRegex.Match(text.Trim());
			if (!match.Success) {
				throw new FormatException("Invalid for-each expression.");
			}

			string variable = match.Groups["variable"].Value;

			string exprText = match.Groups["expr"].Value;
			EvaluationNode expr = Evaluation.Parse(exprText, variables);

			return new ForEachExpression(variable, expr);
		}
	}

}
