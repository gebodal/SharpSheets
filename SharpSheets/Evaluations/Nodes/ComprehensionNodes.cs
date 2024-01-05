using SharpSheets.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	internal interface IVariableProvider {
		/// <summary></summary>
		/// <exception cref="EvaluationProcessingException"></exception>
		IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> ProvidedVariables();
	}

	public class ComprehensionNode : BinaryOperatorNode, IVariableProvider {
		public override int Precedence { get; } = 12;
		public override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol => throw new NotSupportedException();

		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public override EvaluationType ReturnType {
			get {
				if (Second.ReturnType.IsArray || Second.ReturnType.IsTuple) {
					EvaluationType arrayElementType = First.ReturnType;
					if (arrayElementType.DataType == null) {
						// Shouldn't this just be handled downstream?
						throw new EvaluationTypeException($"Cannot construct array of dynamic type {arrayElementType}.");
					}
					return First.ReturnType.MakeArray(); // EvaluationType.Array(First.ReturnType);
				}
				else {
					throw new EvaluationTypeException("Comprehension requires an array from which to draw values.");
				}
			}
		}

		public EvaluationName LoopVariable { get; }

		public ComprehensionNode(EvaluationName loopVariable) {
			this.LoopVariable = loopVariable;
		}

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> ProvidedVariables() {
			try {
				return new KeyValuePair<EvaluationName, EvaluationType>(LoopVariable, Second.ReturnType.ElementType!).Yield();
			}
			catch (UndefinedVariableException) {
				return Enumerable.Empty<KeyValuePair<EvaluationName, EvaluationType>>();
			}
		}

		public override object Evaluate(IEnvironment environment) {
			ComprehensionEnvironment loopEnv = new ComprehensionEnvironment(LoopVariable, Second.ReturnType.ElementType!, environment);

			Type resultType = First.ReturnType.DataType;
			if (resultType == null) {
				throw new EvaluationCalculationException($"Cannot construct array of dynamic type {resultType}.");
			}

			object? arg2 = Second.Evaluate(environment);
			if (arg2 is not null && EvaluationTypes.TryGetArray(arg2, out Array? array)) {
				List<object?> result = new List<object?>();

				foreach (object loopValue in array) {
					loopEnv.SetLoopVariable(loopValue);
					object? loopResult = First.Evaluate(loopEnv);
					result.Add(loopResult);
				}

				return EvaluationTypes.MakeArray(resultType, result);
			}
			else {
				throw new EvaluationTypeException($"Comprehensions not defined for sources of type {GetDataTypeName(arg2)}.");
			}
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			return First.GetVariables().Where(k => k != LoopVariable).Concat(Second.GetVariables()).Distinct();
		}

		protected override BinaryOperatorNode Empty() {
			return new ComprehensionNode(LoopVariable);
		}

		protected override string GetRepresentation() {
			return $"{First} for ${LoopVariable} in {Second}";
		}
	}

	public class ComprehensionIfNode : TernaryOperatorNode, IVariableProvider {
		public override int Precedence { get; } = 12;
		public override Associativity Associativity { get; } = Associativity.RIGHT;

		public override int[] CalculationOrder { get; } = new int[] { 1, 2, 0 };

		public override EvaluationType ReturnType {
			get {
				if (Third.ReturnType != EvaluationType.BOOL) {
					throw new EvaluationTypeException("Comprehension condition must be a boolean expression.");
				}
				else if (Second.ReturnType.IsArray || Second.ReturnType.IsTuple) {
					EvaluationType arrayElementType = First.ReturnType;
					if (arrayElementType.DataType == null) {
						// I don't think this makes sense anymore...
						throw new EvaluationTypeException($"Cannot construct array of dynamic type {arrayElementType}.");
					}
					return First.ReturnType.MakeArray(); // EvaluationType.Array(First.ReturnType);
				}
				else {
					throw new EvaluationTypeException("Comprehension requires an array from which to draw values.");
				}
			}
		}

		public override Type OpeningType { get; } = typeof(ComprehensionNode);

		public EvaluationName? LoopVariable { get; set; } = null;

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> ProvidedVariables() {
			if (LoopVariable == null) {
				throw new EvaluationProcessingException("Loop variable not yet assigned.");
			}
			try {
				return new KeyValuePair<EvaluationName, EvaluationType>(LoopVariable.Value, Second.ReturnType.ElementType!).Yield();
			}
			catch (UndefinedVariableException) {
				return Enumerable.Empty<KeyValuePair<EvaluationName, EvaluationType>>();
			}
		}

		internal override void AssignOpening(OperatorNode openingNode) {
			if (openingNode is ComprehensionNode compNode) {
				LoopVariable = compNode.LoopVariable;
			}
			else {
				throw new EvaluationProcessingException("Invalid opening node for comprehension-if statement.");
			}
		}

		public override object Evaluate(IEnvironment environment) {

			if (LoopVariable == null) {
				throw new EvaluationProcessingException("Loop variable not yet assigned.");
			}

			ComprehensionEnvironment loopEnv = new ComprehensionEnvironment(LoopVariable.Value, Second.ReturnType.ElementType!, environment);

			Type resultType = First.ReturnType.DataType;
			if (resultType == null) {
				throw new EvaluationCalculationException($"Cannot construct array of dynamic type {resultType}.");
			}

			object? arg2 = Second.Evaluate(environment);
			if (arg2 is not null && EvaluationTypes.TryGetArray(arg2, out Array? array)) {
				List<object?> result = new List<object?>();

				foreach (object loopValue in array) {
					loopEnv.SetLoopVariable(loopValue);
					bool condition = (bool)(Third.Evaluate(loopEnv) ?? throw new EvaluationCalculationException("Cannot evaluate loop conditional."));
					if (condition) {
						object? loopResult = First.Evaluate(loopEnv);
						result.Add(loopResult);
					}
				}

				return EvaluationTypes.MakeArray(resultType, result);
			}
			else {
				throw new EvaluationTypeException($"Comprehensions not defined for sources of type {GetDataTypeName(arg2)}.");
			}
		}

		protected override TernaryOperatorNode Empty() {
			ComprehensionIfNode empty = new ComprehensionIfNode {
				LoopVariable = LoopVariable
			};
			return empty;
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			if (LoopVariable == null) {
				throw new EvaluationProcessingException("Loop variable not yet assigned.");
			}
			return First.GetVariables().Where(k => k != LoopVariable).Concat(Second.GetVariables(), Third.GetVariables().Where(k => k != LoopVariable)).Distinct();
		}

		protected override string GetRepresentation() {
			return $"{First} for ${LoopVariable} in {Second} if {Third}";
		}
	}

	internal class ComprehensionEnvironment : IEnvironment {

		private readonly IEnvironment environment;

		private readonly EvaluationName loopIdentifier;
		private readonly EvaluationType loopVariableType;
		private object? currentValue = null;
		private bool initialized;

		public ComprehensionEnvironment(EvaluationName loopIdentifier, EvaluationType loopVariableType, IEnvironment environment) {
			this.loopIdentifier = loopIdentifier;
			this.loopVariableType = loopVariableType;
			this.environment = environment;
			initialized = false;
		}

		public void SetLoopVariable(object? value) {
			currentValue = value;
			initialized = true;
		}

		/*
		public object this[string key] {
			get {
				if (!initialized) { throw new EvaluationProcessingException("Loop value not set."); }
				if (IsLoopVar(key)) {
					return currentValue;
				}
				else {
					return environment[key];
				}
			}
		}
		*/

		public bool TryGetValue(EvaluationName key, out object? value) {
			if (!initialized) { throw new EvaluationProcessingException("Loop value not set."); }
			if (loopIdentifier == key) {
				value = currentValue;
				return true;
			}
			else {
				return environment.TryGetValue(key, out value);
			}
		}

		public EvaluationType GetReturnType(EvaluationName key) {
			if (loopIdentifier == key) {
				return loopVariableType;
			}
			else {
				return environment.GetReturnType(key);
			}
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return environment.GetVariables().Append(loopIdentifier).Distinct();
		}

		public bool IsVariable(EvaluationName key) {
			return loopIdentifier == key || environment.IsVariable(key);
		}

		public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
			if (loopIdentifier == key) {
				node = null;
				return false;
			}
			else {
				return environment.TryGetNode(key, out node);
			}
		}

		public bool IsFunction(EvaluationName name) {
			return environment.IsFunction(name);
		}

		public EnvironmentFunctionInfo GetFunctionInfo(EvaluationName name) {
			return environment.GetFunctionInfo(name);
		}

		public EnvironmentFunction GetFunction(EvaluationName name) {
			return environment.GetFunction(name);
		}

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> GetReturnTypes() {
			return GetVariables().ToDictionary(v => v, v => GetReturnType(v));
		}
		public IEnumerable<KeyValuePair<EvaluationName, EvaluationNode>> GetNodes() {
			return environment.GetNodes();
		}
		public IEnumerable<EnvironmentFunctionInfo> GetFunctionInfos() {
			return environment.GetFunctionInfos();
		}

		public IEnumerable<KeyValuePair<EvaluationName, object?>> GetValues() {
			return environment.GetValues().Append(new KeyValuePair<EvaluationName, object?>(loopIdentifier, currentValue));
		}
		public IEnumerable<EnvironmentFunctionDefinition> GetFunctions() {
			return environment.GetFunctions();
		}
	}

}
