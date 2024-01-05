using SharpSheets.Canvas;
using System;
using System.Linq;

namespace SharpSheets.Utilities {
	public static class MathUtils {

		public static float PI = (float)Math.PI;

		public static double Rad2Deg(double radians) {
			return (180 / Math.PI) * radians;
		}
		public static double Deg2Rad(double degrees) {
			return (Math.PI / 180) * degrees;
		}

		public static float Lerp(float a, float b, float t) {
			return a + (b - a) * t;
		}

		public static DrawPoint Lerp(DrawPoint a, DrawPoint b, float t) {
			return new DrawPoint(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));
		}

		public static byte Clamp(this byte x, byte min, byte max) {
			return Math.Max(min, Math.Min(x, max));
		}

		public static int Clamp(this int x, int min, int max) {
			return Math.Max(min, Math.Min(x, max));
		}

		public static float Clamp(this float x, float min, float max) {
			return Math.Max(min, Math.Min(x, max));
		}

		public static double Clamp(this double x, double min, double max) {
			return Math.Max(min, Math.Min(x, max));
		}

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static byte Min(params byte[] values) {
			return values.Min();
		}

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static int Min(params int[] values) {
			return values.Min();
		}

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static float Min(params float[] values) {
			return values.Min();
		}

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static double Min(params double[] values) {
			return values.Min();
		}

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static byte Max(params byte[] values) {
			return values.Max();
		}

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static int Max(params int[] values) {
			return values.Max();
		}

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static float Max(params float[] values) {
			return values.Max();
		}

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static double Max(params double[] values) {
			return values.Max();
		}

	}
}
