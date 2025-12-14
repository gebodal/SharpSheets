using System;
using System.Linq;
using System.Text.RegularExpressions;
using SharpSheets.Utilities;

namespace SharpSheets.Evaluations.Nodes {

	public class IntCastFunction : AbstractFunction {

		public static readonly IntCastFunction Instance = new IntCastFunction();
		private IntCastFunction() { }

		public override EvaluationName Name { get; } = "int";
		public override string? Description { get; } = "Converts the argument into an integer. Strings will be parsed, floats will be rounded down, bools cast, and integers unchanged.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.STRING, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.FLOAT, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.BOOL, null))
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null))
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType argType = args[0].GetReturnType();
			if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType)) {
				return context.GetType<IntEvaluationType>();
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {argType} to integer.");
			}
		}

		private static EvaluationValue MakeResult(int result, EvaluationContext context) {
			return new EvaluationValue(result, context.GetType<IntEvaluationType>());
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);

			if (IntEvaluationType.TryGetInt(a, out int aInt)) {
				return MakeResult(aInt, environment.Context);
			}
			else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
				return MakeResult((int)aFloat, environment.Context);
			}
			else if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
				return MakeResult(aBool ? 1 : 0, environment.Context);
			}
			else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
				if (int.TryParse(aStr, out int parsed)) {
					return MakeResult(parsed, environment.Context);
				}
				else {
					throw new EvaluationCalculationException($"Provided string is not a valid int: \"{aStr}\"");
				}
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {a.Type} to integer.");
			}
		}

		public static EvaluationNode MakeIntCastNode(EvaluationNode argument) {
			EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, argument.Context);
			node.SetArguments(argument);
			return node.Simplify();
		}
	}

	public partial class FloatCastFunction : AbstractFunction {

		public static readonly FloatCastFunction Instance = new FloatCastFunction();
		private FloatCastFunction() { }

		public override EvaluationName Name { get; } = "float";
		public override string? Description { get; } = "Converts the argument into a floating point number. Strings will be parsed, bools and integers will be cast, and floating point numbers will be unchanged.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.STRING, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.FLOAT, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.BOOL, null))
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null))
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType argType = args[0].GetReturnType();
			if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType)) {
				return context.GetType<FloatEvaluationType>();
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {argType} to float.");
			}
		}

		private static EvaluationValue MakeResult(float result, EvaluationContext context) {
			return new EvaluationValue(result, context.GetType<FloatEvaluationType>());
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);

			if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
				return MakeResult(aFloat, environment.Context);
			}
			else if (IntEvaluationType.TryGetInt(a, out int aInt)) {
				return MakeResult((float)aInt, environment.Context);
			}
			else if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
				return MakeResult(aBool ? 1f : 0f, environment.Context);
			}
			else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
				return MakeResult(Parse(aStr), environment.Context);
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {a.Type} to float.");
			}
		}

		[GeneratedRegex(@"^\s*[\-\+]?\s*([0-9]+(\.[0-9]*)?|\.[0-9]+)\s*$")]
		private static partial Regex FloatRegex();
		[GeneratedRegex(@"^\s*(?<numer>[\-\+]?\s*([0-9]+(\.[0-9]*)?|\.[0-9]+))\s*\/\s*(?<denom>([0-9]+(\.[0-9]*)?|\.[0-9]+))\s*")]
		private static partial Regex FracRegex();
		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		private static float Parse(string str) {
			try {
				Match match;
				if (FloatRegex().IsMatch(str)) {
					return float.Parse(str.Replace(" ", ""));
				}
				else if ((match = FracRegex().Match(str)).Success) {
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
			EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, argument.Context);
			node.SetArguments(argument);
			return node.Simplify();
		}
	}

	public class BoolCastFunction : AbstractFunction {

		public static readonly BoolCastFunction Instance = new BoolCastFunction();
		private BoolCastFunction() { }

		public override EvaluationName Name { get; } = "bool";
		public override string? Description { get; } = "Converts the argument into a boolean value. Strings will be parsed, floats and integers will be compared to zero, and bools will be unchanged.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.STRING, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.FLOAT, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.BOOL, null))
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null))
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType argType = args[0].GetReturnType();
			if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType)) {
				return context.GetType<BoolEvaluationType>();
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {argType} to boolean.");
			}
		}

		private static EvaluationValue MakeResult(bool result, EvaluationContext context) {
			return new EvaluationValue(result, context.GetType<BoolEvaluationType>());
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);

			if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
				return MakeResult(aBool, environment.Context);
			}
			else if (IntEvaluationType.TryGetInt(a, out int aInt)) {
				return MakeResult(aInt != 0, environment.Context);
			}
			else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
				return MakeResult(aFloat != 0f, environment.Context);
			}
			else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
				if (bool.TryParse(aStr, out bool parsed)) {
					return MakeResult(parsed, environment.Context);
				}
				else {
					throw new EvaluationCalculationException($"Provided string is not a valid boolean: \"{aStr}\"");
				}
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {a.Type} to boolean.");
			}
		}
	}

	public class StringCastFunction : AbstractFunction {

		public static readonly StringCastFunction Instance = new StringCastFunction();
		private StringCastFunction() { }

		public override EvaluationName Name { get; } = "str";
		public override string? Description { get; } = "Converts the argument into a string value. Real values will be serialised as decimal numbers, bools as true/false, enums as the value name, and strings will be unchanged.";

		/*
		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.FLOAT, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.INT, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.BOOL, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", EvaluationTypes.STRING, null)),
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("enumVal", null, null))
		);
		*/

		public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<IntEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("enumVal", null, null))
			);
		}

		public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
			EvaluationType argType = args[0].GetReturnType();
			if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType) || EnumEvaluationType.IsEnum(argType)) {
				return context.GetType<StringEvaluationType>();
			}
			else {
				throw new EvaluationTypeException($"Cannot cast value of type {argType} to string.");
			}
		}

		private static EvaluationValue MakeResult(string result, EvaluationContext context) {
			return new EvaluationValue(result, context.GetType<StringEvaluationType>());
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);

			if (FloatEvaluationType.IsReal(a.Type) || BoolEvaluationType.IsBool(a.Type) || StringEvaluationType.IsString(a.Type)) {
				return MakeResult(a.Value?.ToString() ?? "", environment.Context); // Sensible fallback?
			}
			else if (EnumEvaluationType.IsEnum(a.Type)) {
				if (a.Value is null) {
					throw new EvaluationCalculationException("Cannot convert null enum value to string.");
				}
				return MakeResult((a.ToString() ?? throw new EvaluationCalculationException("Could not resolve enum name.")).ToUpperInvariant(), environment.Context);
			}
			else {
				throw new EvaluationCalculationException($"Cannot cast variable of type {a.Type} to string.");
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		public static EvaluationNode MakeStringCastNode(EvaluationNode argument) {
			EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, argument.Context);
			node.SetArguments(argument);
			return node.Simplify();
		}
	}

	/*
	public class ColorCreateFunction : AbstractFunction {

		public static readonly ColorCreateFunction Instance = new ColorCreateFunction();
		private ColorCreateFunction() { }

		public override EvaluationName Name { get; } = "color";
		public override string? Description { get; } = "Creates a color from the arguments, either from a single greyscale value, 3 RGB values, or 4 ARGB values. All arguments will be clamped between 0 and 1.";

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
				),
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("colorStr", EvaluationType.STRING, null)
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
			else if(args.Length == 1 && args[0].ReturnType == EvaluationType.STRING) {
				return EvaluationType.COLOR;
			}
			else {
				string s = (args.Length > 1) ? "s" : "";
				throw new EvaluationTypeException($"Cannot create a color from arguments with type{s}: " + string.Join(", ", args.Select(a => a.ReturnType.ToString())));
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object[] argVals = EvaluationTypes.VerifyArray(args.Select(a => a.Evaluate(environment)).ToArray());

			if (argVals.Length != 1 && argVals.Length != 3 && argVals.Length != 4) {
				throw new EvaluationCalculationException("Color must take 1, 3, or 4 real-valued arguments, or a single string.");
			}

			// TODO Should be able to provide a color name somehow. Should it be in this function, or is that too much overloading?

			if (argVals.Length == 1 && argVals[0] is string name) {
				try {
					return ColorUtils.Parse(name);
				}
				catch (FormatException e) {
					throw new EvaluationCalculationException(e.Message, e);
				}
			}
			else {
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
	*/

}
