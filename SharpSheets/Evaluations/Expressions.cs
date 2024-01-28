using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Utilities;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Colors;
using SharpSheets.Canvas;

namespace SharpSheets.Evaluations {

	public interface IExpression<T> {

		bool IsConstant { get; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="environment"></param>
		/// <returns></returns>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		T Evaluate(IEnvironment environment);

		IEnumerable<EvaluationName> GetVariables();

	}

	public static class ExpressionUtils {
		public static bool CanCompute<T>(this IExpression<T> expr, IVariableBox variables) {
			return expr.GetVariables().All(variables.IsVariable);
		}
	}

	public class FloatExpression : IExpression<float> {

		public static readonly FloatExpression Zero = new FloatExpression(0f);

		public EvaluationNode Evaluation { get { return value ?? evaluation!; } }
		private readonly EvaluationNode? evaluation;
		private readonly float? value;

		public bool IsConstant { get { return value.HasValue; } }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public FloatExpression(EvaluationNode evaluation) {
			if (evaluation.ReturnType.IsReal()) {
				if (evaluation.IsConstant) {
					float value;
					object eval = evaluation.Evaluate(Environments.Empty) ?? throw new EvaluationProcessingException("Provided constant evaluation does not produce a value.");

					if(EvaluationTypes.TryGetReal(eval, out float evalResult)) {
						value = evalResult;
					}
					else {
						throw new EvaluationCalculationException("Provided constant evaluation does not produce a valid float value.");
					}

					this.evaluation = null;
					this.value = value;
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
		public FloatExpression(float value) {
			this.evaluation = null;
			this.value = value;
		}
		public static implicit operator FloatExpression(EvaluationNode evaluation) {
			return new FloatExpression(evaluation);
		}
		public static implicit operator FloatExpression(float value) {
			return new FloatExpression(value);
		}

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
			if (value.HasValue) {
				return value.Value;
			}
			else {
				object result = evaluation!.Evaluate(environment) ?? throw new EvaluationCalculationException("Evaluation does not produce a value.");
				
				if(EvaluationTypes.TryGetReal(result, out float realValue)) {
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
		public static FloatExpression operator -(FloatExpression a, FloatExpression b) {
			return new FloatExpression(a.Evaluation - b.Evaluation);
		}
		public static FloatExpression operator -(FloatExpression a) {
			return new FloatExpression(-a.Evaluation);
		}
		public static FloatExpression operator *(FloatExpression a, FloatExpression b) {
			return new FloatExpression(a.Evaluation * b.Evaluation);
		}
		public static FloatExpression operator /(FloatExpression a, FloatExpression b) {
			return new FloatExpression(a.Evaluation / b.Evaluation);
		}

		public override string ToString() {
			return value.HasValue ? value.Value.ToString() : evaluation!.ToString()!;
		}
	}

	public class IntExpression : IExpression<int> {
		public EvaluationNode Evaluation { get { return value ?? evaluation!; } }
		private readonly EvaluationNode? evaluation;
		private readonly int? value;

		public bool IsConstant { get { return value.HasValue; } }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public IntExpression(EvaluationNode evaluation) {
			if (evaluation.ReturnType.IsIntegral()) {
				if (evaluation.IsConstant) {
					int value;
					object eval = evaluation.Evaluate(Environments.Empty) ?? throw new EvaluationProcessingException("Provided constant evaluation does not produce a value.");

					if (EvaluationTypes.TryGetIntegral(eval, out int evalResult)) {
						value = evalResult;
					}
					else {
						throw new EvaluationCalculationException("Provided constant evaluation does not produce a valid integer value.");
					}

					this.evaluation = null;
					this.value = value;
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
		public IntExpression(int value) {
			this.evaluation = null;
			this.value = value;
		}
		public static implicit operator IntExpression(EvaluationNode evaluation) {
			return new IntExpression(evaluation);
		}
		public static implicit operator IntExpression(int value) {
			return new IntExpression(value);
		}

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
			if (value.HasValue) {
				return value.Value;
			}
			else {
				object result = evaluation!.Evaluate(environment) ?? throw new EvaluationCalculationException("Evaluation does not produce a value.");

				if (EvaluationTypes.TryGetIntegral(result, out int intValue)) {
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
			return value.HasValue ? value.Value.ToString() : evaluation!.ToString()!;
		}
	}

	public class StringExpression : IExpression<string> {
		public EvaluationNode Evaluation { get { return value ?? evaluation!; } }
		private readonly EvaluationNode? evaluation;
		private readonly string? value;

		public bool IsConstant { get { return value != null; } }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public StringExpression(EvaluationNode evaluation) {
			if (evaluation.ReturnType == EvaluationType.STRING) {
				if (evaluation.IsConstant) {
					this.evaluation = null;
					value = (string)(evaluation.Evaluate(Environments.Empty) ?? throw new EvaluationProcessingException("Provided constant evaluation does not produce a value."));
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
		public StringExpression(string value) {
			this.evaluation = null;
			this.value = value;
		}
		public static implicit operator StringExpression(EvaluationNode evaluation) {
			return new StringExpression(evaluation);
		}
		public static implicit operator StringExpression(string value) {
			return new StringExpression(value);
		}

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
			if (value != null) {
				return value;
			}
			else {
				return (string)(evaluation!.Evaluate(environment) ?? throw new EvaluationCalculationException("Evaluation does not produce a value."));
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
		private readonly bool? value;

		public bool IsConstant { get { return value.HasValue; } }
		public bool IsTrue { get { return value.HasValue && value.Value; } }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public BoolExpression(EvaluationNode evaluation) {
			if (evaluation.ReturnType == EvaluationType.BOOL) {
				if (evaluation.IsConstant) {
					this.evaluation = null;
					value = (bool)(evaluation.Evaluate(Environments.Empty) ?? throw new EvaluationProcessingException("Provided constant evaluation does not produce a value."));
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
		public BoolExpression(bool value) {
			this.evaluation = null;
			this.value = value;
		}
		public static implicit operator BoolExpression(EvaluationNode evaluation) {
			return new BoolExpression(evaluation);
		}
		public static implicit operator BoolExpression(bool value) {
			return new BoolExpression(value);
		}
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
			if (value.HasValue) {
				return value.Value;
			}
			else {
				return (bool)(evaluation!.Evaluate(environment) ?? throw new EvaluationCalculationException("Evaluation does not produce a value."));
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static BoolExpression IsNonZero(FloatExpression floatExpr) {
			return new BoolExpression(new InequalityNode() { First = floatExpr.Evaluation, Second = 0f });
		}

		public static BoolExpression operator &(BoolExpression a, BoolExpression b) {
			return new BoolExpression(a.Evaluation & b.Evaluation);
		}
		public static BoolExpression operator |(BoolExpression a, BoolExpression b) {
			return new BoolExpression(a.Evaluation | b.Evaluation);
		}

		public override string ToString() {
			return value.HasValue ? value.Value.ToString() : evaluation!.ToString()!;
		}
	}

	public class ColorExpression : IExpression<Color> {
		public EvaluationNode Evaluation { get { return value ?? evaluation!; } }
		private readonly EvaluationNode? evaluation;
		private readonly Color? value;
		private FloatExpression? Opacity { get; set; }

		public bool IsConstant { get { return value.HasValue && (Opacity == null || Opacity.IsConstant); } }

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public ColorExpression(EvaluationNode evaluation) {
			if (evaluation.ReturnType == EvaluationType.COLOR || evaluation.ReturnType == EvaluationType.STRING) {
				if (evaluation.IsConstant) {
					this.evaluation = null;
					object? result = evaluation.Evaluate(Environments.Empty);
					if(result is Color color) {
						value = color;
					}
					else if (result is string colorStr) {
						value = ParseColor(colorStr);
					}
					else {
						throw new EvaluationTypeException("Invalid expression type.");
					}
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
		public ColorExpression(Color value) {
			this.evaluation = null;
			this.value = value;
		}
		public static implicit operator ColorExpression(Color value) {
			return new ColorExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			if(evaluation is not null) {
				foreach(EvaluationName name in evaluation.GetVariables()) { yield return name; }
			}
			if (Opacity is not null) {
				foreach (EvaluationName name in Opacity.GetVariables()) { yield return name; }
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public static ColorExpression Parse(string text, IVariableBox variables) {
			try {
				return new ColorExpression(ColorUtils.Parse(text));
			}
			catch (FormatException) { }
			return new ColorExpression(Evaluations.Evaluation.Parse(text, variables));
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public ColorExpression WithOpacity(FloatExpression opacity) {
			if (value.HasValue) {
				return new ColorExpression(value.Value) { Opacity = opacity };
			}
			else {
				return new ColorExpression(evaluation!) { Opacity = opacity };
			}
		}

		public Color Evaluate(IEnvironment environment) {
			Color result;
			if (value.HasValue) {
				result = value.Value;
			}
			else {
				object? eval = evaluation!.Evaluate(environment);
				if (eval is Color color) {
					result = color;
				}
				else if (eval is string colorStr) {
					result = ParseColor(colorStr);
				}
				else {
					throw new EvaluationCalculationException("Invalid expression type.");
				}
			}
			if (Opacity != null) {
				float opacity = this.Opacity.Evaluate(environment);
				result = result.WithOpacity(opacity);
			}
			return result;
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		private static Color ParseColor(string colorStr) {
			try {
				return ColorUtils.Parse(colorStr);
			}
			catch (FormatException e) {
				throw new EvaluationCalculationException($"Could not parse color string \"{colorStr}\".", e);
			}
		}
	}

	public class EnumExpression<T> : IExpression<T> where T : Enum {

		public bool IsConstant { get { return evaluation is null || evaluation.IsConstant; } }

		private readonly EvaluationNode? evaluation;
		private readonly T? value;

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
			if (evaluation.IsConstant) {
				this.evaluation = null;
				object? eval = evaluation.Evaluate(Environments.Empty);
				if (evaluation.ReturnType == EvaluationType.STRING && eval is string result) {
					this.value = ParseEnum(result);
				}
				else if (evaluation.ReturnType.IsEnum && evaluation.ReturnType.DisplayType == typeof(T) && eval is T enumVal) {
					this.value = enumVal;
				}
				else {
					throw new EvaluationTypeException("Invalid expression type.");
				}
			}
			else if ((evaluation.ReturnType.IsEnum && evaluation.ReturnType.DisplayType == typeof(T)) || evaluation.ReturnType == EvaluationType.STRING) {
				this.evaluation = evaluation;
				this.value = default;
			}
			else {
				throw new EvaluationTypeException("Invalid expression type.");
			}
		}
		public EnumExpression(T value) {
			this.evaluation = null;
			this.value = value;
		}
		public static implicit operator EnumExpression<T>(T value) {
			return new EnumExpression<T>(value);
		}

		public T Evaluate(IEnvironment environment) {
			if (evaluation is null) {
				return value!;
			}
			else {
				object? evaluated = evaluation.Evaluate(environment);
				if(evaluated is string stringVal) {
					return ParseEnum(stringVal);
				}
				else if(evaluated is T enumVal) {
					return enumVal;
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
				return ParseEnum(text);
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
