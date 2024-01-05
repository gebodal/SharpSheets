using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Evaluations.Nodes {

	public class LowerNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				return argType == EvaluationType.STRING ? EvaluationType.STRING : throw new EvaluationTypeException($"Lower not defined for value of type {argType}.");
			}
		}
		public sealed override string Name { get; } = "lower";

		public override object Evaluate(IEnvironment environment) {
			object? a = Arguments[0].Evaluate(environment);

			if (a is string aString) {
				return aString.ToLowerInvariant();
			}
			else {
				throw new EvaluationTypeException($"Lower not defined for value of type {GetDataTypeName(a)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new LowerNode();
		}
	}

	public class UpperNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				return argType == EvaluationType.STRING ? EvaluationType.STRING : throw new EvaluationTypeException($"Upper not defined for value of type {argType}.");
			}
		}
		public sealed override string Name { get; } = "upper";

		public override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

			if (a is string aString) {
				return aString.ToUpper();
			}
			else {
				throw new EvaluationTypeException($"Upper not defined for value of type {GetDataTypeName(a)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new UpperNode();
		}
	}

	public class TitleCaseNode : SingleArgFunctionNode {
		public override EvaluationType ReturnType {
			get {
				EvaluationType argType = Argument.ReturnType;
				return argType == EvaluationType.STRING ? EvaluationType.STRING : throw new EvaluationTypeException($"Title case not defined for value of type {argType}.");
			}
		}
		public sealed override string Name { get; } = "titlecase";

		public override object Evaluate(IEnvironment environment) {
			object? a = Argument.Evaluate(environment);

			if (a is string aString) {
				return aString.ToTitleCase();
			}
			else {
				throw new EvaluationTypeException($"TitleCase not defined for value of type {GetDataTypeName(a)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new TitleCaseNode();
		}
	}

	public class StringJoinNode : FunctionNode {
		public override string Name { get; } = "join";
		public override EvaluationNode[] Arguments { get; } = new EvaluationNode[2];
		public override bool IsConstant { get { return Arguments[0].IsConstant && Arguments[1].IsConstant; } }

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType arg1Type = Arguments[0].ReturnType;
				EvaluationType arg2Type = Arguments[1].ReturnType;
				if (arg1Type == EvaluationType.STRING && (arg2Type.IsArray || arg2Type.IsTuple) && (arg2Type.ElementType.IsReal() || arg2Type.ElementType == EvaluationType.BOOL || arg2Type.ElementType == EvaluationType.STRING)) { // TODO We sure about those ElementType constraints...?
					return EvaluationType.STRING;
				}
				else {
					throw new EvaluationTypeException($"Join not defined for operands of type {arg1Type} and {arg2Type}.");
				}
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? a = Arguments[0].Evaluate(environment);
			object? b = Arguments[1].Evaluate(environment);

			if (a is string separator && b is not null && EvaluationTypes.TryGetArray(b, out Array? values)) {
				try {
					return string.Join(separator, values.Cast<object>().Select(v => v.ToString()));
				}
				catch(InvalidCastException e) {
					throw new EvaluationCalculationException("Error collecting string values for array argument.", e); // This should never happen...
				}
			}
			else {
				throw new EvaluationTypeException($"Join not defined for operands of type {GetDataTypeName(a)} and {GetDataTypeName(b)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new StringJoinNode();
		}
	}

	public class StringSplitNode : FunctionNode {
		public override string Name { get; } = "split";
		public override EvaluationNode[] Arguments { get; } = new EvaluationNode[2];
		public override bool IsConstant { get { return Arguments[0].IsConstant && Arguments[1].IsConstant; } }

		public sealed override EvaluationType ReturnType {
			get {
				EvaluationType arg1Type = Arguments[0].ReturnType;
				EvaluationType arg2Type = Arguments[1].ReturnType;
				if (arg1Type == EvaluationType.STRING && arg2Type == EvaluationType.STRING) {
					return EvaluationType.STRING.MakeArray();
				}
				else {
					throw new EvaluationTypeException($"Split not defined for operands of type {arg1Type} and {arg2Type}.");
				}
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? a = Arguments[0].Evaluate(environment);
			object? b = Arguments[1].Evaluate(environment);

			if (a is string text && b is string delimiter) {
				return text.Split(delimiter);
			}
			else {
				throw new EvaluationTypeException($"String not defined for operands of type {GetDataTypeName(a)} and {GetDataTypeName(b)}.");
			}
		}

		protected override FunctionNode Empty() {
			return new StringSplitNode();
		}
	}

	public class StringFormatNode : VariableArgsFunctionNode {
		public override string Name { get; } = "format";

		public override EvaluationType ReturnType {
			get {
				EvaluationType[] returnTypes = Arguments.Select(a => a.ReturnType).ToArray();
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
		}

		public override void SetArgumentCount(int count) {
			if (count < 2) {
				throw new EvaluationSyntaxException("Format must have a string format argument and at least one content argument.");
			}
			base.SetArgumentCount(count);
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

		public override object Evaluate(IEnvironment environment) {
			object? a = Arguments[0].Evaluate(environment);
			object?[] contents = new object[Operands - 1];
			for (int i = 0; i < contents.Length; i++) {
				contents[i] = Arguments[i + 1].Evaluate(environment);
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
				throw new EvaluationTypeException($"Format must be a string, not {GetDataTypeName(a)}.");
			}
		}

		protected override VariableArgsFunctionNode MakeEmptyBase() {
			return new StringFormatNode();
		}
	}

}
