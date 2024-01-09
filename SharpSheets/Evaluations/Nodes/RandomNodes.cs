using System;

namespace SharpSheets.Evaluations.Nodes {

	public class RandomFunction : AbstractFunction {

		public static readonly RandomFunction Instance = new RandomFunction();
		private RandomFunction() { }

		public override EvaluationName Name { get; } = "random";
		public override string? Description { get; } = "Returns a pseudo-random number based on the provided seed. The relationship between seed and return value is deterministic (you will always get the same pseudo-random number for a given input).";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("seed", EvaluationType.FLOAT, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			return argType.IsReal() ? EvaluationType.FLOAT : throw new EvaluationTypeException($"Random must take a real number for a seed, not {argType}.");
		}

		public sealed override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			int seed;

			if (EvaluationTypes.TryGetIntegral(a, out int intSeed)) {
				seed = intSeed;
			}
			else if (EvaluationTypes.TryGetReal(a, out float floatSeed)) {
				//seed = (int)floatSeed;
				seed = BitConverter.SingleToInt32Bits(floatSeed); // use bits directly, to avoid rounding/conversion errors
			}
			else {
				throw new EvaluationTypeException($"Random must take a real number for a seed, not {EvaluationUtils.GetDataTypeName(a)}.");
			}

			Random rng = new Random(seed);

			float result = (float)rng.NextDouble();

			return result;
		}
	}

}
