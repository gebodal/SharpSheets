using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using SharpSheets.Documentation;
using SharpSheets.Shapes;

namespace SharpEditor.CodeHelpers {

	public interface ICodeHelper {

		IList<ICompletionData> GetCompletionData();
		int GetCompletionStartOffset();
		bool TextEnteredTriggerCompletion(TextInputEventArgs e);
		//bool TextEnteringTriggerCompletion(TextCompositionEventArgs e);

		void TextEntered(TextInputEventArgs e);

		IList<Control> GetToolTipContent(int offset, string word);
		IList<Control> GetFallbackToolTipContent(int offset);
		IList<Control> GetContextMenuItems(int offset, string? word);

		bool SupportsIncrementComment { get; }
		void IncrementComment(int offset, int length);
		void DecrementComment(int offset, int length);

		void TextPasted(EventArgs args, int offset);

	}

	public static class CodeHelpers {

		public static Type? DefaultType(Type type) {
			// TODO Others?
			return ShapeFactory.GetDefaultStyle(type);
		}

		public static Type? DefaultType(ArgumentType type) {
			return DefaultType(type.DisplayType);
		}

	}

	public static class CodeHelperUtils {

		public static int GetOffset(this TextDocument document, TextViewPosition viewPosition) {
			return document.GetOffset(viewPosition.Line, viewPosition.Column);
		}

		public static ISegment? GetSharpTermSegmentFromOffset(this TextDocument document, int offset) {

			if (document.TextLength == 0) { return null; }

			if ((offset + 1) >= document.TextLength) { return null; }
			string currentChar = document.GetText(offset, 1);
			if (string.IsNullOrWhiteSpace(currentChar)) { return null; }

			if (offset >= document.TextLength) { offset--; }

			//int offsetStart = TextUtilities.GetNextCaretPosition(document, offset, LogicalDirection.Backward, CaretPositioningMode.WordBorder);
			//int offsetEnd = TextUtilities.GetNextCaretPosition(document, offset, LogicalDirection.Forward, CaretPositioningMode.WordBorder);

			int offsetStart = document.GetNextSharpCaretPosition(offset, LogicalDirection.Backward);
			int offsetEnd = document.GetNextSharpCaretPosition(offset, LogicalDirection.Forward);

			if (offsetEnd == -1 || offsetStart == -1) { return null; }

			TextSegment segment = new TextSegment {
				StartOffset = offsetStart,
				EndOffset = offsetEnd
			};

			return segment; // textEditor.Document.GetText(offsetStart, offsetEnd - offsetStart);
		}

		public static ISegment? GetSharpTermSegmentFromViewPosition(this TextDocument document, TextViewPosition viewPosition) {

			if (document.TextLength == 0) { return null; }

			int line = viewPosition.Line;
			int column = viewPosition.Column;
			int offset = document.GetOffset(line, column);

			return GetSharpTermSegmentFromOffset(document, offset);
		}

		public static int GetNextSharpCaretPosition(this ITextSource textSource, int offset, LogicalDirection direction) {
			if (textSource == null)
				throw new ArgumentNullException(nameof(textSource));
			if (direction != LogicalDirection.Backward && direction != LogicalDirection.Forward) {
				throw new ArgumentException("Invalid LogicalDirection: " + direction, nameof(direction));
			}

			int textLength = textSource.TextLength;

			if (textLength <= 0) {
				return -1;
			}

			while (true) {
				int nextPos = (direction == LogicalDirection.Backward) ? offset - 1 : offset + 1;

				// Handle offset values outside the valid range
				if (nextPos < 0 || nextPos > textLength) {
					return -1;
				}

				// Check if we've run against the textSource borders
				if (nextPos == 0 || nextPos == textLength) {
					return nextPos;
				}

				char nextChar = textSource.GetCharAt(nextPos);

				// Check if we've run against the word borders
				if (char.IsWhiteSpace(nextChar) || !(char.IsLetterOrDigit(nextChar) || nextChar == '.')) {
					return nextPos;
				}

				// Continue search
				offset = nextPos;
			}
		}

		public static string? GetSharpTermStringFromViewPosition(this TextDocument document, TextViewPosition viewPosition) {
			ISegment? segment = GetSharpTermSegmentFromViewPosition(document, viewPosition);
			if (segment != null && segment.Length > 0) {
				return document.GetText(segment.Offset, segment.Length)?.Trim();
			}
			else {
				return null;
			}
		}

		public static string? GetSharpTermStringFromOffset(this TextDocument document, int offset) {
			ISegment? segment = GetSharpTermSegmentFromOffset(document, offset);
			if (segment != null && segment.Length > 0) {
				return document.GetText(segment.Offset, segment.Length)?.Trim();
			}
			else {
				return null;
			}
		}

	}
}
