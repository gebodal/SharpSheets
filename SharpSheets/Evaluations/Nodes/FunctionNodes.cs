using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class FunctionNode : OperatorNode {
		public sealed override int Precedence { get; } = 0;
		public sealed override int Operands => Arguments.Length;
		public sealed override Associativity Associativity { get; } = Associativity.RIGHT;
		public override bool IsConstant { get { return Arguments.All(a => a.IsConstant); } }
		public abstract string Name { get; }

		/// <summary>
		/// 
		/// </summary>
		/// <exception cref="EvaluationProcessingException"></exception>
		public abstract EvaluationNode[] Arguments { get; }

		/*
		public override void Print(int indent, IEnvironment environment) {
			Console.WriteLine(new string(' ', indent * 4) + this.GetType().Name + ":");
			for (int i = 0; i < Arguments.Length; i++) {
				Arguments[i].Print(indent + 1, environment);
			}
		}
		*/

		protected override string GetRepresentation() {
			return Name + "(" + string.Join(", ", Arguments.Select(a => a.ToString())) + ")";
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		/// <exception cref="EvaluationProcessingException"></exception>
		protected abstract FunctionNode Empty();

		public sealed override EvaluationNode Clone() {
			FunctionNode empty = Empty();
			for (int i = 0; i < Arguments.Length; i++) {
				empty.Arguments[i] = Arguments[i].Clone();
			}
			return empty;
		}

		public sealed override EvaluationNode Simplify() {
			if (IsConstant) {
				return new ConstantNode(Evaluate(Environments.Empty), ReturnType);
			}
			else {
				FunctionNode empty = Empty();
				for (int i = 0; i < Arguments.Length; i++) {
					empty.Arguments[i] = Arguments[i].Simplify();
				}
				return empty;
			}
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			return Arguments.SelectMany(node => node.GetVariables()).Distinct();
		}
	}

	public abstract class VariableArgsFunctionNode : FunctionNode {
		private EvaluationNode[]? arguments;
		public sealed override EvaluationNode[] Arguments { get { return arguments ?? throw new EvaluationProcessingException("Function arguments not initialized."); ; } }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="count"></param>
		/// <exception cref="EvaluationSyntaxException"></exception>
		public virtual void SetArgumentCount(int count) {
			arguments = new EvaluationNode[count];
		}

		protected abstract VariableArgsFunctionNode MakeEmptyBase();

		protected sealed override FunctionNode Empty() {
			VariableArgsFunctionNode empty = MakeEmptyBase();
			empty.SetArgumentCount(this.Operands);
			return empty;
		}
	}

	public abstract class SingleArgFunctionNode : FunctionNode {
		public sealed override EvaluationNode[] Arguments { get; } = new EvaluationNode[1];
		public EvaluationNode Argument { get { return Arguments[0]; } set { Arguments[0] = value; } }
		public override bool IsConstant { get { return Arguments[0].IsConstant; } }
	}

	public static class VariableArgsFunctionNodeUtils {

		public static T AssignArguments<T>(this T function, params EvaluationNode[] arguments) where T : VariableArgsFunctionNode {
			function.SetArgumentCount(arguments.Length);
			for (int i = 0; i < arguments.Length; i++) {
				function.Arguments[i] = arguments[i];
			}
			return function;
		}

	}

}
