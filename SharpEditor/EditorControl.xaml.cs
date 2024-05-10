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
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Search;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpEditor.CodeHelpers;
using SharpEditor.DataManagers;
using SharpSheets.Exceptions;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using ICSharpCode.AvalonEdit.Editing;
using System.Collections.ObjectModel;

namespace SharpEditor {

	public class UpdateHeaderEventArgs : EventArgs {
		public string Header { get; }
		public bool UnsavedProgress { get; }
		public UpdateHeaderEventArgs(string header, bool unsavedProgress) {
			this.Header = header;
			this.UnsavedProgress = unsavedProgress;
		}
	}

	public partial class SharpDocumentEditor : UserControl {

		public event EventHandler<UpdateHeaderEventArgs>? UpdateHeader;
		public event EventHandler<SharpDocumentEditor>? DocumentTypeChanged;
		public event EventHandler<SharpDocumentEditor>? RequestFocus;

		public string? Header { get; private set; }

		public string Text { get { return textEditor.Document.CreateSnapshot().Text; } } // Threadsafe too much?
		public int TextLength { get { return textEditor.Document.TextLength; } }

		private bool intentional = true;
		public bool IsDisposable { get { return !(intentional || TextLength > 0 || CurrentFilePath != null || HasUnsavedProgress); } }

		private readonly SearchPanel searchPanel;

		private class CustomLocalization : ICSharpCode.AvalonEdit.Search.Localization {
			public override string NoMatchesFoundText => "No matches"; // TODO What to do with this?
		}

		public SharpDocumentEditor(string? filename) {

			InitializeComponent();
			
			this.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);

			this.searchPanel = SearchPanel.Install(textEditor);
			this.searchPanel.Localization = new CustomLocalization();
			this.searchPanel.MarkerBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
			TextBox searchTextBox = (TextBox)searchPanel.Template.FindName("PART_searchTextBox", searchPanel);
			//searchPanel.SearchOptionsChanged += delegate { Validation.GetHasError(searchTextBox); };
			/*
			Validation.AddErrorHandler(this.searchPanel, (s,e) => { Console.WriteLine("ERROR IN SEARCH PANEL"); });
			searchPanel.SearchOptionsChanged += delegate {
				Console.WriteLine("Changed search options");
				Console.WriteLine(Validation.GetHasError(this.searchPanel));
				Console.WriteLine(Validation.GetHasError(searchTextBox));
			};
			*/

			textEditor.Encoding = System.Text.Encoding.UTF8;

			textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
			Caret_PositionChanged(this, EventArgs.Empty);

			InitializeTextEditorDependencyProperties();
			InitializeTextZoom();
			InitializeBackgroundMessages();
			InitializeParsingManager();
			InitializeDesigner();
			InitializeFoldingManager();
			InitializeToolTip();
			InitializeErrorMarkers();
			InitializeUnsavedTracker();
			InitializeCompletionWindow();

			textEditor.Options.HighlightCurrentLine = true;
			textEditor.Options.EnableEmailHyperlinks = textEditor.Options.EnableHyperlinks = false;
			textEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
			textEditor.Options.AllowScrollBelowDocument = true;

			textEditor.TextArea.SelectionCornerRadius = 1.0; // Can also adjust Selection Brush from here
			textEditor.TextArea.SelectionBorder = null;
			textEditor.TextArea.SelectionForeground = null;
			textEditor.TextArea.SelectionBrush = new SolidColorBrush(Colors.LightSlateGray) { Opacity = 0.5 };

			textEditor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Colors.LightSlateGray) { Opacity = 0.0 };
			textEditor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Colors.LightSlateGray) { Opacity = 0.3 }, 2f);

			textEditor.SnapsToDevicePixels = true;
			textEditor.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

			DataObject.AddPastingHandler(this, PasteEvent);
			//DataObject.AddCopyingHandler(this, CopyEvent);

			SharpDataManager.Instance.WarnFontLicensingChanged += Refresh;

			// Initialise state of text editor
			if (filename == null) {
				New(DocumentType.UNKNOWN, false);
			}
			else {
				Open(filename);
			}
			textEditor.Focus();
		}

		public SharpDocumentEditor(DocumentType documentType, bool intentional) : this(null) {
			this.intentional = intentional;
			SetDocumentType(true, documentType);
		}

		public void Uninstall() {
			textEditor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
			SharpDataManager.Instance.WarnFontLicensingChanged -= Refresh;

			UninstallTextEditorDependencyProperties();
			UninstallTextZoom();
			UninstallParsingManager();
			UninstallDesigner();
			UninstallFoldingManager();
			UninstallToolTip();
			UninstallErrorMarkers();
			UninstallUnsavedTracker();
			UninstallCompletionWindow();
			UninstallBackgroundMessages();
		}

		#region UIElement Helpers
		public bool FocusEditor() {
			return textEditor.TextArea.Focus();
			//Keyboard.Focus(textEditor.TextArea);
			//return textEditor.Focus();
		}

		public void ScrollTo(int offset) {
			textEditor.CaretOffset = offset;
			textEditor.ScrollToLine(textEditor.Document.GetLineByOffset(offset).LineNumber);
		}

		private void Refresh(object? sender, EventArgs e) {
			textEditor.InvalidateVisual();
			textEditor.InvalidateMeasure();
			textEditor.TextArea.TextView.Redraw();
		}
		#endregion UIElement Helpers

		#region TextEditor Dependency Properties

		void InitializeTextEditorDependencyProperties() {
			// Line numbers
			textEditor.ShowLineNumbers = SharpDataManager.Instance.ShowLineNumbers;
			SharpDataManager.Instance.ShowLineNumbersChanged += OnShowLineNumbersChanged;

			// Word wrap
			textEditor.WordWrap = SharpDataManager.Instance.WrapLines;
			SharpDataManager.Instance.WrapLinesChanged += OnWrapLinesChanged;

			// Show end of line
			textEditor.Options.ShowEndOfLine = SharpDataManager.Instance.ShowEndOfLine;
			SharpDataManager.Instance.ShowEndOfLineChanged += OnShowEndOfLineChanged;
		}

		void UninstallTextEditorDependencyProperties() {
			SharpDataManager.Instance.ShowLineNumbersChanged -= OnShowLineNumbersChanged;
			SharpDataManager.Instance.WrapLinesChanged -= OnWrapLinesChanged;
			SharpDataManager.Instance.ShowEndOfLineChanged -= OnShowEndOfLineChanged;
		}

		private void OnShowLineNumbersChanged(object? sender, EventArgs e) {
			this.textEditor.ShowLineNumbers = SharpDataManager.Instance.ShowLineNumbers;
		}

		private void OnWrapLinesChanged(object? sender, EventArgs e) {
			this.textEditor.WordWrap = SharpDataManager.Instance.WrapLines;
		}

		private void OnShowEndOfLineChanged(object? sender, EventArgs e) {
			this.textEditor.Options.ShowEndOfLine = SharpDataManager.Instance.ShowEndOfLine;
		}

		#endregion TextEditor Dependency Properties

		#region Track User Changes
		public bool HasUnsavedProgress { get; private set; }

		private void UpdateUnsavedProgress(object? sender, EventArgs e) {
			bool previousState = HasUnsavedProgress;
			HasUnsavedProgress = true;
			if (HasUnsavedProgress != previousState) { OnUpdateHeader(); }
		}

		void InitializeUnsavedTracker() {
			textEditor.Document.TextChanged += UpdateUnsavedProgress;
		}

		void UninstallUnsavedTracker() {
			textEditor.Document.TextChanged -= UpdateUnsavedProgress;
		}

		private void Caret_PositionChanged(object? sender, EventArgs e) {
			int line = textEditor.TextArea.Caret.Line;
			int column = textEditor.TextArea.Caret.Column;
			CursorLineText.Text = "Ln " + line;
			CursorColText.Text = "Col " + column;
			textEditor.TextArea.TextView.Redraw();
		}
		#endregion Track User Changes

		#region Text Zoom
		//IVisualLineTransformer zoomTransformer;

		private void InitializeTextZoom() {
			//zoomTransformer = new ZoomFontSizeTransformer(this);
			//textEditor.TextArea.TextView.LineTransformers.Add(zoomTransformer);
			baseFontSize = textEditor.FontSize;
			SetTextZoom(SharpDataManager.Instance.TextZoom);
			SharpDataManager.Instance.TextZoomChanged += OnTextZoomChanged;
		}

		private void UninstallTextZoom() {
			//textEditor.TextArea.TextView.LineTransformers.Remove(zoomTransformer);
			SharpDataManager.Instance.TextZoomChanged -= OnTextZoomChanged;
		}

		private void OnTextZoomChanged(object? sender, EventArgs e) {
			SetTextZoom(SharpDataManager.Instance.TextZoom);
		}

		// React to ctrl + mouse wheel to zoom in and out
		private void OnTextPreviewMouseWheel(object? sender, MouseWheelEventArgs e) {
			bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
			if (ctrl) {
				this.StepTextZoom(e.Delta > 0);
				e.Handled = true;
			}
		}

		private const double MAX_TEXT_ZOOM = 4;
		private const double MIN_TEXT_ZOOM = 0.2;
		private const double TEXT_ZOOM_DELTA = 0.05;
		//public double CurrentTextZoom { get; private set; } = 1.0;
		public double baseFontSize;

		private void SetTextZoom(double zoom) {
			DocumentLine firstVisibleLine = textEditor.TextArea.TextView.GetDocumentLineByVisualTop(textEditor.TextArea.TextView.ScrollOffset.Y);

			textEditor.FontSize = baseFontSize * SharpDataManager.Instance.TextZoom;

			textEditor.TextArea.TextView.Redraw();
			if (firstVisibleLine != null) {
				textEditor.ScrollToVerticalOffset(textEditor.TextArea.TextView.GetVisualTopByDocumentLine(firstVisibleLine.LineNumber));
			}

			UpdateZoomText();
		}

		public void StepTextZoom(bool increase) {
			double newZoom;
			if (increase) {
				newZoom = Math.Floor(Math.Round((SharpDataManager.Instance.TextZoom + TEXT_ZOOM_DELTA) / TEXT_ZOOM_DELTA, 2)) * TEXT_ZOOM_DELTA;
			}
			else {
				newZoom = Math.Ceiling(Math.Round((SharpDataManager.Instance.TextZoom - TEXT_ZOOM_DELTA) / TEXT_ZOOM_DELTA, 2)) * TEXT_ZOOM_DELTA;
			}
			SharpDataManager.Instance.TextZoom = newZoom.Clamp(MIN_TEXT_ZOOM, MAX_TEXT_ZOOM);
		}

		private void UpdateZoomText() {
			TextZoomTextBox.Text = string.Format("{0:0.#}%", SharpDataManager.Instance.TextZoom * 100.0);
		}

		private void TextZoomBoxKeyDown(object? sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				e.Handled = true;
				FocusEditor();
			}
		}

		readonly Regex percentageRegex = new Regex("[0-9%\\.]+");
		private void ValidateTextZoomBox(object? sender, TextCompositionEventArgs e) {
			e.Handled = !percentageRegex.IsMatch(e.Text);
		}

		private void TextZoomBoxLostFocus(object? sender, RoutedEventArgs e) {
			Match match = Regex.Match(TextZoomTextBox.Text.Trim(), "^(?<number>[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)%?$");

			//Console.WriteLine("Focus lost: " + TextZoomTextBox.Text + ", " + match + ", " + match.Groups[0] + ", " + match.Groups[1]);

			if (match.Groups[1].Success) {
				try {
					double zoom = double.Parse(match.Groups[1].Value) / 100.0;
					SharpDataManager.Instance.TextZoom = zoom.Clamp(MIN_TEXT_ZOOM, MAX_TEXT_ZOOM);
					return;
				}
				catch (FormatException) { }
			}

			UpdateZoomText();
		}

		private void TextZoomBoxMouseWheel(object? sender, MouseWheelEventArgs e) {
			this.StepTextZoom(e.Delta > 0);
			e.Handled = true;
		}
		#endregion Text Zoom

		#region Background Messages
		private DispatcherTimer backgroundMessageTimer;
		public TimeSpan BackgroundMessageDuration => backgroundMessageTimer.Interval;
		private BackgroundMessage? currentMessage;
		private readonly object backgroundMessageLock = new object();

		private static readonly int LowestMessagePriority = int.MinValue;
		public static readonly int ParsingMessagePriority = 100;
		public static readonly int DesignerMessagePriority = 50;

		private class BackgroundMessage {
			public readonly string Message;
			public readonly int Priority;
			public BackgroundMessage(string message, int priority) {
				Message = message;
				Priority = priority;
			}
		}

		[MemberNotNull(nameof(backgroundMessageTimer))]
		void InitializeBackgroundMessages() {
			backgroundMessageTimer = new DispatcherTimer() { Interval = new TimeSpan(0, 0, 3) };
			backgroundMessageTimer.Tick += new EventHandler((o, s) => BackgroundMessageTick());

			currentMessage = null;
			BackgroundMessageText.Text = "";
		}

		void UninstallBackgroundMessages() {
			backgroundMessageTimer.IsEnabled = false;
		}

		private void BackgroundMessageTick() {
			this.Dispatcher.Invoke(() => {
				lock (backgroundMessageLock) {
					backgroundMessageTimer.IsEnabled = false;
					BackgroundMessages.Visibility = Visibility.Collapsed;
					BackgroundMessageText.Text = "";
				}
			});
		}

		private BackgroundMessage? DisplayBackgroundMessage(string message, int priority, bool withTimer) {
			if(priority >= (currentMessage?.Priority ?? LowestMessagePriority)) {
				lock (backgroundMessageLock) {
					this.Dispatcher.Invoke(() => {
						backgroundMessageTimer.IsEnabled = false;
						BackgroundMessages.Visibility = Visibility.Visible;
						BackgroundMessageText.Text = message;
						if (withTimer) { backgroundMessageTimer.Start(); }
					});
					currentMessage = new BackgroundMessage(message, priority);
					return currentMessage;
				}
			}
			else {
				return null;
			}
		}

		private void FinishBackgroundMessage(BackgroundMessage? message) {
			lock (backgroundMessageLock) {
				if (message == currentMessage) {
					this.Dispatcher.Invoke(() => {
						backgroundMessageTimer.IsEnabled = false;
						BackgroundMessages.Visibility = Visibility.Collapsed;
						BackgroundMessageText.Text = "";
					});
					currentMessage = null;
				}
			}
		}
		#endregion Background Messages

		#region Files

		//static readonly string untitledFilename = "Untitled";

		void OnUpdateHeader() {
			string headerBase = CurrentFileName;

			Header = headerBase + (HasUnsavedProgress ? "*" : "");

			// And call event to notify any listeners that we've changed things
			UpdateHeader?.Invoke(this, new UpdateHeaderEventArgs(Header, HasUnsavedProgress));
		}

		//private string _currentPath;
		public string? CurrentFilePath {
			get { return textEditor.Document.FileName; }
			set {
				textEditor.Document.FileName = value;
				OnUpdateHeader();
			}
		}

		public string CurrentFileName {
			get {
				if (textEditor.Document.FileName != null) {
					return System.IO.Path.GetFileName(textEditor.Document.FileName);
				}
				else {
					return parsingManager.GetCurrentDefaultFilename() + parsingManager.GetCurrentExtension();
				}
			}
		}

		public string? CurrentGeneratePath { get; set; } = null;
		public string? CurrentFieldSourcePath { get; set; } = null;

		private DocumentType CurrentDocumentType = DocumentType.UNKNOWN;

		private void UpdateDocumentType(bool forceUpdate) {
			DocumentType newDocumentType = SharpEditorFileInfo.GetDocumentType(CurrentFilePath);
			SetDocumentType(forceUpdate, newDocumentType);
		}

		private void SetDocumentType(bool forceUpdate, DocumentType newDocumentType) {
			if (newDocumentType != CurrentDocumentType) {
				foldingManager.Clear();
				parsingManager.Reset();

				// TODO These should really be read from a registry somewhere, to make things easier to extend
				if (newDocumentType == DocumentType.SHARPCONFIG) {
					textEditor.SyntaxHighlighting = SharpEditorPalette.CharacterSheetHighlighting;
					foldingStrategy = new SharpSheetsFoldingStrategy(2);
					textEditor.TextArea.IndentationStrategy = new SharpSheetsIndentationStrategy();
					CharacterSheetParsingState characterSheetParsingState = new CharacterSheetParsingState(textEditor.Document);
					parsingState = characterSheetParsingState;
					codeHelper = CharacterSheetCodeHelper.GetCodeHelper(textEditor, characterSheetParsingState);
				}
				else if (newDocumentType == DocumentType.CARDCONFIG) {
					textEditor.SyntaxHighlighting = SharpEditorPalette.CardConfigHighlighting;
					foldingStrategy = new SharpSheetsFoldingStrategy(2);
					textEditor.TextArea.IndentationStrategy = new SharpSheetsIndentationStrategy();
					CardConfigParsingState cardDefinitionParsingState = new CardConfigParsingState(textEditor.Document);
					parsingState = cardDefinitionParsingState;
					codeHelper = CardConfigCodeHelper.GetCodeHelper(textEditor, cardDefinitionParsingState);
				}
				else if (newDocumentType == DocumentType.CARDSUBJECT) {
					textEditor.SyntaxHighlighting = SharpEditorPalette.CardSubjectHighlighting;
					foldingStrategy = new SharpSheetsCardSubjectFoldingStrategy();
					textEditor.TextArea.IndentationStrategy = null;
					CardSubjectParsingState cardSubjectParsingState = new CardSubjectParsingState(textEditor.Document);
					parsingState = cardSubjectParsingState;
					codeHelper = CardSubjectCodeHelper.GetCodeHelper(textEditor, cardSubjectParsingState);
				}
				else if (newDocumentType == DocumentType.MARKUP) {
					textEditor.SyntaxHighlighting = SharpEditorPalette.BoxMarkupHighlighting;
					//foldingStrategy = new XMLFoldingStrategy() { ShowAttributesWhenFolded = true };
					foldingStrategy = new SharpXMLFoldingStrategy() { ShowAttributesWhenFolded = true };
					textEditor.TextArea.IndentationStrategy = null; // new DefaultIndentationStrategy();
					MarkupParsingState markupParsingState = new MarkupParsingState(textEditor.Document);
					parsingState = markupParsingState;
					codeHelper = MarkupCodeHelper.GetCodeHelper(textEditor, markupParsingState);
				}
				else { // fileType == DocumentType.UNKNOWN
					textEditor.SyntaxHighlighting = null;
					foldingStrategy = null;
					textEditor.TextArea.IndentationStrategy = null;
					parsingState = null;
					codeHelper = null;
				}

				parsingManager.SetParsingState(parsingState);

				UpdateFoldings();
				UpdateErrorDisplay(null, EventArgs.Empty);

				DesignerArea.Reset();
				designerGenerator.Reset();
				//ResetDesignerDocument();

				CurrentDocumentType = newDocumentType;

				DocumentTypeChanged?.Invoke(this, this);
				OnUpdateHeader();
			}
		}

		public void New(DocumentType documentType, bool intentional) {
			CurrentFilePath = null;
			textEditor.Text = "";
			textEditor.Encoding = System.Text.Encoding.UTF8;
			HasUnsavedProgress = false;
			CurrentFieldSourcePath = null;

			this.intentional = intentional;

			textEditor.TextArea.Caret.Offset = 0;
			textEditor.ScrollToHome();

			SetDocumentType(true, documentType);

			OnUpdateHeader();
		}

		public void Open(string filename) {
			textEditor.Load(filename); // This will attempt to auto-detect encoding
			textEditor.Encoding = System.Text.Encoding.UTF8; // Now force this document to be considered UTF8

			CurrentFilePath = filename;

			textEditor.TextArea.Caret.Offset = 0;
			textEditor.ScrollToHome();

			UpdateDocumentType(true);

			HasUnsavedProgress = false;
			CurrentFieldSourcePath = null;
			OnUpdateHeader();
		}

		public bool Save(string filename) {
			if(CurrentFilePath != filename) {
				CurrentGeneratePath = null;
			}

			textEditor.Save(filename); // We have manually set document to UTF8, so it will be saved as such
			CurrentFilePath = filename;

			UpdateDocumentType(false); // Check that we haven't saved with a different extension

			HasUnsavedProgress = false;
			OnUpdateHeader();
			return true;
		}

		#endregion Files

		#region CodeHelper
		private ICodeHelper? codeHelper = null;

		public bool SupportsIncrementComment { get { return codeHelper?.SupportsIncrementComment ?? false; } }

		private (int offset, int length) GetSelection() {
			if (textEditor.TextArea.Selection.IsEmpty) {
				int caretOffset = textEditor.CaretOffset;
				return (caretOffset, 0);
			}
			else {
				Selection selection = textEditor.TextArea.Selection;
				int offset1 = textEditor.Document.GetOffset(selection.StartPosition.Location);
				int offset2 = textEditor.Document.GetOffset(selection.EndPosition.Location);
				int startOffset = Math.Min(offset1, offset2);
				int endOffset = Math.Max(offset1, offset2);
				return (startOffset, endOffset - startOffset);
			}
		}

		public void IncrementComment() {
			if (codeHelper is not null) {
				(int offset, int length) = GetSelection();
				codeHelper.IncrementComment(offset, length);
			}
		}
		public void DecrementComment() {
			if (codeHelper is not null) {
				(int offset, int length) = GetSelection();
				codeHelper.DecrementComment(offset, length);
			}
		}

		private void PasteEvent(object sender, DataObjectPastingEventArgs args) {
			codeHelper?.TextPasted(args, textEditor.CaretOffset);
		}

		#endregion CodeHelper

		#region Completion Window

		SharpCompletionWindow? completionWindow;

		void InitializeCompletionWindow() {
			textEditor.TextArea.TextEntering += TextAreaTextEntering;
			textEditor.TextArea.TextEntered += TextAreaTextEntered;
		}

		void UninstallCompletionWindow() {
			textEditor.TextArea.TextEntering -= TextAreaTextEntering;
			textEditor.TextArea.TextEntered -= TextAreaTextEntered;
		}

		void OpenCompletionWindow() {
			if (codeHelper != null) {
				IList<ICompletionData> data = codeHelper.GetCompletionData();

				if (data.Count > 0) {
					int startOffset = codeHelper.GetCompletionStartOffset();
					//Console.WriteLine($"Caret: {textEditor.CaretOffset}, Start: {startOffset}");
					completionWindow = new SharpCompletionWindow(textEditor.TextArea) { StartOffset = startOffset };

					string startingText = textEditor.Document.GetText(completionWindow.StartOffset, completionWindow.EndOffset - completionWindow.StartOffset);
					
					completionWindow.CompletionList.CompletionData.AddRange(data);

					if (!string.IsNullOrEmpty(startingText)) {
						completionWindow.CompletionList.SelectItem(startingText);
					}

					completionWindow.Show();
					completionWindow.Closed += delegate {
						completionWindow = null;
					};
				}
			}
		}

		void TextAreaTextEntered(object? sender, TextCompositionEventArgs e) {
			if (codeHelper?.TextEnteredTriggerCompletion(e) ?? false) {
				OpenCompletionWindow();
			}
			else {
				codeHelper?.TextEntered(e);
			}
		}

		void TextAreaTextEntering(object? sender, TextCompositionEventArgs e) {
			/*
			if (codeHelper?.TextEnteringTriggerCompletion(e) ?? false) {
				OpenCompletionWindow();
				// codeHelper method should be calling e.Handled if needed
			}
			else */
			if (e.Text == " " && Keyboard.Modifiers == ModifierKeys.Control) {
				OpenCompletionWindow();
				e.Handled = true; // Don't actually enter this character
			}
			else if (completionWindow != null && e.Text.Length > 0) {
				/*
				if (!char.IsLetterOrDigit(e.Text[0])) {
					// Whenever a non-letter is typed while the completion window is open,
					// insert the currently selected element.
					completionWindow.CompletionList.RequestInsertion(e);
				}
				*/
				/*
				if (char.IsWhiteSpace(e.Text[0])) {
					// Whenever a whitespace character is typed while the completion window is open,
					// insert the currently selected element.
					completionWindow.CompletionList.RequestInsertion(e);
				}
				*/
				if (e.Text[0] == ':' || completionWindow.VisibleItemCount == 0) {
					// Whenever a colon character is typed, or there are no more visible completion items,
					// while the completion window is open, close the completion window with no insertions
					completionWindow.Close();
					// TODO Should this type of trigger be left up to the CodeHelper?
				}
				// do not set e.Handled=true - we still want to insert the character that was typed
			}
		}

		private void OnPreviewKeyDown(object? sender, KeyEventArgs e) {
			if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
				DocumentLine currentLine = textEditor.Document.GetLineByOffset(textEditor.CaretOffset);
				if (currentLine.NextLine is DocumentLine nextLine) {
					string currentLineText = textEditor.Document.GetText(currentLine.Offset, nextLine.Offset - currentLine.Offset);
					textEditor.Document.Insert(currentLine.Offset, currentLineText);
					e.Handled = true; // Don't actually enter this character
				}
			}
		}
		#endregion Completion Window

		#region Text Context Menu

		private void OnTextPreviewRightMouseDown(object? sender, MouseButtonEventArgs e) {

			ContextMenu contextMenu = new ContextMenu();
			contextMenu.Style = (Style)Resources["ContextMenuStyle"];

			contextMenu.Items.Add(new MenuItem {
				Command = ApplicationCommands.Cut,
				CommandTarget = textEditor.TextArea,
				Icon = (System.Windows.Controls.Image)Resources["CutIcon"]
			});
			contextMenu.Items.Add(new MenuItem {
				Command = ApplicationCommands.Copy,
				CommandTarget = textEditor.TextArea,
				Icon = (System.Windows.Controls.Image)Resources["CopyIcon"]
			});
			contextMenu.Items.Add(new MenuItem {
				Command = ApplicationCommands.Paste,
				CommandTarget = textEditor.TextArea,
				Icon = (System.Windows.Controls.Image)Resources["PasteIcon"]
			});

			// Determine possible actions based on what we clicked on (probably from CodeHelper)

			// Get clicked location
			TextViewPosition? pos = textEditor.GetPositionFromPoint(e.GetPosition(textEditor));
			if (pos.HasValue) {
				// Move cursor to clicked location
				int offset = textEditor.Document.GetOffset(pos.Value);
				textEditor.CaretOffset = offset;

				// If there is no CodeHelper provided, we can't find necessary information
				if (codeHelper != null) {

					// Find out if we've clicked on a drawn object
					object[]? currentDrawnObjects = designerGenerator?.DrawingMapper?.GetDrawnObjects(textEditor.TextArea.Caret.Offset)?.ToArray();
					if (currentDrawnObjects != null && currentDrawnObjects.Length > 0) {
						contextMenu.Items.Add(new Separator());

						MenuItem jumpToDrawnObjectMenuItem = new MenuItem {
							Header = "Show in Designer"
						};
						jumpToDrawnObjectMenuItem.Click += delegate {
							if (DesignerArea?.DisplayObject(currentDrawnObjects.First(), false) ?? false) {
								if (DesignerFloating != null) {
									DesignerFloating.Activate();
								}
							}
						};
						contextMenu.Items.Add(jumpToDrawnObjectMenuItem);

						MenuItem zoomToDrawnObjectMenuItem = new MenuItem {
							Header = "Zoom to in Designer"
						};
						zoomToDrawnObjectMenuItem.Click += delegate {
							if (DesignerArea?.DisplayObject(currentDrawnObjects.First(), true) ?? false) {
								if (DesignerFloating != null) {
									DesignerFloating.Activate();
								}
							}
						};
						contextMenu.Items.Add(zoomToDrawnObjectMenuItem);
					}

					string? mouseWord = textEditor.Document.GetSharpTermStringFromViewPosition(pos.Value);
					if (codeHelper.GetContextMenuItems(offset, mouseWord) is IList<Control> codeHelperMenuItems && codeHelperMenuItems.Count > 0) {
						contextMenu.Items.Add(new Separator());
						foreach (Control codeHelperMenuItem in codeHelperMenuItems) {
							contextMenu.Items.Add(codeHelperMenuItem);
						}
					}
				}
			}

			contextMenu.IsOpen = true;

			//e.Handled = true;
		}

		#endregion Text Context Menu

		#region Tooltip

		void InitializeToolTip() {
			textEditor.MouseHover += TextEditorMouseHover;
			textEditor.MouseHoverStopped += TextEditorMouseHoverStopped;
			textEditor.TextArea.TextEntered += TextAreaTextEnteredTooptipClose;
		}

		void UninstallToolTip() {
			textEditor.MouseHover -= TextEditorMouseHover;
			textEditor.MouseHoverStopped -= TextEditorMouseHoverStopped;
			textEditor.TextArea.TextEntered -= TextAreaTextEnteredTooptipClose;
		}

		void TextEditorMouseHover(object? sender, MouseEventArgs args) {
			TextViewPosition? pos = textEditor.GetPositionFromPoint(args.GetPosition(textEditor));
			if (pos == null) return;
			string? mouseWord = textEditor.Document.GetSharpTermStringFromViewPosition(pos.Value);
			int posOffset = textEditor.Document.GetOffset(pos.Value);
			if (mouseWord is not null) {

				ToolTipPanel.Children.Clear();

				if (codeHelper != null) {
					foreach (UIElement element in codeHelper.GetToolTipContent(posOffset, mouseWord)) {
						ToolTipPanel.Children.Add(element);
					}
				}

				if (parsingState != null) {
					bool madeSeparator = false;
					foreach (string message in parsingState.GetErrors(posOffset).Select(e => e.Message).Distinct()) {
						if (ToolTipPanel.Children.Count > 0 && !madeSeparator) {
							ToolTipPanel.Children.Add(TooltipBuilder.MakeSeparator());
						}
						madeSeparator = true;
						ToolTipPanel.Children.Add(TooltipBuilder.GetToolTipTextBlock(message));
					}
				}
			}
			else if(codeHelper != null) { // No mouse-over word
				ToolTipPanel.Children.Clear();

				foreach (UIElement element in codeHelper.GetFallbackToolTipContent(posOffset)) {
					ToolTipPanel.Children.Add(element);
				}
			}

			if (ToolTipPanel.Children.Count > 0) {
				TextEditorToolTip.IsOpen = true;
				args.Handled = true;
			}
		}

		private void TextEditorMouseHoverStopped(object? sender, MouseEventArgs e) {
			TextEditorToolTip.IsOpen = false;
		}
		private void TextAreaTextEnteredTooptipClose(object? sender, TextCompositionEventArgs e) {
			TextEditorToolTip.IsOpen = false;
		}
		#endregion Tooltip

		#region Folding
		FoldingManager foldingManager;
		IFoldingStrategy? foldingStrategy;
		DispatcherTimer foldingUpdateTimer;
		FoldingBackgroundRenderer foldingRenderer;

		[MemberNotNull(nameof(foldingManager), nameof(foldingUpdateTimer), nameof(foldingRenderer))]
		void InitializeFoldingManager() {
			foldingManager = FoldingManager.Install(textEditor.TextArea);

			foldingStrategy = null; // new SharpSheetsFoldingStrategy(2); // This is not a good initialization. Set to null (no folding)

			foldingUpdateTimer = new DispatcherTimer {
				Interval = TimeSpan.FromSeconds(2)
			};
			foldingUpdateTimer.Tick += delegate { UpdateFoldings(); };
			foldingUpdateTimer.Start();

			foldingRenderer = new FoldingBackgroundRenderer(foldingManager);
			textEditor.TextArea.TextView.BackgroundRenderers.Add(foldingRenderer);
		}
		
		void UninstallFoldingManager() {
			foldingUpdateTimer.Stop();
			textEditor.TextArea.TextView.BackgroundRenderers.Remove(foldingRenderer);
			FoldingManager.Uninstall(foldingManager);
		}

		void UpdateFoldings() {
			if (foldingStrategy != null) {
				foldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
			}
		}
		#endregion Folding

		#region Parsing Manager
		public ParsingManager parsingManager;
		public IParsingState? parsingState;

		[MemberNotNull(nameof(parsingManager))]
		void InitializeParsingManager() {
			parsingManager = ParsingManager.Install(textEditor.TextArea);
			parsingState = null; // Initialize to null as we don't yet know what kind of document we're dealing with
			parsingManager.SetParsingState(null);

			parsingManager.ParseStarted += s => Dispatcher.Invoke(() => { ParseStart(); });
			parsingManager.ParseEnded += s => Dispatcher.Invoke(() => { ParseEnd(s); });

			//parsingManager.ParseEnded += s => Dispatcher.Invoke(() => { RedrawAfterParse(); });
			parsingManager.ResultLoaded += RedrawAfterParse;
			parsingManager.ResultLoaded += () => Dispatcher.Invoke(() => { UpdateDesignerOverlay(); }); // Dispatcher necessary?

			parsingManager.Start();
		}

		void UninstallParsingManager() {
			parsingManager.Stop();
			ParsingManager.Uninstall(parsingManager);
		}

		BackgroundMessage? currentParsingMessage;

		private void ParseStart() {
			currentParsingMessage = DisplayBackgroundMessage("Parsing...", ParsingMessagePriority, false);
		}

		private void ParseEnd(ParseState state) {
			if (state == ParseState.NONE || state == ParseState.SUCCESS || state == ParseState.TERMINATED) {
				FinishBackgroundMessage(currentParsingMessage);
			}
			else {
				DisplayBackgroundMessage("Parsing error", ParsingMessagePriority, true); // TODO Better message
			}
		}

		private void RedrawAfterParse() {
			//Console.WriteLine("Redrawing after parse.");
			textEditor.TextArea.TextView.Redraw();
		}

		private void EditorGotFocus(object? sender, RoutedEventArgs e) {
			parsingManager?.OptionalTrigger();
		}

		// TODO Feels janky that this is here, referencing the designer...
		void UpdateDesignerOverlay() {
			if (DesignerArea != null) {
				SharpDrawingException[]? errors = parsingManager?.GetParsingState()?.GetDrawingErrors().ToArray();
				if (errors != null && errors.Length > 0) {
					DesignerArea.OverlayText = string.Join("\n", errors.Select(e => e.Message).WhereNotEmpty());
				}
				else {
					DesignerArea.OverlayText = ""; // null
				}
			}
		}
		#endregion Parsing Manager

		#region Error Markers

		private ObservableCollection<ErrorPopupEntry> ErrorPopupEntries { get; } = new ObservableCollection<ErrorPopupEntry>();

		void InitializeErrorMarkers() {
			parsingManager.ParseStateChanged += UpdateErrorDisplay;
			SharpDataManager.Instance.WarnFontLicensingChanged += UpdateErrorDisplay;

			ErrorPopupListBox.ItemsSource = ErrorPopupEntries;
		}

		void UninstallErrorMarkers() {
			parsingManager.ParseStateChanged -= UpdateErrorDisplay;
			SharpDataManager.Instance.WarnFontLicensingChanged -= UpdateErrorDisplay;

			ErrorPopupListBox.ItemsSource = null;
		}

		void UpdateErrorDisplay(object? sender, EventArgs args) {
			IParsingState? parsingState = parsingManager.GetParsingState();
			ErrorComparer errorComparer = new ErrorComparer(parsingState);

			int numErrors = parsingState?.GetErrors().Distinct(errorComparer).Count() ?? 0; // parsingState?.ErrorCount ?? 0;
			ErrorStatus.Visibility = numErrors > 0 ? Visibility.Visible : Visibility.Collapsed;
			ErrorCount.Text = $"{numErrors} Error{(numErrors == 1 ? "" : "s")} Found";

			ErrorPopupEntries.Clear();
			if (parsingState is not null) {
				ErrorPopupEntries.AddRange(parsingState.GetErrors()
					.Distinct(errorComparer)
					.Select(e => {
						string message = e.Message;
						DocumentSpan? location = null;
						if (e is SharpParsingException spe && spe.Location is DocumentSpan parseLocation) {
							location = parseLocation;
						}
						else if (e is SharpDrawingException sde && parsingState.DrawingMapper?.GetDrawnObjectLocation(sde.Origin) is DocumentSpan drawLocation) {
							location = drawLocation;
						}
						return new ErrorPopupEntry(location, message);
					}) ?? Enumerable.Empty<ErrorPopupEntry>());
			}
		}

		private class ErrorComparer : IEqualityComparer<SharpSheetsException> {

			private readonly IParsingState? parsingState;

			public ErrorComparer(IParsingState? parsingState) {
				this.parsingState = parsingState;
			}

			public bool Equals(SharpSheetsException? x, SharpSheetsException? y) {
				if (x is null || y is null) { return x is null & y is null; }
				else if (x is SharpParsingException xp && y is SharpParsingException yp) {
					if(xp.Location.HasValue && yp.Location.HasValue) {
						return DocumentSpan.Equals(xp.Location, yp.Location) && xp.Message == yp.Message;
					}
					else {
						return xp.Message == yp.Message;
					}
				}
				else if (x is SharpDrawingException xd && y is SharpDrawingException yd
					&& parsingState is not null
					&& parsingState.DrawingMapper?.GetDrawnObjectLocation(xd.Origin) is DocumentSpan xl
					&& parsingState.DrawingMapper?.GetDrawnObjectLocation(yd.Origin) is DocumentSpan yl) {
					return DocumentSpan.Equals(xl, yl) && xd.Message == yd.Message;
				}
				else {
					return x.Equals(y);
				}
			}

			public int GetHashCode([DisallowNull] SharpSheetsException e) {
				if (e is SharpParsingException ep) {
					return HashCode.Combine(ep.Location, ep.Message);
				}
				else if (e is SharpDrawingException ed) {
					return HashCode.Combine(parsingState?.DrawingMapper?.GetDrawnObjectLocation(ed.Origin), ed.Message);
				}
				else {
					return e.GetHashCode();
				}
			}

		}

		public void ErrorStatusClick(object? sender, MouseButtonEventArgs e) {

			int[]? offsets = parsingManager.GetParsingState()?.ErrorOffsets;

			if (offsets is not null) {
				Array.Sort(offsets);
				int currentOffset = textEditor.TextArea.Caret.Offset;
				int nextOffset = currentOffset;
				//Console.WriteLine($"Current line: {currentLine}, next line: {nextLine}, lines = {string.Join(", ", lines.Enumerate().Select(kv => $"({kv.Key}) {kv.Value}"))}");
				//Console.WriteLine($"lines.Length > 0 && currentLine >= lines[lines.Length - 1] = {lines.Length > 0 && currentLine >= lines[lines.Length - 1]} = {lines.Length} > {0} && {currentLine} >= {lines[lines.Length - 1]} = {lines.Length > 0} && {currentLine >= lines[lines.Length - 1]}");
				if (offsets.Length > 0 && currentOffset >= offsets[^1]) {
					//Console.WriteLine("Beyond last line, returning to first");
					nextOffset = offsets[0];
				}
				else {
					//Console.WriteLine("Checking all lines");
					for (int i = 0; i < offsets.Length; i++) {
						if (offsets[i] > currentOffset) {
							nextOffset = offsets[i];
							break;
						}
					}
				}

				//Console.WriteLine($"Checking ({nextLine} =? {currentLine})");
				if (nextOffset != currentOffset) {
					//Console.WriteLine($"nextLine != currentLine ({nextLine} != {currentLine})");
					ScrollTo(nextOffset);
				}
			}
		}

		private void ErrorPopupClick(object sender, RoutedEventArgs e) {
			ErrorPopup.IsOpen = true;
		}

		private class ErrorPopupEntry {

			public string Line => Location?.Line.ToString() ?? "-";
			public DocumentSpan? Location { get; }
			public string Message { get; }

			public ErrorPopupEntry(DocumentSpan? location, string message) {
				Location = location;
				Message = message;
			}

		}

		private void ErrorPopupItemMouseDoubleClick(object sender, MouseButtonEventArgs e) {
			if (sender is ListBoxItem listItem && listItem.DataContext is ErrorPopupEntry errorData) {
				if(errorData.Location is DocumentSpan location && location.Offset > 0 && location.Offset < textEditor.Document.TextLength) {
					ScrollTo(location.Offset);
					this.Dispatcher.BeginInvoke(() => {
						if (FocusEditor()) {
							ErrorPopup.IsOpen = false;
						}
					});
				}
			}
		}

		private void ErrorPopupClosed(object sender, EventArgs e) {
			ErrorPopupToggleButton.IsChecked = false;
		}

		#endregion Error Markers

		#region Designer Area

		public DesignerFloating? DesignerFloating;

		[MemberNotNull(nameof(designerGenerator))]
		void InitializeDesigner() {
			InitializeDesignerGenerator();

			designerPanesOrientation = Orientation.Horizontal;

			splitterLength = MainGrid.ColumnDefinitions[1].Width;
			testBlockPreviousLength = MainGrid.ColumnDefinitions[2].Width;
			testBlockPreviousMinLength = MainGrid.ColumnDefinitions[2].MinWidth;

			DesignerHolder.Visibility = Visibility.Collapsed;
			Splitter.Visibility = Visibility.Collapsed;
			MainGrid.ColumnDefinitions[1].Width = new GridLength(0);
			MainGrid.ColumnDefinitions[2].Width = new GridLength(0);
			MainGrid.ColumnDefinitions[2].MinWidth = 0;

			/*
			TestBlock.Width = 1000;
			TestBlock.Height = 1000;
			Line line1 = new Line() { X1 = 0, Y1 = 0, X2 = TestBlock.Width, Y2 = TestBlock.Height, Stroke = Brushes.LightSteelBlue, StrokeThickness = 2 };
			Line line2 = new Line() { X1 = 0, Y1 = TestBlock.Height, X2 = TestBlock.Width, Y2 = 0, Stroke = Brushes.LightSteelBlue, StrokeThickness = 2 };
			TestBlock.Children.Add(line1);
			TestBlock.Children.Add(line2);
			*/

			this.IsVisibleChanged += CheckDesignerDrawn;
		}

		private void CheckDesignerDrawn(object? sender, DependencyPropertyChangedEventArgs e) {
			// This method exists to draw a document if we're in a state where one has been parsed but not drawn (which can happen if switching tabs quickly)
			if (designerGenerator.Document != null && DesignerArea.IsValidNewDocument(designerGenerator.Document)) {
				UpdateDesignerDocument(); // Is this even useful?
			}
			if(parsingState?.DrawableContent!=null && designerGenerator.Document == null) {
				designerGenerator.Redraw();
			}
		}

		void UninstallDesigner() {
			this.IsVisibleChanged -= CheckDesignerDrawn;

			if (DesignerFloating != null) {
				DesignerFloating.Close();
				DesignerFloating = null;
			}

			UninstallDesignerGenerator();
		}

		public void FloatDesigner() {
			if (DesignerFloating != null) {
				DesignerFloating.Activate();
			}
			else {
				DesignerFloating? floating = DesignerFloating.Open(this);
				if (floating != null) {
					bool needsRedraw = !IsDesignerVisible;

					IsDesignerVisible = false;
					floating.Show();
					floating.Activate();
					DesignerFloating = floating;

					if (needsRedraw) {
						designerGenerator?.Redraw();
					}
				}
			}
		}

		private Orientation designerPanesOrientation;
		GridLength testBlockPreviousLength;
		double testBlockPreviousMinLength;
		GridLength splitterLength;

		public bool IsDesignerVisible {
			get {
				return DesignerHolder.Visibility == Visibility.Visible;
			}
			set {
				if (value && DesignerHolder.Children.Count > 0) {
					if (DesignerHolder.Visibility == Visibility.Visible) { return; }

					DesignerHolder.Visibility = Visibility.Visible;
					Splitter.Visibility = Visibility.Visible;

					if (MainGrid.ColumnDefinitions.Count > 0) {
						MainGrid.ColumnDefinitions[1].Width = splitterLength;
						MainGrid.ColumnDefinitions[2].Width = testBlockPreviousLength;
						MainGrid.ColumnDefinitions[2].MinWidth = testBlockPreviousMinLength;
					}
					else {
						MainGrid.RowDefinitions[1].Height = splitterLength;
						MainGrid.RowDefinitions[2].Height = testBlockPreviousLength;
						MainGrid.RowDefinitions[2].MinHeight = testBlockPreviousMinLength;
					}

					designerGenerator?.Redraw();
				}
				else {
					if (DesignerHolder.Visibility != Visibility.Visible) { return; }

					DesignerHolder.Visibility = Visibility.Collapsed;
					Splitter.Visibility = Visibility.Collapsed;

					if (MainGrid.ColumnDefinitions.Count > 0) {
						testBlockPreviousLength = MainGrid.ColumnDefinitions[2].Width; // remember previous width
						MainGrid.ColumnDefinitions[1].Width = new GridLength(0);
						MainGrid.ColumnDefinitions[2].Width = new GridLength(0);
						MainGrid.ColumnDefinitions[2].MinWidth = 0;
					}
					else {
						testBlockPreviousLength = MainGrid.RowDefinitions[2].Height; // remember previous height
						MainGrid.RowDefinitions[1].Height = new GridLength(0);
						MainGrid.RowDefinitions[2].Height = new GridLength(0);
						MainGrid.RowDefinitions[2].MinHeight = 0;
					}
				}
			}
		}

		public Orientation DesignerPanesOrientation {
			get {
				return designerPanesOrientation;
			}
			set {
				designerPanesOrientation = value;
				if (designerPanesOrientation == Orientation.Vertical && MainGrid.ColumnDefinitions.Count > 0) {
					GridLength length0 = MainGrid.ColumnDefinitions[0].Width;
					GridLength length1 = MainGrid.ColumnDefinitions[1].Width;
					GridLength length2 = MainGrid.ColumnDefinitions[2].Width;

					MainGrid.RowDefinitions.Add(new RowDefinition() { Height = length0, MinHeight = MainGrid.ColumnDefinitions[0].MinWidth });
					MainGrid.RowDefinitions.Add(new RowDefinition() { Height = length1, MinHeight = MainGrid.ColumnDefinitions[1].MinWidth });
					MainGrid.RowDefinitions.Add(new RowDefinition() { Height = length2, MinHeight = MainGrid.ColumnDefinitions[2].MinWidth });
					MainGrid.ColumnDefinitions.Clear();

					MainGrid.Children[0].ClearValue(Grid.ColumnProperty);
					MainGrid.Children[1].ClearValue(Grid.ColumnProperty);
					MainGrid.Children[2].ClearValue(Grid.ColumnProperty);
					Grid.SetRow(MainGrid.Children[0], 0);
					Grid.SetRow(MainGrid.Children[1], 1);
					Grid.SetRow(MainGrid.Children[2], 2);

					Splitter.Height = Splitter.Width;
					Splitter.ClearValue(FrameworkElement.WidthProperty);
				}
				else if (designerPanesOrientation == Orientation.Horizontal && MainGrid.RowDefinitions.Count > 0) {
					GridLength length0 = MainGrid.RowDefinitions[0].Height;
					GridLength length1 = MainGrid.RowDefinitions[1].Height;
					GridLength length2 = MainGrid.RowDefinitions[2].Height;

					MainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = length0, MinWidth = MainGrid.RowDefinitions[0].MinHeight });
					MainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = length1, MinWidth = MainGrid.RowDefinitions[1].MinHeight });
					MainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = length2, MinWidth = MainGrid.RowDefinitions[2].MinHeight });
					MainGrid.RowDefinitions.Clear();

					MainGrid.Children[0].ClearValue(Grid.RowProperty);
					MainGrid.Children[1].ClearValue(Grid.RowProperty);
					MainGrid.Children[2].ClearValue(Grid.RowProperty);
					Grid.SetColumn(MainGrid.Children[0], 0);
					Grid.SetColumn(MainGrid.Children[1], 1);
					Grid.SetColumn(MainGrid.Children[2], 2);

					Splitter.Width = Splitter.Height;
					Splitter.ClearValue(FrameworkElement.HeightProperty);
				}
			}
		}

		public DesignerGenerator designerGenerator;

		[MemberNotNull(nameof(designerGenerator))]
		void InitializeDesignerGenerator() {
			designerGenerator = DesignerGenerator.Install(DesignerArea, parsingManager);
			//designerGenerator.Start();

			designerGenerator.NewContentAvailable += delegate { Dispatcher.Invoke(UpdateDesignerDocument); };
			designerGenerator.NewContentAvailable += delegate { Dispatcher.Invoke(() => UpdateErrorDisplay(null, EventArgs.Empty)); }; // To update drawing errors

			designerGenerator.GenerationStarted += s => Dispatcher.Invoke(() => { DesignerGenerateStart(); });
			designerGenerator.GenerationEnded += s => Dispatcher.Invoke(() => { DesignerGenerateEnd(s); });

			textEditor.TextArea.Caret.PositionChanged += UpdateDesignerDocumentHightlight;
			textEditor.TextArea.SelectionChanged += UpdateDesignerDocumentHightlight;
			DesignerArea.CanvasChanged += UpdateDesignerDocumentHightlight;
		}

		BackgroundMessage? currentBackgroundMessage;

		private void DesignerGenerateStart() {
			currentBackgroundMessage = DisplayBackgroundMessage("Drawing...", DesignerMessagePriority, false);
		}

		private void DesignerGenerateEnd(GenerationState state) {
			if (state == GenerationState.NONE || state == GenerationState.SUCCESS || state == GenerationState.TERMINATED) {
				FinishBackgroundMessage(currentBackgroundMessage);
			}
			else {
				DisplayBackgroundMessage("Drawing error", DesignerMessagePriority, true); // TODO Better message
			}
		}

		void UninstallDesignerGenerator() {
			DesignerArea.CanvasChanged -= UpdateDesignerDocumentHightlight;
			textEditor.TextArea.Caret.PositionChanged -= UpdateDesignerDocumentHightlight;
			textEditor.TextArea.SelectionChanged -= UpdateDesignerDocumentHightlight;

			//designerGenerator.Stop();
			DesignerGenerator.Uninstall(designerGenerator);
		}

		void UpdateDesignerDocumentHightlight(object? sender, EventArgs args) {
			if (!textEditor.TextArea.Selection.IsMultiline) {
				object[]? currentDrawnObjects = designerGenerator?.DrawingMapper?.GetDrawnObjects(textEditor.TextArea.Caret.Offset)?.ToArray();

				if (currentDrawnObjects != null && currentDrawnObjects.Length > 0) {
					//Console.WriteLine($"Areas: {string.Join(", ", currentDrawnObjects.Select(d => d.GetType().Name))}");
					DesignerArea.Highlight(currentDrawnObjects);
				}
				else {
					DesignerArea.Highlight(null);
				}
			}
		}

		void UpdateDesignerDocument() {
			if (DesignerArea != null) {
				if (designerGenerator.Document != null) {
					DesignerArea.SetDesignerDocument(designerGenerator.Document);
					UpdateDesignerDocumentHightlight(null, new EventArgs());
				}
				else if (designerGenerator.Document == null && parsingManager.GetParsingState() is IParsingState) {
					// Something went wrong, as we have a parsing state but no document
					// Display a special warning?
				}
			}
		}

		private void OnCanvasDoubleClick(object? sender, Dictionary<object, RegisteredAreas> areas) {
			IDrawingMapper? drawingMapper = designerGenerator?.DrawingMapper;
			if (drawingMapper != null && areas.Count > 0) {
				//Console.WriteLine("Areas: " + string.Join(", ", areas.Select(r => (r.Key.GetType().Name ?? "NULL") + $" ({r.Value})")));
				//Console.WriteLine("Position: " + pos);
				//Console.WriteLine($"Double click: {string.Join("; ", areas.Select(kv => drawingMapper.GetDrawnObjectLocation(kv.Key)).WhereNotNull().OrderBy(l => l.Line).Select(l => $"line {l.Line} offset {l.Offset}"))}");
				//Console.WriteLine($"Double click: {string.Join("; ", areas.OrderBy(kv => kv.Value.Total.Area).ThenByDescending(kv => drawingMapper.GetDrawnObjectDepth(kv.Key) ?? -1).Select(l => $"{l.Key.GetType()}"))}");
				//Console.WriteLine($"Double click: {string.Join("; ", areas.OrderBy(kv => kv.Value.Total.Area).ThenByDescending(kv => drawingMapper.GetDrawnObjectDepth(kv.Key) ?? -1).Select(kv => drawingMapper.GetDrawnObjectLocation(kv.Key)).Select(l => $"{l?.ToString() ?? "NULL"}"))}");
				if (areas.OrderBy(kv => kv.Value.Total.Area).ThenByDescending(kv => drawingMapper.GetDrawnObjectDepth(kv.Key) ?? -1).Select(kv => drawingMapper.GetDrawnObjectLocation(kv.Key)).WhereNotNull().Where(l => l.Line >= 0).FirstOrDefault() is DocumentSpan location) {
					//if (areas.OrderByDescending(kv => drawingMapper.GetDrawnObjectDepth(kv.Key) ?? -1).Select(kv => drawingMapper.GetDrawnObjectLocation(kv.Key)).WhereNotNull().Where(s => s.Line >= 0).FirstOrDefault() is DocumentSpan location) {

					if (location.Line + 1 > 0 && location.Line + 1 <= textEditor.Document.LineCount) {
						DocumentLine line = textEditor.Document.GetLineByNumber(location.Line + 1);
						textEditor.ScrollToLine(line.LineNumber);
						textEditor.CaretOffset = TextUtilities.GetWhitespaceAfter(textEditor.Document, line.Offset).EndOffset;

						RequestFocus?.Invoke(sender, this);
						//textEditor.TextArea.Focus();
						//Keyboard.Focus(textEditor.TextArea);
						FocusEditor();
					}
				}
			}
		}

		private void OnCanvasMouseMove(object? sender, Dictionary<object, RegisteredAreas> areas) {
			IDrawingMapper? drawingMapper = designerGenerator?.DrawingMapper;
			if (drawingMapper != null) {
				if (areas.Count > 0) {
					object[] drawnObjects = areas.Where(kv => drawingMapper.GetDrawnObjectDepth(kv.Key).HasValue)
						.OrderBy(kv => kv.Value.Total.Area)
						.ThenByDescending(kv => drawingMapper.GetDrawnObjectDepth(kv.Key) ?? -1)
						.Select(kv => kv.Key)
						.FirstOrDefault() is object key ? key.Yield().ToArray() : Array.Empty<object>();

					DesignerArea.HighlightMinor(drawnObjects);
				}
				else {
					DesignerArea.HighlightMinor(null);
				}
			}
		}

		#endregion Designer Area

	}

	/*
	public class ZoomFontSizeTransformer : DocumentColorizingTransformer {
		readonly SharpDocumentEditor documentEditor;

		public ZoomFontSizeTransformer(SharpDocumentEditor documentEditor) {
			this.documentEditor = documentEditor;
		}

		protected override void ColorizeLine(DocumentLine line) {
			foreach (VisualLineElement elem in CurrentContext.VisualLine.Elements) {
				ChangeVisualElements(
					elem.VisualColumn, // startOffset
					elem.VisualColumn + elem.VisualLength, // endOffset
					(VisualLineElement element) => {
						double origRend = element.TextRunProperties.FontRenderingEmSize;
						double origHint = element.TextRunProperties.FontHintingEmSize;
						element.TextRunProperties.SetFontRenderingEmSize(origRend * documentEditor.CurrentTextZoom);
						element.TextRunProperties.SetFontHintingEmSize(origHint * documentEditor.CurrentTextZoom);
					});
			}

		}
	}
	*/

}