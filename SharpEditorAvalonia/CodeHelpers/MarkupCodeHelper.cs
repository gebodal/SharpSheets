using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using SharpEditorAvalonia.ContentBuilders;
using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Markup.Canvas;
using SharpSheets.Markup.Elements;
using SharpSheets.Markup.Parsing;
using SharpSheets.Markup.Patterns;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpEditorAvalonia.DataManagers;
using Avalonia.Controls;
using Avalonia.Input;
using SharpEditorAvalonia.Windows;

namespace SharpEditorAvalonia.CodeHelpers {

	public class MarkupCodeHelper : ICodeHelper {

		protected readonly TextEditor textEditor;
		protected readonly MarkupParsingState parsingState;

		protected TextDocument Document { get { return textEditor.Document; } }

		private MarkupCodeHelper(TextEditor textEditor, MarkupParsingState parsingState) {
			this.textEditor = textEditor;
			this.parsingState = parsingState;
		}

		public static ICodeHelper GetCodeHelper(TextEditor textEditor, MarkupParsingState parsingState) {
			return new MarkupCodeHelper(textEditor, parsingState);
		}

		#region Completion Window
		public int GetCompletionStartOffset() {
			return textEditor.CaretOffset;
		}

		public IList<ICompletionData> GetCompletionData() {
			List<ICompletionData> data = new List<ICompletionData>();

			bool expressionRegion = false; // TODO Implement
			if (expressionRegion) {
				bool escaped = textEditor.CaretOffset > 0 && textEditor.Document.Text[textEditor.CaretOffset - 1] == '$';
				if (parsingState?.GetVariables(textEditor.CaretOffset) is IVariableBox variables) {
					foreach (KeyValuePair<EvaluationName, EvaluationType> variable in variables.GetReturnTypes()) {
						string escapedName = (escaped ? "" : "$") + variable.Key.ToString();
						data.Add(new CompletionEntry(escapedName) { DescriptionElements = new TextBlock() { Text = SharpValueHandler.GetTypeName(variable.Value) }.Yield().ToArray() });
					}
				}
			}

			return data;
		}

		public bool TextEnteredTriggerCompletion(TextInputEventArgs e) {
			if (e.Text == "$") {
				return true;
			}
			return false;
		}

		#endregion

		#region Tooltip
		public IList<Control> GetToolTipContent(int offset, string word) {
			List<Control> contents = new List<Control>();

			if(word[0] == '<') {
				// TODO This is horrible. Any better of solving this one? Better handling of punctuation in word-finding?
				offset += 1;
			}

			/*
			IList<object> allResulting = parsingState.GetSpansAtOffset(offset).Where(s => s.Type == MarkupSpanType.ELEMENT).Select(s => s.Resulting).WhereNotNull().FirstOrDefault();

			if (allResulting != null) {
				bool first = true;
				foreach (object resulting in OrderResulting(allResulting)) {
					if (!first) { contents.Add(TooltipBuilder.MakeSeparator()); }
					if (resulting is MarkupPattern pattern) {
						contents.AddRange(TooltipBuilder.MakeConstructorEntry(pattern.GetConstructorDetails(), null, true));
					}
					else if (resulting is DivElement divElement) {
						contents.Add(TooltipBuilder.GetToolTipTextBlock("Available variables:"));

						contents.Add(TooltipBuilder.GetVariableBoxEntries(divElement.Variables));
					}
					else if (resulting != null) {
						contents.Add(new TextBlock() { Text = "Something" });
					}
					first = false;
				}
			}
			*/

			if (GetElementSpans(offset).OrderByDescending(s => s.StartOffset).FirstOrDefault() is MarkupSpan span) {
			//foreach (MarkupSpan span in GetElementSpans(offset)) {

				if (span.Resulting != null) {
					foreach (MarkupPattern pattern in span.Resulting.OfType<MarkupPattern>()) {
						if (pattern.GetConstructorDetails() is MarkupConstructorDetails constructorDetails && constructorDetails.DeclaringType != typeof(ErrorPattern) && constructorDetails.Name != null) {
							contents.AddRange(TooltipBuilder.MakeConstructorEntry(pattern.GetConstructorDetails(), null, true, null));
						}
					}
				}

				if (span.Entity is XMLElement element && MarkupDocumentation.MarkupConstructors.TryGetValue(element.Name, out ConstructorDetails? constructor)) {
					if (contents.Count > 0) { contents.Add(TooltipBuilder.MakeSeparator()); }

					TextBlock xmlBlock = XMLContentBuilder.GetXMLConstructorBlock(constructor, element);
					xmlBlock.Margin = TooltipBuilder.TextBlockMargin;

					//System.Windows.Documents.Paragraph xmlBlock = XMLContentBuilder.GetXMLConstructorBlock(constructor, element);
					//xmlBlock.Margin = TooltipBuilder.TextBlockMargin;
					//xmlBlock.FontFamily = TooltipBuilder.GetToolTipTextBlock().FontFamily;

					//System.Windows.Documents.FlowDocument flowDocument = new System.Windows.Documents.FlowDocument(xmlBlock);
					//FlowDocumentScrollViewer scrollViewer = new FlowDocumentScrollViewer() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
					//scrollViewer.Document = flowDocument;

					contents.Add(xmlBlock);

					if (TooltipBuilder.MakeDescriptionTextBlock(constructor.Description) is TextBlock descriptionBlock) {
						//contents.Add(TooltipBuilder.MakeIndentedBlock(constructor.Description));
						contents.Add(descriptionBlock);
					}
				}

				List<Control> variableBlocks = new List<Control>();
				if (GetOwningDiv(span) is DivElement divElement) {
					variableBlocks.Add(TooltipBuilder.GetToolTipTextBlock("Available variables:"));
					variableBlocks.Add(TooltipBuilder.GetVariableBoxEntries(divElement.Variables));
				}
				if (span.Resulting != null && span.Resulting.OfType<MarkupPattern>().FirstOrDefault() is MarkupPattern) {
					variableBlocks.Add(TooltipBuilder.GetToolTipTextBlock("Canvas variables:"));
					variableBlocks.Add(TooltipBuilder.GetVariableBoxEntries(MarkupEnvironments.DrawingStateVariables));
				}

				if(variableBlocks.Count > 0) {
					if (contents.Count > 0) { contents.Add(TooltipBuilder.MakeSeparator()); }
					contents.AddRange(variableBlocks);
				}

				/*
				if (span.Resulting != null) {
					foreach (DivElement divElement in span.Resulting.OfType<DivElement>()) {
						if (contents.Count > 0) { contents.Add(TooltipBuilder.MakeSeparator()); }
						contents.Add(TooltipBuilder.GetToolTipTextBlock("Available variables:"));
						contents.Add(TooltipBuilder.GetVariableBoxEntries(divElement.Variables));
					}
				}
				*/
			}

			foreach (MarkupSpan attrSpan in GetAttributeSpans(offset)) {
				if (attrSpan.Entity is XMLElement element && attrSpan.Name != null && element.GetAttribute1(attrSpan.Name) is ContextProperty<string> attr) {
					foreach (ArgumentDetails attrArg in GetAttributeDetails(element, attr.Name).DistinctPreserveOrder(ArgumentComparer.Instance)) {
						TextBlock argBlock = XMLContentBuilder.GetXMLArgumentBlock(attrArg, element);
						argBlock.Margin = TooltipBuilder.TextBlockMargin;
						contents.Add(argBlock);

						if (TooltipBuilder.MakeDescriptionTextBlock(attrArg.Description) is TextBlock attrDescriptionBlock) {
							//contents.Add(TooltipBuilder.MakeIndentedBlock(attrArg.Description));
							contents.Add(attrDescriptionBlock);
						}

						if (XMLContentBuilder.GetAttributeType(attrArg.Type) is Type attrType && attrType.IsEnum && SharpDocumentation.GetEnumDoc(attrType) is EnumDoc enumDoc) {
							contents.Add(TooltipBuilder.MakeEnumOptionsBlock(enumDoc, true));
						}

						if (attrArg.UseLocal) {
							contents.Add(TooltipBuilder.MakeIndentedBlock("(Local)"));
						}
					}
				}
			}

			return contents;
		}

		public IList<Control> GetFallbackToolTipContent(int offset) {
			return Array.Empty<Control>();
		}

		private IEnumerable<MarkupSpan> GetElementSpans(int offset) {
			foreach(MarkupSpan span in parsingState.GetSpansAtOffset(offset)) {
				if(span.Type == MarkupSpanType.ELEMENT) {
					yield return span;
				}
				else if(span.Type == MarkupSpanType.ENDTAG) {
					if(span.Owners.FirstOrDefault() is MarkupSpan parentSpan && parentSpan.Type == MarkupSpanType.ELEMENT) {
						yield return parentSpan;
					}
				}
			}
		}

		private IEnumerable<MarkupSpan> GetAttributeSpans(int offset) {
			foreach (MarkupSpan span in parsingState.GetSpansAtOffset(offset)) {
				if(span.Type == MarkupSpanType.ATTRIBUTE && !string.IsNullOrWhiteSpace(span.Name) && span.Entity is XMLElement) {
					yield return span;
				}
			}
		}

		private IEnumerable<ArgumentDetails> GetAttributeDetails(XMLElement elem, string attribute) {
			if (MarkupDocumentation.MarkupConstructors.TryGetValue(elem.Name, out ConstructorDetails? constructor)) {
				if (constructor.Arguments.FirstOrDefault(a => a.Name == attribute) is ArgumentDetails arg) {
					yield return arg;
				}
			}

			foreach (XMLElement child in elem.Children.OfType<XMLElement>()) {
				foreach (ArgumentDetails arg in GetAttributeDetails(child, attribute)) {
					if (!arg.UseLocal) {
						yield return arg;
					}
				}
			}
		}

		private static IEnumerable<object> OrderResulting(IEnumerable<object> source) {
			List<object> remaining = new List<object>();
			foreach(object entry in source) {
				if(entry is MarkupPattern pattern) {
					yield return pattern;
				}
				else {
					remaining.Add(entry);
				}
			}
			foreach(object remain in remaining) {
				yield return remain;
			}
		}

		private static DivElement? GetOwningDiv(MarkupSpan span) {
			if (span.Resulting != null && span.Resulting.OfType<DivElement>().FirstOrDefault() is DivElement divElement) {
				return divElement;
			}

			foreach (MarkupSpan parentSpan in span.Owners.OfType<MarkupSpan>()) {
				if (GetOwningDiv(parentSpan) is DivElement parentDiv) {
					return parentDiv;
				}
			}

			return null;
		}
		#endregion

		#region Context Menu
		public IList<Control> GetContextMenuItems(int offset, string? word) {
			List<Control> items = new List<Control>();

			if (parsingState != null) {
				if(parsingState.GetOwner(offset) is MarkupSpan span && span.Name != null && MarkupDocumentation.MarkupConstructors.TryGetValue(span.Name, out ConstructorDetails? contextConstructor)) {
					MenuItem item = new MenuItem() { Header = "<" + contextConstructor.Name + "> Documentation..." };
					item.Click += delegate { SharpEditorWindow.Instance?.controller.ActivateDocumentationWindow().NavigateTo(contextConstructor, () => MarkupDocumentation.MarkupConstructors.Get(contextConstructor.Name)); };
					items.Add(item);
				}
			}

			return items;
		}

		#endregion

		#region Text Entered

		public void TextEntered(TextInputEventArgs e) {
			int carat = textEditor.CaretOffset;
			DocumentLine currentLine = textEditor.Document.GetLineByOffset(carat);
			int startLimit = 0; // currentLine?.PreviousLine?.Offset ?? currentLine?.Offset ?? 0;
			if (e.Text == "=") {
				textEditor.Document.Insert(carat, "\"\"", AnchorMovementType.AfterInsertion);
				textEditor.CaretOffset--;
				e.Handled = true;
			}
			else if (e.Text == ">") {
				if (FindBackwards(carat - 2, startLimit, '<', out _, out string? elementName).HasValue && elementName != null && elementName.Length > 0) {
					textEditor.Document.Insert(carat, $"</{elementName}>", AnchorMovementType.BeforeInsertion);
					e.Handled = true;
				}
			}
			//else if(e.Text == "/") { }
			else if (e.Text == "{") {
				textEditor.Document.Insert(carat, "}", AnchorMovementType.BeforeInsertion);
				e.Handled = true;
			}
			else if (e.Text == "\"") {
				if (carat > 0 && textEditor.Document.Text[carat - 2] != '"' && FindBackwards(carat - 1, startLimit, '"', out SeekState finalState, out _).HasValue && finalState == SeekState.None) {
					textEditor.Document.Insert(carat, "\"", AnchorMovementType.BeforeInsertion);
					e.Handled = true;
				}
			}
			else if (e.Text == "'") {
				if (carat > 0 && textEditor.Document.Text[carat - 2] != '\'' && FindBackwards(carat - 1, startLimit, '\'', out SeekState finalState, out _).HasValue && finalState == SeekState.None) {
					textEditor.Document.Insert(carat, "'", AnchorMovementType.BeforeInsertion);
					e.Handled = true;
				}
			}
			else if (e.Text == "\n" && currentLine?.PreviousLine is DocumentLine previousLine) {
				ISegment previousIndentationSegment = TextUtilities.GetWhitespaceAfter(textEditor.Document, previousLine.Offset);
				string previousIndentation = textEditor.Document.GetText(previousIndentationSegment);
				//if (carat > 1 && FindNonWhitespaceBackward(carat - 2, currentLine?.PreviousLine?.Offset ?? currentLine?.Offset ?? 0) == '>' && FindNonWhitespaceForward(carat, currentLine?.NextLine?.EndOffset ?? currentLine?.EndOffset ?? textEditor.Document.TextLength) == '<') {
				if (carat > 1 && FindNonWhitespaceBackward(carat - 2, currentLine?.PreviousLine?.Offset ?? currentLine?.Offset ?? 0, 1) == ">" && FindNonWhitespaceForward(carat, currentLine?.NextLine?.EndOffset ?? currentLine?.EndOffset ?? textEditor.Document.TextLength, 2) == "</") {
					textEditor.Document.Insert(carat, "\n" + previousIndentation, AnchorMovementType.BeforeInsertion);
					textEditor.Document.Insert(carat, previousIndentation + (previousIndentation.Length > 0 ? previousIndentation[previousIndentation.Length - 1].ToString() : ""), AnchorMovementType.AfterInsertion);
				}
				else {
					textEditor.Document.Insert(carat, previousIndentation, AnchorMovementType.AfterInsertion);
				}
				e.Handled = true;
			}

			//Console.WriteLine(e.Text=="\n" ? "\\n" : e.Text);
		}

		private enum SeekState {
			Unset = 0,
			None = 1,
			SingleString = 2,
			DoubleString = 3,
			AwaitEquals = 4,
			AwaitAttributeName = 5,
			AttributeName = 6,
			ElementTag = 7,
			AwaitOpen = 8,
			End = 9
		}

		private int? FindBackwards(int offset, int startLimit, char stopChar, out SeekState finalState, out string? elementName) {
			if(offset < startLimit) {
				finalState = SeekState.Unset;
				elementName = null;
				return null;
			}

			SeekState state = SeekState.None;
			//char stopChar = '<';
			StringBuilder elementNameBuilder = new StringBuilder();
			elementName = null;

			for (int i = offset; i >= startLimit; i--) {
				char current = textEditor.Document.Text[i];

				if(!(state == SeekState.SingleString || state == SeekState.DoubleString) && current == stopChar) {
					elementName = elementNameBuilder.Length > 0 ? elementNameBuilder.ToString() : null;
					finalState = state;
					return i;
				}

				if(state == SeekState.None) {
					if(current == '<') {
						finalState = state;
						return null;
					}
					else if(current == '/') {
						finalState = state;
						return null; // Is this the right response here?
					}
					else if(current == '\'') {
						state = SeekState.SingleString;
					}
					else if(current == '"') {
						state = SeekState.DoubleString;
					}
					else if (IsValidTagChar(current)) {
						state = SeekState.ElementTag;
						elementNameBuilder.Insert(0, current);
					}
					else if (!char.IsWhiteSpace(current)) {
						finalState = state;
						return null; // Error
					}
				}
				else if(state == SeekState.SingleString || state == SeekState.DoubleString) {
					char endChar = state == SeekState.SingleString ? '\'' : '\"';
					if(current == endChar) {
						state = SeekState.AwaitEquals;
					}
				}
				else if(state == SeekState.AwaitEquals) {
					if(current == '=') {
						state = SeekState.AwaitAttributeName;
					}
					else if (!char.IsWhiteSpace(current)) {
						finalState = state;
						return null; // Error
					}
				}
				else if(state == SeekState.AwaitAttributeName) {
					if (IsValidTagChar(current)) {
						state = SeekState.AttributeName;
					}
					else if (!char.IsWhiteSpace(current)) {
						finalState = state;
						return null; // Error
					}
				}
				else if(state == SeekState.AttributeName) {
					if (char.IsWhiteSpace(current)) {
						state = SeekState.None;
					}
					else if(!IsValidTagChar(current)) {
						finalState = state;
						return null; // Error
					}
				}
				else if(state == SeekState.ElementTag) {
					if (char.IsWhiteSpace(current)) {
						state = SeekState.AwaitOpen;
					}
					else if (IsValidTagChar(current)) {
						elementNameBuilder.Insert(0, current);
					}
					else {
						finalState = state;
						return null; // Error
					}
				}
				else if(state == SeekState.AwaitOpen) {
					if(current == '<') {
						state = SeekState.End;
						break;
					}
					else if (!char.IsWhiteSpace(current)) {
						finalState = state;
						return null;
					}
				}
				else {
					finalState = state;
					return null;
				}
			}

			finalState = state;
			return null;
		}

		private static bool IsValidTagChar(char c) => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_';

		private string? FindNonWhitespaceBackward(int offset, int startLimit, int charCount) {
			StringBuilder result = new StringBuilder(charCount);
			for (int i = offset; i >= startLimit; i--) {
				char current = textEditor.Document.Text[i];
				if (!char.IsWhiteSpace(current)) {
					result.Insert(0, current);
					if(result.Length >= charCount) {
						return result.ToString();
					}
				}
			}
			return result.Length > 0 ? result.ToString() : null;
		}

		private string? FindNonWhitespaceForward(int offset, int endLimit, int charCount) {
			StringBuilder result = new StringBuilder(charCount);
			for (int i = offset; i < endLimit && i < textEditor.Document.TextLength; i++) {
				char current = textEditor.Document.Text[i];
				if (!char.IsWhiteSpace(current)) {
					result.Append(current);
					if (result.Length >= charCount) {
						return result.ToString();
					}
				}
			}
			return result.Length > 0 ? result.ToString() : null;
		}

		#endregion

		#region Commenting

		public bool SupportsIncrementComment { get; } = false;

		public void IncrementComment(int offset, int length) {
			
		}

		public void DecrementComment(int offset, int length) {

		}

		#endregion

		#region Pasting

		public void TextPasted(EventArgs args, int offset) {

		}

		#endregion
	}

}
