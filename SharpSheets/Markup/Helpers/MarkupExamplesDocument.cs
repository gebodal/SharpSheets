using SharpSheets.Utilities;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpSheets.Shapes;
using SharpSheets.Widgets;
using SharpSheets.Canvas;
using SharpSheets.Markup.Patterns;
using SharpSheets.Exceptions;
using SharpSheets.Colors;

namespace SharpSheets.Markup.Helpers {

	public class MarkupExamplesDocument : IDocumentContent {

		public bool HasContent { get { return patterns.Count > 0; } }

		protected readonly List<MarkupPattern> patterns;
		protected readonly float margin;
		protected readonly WidgetFactory widgetFactory;
		protected readonly ShapeFactory shapeFactory;

		public MarkupExamplesDocument(IEnumerable<MarkupPattern> patterns, float margin, WidgetFactory widgetFactory, ShapeFactory shapeFactory) {
			this.patterns = patterns.ToList();
			this.margin = margin;
			this.widgetFactory = widgetFactory;
			this.shapeFactory = shapeFactory;
		}

		public int Count { get { return patterns.Count; } }

		public void Add(MarkupPattern pattern) {
			this.patterns.Add(pattern);
		}

		public void DrawTo(ISharpDocument document, out SharpDrawingException[] errors, CancellationToken cancellationToken) {
			//Console.WriteLine("Draw boxes");

			List<SharpDrawingException> errorList = new List<SharpDrawingException>(); // TODO This needs to be filled properly

			foreach (MarkupPattern pattern in patterns) {
				if (cancellationToken.IsCancellationRequested) {
					break;
				}

				if (pattern is null) {
					document.AddNewPage(new Size(100f, 100f));
					continue;
				}

				if(pattern.rootElement is null) {
					errorList.Add(new SharpDrawingException(pattern, "Invalid pattern root element."));
					continue;
				}

				Size pageSize;
				Rectangle initialExampleRect;

				if (pattern.exampleCanvas != null) {
					pageSize = pattern.exampleCanvas;
					initialExampleRect = pattern.exampleRect ?? new Rectangle(pattern.exampleCanvas.Width, pattern.exampleCanvas.Height).MarginsRel(this.margin, false);
				}
				else {
					Size exampleSize = (Size?)pattern.exampleRect ?? (pattern.rootElement?.setup.canvasArea ?? new Size(100f, 100f));

					float width = exampleSize.Width;
					float height = exampleSize.Height;
					float margin = this.margin * Math.Max(width, height);

					pageSize = new PageSize(width + 2 * margin, height + 2 * margin);
					initialExampleRect = new Rectangle(margin, margin, width, height);
				}

				object? markupObject;
				try {
					markupObject = pattern.MakeExample(widgetFactory, shapeFactory, true, out SharpParsingException[] exampleErrors);
					errorList.AddRange(exampleErrors.Select(e => new SharpDrawingException(pattern, e.Message, e)));
				}
				catch(SharpDrawingException e) {
					errorList.Add(e);
					markupObject = null;
				}
				catch(SharpSheetsException e) {
					errorList.Add(new SharpDrawingException(pattern, e.Message, e));
					markupObject = null;
				}

				if (markupObject == null) {
					continue;
				}

				ISharpCanvas canvas = document.AddNewPage(pageSize);
				canvas.SetDefaultLineWidth(1.35f); // TODO Seems strange to have hard-coded this in so many places?

				Rectangle exampleDrawRect = (markupObject as IAreaShape)?.AspectRect(canvas, initialExampleRect) ?? initialExampleRect;

				canvas.SaveState();
				canvas.SetLineWidth(0.2f * canvas.GetDefaultLineWidth());
				canvas.SetStrokeDash(new StrokeDash(new float[] { 1f, 4f }, 0f));
				canvas.SetStrokeColor(Color.Gray);
				canvas.Rectangle(initialExampleRect).Stroke();
				canvas.RestoreState();

				// Actually draw example content
				try {
					if (markupObject is IShape markupShape) {
						markupShape.Draw(canvas, exampleDrawRect);
					}
					else if (markupObject is IWidget markupWidget) {
						markupWidget.Draw(canvas, exampleDrawRect, default);
					}
					else {
						// Something has gone wrong. Draw error marker.
						canvas.SaveState();
						canvas.SetFillColor(Color.Red.WithOpacity(0.5f));
						canvas.Rectangle(exampleDrawRect).Fill();
						canvas.RestoreState();
					}
					//markupObject.Draw(canvas, shapeRect);
				}
				catch (SharpDrawingException e) {
					canvas.LogError(pattern, e.Message, e);
				}
				catch (SharpSheetsException e) {
					canvas.LogError(pattern, e.Message, e);
				}
				catch (Exception e) { // Too broad?
					canvas.LogError(pattern, e.GetType().Name + ": " + e.Message, e);
				}

				canvas.RegisterAreas(pattern, canvas.CanvasRect, null, Array.Empty<Rectangle>());

				// Draw bounding boxes
				canvas.SaveState();
				canvas.SetLineWidth(0.2f * canvas.GetDefaultLineWidth());

				List<Rectangle?> remainingRects = new List<Rectangle?>();
				Rectangle? fullRect = null;

				/*
				public interface IFramedArea {
					Rectangle RemainingRect(ISharpCanvas canvas, Rectangle rect);
				}
				public interface IFramedContainerArea : IFramedArea {
					Rectangle FullRect(ISharpCanvas canvas, Rectangle rect);
				}
				public interface ILabelledArea {
					Rectangle LabelRect(ISharpCanvas canvas, Rectangle rect);
				}
				public interface IEntriedArea {
					int EntryCount { get; }
					Rectangle EntryRect(ISharpCanvas canvas, int areaIndex, Rectangle rect);
				}
				*/

				try {
					if (markupObject is IFramedArea framedArea) {
						if (framedArea.RemainingRect(canvas, exampleDrawRect) is Rectangle remainingRect) {
							remainingRects.Add(remainingRect);

							if (framedArea is IFramedContainerArea framedContainerArea) {
								try {
									fullRect = framedContainerArea.FullRect(canvas, remainingRect);
								}
								catch (InvalidRectangleException) {
									fullRect = null;
								}
							}
						}
						else {
							canvas.LogError(pattern, "Framed areas must have a valid \"remaining\" area.");
						}
					}
					if (markupObject is ILabelledArea labelledArea) {
						if (labelledArea.LabelRect(canvas, exampleDrawRect) is Rectangle labelRect) {
							remainingRects.Add(labelRect);
						}
						else {
							canvas.LogError(pattern, "Labelled areas must have a valid \"label\" area.");
						}
					}
					if (markupObject is IEntriedArea entriedArea) {
						Rectangle[] entryRects = entriedArea.EntryRects(canvas, exampleDrawRect).Where(r => r != null).ToArray();
						if (entryRects.Length > 0) {
							remainingRects.AddRange(entryRects);
						}
						else {
							canvas.LogError(pattern, "Entried areas must have at least one valid \"entry\" area.");
						}
					}
					if (markupObject is IWidget widgetArea) {
						Size minRowsSize = widgetArea.MinimumSize(canvas, Layout.ROWS, (Size)exampleDrawRect);
						Rectangle minRows = new Rectangle(exampleDrawRect.X, exampleDrawRect.Top - minRowsSize.Height, minRowsSize.Width, minRowsSize.Height);
						Size minColsSize = widgetArea.MinimumSize(canvas, Layout.COLUMNS, (Size)exampleDrawRect);
						Rectangle minCols = new Rectangle(exampleDrawRect.X, exampleDrawRect.Top - minColsSize.Height, minColsSize.Width, minColsSize.Height);

						remainingRects.Add(widgetArea.RemainingRect(canvas, exampleDrawRect));
						remainingRects.Add(minRows);
						remainingRects.Add(minCols);
					}
				}
				catch (SharpDrawingException e) {
					canvas.LogError(e);
				}
				catch (SharpSheetsException e) {
					canvas.LogError(pattern, e.Message, e);
				}

				canvas.SetStrokeDash(new StrokeDash(new float[] { 1f, 4f }, 0f));
				canvas.SetStrokeColor(Color.Red);
				canvas.Rectangle(exampleDrawRect).Stroke();
				if (fullRect != null) {
					//canvas.SetStrokeDash(new float[] { 1f, 4f }, 0.5f);
					canvas.SetStrokeColor(Color.Blue);
					canvas.Rectangle(fullRect).Stroke();
				}

				if (remainingRects != null && remainingRects.Count > 0) {
					canvas.SetStrokeColor(Color.Green);
					for (int i=0; i<remainingRects.Count; i++) {
						canvas.SetStrokeDash(new StrokeDash(new float[] { 1f, 4f }, 2f + i));
						if (remainingRects[i] != null) {
							canvas.Rectangle(remainingRects[i]!).Stroke();
						}
					}
				}

				errorList.AddRange(canvas.GetDrawingErrors());
			}

			errors = errorList.ToArray();
		}
	}

}
