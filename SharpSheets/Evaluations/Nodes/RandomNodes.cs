using System;

namespace SharpSheets.Evaluations.Nodes {

	public class RandomNode : SingleArgFunctionNode {
		public override string Name { get; } = "random";

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				return argType.IsReal() ? EvaluationType.FLOAT : throw new EvaluationTypeException($"Random must take a real number for a seed, not {argType}.");
			}
		}

		public sealed override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

			int seed;

			if (EvaluationTypes.TryGetIntegral(a, out int intSeed)) {
				seed = intSeed;
			}
			else if (EvaluationTypes.TryGetReal(a, out float floatSeed)) {
				//seed = (int)floatSeed;
				seed = BitConverter.SingleToInt32Bits(floatSeed); // To avoid rounding errors
			}
			else {
				throw new EvaluationTypeException($"Random must take a real number for a seed, not {GetDataTypeName(a)}.");
			}

			Random rng = new Random(seed);

			float result = (float)rng.NextDouble();

			return result;
		}

		protected override FunctionNode Empty() { return new RandomNode(); }
	}

}
