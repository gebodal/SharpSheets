using SharpSheets.Utilities;
using System;
using System.Collections;

namespace SharpSheets.Evaluations.Nodes {

	/*
	 * Make the evaluation function types into interfaces:
	 * IEnvironmentFunctionInfo and IEnvironmentFunction.
	 * IEnvironmentFunction returns an EvaluationNode.
	 * A type may implement both FunctionInfo and Function,
	 * in which case it can provide its own node without
	 * the need for an environment.
	 * 
	 * EnvironmentFunctionNode now gets the IEvaluationFunction,
	 * requests the EvaluationNode, provides it with the arguments
	 * (by copying over its own), and then requests the result from
	 * that node.
	 * 
	 * The Args on FunctionInfo should now have multiple possible
	 * arrangements of args.
	 * 
	 */

	public class EnvironmentFunctionArg {
		public EvaluationName Name { get; }
		/// <summary>
		/// A value of <see langword="null"/> indicates an "Any" type.
		/// </summary>
		public EvaluationType? ArgType { get; }
		public string? Description { get; }

		public EnvironmentFunctionArg(EvaluationName name, EvaluationType? argType, string? description) {
			this.Name = name;
			this.ArgType = argType;
			this.Description = description;
		}
	}

	public class EnvironmentFunctionArgList {
		public EnvironmentFunctionArg[] Arguments { get; }
		public bool IsParams { get; }

		public EnvironmentFunctionArgList(EnvironmentFunctionArg[] arguments, bool isParams) {
			Arguments = arguments;
			IsParams = isParams;
		}

		public EnvironmentFunctionArgList(EnvironmentFunctionArg argument, bool isParams = false) {
			Arguments = new EnvironmentFunctionArg[] { argument };
			IsParams = isParams;
		}

		public EnvironmentFunctionArgList(params EnvironmentFunctionArg[] arguments) {
			Arguments = arguments;
			IsParams = false;
		}

	}

	public class EnvironmentFunctionArguments : IReadOnlyList<EnvironmentFunctionArgList> {
		public string? Warning { get; }
		private readonly EnvironmentFunctionArgList[] argumentLists;

		public EnvironmentFunctionArguments(string? warning, params EnvironmentFunctionArgList[] argumentLists) {
			this.Warning = warning;
			this.argumentLists = argumentLists.Length == 0 ? new EnvironmentFunctionArgList[] { new EnvironmentFunctionArgList(Array.Empty<EnvironmentFunctionArg>(), false) } : argumentLists;
		}

		public EnvironmentFunctionArgList this[int index] => argumentLists[index];
		public int Count => argumentLists.Length;

		public IEnumerator<EnvironmentFunctionArgList> GetEnumerator() =>
			((IEnumerable<EnvironmentFunctionArgList>)argumentLists).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => argumentLists.GetEnumerator();
	}

	public interface IEnvironmentFunctionInfo {
		EvaluationName Name { get; }
		EnvironmentFunctionArguments Args { get; }
		string? Description { get; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		/// <exception cref="EvaluationTypeException"></exception>
		EvaluationType GetReturnType(EvaluationNode[] args);
	}
	public interface IEnvironmentFunctionEvaluator {
		/// <summary>
		/// 
		/// </summary>
		/// <param name="environment"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		object? Evaluate(IEnvironment environment, EvaluationNode[] args);
	}

	public interface IEnvironmentFunction : IEnvironmentFunctionInfo, IEnvironmentFunctionEvaluator { }

	/*
	public class MyEnvironmentFunction : IEnvironmentFunction {

		public EvaluationName Name { get; } = "func";

		public ICollection<(EnvironmentFunctionArg[] args, bool isParams)> Args { get; } = new List<(EnvironmentFunctionArg[] args, bool isParams)> {
			(new EnvironmentFunctionArg[] {
				new EnvironmentFunctionArg("arg1", EvaluationType.FLOAT, "My argument description.")
			}, false)
		};

		public string? Description { get; } = "My function description.";

		public EvaluationType GetReturnType(EvaluationNode[] args) {
			return EvaluationType.INT;
		}

		public object? Evaluate(EvaluationNode[] args) {
			return 1;
		}
	}
	*/

}
