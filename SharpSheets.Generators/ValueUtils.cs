using System;
using System.Collections.Generic;
using System.Text;

namespace SharpSheets.Generators {

	public static class ValueUtils {

		public static string ToCodeString(this bool value) {
			return value ? "true" : "false";
		}

		public static string ToCodeString(this float value) {
			return $"{value}f";
		}

		public static string ToRepr(this string? s) {
			// produce "..." with escapes for control characters, backslash and double-quote; or null keyword

			if (s is null) {
				return "null";
			}

			StringBuilder sb = new StringBuilder();
			sb.Append('"');
			foreach (char c in s) {
				switch (c) {
					case '\"':
						sb.Append("\\\""); break;
					case '\\':
						sb.Append("\\\\"); break;
					case '\0':
						sb.Append("\\0"); break;
					case '\a':
						sb.Append("\\a"); break;
					case '\b':
						sb.Append("\\b"); break;
					case '\f':
						sb.Append("\\f"); break;
					case '\n':
						sb.Append("\\n"); break;
					case '\r':
						sb.Append("\\r"); break;
					case '\t':
						sb.Append("\\t"); break;
					case '\v':
						sb.Append("\\v"); break;
					default:
						if (char.IsControl(c)) {
							// non-printable control, use \uXXXX
							sb.AppendFormat("\\u{0:X4}", (int)c);
						}
						else {
							sb.Append(c);
						}
						break;
				}
			}
			sb.Append('"');
			return sb.ToString();
		}

	}

}
