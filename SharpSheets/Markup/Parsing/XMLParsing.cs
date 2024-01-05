using SharpSheets.Exceptions;
using SharpSheets.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpSheets.Markup.Parsing {

	public abstract class XMLNode : IDocumentEntity {
		public string SimpleName { get; }
		public string DetailedName { get { return (Parent != null ? $"<{Parent.children.IndexOf(this)}>" : "") + $"{SimpleName}({Depth})"; } }
		public string FullName { get { return (Parent != null ? Parent.FullName + "." : "") + DetailedName; } }

		public DocumentSpan Location { get; }
		public int Depth { get { return Parent != null ? Parent.Depth + 1 : 0; } }

		public XMLElement? Parent { get; }
		public IEnumerable<XMLNode> Children { get { return children.Where(c => c is not XMLComment); } } // TODO Ignoring comments good idea?

		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		public virtual XMLNode Original { get; protected set; }

		protected readonly List<XMLNode> children;

		public XMLNode(XMLElement? parent, string name, DocumentSpan location) {
			this.Parent = parent;
			this.SimpleName = name;
			this.Location = location;

			children = new List<XMLNode>();

			Original = this;
		}

		public abstract XMLNode Clone(XMLElement parent);

		public override int GetHashCode() {
			return FullName.GetHashCode();
		}

		public override bool Equals(object? obj) {
			if (obj is XMLNode node) {
				return this.SimpleName == node.SimpleName && this.Location == node.Location; // FullName == node.FullName;
			}
			else {
				return false;
			}
		}

		public IEnumerable<XMLNode> TraverseNodes() {
			yield return this;
			foreach (XMLNode child in children) {
				foreach (XMLNode node in child.TraverseNodes()) {
					yield return node;
				}
			}
			if (this is XMLElement thisElement && thisElement.EndTag is XMLElementEnd endTag) {
				yield return endTag;
			}
		}

	}

	public class XMLElement : XMLNode {

		public IEnumerable<XMLElement> Elements => children.OfType<XMLElement>();
		public IEnumerable<ContextProperty<string>> Attributes => attributes.Values;

		public int AttributeCount { get { return attributes.Count; } }

		public string Name => NameValue.Value;
		public readonly ContextValue<string> NameValue;
		private readonly Dictionary<string, ContextProperty<string>> attributes;

		public XMLElementEnd? EndTag { get; internal set; }

		public static readonly StringComparer AttributeNameComparer = StringComparer.Ordinal;

		public XMLElement(XMLElement? parent, DocumentSpan startLocation, ContextValue<string> name, IDictionary<string, ContextProperty<string>> attributes, XMLElementEnd? endTag) : base(parent, name.Value, startLocation) {
			this.NameValue = name;
			this.EndTag = endTag;
			this.attributes = new Dictionary<string, ContextProperty<string>>(attributes, AttributeNameComparer);
		}

		public void AddChild(XMLNode child) {
			children.Add(child);
		}

		public XMLElement? FindElement(string name) {
			return Elements.Where(e => e.Name == name).FirstOrDefault();
		}
		public IEnumerable<XMLElement> FindElements(string name) {
			return Elements.Where(e => e.Name == name);
		}

		public ContextProperty<string>? GetAttribute1(string name, bool inheritable) {
			if (attributes.TryGetValue(name, out ContextProperty<string> attribute)) {
				return attribute;
			}
			else if (inheritable && Parent != null) {
				return Parent.GetAttribute1(name, inheritable);
			}
			else {
				return null;
			}
		}

		public ContextProperty<string>? GetAttribute1(string name) {
			return GetAttribute1(name, false);
		}

		public bool HasAttribute(string name, bool inheritable) {
			if (attributes.ContainsKey(name)) {
				return true;
			}
			else if (inheritable && Parent != null) {
				return Parent.HasAttribute(name, inheritable);
			}
			else {
				return false;
			}
		}

		public override XMLNode Clone(XMLElement parent) {
			return CloneElement(parent, this.attributes);
		}

		public XMLElement Clone(XMLElement parent, IDictionary<string, ContextProperty<string>> replacementAttributes) {
			/*
			Dictionary<string, ContextProperty<string>> newAttributes = new Dictionary<string, ContextProperty<string>>(attributes, attributeNameComparer);
			foreach(KeyValuePair<string, ContextProperty<string>> attribute in replacementAttributes) {
				newAttributes[attribute.Key] = attribute.Value;
			}
			*/

			return CloneElement(parent, replacementAttributes);
		}

		private XMLElement CloneElement(XMLElement parent, IDictionary<string, ContextProperty<string>> attributes) {
			XMLElement cloned = new XMLElement(parent, this.Location, this.NameValue, attributes, null) { Original = this.Original };
			cloned.EndTag = parent.EndTag?.Clone(cloned) as XMLElementEnd;

			foreach (XMLNode child in Children) {
				XMLNode clonedChild = child.Clone(cloned);
				cloned.AddChild(clonedChild);
			}

			return cloned;
		}

	}

	public class XMLElementEnd : XMLNode {
		//public readonly DocumentSpan Location;
		public readonly DocumentSpan NameLocation;

		public XMLElementEnd(XMLElement start, DocumentSpan location, DocumentSpan nameLocation) : base(start, start.Name, location) {
			NameLocation = nameLocation;
		}

		public override XMLNode Clone(XMLElement parent) {
			return new XMLElementEnd(parent, Location, NameLocation) { Original = this.Original };
		}
	}

	public class XMLText : XMLNode {

		public readonly string Text;

		public XMLText(XMLElement parent, DocumentSpan location, string text) : base(parent, "__text__", location) {
			this.Text = text;
		}

		public override XMLNode Clone(XMLElement parent) {
			return new XMLText(parent, this.Location, this.Text) { Original = this.Original };
		}

	}

	public class XMLComment : XMLNode {

		public readonly string Text;

		public XMLComment(XMLElement parent, DocumentSpan location, string text) : base(parent, "__comment__", location) {
			this.Text = text;
		}

		public override XMLNode Clone(XMLElement parent) {
			return new XMLComment(parent, this.Location, this.Text) { Original = this.Original };
		}

	}

	public static class XMLParsing {

		private enum ParseState { AwaitNode, AwaitElementType, AwaitElementTag, ElementTag, AwaitAttributeName, AttributeName, AwaitAttributeEquals, AwaitAttributeValue, AttributeValue, AttributeEscapeSequence, AwaitEmptyTagEnd, AwaitEndTag, EndTag, AwaitEndTagClose, Text, TextEscapeSequence, Comment }

		private readonly struct DocumentLocation {
			public int Offset { get; }
			public int Line { get; }
			public int Column { get; }
			public DocumentLocation(int offset, int line, int column) {
				Offset = offset;
				Line = line;
				Column = column;
			}
		}

		public static XMLElement? Parse(string text, out List<SharpParsingException> errors, bool preserveComments) {

			errors = new List<SharpParsingException>();

			ElementBuilder? root = null;

			Stack<ElementBuilder> elementStack = new Stack<ElementBuilder>();
			ElementBuilder? currentElement = null;
			TextNodeBuilder? currentTextNode = null;
			CommentNodeBuiler? currentCommentNode = null;
			EndTag? currentEndTag = null;

			ParseState state = ParseState.AwaitNode;

			DocumentLocation currentHead = new DocumentLocation(-1, -1, -1);
			int lastNonWhitespace = -1;

			int line = 0;
			int lineChar = -1;

			// TODO Cleanup?
			#nullable disable
			for(int i=0; i< text.Length; i++) {
				char current = text[i];

				//string printChar = "\"" + (current == '\n' ? "\\n" : (current == '\t' ? "\\t" : (current == '\r' ? "\\r" : current.ToString()))) + "\"";
				//string currentElementStr = currentElement == null ? "none" : currentElement.GetName();
				//Console.WriteLine($"{state,25}: {printChar,4} ({elementStack.Count}, {currentElementStr})");

				if(current == '\n') {
					line++;
					lineChar = -1;
				}
				else {
					lineChar++;
				}

				DocumentLocation CurrentLocation() {
					return new DocumentLocation(i, line, lineChar);
				}

				if (state == ParseState.AwaitNode) {
					if (current == '<') {
						if (NextCharsEqual(text, i, "<!--")) { // text.Length > (i + 3) && text.Substring(i, 4) is string nextChars && nextChars == "<!--") { // current == '!' && text.Length > (i + 2) && text[i + 1] == '-' && text[i + 2] == '-') {
							// This is the start of a comment
							state = ParseState.Comment;
							currentCommentNode = new CommentNodeBuiler(CurrentLocation());
							currentHead = CurrentLocation();
							i += 3;
							lineChar += 3;
						}
						else {
							// Begin element
							state = ParseState.AwaitElementType;
							currentHead = CurrentLocation();
						}
					}
					else if (elementStack.Count > 0) { // !char.IsWhiteSpace(current)
						// Start text node
						currentTextNode = new TextNodeBuilder(CurrentLocation());
						state = ParseState.Text;
						currentHead = CurrentLocation();
					}
				}
				else if (state == ParseState.AwaitElementType) {
					/*
					if (current == '!' && text.Length > (i + 2) && text[i + 1] == '-' && text[i + 2] == '-') {
						// This is the start of a comment
						state = ParseState.Comment;
						i += 2;
					}
					else*/
					if (current == '/') {
						// This is an end tag
						state = ParseState.AwaitEndTag;
					}
					else if (!char.IsWhiteSpace(current)) {
						// Start tag
						currentElement = new ElementBuilder(currentHead) { TagStartLocation = CurrentLocation() };
						state = ParseState.ElementTag;
						currentElement.AddNameChar(current);
						if (!ValidTagStartChar(current)) { currentElement.SetNameInvalid(); }
						if (root == null) { root = currentElement; }
					}
					else {
						state = ParseState.AwaitElementTag;
					}
				}
				else if (state == ParseState.AwaitElementTag) {
					if (current == '/') {
						// This is an end tag
						state = ParseState.AwaitEndTag;
					}
					else if (!char.IsWhiteSpace(current)) {
						// Start tag
						currentElement = new ElementBuilder(currentHead) { TagStartLocation = CurrentLocation() };
						state = ParseState.ElementTag;
						currentElement.AddNameChar(current);
						if (!ValidTagStartChar(current)) { currentElement.SetNameInvalid(); }
						if (root == null) { root = currentElement; }
					}
				}
				else if (state == ParseState.ElementTag) {
					if (char.IsWhiteSpace(current)) {
						// End tag
						currentElement.TagEnd = lastNonWhitespace;
						state = ParseState.AwaitAttributeName;
					}
					else if (current == '>') {
						// Close tag
						currentElement.TagEnd = lastNonWhitespace;
						currentElement.End = i;
						if (elementStack.Count > 0) { elementStack.Peek().AddChild(currentElement); }
						elementStack.Push(currentElement);
						if (currentElement.InvalidNode) {
							errors.Add(new SharpParsingException(currentElement.GetLocation(), "Badly formatted element node."));
						}
						currentElement = null;
						state = ParseState.AwaitNode;
					}
					else if (current == '/') {
						// End element
						currentElement.TagEnd = lastNonWhitespace;
						state = ParseState.AwaitEmptyTagEnd;
					}
					else {
						// Append char
						currentElement.AddNameChar(current);
						if (!ValidTagChar(current)) { currentElement.SetNameInvalid(); }
					}
				}
				else if (state == ParseState.AwaitAttributeName) {
					if (current == '>') {
						// Close tag
						currentElement.End = i;
						if (elementStack.Count > 0) { elementStack.Peek().AddChild(currentElement); }
						elementStack.Push(currentElement);
						if (currentElement.InvalidNode) {
							errors.Add(new SharpParsingException(currentElement.GetLocation(), "Badly formatted element node."));
						}
						currentElement = null;
						state = ParseState.AwaitNode;
					}
					else if (current == '/') {
						// End element
						state = ParseState.AwaitEmptyTagEnd;
					}
					else if (!char.IsWhiteSpace(current)) {
						// Start attribute name
						currentElement.AddAttribute(CurrentLocation());
						state = ParseState.AttributeName;
						currentElement.CurrentAttribute.AddNameChar(current);
						if (!ValidAttributeStartChar(current)) { currentElement.CurrentAttribute.SetNameInvalid(); }
					}
				}
				else if (state == ParseState.AttributeName) {
					if (char.IsWhiteSpace(current)) {
						// End name
						currentElement.CurrentAttribute.NameEnd = lastNonWhitespace;
						state = ParseState.AwaitAttributeEquals;
					}
					else if (current == '=') {
						currentElement.CurrentAttribute.NameEnd = lastNonWhitespace;
						state = ParseState.AwaitAttributeValue;
					}
					else {
						// Add to attribute name
						currentElement.CurrentAttribute.AddNameChar(current);
						if (!ValidAttributeChar(current)) { currentElement.CurrentAttribute.SetNameInvalid(); }
					}
				}
				else if (state == ParseState.AwaitAttributeEquals) {
					if (current == '=') {
						// Start looking for attribute value
						state = ParseState.AwaitAttributeValue;
					}
					else if (current == '>') {
						// Premature end for this element
						currentElement.End = i;
						currentElement.SetInvalid();
						elementStack.Push(currentElement);
						currentElement = null;
					}
					else if (current == '/') {
						// Premature end for empty node
						currentElement.SetInvalid();
						state = ParseState.AwaitEmptyTagEnd;
					}
					else if (!char.IsWhiteSpace(current)) {
						// Error
						currentElement?.SetInvalid();
					}
				}
				else if (state == ParseState.AwaitAttributeValue) {
					if (current == '"' || current == '\'') {
						// Start value
						currentElement.CurrentAttribute.SetValueStart(CurrentLocation());
						currentElement.CurrentAttribute.ValueQuote = current;
						state = ParseState.AttributeValue;
					}
					else if (!char.IsWhiteSpace(current)) {
						// Error
						currentElement.SetInvalid();
					}
				}
				else if (state == ParseState.AttributeValue) {
					if (current == '&') {
						// Escape sequence
						state = ParseState.AttributeEscapeSequence;
						currentHead = CurrentLocation();
					}
					else if (current == currentElement.CurrentAttribute.ValueQuote.Value) {
						// End value
						currentElement.CurrentAttribute.ValueEnd = i;
						state = ParseState.AwaitAttributeName;
					}
					else {
						currentElement.CurrentAttribute.AddValueChar(current);
					}
				}
				else if (state == ParseState.AttributeEscapeSequence) {
					if (current == ';' || !char.IsLetter(current)) {
						string escapeSequence = text.Substring(currentHead.Offset, i - currentHead.Offset + 1);
						if (escapeSequences.TryGetValue(escapeSequence, out char escapeChar)) {
							currentElement.CurrentAttribute.AddValueChar(escapeChar);
						}
						else {
							errors.Add(new SharpParsingException(new DocumentSpan(currentHead.Offset, currentHead.Line, currentHead.Column, escapeSequence.Length), $"Unrecognized escape sequence: \"{escapeSequence}\"."));
							currentElement.CurrentAttribute.AddValueChars(escapeSequence);
						}
						state = ParseState.AttributeValue;
					}
				}
				else if (state == ParseState.AwaitEmptyTagEnd) {
					if (current == '>') {
						// End element
						currentElement.End = i;
						if (elementStack.Count > 0) { elementStack.Peek().AddChild(currentElement); }
						if (currentElement.InvalidNode) {
							errors.Add(new SharpParsingException(currentElement.GetLocation(), "Badly formatted element node."));
						}
						currentElement = null;
						state = ParseState.AwaitNode;
					}
					else if (!char.IsWhiteSpace(current)) {
						// Error
						currentElement.SetInvalid();
					}
				}
				else if (state == ParseState.AwaitEndTag) {
					if (!char.IsWhiteSpace(current)) {
						// Start tag
						currentEndTag = new EndTag(currentHead) { TagStartLocation = CurrentLocation() };
						state = ParseState.EndTag;
						currentEndTag.AddNameChar(current);
						if (!ValidTagStartChar(current)) { currentEndTag.SetNameInvalid(); }
					}
				}
				else if (state == ParseState.EndTag) {
					if (current == '>') {
						currentEndTag.TagEnd = lastNonWhitespace;
						currentEndTag.End = i;
						if (elementStack.Count > 0 && currentEndTag.GetName() == elementStack.Peek().GetName()) {
							elementStack.Peek().EndTag = currentEndTag;
							elementStack.Pop();
						}
						else if (elementStack.Count > 0) {
							errors.Add(new SharpParsingException(currentEndTag.GetLocation(), $"Invalid end tag for {elementStack.Peek().GetName()} element."));
						}
						else {
							errors.Add(new SharpParsingException(currentEndTag.GetLocation(), "Invalid end tag: no corresponding start tag found."));
						}
						if (currentEndTag.InvalidNode) {
							errors.Add(new SharpParsingException(currentEndTag.GetLocation(), "Badly formatted end tag."));
						}
						currentEndTag = null;
						state = ParseState.AwaitNode;
					}
					else if (char.IsWhiteSpace(current)) {
						// End tag
						currentEndTag.TagEnd = lastNonWhitespace;
						state = ParseState.AwaitEndTagClose;
					}
					else {
						// Append char
						currentEndTag.AddNameChar(current);
						if (!ValidTagChar(current)) { currentEndTag.SetNameInvalid(); }
					}
				}
				else if (state == ParseState.AwaitEndTagClose) {
					if (current == '>') {
						//currentEndTag.TagEnd = lastNonWhitespace;
						currentEndTag.End = i;
						if (elementStack.Count > 0 && currentEndTag.GetName() == elementStack.Peek().GetName()) {
							elementStack.Peek().EndTag = currentEndTag;
							elementStack.Pop();
						}
						else if (elementStack.Count > 0) {
							errors.Add(new SharpParsingException(currentEndTag.GetLocation(), $"Invalid end tag for {elementStack.Peek().GetName()} element."));
						}
						else {
							errors.Add(new SharpParsingException(currentEndTag.GetLocation(), "Invalid end tag: no corresponding start tag found."));
						}
						if (currentEndTag.InvalidNode) {
							errors.Add(new SharpParsingException(currentEndTag.GetLocation(), "Badly formatted end tag."));
						}
						currentEndTag = null;
						state = ParseState.AwaitNode;
					}
					else if (!char.IsWhiteSpace(current)) {
						// Error
						currentEndTag.SetInvalid();
					}
				}
				else if (state == ParseState.Text) {
					if (current == '&') {
						// Escape sequence
						currentTextNode.AppendText(text.Substring(currentHead.Offset, lastNonWhitespace - currentHead.Offset + 1));
						state = ParseState.TextEscapeSequence;
						currentHead = CurrentLocation();
					}
					else if (current == '<') {
						// Start element
						currentTextNode.End = i - 1; // lastNonWhitespace
						currentTextNode.AppendText(text.Substring(currentHead.Offset, currentTextNode.End - currentHead.Offset + 1)); // lastNonWhitespace - currentHead.Offset + 1
						if (elementStack.Count > 0) {
							elementStack.Peek().AddChild(currentTextNode);
						}
						else {
							currentTextNode.SetInvalid();
						}
						if (currentTextNode.InvalidNode) {
							errors.Add(new SharpParsingException(currentTextNode.GetLocation(), "Invalid text node."));
						}
						currentTextNode = null;
						if (NextCharsEqual(text, i, "<!--")) { // text.Length > (i + 3) && text.Substring(i, 4) is string nextChars && nextChars == "<!--") { // current == '!' && text.Length > (i + 2) && text[i + 1] == '-' && text[i + 2] == '-') {
							// This is the start of a comment
							state = ParseState.Comment;
							currentCommentNode = new CommentNodeBuiler(CurrentLocation());
							currentHead = CurrentLocation();
							i += 3;
							lineChar += 3;
						}
						else {
							state = ParseState.AwaitElementType;
							currentHead = CurrentLocation();
						}
					}
				}
				else if (state == ParseState.TextEscapeSequence) {
					if (current == ';' || !char.IsLetter(current)) {
						string escapeSequence = text.Substring(currentHead.Offset, i - currentHead.Offset + 1);
						if (escapeSequences.TryGetValue(escapeSequence, out char escapeChar)) {
							currentTextNode.AppendText(escapeChar);
						}
						else {
							errors.Add(new SharpParsingException(new DocumentSpan(currentHead.Offset, currentHead.Line, currentHead.Column, escapeSequence.Length), $"Unrecognized escape sequence: \"{escapeSequence}\"."));
							currentTextNode.AppendText(escapeSequence);
						}
						state = ParseState.Text;
						currentHead = new DocumentLocation(i + 1, -1, -1);
					}
				}
				else if (state == ParseState.Comment) {
					if (NextCharsEqual(text, i, "-->")) {
						currentCommentNode.End = i + 2;
						if (elementStack.Count > 0) {
							if (preserveComments) {
								elementStack.Peek().AddChild(currentCommentNode);
							}
						}
						else {
							currentCommentNode.SetInvalid();
						}
						if (currentCommentNode.InvalidNode) {
							errors.Add(new SharpParsingException(currentCommentNode.GetLocation(), "Invalid comment node."));
						}
						currentCommentNode = null;
						state = ParseState.AwaitNode;
						i += 2;
						lineChar += 2;
						currentHead = CurrentLocation();
					}
					else if (preserveComments) {
						currentCommentNode.AppendText(current);
					}
				}

				if (!char.IsWhiteSpace(current)) { lastNonWhitespace = i; }
			}
			#nullable enable

			if (elementStack.Count > 0) {
				foreach(ElementBuilder unclosedElement in elementStack) {
					errors.Add(new SharpParsingException(unclosedElement.GetNameLocation(), $"Unclosed \"{unclosedElement.GetName()}\" element in document."));
				}
			}
			if (currentElement != null) {
				currentElement.End = text.Length - 1;
				errors.Add(new SharpParsingException(currentElement.GetLocation(), "Badly formatted element at end of document."));
			}

			if (root == null) {
				return null;
			}

			return MakeElement(null, root);
		}

		private static bool ValidTagStartChar(char c) => char.IsLetter(c);
		private static bool ValidTagChar(char c) => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_';

		private static bool ValidAttributeStartChar(char c) => char.IsLetter(c);
		private static bool ValidAttributeChar(char c) => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_';

		private static bool NextCharsEqual(string text, int index, string nextChars) {
			if (index + nextChars.Length > text.Length) {
				return false;
			}

			//Console.WriteLine("Got here: " + text.Substring(index, 4) + " (" + nextChars + ")");

			for (int i = 0; i < nextChars.Length; i++) {
				if (text[index + i] != nextChars[i]) {
					return false;
				}
			}

			return true;
		}

		private static readonly Dictionary<string, char> escapeSequences = new Dictionary<string, char>() {
			{ "&quot;", '"' },
			{ "&apos;", '\'' },
			{ "&lt;", '<' },
			{ "&gt;", '>' },
			{ "&amp;", '&' }
		};

		private class NodeBuilder {

			public int Start => StartLocation.Offset;
			public DocumentLocation StartLocation { get; }
			public int End { get; set; } = -1;

			public bool InvalidNode { get; private set; } = false;

			public NodeBuilder(DocumentLocation startLocation) {
				StartLocation = startLocation;
			}

			public void SetInvalid() {
				this.InvalidNode = true;
			}

		}

		private class ElementBuilder : NodeBuilder {

			public int TagStart => TagStartLocation.Offset;
			public DocumentLocation TagStartLocation { get; set; } = new DocumentLocation(-1, -1, -1);
			public int TagEnd { get; set; } = -1;

			public bool NameInvalid { get; private set; } = false;

			private readonly StringBuilder name;
			private readonly List<AttributeBuilder> attributes;

			public AttributeBuilder? CurrentAttribute { get; private set; } = null;

			private readonly List<NodeBuilder> children;
			public IEnumerable<NodeBuilder> Children { get { return children; } }

			public EndTag? EndTag { get; set; } = null;

			public ElementBuilder(DocumentLocation startLocation) : base(startLocation) {
				this.name = new StringBuilder();
				this.attributes = new List<AttributeBuilder>();
				this.children = new List<NodeBuilder>();
			}

			public void AddNameChar(char c) {
				name.Append(c);
			}

			public void SetNameInvalid() {
				NameInvalid = true;
			}

			public void AddAttribute(DocumentLocation nameStartLocation) {
				AttributeBuilder attributeBuilder = new AttributeBuilder(nameStartLocation);
				attributes.Add(attributeBuilder);
				CurrentAttribute = attributeBuilder;
			}

			public void AddChild(NodeBuilder node) {
				this.children.Add(node);
			}

			public string GetName() {
				return name.ToString();
			}

			public DocumentSpan GetLocation() {
				DocumentSpan location = new DocumentSpan(StartLocation.Offset, StartLocation.Line, StartLocation.Column, End - Start + 1);
				return location;
			}

			public DocumentSpan GetNameLocation() {
				DocumentSpan nameLocation = new DocumentSpan(TagStartLocation.Offset, TagStartLocation.Line, TagStartLocation.Column, TagEnd - TagStart + 1);
				return nameLocation;
			}

			public XMLElement Build(XMLElement? parent) {
				DocumentSpan location = GetLocation();
				DocumentSpan nameLocation = GetNameLocation();
				ContextValue<string> name = new ContextValue<string>(nameLocation, this.name.ToString());
				XMLElement element = new XMLElement(parent, location, name, BuildAttributes(), null);
				element.EndTag = EndTag?.Build(element);
				return element;
			}

			private IDictionary<string, ContextProperty<string>> BuildAttributes() {
				Dictionary<string, ContextProperty<string>> attributes = new Dictionary<string, ContextProperty<string>>();
				foreach(AttributeBuilder attrBuilder in this.attributes) {
					ContextProperty<string>  attr = attrBuilder.Build();
					attributes[attr.Name] = attr;
				}
				return attributes;
			}

		}

		private class EndTag {

			public int Start => StartLocation.Offset;
			public DocumentLocation StartLocation { get; }
			public int End { get; set; } = -1;

			public int TagStart => TagStartLocation.Offset;
			public DocumentLocation TagStartLocation { get; set; } = new DocumentLocation(-1, -1, -1);
			public int TagEnd { get; set; } = -1;

			public bool NameInvalid { get; private set; } = false;
			public bool InvalidNode { get; private set; } = false;

			private readonly StringBuilder name;

			public EndTag(DocumentLocation startLocation) {
				StartLocation = startLocation;
				this.name = new StringBuilder();
			}

			public void SetInvalid() {
				this.InvalidNode = true;
			}

			public void AddNameChar(char c) {
				name.Append(c);
			}

			public void SetNameInvalid() {
				NameInvalid = true;
			}

			public string GetName() {
				return name.ToString();
			}

			public DocumentSpan GetLocation() {
				DocumentSpan location = new DocumentSpan(StartLocation.Offset, StartLocation.Line, StartLocation.Column, End - Start + 1);
				return location;
			}

			public XMLElementEnd Build(XMLElement parent) {
				DocumentSpan location = GetLocation();
				DocumentSpan nameLocation = new DocumentSpan(TagStartLocation.Offset, TagStartLocation.Line, TagStartLocation.Column, TagEnd - TagStart + 1);
				return new XMLElementEnd(parent, location, nameLocation);
			}

		}

		private class AttributeBuilder {

			public int NameStart => NameStartLocation.Offset;
			public DocumentLocation NameStartLocation { get; }
			public int NameEnd { get; set; } = -1;

			public bool NameInvalid { get; private set; } = false;

			public int ValueStart => ValueStartLocation.Offset;
			public DocumentLocation ValueStartLocation { get; private set; } = new DocumentLocation(-1, -1, -1);
			public int ValueEnd { get; set; } = -1;
			public char? ValueQuote { get; set; } = null;

			private readonly StringBuilder name;
			private readonly StringBuilder value;

			public AttributeBuilder(DocumentLocation nameStartLocation) {
				NameStartLocation = nameStartLocation;
				this.name = new StringBuilder();
				this.value = new StringBuilder();
			}

			public void AddNameChar(char c) {
				name.Append(c);
			}

			public void SetNameInvalid() {
				NameInvalid = true;
			}

			public void SetValueStart(DocumentLocation valueStartLocation) {
				ValueStartLocation = valueStartLocation;
			}

			public void AddValueChar(char c) {
				value.Append(c);
			}

			public void AddValueChars(string str) {
				value.Append(str);
			}

			public ContextProperty<string> Build() {
				DocumentSpan nameLocation = new DocumentSpan(NameStartLocation.Offset, NameStartLocation.Line, NameStartLocation.Column, NameEnd - NameStart + 1);
				DocumentSpan valueLocation = new DocumentSpan(ValueStartLocation.Offset, ValueStartLocation.Line, ValueStartLocation.Column, ValueEnd - ValueStart + 1);
				return new ContextProperty<string>(nameLocation, name.ToString(), valueLocation, value.ToString());
			}

		}

		private class TextNodeBuilder : NodeBuilder {

			private readonly StringBuilder text;

			public TextNodeBuilder(DocumentLocation startLocation) : base(startLocation) {
				this.text = new StringBuilder();
			}

			public void AppendText(string text) {
				this.text.Append(text);
			}
			public void AppendText(char c) {
				this.text.Append(c);
			}

			public DocumentSpan GetLocation() {
				DocumentSpan location = new DocumentSpan(StartLocation.Offset, StartLocation.Line, StartLocation.Column, End - Start + 1);
				return location;
			}

			public XMLText? Build(XMLElement parent) {
				DocumentSpan location = GetLocation();
				//string finalText = text.ToString();
				string finalText = Regex.Replace(text.ToString(), @"\s+", " ");
				if (!string.IsNullOrWhiteSpace(finalText)) {
					return new XMLText(parent, location, finalText);
				}
				else {
					return null;
				}
			}

		}

		private class CommentNodeBuiler : NodeBuilder {

			private readonly StringBuilder text;

			public CommentNodeBuiler(DocumentLocation startLocation) : base(startLocation) {
				text = new StringBuilder();
			}

			public void AppendText(char c) {
				text.Append(c);
			}

			public DocumentSpan GetLocation() {
				DocumentSpan location = new DocumentSpan(StartLocation.Offset, StartLocation.Line, StartLocation.Column, End - Start + 1);
				return location;
			}

			public XMLComment Build(XMLElement parent) {
				DocumentSpan location = GetLocation();
				string finalText = Regex.Replace(Regex.Replace(text.ToString(), @"[^\S\n]+\n[^\S\n]+", "\n"), @"[^\S\n]+", " ").Trim();
				return new XMLComment(parent, location, finalText);
			}

		}

		private static XMLElement MakeElement(XMLElement? parent, ElementBuilder element) {

			XMLElement result = element.Build(parent);

			foreach (NodeBuilder childNode in element.Children) {
				if (childNode is ElementBuilder childElement) {
					XMLElement elementNode = MakeElement(result, childElement);
					result.AddChild(elementNode);
				}
				else if (childNode is TextNodeBuilder childText) {
					XMLText? textNode = childText.Build(result);
					if (textNode != null) {
						result.AddChild(textNode);
					}
				}
				else if (childNode is CommentNodeBuiler childComment) {
					XMLComment commentNode = childComment.Build(result);
					result.AddChild(commentNode);
				}
			}

			return result;
		}

		#region Utilities

		private static readonly Regex urlRegex = new Regex(@"
				\# (?<simple> [a-z] [a-z0-9\-\._]* )
			|
				url\(
				(?:
					\"" \# (?<double> [a-z] (?: [^\(\)\""] | \\[\(\)\""])* ) \""
				|
					\' \# (?<single> [a-z] (?: [^\(\)\'] | \\[\(\)\'])* ) \'
				|
					\# (?<noquotes> [a-z] [a-z0-9\-\._]* )
				)
				\)
			", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);

		public static string? NormaliseURL(string value) {
			value = value.Trim();
			Match match = urlRegex.Match(value);
			if (match.Success && match.Length == value.Length) {
				if (match.Groups[1].Success) {
					return match.Groups[1].Value;
				}
				else if (match.Groups[2].Success) {
					return match.Groups[2].Value;
				}
				else if (match.Groups[3].Success) {
					return match.Groups[3].Value;
				}
				else if (match.Groups[4].Success) {
					return match.Groups[4].Value;
				}
			}
			return null;
		}

		#endregion

	}

}
