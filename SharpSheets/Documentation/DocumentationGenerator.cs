using SharpSheets.Shapes;

namespace SharpSheets.Documentation {

	public static class DocumentationGenerator {

		public static IEnumerable<ArgumentDetails> GetShapeArguments(string parameterName, string prefix, Type argumentType, string? description, bool isOptional, bool useLocal) {
			string name = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
			DocumentationString? styleDescription = description is not null ? new DocumentationString(description) : null;

			string? styleDefaultValue = ShapeFactory.GetDefaultStyle(argumentType)?.Name;

			IShape? exampleValue = ShapeFactory.GetDefaultShape(argumentType);

			yield return new ArgumentDetails(name, styleDescription, new ArgumentType(DisplayType.Create(argumentType), argumentType), isOptional, useLocal, styleDefaultValue, exampleValue, "style");
		}

	}

}
