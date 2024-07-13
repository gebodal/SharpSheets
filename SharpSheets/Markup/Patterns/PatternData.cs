using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using System;
using System.Linq;
using SharpSheets.Markup.Parsing;
using SharpSheets.Widgets;
using SharpSheets.Utilities;

namespace SharpSheets.Markup.Patterns {

	public static class PatternData {

		public static readonly ArgumentDetails[] WidgetVariables;

		public static readonly ArgumentDetails[] AreaShapeVariables;
		public static readonly ArgumentDetails[] TitledShapeVariables;

		public static readonly (ArgumentDetails arg, EnvironmentVariableInfo info)[] TitledShapeArgs;

		public static readonly ArgumentDetails[] TitledShapeConstructorArgs;

		public static readonly ArgumentDetails[] DetailVariables;

		// Widget Environment Variables
		public static readonly EnvironmentVariableInfo WidgetGutterVariable = new EnvironmentVariableInfo("gutter", EvaluationType.FLOAT, "Gutter size for this widget.");
		public static readonly EnvironmentVariableInfo WidgetLayoutVariable = new EnvironmentVariableInfo("layout", MarkupEvaluationTypes.LAYOUT, "Layout for this widgets children.");
		// Area Shape Environment Variables
		public static readonly EnvironmentVariableInfo AreaShapeAspectVariable = new EnvironmentVariableInfo("aspect", EvaluationType.FLOAT, "Aspect ratio for this shape.");
		// Shape Name Environment Variables
		public static readonly EnvironmentVariableInfo ShapeNameVariable = new EnvironmentVariableInfo("name", EvaluationType.STRING, "Name to use for the title of this shape.");
		public static readonly EnvironmentVariableInfo ShapePartsVariable = new EnvironmentVariableInfo("parts", EvaluationType.STRING.MakeArray(), "Parts of the name to use for the title of this shape, split on newlines.");
		// Title Shape Environment Variables
		public static readonly EnvironmentVariableInfo TitledFormatVariable = new EnvironmentVariableInfo("format", MarkupEvaluationTypes.TEXT_FORMAT, "Font format to use for the title of this shape.");
		public static readonly EnvironmentVariableInfo TitledFontsizeVariable = new EnvironmentVariableInfo("fontSize", EvaluationType.FLOAT, "Font size to use for the title of this shape.");
		// Detail Environment Variables
		public static readonly EnvironmentVariableInfo DetailLayoutVariable = new EnvironmentVariableInfo("layout", MarkupEvaluationTypes.LAYOUT, "The current layout of the detail shape.");

		static PatternData() {

			WidgetVariables = new ArgumentDetails[] {
				new ArgumentDetails("gutter", new DocumentationString("Gutter size for this widget."), ArgumentType.Simple(typeof(float)), true, false, 8f, 8f, null),
				new ArgumentDetails("layout", new DocumentationString("Layout for this widgets children."), ArgumentType.Simple(typeof(Layout)), true, false, Layout.ROWS, Layout.ROWS, null),
			};

			ArgumentDetails aspectArg = new ArgumentDetails("aspect", new DocumentationString("Aspect ratio for this shape."), ArgumentType.Simple(typeof(float)), true, true, -1f, -1f, null);
			ArgumentDetails nameArg = new ArgumentDetails("name", new DocumentationString("Name to use for the title of this shape."), ArgumentType.Simple(typeof(string)), true, true, "NAME", "NAME", null);
			ArgumentDetails partsArg = new ArgumentDetails("parts", new DocumentationString("Parts of the name to use for the title of this shape, split on newlines."), ArgumentType.Simple(typeof(string[])), true, true, new string[] { "NAME" }, new string[] { "NAME" }, null);
			ArgumentDetails formatArg = new ArgumentDetails("format", new DocumentationString("Font format to use for the title of this shape."), ArgumentType.Simple(typeof(TextFormat)), true, false, TextFormat.BOLD, TextFormat.BOLD, null);
			ArgumentDetails fontsizeArg = new ArgumentDetails("fontSize", new DocumentationString("Font size to use for the title of this shape."), ArgumentType.Simple(typeof(float)), true, false, 6f, 6f, null);
			
			ArgumentDetails detailLayoutArg = new ArgumentDetails("layout", new DocumentationString("The current layout of the detail shape."), ArgumentType.Simple(typeof(Layout)), false, true, Layout.ROWS, Layout.ROWS, null);

			AreaShapeVariables = new ArgumentDetails[] { aspectArg };

			TitledShapeVariables = new ArgumentDetails[] {
				aspectArg,
				nameArg,
				partsArg,
				formatArg,
				fontsizeArg
			};

			TitledShapeArgs = new (ArgumentDetails, EnvironmentVariableInfo)[] {
				(formatArg, TitledFormatVariable),
				(fontsizeArg, TitledFontsizeVariable)
			};

			TitledShapeConstructorArgs = new ArgumentDetails[] {
				aspectArg,
				formatArg,
				fontsizeArg
			};

			DetailVariables = new ArgumentDetails[] {
				detailLayoutArg
			};
		}

		private static IVariableBox GetVariableBox(ArgumentDetails[] args) {
			return SimpleVariableBoxes.Create(
				args.Select(
					a => new EnvironmentVariableInfo(
						a.Name,
						EvaluationType.FromSystemType(a.Type.DataType),
						GetDocumentationStringAsText(a.Description)
						)
					)
				);
		}

		public static string? GetDocumentationStringAsText(DocumentationString? documentationString) {
			return documentationString is not null ? string.Join("", documentationString.Process(DocumentSpanTextVisitor.Instance)) : null;
		}

		private class DocumentSpanTextVisitor : IDocumentationSpanVisitor<string> {
			public static readonly DocumentSpanTextVisitor Instance = new DocumentSpanTextVisitor();

			public string Visit(TextSpan span) => span.Text;
			public string Visit(LineBreakSpan span) => "\n\n";
			public string Visit(TypeSpan span) => span.Name;
			public string Visit(ParameterSpan span) => $"\"{span.Parameter}\"";
			public string Visit(EnumValueSpan span) => $"{span.Type}.{span.Value}";
		}

		/// <summary></summary>
		/// <exception cref="NotSupportedException">Thrown when <typeparamref name="T"/> is not a valid pattern type.</exception>
		public static IVariableBox GetPatternVariables<T>() where T : MarkupPattern {
			if (typeof(T) == typeof(MarkupBoxPattern)) {
				return GetVariableBox(AreaShapeVariables);
			}
			else if (typeof(T) == typeof(MarkupLabelledBoxPattern)) {
				return GetVariableBox(AreaShapeVariables);
			}
			else if (typeof(T) == typeof(MarkupTitledBoxPattern)) {
				return GetVariableBox(TitledShapeVariables);
			}
			else if (typeof(T) == typeof(MarkupEntriedShapePattern)) {
				return GetVariableBox(AreaShapeVariables);
			}
			else if (typeof(T) == typeof(MarkupBarPattern)) {
				return GetVariableBox(AreaShapeVariables);
			}
			else if (typeof(T) == typeof(MarkupUsageBarPattern)) {
				return GetVariableBox(AreaShapeVariables);
			}
			else if (typeof(T) == typeof(MarkupDetailPattern)) {
				return GetVariableBox(DetailVariables);
			}
			else if (typeof(T) == typeof(MarkupWidgetPattern)) {
				return GetVariableBox(WidgetVariables);
			}
			else {
				throw new NotSupportedException($"Unrecognized pattern type: {nameof(T)}");
			}
		}

	}

}
