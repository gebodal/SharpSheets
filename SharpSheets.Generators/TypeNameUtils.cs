using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpSheets.Generators {

	public static class TypeNameUtils {

		public static string ReduceParameterTypeName(string typeStr) {
			if (typeStr.EndsWith("?")) {
				typeStr = typeStr.Substring(0, typeStr.Length - 1);
			}
			else if (typeStr.StartsWith("Nullable<") && typeStr.EndsWith(">")) {
				typeStr = typeStr.Substring(9, typeStr.Length - 10);
			}

			static IEnumerable<string> SplitTypeParts(string text, char delim) {
				int roundCount = 0, angleCount = 0, squareCount = 0;

				int head = 0;
				for (int i = 0; i < text.Length; i++) {
					char c = text[i];
					if (c == '(') { roundCount++; }
					else if (c == ')') { roundCount--; }
					else if (c == '<') { angleCount++; }
					else if (c == '>') { angleCount--; }
					else if (c == '[') { squareCount++; }
					else if (c == ']') { squareCount--; }
					else if (c == delim && roundCount == 0 && angleCount == 0 && squareCount == 0) {
						yield return text.Substring(head, i - head).Trim();
						head = i + 1;
					}
				}

				yield return text.Substring(head, text.Length - head).Trim();
			}

			static bool TryGetTupleParts(string text, out string[] tupleParts) {
				if (text.StartsWith("(") && text.EndsWith(")")) {
					tupleParts = SplitTypeParts(text.Substring(1, text.Length - 2), ',').ToArray();
					return true;
				}
				else if (text.StartsWith("ValueTuple<") && text.EndsWith(">")) {
					tupleParts = SplitTypeParts(text.Substring(11, text.Length - 12), ',').ToArray();
					return true;
				}
				else {
					tupleParts = null!;
					return false;
				}
			}

			static string SimplifyTuplePart(string text) {
				return ReduceParameterTypeName(SplitTypeParts(text, ' ').First().Trim());
			}

			if (TryGetTupleParts(typeStr, out string[] tupleParts)) {
				return $"({string.Join(", ", tupleParts.Select(SimplifyTuplePart))})";
			}

			return typeStr;
		}

	}

}
