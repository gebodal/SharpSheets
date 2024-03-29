using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SharpSheets.Canvas;

namespace SharpSheets.Canvas.Text {

	[DebuggerDisplay("{Formatted}")]
	public class RichString {

		public static readonly RichString Empty = Create("", TextFormat.REGULAR);

		public readonly char[] chars;
		public readonly TextFormat[] formats;

		public int Length { get { return chars.Length; } }

		/// <summary>
		/// The unformatted and unescaped version of this string (i.e. without any markdown syntax indicating text format).
		/// </summary>
		public string Text { get; }
		/// <summary>
		/// Formatted and escaped version of this string.
		/// </summary>
		public string Formatted {
			get {
				StringBuilder sb = new StringBuilder();
				TextFormat currentFormat = TextFormat.REGULAR;
				for (int i = 0; i < chars.Length; i++) {
					if (formats[i] != currentFormat) {
						if ((currentFormat == TextFormat.REGULAR && formats[i] == TextFormat.BOLD) || (currentFormat == TextFormat.BOLD && formats[i] == TextFormat.REGULAR)) {
							sb.Append('*');
						}
						else if ((currentFormat == TextFormat.REGULAR && formats[i] == TextFormat.ITALIC) || (currentFormat == TextFormat.ITALIC && formats[i] == TextFormat.REGULAR)) {
							sb.Append('_');
						}
						else if (currentFormat == TextFormat.BOLDITALIC) {
							if (formats[i] == TextFormat.REGULAR) {
								sb.Append("*_");
							}
							else if (formats[i] == TextFormat.BOLD) {
								sb.Append('_');
							}
							else { // formats[i] == TextFormat.ITALIC
								sb.Append('*');
							}
						}
						else { // formats[i] == TextFormat.BOLDITALIC
							if (currentFormat == TextFormat.REGULAR) {
								sb.Append("_*");
							}
							else if (currentFormat == TextFormat.BOLD) {
								sb.Append('_');
							}
							else { // currentFormat == TextFormat.ITALIC
								sb.Append('*');
							}
						}
						currentFormat = formats[i];
					}
					if(chars[i] == '*' || chars[i] == '_') { sb.Append('\\'); }
					sb.Append(chars[i]);
				}
				if (currentFormat == TextFormat.BOLD) {
					sb.Append('*');
				}
				else if (currentFormat == TextFormat.ITALIC) {
					sb.Append('_');
				}
				else if (currentFormat == TextFormat.BOLDITALIC) {
					sb.Append("*_");
				}
				return sb.ToString();
			}
		}

		public char this[int i] { get { return chars[i]; } }

		private RichString(char[] chars, TextFormat[] formats, string strippedText) {
			this.chars = chars;
			this.formats = formats;
			this.Text = strippedText;
		}

		public RichString(char[] chars, TextFormat[] formats) : this(chars, formats, new string(chars)) { }

		public RichString(string text) : this(text, TextFormat.REGULAR) { }

		public RichString(string text, TextFormat startingFormat) {

			// TODO Does this properly take account of escape backslashes "\"? (Do they cause null chars?)

			List<char> chars = new List<char>();
			List<TextFormat> formats = new List<TextFormat>();

			TextFormat currentType = startingFormat;
			bool escapeNext = false;
			for (int i = 0; i < text.Length; i++) {
				//Console.WriteLine("Character: " + text[i]);
				if (!escapeNext && text[i] == '\\') {
					escapeNext = true;
				}
				else if (!escapeNext && "*_".Contains(text[i])) {
					//TextFormat newType;
					if (text[i] == '*') {
						if (currentType == TextFormat.REGULAR) {
							//Console.WriteLine("Start bold.");
							currentType = TextFormat.BOLD;
						}
						else if (currentType == TextFormat.BOLD) {
							//Console.WriteLine("End bold.");
							currentType = TextFormat.REGULAR;
						}
						else if (currentType == TextFormat.ITALIC) {
							//Console.WriteLine("Start bolditalic.");
							currentType = TextFormat.BOLDITALIC;
						}
						else { //currentType == TextFormat.BOLDITALIC
							   //Console.WriteLine("Move from bolditalic to italic.");
							currentType = TextFormat.ITALIC;
						}
					}
					else { // text[i] == '_'
						if (currentType == TextFormat.REGULAR) {
							//Console.WriteLine("Start italic.");
							currentType = TextFormat.ITALIC;
						}
						else if (currentType == TextFormat.BOLD) {
							//Console.WriteLine("Start bolditalic.");
							currentType = TextFormat.BOLDITALIC;
						}
						else if (currentType == TextFormat.ITALIC) {
							//Console.WriteLine("End italic.");
							currentType = TextFormat.REGULAR;
						}
						else { //currentType == TextFormat.BOLDITALIC
							   //Console.WriteLine("Move from bolditalic to bold.");
							currentType = TextFormat.BOLD;
						}
					}
					//currentType = newType;
				}
				else {
					//Console.WriteLine("Just add character.");
					chars.Add(text[i]);
					formats.Add(currentType);

					escapeNext = false;
				}
			}

			this.chars = chars.ToArray();
			this.formats = formats.ToArray();
			Text = new string(this.chars);
		}

		public static RichString Create(string text, TextFormat format) {
			char[] chars = new char[text.Length];
			TextFormat[] formats = new TextFormat[text.Length];

			for(int i=0; i<text.Length; i++) {
				chars[i] = text[i];
				formats[i] = format;
			}

			return new RichString(chars, formats, text);
		}

		public static explicit operator RichString(string input) {
			return new RichString(input);
		}

		public static RichString operator +(RichString a, RichString b) {
			return new RichString(a.chars.Concat(b.chars).ToArray(), a.formats.Concat(b.formats).ToArray(), a.Text + b.Text);
		}

		public RichString Clone() {
			char[] charsClone = new char[chars.Length];
			TextFormat[] formatsClone = new TextFormat[formats.Length];
			for (int i = 0; i < Length; i++) {
				charsClone[i] = chars[i];
				formatsClone[i] = formats[i];
			}
			return new RichString(charsClone, formatsClone, Text);
		}

		public RichString Substring(int startIndex, int length) {
			char[] subchars = new char[length];
			TextFormat[] subformats = new TextFormat[length];

			for (int i = 0; i < length; i++) {
				subchars[i] = chars[startIndex + i];
				subformats[i] = formats[startIndex + i];
			}

			return new RichString(subchars, subformats, Text.Substring(startIndex, length));
		}

		public RichString Substring(int startIndex) {
			return Substring(startIndex, Length - startIndex);
		}

		/*
		public float GetWidth(PdfFontGrouping fonts, float fontSize) {
			float total = 0f;
			for (int i = 0; i < chars.Length; i++) {
				total += fonts[formats[i]].GetWidth(chars[i], fontSize);
			}
			return total;
		}

		public float GetDescent(PdfFontGrouping fonts, float fontSize) {
			return fonts.Select(f => f.GetDescent(Text, fontSize)).Min();
		}

		public float GetAscent(PdfFontGrouping fonts, float fontSize) {
			return fonts.Select(f => f.GetAscent(Text, fontSize)).Max();
		}
		*/

		public RichString[] Split(char separator) {
			
			List<int> separatorIndexes = new List<int>();

			for (int i = 0; i < chars.Length; i++) {
				if (chars[i] == separator) {
					separatorIndexes.Add(i);
				}
			}

			if (separatorIndexes.Count == 0) {
				return new RichString[] { new RichString(this.chars, this.formats, this.Text) };
			}
			else {
				List<RichString> substrings = new List<RichString>();

				int start;
				int end;
				int length;
				char[] splitChars;
				TextFormat[] splitFormat;
				if (separatorIndexes.Count > 0) {
					start = 0;
					end = separatorIndexes[0];
					length = end - start;
					splitChars = new char[length];
					splitFormat = new TextFormat[length];
					for (int i = 0; i < end; i++) {
						splitChars[i] = chars[i];
						splitFormat[i] = formats[i];
					}
					substrings.Add(new RichString(splitChars, splitFormat, Text.Substring(start, length)));
				}
				if (separatorIndexes.Count >= 1) {
					for (int s = 0; s < separatorIndexes.Count - 1; s++) {
						start = separatorIndexes[s] + 1;
						end = separatorIndexes[s + 1];
						length = end - start;
						splitChars = new char[length];
						splitFormat = new TextFormat[length];
						for (int i = start; i < end; i++) {
							splitChars[i - start] = chars[i];
							splitFormat[i - start] = formats[i];
						}
						substrings.Add(new RichString(splitChars, splitFormat, Text.Substring(start, length)));
					}

					start = separatorIndexes[separatorIndexes.Count - 1] + 1;
					end = chars.Length;
					length = end - start;
					splitChars = new char[length];
					splitFormat = new TextFormat[length];
					for (int i = start; i < end; i++) {
						splitChars[i - start] = chars[i];
						splitFormat[i - start] = formats[i];
					}
					substrings.Add(new RichString(splitChars, splitFormat, Text.Substring(start, length)));
				}

				return substrings.ToArray();
			}
		}

		public static RichString Join(RichString separator, params RichString[] values) {
			int count = separator.Length * Math.Max(values.Length - 1, 0);
			for (int i = 0; i < values.Length; i++) {
				count += values[i].Length;
			}

			char[] joinedChars = new char[count];
			TextFormat[] joinedFormats = new TextFormat[count];

			int counter = 0;
			for (int i = 0; i < values.Length; i++) {
				for (int j = 0; j < values[i].Length; j++) {
					joinedChars[counter] = values[i].chars[j];
					joinedFormats[counter] = values[i].formats[j];
					counter++;
				}
				if (i != values.Length - 1) {
					for (int s = 0; s < separator.Length; s++) {
						joinedChars[counter] = separator.chars[s];
						joinedFormats[counter] = separator.formats[s];
						counter++;
					}
				}
			}

			return new RichString(joinedChars, joinedFormats, string.Join(separator.Text, values.Select(v => v.Text)));
		}

		public static RichString Join(string separator, TextFormat separatorFormat, params RichString[] values) {
			return Join(new RichString(separator, separatorFormat), values);
		}

		public static RichString Join(RichString separator, IEnumerable<RichString> values) {
			return Join(separator, values.ToArray());
		}
		public static RichString Join(string separator, TextFormat separatorFormat, IEnumerable<RichString> values) {
			return Join(separator, separatorFormat, values.ToArray());
		}

		public RichString ApplyFormat(TextFormat format) {
			TextFormat[] newFormats = (TextFormat[])formats.Clone();

			for (int i = 0; i < newFormats.Length; i++) {
				if (format == TextFormat.REGULAR) {
					newFormats[i] = TextFormat.REGULAR;
				}
				else if (format == TextFormat.BOLDITALIC) {
					newFormats[i] = TextFormat.BOLDITALIC;
				}
				else if (format == TextFormat.BOLD) {
					if (newFormats[i] == TextFormat.ITALIC) {
						newFormats[i] = TextFormat.BOLDITALIC;
					}
					else if (newFormats[i] == TextFormat.REGULAR) {
						newFormats[i] = TextFormat.BOLD;
					}
				}
				else if (format == TextFormat.ITALIC) {
					if (newFormats[i] == TextFormat.BOLD) {
						newFormats[i] = TextFormat.BOLDITALIC;
					}
					else if (newFormats[i] == TextFormat.REGULAR) {
						newFormats[i] = TextFormat.ITALIC;
					}
				}
			}

			return new RichString((char[])chars.Clone(), newFormats, Text);
		}

		public RichString ApplyFormat(Regex regex, TextFormat format) {
			MatchCollection matches = regex.Matches(Text);

			TextFormat[] newFormats = (TextFormat[])formats.Clone();

			foreach(Match match in matches) {
				for(int i=match.Index; i<match.Index+match.Length; i++) {
					if(format == TextFormat.REGULAR) {
						newFormats[i] = TextFormat.REGULAR;
					}
					else if(format == TextFormat.BOLDITALIC) {
						newFormats[i] = TextFormat.BOLDITALIC;
					}
					else if(format == TextFormat.BOLD) {
						if(newFormats[i] == TextFormat.ITALIC) {
							newFormats[i] = TextFormat.BOLDITALIC;
						}
						else if(newFormats[i] == TextFormat.REGULAR) {
							newFormats[i] = TextFormat.BOLD;
						}
					}
					else if(format == TextFormat.ITALIC) {
						if(newFormats[i] == TextFormat.BOLD) {
							newFormats[i] = TextFormat.BOLDITALIC;
						}
						else if(newFormats[i] == TextFormat.REGULAR) {
							newFormats[i] = TextFormat.ITALIC;
						}
					}
				}
			}

			return new RichString((char[])chars.Clone(), newFormats, Text);
		}

		public IEnumerable<(string text, TextFormat format)> GetSegments() {
			StringBuilder builder = new StringBuilder(Length);
			TextFormat format = TextFormat.REGULAR;

			for (int i = 0; i < Length; i++) {
				if (format != formats[i]) {
					if (builder.Length > 0) {
						yield return (builder.ToString(), format);
						builder.Clear();
					}
					format = formats[i];
				}
				builder.Append(chars[i]);
			}

			if (builder.Length > 0) {
				yield return (builder.ToString(), format);
			}
		}
	}

}
