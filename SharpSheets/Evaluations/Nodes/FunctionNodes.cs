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

		public override EvaluationType GetReturnType() => functionInfo.GetReturnType(Context, Arguments);

		private readonly IEnvironmentFunctionInfo functionInfo;

		public EnvironmentFunctionNode(IEnvironmentFunctionInfo functionInfo, EvaluationContext context) : base(context) {
			this.functionInfo = functionInfo;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="count"></param>
		/// <exception cref="EvaluationProcessingException"></exception>
		public void SetArgumentCount(int count) {
			EnvironmentFunctionArguments args = functionInfo.GetArguments(Context);

			if (args.Count > 0) {
				foreach (EnvironmentFunctionArgList argList in args) {
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
				string[] expected = args.Select(args => $"{(args.IsParams ? ">=" : "")}{args.Arguments.Length}").ToArray();
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

		public void SetArguments(params EvaluationNode[] arguments) {
			SetArgumentCount(arguments.Length);
			for (int i = 0; i < arguments.Length; i++) {
				Arguments[i] = arguments[i];
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			if(functionInfo is not IEnvironmentFunctionEvaluator func) {
				func = environment.GetFunction(functionInfo.Name);
			}

			EvaluationValue result = func.Evaluate(environment, Arguments);

			return result;
		}

		protected override string GetRepresentation() {
			return Name + "(" + string.Join(", ", Arguments.Select(a => a.ToString())) + ")";
		}

		public sealed override EvaluationNode Simplify() {
			if (IsConstant && functionInfo is IEnvironmentFunctionEvaluator func) {
				return new ConstantNode(func.Evaluate(Environments.Create(Context), Arguments));
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
			EnvironmentFunctionNode empty = new EnvironmentFunctionNode(functionInfo, Context);
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

		//public abstract EnvironmentFunctionArguments Args { get; }

		public abstract EnvironmentFunctionArguments GetArguments(EvaluationContext context);

		public abstract EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args);
		public abstract EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args);
	}

	public abstract class AbstractSingleArgFunction : AbstractFunction {
		protected abstract string? Warning { get; }

		protected abstract EnvironmentFunctionArg GetArgument(EvaluationContext context);

		/*
		public override sealed EnvironmentFunctionArguments Args => new EnvironmentFunctionArguments(Warning,
			new EnvironmentFunctionArgList(Argument)
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(Warning, new EnvironmentFunctionArgList(GetArgument(context)));
		}

		public abstract EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg);
		public sealed override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			return GetReturnType(context, args[0]);
		}

		public abstract EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg);
		public override sealed EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			return Evaluate(environment, args[0]);
		}
	}

	public static class EnvironmentFunctionUtils {

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static EvaluationNode MakeNode(this IEnvironmentFunctionInfo functionInfo, EvaluationContext context, params EvaluationNode[] arguments) {
			EnvironmentFunctionNode node = new EnvironmentFunctionNode(functionInfo, context);
			node.SetArguments(arguments);
			return node.Simplify();
		}
	}

}
