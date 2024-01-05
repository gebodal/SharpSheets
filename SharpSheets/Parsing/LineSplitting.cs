using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SharpSheets.Parsing {

	public static class LineSplitting {

		public static IEnumerable<ContextValue<string>> SplitLines(string document) {
			// Would be good to have access to document offsets here, to include with DocumentSpan information?
			//string[] lines = Regex.Split(document, "\r\n|\r|\n", RegexOptions.Multiline, TimeSpan.FromMilliseconds(500));
			MatchCollection lineMatches = Regex.Matches(document, @"^[^\n]*$", RegexOptions.Multiline, TimeSpan.FromMilliseconds(500));

			for (int lineIdx = 0; lineIdx < lineMatches.Count; lineIdx++) {
				Match lineMatch = lineMatches[lineIdx];
				
				string line = lineMatch.Value.TrimEnd('\r');

				DocumentSpan location = new DocumentSpan(lineMatch.Index, lineIdx, 0, line.Length);

				yield return new ContextValue<string>(location, line);
			}
		}

	}

}
