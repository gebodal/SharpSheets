using SharpSheets.Exceptions;

namespace SharpSheets.Markup.Canvas {

	public class MarkupCanvasStateException : SharpSheetsException {

		public MarkupCanvasStateException() : base("Invalid Markup canvas state for this operation.") { }
		public MarkupCanvasStateException(string message) : base(message) { }
		public MarkupCanvasStateException(string message, Exception? innerException) : base(message, innerException) { }

	}

}
