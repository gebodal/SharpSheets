using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SharpEditor.DataManagers;

namespace SharpEditor {

	/// <summary>
	/// Interaction logic for DesignerArea.xaml
	/// </summary>
	public partial class DesignerArea : UserControl {

		public readonly double DesignerHighlightStrokeThickness = 2.0; // TODO Turn into property

		SharpWPFDrawingDocument? designerDocument;
		SharpWPFDrawingCanvas? currentPageCanvas;
		int currentDesignerPageIndex;

		DesignerPageHolder? currentPage;

		public event EventHandler? CanvasChanged; // TODO Better event type?
		//public event MouseButtonEventHandler PreviewCanvasLeftDoubleClick;
		public event EventHandler<Dictionary<object, RegisteredAreas>>? CanvasAreaDoubleClick;

		public DesignerArea() {
			InitializeComponent();

			SharpDataManager.Instance.DesignerDisplayFieldsChanged += OnDesignerDisplayFieldsChanged;

			this.DesignerViewer.ScaleChanged += OnCanvasScaleChanged;
		}

		/// <summary>
		/// True if the provided document is not null, and is different to the current document, otherwise false.
		/// </summary>
		/// <param name="newDocument"> The candidate new document. </param>
		/// <returns></returns>
		public bool IsValidNewDocument(SharpWPFDrawingDocument newDocument) {
			return newDocument != null && !object.ReferenceEquals(designerDocument, newDocument);
		}

		public void SetDesignerDocument(SharpWPFDrawingDocument newDocument) {
			// Only update if designer is visible, the provided document is new and valid
			if (this.Visibility == Visibility.Visible && IsValidNewDocument(newDocument)) {
				designerDocument = newDocument;

				PageCountTextBlock.Text = designerDocument.Pages.Count.ToString();
				currentDesignerPageIndex = Math.Min(Math.Max(0, currentDesignerPageIndex), designerDocument.Pages.Count - 1);
				currentPageCanvas = designerDocument.Pages.Count > 0 ? designerDocument.Pages[currentDesignerPageIndex] : null;

				if (designerDocument.Pages.Count > 0 && currentPageCanvas != null) {
					currentPage = GetDesignerPage(currentPageCanvas);
					DesignerViewer.CanvasContent = currentPage.PageCanvas;

					//UpdateDesignerDocumentHightlight(null, EventArgs.Empty);
					CanvasChanged?.Invoke(null, EventArgs.Empty);

					PageNumber.Text = (currentDesignerPageIndex + 1).ToString();
				}
				else {
					PageNumber.Text = "0";
				}
			}
			/*
			else if (newDocument == null && designerDocument != null) {
				// Something went wrong
				// TODO Display an error warning?
				DesignerViewer.OverlayText = "Null document sent to designer."; // string.Join("\n", parsingManager.GetParsingState().GetDrawingErrors().Select(e => e.Message));
				DesignerViewer.OverlayVisibility = Visibility.Visible;
			}
			*/
		}

		public void Reset() {
			designerDocument = null;
			currentPageCanvas = null;
			currentPage = null;
			//designerGenerator.Reset();
			PageCountTextBlock.Text = "0";
			PageNumber.Text = "0";
			DesignerViewer.Reset();
		}

		private void SetDesignerPageByIndex(int pageIndex) {
			if (designerDocument != null) {
				currentDesignerPageIndex = pageIndex;

				PageNumber.Text = (currentDesignerPageIndex + 1).ToString();

				currentPageCanvas = designerDocument.Pages[currentDesignerPageIndex];
				currentPage = GetDesignerPage(currentPageCanvas);
				DesignerViewer.CanvasContent = currentPage.PageCanvas;

				//UpdateDesignerDocumentHightlight(null, EventArgs.Empty);
				CanvasChanged?.Invoke(null, EventArgs.Empty);
			}
		}

		private void SetDesignerPage() {
			if (designerDocument != null && designerDocument.Pages.Count > 0) {
				int newIndex = (int.TryParse(PageNumber.Text, out int parsed) ? parsed : 1).Clamp(1, designerDocument.Pages.Count) - 1;

				SetDesignerPageByIndex(newIndex);
			}
		}

		private DesignerPageHolder GetDesignerPage(SharpWPFDrawingCanvas page) {

			DesignerPageHolder pageHolder = new DesignerPageHolder {
				Page = page,
				PageCanvas = new Canvas() {
					LayoutTransform = DesignerViewer.CanvasLayoutTransform,
					Width = page.CanvasRect.Width,
					Height = page.CanvasRect.Height
				}
			};

			DrawingElement element = new DrawingElement(page.drawingGroup) {
				//LayoutTransform = TestBlock.LayoutTransform,
				Width = page.CanvasRect.Width,
				Height = page.CanvasRect.Height
			};
			pageHolder.PageCanvas.Children.Add(element);

			pageHolder.Fields = MakeNewFieldCanvas(page.CanvasRect.Width, page.CanvasRect.Height);
			foreach (WPFCanvasField field in page.Fields.Values.Where(f => f.Rect.Width > 0 && f.Rect.Height > 0)) {
				System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle() {
					Width = field.Rect.Width,
					Height = field.Rect.Height,
					ToolTip = GetFieldTooltip(field)
				};
				if (field.Type == FieldType.IMAGE) {
					rect.StrokeDashArray = new DoubleCollection() { 3, 3 };
					rect.StrokeThickness = 1;
					rect.Stroke = new SolidColorBrush(Colors.DarkGray) { Opacity = 0.5 };
					rect.Fill = new SolidColorBrush(Colors.LightGray) { Opacity = 0.1 };
				}
				else {
					rect.Fill = new SolidColorBrush(Colors.Blue) { Opacity = 0.1 };
				}
				pageHolder.Fields.Children.Add(rect);
				Canvas.SetLeft(rect, field.Rect.X);
				Canvas.SetTop(rect, page.CanvasRect.Height - field.Rect.Y - field.Rect.Height);


			}
			pageHolder.PageCanvas.Children.Add(pageHolder.Fields);
			Canvas.SetLeft(pageHolder.Fields, 0);
			Canvas.SetTop(pageHolder.Fields, 0);

			pageHolder.Highlights = new Canvas() {
				Width = page.CanvasRect.Width,
				Height = page.CanvasRect.Height
			};
			pageHolder.PageCanvas.Children.Add(pageHolder.Highlights);
			Canvas.SetLeft(pageHolder.Highlights, 0);
			Canvas.SetTop(pageHolder.Highlights, 0);

			return pageHolder;
		}

		private object GetFieldTooltip(WPFCanvasField field) {
			return field.Name + (!string.IsNullOrEmpty(field.Tooltip) ? $"\n\n{field.Tooltip}" : "");
		}

		public string OverlayText {
			get {
				return DesignerViewer.OverlayText;
			}
			set {
				if (!string.IsNullOrWhiteSpace(value)) {
					DesignerViewer.OverlayText = value;
					DesignerViewer.OverlayVisibility = Visibility.Visible;
				}
				else {
					DesignerViewer.OverlayText = "";
					DesignerViewer.OverlayVisibility = Visibility.Hidden;
				}
			}
		}

		private int IncrementPage(int increment) {
			int initialPageIndex = currentDesignerPageIndex;
			PageNumber.Text = (int.Parse(PageNumber.Text) + increment).Clamp(designerDocument != null ? 1 : 0, designerDocument != null ? designerDocument.Pages.Count : 0).ToString();
			SetDesignerPage();
			int finalPageIndex = currentDesignerPageIndex;
			return finalPageIndex - initialPageIndex;
		}

		private void CanvasPreviousPage(object? sender, RoutedEventArgs e) {
			IncrementPage(-1);
		}

		private void CanvasNextPage(object? sender, RoutedEventArgs e) {
			IncrementPage(1);
		}

		// Navigation commands
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

		private void OnPreviewMouseWheel(object? sender, MouseWheelEventArgs e) {
			if (Keyboard.Modifiers == ModifierKeys.None) { // Only try and scroll if no modifiers are pressed
				//Console.WriteLine($"Hello {e.Handled} ({e.Delta}) {DesignerViewer.CanvasView.VerticalOffset} / {DesignerViewer.CanvasView.ScrollableHeight}");
				if (designerDocument != null && designerDocument.PageCount > 0) {
					//Console.WriteLine("Here");

					int change = (int)Math.Round(Math.Max(1, Math.Abs(1f * e.Delta) / 120f));
					//Console.WriteLine($"e.delta={e.Delta}, change={change}, verticalOffset={DesignerViewer.CanvasView.VerticalOffset}, scrollableHeight={DesignerViewer.CanvasView.ScrollableHeight}");
					if (e.Delta > 0 && Math.Abs(DesignerViewer.CanvasView.VerticalOffset) < 1e-9) {
						//Console.WriteLine("Up");
						int pageDelta = IncrementPage(-change);
						if (pageDelta != 0) {
							DesignerViewer.CanvasView.ScrollToVerticalOffset(DesignerViewer.CanvasView.ScrollableHeight); // Scroll to bottom
						}
						e.Handled = true;
					}
					else if (e.Delta < 0 && Math.Abs(DesignerViewer.CanvasView.VerticalOffset - DesignerViewer.CanvasView.ScrollableHeight) < 1e-9) {
						//Console.WriteLine("Down");
						int pageDelta = IncrementPage(change);
						if (pageDelta != 0) {
							DesignerViewer.CanvasView.ScrollToVerticalOffset(0); // Scroll to top
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
			else if(e.Key == Key.F) {
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
		private void ValidatePageNumber(object? sender, TextCompositionEventArgs e) {
			e.Handled = !digitRegex.IsMatch(e.Text);
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
		private void ValidateCanvasZoom(object? sender, TextCompositionEventArgs e) {
			e.Handled = !percentageRegex.IsMatch(e.Text);
		}

		private void CanvasZoomPreviewKeyDown(object? sender, KeyEventArgs e) {
			if (e.Key == Key.Enter) {
				e.Handled = true;
				DesignerViewer.Focus();
			}
		}

		private void UpdateCanvasZoomLevel(object? sender, RoutedEventArgs e) {
			Match match = Regex.Match(CanvasZoomLevelText.Text, "^(?<number>[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)%?$");

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

		void OnCanvasScaleChanged(object? sender, EventArgs e) { // TODO Utilise this function somewhere
			if (currentPage?.Highlights != null) {
				foreach (Shape shape in currentPage.Highlights.Children.OfType<Shape>()) {
					shape.StrokeThickness = DesignerHighlightStrokeThickness / DesignerViewer.Scale;
				}
			}

			UpdateCanvasZoomText();
		}

		void UpdateCanvasZoomText() {
			CanvasZoomLevelText.Text = string.Format("{0:0.#}%", DesignerViewer.Scale * 100.0);
		}

		void OnCanvasDoubleClick(object? sender, MouseButtonEventArgs e) {
			if (currentPageCanvas != null && DesignerViewer.CanvasContent is IInputElement) {
				//Point pos = e.GetPosition((Canvas)DesignerViewer.CanvasContent);
				Point pos = e.GetPosition(DesignerViewer.CanvasContent);

				Dictionary<object, RegisteredAreas> areas = currentPageCanvas.GetAreas(pos);

				if (areas.Count > 0) {
					CanvasAreaDoubleClick?.Invoke(sender, areas);
				}
			}
		}

		public void Highlight(object[]? drawnObjects, bool scrollToArea = false, bool zoomToArea = false) {
			if (Visibility == Visibility.Visible && designerDocument != null && currentPageCanvas != null && currentPage?.Highlights != null && designerDocument.Pages.Count > 0) {
				try {
					currentPage.Highlights.Children.Clear();
					currentPage.Highlights.Visibility = Visibility.Hidden;

					//object currentDrawnObject = designerGenerator?.DrawingMapper?.GetDrawnObject(textEditor.Document.GetLineByNumber(textEditor.TextArea.Caret.Line));
					if (drawnObjects != null && drawnObjects.Length > 0) {
						currentPage.Highlights.Visibility = Visibility.Visible;
						//if (currentDesignerPage.Areas.Where(kv => kv.Value == currentDrawnObject).Select(kv => kv.Key).MaxBy(r => r.Area) is Rectangle rectangle) {
						HashSet<SharpSheets.Layouts.Rectangle> highlightedRects = new HashSet<SharpSheets.Layouts.Rectangle>();
						foreach (RegisteredAreas areas in drawnObjects.SelectMany(d => currentPageCanvas.GetAreas(d)).OrderBy(r => r.Total.Area)) {
							// Draw smallest rectangles first. If a new rectangle completely contains a previously drawn rectangle, ignore it.
							// The larger rects are needed as targets for going from the designer back to the document
							if (areas.Original.Width >= 0 && areas.Original.Height >= 0 && !highlightedRects.Any(r => areas.Original.Contains(r))) {
								highlightedRects.Add(areas.Original);

								if (areas.Inner.Length > 0) {
									foreach (SharpSheets.Layouts.Rectangle innerRect in areas.Inner) {
										if(innerRect.Width < 0f || innerRect.Height < 0f) { continue; } // Ignore malformed areas
										System.Windows.Shapes.Rectangle adjustedHighlight = new System.Windows.Shapes.Rectangle() {
											Width = innerRect.Width,
											Height = innerRect.Height,
											Stroke = new SolidColorBrush(Colors.Orange) { Opacity = 0.6 },
											StrokeThickness = DesignerHighlightStrokeThickness / DesignerViewer.Scale
										};
										currentPage.Highlights.Children.Add(adjustedHighlight);
										Canvas.SetLeft(adjustedHighlight, innerRect.Left);
										Canvas.SetTop(adjustedHighlight, currentPageCanvas.CanvasRect.Height - innerRect.Top);
									}
								}

								if (areas.Adjusted is not null && areas.Original != areas.Adjusted && areas.Adjusted.Width >= 0f && areas.Adjusted.Height >= 0f) {
									System.Windows.Shapes.Rectangle adjustedHighlight = new System.Windows.Shapes.Rectangle() {
										Width = areas.Adjusted.Width,
										Height = areas.Adjusted.Height,
										Stroke = new SolidColorBrush(Colors.Cyan) { Opacity = 0.75 },
										StrokeThickness = DesignerHighlightStrokeThickness / DesignerViewer.Scale
									};
									currentPage.Highlights.Children.Add(adjustedHighlight);
									Canvas.SetLeft(adjustedHighlight, areas.Adjusted.Left);
									Canvas.SetTop(adjustedHighlight, currentPageCanvas.CanvasRect.Height - areas.Adjusted.Top);
								}

								if (areas.Original.Width >= 0f && areas.Original.Height >= 0f) {
									System.Windows.Shapes.Rectangle originalHighlight = new System.Windows.Shapes.Rectangle() {
										Width = areas.Original.Width,
										Height = areas.Original.Height,
										Stroke = new SolidColorBrush(Colors.Lime) { Opacity = 0.75 },
										StrokeThickness = DesignerHighlightStrokeThickness / DesignerViewer.Scale
									};
									currentPage.Highlights.Children.Add(originalHighlight);
									Canvas.SetLeft(originalHighlight, areas.Original.Left);
									Canvas.SetTop(originalHighlight, currentPageCanvas.CanvasRect.Height - areas.Original.Top);
								}
							}
						}

						if(scrollToArea && highlightedRects.Count > 0) {
							SharpSheets.Layouts.Rectangle wholeArea = SharpSheets.Layouts.Rectangle.GetCommonRectangle(highlightedRects);
							float canvasHeight = currentPageCanvas.CanvasRect.Height;
							Console.WriteLine(currentPageCanvas.CanvasRect);
							DesignerViewer.ShowArea(wholeArea.Left, canvasHeight - wholeArea.Bottom, wholeArea.Right, canvasHeight - wholeArea.Top, zoomToArea);
						}
					}
				}
				catch (InvalidOperationException) { } // TODO Why...?
			}
		}

		public bool DisplayObject(object drawnObject, bool zoomToArea) {
			if (Visibility == Visibility.Visible && designerDocument != null && currentPage?.Highlights != null && designerDocument.Pages.Count > 0) {
				int? objectPageIndex = designerDocument.GetOwnerPage(drawnObject);

				if (objectPageIndex.HasValue) {
					if(objectPageIndex.Value != currentDesignerPageIndex) {
						SetDesignerPageByIndex(objectPageIndex.Value);
					}

					Highlight(new object[] { drawnObject }, scrollToArea: true, zoomToArea: zoomToArea);

					return true; // Successfully displayed drawnObject
				}
			}

			return false; // Could not display drawnObject
		}

		private void OnDesignerDisplayFieldsChanged(object? sender, EventArgs e) {
			if (currentPage?.Fields != null) {
				currentPage.Fields.Visibility = SharpDataManager.Instance.DesignerDisplayFields ? Visibility.Visible : Visibility.Hidden;
			}
		}

		private Canvas MakeNewFieldCanvas(double width, double height) {
			return new Canvas() {
					Width = width,
					Height = height,
					Visibility = SharpDataManager.Instance.DesignerDisplayFields ? Visibility.Visible : Visibility.Hidden
				};
		}

		public class DesignerPageHolder {
			public SharpWPFDrawingCanvas? Page { get; set; }

			public Canvas? PageCanvas { get; set; }

			public Canvas? Highlights { get; set; }
			public Canvas? Fields { get; set; }
		}
	}

	
}
