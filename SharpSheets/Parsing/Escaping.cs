using System;
using System.Collections.Generic;
using System.Text;

namespace SharpSheets.Parsing {

	public static class Escaping {

		public static readonly char EscapeChar = '\\';

		/// <summary></summary>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public static string[] SplitUnescaped(string str, char[] separator, int count, StringSplitOptions options) {
			if (count < 0) {
				throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero.");
			}
			else if (count == 0) {
				return Array.Empty<string>();
			}

			List<string> split = new List<string>();
			HashSet<char> seps = new HashSet<char>(separator);

			void Add(string entry) {
				if (options == StringSplitOptions.RemoveEmptyEntries && entry.Length == 0) {
					return;
				}
				else {
					split.Add(entry);
				}
			}

			int start = 0;
			bool escape = false;
			for (int i = 0; i < str.Length && split.Count < count - 1; i++) {
				if (escape) {
					escape = false;
				}
				else if (str[i] == EscapeChar) {
					escape = true;
				}
				else if (seps.Contains(str[i])) {
					Add(str.Substring(start, i - start));
					start = i + 1;
				}
			}

			Add(str.Substring(start, str.Length - start));

			return split.ToArray();
		}

		public static string[] SplitUnescaped(string str, char[] separator, int count) {
			return SplitUnescaped(str, separator, count, StringSplitOptions.None);
		}

		public static string[] SplitUnescaped(string str, char[] separator) {
			return SplitUnescaped(str, separator, str.Length);
		}

		public static string ReplaceEscapeSequences(string str, Dictionary<char, string> escapeSequences) {
			StringBuilder builder = new StringBuilder();
			bool escape = false;
			for (int i = 0; i < str.Length; i++) {
				if (escape) {
					//string escaped = null;
					if(escapeSequences?.TryGetValue(str[i], out string? escaped) ?? false) {
						builder.Append(escaped);
					}
					else {
						builder.Append(str[i]);
					}
					escape = false;
				}
				else if (str[i] == EscapeChar) {
					escape = true;
				}
				else {
					builder.Append(str[i]);
				}
			}
			return builder.ToString();
		}

		public static string Escape(string str, char[] escapedChars) {
			HashSet<char> escape = new HashSet<char>(escapedChars) { EscapeChar }; // Also always escape the escape character
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < str.Length; i++) {
				if (escape.Contains(str[i])) {
					builder.Append(EscapeChar.ToString() + str[i].ToString());
				}
				else {
					builder.Append(str[i]);
				}
			}
			return builder.ToString();
		}

		public static string Unescape(string str) {
			StringBuilder builder = new StringBuilder();
			bool escape = false;
			for (int i = 0; i < str.Length; i++) {
				if (escape) {
					escape = false;
				}
				else if (str[i] == EscapeChar) {
					escape = true;
					continue;
				}

				builder.Append(str[i]);
			}
			return builder.ToString();
		}

	}

}
