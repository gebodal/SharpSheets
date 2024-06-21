using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit.Utils;
using System;
using System.Linq;

namespace SharpEditorAvalonia.Designer {

	// FrameworkElement -> Control
	// MouseButtonEventHandler -> EventHandler<PointerPressedEventArgs>

	public partial class CanvasViewer : UserControl {

		private readonly double[] canvasZoomLevels = new double[] { 0.01, 0.0625, 0.125, 0.25, 0.333, 0.5, 0.667, 0.75, 1, 1.25, 1.5, 2, 3, 4, 6, 8, 12, 16, 24, 32, 64 };

		public event EventHandler? ScaleChanged;
		public event EventHandler<PointerPressedEventArgs>? CanvasLeftDoubleClick;

		public bool WholePageZoomOn { get; private set; } = true;
		private bool hasValidContent = false;

		//public Transform CanvasLayoutTransform { get { return TestBlock.LayoutTransform; } }
		public Control? CanvasContent {
			get {
				return layoutTransformWrapper.Child;
			}
			set {
				layoutTransformWrapper.Child = value;
				hasValidContent = value != null;
				if (WholePageZoomOn) {
					ZoomCanvasToWholePage();
				}
			}
		}

		public string OverlayText {
			get {
				return CanvasOverlay.Text ?? "";
			}
			set {
				CanvasOverlay.Text = value;
			}
		}
		public bool OverlayVisible {
			get {
				return CanvasOverlay.IsVisible;
			}
			set {
				CanvasOverlay.IsVisible = value;
			}
		}

		public CanvasViewer() {
			InitializeComponent();

			scaleTransform1 = new ScaleTransform(1.0, 1.0);
			layoutTransformWrapper.LayoutTransform = scaleTransform1;

			this.Loaded += OnLoaded;
			this.Unloaded += OnUnloaded;

			// Seems to work
			CanvasViewScroller.AddHandler(InputElement.PointerWheelChangedEvent, OnCanvasPointerWheelChanged, RoutingStrategies.Tunnel, false);
		}

		private readonly ScaleTransform scaleTransform1;
		public double Scale {
			get {
				return scaleTransform1.ScaleX;
			}
			private set {
				double newScale = value > 0 ? value : 1f;
				scaleTransform1.ScaleX = newScale;
				scaleTransform1.ScaleY = newScale;

				ScaleChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public void SetScale(double scale) {
			Scale = scale;
			WholePageZoomOn = false;
		}

		//private double? canvasViewHorizontalOffset = null;
		//private double? canvasViewVerticalOffset = null;
		private Vector? canvasViewOffset = null;
		private void OnLoaded(object? sender, RoutedEventArgs e) {
			if (WholePageZoomOn) {
				ZoomCanvasToWholePage();
			}
			else {
				/*
				if (canvasViewHorizontalOffset.HasValue) {
					CanvasView.Offset
					CanvasView.ScrollToHorizontalOffset(canvasViewHorizontalOffset.Value);
				}
				if (canvasViewVerticalOffset.HasValue) {
					CanvasView.ScrollToVerticalOffset(canvasViewVerticalOffset.Value);
				}
				*/
				if (canvasViewOffset.HasValue) {
					CanvasViewScroller.Offset = canvasViewOffset.Value;
				}
			}
			/*
			canvasViewHorizontalOffset = null;
			canvasViewHorizontalOffset = null;
			*/
			canvasViewOffset = null;
		}
		private void OnUnloaded(object? sender, RoutedEventArgs e) {
			if (!WholePageZoomOn) {
				/*
				canvasViewHorizontalOffset = CanvasView.HorizontalOffset;
				canvasViewVerticalOffset = CanvasView.VerticalOffset;
				*/
				canvasViewOffset = CanvasViewScroller.Offset;
			}
		}

		// React to ctrl + mouse wheel to zoom in and out
		private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e) {
			if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && CanvasContent is Control canvas) {

				// https://stackoverflow.com/a/26746592/11002708
				Point mouseAtImage = e.GetPosition(canvas); // ScrollViewer_CanvasMain.TranslatePoint(middleOfScrollViewer, Canvas_Main);
				Point mouseAtScrollViewer = e.GetPosition(CanvasViewScroller);

				this.IncrementCanvasZoom(e.Delta.Y > 0);

				AlignCanvas(mouseAtImage, mouseAtScrollViewer);

				e.Handled = true;
			}
		}

		private void AlignCanvas(Point positionAtImage, Point positionAtScrollViewer) {
			// TODO Implement align canvas

			if (CanvasContent is Control canvas) {
				// this step is critical for offset
				//CanvasViewScroller.ScrollToHorizontalOffset(0);
				//CanvasViewScroller.ScrollToVerticalOffset(0);
				CanvasViewScroller.Offset = new Vector(0, 0);
				this.UpdateLayout();
				
				Point? translatedPoint = canvas.TranslatePoint(positionAtImage, CanvasViewScroller);
				if (translatedPoint.HasValue) {
					Vector offset = translatedPoint.Value - positionAtScrollViewer; // (Vector)middleOfScrollViewer;
					//CanvasViewScroller.ScrollToHorizontalOffset(offset.X);
					//CanvasViewScroller.ScrollToVerticalOffset(offset.Y);
					CanvasViewScroller.Offset = offset;
				}
				this.UpdateLayout();
			}
		}

		public void UpdateCanvasZoom(bool increase) {
			if (CanvasContent is Control canvas) {
				Point positionAtScrollViewer = new Point(CanvasViewScroller.Bounds.Width / 2, CanvasViewScroller.Bounds.Height / 2); // CanvasView.ActualWidth / 2, CanvasView.ActualHeight / 2
				Point? positionAtImage = CanvasViewScroller.TranslatePoint(positionAtScrollViewer, canvas);

				IncrementCanvasZoom(increase);

				if (positionAtImage.HasValue) {
					AlignCanvas(positionAtImage.Value, positionAtScrollViewer);
				}
			}
		}

		private void IncrementCanvasZoom(bool increase) {
			double currentScale = Scale;

			if (CanvasContent is Control canvas) {
				double scaleX = CanvasViewScroller.Viewport.Width / canvas.Width; // CanvasView.ViewportWidth / canvas.Width;
				double scaleY = CanvasViewScroller.Viewport.Height / canvas.Height; // CanvasView.ViewportHeight / canvas.Height;

				double[] zoomLevels = canvasZoomLevels.Concat(new double[] { scaleX, scaleY }).OrderBy(d => d).ToArray();

				if (increase) {
					int i = 0;
					while (i < zoomLevels.Length && currentScale >= zoomLevels[i]) i++;

					if (i < zoomLevels.Length) {
						//double newSize = Math.Min(MAX_CANVAS_ZOOM, currentScale + CANVAS_ZOOM_DELTA);
						double newSize = zoomLevels[i];
						SetScale(newSize);
						//Scale = newSize;
					}
				}
				else {
					int i = zoomLevels.Length - 1;
					while (i >= 0 && currentScale <= zoomLevels[i]) i--;

					if (i >= 0) {
						//double newSize = Math.Max(MIN_CANVAS_ZOOM, currentScale - CANVAS_ZOOM_DELTA);
						double newSize = zoomLevels[i];
						SetScale(newSize);
						//Scale = newSize;
					}
				}

				//UpdateCanvasZoomText();
			}
		}

		private double WholePageScale(Control canvas) {
			double scaleX = CanvasViewScroller.Bounds.Width / canvas.Width; // CanvasView.ActualWidth / canvas.Width;
			double scaleY = CanvasViewScroller.Bounds.Height / canvas.Height; // CanvasView.ActualHeight / canvas.Height;
			double scale = Math.Min(scaleX, scaleY);
			return scale;
		}

		public void ZoomCanvasToWholePage() {
			//Console.WriteLine($"View size: {CanvasView.ActualHeight}, Canvas size: {TestBlock.Height}");
			if (hasValidContent && CanvasContent is Control canvas) {
				double scale = WholePageScale(canvas);
				//SetCanvasScale(scale);
				Scale = scale;
			}
			else {
				Scale = 1.0f;
			}
			WholePageZoomOn = true;

			//UpdateCanvasZoomText();
		}

		//private static readonly double ShowAreaBorderFactor = 0.1;
		public void ShowArea(double x1, double y1, double x2, double y2, bool zoomToArea) {
			// TODO Implement show area
			/*
			if (!hasValidContent) { return; }

			double xMin = Math.Min(x1, x2);
			double xMax = Math.Max(x1, x2);
			double yMin = Math.Min(y1, y2);
			double yMax = Math.Max(y1, y2);
			double areaWidth = xMax - xMin;
			double areaHeight = yMax - yMin;
			double x = xMin + 0.5 * (xMax - xMin); // Centre x
			double y = yMin + 0.5 * (yMax - yMin); // Centre y

			double border = ShowAreaBorderFactor * Math.Max(areaWidth, areaHeight);
			double areaXScale = CanvasView.ActualWidth / (2 * border + areaWidth);
			double areaYScale = CanvasView.ActualHeight / (2 * border + areaHeight);
			double areaScale = Math.Min(areaXScale, areaYScale);

			if (zoomToArea || areaScale < Scale) {
				double wholePageScale = WholePageScale((Control)CanvasContent);
				double finalScale = Math.Max(areaScale, wholePageScale);
				SetScale(finalScale);
			}

			//Console.WriteLine($"CanvasView > x: {CanvasView.HorizontalOffset:F3}, y: {CanvasView.VerticalOffset:F3}, w: {CanvasView.ActualWidth:F3}, h: {CanvasView.ActualHeight:F3}");
			//Console.WriteLine($"CanvasView.Scrollable > w: {CanvasView.ScrollableWidth:F3}, h: {CanvasView.ScrollableHeight:F3}");
			//Console.WriteLine($"CanvasContent > w: {CanvasContent.Width:F3} ({CanvasContent.ActualWidth:F3}), h: {CanvasContent.Height:F3} ({CanvasContent.ActualHeight:F3})");
			//Console.WriteLine($"CanvasContent (scaled) > w: {Scale * CanvasContent.Width:F3}, h: {Scale * CanvasContent.Height:F3}");

			double horizontalCenter = ((CanvasView.ViewportWidth / 2) - (areaWidth * Scale / 2));
			double horizontalOffset = Scale * (x-0.5*areaWidth) - horizontalCenter;

			double verticalCenter = ((CanvasView.ViewportHeight / 2) - (areaHeight * Scale / 2));
			double verticalOffset = Scale * (y-0.5*areaHeight) - verticalCenter;

			CanvasView.ScrollToHorizontalOffset(horizontalOffset);
			CanvasView.ScrollToVerticalOffset(verticalOffset);
			*/
		}

		private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e) {
			if (WholePageZoomOn) {
				ZoomCanvasToWholePage();
			}
		}

		//Point? lastCenterPositionOnTarget;
		//Point? lastMousePositionOnTarget;
		Point? lastDragPoint;

		void OnCanvasPointerMoved(object? sender, PointerEventArgs e) {
			if (lastDragPoint.HasValue) {
				Point posNow = e.GetPosition(CanvasViewScroller);

				double dX = posNow.X - lastDragPoint.Value.X;
				double dY = posNow.Y - lastDragPoint.Value.Y;

				lastDragPoint = posNow;

				//CanvasViewScroller.ScrollToHorizontalOffset(CanvasViewScroller.HorizontalOffset - dX);
				//CanvasViewScroller.ScrollToVerticalOffset(CanvasViewScroller.VerticalOffset - dY);
				CanvasViewScroller.Offset -= new Vector(dX, dY);
			}
		}

		void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e) {
			PointerPoint point = e.GetCurrentPoint(CanvasViewScroller);

			if (point.Properties.IsLeftButtonPressed) {
				if (e.ClickCount == 2) {
					CanvasLeftDoubleClick?.Invoke(this, e);
					e.Handled = true;
				}
				else if (e.ClickCount == 1) {
					Point mousePos = point.Position;
					//Console.WriteLine($"OnCanvasMouseLeftButtonDown: {mousePos}");
					if (mousePos.X <= CanvasViewScroller.Viewport.Width && mousePos.Y < CanvasViewScroller.Viewport.Height) { // Make sure we still can use the scrollbars
						CanvasViewScroller.Cursor = new Cursor(StandardCursorType.SizeAll); // Cursors.SizeAll;
						lastDragPoint = mousePos;
						//Mouse.Capture(CanvasView);
						e.Pointer.Capture(CanvasViewScroller);
					}
				}
			}
		}

		void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e) {
			CanvasViewScroller.Cursor = new Cursor(StandardCursorType.Arrow); // Cursors.Arrow;
			//CanvasViewScroller.ReleaseMouseCapture();
			CanvasViewScroller.ReleasePointerCapture(e.Pointer);
			lastDragPoint = null;
		}

		public void Reset() {
			CanvasContent = new Canvas() { Width = 100, Height = 100 }; // TestBlock
			hasValidContent = false;
			WholePageZoomOn = true;
		}

		#region Forwarded ScrollViewer functions
		public double VerticalOffset {
			get {
				return CanvasViewScroller.Offset.Y;
			}
		}

		public double ScrollableHeight {
			get {
				return CanvasViewScroller.ScrollBarMaximum.Y;
			}
		}

		public void ScrollToVerticalOffset(double offset) {
			CanvasViewScroller.Offset = new Vector(CanvasViewScroller.Offset.X, offset);
		}
		#endregion
	}
}
