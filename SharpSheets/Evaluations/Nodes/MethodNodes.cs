using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Evaluations.Nodes {

	public class MethodNode : FunctionNode {

		public sealed override int Precedence { get; } = 0;
		public sealed override int Operands => 1 + Arguments.Length;
		public sealed override Associativity Associativity { get; } = Associativity.LEFT;
		public override bool IsConstant { get { return Receiver.IsConstant && Arguments.All(a => a.IsConstant); } }
		public override EvaluationName Name => method.Name;

		private EvaluationNode? _receiver;
		public EvaluationNode Receiver {
			get { return _receiver ?? throw new EvaluationCalculationException("Receiver not initialized."); }
			set { _receiver = value; }
		}

		public override EvaluationType GetReturnType() => method.GetReturnType(Receiver, Arguments);

		private readonly IMethod method;

		public MethodNode(IMethod method) : base(method.Context) {
			this.method = method;
		}

		protected override EnvironmentFunctionArguments GetFunctionArguments() {
			return method.GetArguments();
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationValue result = method.Evaluate(environment, Receiver, Arguments);
			return result;
		}

		protected override string GetRepresentation() {
			string receiver = Receiver.ToString();
			if (Receiver is OperatorNode receiverNode && receiverNode.Precedence > this.Precedence) {
				receiver = "(" + receiver + ")";
			}
			return receiver + "." + base.GetRepresentation();
		}

		public sealed override EvaluationNode Simplify() {
			if (IsConstant) {
				return new ConstantNode(method.Evaluate(Environments.Create(Context), Receiver, Arguments));
			}
			else {
				MethodNode empty = Empty();
				empty.Receiver = Receiver;
				for (int i = 0; i < Arguments.Length; i++) {
					empty.Arguments[i] = Arguments[i].Simplify();
				}
				return empty;
			}
		}

		public sealed override EvaluationNode Clone() {
			MethodNode empty = Empty();
			empty.Receiver = Receiver;
			for (int i = 0; i < Arguments.Length; i++) {
				empty.Arguments[i] = Arguments[i].Clone();
			}
			return empty;
		}

		protected MethodNode Empty() {
			MethodNode empty = new MethodNode(method);
			empty.SetArgumentCount(this.Operands);
			return empty;
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			return Receiver.GetVariables().Concat(Arguments.SelectMany(node => node.GetVariables())).Distinct();
		}

	}

	public interface IMethod {
		EvaluationName Name { get; }
		string? Description { get; }

		EvaluationType ReceiverType { get; }

		sealed EvaluationContext Context => ReceiverType.Context;

		EnvironmentFunctionArguments GetArguments();

		/// <exception cref="EvaluationTypeException"></exception>
		EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode[] args);

		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode[] args);
	}

	public abstract class AbstractMethod : IMethod {
		public abstract EvaluationName Name { get; }
		public abstract string? Description { get; }

		public EvaluationType ReceiverType { get; }
		public EvaluationContext Context => ReceiverType.Context;

		public AbstractMethod(EvaluationType receiverType) {
			this.ReceiverType = receiverType;
		}

		public abstract EnvironmentFunctionArguments GetArguments();

		public abstract EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode[] args);
		public abstract EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode[] args);
	}

	public abstract class AbstractSingleArgMethod : AbstractMethod {
		protected abstract string? Warning { get; }

		public AbstractSingleArgMethod(EvaluationType receiverType) : base(receiverType) { }

		protected abstract EnvironmentFunctionArg GetArgument();

		public override EnvironmentFunctionArguments GetArguments() {
			return new EnvironmentFunctionArguments(Warning, new EnvironmentFunctionArgList(GetArgument()));
		}

		public abstract EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode arg);
		public sealed override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode[] args) {
			return GetReturnType(receiver, args[0]);
		}

		public abstract EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode arg);
		public override sealed EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode[] args) {
			return Evaluate(environment, receiver, args[0]);
		}
	}

}
