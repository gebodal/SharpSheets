using Avalonia.Controls;
using SharpEditor.DataManagers;
using System;
using System.Collections.Generic;
using SharpSheets.Utilities;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Input;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using System.Windows.Input;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Collections;
using SharpEditor.Designer.DrawingCanvas;

namespace SharpEditor.Designer {

	public class CanvasAreaEventArgs : EventArgs {

		public readonly IReadOnlyDictionary<object, RegisteredAreas> Areas;

		public CanvasAreaEventArgs(IReadOnlyDictionary<object, RegisteredAreas> areas) {
			Areas = areas;
		}

	}

	public partial class DesignerArea : UserControl {

		public readonly double DesignerHighlightStrokeThickness = 2.0; // TODO Turn into property
		public readonly double DesignerMouseHighlightStrokeThickness = 1.25; // TODO Turn into property

		DesignerDocumentHolder? currentDocument;

		public event EventHandler? CanvasChanged; // Better event type?
		public event EventHandler<CanvasAreaEventArgs>? CanvasAreaDoubleClick;

		public SharpDataManager DataManager { get; }

		public DesignerArea() {
			DataManager = SharpDataManager.Instance;
			// Create the commands here so they're available for initialisation
			InitialiseCommands();

			InitializeComponent();
			this.DataContext = this;

			// Wire up command events here so that the components have been initialised
			WireUpCommands();

			SharpDataManager.Instance.DesignerDisplayFieldsChanged += OnDesignerDisplayFieldsChanged;
			SharpDataManager.Instance.ScreenDPIChanged += OnScreenDPIChanged;

			this.AddHandler(UserControl.PointerPressedEvent, OnDesignerPointerPressed, RoutingStrategies.Tunnel, false);

			DesignerViewer.DPI = SharpDataManager.Instance.ScreenDPI;
			this.DesignerViewer.ScaleChanged += OnCanvasScaleChanged;
		}

		/// <summary>
		/// True if the provided document is not null, and is different to the current document, otherwise false.
		/// </summary>
		/// <param name="newDocument"> The candidate new document. </param>
		/// <returns></returns>
		public bool IsValidNewDocument(SharpGeometryDrawingDocument newDocument) {
			return newDocument != null && (currentDocument is null || currentDocument.IsValidNewDocument(newDocument));
		}

		public void SetDesignerDocument(SharpGeometryDrawingDocument newDocument) {
			// Only update if designer is visible, and provided document is new and valid
			if (this.IsVisible && IsValidNewDocument(newDocument)) {

				int previousPageIndex = currentDocument?.CurrentPageIndex ?? 0;
				currentDocument = new DesignerDocumentHolder(newDocument, previousPageIndex);

				PageCountTextBlock.Text = currentDocument.PageCount.ToString();

				if (currentDocument.PageCount > 0 && currentDocument.CurrentPage != null) {
					DesignerViewer.CanvasContent = currentDocument.CurrentPage.PageCanvas;

					PageNumber.Text = (currentDocument.CurrentPageIndex + 1).ToString();
				}
				else {
					PageNumber.Text = "0";
				}

				CanvasChanged?.Invoke(null, EventArgs.Empty);
			}
		}

		public void Reset() {
			currentDocument = null;
			PageCountTextBlock.Text = "0";
			PageNumber.Text = "0";
			DesignerViewer.Reset();
		}

		private void SetDesignerPageByIndex(int pageIndex) {
			if (currentDocument != null) {
				currentDocument.CurrentPageIndex = pageIndex;
				PageNumber.Text = (currentDocument.CurrentPageIndex + 1).ToString();
				DesignerViewer.CanvasContent = currentDocument.CurrentPage?.PageCanvas;

				//UpdateDesignerDocumentHightlight(null, EventArgs.Empty);
				CanvasChanged?.Invoke(null, EventArgs.Empty);
			}
		}

		private void SetDesignerPage() {
			if (currentDocument != null && currentDocument.PageCount > 0) {
				int newIndex = (int.TryParse(PageNumber.Text, out int parsed) ? parsed : 1).Clamp(1, currentDocument.PageCount) - 1;

				SetDesignerPageByIndex(Math.Max(0, newIndex));
			}
		}

		private int IncrementPage(int increment) {
			if (currentDocument is not null) {
				int initialPageIndex1 = currentDocument.CurrentPageIndex;
				int finalPageIndex1 = initialPageIndex1 + increment;
				SetDesignerPageByIndex(finalPageIndex1);
				return currentDocument.CurrentPageIndex - initialPageIndex1;
			}
			else {
				return 0;
			}
		}

		private void CanvasPreviousPage(object? sender, RoutedEventArgs e) {
			IncrementPage(-1);
		}

		private void CanvasNextPage(object? sender, RoutedEventArgs e) {
			IncrementPage(1);
		}

		// Navigation commands
		/*
		void BrowseBackExecuted(object target, ExecutedRoutedEventArgs e) {
			IncrementPage(-1);
			e.Handled = true;
		}
		void BrowseForwardExecuted(object target, ExecutedRoutedEventArgs e) {
			IncrementPage(1);
			e.Handled = true;
		}
		void NavigationCommandCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = true;
		}
		*/

		private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e) {
			if (e.KeyModifiers == KeyModifiers.None) { // Only try and scroll if no modifiers are pressed
				if (currentDocument != null && currentDocument.PageCount > 0) {
					double delta = e.Delta.Y;
					int change = (int)Math.Round(Math.Max(1, Math.Abs(1f * delta) / 120f));

					if (delta > 0 && Math.Abs(DesignerViewer.VerticalOffset) < 1e-9) {
						//Console.WriteLine("Up");
						int pageDelta = IncrementPage(-change);
						if (pageDelta != 0) {
							DesignerViewer.ScrollToVerticalOffset(DesignerViewer.ScrollableHeight); // Scroll to bottom
						}
						e.Handled = true;
					}
					else if (delta < 0 && Math.Abs(DesignerViewer.VerticalOffset - DesignerViewer.ScrollableHeight) < 1e-9) {
						//Console.WriteLine("Down");
						int pageDelta = IncrementPage(change);
						if (pageDelta != 0) {
							DesignerViewer.ScrollToVerticalOffset(0); // Scroll to top
						}
						e.Handled = true;
					}
				}
			}
		}

		private void DesignerKeyHandler(object? sender, KeyEventArgs e) {
			if (e.Key == Key.Left) {
				CanvasPreviousPage(sender, e);
				e.Handled = true;
			}
			else if (e.Key == Key.Right) {
				CanvasNextPage(sender, e);
				e.Handled = true;
			}
			else if (e.Key == Key.OemPlus) {
				DesignerViewer.UpdateCanvasZoom(true);
				e.Handled = true;
			}
			else if (e.Key == Key.OemMinus) {
				DesignerViewer.UpdateCanvasZoom(false);
				e.Handled = true;
			}
			else if (e.Key == Key.F) {
				SharpDataManager.Instance.DesignerDisplayFields = !SharpDataManager.Instance.DesignerDisplayFields;
				e.Handled = true;
			}
		}

		private void PageNumberKeyDown(object? sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				//CanvasView.Focus();
				DesignerViewer.Focus(); // TODO Does this do what it's supposed to?
			}
		}

		readonly Regex digitRegex = new Regex("[0-9]+");
		private void ValidatePageNumber(object? sender, TextInputEventArgs e) {
			e.Handled = !digitRegex.IsMatch(e.Text ?? "");
		}

		private void UpdatedDesignerPageNumber(object? sender, RoutedEventArgs e) {
			SetDesignerPage();
		}

		private void CanvasZoomOut(object? sender, RoutedEventArgs e) {
			DesignerViewer.UpdateCanvasZoom(false);
		}

		private void CanvasZoomIn(object? sender, RoutedEventArgs e) {
			DesignerViewer.UpdateCanvasZoom(true);
		}

		readonly Regex percentageRegex = new Regex("[0-9%\\.]+");
		private void ValidateCanvasZoom(object? sender, TextInputEventArgs e) {
			e.Handled = !percentageRegex.IsMatch(e.Text ?? "");
		}

		private void CanvasZoomPreviewKeyDown(object? sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				e.Handled = true;
				DesignerViewer.Focus();
			}
		}

		private void UpdateCanvasZoomLevel(object? sender, RoutedEventArgs e) {
			Match match = Regex.Match(CanvasZoomLevelText.Text ?? "", "^(?<number>[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)%?$");

			Console.WriteLine("Focus lost: " + CanvasZoomLevelText.Text + ", " + match + ", " + match.Groups[0] + ", " + match.Groups[1]);

			if (match.Groups[1].Success) {
				try {
					double zoom = double.Parse(match.Groups[1].Value) / 100.0;
					DesignerViewer.SetScale(zoom);
					return;
				}
				catch (FormatException) { }
			}

			UpdateCanvasZoomText();
		}

		private void SetCanvasZoomWholePage(object? sender, RoutedEventArgs e) {
			DesignerViewer.ZoomCanvasToWholePage();
		}

		void OnCanvasScaleChanged(object? sender, EventArgs e) {
			if (currentDocument?.CurrentPage is DesignerPageHolder currentPage) {
				double newStrokeThickness = DesignerHighlightStrokeThickness / DesignerViewer.Scale;
				currentPage.Highlights?.UpdateHighlightStrokeWidth(newStrokeThickness);
				currentPage.MouseHighlights?.UpdateHighlightStrokeWidth(newStrokeThickness);
			}

			UpdateCanvasZoomText();
		}

		void UpdateCanvasZoomText() {
			CanvasZoomLevelText.Text = string.Format("{0:0.#}%", DesignerViewer.Scale * 100.0);
		}

		private void OnScreenDPIChanged(object? sender, EventArgs e) {
			DesignerViewer.DPI = SharpDataManager.Instance.ScreenDPI;
			UpdateCanvasZoomText();
		}

		void OnCanvasDoubleClick(object? sender, PointerPressedEventArgs e) {
			if (currentDocument?.CurrentPage is DesignerPageHolder currentPage && DesignerViewer.CanvasContent is IInputElement) {
				//Point pos = e.GetPosition((Canvas)DesignerViewer.CanvasContent);
				Point pos = e.GetPosition(DesignerViewer.CanvasContent);

				Dictionary<object, RegisteredAreas> areas = currentPage.GetAreas(pos);

				if (areas.Count > 0) {
					CanvasAreaDoubleClick?.Invoke(sender, new CanvasAreaEventArgs(areas));
				}
			}
		}

		//private HashSet<object>? currentMouseHighlightedObjects = null;

		/*
		private void OnDesignerViewerMouseMove(object sender, MouseEventArgs e) {
			if (currentPageCanvas != null && currentPage?.MouseHighlights is not null && DesignerViewer.CanvasContent is IInputElement) {
				Point pos = e.GetPosition(DesignerViewer.CanvasContent);

				Dictionary<object, RegisteredAreas> areas = currentPageCanvas.GetAreas(pos);

				if (areas.Count > 0) {
					HashSet<object> drawnObjects = new HashSet<object>(areas.Keys);
					if (currentMouseHighlightedObjects is null || !currentMouseHighlightedObjects.SetEquals(drawnObjects)) {
						currentMouseHighlightedObjects = drawnObjects;
						Highlight(currentPage?.MouseHighlights, areas.Reverse().OrderBy(kv => kv.Value.Total.Area).Select(kv => kv.Key).First().Yield().ToArray(),
							Colors.Gray, Colors.Gray, Colors.LightGray, 0.6, DesignerMouseHighlightStrokeThickness, false, false);
					}
				}
				else {
					currentMouseHighlightedObjects = null;
					currentPage.MouseHighlights.Visibility = Visibility.Hidden;
					currentPage.MouseHighlights.Children.Clear();
				}
			}
		}
		*/

		public event EventHandler<CanvasAreaEventArgs>? CanvasMouseMove;

		private void OnDesignerViewerPointerMoved(object sender, PointerEventArgs e) {
			if (currentDocument?.CurrentPage != null && DesignerViewer.CanvasContent is IInputElement) {
				//Point pos = e.GetPosition((Canvas)DesignerViewer.CanvasContent);
				Point pos = e.GetPosition(DesignerViewer.CanvasContent);

				Dictionary<object, RegisteredAreas> areas = currentDocument.CurrentPage.GetAreas(pos);

				CanvasMouseMove?.Invoke(sender, new CanvasAreaEventArgs(areas));
			}
		}

		private void OnDesignerViewerPointerExited(object sender, PointerEventArgs e) {
			if (currentDocument?.CurrentPage is DesignerPageHolder currentPage) {
				//currentMouseHighlightedObjects = null;
				currentPage.MouseHighlights.IsVisible = false;
				currentPage.MouseHighlights.Children.Clear();
			}
		}

		public void HighlightMinor(object[]? drawnObjects) {
			Highlight(currentDocument?.CurrentPage?.MouseHighlights, drawnObjects, Colors.Gray, Colors.Gray, Colors.LightGray, 0.6, DesignerMouseHighlightStrokeThickness, false, false);
		}

		public void Highlight(object[]? drawnObjects, bool scrollToArea = false, bool zoomToArea = false) {
			Highlight(currentDocument?.CurrentPage?.Highlights, drawnObjects, Colors.Lime, Colors.Orange, Colors.Cyan, 0.75, DesignerHighlightStrokeThickness, scrollToArea: scrollToArea, zoomToArea: zoomToArea);
		}

		private void Highlight(Canvas? highlightCanvas, object[]? drawnObjects, Color originalColor, Color innerColor, Color adjustedColor, double opacity, double lineThickness, bool scrollToArea = false, bool zoomToArea = false) {
			if (IsVisible && currentDocument != null && currentDocument?.CurrentPage is DesignerPageHolder currentPage && highlightCanvas != null && currentDocument.PageCount > 0) {
				try {
					highlightCanvas.IsVisible = false;
					highlightCanvas.Children.Clear();

					if (drawnObjects != null && drawnObjects.Length > 0) {
						highlightCanvas.IsVisible = true;
						HashSet<SharpSheets.Layouts.Rectangle> highlightedRects = new HashSet<SharpSheets.Layouts.Rectangle>();
						foreach (RegisteredAreas areas in drawnObjects.SelectMany(d => currentPage.GetAreas(d)).OrderBy(r => r.Total.Area)) {
							// Draw smallest rectangles first. If a new rectangle completely contains a previously drawn rectangle, ignore it.
							// The larger rects are needed as targets for going from the designer back to the document
							if (areas.Original.Width >= 0 && areas.Original.Height >= 0 && !highlightedRects.Any(r => areas.Original.Contains(r))) {
								highlightedRects.Add(areas.Original);

								if (areas.Inner.Length > 0) {
									foreach (SharpSheets.Layouts.Rectangle innerRect in areas.Inner) {
										if (innerRect.Width < 0f || innerRect.Height < 0f) { continue; } // Ignore malformed areas

										highlightCanvas.AddRect(innerRect, new SolidColorBrush(innerColor) { Opacity = 0.8 * opacity }, lineThickness / DesignerViewer.Scale);
									}
								}

								if (areas.Adjusted is not null && areas.Original != areas.Adjusted && areas.Adjusted.Width >= 0f && areas.Adjusted.Height >= 0f) {
									highlightCanvas.AddRect(areas.Adjusted, new SolidColorBrush(adjustedColor) { Opacity = opacity }, lineThickness / DesignerViewer.Scale);
								}

								if (areas.Original.Width >= 0f && areas.Original.Height >= 0f) {
									highlightCanvas.AddRect(areas.Original, new SolidColorBrush(originalColor) { Opacity = opacity }, lineThickness / DesignerViewer.Scale);
								}
							}
						}

						if (scrollToArea && highlightedRects.Count > 0) {
							SharpSheets.Layouts.Rectangle wholeArea = SharpSheets.Layouts.Rectangle.GetCommonRectangle(highlightedRects);
							float canvasHeight = currentPage.Height;
							DesignerViewer.ShowArea(wholeArea.Left, canvasHeight - wholeArea.Bottom, wholeArea.Right, canvasHeight - wholeArea.Top, zoomToArea);
						}
					}
				}
				catch (InvalidOperationException) { } // TODO Why...?
			}
		}

		public bool DisplayObject(object drawnObject, bool zoomToArea) {
			if (IsVisible && currentDocument != null&& currentDocument.PageCount > 0) {
				int? objectPageIndex = currentDocument.GetOwnerPage(drawnObject);

				if (objectPageIndex.HasValue) {
					if (objectPageIndex.Value != currentDocument.CurrentPageIndex) {
						SetDesignerPageByIndex(objectPageIndex.Value);
					}

					Highlight(new object[] { drawnObject }, scrollToArea: true, zoomToArea: zoomToArea);

					return true; // Successfully displayed drawnObject
				}
			}

			return false; // Could not display drawnObject
		}

		private void OnDesignerDisplayFieldsChanged(object? sender, EventArgs e) {
			if (currentDocument?.CurrentPage is DesignerPageHolder currentPage) {
				currentPage.Fields.IsVisible = SharpDataManager.Instance.DesignerDisplayFields;
			}
		}

		private class DesignerDocumentHolder {

			private readonly SharpGeometryDrawingDocument designerDocument;
			private readonly Dictionary<int, DrawingGroup> pages;

			public int PageCount => designerDocument.PageCount;

			private int _currentPageIndex;
			public int CurrentPageIndex {
				get {
					return _currentPageIndex;
				}
				set {
					SetPage(value);
				}
			}

			public DesignerPageHolder? CurrentPage { get; private set; }

			public DesignerDocumentHolder(SharpGeometryDrawingDocument designerDocument, int currentPageIndex) {
				this.designerDocument = designerDocument;

				//pages = designerDocument.Pages.Select(p => p.drawingGroup.BuildGroup()).ToArray();
				pages = new Dictionary<int, DrawingGroup>();

				if(PageCount > 0) {
					int initialPageIndex = Math.Min(Math.Max(0, currentPageIndex), designerDocument.Pages.Count - 1);
					_currentPageIndex = -1;
					SetPage(initialPageIndex);
				}
				else {
					_currentPageIndex = 0;
					CurrentPage = null;
				}
			}

			public bool IsValidNewDocument(SharpGeometryDrawingDocument newDocument) {
				return !object.ReferenceEquals(designerDocument, newDocument);
			}

			private void SetPage(int proposedIndex) {
				int newIndex = Math.Min(Math.Max(0, proposedIndex), designerDocument.PageCount - 1);
				if(newIndex != _currentPageIndex) {
					_currentPageIndex = newIndex;

					DrawingGroup newPage;
					if(pages.TryGetValue(_currentPageIndex, out DrawingGroup? pageGroup)) {
						newPage = pageGroup;
					}
					else {
						newPage = designerDocument.Pages[_currentPageIndex].drawingGroup.BuildGroup();
						pages[_currentPageIndex] = newPage;
					}

					CurrentPage = new DesignerPageHolder(designerDocument.Pages[_currentPageIndex], newPage);
				}
			}

			public int? GetOwnerPage(object owner) {
				return designerDocument.GetOwnerPage(owner);
			}

		}

		private class DesignerPageHolder {
			private readonly SharpGeometryDrawingCanvas page;
			private readonly DrawingGroup pageDrawing;

			public Canvas PageCanvas { get; }

			public Canvas Highlights { get; }
			public Canvas MouseHighlights { get; }
			public Canvas Fields { get; }

			public float Width => page.CanvasRect.Width;
			public float Height => page.CanvasRect.Height;

			public DesignerPageHolder(SharpGeometryDrawingCanvas documentPage, DrawingGroup pageDrawing) {
				page = documentPage;
				this.pageDrawing = pageDrawing;

				PageCanvas = new Canvas() {
					Width = page.CanvasRect.Width,
					Height = page.CanvasRect.Height
				};

				DrawingElement element = new DrawingElement(this.pageDrawing) {
					Width = page.CanvasRect.Width,
					Height = page.CanvasRect.Height
				};
				PageCanvas.Children.Add(element);

				Fields = MakeNewFieldCanvas(page.CanvasRect.Width, page.CanvasRect.Height);
				foreach (CanvasField field in page.Fields.Values.Where(f => f.Rect.Width > 0 && f.Rect.Height > 0)) {
					Fields.AddHighlight(field.Rect,
						stroke: field.Type == FieldType.IMAGE ? new SolidColorBrush(Colors.DarkGray) { Opacity = 0.5 } : null,
						strokeThickness: field.Type == FieldType.IMAGE ? 1 : 0,
						strokeDashArray: field.Type == FieldType.IMAGE ? new AvaloniaList<double> { 3, 3 } : null,
						fill: new SolidColorBrush(field.Type == FieldType.IMAGE ? Colors.LightGray : Colors.Blue) { Opacity = 0.1 },
						toolTip: GetFieldTooltip(field));
				}
				PageCanvas.Children.Add(Fields);
				Canvas.SetLeft(Fields, 0);
				Canvas.SetTop(Fields, 0);

				MouseHighlights = new Canvas() {
					Width = page.CanvasRect.Width,
					Height = page.CanvasRect.Height,
					IsHitTestVisible = false
				};
				PageCanvas.Children.Add(MouseHighlights);
				Canvas.SetLeft(MouseHighlights, 0);
				Canvas.SetTop(MouseHighlights, 0);

				Highlights = new Canvas() {
					Width = page.CanvasRect.Width,
					Height = page.CanvasRect.Height,
					IsHitTestVisible = false
				};
				PageCanvas.Children.Add(Highlights);
				Canvas.SetLeft(Highlights, 0);
				Canvas.SetTop(Highlights, 0);
			}

			private static Canvas MakeNewFieldCanvas(double width, double height) {
				return new Canvas() {
					Width = width,
					Height = height,
					IsVisible = SharpDataManager.Instance.DesignerDisplayFields
				};
			}

			public static object GetFieldTooltip(CanvasField field) {
				return field.Name + (!string.IsNullOrEmpty(field.Tooltip) ? $"\n\n{field.Tooltip}" : "");
			}

			public Dictionary<object, RegisteredAreas> GetAreas(Point point) {
				return page.GetAreas(point);
			}

			public IEnumerable<RegisteredAreas> GetAreas(object owner) {
				return page.GetAreas(owner);
			}
		}

		#region Commands

		public ICommand BackCommand { get; private set; }
		public ICommand ForwardCommand { get; private set; }

		public ICommand ZoomInCommand { get; private set; }
		public ICommand ZoomOutCommand { get; private set; }

		public ICommand ZoomWholePageCommand { get; private set; }

		[MemberNotNull(nameof(BackCommand), nameof(ForwardCommand),
			nameof(ZoomInCommand), nameof(ZoomOutCommand),
			nameof(ZoomWholePageCommand))]
		private void InitialiseCommands() {
			BackCommand = new RelayCommand(BackExecuted, CanGoBack);
			ForwardCommand = new RelayCommand(ForwardExecuted, CanGoForward);

			ZoomInCommand = new RelayCommand(ZoomInExecuted, CanZoomIn);
			ZoomOutCommand = new RelayCommand(ZoomOutExecuted, CanZoomOut);

			ZoomWholePageCommand = new RelayCommand(ZoomWholePageExecuted, CanZoomWholePage);
		}

		private void WireUpCommands() {
			CanvasChanged += OnCanvasChangedNotifyCommands;
			DesignerViewer.ScaleChanged += OnCanvasScaleChangedNotifyCommands;
		}

		private void OnCanvasChangedNotifyCommands(object? sender, EventArgs e) {
			(BackCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(ForwardCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(ZoomInCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(ZoomOutCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(ZoomWholePageCommand as RelayCommand)?.NotifyCanExecuteChanged();
		}

		private void OnCanvasScaleChangedNotifyCommands(object? sender, EventArgs e) {
			(ZoomInCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(ZoomOutCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(ZoomWholePageCommand as RelayCommand)?.NotifyCanExecuteChanged();
		}

		private void BackExecuted() {
			IncrementPage(-1);
		}
		private bool CanGoBack() {
			return currentDocument is not null && currentDocument.CurrentPageIndex > 0;
		}

		private void ForwardExecuted() {
			IncrementPage(1);
		}
		private bool CanGoForward() {
			return currentDocument is not null && currentDocument.CurrentPageIndex < currentDocument.PageCount - 1;
		}

		private void ZoomInExecuted() {
			DesignerViewer.UpdateCanvasZoom(true);
		}
		private bool CanZoomIn() {
			return currentDocument is not null;
		}

		private void ZoomOutExecuted() {
			DesignerViewer.UpdateCanvasZoom(false);
		}
		private bool CanZoomOut() {
			return currentDocument is not null;
		}

		private void ZoomWholePageExecuted() {
			DesignerViewer.ZoomCanvasToWholePage();
		}
		private bool CanZoomWholePage() {
			return currentDocument is not null && !DesignerViewer.WholePageZoomOn;
		}

		#endregion

		#region Pointer events

		private void OnDesignerPointerPressed(object? sender, PointerPressedEventArgs e) {
			PointerPoint point = e.GetCurrentPoint(sender as Control);
			if (point.Properties.IsXButton1Pressed) {
				if (CanGoBack()) {
					BackExecuted();
				}
				e.Handled = true;
			}
			else if (point.Properties.IsXButton2Pressed) {
				if (CanGoForward()) {
					ForwardExecuted();
				}
				e.Handled = true;
			}
		}

		#endregion Pointer events

	}

	public static class HighlightingUtils {

		public static void AddHighlight(this Canvas canvas, SharpSheets.Layouts.Rectangle rect, Brush? stroke, double? strokeThickness, AvaloniaList<double>? strokeDashArray, Brush? fill, object? toolTip) {
			/*
			Rectangle highlight = new Rectangle() {
				Width = rect.Width,
				Height = rect.Height,
				Stroke = stroke,
				StrokeThickness = strokeThickness ?? 0.0,
				StrokeDashArray = strokeDashArray,
				Fill = fill,
				IsHitTestVisible = false
			};

			canvas.Children.Add(highlight);

			Canvas.SetLeft(highlight, rect.Left);
			//Canvas.SetTop(highlight, currentPageCanvas.CanvasRect.Height - rect.Top);
			Canvas.SetTop(highlight, canvas.Height - rect.Top);

			if (toolTip is not null) {
				ToolTip.SetTip(highlight, toolTip);
			}
			*/

			GeometryDrawing drawing = new GeometryDrawing {
				Geometry = new RectangleGeometry(new Rect(rect.X, canvas.Height - rect.Top, rect.Width, rect.Height)),
				Pen = stroke is not null && strokeThickness > 0 ? new Pen(stroke, strokeThickness ?? 0) {
					DashStyle = new DashStyle(strokeDashArray, 0.0)
				} : null,
				Brush = fill
			};

			DrawingGroup drawingGroup = new DrawingGroup() {
				Children = { drawing }
			};

			DrawingElement element = new DrawingElement(drawingGroup) {
				//LayoutTransform = TestBlock.LayoutTransform,
				Width = canvas.Width,
				Height = canvas.Height
			};

			if (toolTip is not null) {
				ToolTip.SetTip(element, toolTip);
			}

			canvas.Children.Add(element);
		}

		public static void AddRect(this Canvas canvas, SharpSheets.Layouts.Rectangle rect, Brush? stroke, double? strokeThickness) {
			AddHighlight(canvas, rect, stroke, strokeThickness, null, null, null);
		}

		public static void UpdateHighlightStrokeWidth(this Canvas canvas, double strokeThickness) {
			/*
			foreach (Shape shape in canvas.Children.OfType<Shape>()) {
				shape.StrokeThickness = strokeThickness;
			}
			*/

			foreach(DrawingElement element in canvas.Children.OfType<DrawingElement>()) {
				if (element.drawing is DrawingGroup elementGroup) {
					foreach (Drawing i in elementGroup.Children) {
						if (i is GeometryDrawing g && g.Pen is Pen p) {
							//g.Pen?.Thickness = strokeThickness;
							g.Pen = new Pen(p.Brush, strokeThickness, p.DashStyle, p.LineCap, p.LineJoin, p.MiterLimit);
							element.InvalidateVisual();
						}
					}
				}
			}
		}

	}

}
