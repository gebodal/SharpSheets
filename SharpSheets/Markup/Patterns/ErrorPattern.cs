using SharpSheets.Evaluations;
using SharpSheets.Exceptions;
using SharpSheets.Markup.Elements;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System;

namespace SharpSheets.Markup.Patterns {

	public class ErrorPattern : MarkupPattern {

		public override bool ValidPattern { get { return false; } }

		private readonly Exception error;

		public ErrorPattern(string? library, string name, Exception error, Layouts.Rectangle? exampleSize, Layouts.Size? exampleCanvas, FilePath source)
			: base(
				  library, name, null,
				  Array.Empty<IMarkupArgument>(),
				  Array.Empty<MarkupValidation>(),
				  exampleSize, exampleCanvas,
				  new DivElement(null, new DivSetup(source), VariableBoxes.Empty, Enumerable.Empty<MarkupVariable>()),
				  source
				  ) {
			
			this.error = error;
		}

		public override MarkupConstructorDetails GetConstructorDetails() {
			return new MarkupConstructorDetails(this, typeof(MarkupPattern), typeof(ErrorPattern), Array.Empty<Documentation.ArgumentDetails>(), new Documentation.DocumentationString("Invalid markup pattern."));
		}

		public override object MakeExample(WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool diagnostic, out SharpParsingException[] buildErrors) {
			string message;
			if (Name != null) {
				message = $"Error processing {FullName}.";
			}
			else {
				message = "Error processing MarkupPattern.";
			}
			buildErrors = Array.Empty<SharpParsingException>();
			return new ErrorWidget(message, error, WidgetSetup.ErrorSetup);
		}
	}

}
