using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Utilities;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Colors;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Cards.CardSubjects {

	public static class DryRunConstants {
		private static readonly int Int = 1;
		private static readonly float Float = 0.5f;
		private static readonly bool Bool = true;
		private static readonly string String = "string";
		private static readonly Color Color = Color.Black;

		private static readonly Dictionary<EvaluationType, object> values = new Dictionary<EvaluationType, object>() {
			[EvaluationType.INT] = Int,
			[EvaluationType.FLOAT] = Float,
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

		public DryRunEnvironment(IVariableBox variables) {
			this.variables = variables;
		}

		public bool TryGetReturnType(EvaluationName key, [MaybeNullWhen(false)] out EvaluationType returnType) => variables.TryGetReturnType(key, out returnType);
		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) => variables.TryGetNode(key, out node);
		public IEnumerable<EvaluationName> GetVariables() => variables.GetVariables();
		public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() => variables.GetFunctionInfos();

		public bool TryGetValue(EvaluationName key, out object? value) {
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
		public bool TryGetFunction(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionDefinition functionDefinition) {
			functionDefinition = null;
			return false;
		}
		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out EnvironmentFunctionInfo functionInfo) {
			return variables.TryGetFunctionInfo(name, out functionInfo);
		}

	}

}
