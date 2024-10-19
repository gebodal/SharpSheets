using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpSheets.Parsing {

	public static class StringParsing {

		private static string NormalizeString(string text) {
			return text.Normalize(NormalizationForm.FormC); // Is this the correct form here?
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static string Parse(string raw) {
			StringBuilder output = new StringBuilder(raw.Length);

			bool escaped = false;
			int i = 0;
			while(i < raw.Length) {

				if (escaped) {
					if (raw[i] == 'n') {
						output.Append('\n');
					}
					else if (raw[i] == 't') {
						output.Append('\t');
					}
					else if (raw[i] == 'u') {
						if (i + 4 >= raw.Length) {
							throw new FormatException("Incomplete Unicode code point value.");
						}

						string codeStr = raw.Substring(i + 1, 4);
						string unicode = GetUnicode(codeStr);
						output.Append(unicode);

						i += 4;
					}
					else if (raw[i] == 'U') {
						if (i + 8 >= raw.Length) {
							throw new FormatException("Incomplete Unicode code point value.");
						}

						string codeStr = raw.Substring(i + 1, 8);
						string unicode = GetUnicode(codeStr);
						output.Append(unicode);

						i += 8;
					}
					else if (raw[i] != '0') { // Use "\0" as a "nothing" escape sequence
						output.Append(raw[i]);
					}
					escaped = false;
				}
				else if(raw[i] == '\\') {
					escaped = true;
				}
				else if (raw[i] == '-') {
					if(i + 2 < raw.Length && raw[i + 1] == '-' && raw[i + 2] == '-') {
						output.Append('\u2014');
						i += 2;
					}
					else if (i + 1 < raw.Length && raw[i + 1] == '-') {
						output.Append('\u2013');
						i += 1;
					}
					else {
						output.Append(raw[i]);
					}
				}
				else {
					output.Append(raw[i]);
				}

				i++;
			}

			if (escaped) {
				throw new FormatException("Invalid escape sequence at end of string.");
			}

			return NormalizeString(output.ToString());
		}

		private static readonly int MAX_CHAR_VALUE = 0xFF;
		private static readonly int MAX_UNICODE_LOWER = 0xFFFF;
		//private static readonly long MAX_UNICODE_UPPER = 0xFFFFFFFF;

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static string Escape(string unescaped) {
			StringBuilder output = new StringBuilder(unescaped.Length);

			for (int i = 0; i < unescaped.Length; i++) {
				if(unescaped[i] == '\\') {
					output.Append("\\\\");
				}
				else if (unescaped[i] == '\n') {
					output.Append("\\n");
				}
				else if (unescaped[i] == '\t') {
					output.Append("\\t");
				}
				else if (unescaped[i] > MAX_CHAR_VALUE) {
					if (unescaped[i] > MAX_UNICODE_LOWER) {
						output.Append("\\U");
						output.Append(((long)unescaped[i]).ToString("X8"));
					}
					else {
						output.Append("\\u");
						output.Append(((int)unescaped[i]).ToString("X4"));
					}
				}
				else {
					output.Append(unescaped[i]);
				}
			}

			return output.ToString();
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		private static string GetUnicode(string codeStr) {
			try {
				int code = int.Parse(codeStr, System.Globalization.NumberStyles.HexNumber);
				string unicodeString = char.ConvertFromUtf32(code);
				return unicodeString;
			}
			catch (FormatException e) {
				throw new FormatException($"Invalid code point value: \"{codeStr}\"", e);
			}
			catch (ArgumentOutOfRangeException e) {
				throw new FormatException($"Unrecognized Unicode code point: \"{codeStr.ToUpper()}\"", e);
			}
		}

		public static string[] SplitOnUnescaped(string raw, char separator) {

			List<string> splits = new List<string>();
			int head = 0;

			bool escaped = false;
			int i = 0;
			while(i < raw.Length) {
				if (escaped) {
					if (raw[i] == 'u') {
						i += 4;
					}
					else if (raw[i] == 'U') {
						i += 8;
					}

					escaped = false;
				}
				else if(raw[i] == '\\') {
					escaped = true;
				}
				else if(raw[i] == separator) {
					string split = raw.Substring(head, i - head);
					splits.Add(split);
					head = i + 1;
				}

				i++;
			}

			splits.Add(raw.Substring(head));

			return splits.ToArray();
		}

		public static string Unescape(string text, params char[] toUnescape) {
			HashSet<char> unescapeChars = new HashSet<char>(toUnescape);

			StringBuilder output = new StringBuilder(text.Length);

			bool escaped = false;
			for (int i = 0; i < text.Length; i++) {
				if (escaped) {
					escaped = false;
					if (!unescapeChars.Contains(text[i])) {
						output.Append('\\');
					}
					output.Append(text[i]);
				}
				else if (text[i] == '\\' && i != text.Length - 1) {
					escaped = true;
				}
				else {
					output.Append(text[i]);
				}
			}

			return output.ToString();
		}

		public static string EnsureEscaped(string partial, params char[] toEscape) {
			HashSet<char> escapeChars = new HashSet<char>(toEscape);

			StringBuilder output = new StringBuilder(partial.Length);

			bool escaped = false;
			for (int i = 0; i < partial.Length; i++) {
				if (escaped) {
					escaped = false;
				}
				else if (partial[i] == '\\') {
					escaped = true;
				}
				else if (escapeChars.Contains(partial[i])) {
					output.Append('\\');
				}

				output.Append(partial[i]);
			}

			return output.ToString();
		}

		#region RichString

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static RichString ParseRich(string raw) {

			List<char> chars = new List<char>(raw.Length);
			List<TextFormat> formats = new List<TextFormat>(raw.Length);

			TextFormat currentFormat = TextFormat.REGULAR;
			bool escaped = false;

			void AddChar(char c) {
				chars.Add(c);
				formats.Add(currentFormat);
			}

			int i = 0;
			while (i < raw.Length) {

				if (escaped) {

					if (raw[i] == 'n') {
						AddChar('\n');
					}
					else if (raw[i] == 't') {
						AddChar('\t');
					}
					else if (raw[i] == 'u') {
						if (i + 4 >= raw.Length) {
							throw new FormatException("Incomplete Unicode code point value.");
						}

						string codeStr = raw.Substring(i + 1, 4);
						string unicode = GetUnicode(codeStr);

						for (int c = 0; c < unicode.Length; c++) {
							AddChar(unicode[c]);
						}

						i += 4;
					}
					else if (raw[i] == 'U') {
						if (i + 8 >= raw.Length) {
							throw new FormatException("Incomplete Unicode code point value.");
						}

						string codeStr = raw.Substring(i + 1, 8);
						string unicode = GetUnicode(codeStr);

						for (int c = 0; c < unicode.Length; c++) {
							AddChar(unicode[c]);
						}

						i += 8;
					}
					else if (raw[i] != '0') { // Use "\0" as a "nothing" escape sequence
						AddChar(raw[i]);
					}

					escaped = false;
				}
				else if (raw[i] == '\\') {
					escaped = true;
				}
				else if (raw[i] == '*') {
					if ((currentFormat & TextFormat.BOLD) == TextFormat.BOLD) { // Already bold
						currentFormat &= ~TextFormat.BOLD; // Remove bold
					}
					else { // Not bold
						currentFormat |= TextFormat.BOLD;
					}
				}
				else if (raw[i] == '_') {
					if ((currentFormat & TextFormat.ITALIC) == TextFormat.ITALIC) { // Already italic
						currentFormat &= ~TextFormat.ITALIC; // Add bold
					}
					else { // Not bold
						currentFormat |= TextFormat.ITALIC;
					}
				}
				else if (raw[i] == '-') {
					if (i + 2 < raw.Length && raw[i + 1] == '-' && raw[i + 2] == '-') {
						AddChar('\u2014');
						i += 2;
					}
					else if (i + 1 < raw.Length && raw[i + 1] == '-') {
						AddChar('\u2013');
						i += 1;
					}
					else {
						AddChar(raw[i]);
					}
				}
				else {
					AddChar(raw[i]);
				}

				i++;
			}

			RichString initial = new RichString(chars.ToArray(), formats.ToArray());
			List<char> finalChars = new List<char>();
			List<TextFormat> finalFormats = new List<TextFormat>(raw.Length);

			foreach((string segment, TextFormat format) in initial.GetSegments()) {
				string normalized = NormalizeString(segment);
				finalChars.AddRange(normalized);
				finalFormats.AddRange(format.Yield(normalized.Length));
			}

			return new RichString(finalChars.ToArray(), finalFormats.ToArray());
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static string EscapeRich(RichString unescaped) {
			StringBuilder output = new StringBuilder(unescaped.Length);

			TextFormat currentFormat = TextFormat.REGULAR;

			for (int i = 0; i < unescaped.Length; i++) {
				// Check if the format has changed
				if (unescaped.formats[i] != currentFormat) {
					if (((currentFormat ^ unescaped.formats[i]) & TextFormat.BOLD) == TextFormat.BOLD) {
						output.Append('*');
					}

					if (((currentFormat ^ unescaped.formats[i]) & TextFormat.ITALIC) == TextFormat.ITALIC) {
						output.Append('_');
					}

					currentFormat = unescaped.formats[i];
				}

				// Check if anything needs escaping
				if (unescaped[i] == '\\') {
					output.Append("\\\\");
				}
				else if (unescaped[i] == '\n') {
					output.Append("\\n");
				}
				else if (unescaped[i] == '\t') {
					output.Append("\\t");
				}
				else if(unescaped[i] == '*') {
					output.Append("\\*");
				}
				else if (unescaped[i] == '_') {
					output.Append("\\_");
				}
				else if (unescaped[i] > MAX_CHAR_VALUE) {
					if (unescaped[i] > MAX_UNICODE_LOWER) {
						output.Append("\\U");
						output.Append(((long)unescaped[i]).ToString("X8"));
					}
					else {
						output.Append("\\u");
						output.Append(((int)unescaped[i]).ToString("X4"));
					}
				}
				else {
					// Otherwise, just output this character
					output.Append(unescaped[i]);
				}
			}

			// Make sure we don't have a dangling format at the end
			if ((currentFormat & TextFormat.BOLD) == TextFormat.BOLD) {
				output.Append('*');
			}
			if ((currentFormat & TextFormat.ITALIC) == TextFormat.ITALIC) {
				output.Append('_');
			}

			return output.ToString();
		}

		#endregion

	}

}
