using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpSheets.Utilities {

	public class RegexChunker {

		private readonly bool trimStart;
		private readonly Regex regex;
		private readonly Regex separatorRegex;

		public RegexChunker(string regex, RegexOptions options, string separatorRegex, RegexOptions separatorOptions, bool trimStart) {
			this.regex = new Regex(regex, options);
			this.separatorRegex = new Regex(@"^(?:" + separatorRegex + @")", separatorOptions);
			this.trimStart = trimStart;
		}

		public RegexChunker(string regex, bool trimStart) : this(regex, RegexOptions.None, @"\s+", RegexOptions.None, trimStart) { }
		public RegexChunker(string regex, string separatorRegex, bool trimStart) : this(regex, RegexOptions.None, separatorRegex, RegexOptions.None, trimStart) { }
		public RegexChunker(string regex, RegexOptions options, bool trimStart) : this(regex, options, @"\s+", RegexOptions.None, trimStart) { }

		public RegexChunker(Regex regex, bool trimStart) {
			this.regex = regex;
			this.separatorRegex = new Regex(@"\s+");
			this.trimStart = trimStart;
		}
		public RegexChunker(Regex regex, Regex separatorRegex, bool trimStart) {
			this.regex = regex;
			this.separatorRegex = separatorRegex;
			this.trimStart = trimStart;
		}

		public bool IsMatch(string input, int startAt) {
			int index = startAt;

			if (trimStart) {
				Match initialSeparatorMatch;
				while (index < input.Length && (initialSeparatorMatch = separatorRegex.Match(input.Substring(index))).Success) {
					if (initialSeparatorMatch.Length == 0 || initialSeparatorMatch.Index != 0) { break; }
					index += initialSeparatorMatch.Length;
				}
			}

			while (index < input.Length) {
				Match match = regex.Match(input, index);
				if (match.Success && match.Index == index) {
					index = match.Index + match.Length;

					Match separatorMatch;
					while (index < input.Length && (separatorMatch = separatorRegex.Match(input.Substring(index))).Success) {
						if (separatorMatch.Length == 0 || separatorMatch.Index != 0) { break; }
						index += separatorMatch.Length;
					}
				}
				else {
					//Console.WriteLine(input.Substring(index));
					return false;
				}
			}

			return true;
		}

		public bool IsMatch(string input) {
			return IsMatch(input, 0);
		}

		/// <summary></summary>
		/// <exception cref="FormatException">Thrown when the input cannot be chunked according to the provided regular expressions.</exception>
		public IEnumerable<Match> Matches(string input, int startAt) {
			int index = startAt;

			if (trimStart) {
				Match initialSeparatorMatch;
				while (index < input.Length && (initialSeparatorMatch = separatorRegex.Match(input.Substring(index))).Success) {
					if (initialSeparatorMatch.Length == 0 || initialSeparatorMatch.Index != 0) { break; }
					index += initialSeparatorMatch.Length;
				}
			}

			while (index < input.Length) {
				Match match = regex.Match(input, index);
				if (match.Success && match.Index == index) {
					yield return match;
					index = match.Index + match.Length;

					Match separatorMatch;
					while (index < input.Length && (separatorMatch = separatorRegex.Match(input.Substring(index))).Success) {
						if (separatorMatch.Length == 0 || separatorMatch.Index != 0) { break; }
						index += separatorMatch.Length;
					}
				}
				else {
					//Console.WriteLine(input.Substring(index));
					throw new FormatException("Could not chunk input.");
				}
			}
		}

		public IEnumerable<Match> Matches(string input) {
			return Matches(input, 0);
		}

		public IEnumerable<T> Matches<T>(string input, int startAt, Func<Match, T> selector) {
			return Matches(input, startAt).Select(selector);
		}

		public IEnumerable<T> Matches<T>(string input, Func<Match, T> selector) {
			return Matches(input).Select(selector);
		}

		public IEnumerable<T> Matches<T>(string input, int startAt, Func<string, T> selector) {
			return Matches(input, startAt).Select(m => selector(m.Value));
		}

		public IEnumerable<T> Matches<T>(string input, Func<string, T> selector) {
			return Matches(input).Select(m => selector(m.Value));
		}
	}

}
