using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SharpSheets.Utilities {

	public class RegexDelimiter<T> {

		private readonly Regex delimiter;
		private readonly Func<Match, T> delimiterFunc;
		private readonly Func<string, T> spanFunc;

		public RegexDelimiter(Regex delimiter, Func<Match, T> delimiterFunc, Func<string, T> spanFunc) {
			this.delimiter = delimiter;
			this.delimiterFunc = delimiterFunc;
			this.spanFunc = spanFunc;
		}

		public IEnumerable<T> Split(string text) {

			MatchCollection matches = delimiter.Matches(text);

			if(matches.Count == 0) {
				yield return spanFunc(text);
				yield break;
			}

			yield return spanFunc(text.Substring(0, matches[0].Index));

			for (int i = 0; i < matches.Count; i++) {

				yield return delimiterFunc(matches[i]);

				int thisEnd = matches[i].Index + matches[i].Length;
				if (i+1 < matches.Count) {
					int nextStart = matches[i + 1].Index;
					yield return spanFunc(text.Substring(thisEnd, nextStart - thisEnd));
				}
				else {
					yield return spanFunc(text.Substring(thisEnd, text.Length - thisEnd));
				}

			}

		}

	}

}
