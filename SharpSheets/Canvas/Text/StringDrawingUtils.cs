using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas.Text {

	public static class StringDrawingUtils {

		public static ISharpCanvas DrawText(this ISharpCanvas canvas, Layouts.Rectangle rect, string text, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy, (float x, float y) offset = default) {

			float fontSize = canvas.GetTextSize();
			TextFormat font = canvas.GetTextFormat();

			float width = canvas.GetWidth(text, font, fontSize);

			float x, y;

			if (justification == Justification.LEFT) {
				x = rect.Left;
			}
			else if (justification == Justification.CENTRE) {
				x = rect.CentreX - width / 2;
			}
			else { // justification == Justification.RIGHT
				x = rect.Right - width;
			}

			if (alignment == Alignment.BOTTOM) {
				y = rect.Bottom + TextHeightUtils.GetBaselineOffset(canvas, text, font, fontSize, fontSize, heightStrategy);
			}
			else if (alignment == Alignment.CENTRE) {
				float height = TextHeightUtils.GetHeight(canvas, text, font, fontSize, fontSize, heightStrategy); // canvas.GetAscent(text, font, fontSize);
				y = rect.CentreY - height / 2 + TextHeightUtils.GetBaselineOffset(canvas, text, font, fontSize, fontSize, heightStrategy);
			}
			else { // alignment == Alignment.TOP
				y = rect.Top + TextHeightUtils.GetYOffset(canvas, text, font, fontSize, fontSize, heightStrategy);
			}

			canvas.DrawText(text, x + offset.x, y + offset.y);

			return canvas;
		}

		/*
		private static string[] SplitLines(ISharpCanvas canvas, string text, float width, TextFormat format, float fontSize) {
			string[] parts = text.Split(' ');

			List<string> lines = new List<string>();

			while (parts.Length > 0) {
				//Console.WriteLine("parts.Length = " + parts.Length);
				int i = parts.Length;
				while (i > 1 && canvas.GetWidth(string.Join(" ", parts.Take(i)), format, fontSize) > width) {
					i--;
				}
				lines.Add(string.Join(" ", parts.Take(i)));
				parts = parts.Skip(i).ToArray();
			}

			return lines.ToArray();
		}

		public static ISharpCanvas FitText(this ISharpCanvas canvas, Layouts.Rectangle rect, string text, Justification justification, float lineSpacing) {

			// TODO This method definitely needs improving. Bisection search? Variable epsilon?

			float fontSize = canvas.GetTextSize() + 0.25f;
			TextFormat format = canvas.GetTextFormat();

			string[] lines = text.Split('\n');

			string[] splitLines;
			float height;
			Layouts.Rectangle textRect;

			do {
				fontSize -= 0.25f;
				if (fontSize == 0) { return canvas; }
				textRect = rect.Margins(0f, 0f, canvas.GetDescent(text, format, fontSize), 0f, true);
				splitLines = lines.SelectMany(l => SplitLines(canvas, l, textRect.Width, format, fontSize)).ToArray();
				height = splitLines.Length * fontSize * lineSpacing;
				//Console.WriteLine("fontSize = " + fontSize);
			}
			while (height > textRect.Height);

			canvas.SaveState();

			canvas.SetTextSize(fontSize);

			float yPosition = textRect.Top - fontSize;
			for (int i = 0; i < splitLines.Length; i++) {
				float xPosition;
				float width = canvas.GetWidth(splitLines[i], format, fontSize);
				if (justification == Justification.LEFT) {
					xPosition = textRect.Left;
				}
				else if (justification == Justification.CENTRE) {
					xPosition = textRect.CentreX - width / 2;
				}
				else { // justification == Justification.RIGHT
					xPosition = textRect.Right - width;
				}
				canvas.DrawText(splitLines[i], xPosition, yPosition);
				yPosition -= fontSize * lineSpacing;
			}

			canvas.RestoreState();

			return canvas;
		}
		*/

	}

}
