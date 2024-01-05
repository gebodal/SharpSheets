using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpSheets.Canvas;
using SharpSheets.Fonts;

namespace SharpSheets.Canvas.Text {

	/// <summary>
	/// Strategy to use for calculating the height of a block of text, given a specific font.
	/// </summary>
	public enum TextHeightStrategy {
		/// <summary>
		/// Indicates that the height of the text should be calculated from the baseline of the
		/// bottom line to a distance equal to the current line-height above the baseline of the
		/// top line.
		/// </summary>
		LineHeightBaseline,
		/// <summary>
		/// Indicates that the height of the text should be calculated from the baseline of the
		/// bottom line to a distance equal to the current fontsize above the baseline of the
		/// top line.
		/// </summary>
		FontsizeBaseline,
		/// <summary>
		/// Indicates that the height of the text should be calculated from the baseline of the
		/// bottom line to a distance equal to the maximum ascent of the top line above the baseline
		/// of the top line.
		/// </summary>
		AscentBaseline,
		/// <summary>
		/// Indicates that the height of the text should be calculated from the maximum descent of
		/// the bottom line below the bottom baseline, to a distance equal to the current line-height
		/// above the baseline of the top line.
		/// </summary>
		LineHeightDescent,
		/// <summary>
		/// Indicates that the height of the text should be calculated from the maximum descent of
		/// the bottom line below the bottom baseline, to a distance equal to the current fontsize
		/// above the baseline of the top line.
		/// </summary>
		FontsizeDescent,
		/// <summary>
		/// Indicates that the height of the text should be calculated from the maximum descent of
		/// the bottom line below the bottom baseline, to a distance equal to the maximum ascent of
		/// the top line above the baseline of the top line.
		/// </summary>
		AscentDescent
	}

	public static class TextHeightStrategyUtils {

		public static bool UseLineHeight(this TextHeightStrategy strategy) {
			return strategy == TextHeightStrategy.LineHeightBaseline || strategy == TextHeightStrategy.LineHeightDescent;
		}

		public static bool UseAscent(this TextHeightStrategy strategy) {
			return strategy == TextHeightStrategy.AscentBaseline || strategy == TextHeightStrategy.AscentDescent;
		}

		public static bool UseDescent(this TextHeightStrategy strategy) {
			return strategy == TextHeightStrategy.LineHeightDescent || strategy == TextHeightStrategy.FontsizeDescent || strategy == TextHeightStrategy.AscentDescent;
		}

	}

	public static class TextHeightUtils {

		public static float GetHeight(ISharpGraphicsData graphics, string line, TextFormat format, float fontSize, float lineHeight, TextHeightStrategy heightStrategy) {
			return (heightStrategy.UseLineHeight() ? lineHeight : (heightStrategy.UseAscent() ? graphics.GetAscent(line, format, fontSize) : fontSize))
				- (heightStrategy.UseDescent() ? graphics.GetDescent(line, format, fontSize) : 0f);
		}

		public static float GetHeight(ISharpGraphicsData graphics, RichString line, float fontSize, float lineHeight, TextHeightStrategy heightStrategy) {
			return (heightStrategy.UseLineHeight() ? lineHeight : (heightStrategy.UseAscent() ? graphics.GetAscent(line, fontSize) : fontSize))
				- (heightStrategy.UseDescent() ? graphics.GetDescent(line, fontSize) : 0f);
		}

		public static float GetHeight(ISharpGraphicsData graphics, string[] lines, TextFormat format, float fontSize, float lineHeight, TextHeightStrategy heightStrategy) {
			if (lines.Length == 0) {
				return 0f;
			}

			return (heightStrategy.UseLineHeight() ? lineHeight : (heightStrategy.UseAscent() ? graphics.GetAscent(lines[0], format, fontSize) : fontSize))
				+ (lines.Length - 1) * lineHeight // Calculate first line separately
				- (heightStrategy.UseDescent() ? graphics.GetDescent(lines[lines.Length - 1], format, fontSize) : 0f);
		}

		public static float GetHeight(ISharpGraphicsData graphics, RichString[] lines, float fontSize, float lineHeight, TextHeightStrategy heightStrategy) {
			if (lines.Length == 0) {
				return 0f;
			}

			return (heightStrategy.UseLineHeight() ? lineHeight : (heightStrategy.UseAscent() ? graphics.GetAscent(lines[0], fontSize) : fontSize))
				+ (lines.Length - 1) * lineHeight // Calculate first line separately
				- (heightStrategy.UseDescent() ? graphics.GetDescent(lines[lines.Length - 1], fontSize) : 0f);
		}

		public static float GetHeight(ISharpGraphicsData graphics, string[][] lines, TextFormat format, float fontSize, float lineHeight, float paragraphSpacing, TextHeightStrategy heightStrategy) {
			if (lines.Length == 0) {
				return 0f;
			}

			float total = 0f;

			for (int p = 0; p < lines.Length; p++) {
				if (p == 0) {
					total += (heightStrategy.UseLineHeight() ? lineHeight : (heightStrategy.UseAscent() ? graphics.GetAscent(lines[p].Length > 0 ? lines[p][0] : "", format, fontSize) : fontSize))
						+ (lines[p].Length - 1) * lineHeight; // Calculate first line separately
				}
				else {
					total += lines[p].Length * lineHeight;
				}
			}

			total += (lines.Length - 1) * paragraphSpacing;

			if (heightStrategy.UseDescent()) {
				string[] lastPara = lines[lines.Length - 1];
				string lastLine = lastPara.Length > 0 ? lastPara[lastPara.Length - 1] : "";
				
				total -= graphics.GetDescent(lastLine, format, fontSize);
			}

			return total;
		}

		public static float GetHeight(ISharpGraphicsData graphics, RichString[][] lines, float fontSize, float lineHeight, float paragraphSpacing, TextHeightStrategy heightStrategy) {
			if (lines.Length == 0) {
				return 0f;
			}

			float total = 0f;

			for (int p = 0; p < lines.Length; p++) {
				if (p == 0) {
					total += (heightStrategy.UseLineHeight() ? lineHeight : (heightStrategy.UseAscent() ? graphics.GetAscent(lines[p].Length > 0 ? lines[p][0] : RichString.Empty, fontSize) : fontSize))
						+ (lines[p].Length - 1) * lineHeight; // Calculate first line separately
				}
				else {
					total += lines[p].Length * lineHeight;
				}
			}

			total += (lines.Length - 1) * paragraphSpacing;

			if (heightStrategy.UseDescent()) {
				RichString[] lastPara = lines[lines.Length - 1];
				RichString lastLine = lastPara.Length > 0 ? lastPara[lastPara.Length - 1] : RichString.Empty;

				total -= graphics.GetDescent(lastLine, fontSize);
			}

			return total;
		}

		/// <summary>
		/// Returns the Y offset of the basline from the top of the text area. This will be a negative number.
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="line"></param>
		/// <param name="format"></param>
		/// <param name="fontSize"></param>
		/// <param name="lineHeight"></param>
		/// <param name="heightStrategy"></param>
		/// <returns></returns>
		public static float GetYOffset(ISharpGraphicsData graphics, string line, TextFormat format, float fontSize, float lineHeight, TextHeightStrategy heightStrategy) {
			return -(heightStrategy.UseLineHeight() ? lineHeight : (heightStrategy.UseAscent() ? graphics.GetAscent(line, format, fontSize) : fontSize));
		}

		/// <summary>
		/// Returns the Y offset of the basline from the top of the text area. This will be a negative number.
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="line"></param>
		/// <param name="fontSize"></param>
		/// <param name="lineHeight"></param>
		/// <param name="heightStrategy"></param>
		/// <returns></returns>
		public static float GetYOffset(ISharpGraphicsData graphics, RichString line, float fontSize, float lineHeight, TextHeightStrategy heightStrategy) {
			return -(heightStrategy.UseLineHeight() ? lineHeight : (heightStrategy.UseAscent() ? graphics.GetAscent(line, fontSize) : fontSize));
		}

		/// <summary>
		/// Returns the Y offset of the baseline from the bottom of the text area. This number could be positive (if the text has descenders), negative (if all glyphs begin above the line), or zero (if the text is to simply be drawn from the baseline), depending on settings. 
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="line"></param>
		/// <param name="format"></param>
		/// <param name="fontSize"></param>
		/// <param name="lineHeight"></param>
		/// <param name="heightStrategy"></param>
		/// <returns></returns>
		public static float GetBaselineOffset(ISharpGraphicsData graphics, string line, TextFormat format, float fontSize, float lineHeight, TextHeightStrategy heightStrategy) {
			return (heightStrategy.UseDescent() ? -graphics.GetDescent(line, format, fontSize) : 0f);
		}

		/// <summary>
		/// Returns the Y offset of the baseline from the bottom of the text area. This number could be positive (if the text has descenders), negative (if all glyphs begin above the line), or zero (if the text is to simply be drawn from the baseline), depending on settings. 
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="line"></param>
		/// <param name="fontSize"></param>
		/// <param name="lineHeight"></param>
		/// <param name="heightStrategy"></param>
		/// <returns></returns>
		public static float GetBaselineOffset(ISharpGraphicsData graphics, RichString line, float fontSize, float lineHeight, TextHeightStrategy heightStrategy) {
			return (heightStrategy.UseDescent() ? -graphics.GetDescent(line, fontSize) : 0f);
		}

	}

}
