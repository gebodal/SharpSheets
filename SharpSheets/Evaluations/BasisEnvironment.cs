using SharpSheets.Evaluations.Nodes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Evaluations {

	public sealed class BasisEnvironment : IEnvironment {

		public static readonly BasisEnvironment Instance = new BasisEnvironment();
		private BasisEnvironment() { }

		private static readonly Dictionary<EvaluationName, IEnvironmentFunction> functions = new List<IEnvironmentFunction> {
			ArrayCreateFunction.Instance,
			ArrayContainsFunction.Instance,
			ArrayAllFunction.Instance, ArrayAnyFunction.Instance,
			ArraySortFunction.Instance,
			ArrayReverseFunction.Instance,
			IntCastFunction.Instance, FloatCastFunction.Instance, BoolCastFunction.Instance, StringCastFunction.Instance,
			ColorCreateFunction.Instance,
			LengthFunction.Instance,
			ExistsFunction.Instance, TryFunction.Instance,
			RangeFunction.Instance,
			RandomFunction.Instance,
			LowerFunction.Instance, UpperFunction.Instance, TitleCaseFunction.Instance,
			StringJoinFunction.Instance, StringSplitFunction.Instance,
			StringFormatFunction.Instance,
			MinVarFunction.Instance, MaxVarFunction.Instance,
			SumFunction.Instance,
			FloorFunction.Instance, CeilingFunction.Instance,
			SquareRootFunction.Instance,
			SinFunction.Instance, CosFunction.Instance, TanFunction.Instance,
			AsinFunction.Instance, AcosFunction.Instance, AtanFunction.Instance, Atan2Function.Instance,
			SinhFunction.Instance, CoshFunction.Instance, TanhFunction.Instance,
			LerpFunction.Instance
		}.ToDictionary(f => f.Name);

		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
			if(functions.TryGetValue(name, out IEnvironmentFunction? function)) {
				functionInfo = function;
				return true;
			}
			else {
				functionInfo = null;
				return false;
			}
		}

		public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
			variableInfo = null;
			return false;
		}

		public bool TryGetValue(EvaluationName key, out object? value) {
			value = null;
			return false;
		}

		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
			node = null;
			return false;
		}

		public bool TryGetFunction(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionEvaluator functionEvaluator) {
			if (functions.TryGetValue(name, out IEnvironmentFunction? function)) {
				functionEvaluator = function;
				return true;
			}
			else {
				functionEvaluator = null;
				return false;
			}
		}

		public IEnumerable<EnvironmentVariableInfo> GetVariables() {
			return Enumerable.Empty<EnvironmentVariableInfo>();
		}

		public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() {
			return functions.Values;
		}

	}

}
