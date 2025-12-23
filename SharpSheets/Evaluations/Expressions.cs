using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Utilities;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Colors;
using SharpSheets.Canvas;
using SharpSheets.Evaluations.Types;

namespace SharpSheets.Evaluations {

	public interface IExpression {

		bool IsConstant { get; }

		EvaluationContext Context { get; }

		IEnumerable<EvaluationName> GetVariables();

	}

	public interface IExpression<T> : IExpression {

		/// <summary>
		/// 
		/// </summary>
		/// <param name="environment"></param>
		/// <returns></returns>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		T Evaluate(IEnvironment environment);

	}

	public static class Expressions {

		public static IEnumerable<EvaluationName> GetVariables(params IExpression[] expr) {
			return EnumerableUtils.Concat(expr.SelectMany(e => e.GetVariables()));
		}

		public static bool IsConstant(params IExpression[] expr) {
			return expr.All(e => e.IsConstant);
		}

	}

	public static class ExpressionUtils {
		
		public static bool CanCompute(this IExpression expr, IVariableBox variables) {
			return expr.GetVariables().All(variables.IsVariable);
		}

		public static T[] Evaluate<T>(this IEnumerable<IExpression<T>> exprs, IEnvironment environment) {
			return exprs.Select(e => e.Evaluate(environment)).ToArray();
		}

	}

	public sealed class ConstantExpression<T> : IExpression<T> {
		public bool IsConstant => true;

		public T Value { get; }
		public EvaluationContext Context { get; }

		public ConstantExpression(T value, EvaluationContext context) {
			this.Value = value;
			this.Context = context;
		}

		public T Evaluate(IEnvironment environment) => Value;
		public IEnumerable<EvaluationName> GetVariables() => Enumerable.Empty<EvaluationName>();
	}

	public class FloatExpression : IExpression<float> {

		//public static readonly FloatExpression Zero = new FloatExpression(0f);

		public EvaluationNode Evaluation { get { return value ?? evaluation!; } }
		private readonly EvaluationNode? evaluation;
		private readonly EvaluationValue? value;

		public EvaluationContext Context => value?.Type.Context ?? evaluation!.Context;

		public bool IsConstant { get { return value.HasValue; } }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public FloatExpression(EvaluationNode evaluation) {
			if (FloatEvaluationType.IsReal(evaluation.GetReturnType())) {
				if (evaluation.IsConstant) {
					float value;
					EvaluationValue eval = evaluation.Evaluate(Environments.Empty(evaluation.Context)); // ?? throw new EvaluationProcessingException("Provided constant evaluation does not produce a value.");

					if (FloatEvaluationType.TryGetFloat(eval, out float evalResult)) {
						value = evalResult;
					}
					else {
						throw new EvaluationCalculationException("Provided constant evaluation does not produce a valid float value.");
					}

					this.evaluation = null;
					this.value = new EvaluationValue(value, evaluation.Context.GetType<FloatEvaluationType>());
				}
				else {
					this.evaluation = evaluation;
					this.value = null;
				}
			}
			else {
				throw new EvaluationTypeException("Invalid expression type.");
			}
		}
		public FloatExpression(float value, EvaluationContext context) {
			this.evaluation = null;
			this.value = new EvaluationValue(value, context.GetType<FloatEvaluationType>());
		}
		public static implicit operator FloatExpression(EvaluationNode evaluation) {
			return new FloatExpression(evaluation);
		}
		/*
		public static implicit operator FloatExpression(float value) {
			return new FloatExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return evaluation is not null ? evaluation.GetVariables() : Enumerable.Empty<EvaluationName>();
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static FloatExpression Parse(string text, IVariableBox variables) {
			EvaluationNode node = Evaluations.Evaluation.Parse(text, variables);
			return new FloatExpression(node);
		}

		public float Evaluate(IEnvironment environment) {
			if (value.HasValue && FloatEvaluationType.TryGetFloat(value.Value, out float floatVal)) {
				return floatVal;
			}
			else {
				EvaluationValue result = evaluation!.Evaluate(environment); // ?? throw new EvaluationCalculationException("Evaluation does not produce a value.");
				
				if(FloatEvaluationType.TryGetFloat(result, out float realValue)) {
					return realValue;
				}
				else {
					throw new EvaluationCalculationException("Evaluation does not produce a valid float value.");
				}
			}
		}

		public static FloatExpression operator +(FloatExpression a, FloatExpression b) {
			return new FloatExpression(a.Evaluation + b.Evaluation);
		}
		public static FloatExpression operator +(FloatExpression a, float b) {
			return a + new FloatExpression(b, a.Context);
		}
		public static FloatExpression operator +(float a, FloatExpression b) {
			return new FloatExpression(a, b.Context) + b;
		}
		public static FloatExpression operator -(FloatExpression a, FloatExpression b) {
			return new FloatExpression(a.Evaluation - b.Evaluation);
		}
		public static FloatExpression operator -(FloatExpression a, float b) {
			return a - new FloatExpression(b, a.Context);
		}
		public static FloatExpression operator -(float a, FloatExpression b) {
			return new FloatExpression(a, b.Context) - b;
		}
		public static FloatExpression operator -(FloatExpression a) {
			return new FloatExpression(-a.Evaluation);
		}
		public static FloatExpression operator *(FloatExpression a, FloatExpression b) {
			return new FloatExpression(a.Evaluation * b.Evaluation);
		}
		public static FloatExpression operator *(FloatExpression a, float b) {
			return a * new FloatExpression(b, a.Context);
		}
		public static FloatExpression operator *(float a, FloatExpression b) {
			return new FloatExpression(a, b.Context) * b;
		}
		public static FloatExpression operator /(FloatExpression a, FloatExpression b) {
			return new FloatExpression(a.Evaluation / b.Evaluation);
		}
		public static FloatExpression operator /(FloatExpression a, float b) {
			return a / new FloatExpression(b, a.Context);
		}
		public static FloatExpression operator /(float a, FloatExpression b) {
			return new FloatExpression(a, b.Context) / b;
		}

		public override string ToString() {
			return value.HasValue ? (value.Value.Value?.ToString() ?? "") : evaluation!.ToString()!;
		}
	}

	public class IntExpression : IExpression<int> {
		public EvaluationNode Evaluation { get { return value ?? evaluation!; } }
		private readonly EvaluationNode? evaluation;
		private readonly EvaluationValue? value;

		public EvaluationContext Context => value?.Type.Context ?? evaluation!.Context;

		public bool IsConstant { get { return value.HasValue; } }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public IntExpression(EvaluationNode evaluation) {
			if (IntEvaluationType.IsIntegral(evaluation.GetReturnType())) {
				if (evaluation.IsConstant) {
					int value;
					EvaluationValue eval = evaluation.Evaluate(Environments.Empty(evaluation.Context)); // ?? throw new EvaluationProcessingException("Provided constant evaluation does not produce a value.");

					if (IntEvaluationType.TryGetInt(eval, out int evalResult)) {
						value = evalResult;
					}
					else {
						throw new EvaluationCalculationException("Provided constant evaluation does not produce a valid integer value.");
					}

					this.evaluation = null;
					this.value = new EvaluationValue(value, evaluation.Context.GetType<IntEvaluationType>());
				}
				else {
					this.evaluation = evaluation;
					this.value = null;
				}
			}
			else {
				throw new EvaluationTypeException("Invalid expression type.");
			}
		}
		public IntExpression(int value, EvaluationContext context) {
			this.evaluation = null;
			this.value = new EvaluationValue(value, context.GetType<IntEvaluationType>());
		}
		public static implicit operator IntExpression(EvaluationNode evaluation) {
			return new IntExpression(evaluation);
		}
		/*
		public static implicit operator IntExpression(int value) {
			return new IntExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return evaluation is not null ? evaluation.GetVariables() : Enumerable.Empty<EvaluationName>();
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static IntExpression Parse(string text, IVariableBox variables) {
			EvaluationNode node = Evaluations.Evaluation.Parse(text, variables);
			return new IntExpression(node);
		}

		public int Evaluate(IEnvironment environment) {
			if (value.HasValue && IntEvaluationType.TryGetInt(value.Value, out int intVal)) {
				return intVal;
			}
			else {
				EvaluationValue result = evaluation!.Evaluate(environment); // ?? throw new EvaluationCalculationException("Evaluation does not produce a value.");

				if (IntEvaluationType.TryGetInt(result, out int intValue)) {
					return intValue;
				}
				else {
					throw new EvaluationCalculationException("Evaluation does not produce a valid integer value.");
				}
			}
		}

		public static IntExpression operator +(IntExpression a, IntExpression b) {
			return new IntExpression(a.Evaluation + b.Evaluation);
		}
		public static IntExpression operator -(IntExpression a, IntExpression b) {
			return new IntExpression(a.Evaluation - b.Evaluation);
		}
		public static IntExpression operator -(IntExpression a) {
			return new IntExpression(-a.Evaluation);
		}
		public static IntExpression operator *(IntExpression a, IntExpression b) {
			return new IntExpression(a.Evaluation * b.Evaluation);
		}
		public static IntExpression operator /(IntExpression a, IntExpression b) {
			return new IntExpression(a.Evaluation / b.Evaluation);
		}

		public override string ToString() {
			return value.HasValue ? (value.Value.Value?.ToString() ?? "") : evaluation!.ToString()!;
		}
	}

	public class StringExpression : IExpression<string> {
		public EvaluationNode Evaluation { get { return value ?? evaluation!; } }
		private readonly EvaluationNode? evaluation;
		private readonly EvaluationValue? value;

		public EvaluationContext Context => value?.Type.Context ?? evaluation!.Context;

		public bool IsConstant { get { return value != null; } }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public StringExpression(EvaluationNode evaluation) {
			if (StringEvaluationType.IsString(evaluation.GetReturnType())) {
				if (evaluation.IsConstant) {
					string value;
					EvaluationValue eval = evaluation.Evaluate(Environments.Empty(evaluation.Context)); // ?? throw new EvaluationProcessingException("Provided constant evaluation does not produce a value.");

					if (StringEvaluationType.TryGetString(eval, out string? evalResult)) {
						value = evalResult;
					}
					else {
						throw new EvaluationCalculationException("Provided constant evaluation does not produce a valid string value.");
					}

					this.evaluation = null;
					this.value = new EvaluationValue(value, evaluation.Context.GetType<StringEvaluationType>());
				}
				else {
					this.evaluation = evaluation;
					this.value = null;
				}
			}
			else {
				throw new EvaluationTypeException("Invalid expression type.");
			}
		}
		public StringExpression(string value, EvaluationContext context) {
			this.evaluation = null;
			this.value = new EvaluationValue(value, context.GetType<StringEvaluationType>());
		}
		public static implicit operator StringExpression(EvaluationNode evaluation) {
			return new StringExpression(evaluation);
		}
		/*
		public static implicit operator StringExpression(string value) {
			return new StringExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return evaluation is not null ? evaluation.GetVariables() : Enumerable.Empty<EvaluationName>();
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static StringExpression Parse(string text, IVariableBox variables) {
			EvaluationNode node = Evaluations.Evaluation.Parse(text, variables);
			return new StringExpression(node);
		}

		public string Evaluate(IEnvironment environment) {
			if (value.HasValue && StringEvaluationType.TryGetString(value.Value, out string? stringVal)) {
				return stringVal;
			}
			else {
				EvaluationValue result = evaluation!.Evaluate(environment); // ?? throw new EvaluationCalculationException("Evaluation does not produce a value.");

				if (StringEvaluationType.TryGetString(result, out string? stringValue)) {
					return stringValue;
				}
				else {
					throw new EvaluationCalculationException("Evaluation does not produce a valid string value.");
				}
			}
		}

		public static StringExpression operator +(StringExpression a, StringExpression b) {
			return new StringExpression(a.Evaluation + b.Evaluation);
		}

		public override string ToString() {
			return value != null ? ("\"" + value + "\"") : evaluation!.ToString()!;
		}
	}

	public class BoolExpression : IExpression<bool> {
		private EvaluationNode Evaluation { get { return value ?? evaluation!; } }
		private readonly EvaluationNode? evaluation;
		private readonly EvaluationValue? value;

		public EvaluationContext Context => value?.Type.Context ?? evaluation!.Context;

		public bool IsConstant { get { return value.HasValue; } }
		public bool IsTrue { get { return value.HasValue && value.Value.Value is bool boolVal && boolVal; } }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public BoolExpression(EvaluationNode evaluation) {
			if (BoolEvaluationType.IsBool(evaluation.GetReturnType())) {
				if (evaluation.IsConstant) {
					bool value;
					EvaluationValue eval = evaluation.Evaluate(Environments.Empty(evaluation.Context)); // ?? throw new EvaluationProcessingException("Provided constant evaluation does not produce a value.");

					if (BoolEvaluationType.TryGetBool(eval, out bool evalResult)) {
						value = evalResult;
					}
					else {
						throw new EvaluationCalculationException("Provided constant evaluation does not produce a valid boolean value.");
					}

					this.evaluation = null;
					this.value = new EvaluationValue(value, evaluation.Context.GetType<BoolEvaluationType>());
				}
				else {
					this.evaluation = evaluation;
					this.value = null;
				}
			}
			else {
				throw new EvaluationTypeException("Invalid expression type.");
			}
		}
		public BoolExpression(bool value, EvaluationContext context) {
			this.evaluation = null;
			this.value = new EvaluationValue(value, context.GetType<BoolEvaluationType>());
		}
		public static implicit operator BoolExpression(EvaluationNode evaluation) {
			return new BoolExpression(evaluation);
		}
		/*
		public static implicit operator BoolExpression(bool value) {
			return new BoolExpression(value);
		}
		*/
		/*
		public static explicit operator BoolExpression(FloatExpression floatExpr) {
			return new BoolExpression(new InequalityNode() { First = floatExpr.Evaluation, Second = 0f });
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return evaluation is not null ? evaluation.GetVariables() : Enumerable.Empty<EvaluationName>();
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static BoolExpression Parse(string text, IVariableBox variables) {
			EvaluationNode node = Evaluations.Evaluation.Parse(text, variables);
			return new BoolExpression(node);
		}

		public bool Evaluate(IEnvironment environment) {
			if (value.HasValue && BoolEvaluationType.TryGetBool(value.Value, out bool boolVal)) {
				return boolVal;
			}
			else {
				EvaluationValue result = evaluation!.Evaluate(environment); // ?? throw new EvaluationCalculationException("Evaluation does not produce a value.");

				if (BoolEvaluationType.TryGetBool(result, out bool boolValue)) {
					return boolValue;
				}
				else {
					throw new EvaluationCalculationException("Evaluation does not produce a valid boolean value.");
				}
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static BoolExpression IsNonZero(FloatExpression floatExpr) {
			return new BoolExpression(
				new InequalityNode(floatExpr.Context) {
					First = floatExpr.Evaluation,
					Second = new EvaluationValue(0f, floatExpr.Context.GetType<FloatEvaluationType>()) 
				}
			);
		}

		public static BoolExpression operator &(BoolExpression a, BoolExpression b) {
			return new BoolExpression(a.Evaluation & b.Evaluation);
		}
		public static BoolExpression operator |(BoolExpression a, BoolExpression b) {
			return new BoolExpression(a.Evaluation | b.Evaluation);
		}

		public override string ToString() {
			return value.HasValue ? (value.Value.Value?.ToString() ?? "") : evaluation!.ToString()!;
		}
	}

	public class EnumExpression<T> : IExpression<T> where T : Enum {

		public bool IsConstant { get { return evaluation is null || evaluation.IsConstant; } }

		private EnumEvaluationType EvaluationType { get; }

		private readonly EvaluationNode? evaluation;
		private readonly T? value;

		public EvaluationContext Context => this.EvaluationType.Context;

		/*
		public EnumExpression(StringExpression expression) {
			this.expression = expression;
			this.value = default;
		}
		*/

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public EnumExpression(EvaluationNode evaluation) {
			this.EvaluationType = EnumEvaluationType.FromSystemType<T>(evaluation.Context);

			EvaluationType expressionType = evaluation.GetReturnType();

			if (evaluation.IsConstant) {
				this.evaluation = null;
				EvaluationValue eval = evaluation.Evaluate(Environments.Empty(evaluation.Context));
				if (eval.Type == this.EvaluationType && this.EvaluationType.TryGetEnumValue(eval, out Enum? evalEnum) && evalEnum is T finalEnum) {
					this.value = finalEnum;
				}
				else {
					throw new EvaluationTypeException("Invalid expression type.");
				}
			}
			else if (this.EvaluationType == expressionType || StringEvaluationType.IsString(expressionType)) {
				this.evaluation = evaluation;
				this.value = default;
			}
			else {
				throw new EvaluationTypeException("Invalid expression type.");
			}
		}
		public EnumExpression(T value, EvaluationContext context) {
			this.EvaluationType = EnumEvaluationType.FromSystemType<T>(context);

			this.evaluation = null;
			this.value = value;
		}
		/*
		public static implicit operator EnumExpression<T>(T value) {
			return new EnumExpression<T>(value);
		}
		*/

		public T Evaluate(IEnvironment environment) {
			if (evaluation is null) {
				return value!;
			}
			else {
				EvaluationValue evaluated = evaluation.Evaluate(environment);
				if(this.EvaluationType.TryGetEnumValue(evaluated, out Enum? enumEval) && enumEval is T result) {
					return result;
				}
				else {
					throw new EvaluationCalculationException($"Invalid evaluation result type for EnumExpression<{typeof(T).Name}>.");
				}
			}
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return evaluation is null ? Enumerable.Empty<EvaluationName>() : evaluation.GetVariables();
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public static EnumExpression<T> Parse(string text, IVariableBox variables) {
			if (EnumUtils.IsDefined<T>(text)) { // Enum.IsDefined(typeof(T), text)
				return new EnumExpression<T>(ParseEnum(text), variables.Context);
			}
			else {
				EvaluationNode node = Evaluation.Parse(text, variables);
				return new EnumExpression<T>(node);
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		private static T ParseEnum(string text) {
			try {
				return EnumUtils.ParseEnum<T>(text);
			}
			catch(FormatException e) {
				throw new EvaluationCalculationException($"Could not parse string \"{text}\" into {typeof(T).Name}.", e);
			}
		}
	}

}
