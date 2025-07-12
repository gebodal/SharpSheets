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
		IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> ProvidedVariables(EvaluationContext context);
	}

	public class ComprehensionNode : BinaryOperatorNode, IVariableProvider {
		public override int Precedence { get; } = 12;
		public override Associativity Associativity { get; } = Associativity.RIGHT;
		public override string Symbol => throw new NotSupportedException();

		public override int[] CalculationOrder { get; } = new int[] { 1, 0 };

		public override EvaluationType GetReturnType() {
			EvaluationType secondType = Second.GetReturnType();
			if (secondType.IterationResult() is not null) {
				return First.GetReturnType().MakeArray();
			}
			else {
				throw new EvaluationTypeException($"Comprehension requires an array from which to draw values (got {secondType}).");
			}
		}

		public EvaluationName LoopVariable { get; }

		public ComprehensionNode(EvaluationName loopVariable, EvaluationContext context) : base(context) {
			this.LoopVariable = loopVariable;
		}

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> ProvidedVariables(EvaluationContext context) {
			/*
			EvaluationType loopVarType = Second.GetReturnType().IterationResult() ?? throw new EvaluationTypeException("Comprehension requires an iterable source from which to draw values.");
			return new KeyValuePair<EvaluationName, EvaluationType>(LoopVariable, loopVarType).Yield();
			*/
			try {
				EvaluationType loopVarType = Second.GetReturnType().IterationResult() ?? throw new EvaluationTypeException("Comprehension requires an iterable source from which to draw values.");
				return new KeyValuePair<EvaluationName, EvaluationType>(LoopVariable, loopVarType).Yield();
			}
			catch (UndefinedVariableException) {
				return Enumerable.Empty<KeyValuePair<EvaluationName, EvaluationType>>();
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			EvaluationType secondIterationType = Second.GetReturnType().IterationResult() ?? throw new EvaluationTypeException("Comprehension requires an iterable source from which to draw values.");

			ComprehensionEnvironment loopEnv = new ComprehensionEnvironment(LoopVariable, secondIterationType, environment);

			EvaluationType resultElementType = First.GetReturnType();

			EvaluationValue secondResult = Second.Evaluate(environment);

			List<EvaluationValue> result = new List<EvaluationValue>();
			foreach (EvaluationValue loopVal in (secondResult.Type.Iteration(secondResult) ?? throw new EvaluationTypeException($"Comprehension cannot iterate over source of type {secondResult.Type}."))) {
				loopEnv.SetLoopVariable(loopVal.Value);
				EvaluationValue loopResult = First.Evaluate(loopEnv);
				result.Add(loopResult);
			}

			return ArrayEvaluationType.MakeArray(resultElementType, result);
		}

		public override IEnumerable<EvaluationName> GetVariables() {
			return First.GetVariables().Where(k => k != LoopVariable).Concat(Second.GetVariables()).Distinct();
		}

		protected override BinaryOperatorNode Empty() {
			return new ComprehensionNode(LoopVariable, Context);
		}

		protected override string GetRepresentation() {
			return $"{First} for ${LoopVariable} in {Second}";
		}
	}

	public class ComprehensionIfNode : TernaryOperatorNode, IVariableProvider {
		public override int Precedence { get; } = 12;
		public override Associativity Associativity { get; } = Associativity.RIGHT;

		public override int[] CalculationOrder { get; } = new int[] { 1, 2, 0 };

		public ComprehensionIfNode(EvaluationContext context) : base(context) { }

		public override EvaluationType GetReturnType() {
			if (!BoolEvaluationType.IsBool(Third.GetReturnType())) {
				throw new EvaluationTypeException("Comprehension condition must be a boolean expression.");
			}
			else if (Second.GetReturnType().IterationResult() is not null) {
				return First.GetReturnType().MakeArray();
			}
			else {
				throw new EvaluationTypeException("Comprehension requires an array from which to draw values.");
			}
		}

		public override Type OpeningType { get; } = typeof(ComprehensionNode);

		public EvaluationName? LoopVariable { get; set; } = null;

		public IEnumerable<KeyValuePair<EvaluationName, EvaluationType>> ProvidedVariables(EvaluationContext context) {
			if (LoopVariable == null) {
				throw new EvaluationProcessingException("Loop variable not yet assigned.");
			}
			EvaluationType loopVarType = Second.GetReturnType().IterationResult() ?? throw new EvaluationTypeException("Comprehension-if requires an iterable source from which to draw values.");
			return new KeyValuePair<EvaluationName, EvaluationType>(LoopVariable.Value, loopVarType).Yield();
		}

		internal override void AssignOpening(OperatorNode openingNode) {
			if (openingNode is ComprehensionNode compNode) {
				LoopVariable = compNode.LoopVariable;
			}
			else {
				throw new EvaluationProcessingException("Invalid opening node for comprehension-if statement.");
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment) {
			if (LoopVariable == null) {
				throw new EvaluationProcessingException("Loop variable not yet assigned.");
			}

			EvaluationType secondIterationType = Second.GetReturnType().IterationResult() ?? throw new EvaluationTypeException("Comprehension-if requires an iterable source from which to draw values.");

			ComprehensionEnvironment loopEnv = new ComprehensionEnvironment(LoopVariable.Value, secondIterationType, environment);

			EvaluationType resultElementType = First.GetReturnType();

			EvaluationValue secondResult = Second.Evaluate(environment);

			List<EvaluationValue> result = new List<EvaluationValue>();
			foreach (EvaluationValue loopVal in (secondResult.Type.Iteration(secondResult) ?? throw new EvaluationTypeException($"Comprehension cannot iterate over source of type {secondResult.Type}."))) {
				loopEnv.SetLoopVariable(loopVal.Value);

				EvaluationValue conditionResult = Third.Evaluate(loopEnv);

				if (BoolEvaluationType.TryGetBool(conditionResult, out bool condition)) {
					EvaluationValue loopResult = First.Evaluate(loopEnv);
					result.Add(loopResult);
				}
				else {
					throw new EvaluationCalculationException("Cannot evaluate loop conditional.");
				}
			}

			return ArrayEvaluationType.MakeArray(resultElementType, result);
		}

		protected override TernaryOperatorNode Empty() {
			ComprehensionIfNode empty = new ComprehensionIfNode(Context) {
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
		private readonly EnvironmentVariableInfo loopVariableInfo;
		private object? currentValue = null;
		private bool initialized;

		public bool IsEmpty { get; } = false;
		public EvaluationContext Context => environment.Context;

		public ComprehensionEnvironment(EvaluationName loopIdentifier, EvaluationType loopVariableType, IEnvironment environment) {
			this.loopIdentifier = loopIdentifier;
			this.loopVariableInfo = new EnvironmentVariableInfo(loopIdentifier, loopVariableType, null);
			this.environment = environment;
			initialized = false;
		}

		public void SetLoopVariable(object? value) {
			currentValue = value;
			initialized = true;
		}

		public bool TryGetValue(EvaluationName key, [NotNullWhen(true)] out EvaluationValue? value) {
			if (!initialized) { throw new EvaluationProcessingException("Loop value not set."); }
			if (loopIdentifier == key) {
				value = new EvaluationValue(currentValue, loopVariableInfo.EvaluationType);
				return true;
			}
			else {
				return environment.TryGetValue(key, out value);
			}
		}

		public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
			if (loopIdentifier == key) {
				variableInfo = loopVariableInfo;
				return true;
			}
			else {
				return environment.TryGetVariableInfo(key, out variableInfo);
			}
		}

		public IEnumerable<EnvironmentVariableInfo> GetVariables() {
			return environment.GetVariables().Append(loopVariableInfo).Distinct();
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

		public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
			return environment.TryGetFunctionInfo(name, out functionInfo);
		}

		public bool TryGetFunction(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionEvaluator functionEvaluator) {
			return environment.TryGetFunction(name, out functionEvaluator);
		}

		public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() {
			return environment.GetFunctionInfos();
		}
	}

}
