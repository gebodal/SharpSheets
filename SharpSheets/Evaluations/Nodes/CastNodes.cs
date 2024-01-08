using System;
using System.Linq;
using System.Text.RegularExpressions;
using SharpSheets.Utilities;

namespace SharpSheets.Evaluations.Nodes {

	public class IntCastFunction : AbstractFunction {

		public static readonly IntCastFunction Instance = new IntCastFunction();
		private IntCastFunction() { }

		public override EvaluationName Name { get; } = "int";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			if (argType.IsReal() || argType == EvaluationType.BOOL || argType == EvaluationType.STRING) {
				return EvaluationType.INT;
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {argType} to integer.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
				return aInt;
			}
			else if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return (int)aFloat;
			}
			else if (a is bool aBool) {
				return aBool ? 1 : 0;
			}
			else if (a is string aStr) {
				try {
					return int.Parse(aStr);
				}
				catch(FormatException e) {
					throw new EvaluationCalculationException($"Provided string is not a valid int: \"{aStr}\"", e);
				}
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {EvaluationUtils.GetDataTypeName(a)} to integer.");
			}
		}
	}

	public class FloatCastFunction : AbstractFunction {

		public static readonly FloatCastFunction Instance = new FloatCastFunction();
		private FloatCastFunction() { }

		public override EvaluationName Name { get; } = "float";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			if (argType.IsReal() || argType == EvaluationType.BOOL || argType == EvaluationType.STRING) {
				return EvaluationType.FLOAT;
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {argType} to float.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return aFloat;
			}
			else if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
				return (float)aInt;
			}
			else if (a is bool aBool) {
				return aBool ? 1f : 0f;
			}
			else if (a is string aStr) {
				return Parse(aStr);
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {EvaluationUtils.GetDataTypeName(a)} to float.");
			}
		}

		private static readonly Regex floatRegex = new Regex(@"^\s*[\-\+]?\s*([0-9]+(\.[0-9]*)?|\.[0-9]+)\s*$");
		private static readonly Regex fracRegex = new Regex(@"^\s*(?<numer>[\-\+]?\s*([0-9]+(\.[0-9]*)?|\.[0-9]+))\s*\/\s*(?<denom>([0-9]+(\.[0-9]*)?|\.[0-9]+))\s*");
		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		private static float Parse(string str) {
			try {
				Match match;
				if (floatRegex.IsMatch(str)) {
					return float.Parse(str.Replace(" ", ""));
				}
				else if ((match = fracRegex.Match(str)).Success) {
					float numer = float.Parse(match.Groups["numer"].Value.Replace(" ", ""));
					float denom = float.Parse(match.Groups["denom"].Value.Replace(" ", ""));
					return numer / denom;
				}
			}
			catch (FormatException) { }

			throw new EvaluationCalculationException($"Provided string is not a valid float: \"{str}\"");
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static EvaluationNode MakeFloatCastNode(EvaluationNode argument) {
			EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance);
			node.SetArgumentCount(1);
			node.Arguments[0] = argument;
			return node.Simplify();
		}
	}

	public class BoolCastFunction : AbstractFunction {

		public static readonly BoolCastFunction Instance = new BoolCastFunction();
		private BoolCastFunction() { }

		public override EvaluationName Name { get; } = "bool";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			if (argType.IsReal() || argType == EvaluationType.BOOL || argType == EvaluationType.STRING) {
				return EvaluationType.BOOL;
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {argType} to boolean.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (a is bool aBool) {
				return aBool;
			}
			else if (EvaluationTypes.TryGetReal(a, out float aFloat)) {
				return aFloat != 0f;
			}
			else if (EvaluationTypes.TryGetIntegral(a, out int aInt)) {
				return aInt != 0;
			}
			else if (a is string aStr) {
				try {
					return bool.Parse(aStr);
				}
				catch (FormatException e) {
					throw new EvaluationCalculationException($"Provided string is not a valid boolean: \"{aStr}\"", e);
				}
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {EvaluationUtils.GetDataTypeName(a)} to boolean.");
			}
		}
	}

	public class StringCastFunction : AbstractFunction {

		public static readonly StringCastFunction Instance = new StringCastFunction();
		private StringCastFunction() { }

		public override EvaluationName Name { get; } = "str";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", null, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			if (argType.IsReal() || argType == EvaluationType.BOOL || argType == EvaluationType.STRING || argType.IsEnum) {
				return EvaluationType.STRING;
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {argType} to string.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			object? a = args[0].Evaluate(environment);

			if (argType.IsReal() || argType == EvaluationType.BOOL || argType == EvaluationType.STRING) {
				return a?.ToString() ?? ""; // Sensible fallback?
			}
			else if (argType.IsEnum) {
				if (a is null) {
					throw new EvaluationCalculationException("Cannot convert null enum value to string.");
				}
				return (a.ToString() ?? throw new EvaluationCalculationException("Could not resolve enum name.")).ToUpperInvariant();
			}
			else {
				throw new EvaluationCalculationException($"Cannot cast variable of type {argType} to string.");
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static EvaluationNode MakeStringCastNode(EvaluationNode argument) {
			EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance);
			node.SetArgumentCount(1);
			node.Arguments[0] = argument;
			return node.Simplify();
		}
	}

	public class ColorCreateFunction : AbstractFunction {

		public static readonly ColorCreateFunction Instance = new ColorCreateFunction();
		private ColorCreateFunction() { }

		public override EvaluationName Name { get; } = "color";
		public override string? Description { get; } = null;

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(
			"Color must take 1, 3, or 4 real-valued arguments.",
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("a", EvaluationType.FLOAT, null),
				new EnvironmentFunctionArg("r", EvaluationType.FLOAT, null),
				new EnvironmentFunctionArg("g", EvaluationType.FLOAT, null),
				new EnvironmentFunctionArg("b", EvaluationType.FLOAT, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("r", EvaluationType.FLOAT, null),
				new EnvironmentFunctionArg("g", EvaluationType.FLOAT, null),
				new EnvironmentFunctionArg("b", EvaluationType.FLOAT, null)
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("gray", EvaluationType.FLOAT, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			if (args.All(a => a.ReturnType.IsReal())) {
				int count = args.Length;
				if (count == 1 || count == 3 || count == 4) {
					return EvaluationType.COLOR;
				}
				else {
					throw new EvaluationTypeException("Color must take 1, 3, or 4 real-valued arguments.");
				}
			}
			else {
				string s = (args.Length > 1) ? "s" : "";
				throw new EvaluationTypeException($"Cannot create a color from arguments with type{s}: " + string.Join(", ", args.Select(a => a.ReturnType.ToString())));
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object[] argVals = EvaluationTypes.VerifyArray(args.Select(a => a.Evaluate(environment)).ToArray());

			if (argVals.Length != 1 && argVals.Length != 3 && argVals.Length != 4) {
				throw new EvaluationCalculationException("Color must take 1, 3, or 4 real-valued arguments.");
			}

			// TODO Should be able to provide a color name somehow. Should it be in this function, or is that too much overloading?

			bool badTypes = false;
			float[] values = new float[argVals.Length];

			for (int i = 0; i < argVals.Length; i++) {
				if (EvaluationTypes.TryGetReal(argVals[i], out float realVal)) {
					values[i] = realVal.Clamp(0f, 1f);
				}
				else {
					badTypes = true;
					break;
				}
			}

			if (badTypes) {
				Type[] types = argVals.Select(a => a.GetType()).Distinct().ToArray();
				string s = (types.Length > 1) ? "s" : "";
				throw new EvaluationTypeException($"Cannot create a color from arguments with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
			}

			if (values.Length == 1) {
				return ColorUtils.FromGrayscale(values[0]);
			}
			else if (values.Length == 3) {
				return ColorUtils.FromRGB(values[0], values[1], values[2]);
			}
			else {
				return ColorUtils.FromRGBA(values[1], values[2], values[3], values[0]);
			}
		}

	}

}
