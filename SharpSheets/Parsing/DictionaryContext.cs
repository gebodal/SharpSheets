using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharpSheets.Parsing {

	public static class DictionaryContext {

		public static readonly string EmptyContextString = "{}";

		private static readonly Regex dictRegex = new Regex(@"(?<!\\)\{(?<dict>.+)(?<!\\)\}");
		private static readonly Regex commaSplitRegex = new Regex(@"(?<!\\)\,");
		private static readonly Regex argRegex = new Regex(@"
				^(
					(?<property>[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)*) \s* \: \s* (?<value>.+)
					|
					(?<flag>\!?[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)*)
				)$
			", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static IContext CreateContext(string contextName, string value) {
			Match dictMatch = dictRegex.Match(value);
			if (dictMatch.Success) {
				value = dictMatch.Groups["dict"].Value;
			}
			else {
				throw new FormatException("Badly formatted dictionary string.");
			}

			Dictionary<string, string> properties = new Dictionary<string, string>(SharpDocuments.StringComparer);
			Dictionary<string, bool> flags = new Dictionary<string, bool>(SharpDocuments.StringComparer);
			string[] argStrings = commaSplitRegex.Split(value);
			foreach (string argStr in argStrings) {
				Match match = argRegex.Match(argStr.Trim());

				if (match.Groups["flag"].Success) {
					string flag = match.Groups["flag"].Value;
					bool flagVal = true;
					if (flag.StartsWith("!")) {
						flagVal = false;
						flag = flag.Substring(1);
					}
					if (flags.ContainsKey(flag)) {
						throw new FormatException("Repeated argument in dictionary.");
					}
					flags.Add(flag, flagVal);
				}
				else if (match.Groups["property"].Success) {
					string property = match.Groups["property"].Value;
					string argVal = match.Groups["value"].Value.Replace("\\,", ",");
					if (properties.ContainsKey(property)) {
						throw new FormatException("Repeated argument in dictionary.");
					}
					properties.Add(property, argVal);
				}
				else {
					throw new FormatException("Badly formatted dictionary argument.");
				}
			}

			return Context.Simple(contextName, properties, flags);
		}

		public static bool TryGetContext(string contextName, string value, [MaybeNullWhen(false)] out IContext context, [MaybeNullWhen(true)] out Exception ex) {
			try {
				context = CreateContext(contextName, value);
				ex = null;
				return true;
			}
			catch(FormatException e) {
				context = null;
				ex = e;
				return false;
			}
		}

	}

}
