using System;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using Avalonia.Media;
using AvaloniaEdit;
using Avalonia;

namespace SharpEditor.Folding {
	public class FoldingBackgroundRenderer : IBackgroundRenderer {

		private readonly FoldingManager foldingManager;
		public IPen Pen { get; set; }

		public FoldingBackgroundRenderer(FoldingManager foldingManager, IPen pen) {
			this.foldingManager = foldingManager;
			this.Pen = pen;
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
					end = new Point(xPos, Math.Max(textView.DocumentHeight, textView.Bounds.Height)); // textView.ActualHeight
				}

				drawingContext.DrawLine(Pen, start, end);

				if (endRect.HasValue) {
					//Pen simplePen = new Pen(brush, 1);
					drawingContext.DrawLine(Pen, end, new Point(end.X + 5, end.Y));
				}
			}
		}

	}
}
