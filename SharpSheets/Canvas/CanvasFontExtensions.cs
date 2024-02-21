using SharpSheets.Fonts;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas {

	public static class CanvasFontxtensions {

		public static float GetWidth(this ISharpGraphicsState graphicsState, string text) {
			return graphicsState.GetWidth(text, graphicsState.GetTextFormat(), graphicsState.GetTextSize());
		}

		public static float GetAscent(this ISharpGraphicsState graphicsState, string text) {
			return graphicsState.GetAscent(text, graphicsState.GetTextFormat(), graphicsState.GetTextSize());
		}

		public static float GetDescent(this ISharpGraphicsState graphicsState, string text) {
			return graphicsState.GetDescent(text, graphicsState.GetTextFormat(), graphicsState.GetTextSize());
		}

		public static ISharpGraphicsState SetFonts(this ISharpGraphicsState graphicsState, FontSettingGrouping fonts) {
			graphicsState.SetFont(TextFormat.REGULAR, fonts.Regular);
			graphicsState.SetFont(TextFormat.BOLD, fonts.Bold);
			graphicsState.SetFont(TextFormat.ITALIC, fonts.Italic);
			graphicsState.SetFont(TextFormat.BOLDITALIC, fonts.BoldItalic);
			return graphicsState;
		}

	}
}
