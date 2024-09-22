using SharpSheets.Canvas;
using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;

namespace SharpSheets.Markup.Elements {

	public interface IPathCalculator {
		float Length { get; }
		bool IsClosed { get; }
		DrawPoint? PointAt(float distance, out Vector? normal);
		Layouts.Rectangle? GetBoundingBox();
		PathHandleData[]? GetPathHandles();
	}

	public static class PathCalculatorUtils {

		public static DrawPoint PointAtDistanceOnLine(DrawPoint start, DrawPoint end, float length, float distance, out Vector normal) {
			float t = distance / length;
			normal = new Vector(-(end.Y - start.Y) / length, (end.X - start.X) / length);
			return new DrawPoint(MathUtils.Lerp(start.X, end.X, t), MathUtils.Lerp(start.Y, end.Y, t));
		}

		public static DrawPoint PointAtDistanceAlongLine(DrawPoint start, DrawPoint end, float t, out Vector normal) {
			normal = new Vector(-(end.Y - start.Y), (end.X - start.X)).Normal();
			return new DrawPoint(MathUtils.Lerp(start.X, end.X, t), MathUtils.Lerp(start.Y, end.Y, t));
		}

		public static Layouts.Rectangle BoundingBoxFromPoints(IEnumerable<DrawPoint>? points) {
			if (points != null) {
				float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
				int count = 0;

				foreach (DrawPoint p in points) {
					minX = Math.Min(minX, p.X);
					maxX = Math.Max(maxX, p.X);
					minY = Math.Min(minY, p.Y);
					maxY = Math.Max(maxY, p.Y);
					count++;
				}

				if (count > 0) {
					return Layouts.Rectangle.RectangleFromBounding(minX, minY, maxX, maxY);
				}
			}

			return new Layouts.Rectangle(0f, 0f, 0f, 0f);
		}

	}

	public class LinePathCalculator : IPathCalculator {

		private readonly DrawPoint point1;
		private readonly DrawPoint point2;

		public float Length { get; }
		public bool IsClosed { get; } = false;

		public LinePathCalculator(DrawPoint point1, DrawPoint point2) {
			this.point1 = point1;
			this.point2 = point2;
			Length = DrawPoint.Distance(this.point1, this.point2);
		}

		public DrawPoint? PointAt(float distance, out Vector? normal) {
			if (distance < 0 || distance > Length) {
				normal = null;
				return null;
			}
			DrawPoint p = PathCalculatorUtils.PointAtDistanceOnLine(point1, point2, Length, distance, out Vector n);
			normal = n;
			return p;
		}

		public Layouts.Rectangle GetBoundingBox() {
			return Layouts.Rectangle.RectangleFromBounding(point1.X, point1.Y, point2.X, point2.Y);
		}

		public PathHandleData[] GetPathHandles() {
			return new PathHandleData[] { new PathHandleData(new DrawPoint[] { point1, point2 }, new bool[] { true, true }, false) };
		}
	}

	class CirclePathCalculator : IPathCalculator {

		private readonly DrawPoint centre;
		private readonly float radius;

		public float Length { get; }
		public bool IsClosed { get; } = true;

		public CirclePathCalculator(DrawPoint centre, float radius) {
			this.centre = centre;
			this.radius = radius;
			this.Length = (float)(2 * Math.PI * radius);
		}

		public DrawPoint? PointAt(float distance, out Vector? normal) {
			if (distance < 0 || distance > Length) {
				normal = null;
				return null;
			}
			float theta = -distance / radius;
			float cosT = (float)Math.Cos(theta);
			float sinT = (float)Math.Sin(theta);
			normal = new Vector(cosT, sinT);
			return new DrawPoint(centre.X + radius * cosT, centre.Y + radius * sinT);
		}

		public Layouts.Rectangle GetBoundingBox() {
			return Layouts.Rectangle.RectangleFromBounding(centre.X - radius, centre.Y - radius, centre.X + radius, centre.Y + radius);
		}

		public PathHandleData[]? GetPathHandles() {
			return null;
		}
	}

	public class PolylinePathCalculator : IPathCalculator {

		private readonly DrawPoint[]? points;
		private readonly float[]? lengths;
		private readonly float[]? distances;

		public float Length { get; }
		public bool IsClosed { get; }

		private PolylinePathCalculator() {
			points = null;
			lengths = null;
			distances = null;
			Length = 0f;
			IsClosed = false;
		}

		private PolylinePathCalculator(DrawPoint[] points, float[] lengths, float[] distances, bool isClosed) {
			this.points = points;
			this.lengths = lengths;
			this.distances = distances;
			Length = distances.Length > 0 ? distances[^1] : 0f;
			IsClosed = isClosed;
		}

		public DrawPoint? PointAt(float distance, out Vector? normal) {
			if (points != null && distance >= 0 && distance <= Length) {
				for (int i = 1; i < points.Length; i++) {
					if (distance <= distances![i - 1]) {
						DrawPoint p = PathCalculatorUtils.PointAtDistanceOnLine(points[i - 1], points[i], Length, lengths![i - 1], out Vector n);
						normal = n;
						return p;
					}
				}
			}
			normal = null;
			return null;
		}

		public Layouts.Rectangle GetBoundingBox() {
			return PathCalculatorUtils.BoundingBoxFromPoints(points);
		}

		public PathHandleData[]? GetPathHandles() {
			return points is not null ? new PathHandleData[] { new PathHandleData(points, true.MakeArray(points.Length), IsClosed) } : null;
		}

		public static IPathCalculator Create(DrawPoint[] pointsIn, bool closed) {
			if (pointsIn.Length > 1) {
				DrawPoint[] points = new DrawPoint[pointsIn.Length + (closed ? 1 : 0)];
				float[] lengths = new float[points.Length - 1];
				float[] distances = new float[points.Length - 1];
				float traversed = 0f;
				points[0] = pointsIn[0];
				if (closed) {
					for (int i = 1; i <= points.Length; i++) {
						points[i] = pointsIn[i % points.Length];
						lengths[i - 1] = DrawPoint.Distance(points[i - 1], points[i]);
						traversed += lengths[i - 1];
						distances[i - 1] = traversed;
					}
				}
				else {
					for (int i = 1; i < points.Length; i++) {
						points[i] = pointsIn[i];
						lengths[i - 1] = DrawPoint.Distance(points[i - 1], points[i]);
						traversed += lengths[i - 1];
						distances[i - 1] = traversed;
					}
				}
				return new PolylinePathCalculator(points, lengths, distances, closed);
			}
			else {
				return new PolylinePathCalculator();
			}
		}
	}

	public class RectPathCalculator : IPathCalculator {

		private readonly DrawPoint position;
		private readonly float width;
		private readonly float height;

		public float Length { get; }
		public bool IsClosed { get; } = true;

		public RectPathCalculator(DrawPoint position, float width, float height) {
			this.position = position;
			this.width = width;
			this.height = height;
			this.Length = 2 * width + 2 * height;
		}

		public DrawPoint? PointAt(float distance, out Vector? normal) {
			DrawPoint p;
			Vector n;
			if (distance < 0 || distance > Length) {
				normal = null;
				return null;
			}
			else if (distance <= width) {
				p = PathCalculatorUtils.PointAtDistanceOnLine(
					new DrawPoint(position.X, position.Y + height),
					new DrawPoint(position.X + width, position.Y + height),
					width, distance, out n);
			}
			else if (distance <= width + height) {
				p = PathCalculatorUtils.PointAtDistanceOnLine(
					new DrawPoint(position.X + width, position.Y + height),
					new DrawPoint(position.X + width, position.Y),
					height, distance - width, out n);
			}
			else if (distance <= 2 * width + height) {
				p = PathCalculatorUtils.PointAtDistanceOnLine(
					new DrawPoint(position.X + width, position.Y),
					new DrawPoint(position.X, position.Y),
					width, distance - (width + height), out n);
			}
			else {
				p = PathCalculatorUtils.PointAtDistanceOnLine(
					new DrawPoint(position.X, position.Y),
					new DrawPoint(position.X, position.Y + height),
					height, distance - (2 * width + height), out n);
			}
			normal = n;
			return p;
		}

		public Layouts.Rectangle GetBoundingBox() {
			return new Layouts.Rectangle(position.X, position.Y, width, height);
		}

		public PathHandleData[]? GetPathHandles() {
			return null;
		}

		public static IPathCalculator RoundedRectPath(DrawPoint position, float width, float height, float rx, float ry) {
			float left = position.X, right = position.X + width, bottom = position.Y, top = position.Y + height;
			return CompositePathCalculator.Create(new IPathCalculator[] {
				new LinePathCalculator(new DrawPoint(left+rx, top), new DrawPoint(right-rx, top)),
				EllipseArcCalculator.Create(new DrawPoint(right-rx, top), new DrawPoint(right, top-ry), rx, ry, 0f, false, false),
				new LinePathCalculator(new DrawPoint(right, top-ry), new DrawPoint(right, bottom+ry)),
				EllipseArcCalculator.Create(new DrawPoint(right, bottom+ry), new DrawPoint(right - rx, bottom), rx, ry, 0f, false, false),
				new LinePathCalculator(new DrawPoint(right - rx, bottom), new DrawPoint(left + rx, bottom)),
				EllipseArcCalculator.Create(new DrawPoint(left + rx, bottom), new DrawPoint(left, bottom+ry), rx, ry, 0f, false, false),
				new LinePathCalculator(new DrawPoint(left, bottom+ry), new DrawPoint(left, top-ry)),
				EllipseArcCalculator.Create(new DrawPoint(left, top-ry), new DrawPoint(left+rx, top), rx, ry, 0f, false, false)
			}, Array.Empty<PathHandleData>(), true)!; // Should never be null, as we are passing a non-zero number of notnull calculators
		}
	}

	public class EllipseArcCalculator {

		private readonly DrawPoint start, end;
		private readonly DrawPoint centre;
		private readonly float rx, ry;
		private readonly float cosA, sinA;
		private readonly float theta1, theta2;

		public EllipseArcCalculator(float x1, float y1, float x2, float y2, float rx, float ry, float angle, bool largeArc, bool sweep) {
			// https://www.w3.org/TR/SVG/implnote.html#ArcConversionEndpointToCenter

			start = new DrawPoint(x1, y1);
			end = new DrawPoint(x2, y2);

			if (rx == 0 || ry == 0) {
				this.rx = rx;
				this.ry = ry;
			}
			else {
				rx = Math.Abs(rx);
				ry = Math.Abs(ry);

				sinA = (float)Math.Sin(angle);
				cosA = (float)Math.Cos(angle);

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
				centre = new DrawPoint(cx, cy);

				static float Angle(float ux, float uy, float vx, float vy) {
					// Angle between two vectors u and v -> (ux,uy), (vx,vy)
					float sign = (ux * vy - uy * vx) < 0 ? -1 : +1;
					float dot = ux * vx + uy * vy;
					float uabs = (float)Math.Sqrt(ux * ux + uy * uy);
					float vabs = (float)Math.Sqrt(vx * vx + vy * vy);
					return sign * (float)Math.Acos(dot / (uabs * vabs));
				}

				theta1 = Angle(1f, 0f, (x1p - cxp) / rx, (y1p - cyp) / ry);
				float dtheta = Angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
				if (!sweep && dtheta > 0) {
					dtheta -= 2f * (float)Math.PI;
				}
				else if (sweep && dtheta < 0) {
					dtheta += 2f * (float)Math.PI;
				}

				theta2 = theta1 + dtheta;

				this.rx = rx;
				this.ry = ry;
			}
		}

		public DrawPoint Point(float t, out Vector normal) {
			if(rx == 0 || ry == 0) {
				return PathCalculatorUtils.PointAtDistanceAlongLine(start, end, t, out normal);
			}
			float theta = MathUtils.Lerp(theta1, theta2, t);
			float cosT = (float)Math.Cos(theta);
			float sinT = (float)Math.Sin(theta);
			float dx = rx * cosT * cosA - ry * sinT * sinA;
			float dy = rx * cosT * sinA + ry * sinT * cosA;
			//normal = new Vector(dx, dy).Normal(); // This is not correct
			normal = new Vector(-rx * sinT * sinA + ry * cosT * cosA, rx * sinT * cosA + ry * cosT * sinA).Normal(); // Normal necessary here?
			float x = centre.X + dx;
			float y = centre.Y + dy;
			return new DrawPoint(x, y);
		}

		public static IPathCalculator Create(DrawPoint p1, DrawPoint p2, float rx, float ry, float angle, bool largeArc, bool sweep) {
			EllipseArcCalculator calc = new EllipseArcCalculator(p1.X, p1.Y, p2.X, p2.Y, rx, ry, angle, largeArc, sweep);
			PathHandleData handles = new PathHandleData(new DrawPoint[] { p1, p2 }, new bool[] { true, true }, false);
			return LUTPathCalculator.Create((float t, out Vector v) => calc.Point(t, out v), new PathHandleData[] { handles }, 100, false);
		}
	}

	public delegate DrawPoint LUTFunc(float t, out Vector normal);
	public class LUTPathCalculator : IPathCalculator {

		private readonly LUTFunc func;
		private readonly float[] distances;

		public float Length { get; }
		public bool IsClosed { get; }

		private readonly Layouts.Rectangle bounds;
		private readonly PathHandleData[]? handles;

		private LUTPathCalculator(LUTFunc func, float[] distances, Layouts.Rectangle bounds, PathHandleData[]? handles, bool isClosed) {
			this.func = func;
			this.distances = distances;
			Length = distances[^1];
			IsClosed = isClosed;

			this.bounds = bounds;
			this.handles = handles;
		}

		public DrawPoint? PointAt(float distance, out Vector? normal) {
			if(distance < 0 || distance > Length) {
				normal = null;
				return null;
			}

			if(distances.Length == 1) {
				float t1 = distance / Length;
				DrawPoint p1 = func(t1, out Vector n1);
				normal = n1;
				return p1;
			}

			int i = 0;
			int j = distances.Length - 1;
			while (j > i + 1) {
				int m = (i + j) / 2;
				if (distances[m] > distance) {
					j = m;
				}
				else {
					i = m;
				}
			}

			float a = (distance - distances[i]) / (distances[j] - distances[i]);
			float t = (i + 1 + a * (j - i)) / distances.Length;

			DrawPoint p = func(t, out Vector n);
			normal = n;
			return p;
		}

		public Layouts.Rectangle GetBoundingBox() {
			return bounds;
		}

		public PathHandleData[]? GetPathHandles() {
			return handles;
		}

		public static IPathCalculator Create(LUTFunc func, PathHandleData[]? handles, int n, bool isClosed) {

			n = Math.Max(n, 1);

			float[] distances = new float[n];
			float running = 0f;
			DrawPoint previous = func(0, out _);
			float minX = previous.X, maxX = previous.X, minY = previous.Y, maxY = previous.Y;
			for (int i = 0; i < n; i++) {
				DrawPoint current = func((i + 1f) / n, out _);

				running += DrawPoint.Distance(previous, current);
				distances[i] = running;

				minX = Math.Min(minX, current.X);
				maxX = Math.Max(maxX, current.X);
				minY = Math.Min(minY, current.Y);
				maxY = Math.Max(maxY, current.Y);

				previous = current;
			}

			return new LUTPathCalculator(func, distances, Layouts.Rectangle.RectangleFromBounding(minX, minY, maxX, maxY), handles, isClosed);
		}
	}

	public class CompositePathCalculator : IPathCalculator {

		private readonly IPathCalculator[] segments;
		private readonly float[] distances;

		public float Length { get; }
		public bool IsClosed { get; }

		private readonly Layouts.Rectangle? bounds;
		private readonly PathHandleData[]? handles;

		private CompositePathCalculator(IPathCalculator[] segments, float[] distances, Layouts.Rectangle? bounds, PathHandleData[]? handles, bool isClosed) {
			this.segments = segments;
			this.distances = distances;
			Length = distances.Length > 0 ? distances[^1] : 0f;
			IsClosed = isClosed;

			this.bounds = bounds;
			this.handles = handles;
		}

		public DrawPoint? PointAt(float distance, out Vector? normal) {
			if(segments.Length == 0 || distance < 0 || distance > Length) {
				normal = null;
				return null;
			}

			if (segments.Length == 1) {
				return segments[0].PointAt(distance, out normal);
			}
			else if(distance <= distances[0]) {
				return segments[0].PointAt(distance, out normal);
			}
			else {
				int i = 0;
				int j = distances.Length - 1;
				while (j > i + 1) {
					int m = (i + j) / 2;
					if (distances[m] > distance) {
						j = m;
					}
					else {
						i = m;
					}
				}

				return segments[i + 1].PointAt(distance - distances[i], out normal);
			}
		}

		public Layouts.Rectangle? GetBoundingBox() {
			return bounds;
		}

		public PathHandleData[] GetPathHandles() {
			//return segments.Select(s => s.GetPathHandles()).WhereNotNull().SelectAll().Distinct().ToArray();
			return handles is not null ? handles : segments.Select(s => s.GetPathHandles()).WhereNotNull().SelectAll().ToArray();
		}

		public static IPathCalculator? Create(IPathCalculator[] segments, PathHandleData[]? handles, bool isClosed) {
			if(segments.Length == 0) {
				return null;
			}
			else if(segments.Length == 1) {
				return segments[0];
			}

			float[] distances = new float[segments.Length];
			float running = 0f;
			Layouts.Rectangle? bounds = null;
			for (int i = 0; i < segments.Length; i++) {
				running += segments[i].Length;
				distances[i] = running;

				if(bounds == null) {
					bounds = segments[i].GetBoundingBox();
				}
				else if(segments[i].GetBoundingBox() is Rectangle segmentBounds) {
					bounds = Layouts.Rectangle.Union(bounds, segmentBounds);
				}
			}

			return new CompositePathCalculator(segments, distances, bounds, handles, isClosed);
		}
	}

}
