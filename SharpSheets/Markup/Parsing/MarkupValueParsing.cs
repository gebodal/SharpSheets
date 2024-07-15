using SharpSheets.Evaluations;
using SharpSheets.Utilities;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using SharpSheets.Layouts;
using SharpSheets.Markup.Canvas;
using SharpSheets.Evaluations.Nodes;

namespace SharpSheets.Markup.Parsing {

	// All the parsing methods in this class should throw only EvaluationException or FormatException errors
	public static class MarkupValueParsing {

		#region Basic Properties

		private static readonly RegexChunker valueRegex = new RegexChunker(
				new Regex(@"
				\{(?<expression>(?:[^\{\}]|\\[\{\}])+)\}
				|
				(?<number>
					(
						\-?[0-9]+(?:\.[0-9]*)?
						|
						\.[0-9]+
					)
					([eE][\-\+]?[0-9]+)?
				)
			", RegexOptions.IgnorePatternWhitespace),
				new Regex(@"\s*\,\s*|\s+"), true);
		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		private static FloatExpression ParseValue(Match match, IVariableBox variables) {
			if (match.Groups["expression"].Success) {
				return FloatExpression.Parse(match.Groups["expression"].Value.Trim(), variables);
			}
			else {
				return new FloatExpression(float.Parse(match.Groups["number"].Value.Trim()));
			}
		}
		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		public static FloatExpression[] ParseSVGNumbers(string str, IVariableBox variables) {
			return valueRegex.Matches(str.Trim(), s => ParseValue(s, variables)).ToArray();
			//return numbersSplitRegex.Split(str).Select(s => FloatExpression.Parse(s, variables)).ToArray();
			//return str.SplitAndTrim(',', ' ').Select(float.Parse).ToArray();
		}
		public static bool IsSVGNumbers(string str) {
			return valueRegex.IsMatch(str);
		}

		private static readonly Regex percentRegex = new Regex(@"^(?<percent>[\-\+]?[0-9]+(\.[0-9]*)?|\.[0-9]+)\s*\%$");

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		private static float GetPercentValue(Match percentMatch) {
			float percent = float.Parse(percentMatch.Groups["percent"].Value.TrimStart('+')) / 100f;
			return percent;
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		public static FloatExpression ParsePercentage(string text, IVariableBox variables) {
			text = text.Trim();
			Match percentMatch = percentRegex.Match(text);
			if (percentMatch.Success) {
				float percent = GetPercentValue(percentMatch);
				return new FloatExpression(percent);
			}
			else {
				return FloatExpression.Parse(text, variables);
			}
		}

		/*
		public enum Axis { WIDTH, HEIGHT }
		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		public static FloatExpression ParseLength(string text, Axis axis, IVariableBox variables) {
			text = text.Trim();
			Match percentMatch = percentRegex.Match(text);
			if (percentMatch.Success) {
				float percent = GetPercentValue(percentMatch);
				if (axis == Axis.WIDTH && variables.IsVariable("width")) {
					return new FloatExpression(Evaluation.Parse("width", variables) * percent);
				}
				else if (axis == Axis.HEIGHT && variables.IsVariable("height")) {
					return new FloatExpression(Evaluation.Parse("height", variables) * percent);
				}
				else {
					throw new FormatException($"Appropriate length variable not defined for {axis}.");
				}
			}
			else {
				return FloatExpression.Parse(text, variables);
			}
		}
		*/

		public static XLengthExpression ParseXLength(string text, IVariableBox variables) {
			text = text.Trim();
			Match percentMatch = percentRegex.Match(text);
			if (percentMatch.Success) {
				float percent = GetPercentValue(percentMatch);
				if (MarkupEnvironments.IsWidthDefined(variables)) {
					return new XLengthExpression(MarkupEnvironments.WidthNode * percent);
				}
				else {
					throw new FormatException("\"width\" variable is not defined.");
				}
			}
			else {
				return new XLengthExpression(FloatExpression.Parse(text, variables).Evaluation);
			}
		}

		public static YLengthExpression ParseYLength(string text, IVariableBox variables) {
			text = text.Trim();
			Match percentMatch = percentRegex.Match(text);
			if (percentMatch.Success) {
				float percent = GetPercentValue(percentMatch);
				if (MarkupEnvironments.IsHeightDefined(variables)) {
					return new YLengthExpression(MarkupEnvironments.HeightNode * percent);
				}
				else {
					throw new FormatException("\"height\" variable is not defined.");
				}
			}
			else {
				return new YLengthExpression(FloatExpression.Parse(text, variables).Evaluation);
			}
		}

		public static BoundingBoxLengthExpression ParseBoundingBoxLength(string text, IVariableBox variables) {
			text = text.Trim();
			Match percentMatch = percentRegex.Match(text);
			if (percentMatch.Success) {
				float percent = GetPercentValue(percentMatch);
				if (MarkupEnvironments.IsBoundingBoxDefined(variables)) {
					return new BoundingBoxLengthExpression(MarkupEnvironments.BoundingBoxLengthNode * percent);
				}
				else {
					throw new FormatException("The bounding box variables (\"width\", \"height\") are not defined.");
				}
			}
			else {
				return new BoundingBoxLengthExpression(FloatExpression.Parse(text, variables).Evaluation);
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		public static LengthExpression ParsePercentOrLength(string text, IVariableBox variables) {
			text = text.Trim();
			Match percentMatch = percentRegex.Match(text);
			if (percentMatch.Success) {
				float percent = GetPercentValue(percentMatch);
				Length length = Length.FromPercentage(percent);
				return new LengthExpression(length);
			}
			else {
				return new LengthExpression(FloatExpression.Parse(text, variables));
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static ColorExpression ParseColor(string text, IVariableBox variables) {
			if (string.Equals(text, "none")) {
				return Colors.Color.None; // TODO Is this right? (This was "null" at one point, is that better in any way?)
				//return null;
			}
			else {
				return ColorExpression.Parse(text, variables);
			}
		}

		/*
		private static readonly Regex enumRegex = new Regex(@"
				^(?:
					\{(?<expression>(?:[^\{\}]|\\[\{\}])+)\}
					|
					(?<value>.+)
				)$
			", RegexOptions.IgnorePatternWhitespace);
		public static EnumExpression<T> ParseEnum<T>(string str, IVariableBox variables) where T : Enum {
			Match match = enumRegex.Match(str);
			if (match.Groups["expression"].Success) {
				return EnumExpression<T>.Parse(match.Groups["expression"].Value, variables);
			}
			else if (match.Groups["value"].Success) {
				return EnumUtils.ParseEnum<T>(match.Groups["value"].Value);
			}
			else {
				throw new FormatException("Invalid enum expression.");
			}
		}
		*/
		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static EnumExpression<T> ParseEnum<T>(string str, IVariableBox variables) where T : Enum {
			return EnumExpression<T>.Parse(str, variables);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TextExpression ParseText(string text, IVariableBox variables) {
			return Interpolation.Parse(text, variables, false);
		}

		public static bool ParseConcreteBool(string text) {
			return string.Equals(text, "true", StringComparison.InvariantCultureIgnoreCase);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static FilePathExpression ParseFilePath(string text, DirectoryPath source, IVariableBox variables) {
			FilePath concretePath = new FilePath(source.Path, text);
			if (concretePath.Exists) {
				return concretePath;
			}
			else {
				return new FilePathExpression(Evaluation.Parse(text, variables));
			}
		}

		#endregion

		#region Structured Properties

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static Size ParseConcreteSize(string value) {
			float[] parts = value.SplitAndTrim(' ', ',').Select(float.Parse).ToArray();
			if (parts.Length == 2) {
				return new Size(parts[0], parts[1]);
			}
			else {
				throw new FormatException("Size must be defined as two fixed-value numbers.");
			}
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static Rectangle ParseConcreteRectangle(string value) {
			float[] parts = value.SplitAndTrim(' ', ',').Select(float.Parse).ToArray();
			if (parts.Length == 2) {
				return new Rectangle(0f, 0f, parts[0], parts[1]);
			}
			else if (parts.Length == 4) {
				return new Rectangle(parts[0], parts[1], parts[2], parts[3]);
			}
			else {
				throw new FormatException("Rectangle must be defined as four (c, y, width, height) or two (width, height) fixed-value numbers.");
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		public static RectangleExpression ParseRectangle(string value, IVariableBox variables) {
			FloatExpression[] parts = ParseSVGNumbers(value, variables);
			if (parts.Length == 4) {
				return new RectangleExpression(parts[0], parts[1], parts[2], parts[3]);
			}
			else {
				throw new FormatException("Rectangle must be defined as four numbers (x,y,height,width).");
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		public static MarginsExpression ParseMargins(string text, IVariableBox variables) {
			try {
				EvaluationNode node = Evaluation.Parse(text, variables);
				if (node.ReturnType == MarkupEvaluationTypes.MARGINS) {
					return new MarginsExpression(node);
				}
				else if (node.ReturnType == EvaluationType.FLOAT) {
					return new MarginsExpression(new FloatExpression(node));
				}
			}
			catch (EvaluationException) { }

			FloatExpression[] values = ParseSVGNumbers(text, variables);
			if (values.Length == 1) {
				return new MarginsExpression(values[0]);
			}
			else if (values.Length == 2) {
				return new MarginsExpression(values[0], values[1]);
			}
			else if (values.Length == 4) {
				return new MarginsExpression(values[0], values[1], values[2], values[3]);
			}
			else {
				throw new FormatException("Badly formatted margins value, must be 1 (all sides), 2 (vertical, horizontal), or 4 (top, right, bottom, left) values.");
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		public static DimensionExpression ParseDimension(string text, IVariableBox variables) {
			try {
				return Dimension.Parse(text);
			}
			catch (FormatException) { }
			return new DimensionExpression(Evaluation.Parse(text, variables));
		}

		#endregion

		#region Graphics Properties

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		public static DrawPointExpression[] ParseDrawPoints(string value, IVariableBox variables) {

			FloatExpression[] values = ParseSVGNumbers(value, variables); // Regex.Split(value, @"\s*\,\s*|\s+").Select(float.Parse).ToArray();

			if (values.Length % 2 == 0) {
				DrawPointExpression[] points = new DrawPointExpression[values.Length / 2];
				for (int i = 0; i < values.Length; i += 2) {
					points[i / 2] = new DrawPointExpression(values[i], values[i + 1]);
				}
				return points;
			}
			else {
				throw new FormatException("Points must be specified in x,y pairs.");
			}
		}

		private static readonly RegexChunker transformRegex = new RegexChunker(@"
			(?<type>matrix|translate|scale|rotate|skewX|skewY)
			\(
				(?<values>
					(?:(?:\{(?:[^\{\}]|\\[\{\}])+\} | \-?[0-9]+(?:\.[0-9]*)? | \.[0-9]+) (\,|\s)*)+
				)
			\)
			", RegexOptions.IgnorePatternWhitespace, true);

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="FormatException"></exception>
		public static TransformExpression ParseTransform(string text, IVariableBox variables) {

			TransformExpression transform = TransformExpression.Identity();

			foreach (Match match in transformRegex.Matches(text)) {
				TransformExpression next;

				string type = match.Groups["type"].Value;
				FloatExpression[] values = ParseSVGNumbers(match.Groups["values"].Value, variables);

				if (type == "matrix") {
					if (values.Length == 6) {
						next = TransformExpression.Matrix(values[0], values[1], values[2], values[3], values[4], values[5]);
					}
					else {
						throw new FormatException("A transform matrix must have 6 values: \"matrix(<a> <b> <c> <d> <e> <f>)\"");
					}
				}
				else if (type == "translate") {
					FloatExpression x;
					FloatExpression y = 0;
					if (values.Length == 1) {
						x = values[0];
					}
					else if (values.Length == 2) {
						x = values[0];
						y = values[1];
					}
					else {
						throw new FormatException("A translate transform must be of the form \"translate(<x> [<y>])\".");
					}
					next = TransformExpression.Translate(x, y);
				}
				else if (type == "scale") {
					FloatExpression x;
					FloatExpression y;
					if (values.Length == 1) {
						x = values[0];
						y = x;
					}
					else if (values.Length == 2) {
						x = values[0];
						y = values[1];
					}
					else {
						throw new FormatException("A scale transform must be of the form \"scale(<x> [<y>])\".");
					}
					next = TransformExpression.Scale(x, y);
				}
				else if (type == "rotate") {
					FloatExpression a;
					FloatExpression x = 0;
					FloatExpression y = 0;
					if (values.Length == 1) {
						a = values[0];
					}
					else if (values.Length == 3) {
						a = values[0];
						x = values[1];
						y = values[2];
					}
					else {
						throw new FormatException("A rotate transform must be of the form \"rotate(<a> [<x> <y>])\".");
					}
					next = TransformExpression.Rotate(a * ((float)Math.PI / 180f), x, y);
				}
				else if (type == "skewX") {
					if (values.Length == 1) {
						next = TransformExpression.SkewX(values[0] * ((float)Math.PI / 180f));
					}
					else {
						throw new FormatException("A skewX transform must be of the form \"skewX(<a>)\".");
					}
				}
				else if (type == "skewY") {
					if (values.Length == 1) {
						next = TransformExpression.SkewY(values[0] * ((float)Math.PI / 180f));
					}
					else {
						throw new FormatException("A skewY transform must be of the form \"skewY(<a>)\".");
					}
				}
				else {
					throw new FormatException("Badly formatted transform string.");
				}
				transform *= next;
			}

			return transform;
		}

		#endregion

		#region Arrangement Properties

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static PreserveAspectRatioExpression ParsePreserveAspectRatio(string value, IVariableBox variables) {
			// TODO Should this be an expression? Or does that break something?

			if (PreserveAspectRatio.TryParse(value, out PreserveAspectRatio preserveAspectRatio)) {
				return preserveAspectRatio;
			}
			else {
				EvaluationNode node = Evaluation.Parse(value, variables);
				return new PreserveAspectRatioExpression(node);
			}
		}

		#endregion

		#region Pattern Properties

		private static readonly Regex libraryRegex = new Regex(@"^\w+(?:\.\w+)*$", RegexOptions.IgnoreCase);
		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static string? ParseLibraryName(string text) {
			if (string.IsNullOrWhiteSpace(text)) {
				return null;
			}
			else if (libraryRegex.IsMatch(text)) {
				return text;
			}
			else {
				throw new FormatException("Invalid library name. Must be empty or contain only alphanumeric characters groups without whitespace potentially separated by \".\".");
			}
		}

		#endregion

	}

}
