using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Utils;
using System;
using System.Linq;

namespace SharpEditor.Designer {

	// FrameworkElement -> Control
	// MouseButtonEventHandler -> EventHandler<PointerPressedEventArgs>
	// .ActualWidth -> .Bounds.Width

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

		public CanvasViewer() {
			InitializeComponent();

			dpiScaleTransform = new ScaleTransform(1.0, 1.0);
			scaleTransform1 = new ScaleTransform(1.0, 1.0);
			layoutTransformWrapper.LayoutTransform = new TransformGroup() {
				Children = {
					dpiScaleTransform,
					scaleTransform1
				}
			};

			DPI = 96; // Default value

			this.Loaded += OnLoaded;
			this.Unloaded += OnUnloaded;

			// Seems to work
			CanvasViewScroller.AddHandler(InputElement.PointerWheelChangedEvent, OnCanvasPointerWheelChanged, RoutingStrategies.Tunnel, false);
		}

		private int dpi;
		private readonly ScaleTransform dpiScaleTransform;
		public int DPI {
			get {
				return dpi;
			}
			set {
				// Canvas size measured in points (1/72 inch)
				dpi = value;

				double newScale = dpi / 72.0;
				dpiScaleTransform.ScaleX = newScale;
				dpiScaleTransform.ScaleY = newScale;

				WholePageZoomOn = false;
				ScaleChanged?.Invoke(this, EventArgs.Empty);
			}
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
			WholePageZoomOn = false;
			Scale = scale;
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
				(double scaleX, double scaleY) = WholePageScales(canvas);

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

		private (double x, double y) WholePageScales(Control canvas) {
			double currentDPIscaling = dpiScaleTransform.ScaleX;

			double scaleX = CanvasViewScroller.Bounds.Width / canvas.Width; // CanvasView.ActualWidth / canvas.Width;
			double scaleY = CanvasViewScroller.Bounds.Height / canvas.Height; // CanvasView.ActualHeight / canvas.Height;
			//return (scaleX * 0.99, scaleY * 0.99);
			return (scaleX * 0.999 / currentDPIscaling, scaleY * 0.999 / currentDPIscaling);
		}

		private double WholePageScale(Control canvas) {
			(double scaleX, double scaleY) = WholePageScales(canvas);
			double scale = Math.Min(scaleX, scaleY);
			return scale;
		}

		public void ZoomCanvasToWholePage() {
			//Console.WriteLine($"View size: {CanvasView.ActualHeight}, Canvas size: {TestBlock.Height}");
			WholePageZoomOn = true;
			if (hasValidContent && CanvasContent is Control canvas) {
				double scale = WholePageScale(canvas);
				//SetCanvasScale(scale);
				Scale = scale;
			}
			else {
				Scale = 1.0f;
			}

			//UpdateCanvasZoomText();
		}

		private static readonly double ShowAreaBorderFactor = 0.1;
		public void ShowArea(double x1, double y1, double x2, double y2, bool zoomToArea) {
			if (!hasValidContent) { return; }

			Dispatcher.UIThread.Invoke(() => { // Send to dispatcher so that whole operation is atomic with regards to UI (no flicker)
				double xMin = Math.Min(x1, x2);
				double xMax = Math.Max(x1, x2);
				double yMin = Math.Min(y1, y2);
				double yMax = Math.Max(y1, y2);
				double areaWidth = xMax - xMin;
				double areaHeight = yMax - yMin;
				double cx = xMin + 0.5 * (xMax - xMin); // Centre x
				double cy = yMin + 0.5 * (yMax - yMin); // Centre y

				double border = ShowAreaBorderFactor * Math.Max(areaWidth, areaHeight);
				double areaXScale = CanvasViewScroller.Bounds.Width / (2 * border + areaWidth);
				double areaYScale = CanvasViewScroller.Bounds.Height / (2 * border + areaHeight);
				double areaScale = Math.Min(areaXScale, areaYScale);

				double finalScale = Scale;
				if (zoomToArea || areaScale < Scale) {
					double wholePageScale = WholePageScale(CanvasContent!);
					finalScale = Math.Max(areaScale, wholePageScale); // Don't zoom out more than "whole page" level
				}

				//Console.WriteLine($"CanvasView > x: {CanvasView.HorizontalOffset:F3}, y: {CanvasView.VerticalOffset:F3}, w: {CanvasView.ActualWidth:F3}, h: {CanvasView.ActualHeight:F3}");
				//Console.WriteLine($"CanvasView.Scrollable > w: {CanvasView.ScrollableWidth:F3}, h: {CanvasView.ScrollableHeight:F3}");
				//Console.WriteLine($"CanvasContent > w: {CanvasContent.Width:F3} ({CanvasContent.ActualWidth:F3}), h: {CanvasContent.Height:F3} ({CanvasContent.ActualHeight:F3})");
				//Console.WriteLine($"CanvasContent (scaled) > w: {Scale * CanvasContent.Width:F3}, h: {Scale * CanvasContent.Height:F3}");

				double horizontalCenter = ((CanvasViewScroller.Viewport.Width / 2) - (areaWidth * finalScale / 2));
				double horizontalOffset = finalScale * (cx - 0.5 * areaWidth) - horizontalCenter;

				double verticalCenter = ((CanvasViewScroller.Viewport.Height / 2) - (areaHeight * finalScale / 2));
				double verticalOffset = finalScale * (cy - 0.5 * areaHeight) - verticalCenter;

				//CanvasViewScroller.ScrollToHorizontalOffset(horizontalOffset);
				//CanvasViewScroller.ScrollToVerticalOffset(verticalOffset);

				//Console.WriteLine($"({horizontalOffset}, {verticalOffset})");

				SetScale(finalScale);
				// Why does this need to be called twice to not break in some cases?
				CanvasViewScroller.Offset = new Vector(horizontalOffset, verticalOffset);
				CanvasViewScroller.Offset = new Vector(horizontalOffset, verticalOffset); // Second call seems to be needed for some reason...
			}, DispatcherPriority.Background);
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
