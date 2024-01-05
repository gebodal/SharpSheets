using System.Collections.Generic;
using System;
using System.Text;

namespace SharpSheets.Utilities {
	public static class DelimitedUtils {

		// TODO This needs some serious improvement
		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static string[] SplitDelimitedString(string text, char delimiter = ',', bool trim = false) {
			List<string> substrings = new List<string>();

			StringBuilder sb = new StringBuilder();
			bool insideQuote = false;
			bool escaped = false;
			for (int i = 0; i < text.Length; i++) {
				if (escaped) {
					escaped = false;
					if (text[i] == 'n') {
						sb.Append('\n');
					}
					else if (text[i] == 't') {
						sb.Append('\t');
					}
					else {
						sb.Append(text[i]);
					}
					continue;
				}
				else if (!insideQuote && text[i] == delimiter) {
					string result = sb.ToString();
					if (trim) {
						result = result.Trim();
					}
					substrings.Add(result);
					sb.Clear();
				}
				else if (text[i] == '\\') {
					escaped = true;
				}
				else if (insideQuote && text[i] == '"' && i < text.Length - 1 && text[i + 1] == '"') {
					// Double "" used for " inside quoted fields (essentially, the next character, '"', is escaped)
					escaped = true;
					continue;
				}
				else if (text[i] == '"') {
					insideQuote = !insideQuote;
				}
				else {
					sb.Append(text[i]);
				}
			}

			if (insideQuote) {
				throw new FormatException("Unclosed quoted field in delimited string.");
			}

			string finalresult = sb.ToString();
			if (trim) {
				finalresult = finalresult.Trim();
			}
			substrings.Add(finalresult);

			string[] parts = substrings.ToArray();
			/*
			for (int i = 0; i < parts.Length; i++) {
				if (parts[i].Length >= 2 && parts[i][0] == '"' && parts[i][parts[i].Length - 1] == '"' && parts[i][parts[i].Length - 2] != '\\') {
					parts[i] = parts[i].Substring(1, parts[i].Length - 2);
				}
			}
			*/

			return parts;
		}

	}
}