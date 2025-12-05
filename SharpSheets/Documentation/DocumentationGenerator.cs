using SharpSheets.Shapes;

namespace SharpSheets.Documentation {

	public static class DocumentationGenerator {

		public static IEnumerable<ArgumentDetails> GetAreaShapeArguments(string parameterName, string prefix, Type argumentType, string? description, bool isOptional, bool useLocal, bool includeNameArg) {
			string name = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
			DocumentationString? styleDescription = description is not null ? new DocumentationString(description) : null;

			string? styleDefaultValue = ShapeFactory.GetDefaultStyle(argumentType)?.Name;

			IShape? exampleValue = ShapeFactory.GetDefaultShape(argumentType);

			yield return new ArgumentDetails(name, styleDescription, new ArgumentType(DisplayType.Create(argumentType), argumentType), isOptional, useLocal, styleDefaultValue, exampleValue, "style");

			if (argumentType == typeof(IContainerShape)) {
				if (includeNameArg) {
					yield return new ArgumentDetails("name", new DocumentationString("Text to use for shape titles."), ArgumentType.Simple<string>(), true, true, "NAME", "NAME", null);
				}

				string titleStyleDefaultValue = ShapeFactory.GetDefaultStyle(typeof(ITitleStyledBox))!.Name;
				ITitleStyledBox exampleTitleStyle = (ITitleStyledBox)ShapeFactory.GetDefaultShape(typeof(ITitleStyledBox))!;
				yield return new ArgumentDetails("title", new DocumentationString($"Title style to be used with {name} if a name is provided."), ArgumentType.Simple<ITitleStyledBox>(), true, false, titleStyleDefaultValue, exampleTitleStyle, "style").Prefixed(name);
			}
		}

		public static IEnumerable<ArgumentDetails> GetDetailArguments(string parameterName, string prefix, Type argumentType, string? description, bool isOptional, bool useLocal) {
			string name = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
			DocumentationString? styleDescription = description is not null ? new DocumentationString(description) : null;
			string? styleDefaultValue = ShapeFactory.GetDefaultStyle(argumentType)?.Name;
			IDetail exampleDetail = (IDetail)ShapeFactory.GetDefaultShape(typeof(IDetail))!;
			yield return new ArgumentDetails(name, styleDescription, new ArgumentType(DisplayType.Create(argumentType), argumentType), isOptional, useLocal, styleDefaultValue, exampleDetail, "style");
		}

	}

}
