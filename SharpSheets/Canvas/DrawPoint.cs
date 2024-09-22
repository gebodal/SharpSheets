using SharpSheets.Utilities;
using System;

namespace SharpSheets.Canvas {

	public readonly struct DrawPoint : IEquatable<DrawPoint> {
		public static readonly DrawPoint Zero = new DrawPoint(0f, 0f);

		public float X { get; }
		public float Y { get; }
		public DrawPoint(float x, float y) {
			this.X = x;
			this.Y = y;
		}

		public DrawPoint Offset(float x, float y) {
			return new DrawPoint(this.X + x, this.Y + y);
		}

		public static DrawPoint operator +(DrawPoint a, DrawPoint b) {
			return new DrawPoint(a.X + b.X, a.Y + b.Y);
		}
		public static DrawPoint operator -(DrawPoint a, DrawPoint b) {
			return new DrawPoint(a.X - b.X, a.Y - b.Y);
		}

		public static DrawPoint operator +(DrawPoint a, Vector b) {
			return new DrawPoint(a.X + b.X, a.Y + b.Y);
		}
		public static DrawPoint operator -(DrawPoint a, Vector b) {
			return new DrawPoint(a.X - b.X, a.Y - b.Y);
		}
		public static DrawPoint operator +(Vector a, DrawPoint b) {
			return new DrawPoint(a.X + b.X, a.Y + b.Y);
		}
		public static DrawPoint operator -(Vector a, DrawPoint b) {
			return new DrawPoint(a.X - b.X, a.Y - b.Y);
		}

		public override string ToString() {
			return "(" + X + "," + Y + ")";
		}

		public static float Distance(DrawPoint a, DrawPoint b) {
			float dx = b.X - a.X;
			float dy = b.Y - a.Y;
			return (float)Math.Sqrt(dx * dx + dy * dy);
		}

		public bool Equals(DrawPoint other) {
			return X == other.X && Y == other.Y;
		}

		public override bool Equals(object? obj) {
			return obj is DrawPoint point && Equals(point);
		}

		public static bool operator ==(DrawPoint left, DrawPoint right) {
			return left.Equals(right);
		}

		public static bool operator !=(DrawPoint left, DrawPoint right) {
			return !(left == right);
		}

		public override int GetHashCode() {
			return HashCode.Combine(X, Y);
		}
	}

}
