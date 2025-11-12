using SharpSheets.Canvas.Text;

namespace SharpSheets.Parsing {

	public class ParagraphIndentArg : ISharpArgsGrouping {
		public readonly ParagraphIndent Indent;

		/// <summary>
		/// Constructor for <see cref="ParagraphIndentArg"/>.
		/// </summary>
		/// <param name="indent">The indentation to use for the first line of a paragraph.</param>
		/// <param name="hanging">The indentation to use for all subsequent lines of a paragraph after the first.</param>
		public ParagraphIndentArg(float indent = 0f, float hanging = 0f) {
			this.Indent = new ParagraphIndent(indent, hanging);
		}

		/// <param name="indent">The indentation to use for the first line of a paragraph.</param>
		/// <param name="hanging">The indentation to use for all subsequent lines of a paragraph after the first.</param>
		[FactoryBuilder(typeof(ParagraphIndentArg))]
		[GroupedArgumentBuilder]
		public static ParagraphIndentArg Build(float indent = 0f, float hanging = 0f) {
			// TODO Does this class still need to exist, with the FactoryBuilder setup? The builder doesn't even have to be in the same class definition
			// 		Wait, maybe it does, because it needs the interface? But that could be replaced with an attribute flag!

			return new ParagraphIndentArg(indent, hanging);
		}

	}

}
