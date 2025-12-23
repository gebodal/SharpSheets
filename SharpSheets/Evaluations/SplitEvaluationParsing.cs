using SharpSheets.Exceptions;
using SharpSheets.Parsing;
using System.Text.RegularExpressions;

namespace SharpSheets.Evaluations {

	public static partial class SplitEvaluationParsing {

		[GeneratedRegex(@"[a-z]([a-z0-9 ]*[a-z0-9])?", RegexOptions.IgnoreCase)]
		private static partial Regex NameRegex(); // Names can have whitespace

		private enum SplitState { NAME, VALUE_WAITING, VALUE, ROUND, SQUARE, CURLY, ESCAPE }
		public static IList<ContextProperty<string>>? SplitDictionaryValues(ContextValue<string>? detailsValue, List<SharpParsingException> errors) {

			List<ContextProperty<string>> values = new List<ContextProperty<string>>();

			if (!detailsValue.HasValue || string.IsNullOrWhiteSpace(detailsValue.Value.Value)) {
				return values;
			}

			ContextValue<string> details = detailsValue.Value;

			Stack<SplitState> stateStack = new Stack<SplitState>(5);
			//stateStack.Push(SplitState.NAME);

			int start = 0;
			ContextValue<string> name = default;
			int lastNonWhitespace = -1;

			for (int i = 0; i <= details.Value.Length; i++) {
				char c = i == details.Value.Length ? ',' : details.Value[i]; // Last comma to finalise any entities

				if (stateStack.Count == 0) {
					if (!char.IsWhiteSpace(c)) {
						stateStack.Push(SplitState.NAME);
						start = i;
						lastNonWhitespace = i;
					}
					continue;
				}

				SplitState state = stateStack.Peek();

				if (state == SplitState.ESCAPE) {
					stateStack.Pop();
				}
				else if (state == SplitState.NAME) {
					if (c == ':') {
						stateStack.Pop();
						stateStack.Push(SplitState.VALUE_WAITING);

						int end = lastNonWhitespace + 1;
						string nameText = details.Value[start..end];
						DocumentSpan nameLocation = new DocumentSpan(details.Location.Offset + start, details.Location.Line, details.Location.Column + start, end - start);
						name = new ContextValue<string>(nameLocation, nameText);
					}
					else if (c == ',') {
						// TODO Need to account for empty names, e.g. "[name1: value, , name2: value]"
						stateStack.Pop();
						int end = lastNonWhitespace + 1;
						string flagText = details.Value[start..end];
						string value;
						if (flagText.StartsWith('!')) {
							flagText = flagText[1..];
							value = "false";
						}
						else {
							value = "true";
						}
						DocumentSpan flagLocation = new DocumentSpan(details.Location.Offset + start, details.Location.Line, details.Location.Column + start, end - start);
						if (NameRegex().IsMatch(flagText)) {
							values.Add(new ContextProperty<string>(flagLocation, flagText, flagLocation, value));
						}
						else {
							errors.Add(new SharpParsingException(flagLocation, "Invalid variable name."));
						}
					}
				}
				else if (state == SplitState.VALUE_WAITING) {
					if (!char.IsWhiteSpace(c)) {
						stateStack.Pop();
						stateStack.Push(SplitState.VALUE);
						start = i;
					}
				}
				else if (state == SplitState.VALUE && c == ',') {
					stateStack.Pop();

					// TODO Need to account for empty values, e.g. "[name: , name2: value]"
					int end = lastNonWhitespace + 1;
					string valueText = details.Value[start..end];
					DocumentSpan valueLocation = new DocumentSpan(details.Location.Offset + start, details.Location.Line, details.Location.Column + start, end - start);
					if (NameRegex().IsMatch(name.Value)) {
						values.Add(new ContextProperty<string>(name.Location, name.Value, valueLocation, valueText));
					}
					else {
						errors.Add(new SharpParsingException(name.Location, "Invalid variable name."));
					}
				}
				else if (state == SplitState.ROUND && c == ')') {
					stateStack.Pop();
				}
				else if (state == SplitState.SQUARE && c == ']') {
					stateStack.Pop();
				}
				else if (state == SplitState.CURLY && c == '}') {
					stateStack.Pop();
				}
				else if (c == '\\') {
					stateStack.Push(SplitState.ESCAPE);
				}
				else if (c == '(') {
					stateStack.Push(SplitState.ROUND);
				}
				else if (c == '[') {
					stateStack.Push(SplitState.SQUARE);
				}
				else if (c == '{') {
					stateStack.Push(SplitState.CURLY);
				}

				if (!char.IsWhiteSpace(c)) {
					lastNonWhitespace = i;
				}
			}

			if (stateStack.Count > 0) {
				errors.Add(new SharpParsingException(details.Location, "Could not split values."));
				return null;
			}
			else {
				return values;
			}
		}

		public static IList<ContextValue<string>>? SplitListValues(ContextValue<string> details, List<SharpParsingException> errors, char splitChar) {

			List<ContextValue<string>> values = new List<ContextValue<string>>();

			if (string.IsNullOrWhiteSpace(details.Value)) {
				return values;
			}

			Stack<SplitState> stateStack = new Stack<SplitState>(5);

			int start = 0;
			int lastNonWhitespace = -1;

			for (int i = 0; i <= details.Value.Length; i++) {
				char c = i == details.Value.Length ? splitChar : details.Value[i]; // Last comma to finalise any entities

				if (stateStack.Count == 0) {
					if (!char.IsWhiteSpace(c) && c != splitChar) {
						stateStack.Push(SplitState.VALUE);
						start = i;
						lastNonWhitespace = i;
					}
					else if (c == splitChar) {
						errors.Add(new SharpParsingException(new DocumentSpan(details.Location.Offset + i, details.Location.Line, details.Location.Column + i, 1), "Empty value."));
					}
					continue;
				}

				SplitState state = stateStack.Peek();

				if (state == SplitState.ESCAPE) {
					stateStack.Pop();
				}
				else if (state == SplitState.VALUE && c == splitChar) {
					stateStack.Pop();

					int end = lastNonWhitespace + 1;
					if (end > start) {
						string valueText = details.Value[start..end];
						DocumentSpan valueLocation = new DocumentSpan(details.Location.Offset + start, details.Location.Line, details.Location.Column + start, end - start);
						values.Add(new ContextValue<string>(valueLocation, valueText));
					}
					else {
						DocumentSpan errorLocation = new DocumentSpan(details.Location.Offset + start, details.Location.Line, details.Location.Column + start, Math.Max(end - start, 1));
						errors.Add(new SharpParsingException(errorLocation, "Empty value."));
					}
				}
				else if (state == SplitState.ROUND && c == ')') {
					stateStack.Pop();
				}
				else if (state == SplitState.SQUARE && c == ']') {
					stateStack.Pop();
				}
				else if (state == SplitState.CURLY && c == '}') {
					stateStack.Pop();
				}
				else if (c == '\\') {
					stateStack.Push(SplitState.ESCAPE);
				}
				else if (c == '(') {
					stateStack.Push(SplitState.ROUND);
				}
				else if (c == '[') {
					stateStack.Push(SplitState.SQUARE);
				}
				else if (c == '{') {
					stateStack.Push(SplitState.CURLY);
				}

				if (!char.IsWhiteSpace(c)) {
					lastNonWhitespace = i;
				}
			}

			if (stateStack.Count > 0) {
				errors.Add(new SharpParsingException(details.Location, "Could not split values."));
				return null;
			}
			else {
				return values;
			}
		}

	}

}
