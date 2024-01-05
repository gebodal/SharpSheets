using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Markup.Canvas;
using SharpSheets.Markup.Elements;
using SharpSheets.Markup.Patterns;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace SharpSheets.Markup.Parsing {

	public static class MarkupDocumentation {

		public static readonly ConstructorDetails DivConstructor;
		public static readonly ConstructorDetails PatternConstructor;

		public static readonly ITypeDetailsCollection MarkupConstructors;

		// DivSetup
		private static readonly ConstructorInfo divSetupConstructorInfo;
		private static readonly ConstructorDoc divSetupConstructorDoc;
		// StyleSheet
		private static readonly ConstructorInfo styleSheetConstructorInfo;
		private static readonly ConstructorDoc styleSheetConstructorDoc;
		// PositionExpression
		private static readonly ConstructorInfo positionExpressionConstructorInfo;
		private static readonly ConstructorDoc positionExpressionConstructorDoc;
		// LabelDetailsExpression
		private static readonly ConstructorInfo labelDetailsExpressionConstructorInfo;
		private static readonly ConstructorDoc labelDetailsExpressionConstructorDoc;
		// RectangleExpression
		private static readonly ConstructorInfo rectangleExpressionConstructorInfo;
		private static readonly ConstructorDoc rectangleExpressionConstructorDoc;

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="SystemException"></exception>
		/// <exception cref="TargetInvocationException"></exception>
		static MarkupDocumentation() {

			// Initialize Constructor Infos
			divSetupConstructorInfo = typeof(DivSetup).GetConstructors().First();
			divSetupConstructorDoc = SharpDocumentation.GetConstructorDoc(divSetupConstructorInfo) ?? throw new InvalidOperationException($"Cannot access {nameof(DivSetup)} documentation.");
			
			styleSheetConstructorInfo = typeof(StyleSheet).GetConstructors().First(c => c.GetParameters().Length > 0);
			styleSheetConstructorDoc = SharpDocumentation.GetConstructorDoc(styleSheetConstructorInfo) ?? throw new InvalidOperationException($"Cannot access {nameof(StyleSheet)} documentation.");

			positionExpressionConstructorInfo = typeof(PositionExpression).GetConstructors().First(c => c.GetParameters().Length == 5);
			positionExpressionConstructorDoc = SharpDocumentation.GetConstructorDoc(positionExpressionConstructorInfo) ?? throw new InvalidOperationException($"Cannot access {nameof(PositionExpression)} documentation.");

			labelDetailsExpressionConstructorInfo = typeof(LabelDetailsExpression).GetConstructors().First(c => c.GetParameters().Length == 7);
			labelDetailsExpressionConstructorDoc = SharpDocumentation.GetConstructorDoc(positionExpressionConstructorInfo) ?? throw new InvalidOperationException($"Cannot access {nameof(LabelDetailsExpression)} documentation.");

			rectangleExpressionConstructorInfo = typeof(RectangleExpression).GetConstructors().First(c => c.GetParameters().Length == 4);
			rectangleExpressionConstructorDoc = SharpDocumentation.GetConstructorDoc(rectangleExpressionConstructorInfo) ?? throw new InvalidOperationException($"Cannot access {nameof(RectangleExpression)} documentation.");
			// Constructor Infos Initialized

			DivConstructor = GetConstructorDetails(typeof(DivElement), "div");

			PatternConstructor = new ConstructorDetails(
				typeof(MarkupPattern), typeof(MarkupPattern),
				"pattern", "pattern",
				new ArgumentDetails[] {
					new ArgumentDetails("type", new DocumentationString("The type for this pattern. This determines what kind of object is represented by this pattern, and determines where it will be made available within configuration files."), ArgumentType.Simple(typeof(MarkupPatternType)), false, true, null, MarkupPatternType.BOX, null), // TODO Box example value is highly misleading here
					new ArgumentDetails("name", new DocumentationString("The name for this pattern. This is the identifier that will be used to specify this pattern in configuration files. Ideally it should be unique, but this is not a requirement."), ArgumentType.Simple(typeof(string)), false, true, null, null, null),
					new ArgumentDetails("example-size", new DocumentationString(new TextSpan("The size (and optionally position) for the example pattern (the position will be ignored unless "), new ParameterSpan("example-canvas"), new TextSpan(" is also specified) displayed in the designer and documentation.")), ArgumentType.Simple(typeof(Rectangle)), true, true, null, null, null),
					new ArgumentDetails("example-canvas", new DocumentationString("The page size that the example pattern will be drawn on in the designer and documentation."), ArgumentType.Simple(typeof(Size)), true, true, null, null, null)
				}.Concat(DivConstructor.Arguments).ToArray(),
				new DocumentationString(new TextSpan("This is the base element for a Markup pattern. Every pattern " +
					"must have a "), new TypeSpan("pattern", typeof(MarkupPattern)), new TextSpan(" element as its root. " +
					"This element may also be the root element of a Markup file. If not, it must be a direct child of a "),
					new TypeSpan("patternLibrary", typeof(IMarkupElement)) ,new TextSpan(" element. This element also acts as the first "),
					new TypeSpan("div", typeof(DivElement)), new TextSpan(" element for the pattern, and so " +
					"accepts all the arguments that a "), new TypeSpan("div", typeof(DivElement)), new TextSpan(" accepts.")),
				null, null
				);

			ConstructorDetails libraryConstructor = new ConstructorDetails(
				typeof(IMarkupElement), typeof(IMarkupElement),
				"patternLibrary", "patternLibrary",
				new ArgumentDetails[] {
					new ArgumentDetails("name", new DocumentationString("The name for this pattern library, which can be used to distinguish child patterns in configuration files (and in the documentation). The Markup file name will be used as a default value."), ArgumentType.Simple(typeof(string)), true, true, null, null, null),
				},
				new DocumentationString(new TextSpan("This element is a container for other pattern elements. It can be used " +
				"as the root of a Markup document, and should only have other "), new TypeSpan("patternLibrary", typeof(IMarkupElement)),
				new TextSpan(" and "), new TypeSpan("pattern", typeof(IMarkupElement)), new TextSpan(" elements as its children. It can " +
				"be provided with a name, which will be used to group patterns, and distinguish patterns with the same name.")),
				null, null
				);

			ConstructorDetails useConstructor = new ConstructorDetails(
				typeof(IDrawableElement), typeof(IDrawableElement),
				"use", "use",
				new ArgumentDetails[] {
					new ArgumentDetails("href", new DocumentationString("A reference to the drawable element to duplicate here."), ArgumentType.Simple(typeof(IDrawableElement)), true, true, null, null, null),
					new ArgumentDetails("x", new DocumentationString("The x-coordinate at which to draw the duplicate element."), ArgumentType.Simple(typeof(XLengthExpression)), true, true, MarkupEnvironments.ZeroWidthExpression, null, null),
					new ArgumentDetails("y", new DocumentationString("The y-coordinate at which to draw the duplicate element."), ArgumentType.Simple(typeof(YLengthExpression)), true, true, MarkupEnvironments.ZeroHeightExpression, null, null),
					new ArgumentDetails("width", new DocumentationString(new TextSpan("The width to use for the duplicate element. This will only be used if the referenced element is a "), new TypeSpan("symbol", typeof(Symbol)), new TextSpan(" element.")), ArgumentType.Simple(typeof(XLengthExpression)), true, true, MarkupEnvironments.ZeroWidthExpression, null, null),
					new ArgumentDetails("height", new DocumentationString(new TextSpan("The height to use for the duplicate element. This will only be used if the referenced element is a "), new TypeSpan("symbol", typeof(Symbol)), new TextSpan(" element.")), ArgumentType.Simple(typeof(YLengthExpression)), true, true, MarkupEnvironments.ZeroHeightExpression, null, null),
				}.Concat(GetArgumentDetails(styleSheetConstructorInfo, styleSheetConstructorDoc, true)).ToArray(),
				new DocumentationString(new TextSpan("This element duplicates another drawable element at a specified location. " +
				"If the duplicated element is a "), new TypeSpan("symbol", typeof(Symbol)), new TextSpan(" element, then a new " +
				"width and height may be specified for it. The effect of this element is essentially the same as if the referenced " +
				"element was cloned into the location of this element in the Markup document.")),
				null, null
				); // "width" and "height" ignored unless "href" is a <symbol>

			ConstructorDetails optionConstructor = new ConstructorDetails(
				typeof(IMarkupElement), typeof(IMarkupElement),
				"option", "option",
				new ArgumentDetails[] {
					new ArgumentDetails("name", new DocumentationString("The name for this option, which will be used as the enumeration name by the user."), ArgumentType.Simple(typeof(string)), false, true, null, null, null),
					new ArgumentDetails("desc", new DocumentationString("A description of this option, to be displayed to the user as hints or documentation."), ArgumentType.Simple(typeof(string)), true, true, null, null, null)
				},
				new DocumentationString(new TextSpan("This element represents an option for a custom enumeration Markup argument. " +
				"This element should only be a child of "), new TypeSpan("arg", typeof(MarkupSingleArgument)), new TextSpan(" elements.")), 
				null, null
				);

			ConstructorDetails argsConstructor = new ConstructorDetails(
				typeof(IMarkupElement), typeof(IMarkupElement),
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

			ConstructorDetails defsConstructor = new ConstructorDetails(
				typeof(IMarkupElement), typeof(IMarkupElement),
				"defs", "defs",
				Array.Empty<ArgumentDetails>(),
				new DocumentationString("This element is a holder for other graphical elements which " +
					"you do not wish to draw, but may reference elsewhere in the pattern."),
				null, null
				);

			ConstructorDetails descConstructor = new ConstructorDetails(
				typeof(IMarkupElement), typeof(IMarkupElement),
				"desc", "desc",
				Array.Empty<ArgumentDetails>(),
				new DocumentationString(new TextSpan("This element is used to provide a description for a pattern. " +
					"It is only allowed as a direct child of a "), new TypeSpan("pattern", typeof(MarkupPattern)),
					new TextSpan(" element. The text contents of this element will be used as the pattern description " +
					"in documentation and other user-facing displays (such as tooltips).")),
				null, null
				);

			ConstructorDetails stopConstructor = new ConstructorDetails(
				typeof(IMarkupElement), typeof(IMarkupElement),
				"stop", "stop",
				new ArgumentDetails[] {
					new ArgumentDetails("offset", new DocumentationString("The location of this stop in the gradient, expressed as a percentage (e.g. \"50%\") or float value (in the range 0-1)."), ArgumentType.Simple(typeof(FloatExpression)), true, true, FloatExpression.Zero, null, null),
					new ArgumentDetails("stop-color", new DocumentationString(new TextSpan("The color for this gradient stop, indicating the color value at the specified "), new ParameterSpan("offset"), new TextSpan(" in the gradient.")), ArgumentType.Simple(typeof(ColorExpression)), true, true, new ColorExpression(Colors.Color.Black), null, null)
				},
				new DocumentationString("This element defines a color and its position in a gradient."),
				null, null
				);

			List<ConstructorDetails> markupConstructors = new List<ConstructorDetails>() {
				// Document Level Elements
				PatternConstructor,
				libraryConstructor,
				// Structural Elements
				DivConstructor,
				GetConstructorDetails(typeof(AreaElement), "area"),
				GetConstructorDetails(typeof(DiagnosticElement), "diagnostic"),
				GetConstructorDetails(typeof(SlicingValuesElement), "slicing"),
				// Child Div Element
				GetConstructorDetails(typeof(ChildDivElement), "child"),
				// Styled Div Elements
				GetConstructorDetails(typeof(BoxStyledDivElement), "box"),
				GetConstructorDetails(typeof(LabelledBoxStyledDivElement), "labelledBox"),
				GetConstructorDetails(typeof(TitledBoxStyledDivElement), "titledBox"),
				GetConstructorDetails(typeof(BarStyledDivElement), "bar"),
				GetConstructorDetails(typeof(LabelledUsageBarStyledDivElement), "usageBar"),
				GetConstructorDetails(typeof(DetailStyledDivElement), "detail"),
				// Graphics Elements
				GetConstructorDetails(typeof(Line), "line"),
				GetConstructorDetails(typeof(Rect), "rect"),
				GetConstructorDetails(typeof(Elements.Circle), "circle"),
				GetConstructorDetails(typeof(Elements.Ellipse), "ellipse"),
				GetConstructorDetails(typeof(Polyline), "polyline"),
				GetConstructorDetails(typeof(Polygon), "polygon"),
				GetConstructorDetails(typeof(Elements.Path), "path"),
				GetConstructorDetails(typeof(Grouping), "g"),
				GetConstructorDetails(typeof(Symbol), "symbol"),
				GetConstructorDetails(typeof(Elements.Text), "text"),
				GetConstructorDetails(typeof(TextPath), "textPath"),
				GetConstructorDetails(typeof(TextRect), "textRect"),
				GetConstructorDetails(typeof(TSpan), "tspan"),
				GetConstructorDetails(typeof(Image), "image"),
				GetConstructorDetails(typeof(ClipPath), "clipPath"),
				useConstructor,
				// Paint Elements
				GetConstructorDetails(typeof(SolidPaint), "solidPaint"),
				GetConstructorDetails(typeof(LinearGradient), "linearGradient"),
				GetConstructorDetails(typeof(RadialGradient), "radialGradient"),
				stopConstructor,
				// Field Elements
				GetConstructorDetails(typeof(TextField), "textField"),
				GetConstructorDetails(typeof(CheckField), "checkField"),
				GetConstructorDetails(typeof(ImageField), "imageField"),
				// Argument elements
				GetConstructorDetails(typeof(MarkupSingleArgument), "arg"),
				GetConstructorDetails(typeof(MarkupGroupArgument), "grouparg"),
				GetConstructorDetails(typeof(MarkupVariable), "var"),
				GetConstructorDetails(typeof(MarkupValidation), "validation"),
				optionConstructor,
				// Placeholder Elements
				argsConstructor,
				defsConstructor,
				descConstructor
			};

			// TODO <slicing> (does this even work?)

			MarkupConstructors = new TypeDetailsCollection(markupConstructors, StringComparer.Ordinal); // Ordinal better than InvariantCulture?
		}

		private static string NormaliseParameterName(string name) {
			return name.Replace("_", "-").Trim('-'); //.ToLowerInvariant();
		}

		private class MarkupDocumentationSpanProcessor : IDocumentationSpanVisitor<IDocumentationSpan> {
			public static readonly MarkupDocumentationSpanProcessor Instance = new MarkupDocumentationSpanProcessor();
			private MarkupDocumentationSpanProcessor() { }

			public IDocumentationSpan Visit(TextSpan span) => span;
			public IDocumentationSpan Visit(LineBreakSpan span) => span;
			public IDocumentationSpan Visit(TypeSpan span) => span;
			public IDocumentationSpan Visit(EnumValueSpan span) => span;

			public IDocumentationSpan Visit(ParameterSpan span) {
				return new ParameterSpan(NormaliseParameterName(span.Parameter));
			}
		}

		[return: NotNullIfNotNull(nameof(description))]
		private static DocumentationString? NormaliseDescription(DocumentationString? description) {
			if(description is null) { return null; }

			return description.Convert(MarkupDocumentationSpanProcessor.Instance);
		}

		private static bool IncludeParameterType(Type argType) {
			return argType != typeof(FilePath) // FilePathExpressions are allowed, but all raw FilePaths are technical details
				&& argType != typeof(IVariableBox)
				&& argType != typeof(ContextExpression)
				&& argType.TryGetGenericTypeDefinition() != typeof(IEnumerable<>);
		}

		/// <summary></summary>
		/// <exception cref="SystemException"></exception>
		/// <exception cref="TargetInvocationException"></exception>
		private static ArgumentDetails[] GetArgumentDetails(ConstructorInfo constructorInfo, ConstructorDoc? constructorDoc, bool forceOptional = false, string? prefix = null) {
			List<ArgumentDetails> arguments = new List<ArgumentDetails>();

			bool addStyleSheetArgs = false;
			bool addDivSetupArgs = false;
			foreach (ParameterInfo parameterInfo in constructorInfo.GetParameters()) {
				if (parameterInfo.Name is null) { continue; }

				ArgumentDoc? argumentDoc = constructorDoc?.GetArgument(parameterInfo.Name);

				if (argumentDoc?.exclude ?? false) {
					continue;
				}
				else if (parameterInfo.ParameterType == typeof(StyleSheet)) {
					addStyleSheetArgs = true;
				}
				else if (parameterInfo.ParameterType == typeof(DivSetup)) {
					addDivSetupArgs = true;
				}
				else if (parameterInfo.ParameterType == typeof(PositionExpression)) {
					arguments.AddRange(GetArgumentDetails(positionExpressionConstructorInfo, positionExpressionConstructorDoc, forceOptional: true));
				}
				else if (parameterInfo.ParameterType == typeof(LabelDetailsExpression)) {
					string detailsPrefix = NormaliseParameterName(parameterInfo.Name) + "-";
					arguments.AddRange(GetArgumentDetails(labelDetailsExpressionConstructorInfo, labelDetailsExpressionConstructorDoc, forceOptional: true, prefix: detailsPrefix));
				}
				/*
				else if (parameterInfo.ParameterType == typeof(RectangleExpression)) {
					arguments.AddRange(GetArgumentDetails(rectangleExpressionConstructorInfo, rectangleExpressionConstructorDoc, true));
				}
				*/
				else if (IncludeParameterType(parameterInfo.ParameterType)) {
					bool useLocal = parameterInfo.Name[0] == '_';
					string argName = (prefix ?? "") + NormaliseParameterName(parameterInfo.Name);

					bool isOptional = forceOptional || parameterInfo.IsOptional || argumentDoc?.defaultValue != null; // Is this OK?

					//object? defaultValue = string.Equals("null", argumentDoc?.defaultValue ?? "", StringComparison.OrdinalIgnoreCase) ? null : argumentDoc?.defaultValue;
					object? defaultValue;
					if (argumentDoc != null && argumentDoc.defaultValue != null) {
						defaultValue = string.Equals("null", argumentDoc.defaultValue, StringComparison.OrdinalIgnoreCase) ? null : argumentDoc.defaultValue;
					}
					else {
						defaultValue = parameterInfo.DefaultValue;
					}

					object? exampleValue = GetExampleValue(parameterInfo);

					ArgumentType argType = ArgumentType.Simple(parameterInfo.ParameterType);

					arguments.Add(new ArgumentDetails(argName, NormaliseDescription(argumentDoc?.description), argType, isOptional, useLocal, defaultValue, exampleValue, null));
				}
			}

			if (addDivSetupArgs) {
				arguments.AddRange(GetArgumentDetails(divSetupConstructorInfo, divSetupConstructorDoc, forceOptional: true));
			}
			if (addStyleSheetArgs) {
				arguments.AddRange(GetArgumentDetails(styleSheetConstructorInfo, styleSheetConstructorDoc, forceOptional: true));
			}

			return arguments.ToArray();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="param"></param>
		/// <returns></returns>
		/// <exception cref="SystemException"></exception>
		/// <exception cref="TargetInvocationException"></exception>
		private static object? GetExampleValue(ParameterInfo param) {
			if (param.ParameterType.IsValueType && (param.DefaultValue == null || param.DefaultValue == System.DBNull.Value)) {
				return Activator.CreateInstance(param.ParameterType);
			}
			else {
				return param.DefaultValue;
			}
		}

		/// <summary></summary>
		/// <exception cref="SystemException"></exception>
		/// <exception cref="TargetInvocationException"></exception>
		private static ConstructorDetails MakeConstructorDetails(string name, ConstructorInfo constructorInfo, Type declaringType, ConstructorDoc? constructorDoc, DocumentationString? typeDescription) {
			return new ConstructorDetails(
				declaringType, declaringType,
				name, name,
				GetArgumentDetails(constructorInfo, constructorDoc),
				NormaliseDescription(typeDescription),
				null, null
				);
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="SystemException"></exception>
		/// <exception cref="TargetInvocationException"></exception>
		private static ConstructorDetails GetConstructorDetails(Type type, string name) {
			ConstructorInfo constructorInfo = type.GetConstructors().FirstOrDefault() ?? throw new ArgumentException($"Could not find {nameof(ConstructorInfo)} for {nameof(type)}.");
			if (constructorInfo.DeclaringType is null) {
				throw new InvalidOperationException($"Cannot get valid {nameof(ConstructorInfo)} for {type.Name}.");
			}

			ConstructorDoc? constructorDoc;
			if (type.TryGetGenericTypeDefinition() is Type genericType && genericType.GetConstructors().FirstOrDefault() is ConstructorInfo genericConstructorInfo) {
				// TODO Is this supposed to be for Expressions...?
				constructorDoc = SharpDocumentation.GetConstructorDoc(genericConstructorInfo);
			}
			else {
				constructorDoc = SharpDocumentation.GetConstructorDoc(constructorInfo);
			}

			DocumentationString? typeDescription = SharpDocumentation.GetTypeDescription(constructorInfo.DeclaringType);

			return MakeConstructorDetails(name, constructorInfo, constructorInfo.DeclaringType, constructorDoc, typeDescription);
		}

	}

}
