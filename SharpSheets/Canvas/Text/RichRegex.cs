using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SharpSheets.Canvas.Text {

	public class RichRegex {

		private readonly Regex regex;

		public RichRegex(Regex regex) {
			this.regex = regex;
		}

		public Match Match(RichString input, int startAt, int length) {
			return regex.Match(input.Text, startAt, length);
		}

		public Match Match(RichString input, int startAt) {
			return regex.Match(input.Text, startAt);
		}

		public Match Match(RichString input) {
			return regex.Match(input.Text);
		}

		public RichString[] Split(RichString input, int count, int startAt) {
			
			if (count == 1) {
				return new RichString[] { input.Clone() };
			}

			List<RichString> parts;
			if (count > 0) {
				parts = new List<RichString>(count);
			}
			else {
				parts = new List<RichString>(); // Better initialisation value?
			}

			int found = 0;
			int head = startAt;

			MatchCollection matches = regex.Matches(input.Text, startAt);
			for(int i=0; i<matches.Count; i++) {
				Match match = matches[i];
				parts.Add(input.Substring(head, match.Index - head));
				head = match.Index + match.Length;
				found++;
				if(found == count) {
					break;
				}
			}

			parts.Add(input.Substring(head));

			return parts.ToArray();
		}

		public RichString[] Split(RichString input, int count) {
			return Split(input, count, 0);
		}

		public RichString[] Split(RichString input) {
			return Split(input, -1, 0);
		}

		public RichString Replace(RichString input, RichString replacement, int count, int startAt) {

			if(count == 0) {
				return input.Clone();
			}

			List<char> chars = new List<char>();
			List<TextFormat> formats = new List<TextFormat>();

			for(int i=0; i<startAt; i++) {
				chars.Add(input.chars[i]);
				formats.Add(input.formats[i]);
			}

			int found = 0;
			int head = startAt;

			MatchCollection matches = regex.Matches(input.Text, startAt);
			for (int m = 0; m < matches.Count; m++) {
				Match match = matches[m];

				for(int i=head; i<match.Index; i++) {
					chars.Add(input.chars[i]);
					formats.Add(input.formats[i]);
				}

				for(int j=0; j<replacement.Length; j++) {
					chars.Add(replacement.chars[j]);
					formats.Add(replacement.formats[j]);
				}

				head = match.Index + match.Length;
				found++;
				if (found == count) {
					break;
				}
			}

			for (int i = head; i < input.Length; i++) {
				chars.Add(input.chars[i]);
				formats.Add(input.formats[i]);
			}

			return new RichString(chars.ToArray(), formats.ToArray());
		}

		public RichString Replace(RichString input, RichString replacement, int count) {
			return Replace(input, replacement, count, 0);
		}

		public RichString Replace(RichString input, RichString replacement) {
			return Replace(input, replacement, -1, 0);
		}

	}

}
