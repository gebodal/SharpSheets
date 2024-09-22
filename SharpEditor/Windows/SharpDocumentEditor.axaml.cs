using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using CommunityToolkit.Mvvm.Input;
using SharpEditor.CodeHelpers;
using SharpEditor.Completion;
using SharpEditor.DataManagers;
using SharpEditor.Designer;
using SharpEditor.Folding;
using SharpEditor.Indentation;
using SharpEditor.Parsing;
using SharpEditor.Parsing.ParsingState;
using SharpEditor.Utilities;
using SharpSheets.Exceptions;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace SharpEditor.Windows {

	public class UpdateHeaderEventArgs : EventArgs {
		public string Header { get; }
		public bool UnsavedProgress { get; }
		public UpdateHeaderEventArgs(string header, bool unsavedProgress) {
			this.Header = header;
			this.UnsavedProgress = unsavedProgress;
		}
	}

	public class DocumentEditorEventArgs : EventArgs {
		public SharpDocumentEditor Editor { get; }
		public DocumentEditorEventArgs(SharpDocumentEditor editor) {
			this.Editor = editor;
		}
	}

	public partial class SharpDocumentEditor : UserControl {

		public event EventHandler<UpdateHeaderEventArgs>? UpdateHeader;
		public event EventHandler<DocumentEditorEventArgs>? DocumentTypeChanged;
		public event EventHandler<DocumentEditorEventArgs>? RequestFocus;

		public string? Header { get; private set; }

		public string Text { get { return textEditor.Document.CreateSnapshot().Text; } } // Threadsafe too much?
		public int TextLength { get { return textEditor.Document.TextLength; } }

		private bool intentional = true;
		public bool IsDisposable { get { return !(intentional || TextLength > 0 || CurrentFilePath != null || HasUnsavedProgress); } }

		// Search panel stuff goes here

		public SharpDocumentEditor(string? filename) {
			InitialiseCommands();
			InitializeComponent();
			
			this.DataContext = this;

			// Search panel stuff here

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
			InitializeContextMenu();

			// TODO Pasting event handling goes here (not yet implemented)
			//DataObject.AddPastingHandler(this, PasteEvent);

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

		public SharpDocumentEditor() : this(null) {
			this.intentional = false;
			SetDocumentType(true, DocumentType.UNKNOWN);
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
			UninstallContextMenu();
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

		[MemberNotNull(nameof(lineNumberControls))]
		void InitializeTextEditorDependencyProperties() {

			textEditor.Options.HighlightCurrentLine = true;
			textEditor.Options.EnableEmailHyperlinks = textEditor.Options.EnableHyperlinks = false;
			textEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
			textEditor.Options.AllowScrollBelowDocument = true;

			textEditor.TextArea.SelectionCornerRadius = 1.0; // Can also adjust Selection Brush from here
			textEditor.TextArea.SelectionBorder = null;
			textEditor.TextArea.SelectionForeground = null;
			//textEditor.TextArea.SelectionBrush = new SolidColorBrush(Colors.LightSlateGray) { Opacity = 0.5 };

			//textEditor.TextArea.TextView.CurrentLineBackground = Brushes.Transparent; // new SolidColorBrush(Colors.LightSlateGray) { Opacity = 0.0 };
			//textEditor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Colors.LightSlateGray) { Opacity = 0.3 }, 2f);

			//textEditor.SnapsToDevicePixels = true;
			//textEditor.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

			textEditor.Options.ShowSpacesGlyph = "\u2022";

			// Line numbers
			textEditor.ShowLineNumbers = true;
			List<Control> lineNumberControlsList = new List<Control>();
			ObservableCollection<Control> leftMargins = textEditor.TextArea.LeftMargins;
			var decorationBrush = Resources.GetResourceObservable(SharpEditorThemeManager.EditorDecorationBrush);
			for (int i = 0; i < leftMargins.Count; i++) {
				if (leftMargins[i] is LineNumberMargin lineNumberMargin) {
					lineNumberControlsList.Add(lineNumberMargin);
					lineNumberMargin.Bind(TextBlock.ForegroundProperty, decorationBrush);
					if (i + 1 < leftMargins.Count && DottedLineMargin.IsDottedLineMargin(leftMargins[i + 1])) {
						lineNumberControlsList.Add(leftMargins[i + 1]);
						leftMargins[i + 1].Bind(Avalonia.Controls.Shapes.Line.StrokeProperty, decorationBrush);
					}
					break;
				}
			}
			lineNumberControls = lineNumberControlsList.ToArray();
			//ShowLineNumbers = SharpDataManager.Instance.ShowLineNumbers;
			this.textEditor.Loaded += OnLoadedAdjustShowLineNumbers;

			SharpDataManager.Instance.ShowLineNumbersChanged += OnShowLineNumbersChanged;

			// Word wrap
			textEditor.WordWrap = SharpDataManager.Instance.WrapLines;
			SharpDataManager.Instance.WrapLinesChanged += OnWrapLinesChanged;

			// Show end of line
			textEditor.Options.ShowEndOfLine = SharpDataManager.Instance.ShowEndOfLine;
			SharpDataManager.Instance.ShowEndOfLineChanged += OnShowEndOfLineChanged;

			// Show whitespace
			textEditor.Options.ShowSpaces = textEditor.Options.ShowTabs = SharpDataManager.Instance.ShowWhitespace;
			SharpDataManager.Instance.ShowWhitespaceChanged += OnShowWhitespaceChanged;

			// Tab width
			textEditor.Options.IndentationSize = SharpDataManager.Instance.TabWidth;
			SharpDataManager.Instance.TabWidthChanged += OnTabWidthChanged;

			// NonPrintableCharacterBrush
			if (SharpEditorWindow.Instance?.controller?.appInstance is App currentApp) {
				textEditor.TextArea.TextView.NonPrintableCharacterBrush = SharpEditorThemeManager.GetBrush(currentApp, SharpEditorThemeManager.EditorTextMarkerBrush) ?? Brushes.Red;
			}
		}

		void UninstallTextEditorDependencyProperties() {
			SharpDataManager.Instance.ShowLineNumbersChanged -= OnShowLineNumbersChanged;
			SharpDataManager.Instance.WrapLinesChanged -= OnWrapLinesChanged;
			SharpDataManager.Instance.ShowEndOfLineChanged -= OnShowEndOfLineChanged;
		}

		private Control[] lineNumberControls;
		private bool ShowLineNumbers {
			get {
				return lineNumberControls.Any(c => c.IsVisible);
			}
			set {
				foreach(Control c in lineNumberControls) {
					c.IsVisible = value;
				}
			}
		}

		private void OnLoadedAdjustShowLineNumbers(object? sender, RoutedEventArgs e) {
			ShowLineNumbers = SharpDataManager.Instance.ShowLineNumbers;
		}

		private void OnShowLineNumbersChanged(object? sender, EventArgs e) {
			this.ShowLineNumbers = SharpDataManager.Instance.ShowLineNumbers;
		}

		private void OnWrapLinesChanged(object? sender, EventArgs e) {
			this.textEditor.WordWrap = SharpDataManager.Instance.WrapLines;
		}

		private void OnShowEndOfLineChanged(object? sender, EventArgs e) {
			this.textEditor.Options.ShowEndOfLine = SharpDataManager.Instance.ShowEndOfLine;
		}

		private void OnShowWhitespaceChanged(object? sender, EventArgs e) {
			this.textEditor.Options.ShowSpaces = this.textEditor.Options.ShowTabs = SharpDataManager.Instance.ShowWhitespace;
		}

		private void OnTabWidthChanged(object? sender, EventArgs e) {
			this.textEditor.Options.IndentationSize = SharpDataManager.Instance.TabWidth;
		}

		#endregion TextEditor Dependency Properties

		#region Track User Changes
		public bool HasUnsavedProgress { get; private set; }

		private void UpdateUnsavedProgress(object? sender, EventArgs e) {
			bool previousState = HasUnsavedProgress;
			HasUnsavedProgress = !textEditor.Document.UndoStack.IsOriginalFile; // Seems to be working now...
			if (HasUnsavedProgress != previousState) { OnUpdateHeader(); }
		}

		void InitializeUnsavedTracker() {
			textEditor.Document.UpdateFinished += UpdateUnsavedProgress;
		}

		void UninstallUnsavedTracker() {
			textEditor.Document.UpdateFinished -= UpdateUnsavedProgress;
		}

		private void Caret_PositionChanged(object? sender, EventArgs e) {
			int line = textEditor.TextArea.Caret.Line;
			int column = textEditor.TextArea.Caret.Column;
			CursorLineText.Text = "Ln " + line;
			CursorColText.Text = "Col " + column;
			textEditor.TextArea.TextView.Redraw(); // Still needed?
		}
		#endregion Track User Changes

		#region Text Zoom

		private void InitializeTextZoom() {
			baseFontSize = textEditor.FontSize;
			SetTextZoom(SharpDataManager.Instance.TextZoom);
			SharpDataManager.Instance.TextZoomChanged += OnTextZoomChanged;

			textEditor.AddHandler(InputElement.PointerWheelChangedEvent, OnTextPreviewMouseWheel, RoutingStrategies.Tunnel, false);
		}

		private void UninstallTextZoom() {
			SharpDataManager.Instance.TextZoomChanged -= OnTextZoomChanged;

			textEditor.RemoveHandler(InputElement.PointerWheelChangedEvent, OnTextPreviewMouseWheel);
		}

		private void OnTextZoomChanged(object? sender, EventArgs e) {
			SetTextZoom(SharpDataManager.Instance.TextZoom);
		}

		// React to ctrl + mouse wheel to zoom in and out
		private void OnTextPreviewMouseWheel(object? sender, PointerWheelEventArgs e) {
			if (e.KeyModifiers == KeyModifiers.Control) {
				this.StepTextZoom(e.Delta.Y > 0);
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

			textEditor.FontSize = baseFontSize * zoom; // baseFontSize * SharpDataManager.Instance.TextZoom
			textEditor.TextArea.FontSize = baseFontSize * zoom;

			foreach (LineNumberMargin margin in textEditor.TextArea.LeftMargins.OfType<LineNumberMargin>()) {
				margin.SetValue(TextBlock.FontSizeProperty, textEditor.FontSize);
			}

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

		/*
		readonly Regex percentageRegex = new Regex("[0-9%\\.]+");
		private void ValidateTextZoomBox(object? sender, TextCompositionEventArgs e) {
			e.Handled = !percentageRegex.IsMatch(e.Text);
		}
		*/

		private void TextZoomBoxLostFocus(object? sender, RoutedEventArgs e) {
			Match match = Regex.Match(TextZoomTextBox.Text?.Trim() ?? "", "^(?<number>[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)%?$");

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

		private void TextZoomBoxMouseWheel(object? sender, PointerWheelEventArgs e) {
			this.StepTextZoom(e.Delta.Y > 0);
			e.Handled = true;
		}
		#endregion Text Zoom

		#region Background Messages
		public static readonly int LowestMessagePriority = int.MinValue;
		public static readonly int ParsingMessagePriority = 100;
		public static readonly int DrawingMessagePriority = 50;
		public static readonly int ParsedMessagePriority = 10;
		public static readonly int DrawnMessagePriority = 10; // 5

		public static readonly int ErrorMessageDuration = 5;
		public static readonly int InfoMessageDuration = 3;
		public static readonly int AwaitingMessageDuration = 120;

		void InitializeBackgroundMessages() {
			BackgroundMessageControl.Start();
		}

		void UninstallBackgroundMessages() {
			BackgroundMessageControl.Stop();
		}

		private ToolbarMessage DisplayBackgroundMessage(string message, int priority, int duration) {
			return BackgroundMessageControl.LogMessage(message, duration, priority);
		}

		private bool FinishBackgroundMessage(ToolbarMessage? message) {
			return BackgroundMessageControl.RemoveMessage(message);
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
			if (forceUpdate || newDocumentType != CurrentDocumentType) {
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

				SetParsingState(parsingState);

				UpdateFoldings();
				UpdateErrorDisplay(null, EventArgs.Empty);

				ResetDesigner();

				CurrentDocumentType = newDocumentType;

				DocumentTypeChanged?.Invoke(this, new DocumentEditorEventArgs(this));
				OnUpdateHeader();
			}
		}

		private void ReloadHighlighting() {
			textEditor.SyntaxHighlighting = CurrentDocumentType switch {
				DocumentType.SHARPCONFIG => SharpEditorPalette.CharacterSheetHighlighting,
				DocumentType.CARDCONFIG => SharpEditorPalette.CardConfigHighlighting,
				DocumentType.CARDSUBJECT => SharpEditorPalette.CardSubjectHighlighting,
				DocumentType.MARKUP => SharpEditorPalette.BoxMarkupHighlighting,
				_ => null
			};
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

			textEditor.Document.UndoStack.ClearAll();
			textEditor.Document.UndoStack.MarkAsOriginalFile();

			OnUpdateHeader();
		}

		public void Open(string filename) {
			Console.WriteLine($"Open {filename}");

			//textEditor.Load(filename); // This will attempt to auto-detect encoding
			using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				textEditor.Load(fs); // This will attempt to auto-detect encoding
			}

			textEditor.Encoding = System.Text.Encoding.UTF8; // Now force this document to be considered UTF8

			CurrentFilePath = filename;

			textEditor.TextArea.Caret.Offset = 0;
			textEditor.ScrollToHome();

			UpdateDocumentType(true);

			HasUnsavedProgress = false;
			CurrentFieldSourcePath = null;

			textEditor.Document.UndoStack.ClearAll();
			textEditor.Document.UndoStack.MarkAsOriginalFile();

			OnUpdateHeader();
		}

		public bool Save(string filename) {
			if (CurrentFilePath != filename) {
				CurrentGeneratePath = null;
			}

			using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Write)) {
				textEditor.Save(fs); // We have manually set document to UTF8, so it will be saved as such
			}
			CurrentFilePath = filename;

			UpdateDocumentType(false); // Check that we haven't saved with a different extension

			HasUnsavedProgress = false;

			textEditor.Document.UndoStack.MarkAsOriginalFile();

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

		// TODO Past event handling?
		/*
		private void PasteEvent(object sender, DataObjectPastingEventArgs args) {
			codeHelper?.TextPasted(args, textEditor.CaretOffset);
		}
		*/

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
			if (codeHelper != null && completionWindow is null) {
				IList<ICompletionData> data = codeHelper.GetCompletionData();

				if (data.Count > 0) {
					int startOffset = codeHelper.GetCompletionStartOffset();
					//Console.WriteLine($"Caret: {textEditor.CaretOffset}, Start: {startOffset}");
					completionWindow = new SharpCompletionWindow(textEditor.TextArea) { StartOffset = startOffset };
					completionWindow.CompletionList.CompletionData.AddRange(data);

					string startingText = textEditor.Document.GetText(completionWindow.StartOffset, completionWindow.EndOffset - completionWindow.StartOffset);

					completionWindow.Show();
					completionWindow.Closed += delegate {
						completionWindow = null;
					};

					if (!string.IsNullOrEmpty(startingText)) {
						completionWindow.CompletionList.SelectItem(startingText);
					}
				}
			}
		}

		void TextAreaTextEntered(object? sender, TextInputEventArgs e) {
			if (codeHelper?.TextEnteredTriggerCompletion(e) ?? false) {
				OpenCompletionWindow();
			}
			else {
				codeHelper?.TextEntered(e);
			}
		}

		void TextAreaTextEntering(object? sender, TextInputEventArgs e) {
			if(e.Text is null) { return; }
			/*
			if (codeHelper?.TextEnteringTriggerCompletion(e) ?? false) {
				OpenCompletionWindow();
				// codeHelper method should be calling e.Handled if needed
			}
			else */
			if (completionWindow != null && e.Text.Length > 0) {
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
			if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control) {
				Console.WriteLine("Fire Ctrl+Space");
				OpenCompletionWindow();
				e.Handled = true; // Don't actually enter this character
			}
		}
		#endregion Completion Window

		#region Text Context Menu

		private ContextMenu contextMenu;

		[MemberNotNull(nameof(contextMenu))]
		void InitializeContextMenu() {
			contextMenu = new ContextMenu();
			textEditor.ContextMenu = contextMenu;
			//contextMenu.Style = (Style)Resources["ContextMenuStyle"];

			textEditor.TextArea.PointerPressed += TextArea_PointerPressed;
			contextMenu.Opening += ContextMenu_Opening;
		}

		void UninstallContextMenu() {
			contextMenu.Opening -= ContextMenu_Opening;
			textEditor.TextArea.PointerPressed -= TextArea_PointerPressed;
		}

		private void TextArea_PointerPressed(object? sender, PointerPressedEventArgs e) {
			PointerPoint point = e.GetCurrentPoint(textEditor);

			if (point.Properties.IsRightButtonPressed) {
				TextViewPosition? pos = textEditor.GetPositionFromPoint(e.GetPosition(textEditor));

				if (pos.HasValue) {
					// Move cursor to clicked location
					int offset = textEditor.Document.GetOffset(pos.Value);
					textEditor.CaretOffset = offset;
				}
			}
		}

		private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e) {

			contextMenu.Items.Clear();

			contextMenu.Items.Add(new MenuItem {
				Header = "Cut",
				Command = SharpEditorWindow.Instance?.CutCommand,
				//Icon = (System.Windows.Controls.Image)Resources["CutIcon"]
				//Icon = new Image() { Source = new Bitmap(AssetLoader.Open(new Uri("avares://SharpEditor/Images/Cut3.png"))) }
				Icon = new ContentControl() { Classes = { "cutIcon" } }
			});
			contextMenu.Items.Add(new MenuItem {
				Header = "Copy",
				Command = SharpEditorWindow.Instance?.CopyCommand,
				//Icon = (System.Windows.Controls.Image)Resources["CopyIcon"]
				//Icon = new Image() { Source = new Bitmap(AssetLoader.Open(new Uri("avares://SharpEditor/Images/Copy2.png"))) }
				Icon = new ContentControl() { Classes = { "copyIcon" } }
			});
			contextMenu.Items.Add(new MenuItem {
				Header = "Paste",
				Command = SharpEditorWindow.Instance?.PasteCommand,
				//Icon = (System.Windows.Controls.Image)Resources["PasteIcon"]
				//Icon = new Image() { Source = new Bitmap(AssetLoader.Open(new Uri("avares://SharpEditor/Images/Paste2.png"))) }
				Icon = new ContentControl() { Classes = { "pasteIcon" } }
			});

			// Determine possible actions based on what we clicked on (probably from CodeHelper)

			int offset = textEditor.CaretOffset;

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

				string? mouseWord = textEditor.Document.GetSharpTermStringFromOffset(offset);
				if (codeHelper.GetContextMenuItems(offset, mouseWord) is IList<Control> codeHelperMenuItems && codeHelperMenuItems.Count > 0) {
					contextMenu.Items.Add(new Separator());
					foreach (Control codeHelperMenuItem in codeHelperMenuItems) {
						contextMenu.Items.Add(codeHelperMenuItem);
					}
				}
			}
		}

		#endregion Text Context Menu

		#region Tooltip
		private ToolTip currentTooltip;

		[MemberNotNull(nameof(currentTooltip))]
		void InitializeToolTip() {
			currentTooltip = TextEditorToolTip;
			ToolTip.SetTip(textEditor, null); // This is a bit hacky, but it works for now

			textEditor.PointerHover += TextEditorMouseHover;
			textEditor.PointerHoverStopped += TextEditorMouseHoverStopped;
			textEditor.PointerMoved += TextEditorPointerMoved;
			textEditor.TextArea.TextEntered += TextAreaTextEnteredTooptipClose;
		}

		void UninstallToolTip() {
			textEditor.PointerHover -= TextEditorMouseHover;
			textEditor.PointerHoverStopped -= TextEditorMouseHoverStopped;
			textEditor.PointerMoved -= TextEditorPointerMoved;
			textEditor.TextArea.TextEntered -= TextAreaTextEnteredTooptipClose;
		}

		private Avalonia.Point? toolTipOpenPos = null;
		private readonly double PointerMoveEndToolTipDist = 30.0;

		private void OpenToolTip(Avalonia.Point? position) {
			toolTipOpenPos = position;

			//TextEditorToolTip.IsOpen = true;
			ToolTip.SetTip(textEditor, currentTooltip);
			ToolTip.SetIsOpen(textEditor, true);
		}

		private void CloseToolTip() {
			toolTipOpenPos = null;

			//TextEditorToolTip.IsOpen = false;
			ToolTip.SetIsOpen(textEditor, false);
			ToolTip.SetTip(textEditor, null);
		}
		
		private void TextEditorPointerMoved(object? sender, PointerEventArgs e) {
			if (ToolTip.GetIsOpen(textEditor) && toolTipOpenPos.HasValue) {
				Avalonia.Point pointerPos = e.GetPosition(textEditor);

				if(Avalonia.Point.Distance(toolTipOpenPos.Value, pointerPos) > PointerMoveEndToolTipDist) {
					CloseToolTip();
				}
			}
		}

		void TextEditorMouseHover(object? sender, PointerEventArgs args) {
			//ToolTip.SetIsOpen(textEditor, false);
			//TextEditorToolTip.IsVisible = false;
			CloseToolTip();

			Avalonia.Point pointerPos = args.GetPosition(textEditor);
			TextViewPosition? pos = textEditor.GetPositionFromPoint(pointerPos);
			if (pos == null) return;
			string? mouseWord = textEditor.Document.GetSharpTermStringFromViewPosition(pos.Value);
			int posOffset = textEditor.Document.GetOffset(pos.Value);
			if (mouseWord is not null) {

				ToolTipPanel.Children.Clear();

				if (codeHelper != null) {
					foreach (Control element in codeHelper.GetToolTipContent(posOffset, mouseWord)) {
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
			else if (codeHelper != null) { // No mouse-over word
				ToolTipPanel.Children.Clear();

				foreach (Control element in codeHelper.GetFallbackToolTipContent(posOffset)) {
					ToolTipPanel.Children.Add(element);
				}
			}

			if (ToolTipPanel.Children.Count > 0) {
				OpenToolTip(pointerPos);
				args.Handled = true;
			}
			else {
				CloseToolTip();
			}
		}

		private void TextEditorMouseHoverStopped(object? sender, PointerEventArgs e) {
			CloseToolTip();
			//Console.WriteLine("Hover stopped");
		}
		private void TextAreaTextEnteredTooptipClose(object? sender, TextInputEventArgs e) {
			CloseToolTip();
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

			IBrush decorationBrush = Application.Current?.GetResource<IBrush>(SharpEditorThemeManager.EditorDecorationBrush) ?? throw new InvalidOperationException();
			Pen foldIndicatorPen = new Pen(decorationBrush, 1) {
				DashStyle = new DashStyle(new double[] { 3, 7 }, 0)
			};

			foldingRenderer = new FoldingBackgroundRenderer(foldingManager, foldIndicatorPen);
			textEditor.TextArea.TextView.BackgroundRenderers.Add(foldingRenderer);
		}

		void UninstallFoldingManager() {
			foldingUpdateTimer.Stop();
			textEditor.TextArea.TextView.BackgroundRenderers.Remove(foldingRenderer);
			FoldingManager.Uninstall(foldingManager);
		}

		void UpdateFoldings() {
			foldingStrategy?.UpdateFoldings(foldingManager, textEditor.Document);
		}
		#endregion Folding

		#region Parsing Manager
		public ParsingManager parsingManager;
		public IParsingState? parsingState;

		public static readonly DirectProperty<SharpDocumentEditor, ParseState> LastParseStateProperty =
			AvaloniaProperty.RegisterDirect<SharpDocumentEditor, ParseState>(
				nameof(LastParseState),
				o => o.LastParseState);

		private ParseState _lastParseState;
		public ParseState LastParseState {
			get { return _lastParseState; }
			private set { SetAndRaise(LastParseStateProperty, ref _lastParseState, value); }
		}

		[MemberNotNull(nameof(parsingManager))]
		void InitializeParsingManager() {
			parsingManager = ParsingManager.Install(textEditor.TextArea);
			parsingState = null; // Initialize to null as we don't yet know what kind of document we're dealing with
			parsingManager.SetParsingState(null);

			parsingManager.ParseStarted += s => Dispatcher.UIThread.Invoke(() => { ParseStart(); });
			parsingManager.ParseEnded += s => Dispatcher.UIThread.Invoke(() => { ParseEnd(s); });

			//parsingManager.ParseEnded += s => Dispatcher.Invoke(() => { RedrawAfterParse(); });
			parsingManager.ResultLoaded += RedrawAfterParse;

			SharpEditorPalette.HighlightColorChanged += delegate { this.ReloadHighlighting(); };

			parsingManager.Start();
		}

		void UninstallParsingManager() {
			parsingManager.Stop();
			ParsingManager.Uninstall(parsingManager);
		}

		private void SetParsingState(IParsingState? newParsingState) {
			parsingManager.SetParsingState(newParsingState);
			LastParseState = ParseState.NONE;
		}

		ToolbarMessage? currentParsingMessage;

		private void ParseStart() {
			currentParsingMessage = DisplayBackgroundMessage("Parsing...", ParsingMessagePriority, AwaitingMessageDuration);
		}

		private void ParseEnd(ParseState state) {
			FinishBackgroundMessage(currentParsingMessage);
			if (state == ParseState.NONE || state == ParseState.SUCCESS || state == ParseState.TERMINATED) {
				DisplayBackgroundMessage("Parsing complete", ParsedMessagePriority, InfoMessageDuration);
			}
			else {
				DisplayBackgroundMessage("Parsing error", ParsingMessagePriority, ErrorMessageDuration); // TODO Better message
			}
			LastParseState = state;
		}

		private void RedrawAfterParse() {
			//Console.WriteLine("Redrawing after parse.");
			textEditor.TextArea.TextView.Redraw();
		}

		private void EditorGotFocus(object? sender, RoutedEventArgs e) {
			parsingManager?.OptionalTrigger();
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
			ErrorStatus.IsVisible = numErrors > 0;
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
						return new ErrorPopupEntry(location, message, ErrorPopupItemMouseDoubleClick);
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
					if (xp.Location.HasValue && yp.Location.HasValue) {
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

		public void ErrorStatusClick(object? sender, PointerPressedEventArgs e) {
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

		private void ItemTappedEvent(object? sender, TappedEventArgs e) {
			Console.WriteLine("Tapped");
		}

		private void ErrorPopupItemMouseDoubleClick(ErrorPopupEntry errorData) {
			if (errorData.Location is DocumentSpan location && location.Offset > 0 && location.Offset < textEditor.Document.TextLength) {
				ScrollTo(location.Offset);
				Dispatcher.UIThread.Invoke(() => { // InvokeAsync?
					if (FocusEditor()) {
						ErrorPopup.IsOpen = false;
					}
				});
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

			DesignerHolder.IsVisible = false;
			Splitter.IsVisible = false;
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

			//this.IsVisibleChanged += CheckDesignerDrawn;
			this.LayoutUpdated += CheckDesignerDrawn;
		}

		private void ResetDesigner() {
			DesignerArea.Reset();
			designerGenerator.Reset();
			//ResetDesignerDocument();
		}

		private void CheckDesignerDrawn(object? sender, EventArgs e) {
			// This method exists to draw a document if we're in a state where one has been parsed but not drawn (which can happen if switching tabs quickly)
			if (designerGenerator.Document != null && DesignerArea.IsValidNewDocument(designerGenerator.Document)) {
				UpdateDesignerDocument(); // Is this even useful?
			}
			if (parsingState?.DrawableContent != null && designerGenerator.Document == null) {
				designerGenerator.Redraw();
			}
		}

		void UninstallDesigner() {
			//this.IsVisibleChanged -= CheckDesignerDrawn;
			this.LayoutUpdated -= CheckDesignerDrawn;

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
				return DesignerHolder.IsVisible;
			}
			set {
				if (value && DesignerHolder.Children.Count > 0) {
					if (DesignerHolder.IsVisible) { return; }

					DesignerHolder.IsVisible = true;
					Splitter.IsVisible = true;

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
					if (!DesignerHolder.IsVisible) { return; }

					DesignerHolder.IsVisible = false;
					Splitter.IsVisible = false;

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
					Splitter.ClearValue(Layoutable.WidthProperty);
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
					Splitter.ClearValue(Layoutable.HeightProperty);
				}
			}
		}

		public DesignerGenerator designerGenerator;

		[MemberNotNull(nameof(designerGenerator))]
		void InitializeDesignerGenerator() {
			designerGenerator = DesignerGenerator.Install(DesignerArea, parsingManager);
			//designerGenerator.Start();

			designerGenerator.NewContentAvailable += delegate { Dispatcher.UIThread.Invoke(UpdateDesignerDocument); };
			designerGenerator.NewContentAvailable += delegate { Dispatcher.UIThread.Invoke(() => UpdateErrorDisplay(null, EventArgs.Empty)); }; // To update drawing errors

			designerGenerator.GenerationStarted += s => Dispatcher.UIThread.Invoke(() => { DesignerGenerateStart(); });
			designerGenerator.GenerationEnded += s => Dispatcher.UIThread.Invoke(() => { DesignerGenerateEnd(s); });

			textEditor.TextArea.Caret.PositionChanged += UpdateDesignerDocumentHightlight;
			textEditor.TextArea.SelectionChanged += UpdateDesignerDocumentHightlight;
			DesignerArea.CanvasChanged += UpdateDesignerDocumentHightlight;
		}

		ToolbarMessage? currentBackgroundMessage;

		private void DesignerGenerateStart() {
			currentBackgroundMessage = DisplayBackgroundMessage("Drawing...", DrawingMessagePriority, AwaitingMessageDuration);
		}

		private void DesignerGenerateEnd(GenerationState state) {
			FinishBackgroundMessage(currentBackgroundMessage);
			if (state == GenerationState.NONE || state == GenerationState.SUCCESS || state == GenerationState.TERMINATED) {
				DisplayBackgroundMessage("Drawing complete", DrawnMessagePriority, InfoMessageDuration);
			}
			else {
				DisplayBackgroundMessage("Drawing error", DrawingMessagePriority, ErrorMessageDuration);
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

		private void OnCanvasDoubleClick(object? sender, CanvasAreaEventArgs e) {
			IDrawingMapper? drawingMapper = designerGenerator?.DrawingMapper;
			if (drawingMapper != null && e.Areas.Count > 0) {
				//Console.WriteLine("Areas: " + string.Join(", ", areas.Select(r => (r.Key.GetType().Name ?? "NULL") + $" ({r.Value})")));
				//Console.WriteLine("Position: " + pos);
				//Console.WriteLine($"Double click: {string.Join("; ", areas.Select(kv => drawingMapper.GetDrawnObjectLocation(kv.Key)).WhereNotNull().OrderBy(l => l.Line).Select(l => $"line {l.Line} offset {l.Offset}"))}");
				//Console.WriteLine($"Double click: {string.Join("; ", areas.OrderBy(kv => kv.Value.Total.Area).ThenByDescending(kv => drawingMapper.GetDrawnObjectDepth(kv.Key) ?? -1).Select(l => $"{l.Key.GetType()}"))}");
				//Console.WriteLine($"Double click: {string.Join("; ", areas.OrderBy(kv => kv.Value.Total.Area).ThenByDescending(kv => drawingMapper.GetDrawnObjectDepth(kv.Key) ?? -1).Select(kv => drawingMapper.GetDrawnObjectLocation(kv.Key)).Select(l => $"{l?.ToString() ?? "NULL"}"))}");
				if (e.Areas.OrderBy(kv => kv.Value.Total.Area).ThenByDescending(kv => drawingMapper.GetDrawnObjectDepth(kv.Key) ?? -1).Select(kv => drawingMapper.GetDrawnObjectLocation(kv.Key)).WhereNotNull().Where(l => l.Line >= 0).FirstOrDefault() is DocumentSpan location) {
					//if (areas.OrderByDescending(kv => drawingMapper.GetDrawnObjectDepth(kv.Key) ?? -1).Select(kv => drawingMapper.GetDrawnObjectLocation(kv.Key)).WhereNotNull().Where(s => s.Line >= 0).FirstOrDefault() is DocumentSpan location) {

					if (location.Line + 1 > 0 && location.Line + 1 <= textEditor.Document.LineCount) {
						DocumentLine line = textEditor.Document.GetLineByNumber(location.Line + 1);
						textEditor.ScrollToLine(line.LineNumber);
						textEditor.CaretOffset = TextUtilities.GetWhitespaceAfter(textEditor.Document, line.Offset).EndOffset;

						RequestFocus?.Invoke(sender, new DocumentEditorEventArgs(this));
						//textEditor.TextArea.Focus();
						//Keyboard.Focus(textEditor.TextArea);
						FocusEditor();
					}
				}
			}
		}

		private void OnCanvasMouseMove(object? sender, CanvasAreaEventArgs e) {
			IDrawingMapper? drawingMapper = designerGenerator?.DrawingMapper;
			if (drawingMapper != null) {
				if (e.Areas.Count > 0) {
					object[] drawnObjects = e.Areas.Where(kv => drawingMapper.GetDrawnObjectDepth(kv.Key).HasValue)
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

		#region Commands

		public ICommand DuplicateCommand { get; private set; }
		//public ICommand CompletionTriggerCommand { get; private set; }

		[MemberNotNull(nameof(DuplicateCommand))]
		private void InitialiseCommands() {
			DuplicateCommand = new RelayCommand(DuplicateExecuted);
			//CompletionTriggerCommand = new RelayCommand(CompletionTriggerExecuted);
		}

		private void DuplicateExecuted() {
			if (textEditor is not null) {
				if (textEditor.TextArea.Selection.IsEmpty) {
				DocumentLine currentLine = textEditor.Document.GetLineByOffset(textEditor.CaretOffset);
				string currentLineText;
				if (currentLine.NextLine is DocumentLine nextLine) {
					currentLineText = textEditor.Document.GetText(currentLine.Offset, nextLine.Offset - currentLine.Offset);
				}
				else {
					currentLineText = textEditor.Document.GetText(currentLine.Offset, currentLine.TotalLength) + "\n";
				}
				textEditor.Document.Insert(currentLine.Offset, currentLineText);
			}
				else {
					(int startOffset, int length) = GetSelection();
					int endOffset = startOffset + length;

					string selectedText = textEditor.Document.GetText(startOffset, length);
					textEditor.Document.Insert(endOffset, selectedText);
				}
			}
		}

		//private void CompletionTriggerExecuted() {
		//	OpenCompletionWindow();
		//}

		public void OpenSearchPanel(bool replaceMode) {
			if (textEditor.SearchPanel is AvaloniaEdit.Search.SearchPanel searchPanel) {
				textEditor.Focus();
				searchPanel.Open();
				searchPanel.IsReplaceMode = replaceMode;
				searchPanel.Focus();
			}
		}

		#endregion Commands

	}

	public class ErrorPopupEntry {

		public string Line => (Location?.Line + 1).ToString() ?? "-";
		public DocumentSpan? Location { get; }
		public string Message { get; }

		public ICommand ItemCommand { get; }

		public ErrorPopupEntry(DocumentSpan? location, string message, Action<ErrorPopupEntry> command) {
			Location = location;
			Message = message;

			ItemCommand = new RelayCommand(() => { command(this); });
		}

	}

}
