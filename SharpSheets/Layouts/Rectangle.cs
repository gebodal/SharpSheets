using SharpSheets.Canvas;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.RegularExpressions;

namespace SharpSheets.Layouts {

	public class Rectangle : IEquatable<Rectangle> {

		public float X { get; }
		public float Y { get; }
		public float Width { get; }
		public float Height { get; }

		public float Left { get { return X; } }
		public float Right { get { return X + Width; } }
		public float Bottom { get { return Y; } }
		public float Top { get { return Y + Height; } }

		public float CentreX { get { return X + Width / 2; } }
		public float CentreY { get { return Y + Height / 2; } }

		public float Area { get { return Width * Height; } }
		public float AspectRatio { get { return Width / Height; } }

		public bool HasArea { get { return Width > 0 && Height > 0; } }

		public Rectangle(float x, float y, float width, float height) {
			X = x;
			Y = y;
			Width = width;
			Height = height;
		}

		public Rectangle(float width, float height) : this(0f, 0f, width, height) { }

		[return: NotNullIfNotNull(nameof(size))]
		public static explicit operator Rectangle?(Size? size) {
			if (size == null) return null;
			return new Rectangle(0f, 0f, size.Width, size.Height);
		}

		public Rectangle Clone() { // protected?
			return new Rectangle(X, Y, Width, Height);
		}

		public float RelX(float x) {
			return X + Width * x;
		}
		public float RelY(float y) {
			return Y + Height * y;
		}

		public Rectangle Margins(float topIndent, float rightIndent, float bottomIndent, float leftIndent, bool reverse) {
			float x = X + leftIndent * (reverse ? -1 : 1);
			float width = Width - (leftIndent + rightIndent) * (reverse ? -1 : 1);
			float y = Y + bottomIndent * (reverse ? -1 : 1);
			float height = Height - (topIndent + bottomIndent) * (reverse ? -1 : 1);
			return new Rectangle(x, y, width, height);
		}

		public Rectangle Margins(float indent, bool reverse) {
			return Margins(indent, indent, indent, indent, reverse);
		}

		public Rectangle Margins(float vertical, float horizontal, bool reverse) {
			return Margins(vertical, horizontal, vertical, horizontal, reverse);
		}

		public Rectangle Margins(Margins margins, bool reverse) {
			return Margins(margins.Top, margins.Right, margins.Bottom, margins.Left, reverse);
		}

		public Rectangle MarginsRel(float topIndent, float rightIndent, float bottomIndent, float leftIndent, bool reverse) {
			if (reverse) {
				return Margins(Height / topIndent, Width / rightIndent, Height / bottomIndent, Width / leftIndent, true);
			}
			else {
				return Margins(Height * topIndent, Width * rightIndent, Height * bottomIndent, Width * leftIndent, false);
				//return Margins(topIndent * Height, rightIndent * Width, bottomIndent * Height, leftIndent * Width, false);
			}
		}

		public Rectangle MarginsRel(float indent, bool reverse) {
			return MarginsRel(indent, indent, indent, indent, reverse);
		}

		public Rectangle MarginsRel(float vertical, float horizontal, bool reverse) {
			return MarginsRel(vertical, horizontal, vertical, horizontal, reverse);
		}

		public Rectangle Offset(float x, float y) {
			return new Rectangle(this.X + x, this.Y + y, this.Width, this.Height);
		}

		public Rectangle Offset(Utilities.Vector offset) {
			return new Rectangle(this.X + offset.X, this.Y + offset.Y, this.Width, this.Height);
		}

		public Rectangle Aspect(float aspect) {
			if (aspect <= 0) {
				return Clone();
			}
			float initialAspect = Width / Height;
			if (initialAspect > aspect) { // Rectangle is too wide
				return RectangleAt(CentreX, CentreY, Height * aspect, Height);
			}
			else { // Rectangle is too tall
				return RectangleAt(CentreX, CentreY, Width, Width / aspect);
			}
		}

		// The inverse of Aspect. This function returns the rect with the desired aspect that fully contains this rect.
		public Rectangle ContainAspect(float aspect) {
			if(aspect <= 0) {
				return Clone();
			}
			float initialAspect = Width / Height;
			if(initialAspect > aspect) { // Rectangle is wider than desired aspect, need to increase height
				return RectangleAt(CentreX, CentreY, Width, Width / aspect);
			}
			else { // Rectangle is taller than desired aspect, need to increase width
				return RectangleAt(CentreX, CentreY, Height * aspect, Height);
			}
		}

		public bool Contains(float x, float y) {
			return x >= Left && x <= Right && y >= Bottom && y <= Top;
		}

		public bool Contains(Rectangle other) {
			return Left <= other.Left && Right >= other.Right && Top >= other.Top && Bottom <= other.Bottom;
		}

		public static Rectangle RectangleAt(float x, float y, float width, float height) {
			return new Rectangle(x - width / 2, y - height / 2, width, height);
		}

		public static Rectangle RectangleFromBounding(float left, float bottom, float right, float top) {
			float leftf = Math.Min(left, right);
			float rightf = Math.Max(left, right);
			float bottomf = Math.Min(bottom, top);
			float topf = Math.Max(bottom, top);
			return new Rectangle(leftf, bottomf, rightf - leftf, topf - bottomf);
		}

		public static Rectangle Union(Rectangle r1, Rectangle r2) {
			/*
			float x = Math.Min(r1.X, r2.X);
			float y = Math.Min(r1.Y, r2.Y);
			float width = Math.Max(r1.X + r1.Width, r2.X + r2.Width) - x;
			float height = Math.Max(r1.Y + r1.Height, r2.Y + r2.Height) - y;
			return new Rectangle(x, y, width, height);
			*/
			float left = Math.Min(r1.Left, r2.Left);
			float bottom = Math.Min(r1.Bottom, r2.Bottom);
			float right = Math.Max(r1.Right, r2.Right);
			float top = Math.Max(r1.Top, r2.Top);
			return RectangleFromBounding(left, bottom, right, top);
		}

		public static Rectangle Intersection(Rectangle r1, Rectangle r2) {
			float x = Math.Max(r1.X, r2.X);
			float y = Math.Max(r1.Y, r2.Y);
			float width = Math.Min(r1.X + r1.Width, r2.X + r2.Width) - x;
			float height = Math.Min(r1.Y + r1.Height, r2.Y + r2.Height) - y;
			return new Rectangle(x, y, width, height);
		}

		/// <summary>Calculates the common rectangle which includes all the input rectangles.</summary>
		/// <param name="rectangles">Input rectangles.</param>
		/// <returns>Common rectangle.</returns>
		public static Rectangle GetCommonRectangle(IEnumerable<Rectangle> rectangles) {
			float ury = -float.MaxValue;
			float llx = float.MaxValue;
			float lly = float.MaxValue;
			float urx = -float.MaxValue;
			foreach (Rectangle rectangle in rectangles) {
				if (rectangle is null) {
					continue;
				}
				Rectangle rec = rectangle.Clone();
				if (rec.Y < lly) {
					lly = rec.Y;
				}
				if (rec.X < llx) {
					llx = rec.X;
				}
				if (rec.Y + rec.Height > ury) {
					ury = rec.Y + rec.Height;
				}
				if (rec.X + rec.Width > urx) {
					urx = rec.X + rec.Width;
				}
			}
			return new Rectangle(llx, lly, urx - llx, ury - lly);
		}

		public Rectangle Include(DrawPoint point) {
			float x = Math.Min(X, point.X);
			float y = Math.Min(Y, point.Y);
			float width = Math.Max(X + Width, point.X) - x;
			float height = Math.Max(Y + Height, point.Y) - y;
			return new Rectangle(x, y, width, height);
		}

		/*
		public static Rectangle GetCommonRectangle(params Rectangle[] rectangles) {
			return GetCommonRectangle(rectangles);
		}
		*/

		public static float CalculateAspectRatio(float width, float height) {
			return width / height;
		}

		public override string ToString() {
			return $"Rectangle({X}, {Y}, {Width}, {Height})";
		}

		public static bool Equals(Rectangle? a, Rectangle? b) {
			if (a is null && b is null) { return true; }
			else if(a is null || b is null) { return false; }
			
			return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
		}

		public bool Equals(Rectangle? other) {
			return Equals(this, other);
		}

		public override bool Equals(object? obj) {
			return Equals(this, obj as Rectangle);
		}

		public static bool operator ==(Rectangle? a, Rectangle? b) => Equals(a, b);
		public static bool operator !=(Rectangle? a, Rectangle? b) => !Equals(a, b);

		public override int GetHashCode() {
			return HashCode.Combine(X, Y, Width, Height);
		}
	}

	public class Size : IEquatable<Size> {

		public float Width { get; }
		public float Height { get; }

		public float Area { get { return Width * Height; } }
		public float AspectRatio { get { return Width / Height; } }

		public bool HasArea { get { return Width > 0 && Height > 0; } }

		public Size(float width, float height) {
			Width = width;
			Height = height;
		}

		[return: NotNullIfNotNull(nameof(rect))]
		public static explicit operator Size?(Rectangle? rect) {
			if (rect is null) return null;
			return new Size(rect.Width, rect.Height);
		}

		public Rectangle AsRectangle() {
			return new Rectangle(Width, Height);
		}

		protected Size Clone() {
			return new Size(Width, Height);
		}

		/*
		public float RelX(float x) {
			return Width * x;
		}
		public float RelY(float y) {
			return Height * y;
		}
		*/

		public Size Margins(float topIndent, float rightIndent, float bottomIndent, float leftIndent, bool reverse) {
			float width = Width - (leftIndent + rightIndent) * (reverse ? -1 : 1);
			float height = Height - (topIndent + bottomIndent) * (reverse ? -1 : 1);
			return new Size(width, height);
		}

		public Size Margins(float indent, bool reverse) {
			return Margins(indent, indent, indent, indent, reverse);
		}

		public Size Margins(float vertical, float horizontal, bool reverse) {
			return Margins(vertical, horizontal, vertical, horizontal, reverse);
		}

		public Size Margins(Margins margins, bool reverse) {
			return Margins(margins.Top, margins.Right, margins.Bottom, margins.Left, reverse);
		}

		public Size MarginsRel(float topIndent, float rightIndent, float bottomIndent, float leftIndent, bool reverse) {
			if (reverse) {
				return Margins(Height / topIndent, Width / rightIndent, Height / bottomIndent, Width / leftIndent, true);
			}
			else {
				return Margins(Height * topIndent, Width * rightIndent, Height * bottomIndent, Width * leftIndent, false);
				//return Margins(topIndent * Height, rightIndent * Width, bottomIndent * Height, leftIndent * Width, false);
			}
		}

		public Size MarginsRel(float indent, bool reverse) {
			return MarginsRel(indent, indent, indent, indent, reverse);
		}

		public Size MarginsRel(float vertical, float horizontal, bool reverse) {
			return MarginsRel(vertical, horizontal, vertical, horizontal, reverse);
		}

		public Size Aspect(float aspect) {
			if (aspect <= 0) {
				return Clone();
			}
			float initialAspect = Width / Height;
			if (initialAspect > aspect) { // Area is too wide
				return new Size(Height * aspect, Height);
			}
			else { // Area is too tall
				return new Size(Width, Width / aspect);
			}
		}

		// The inverse of Aspect. This function returns the area with the desired aspect that fully contains this area.
		public Size ContainAspect(float aspect) {
			if (aspect <= 0) {
				return Clone();
			}
			float initialAspect = Width / Height;
			if (initialAspect > aspect) { // Area is wider than desired aspect, need to increase height
				return new Size(Width, Width / aspect);
			}
			else { // Area is taller than desired aspect, need to increase width
				return new Size(Height * aspect, Height);
			}
		}

		public static Size Combine(Size s1, Size s2) {
			float width = Math.Max(s1.Width, s2.Width) ;
			float height = Math.Max(s1.Height, s2.Height);
			return new Size(width, height);
		}

		public static float CalculateAspectRatio(float width, float height) {
			return width / height;
		}

		public override string ToString() {
			return $"Size({Width}, {Height})";
		}

		public static bool Equals(Size? a, Size? b) {
			if (a is null && b is null) { return true; }
			else if (a is null || b is null) { return false; }

			return a.Width == b.Width && a.Height == b.Height;
		}

		public bool Equals(Size? other) {
			return Equals(this, other);
		}

		public override bool Equals(object? obj) {
			return Equals(this, obj as Size);
		}

		public static bool operator ==(Size? a, Size? b) => Equals(a, b);
		public static bool operator !=(Size? a, Size? b) => !Equals(a, b);

		public override int GetHashCode() {
			return HashCode.Combine(Width, Height);
		}
	}

	public class PageSize : Size {
		public PageSize(float width, float height) : base(width, height) { }

		public PageSize Rotate() {
			return new PageSize(Height, Width);
		}

		public static readonly PageSize A0 = new PageSize(2384, 3370);
		public static readonly PageSize A1 = new PageSize(1684, 2384);
		public static readonly PageSize A2 = new PageSize(1190, 1684);
		public static readonly PageSize A3 = new PageSize(842, 1190);
		public static readonly PageSize A4 = new PageSize(595, 842);
		public static readonly PageSize A5 = new PageSize(420, 595);
		public static readonly PageSize A6 = new PageSize(298, 420);
		public static readonly PageSize A7 = new PageSize(210, 298);
		public static readonly PageSize A8 = new PageSize(148, 210);
		public static readonly PageSize A9 = new PageSize(105, 547);
		public static readonly PageSize A10 = new PageSize(74, 105);
		public static readonly PageSize B0 = new PageSize(2834, 4008);
		public static readonly PageSize B1 = new PageSize(2004, 2834);
		public static readonly PageSize B2 = new PageSize(1417, 2004);
		public static readonly PageSize B3 = new PageSize(1000, 1417);
		public static readonly PageSize B4 = new PageSize(708, 1000);
		public static readonly PageSize B5 = new PageSize(498, 708);
		public static readonly PageSize B6 = new PageSize(354, 498);
		public static readonly PageSize B7 = new PageSize(249, 354);
		public static readonly PageSize B8 = new PageSize(175, 249);
		public static readonly PageSize B9 = new PageSize(124, 175);
		public static readonly PageSize B10 = new PageSize(88, 124);
		public static readonly PageSize LETTER = new PageSize(612, 792);
		public static readonly PageSize LEGAL = new PageSize(612, 1008);
		public static readonly PageSize TABLOID = new PageSize(792, 1224);
		public static readonly PageSize LEDGER = new PageSize(1224, 792);
		public static readonly PageSize EXECUTIVE = new PageSize(522, 756);

		private static readonly Regex pageSizeRegex = new Regex(@"^(?<width>[0-9]+(\.[0-9]+)?|\.[0-9]+) \s* x \s* (?<height>[0-9]+(\.[0-9]+)?|\.[0-9]+) (\s* (?<unit>pt|in|cm|mm))?$", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static PageSize ParsePageSize(string sizeStr, IFormatProvider? provider) {
			string size = sizeStr.ToLower();
			if (size.StartsWith("a")) {
				if (size == "a0") { return PageSize.A0; }
				else if (size == "a1") { return PageSize.A1; }
				else if (size == "a2") { return PageSize.A2; }
				else if (size == "a3") { return PageSize.A3; }
				else if (size == "a4") { return PageSize.A4; }
				else if (size == "a5") { return PageSize.A5; }
				else if (size == "a6") { return PageSize.A6; }
				else if (size == "a7") { return PageSize.A7; }
				else if (size == "a8") { return PageSize.A8; }
				else if (size == "a9") { return PageSize.A9; }
				else if (size == "a10") { return PageSize.A10; }
				else { throw new FormatException(string.Format("Size \"{0}\" not supported (use sizes A0-A10).", sizeStr)); }
			}
			else if (size.StartsWith("b")) {
				if (size == "b0") { return PageSize.B0; }
				else if (size == "b1") { return PageSize.B1; }
				else if (size == "b2") { return PageSize.B2; }
				else if (size == "b3") { return PageSize.B3; }
				else if (size == "b4") { return PageSize.B4; }
				else if (size == "b5") { return PageSize.B5; }
				else if (size == "b6") { return PageSize.B6; }
				else if (size == "b7") { return PageSize.B7; }
				else if (size == "b8") { return PageSize.B8; }
				else if (size == "b9") { return PageSize.B9; }
				else if (size == "b10") { return PageSize.B10; }
				else { throw new FormatException(string.Format("Size \"{0}\" not supported (use sizes B0-B10).", sizeStr)); }
			}
			else if (size == "letter") { return PageSize.LETTER; }
			else if (size == "legal") { return PageSize.LEGAL; }
			else if (size == "ledger") { return PageSize.LEDGER; }
			else if (size == "tabloid") { return PageSize.TABLOID; }
			else if (size == "executive") { return PageSize.EXECUTIVE; }
			else if (pageSizeRegex.Match(size) is Match match) {
				float width = float.Parse(match.Groups["width"].Value, provider);
				float height = float.Parse(match.Groups["height"].Value, provider);
				string unit = match.Groups["unit"].Value.ToLowerInvariant();
				if (!string.IsNullOrWhiteSpace(unit)) {
					if (unit == "in") {
						width = Dimension.InchToPoint(width);
						height = Dimension.InchToPoint(height);
					}
					else if (unit == "cm") {
						width = Dimension.CentimetreToPoint(width);
						height = Dimension.CentimetreToPoint(height);
					}
					else if (unit == "mm") {
						width = Dimension.MillimetreToPoint(width);
						height = Dimension.MillimetreToPoint(height);
					}
				}
				return new PageSize(width, height);
			}
			else { throw new FormatException(string.Format("\"{0}\" is not a valid page size.", sizeStr)); }
		}

		public static PageSize ParsePageSize(string sizeStr) {
			return ParsePageSize(sizeStr, null);
		}

		public override string ToString() {
			return ToString(null);
		}

		public string ToString(IFormatProvider? formatProvider) {
			return $"{Width.ToString(formatProvider)}x{Height.ToString(formatProvider)}pt";
		}
	}
}
