using SharpSheets.Cards.Definitions;
using SharpSheets.Exceptions;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpSheets.Parsing {

	public readonly struct SharpDocumentLine {
		
		/// <summary>
		/// Location of the <see cref="Content"/> string in the document.
		/// </summary>
		public DocumentSpan Location { get; }
		/// <summary>
		/// Indent level of this line in the hierarchy.
		/// </summary>
		public int IndentLevel { get; }
		/// <summary>
		/// Primary string content of this line, excluding indent and any boilerplate punctuation.
		/// </summary>
		public string Content { get; }
		/// <summary>
		/// String property value for this line, if one if given. Only present if LineType == LineType.Property.
		/// </summary>
		public string? Property { get; }
		/// <summary>
		/// Location of the <see cref="Property"/> string in the document.
		/// </summary>
		public DocumentSpan PropertyLocation { get; }
		/// <summary>
		/// Enum denoting the presumed function of this line.
		/// </summary>
		public LineType LineType { get; }
		/// <summary>
		/// Indicates that this line's content is marked as local only.
		/// </summary>
		public bool LocalOnly { get; }

		public SharpDocumentLine(DocumentSpan location, int indentLevel, string content, string? property, DocumentSpan propertyLocation, LineType lineType, bool localOnly) {
			Location = location;
			IndentLevel = indentLevel;
			Content = content;
			Property = property;
			PropertyLocation = propertyLocation;
			LineType = lineType;
			LocalOnly = localOnly;
		}

	}

	public enum LineType { DIV, NAMEDCHILD, PROPERTY, FLAG, ENTRY, DEFINITION, ERROR }

	public static class SharpDocumentLineParsing {

		private static readonly Regex regex = new Regex(@"^(?:(?<definition>(?:def|fun)\s+.+)|(?<property>\@?[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)*\s*:.+)|(?<namedchild>\@?\&[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)*)\:?|(?<div>[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)*):|-\s*(?<entry>.+)|(?<flag>\@?\!?[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)*))$", RegexOptions.IgnoreCase);
		private static readonly string[] types = new string[] { "div", "namedchild", "definition", "property", "entry", "flag" };

		private static readonly bool removeComments = true;

		private static int GetIndent(string text) {
			int indent = 0;
			for (int i = 0; i < text.Length; i++) {
				if (char.IsWhiteSpace(text[i])) {
					indent++;
				}
				else {
					break;
				}
			}
			return indent;
		}

		/*
		private static string DeEscapeNewlines(string text) {
			// TODO Needs improving
			return text?.Replace("\\n", "\n"); // TODO Should this account for other numbers of "\"s?
		}
		*/
		[return: NotNullIfNotNull(nameof(text))]
		private static string? DeEscapeHash(string? text) {
			// TODO Needs improving
			return text != null ? Regex.Replace(text, @"\\#", "#") : null; // TODO Should this account for other numbers of "\"s?
		}

		private static readonly Regex propertyRegex = new Regex(@"^(?<name>\@?[a-z][a-z0-9]+(\.[a-z][a-z0-9]+)*)\s*:\s*(?<value>.+)$", RegexOptions.IgnoreCase);
		private static void SplitProperty(string property, DocumentSpan location, out string nameStr, out DocumentSpan nameLocation, out string valueStr, out DocumentSpan valueLocation, out bool localOnly) {
			Match match = propertyRegex.Match(property);
			Group name = match.Groups["name"];
			Group value = match.Groups["value"];
			nameStr = name.Value;
			nameLocation = new DocumentSpan(location.Offset, location.Line, location.Column, name.Length);
			valueStr = value.Value.TrimEnd();
			valueLocation = new DocumentSpan(location.Offset + value.Index, location.Line, location.Column + value.Index, value.Length);

			if (nameStr.StartsWith("@")) {
				localOnly = true;
				nameStr = nameStr[1..];
				//nameLocation = new DocumentSpan(nameLocation.Offset + 1, nameLocation.Line, nameLocation.Column + 1, name.Length - 1);
			}
			else {
				localOnly = false;
			}
		}

		private static string SplitLocalOnly(string text, out bool localOnly) {
			if (text.StartsWith("@")) {
				localOnly = true;
				return text[1..];
			}
			else {
				localOnly = false;
				return text;
			}
		}

		private class Indentation {
			public int DivIndent { get; }
			public int? PropertyIndent { get; set; }
			public int IndentLevel { get; }

			public Indentation(int divIndent, int? propertyIndent, int indentLevel) {
				DivIndent = divIndent;
				PropertyIndent = propertyIndent;
				IndentLevel = indentLevel;
			}

			public override string ToString() {
				return $"{DivIndent}, {PropertyIndent?.ToString() ?? "n/a"}, {IndentLevel}";
			}
		}

		public static IEnumerable<SharpDocumentLine> SplitLines(string document) {

			// TODO Implement block comments #-- --#

			// Would be good to have access to document offsets here, to include with DocumentSpan information?
			//string[] lines = Regex.Split(document, "\r\n|\r|\n", RegexOptions.Multiline, TimeSpan.FromMilliseconds(500));
			//MatchCollection lineMatches = Regex.Matches(document, @"^[^\n]$", RegexOptions.Multiline, TimeSpan.FromMilliseconds(500));

			Stack<Indentation> indentationStack = new Stack<Indentation>();
			indentationStack.Push(new Indentation(-1, 0, 0));

			foreach (ContextValue<string> lineValue in LineSplitting.SplitLines(document)) { // (int lineIdx = 0; lineIdx < lineMatches.Count; lineIdx++) {
				//Match lineMatch = lineMatches[lineIdx];
				if (!string.IsNullOrWhiteSpace(lineValue.Value)) {
					string lineText = lineValue.Value.TrimEnd();
					if (removeComments) {
						// TODO Should this account for other numbers of "\"s?
						lineText = Regex.Replace(lineText, @"(?<!\\)\#.*$", "").TrimEnd(); // Ignore comments
					}

					if (!string.IsNullOrWhiteSpace(lineText)) {
						int lineIndentLength = GetIndent(lineText);
						string content = lineText.Substring(lineIndentLength);

						Match match = regex.Match(content);
						Group? group = match.Groups.Cast<Group>().Where(g => g.Success && types.Contains(g.Name)).FirstOrDefault();
						content = (group?.Value ?? content).Trim();

						DocumentSpan contentLocation;
						if (group?.Success ?? false) {
							contentLocation = new DocumentSpan(lineValue.Location.Offset + lineIndentLength + group.Index, lineValue.Location.Line, lineIndentLength + group.Index, group.Length);
						}
						else {
							contentLocation = new DocumentSpan(lineValue.Location.Offset + lineIndentLength, lineValue.Location.Line, lineIndentLength, content.Length);
						}

						string? property = null;
						DocumentSpan propertyLocation = DocumentSpan.Imaginary;
						bool localOnly = false;

						LineType lineType = LineType.ERROR;
						if (group != null) {
							if (group.Name == "div") {
								lineType = LineType.DIV;
							}
							else if (group.Name == "namedchild") {
								lineType = LineType.NAMEDCHILD;
								content = SplitLocalOnly(content, out localOnly); // Remove any "@"
								content = content[1..]; // Remove the "&"
							}
							else if (group.Name == "property") {
								SplitProperty(content, contentLocation, out content, out contentLocation, out property, out propertyLocation, out localOnly);
								lineType = LineType.PROPERTY;
							}
							else if (group.Name == "flag") {
								lineType = LineType.FLAG;
								content = SplitLocalOnly(content, out localOnly);
							}
							else if (group.Name == "entry") {
								//content = DeEscapeNewlines(content);
								lineType = LineType.ENTRY;
							}
							else if (group.Name == "definition") {
								lineType = LineType.DEFINITION;
								property = Definition.GetDefinitionName(content);
								if (property != null) {
									propertyLocation = contentLocation;
								}
							}
							// else // group == null
						}

						// Best to organise indent tracking here, to avoid confusion
						while ((indentationStack.Peek().PropertyIndent != null && lineIndentLength < indentationStack.Peek().PropertyIndent) || (lineIndentLength <= indentationStack.Peek().DivIndent)) {
							indentationStack.Pop();
						}
						if (indentationStack.Peek().PropertyIndent == null) {
							indentationStack.Peek().PropertyIndent = lineIndentLength;
						}

						int indentLevel;
						if (indentationStack.Peek().PropertyIndent != lineIndentLength) {
							// Invalid indent
							indentLevel = -1;
						}
						else {
							indentLevel = indentationStack.Peek().IndentLevel;

							if (lineType == LineType.DIV || lineType == LineType.NAMEDCHILD) {
								indentationStack.Push(new Indentation(lineIndentLength, null, indentationStack.Peek().IndentLevel + 1));
							}
						}

						if (removeComments) {
							// Unescape hash symbols
							content = DeEscapeHash(content);
							property = DeEscapeHash(property);
						}

						yield return new SharpDocumentLine(contentLocation, indentLevel, content, property, propertyLocation, lineType, localOnly);
					}
				}
			}
		}

	}

	[System.Diagnostics.DebuggerDisplay("Count = {Count}")]
	public class EntryStack {

		[System.Diagnostics.DebuggerDisplay("{indentLevel}: {item}")]
		protected class EntryStackItem {
			public readonly int indentLevel;
			public readonly ConfigEntry item;

			public EntryStackItem(int indentLevel, ConfigEntry item) {
				this.indentLevel = indentLevel;
				this.item = item;
			}
		}

		public readonly DirectoryPath source;

		public readonly ConfigEntry rootEntry;
		protected readonly Stack<EntryStackItem> stack;

		public readonly List<SharpParsingException> parsingExceptions;

		public EntryStack(DirectoryPath source, string rootEntryName) {
			this.source = source;
			this.rootEntry = new ConfigEntry(null, DocumentSpan.Imaginary, rootEntryName);

			stack = new Stack<EntryStackItem>();
			parsingExceptions = new List<SharpParsingException>();
		}

		public int Count => stack.Count;

		public int CurrentIndentLevel { get { return stack.Count > 0 ? stack.Peek().indentLevel : 0; } }

		public virtual ConfigEntry Peek() {
			return (stack.Count == 0) ? rootEntry : stack.Peek().item;
		}
		public ConfigEntry Pop() {
			return stack.Pop().item;
		}
		public virtual void Push(DocumentSpan location, int indentLevel, string type, bool isNamed, bool localOnly) {
			ConfigEntry parent = Peek();
			ConfigEntry newEntry = new ConfigEntry(parent, location, type);
			stack.Push(new EntryStackItem(indentLevel, newEntry));

			if (isNamed) {
				parent.AddNamedChild(newEntry, localOnly);
			}
			else {
				parent.AddChild(newEntry);
			}
		}

		public void SetProperty(DocumentSpan location, string name, DocumentSpan valueLocation, string value, bool localOnly) {
			Peek().SetProperty(location, name, valueLocation, value, localOnly);
		}
		public void AddFlag(DocumentSpan location, string flag, bool localOnly) {
			Peek().AddFlag(location, flag, localOnly);
		}
		public void AddEntry(DocumentSpan location, string entry) {
			Peek().AddEntry(location, entry);
		}
	}

}
