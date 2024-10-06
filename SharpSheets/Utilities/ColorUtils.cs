using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SharpSheets.Colors;

namespace SharpSheets.Utilities {
	public static class ColorUtils {

		private static readonly Regex hexColorRegex = new Regex(@"\#?(?:[0-9abcdef]{8}|[0-9abcdef]{6})", RegexOptions.IgnoreCase);

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static Color Parse(string colorStr, IFormatProvider? provider) {
			colorStr = Regex.Replace(colorStr.ToLowerInvariant(), @"\s+", " ").Trim();
			
			Color namedColor = Color.FromName(colorStr);
			if (namedColor.Initialised) {
				return namedColor;
			}

			if (hexColorRegex.IsMatch(colorStr)) {
				string hexString = colorStr;
				if (hexString.StartsWith("#")) { hexString = hexString.Substring(1); }
				byte[] values = hexString.Chunk(2).Select(h => (byte)int.Parse(h, System.Globalization.NumberStyles.HexNumber)).ToArray();
				if (values.Length == 3) {
					return new Color(values[0], values[1], values[2]);
				}
				else if (values.Length == 4) {
					return new Color(values[1], values[2], values[3], values[0]);
				}
			}

			try {
				float[] parts = colorStr.SplitAndTrim(Array.Empty<char>()).Select(s => float.Parse(s, provider)).ToArray();

				// TODO What about negative numbers, or numbers greater than 255?

				byte[] values;
				if (parts.All(p => p <= 1f)) {
					values = parts.Select(p => (byte)(p * 255)).ToArray();
				}
				else {
					values = parts.Select(p => (byte)p).ToArray();
				}

				if (values.Length == 1) {
					return new Color(values[0], values[0], values[0]);
				}
				else if (values.Length == 3) {
					return new Color(values[0], values[1], values[2]);
				}
				else if (values.Length == 4) {
					return new Color(values[1], values[2], values[3], values[0]);
				}
			}
			catch (FormatException) { }

			//throw new FormatException("Color string not properly formatted. Can accept 1 (Grey), 3 (RBG), and 4 (CMYK) part strings.");
			throw new FormatException("Color string not properly formatted. Can accept space-separated number lists (of size 1 (Grey), 3 (RBG), or 4 (ARBG)), a hexcode (6 or 8 digits), or a recognized color name.");
		}

		public static Color Parse(string colorStr) {
			return Parse(colorStr, null);
		}

		public static string ToHexString(this Color color) {
			string a = color.A.Clamp(0, 255).ToString("X2");
			string r = color.R.Clamp(0, 255).ToString("X2");
			string g = color.G.Clamp(0, 255).ToString("X2");
			string b = color.B.Clamp(0, 255).ToString("X2");
			return "#" + a + r + g + b;
		}

		public static Color WithOpacity(this Color baseColor, float opacity) {
			byte alpha = (byte)(opacity.Clamp(0, 1) * 255);
			return new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
		}

		public static Color FromGrayscale(float gray) {
			byte val = (byte)(gray.Clamp(0f, 1f) * 255);
			return new Color(val, val, val);
		}

		public static Color FromRGB(float r, float g, float b) {
			byte red = (byte)(r.Clamp(0f, 1f) * 255);
			byte green = (byte)(g.Clamp(0f, 1f) * 255);
			byte blue = (byte)(b.Clamp(0f, 1f) * 255);
			return new Color(red, green, blue);
		}

		public static Color FromRGBA(float r, float g, float b, float a) {
			byte red = (byte)(r.Clamp(0f, 1f) * 255);
			byte green = (byte)(g.Clamp(0f, 1f) * 255);
			byte blue = (byte)(b.Clamp(0f, 1f) * 255);
			byte alpha = (byte)(a.Clamp(0f, 1f) * 255);
			return new Color(red, green, blue, alpha);
		}

		public static Color Darken(this Color color, float factor) {
			float hue = color.GetHue();
			float saturation = color.GetSaturation();
			float lightness = (color.GetLightness() * factor.Clamp(0f, 1f)).Clamp(0f, 1f);

			return ColorFromHSV(hue, saturation, lightness);
		}

		public static Color Lighten(this Color color, float factor) {
			float hue = color.GetHue();
			float saturation = color.GetSaturation();
			float lightness = 1f - ((1f - color.GetLightness()) * factor.Clamp(0f, 1f)).Clamp(0f, 1f);

			return ColorFromHSV(hue, saturation, lightness);
		}

		public static Color AdjustBrightness(this Color color, float delta) {
			float hue = color.GetHue();
			float saturation = color.GetSaturation();
			float lightness = (color.GetLightness() + delta).Clamp(0f, 1f);

			return ColorFromHSV(hue, saturation, lightness);
		}

		public static Color Lerp(Color a, Color b, float t) {
			float hue = MathUtils.Lerp(a.GetHue(), b.GetHue(), t);
			float saturation = MathUtils.Lerp(a.GetSaturation(), b.GetSaturation(), t);
			float lightness = MathUtils.Lerp(a.GetLightness(), b.GetLightness(), t);

			Color result = ColorFromHSV(hue, saturation, lightness);

			return result;
		}

		public static Color ColorFromHSV(float h, float s, float v) {
			if (s == 0) {
				byte L = (byte)(255 * v);
				return new Color(L, L, L);
			}

			double newH = h / 360d;

			double max = v < 0.5d ? v * (1 + s) : (v + s) - (v * s);
			double min = (v * 2d) - max;

			byte r = (byte)(255 * RGBChannelFromHue(min, max, newH + 1 / 3d));
			byte g = (byte)(255 * RGBChannelFromHue(min, max, newH));
			byte b = (byte)(255 * RGBChannelFromHue(min, max, newH - 1 / 3d));

			Color c = new Color(r, g, b);
			return c;
		}

		private static double RGBChannelFromHue(double m1, double m2, double h) {
			h = (h + 1d) % 1d;
			if (h < 0) h += 1;
			if (h * 6 < 1) return m1 + (m2 - m1) * 6 * h;
			else if (h * 2 < 1) return m2;
			else if (h * 3 < 2) return m1 + (m2 - m1) * 6 * (2d / 3d - h);
			else return m1;
		}
	}
}