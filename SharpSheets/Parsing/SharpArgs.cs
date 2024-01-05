using SharpSheets.Canvas.Text;

namespace SharpSheets.Parsing {

	public class ParagraphIndentArg : ISharpArgsGrouping {
		public readonly ParagraphIndent Indent;

		public ParagraphIndentArg(float indent = 0f, float hanging = 0f) {
			this.Indent = new ParagraphIndent(indent, hanging);
		}
	}

}
