using System;

namespace GeboPdf.Utilities {

	public class Transform {
		public readonly float a, b, c, d, e, f;

		private Transform(float a, float b, float c, float d, float e, float f) {
			this.a = a;
			this.b = b;
			this.c = c;
			this.d = d;
			this.e = e;
			this.f = f;
		}

		public Transform Clone() {
			return new Transform(a, b, c, d, e, f);
		}

		public static readonly Transform Identity = new Transform(1, 0, 0, 1, 0, 0);

		public static Transform Matrix(float a, float b, float c, float d, float e, float f) {
			return new Transform(a, b, c, d, e, f);
		}

		public static Transform Translate(float x, float y) {
			return new Transform(1, 0, 0, 1, x, y);
		}

		public static Transform Scale(float scaleX, float scaleY) {
			return new Transform(scaleX, 0, 0, scaleY, 0, 0);
		}

		public static Transform Rotate(float theta) {
			float cos = (float)Math.Cos(theta);
			float sin = (float)Math.Sin(theta);
			return new Transform(cos, sin, -sin, cos, 0, 0);
		}

		public static Transform Rotate(float theta, float x, float y) {
			// For rotating about an arbitrary point
			// Still need to verify this is correct?
			float cos = (float)Math.Cos(theta);
			float sin = (float)Math.Sin(theta);
			float e = -x * cos + y * sin + x;
			float f = -x * sin - y * cos + y;
			return new Transform(cos, sin, -sin, cos, e, f);
		}

		public static Transform Skew(float x, float y) {
			return new Transform(1, (float)Math.Tan(y), (float)Math.Tan(x), 1, 0, 0);
		}

		public static Transform SkewX(float theta) {
			return new Transform(1, 0, (float)Math.Tan(theta), 1, 0, 0);
		}
		public static Transform SkewY(float theta) {
			return new Transform(1, (float)Math.Tan(theta), 0, 1, 0, 0);
		}

		public (float xt, float yt) Map(float x, float y) {
			float xt = (a * x) + (c * y) + e;
			float yt = (b * x) + (d * y) + f;
			return (xt, yt);
		}

		public Transform Invert() {
			float det = a * d - b * c;
			if (det == 0) { throw new ArithmeticException("Transform is not invertible."); }
			float A = d / det;
			float B = -b / det;
			float C = -c / det;
			float D = a / det;
			float E = (c * f - d * e) / det;
			float F = (b * e - a * f) / det;
			return new Transform(A, B, C, D, E, F);
		}

		public static Transform operator *(Transform t1, Transform t2) {
			// Still need to verify this is correct?
			float a = (t1.a * t2.a) + (t1.c * t2.b);
			float c = (t1.a * t2.c) + (t1.c * t2.d);
			float e = (t1.a * t2.e) + (t1.c * t2.f) + t1.e;
			float b = (t1.b * t2.a) + (t1.d * t2.b);
			float d = (t1.b * t2.c) + (t1.d * t2.d);
			float f = (t1.b * t2.e) + (t1.d * t2.f) + t1.f;
			return new Transform(a, b, c, d, e, f);
		}

		public override string ToString() {
			return $"({a}, {b}, {c}, {d}, {e}, {f})";
		}
	}

}
