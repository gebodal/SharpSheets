using System.Text;

namespace SharpSheets.Generators.Utilities {

	public class IndentedStringBuilder {

		private readonly StringBuilder sb;
		private readonly int level;
		private bool newline = true;

		public IndentedStringBuilder(StringBuilder builder, int level) {
			sb = builder;
			this.level = level;
		}

		public IndentedStringBuilder Append(string str) {
			for(int i=0; i<str.Length; i++) {
				Append(str[i]);
			}
			return this;
		}

		public IndentedStringBuilder Append(char c) {
			if(c == '\n') {
				sb.Append(c);
				newline = true;
			}
			else if (newline) {
				sb.Append('\t', level);
				sb.Append(c);
				newline = false;
			}
			else {
				sb.Append(c);
			}

			return this;
		}

	}

}
