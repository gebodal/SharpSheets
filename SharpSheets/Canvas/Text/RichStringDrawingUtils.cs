using SharpSheets.Canvas;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas.Text {

	public static class RichStringDrawingUtils {

		/// <summary>Draws a single line of <see cref="RichString"/> to the canvas as position (x,y).</summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas DrawRichText(this ISharpCanvas canvas, RichString line, float x, float y) {
			canvas.SaveState();

			float fontSize = canvas.GetTextSize();
			float xPos = x;

			foreach((string segment, TextFormat format) in line.GetSegments()) {
				canvas.SetTextFormatAndSize(format, fontSize);
				canvas.DrawText(segment, xPos, y);
				xPos += canvas.GetWidth(segment);
			}

			canvas.RestoreState();
			return canvas;
		}

		/// <summary>Draws a single line of <see cref="RichString"/> to the canvas inside <paramref name="rect"/> at the current font size.</summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpCanvas DrawRichText(this ISharpCanvas canvas, Rectangle rect, RichString line, Justification justification, Alignment alignment, TextHeightStrategy heightStrategy, (float x, float y) offset = default) {
			
			float fontSize = canvas.GetTextSize();

			float width = canvas.GetWidth(line, fontSize);

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
				y = rect.Bottom + TextHeightUtils.GetBaselineOffset(canvas, line, fontSize, fontSize, heightStrategy);
			}
			else if (alignment == Alignment.CENTRE) {
				float height = TextHeightUtils.GetHeight(canvas, line, fontSize, fontSize, heightStrategy);
				y = rect.CentreY - height / 2 + TextHeightUtils.GetBaselineOffset(canvas, line, fontSize, fontSize, heightStrategy);
			}
			else { // alignment == Alignment.TOP
				y = rect.Top + TextHeightUtils.GetYOffset(canvas, line, fontSize, fontSize, heightStrategy);
			}

			canvas.DrawRichText(line, x, y);

			return canvas;
		}

	}

}
