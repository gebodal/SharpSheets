using SharpSheets.Canvas;
using SharpSheets.Evaluations;
using SharpSheets.Markup.Canvas;

namespace SharpSheets.Markup.Elements {

	public interface IMarkupElement { }

	public interface IIdentifiableMarkupElement : IMarkupElement {
		string? ID { get; }
	}

	public interface IStyledElement : IIdentifiableMarkupElement {
		//string Class { get; }
		StyleSheet StyleSheet { get; }
	}

	public interface IDrawableElement : IStyledElement {
		/// <summary></summary>
		/// <exception cref="MarkupCanvasStateException"></exception>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		void Draw(MarkupCanvas canvas);
	}

	public interface IShapeElement : IDrawableElement {

		/// <summary></summary>
		/// <exception cref="MarkupCanvasStateException"></exception>
		/// <exception cref="EvaluationException"></exception>
		void AssignGeometry(MarkupCanvas canvas);

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		IPathCalculator? GetPath(MarkupCanvas canvas);
	}

	public enum PaintApplication { FILL, STROKE }

	public interface ICanvasPaint {
		/// <summary></summary>
		/// <exception cref="MarkupCanvasStateException"></exception>
		/// <exception cref="EvaluationException"></exception>
		void Apply(MarkupCanvas canvas, PaintApplication application);
	}

	//public interface IStructuralElement { }

	//public interface IDescriptiveElment : IMarkupElement { }

	public abstract class DrawableElement : IDrawableElement {
		public string? ID { get; }
		//public string Class { get; }
		public StyleSheet StyleSheet { get; }

		public DrawableElement(string? id, StyleSheet styleSheet) {
			this.ID = id;
			//this.Class = classname;
			this.StyleSheet = styleSheet;
		}

		public abstract void Draw(MarkupCanvas canvas);
	}

	public abstract class ShapeElement : DrawableElement, IShapeElement {

		protected virtual bool CanFill { get; } = true;

		public ShapeElement(string? id, StyleSheet styleSheet) : base(id, styleSheet) { }

		public void AssignGeometry(MarkupCanvas canvas) {
			if (StyleSheet.Enabled.Evaluate(canvas.Environment)) {
				DoAssignGeometry(canvas);
			}
		}

		public abstract IPathCalculator? GetPath(MarkupCanvas canvas);

		/// <summary></summary>
		/// <exception cref="MarkupCanvasStateException"></exception>
		/// <exception cref="EvaluationException"></exception>
		protected abstract void DoAssignGeometry(MarkupCanvas canvas); // TODO Better name needed

		public override sealed void Draw(MarkupCanvas canvas) {
			if (StyleSheet.Enabled.Evaluate(canvas.Environment)) {
				canvas.SaveState();
				if (StyleSheet.DrawingCoords != null) {
					canvas.SetDrawingCoords(StyleSheet.DrawingCoords);
				}
				if (StyleSheet.Transform != null) {
					canvas.ApplyTransform(StyleSheet.Transform);
				}
				if (StyleSheet.ClipPath != null) {
					StyleSheet.ClipPath.Apply(canvas);
				}
				AssignGraphicsParameters(canvas, out bool fill, out bool stroke);
				AssignGeometry(canvas);
				RenderFinalGeometry(canvas, fill, stroke);
				if (canvas.CollectingDiagnostics && GetPath(canvas)?.GetBoundingBox() is Layouts.Rectangle boundingRect) { canvas.RegisterArea(this, boundingRect); }
				canvas.RestoreState();
			}
		}

		/// <summary></summary>
		/// <exception cref="MarkupCanvasStateException"></exception>
		/// <exception cref="EvaluationException"></exception>
		protected void AssignGraphicsParameters(MarkupCanvas canvas, out bool fill, out bool stroke) {
			fill = false;
			stroke = false;

			if (StyleSheet.Fill != null && CanFill) {
				if (StyleSheet.Fill is SolidPaint solidPaint) {
					ColorExpression fillColor = solidPaint.Color;
					if (StyleSheet.FillOpacity != null) {
						fillColor = fillColor.WithOpacity(StyleSheet.FillOpacity);
					}
					canvas.SetFillColor(fillColor);
					fill = (canvas.GetFillPaint() is CanvasSolidPaint fillPaint) ? fillPaint.Color.A > 0 : true;
				}
				else {
					StyleSheet.Fill.Apply(canvas, PaintApplication.FILL);
					fill = true;
				}
			}

			if (StyleSheet.Stroke != null) {
				if (StyleSheet.Stroke is SolidPaint solidPaint) {
					ColorExpression strokeColor = solidPaint.Color;
					if (StyleSheet.StrokeOpacity != null) {
						strokeColor = strokeColor.WithOpacity(StyleSheet.StrokeOpacity);
					}
					canvas.SetStrokeColor(strokeColor);
					stroke = (canvas.GetStrokePaint() is CanvasSolidPaint strokePaint) ? strokePaint.Color.A > 0 : true;
				}
				else {
					StyleSheet.Stroke.Apply(canvas, PaintApplication.STROKE);
					stroke = true;
				}

				if (StyleSheet.StrokeLineCap != null) { canvas.SetLineCapStyle(StyleSheet.StrokeLineCap); }
				if (StyleSheet.StrokeLineJoin != null) { canvas.SetLineJoinStyle(StyleSheet.StrokeLineJoin); }
				if (StyleSheet.StrokeMiterLimit != null) { canvas.SetMiterLimit(StyleSheet.StrokeMiterLimit); }
				if (StyleSheet.StrokeWidth != null) { canvas.SetLineWidth(StyleSheet.StrokeWidth); }
			}

			
		}

		/// <summary></summary>
		/// <exception cref="MarkupCanvasStateException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		protected void RenderFinalGeometry(MarkupCanvas canvas, bool fill, bool stroke) {
			if (fill && stroke) {
				canvas.FillStrokeUsing(StyleSheet.FillRule, AreaRule.NonZero);
			}
			else if (fill) {
				canvas.FillUsing(StyleSheet.FillRule, AreaRule.NonZero);
			}
			else if (stroke) {
				canvas.Stroke();
			}
			else {
				canvas.EndPath();
			}
		}

	}

}
