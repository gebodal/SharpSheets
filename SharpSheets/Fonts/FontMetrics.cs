using System.Collections.Generic;
using GeboPdf.Fonts;
using SharpSheets.Canvas;
using SharpSheets.PDFs;
using SharpSheets.Canvas.Text;

namespace SharpSheets.Fonts {

	public static class FontMetrics {

		public static float GetWidth(string text, FontPathGrouping fonts, TextFormat format, float fontSize) {
			return FontGraphicsRegistry.GetPdfFont(fonts, format).GetWidth(text, fontSize);
		}

		public static float GetAscent(string text, FontPathGrouping fonts, TextFormat format, float fontSize) {
			return FontGraphicsRegistry.GetPdfFont(fonts, format).GetAscent(text, fontSize);
		}

		public static float GetDescent(string text, FontPathGrouping fonts, TextFormat format, float fontSize) {
			return FontGraphicsRegistry.GetPdfFont(fonts, format).GetDescent(text, fontSize);
		}

	}

}
