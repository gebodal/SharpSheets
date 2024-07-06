using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using SharpSheets.Markup.Parsing;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpEditor.Folding {
	/// <summary>
	/// Holds information about the start of a fold in an xml string.
	/// </summary>
	sealed class SharpXMLFoldStart : NewFolding {
		internal int StartLine;
	}

	/// <summary>
	/// Determines folds for an xml string in the editor.
	/// </summary>
	public class SharpXMLFoldingStrategy : IFoldingStrategy {
		/// <summary>
		/// Flag indicating whether attributes should be displayed on folded
		/// elements.
		/// </summary>
		public bool ShowAttributesWhenFolded { get; set; }

		/// <summary>
		/// Create <see cref="NewFolding"/>s for the specified document and updates the folding manager with them.
		/// </summary>
		public void UpdateFoldings(FoldingManager manager, TextDocument document) {
			IEnumerable<NewFolding> foldings = CreateNewFoldings(document, out int firstErrorOffset);
			manager.UpdateFoldings(foldings, firstErrorOffset);
		}

		/// <summary>
		/// Create <see cref="NewFolding"/>s for the specified document.
		/// </summary>
		public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset) {
			try {
				XMLElement? root = XMLParsing.Parse(document.Text, out List<SharpSheets.Exceptions.SharpParsingException> errors, true);

				if(root != null) {
					return CreateNewFoldings(document, root, out firstErrorOffset);
				}
			}
			catch (Exception) { }

			firstErrorOffset = 0;
			return Enumerable.Empty<NewFolding>();
		}

		/// <summary>
		/// Create <see cref="NewFolding"/>s for the specified document.
		/// </summary>
		public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, XMLElement root, out int firstErrorOffset) {
			Stack<SharpXMLFoldStart> stack = new Stack<SharpXMLFoldStart>();
			List<NewFolding> foldMarkers = new List<NewFolding>();
			
			foreach (XMLNode node in root.TraverseNodes()) {
				switch (node) {
					case XMLElement element:
						if (element.EndTag != null) {
							SharpXMLFoldStart newFoldStart = CreateElementFoldStart(document, element);
							stack.Push(newFoldStart);
						}
						else {
							CreateEmptyElementFold(document, foldMarkers, element);
						}
						break;

					case XMLElementEnd elementEnd:
						SharpXMLFoldStart foldStart = stack.Pop();
						CreateElementFold(document, foldMarkers, elementEnd, foldStart);
						break;

					case XMLComment comment:
						CreateCommentFold(document, foldMarkers, comment);
						break;
				}
			}
			firstErrorOffset = -1;

			foldMarkers.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
			return foldMarkers;
		}

		static bool SpansMultipleLines(TextDocument document, XMLNode node) {
			int lineStart = document.GetLineByOffset(node.Location.Offset).LineNumber;
			int lineEnd = document.GetLineByOffset(node.Location.Offset + node.Location.Length).LineNumber;
			return lineStart != lineEnd;
		}

		/// <summary>
		/// Creates a comment fold if the comment spans more than one line.
		/// </summary>
		/// <remarks>The text displayed when the comment is folded is the first
		/// line of the comment.</remarks>
		static void CreateCommentFold(TextDocument document, List<NewFolding> foldMarkers, XMLComment comment) {
			if (SpansMultipleLines(document, comment)) {

				int startOffset = comment.Location.Offset;
				int endOffset = startOffset + comment.Location.Length;

				string commentText = GetSubstring(comment.Text, 28);

				string foldText = "<!-- " + commentText + " -->";
				foldMarkers.Add(new NewFolding(startOffset, endOffset) { Name = foldText });
			}
		}

		/// <summary>
		/// Creates an XmlFoldStart for the start tag of an element.
		/// </summary>
		SharpXMLFoldStart CreateElementFoldStart(TextDocument document, XMLElement element) {
			SharpXMLFoldStart newFoldStart = new SharpXMLFoldStart();

			newFoldStart.StartOffset = element.Location.Offset;
			newFoldStart.StartLine = document.GetLineByOffset(newFoldStart.StartOffset).LineNumber;
			newFoldStart.Name = GetElementFoldText(element, false);

			return newFoldStart;
		}

		/// <summary>
		/// Create an element fold if the start and end tag are on different lines.
		/// </summary>
		static void CreateElementFold(TextDocument document, List<NewFolding> foldMarkers, XMLElementEnd elementEnd, SharpXMLFoldStart foldStart) {
			int endOffset = elementEnd.Location.Offset + elementEnd.Location.Length;
			int endLine = document.GetLineByOffset(endOffset).LineNumber;
			if (endLine > foldStart.StartLine) {
				foldStart.EndOffset = endOffset;
				foldMarkers.Add(foldStart);
			}
		}

		/// <summary>
		/// Create an element fold from an empty element, if the start and end are on different lines.
		/// </summary>
		void CreateEmptyElementFold(TextDocument document, List<NewFolding> foldMarkers, XMLElement element) {
			int startOffset = element.Location.Offset;
			int endOffset = element.Location.Offset + element.Location.Length;
			int startLine = document.GetLineByOffset(startOffset).LineNumber;
			if (startLine != document.GetLineByOffset(endOffset).LineNumber) {
				foldMarkers.Add(new NewFolding(startOffset, endOffset) { Name = GetElementFoldText(element, true) });
			}
		}

		string GetElementFoldText(XMLElement element, bool isEmpty) {
			if (ShowAttributesWhenFolded && element.AttributeCount > 0) {
				string attributeStr = GetAttributeFoldText(element);
				return String.Concat("<", element.Name, " ", GetAttributeFoldText(element), (isEmpty ? "/>" : ">"));
			}
			else {
				return String.Concat("<", element.Name, (isEmpty ? "/>" : ">"));
			}
		}

		/// <summary>
		/// Gets the element's attributes as a string on one line that will
		/// be displayed when the element is folded.
		/// </summary>
		/// <remarks>
		/// Currently this puts all attributes from an element on the same
		/// line of the start tag.  It does not cater for elements where attributes
		/// are not on the same line as the start tag.
		/// </remarks>
		static string GetAttributeFoldText(XMLElement element) {
			ContextProperty<string>? nameAttr = null;
			ContextProperty<string>? idAttr = null;
			ContextProperty<string>? typeAttr = null;
			ContextProperty<string>? firstAttr = null;

			foreach(ContextProperty<string> attr in element.Attributes) {
				if(attr.Name == "name") {
					nameAttr = attr;
				}
				else if(attr.Name == "id") {
					idAttr = attr;
				}
				else if (attr.Name == "type") {
					typeAttr = attr;
				}
				else if (!firstAttr.HasValue) {
					firstAttr = attr;
				}
			}

			return string.Join(" ", EnumerableUtils.Yield(idAttr, nameAttr, typeAttr, firstAttr).WhereNotNull().Select(GetAttributeText));
		}

		static string GetAttributeText(ContextProperty<string> attribute) {
			char quoteChar = '"'; // Better way?
			string attrText = GetSubstring(attribute.Value, 28);
			return attribute.Name + "=" + quoteChar + attrText + quoteChar;
		}

		static string GetSubstring(string text, int maxLength) {
			int textEnd = Math.Min(text.Length, maxLength - 3);

			if(textEnd <= 0) {
				return "";
			}

			if (text.IndexOf('\n') is int newlineIndex && newlineIndex >= 0 && newlineIndex < textEnd) {
				textEnd = newlineIndex;
			}

			return text.Substring(0, textEnd).Trim() + (text.Length > textEnd ? "..." : "");
		}
	}
}
