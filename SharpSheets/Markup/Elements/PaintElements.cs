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
	/// A solid paint element, which can be used to represent a brush which
	/// draws a single, consistent color, with no spatial variability.
	/// </summary>
	public class SolidPaint : ICanvasPaint, IIdentifiableMarkupElement {

		public string? ID { get; }

		public ColorExpression Color { get; }

		/// <summary>
		/// Constructor for SolidPaint.
		/// </summary>
		/// <param name="_id">A unique name for this element.</param>
		/// <param name="_color">The color for this paint.</param>
		public SolidPaint(string? _id, ColorExpression _color) {
			this.ID = _id;
			this.Color = _color;
		}

		public void Apply(MarkupCanvas canvas, PaintApplication application) {
			if (application == PaintApplication.FILL) {
				canvas.SetFillColor(Color);
			}
			else {
				canvas.SetStrokeColor(Color);
			}
		}
	}

	//public enum SpreadMethod { Pad, Reflect, Repeat }

	/// <summary>
	/// A linear gradient paint element, which is used to represent a gradient
	/// that changes linearly between two specified points.
	/// </summary>
	public class LinearGradient : ICanvasPaint, IIdentifiableMarkupElement {
		
		public string? ID { get; }

		// gradientUnits // Worth implementing?
		//Transform gradientTransform;
		//LinearGradient template; // href
		//SpreadMethod spreadMethod;

		private readonly DrawPointExpression p1; // x1, y1
		private readonly DrawPointExpression p2; // x2, y2
		private readonly ColorStopExpression[] stops;

		/// <summary>
		/// Constructor for LinearGradient.
		/// </summary>
		/// <param name="_id">A unique name for this element.</param>
		/// <param name="_x1" default="0">The x coordinate of the start point.</param>
		/// <param name="_y1" default="0">The y coordinate of the start point.</param>
		/// <param name="_x2" default="$width">The x coordinate of the end point.</param>
		/// <param name="_y2" default="0">The y coordinate of the end point.</param>
		/// <param name="stops">The color stops for this gradient.</param>
		public LinearGradient(string? _id,
			XLengthExpression _x1, YLengthExpression _y1,
			XLengthExpression _x2, YLengthExpression _y2,
			IEnumerable<ColorStopExpression> stops) {
			
			this.ID = _id;
			this.p1 = new DrawPointExpression(_x1, _y1);
			this.p2 = new DrawPointExpression(_x2, _y2);
			this.stops = stops.ToArray();
		}

		public void Apply(MarkupCanvas canvas, PaintApplication application) {
			if (application == PaintApplication.FILL) {
				canvas.SetFillLinearGradient(p1, p2, stops);
			}
			else {
				canvas.SetStrokeLinearGradient(p1, p2, stops);
			}
		}
	}

	/// <summary>
	/// A radial gradient paint element, which is used to represent a gradient
	/// that changes radially between two specified circles.
	/// </summary>
	public class RadialGradient : ICanvasPaint, IIdentifiableMarkupElement {

		public string? ID { get; }

		// gradientUnits?
		//Transform gradientTransform;
		//RadialGradient template; // href
		//SpreadMethod spreadMethod;

		private readonly DrawPointExpression c; // cx, cy
		private readonly FloatExpression r;
		private readonly DrawPointExpression f; // fx, fy
		private readonly FloatExpression fr;
		private readonly ColorStopExpression[] stops;

		/// <summary>
		/// Constructor for RadialGradient.
		/// </summary>
		/// <param name="_id">A unique name for this element.</param>
		/// <param name="_cx" default="$width / 2">The x coordinate of the end circle.</param>
		/// <param name="_cy" default="$height / 2">The y coordinate of the end circle.</param>
		/// <param name="_r" default="min($width, $height) / 2">The radius of the end circle.</param>
		/// <param name="_fx" default="$width / 2">The x coordinate of the start circle.</param>
		/// <param name="_fy" default="$height / 2">The y coordinate of the start circle.</param>
		/// <param name="_fr" default="0">The radius of the start circle.</param>
		/// <param name="stops">The color stops for this gradient.</param>
		public RadialGradient(string? _id,
			XLengthExpression _cx, YLengthExpression _cy,
			BoundingBoxLengthExpression _r,
			XLengthExpression _fx, YLengthExpression _fy,
			BoundingBoxLengthExpression _fr,
			IEnumerable<ColorStopExpression> stops) {
			
			this.ID = _id;
			this.c = new DrawPointExpression(_cx, _cy); ;
			this.r = _r;
			this.f = new DrawPointExpression(_fx, _fy); ;
			this.fr = _fr;
			this.stops = stops.ToArray();
		}

		public void Apply(MarkupCanvas canvas, PaintApplication application) {
			if (application == PaintApplication.FILL) {
				canvas.SetFillRadialGradient(c, r, f, fr, stops);
			}
			else {
				canvas.SetStrokeRadialGradient(c, r, f, fr, stops);
			}
		}
	}

	/*
	public class Pattern {
		float x;
		float y;
		float width;
		float height;
		FilePath image; // href
		// patternUnits // ??
		// patternContentUnits // ??
		Transform patternTransform;
		bool preserveAspectRatio;
		// viewBox?
	}
	*/

}
