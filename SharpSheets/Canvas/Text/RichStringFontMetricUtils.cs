using SharpSheets.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas.Text {

	public static class RichStringFontMetricUtils {

		public static float GetWidth(this ISharpGraphicsData graphicsData, RichString text, float fontsize) {
			float total = 0f;
			StringBuilder builder = new StringBuilder(text.Length);
			TextFormat format = TextFormat.REGULAR;
			for (int i = 0; i < text.chars.Length; i++) {
				if (format != text.formats[i]) {
					if (builder.Length > 0) {
						total += graphicsData.GetWidth(builder.ToString(), format, fontsize);
						builder.Clear();
					}
					format = text.formats[i];
				}
				builder.Append(text.chars[i]);
			}
			if (builder.Length > 0) {
				total += graphicsData.GetWidth(builder.ToString(), format, fontsize);
			}
			return total;
		}

		public static float GetAscent(this ISharpGraphicsData graphicsData, RichString text, float fontsize) {
			float ascent = 0f;
			StringBuilder builder = new StringBuilder(text.Length);
			TextFormat format = TextFormat.REGULAR;
			bool calculated = false;
			for (int i = 0; i < text.chars.Length; i++) {
				if (format != text.formats[i]) {
					if (builder.Length > 0) {
						float newAscent = graphicsData.GetAscent(builder.ToString(), format, fontsize);
						if(!calculated || newAscent > ascent) {
							ascent = newAscent;
						}
						calculated = true;
						builder.Clear();
					}
					format = text.formats[i];
				}
				builder.Append(text.chars[i]);
			}
			if (builder.Length > 0) {
				float newAscent = graphicsData.GetAscent(builder.ToString(), format, fontsize);
				if (!calculated || newAscent > ascent) {
					ascent = newAscent;
				}
			}
			return ascent;
		}

		public static float GetDescent(this ISharpGraphicsData graphicsData, RichString text, float fontsize) {
			float descent = 0f;
			StringBuilder builder = new StringBuilder(text.Length);
			TextFormat format = TextFormat.REGULAR;
			bool calculated = false;
			for (int i = 0; i < text.chars.Length; i++) {
				if (format != text.formats[i]) {
					if (builder.Length > 0) {
						float newDescent = graphicsData.GetDescent(builder.ToString(), format, fontsize);
						if(!calculated || newDescent < descent) {
							descent = newDescent;
						}
						calculated = true;
						builder.Clear();
					}
					format = text.formats[i];
				}
				builder.Append(text.chars[i]);
			}
			if (builder.Length > 0) {
				float newDescent = graphicsData.GetDescent(builder.ToString(), format, fontsize);
				if (!calculated || newDescent < descent) {
					descent = newDescent;
				}
			}
			return descent;
		}

		public static float GetWidth(this ISharpGraphicsData graphicsData, RichString text) {
			return GetWidth(graphicsData, text, graphicsData.GetTextSize());
		}
		public static float GetAscent(this ISharpGraphicsData graphicsData, RichString text) {
			return GetAscent(graphicsData, text, graphicsData.GetTextSize());
		}
		public static float GetDescent(this ISharpGraphicsData graphicsData, RichString text) {
			return GetDescent(graphicsData, text, graphicsData.GetTextSize());
		}

	}
}
