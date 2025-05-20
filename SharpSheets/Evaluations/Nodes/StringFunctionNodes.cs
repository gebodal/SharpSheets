using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public abstract class StringConvertFunction : AbstractSingleArgFunction {

		public sealed override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode arg) {
			EvaluationType argType = arg.GetReturnType(typeSystem);
			return StringEvaluationType.IsString(argType) ? EvaluationTypes.STRING : throw new EvaluationTypeException($"{Name} not defined for value of type {argType}.");
		}

		public sealed override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
			EvaluationValue a = arg.Evaluate(environment);

			if (StringEvaluationType.TryGetString(a, out string? aString)) {
				return new EvaluationValue(ConvertString(aString), EvaluationTypes.STRING);
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {a.Type}.");
			}
		}

		protected abstract string ConvertString(string str);
	}

	public class LowerFunction : StringConvertFunction {

		public static readonly LowerFunction Instance = new LowerFunction();
		private LowerFunction() { }

		public override EvaluationName Name { get; } = "lower";
		public override string? Description { get; } = "Convert the string argument to all lowercase.";

		protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("text", EvaluationTypes.STRING, null);
		protected override string? Warning => null;

		protected override string ConvertString(string str) {
			return str.ToLowerInvariant();
		}
	}

	public class UpperFunction : StringConvertFunction {

		public static readonly UpperFunction Instance = new UpperFunction();
		private UpperFunction() { }

		public override EvaluationName Name { get; } = "upper";
		public override string? Description { get; } = "Convert the string argument to all uppercase.";

		protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("text", EvaluationTypes.STRING, null);
		protected override string? Warning => null;

		protected override string ConvertString(string str) {
			return str.ToUpperInvariant();
		}
	}

	public class TitleCaseFunction : StringConvertFunction {

		public static readonly TitleCaseFunction Instance = new TitleCaseFunction();
		private TitleCaseFunction() { }

		public override EvaluationName Name { get; } = "titlecase";
		public override string? Description { get; } = "Convert the string argument to titlecase (lowercase except for first letter of each whitespace-separated word, which are uppercase).";

		protected override EnvironmentFunctionArg Argument { get; } = new EnvironmentFunctionArg("text", EvaluationTypes.STRING, null);
		protected override string? Warning => null;

		protected override string ConvertString(string str) {
			return str.ToTitleCase();
		}
	}

	public class StringJoinFunction : AbstractFunction {

		public static readonly StringJoinFunction Instance = new StringJoinFunction();
		private StringJoinFunction() { }

		public override EvaluationName Name { get; } = "join";
		public override string? Description { get; } = "Join an array of values (converting to strings first if necessary), with the provided separator between each value. The values array must contain real numbers, booleans, or strings.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("separator", EvaluationTypes.STRING, null),
				new EnvironmentFunctionArg("arrayOrTuple", null, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].GetReturnType(typeSystem);
			EvaluationType arg2Type = args[1].GetReturnType(typeSystem);
			if (StringEvaluationType.IsString(arg1Type) && arg2Type.IterationResult() is EvaluationType arg2ElemType && StringEvaluationType.IsString(arg2ElemType)) {
				return EvaluationTypes.STRING;
			}
			else {
				throw new EvaluationTypeException($"Join not defined for operands of type {arg1Type} and {arg2Type}.");
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);
			EvaluationValue b = args[1].Evaluate(environment);

			if (StringEvaluationType.TryGetString(a, out string? separator)) {
				List<string> items = new List<string>();

				foreach(EvaluationValue item in b.Type.Iteration(b) ?? throw new EvaluationTypeException($"Cannot iterate through value of type {b.Type}.")) {
					if(StringEvaluationType.TryGetString(item, out string? itemStr)) {
						items.Add(itemStr);
					}
					else {
						throw new EvaluationTypeException($"Cannot convert item of type {item.Type} to {EvaluationTypes.STRING}.");
					}
				}

				return new EvaluationValue(string.Join(separator, items), EvaluationTypes.STRING);
			}
			else {
				throw new EvaluationTypeException($"Join not defined for operands of type {a.Type} and {b.Type}.");
			}
		}
	}

	public class StringSplitFunction : AbstractFunction {

		public static readonly StringSplitFunction Instance = new StringSplitFunction();
		private StringSplitFunction() { }

		public override EvaluationName Name { get; } = "split";
		public override string? Description { get; } = "Split the text argument on each occurrence of the separator (the separator will not be included in the resulting strings), returning an array of string values.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("text", EvaluationTypes.STRING, null),
				new EnvironmentFunctionArg("separator", EvaluationTypes.STRING, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].GetReturnType(typeSystem);
			EvaluationType arg2Type = args[1].GetReturnType(typeSystem);
			if (StringEvaluationType.IsString(arg1Type) && StringEvaluationType.IsString(arg2Type)) {
				return EvaluationTypes.STRING.MakeArray();
			}
			else {
				throw new EvaluationTypeException($"Split not defined for operands of type {arg1Type} and {arg2Type}.");
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);
			EvaluationValue b = args[1].Evaluate(environment);

			if (StringEvaluationType.TryGetString(a, out string? text) && StringEvaluationType.TryGetString(b, out string? delimiter)) {
				return new EvaluationValue(text.Split(delimiter), EvaluationTypes.STRING);
			}
			else {
				throw new EvaluationTypeException($"String not defined for operands of type {a.Type} and {b.Type}.");
			}
		}
	}

	public class StringFormatFunction : AbstractFunction {

		public static readonly StringFormatFunction Instance = new StringFormatFunction();
		private StringFormatFunction() { }

		public override EvaluationName Name { get; } = "format";
		public override string? Description { get; } = "Returns a copy of the format string where each reference has been replaced with the corresponding content value.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(
			"Format must have a string format argument and at least one content argument.",
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg[] {
					new EnvironmentFunctionArg("format", EvaluationTypes.STRING, null),
					new EnvironmentFunctionArg("content", null, null)
				}, true)
		);

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode[] args) {
			EvaluationType[] returnTypes = args.Select(a => a.GetReturnType(typeSystem)).ToArray();
			if (returnTypes.Length >= 1 && StringEvaluationType.IsString(returnTypes[0])) {
				List<EvaluationType> badTypes = new List<EvaluationType>();
				for (int i = 1; i < returnTypes.Length; i++) {
					if (!(FloatEvaluationType.IsReal(returnTypes[i]) || StringEvaluationType.IsString(returnTypes[i]))) {
						badTypes.Add(returnTypes[i]);
					}
				}
				if (badTypes.Count == 0) {
					return EvaluationTypes.STRING;
				}
				else {
					throw new EvaluationTypeException($"Format can only accept content of types int, float, or string, not: " + string.Join(", ", badTypes.Select(t => t.ToString())));
				}
			}
			else {
				throw new EvaluationTypeException("Format must have a format argument with a string type, not " + (returnTypes.Length > 0 ? returnTypes[0].ToString() : "null") + ".");
			}
		}

		private enum ReplaceState { TEXT, OPEN_BRACE, FORMAT }
		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		private static string ReplaceFormatChars(string format) {
			System.Text.StringBuilder sb = new System.Text.StringBuilder(format.Length);
			ReplaceState state = ReplaceState.TEXT;
			for (int i = 0; i < format.Length; i++) {
				if (state == ReplaceState.TEXT) {
					if (format[i] == '[') {
						state = ReplaceState.OPEN_BRACE;
					}
					else if (format[i] == '{') {
						sb.Append("{{");
					}
					else if (format[i] == '}') {
						sb.Append("}}");
					}
					else {
						sb.Append(format[i]);
					}
				}
				else if (state == ReplaceState.OPEN_BRACE) {
					if (format[i] == '[' || format[i] == ']') {
						state = ReplaceState.TEXT;
					}
					else {
						state = ReplaceState.FORMAT;
						sb.Append('{');
					}
					sb.Append(format[i]);
				}
				else { // state == ReplaceState.FORMAT
					if (format[i] == ']') {
						state = ReplaceState.TEXT;
						sb.Append('}');
					}
					else if (format[i] == '?') {
						sb.Append('#');
					}
					else {
						sb.Append(format[i]);
					}
				}
			}
			if (state != ReplaceState.TEXT) {
				throw new FormatException("Invalid format string.");
			}
			return sb.ToString();
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);
			object?[] contents = new object?[args.Length - 1];
			for (int i = 0; i < contents.Length; i++) {
				EvaluationValue arg = args[i + 1].Evaluate(environment);
				if (IntEvaluationType.TryGetInt(arg, out int argInt)) { contents[i] = argInt; }
				else if (FloatEvaluationType.TryGetFloat(arg, out float argFloat)) { contents[i] = argFloat; }
				else { contents[i] = arg.Value; }
			}

			if (StringEvaluationType.TryGetString(a, out string? format)) {
				try {
					format = ReplaceFormatChars(format);
					return new EvaluationValue(string.Format(format, contents), EvaluationTypes.STRING);
				}
				catch (FormatException e) {
					throw new EvaluationCalculationException("Invalid format string.", e);
				}
			}
			else {
				throw new EvaluationTypeException($"Format must be a string, not {a.Type}.");
			}
		}
	}

	public class StringReplaceFunction : AbstractFunction {

		public static readonly StringReplaceFunction Instance = new StringReplaceFunction();
		private StringReplaceFunction() { }

		public override EvaluationName Name { get; } = "replace";
		public override string? Description { get; } = "Replace any occurrence of a substring within an input string with another value.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("input", EvaluationTypes.STRING, null),
				new EnvironmentFunctionArg("oldValue", EvaluationTypes.STRING, null),
				new EnvironmentFunctionArg("newValue", EvaluationTypes.STRING, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationTypeSystem typeSystem, EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].GetReturnType(typeSystem);
			EvaluationType arg2Type = args[1].GetReturnType(typeSystem);
			EvaluationType arg3Type = args[2].GetReturnType(typeSystem);
			if (StringEvaluationType.IsString(arg1Type) && StringEvaluationType.IsString(arg2Type) && StringEvaluationType.IsString(arg3Type)) {
				return EvaluationTypes.STRING;
			}
			else {
				throw new EvaluationTypeException($"Replace not defined for operands of type {arg1Type}, {arg2Type}, and {arg3Type}.");
			}
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
			EvaluationValue a = args[0].Evaluate(environment);
			EvaluationValue b = args[1].Evaluate(environment);
			EvaluationValue c = args[2].Evaluate(environment);

			if (StringEvaluationType.TryGetString(a, out string? input) && StringEvaluationType.TryGetString(b, out string? oldValue) && StringEvaluationType.TryGetString(c, out string? newValue)) {
				return new EvaluationValue(input.Replace(oldValue, newValue), EvaluationTypes.STRING);
			}
			else {
				throw new EvaluationTypeException($"Replace not defined for operands of type {a.Type}, {b.Type}, and {c.Type}.");
			}
		}
	}

}
