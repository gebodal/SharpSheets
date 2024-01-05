using SharpSheets.Evaluations.Nodes;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpSheets.Evaluations {

	public static class Interpolation {

		private enum ParseState { TEXT, EXPRESSION_START, EXPRESSION, EXPRESSION_STRING, KEY, ESCAPE }

		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="UndefinedVariableException"></exception>
		/// <exception cref="UndefinedFunctionException"></exception>
		public static TextExpression Parse(string text, IVariableBox variables, bool requireEscape) {
			List<InterpolatedStringExpression> parts = new List<InterpolatedStringExpression>();

			Stack<ParseState> stateStack = new Stack<ParseState>();
			stateStack.Push(ParseState.TEXT);

			try {
				int start = 0;
				int lastEnd = 0;
				for (int i = 0; i < text.Length; i++) {
					char c = text[i];

					ParseState state = stateStack.Peek();

					if (state == ParseState.ESCAPE) {
						stateStack.Pop();
					}
					else if (state == ParseState.TEXT) {
						if (c == '\\') {
							stateStack.Push(ParseState.ESCAPE);
						}
						else if (c == '$' || (c == '{' && !requireEscape)) {
							if (i - start > 0) {
								string textPart = text.Substring(start, i - start);
								parts.Add(ProcessText(textPart));
							}
							lastEnd = i;

							if (c == '$') {
								stateStack.Push(ParseState.EXPRESSION_START);
								start = i;
							}
							else if (c == '{') {
								stateStack.Push(ParseState.EXPRESSION);
								start = i + 1;
							}
						}
					}
					else if (state == ParseState.EXPRESSION_START) {
						stateStack.Pop();
						if (c == '{') {
							stateStack.Push(ParseState.EXPRESSION);
							start = i + 1;
						}
						else if (char.IsLetter(c)) {
							stateStack.Push(ParseState.KEY);
							// No change to start position
						}
						else {
							throw new EvaluationSyntaxException($"Error in parsing text for interpolation: empty key at position {i}.");
						}
					}
					else if (state == ParseState.KEY) {
						if (!char.IsLetterOrDigit(c) && c != '.') {
							string keyText = text.Substring(start, i - start);
							parts.Add(ProcessKey(keyText, variables));
							lastEnd = i;

							stateStack.Pop();
							start = i;
						}
					}
					else if (state == ParseState.EXPRESSION) {
						if (c == '"') {
							stateStack.Push(ParseState.EXPRESSION_STRING);
						}
						else if (c == '}') {
							string expressionText = text.Substring(start, i - start);
							parts.Add(ProcessExpression(expressionText, variables));
							lastEnd = i + 1;

							stateStack.Pop();
							start = i + 1;
						}
					}
					else if (state == ParseState.EXPRESSION_STRING) {
						if (c == '\\') {
							stateStack.Push(ParseState.ESCAPE);
						}
						else if (c == '"') {
							stateStack.Pop();
						}
					}
				}

				if (text.Length > lastEnd) {
					if (stateStack.Count > 0 && stateStack.Peek() == ParseState.EXPRESSION_START) {
						throw new EvaluationSyntaxException($"Error in parsing text for interpolation: empty key at position {text.Length - 1}.");
					}
					else if (stateStack.Count > 0 && stateStack.Peek() == ParseState.KEY) {
						string keyText = text.Substring(start, text.Length - start);
						parts.Add(ProcessKey(keyText, variables));
					}
					else {
						string textPart = text.Substring(start, text.Length - lastEnd);
						parts.Add(ProcessText(textPart));
					}
				}

				return new TextExpression(parts.ToArray());
			}
			catch(InvalidOperationException e) {
				throw new EvaluationSyntaxException("Invalid interpolated text expression.", e);
			}
		}

		private static readonly Regex formatRegex = new Regex(@"\:(?<format>[a-z0-9\,\;\%\+\-\'\,\.\?\\\ ]+)$");
		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="UndefinedVariableException"></exception>
		/// <exception cref="UndefinedFunctionException"></exception>
		private static InterpolatedStringExpression ProcessExpression(string expr, IVariableBox variables) {
			Match formatMatch = formatRegex.Match(expr);
			string? format = null;
			if (formatMatch.Success) {
				format = StringParsing.Parse(formatMatch.Groups["format"].Value);
				expr = expr.Substring(0, formatMatch.Index);
			}
			return new InterpolatedStringExpression(Evaluation.Parse(expr, variables), format);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationSyntaxException"></exception>
		/// <exception cref="EvaluationProcessingException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="UndefinedVariableException"></exception>
		/// <exception cref="UndefinedFunctionException"></exception>
		private static InterpolatedStringExpression ProcessKey(string keyText, IVariableBox variables) {
			return new InterpolatedStringExpression(Evaluation.Parse(keyText, variables), null);
		}

		private static InterpolatedStringExpression ProcessText(string str) {
			// TODO What to do about escaped character, etc., for TextExpressions?
			return str;
		}

		public static string Format(string format, object? content) {
			if (content is double || content is float || content is int) {
				format = format.Replace("?", "#");
				// format = string.Join(";", format.Split(';').Select(f => f.Length == 0 ? "**" : f)); // ???
				return string.Format($"{{0:{format}}}", content);
			}
			else {
				return content?.ToString() ?? ""; // Good fallback here? Throw error instead?
			}
		}

	}

	public class InterpolatedStringExpression : IExpression<string> {

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public EvaluationNode Evaluation {
			get {
				if(format != null) {
					return new FormattedStringNode(content, format);
				}
				else {
					return new StringCastNode() { Argument = content }.Simplify();
				}
			}
		}

		private readonly EvaluationNode content;
		private readonly string? format;

		public bool IsConstant => content.IsConstant;

		public InterpolatedStringExpression(EvaluationNode content, string? format) {
			this.content = content;
			this.format = format;
		}

		public static implicit operator InterpolatedStringExpression(string value) {
			return new InterpolatedStringExpression(new ConstantNode(value), null);
		}

		public override string ToString() {
			if (content.IsConstant && string.IsNullOrEmpty(format)) {
				return content.Evaluate(Environments.Empty)?.ToString() ?? "";
			}
			else {
				return "{" + content.ToString() + (format != null ? (":" + format) : "") + "}";
			}
		}

		public string Evaluate(IEnvironment environment) {
			object? content = this.content.Evaluate(environment);

			if (format != null && environment is IInterpolationFormatter formatter) {
				return formatter.Format(format, content); // string.Format(formatProvider, "{0:" + part.Format + "}", content); // Yes...?
			}
			else if (format != null) {
				return Interpolation.Format(format, content);
			}
			else {
				return content?.ToString() ?? ""; // Good fallback?
			}
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return content.GetVariables();
		}
	}

	public class FormattedStringNode : EvaluationNode {
		private readonly EvaluationNode content;
		private readonly string format;

		public override bool IsConstant => content.IsConstant;
		public override EvaluationType ReturnType => EvaluationType.STRING;

		public FormattedStringNode(EvaluationNode content, string format) {
			this.content = content;
			this.format = format;
		}

		public override EvaluationNode Clone() {
			return new FormattedStringNode(content.Clone(), format);
		}

		public override EvaluationNode Simplify() {
			if (format == null) {
				return content.Simplify();
			}
			else if(IsConstant) {
				return new ConstantNode(Evaluate(Environments.Empty));
			}
			else {
				return new FormattedStringNode(content.Simplify(), format);
			}
		}

		public override object Evaluate(IEnvironment environment) {
			object? content = this.content.Evaluate(environment);

			if (format != null && environment is IInterpolationFormatter formatter) {
				return formatter.Format(format, content); // string.Format(formatProvider, "{0:" + part.Format + "}", content); // Yes...?
			}
			else if (format != null) {
				return Interpolation.Format(format, content);
			}
			else {
				return content?.ToString() ?? ""; // Good fallback?
			}
		}

		public override IEnumerable<EvaluationName> GetVariables() => content.GetVariables();

		protected override string GetRepresentation() { throw new NotSupportedException(); }
	}

	public interface IInterpolationFormatter {
		string Format(string format, object? content);
	}

	public class TextExpression : IExpression<string> { // TODO Should this be IExpression<RichString>?
		public EvaluationNode Evaluation {
			get {
				if(value != null) {
					return value;
				}
				else if(parts!.Length == 0) {
					return "";
				}
				else {
					EvaluationNode node = parts[0].Evaluation;
					for(int i=1; i<parts.Length; i++) {
						node += parts[i].Evaluation;
					}
					return node;
				}
			}
		}

		private readonly InterpolatedStringExpression[]? parts;
		private readonly string? value; // TODO RichString?
		private InterpolatedStringExpression[] Parts { get { return (value != null ? ((InterpolatedStringExpression)value).Yield() : parts!).ToArray(); } }

		public bool IsConstant { get { return value != null; } }

		public TextExpression(InterpolatedStringExpression[] parts) {
			if(parts.Length == 0) {
				this.parts = null;
				this.value = "";
			}
			else if (parts.All(p => p.IsConstant)) {
				this.parts = null;
				this.value = string.Join("", parts.Select(p => p.Evaluate(Environments.Empty)));
			}
			else {
				this.parts = parts;
				this.value = null;
			}
		}
		public TextExpression(string value) {
			this.parts = null;
			this.value = value;
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : parts!.SelectMany(p => p.GetVariables()).Distinct();
		}

		public string Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return string.Join("", parts!.Select(p => p.Evaluate(environment)));
			}
		}

		public static TextExpression operator +(TextExpression a, TextExpression b) {
			return new TextExpression(a.Parts.Concat(b.Parts).ToArray());
		}

		public override string ToString() {
			//return "[ " + (value != null ? ("\"" + value + "\"") : string.Join(", ", parts.Select(p => p.ToString()))) + " ]";
			if (value != null) {
				return value;
			}
			else {
				return string.Join("", parts!.Select(p => p.ToString()));
			}
		}
	}

}
