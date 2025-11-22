using SharpSheets.Shapes;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Documentation {

	public static class DocumentationGenerator {

		public static IEnumerable<ArgumentDetails> GetAreaShapeArguments(string parameterName, string prefix, Type argumentType, ArgumentDoc? argDoc, bool isOptional, bool useLocal, bool includeNameArg) {
			string name = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
			//string styleName = name + ".style";
			DocumentationString? styleDescription = NormaliseDescription(argDoc?.description);
			//object styleDefaultValue = styleArgDoc?.defaultValue ?? param.DefaultValue;

			string? styleDefaultValue = ShapeFactory.GetDefaultStyle(argumentType)?.Name;

			//Type argumentType = param.ParameterType == typeof(IContainerShape) ? typeof(IBox) : param.ParameterType;

			IShape? exampleValue;
			if (argDoc?.exampleValue is string exampleStyle) {
				exampleValue = ShapeFactory.GetExampleShape(argumentType, exampleStyle);
			}
			else {
				exampleValue = ShapeFactory.GetDefaultShape(argumentType);
			}

			yield return new ArgumentDetails(name, styleDescription, ArgumentType.Simple(argumentType), isOptional, useLocal, styleDefaultValue, exampleValue, "style");
			// TODO What about nested parameters here? (Some AbstractShape types have required arguments?)
			// How about a placeholder argument with an "ImpliedParameters" flag?
			// TODO Implied arguments

			//yield return new ArgumentDetails("aspect", "Aspect ratio for rect shape.", typeof(float), true, true, -1f, null);
			//yield return new ArgumentDetails(name, "Aspect ratio for rect shape.", typeof(float), true, true, -1f, "aspect"); // This should belong to the shape
			if (argumentType == typeof(IContainerShape)) {
				if (includeNameArg) {
					yield return new ArgumentDetails("name", new DocumentationString("Text to use for shape titles."), ArgumentType.Simple(typeof(string)), true, true, "NAME", "NAME", null);
				}

				string titleStyleDefaultValue = ShapeFactory.GetDefaultStyle(typeof(ITitleStyledBox))!.Name;
				ITitleStyledBox exampleTitleStyle = (ITitleStyledBox)ShapeFactory.GetDefaultShape(typeof(ITitleStyledBox))!;
				yield return new ArgumentDetails("title", new DocumentationString($"Title style to be used with {name} if a name is provided."), ArgumentType.Simple(typeof(ITitleStyledBox)), true, false, titleStyleDefaultValue, exampleTitleStyle, "style").Prefixed(name);
			}
		}

		public static IEnumerable<ArgumentDetails> GetDetailArguments(string parameterName, string prefix, Type argumentType, ArgumentDoc? argDoc, bool isOptional, bool useLocal) {
			string name = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
			//string styleName = name + ".style";
			DocumentationString? styleDescription = NormaliseDescription(argDoc?.description);
			//object styleDefaultValue = styleArgDoc?.defaultValue ?? param.DefaultValue;
			string? styleDefaultValue = ShapeFactory.GetDefaultStyle(argumentType)?.Name;
			IDetail exampleDetail = (IDetail)ShapeFactory.GetDefaultShape(typeof(IDetail))!;
			yield return new ArgumentDetails(name, styleDescription, ArgumentType.Simple(argumentType), isOptional, useLocal, styleDefaultValue, exampleDetail, "style");
		}

		private class SharpDocumentationSpanProcessor : IDocumentationSpanVisitor<IDocumentationSpan> {
			public static readonly SharpDocumentationSpanProcessor Instance = new SharpDocumentationSpanProcessor();
			private SharpDocumentationSpanProcessor() { }

			public IDocumentationSpan Visit(TextSpan span) => span;
			public IDocumentationSpan Visit(LineBreakSpan span) => span;
			public IDocumentationSpan Visit(TypeSpan span) => span;
			public IDocumentationSpan Visit(EnumValueSpan span) => span;

			public IDocumentationSpan Visit(ParameterSpan span) {
				return new ParameterSpan(NormaliseParameterName(span.Parameter));
			}

			private static string NormaliseParameterName(string parameter) {
				return parameter.Trim().Trim('_').Replace("_", "-");
			}
		}

		[return: NotNullIfNotNull(nameof(description))]
		private static DocumentationString? NormaliseDescription(DocumentationString? description) {
			if (description is null) { return null; }

			return description.Convert(SharpDocumentationSpanProcessor.Instance);
		}

	}

}
