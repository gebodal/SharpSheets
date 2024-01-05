using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SharpEditor {
	/// <summary>
	/// Interaction logic for UserControl1.xaml
	/// </summary>
	public partial class CanvasViewer : UserControl {

		private readonly double[] canvasZoomLevels = new double[] { 0.01, 0.0625, 0.125, 0.25, 0.333, 0.5, 0.667, 0.75, 1, 1.25, 1.5, 2, 3, 4, 6, 8, 12, 16, 24, 32, 64 };

		public event EventHandler? ScaleChanged;
		public event MouseButtonEventHandler? CanvasLeftDoubleClick;

		public bool WholePageZoomOn { get; private set; } = true;
		private bool hasValidContent = false;

		public Transform CanvasLayoutTransform { get { return TestBlock.LayoutTransform; } }
		public FrameworkElement? CanvasContent {
			get {
				return CanvasView.Content as FrameworkElement;
			}
			set {
				CanvasView.Content = value;
				hasValidContent = value != null;
				if (WholePageZoomOn) {
					ZoomCanvasToWholePage();
				}
			}
		}

		public string OverlayText {
			get {
				return CanvasOverlay.Text;
			}
			set {
				CanvasOverlay.Text = value;
			}
		}
		public Visibility OverlayVisibility {
			get {
				return CanvasOverlay.Visibility;
			}
			set {
				CanvasOverlay.Visibility = value;
			}
		}

		public CanvasViewer() {
			InitializeComponent();

			this.Loaded += OnLoaded;
			this.Unloaded += OnUnloaded;
		}

		public double Scale {
			get {
				return scaleTransform.ScaleX;
			}
			private set {
				double newScale = value > 0 ? value : 1f;
				scaleTransform.ScaleX = newScale;
				scaleTransform.ScaleY = newScale;

				ScaleChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public void SetScale(double scale) {
			Scale = scale;
			WholePageZoomOn = false;
		}

		private double? canvasViewHorizontalOffset = null;
		private double? canvasViewVerticalOffset = null;
		private void OnLoaded(object? sender, RoutedEventArgs e) {
			if (WholePageZoomOn) {
				ZoomCanvasToWholePage();
			}
			else {
				if (canvasViewHorizontalOffset.HasValue) {
					CanvasView.ScrollToHorizontalOffset(canvasViewHorizontalOffset.Value);
				}
				if (canvasViewVerticalOffset.HasValue) {
					CanvasView.ScrollToVerticalOffset(canvasViewVerticalOffset.Value);
				}
			}
			canvasViewHorizontalOffset = null;
			canvasViewHorizontalOffset = null;
		}
		private void OnUnloaded(object? sender, RoutedEventArgs e) {
			if (!WholePageZoomOn) {
				canvasViewHorizontalOffset = CanvasView.HorizontalOffset;
				canvasViewVerticalOffset = CanvasView.VerticalOffset;
			}
		}

		// React to ctrl + mouse wheel to zoom in and out
		private void OnCanvasPreviewMouseWheel(object? sender, MouseWheelEventArgs e) {
			if (Keyboard.Modifiers == ModifierKeys.Control) {
				FrameworkElement canvas = (FrameworkElement)CanvasView.Content;

				// https://stackoverflow.com/a/26746592/11002708
				Point mouseAtImage = e.GetPosition(canvas); // ScrollViewer_CanvasMain.TranslatePoint(middleOfScrollViewer, Canvas_Main);
				Point mouseAtScrollViewer = e.GetPosition(CanvasView);

				this.IncrementCanvasZoom(e.Delta > 0);

				AlignCanvas(mouseAtImage, mouseAtScrollViewer);

				e.Handled = true;
			}
		}

		private void AlignCanvas(Point positionAtImage, Point positionAtScrollViewer) {
			FrameworkElement canvas = (FrameworkElement)CanvasView.Content;

			// this step is critical for offset
			CanvasView.ScrollToHorizontalOffset(0);
			CanvasView.ScrollToVerticalOffset(0);
			this.UpdateLayout();

			Vector offset = canvas.TranslatePoint(positionAtImage, CanvasView) - positionAtScrollViewer; // (Vector)middleOfScrollViewer;
			CanvasView.ScrollToHorizontalOffset(offset.X);
			CanvasView.ScrollToVerticalOffset(offset.Y);
			this.UpdateLayout();
		}

		public void UpdateCanvasZoom(bool increase) {
			FrameworkElement canvas = (FrameworkElement)CanvasView.Content;

			Point positionAtScrollViewer = new Point(CanvasView.ActualWidth / 2, CanvasView.ActualHeight / 2);
			Point positionAtImage = CanvasView.TranslatePoint(positionAtScrollViewer, canvas);

			IncrementCanvasZoom(increase);

			AlignCanvas(positionAtImage, positionAtScrollViewer);
		}

		private void IncrementCanvasZoom(bool increase) {
			double currentScale = Scale;

			FrameworkElement canvas = (FrameworkElement)CanvasView.Content;
			double scaleX = CanvasView.ViewportWidth / canvas.Width;
			double scaleY = CanvasView.ViewportHeight / canvas.Height;

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

		private double WholePageScale(FrameworkElement canvas) {
			double scaleX = CanvasView.ActualWidth / canvas.Width;
			double scaleY = CanvasView.ActualHeight / canvas.Height;
			double scale = Math.Min(scaleX, scaleY);
			return scale;
		}

		public void ZoomCanvasToWholePage() {
			//Console.WriteLine($"View size: {CanvasView.ActualHeight}, Canvas size: {TestBlock.Height}");
			FrameworkElement canvas = (FrameworkElement)CanvasView.Content;
			if (hasValidContent && canvas != null) {
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

		private static readonly double ShowAreaBorderFactor = 0.1;
		public void ShowArea(double x1, double y1, double x2, double y2, bool zoomToArea) {
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
				double wholePageScale = WholePageScale((FrameworkElement)CanvasView.Content);
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
		}

		private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e) {
			if (WholePageZoomOn) {
				ZoomCanvasToWholePage();
			}
		}

		//Point? lastCenterPositionOnTarget;
		//Point? lastMousePositionOnTarget;
		Point? lastDragPoint;

		void OnCanvasMouseMove(object? sender, MouseEventArgs e) {
			if (lastDragPoint.HasValue) {
				Point posNow = e.GetPosition(CanvasView);

				double dX = posNow.X - lastDragPoint.Value.X;
				double dY = posNow.Y - lastDragPoint.Value.Y;

				lastDragPoint = posNow;

				CanvasView.ScrollToHorizontalOffset(CanvasView.HorizontalOffset - dX);
				CanvasView.ScrollToVerticalOffset(CanvasView.VerticalOffset - dY);
			}
		}

		void OnCanvasPreviewMouseLeftButtonDown(object? sender, MouseButtonEventArgs e) {
			if (e.ChangedButton == MouseButton.Left) {
				if (e.ClickCount == 2) {
					CanvasLeftDoubleClick?.Invoke(this, e);
					e.Handled = true;
				}
				else if (e.ClickCount == 1) {
					Point mousePos = e.GetPosition(CanvasView);
					//Console.WriteLine($"OnCanvasMouseLeftButtonDown: {mousePos}");
					if (mousePos.X <= CanvasView.ViewportWidth && mousePos.Y < CanvasView.ViewportHeight) { // Make sure we still can use the scrollbars
						CanvasView.Cursor = Cursors.SizeAll;
						lastDragPoint = mousePos;
						Mouse.Capture(CanvasView);
					}
				}
			}
		}

		void OnCanvasPreviewMouseLeftButtonUp(object? sender, MouseButtonEventArgs e) {
			CanvasView.Cursor = Cursors.Arrow;
			CanvasView.ReleaseMouseCapture();
			lastDragPoint = null;
		}

		public void Reset() {
			CanvasView.Content = TestBlock;
			hasValidContent = false;
			WholePageZoomOn = true;
		}

	}
}
