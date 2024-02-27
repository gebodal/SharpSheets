using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Colors;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Exceptions;

namespace SharpSheets.Markup.Canvas {

	public enum DrawingCoords { ABSOLUTE, RELATIVE } // TODO Need better names

	// TODO Make sure the following is actually implemented
	/* 
	 * Canvas should have the option of taking an "aspect", a "canvasArea", and a "slicingValues".
	 * The aspect defines the aspect ratio that the drawing area will be cropped to (regardless of the presence of a canvasArea).
	 * The canvasArea defines a relative region for things to be drawn in, and only this
	 * (i.e. defines the coordinates for the drawing area, and does not change the area itself).
	 * The slicingValues are only used if a canvasArea is provided, and specify a region of the relative canvasArea coordinates
	 * that are drawn in absolute units.
	*/

	public class MarkupGeometryState {

		public DrawingCoords DrawingCoords { get; set; }

		public Transform Transform { get; private set; }
		public Transform InverseTransform { get; private set; }

		public MarkupGeometryState() {
			this.DrawingCoords = DrawingCoords.RELATIVE;
			this.Transform = Transform.Identity;
			this.InverseTransform = Transform.Identity; // this.Transform.Invert();
		}
		public MarkupGeometryState(MarkupGeometryState source) {
			this.DrawingCoords = source.DrawingCoords;
			this.Transform = source.Transform;
			this.InverseTransform = source.InverseTransform;
		}

		public void ApplyTransform(Transform transform) {
			this.Transform *= transform;
			this.InverseTransform = this.Transform.Invert();
		}
	}

	public class MarkupGeometry {

		protected readonly Layouts.Rectangle pageCanvas;
		protected readonly Layouts.Rectangle drawingRect;
		protected readonly Layouts.Size? referenceRect;

		public DrawPoint OriginalSpaceOrigin { get; }
		public Transform CanvasOriginTranslation { get; }

		private readonly NSliceValues? slicingValues;
		public NSliceScaling? Slicing { get; }

		private MarkupGeometryState state;
		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		public MarkupGeometryState State {
			get {
				return state;
			}
			set {
				if(value == null) {
					throw new ArgumentNullException(nameof(value));
				}
				else {
					state = value;
				}
			}
		}

		private MarkupGeometry(Layouts.Rectangle drawingRect, Layouts.Size? referenceRect, NSliceValues? slicingValues) {

			this.pageCanvas = drawingRect;
			this.drawingRect = new Layouts.Rectangle(drawingRect.Width, drawingRect.Height);

			this.referenceRect = referenceRect;

			OriginalSpaceOrigin = new DrawPoint(drawingRect.X, drawingRect.Y);
			CanvasOriginTranslation = Transform.Translate(OriginalSpaceOrigin.X, OriginalSpaceOrigin.Y);

			if (referenceRect != null) {
				this.slicingValues = slicingValues;
				if (this.slicingValues != null) {
					Slicing = new NSliceScaling(referenceRect, drawingRect, this.slicingValues);
				}
				else {
					Slicing = null;
				}
			}
			else {
				this.slicingValues = null;
				Slicing = null;
			}

			state = new MarkupGeometryState();
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public static MarkupGeometry CreateGeometry(SharpCanvasGraphicsSnapshot snapshot, Layouts.Rectangle drawingRect, Layouts.Size? referenceRect, NSliceValuesExpression? slicingValues, IEnvironment contextEnvironment, out IEnvironment finalEnvironment) {
			IEnvironment drawingEnvironment = MarkupEnvironments.MakeDrawingStateEnvironment(
				snapshot.GetMarkupData(),
				drawingRect,
				new Layouts.Rectangle(drawingRect.Width, drawingRect.Height),
				referenceRect);

			finalEnvironment = Environments.Concat(contextEnvironment, drawingEnvironment);

			NSliceValues? slicingValuesEval = slicingValues?.Evaluate(finalEnvironment);

			return new MarkupGeometry(drawingRect, referenceRect, slicingValuesEval);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public DrawPoint TransformPoint(DrawPointExpression currentTransformPointExpr, IEnvironment environment) {
			DrawPoint currentTransformPoint = currentTransformPointExpr.Evaluate(environment); // Point in the currently transformed space
			if (referenceRect != null && state.DrawingCoords == DrawingCoords.RELATIVE) { //if (state.DrawingCoords == DrawingCoords.RELATIVE) {
				DrawPoint referenceSpacePoint = state.Transform.Map(currentTransformPoint); // That point mapped to the reference space
				DrawPoint adjustedPoint; // That point adjusted to the current drawing rect
				if (Slicing != null) {
					adjustedPoint = Slicing.TransformPoint(referenceSpacePoint);
				}
				else {
					adjustedPoint = NSliceScaling.StretchPoint(referenceRect, referenceSpacePoint, drawingRect);
				}
				DrawPoint currentTransformAdjustedPoint = state.InverseTransform.Map(adjustedPoint); // Adjusted point mapped back to current transform
				return currentTransformAdjustedPoint;
			}
			else {
				return currentTransformPoint;
			}
		}

		public DrawPoint InverseTransformPoint(DrawPoint currentTransformAdjustedPoint) {
			//if (state.DrawingCoords == DrawingCoords.RELATIVE) {
			if (referenceRect != null && state.DrawingCoords == DrawingCoords.RELATIVE) {
				DrawPoint adjustedPoint = state.Transform.Map(currentTransformAdjustedPoint);
				DrawPoint referenceSpacePoint;
				if (Slicing != null) {
					referenceSpacePoint = Slicing.InvertTransformPoint(adjustedPoint);
				}
				else {
					referenceSpacePoint = NSliceScaling.InvertStretchPoint(referenceRect, adjustedPoint, drawingRect);
				}
				DrawPoint currentTransformPoint = state.InverseTransform.Map(referenceSpacePoint);
				return currentTransformPoint;
			}
			else {
				return currentTransformAdjustedPoint;
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public DrawPoint TransformPoint(FloatExpression x, FloatExpression y, IEnvironment environment) {
			return TransformPoint(new DrawPointExpression(x, y), environment);
		}
		public DrawPoint InverseTransformPoint(float x, float y) {
			return InverseTransformPoint(new DrawPoint(x, y));
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public float TransformLength(FloatExpression currentTransformLengthExpr, IEnvironment environment) {
			float currentTransformLength = currentTransformLengthExpr.Evaluate(environment);
			//if (state.DrawingCoords == DrawingCoords.RELATIVE) {
			if (referenceRect != null && state.DrawingCoords == DrawingCoords.RELATIVE) {
				if (Slicing != null) {
					return Slicing.TransformLength(currentTransformLength);
				}
				else {
					return NSliceScaling.StretchLength(referenceRect, currentTransformLength, drawingRect);
					//return currentTransformLength;
				}
			}
			else {
				return currentTransformLength;
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public Vector TransformVector(VectorExpression vectorExpr, IEnvironment environment) {
			return new Vector(TransformLength(vectorExpr.X, environment), TransformLength(vectorExpr.Y, environment));
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public Vector TransformVector(FloatExpression xExpr, FloatExpression yExpr, IEnvironment environment) {
			return new Vector(TransformLength(xExpr, environment), TransformLength(yExpr, environment));
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public Layouts.Rectangle TransformRectangle(RectangleExpression referenceSpaceRect, IEnvironment environment) {
			DrawPoint location = TransformPoint(referenceSpaceRect.X, referenceSpaceRect.Y, environment);
			DrawPoint topLeft = TransformPoint(referenceSpaceRect.Left, referenceSpaceRect.Top, environment);
			DrawPoint bottomRight = TransformPoint(referenceSpaceRect.Right, referenceSpaceRect.Bottom, environment);

			float width = DrawPoint.Distance(location, bottomRight);
			float height = DrawPoint.Distance(location, topLeft);

			return new Layouts.Rectangle(location.X, location.Y, width, height);
		}

		public Layouts.Rectangle InverseTransformRectangle(Layouts.Rectangle drawingSpaceRect) {
			DrawPoint location = InverseTransformPoint(drawingSpaceRect.X, drawingSpaceRect.Y);
			DrawPoint topLeft = InverseTransformPoint(drawingSpaceRect.Left, drawingSpaceRect.Top);
			DrawPoint bottomRight = InverseTransformPoint(drawingSpaceRect.Right, drawingSpaceRect.Bottom);

			float width = DrawPoint.Distance(location, bottomRight);
			float height = DrawPoint.Distance(location, topLeft);

			return new Layouts.Rectangle(location.X, location.Y, width, height);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public Layouts.Rectangle GetOriginalSpaceRectangle(RectangleExpression referenceSpaceRect, IEnvironment environment) {
			// TODO Does this work like this?

			DrawPoint location = TransformPoint(referenceSpaceRect.X, referenceSpaceRect.Y, environment);
			DrawPoint topLeft = TransformPoint(referenceSpaceRect.Left, referenceSpaceRect.Top, environment);
			DrawPoint bottomRight = TransformPoint(referenceSpaceRect.Right, referenceSpaceRect.Bottom, environment);

			float width = DrawPoint.Distance(location, bottomRight);
			float height = DrawPoint.Distance(location, topLeft);

			return new Layouts.Rectangle(location.X + pageCanvas.X, location.Y + pageCanvas.Y, width, height);
		}

		public Layouts.Rectangle InferCanvasArea(Layouts.Rectangle referenceInnerRect, Layouts.Rectangle drawingSpaceInnerRect) {
			if (slicingValues != null && referenceRect is not null) {
				return NSliceScaling.InferFullRect(referenceRect, slicingValues.xs, slicingValues.ys, referenceInnerRect, drawingSpaceInnerRect);
			}
			else if (referenceRect != null) {
				return NSliceScaling.InferFullRectStretch(referenceRect, referenceInnerRect, drawingSpaceInnerRect);
			}
			else {
				throw new NotSupportedException("Cannot infer rectangle without reference area.");
			}
		}

		public static Layouts.Rectangle TransformRectangle(Layouts.Rectangle rect, Transform transform) {
			DrawPoint bottomLeft = transform.Map(new DrawPoint(rect.Left, rect.Bottom));
			DrawPoint bottomRight = transform.Map(new DrawPoint(rect.Right, rect.Bottom));
			DrawPoint topRight = transform.Map(new DrawPoint(rect.Right, rect.Top));
			DrawPoint topLeft = transform.Map(new DrawPoint(rect.Left, rect.Top));

			float minX = Math.Min(Math.Min(bottomLeft.X, bottomRight.X), Math.Min(topRight.X, topLeft.X));
			float maxX = Math.Max(Math.Max(bottomLeft.X, bottomRight.X), Math.Max(topRight.X, topLeft.X));
			float minY = Math.Min(Math.Min(bottomLeft.Y, bottomRight.Y), Math.Min(topRight.Y, topLeft.Y));
			float maxY = Math.Max(Math.Max(bottomLeft.Y, bottomRight.Y), Math.Max(topRight.Y, topLeft.Y));

			return Layouts.Rectangle.RectangleFromBounding(minX, minY, maxX, maxY);
		}

	}

	public class MarkupCanvas {

		/*
		protected class MarkupCanvasState {

			public DrawingCoords DrawingCoords { get; set; }

			public Transform Transform { get; private set; }
			public Transform InverseTransform { get; private set; }

			public MarkupCanvasState() {
				this.DrawingCoords = DrawingCoords.RELATIVE;
				this.Transform = Transform.Identity();
				this.InverseTransform = this.Transform.Invert();
			}
			public MarkupCanvasState(MarkupCanvasState source) {
				this.DrawingCoords = source.DrawingCoords;
				this.Transform = source.Transform;
				this.InverseTransform = source.InverseTransform;
			}

			public void ApplyTransform(Transform transform) {
				this.Transform *= transform;
				this.InverseTransform = this.Transform.Invert();
			}
		}
		*/

		private readonly MarkupCanvas? parent;

		public bool IsClosed { get; private set; }

		protected ISharpCanvas? Canvas { get; private set; }
		//protected readonly Rectangle pageCanvas;
		//protected readonly Rectangle drawingRect;
		//protected readonly Rectangle referenceRect;

		//public NSliceValues SlicingValues { get; private set; }
		//public NSliceScaling Slicing { get; private set; } = null;

		//private readonly IEnvironment contextEnvironment;
		//private readonly IEnvironment drawingEnvironment;
		//public IEnvironment Environment { get; }
		//public IEnvironment Environment => _geometry.Environment;

		/*
		private readonly Stack<MarkupCanvasState> stateStack;
		private MarkupCanvasState state;
		*/
		private readonly Stack<MarkupGeometryState> stateStack;
		//private MarkupGeometryState state;
		
		private readonly Stack<IEnvironment> envStack;
		public IEnvironment Environment { get; private set; }

		private readonly MarkupGeometry _geometry;

		public bool CollectingDiagnostics { get; }

		private readonly List<SharpDrawingException> exceptions;

		private MarkupCanvas(MarkupCanvas? parent, ISharpCanvas canvas, MarkupGeometry geometry, IEnvironment environment, bool collectDiagnostics) {
			this.parent = parent;
			this.Canvas = canvas;
			this._geometry = geometry;

			this.stateStack = new Stack<MarkupGeometryState>();

			this.Environment = environment;
			this.envStack = new Stack<IEnvironment>();

			this.CollectingDiagnostics = collectDiagnostics;

			this.IsClosed = false;

			this.exceptions = new List<SharpDrawingException>();
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		private static MarkupCanvas SetupCanvas(MarkupCanvas? parent, ISharpCanvas canvas, Layouts.Rectangle drawingRect, Layouts.Size? referenceRect, NSliceValuesExpression? slicingValues, IEnvironment contextEnvironment, bool collectDiagnostics) {
			canvas.SaveState();

			MarkupGeometry geometry = MarkupGeometry.CreateGeometry(canvas.GetSnapshot(), drawingRect, referenceRect, slicingValues, contextEnvironment, out IEnvironment finalEnvironment);

			#pragma warning disable GJT0001 // Unhandled thrown exception from statement
			canvas.ApplyTransform(geometry.CanvasOriginTranslation);
			#pragma warning restore GJT0001 // Should never occur, as the translation Transform will always have a non-zero determinent
			MarkupCanvas markupCanvas = new MarkupCanvas(parent, canvas, geometry, finalEnvironment, collectDiagnostics);

			return markupCanvas;
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public static MarkupCanvas Open(ISharpCanvas canvas, Layouts.Rectangle drawingRect, Layouts.Size? referenceRect, NSliceValuesExpression? slicingValues, IEnvironment contextEnvironment, bool collectDiagnostics) {
			return SetupCanvas(null, canvas, drawingRect, referenceRect, slicingValues, contextEnvironment, collectDiagnostics);
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// 
		public MarkupCanvas CreateChild(RectangleExpression drawingRectExpr, SizeExpression referenceRectExpr, NSliceValuesExpression slicingValues, IEnvironment contextEnvironment) {
			if(Canvas is null) { throw new MarkupCanvasStateException(); }
			
			ISharpCanvas documentCanvas = Canvas;
			Canvas = null;

			Layouts.Rectangle drawingRect = Evaluate(drawingRectExpr);
			Layouts.Size? referenceRect = Evaluate(referenceRectExpr, null);

			return SetupCanvas(this, documentCanvas, drawingRect, referenceRect, slicingValues, contextEnvironment, CollectingDiagnostics);
		}

		/// <summary></summary>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="InvalidOperationException"></exception>
		public void Close(out SharpDrawingException[] errors) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }

			while (stateStack.Count > 0) {
				RestoreState(); // Undo any unresolved saved states on the held ISharpCanvas object
			}
			Canvas.RestoreState(); // It would be good if we had some way to checkpoint a specific state here?

			if (parent != null) {
				parent.Canvas = Canvas; // If this is a child canvas, reset parent canvas
			}

			Canvas = null; // Invalidate this MarkupCanvas object so it cannot be used accidentally after closing

			IsClosed = true;

			errors = this.exceptions.ToArray();
		}

		/*
		public DrawPoint GetAbsolutePoint(DrawPointExpression rectSpacePoint) {
			DrawPoint point = rectSpacePoint.Evaluate(this);
			return new DrawPoint(drawingRect.X + point.X, drawingRect.Y + point.Y);
		}
		*/

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		private T Evaluate<T>(IExpression<T> expression) {
			return expression.Evaluate(Environment);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		[return: NotNullIfNotNull(nameof(defaultVal))]
		public T? Evaluate<T>(IExpression<T>? expression, T? defaultVal) where T : notnull {
			if (expression != null) {
				return expression.Evaluate(Environment);
			}
			else {
				return defaultVal;
			}
		}

		private T[] Evaluate<T>(IExpression<T>[] expressions) {
			T[] result = new T[expressions.Length];
			for(int i=0; i<expressions.Length; i++) {
				result[i] = expressions[i].Evaluate(Environment);
			}
			return result;
		}

		public void LogError(object origin, string message, Exception innerException) {
			exceptions.Add(new SharpDrawingException(origin, message, innerException));
		}

		#region Geometry Methods

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public DrawPoint TransformPoint(DrawPointExpression currentTransformPointExpr) => _geometry.TransformPoint(currentTransformPointExpr, Environment);
		public DrawPoint InverseTransformPoint(DrawPoint currentTransformAdjustedPoint) => _geometry.InverseTransformPoint(currentTransformAdjustedPoint);

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public DrawPoint TransformPoint(FloatExpression x, FloatExpression y) => _geometry.TransformPoint(x, y, Environment);
		public DrawPoint InverseTransformPoint(float x, float y) => _geometry.InverseTransformPoint(x, y);

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public float TransformLength(FloatExpression currentTransformLengthExpr) => _geometry.TransformLength(currentTransformLengthExpr, Environment);

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public Vector TransformVector(VectorExpression vectorExpr) => _geometry.TransformVector(vectorExpr, Environment);
		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public Vector TransformVector(FloatExpression xExpr, FloatExpression yExpr) => _geometry.TransformVector(xExpr, yExpr, Environment);

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public Layouts.Rectangle TransformRectangle(RectangleExpression referenceSpaceRect) => _geometry.TransformRectangle(referenceSpaceRect, Environment);

		public Layouts.Rectangle InverseTransformRectangle(Layouts.Rectangle drawingSpaceRect) => _geometry.InverseTransformRectangle(drawingSpaceRect);

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		public Layouts.Rectangle GetOriginalSpaceRectangle(RectangleExpression referenceSpaceRect) => _geometry.GetOriginalSpaceRectangle(referenceSpaceRect, Environment);

		public Layouts.Rectangle InferCanvasArea(Layouts.Rectangle referenceInnerRect, Layouts.Rectangle drawingSpaceInnerRect) => _geometry.InferCanvasArea(referenceInnerRect, drawingSpaceInnerRect);

		#endregion

		#region Geometry and Graphics State

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas SaveState() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			stateStack.Push(_geometry.State);
			_geometry.State = new MarkupGeometryState(_geometry.State);
			Canvas.SaveState();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="InvalidOperationException"></exception>
		public MarkupCanvas RestoreState() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			_geometry.State = stateStack.Pop();
			Canvas.RestoreState();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas SaveEnvironment() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			envStack.Push(Environment);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="InvalidOperationException"></exception>
		public MarkupCanvas RestoreEnvironment() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Environment = envStack.Pop();
			return this;
		}

		public MarkupCanvas ApplyEnvironment(IEnvironment added) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			this.Environment = Environments.Concat(added, this.Environment); // this.Environment.AppendEnvironment(added);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetDrawingCoords(EnumExpression<DrawingCoords> coordsExression) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			if (coordsExression != null) {
				DrawingCoords coords = Evaluate(coordsExression);
				_geometry.State.DrawingCoords = coords;
			}
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public MarkupCanvas ApplyTransform(TransformExpression transform) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Transform abs = Evaluate(transform);
			Canvas.ApplyTransform(abs);
			_geometry.State.ApplyTransform(abs);
			return this;
		}

		#endregion

		#region Canvas Book-keeping

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas RegisterArea(object owner, RectangleExpression rect) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Layouts.Rectangle finalRect = TransformRectangle(rect);
			return this.RegisterArea(owner, finalRect, null, Array.Empty<Rectangle>());
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas RegisterArea(object owner, Rectangle rect) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return this.RegisterArea(owner, rect, null, Array.Empty<Rectangle>());
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas RegisterArea(object owner, RectangleExpression rect, MarginsExpression margins) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Layouts.Rectangle rectArea = TransformRectangle(rect);
			Margins rectMargins = Evaluate(margins, Margins.Zero);
			Layouts.Rectangle finalRect = rectArea.Margins(rectMargins, false);
			return this.RegisterArea(owner, finalRect, null, Array.Empty<Rectangle>());
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas RegisterArea(object owner, Rectangle originalRect, Rectangle? adjustedRect, Rectangle[] innerAreas) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			// Rectangle needs transforming back to original space
			//Layouts.Rectangle finalRect = MarkupGeometry.TransformRectangle(rect, geometry.State.InverseTransform * geometry.CanvasOriginTranslation);
			//Layouts.Rectangle finalRect = MarkupGeometry.TransformRectangle(rect, geometry.State.Transform);
			//finalRect = MarkupGeometry.TransformRectangle(finalRect, geometry.CanvasOriginTranslation);

			Transform rectTransform = _geometry.CanvasOriginTranslation * _geometry.State.Transform;
			Rectangle originalFinal = MarkupGeometry.TransformRectangle(originalRect, rectTransform);
			Rectangle? adjustedFinal = adjustedRect != null ? MarkupGeometry.TransformRectangle(adjustedRect, rectTransform) : null;
			Rectangle[] innerFinal = innerAreas.Select(i => MarkupGeometry.TransformRectangle(i, rectTransform)).ToArray();
			Canvas.RegisterAreas(owner, originalFinal, adjustedFinal, innerFinal);
			return this;
		}

		#endregion

		#region Canvas Implementation

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas CircleAt(DrawPointExpression centre, FloatExpression radius) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint abs = TransformPoint(centre);
			float absRadius = TransformLength(radius);
			Canvas.CircleAt(abs.X, abs.Y, absRadius);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas Ellipse(DrawPointExpression centre, FloatExpression rx, FloatExpression ry) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint abs = TransformPoint(centre);
			float absRx = TransformLength(rx);
			float absRy = TransformLength(ry);
			Canvas.Ellipse(abs.X - absRx, abs.Y - absRy, abs.X + absRx, abs.Y + absRy);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas MoveTo(DrawPointExpression start) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint abs = TransformPoint(start);
			Canvas.MoveTo(abs.X, abs.Y);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas LineTo(DrawPointExpression point) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint abs = TransformPoint(point);
			Canvas.LineTo(abs.X, abs.Y);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas CurveTo(DrawPointExpression control1, DrawPointExpression control2, DrawPointExpression end) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint c1 = TransformPoint(control1);
			DrawPoint c2 = TransformPoint(control2);
			DrawPoint e = TransformPoint(end);
			Canvas.CurveTo(c1.X, c1.Y, c2.X, c2.Y, e.X, e.Y);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas CurveTo(DrawPointExpression control, DrawPointExpression end) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint c = TransformPoint(control);
			DrawPoint e = TransformPoint(end);
			Canvas.CurveTo(c.X, c.Y, e.X, e.Y);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas EllipseArc(DrawPointExpression end, FloatExpression rx, FloatExpression ry, FloatExpression angle, BoolExpression largeArc, BoolExpression sweep) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint endPoint = TransformPoint(end);
			float absRx = TransformLength(rx);
			float absRy = TransformLength(ry);
			float absAngle = Evaluate(angle); // angle.Evaluate(Environment);
			bool finalLargeArc = Evaluate(largeArc); // largeArc.Evaluate(Environment);
			bool finalSweep = Evaluate(sweep); // sweep.Evaluate(Environment);
			Canvas.ArcTo(
				endPoint.X, endPoint.Y,
				absRx, absRy, absAngle,
				finalLargeArc, finalSweep);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas ClosePath() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.ClosePath();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas EndPath() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.EndPath();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas Rectangle(RectangleExpression rect) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Layouts.Rectangle finalRect = TransformRectangle(rect);
			Canvas.Rectangle(finalRect);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas RoundRectangle(RectangleExpression rect, FloatExpression? rx, FloatExpression? ry) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Layouts.Rectangle finalRect = TransformRectangle(rect);
			float rxVal = 0f, ryVal = 0f;
			if (rx != null) {
				rxVal = TransformLength(rx);
			}
			if (ry != null) {
				ryVal = TransformLength(ry);
			}
			Canvas.RoundRectangle(finalRect, rxVal, ryVal);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas Stroke() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.Stroke();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas Fill() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.Fill();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas EoFill() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.EoFill();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas FillUsing(EnumExpression<AreaRule>? fillRule, AreaRule defaultRule = AreaRule.NonZero) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			AreaRule rule = fillRule != null ? Evaluate(fillRule) : defaultRule;
			if (rule == AreaRule.EvenOdd) {
				Canvas.EoFill();
			}
			else {
				Canvas.Fill();
			}
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas FillStroke() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.FillStroke();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas EoFillStroke() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.EoFillStroke();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas FillStrokeUsing(EnumExpression<AreaRule>? fillRule, AreaRule defaultRule = AreaRule.NonZero) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			AreaRule rule = fillRule != null ? Evaluate(fillRule) : defaultRule;
			if (rule == AreaRule.EvenOdd) {
				Canvas.EoFillStroke();
			}
			else {
				Canvas.FillStroke();
			}
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas Clip() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.Clip();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas EoClip() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.EoClip();
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas ClipUsing(EnumExpression<AreaRule>? clipRule, AreaRule defaultRule = AreaRule.NonZero) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			AreaRule rule = clipRule != null ? Evaluate(clipRule) : defaultRule;
			if (rule == AreaRule.EvenOdd) {
				Canvas.EoClip();
			}
			else {
				Canvas.Clip();
			}
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas ApplyViewBox(RectangleExpression placement, RectangleExpression viewBoxExpr, PreserveAspectRatioExpression aspect, out RectangleExpression contentRect) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }

			Layouts.Rectangle viewBox = Evaluate(viewBoxExpr);
			PreserveAspectRatio aspectEval = Evaluate(aspect, PreserveAspectRatio.Default);

			Layouts.Rectangle drawingSpaceBaseRect = TransformRectangle(placement);
			Layouts.Rectangle drawingSpaceContentRect;

			if (aspectEval.xAlignment == Alignment.NONE) {
				drawingSpaceContentRect = drawingSpaceBaseRect.Clone();
			}
			else {
				if (aspectEval.meetSlice == PreserveType.MEET) {
					drawingSpaceContentRect = drawingSpaceBaseRect.Aspect(viewBox.AspectRatio);
				}
				else { // meetSlice == PreserveType.SLICE
					drawingSpaceContentRect = drawingSpaceBaseRect.ContainAspect(viewBox.AspectRatio);
				}

				float xContent;
				float yContent;

				if (aspectEval.xAlignment == Alignment.MIN) {
					xContent = drawingSpaceBaseRect.X;
				}
				else if (aspectEval.xAlignment == Alignment.MID) {
					xContent = drawingSpaceBaseRect.CentreX - drawingSpaceContentRect.Width / 2;
				}
				else { // aspect.xAlignment == Alignment.MAX
					xContent = drawingSpaceBaseRect.Right - drawingSpaceContentRect.Width;
				}

				if (aspectEval.yAlignment == Alignment.MIN) {
					yContent = drawingSpaceBaseRect.Y;
				}
				else if (aspectEval.yAlignment == Alignment.MID) {
					yContent = drawingSpaceBaseRect.CentreY - drawingSpaceContentRect.Height / 2;
				}
				else { // aspect.yAlignment == Alignment.MAX
					yContent = drawingSpaceBaseRect.Top - drawingSpaceContentRect.Height;
				}

				drawingSpaceContentRect = new Layouts.Rectangle(xContent, yContent, drawingSpaceContentRect.Width, drawingSpaceContentRect.Height);
			}

			Canvas.Rectangle(drawingSpaceBaseRect).Clip().EndPath();

			Layouts.Rectangle referenceSpaceContent = InverseTransformRectangle(drawingSpaceContentRect);

			contentRect = referenceSpaceContent;

			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetLineWidth(FloatExpression width) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			float abs = Evaluate(width);
			Canvas.SetLineWidth(abs);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetLineCapStyle(EnumExpression<LineCapStyle> capStyle) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			LineCapStyle val = Evaluate(capStyle);
			Canvas.SetLineCapStyle(val);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetLineJoinStyle(EnumExpression<LineJoinStyle> joinStyle) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			LineJoinStyle val = Evaluate(joinStyle);
			Canvas.SetLineJoinStyle(val);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetMiterLimit(FloatExpression mitreLimit) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			float abs = Evaluate(mitreLimit);
			Canvas.SetMiterLimit(abs);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetStrokeDash(FloatExpression[] dashArray, FloatExpression? offset) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			float[] absArray = Evaluate(dashArray);
			float absOffset = Evaluate(offset, 0f);
			Canvas.SetStrokeDash(new StrokeDash(absArray, absOffset));
			return this;
		}

		public CanvasPaint GetStrokePaint() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetStrokePaint(); // Do we need to be reversing the transformation on the gradient paints?
		}
		public CanvasPaint GetFillPaint() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetFillPaint(); // Do we need to be reversing the transformation on the gradient paints?
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetStrokeColor(ColorExpression color) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Color strokeColor = Evaluate(color);
			Canvas.SetStrokeColor(strokeColor);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetFillColor(ColorExpression color) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Color strokeColor = Evaluate(color);
			Canvas.SetFillColor(strokeColor);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetStrokeLinearGradient(DrawPointExpression point1, DrawPointExpression point2, IList<ColorStopExpression> stopExprs) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint p1 = TransformPoint(point1);
			DrawPoint p2 = TransformPoint(point2);
			ColorStop[] stops = stopExprs.Select(e => Evaluate(e)).ToArray();
			Canvas.SetStrokeLinearGradient(p1.X, p1.Y, p2.X, p2.Y, stops);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetFillLinearGradient(DrawPointExpression point1, DrawPointExpression point2, IList<ColorStopExpression> stopExprs) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint p1 = TransformPoint(point1);
			DrawPoint p2 = TransformPoint(point2);
			ColorStop[] stops = stopExprs.Select(e => Evaluate(e)).ToArray();
			Canvas.SetFillLinearGradient(p1.X, p1.Y, p2.X, p2.Y, stops);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetStrokeRadialGradient(DrawPointExpression point1, FloatExpression radius1, DrawPointExpression point2, FloatExpression radius2, IList<ColorStopExpression> stopExprs) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint p1 = TransformPoint(point1);
			float r1 = TransformLength(radius1);
			DrawPoint p2 = TransformPoint(point2);
			float r2 = TransformLength(radius2);
			ColorStop[] stops = stopExprs.Select(e => Evaluate(e)).ToArray();
			Canvas.SetStrokeRadialGradient(p1.X, p1.Y, r1, p2.X, p2.Y, r2, stops);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetFillRadialGradient(DrawPointExpression point1, FloatExpression radius1, DrawPointExpression point2, FloatExpression radius2, IList<ColorStopExpression> stopExprs) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint p1 = TransformPoint(point1);
			float r1 = TransformLength(radius1);
			DrawPoint p2 = TransformPoint(point2);
			float r2 = TransformLength(radius2);
			ColorStop[] stops = stopExprs.Select(e => Evaluate(e)).ToArray();
			Canvas.SetFillRadialGradient(p1.X, p1.Y, r1, p2.X, p2.Y, r2, stops);
			return this;
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public Color GetForegroundColor() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetForegroundColor();
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public Color GetBackgroundColor() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetBackgroundColor();
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public Color GetMidtoneColor() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetMidtoneColor();
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public Color GetTextColor() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetTextColor();
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="IOException"></exception>
		public MarkupCanvas AddImage(CanvasImageData image, RectangleExpression rect, float? imageAspect) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Layouts.Rectangle finalRect = TransformRectangle(rect);
			Canvas.AddImage(image, finalRect, imageAspect);
			return this;
		}

		#region Text

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetTextColor(ColorExpression color) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Color textColor = Evaluate(color);
			Canvas.SetTextColor(textColor);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas SetTextFormatAndSize(EnumExpression<TextFormat>? font, FloatExpression? size) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.SetTextFormatAndSize(font != null ? Evaluate(font) : Canvas.GetTextFormat(), size != null ? Evaluate(size) : Canvas.GetTextSize());
			return this;
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public TextFormat GetTextFormat() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetTextFormat();
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public float GetTextSize() {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetTextSize();
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas SetTextRenderingMode(TextRenderingMode mode) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.SetTextRenderingMode(mode);
			return this;
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public float GetWidth(IExpression<string> text, EnumExpression<TextFormat>? format, FloatExpression? size) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetWidth(Evaluate(text),
				format != null ? Evaluate(format) : Canvas.GetTextFormat(),
				size != null ? Evaluate(size) : Canvas.GetTextSize());
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public float GetAscent(IExpression<string> text, EnumExpression<TextFormat>? format, FloatExpression? size) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetAscent(Evaluate(text),
				format != null ? Evaluate(format) : Canvas.GetTextFormat(),
				size != null ? Evaluate(size) : Canvas.GetTextSize());
		}

		/// <summary></summary>
		/// <returns></returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public float GetDescent(IExpression<string> text, EnumExpression<TextFormat>? format, FloatExpression? size) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			return Canvas.GetDescent(Evaluate(text),
				format != null ? Evaluate(format) : Canvas.GetTextFormat(),
				size != null ? Evaluate(size) : Canvas.GetTextSize());
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas DrawText(IExpression<string> text, DrawPointExpression location) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint xy = TransformPoint(location);
			string str = Evaluate(text);
			Canvas.DrawText(str, xy.X, xy.Y);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		public MarkupCanvas DrawTextExact(string text, float x, float y) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Canvas.DrawText(text, x, y);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas DrawRichText(IExpression<RichString> text, DrawPointExpression location) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			DrawPoint xy = TransformPoint(location);
			RichString str = Evaluate(text);
			Canvas.DrawRichText(str, xy.X, xy.Y);
			return this;
		}

		/// <summary>Draws a single line of <see cref="RichString"/> to the canvas inside <paramref name="rect"/> at the current font size.</summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas DrawRichText(RectangleExpression rect, IExpression<RichString> text, EnumExpression<Justification> justification, EnumExpression<SharpSheets.Canvas.Text.Alignment> alignment, EnumExpression<TextHeightStrategy> heightStrategy) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Layouts.Rectangle absRect = TransformRectangle(rect);
			RichString str = Evaluate(text);
			Justification justif = Evaluate(justification);
			SharpSheets.Canvas.Text.Alignment align = Evaluate(alignment);
			TextHeightStrategy heightStrat = Evaluate(heightStrategy);
			Canvas.DrawRichText(absRect, str, justif, align, heightStrat);
			return this;
		}

		/// <summary>Draws <see cref="RichString"/> to the canvas inside <paramref name="rect"/> at the current font size, splitting the text as necessary (into lines and paragraphs).</summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas DrawRichText(RectangleExpression rect, IExpression<RichString> text, FloatExpression lineSpacing, FloatExpression paragraphSpacing, EnumExpression<Justification> justification, EnumExpression<SharpSheets.Canvas.Text.Alignment> alignment, EnumExpression<TextHeightStrategy> heightStrategy) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Layouts.Rectangle absRect = TransformRectangle(rect);
			RichString str = Evaluate(text);
			float absLineSpacing = Evaluate(lineSpacing, 1f);
			float absParagraphSpacing = Evaluate(paragraphSpacing, 0f);
			Justification justif = Evaluate(justification);
			SharpSheets.Canvas.Text.Alignment align = Evaluate(alignment);
			TextHeightStrategy heightStrat = Evaluate(heightStrategy);
			// TODO Should we be able to adjust paragraph indenting here?
			Canvas.DrawRichText(absRect, str, Canvas.GetTextSize(), new ParagraphSpecification(absLineSpacing, absParagraphSpacing, 0f, 0f), justif, align, heightStrat, false);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas FitRichTextLine(RectangleExpression rect, IExpression<RichString> text, FloatExpression? maxFontSize, FloatExpression lineSpacing, EnumExpression<Justification> justification, EnumExpression<SharpSheets.Canvas.Text.Alignment> alignment, EnumExpression<TextHeightStrategy> heightStrategy) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Layouts.Rectangle absRect = TransformRectangle(rect);
			RichString str = Evaluate(text);
			float maxSize = Evaluate(maxFontSize, absRect.Height);
			float absLineSpacing = Evaluate(lineSpacing, 1.0f);
			Justification justif = Evaluate(justification);
			SharpSheets.Canvas.Text.Alignment align = Evaluate(alignment);
			TextHeightStrategy heightStrat = Evaluate(heightStrategy);
			ParagraphSpecification paraSpec = new ParagraphSpecification(absLineSpacing, 0f, default);
			FontSizeSearchParams searchParams = new FontSizeSearchParams(0.05f, maxSize, 0.1f); // TODO Should be able to adjust epsilon
			Canvas.FitRichTextLine(absRect, str, paraSpec, searchParams, justif, align, heightStrat);
			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas FitRichText(RectangleExpression rect, IExpression<RichString> text, FloatExpression? minFontSize, FloatExpression? maxFontSize, FloatExpression lineSpacing, FloatExpression paragraphSpacing, EnumExpression<Justification> justification, EnumExpression<SharpSheets.Canvas.Text.Alignment> alignment, EnumExpression<TextHeightStrategy> heightStrategy) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }
			Layouts.Rectangle absRect = TransformRectangle(rect);
			RichString str = Evaluate(text);
			float minSize = Evaluate(minFontSize, 0.1f);
			float maxSize = Evaluate(maxFontSize, absRect.Height);
			float absLineSpacing = Evaluate(lineSpacing, 1f);
			float absParagraphSpacing = Evaluate(paragraphSpacing, 0f);
			Justification justif = Evaluate(justification);
			SharpSheets.Canvas.Text.Alignment align = Evaluate(alignment);
			TextHeightStrategy heightStrat = Evaluate(heightStrategy);
			// TODO Should we be able to adjust paragraph indenting here?
			Canvas.FitRichText(absRect, str, new ParagraphSpecification(absLineSpacing, absParagraphSpacing, 0f, 0f), new FontSizeSearchParams(minSize, maxSize, 0.1f), justif, align, heightStrat, true);
			return this;
		}

		#endregion

		#region Fields

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas TextField(
			RectangleExpression rect,
			IExpression<string> name,
			IExpression<string>? tooltip,
			EnumExpression<TextFieldType> fieldType,
			IExpression<string>? value,
			EnumExpression<TextFormat>? format,
			FloatExpression? fontSize,
			ColorExpression? color,
			BoolExpression multiline,
			BoolExpression rich,
			EnumExpression<Justification> justification,
			IntExpression maxLen) {

			if (Canvas is null) { throw new MarkupCanvasStateException(); }

			Layouts.Rectangle absRect = TransformRectangle(rect);
			string evalName = Evaluate(name, "TEXTFIELD"); // name?.Evaluate(Environment) ?? "TEXTFIELD";
			string? evalTooltip = Evaluate(tooltip, null); // tooltip?.Evaluate(Environment);
			TextFieldType evalFieldType = Evaluate(fieldType, TextFieldType.STRING); // fieldType?.Evaluate(Environment) ?? TextFieldType.STRING;
			string? evalValue = Evaluate(value, null); // value?.Evaluate(Environment) ?? "";
			TextFormat evalFormat = Evaluate(format, TextFormat.REGULAR); // format?.Evaluate(Environment) ?? TextFormat.REGULAR;
			float evalFontSize = Evaluate(fontSize, 0f); // fontSize?.Evaluate(Environment) ?? 0f;
			Color evalColor = Evaluate(color, Canvas.GetTextColor()); // color?.Evaluate(Environment) ?? Canvas.GetTextColor();
			bool evalMultiline = Evaluate(multiline, false); // multiline?.Evaluate(Environment) ?? false;
			bool evalRich = Evaluate(rich, false); // rich?.Evaluate(Environment) ?? false;
			Justification evalJustification = Evaluate(justification, Justification.LEFT); // justification?.Evaluate(Environment) ?? Justification.LEFT;
			int evalMaxLen = Evaluate(maxLen, -1); // maxLen?.Evaluate(Environment) ?? -1;

			Canvas.TextField(absRect, evalName, evalTooltip, evalFieldType, evalValue, evalFormat, evalFontSize, evalColor, evalMultiline, evalRich, evalJustification, evalMaxLen);

			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas CheckField(RectangleExpression rect, IExpression<string> name, IExpression<string>? tooltip, EnumExpression<CheckType> checkType, ColorExpression? color) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }

			Layouts.Rectangle absRect = TransformRectangle(rect);
			string evalName = Evaluate(name, "CHECKFIELD"); // name?.Evaluate(Environment) ?? "CHECKFIELD";
			string? evalTooltip = Evaluate(tooltip, null); // tooltip?.Evaluate(Environment);
			CheckType evalCheckType = Evaluate(checkType, CheckType.CROSS); // checkType?.Evaluate(Environment) ?? CheckType.CROSS;
			Color evalColor = Evaluate(color, Canvas.GetTextColor());

			Canvas.CheckField(absRect, evalName, evalTooltip, evalCheckType, evalColor);

			return this;
		}

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="IOException"></exception>
		public MarkupCanvas ImageField(RectangleExpression rect, IExpression<string> name, IExpression<string>? tooltip) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }

			Layouts.Rectangle absRect = TransformRectangle(rect);
			string evalName = Evaluate(name, "IMAGEFIELD"); // name?.Evaluate(Environment) ?? "IMAGEFIELD";
			string? evalTooltip = Evaluate(tooltip, null); // tooltip?.Evaluate(Environment);

			// TODO Implement FilePath arguments and default image for image field

			Canvas.ImageField(absRect, evalName, evalTooltip);

			return this;
		}

		#endregion

		#endregion

		#region IAbstractShape

		/// <summary></summary>
		/// <returns> This MarkupCanvas instance. </returns>
		/// <exception cref="MarkupCanvasStateException"> If the canvas has an unclosed child canvas. </exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public MarkupCanvas DrawShape(IAreaShape shape, RectangleExpression rect) {
			if (Canvas is null) { throw new MarkupCanvasStateException(); }

			Layouts.Rectangle absRect = TransformRectangle(rect);

			shape.Draw(Canvas, absRect);

			return this;
		}

		#endregion
	}

}
