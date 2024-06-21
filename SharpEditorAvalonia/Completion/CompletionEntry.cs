// Copyright (c) 2009 Daniel Grunwald
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace SharpEditorAvalonia {
	/// <summary>
	/// Implements AvalonEdit ICompletionData interface to provide the entries in the completion drop down.
	/// </summary>
	public class CompletionEntry : ICompletionData {

		public CompletionEntry(string text) {
			this.Text = text;
		}

		public IImage? Image { get { return null; } }

		public string Text { get; private set; }

		public string? Append { get; set; } = null;
		public string? AfterCaretAppend { get; set; } = null;

		// Use this property if you want to show a fancy UIElement in the drop down list.
		public object Content { get { return this.Text; } }

		public Control[]? DescriptionElements { private get; set; }
		private StackPanel? descriptionPanel = null;
		public object? Description {
			get {
				if (descriptionPanel == null && DescriptionElements != null) {
					//Console.WriteLine("Making panel");
					descriptionPanel = new StackPanel() { Orientation = Orientation.Vertical };
					foreach (Control element in DescriptionElements) {
						if (element.Parent is Panel panel) {
							panel.Children.Remove(element);
						}
						descriptionPanel.Children.Add(element);
					}
				}
				return descriptionPanel;
			}
		}

		public double Priority { get; set; } = 0;

		public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) {
			textArea.Document.Replace(completionSegment, this.Text);
			if (!string.IsNullOrEmpty(this.Append)) {
				textArea.PerformTextInput(this.Append);
			}
			if (!string.IsNullOrEmpty(this.AfterCaretAppend)) {
				textArea.PerformTextInput(this.AfterCaretAppend);
				textArea.Caret.Offset -= AfterCaretAppend.Length;
			}
		}
	}

	public class CompletionEntryComparer : IComparer<CompletionEntry> {
		public int Compare(CompletionEntry? x, CompletionEntry? y) {
			throw new NotImplementedException();
		}
	}
}
