using SharpSheets.Evaluations.Nodes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Evaluations {

	public abstract class ConcatVariableBox : IVariableBox {

		private readonly IVariableBox basis;

		protected ConcatVariableBox(EvaluationContext context, params IVariableBox[] basis) {
			this.basis = VariableBoxes.Concat(context, basis);
		}

		public bool IsEmpty => basis.IsEmpty;
		public EvaluationContext Context => basis.Context;

		public IEnumerable<EnvironmentVariableInfo> GetVariables() => basis.GetVariables();
		public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) => basis.TryGetVariableInfo(key, out variableInfo);
		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) => basis.TryGetNode(key, out node);

		public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() => basis.GetFunctionInfos();
		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) => basis.TryGetFunctionInfo(name, out functionInfo);
	}

	public class ConcatVariableBox<T1, T2> : ConcatVariableBox where T1 : IVariableBox where T2 : IVariableBox {

		public T1 Item1 { get; }
		public T2 Item2 { get; }

		public ConcatVariableBox(T1 box1, T2 box2) : base(box1.Context, box1, box2) {
			Item1 = box1;
			Item2 = box2;
		}

	}

	public class ConcatVariableBox<T1, T2, T3> : ConcatVariableBox where T1 : IVariableBox where T2 : IVariableBox where T3 : IVariableBox {

		public T1 Item1 { get; }
		public T2 Item2 { get; }
		public T3 Item3 { get; }

		public ConcatVariableBox(T1 box1, T2 box2, T3 box3) : base(box1.Context, box1, box2, box3) {
			Item1 = box1;
			Item2 = box2;
			Item3 = box3;
		}

	}

	public class ConcatVariableBox<T1, T2, T3, T4> : ConcatVariableBox where T1 : IVariableBox where T2 : IVariableBox where T3 : IVariableBox where T4 : IVariableBox {

		public T1 Item1 { get; }
		public T2 Item2 { get; }
		public T3 Item3 { get; }
		public T4 Item4 { get; }

		public ConcatVariableBox(T1 box1, T2 box2, T3 box3, T4 box4) : base(box1.Context, box1, box2, box3, box4) {
			Item1 = box1;
			Item2 = box2;
			Item3 = box3;
			Item4 = box4;
		}

	}

	public abstract class ConcatEnvironment : IEnvironment {

		private readonly IEnvironment basis;

		protected ConcatEnvironment(EvaluationContext context, params IEnvironment[] basis) {
			this.basis = Environments.Concat(context, basis);
		}

		public bool IsEmpty => basis.IsEmpty;
		public EvaluationContext Context => basis.Context;

		public IEnumerable<EnvironmentVariableInfo> GetVariables() => basis.GetVariables();
		public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) => basis.TryGetVariableInfo(key, out variableInfo);
		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) => basis.TryGetNode(key, out node);
		public bool TryGetValue(EvaluationName key, [NotNullWhen(true)] out EvaluationValue? value) => basis.TryGetValue(key, out value);
	
		public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() => basis.GetFunctionInfos();
		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) => basis.TryGetFunctionInfo(name, out functionInfo);
		public bool TryGetFunction(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionEvaluator functionEvaluator) => basis.TryGetFunction(name, out functionEvaluator);

	}

	public class ConcatEnvironment<T1, T2> : ConcatEnvironment where T1 : IEnvironment where T2 : IEnvironment {

		public T1 Item1 { get; }
		public T2 Item2 { get; }

		public ConcatEnvironment(T1 env1, T2 env2) : base(env1.Context, env1, env2) {
			Item1 = env1;
			Item2 = env2;
		}

		public static implicit operator ConcatVariableBox<T1, T2>(ConcatEnvironment<T1, T2> concatEnv) {
			return new ConcatVariableBox<T1, T2>(concatEnv.Item1, concatEnv.Item2);
		}

	}

	public class ConcatEnvironment<T1, T2, T3> : ConcatEnvironment where T1 : IEnvironment where T2 : IEnvironment where T3 : IEnvironment {

		public T1 Item1 { get; }
		public T2 Item2 { get; }
		public T3 Item3 { get; }

		public ConcatEnvironment(T1 env1, T2 env2, T3 env3) : base(env1.Context, env1, env2, env3) {
			Item1 = env1;
			Item2 = env2;
			Item3 = env3;
		}

		public static implicit operator ConcatVariableBox<T1, T2, T3>(ConcatEnvironment<T1, T2, T3> concatEnv) {
			return new ConcatVariableBox<T1, T2, T3>(concatEnv.Item1, concatEnv.Item2, concatEnv.Item3);
		}

	}

	public class ConcatEnvironment<T1, T2, T3, T4> : ConcatEnvironment where T1 : IEnvironment where T2 : IEnvironment where T3 : IEnvironment where T4 : IEnvironment {

		public T1 Item1 { get; }
		public T2 Item2 { get; }
		public T3 Item3 { get; }
		public T4 Item4 { get; }

		public ConcatEnvironment(T1 env1, T2 env2, T3 env3, T4 env4) : base(env1.Context, env1, env2, env3, env4) {
			Item1 = env1;
			Item2 = env2;
			Item3 = env3;
			Item4 = env4;
		}

		public static implicit operator ConcatVariableBox<T1, T2, T3, T4>(ConcatEnvironment<T1, T2, T3, T4> concatEnv) {
			return new ConcatVariableBox<T1, T2, T3, T4>(concatEnv.Item1, concatEnv.Item2, concatEnv.Item3, concatEnv.Item4);
		}

	}

}
