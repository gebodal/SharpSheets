using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;
using System;
using System.Text.RegularExpressions;

namespace SharpEditorAvalonia.Indentation {
	/// <summary>
	/// Handles indentation by copying the indentation from the previous line,
	/// and adding one more whitespace character if previous line was division.
	/// Does not support indenting multiple lines.
	/// </summary>
	public class SharpSheetsIndentationStrategy : IIndentationStrategy {

		private string indentationString = "\t";

		/// <summary>
		/// Gets/Sets the indentation string.
		/// </summary>
		public string IndentationString {
			get { return indentationString; }
			set {
				if (string.IsNullOrEmpty(value))
					throw new ArgumentException("Indentation string must not be null or empty");
				indentationString = value;
			}
		}

		/// <inheritdoc/>
		public virtual void IndentLine(TextDocument document, DocumentLine line) {
			if (document == null) { throw new ArgumentNullException(nameof(document)); }
			if (line == null) { throw new ArgumentNullException(nameof(line)); }
			DocumentLine previousLine = line.PreviousLine;
			if (previousLine != null) {
				ISegment indentationSegment = TextUtilities.GetWhitespaceAfter(document, previousLine.Offset);
				string indentation = document.GetText(indentationSegment);

				string previousLineText = Regex.Replace(document.GetText(previousLine.Offset, previousLine.Length), "(?<!\\\\)#.*$", "").Trim();
				if (previousLineText.Length > 0 && previousLineText[previousLineText.Length - 1] == ':' && previousLineText[0] != '-') {
					indentation += indentation.Length > 0 ? indentation[indentation.Length - 1].ToString() : indentationString;
				}

				// copy indentation to line
				indentationSegment = TextUtilities.GetWhitespaceAfter(document, line.Offset);
				document.Replace(indentationSegment.Offset, indentationSegment.Length, indentation,
								 OffsetChangeMappingType.RemoveAndInsert);
				// OffsetChangeMappingType.RemoveAndInsert guarantees the caret moves behind the new indentation.
			}
		}

		/// <summary>
		/// Does nothing: indenting multiple lines is useless without a smart indentation strategy.
		/// </summary>
		public virtual void IndentLines(TextDocument document, int beginLine, int endLine) { } // TODO What is this supposed to do?
	}
}
