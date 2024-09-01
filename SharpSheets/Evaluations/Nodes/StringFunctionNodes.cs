using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public class LowerFunction : AbstractFunction {

		public static readonly LowerFunction Instance = new LowerFunction();
		private LowerFunction() { }

		public override EvaluationName Name { get; } = "lower";
		public override string? Description { get; } = "Convert the string argument to all lowercase.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("text", EvaluationType.STRING, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			return argType == EvaluationType.STRING ? EvaluationType.STRING : throw new EvaluationTypeException($"{Name} not defined for value of type {argType}.");
		}
		
		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (a is string aString) {
				return aString.ToLowerInvariant();
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {EvaluationUtils.GetDataTypeName(a)}.");
			}
		}
	}

	public class UpperFunction : AbstractFunction {

		public static readonly UpperFunction Instance = new UpperFunction();
		private UpperFunction() { }

		public override EvaluationName Name { get; } = "upper";
		public override string? Description { get; } = "Convert the string argument to all uppercase.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("text", EvaluationType.STRING, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			return argType == EvaluationType.STRING ? EvaluationType.STRING : throw new EvaluationTypeException($"{Name} not defined for value of type {argType}.");
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (a is string aString) {
				return aString.ToUpper();
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {EvaluationUtils.GetDataTypeName(a)}.");
			}
		}
	}

	public class TitleCaseFunction : AbstractFunction {

		public static readonly TitleCaseFunction Instance = new TitleCaseFunction();
		private TitleCaseFunction() { }

		public override EvaluationName Name { get; } = "titlecase";
		public override string? Description { get; } = "Convert the string argument to titlecase (lowercase except for first letter of each whitespace-separated word, which are uppercase).";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(new EnvironmentFunctionArg("text", EvaluationType.STRING, null))
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType argType = args[0].ReturnType;
			return argType == EvaluationType.STRING ? EvaluationType.STRING : throw new EvaluationTypeException($"{Name} not defined for value of type {argType}.");
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);

			if (a is string aString) {
				return aString.ToTitleCase();
			}
			else {
				throw new EvaluationTypeException($"{Name} not defined for value of type {EvaluationUtils.GetDataTypeName(a)}.");
			}
		}
	}

	public class StringJoinFunction : AbstractFunction {

		public static readonly StringJoinFunction Instance = new StringJoinFunction();
		private StringJoinFunction() { }

		public override EvaluationName Name { get; } = "join";
		public override string? Description { get; } = "Join an array of values (converting to strings first if necessary), with the provided separator between each value. The values array must contain real numbers, booleans, or strings.";

		public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
			new EnvironmentFunctionArgList(
				new EnvironmentFunctionArg("separator", EvaluationType.STRING, null),
				new EnvironmentFunctionArg("arrayOrTuple", null, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].ReturnType;
			EvaluationType arg2Type = args[1].ReturnType;
			if (arg1Type == EvaluationType.STRING && (arg2Type.IsArray || arg2Type.IsTuple) && (arg2Type.ElementType.IsReal() || arg2Type.ElementType == EvaluationType.BOOL || arg2Type.ElementType == EvaluationType.STRING)) { // TODO We sure about those ElementType constraints...?
				return EvaluationType.STRING;
			}
			else {
				throw new EvaluationTypeException($"Join not defined for operands of type {arg1Type} and {arg2Type}.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);
			object? b = args[1].Evaluate(environment);

			if (a is string separator && b is not null && EvaluationTypes.TryGetArray(b, out Array? values)) {
				try {
					return string.Join(separator, values.Cast<object>().Select(v => v.ToString()));
				}
				catch(InvalidCastException e) {
					throw new EvaluationCalculationException("Error collecting string values for array argument.", e); // This should never happen...
				}
			}
			else {
				throw new EvaluationTypeException($"Join not defined for operands of type {EvaluationUtils.GetDataTypeName(a)} and {EvaluationUtils.GetDataTypeName(b)}.");
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
				new EnvironmentFunctionArg("text", EvaluationType.STRING, null),
				new EnvironmentFunctionArg("separator", EvaluationType.STRING, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].ReturnType;
			EvaluationType arg2Type = args[1].ReturnType;
			if (arg1Type == EvaluationType.STRING && arg2Type == EvaluationType.STRING) {
				return EvaluationType.STRING.MakeArray();
			}
			else {
				throw new EvaluationTypeException($"Split not defined for operands of type {arg1Type} and {arg2Type}.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);
			object? b = args[1].Evaluate(environment);

			if (a is string text && b is string delimiter) {
				return text.Split(delimiter);
			}
			else {
				throw new EvaluationTypeException($"String not defined for operands of type {EvaluationUtils.GetDataTypeName(a)} and {EvaluationUtils.GetDataTypeName(b)}.");
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
					new EnvironmentFunctionArg("format", EvaluationType.STRING, null),
					new EnvironmentFunctionArg("content", null, null)
				}, true)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType[] returnTypes = args.Select(a => a.ReturnType).ToArray();
			if (returnTypes.Length > 0 && returnTypes[0] == EvaluationType.STRING) {
				List<EvaluationType> badTypes = new List<EvaluationType>();
				for (int i = 1; i < returnTypes.Length; i++) {
					if (!(returnTypes[i].IsReal() || returnTypes[i] == EvaluationType.STRING)) {
						badTypes.Add(returnTypes[i]);
					}
				}
				if (badTypes.Count == 0) {
					return EvaluationType.STRING;
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

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);
			object?[] contents = new object[args.Length - 1];
			for (int i = 0; i < contents.Length; i++) {
				contents[i] = args[i + 1].Evaluate(environment);
				if (contents[i] is uint auint) { contents[i] = (int)auint; }
				else if (contents[i] is UFloat aufloat) { contents[i] = aufloat.Value; }
			}

			if (a is string format) {
				try {
					format = ReplaceFormatChars(format);
					return string.Format(format, contents);
				}
				catch (FormatException e) {
					throw new EvaluationCalculationException("Invalid format string.", e);
				}
			}
			else {
				throw new EvaluationTypeException($"Format must be a string, not {EvaluationUtils.GetDataTypeName(a)}.");
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
				new EnvironmentFunctionArg("input", EvaluationType.STRING, null),
				new EnvironmentFunctionArg("oldValue", EvaluationType.STRING, null),
				new EnvironmentFunctionArg("newValue", EvaluationType.STRING, null)
				)
		);

		public override EvaluationType GetReturnType(EvaluationNode[] args) {
			EvaluationType arg1Type = args[0].ReturnType;
			EvaluationType arg2Type = args[1].ReturnType;
			EvaluationType arg3Type = args[2].ReturnType;
			if (arg1Type == EvaluationType.STRING && arg2Type == EvaluationType.STRING && arg3Type == EvaluationType.STRING) {
				return EvaluationType.STRING;
			}
			else {
				throw new EvaluationTypeException($"Replace not defined for operands of type {arg1Type}, {arg2Type}, and {arg3Type}.");
			}
		}

		public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
			object? a = args[0].Evaluate(environment);
			object? b = args[1].Evaluate(environment);
			object? c = args[2].Evaluate(environment);

			if (a is string input && b is string oldValue && c is string newValue) {
				return input.Replace(oldValue, newValue);
			}
			else {
				throw new EvaluationTypeException($"Replace not defined for operands of type {EvaluationUtils.GetDataTypeName(a)}, {EvaluationUtils.GetDataTypeName(b)}, and {EvaluationUtils.GetDataTypeName(c)}.");
			}
		}
	}

}
