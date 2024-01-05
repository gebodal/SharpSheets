using SharpSheets.Canvas;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas.Text {

	public static class RichStringLayout {

		#region Indentation

		public static float LineIndent(int line, float indent, float hangingIndent) {
			return IsLineIndented(line) ? indent : hangingIndent;
		}

		public static float LineIndent(int paragraph, int line, bool indentFirstLine, float indent, float hangingIndent) {
			return IsLineIndented(paragraph, line, indentFirstLine) ? indent : hangingIndent;
		}

		public static bool IsLineIndented(int line) {
			return line == 0;
		}
		public static bool IsLineIndented(int paragraph, int line, bool indentFirstLine) {
			return (paragraph > 0 && line == 0) || (indentFirstLine && paragraph == 0 && line == 0);
		}

		#endregion

		/// <summary>Calculate the width of a given set of <see cref="RichString"/> lines (paragraph grouped), assuming no further splitting required.</summary>
		public static float CalculateWidth(ISharpGraphicsData graphicsData, RichString[][] splitLines, float fontSize, ParagraphSpecification paragraphSpec, bool indentFirstLine) {
			float width = 0f;

			for (int paragraph = 0; paragraph < splitLines.Length; paragraph++) {
				for (int line = 0; line < splitLines[paragraph].Length; line++) {
					float lineTextWidth = graphicsData.GetWidth(splitLines[paragraph][line], fontSize);
					float lineIndent = RichStringLayout.LineIndent(paragraph, line, indentFirstLine, paragraphSpec.Indent, paragraphSpec.HangingIndent);

					float totalWidth = lineIndent + lineTextWidth;
					if (totalWidth > width) {
						width = totalWidth;
					}
				}
			}

			return width;
		}

		/// <summary>Calculate the width of a given set of <see cref="RichString"/> lines (single paragraph), assuming no further splitting required.</summary>
		public static float CalculateWidth(ISharpGraphicsData graphicsData, RichString[] splitLines, float fontSize, ParagraphSpecification paragraphSpec, bool indentFirstLine) {
			float width = 0f;

			for (int line = 0; line < splitLines.Length; line++) {
				float lineTextWidth = graphicsData.GetWidth(splitLines[line], fontSize);
				float lineIndent = RichStringLayout.LineIndent(0, line, indentFirstLine, paragraphSpec.Indent, paragraphSpec.HangingIndent);

				float totalWidth = lineIndent + lineTextWidth;
				if (totalWidth > width) {
					width = totalWidth;
				}
			}

			return width;
		}

		#region Calculate Height

		/*
		/// <summary>Calculate the height for a given number of lines (separated by paragraph).</summary>
		public static float CalculateHeight(int[] lineCounts, float fontSize, float lineSpacing, float paragraphSpacing) {
			//int totalCount = splitLines.SelectMany(p => p).Count();
			int totalCount = 0;
			for (int i = 0; i < lineCounts.Length; i++) {
				totalCount += lineCounts[i];
			}
			if (totalCount == 0) {
				return 0f;
			}
			else {
				//return (totalCount - 1) * fontSize * lineSpacing + fontSize + (splitLines.Length - 1) * paragraphSpacing;
				return (totalCount * fontSize * lineSpacing) + ((lineCounts.Length - 1) * paragraphSpacing);
			}
		}

		public static float CalculateHeight(int[] lineCounts, ParagraphSpecification paragraphSpec) {
			return CalculateHeight(lineCounts, paragraphSpec.FontSize, paragraphSpec.LineSpacing, paragraphSpec.ParagraphSpacing);
		}
		*/

		/// <summary>Calculate the height of a given set of <see cref="RichString"/> lines (paragraph grouped), assuming no further splitting required.</summary>
		public static float CalculateHeight(ISharpGraphicsData graphicsData, RichString[][] splitLines, float fontSize, float lineSpacing, float paragraphSpacing, TextHeightStrategy heightStrategy) {
			/*
			int[] lineCounts = new int[splitLines.Length];
			for (int i = 0; i < splitLines.Length; i++) {
				lineCounts[i] = splitLines[i].Length;
			}
			return CalculateHeight(lineCounts, fontSize, lineSpacing, paragraphSpacing);
			*/

			return TextHeightUtils.GetHeight(graphicsData, splitLines, fontSize, fontSize * lineSpacing, paragraphSpacing, heightStrategy);
		}

		/// <summary>Calculate the height of a given set of <see cref="RichString"/> lines (single paragraph), assuming no further splitting required.</summary>
		public static float CalculateHeight(ISharpGraphicsData graphicsData, RichString[] splitLines, float fontSize, float lineSpacing, TextHeightStrategy heightStrategy) {
			RichString[][] splitParagraphs = new RichString[][] { splitLines };
			return CalculateHeight(graphicsData, splitParagraphs, fontSize, lineSpacing, 0f, heightStrategy);
		}

		/// <summary>Calculated the height of a given set of <see cref="RichString"/> lines (paragraph grouped), assuming no further splitting required.</summary>
		public static float CalculateHeight(ISharpGraphicsData graphicsData, RichString[][] splitLines, float fontSize, ParagraphSpecification paragraphSpec, TextHeightStrategy heightStrategy) {
			return CalculateHeight(graphicsData, splitLines, fontSize, paragraphSpec.LineSpacing, paragraphSpec.ParagraphSpacing, heightStrategy);
		}

		/// <summary>Calculated the height of a given set of <see cref="RichString"/> lines (single paragraph), assuming no further splitting required.</summary>
		public static float CalculateHeight(ISharpGraphicsData graphicsData, RichString[] splitLines, float fontSize, ParagraphSpecification paragraphSpec, TextHeightStrategy heightStrategy) {
			RichString[][] splitParagraphs = new RichString[][] { splitLines };
			return CalculateHeight(graphicsData, splitParagraphs, fontSize, paragraphSpec, heightStrategy);
		}

		/// <summary>Calculates the height of a <see cref="RichString"/>, which potentially contains newline characters, based on a provided final line <paramref name="width"/>.</summary>
		public static float CalculateHeight(ISharpGraphicsData graphicsData, RichString text, float width, float fontSize, ParagraphSpecification paragraphSpec, TextHeightStrategy heightStrategy, bool indentFirstLine) {
			RichString[][] splitLines = RichStringLineSplitting.SplitParagraphs(graphicsData, text, width, fontSize, paragraphSpec, indentFirstLine);
			return CalculateHeight(graphicsData, splitLines, fontSize, paragraphSpec, heightStrategy);
		}

		#endregion

		#region Start Y Position

		/*
		public static float GetStartY(Rectangle rect, float height, float fontSize, Alignment alignment) {
			float remainingHeight = rect.Height - height;

			float yPosition = rect.Top - fontSize;
			if (alignment == Alignment.CENTRE) {
				yPosition -= remainingHeight / 2;
			}
			else if (alignment == Alignment.BOTTOM) {
				yPosition -= remainingHeight;
			}

			return yPosition;
		}

		public static float GetStartY(Rectangle rect, int[] lineCounts, ParagraphSpecification paragraphSpec, Alignment alignment) {
			return GetStartY(rect, CalculateHeight(lineCounts, paragraphSpec), paragraphSpec.FontSize, alignment);
		}

		public static float GetStartY(ISharpGraphicsData graphicsData, Rectangle rect, RichString[][] lines, ParagraphSpecification paragraphSpec, Alignment alignment, TextHeightStrategy heightStrategy) {
			return GetStartY(rect, CalculateHeight(graphicsData, lines, paragraphSpec, heightStrategy), paragraphSpec.FontSize, alignment);
		}
		*/

		public static float GetStartY(ISharpGraphicsData graphicsData, Rectangle rect, RichString[][] lines, float fontSize, ParagraphSpecification paragraphSpec, Alignment alignment, TextHeightStrategy heightStrategy) {
			float height = CalculateHeight(graphicsData, lines, fontSize, paragraphSpec, heightStrategy);
			RichString firstLine = lines.Length > 0 && lines[0].Length > 0 ? lines[0][0] : RichString.Empty;

			float remainingHeight = rect.Height - height;

			float yPosition = rect.Top + TextHeightUtils.GetYOffset(graphicsData, firstLine, fontSize, paragraphSpec.GetLineHeight(fontSize), heightStrategy);
			if (alignment == Alignment.CENTRE) {
				yPosition -= remainingHeight / 2;
			}
			else if (alignment == Alignment.BOTTOM) {
				yPosition -= remainingHeight;
			}

			return yPosition;
		}

		#endregion

		public static float GetLineStartX(this ISharpCanvas canvas, Rectangle rect, RichString line, float fontSize, ParagraphSpecification paragraphSpec, Justification justification, bool indent) {

			float lineIndent = indent ? paragraphSpec.Indent : paragraphSpec.HangingIndent;

			if (justification == Justification.LEFT) {
				return rect.Left + lineIndent;
			}

			float lineWidth = canvas.GetWidth(line, fontSize);
			float width = lineIndent + lineWidth;

			if (justification == Justification.CENTRE) {
				return rect.CentreX - width / 2;
			}
			else { // justification == Justification.RIGHT
				return rect.Right - width;
			}
		}

		public static (float x, float y) GetRichTextStartLocation(this ISharpCanvas canvas, Rectangle rect, RichString[][] lines, float fontSize, ParagraphSpecification paragraphSpec, Justification justification, Alignment alignment, bool indentFirstLine, TextHeightStrategy heightStrategy) {
			return (
				GetLineStartX(canvas, rect, (lines.Length > 0 && lines[0].Length > 0) ? lines[0][0] : RichString.Empty, fontSize, paragraphSpec, justification, IsLineIndented(0, 0, indentFirstLine)),
				GetStartY(canvas, rect, lines, fontSize, paragraphSpec, alignment, heightStrategy)
				);
		}

	}

	public static class RichStringLayoutDrawingUtils {

		/// <summary>Draws multiple lines (with paragraph subdivisions) of <see cref="RichString"/> to the canvas, without performing any splitting.</summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas DrawRichText(this ISharpCanvas canvas, Rectangle rect, RichString[][] lines, float fontSize, ParagraphSpecification paragraphSpec, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy, bool indentFirstLine, (float x, float y) offset = default) {

			canvas.SaveState();
			canvas.SetTextSize(fontSize);

			float yPosition = RichStringLayout.GetStartY(canvas, rect, lines, fontSize, paragraphSpec, alignment, heightStrategy) + offset.y;

			for (int i = 0; i < lines.Length; i++) {
				for (int j = 0; j < lines[i].Length; j++) {
					float xPosition = RichStringLayout.GetLineStartX(canvas, rect, lines[i][j], fontSize, paragraphSpec, justification, RichStringLayout.IsLineIndented(i, j, indentFirstLine)) + offset.x;

					canvas.DrawRichText(lines[i][j], xPosition, yPosition);
					yPosition -= paragraphSpec.GetLineHeight(fontSize); // fontSize * paragraphSpec.LineSpacing;
				}
				yPosition -= paragraphSpec.ParagraphSpacing;
			}

			canvas.RestoreState();

			return canvas;
		}

		/// <summary>Draws multiple lines (for a single paragraph) of <see cref="RichString"/> to the canvas, without performing any splitting.</summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas DrawRichText(this ISharpCanvas canvas, Rectangle rect, RichString[] lines, float fontSize, ParagraphSpecification paragraphSpec, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy, bool indentFirstLine, (float x, float y) offset = default) {
			RichString[][] paragraphs = new RichString[][] { lines };
			return canvas.DrawRichText(rect, paragraphs, fontSize, paragraphSpec, justification, alignment, heightStrategy, indentFirstLine, offset);
		}

	}

}
