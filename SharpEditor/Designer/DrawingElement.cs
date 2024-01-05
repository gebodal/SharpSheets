using System;
using System.Windows;
using System.Windows.Media;

namespace SharpEditor {
	// https://stackoverflow.com/a/16876915/11002708
	public class DrawingElement : FrameworkElement {
		
		private readonly TranslateTransform translation = new TranslateTransform();

		private readonly DrawingGroup drawing;

		public DrawingElement(DrawingGroup drawing) {
			this.drawing = drawing;
		}

		private TranslateTransform? GetTransform() {
			if (Margin.Left == 0 && Margin.Top == 0) {
				return null;
			}
			translation.X = Margin.Left;
			translation.Y = Margin.Top;
			return translation;
		}

		protected override Size MeasureOverride(Size _) {
			Size sz = drawing.Bounds.Size;
			Size final = new Size {
				Width = sz.Width + Margin.Left + Margin.Right,
				Height = sz.Height + Margin.Top + Margin.Bottom,
			};
			//Console.WriteLine($"MeasureOverride size: {sz}, Margin: {Margin}, Final: {final}");
			return final;
		}

		protected override void OnRender(DrawingContext context) {
			/*
			TranslateTransform? transform = GetTransform();
			if (transform != null) {
				context.PushTransform(transform);
			}
			context.DrawDrawing(drawing);
			if (transform != null) {
				context.Pop();
			}
			*/
			//Console.WriteLine("OnRender called");

			TranslateTransform? overallTransform = GetTransform();
			if (overallTransform != null) {
				context.PushTransform(overallTransform);
			}

			//RenderDrawingGroup(drawing.Clone(), context);
			RenderDrawingGroup(drawing, context);

			if (overallTransform != null) {
				context.Pop();
			}

		}

		protected static void RenderDrawingGroup(DrawingGroup drawingGroup, DrawingContext context) {
			/*
			bool clipped = false;
			if (drawingGroup.ClipGeometry is Geometry clipGeometry) {
				context.PushClip(clipGeometry);
				clipped = true;
			}

			foreach (Drawing? child in drawingGroup.Children) {
				if (child is GeometryDrawing drawing) {
					Transform tr = drawing.Geometry.Transform;
					drawing.Geometry.Transform = Transform.Identity;

					if (drawing.Brush is not null) {
						Matrix trInvert = tr.Value;
						trInvert.Invert();
						drawing.Brush.Transform = new MatrixTransform(drawing.Brush.Transform.Value * trInvert);
					}

					if (drawing.Pen is not null && drawing.Pen.Brush is not null) {
						Matrix trInvert = tr.Value;
						trInvert.Invert();
						drawing.Pen.Brush.Transform = new MatrixTransform(drawing.Pen.Brush.Transform.Value * trInvert);
					}

					context.PushTransform(tr);
					context.DrawGeometry(drawing.Brush, drawing.Pen, drawing.Geometry);
					context.Pop();
				}
				else if (child is DrawingGroup childGroup) {
					RenderDrawingGroup(childGroup, context);
				}
			}

			if (clipped) {
				context.Pop();
			}
			*/

			bool clipped = false;
			if (drawingGroup.ClipGeometry is Geometry clipGeometry) {
				context.PushClip(clipGeometry);
				clipped = true;
			}
			context.PushTransform(drawingGroup.Transform);

			foreach (Drawing? child in drawingGroup.Children) {
				if (child is GeometryDrawing drawing) {
					context.DrawGeometry(drawing.Brush, drawing.Pen, drawing.Geometry);
				}
				else if (child is DrawingGroup childGroup) {
					RenderDrawingGroup(childGroup, context);
				}
				else if (child is ImageDrawing image) {
					context.DrawDrawing(image);
				}
			}

			context.Pop();
			if (clipped) {
				context.Pop();
			}
		}

	}
}
