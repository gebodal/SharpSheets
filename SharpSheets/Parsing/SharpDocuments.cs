using System;

namespace SharpSheets.Parsing {

	public static class SharpDocuments {

		public static readonly StringComparer StringComparer = StringComparer.InvariantCultureIgnoreCase;
		public static readonly StringComparison StringComparison = StringComparison.InvariantCultureIgnoreCase;

		public static bool StringEquals(string a, string b) {
			return StringComparer.Equals(a, b);
		}

	}

}
