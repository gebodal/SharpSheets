using System;

namespace SharpSheets.Evaluations.Nodes {

	public class RandomFunction : AbstractSingleArgFunction {

		public static readonly RandomFunction Instance = new RandomFunction();
		private RandomFunction() { }

		public override EvaluationName Name { get; } = "random";
		public override string? Description { get; } = "Returns a pseudo-random number based on the provided seed. The relationship between seed and return value is deterministic (you will always get the same pseudo-random number for a given input).";

		protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("seed", EvaluationTypes.FLOAT, null);
		protected override string? Warning => null;

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode arg) {
			EvaluationType argType = arg.GetReturnType(typeSystem);
			return FloatEvaluationType.IsReal(argType) ? EvaluationTypes.FLOAT : throw new EvaluationTypeException($"{Name} must take a real number for a seed, not {argType}.");
		}

		public sealed override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			EvaluationValue a = arg.Evaluate(environment);

			int seed;
			if (IntEvaluationType.TryGetInt(a, out int intSeed)) {
				seed = intSeed;
			}
			else if (FloatEvaluationType.TryGetFloat(a, out float floatSeed)) {
				seed = BitConverter.SingleToInt32Bits(floatSeed); // use bits directly, to avoid rounding/conversion errors
			}
			else {
				throw new EvaluationTypeException($"{Name} must take a real number for a seed, not {a.Type}.");
			}

			Random rng = new Random(seed);

			float result = (float)rng.NextDouble();

			return new EvaluationValue(result, EvaluationTypes.FLOAT);
		}
	}

}
