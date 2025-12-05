using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Markup.Canvas;
using SharpSheets.Markup.Elements;
using SharpSheets.Markup.Patterns;
using SharpSheets.Utilities;

namespace SharpSheets.Markup.Parsing {

	public static class MarkupDocumentation {

		public static readonly BuilderDetails DivBuilder;
		public static readonly BuilderDetails PatternBuilder;

		public static readonly ITypeDetailsCollection MarkupBuilders;

		static MarkupDocumentation() {

			MarkupEvaluationContext markupContext = new MarkupEvaluationContext(MarkupEvaluationTypes.BaseContext);

			DivBuilder = BuilderDocs.SharpSheets_Markup_Elements_DivElement;

			PatternBuilder = new BuilderDetails(
				DisplayType.FromSystem<MarkupPattern>(), DisplayType.FromSystem<MarkupPattern>(),
				"pattern", "pattern",
				new ArgumentDetails[] {
					new ArgumentDetails("type", new DocumentationString("The type for this pattern. This determines what kind of object is represented by this pattern, and determines where it will be made available within configuration files."), ArgumentType.Simple<MarkupPatternType>(), false, true, null, MarkupPatternType.BOX, null), // TODO Box example value is highly misleading here
					new ArgumentDetails("name", new DocumentationString("The name for this pattern. This is the identifier that will be used to specify this pattern in configuration files. Ideally it should be unique, but this is not a requirement."), ArgumentType.Simple<string>(), false, true, null, null, null),
					new ArgumentDetails("example-size", new DocumentationString(new TextSpan("The size (and optionally position) for the example pattern (the position will be ignored unless "), new ParameterSpan("example-canvas"), new TextSpan(" is also specified) displayed in the designer and documentation.")), ArgumentType.Simple<Rectangle>(), true, true, null, null, null),
					new ArgumentDetails("example-canvas", new DocumentationString("The page size that the example pattern will be drawn on in the designer and documentation."), ArgumentType.Simple<Size>(), true, true, null, null, null)
				}.Concat(DivBuilder.Arguments).ToArray(),
				new DocumentationString(new TextSpan("This is the base element for a Markup pattern. Every pattern " +
					"must have a "), new TypeSpan("pattern", typeof(MarkupPattern)), new TextSpan(" element as its root. " +
					"This element may also be the root element of a Markup file. If not, it must be a direct child of a "),
					new TypeSpan("patternLibrary", typeof(IMarkupElement)) ,new TextSpan(" element. This element also acts as the first "),
					new TypeSpan("div", typeof(DivElement)), new TextSpan(" element for the pattern, and so " +
					"accepts all the arguments that a "), new TypeSpan("div", typeof(DivElement)), new TextSpan(" accepts.")),
				null, null
				);

			BuilderDetails libraryBuilder = new BuilderDetails(
				DisplayType.FromSystem<IMarkupElement>(), DisplayType.FromSystem<IMarkupElement>(),
				"patternLibrary", "patternLibrary",
				new ArgumentDetails[] {
					new ArgumentDetails("name", new DocumentationString("The name for this pattern library, which can be used to distinguish child patterns in configuration files (and in the documentation). The Markup file name will be used as a default value."), ArgumentType.Simple<string>(), true, true, null, null, null),
				},
				new DocumentationString(new TextSpan("This element is a container for other pattern elements. It can be used " +
				"as the root of a Markup document, and should only have other "), new TypeSpan("patternLibrary", typeof(IMarkupElement)),
				new TextSpan(" and "), new TypeSpan("pattern", typeof(IMarkupElement)), new TextSpan(" elements as its children. It can " +
				"be provided with a name, which will be used to group patterns, and distinguish patterns with the same name.")),
				null, null
				);

			BuilderDetails useBuilder = new BuilderDetails(
				DisplayType.FromSystem<IDrawableElement>(), DisplayType.FromSystem<IDrawableElement>(),
				"use", "use",
				new ArgumentDetails[] {
					new ArgumentDetails("href", new DocumentationString("A reference to the drawable element to duplicate here."), ArgumentType.Simple<IDrawableElement>(), true, true, null, null, null),
					new ArgumentDetails("x", new DocumentationString("The x-coordinate at which to draw the duplicate element."), ArgumentType.Simple<XLengthExpression>(), true, true, markupContext.ZeroWidthExpression, null, null),
					new ArgumentDetails("y", new DocumentationString("The y-coordinate at which to draw the duplicate element."), ArgumentType.Simple<YLengthExpression>(), true, true, markupContext.ZeroHeightExpression, null, null),
					new ArgumentDetails("width", new DocumentationString(new TextSpan("The width to use for the duplicate element. This will only be used if the referenced element is a "), new TypeSpan("symbol", typeof(Symbol)), new TextSpan(" element.")), ArgumentType.Simple<XLengthExpression>(), true, true, markupContext.ZeroWidthExpression, null, null),
					new ArgumentDetails("height", new DocumentationString(new TextSpan("The height to use for the duplicate element. This will only be used if the referenced element is a "), new TypeSpan("symbol", typeof(Symbol)), new TextSpan(" element.")), ArgumentType.Simple<YLengthExpression>(), true, true, markupContext.ZeroHeightExpression, null, null),
				}.Concat(BuilderDocs.SharpSheets_Markup_Elements_StyleSheet.Arguments).ToArray(),
				new DocumentationString(new TextSpan("This element duplicates another drawable element at a specified location. " +
				"If the duplicated element is a "), new TypeSpan("symbol", typeof(Symbol)), new TextSpan(" element, then a new " +
				"width and height may be specified for it. The effect of this element is essentially the same as if the referenced " +
				"element was cloned into the location of this element in the Markup document.")),
				null, null
				); // "width" and "height" ignored unless "href" is a <symbol>

			BuilderDetails optionBuilder = new BuilderDetails(
				DisplayType.FromSystem<IMarkupElement>(), DisplayType.FromSystem<IMarkupElement>(),
				"option", "option",
				new ArgumentDetails[] {
					new ArgumentDetails("name", new DocumentationString("The name for this option, which will be used as the enumeration name by the user."), ArgumentType.Simple<string>(), false, true, null, null, null),
					new ArgumentDetails("desc", new DocumentationString("A description of this option, to be displayed to the user as hints or documentation."), ArgumentType.Simple<string>(), true, true, null, null, null)
				},
				new DocumentationString(new TextSpan("This element represents an option for a custom enumeration Markup argument. " +
				"This element should only be a child of "), new TypeSpan("arg", typeof(MarkupSingleArgument)), new TextSpan(" elements.")), 
				null, null
				);

			BuilderDetails argsBuilder = new BuilderDetails(
				DisplayType.FromSystem<IMarkupElement>(), DisplayType.FromSystem<IMarkupElement>(),
				"args", "args",
				Array.Empty<ArgumentDetails>(),
				new DocumentationString(
					new TextSpan("This element is a holder for other argument elements (e.g. "),
					new TypeSpan("arg", typeof(MarkupSingleArgument)),
					new TextSpan("). It is only allowed as a child of "),
					new TypeSpan("pattern", typeof(MarkupPattern)),
					new TextSpan(" or "),
					new TypeSpan("div", typeof(DivElement)),
					new TextSpan(" elements.")),
				null, null
				);

			BuilderDetails defsBuilder = new BuilderDetails(
				DisplayType.FromSystem<IMarkupElement>(), DisplayType.FromSystem<IMarkupElement>(),
				"defs", "defs",
				Array.Empty<ArgumentDetails>(),
				new DocumentationString("This element is a holder for other graphical elements which " +
					"you do not wish to draw, but may reference elsewhere in the pattern."),
				null, null
				);

			BuilderDetails descBuilder = new BuilderDetails(
				DisplayType.FromSystem<IMarkupElement>(), DisplayType.FromSystem<IMarkupElement>(),
				"desc", "desc",
				Array.Empty<ArgumentDetails>(),
				new DocumentationString(new TextSpan("This element is used to provide a description for a pattern. " +
					"It is only allowed as a direct child of a "), new TypeSpan("pattern", typeof(MarkupPattern)),
					new TextSpan(" element. The text contents of this element will be used as the pattern description " +
					"in documentation and other user-facing displays (such as tooltips).")),
				null, null
				);

			BuilderDetails stopBuilder = new BuilderDetails(
				DisplayType.FromSystem<IMarkupElement>(), DisplayType.FromSystem<IMarkupElement>(),
				"stop", "stop",
				new ArgumentDetails[] {
					new ArgumentDetails("offset", new DocumentationString("The location of this stop in the gradient, expressed as a percentage (e.g. \"50%\") or float value (in the range 0-1)."), ArgumentType.Simple<FloatExpression>(), true, true, new FloatExpression(0f, markupContext.TypeSystem), null, null),
					new ArgumentDetails("stop-color", new DocumentationString(new TextSpan("The color for this gradient stop, indicating the color value at the specified "), new ParameterSpan("offset"), new TextSpan(" in the gradient.")), ArgumentType.Simple<ColorExpression>(), true, true, new ColorExpression(Colors.Color.Black, markupContext.TypeSystem), null, null)
				},
				new DocumentationString("This element defines a color and its position in a gradient."),
				null, null
				);

			List<BuilderDetails> markupBuilders = new List<BuilderDetails>() {
				// Document Level Elements
				PatternBuilder,
				libraryBuilder,
				// Structural Elements
				DivBuilder,
				BuilderDocs.SharpSheets_Markup_Elements_AreaElement, // GetBuilderDetails(typeof(AreaElement), "area"),
				BuilderDocs.SharpSheets_Markup_Elements_DiagnosticElement, // GetBuilderDetails(typeof(DiagnosticElement), "diagnostic"),
				BuilderDocs.SharpSheets_Markup_Elements_SlicingValuesElement, // GetBuilderDetails(typeof(SlicingValuesElement), "slicing"),
				// Child Div Element
				BuilderDocs.SharpSheets_Markup_Elements_ChildDivElement, // GetBuilderDetails(typeof(ChildDivElement), "child"),
				// Styled Div Elements
				BuilderDocs.SharpSheets_Markup_Elements_BoxStyledDivElement, // GetBuilderDetails(typeof(BoxStyledDivElement), "box"),
				BuilderDocs.SharpSheets_Markup_Elements_LabelledBoxStyledDivElement, // GetBuilderDetails(typeof(LabelledBoxStyledDivElement), "labelledBox"),
				BuilderDocs.SharpSheets_Markup_Elements_TitledBoxStyledDivElement, // GetBuilderDetails(typeof(TitledBoxStyledDivElement), "titledBox"),
				BuilderDocs.SharpSheets_Markup_Elements_EntriedShapeStyledDivElement, // GetBuilderDetails(typeof(EntriedShapeStyledDivElement), "entried"),
				BuilderDocs.SharpSheets_Markup_Elements_BarStyledDivElement, // GetBuilderDetails(typeof(BarStyledDivElement), "bar"),
				BuilderDocs.SharpSheets_Markup_Elements_LabelledUsageBarStyledDivElement, // GetBuilderDetails(typeof(LabelledUsageBarStyledDivElement), "usageBar"),
				BuilderDocs.SharpSheets_Markup_Elements_DetailStyledDivElement, // GetBuilderDetails(typeof(DetailStyledDivElement), "detail"),
				// Graphics Elements
				BuilderDocs.SharpSheets_Markup_Elements_Line, // GetBuilderDetails(typeof(Line), "line"),
				BuilderDocs.SharpSheets_Markup_Elements_Rect, // GetBuilderDetails(typeof(Rect), "rect"),
				BuilderDocs.SharpSheets_Markup_Elements_Circle, // GetBuilderDetails(typeof(Elements.Circle), "circle"),
				BuilderDocs.SharpSheets_Markup_Elements_Ellipse, // GetBuilderDetails(typeof(Elements.Ellipse), "ellipse"),
				BuilderDocs.SharpSheets_Markup_Elements_Polyline, // GetBuilderDetails(typeof(Polyline), "polyline"),
				BuilderDocs.SharpSheets_Markup_Elements_Polygon, // GetBuilderDetails(typeof(Polygon), "polygon"),
				BuilderDocs.SharpSheets_Markup_Elements_Path, // GetBuilderDetails(typeof(Elements.Path), "path"),
				BuilderDocs.SharpSheets_Markup_Elements_Grouping, // GetBuilderDetails(typeof(Grouping), "g"),
				BuilderDocs.SharpSheets_Markup_Elements_Symbol, // GetBuilderDetails(typeof(Symbol), "symbol"),
				BuilderDocs.SharpSheets_Markup_Elements_Text, // GetBuilderDetails(typeof(Elements.Text), "text"),
				BuilderDocs.SharpSheets_Markup_Elements_TextPath, // GetBuilderDetails(typeof(TextPath), "textPath"),
				BuilderDocs.SharpSheets_Markup_Elements_TextRect, // GetBuilderDetails(typeof(TextRect), "textRect"),
				BuilderDocs.SharpSheets_Markup_Elements_TSpan, // GetBuilderDetails(typeof(TSpan), "tspan"),
				BuilderDocs.SharpSheets_Markup_Elements_Image, // GetBuilderDetails(typeof(Image), "image"),
				BuilderDocs.SharpSheets_Markup_Elements_ClipPath, // GetBuilderDetails(typeof(ClipPath), "clipPath"),
				useBuilder,
				// Paint Elements
				BuilderDocs.SharpSheets_Markup_Elements_SolidPaint, // GetBuilderDetails(typeof(SolidPaint), "solidPaint"),
				BuilderDocs.SharpSheets_Markup_Elements_LinearGradient, // GetBuilderDetails(typeof(LinearGradient), "linearGradient"),
				BuilderDocs.SharpSheets_Markup_Elements_RadialGradient, // GetBuilderDetails(typeof(RadialGradient), "radialGradient"),
				stopBuilder,
				// Field Elements
				BuilderDocs.SharpSheets_Markup_Elements_TextField, // GetBuilderDetails(typeof(TextField), "textField"),
				BuilderDocs.SharpSheets_Markup_Elements_CheckField, // GetBuilderDetails(typeof(CheckField), "checkField"),
				BuilderDocs.SharpSheets_Markup_Elements_ImageField, // GetBuilderDetails(typeof(ImageField), "imageField"),
				// Argument elements
				BuilderDocs.SharpSheets_Markup_Patterns_MarkupSingleArgument, // GetBuilderDetails(typeof(MarkupSingleArgument), "arg"),
				BuilderDocs.SharpSheets_Markup_Patterns_MarkupGroupArgument, // GetBuilderDetails(typeof(MarkupGroupArgument), "grouparg"),
				BuilderDocs.SharpSheets_Markup_Elements_MarkupVariable, // GetBuilderDetails(typeof(MarkupVariable), "var"),
				BuilderDocs.SharpSheets_Markup_Patterns_MarkupValidation, // GetBuilderDetails(typeof(MarkupValidation), "validation"),
				optionBuilder,
				// Placeholder Elements
				argsBuilder,
				defsBuilder,
				descBuilder
			};

			// TODO <slicing> (does this even work?)

			MarkupBuilders = new TypeDetailsCollection(markupBuilders, StringComparer.Ordinal); // Ordinal better than InvariantCulture?
		}

	}

}
