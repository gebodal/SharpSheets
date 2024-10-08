using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Utilities;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Colors;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Cards.Definitions;
using SharpSheets.Cards.CardConfigs;

namespace SharpSheets.Cards.CardSubjects {

	public static class DryRunConstants {
		private static readonly int Int = 1;
		private static readonly uint UInt = 1;
		private static readonly float Float = 0.5f;
		private static readonly UFloat UFloat = new UFloat(0.5f);
		private static readonly bool Bool = true;
		private static readonly string String = "string";
		private static readonly Color Color = Color.Black;

		private static readonly Dictionary<EvaluationType, object> values = new Dictionary<EvaluationType, object>() {
			[EvaluationType.INT] = Int,
			[EvaluationType.UINT] = UInt,
			[EvaluationType.FLOAT] = Float,
			[EvaluationType.UFLOAT] = UFloat,
			[EvaluationType.BOOL] = Bool,
			[EvaluationType.STRING] = String,
			[EvaluationType.COLOR] = Color
		};

		/// <summary></summary>
		/// <exception cref="NotSupportedException"></exception>
		public static object Get(EvaluationType type) {
			if (type.IsTuple) {
				return EvaluationTypes.MakeTuple(type.ElementType.DataType, Get(type.ElementType).Yield(type.ElementCount.Value).ToArray());
			}
			else if (type.IsArray) {
				return EvaluationTypes.MakeArray(type.ElementType.DataType, Get(type.ElementType).Yield().ToArray());
			}
			return values.GetValueOrFallback(type, null) ?? throw new NotSupportedException($"No dry run constant specified for type {type}.");
		}
	}

	public class DryRunEnvironment : IEnvironment {
		private readonly IVariableBox variables;
		private readonly IVariableDefinitionBox definitions;

		public bool IsEmpty => variables.IsEmpty && definitions.IsEmpty;

		public DryRunEnvironment(IVariableBox variables, IVariableDefinitionBox definitions) {
			this.variables = variables;
			this.definitions = definitions;
		}

		public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) => variables.TryGetVariableInfo(key, out variableInfo);
		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) => variables.TryGetNode(key, out node);
		public IEnumerable<EnvironmentVariableInfo> GetVariables() => variables.GetVariables();

		public bool TryGetValue(EvaluationName key, out object? value) {
			if(definitions.TryGetDefinition(key, out Definition? definition) && definition is ConstantDefinition constant && constant.ExampleValue is not null) {
				value = constant.ExampleValue;
				return true;
			}
			if(variables.TryGetReturnType(key, out EvaluationType? returnType)) {
				value = DryRunConstants.Get(returnType);
				return true;
			}
			else {
				value = null;
				return false;
			}
		}

		// TODO These may need updating if we end up implementing user defined functions
		public bool TryGetFunction(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionEvaluator functionEvaluator) {
			functionEvaluator = null; // TODO What to do here?
			return false;
		}
		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
			return variables.TryGetFunctionInfo(name, out functionInfo);
		}
		public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() => variables.GetFunctionInfos();

	}

}
