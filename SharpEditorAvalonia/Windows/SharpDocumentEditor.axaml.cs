using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit.Document;
using SharpEditorAvalonia.DataManagers;
using SharpEditorAvalonia.Designer;
using System;
using System.Collections.Generic;
using SharpSheets.Utilities;
using System.Text.RegularExpressions;
using Avalonia.Interactivity;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Threading;
using SharpSheets.Exceptions;
using System.Linq;
using Avalonia.Layout;
using SharpSheets.Parsing;
using SharpEditorAvalonia.Folding;
using AvaloniaEdit.Folding;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using SharpEditorAvalonia.CodeHelpers;
using AvaloniaEdit.Editing;
using SharpEditorAvalonia.Indentation;

namespace SharpEditorAvalonia.Windows {

	public class UpdateHeaderEventArgs : EventArgs {
		public string Header { get; }
		public bool UnsavedProgress { get; }
		public UpdateHeaderEventArgs(string header, bool unsavedProgress) {
			this.Header = header;
			this.UnsavedProgress = unsavedProgress;
		}

		private void Binding(object? sender, Avalonia.Input.PointerWheelEventArgs e) {
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

		// TODO Search panel stuff goes here

		public SharpDocumentEditor(string? filename) {
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
			//InitializeToolTip();
			//InitializeErrorMarkers();
			InitializeUnsavedTracker();
			//InitializeCompletionWindow();

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

			//textEditor.SnapsToDevicePixels = true;
			//textEditor.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

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

		public void Uninstall() {
			textEditor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
			SharpDataManager.Instance.WarnFontLicensingChanged -= Refresh;

			UninstallTextEditorDependencyProperties();
			UninstallTextZoom();
			UninstallParsingManager();
			UninstallDesigner();
			UninstallFoldingManager();
			//UninstallToolTip();
			//UninstallErrorMarkers();
			UninstallUnsavedTracker();
			//UninstallCompletionWindow();
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
			textEditor.TextArea.TextView.Redraw(); // Still needed?
		}
		#endregion Track User Changes

		#region Text Zoom

		private void InitializeTextZoom() {
			baseFontSize = textEditor.FontSize;
			SetTextZoom(SharpDataManager.Instance.TextZoom);
			SharpDataManager.Instance.TextZoomChanged += OnTextZoomChanged;
		}

		private void UninstallTextZoom() {
			SharpDataManager.Instance.TextZoomChanged -= OnTextZoomChanged;
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
			Dispatcher.UIThread.Invoke(() => {
				lock (backgroundMessageLock) {
					backgroundMessageTimer.IsEnabled = false;
					BackgroundMessageText.IsVisible = false;
					BackgroundMessageText.Text = "";
				}
			});
		}

		private BackgroundMessage? DisplayBackgroundMessage(string message, int priority, bool withTimer) {
			if (priority >= (currentMessage?.Priority ?? LowestMessagePriority)) {
				lock (backgroundMessageLock) {
					Dispatcher.UIThread.Invoke(() => {
						backgroundMessageTimer.IsEnabled = false;
						BackgroundMessageText.IsVisible = true;
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
					Dispatcher.UIThread.Invoke(() => {
						backgroundMessageTimer.IsEnabled = false;
						BackgroundMessageText.IsVisible = false;
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
			if (CurrentFilePath != filename) {
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

		// TODO Past event handling?
		/*
		private void PasteEvent(object sender, DataObjectPastingEventArgs args) {
			codeHelper?.TextPasted(args, textEditor.CaretOffset);
		}
		*/

		#endregion CodeHelper

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

			parsingManager.ParseStarted += s => Dispatcher.UIThread.Invoke(() => { ParseStart(); });
			parsingManager.ParseEnded += s => Dispatcher.UIThread.Invoke(() => { ParseEnd(s); });

			//parsingManager.ParseEnded += s => Dispatcher.Invoke(() => { RedrawAfterParse(); });
			parsingManager.ResultLoaded += RedrawAfterParse;
			parsingManager.ResultLoaded += () => Dispatcher.UIThread.Invoke(() => { UpdateDesignerOverlay(); }); // Dispatcher necessary?

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

		void UpdateErrorDisplay(object? sender, EventArgs args) { }

	}

}
