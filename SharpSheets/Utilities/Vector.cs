using System;

namespace SharpSheets.Utilities {
	public readonly struct Vector {

		public float X { get; }
		public float Y { get; }

		public Vector(float x, float y) {
			X = x;
			Y = y;
		}

		public Vector Normal() {
			float length = Length();
			return new Vector(X / length, Y / length);
		}

		public float Length() {
			return (float)Math.Sqrt(X * X + Y * Y);
		}

		public Vector Rotate(float theta) {
			float sinT = (float)Math.Sin(theta);
			float cosT = (float)Math.Cos(theta);
			float x = X * cosT - Y * sinT;
			float y = X * sinT + Y * cosT;
			return new Vector(x, y);
		}

		public float Rotation() {
			return (float)Math.Atan2(Y, X);
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static Vector Parse(string str, IFormatProvider? provider) {
			string[] parts = str.SplitAndTrim(2, ',');
			if (parts.Length != 2) {
				throw new FormatException($"\"{str}\" is not a valid vector.");
			}
			try {
				float x = float.Parse(parts[0], provider);
				float y = float.Parse(parts[1], provider);
				return new Vector(x, y);
			}
			catch (FormatException e) {
				throw new FormatException($"\"{str}\" is not a valid vector.", e);
			}
		}

		public static Vector Parse(string str) {
			return Parse(str, null);
		}

		public static Vector operator +(Vector a, Vector b) {
			return new Vector(a.X + b.X, a.Y + b.Y);
		}
		public static Vector operator +(Vector a, float b) {
			return new Vector(a.X + b, a.Y + b);
		}
		public static Vector operator +(float a, Vector b) {
			return new Vector(a + b.X, a + b.Y);
		}
		public static Vector operator -(Vector a, Vector b) {
			return new Vector(a.X - b.X, a.Y - b.Y);
		}
		public static Vector operator -(Vector a, float b) {
			return new Vector(a.X - b, a.Y - b);
		}
		public static Vector operator -(float a, Vector b) {
			return new Vector(a - b.X, a - b.Y);
		}
		public static Vector operator *(Vector a, float b) {
			return new Vector(a.X * b, a.Y * b);
		}
		public static Vector operator *(float a, Vector b) {
			return new Vector(a * b.X, a * b.Y);
		}
		public static Vector operator /(Vector a, float b) {
			return new Vector(a.X / b, a.Y / b);
		}

		public static implicit operator (float, float)(Vector value) {
			return (value.X, value.Y);
		}

		public static implicit operator Vector((float x, float y) values) {
			return new Vector(values.x, values.y);
		}

		public override string ToString() {
			return ToString(null);
		}

		public string ToString(IFormatProvider? formatProvider) {
			return $"{X.ToString(formatProvider)},{Y.ToString(formatProvider)}";
		}
	}
}
