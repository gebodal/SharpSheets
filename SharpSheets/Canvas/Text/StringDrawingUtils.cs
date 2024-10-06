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

	}

}
