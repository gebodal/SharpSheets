using SharpSheets.Canvas;
using SharpSheets.Layouts;
using System;
using System.Text.RegularExpressions;

namespace SharpSheets.Markup.Canvas {

	/// <summary>
	/// Indicates how a text chunk should be aligned, relative to the current start point
	/// of the text layout.
	/// </summary>
	public enum TextAnchor {
		/// <summary>
		/// Indicates that the start of the text should align with the start of the text layout.
		/// </summary>
		Start,
		/// <summary>
		/// Indicates that the middle of the text should align with the start of the text layout.
		/// </summary>
		Middle,
		/// <summary>
		/// Indicates that the end of the text should align with the start of the text layout.
		/// </summary>
		End
	}

	/*
	public enum TextBaseline { Top, Middle, Bottom }
	*/
	
	/// <summary>
	/// Indicates the area rule that should be used when converting a path into a 2D shape.
	/// </summary>
	public enum AreaRule {
		/// <summary>
		/// Indicates that the non-zero winding rule should be used to determine the area inside
		/// the shape.
		/// </summary>
		NonZero,
		/// <summary>
		/// Indicates that the even-odd winding rule should be used to determine the area inside
		/// the shape.
		/// </summary>
		EvenOdd
	}

	//public enum Overflow { Visible, Hidden }
	//public enum Units { UserSpace, ObjectBoundingBox }

	public enum Alignment { MID, MIN, MAX, NONE }
	public enum PreserveType { MEET, SLICE }

	public readonly struct PreserveAspectRatio {
		public readonly Alignment xAlignment;
		public readonly Alignment yAlignment;
		public readonly PreserveType meetSlice;

		public static PreserveAspectRatio None { get; } = new PreserveAspectRatio(Alignment.NONE, Alignment.NONE, PreserveType.MEET);
		public static PreserveAspectRatio Default { get; } = new PreserveAspectRatio(Alignment.MID, Alignment.MID, PreserveType.MEET);

		public PreserveAspectRatio(Alignment xAlignment, Alignment yAlignment, PreserveType meetSlice) {
			this.xAlignment = xAlignment;
			this.yAlignment = yAlignment;
			this.meetSlice = meetSlice;
		}

		public Transform GetTransform(float width, float height, Rectangle viewBox, out Rectangle finalViewArea) {

			Rectangle baseRect = new Rectangle(width, height);

			Transform transform = Transform.Translate(-viewBox.X, -viewBox.Y); // TODO Is this right?

			if (xAlignment == Alignment.NONE) {
				transform *= Transform.Scale(baseRect.Width / viewBox.Width, baseRect.Height / viewBox.Height);

				finalViewArea = new Rectangle(viewBox.Width, viewBox.Height);
			}
			else {
				float scale;
				if (meetSlice == PreserveType.MEET) {
					Rectangle viewBoxVisible = baseRect.Aspect(viewBox.AspectRatio);
					scale = Math.Min(viewBoxVisible.Width / viewBox.Width, viewBoxVisible.Height / viewBox.Height);
				}
				else { // meetSlice == PreserveType.SLICE
					Rectangle viewportCovered = baseRect.ContainAspect(viewBox.AspectRatio);
					scale = Math.Max(viewportCovered.Width / viewBox.Width, viewportCovered.Height / viewBox.Height);
				}
				transform *= Transform.Scale(scale, scale);
				Rectangle viewPort = new Rectangle(width / scale, height / scale);

				float xOffset;
				float yOffset;

				if (xAlignment == Alignment.MIN) {
					xOffset = viewPort.X - viewBox.X;
				}
				else if (xAlignment == Alignment.MID) {
					xOffset = viewPort.CentreX - viewBox.CentreX;
				}
				else { // xAlignment == Alignment.MAX
					xOffset = viewPort.Right - viewBox.Right;
				}

				if (yAlignment == Alignment.MIN) {
					yOffset = viewPort.Y - viewBox.Y;
				}
				else if (yAlignment == Alignment.MID) {
					yOffset = viewPort.CentreY - viewBox.CentreY;
				}
				else { // yAlignment == Alignment.MAX
					yOffset = viewPort.Top - viewBox.Top;
				}

				transform *= Transform.Translate(xOffset, yOffset);

				finalViewArea = new Rectangle(-xOffset, -yOffset, viewPort.Width, viewPort.Height);
			}

			return transform;
		}

		private static readonly Regex preserveAspectRatioRegex = new Regex(@"^(?:(?<none>none)|x(?<x>Min|Mid|Max)Y(?<y>Min|Mid|Max)(\s+(?<meetSlice>meet|slice)))$", RegexOptions.IgnoreCase);
		public static bool TryParse(string value, out PreserveAspectRatio result) {
			if (preserveAspectRatioRegex.Match(value.Trim()) is Match match && match.Success) {
				if (match.Groups["none"].Success) {
					result = PreserveAspectRatio.None;
				}
				else {
					static Canvas.Alignment ParseAlignment(string text) {
						if (text == "Min") { return Canvas.Alignment.MIN; }
						else if (text == "Mid") { return Canvas.Alignment.MID; }
						else { return Canvas.Alignment.MAX; } // text == "Max"
					}

					Canvas.Alignment x = ParseAlignment(match.Groups["x"].Value);
					Canvas.Alignment y = ParseAlignment(match.Groups["y"].Value);
					PreserveType preserve = PreserveType.MEET;
					if (match.Groups["meetSlice"].Value == "slice") {
						preserve = PreserveType.SLICE;
					}
					result = new PreserveAspectRatio(x, y, preserve);
				}
				return true;
			}
			else {
				result = default;
				return false;
			}
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static PreserveAspectRatio Parse(string value) {
			if (TryParse(value, out PreserveAspectRatio result)) {
				return result;
			}
			else {
				throw new FormatException("Invalid value for preserveAspectRatio");
			}
		}

		public static bool IsValid(string value) {
			return preserveAspectRatioRegex.IsMatch(value.Trim());
		}
	}

}
