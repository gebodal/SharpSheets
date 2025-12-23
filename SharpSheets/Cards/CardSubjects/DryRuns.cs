using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Utilities;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Colors;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Cards.Definitions;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Evaluations.Types;

namespace SharpSheets.Cards.CardSubjects {

	public static class DryRunConstants {
		private static readonly int Int = 1;
		private static readonly uint UInt = 1;
		private static readonly float Float = 0.5f;
		private static readonly UFloat UFloat = new UFloat(0.5f);
		private static readonly bool Bool = true;
		private static readonly string String = "string";
		private static readonly Color Color = Color.Black;

		private static readonly Dictionary<Type, object> values = new Dictionary<Type, object>() {
			[typeof(int)] = Int,
			[typeof(uint)] = UInt,
			[typeof(float)] = Float,
			[typeof(UFloat)] = UFloat,
			[typeof(bool)] = Bool,
			[typeof(string)] = String,
			[typeof(Color)] = Color
		};

		/// <summary></summary>
		/// <exception cref="NotSupportedException"></exception>
		public static EvaluationValue Get(EvaluationType type) {
			if (type is TupleEvaluationType tupleType) {
				return TupleEvaluationType.MakeTuple(tupleType.ElementType, Get(tupleType.ElementType).Yield(tupleType.ElementCount).ToArray());
			}
			else if (type is ArrayEvaluationType arrayType) {
				return ArrayEvaluationType.MakeArray(arrayType.ElementType, Get(arrayType.ElementType).Yield().ToArray());
			}
			else {
				object value = values.GetValueOrFallback(type.DataType, null) ?? throw new NotSupportedException($"No dry run constant specified for type {type}.");
				return new EvaluationValue(value, type);
			}
		}
	}

	public class DryRunEnvironment : IEnvironment {
		private readonly IVariableBox variables;
		private readonly IVariableDefinitionBox definitions;

		public bool IsEmpty => variables.IsEmpty && definitions.IsEmpty;
		public EvaluationContext Context => variables.Context;

		public DryRunEnvironment(IVariableBox variables, IVariableDefinitionBox definitions) {
			this.variables = variables;
			this.definitions = definitions;
		}

		public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) => variables.TryGetVariableInfo(key, out variableInfo);
		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) => variables.TryGetNode(key, out node);
		public IEnumerable<EnvironmentVariableInfo> GetVariables() => variables.GetVariables();

		public bool TryGetValue(EvaluationName key, [NotNullWhen(true)] out EvaluationValue? value) {
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
