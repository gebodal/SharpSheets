using SharpSheets.Utilities;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpSheets.Layouts {

	public readonly struct Margins : IEquatable<Margins> {

		public static Margins Zero { get; } = new Margins(0f);

		public float Top { get; }
		public float Right { get; }
		public float Bottom { get; }
		public float Left { get; }

		public Margins(float top, float right, float bottom, float left) {
			Top = top;
			Right = right;
			Bottom = bottom;
			Left = left;
		}

		public Margins(float margin) : this(margin, margin, margin, margin) { }

		/*
		public float this[string i] {
			readonly get {
				i = i.ToLowerInvariant();
				if (i == "top") {
					return Top;
				}
				else if (i == "right") {
					return Right;
				}
				else if (i == "bottom") {
					return Bottom;
				}
				else if (i == "left") {
					return Left;
				}
				else {
					throw new FormatException("Unknown margin: \"" + i + "\"");
				}
			}
			set {
				i = i.ToLowerInvariant();
				if (i == "top") {
					Top = value;
				}
				else if (i == "right") {
					Right = value;
				}
				else if (i == "bottom") {
					Bottom = value;
				}
				else if (i == "left") {
					Left = value;
				}
				else {
					throw new FormatException("Unknown margin: \"" + i + "\"");
				}
			}
		}
		*/

		private static readonly Regex arrayPattern = new Regex(@"^(?:[\-\+]?[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:\s*\,\s*(?:[\-\+]?[0-9]+(?:\.[0-9]*)?|\.[0-9]+))*$");
		private static readonly Regex dictPattern = new Regex(@"^\{(?<match>[^\}]*)\}$");

		// TODO This should ideally be a generic method (i.e., for any dict-style initializer) inside SharpFactory
		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static Margins Parse(string str, float defaultValue, IFormatProvider? provider) {
			// TODO This whole method probably needs updating...?
			if (arrayPattern.IsMatch(str.Trim())) {
				float[] parts = str.Trim().SplitAndTrim(',').Select(s => float.Parse(s, provider)).ToArray();
				if (parts.Length == 1) {
					return new Margins(parts[0]);
				}
				else if (parts.Length == 2) {
					return new Margins(parts[0], parts[1], parts[0], parts[1]);
				}
				else if (parts.Length == 4) {
					return new Margins(parts[0], parts[1], parts[2], parts[3]);
				}
				else {
					throw new FormatException("Margins string badly formatted (comma-separated format must have 1, 2, or 4 values).");
				}
			}
			else if (dictPattern.Match(str.Trim()) is Match dictMatch && dictMatch.Success) {
				Dictionary<string, float> values = new Dictionary<string, float>();

				foreach (string[] parts in dictMatch.Groups[1].Value.SplitAndTrim(',').WhereNotEmpty().Select(s => s.SplitAndTrim(2, ':'))) {
					if (parts.Length != 2) {
						throw new FormatException("Margins string badly formatted.");
					}
					float value;
					try {
						value = float.Parse(parts[1], provider);
					}
					catch (FormatException e) {
						throw new FormatException($"\"{parts[1]}\" is not a number.", e);
					}
					string entryName = parts[0].ToLowerInvariant();
					values[entryName] = value;
				}

				float top = defaultValue, right = defaultValue, bottom = defaultValue, left = defaultValue;

				if (values.Remove("all", out float allValue)) {
					top = right = bottom = left = allValue;
				}
				if (values.Remove("vertical", out float verticalValue)) {
					top = bottom = verticalValue;
				}
				if (values.Remove("horizontal", out float horizontalValue)) {
					right = left = horizontalValue;
				}
				if (values.Remove("top", out float topValue)) {
					top = topValue;
				}
				if (values.Remove("right", out float rightValue)) {
					right = rightValue;
				}
				if (values.Remove("bottom", out float bottomValue)) {
					bottom = bottomValue;
				}
				if (values.Remove("left", out float leftValue)) {
					left = leftValue;
				}

				if(values.Count > 0) {
					throw new FormatException($"Unknown margin{(values.Count > 1 ? "s" : "")}: {string.Join(", ", values.Keys.Select(k => $"\"{k}\""))}");
				}

				return new Margins(top, right, bottom, left);
			}
			else {
				throw new FormatException("Margins string badly formatted.");
			}
		}

		public static Margins Parse(string str, IFormatProvider? provider) {
			return Parse(str, 0f, provider);
		}
		public static Margins Parse(string str, float defaultValue) {
			return Parse(str, defaultValue, null);
		}
		public static Margins Parse(string str) {
			return Parse(str, 0f, null);
		}

		public override string ToString() {
			return $"Margins(top: {Top}, right: {Right}, bottom: {Bottom}, left: {Left})";
		}

		public bool Equals(Margins other) {
			return this.Top == other.Top
				&& this.Right == other.Right
				&& this.Bottom == other.Bottom
				&& this.Left == other.Left;
		}

		public override bool Equals(object? obj) {
			return obj is Margins margins && Equals(margins);
		}

		public static bool operator ==(Margins left, Margins right) {
			return left.Equals(right);
		}

		public static bool operator !=(Margins left, Margins right) {
			return !left.Equals(right);
		}

		public override int GetHashCode() {
			return HashCode.Combine(Top, Right, Bottom, Left);
		}
	}
}
