using System;
using System.Linq;
using System.Text.RegularExpressions;
using SharpSheets.Utilities;

namespace SharpSheets.Evaluations.Nodes {

	public class IntCastNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				if (argType.IsReal() || argType == EvaluationType.BOOL || argType == EvaluationType.STRING) {
					return EvaluationType.INT;
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to integer.");
				}
			}
		}
		public sealed override string Name { get; } = "int";

		public override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

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
				throw new EvaluationTypeException($"Cannot cast value of type {GetDataTypeName(a)} to integer.");
			}
		}

		protected override FunctionNode Empty() {
			return new IntCastNode();
		}
	}

	public class FloatCastNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				if (argType.IsReal() || argType == EvaluationType.BOOL || argType == EvaluationType.STRING) {
					return EvaluationType.FLOAT;
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to float.");
				}
			}
		}
		public sealed override string Name { get; } = "float";

		public override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

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
				throw new EvaluationTypeException($"Cannot cast value of type {GetDataTypeName(a)} to float.");
			}
		}

		protected override FunctionNode Empty() {
			return new FloatCastNode();
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
	}

	public class BoolCastNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				if (argType.IsReal() || argType == EvaluationType.BOOL || argType == EvaluationType.STRING) {
					return EvaluationType.BOOL;
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to boolean.");
				}
			}
		}
		public sealed override string Name { get; } = "bool";

		public override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

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
				throw new EvaluationTypeException($"Cannot cast value of type {GetDataTypeName(a)} to boolean.");
			}
		}

		protected override FunctionNode Empty() {
			return new BoolCastNode();
		}
	}

	public class StringCastNode : SingleArgFunctionNode {
		//public override EvaluationType ReturnType { get; } = EvaluationType.STRING;
		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				if (argType.IsReal() || argType == EvaluationType.BOOL || argType == EvaluationType.STRING || argType.IsEnum) {
					return EvaluationType.STRING;
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to string.");
				}
			}
		}

		public sealed override string Name { get; } = "str";

		public override object Evaluate(IEnvironment environment) {
			EvaluationType argType = Argument.ReturnType;
			object? a = Argument.Evaluate(environment);

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

		protected override FunctionNode Empty() {
			return new StringCastNode();
		}
	}

	public class ColorCreateNode : VariableArgsFunctionNode {
		public override string Name { get; } = "color";

		public override EvaluationType ReturnType {
			get {
				if (Arguments.All(a => a.ReturnType.IsReal())) {
					int count = Arguments.Length;
					if (count == 1 || count == 3 || count == 4) {
						return EvaluationType.COLOR;
					}
					else {
						throw new EvaluationTypeException("Color must take 1, 3, or 4 real-valued arguments.");
					}
				}
				else {
					string s = (Arguments.Length > 1) ? "s" : "";
					throw new EvaluationTypeException($"Cannot create a color from arguments with type{s}: " + string.Join(", ", Arguments.Select(a => a.ReturnType.ToString())));
				}
			}
		}

		public override void SetArgumentCount(int count) {
			if (count == 1 || count == 3 || count == 4) {
				base.SetArgumentCount(count);
			}
			else {
				throw new EvaluationSyntaxException("Color must take 1, 3, or 4 real-valued arguments.");
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object[] args = EvaluationTypes.VerifyArray(Arguments.Select(a => a.Evaluate(environment)).ToArray());

			if (args.Length != 1 && args.Length != 3 && args.Length != 4) {
				throw new EvaluationCalculationException("Color must take 1, 3, or 4 real-valued arguments.");
			}

			// TODO Should be able to provide a color name somehow. Should it be in this function, or is that too much overloading?

			bool badTypes = false;
			float[] values = new float[args.Length];

			for (int i = 0; i < args.Length; i++) {
				if (EvaluationTypes.TryGetReal(args[i], out float realVal)) {
					values[i] = realVal.Clamp(0f, 1f);
				}
				else {
					badTypes = true;
					break;
				}
			}

			if (badTypes) {
				Type[] types = args.Select(a => a.GetType()).Distinct().ToArray();
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

		protected override VariableArgsFunctionNode MakeEmptyBase() {
			return new ColorCreateNode();
		}
	}

}
