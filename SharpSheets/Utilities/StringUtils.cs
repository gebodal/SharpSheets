using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SharpSheets.Utilities {
	public static class StringUtils {

		public static string[] SplitAndTrim(this string str, params string[] separator) {
			return str.Split(separator, StringSplitOptions.None).Select(s => s.Trim()).ToArray();
		}

		public static string[] SplitAndTrim(this string str, params char[] separator) {
			return str.Split(separator).Select(s => s.Trim()).ToArray();
		}

		public static string[] SplitAndTrim(this string str, int count, params char[] separator) {
			return str.Split(separator, count).Select(s => s.Trim()).ToArray();
		}

		public static string[] SplitAt(this string source, params int[] indexes) {
			//indexes = indexes.Distinct().OrderBy(x => x).ToArray();
			string[] output = new string[indexes.Length + 1];
			int pos = 0;

			for (int i = 0; i < indexes.Length; pos = indexes[i++]) {
				output[i] = source.Substring(pos, indexes[i] - pos);
			}

			output[indexes.Length] = source.Substring(pos);
			return output;
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException">Thrown when <paramref name="chunkSize"/> is negative or zero.</exception>
		public static IEnumerable<string> Chunk(this string str, int chunkSize) {
			if (chunkSize <= 0) { throw new ArgumentException("Chunk size must be a positive integer."); }
			for (int i = 0; i < str.Length; i += chunkSize) {
				yield return str.Substring(i, chunkSize);
			}
		}

		public static bool StartsWith(this string str, StringComparison comparisonType, params string[] prefixes) {
			return prefixes.Where(p => str.StartsWith(p, comparisonType)).Any();
		}
		public static bool StartsWith(this string str, params string[] prefixes) {
			return prefixes.Where(p => str.StartsWith(p)).Any();
		}
		public static bool EndsWith(this string str, StringComparison comparisonType, params string[] prefixes) {
			return prefixes.Where(p => str.EndsWith(p, comparisonType)).Any();
		}
		public static bool EndsWith(this string str, params string[] prefixes) {
			return prefixes.Where(p => str.EndsWith(p)).Any();
		}

		public static string ToTitleCase(this string str) {
			return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
		}

		/// <summary>
		/// Returns the length of any prefix shared by both strings. Returns 0 if the two strings do not share a prefix.
		/// </summary>
		/// <param name="a">The first string.</param>
		/// <param name="b">The second string.</param>
		/// <returns></returns>
		public static int PrefixOverlapLength(string a, string b) {
			int c = 0;
			int maxLength = Math.Min(a.Length, b.Length);
			while (c < maxLength) {
				if (a[c] != b[c]) {
					return c;
				}
				c++;
			}
			return c;
		}

	}
}
