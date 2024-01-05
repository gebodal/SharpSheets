using SharpSheets.Evaluations;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Markup.Canvas;

namespace SharpSheets.Markup.Elements {

	/// <summary>
	/// This element draws a circle at a specified location with a specific radius.
	/// </summary>
	public class Circle : ShapeElement {
		readonly DrawPointExpression centre;
		readonly FloatExpression radius;

		/// <summary>
		/// Constructor for Circle.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_cx" default="0">The x coordinate of the circle centre.</param>
		/// <param name="_cy" default="0">The y coordinate of the circle centre.</param>
		/// <param name="_r" default="0">The radius of the circle.</param>
		/// <exception cref="EvaluationException"></exception>
		public Circle(string? _id, StyleSheet styleSheet, XLengthExpression _cx, YLengthExpression _cy, FloatExpression _r) : base(_id, styleSheet) {
			this.centre = new DrawPointExpression(_cx, _cy);
			this.radius = _r;
		}

		protected override void DoAssignGeometry(MarkupCanvas canvas) {
			canvas.CircleAt(centre, radius);
		}

		public override IPathCalculator GetPath(MarkupCanvas canvas) {
			DrawPoint centre = canvas.TransformPoint(this.centre);
			float radius = canvas.TransformLength(this.radius);
			return new CirclePathCalculator(centre, radius);
		}
	}

	/// <summary>
	/// This element draws an ellipse at a specified location with specific x and y radii.
	/// </summary>
	public class Ellipse : ShapeElement {
		readonly DrawPointExpression centre;
		readonly FloatExpression rx;
		readonly FloatExpression ry;

		/// <summary>
		/// Constructor for Ellipse.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_cx" default="0">The x coordinate of the ellipse centre.</param>
		/// <param name="_cy" default="0">The y coordinate of the ellipse centre.</param>
		/// <param name="_rx" default="0">The radius of the ellipse on the x axis.</param>
		/// <param name="_ry" default="0">The radius of the ellipse on the y axis</param>
		/// <exception cref="EvaluationException"></exception>
		public Ellipse(string? _id, StyleSheet styleSheet, XLengthExpression _cx, YLengthExpression _cy, FloatExpression _rx, FloatExpression _ry) : base(_id, styleSheet) {
			this.centre = new DrawPointExpression(_cx, _cy);
			this.rx = _rx;
			this.ry = _ry;
		}

		protected override void DoAssignGeometry(MarkupCanvas canvas) {
			canvas.Ellipse(centre, rx, ry);
		}

		public override IPathCalculator GetPath(MarkupCanvas canvas) {
			DrawPoint centre = canvas.TransformPoint(this.centre);
			float rx = canvas.TransformLength(this.rx);
			float ry = canvas.TransformLength(this.ry);
			return LUTPathCalculator.Create((float t, out Vector v) => { return GetPoint(centre, rx, ry, t, out v); }, 100, true);
		}

		private static DrawPoint GetPoint(DrawPoint centre, float rx, float ry, float t, out Vector normal) {
			float theta = -(float)(2 * Math.PI * t);
			float cosT = (float)Math.Cos(theta);
			float sinT = (float)Math.Sin(theta);
			//normal = new Vector(cosT, sinT);
			normal = new Vector(cosT * ry, sinT * rx).Normal();
			return new DrawPoint(centre.X + cosT * rx, centre.Y + sinT * ry);
		}
	}

	/// <summary>
	/// This element draws a straight line between two points.
	/// </summary>
	public class Line : ShapeElement {
		protected override bool CanFill { get; } = false;

		readonly DrawPointExpression point1;
		readonly DrawPointExpression point2;

		/// <summary>
		/// Constructor for Line.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_x1" default="0">The x coordinate of the start point.</param>
		/// <param name="_y1" default="0">The y coordinate of the start point.</param>
		/// <param name="_x2" default="0">The x coordinate of the end point.</param>
		/// <param name="_y2" default="0">The y coordinate of the end point.</param>
		/// <exception cref="EvaluationException"></exception>
		public Line(string? _id, StyleSheet styleSheet, XLengthExpression _x1, YLengthExpression _y1, XLengthExpression _x2, YLengthExpression _y2) : base(_id, styleSheet) {
			this.point1 = new DrawPointExpression(_x1, _y1);
			this.point2 = new DrawPointExpression(_x2, _y2);
		}

		protected override void DoAssignGeometry(MarkupCanvas canvas) {
			canvas.MoveTo(point1)
				.LineTo(point2);
		}

		public override IPathCalculator GetPath(MarkupCanvas canvas) {
			DrawPoint point1 = canvas.TransformPoint(this.point1);
			DrawPoint point2 = canvas.TransformPoint(this.point2);
			return new LinePathCalculator(point1, point2);
		}
	}

	/// <summary>
	/// This element draws a closed shape, made of a series of straight line segments.
	/// The last point will be connected to the first point.
	/// </summary>
	public class Polygon : ShapeElement {
		readonly DrawPointExpression[] points;

		/// <summary>
		/// Constructor for Polygon.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_points" default="null">A series of x,y coordinates for the
		/// points for the polygon shape. Each point will be connected to the previous
		/// and subsequent point by straight line segments, with the first and last
		/// point also having a straight line connecting them.</param>
		public Polygon(string? _id, StyleSheet styleSheet, DrawPointExpression[] _points) : base(_id, styleSheet) {
			this.points = _points;
		}

		protected override void DoAssignGeometry(MarkupCanvas canvas) {
			if (points.Length > 1) {
				canvas.MoveTo(points[0]);
				for (int i = 1; i < points.Length; i++) {
					canvas.LineTo(points[i]);
				}
				canvas.ClosePath();
			}
		}

		public override IPathCalculator GetPath(MarkupCanvas canvas) {
			return PolylinePathCalculator.Create(points.Select(p => canvas.TransformPoint(p)).ToArray(), true);
		}
	}

	/// <summary>
	/// This element draws a series of connected straight lines.
	/// </summary>
	public class Polyline : ShapeElement {
		readonly DrawPointExpression[] points;

		/// <summary>
		/// Constructor for Polyline.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_points" default="null">A series of x,y coordinates for the
		/// point for the line segments. Each point will be connected to the previous
		/// subsequent point by straight line segments (with no connection between
		/// the first and last point).</param>
		public Polyline(string? _id, StyleSheet styleSheet, DrawPointExpression[] _points) : base(_id, styleSheet) {
			this.points = _points;
		}

		protected override void DoAssignGeometry(MarkupCanvas canvas) {
			if (points.Length > 1) {
				canvas.MoveTo(points[0]);
				for (int i = 1; i < points.Length; i++) {
					canvas.LineTo(points[i]);
				}
			}
		}

		public override IPathCalculator GetPath(MarkupCanvas canvas) {
			return PolylinePathCalculator.Create(points.Select(p => canvas.TransformPoint(p)).ToArray(), false);
		}
	}

	/// <summary>
	/// This element draws a rectangle, defined by a position for the lower-left corner,
	/// a width, and a height. The rectangle may have rounded corners.
	/// </summary>
	public class Rect : ShapeElement {
		readonly DrawPointExpression position;
		readonly FloatExpression width;
		readonly FloatExpression height;
		readonly FloatExpression? rx;
		readonly FloatExpression? ry;

		/// <summary>
		/// Constructor for Rect.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_x" default="0">The x-coordinate of the lower-left corner of the rectangle.</param>
		/// <param name="_y" default="0">The y-coordinate of the lower-left corner of the rectangle.</param>
		/// <param name="_width" default="0">The width of the rectangle.</param>
		/// <param name="_height" default="0">The height of the rectangle.</param>
		/// <param name="_rx" default="null">The horizontal corner radius for rounded corners.</param>
		/// <param name="_ry" default="null">The vertical corner radius for rounded corners.</param>
		/// <exception cref="EvaluationException"></exception>
		public Rect(string? _id, StyleSheet styleSheet, XLengthExpression _x, YLengthExpression _y, XLengthExpression _width, YLengthExpression _height, FloatExpression? _rx, FloatExpression? _ry) : base(_id, styleSheet) {
			this.position = new DrawPointExpression(_x, _y);
			this.width = _width;
			this.height = _height;
			this.rx = _rx;
			this.ry = _ry;
		}

		protected override void DoAssignGeometry(MarkupCanvas canvas) {
			RectangleExpression finalRect = new RectangleExpression(position.X, position.Y, width, height);
			if (ry != null || ry != null) {
				canvas.RoundRectangle(finalRect, rx, ry);
			}
			else {
				canvas.Rectangle(finalRect);
			}
		}

		public override IPathCalculator GetPath(MarkupCanvas canvas) {
			Rectangle pathRect = canvas.TransformRectangle(new RectangleExpression(this.position.X, this.position.Y, this.width, this.height));
			DrawPoint position = new DrawPoint(pathRect.X, pathRect.Y); // canvas.TransformPoint(this.position);
			float width = pathRect.Width; // canvas.TransformLength(this.width);
			float height = pathRect.Height; // canvas.TransformLength(this.height);
			if (rx != null && ry != null) {
				float rxEval = canvas.TransformLength(rx);
				float ryEval = canvas.TransformLength(ry);

				return RectPathCalculator.RoundedRectPath(position, width, height, rxEval, ryEval);
			}
			else {
				return new RectPathCalculator(position, width, height);
			}
		}
	}

}
