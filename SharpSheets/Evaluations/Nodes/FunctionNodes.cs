using SharpSheets.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public class EnvironmentFunctionNode : OperatorNode {

		public sealed override int Precedence { get; } = 0;
		public sealed override int Operands => Arguments.Length;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override bool IsConstant { get { return functionInfo is IEnvironmentFunctionEvaluator && Arguments.All(a => a.IsConstant); } }
		public string Name => functionInfo.Name.ToString(); // TODO This should be an EvaluationName

		/// <summary>
		/// 
		/// </summary>
		/// <exception cref="EvaluationProcessingException"></exception>
		public EvaluationNode[] Arguments { get { return arguments ?? throw new EvaluationProcessingException("Function arguments not initialized."); ; } }
		private EvaluationNode[]? arguments;

		public override EvaluationType ReturnType => functionInfo.GetReturnType(Arguments);

		private readonly IEnvironmentFunctionInfo functionInfo;

		public EnvironmentFunctionNode(IEnvironmentFunctionInfo functionInfo) {
			this.functionInfo = functionInfo;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="count"></param>
		/// <exception cref="EvaluationProcessingException"></exception>
		public void SetArgumentCount(int count) {
			if (functionInfo.Args.Count > 0) {
				foreach (EnvironmentFunctionArgList argList in functionInfo.Args) {
					if ((!argList.IsParams && count == argList.Arguments.Length) || (argList.IsParams && count >= argList.Arguments.Length)) {
						arguments = new EvaluationNode[count];
						return;
					}
				}
			}
			else if (count == 0) {
				arguments = Array.Empty<EvaluationNode>();
				return;
			}

			string GetExpectedString() {
				string[] expected = functionInfo.Args.Select(args => $"{(args.IsParams ? ">=" : "")}{args.Arguments.Length}").ToArray();
				if (expected.Length == 0) {
					return " (expected 0)";
				}
				else if (expected.Length == 1) {
					return " (expected " + expected[0] + ")";
				}
				else {
					return " (expected " + string.Join(", ", expected[..^1]) + ", or " + expected[^1] + ")";
				}
			}

			throw new EvaluationProcessingException($"Invalid number of arguments for function {Name}: {count} {GetExpectedString()}");
		}

		public override object? Evaluate(IEnvironment environment) {
			if(functionInfo is not IEnvironmentFunctionEvaluator func) {
				func = environment.GetFunction(functionInfo.Name);
			}

			object? result = func.Evaluate(environment, Arguments);

			return result;
		}

		protected override string GetRepresentation() {
			return Name + "(" + string.Join(", ", Arguments.Select(a => a.ToString())) + ")";
		}

		public sealed override EvaluationNode Simplify() {
			if (IsConstant && functionInfo is IEnvironmentFunctionEvaluator func) {
				return new ConstantNode(func.Evaluate(Environments.Empty, Arguments), ReturnType);
			}
			else {
				EnvironmentFunctionNode empty = Empty();
				for (int i = 0; i < Arguments.Length; i++) {
					empty.Arguments[i] = Arguments[i].Simplify();
				}
				return empty;
			}
		}

		public sealed override EvaluationNode Clone() {
			EnvironmentFunctionNode empty = Empty();
			for (int i = 0; i < Arguments.Length; i++) {
				empty.Arguments[i] = Arguments[i].Clone();
			}
			return empty;
		}

		protected EnvironmentFunctionNode Empty() {
			EnvironmentFunctionNode empty = new EnvironmentFunctionNode(functionInfo);
			empty.SetArgumentCount(this.Operands);
			return empty;
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			return Arguments.SelectMany(node => node.GetVariables()).Distinct();
		}

	}

	public abstract class AbstractFunction : IEnvironmentFunction {
		public abstract EvaluationName Name { get; }
		public abstract string? Description { get; }

		public abstract EnvironmentFunctionArguments Args { get; }

		public abstract EvaluationType GetReturnType(EvaluationNode[] args);
		public abstract object? Evaluate(IEnvironment environment, EvaluationNode[] args);
	}

	public abstract class AbstractSingleArgFunction : AbstractFunction {
		public abstract EnvironmentFunctionArg Argument { get; }
		public abstract string? Warning { get; }

		public override sealed EnvironmentFunctionArguments Args => new EnvironmentFunctionArguments(Warning,
			new EnvironmentFunctionArgList(Argument)
		);

		public abstract EvaluationType GetReturnType(EvaluationNode arg);
		public sealed override EvaluationType GetReturnType(EvaluationNode[] args) {
			return GetReturnType(args[0]);
		}

		public abstract object? Evaluate(IEnvironment environment, EvaluationNode arg);
		public override sealed object? Evaluate(IEnvironment environment, EvaluationNode[] args) {
			return Evaluate(environment, args[0]);
		}
	}

	public static class EnvironmentFunctionUtils {

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static EvaluationNode MakeNode(this IEnvironmentFunctionInfo functionInfo, params EvaluationNode[] arguments) {
			EnvironmentFunctionNode node = new EnvironmentFunctionNode(functionInfo);
			node.SetArgumentCount(arguments.Length);
			for (int i = 0; i < arguments.Length; i++) {
				node.Arguments[i] = arguments[i];
			}
			return node.Simplify();
		}
	}

}
