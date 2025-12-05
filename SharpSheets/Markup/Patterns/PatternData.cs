using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using System;
using System.Linq;
using SharpSheets.Markup.Parsing;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using SharpSheets.Shapes;

namespace SharpSheets.Markup.Patterns {

	public static class PatternData {

		public static readonly ArgumentDetails[] WidgetVariables;

		public static readonly ArgumentDetails[] AreaShapeVariables;
		public static readonly ArgumentDetails[] TitledShapeVariables;
		public static readonly ArgumentDetails[] TitleStyleShapeVariables;

		//public static readonly (ArgumentDetails arg, EnvironmentVariableInfo info)[] TitledShapeArgs;

		public static readonly ArgumentDetails[] TitledShapeBuilderArgs;
		public static readonly ArgumentDetails[] TitleStyleBuilderArgs;

		public static readonly ArgumentDetails[] DetailVariables;

		/*
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
		*/

		private static readonly ArgumentDetails aspectArg;
		private static readonly ArgumentDetails shapeArg;
		private static readonly ArgumentDetails nameArg;
		private static readonly ArgumentDetails partsArg;
		private static readonly ArgumentDetails formatArg;
		private static readonly ArgumentDetails fontsizeArg;


		static PatternData() {

			WidgetVariables = new ArgumentDetails[] {
				new ArgumentDetails("gutter", new DocumentationString("Gutter size for this widget."), ArgumentType.Simple<float>(), true, false, 8f, 8f, null),
				new ArgumentDetails("layout", new DocumentationString("Layout for this widgets children."), ArgumentType.Simple<LayoutDirection>(), true, false, LayoutDirection.ROWS, LayoutDirection.ROWS, null),
			};

			aspectArg = new ArgumentDetails("aspect", new DocumentationString("Aspect ratio for this shape."), ArgumentType.Simple<float>(), true, true, -1f, -1f, null);
			shapeArg = new ArgumentDetails("shape", new DocumentationString("eriufvbeirugv."), ArgumentType.Simple<IContainerShape>(), true, false, null, null, null);
			nameArg = new ArgumentDetails("name", new DocumentationString("Name to use for the title of this shape."), ArgumentType.Simple<string>(), true, true, "NAME", "NAME", null);
			partsArg = new ArgumentDetails("parts", new DocumentationString("Parts of the name to use for the title of this shape, split on newlines."), ArgumentType.Simple<string[]>(), true, true, new string[] { "NAME" }, new string[] { "NAME" }, null);
			formatArg = new ArgumentDetails("format", new DocumentationString("Font format to use for the title of this shape."), ArgumentType.Simple<TextFormat>(), true, false, TextFormat.BOLD, TextFormat.BOLD, null);
			fontsizeArg = new ArgumentDetails("fontSize", new DocumentationString("Font size to use for the title of this shape."), ArgumentType.Simple<float>(), true, false, 6f, 6f, null);
			
			ArgumentDetails detailLayoutArg = new ArgumentDetails("layout", new DocumentationString("The current layout of the detail shape."), ArgumentType.Simple<LayoutDirection>(), false, true, LayoutDirection.ROWS, LayoutDirection.ROWS, null);

			AreaShapeVariables = new ArgumentDetails[] { aspectArg };

			TitledShapeVariables = new ArgumentDetails[] {
				aspectArg,
				nameArg,
				partsArg,
				formatArg,
				fontsizeArg
			};

			TitleStyleShapeVariables = new ArgumentDetails[] {
				shapeArg,
				nameArg,
				partsArg,
				formatArg,
				fontsizeArg
			};

			/*
			TitledShapeArgs = new (ArgumentDetails, EnvironmentVariableInfo)[] {
				(formatArg, TitledFormatVariable),
				(fontsizeArg, TitledFontsizeVariable)
			};
			*/

			TitledShapeBuilderArgs = new ArgumentDetails[] {
				aspectArg,
				formatArg,
				fontsizeArg
			};

			TitleStyleBuilderArgs = new ArgumentDetails[] {
				formatArg,
				fontsizeArg
			};

			DetailVariables = new ArgumentDetails[] {
				detailLayoutArg
			};
		}

		// Widget Environment Variables
		public static EnvironmentVariableInfo WidgetGutterVariable(EvaluationContext context) => new EnvironmentVariableInfo("gutter", context.GetType<FloatEvaluationType>(), "Gutter size for this widget.");
		public static EnvironmentVariableInfo WidgetLayoutVariable(EvaluationContext context) => new EnvironmentVariableInfo("layout", context.GetSystemType<LayoutDirection>(), "Layout for this widgets children.");
		// Area Shape Environment Variables
		public static EnvironmentVariableInfo AreaShapeAspectVariable(EvaluationContext context) => new EnvironmentVariableInfo("aspect", context.GetType<FloatEvaluationType>(), "Aspect ratio for this shape.");
		public static (EvaluationValue, EnvironmentVariableInfo) AreaShapeAspectVariable(EvaluationContext context, float aspect) {
			EnvironmentVariableInfo info = AreaShapeAspectVariable(context);
			return (info.EvaluationType.MakeValue(aspect), info);
		}
		// Shape Name Environment Variables
		public static EnvironmentVariableInfo ShapeNameVariable(EvaluationContext context) => new EnvironmentVariableInfo("name", context.GetType<StringEvaluationType>(), "Name to use for the title of this shape.");
		public static (EvaluationValue, EnvironmentVariableInfo) ShapeNameVariable(EvaluationContext context, string name) {
			EnvironmentVariableInfo info = ShapeNameVariable(context);
			return (info.EvaluationType.MakeValue(name), info);
		}
		public static EnvironmentVariableInfo ShapePartsVariable(EvaluationContext context) => new EnvironmentVariableInfo("parts", context.GetType<StringEvaluationType>().MakeArray(), "Parts of the name to use for the title of this shape, split on newlines.");
		public static (EvaluationValue, EnvironmentVariableInfo) ShapePartsVariable(EvaluationContext context, string[] parts) {
			EnvironmentVariableInfo info = ShapePartsVariable(context);
			return (info.EvaluationType.MakeValue(parts), info);
		}
		// Title Shape Environment Variables
		public static EnvironmentVariableInfo TitledFormatVariable(EvaluationContext context) => new EnvironmentVariableInfo("format", context.GetSystemType<TextFormat>(), "Font format to use for the title of this shape.");
		public static EnvironmentVariableInfo TitledFontsizeVariable(EvaluationContext context) => new EnvironmentVariableInfo("fontSize", context.GetType<FloatEvaluationType>(), "Font size to use for the title of this shape.");
		// Title Styled Box Environment Variables
		public static EnvironmentVariableInfo TitleStyledBoxVariable(EvaluationContext context) => new EnvironmentVariableInfo("shape", context.GetSystemType<IContainerShape>(), "The box which this title style is being applied to.");
		public static (EvaluationValue, EnvironmentVariableInfo) TitleStyledBoxVariable(EvaluationContext context, IContainerShape shape) {
			EnvironmentVariableInfo info = TitleStyledBoxVariable(context);
			return (info.EvaluationType.MakeValue(shape), info);
		}
		// Detail Environment Variables
		public static EnvironmentVariableInfo DetailLayoutVariable(EvaluationContext context) => new EnvironmentVariableInfo("layout", context.GetSystemType<LayoutDirection>(), "The current layout of the detail shape.");

		public static (ArgumentDetails arg, EnvironmentVariableInfo info)[] TitledShapeArgs(EvaluationContext context) {
			return new (ArgumentDetails, EnvironmentVariableInfo)[] {
				(formatArg, TitledFormatVariable(context)),
				(fontsizeArg, TitledFontsizeVariable(context))
			};
		}

		private static IVariableBox GetVariableBox(ArgumentDetails[] args, EvaluationContext context) {
			return VariableBoxes.Create(
				args.Select(
					a => new EnvironmentVariableInfo(
						a.Name,
						GetEvalType(a.Type.DataType, context),
						GetDocumentationStringAsText(a.Description)
						)
					),
				context
				);
		}

		private static EvaluationType GetEvalType(Type t, EvaluationContext context) {
			if (t.IsArray) {
				return GetEvalType(t.GetElementType()!, context).MakeArray();
			}
			else {
				return context.TryGetSystemType(t, out EvaluationType? argEvalType) ? argEvalType : throw new InvalidOperationException("Pattern data arguments should all use known system types.");
			}
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
		public static IVariableBox GetPatternVariables<T>(EvaluationContext context) where T : MarkupPattern {
			if (typeof(T) == typeof(MarkupBoxPattern)) {
				return GetVariableBox(AreaShapeVariables, context);
			}
			else if (typeof(T) == typeof(MarkupLabelledBoxPattern)) {
				return GetVariableBox(AreaShapeVariables, context);
			}
			else if (typeof(T) == typeof(MarkupTitleStyledBoxPattern)) {
				return GetVariableBox(TitleStyleShapeVariables, context);
			}
			else if (typeof(T) == typeof(MarkupTitledBoxPattern)) {
				return GetVariableBox(TitledShapeVariables, context);
			}
			else if (typeof(T) == typeof(MarkupEntriedShapePattern)) {
				return GetVariableBox(AreaShapeVariables, context);
			}
			else if (typeof(T) == typeof(MarkupBarPattern)) {
				return GetVariableBox(AreaShapeVariables, context);
			}
			else if (typeof(T) == typeof(MarkupUsageBarPattern)) {
				return GetVariableBox(AreaShapeVariables, context);
			}
			else if (typeof(T) == typeof(MarkupDetailPattern)) {
				return GetVariableBox(DetailVariables, context);
			}
			else if (typeof(T) == typeof(MarkupWidgetPattern)) {
				return GetVariableBox(WidgetVariables, context);
			}
			else {
				throw new NotSupportedException($"Unrecognized pattern type: {nameof(T)}");
			}
		}

	}

}
