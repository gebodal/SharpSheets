using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas {

	public static class CanvasDrawingExtensions {

		public static ISharpCanvas Rectangle(this ISharpCanvas canvas, Layouts.Rectangle rect) {
			canvas.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
			return canvas;
		}

		public static ISharpCanvas EllipseAt(this ISharpCanvas canvas, float x, float y, float width, float height) {
			canvas.Ellipse(x - width / 2, y - height / 2, x + width / 2, y + height / 2);
			return canvas;
		}
		public static ISharpCanvas EllipseAt(this ISharpCanvas canvas, Layouts.Rectangle rect, float width, float height) {
			canvas.Ellipse(rect.CentreX - width / 2, rect.CentreY - height / 2, rect.CentreX + width / 2, rect.CentreY + height / 2);
			return canvas;
		}
		public static ISharpCanvas EllipseFrom(this ISharpCanvas canvas, float x, float y, float width, float height) {
			canvas.Ellipse(x, y, x + width, y + height);
			return canvas;
		}
		public static ISharpCanvas EllipseFrom(this ISharpCanvas canvas, Layouts.Rectangle rect) {
			canvas.EllipseFrom(rect.X, rect.Y, rect.Width, rect.Height);
			return canvas;
		}

		public static ISharpCanvas CircleAt(this ISharpCanvas canvas, float x, float y, float radius) {
			canvas.Ellipse(x - radius, y - radius, x + radius, y + radius);
			return canvas;
		}
		public static ISharpCanvas CircleAt(this ISharpCanvas canvas, Layouts.Rectangle rect, float radius) {
			canvas.Ellipse(rect.CentreX - radius, rect.CentreY - radius, rect.CentreX + radius, rect.CentreY + radius);
			return canvas;
		}

		public static ISharpCanvas EllipseAtRel(this ISharpCanvas canvas, Layouts.Rectangle rect, float x, float y, float width, float height) {
			float xAbs = rect.X + rect.Width * x;
			float yAbs = rect.Y + rect.Height * y;
			canvas.Ellipse(xAbs - width / 2, yAbs - height / 2, xAbs + width / 2, yAbs + height / 2);
			return canvas;
		}
		public static ISharpCanvas CircleAtRel(this ISharpCanvas canvas, Layouts.Rectangle rect, float x, float y, float radius) {
			float xAbs = rect.X + rect.Width * x;
			float yAbs = rect.Y + rect.Height * y;
			canvas.Ellipse(xAbs - radius, yAbs - radius, xAbs + radius, yAbs + radius);
			return canvas;
		}

		public static ISharpCanvas MoveToRel(this ISharpCanvas canvas, Layouts.Rectangle rect, float x, float y) {
			canvas.MoveTo(rect.X + rect.Width * x, rect.Y + rect.Height * y);
			return canvas;
		}
		public static ISharpCanvas LineToRel(this ISharpCanvas canvas, Layouts.Rectangle rect, float x, float y) {
			canvas.LineTo(rect.X + rect.Width * x, rect.Y + rect.Height * y);
			return canvas;
		}
		public static ISharpCanvas CurveToRel(this ISharpCanvas canvas, Layouts.Rectangle rect, float x2, float y2, float x3, float y3) {
			canvas.CurveTo(rect.X + rect.Width * x2, rect.Y + rect.Height * y2, rect.X + rect.Width * x3, rect.Y + rect.Height * y3);
			return canvas;
		}
		public static ISharpCanvas CurveToRel(this ISharpCanvas canvas, Layouts.Rectangle rect, float x1, float y1, float x2, float y2, float x3, float y3) {
			canvas.CurveTo(rect.X + rect.Width * x1, rect.Y + rect.Height * y1, rect.X + rect.Width * x2, rect.Y + rect.Height * y2, rect.X + rect.Width * x3, rect.Y + rect.Height * y3);
			return canvas;
		}

		public static ISharpCanvas ArcTo(this ISharpCanvas canvas, float x2, float y2, float rx, float ry, float angle, bool largeArc, bool sweep) {
			// https://www.w3.org/TR/SVG/implnote.html#ArcConversionEndpointToCenter

			DrawPoint? start = canvas.CurrentPenLocation;
			if (!start.HasValue) {
				canvas.MoveTo(x2, y2);
				start = new DrawPoint(x2, y2);
			}
			float x1 = start.Value.X;
			float y1 = start.Value.Y;

			if (rx == 0 || ry == 0) {
				canvas.LineTo(x1, y1)
					.LineTo(x2, y2);
				return canvas;
			}
			rx = Math.Abs(rx);
			ry = Math.Abs(ry);

			float sinA = (float)Math.Sin(angle);
			float cosA = (float)Math.Cos(angle);

			float xc = (x1 - x2) / 2f;
			float yc = (y1 - y2) / 2f;
			float x1p = (float)(xc * cosA + yc * sinA);
			float y1p = (float)(yc * cosA - xc * sinA);

			float lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
			if (lambda > 1) {
				float sqrtLambda = 1e-9f + (float)Math.Sqrt(lambda);
				rx = sqrtLambda * rx;
				ry = sqrtLambda * ry;
			}

			float sgnF = (largeArc == sweep) ? -1f : +1f;
			float F = (float)(sgnF * Math.Sqrt((rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p) / (rx * rx * y1p * y1p + ry * ry * x1p * x1p)));
			if (float.IsNaN(F)) { F = 0f; }

			float cxp = F * rx * y1p / ry;
			float cyp = -F * ry * x1p / rx;

			float cx = cxp * cosA - cyp * sinA + (x1 + x2) / 2f;
			float cy = cxp * sinA + cyp * cosA + (y1 + y2) / 2f;

			float Angle(float ux, float uy, float vx, float vy) {
				// Angle between two vectors u and v -> (ux,uy), (vx,vy)
				float sign = (ux * vy - uy * vx) < 0 ? -1 : +1;
				float dot = ux * vx + uy * vy;
				float uabs = (float)Math.Sqrt(ux * ux + uy * uy);
				float vabs = (float)Math.Sqrt(vx * vx + vy * vy);
				return sign * (float)Math.Acos(dot / (uabs * vabs));
			}

			float theta1 = Angle(1f, 0f, (x1p - cxp) / rx, (y1p - cyp) / ry);
			float dtheta = Angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
			if (!sweep && dtheta > 0) {
				dtheta -= 2f * (float)Math.PI;
			}
			else if (sweep && dtheta < 0) {
				dtheta += 2f * (float)Math.PI;
			}

			List<float[]> ar = BezierArc(rx, ry, theta1, dtheta);
			if (ar.Count == 0) {
				return canvas;
			}
			float[] pt = ar[0];
			float sx = cx + pt[0] * cosA - pt[1] * sinA;
			float sy = cy + pt[0] * sinA + pt[1] * cosA;
			canvas.LineTo(sx, sy);
			for (int i = 0; i < ar.Count; ++i) {
				pt = ar[i];

				float c1x = cx + pt[2] * cosA - pt[3] * sinA;
				float c1y = cy + pt[2] * sinA + pt[3] * cosA;
				float c2x = cx + pt[4] * cosA - pt[5] * sinA;
				float c2y = cy + pt[4] * sinA + pt[5] * cosA;
				float ex = cx + pt[6] * cosA - pt[7] * sinA;
				float ey = cy + pt[6] * sinA + pt[7] * cosA;

				canvas.CurveTo(c1x, c1y, c2x, c2y, ex, ey);
			}

			return canvas;
		}

		/// <summary>Generates an array of bezier curves to draw an arc.</summary>
		/// <remarks>
		/// Generates an array of bezier curves to draw an arc.
		/// <br/>
		/// (x1, y1) and (x2, y2) are the corners of the enclosing rectangle.
		/// Angles, measured in degrees, start with 0 to the right (the positive X
		/// axis) and increase counter-clockwise.  The arc extends from startAng
		/// to startAng+extent.  i.e. startAng=0 and extent=180 yields an openside-down
		/// semi-circle.
		/// <br/>
		/// The resulting coordinates are of the form double[]{x1,y1,x2,y2,x3,y3, x4,y4}
		/// such that the curve goes from (x1, y1) to (x4, y4) with (x2, y2) and
		/// (x3, y3) as their respective Bezier control points.
		/// <br/>
		/// Note: this code was taken from ReportLab (www.reportlab.org), an excellent
		/// PDF generator for Python (BSD license: http://www.reportlab.org/devfaq.html#1.3 ).
		/// </remarks>
		/// <param name="rx">X-axis radius of the ellipse.</param>
		/// <param name="ry">Y-axis radius of the ellipse.</param>
		/// <param name="startAng">Starting angle of the arc in radians.</param>
		/// <param name="extent">Anglular extent of the arc in radians.</param>
		/// <returns>a list of double[] with the bezier curves.</returns>
		private static List<float[]> BezierArc(float rx, float ry, float startAng, float extent) {
			float fragAngle;
			int Nfrag;
			if (Math.Abs(extent) <= 0.5 * Math.PI) {
				fragAngle = extent;
				Nfrag = 1;
			}
			else {
				Nfrag = (int)Math.Ceiling(Math.Abs(extent) / (0.5 * Math.PI));
				fragAngle = extent / Nfrag;
			}

			float halfAng = fragAngle / 2f;
			float kappa = (float)Math.Abs(4.0 / 3.0 * (1.0 - Math.Cos(halfAng)) / Math.Sin(halfAng));

			List<float[]> pointList = new List<float[]>();
			for (int iter = 0; iter < Nfrag; ++iter) {
				float theta0 = startAng + iter * fragAngle;
				float theta1 = startAng + (iter + 1) * fragAngle;

				float sin0 = (float)Math.Sin(theta0);
				float cos0 = (float)Math.Cos(theta0);
				float sin1 = (float)Math.Sin(theta1);
				float cos1 = (float)Math.Cos(theta1);

				if (fragAngle > 0.0) {
					pointList.Add(new float[] {
						rx * cos0, ry * sin0,
						rx * (cos0 - kappa * sin0), ry * (sin0 + kappa * cos0),
						rx * (cos1 + kappa * sin1), ry * (sin1 - kappa * cos1),
						rx * cos1, ry * sin1 });
				}
				else {
					pointList.Add(new float[] {
						rx * cos0, ry * sin0,
						rx * (cos0 + kappa * sin0), ry * (sin0 - kappa * cos0),
						rx * (cos1 - kappa * sin1), ry * (sin1 + kappa * cos1),
						rx * cos1, ry * sin1 });
				}
			}
			return pointList;
		}

		public static ISharpCanvas BezierEllipse(this ISharpCanvas canvas, float x1, float y1, float x2, float y2) {

			float rx = (x2 - x1) / 2;
			float ry = (y2 - y1) / 2;
			float cx = x1 + rx;
			float cy = y1 + ry;

			List<float[]> ar = BezierArc(rx, ry, 0f, (float)(2 * Math.PI));
			if (ar.Count == 0) {
				return canvas;
			}
			float[] pt = ar[0];
			float sx = cx + pt[0];
			float sy = cy + pt[1];
			canvas.MoveTo(sx, sy);
			for (int i = 0; i < ar.Count; ++i) {
				pt = ar[i];

				float c1x = cx + pt[2];
				float c1y = cy + pt[3];
				float c2x = cx + pt[4];
				float c2y = cy + pt[5];
				float ex = cx + pt[6];
				float ey = cy + pt[7];

				canvas.CurveTo(c1x, c1y, c2x, c2y, ex, ey);
			}

			canvas.ClosePath();

			return canvas;
		}

		public static ISharpCanvas ArcTo(this ISharpCanvas canvas, float x2, float y2, float radius, bool largeArc, bool sweep) {
			return ArcTo(canvas, x2, y2, radius, radius, 0f, largeArc, sweep);
		}

	}

}
