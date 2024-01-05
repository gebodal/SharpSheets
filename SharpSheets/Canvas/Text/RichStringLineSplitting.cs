using SharpSheets.Canvas;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas.Text {

	public static class RichStringLineSplitting {

		private static float GetWidth(float[] widths, int start, int end, float space) {
			float total = 0f;
			int counter = 0;
			for (int i = start; i < end && i < widths.Length; i++) {
				total += widths[i];
				counter++;
			}
			if (counter > 0) { total += space * (counter - 1); }
			return total;
		}

		/// <summary>Splits a single line of <c>RichString</c> (i.e. no newlines) into lines below a fixed width.</summary>
		public static RichString[] SplitLines(ISharpGraphicsData graphicsData, RichString text, float width, float fontSize, float indent, float hangingIndent) {
			RichString[] parts = text.Split(' ');
			float space = graphicsData.GetWidth(new RichString(" ", TextFormat.REGULAR), fontSize);
			float[] widths = new float[parts.Length];
			for (int i = 0; i < parts.Length; i++) {
				widths[i] = graphicsData.GetWidth(parts[i], fontSize);
			}

			List<RichString> lines = new List<RichString>();

			int skip = 0;

			while (parts.Length > skip) {
				int end = parts.Length;
				float lineIndent = RichStringLayout.LineIndent(lines.Count, indent, hangingIndent);
				while (end > (skip + 1) && (lineIndent + GetWidth(widths, skip, end, space)) > width) {
					end--;
				}
				lines.Add(RichString.Join(" ", TextFormat.REGULAR, parts.Skip(skip).Take(end - skip)));
				skip += end - skip;
			}

			return lines.ToArray();
		}

		/// <summary>Splits a single line of <c>RichString</c> (i.e. no newlines) into lines below a fixed width.</summary>
		public static RichString[] SplitLines(ISharpGraphicsData graphicsData, RichString text, float width, float fontSize, ParagraphSpecification paragraphSpec) {
			return SplitLines(graphicsData, text, width, fontSize, paragraphSpec.Indent, paragraphSpec.HangingIndent);
		}

		public static RichString[][] SplitParagraphs(ISharpGraphicsData graphicsData, RichString[] paragraphs, float width, float fontSize, ParagraphSpecification paragraphSpec, bool indentFirstLine) {
			RichString[][] splitParagraphs = new RichString[paragraphs.Length][];

			for (int i = 0; i < paragraphs.Length; i++) {
				float paragraphIndent = RichStringLayout.LineIndent(i, 0, indentFirstLine, paragraphSpec.Indent, paragraphSpec.HangingIndent);
				splitParagraphs[i] = SplitLines(graphicsData, paragraphs[i], width, fontSize, paragraphIndent, paragraphSpec.HangingIndent);
			}

			return splitParagraphs;
		}

		public static RichString[][] SplitParagraphs(ISharpGraphicsData graphicsData, RichString text, float width, float fontSize, ParagraphSpecification paragraphSpec, bool indentFirstLine) {
			RichString[] wholeLines = text.Split('\n');
			return SplitParagraphs(graphicsData, wholeLines, width, fontSize, paragraphSpec, indentFirstLine);
		}

	}

	public static class RichStringSplitLineDrawingUtils {

		/// <summary>Draws <see cref="RichString"/> to the canvas inside <c>rect</c> at a given font size, splitting the text as necessary (into lines and paragraphs).</summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas DrawRichText(this ISharpCanvas canvas, Rectangle rect, RichString text, float fontSize, ParagraphSpecification paragraphSpec, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy, bool indentFirstLine, (float x, float y) offset = default) {
			RichString[][] splitLines = RichStringLineSplitting.SplitParagraphs(canvas, text, rect.Width, fontSize, paragraphSpec, indentFirstLine);
			canvas.DrawRichText(rect, splitLines, fontSize, paragraphSpec, justification, alignment, heightStrategy, indentFirstLine, offset);
			return canvas;
		}

	}

}
