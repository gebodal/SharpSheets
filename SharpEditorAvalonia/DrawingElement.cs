using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace SharpEditorAvalonia {
	// https://stackoverflow.com/a/16876915/11002708
	public class DrawingElement : Control {
		
		private readonly DrawingGroup drawing;

		public DrawingElement(DrawingGroup drawing) {
			this.drawing = drawing;
		}

		private Matrix GetTransform() {
			if (Margin.Left == 0 && Margin.Top == 0) {
				return Matrix.Identity;
			}

			return Matrix.CreateTranslation(Margin.Left, Margin.Top);
		}

		protected override Size MeasureOverride(Size _) {
			Size sz = drawing.GetBounds().Size;
			Size final = new Size(sz.Width + Margin.Left + Margin.Right, sz.Height + Margin.Top + Margin.Bottom);
			//Console.WriteLine($"MeasureOverride size: {sz}, Margin: {Margin}, Final: {final}");
			return final;
		}

		public override void Render(DrawingContext context) {
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

			using(context.PushTransform(GetTransform())) {
				RenderDrawingGroup(drawing, context);
			}

			base.Render(context);
		}

		protected static void RenderDrawingGroup(DrawingGroup drawingGroup, DrawingContext context) {

			if (drawingGroup.ClipGeometry is Geometry clipGeometry) {
				using (context.PushGeometryClip(clipGeometry)) {
					using (context.PushTransform(drawingGroup.Transform?.Value ?? Matrix.Identity)) {
						RenderDrawingCollection(drawingGroup.Children, context);
					}
				}
			}
			else {
				using (context.PushTransform(drawingGroup.Transform?.Value ?? Matrix.Identity)) {
					RenderDrawingCollection(drawingGroup.Children, context);
				}
			}

		}

		protected static void RenderDrawingCollection(DrawingCollection drawingCollection, DrawingContext context) {
			foreach (Drawing? child in drawingCollection) {
				if (child is GeometryDrawing drawing && drawing.Geometry is Geometry geometry) {
					context.DrawGeometry(drawing.Brush, drawing.Pen, geometry);
				}
				else if (child is DrawingGroup childGroup) {
					RenderDrawingGroup(childGroup, context);
				}
				else if (child is ImageDrawing image && image.ImageSource is IImage imageSource) {
					context.DrawImage(imageSource, image.Rect);
				}
			}
		}

	}
}
