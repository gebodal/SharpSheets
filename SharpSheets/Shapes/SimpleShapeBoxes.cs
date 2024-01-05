using SharpSheets.Utilities;
using System;
using SharpSheets.Layouts;
using SharpSheets.Canvas;
using SharpSheets.Colors;

namespace SharpSheets.Shapes {

	public abstract class ShapeBox : BoxBase {
		protected readonly Color? stroke;
		protected readonly Color? fill;
		protected readonly float[]? dashes;
		protected readonly float dashOffset;
		protected readonly Margins trim;

		protected bool HasGraphicsChanges { get { return fill.HasValue || stroke.HasValue || (dashes != null && dashes.Length > 0); } }

		public ShapeBox(float aspect, Color? stroke, Color? fill, float[]? dashes, float dashOffset, Margins trim) : base(aspect) {
			this.stroke = stroke;
			this.fill = fill;
			this.dashes = dashes;
			this.dashOffset = dashOffset;
			this.trim = trim;
		}

		protected sealed override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			if (HasGraphicsChanges) {
				canvas.SaveState();
				if (fill.HasValue) {
					canvas.SetFillColor(fill.Value);
				}
				if (stroke.HasValue) {
					canvas.SetStrokeColor(stroke.Value);
				}
				if (dashes != null && dashes.Length > 0) {
					canvas.SetStrokeDash(new StrokeDash(dashes, dashOffset));
				}
			}

			DrawShape(canvas, rect);

			if (HasGraphicsChanges) {
				canvas.RestoreState();
			}
		}

		protected abstract void DrawShape(ISharpCanvas canvas, Rectangle rect);

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.Margins(graphicsState.GetLineWidth() / 2, false).Margins(trim, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.Margins(trim, true).Margins(graphicsState.GetLineWidth() / 2, true);
		}
	}

	/// <summary>
	/// A simple rectangular box. The fill and stroke colors may be specified, an internal padding
	/// specified for the remaining area, and the outline may be drawn using a dashed line.
	/// </summary>
	public class Simple : ShapeBox {

		/// <summary>
		/// Constructor for Simple.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="trim">Padding to apply to the remaining area.</param>
		public Simple(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, Margins trim = default) : base(aspect, stroke, fill, dashes, dashOffset, trim) { }

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.Rectangle(rect).FillStroke();
		}
	}

	/// <summary>
	/// A simple bevelled rectangular box. The fill and stroke colors may be specified, an internal
	/// padding specified for the remaining area, and the outline may be drawn using a dashed line.
	/// </summary>
	public class Bevelled : ShapeBox {

		protected readonly float bevel;

		/// <summary>
		/// Constructor for Bevelled.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="bevel">The bevel size for the rectangle. If the bevel is larger
		/// than min(width, height)/2, then the smaller value will be used.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="trim">Padding to apply to the remaining area. If no value is provided,
		/// a padding of 0.35 times the <paramref name="bevel"/> will be used.</param>
		public Bevelled(float aspect, float bevel = 5f, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, Margins? trim = null) : base(aspect, stroke, fill, dashes, dashOffset, trim ?? new Margins(0.35f * bevel)) {
			this.bevel = bevel;
		}

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.BevelledRectangle(rect, Math.Min(bevel, Math.Min(rect.Height, rect.Height) / 2)).FillStroke();
		}
	}

	/// <summary>
	/// A simple rounded rectangular box. The fill and stroke colors may be specified, an internal
	/// padding specified for the remaining area, and the outline may be drawn using a dashed line.
	/// </summary>
	public class Rounded : ShapeBox {

		protected readonly float radius;

		/// <summary>
		/// Constructor for Rounded.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="radius">The corner radius for the rectangle. If the radius is larger
		/// than min(width, height)/2, then the smaller value will be used.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="trim">Padding to apply to the remaining area. If no value is provided,
		/// a padding of 0.5 times the <paramref name="radius"/> will be used.</param>
		public Rounded(float aspect, float radius = 5f, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, Margins? trim = null) : base(aspect, stroke, fill, dashes, dashOffset, trim ?? new Margins(0.5f * radius)) {
			this.radius = radius;
		}

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.RoundRectangle(rect, Math.Min(radius, Math.Min(rect.Height, rect.Height) / 2)).FillStroke();
		}
	}

	/// <summary>
	/// A simple tablet-shaped box, where the sortest sides are semi-circles, connected by straight
	/// lines to form the longer sides. The fill and stroke colors may be specified, an internal
	/// padding specified for the remaining area, and the outline may be drawn using a dashed line.
	/// </summary>
	public class Tablet : ShapeBox {

		protected readonly float bevel;

		/// <summary>
		/// Constructor for Tablet.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="trim">Padding to apply to the remaining area.</param>
		public Tablet(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, Margins trim = default) : base(aspect, stroke, fill, dashes, dashOffset, trim) { }

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.RoundRectangle(rect, Math.Min(rect.Width, rect.Height) / 2).FillStroke();
		}
	}

	/// <summary>
	/// A simple ellipse-shaped box. The fill and stroke colors may be specified, an internal
	/// padding specified for the remaining area, and the outline may be drawn using a dashed
	/// line.
	/// </summary>
	public class Ellipse : ShapeBox {

		/// <summary>
		/// Constructor for Ellipse.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="trim">Padding to apply to the remaining area.</param>
		public Ellipse(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, Margins trim = default) : base(aspect, stroke, fill, dashes, dashOffset, trim) { }

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.EllipseFrom(rect).FillStroke();
		}
	}

	/// <summary>
	/// A simple circle-shaped box. Any aspect provided will be applied to the initial shape
	/// area, and then a circle will be drawn inside that area. The fill and stroke colors may
	/// be specified, along with an internal padding specified for the remaining area, and the
	/// outline may be drawn using a dashed line.
	/// </summary>
	public class Circle : Ellipse {

		/// <summary>
		/// Constructor for Circle.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="trim">Padding to apply to the remaining area.</param>
		public Circle(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, Margins trim = default) : base(aspect, stroke, fill, dashes, dashOffset, trim) { }

		public override Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return base.AspectRect(graphicsState, rect).Aspect(1f);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			// This is here to deal with the fact that this incoming rect may be an abstract representation of a space
			float width = rect.Width > 0 ? rect.Width : rect.Height;
			float height = rect.Height > 0 ? rect.Height : rect.Width;

			float reducedDiameter = Math.Min(width, height);
			Rectangle aspectRect = Rectangle.RectangleAt(rect.CentreX, rect.CentreY, reducedDiameter, reducedDiameter);
			return base.FullRect(graphicsState, aspectRect);
		}
	}

	/// <summary>
	/// A simple diamond-shaped box, where the midpoint of each side is connected by straight lines
	/// to the two adjoining side midpoints. The fill and stroke colors may be specified, an internal
	/// padding specified for the remaining area, and the outline may be drawn using a dashed line.
	/// </summary>
	public class Diamond : ShapeBox {

		/// <summary>
		/// Constructor for Diamond.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="trim">Padding to apply to the remaining area.</param>
		public Diamond(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, Margins trim = default) : base(aspect, stroke, fill, dashes, dashOffset, trim) { }

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.MoveToRel(rect, 0.5f, 1f)
				.LineToRel(rect, 1f, 0.5f)
				.LineToRel(rect, 0.5f, 0f)
				.LineToRel(rect, 0f, 0.5f)
				.ClosePath();
			canvas.FillStroke();
		}
	}

	/*
	/// <summary>
	/// A shape comprising of a circle with a given number of protruding points,
	/// where the number of points, and the width and length of the points can
	/// be specified.
	/// </summary>
	public class PointedCircle : ShapeBox {

		// TODO Should this be removed?

		protected readonly int points;
		protected readonly double rotation;
		protected readonly double width;
		protected readonly float length;
		protected readonly bool includePoints;

		/// <summary>
		/// Constructor for PointedCircle.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this shape. Note that the eventual shape will always be a circle (aspect = 1),
		/// and so the circle will be drawn inside a rectangle created using this aspect value.</param>
		/// <param name="stroke">Stroke color for this shape.</param>
		/// <param name="fill">Fill color for this shape.</param>
		/// <param name="trim">Internal trim for the remaining rectangle inside this shape, specified in points.</param>
		/// <param name="points">The number of points to be drawn along the perimiter of the circle.</param>
		/// <param name="rotation">An offset rotation for the points around this circle (in degrees).</param>
		/// <param name="width">The width of the base of the points, in degrees.</param>
		/// <param name="length">The length of the points, as a factor of the circle radius.</param>
		/// <param name="includePoints">Flag to indicate that the length of the points should be included
		/// when calculating the extent of this shape.</param>
		public PointedCircle(float aspect, Color? stroke = null, Color? fill = null, float trim = 0f, int points = 4, double rotation = 0f, double width = 10f, float length = 0.1f, bool includePoints = false) : base(aspect, stroke, fill, null, 0f, trim) {
			this.points = points;
			this.rotation = MathUtils.Deg2Rad(rotation);
			this.width = MathUtils.Deg2Rad(width);
			this.length = length;
			this.includePoints = includePoints;
		}

		protected struct Point {
			public float x;
			public float y;
			public Point(float x, float y) {
				this.x = x;
				this.y = y;
			}
		}

		protected static Point GetPoint(Rectangle rect, float radius, double rotation, double radiusOffset = 0f) {
			double x = rect.CentreX + (radius + radiusOffset) * Math.Cos(rotation);
			double y = rect.CentreY + (radius + radiusOffset) * Math.Sin(rotation);
			return new Point((float)x, (float)y);
		}

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			if (includePoints) {
				rect = rect.Aspect(1f).MarginsRel(length / 2, false);
			}

			float radius = Math.Min(rect.Width, rect.Height) / 2;
			double stepSize = 2 * Math.PI / points;
			double pointWidth = Math.Min(this.width, stepSize);

			double currentRotation = -this.rotation + Math.PI / 2 + pointWidth / 2;
			Point start = GetPoint(rect, radius, currentRotation);
			canvas.MoveTo(start.x, start.y);

			for (int i = 0; i < points; i++) {
				currentRotation += stepSize;
				Point nextStart = GetPoint(rect, radius, currentRotation - pointWidth);
				Point nextMid = GetPoint(rect, radius, currentRotation - pointWidth / 2, radius * length);
				Point nextEnd = GetPoint(rect, radius, currentRotation);
				canvas.ArcTo(nextStart.x, nextStart.y, radius, false, true);
				canvas.LineTo(nextMid.x, nextMid.y).LineTo(nextEnd.x, nextEnd.y);
			}

			canvas.ClosePath();

			canvas.FillStroke();
		}

		public override Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return base.AspectRect(graphicsState, rect).Aspect(1f);
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			if (includePoints) {
				rect = rect.Aspect(1f).MarginsRel(length / 2, false);
			}
			return base.GetRemainingRect(graphicsState, rect);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			float reducedDiameter = Math.Min(rect.Width, rect.Height);
			Rectangle aspectRect = Rectangle.RectangleAt(rect.CentreX, rect.CentreY, reducedDiameter, reducedDiameter);
			Rectangle baseFullRect = base.FullRect(graphicsState, aspectRect);
			if (includePoints) {
				return baseFullRect.MarginsRel(length / 2, true);
			}
			else {
				return baseFullRect;
			}
		}
	}
	*/
}