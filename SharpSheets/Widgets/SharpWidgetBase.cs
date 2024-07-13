using System.Collections.Generic;
using System;
using System.Linq;
using SharpSheets.Colors;
using System.Threading;
using SharpSheets.Layouts;
using SharpSheets.Canvas;
using SharpSheets.Exceptions;
using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;

namespace SharpSheets.Widgets {

	public interface IWidget : IDrawableGridElement {
		bool AddChild(IWidget child); // Do we actually need these to be widgets? Can they just be IDrawableGridElement?
	}

	public abstract class SharpWidget : IWidget {

		public readonly WidgetSetup setup;
		protected readonly List<IWidget> children;

		public Margins Margins => setup.margins;
		public Dimension? Size => setup.size;
		public Position? Position => setup.position;

		public IReadOnlyList<IGridElement> Children { get { return children.ToList<IGridElement>(); } }

		public Layout Layout => setup.layout;
		public Arrangement Arrangement => setup.arrangement;
		public LayoutOrder Order => setup.order;
		public float Gutter => setup.gutter;
		public virtual Layout GutterLayout => Layout;

		public virtual bool ProvidesRemaining { get; } = true;

		public SharpWidget(WidgetSetup setup) {
			this.setup = setup;
			children = new List<IWidget>();
		}

		public bool AddChild(IWidget child) {
			children.Add(child);
			return true;
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public Size? MinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			/*
			graphicsState.SaveState();
			graphicsState.ApplySetup(setup);
			Size minimumContentSize = GetMinimumContentSize(graphicsState, availableSpace);
			graphicsState.RestoreState();
			return minimumContentSize;
			*/
			CanvasStateImage stateImage = new CanvasStateImage(graphicsState);
			stateImage.ApplySetup(setup);
			Size? minimumContentSize = GetMinimumContentSize(stateImage, availableSpace);
			return minimumContentSize;
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		protected virtual Size? GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			return GridElements.OverallMinimumSize(children.ToArray<IGridElement>(), graphicsState, availableSpace, Layout, Gutter);
		}

		public Rectangle? ContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			/*
			graphicsState.SaveState();
			graphicsState.ApplySetup(setup);
			Rectangle containerArea = GetContainerArea(graphicsState, rect);
			graphicsState.RestoreState();
			return containerArea;
			*/
			CanvasStateImage stateImage = new CanvasStateImage(graphicsState);
			stateImage.ApplySetup(setup);
			Rectangle? containerArea = GetContainerArea(stateImage, rect);
			return containerArea;
		}
		/// <summary></summary>
		/// <param name="graphicsState"></param>
		/// <param name="rect"></param>
		/// <returns></returns>
		/// <exception cref="InvalidRectangleException"></exception>
		protected abstract Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect);

		protected virtual Rectangle?[] GetChildRects(ISharpGraphicsState graphicsState, Rectangle rect, out Rectangle availableRect, out Rectangle? childrenRectArea, out Rectangle?[] gutters) {
			return GridElements.GetElementRects(this, children.ToArray<IGridElement>(), new CanvasStateImage(graphicsState), rect, out availableRect, out childrenRectArea, out gutters, out _, out _);
		}

		public Rectangle?[] DiagnosticRects(ISharpGraphicsState graphicsState, Rectangle available) {
			/*
			graphicsState.SaveState();
			graphicsState.ApplySetup(setup);
			Rectangle[] diagnosticRects = GetDiagnosticRects(graphicsState, available);
			graphicsState.RestoreState();
			return diagnosticRects;
			*/
			CanvasStateImage stateImage = new CanvasStateImage(graphicsState);
			stateImage.ApplySetup(setup);
			Rectangle?[] diagnosticRects = GetDiagnosticRects(stateImage, available);
			return diagnosticRects;
		}


		/// <summary></summary>
		/// <param name="graphicsState"></param>
		/// <param name="available"></param>
		/// <returns></returns>
		/// <exception cref="InvalidRectangleException"></exception>
		protected virtual Rectangle?[] GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle available) {
			return Array.Empty<Rectangle?>();
		}

		/// <summary>
		/// Draw the widget content inside the available rectangle (after margins), and return any remaining space.
		/// This method does not draw the children of the widget, only this widget's local content.
		/// </summary>
		/// <param name="canvas"> Canvas on which the widget will be drawn. </param>
		/// <param name="rect"> Available rectangle for drawing this widget (after margins applied). </param>
		/// <param name="cancellationToken"> Cancellation token indicating whether the draw procedure should be terminated. </param>
		/// <returns> The remaining rectangle after this widget's contents have been drawn. </returns>
		/// <exception cref="SharpDrawingException"></exception>
		/// <exception cref="SharpLayoutException"></exception>
		/// <exception cref="InvalidRectangleException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		protected abstract void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken);

		protected virtual void RegisterAreas(ISharpCanvas canvas, Rectangle fullRect, Rectangle availableRect) {
			//canvas.RegisterArea(this, rect);
			//canvas.RegisterArea(this, availableRect);
			//canvas.RegisterAreas(this, rect, availableRect, Array.Empty<Rectangle>());
			canvas.RegisterAreas(this, fullRect, availableRect, GetDiagnosticRects(canvas, availableRect).WhereNotNull().ToArray());
		}

		/// <summary></summary>
		/// <exception cref="Exceptions.SharpDrawingException"></exception>
		/// <exception cref="InvalidRectangleException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public virtual void Draw(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			//Console.WriteLine($"Next draw: {GetType().Name} -> {rect} ({Size}), {Margins}");

			//int startStateDepth = canvas.GetStateDepth();
			if (cancellationToken.IsCancellationRequested) { return; }

			canvas.SaveState();

			canvas.ApplySetup(setup);

			Rectangle?[] childRects = GetChildRects(canvas, rect, out Rectangle availableRect, out Rectangle? childrenRectArea, out Rectangle?[] gutters);

			RegisterAreas(canvas, rect, availableRect);

			DrawWidget(canvas, availableRect, cancellationToken);

			if (cancellationToken.IsCancellationRequested) {
				canvas.RestoreState();
				return;
			}

			if (this.ProvidesRemaining && children.Count > 0) {

				if (setup.gutterStyle != null && gutters != null) {
					setup.gutterStyle.Layout = GutterLayout; // This is awful. There must be a better way.
					for (int i = 0; i < gutters.Length; i++) {
						if (gutters[i] != null) {
							setup.gutterStyle.Draw(canvas, gutters[i]!);
						}
					}
				}

				for (int i = 0; i < children.Count; i++) {
					//Console.WriteLine($"CHILD: {children[i].GetType().Name} => {childRects[i]}");
					try {
						if (i < childRects.Length && childRects[i] != null) {
							try {
								children[i].Draw(canvas, childRects[i]!, cancellationToken);
							}
							catch (SharpDrawingException e) {
								throw e;
							}
							catch(InvalidRectangleException e) {
								throw new SharpDrawingException(children[i], "Layout error: " + e.Message, e);
							}
							catch (Exception e) {
								throw new SharpDrawingException(children[i], "Drawing error: " + e.Message, e);
							}
						}
						else if (children[i].GetType() != typeof(Empty)) { // It's fine if Empty children have no space available
							throw new SharpDrawingException(children[i], $"No rect available for child {i} ({children[i].GetType().Name})");
						}
					}
					catch (SharpDrawingException e) {
						Rectangle errorRect = (i < childRects.Length ? childRects[i] : null) ?? rect;
						CreateErrorRect(canvas, errorRect, e);
					}

					if (cancellationToken.IsCancellationRequested) {
						canvas.RestoreState();
						return;
					}
				}
			}

			if (setup.diagnostic) { DrawableElementUtils.DrawDiagnostics(canvas, this, this.GetType().Name, rect, availableRect, childrenRectArea); }

			canvas.RestoreState();
			//Console.WriteLine($"Finish drawing: {GetType().Name} -> {rect} ({Size})");
			//int endStateDepth = canvas.GetStateDepth();

			//Console.WriteLine($"{this.GetType().Name} start: {startStateDepth}, end: {endStateDepth}");

			return;
		}

		protected static void CreateErrorRect(ISharpCanvas canvas, Rectangle errorRect, SharpDrawingException e) {
			canvas.SaveState();
			canvas.SetStrokeColor(Color.Red);
			canvas.SetTextColor(Color.Red);
			canvas.Rectangle(errorRect).Stroke();
			canvas.SetTextFormatAndSize(TextFormat.REGULAR, 200f);
			canvas.FitText(errorRect, e.Message, new ParagraphSpecification(1.35f, 0f, default), new FontSizeSearchParams(0.1f, 200f, 0.2f), Justification.LEFT, Alignment.TOP, TextHeightStrategy.LineHeightBaseline, false);
			canvas.RestoreState();
			canvas.LogError(e);
		}
	}

}