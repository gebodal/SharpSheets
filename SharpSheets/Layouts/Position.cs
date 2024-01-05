using SharpSheets.Utilities;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpSheets.Layouts {

	/// <summary>
	/// Indicates a relative position within a rectangular area to use as an origin point for an offset.
	/// This will affect both the origin point, and the directions which are considered "increasing" when
	/// adding offsets to this origin point.
	/// </summary>
	public enum Anchor {
		/// <summary>
		/// Indicates that the origin point should be the centre of the area, with the increasing directions
		/// being rightwards and upwards.
		/// </summary>
		CENTRE,
		/// <summary>
		/// Indicates that the origin point should be at the top of the area and horizontally centred, with the
		/// increasing directions being rightwards and downwards.
		/// </summary>
		TOP,
		/// <summary>
		/// Indicates that the origin point should be at the top-right corner of the area, with the increasing
		/// directions being leftwards and downwards.
		/// </summary>
		TOPRIGHT,
		/// <summary>
		/// Indicates that the origin point should be at the right of the area and vertically centred, with the
		/// increasing directions leftwards and upwards.
		/// </summary>
		RIGHT,
		/// <summary>
		/// Indicates that the origin point should be at the bottom-right corner of the area, with the increasing
		/// directions being leftwards and upwards.
		/// </summary>
		BOTTOMRIGHT,
		/// <summary>
		/// Indicates that the origin point should be at the bottom of the area and horizontally centred, with the
		/// increasing directions being rightwards and upwards.
		/// </summary>
		BOTTOM,
		/// <summary>
		/// Indicates that the origin point should be at the bottom-left corner of the area, with the increasing
		/// directions being rightwards and upwards.
		/// </summary>
		BOTTOMLEFT,
		/// <summary>
		/// Indicates that the origin point should be at the left of the area and vertically centred, with the
		/// increasing directions being rightwards and upwards.
		/// </summary>
		LEFT,
		/// <summary>
		/// Indicates that the origin point should be at the top-left corner of the area, with the increasing
		/// directions being rightwards and upwards.
		/// </summary>
		TOPLEFT
	}

	public static class AnchorExtensions {

		public static bool IsTop(this Anchor anchor) {
			return anchor == Anchor.TOP || anchor == Anchor.TOPLEFT || anchor == Anchor.TOPRIGHT;
		}

		public static bool IsBottom(this Anchor anchor) {
			return anchor == Anchor.BOTTOM || anchor == Anchor.BOTTOMLEFT || anchor == Anchor.BOTTOMRIGHT;
		}

		public static bool IsLeft(this Anchor anchor) {
			return anchor == Anchor.LEFT || anchor == Anchor.TOPLEFT || anchor == Anchor.BOTTOMLEFT;
		}

		public static bool IsRight(this Anchor anchor) {
			return anchor == Anchor.RIGHT || anchor == Anchor.TOPRIGHT || anchor == Anchor.BOTTOMRIGHT;
		}

		public static bool IsVerticallyCentered(this Anchor anchor) {
			return anchor == Anchor.LEFT || anchor == Anchor.CENTRE || anchor == Anchor.RIGHT;
		}

		public static bool IsHorizontallyCentered(this Anchor anchor) {
			return anchor == Anchor.TOP || anchor == Anchor.CENTRE || anchor == Anchor.BOTTOM;
		}

	}

	public readonly struct Position {
		public Anchor Anchor { get; }
		public Dimension X { get; }
		public Dimension Y { get; }
		public Dimension Width { get; }
		public Dimension Height { get; }
		public Position(Anchor anchor, Dimension x, Dimension y, Dimension width, Dimension height) {
			this.Anchor = anchor;
			this.X = x;
			this.Y = y;
			this.Width = width;
			this.Height = height;
		}

		private static readonly Regex pattern = new Regex(@"\{?(?<match>[^\}]*)\}?");

		// TODO This should ideally be a generic method (i.e., for any dict-style initializer) inside SharpFactory
		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static Position Parse(string str, IFormatProvider? provider) {
			Anchor anchor = Anchor.BOTTOMLEFT;
			Dimension x = Dimension.FromPoints(0), y = Dimension.FromPoints(0), width = Dimension.FromPercent(100), height = Dimension.FromPercent(100);
			foreach (string[] parts in pattern.Match(str).Groups[1].Value.SplitAndTrim(',').WhereNotEmpty().Select(s => s.SplitAndTrim(2, ':'))) {
				if (parts.Length != 2) {
					throw new FormatException("Position string badly formatted.");
				}
				string name = parts[0].ToLowerInvariant();
				if (name == "anchor") {
					anchor = EnumUtils.ParseEnum<Anchor>(parts[1]);
				}
				else {
					Dimension? value;
					try {
						value = Dimension.ParseAbsoluteOrPercent(parts[1], provider);
					}
					catch(FormatException e) {
						throw new FormatException($"\"{parts[1]}\" is not a valid Dimension.", e);
					}
					if (value.HasValue) {
						if (name == "x") { x = value.Value; }
						else if (name == "y") { y = value.Value; }
						else if (name == "width") { width = value.Value; }
						else if (name == "height") { height = value.Value; }
						else { throw new FormatException($"Unrecognized key for Position string: \"{parts[0]}\""); }
					}
					else {
						throw new FormatException($"Error parsing entry \"{parts[0]}: {parts[1]}\"");
					}
				}
			}
			return new Position(anchor, x, y, width, height);
		}

		private float GetLength(Dimension length, float available) {
			//if(length.Absolute == 0 && length.Percent == 0 && length.Relative > 0) { return available; }
			return length.Absolute + available * length.Percent;
		}

		public Rectangle GetFrom(Rectangle rect) {
			float insetX = GetLength(X, rect.Width);
			float insetY = GetLength(Y, rect.Height);
			float insetWidth = GetLength(Width, rect.Width);
			float insetHeight = GetLength(Height, rect.Height);

			if (Anchor == Anchor.CENTRE) {
				return Rectangle.RectangleAt(rect.CentreX + insetX, rect.CentreY + insetY, insetWidth, insetHeight);
			}

			float posX, posY;

			if (Anchor.IsBottom()) {
				posY = rect.Bottom;
			}
			else if (Anchor.IsTop()) {
				posY = rect.Top - insetHeight;
			}
			else {
				posY = rect.CentreY - insetHeight / 2;
			}

			if (Anchor.IsLeft()) {
				posX = rect.Left;
			}
			else if (Anchor.IsRight()) {
				posX = rect.Right - insetWidth;
			}
			else {
				posX = rect.CentreX - insetWidth / 2;
			}

			return new Rectangle(posX + insetX, posY + insetY, insetWidth, insetHeight);
		}
	}

}
