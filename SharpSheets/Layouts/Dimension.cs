using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SharpSheets.Layouts {

	public readonly struct Dimension {

		public static readonly Dimension Zero = new Dimension(0f, 0f, 0f, false);
		public static readonly Dimension Single = new Dimension(1f, 0f, 0f, false);
		public static readonly Dimension Automatic = new Dimension(0f, 0f, 0f, true);

		public float Relative { get; }
		public float Absolute { get; } // in user units
		/// <summary>
		/// Percentage length, as a fraction (0.0-1.0).
		/// </summary>
		public float Percent { get; }
		public bool Auto { get; }

		public bool IsAbsolute { get { return !Auto && Relative == 0 && Percent == 0; } }
		public bool HasAbsolute { get { return !Auto && Absolute > 0; } }
		public bool IsZero { get { return !Auto && Relative == 0f && Absolute == 0f && Percent == 0f; } }

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		private Dimension(float relative, float absolute, float percent, bool auto) {
			if(relative < 0f) { throw new ArgumentException("Relative component of Dimension must be positive."); }

			this.Relative = relative;
			this.Absolute = absolute;
			this.Percent = percent;
			this.Auto = auto;
		}

		public override string ToString() {
			return ToString(null);
		}

		public string ToString(IFormatProvider? formatProvider) {

			if (Auto) { return "auto"; }

			List<string> parts = new List<string>();

			if (Relative != 0f) {
				parts.Add(Relative.ToString(formatProvider));
			}
			if (Absolute != 0f) {
				parts.Add($"{Absolute.ToString(formatProvider)}pt");
			}
			if (Percent != 0f) {
				parts.Add($"{(100f * Percent).ToString(formatProvider)}pc");
			}

			if (parts.Count == 0) {
				parts.Add((0f).ToString(formatProvider));
			}

			//return string.Format("Dimension({0})", string.Join("+", parts));
			return string.Join("+", parts);
		}

		public static Dimension operator +(Dimension a, Dimension b) {
			if (a.Auto || b.Auto) { throw new ArgumentException("Cannot add automatic Dimensions."); }
			return new Dimension(a.Relative + b.Relative, a.Absolute + b.Absolute, a.Percent + b.Percent, false);
		}
		public static Dimension operator -(Dimension a, Dimension b) {
			if (a.Auto || b.Auto) { throw new ArgumentException("Cannot subtract automatic Dimensions."); }
			return new Dimension(a.Relative - b.Relative, a.Absolute - b.Absolute, a.Percent - b.Percent, false);
		}
		public static Dimension operator *(Dimension a, float b) {
			if (a.Auto) { throw new ArgumentException("Cannot multiply automatic Dimensions."); }
			return new Dimension(a.Relative * b, a.Absolute * b, a.Percent * b, false);
		}
		public static Dimension operator *(float a, Dimension b) {
			if (b.Auto) { throw new ArgumentException("Cannot multiply automatic Dimensions."); }
			return new Dimension(a * b.Relative, a * b.Absolute, a * b.Percent, false);
		}
		public static Dimension operator /(Dimension a, float b) {
			if (a.Auto) { throw new ArgumentException("Cannot divide automatic Dimensions."); }
			return new Dimension(a.Relative / b, a.Absolute / b, a.Percent / b, false);
		}

		private static readonly Regex dimensionRegex = new Regex(@"^(?<number>[\+\-]?[0-9]+\.[0-9]+|\.[0-9]+|[\+\-]?[0-9]+\.?)\s*(?<unit>pt|in|cm|mm|pc|\%)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static Dimension Parse(string str, IFormatProvider? provider) {
			str = str.Trim();

			if(string.Equals(str, "auto", StringComparison.InvariantCultureIgnoreCase)) {
				return Automatic;
			}

			Dimension total = Dimension.Zero;

			string[] parts = str.SplitAndTrim('+');
			for (int i = 0; i < parts.Length; i++) {
				Match match = dimensionRegex.Match(parts[i]);
				if (match.Success) {
					float number = float.Parse(match.Groups[1].Value, provider);
					string unit = match.Groups[2].Value.ToLowerInvariant();
					Dimension value;
					if (unit == "pc" || unit == "%") {
						value = FromPercent(number);
					}
					else if (unit == "pt") {
						value = FromPoints(number);
					}
					else if (unit == "in") {
						value = FromInches(number);
					}
					else if (unit == "cm") {
						value = FromCentimetres(number);
					}
					else if (unit == "mm") {
						value = FromMillimetres(number);
					}
					else {
						value = FromRelative(number);
					}
					total += value;
				}
				else {
					throw new FormatException($"\"{str}\" is not a valid Dimension (for units use pt, in, cm, mm, or pc, or specify \"auto\")");
				}
			}

			return total;
		}

		public static Dimension Parse(string str) {
			return Parse(str, null);
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="FormatException"></exception>
		public static Dimension ParseAbsoluteOrPercent(string str, IFormatProvider? provider) {
			Dimension parsed = Parse(str, provider);
			if (parsed.Relative == 0) {
				return parsed;
			}
			else {
				throw new FormatException("Dimension must be absolute or percentage.");
			}
		}

		public static Dimension ParseAbsoluteOrPercent(string str) {
			return ParseAbsoluteOrPercent(str, null);
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="FormatException"></exception>
		public static Dimension ParseAbsolute(string str, IFormatProvider? provider) {
			Dimension parsed = Parse(str, provider);
			if (parsed.Relative == 0 && parsed.Percent == 0) {
				return parsed;
			}
			else {
				throw new FormatException("Dimension must be absolute.");
			}
		}

		public static Dimension ParseAbsolute(string str) {
			return ParseAbsolute(str, null);
		}

		public static Dimension FromRelative(float relative) {
			return new Dimension(relative, 0f, 0f, false);
		}
		public static Dimension FromPoints(float points) {
			return new Dimension(0f, points, 0f, false);
		}
		public static Dimension FromInches(float inches) {
			return new Dimension(0f, InchToPoint(inches), 0f, false);
		}
		public static Dimension FromCentimetres(float centimetres) {
			return new Dimension(0f, CentimetreToPoint(centimetres), 0f, false);
		}
		public static Dimension FromMillimetres(float millimetres) {
			return new Dimension(0f, MillimetreToPoint(millimetres), 0f, false);
		}
		public static Dimension FromPercent(float percent) {
			return new Dimension(0f, 0f, percent * 0.01f, false);
		}

		public static Dimension[] FromRelative(params float[] relative) {
			Dimension[] result = new Dimension[relative.Length];
			for (int i = 0; i < relative.Length; i++) {
				result[i] = FromRelative(relative[i]);
			}
			return result;
		}
		public static Dimension[] FromPoints(params float[] points) {
			Dimension[] result = new Dimension[points.Length];
			for (int i = 0; i < points.Length; i++) {
				result[i] = FromPoints(points[i]);
			}
			return result;
		}

		public static float InchToPoint(float inches) {
			return inches * 72f;
		}

		public static float CentimetreToPoint(float centimetres) {
			return centimetres * 28.3465f;
		}

		public static float MillimetreToPoint(float millimetres) {
			return millimetres * 283.465f;
		}
	}

	public static class DimensionUtils {

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static Dimension Sum(this IEnumerable<Dimension> source) {
			float relative = 0f;
			float absolute = 0f;
			float percent = 0f;

			foreach (Dimension dimension in source) {
				if (dimension.Auto) { throw new InvalidOperationException("Cannot sum automatic Dimensions."); }

				relative += dimension.Relative;
				absolute += dimension.Absolute;
				percent += dimension.Percent;
			}

			return Dimension.FromRelative(relative) + Dimension.FromPoints(absolute) + Dimension.FromPercent(percent);
		}

	}

}
