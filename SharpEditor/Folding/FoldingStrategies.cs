using System.Collections.Generic;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace SharpEditor.Folding {

	public interface IFoldingStrategy {
		void UpdateFoldings(FoldingManager manager, TextDocument document);
	}

	/// <summary>
	/// Allows producing foldings from a document based on indentation in a sharpsheet config.
	/// </summary>
	public class SharpSheetsFoldingStrategy : IFoldingStrategy {

		private readonly Regex commentRegex = new Regex("(?<!\\\\)#.*$");

		private readonly int minFoldLength;

		public SharpSheetsFoldingStrategy(int minFoldLength) {
			this.minFoldLength = minFoldLength;
		}

		public void UpdateFoldings(FoldingManager manager, TextDocument document) {
			IEnumerable<NewFolding> newFoldings = CreateNewFoldings(document, out int firstErrorOffset);
			manager.UpdateFoldings(newFoldings, firstErrorOffset);
		}

		/// <summary>
		/// Create <see cref="NewFolding"/>s for the specified document.
		/// </summary>
		public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset) {

			firstErrorOffset = -1;

			List<NewFolding> newFoldings = new List<NewFolding>();

			Stack<int> startLineNumbers = new Stack<int>();
			Stack<int> startOffsets = new Stack<int>();
			Stack<int> startIndents = new Stack<int>();

			int lastIndent = 0;
			int lastLineWithText = 0;

			IList<DocumentLine> lines = document.Lines;
			//Dictionary<int, string?> lineTypes = new Dictionary<int, string?>();
			string? latestLineType = null;
			bool containsPage = false;
			for (int i = 0; i < lines.Count; i++) {
				//lineTypes[i] = latestLineType;
				if (lines[i].Length > 0) { // We only look at the line if it has contents
					string fullLineText = document.GetText(lines[i].Offset, lines[i].Length);
					string lineText = commentRegex.Replace(fullLineText, "").TrimEnd();
					bool lineHasText = lineText.Length > 0;
					bool lineIsStart = lineText.Length > 0 && lineText[lineText.Length - 1] == ':'; // Is the line the start of a div
					int lineWhitespace = TextUtilities.GetWhitespaceAfter(document, lines[i].Offset).Length;

					while (lineHasText && startIndents.Count > 0 && lineWhitespace <= startIndents.Peek()) {
						int startLineNumber = startLineNumbers.Pop();
						int endLineNumber = lastLineWithText;
						int startOffset = startOffsets.Pop();
						int endOffset = lines[endLineNumber].EndOffset; // Can't be first line
						lastIndent = startIndents.Pop();

						if ((endLineNumber - startLineNumber) >= minFoldLength) { // Only include if it is sufficiently large
							newFoldings.Add(new NewFolding(startOffset, endOffset) { Name = null });
						}
					}

					if (lineIsStart) {
						// If the previous division is empty, just replace it
						if (startIndents.Count > 0 && lineWhitespace == lastIndent && lastLineWithText == startLineNumbers.Peek()) {
							startLineNumbers.Pop();
							startOffsets.Pop();
							startIndents.Pop();
						}

						startLineNumbers.Push(i);
						startOffsets.Push(lines[i].EndOffset);
						startIndents.Push(lineWhitespace);
						lastIndent = lineWhitespace;

						// TODO This doesn't account for Pop from rect stack
						latestLineType = lineText.Substring(0, lineText.Length - 1).TrimStart().ToLowerInvariant();
						containsPage = containsPage || latestLineType == "page";
						//lineTypes[i] = "rect";
					}

					if (fullLineText.Trim().Length > 0) lastLineWithText = i;
				}
			}

			int documentEndOffset = lines[lastLineWithText].EndOffset;
			while (startIndents.Count > 0) {
				int startOffset = startOffsets.Pop();
				lastIndent = startIndents.Pop();

				newFoldings.Add(new NewFolding(startOffset, documentEndOffset) { Name = null });
			}

			newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
			return newFoldings;
		}
	}

	/// <summary>
	/// Allows producing foldings from a document based on indentation in a card subject config.
	/// </summary>
	public class SharpSheetsCardSubjectFoldingStrategy : IFoldingStrategy {
		public SharpSheetsCardSubjectFoldingStrategy() { }

		public void UpdateFoldings(FoldingManager manager, TextDocument document) {
			IEnumerable<NewFolding> newFoldings = CreateNewFoldings(document, out int firstErrorOffset);
			manager.UpdateFoldings(newFoldings, firstErrorOffset);
		}

		private readonly Regex levelRegex = new Regex(@"^\#+");
		private readonly Regex macroRegex = new Regex(@"^(?:\=+|\#\=|\#\!)");

		/// <summary>
		/// Create <see cref="NewFolding"/>s for the specified document.
		/// </summary>
		public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset) {
			firstErrorOffset = -1;
			List<NewFolding> newFoldings = new List<NewFolding>();


			Stack<int> startLineNumbers = new Stack<int>();
			Stack<int> startOffsets = new Stack<int>();
			Stack<int> startLevels = new Stack<int>();

			int lastLineWithText = 0;

			IList<DocumentLine> lines = document.Lines;
			for (int i = 0; i < lines.Count; i++) {
				if (lines[i].Length > 0) { // We only look at the line if it has contents
					string lineText = document.GetText(lines[i].Offset, lines[i].Length).TrimStart();
					string trimmedText = lineText.Trim();

					bool isMacro = macroRegex.IsMatch(trimmedText);
					/*
					if (macroRegex.IsMatch(lineText.Trim())) {
						continue;
					}
					*/

					if (lineText.Length > 0 && lineText[0] == '#') {

						int lineLevel = isMacro ? -1 : levelRegex.Match(lineText).Length;

						while (startLevels.Count > 0 && startLevels.Peek() >= lineLevel) {
							int startLineNumber = startLineNumbers.Pop();
							int endLineNumber = lastLineWithText;
							int startOffset = startOffsets.Pop();
							int endOffset = lines[endLineNumber].EndOffset;
							int startLevel = startLevels.Pop();

							if (startLevel >= 0) {
								newFoldings.Add(new NewFolding(startOffset, endOffset) { Name = null });
							}
						}

						// If the previous division is empty, just replace it
						if (startLevels.Count > 0 && lineLevel == startLevels.Peek() && lastLineWithText == startLineNumbers.Peek()) {
							startLineNumbers.Pop();
							startOffsets.Pop();
							startLevels.Pop();
						}

						startLineNumbers.Push(i);
						startOffsets.Push(lines[i].EndOffset);
						startLevels.Push(lineLevel);
					}
					
					if (trimmedText.Length > 0 && !isMacro) {
						lastLineWithText = i;
					}
				}
			}

			int documentEndOffset = lines[lastLineWithText].EndOffset;
			while (startLevels.Count > 0) {
				int startOffset = startOffsets.Pop();
				int startLevel = startLevels.Pop();

				if (startLevel >= 0) {
					newFoldings.Add(new NewFolding(startOffset, documentEndOffset) { Name = null });
				}
			}

			newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));

			return newFoldings;
		}
	}
}
