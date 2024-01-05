using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;

namespace SharpEditor {
	public class FoldingBackgroundRenderer : IBackgroundRenderer {

		private readonly FoldingManager foldingManager;
		public Color LineColor { get; set; } = Colors.Gray;

		public FoldingBackgroundRenderer(FoldingManager foldingManager) {
			this.foldingManager = foldingManager;
		}

		public KnownLayer Layer { get { return KnownLayer.Selection; } } // draw behind selection

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (textView == null) { throw new ArgumentNullException(nameof(textView)); }
			if (drawingContext == null) { throw new ArgumentNullException(nameof(drawingContext)); }
			if (!textView.VisualLinesValid) { return; }
			ReadOnlyCollection<VisualLine> visualLines = textView.VisualLines;
			if (visualLines.Count == 0) { return; }

			
			VisualLine firstLine = visualLines.First();
			VisualLine lastLine = visualLines.Last();
			int viewStart = firstLine.FirstDocumentLine.Offset;
			int viewEnd = lastLine.LastDocumentLine.EndOffset;

			//Console.WriteLine($"First Visual Line: {firstLine.FirstDocumentLine.LineNumber}, Last Visual Line: {lastLine.LastDocumentLine.LineNumber}");

			foreach (FoldingSection fold in foldingManager.AllFoldings.Where(f => !f.IsFolded)) {
				DocumentLine startLine = textView.Document.GetLineByOffset(fold.StartOffset);
				DocumentLine endLine = textView.Document.GetLineByOffset(fold.EndOffset);
				if (startLine.LineNumber > lastLine.LastDocumentLine.LineNumber || endLine.LineNumber < firstLine.FirstDocumentLine.LineNumber) { continue; }

				ISegment whitespace = TextUtilities.GetLeadingWhitespace(textView.Document, startLine);
				if (whitespace.Length == 0) { continue; }
				double xPos = textView.GetVisualPosition(new TextViewPosition(startLine.LineNumber, whitespace.Length + 1), VisualYPosition.Baseline).X;
				xPos -= textView.ScrollOffset.X;

				Rect? startRect = BackgroundGeometryBuilder.GetRectsForSegment(textView, startLine).Select<Rect, Rect?>(r => r).FirstOrDefault();
				Rect? endRect = BackgroundGeometryBuilder.GetRectsForSegment(textView, endLine).Select<Rect, Rect?>(r => r).FirstOrDefault();

				Point start, end;
				if (startRect.HasValue) {
					start = new Point(xPos, startRect.Value.Y + startRect.Value.Height);
				}
				else {
					start = new Point(xPos, 0);
				}

				if (endRect.HasValue) {
					end = new Point(xPos, endRect.Value.Y + endRect.Value.Height);
				}
				else {
					end = new Point(xPos, Math.Max(textView.DocumentHeight, textView.ActualHeight));
				}

				Brush brush = new SolidColorBrush(LineColor) { Opacity = 0.5 };
				brush.Freeze();
				Pen dashPen = new Pen(brush, 1) {
					DashStyle = new DashStyle(new double[] { 3, 7 }, 0)
				};
				dashPen.Freeze();
				drawingContext.DrawLine(dashPen, start, end);

				if (endRect.HasValue) {
					Pen simplePen = new Pen(brush, 1);
					simplePen.Freeze();
					drawingContext.DrawLine(simplePen, end, new Point(end.X + 5, end.Y));
				}
			}
		}

	}
}
