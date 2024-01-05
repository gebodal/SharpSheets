// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace SharpEditor.TextMarker {
	/// <summary>
	/// Handles the text markers for a code editor.
	/// </summary>
	public sealed class TextMarkerManager : DocumentColorizingTransformer, IBackgroundRenderer, ITextViewConnect {
		private readonly TextSegmentCollection<TextMarker> markers;
		private readonly TextArea textArea;
		private readonly TextDocument document;

		private TextMarkerManager(TextArea textArea) {
			if (textArea == null)
				throw new ArgumentNullException(nameof(textArea));
			this.textArea = textArea;
			this.document = textArea.Document;
			this.markers = new TextSegmentCollection<TextMarker>(document);
		}

		#region Installation
		public static TextMarkerManager Install(TextArea textArea) {
			TextMarkerManager manager = new TextMarkerManager(textArea);
			manager.textArea.TextView.BackgroundRenderers.Add(manager);
			manager.textArea.TextView.LineTransformers.Add(manager);
			IServiceContainer? services = (IServiceContainer?)manager.textArea.Document.ServiceProvider.GetService(typeof(IServiceContainer));
			if (services != null)
				services.AddService(typeof(TextMarkerManager), manager);
			return manager;
		}

		public static void Uninstall(TextMarkerManager manager) {
			manager.textArea.TextView.BackgroundRenderers.Remove(manager);
			manager.textArea.TextView.LineTransformers.Remove(manager);
			IServiceContainer? services = (IServiceContainer?)manager.textArea.Document.ServiceProvider.GetService(typeof(IServiceContainer));
			if (services != null)
				services.RemoveService(typeof(TextMarkerManager));
		}
		#endregion

		#region TextMarkerService
		public ITextMarker Create(int startOffset, int length) {
			if (markers == null)
				throw new InvalidOperationException("Cannot create a marker when not attached to a document");

			int textLength = document.TextLength;
			if (startOffset < 0 || startOffset > textLength)
				throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset, "Value must be between 0 and " + textLength);
			if (length < 0 || startOffset + length > textLength)
				throw new ArgumentOutOfRangeException(nameof(length), length, "length must not be negative and startOffset+length must not be after the end of the document");

			TextMarker m = new TextMarker(this, startOffset, length);
			markers.Add(m);
			// no need to mark segment for redraw: the text marker is invisible until a property is set
			return m;
		}

		public void UpdateMarkers(IEnumerable<NewTextMarker> newMarkers) {
			if (markers == null)
				throw new InvalidOperationException("Cannot create a marker when not attached to a document");

			Clear();

			int textLength = document.TextLength;

			foreach (NewTextMarker marker in newMarkers) {
				if (marker.StartOffset < 0 || marker.StartOffset > textLength)
					throw new ArgumentOutOfRangeException("StartOffset", marker.StartOffset, "Value must be between 0 and " + textLength);
				if (marker.Length < 0 || marker.StartOffset + marker.Length > textLength)
					throw new ArgumentOutOfRangeException("Length", marker.Length, "Length must not be negative and startOffset+length must not be after the end of the document");

				TextMarker m = new TextMarker(this, marker.StartOffset, marker.Length) {
					BackgroundColor = marker.BackgroundColor,
					ForegroundColor = marker.ForegroundColor,
					FontStyle = marker.FontStyle,
					FontWeight = marker.FontWeight,
					MarkerColor = marker.MarkerColor,
					MarkerTypes = marker.MarkerTypes,
					Tag = marker.Tag,
					ToolTip = marker.ToolTip
				};
				markers.Add(m);
			}
			// no need to mark segment for redraw? the text marker is invisible until a property is set
		}

		public IEnumerable<ITextMarker> GetMarkersAtOffset(int offset) {
			if (markers == null)
				return Enumerable.Empty<ITextMarker>();
			else
				return markers.FindSegmentsContaining(offset);
		}

		public IEnumerable<ITextMarker> TextMarkers {
			get { return markers ?? Enumerable.Empty<ITextMarker>(); }
		}

		public void Clear() {
			if (markers != null) {
				foreach (TextMarker m in markers.ToArray()) {
					Remove(m);
				}
			}
		}

		public void RemoveAll(Predicate<ITextMarker> predicate) {
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));
			if (markers != null) {
				foreach (TextMarker m in markers.ToArray()) {
					if (predicate(m)) {
						Remove(m);
					}
				}
			}
		}

		public void Remove(ITextMarker marker) {
			if (marker == null)
				throw new ArgumentNullException(nameof(marker));
			TextMarker? m = marker as TextMarker;
			if (markers != null && m is not null && markers.Remove(m)) {
				Redraw(m);
				m.OnDeleted();
			}
		}

		/// <summary>
		/// Redraws the specified text segment.
		/// </summary>
		internal void Redraw(ISegment segment) {
			foreach (var view in textViews) {
				view.Redraw(segment, DispatcherPriority.Normal);
			}
			if (RedrawRequested != null)
				RedrawRequested(this, EventArgs.Empty);
		}

		public event EventHandler? RedrawRequested;
		#endregion

		#region DocumentColorizingTransformer
		protected override void ColorizeLine(DocumentLine line) {
			if (markers == null)
				return;
			int lineStart = line.Offset;
			int lineEnd = lineStart + line.Length;
			foreach (TextMarker marker in markers.FindOverlappingSegments(lineStart, line.Length)) {
				Brush? foregroundBrush = null;
				if (marker.ForegroundColor != null) {
					foregroundBrush = new SolidColorBrush(marker.ForegroundColor.Value);
					foregroundBrush.Freeze();
				}
				ChangeLinePart(
					Math.Max(marker.StartOffset, lineStart),
					Math.Min(marker.EndOffset, lineEnd),
					element => {
						if (foregroundBrush != null) {
							element.TextRunProperties.SetForegroundBrush(foregroundBrush);
						}
						Typeface tf = element.TextRunProperties.Typeface;
						element.TextRunProperties.SetTypeface(new Typeface(
							tf.FontFamily,
							marker.FontStyle ?? tf.Style,
							marker.FontWeight ?? tf.Weight,
							tf.Stretch
						));
					}
				);
			}
		}
		#endregion

		#region IBackgroundRenderer
		public KnownLayer Layer {
			get {
				// draw behind selection
				return KnownLayer.Selection;
			}
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (textView == null)
				throw new ArgumentNullException(nameof(textView));
			if (drawingContext == null)
				throw new ArgumentNullException(nameof(drawingContext));
			if (markers == null || !textView.VisualLinesValid)
				return;
			var visualLines = textView.VisualLines;
			if (visualLines.Count == 0)
				return;
			int viewStart = visualLines.First().FirstDocumentLine.Offset;
			int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;
			foreach (TextMarker marker in markers.FindOverlappingSegments(viewStart, viewEnd - viewStart)) {
				if (marker.BackgroundColor != null) {
					BackgroundGeometryBuilder geoBuilder = new BackgroundGeometryBuilder();
					geoBuilder.AlignToWholePixels = true;
					geoBuilder.CornerRadius = 3;
					geoBuilder.AddSegment(textView, marker);
					Geometry geometry = geoBuilder.CreateGeometry();
					if (geometry != null) {
						Color color = marker.BackgroundColor.Value;
						SolidColorBrush brush = new SolidColorBrush(color);
						brush.Freeze();
						drawingContext.DrawGeometry(brush, null, geometry);
					}
				}
				var underlineMarkerTypes = TextMarkerTypes.SquigglyUnderline | TextMarkerTypes.NormalUnderline | TextMarkerTypes.DottedUnderline;
				if ((marker.MarkerTypes & underlineMarkerTypes) != 0) {
					foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker)) {
						Point startPoint = r.BottomLeft;
						Point endPoint = r.BottomRight;

						Brush usedBrush = new SolidColorBrush(marker.MarkerColor);
						usedBrush.Freeze();
						if ((marker.MarkerTypes & TextMarkerTypes.SquigglyUnderline) != 0) {
							double offset = 2.5;

							int count = Math.Max((int)((endPoint.X - startPoint.X) / offset) + 1, 4);

							StreamGeometry geometry = new StreamGeometry();

							using (StreamGeometryContext ctx = geometry.Open()) {
								ctx.BeginFigure(startPoint, false, false);
								ctx.PolyLineTo(CreatePoints(startPoint, endPoint, offset, count).ToArray(), true, false);
							}

							geometry.Freeze();

							Pen usedPen = new Pen(usedBrush, 1);
							usedPen.Freeze();
							drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
						}
						if ((marker.MarkerTypes & TextMarkerTypes.NormalUnderline) != 0) {
							Pen usedPen = new Pen(usedBrush, 1);
							usedPen.Freeze();
							drawingContext.DrawLine(usedPen, startPoint, endPoint);
						}
						if ((marker.MarkerTypes & TextMarkerTypes.DottedUnderline) != 0) {
							Pen usedPen = new Pen(usedBrush, 1);
							usedPen.DashStyle = DashStyles.Dot;
							usedPen.Freeze();
							drawingContext.DrawLine(usedPen, startPoint, endPoint);
						}
					}
				}
			}
		}

		IEnumerable<Point> CreatePoints(Point start, Point end, double offset, int count) {
			for (int i = 0; i < count; i++)
				yield return new Point(start.X + i * offset, start.Y - ((i + 1) % 2 == 0 ? offset : 0));
		}
		#endregion

		#region ITextViewConnect
		readonly List<TextView> textViews = new List<TextView>();

		void ITextViewConnect.AddToTextView(TextView textView) {
			if (textView != null && !textViews.Contains(textView)) {
				Debug.Assert(textView.Document == document);
				textViews.Add(textView);
			}
		}

		void ITextViewConnect.RemoveFromTextView(TextView textView) {
			if (textView != null) {
				Debug.Assert(textView.Document == document);
				textViews.Remove(textView);
			}
		}
		#endregion
	}

	public sealed class TextMarker : TextSegment, ITextMarker {
		readonly TextMarkerManager service;

		public TextMarker(TextMarkerManager manager, int startOffset, int length) {
			this.service = manager ?? throw new ArgumentNullException(nameof(manager));
			this.StartOffset = startOffset;
			this.Length = length;
			this.markerTypes = TextMarkerTypes.None;
		}

		public event EventHandler? Deleted;

		public bool IsDeleted {
			get { return !this.IsConnectedToCollection; }
		}

		public void Delete() {
			service.Remove(this);
		}

		internal void OnDeleted() {
			if (Deleted != null)
				Deleted(this, EventArgs.Empty);
		}

		void Redraw() {
			service.Redraw(this);
		}

		Color? backgroundColor;

		public Color? BackgroundColor {
			get { return backgroundColor; }
			set {
				if (backgroundColor != value) {
					backgroundColor = value;
					Redraw();
				}
			}
		}

		Color? foregroundColor;

		public Color? ForegroundColor {
			get { return foregroundColor; }
			set {
				if (foregroundColor != value) {
					foregroundColor = value;
					Redraw();
				}
			}
		}

		FontWeight? fontWeight;

		public FontWeight? FontWeight {
			get { return fontWeight; }
			set {
				if (fontWeight != value) {
					fontWeight = value;
					Redraw();
				}
			}
		}

		FontStyle? fontStyle;

		public FontStyle? FontStyle {
			get { return fontStyle; }
			set {
				if (fontStyle != value) {
					fontStyle = value;
					Redraw();
				}
			}
		}

		public object? Tag { get; set; }

		TextMarkerTypes markerTypes;

		public TextMarkerTypes MarkerTypes {
			get { return markerTypes; }
			set {
				if (markerTypes != value) {
					markerTypes = value;
					Redraw();
				}
			}
		}

		Color markerColor;

		public Color MarkerColor {
			get { return markerColor; }
			set {
				if (markerColor != value) {
					markerColor = value;
					Redraw();
				}
			}
		}

		public object? ToolTip { get; set; }
	}

	/// <summary>
	/// Helper class used for <see cref="TextMarkerManager.UpdateMarkers"/>.
	/// </summary>
	public sealed class NewTextMarker : ISegment {

		/// <summary>
		/// Gets/Sets the start offset.
		/// </summary>
		public int StartOffset { get; set; }
		public int Offset => StartOffset;

		/// <summary>
		/// Gets/Sets the end offset.
		/// </summary>
		public int EndOffset {
			get { return StartOffset + Length; }
			set {
				if (value < StartOffset)
					throw new ArgumentOutOfRangeException("EndOffset", value, "Value must be greater than StartOffset " + StartOffset);
				Length = value - StartOffset;
			}
		}

		public int Length { get; set; }

		/// <summary>
		/// Creates a new NewTextMarker instance.
		/// </summary>
		public NewTextMarker(int startOffset, int length) {
			this.StartOffset = startOffset;
			this.Length = length;
			this.MarkerTypes = TextMarkerTypes.None;
		}

		public Color? BackgroundColor { get; set; }
		public Color? ForegroundColor { get; set; }
		public FontWeight? FontWeight { get; set; }
		public FontStyle? FontStyle { get; set; }
		public object? Tag { get; set; }
		public TextMarkerTypes MarkerTypes { get; set; }
		public Color MarkerColor { get; set; }

		public object? ToolTip { get; set; }
	}
}