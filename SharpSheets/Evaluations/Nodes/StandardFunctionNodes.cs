using System;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public class LengthNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType { get; } = EvaluationType.INT;
		public sealed override string Name { get; } = "len";

		public override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

			if(a is null) {
				throw new EvaluationCalculationException("Cannot take length of null value.");
			}
			else if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
				return aInt;
			}
			else if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return (int)aFloat;
			}
			else if (a is bool aBool) {
				return aBool ? 1 : 0;
			}
			else if (a is string aStr) {
				return aStr.Length;
			}
			else if (a.GetType().IsArray && a is Array aArr && aArr.Rank == 1) {
				return aArr.Length;
			}
			else {
				throw new EvaluationTypeException($"Length not defined for value of type {a.GetType().Name}.");
			}
		}

		protected override FunctionNode Empty() {
			return new LengthNode();
		}
	}

	public class ExistsNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType { get; } = EvaluationType.BOOL;
		public sealed override string Name { get; } = "exists";

		public override object Evaluate(IEnvironment environment) {
			try {
				object? a = Argument.Evaluate(environment);
				if (a is string aString) {
					return aString.Length > 0;
				}
				else {
					return a != null;
				}
			}
			catch (UndefinedVariableException) {
				return false;
			}
		}

		protected override FunctionNode Empty() {
			return new ExistsNode();
		}
	}

	public class TryNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType { get; } = EvaluationType.BOOL;
		public sealed override string Name { get; } = "try";

		public override object Evaluate(IEnvironment environment) {
			try {
				object? a = Argument.Evaluate(environment);
				if (a is string aString) {
					return aString.Length > 0;
				}
				else {
					return a != null;
				}
			}
			catch (EvaluationException) {
				return false;
			}
		}

		protected override FunctionNode Empty() {
			return new TryNode();
		}
	}

	public class RangeNode : VariableArgsFunctionNode {
		public override string Name => "range";

		public override EvaluationType ReturnType { get; } = EvaluationType.INT.MakeArray();

		public override void SetArgumentCount(int count) {
			if (count == 1 || count == 2) {
				base.SetArgumentCount(count);
			}
			else {
				throw new EvaluationSyntaxException($"Invalid number of arguments provided to range function. May accept 1 or 2 arguments, {count} provided.");
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object[] args = EvaluationTypes.VerifyArray(Arguments.Select(a => a.Evaluate(environment)).ToArray());

			if (args.Length == 2) {
				if (args[0] is int start && args[1] is int end) {
					int[] values = new int[Math.Max(0, end - start)];
					int counter = start;
					for (int i = 0; i < values.Length; i++) {
						values[i] = counter;
						counter++;
					}
					return values;
				}
			}
			else if (args.Length == 1) {
				if (args[0] is int end) {
					int[] values = new int[Math.Max(0, end)];
					for (int i = 0; i < end; i++) {
						values[i] = i;
					}
					return values;
				}
			}

			throw new EvaluationCalculationException("Invalid arguments to range function. Must provide 1 or 2 integer values for [start,]end of range.");
		}

		protected override VariableArgsFunctionNode MakeEmptyBase() {
			return new RangeNode();
		}
	}

}
