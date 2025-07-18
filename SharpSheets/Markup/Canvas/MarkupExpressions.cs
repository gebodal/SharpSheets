using System.Collections.Generic;
using System.Linq;
using SharpSheets.Evaluations;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using System;
using SharpSheets.Parsing;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Markup.Parsing;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Colors;

namespace SharpSheets.Markup.Canvas {

	public class ColorExpression : IExpression<Color> {
		public EvaluationNode Evaluation { get { return value ?? evaluation!; } }
		private readonly EvaluationNode? evaluation;
		private readonly EvaluationValue? value;
		private FloatExpression? Opacity { get; set; }

		public bool IsConstant { get { return value.HasValue && (Opacity == null || Opacity.IsConstant); } }

		public EvaluationContext Context => value?.Type.Context ?? evaluation!.Context;

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public ColorExpression(EvaluationNode evaluation) {
			EvaluationType evalType = evaluation.GetReturnType();
			if (ColorEvaluationType.IsColor(evalType) || StringEvaluationType.IsString(evalType)) {
				if (evaluation.IsConstant) {
					this.evaluation = null;
					EvaluationValue result = evaluation.Evaluate(Environments.Empty(evaluation.Context));
					if (ColorEvaluationType.TryGetColor(result, out Color color)) {
						value = new EvaluationValue(color, evaluation.Context.GetType<ColorEvaluationType>());
					}
					else if (StringEvaluationType.TryGetString(result, out string? colorStr)) {
						value = new EvaluationValue(ParseColor(colorStr), evaluation.Context.GetType<ColorEvaluationType>());
					}
					else {
						throw new EvaluationTypeException("Invalid expression type.");
					}
				}
				else {
					this.evaluation = evaluation;
					this.value = null;
				}
			}
			else {
				throw new EvaluationTypeException("Invalid expression type.");
			}
		}
		public ColorExpression(Color value, EvaluationContext context) {
			this.evaluation = null;
			this.value = new EvaluationValue(value, context.GetType<ColorEvaluationType>());
		}
		/*
		public static implicit operator ColorExpression(Color value) {
			return new ColorExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			if (evaluation is not null) {
				foreach (EvaluationName name in evaluation.GetVariables()) { yield return name; }
			}
			if (Opacity is not null) {
				foreach (EvaluationName name in Opacity.GetVariables()) { yield return name; }
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public static ColorExpression Parse(string text, IVariableBox variables) {
			try {
				return new ColorExpression(ColorUtils.Parse(text), variables.Context);
			}
			catch (FormatException) { }
			return new ColorExpression(Evaluations.Evaluation.Parse(text, variables));
		}

		/// <summary></summary>
		/// <exception cref="EvaluationTypeException"></exception>
		/// <exception cref="EvaluationCalculationException"></exception>
		public ColorExpression WithOpacity(FloatExpression opacity) {
			if (value.HasValue) {
				return new ColorExpression(value.Value) { Opacity = opacity };
			}
			else {
				return new ColorExpression(evaluation!) { Opacity = opacity };
			}
		}

		public Color Evaluate(IEnvironment environment) {
			Color result;
			if (value.HasValue && ColorEvaluationType.TryGetColor(value.Value, out Color valueColor)) {
				result = valueColor;
			}
			else {
				EvaluationValue eval = evaluation!.Evaluate(environment);
				if (ColorEvaluationType.TryGetColor(eval, out Color color)) {
					result = color;
				}
				else if (StringEvaluationType.TryGetString(eval, out string? colorStr)) {
					result = ParseColor(colorStr);
				}
				else {
					throw new EvaluationCalculationException("Invalid expression type.");
				}
			}
			if (Opacity != null) {
				float opacity = this.Opacity.Evaluate(environment);
				result = result.WithOpacity(opacity);
			}
			return result;
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		private static Color ParseColor(string colorStr) {
			try {
				return ColorUtils.Parse(colorStr);
			}
			catch (FormatException e) {
				throw new EvaluationCalculationException($"Could not parse color string \"{colorStr}\".", e);
			}
		}
	}

	public class ColorStopExpression : IExpression<ColorStop> {
		public FloatExpression Stop { get; }
		public ColorExpression Color { get; }

		public bool IsConstant { get { return Stop.IsConstant && Color.IsConstant; } }

		public EvaluationContext Context => Stop.Context;

		public ColorStopExpression(FloatExpression stop, ColorExpression color) {
			this.Stop = stop;
			this.Color = color;
		}
		public ColorStopExpression(ColorStop value, EvaluationContext context) {
			this.Stop = new FloatExpression(value.Stop, context);
			this.Color = new ColorExpression(value.Color, context);
		}
		/*
		public static implicit operator ColorStopExpression(ColorStop value) {
			return new ColorStopExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return Expressions.GetVariables(Stop, Color);
		}

		public ColorStop Evaluate(IEnvironment environment) {
			return new ColorStop(Stop.Evaluate(environment).Clamp(0f, 1f), Color.Evaluate(environment));
		}
	}

	public class DrawPointExpression : IExpression<DrawPoint> {
		public FloatExpression X { get; }
		public FloatExpression Y { get; }

		public bool IsConstant { get { return X.IsConstant && Y.IsConstant; } }

		public EvaluationContext Context => X.Context;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public DrawPointExpression(FloatExpression x, FloatExpression y) {
			X = x;
			Y = y;
		}
		public DrawPointExpression(DrawPoint value, EvaluationContext context) {
			X = new FloatExpression(value.X, context);
			Y = new FloatExpression(value.Y, context);
		}
		/*
		public static implicit operator DrawPointExpression(DrawPoint value) {
			return new DrawPointExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return Expressions.GetVariables(X, Y);
		}

		public DrawPoint Evaluate(IEnvironment environment) {
			return new DrawPoint(X.Evaluate(environment), Y.Evaluate(environment));
		}

		public static DrawPointExpression operator +(DrawPointExpression a, DrawPointExpression b) {
			return new DrawPointExpression(a.X + b.X, a.Y + b.Y);
		}
		public static DrawPointExpression operator -(DrawPointExpression a, DrawPointExpression b) {
			return new DrawPointExpression(a.X - b.X, a.Y - b.Y);
		}
	}

	public class VectorExpression : IExpression<Vector> {
		public FloatExpression X { get; }
		public FloatExpression Y { get; }

		public bool IsConstant { get { return X.IsConstant && Y.IsConstant; } }

		public EvaluationContext Context => X.Context;

		public VectorExpression(FloatExpression x, FloatExpression y) {
			X = x;
			Y = y;
		}
		public VectorExpression(Vector value, EvaluationContext context) {
			X = new FloatExpression(value.X, context);
			Y = new FloatExpression(value.Y, context);
		}
		/*
		public static implicit operator VectorExpression(Vector value) {
			return new VectorExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return Expressions.GetVariables(X, Y);
		}

		public Vector Evaluate(IEnvironment environment) {
			return new Vector(X.Evaluate(environment), Y.Evaluate(environment));
		}

		public static VectorExpression operator +(VectorExpression a, VectorExpression b) {
			return new VectorExpression(a.X + b.X, a.Y + b.Y);
		}
		public static VectorExpression operator -(VectorExpression a, VectorExpression b) {
			return new VectorExpression(a.X - b.X, a.Y - b.Y);
		}
	}

	public class SizeExpression : IExpression<Size> {
		public FloatExpression Width { get; }
		public FloatExpression Height { get; }

		public bool IsConstant => Width.IsConstant && Height.IsConstant;

		public EvaluationContext Context => Width.Context;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public SizeExpression(FloatExpression width, FloatExpression height) {
			Width = width;
			Height = height;
		}
		public SizeExpression(Size value, EvaluationContext context) {
			Width = new FloatExpression(value.Width, context);
			Height = new FloatExpression(value.Height, context);
		}
		/*
		public static implicit operator SizeExpression(Size value) {
			return new SizeExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return Expressions.GetVariables(Width, Height);
		}

		public Size Evaluate(IEnvironment environment) {
			return new Size(Width.Evaluate(environment), Height.Evaluate(environment));
		}

		public override string ToString() {
			return $"SizeExpression({Width}, {Height})";
		}
	}

	public class RectangleExpression : IExpression<Rectangle> {
		public FloatExpression X { get; }
		public FloatExpression Y { get; }
		public FloatExpression Width { get; }
		public FloatExpression Height { get; }

		public FloatExpression Left => X;
		public FloatExpression Bottom => Y;
		public FloatExpression Right => X + Width;
		public FloatExpression Top => Y + Height;

		public bool IsConstant => X.IsConstant && Y.IsConstant && Width.IsConstant && Height.IsConstant;

		public EvaluationContext Context => X.Context;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="_x" default="0"></param>
		/// <param name="_y" default="0"></param>
		/// <param name="_width" default="$width"></param>
		/// <param name="_height" default="$height"></param>
		/// <exception cref="EvaluationException"></exception>
		public RectangleExpression(FloatExpression _x, FloatExpression _y, FloatExpression _width, FloatExpression _height) {
			X = _x;
			Y = _y;
			Width = _width;
			Height = _height;
		}
		public RectangleExpression(Rectangle value, EvaluationContext context) {
			X = new FloatExpression(value.X, context);
			Y = new FloatExpression(value.Y, context);
			Width = new FloatExpression(value.Width, context);
			Height = new FloatExpression(value.Height, context);
		}
		/*
		[return: NotNullIfNotNull(nameof(value))]
		public static implicit operator RectangleExpression?(Rectangle? value) {
			if (value is null) { return null; }
			return new RectangleExpression(value);
		}
		*/

		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		[FactoryBuilder(typeof(RectangleExpression))]
		public static RectangleExpression Build(
				[LocalProperty(Default = "0")] FloatExpression x,
				[LocalProperty(Default = "0")] FloatExpression y,
				[LocalProperty(Default = "$width")] FloatExpression width,
				[LocalProperty(Default = "$height")] FloatExpression height
			) {

			return new RectangleExpression(x, y, width, height);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return Expressions.GetVariables(X, Y, Width, Height);
		}

		public Rectangle Evaluate(IEnvironment environment) {
			return new Rectangle(X.Evaluate(environment), Y.Evaluate(environment), Width.Evaluate(environment), Height.Evaluate(environment));
		}

		public override string ToString() {
			return $"RectangleExpression({X}, {Y}, {Width}, {Height})";
		}
	}

	public class ViewBoxExpression : RectangleExpression {

		/// <summary>
		/// 
		/// </summary>
		/// <param name="_x" default="0"></param>
		/// <param name="_y" default="0"></param>
		/// <param name="_width" default="$width"></param>
		/// <param name="_height" default="$height"></param>
		/// <exception cref="EvaluationException"></exception>
		public ViewBoxExpression(FloatExpression _x, FloatExpression _y, FloatExpression _width, FloatExpression _height) : base(_x, _y, _width, _height) { }

	}

	public class XLengthExpression : FloatExpression {
		public XLengthExpression(EvaluationNode node) : base(node) { }
		public XLengthExpression(float value, EvaluationContext context) : base(value, context) { }

		/*
		public static implicit operator XLengthExpression(float value) {
			return new XLengthExpression(value);
		}
		public static implicit operator XLengthExpression(EvaluationNode node) {
			return new XLengthExpression(node);
		}
		*/
	}

	public class YLengthExpression : FloatExpression {
		public YLengthExpression(EvaluationNode node) : base(node) { }
		public YLengthExpression(float value, EvaluationContext context) : base(value, context) { }

		/*
		public static implicit operator YLengthExpression(float value) {
			return new YLengthExpression(value);
		}
		public static implicit operator YLengthExpression(EvaluationNode node) {
			return new YLengthExpression(node);
		}
		*/
	}

	public class BoundingBoxLengthExpression : FloatExpression {
		public BoundingBoxLengthExpression(EvaluationNode node) : base(node) { }
		public BoundingBoxLengthExpression(float value, EvaluationContext context) : base(value, context) { }

		/*
		public static implicit operator BoundingBoxLengthExpression(float value) {
			return new BoundingBoxLengthExpression(value);
		}
		public static implicit operator BoundingBoxLengthExpression(EvaluationNode node) {
			return new BoundingBoxLengthExpression(node);
		}
		*/
	}

	public class AreaRect {
		public Rectangle? Rect { get; }
		public Margins? Margins { get; }
		public AreaRect(Rectangle? rect, Margins? margins) {
			this.Rect = rect;
			this.Margins = margins;
		}
	}

	public class AreaRectExpression : IExpression<AreaRect> {
		public RectangleExpression? Rect { get; }
		public MarginsExpression? Margins { get; }

		public bool IsConstant => (Rect?.IsConstant ?? true) && (Margins?.IsConstant ?? true);

		public EvaluationContext Context { get; }

		public AreaRectExpression(RectangleExpression? rect, MarginsExpression? margins, EvaluationContext context) {
			Rect = rect;
			Margins = margins;
			Context = context;
		}
		public AreaRectExpression(AreaRect value, EvaluationContext context) {
			Rect = value.Rect is null ? null : new RectangleExpression(value.Rect, context);
			Margins = value.Margins.HasValue ? new MarginsExpression(value.Margins.Value, context) : null;
			Context = context;
		}
		/*
		public static implicit operator AreaRectExpression(AreaRect value) {
			return new AreaRectExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return EnumerableUtils.Concat(
				Rect?.GetVariables() ?? Enumerable.Empty<EvaluationName>(),
				Margins?.GetVariables() ?? Enumerable.Empty<EvaluationName>()
				);
		}

		public AreaRect Evaluate(IEnvironment environment) {
			return new AreaRect(Rect?.Evaluate(environment), Margins?.Evaluate(environment));
		}
	}

	public class TransformExpression : IExpression<Transform> {
		public bool IsConstant => Expressions.IsConstant(A, B, C, D, E, F);
		public EvaluationContext Context => A.Context;

		public FloatExpression A { get; }
		public FloatExpression B { get; }
		public FloatExpression C { get; }
		public FloatExpression D { get; }
		public FloatExpression E { get; }
		public FloatExpression F { get; }

		public TransformExpression(FloatExpression a, FloatExpression b, FloatExpression c, FloatExpression d, FloatExpression e, FloatExpression f) {
			this.A = a;
			this.B = b;
			this.C = c;
			this.D = d;
			this.E = e;
			this.F = f;
		}
		public TransformExpression(Transform value, EvaluationContext context) {
			this.A = new FloatExpression(value.a, context);
			this.B = new FloatExpression(value.b, context);
			this.C = new FloatExpression(value.c, context);
			this.D = new FloatExpression(value.d, context);
			this.E = new FloatExpression(value.e, context);
			this.F = new FloatExpression(value.f, context);
		}
		/*
		[return: NotNullIfNotNull(nameof(value))]
		public static implicit operator TransformExpression?(Transform? value) {
			if (value is null) { return null; }
			return new TransformExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return Expressions.GetVariables(A, B, C, D, E, F);
		}

		public static TransformExpression Identity(EvaluationContext context) {
			return new TransformExpression(Transform.Identity, context);
		}

		public static TransformExpression Matrix(FloatExpression a, FloatExpression b, FloatExpression c, FloatExpression d, FloatExpression e, FloatExpression f) {
			return new TransformExpression(a, b, c, d, e, f);
		}

		public static TransformExpression Translate(FloatExpression x, FloatExpression y) {
			FloatExpression zero = new FloatExpression(0f, x.Context);
			FloatExpression one = new FloatExpression(1f, x.Context);
			return new TransformExpression(one, zero, zero, one, x, y);
		}

		public static TransformExpression Scale(FloatExpression scaleX, FloatExpression scaleY) {
			FloatExpression zero = new FloatExpression(0f, scaleX.Context);
			return new TransformExpression(scaleX, zero, zero, scaleY, zero, zero);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression Rotate(FloatExpression theta) {
			FloatExpression zero = new FloatExpression(0f, theta.Context);
			FloatExpression cos = new FloatExpression(CosFunction.Instance.MakeNode(theta.Context, theta.Evaluation.Clone()));
			FloatExpression sin = new FloatExpression(SinFunction.Instance.MakeNode(theta.Context, theta.Evaluation.Clone()));
			return new TransformExpression(cos, sin, -sin, cos, zero, zero);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression Rotate(FloatExpression theta, FloatExpression x, FloatExpression y) {
			// For rotating about an arbitrary point
			// TODO Verify this is correct
			FloatExpression cos = new FloatExpression(CosFunction.Instance.MakeNode(theta.Context, theta.Evaluation.Clone()));
			FloatExpression sin = new FloatExpression(SinFunction.Instance.MakeNode(theta.Context, theta.Evaluation.Clone()));
			FloatExpression e = x - x * cos + y * sin;
			FloatExpression f = y - x * sin - y * cos;
			return new TransformExpression(cos, sin, -sin, cos, e, f);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression Skew(FloatExpression x, FloatExpression y) {
			FloatExpression zero = new FloatExpression(0f, x.Context);
			FloatExpression one = new FloatExpression(1f, x.Context);
			FloatExpression tanX = new FloatExpression(TanFunction.Instance.MakeNode(x.Context, x.Evaluation.Clone()));
			FloatExpression tanY = new FloatExpression(TanFunction.Instance.MakeNode(y.Context, y.Evaluation.Clone()));
			return new TransformExpression(one, tanY, tanX, one, zero, zero);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression SkewX(FloatExpression theta) {
			FloatExpression zero = new FloatExpression(0f, theta.Context);
			FloatExpression one = new FloatExpression(1f, theta.Context);
			FloatExpression tan = new FloatExpression(TanFunction.Instance.MakeNode(theta.Context, theta.Evaluation.Clone()));
			return new TransformExpression(one, zero, tan, one, zero, zero);
		}
		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression SkewY(FloatExpression theta) {
			FloatExpression zero = new FloatExpression(0f, theta.Context);
			FloatExpression one = new FloatExpression(1f, theta.Context);
			FloatExpression tan = new FloatExpression(TanFunction.Instance.MakeNode(theta.Context, theta.Evaluation.Clone()));
			return new TransformExpression(one, tan, zero, one, zero, zero);
		}

		public Transform Evaluate(IEnvironment environment) {
			return Transform.Matrix(
				A.Evaluate(environment),
				B.Evaluate(environment),
				C.Evaluate(environment),
				D.Evaluate(environment),
				E.Evaluate(environment),
				F.Evaluate(environment)
				);
		}

		public static TransformExpression operator *(TransformExpression t1, TransformExpression t2) {
			// TODO Verify this is correct
			FloatExpression a = (t1.A * t2.A) + (t1.C * t2.B);
			FloatExpression c = (t1.A * t2.C) + (t1.C * t2.D);
			FloatExpression e = (t1.A * t2.E) + (t1.C * t2.F) + t1.E;
			FloatExpression b = (t1.B * t2.A) + (t1.D * t2.B);
			FloatExpression d = (t1.B * t2.C) + (t1.D * t2.D);
			FloatExpression f = (t1.B * t2.E) + (t1.D * t2.F) + t1.F;
			return new TransformExpression(a, b, c, d, e, f);
		}
	}

	public class NSliceValuesExpression : IExpression<NSliceValues> {

		public bool IsConstant { get { return value != null; } }
		public EvaluationContext Context { get; }

		private readonly FloatExpression[]? xs;
		private readonly FloatExpression[]? ys;
		private readonly NSliceValues? value;

		public NSliceValuesExpression(FloatExpression[] xs, FloatExpression[] ys, EvaluationContext context) {
			this.xs = xs;
			this.ys = ys;
			this.value = null;
			this.Context = context;
		}
		public NSliceValuesExpression(NSliceValues value, EvaluationContext context) {
			this.xs = null;
			this.xs = null;
			this.value = value;
			this.Context = context;
		}
		/*
		public static implicit operator NSliceValuesExpression(NSliceValues value) {
			return new NSliceValuesExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			if (IsConstant) {
				yield break;
			}
			else {
				foreach(EvaluationName key in xs!.SelectMany(x => x.GetVariables())) {
					yield return key;
				}
				foreach (EvaluationName key in ys!.SelectMany(y => y.GetVariables())) {
					yield return key;
				}
			}
		}

		public NSliceValues Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return new NSliceValues(xs!.Select(x=>x.Evaluate(environment)).ToArray(), ys!.Select(y=>y.Evaluate(environment)).ToArray());
			}
		}
	}

	public class MarginsExpression : IExpression<Margins> {

		public bool IsConstant {
			get {
				if (expressions.HasValue) {
					return expressions.Value.top.IsConstant
						&& expressions.Value.right.IsConstant
						&& expressions.Value.bottom.IsConstant
						&& expressions.Value.left.IsConstant;
				}
				else {
					return node!.IsConstant;
				}
			}
		}

		public EvaluationContext Context => node?.Context ?? expressions!.Value.top.Context;

		public FloatExpression Top {
			get {
				if (node is not null) { return new MarginAttributeNode(node, m => m.Top); }
				else { return expressions!.Value.top; }
			}
		}
		public FloatExpression Right {
			get {
				if (node is not null) { return new MarginAttributeNode(node, m => m.Right); }
				else { return expressions!.Value.right; }
			}
		}
		public FloatExpression Bottom {
			get {
				if (node is not null) { return new MarginAttributeNode(node, m => m.Bottom); }
				else { return expressions!.Value.bottom; }
			}
		}
		public FloatExpression Left {
			get {
				if (node is not null) { return new MarginAttributeNode(node, m => m.Left); }
				else { return expressions!.Value.left; }
			}
		}

		private readonly (FloatExpression top, FloatExpression right, FloatExpression bottom, FloatExpression left)? expressions;
		private readonly EvaluationNode? node;

		public MarginsExpression(FloatExpression top, FloatExpression right, FloatExpression bottom, FloatExpression left) {
			this.expressions = (top, right, bottom, left);
			this.node = null;
		}
		public MarginsExpression(EvaluationNode node) {
			if(!MarginsEvaluationType.IsMargins(node.GetReturnType())) {
				throw new EvaluationTypeException("Evaluation does not return the expected Margins value.");
			}

			this.expressions = null;
			this.node = node;
		}
		public MarginsExpression(Margins value, EvaluationContext context) {
			this.expressions = (
				new FloatExpression(value.Top, context),
				new FloatExpression(value.Right, context),
				new FloatExpression(value.Bottom, context),
				new FloatExpression(value.Left, context)
				);
			this.node = null;
		}
		/*
		public static implicit operator MarginsExpression(Margins value) {
			return new MarginsExpression(value);
		}
		*/

		public MarginsExpression(FloatExpression border) : this(border, border, border, border) { }
		public MarginsExpression(FloatExpression vertical, FloatExpression horizontal) : this(vertical, horizontal, vertical, horizontal) { }

		public IEnumerable<EvaluationName> GetVariables() {
			if(node is not null) {
				return node.GetVariables();
			}
			else {
				return Expressions.GetVariables(expressions!.Value.top, expressions!.Value.right, expressions!.Value.bottom, expressions!.Value.left);
			}
		}

		public Margins Evaluate(IEnvironment environment) {
			if (node is not null) {
				EvaluationValue result = node.Evaluate(environment);
				if (MarginsEvaluationType.TryGetMargins(result, out Margins margins)) {
					return margins;
				}
				else {
					throw new EvaluationCalculationException($"Expected {nameof(Margins)} value.");
				}
			}
			else {
				return new Margins(
					expressions!.Value.top.Evaluate(environment),
					expressions!.Value.right.Evaluate(environment),
					expressions!.Value.bottom.Evaluate(environment),
					expressions!.Value.left.Evaluate(environment)
					);
			}
		}

		public override string ToString() {
			if (node is not null) {
				return $"MarginsExpression({node})";
			}
			else {
				return $"MarginsExpression(top: {expressions!.Value.top}, right: {expressions!.Value.right}, bottom: {expressions!.Value.bottom}, left: {expressions!.Value.left})";
			}
		}

		private class MarginAttributeNode : EvaluationNode {
			public override bool IsConstant => subject.IsConstant;

			public override EvaluationType GetReturnType() => Context.GetType<FloatEvaluationType>();

			private readonly EvaluationNode subject;
			private readonly Func<Margins, float> action;

			public MarginAttributeNode(EvaluationNode subject, Func<Margins, float> action) : base(subject.Context) {
				if(!MarginsEvaluationType.IsMargins(subject.GetReturnType())) {
					throw new EvaluationTypeException($"{nameof(subject)} must return a {nameof(Margins)} value.");
				}

				this.subject = subject;
				this.action = action;
			}

			public override EvaluationValue Evaluate(IEnvironment environment) {
				EvaluationValue result = subject.Evaluate(environment);

				if(MarginsEvaluationType.TryGetMargins(result, out Margins margins)) {
					return new EvaluationValue(action(margins), Context.GetType<FloatEvaluationType>());
				}
				else {
					throw new EvaluationCalculationException($"Expected {nameof(Margins)} value.");
				}
			}

			public override IEnumerable<EvaluationName> GetVariables() {
				return subject.GetVariables();
			}

			public override EvaluationNode Clone() {
				return new MarginAttributeNode(subject.Clone(), action);
			}

			public override EvaluationNode Simplify() {
				return new MarginAttributeNode(subject.Simplify(), action);
			}

			protected override string GetRepresentation() {
				return nameof(MarginAttributeNode);
			}
		}
	}

	public class PreserveAspectRatioExpression : IExpression<PreserveAspectRatio> {

		public bool IsConstant { get { return value != null; } }
		public EvaluationContext Context { get; }

		private readonly EvaluationNode? evaluation;
		private readonly PreserveAspectRatio? value;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public PreserveAspectRatioExpression(EvaluationNode evaluation) {
			if (!StringEvaluationType.IsString(evaluation.GetReturnType())) {
				throw new EvaluationTypeException($"Invalid evaluation type for {nameof(PreserveAspectRatioExpression)}.");
			}
			else if (evaluation.IsConstant) {
				this.evaluation = null;
				this.value = Parse(evaluation, Environments.Empty(evaluation.Context));
			}
			else {
				this.evaluation = evaluation;
				this.value = null;
			}
			this.Context = evaluation.Context;
		}
		public PreserveAspectRatioExpression(PreserveAspectRatio value, EvaluationContext context) {
			this.evaluation = null;
			this.value = value;
			this.Context = context;
		}
		/*
		public static implicit operator PreserveAspectRatioExpression(PreserveAspectRatio value) {
			return new PreserveAspectRatioExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : evaluation!.GetVariables();
		}

		public PreserveAspectRatio Evaluate(IEnvironment environment) {
			if (value.HasValue) {
				return value.Value;
			}
			else {
				return Parse(evaluation!, environment);
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		private static PreserveAspectRatio Parse(EvaluationNode node, IEnvironment environment) {
			if(StringEvaluationType.TryGetString(node.Evaluate(environment), out string? value)) {
				try {
					return PreserveAspectRatio.Parse(value);
				}
				catch (FormatException e) {
					throw new EvaluationCalculationException($"Cannot parse expression into {nameof(PreserveAspectRatio)}", e);
				}
			}
			else {
				throw new EvaluationTypeException($"Invalid evaluation type for {nameof(PreserveAspectRatioExpression)}.");
			}
		}

		public override string ToString() {
			if (IsConstant) {
				return $"DimensionExpression({value})";
			}
			else {
				return $"DimensionExpression({evaluation})";
			}
		}
	}

	public class DimensionExpression : IExpression<Dimension> {

		public bool IsConstant { get { return value != null; } }
		public EvaluationContext Context => value?.Type.Context ?? evaluation!.Context;

		private readonly EvaluationNode? evaluation;
		private readonly EvaluationValue? value;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public DimensionExpression(EvaluationNode evaluation) {
			if(!DimensionEvaluationType.IsDimension(evaluation.GetReturnType())) {
				throw new EvaluationTypeException("Invalid evaluation type for DimensionExpression.");
			}
			else if (evaluation.IsConstant) {
				this.evaluation = null;
				this.value = evaluation.Evaluate(Environments.Empty(evaluation.Context)); // (Dimension)(evaluation.Evaluate(Environments.Empty) ?? throw new EvaluationCalculationException("Provided constant evaluation does not produce a value."));
			}
			else {
				this.evaluation = evaluation;
				this.value = null;
			}
		}
		public DimensionExpression(Dimension value, EvaluationContext context) {
			this.evaluation = null;
			this.value = new EvaluationValue(value, context.GetType<DimensionEvaluationType>());
		}
		/*
		public static implicit operator DimensionExpression(Dimension value) {
			return new DimensionExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : evaluation!.GetVariables();
		}

		public Dimension Evaluate(IEnvironment environment) {
			if (value.HasValue && DimensionEvaluationType.TryGetDimension(value.Value, out Dimension dimensionVal)) {
				return dimensionVal;
			}
			else {
				//return (Dimension)(evaluation!.Evaluate(environment) ?? throw new EvaluationCalculationException("Evaluation does not produce a value."));
				EvaluationValue result = evaluation!.Evaluate(environment); // ?? throw new EvaluationCalculationException("Evaluation does not produce a value.");

				if (DimensionEvaluationType.TryGetDimension(result, out Dimension dimensionValue)) {
					return dimensionValue;
				}
				else {
					throw new EvaluationCalculationException($"Evaluation does not produce a valid {nameof(Dimension)} value.");
				}
			}
		}

		public override string ToString() {
			if (IsConstant) {
				return $"DimensionExpression({value})";
			}
			else {
				return $"DimensionExpression({evaluation})";
			}
		}
	}

	public class PositionExpression : IExpression<Position> {

		public bool IsConstant { get { return value != null; } }
		public EvaluationContext Context { get; }

		public EnumExpression<Anchor>? Anchor { get { return value.HasValue ? new EnumExpression<Anchor>(value.Value.Anchor, Context) : anchor; } }
		public DimensionExpression? X { get { return value.HasValue ? new DimensionExpression(value.Value.X, Context) : x; } }
		public DimensionExpression? Y { get { return value.HasValue ? new DimensionExpression(value.Value.Y, Context) : y; } }
		public DimensionExpression? Width { get { return value.HasValue ? new DimensionExpression(value.Value.Width, Context) : width; } }
		public DimensionExpression? Height { get { return value.HasValue ? new DimensionExpression(value.Value.Height, Context) : height; } }

		public readonly EnumExpression<Anchor>? anchor;
		public readonly DimensionExpression? x;
		public readonly DimensionExpression? y;
		public readonly DimensionExpression? width;
		public readonly DimensionExpression? height;
		private readonly Position? value;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="_anchor" default="null">The anchor point for the position, which
		/// will determine the origin of the coordinates when determining the extend of
		/// the area.</param>
		/// <param name="_x" default="null">The x-coordinate of the position.</param>
		/// <param name="_y" default="null">The y-coordinate of the position.</param>
		/// <param name="_width" default="null">The width for the position area.</param>
		/// <param name="_height" default="null">The height for the position area.</param>
		public PositionExpression(EnumExpression<Anchor>? _anchor, DimensionExpression? _x, DimensionExpression? _y, DimensionExpression? _width, DimensionExpression? _height) {
			this.anchor = _anchor;
			this.x = _x;
			this.y = _y;
			this.width = _width;
			this.height = _height;
			this.value = null;
			this.Context = _anchor?.Context ?? _x?.Context ?? _y?.Context ?? _width?.Context ?? _height?.Context ?? throw new InvalidOperationException("Position must have at least one non-null argument.");
		}
		public PositionExpression(Position value, EvaluationContext context) {
			this.anchor = null;
			this.x = null;
			this.y = null;
			this.width = null;
			this.height = null;
			this.value = value;
			this.Context = context;
		}
		/*
		public static implicit operator PositionExpression(Position value) {
			return new PositionExpression(value);
		}
		*/

		/// <param name="anchor">The anchor point for the position, which
		/// will determine the origin of the coordinates when determining the extend of
		/// the area.</param>
		/// <param name="x">The x-coordinate of the position.</param>
		/// <param name="y">The y-coordinate of the position.</param>
		/// <param name="width">The width for the position area.</param>
		/// <param name="height">The height for the position area.</param>
		[FactoryBuilder(typeof(PositionExpression))]
		public static PositionExpression Build(
				[LocalProperty(Default = "null")] EnumExpression<Anchor>? anchor,
				[LocalProperty(Default = "null")] DimensionExpression? x,
				[LocalProperty(Default = "null")] DimensionExpression? y,
				[LocalProperty(Default = "null")] DimensionExpression? width,
				[LocalProperty(Default = "null")] DimensionExpression? height
			) {

			return new PositionExpression(anchor, x, y, width, height);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() :
				Enumerable.Empty<EvaluationName>()
				.ConcatOrNothing(anchor?.GetVariables())
				.ConcatOrNothing(x?.GetVariables())
				.ConcatOrNothing(y?.GetVariables())
				.ConcatOrNothing(width?.GetVariables())
				.ConcatOrNothing(height?.GetVariables());
		}

		public Position Evaluate(IEnvironment environment) {
			if (value.HasValue) {
				return value.Value;
			}
			else {
				return new Position(
					anchor?.Evaluate(environment) ?? Layouts.Anchor.BOTTOMLEFT,
					x?.Evaluate(environment) ?? Dimension.FromPoints(0),
					y?.Evaluate(environment) ?? Dimension.FromPoints(0),
					width?.Evaluate(environment) ?? Dimension.FromPercent(100),
					height?.Evaluate(environment) ?? Dimension.FromPercent(100));
			}
		}

		public override string ToString() {
			return $"PositionExpression(anchor: {Anchor}, x: {X}, y: {Y}, width: {Width}, height: {Height})";
		}
	}

	public class RichStringExpression : IExpression<RichString> {
		public bool IsConstant { get { return value != null; } }
		public EvaluationContext Context { get; }

		public readonly EnumExpression<TextFormat>? startingFormat;
		public readonly StringExpression? text;
		private readonly RichString? value;

		public RichStringExpression(StringExpression text, EnumExpression<TextFormat>? startingFormat) {
			this.startingFormat = startingFormat;
			this.text = text;
			this.value = null;
			this.Context = text.Context;
		}
		public RichStringExpression(RichString value, EvaluationContext context) {
			this.startingFormat = null;
			this.text = null;
			this.value = value;
			this.Context = context;
		}
		public static implicit operator RichStringExpression(StringExpression value) {
			return new RichStringExpression(value, null);
		}
		/*
		public static implicit operator RichStringExpression(RichString value) {
			return new RichStringExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : text!.GetVariables().ConcatOrNothing(startingFormat?.GetVariables());
		}

		public RichString Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return new RichString(text!.Evaluate(environment), startingFormat?.Evaluate(environment) ?? TextFormat.REGULAR);
			}
		}

		public override string ToString() {
			return $"RichStringExpression(text: {value?.ToString() ?? text?.ToString()}, startingFormat: {startingFormat?.ToString() ?? TextFormat.REGULAR.ToString()})";
		}
	}

	public class ContextExpression : IExpression<IContext> {
		public bool IsConstant { get; } = false;
		public EvaluationContext Context { get; }

		private readonly IExpression<string> name;
		private readonly Dictionary<string, EvaluationNode> values;

		public ContextExpression(IExpression<string> name, Dictionary<string, EvaluationNode> values) {
			this.name = name;
			this.values = values;
			this.Context = name.Context;
		}

		public IContext Evaluate(IEnvironment environment) {
			try {
				string name = this.name.Evaluate(environment);
				Dictionary<string, EvaluationValue> values = this.values.ToDictionary(kv => kv.Key, kv => kv.Value.Evaluate(environment), StringComparer.InvariantCultureIgnoreCase);

				Dictionary<string, string> properties = values.Where(kv => !BoolEvaluationType.IsBool(kv.Value.Type)).ToDictionary(kv => kv.Key, kv => ValueParsing.ToString(kv.Value.Value));
				Dictionary<string, bool> flags = values.Where(kv => BoolEvaluationType.IsBool(kv.Value.Type)).ToDictionary(kv => kv.Key, kv => BoolEvaluationType.TryGetBool(kv.Value, out bool flag) ? flag : throw new EvaluationCalculationException("Invalid bool value."));

				return SharpSheets.Parsing.Context.Simple(name, properties, flags);
			}
			catch(SystemException e) {
				throw new EvaluationCalculationException("Error constructing context environment expression: " + e.Message, e);
			}
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return values.SelectMany(kv => kv.Value.GetVariables()).Distinct();
		}
	}

	public class Length {
		private readonly float fixedLength;
		private readonly float? percentage;

		public static readonly Length Zero = new Length(0f, null);

		private Length(float fixedLength, float? percentage) {
			this.fixedLength = fixedLength;
			this.percentage = percentage;
		}

		public static Length FromFixedLength(float fixedLength) {
			return new Length(fixedLength, null);
		}

		public static Length FromPercentage(float percentage) {
			return new Length(0f, percentage);
		}

		public float GetLength(float length) {
			if (percentage.HasValue) {
				return percentage.Value * length;
			}
			else {
				return fixedLength;
			}
		}
	}

	public class LengthExpression : IExpression<Length> {
		public bool IsConstant { get { return value != null; } }
		public EvaluationContext Context { get; }

		public readonly FloatExpression? fixedLength;
		private readonly Length? value;

		public LengthExpression(FloatExpression fixedLength) {
			this.fixedLength = fixedLength;
			this.value = null;
			this.Context = fixedLength.Context;
		}
		public LengthExpression(Length value, EvaluationContext context) {
			this.fixedLength = null;
			this.value = value;
			this.Context = context;
		}
		/*
		[return: NotNullIfNotNull(nameof(value))]
		public static implicit operator LengthExpression?(Length? value) {
			if (value is null) { return null; }
			return new LengthExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : fixedLength!.GetVariables();
		}

		public Length Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return Length.FromFixedLength(fixedLength!.Evaluate(environment));
			}
		}
	}

	public class FilePathExpression : IExpression<FilePath> {
		[MemberNotNullWhen(true, nameof(value))]
		[MemberNotNullWhen(false, nameof(evaluation))]
		public bool IsConstant { get { return value != null; } }
		public EvaluationContext Context { get; }

		private readonly EvaluationNode[]? evaluation;
		private readonly FilePath? value;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public FilePathExpression(EvaluationNode basePath, params EvaluationNode[] additional) {
			this.Context = basePath.Context;

			List<EvaluationNode> evaluations = basePath.Yield().Concat(additional).ToList();

			if (!evaluations.All(e => FilePathEvaluationType.IsFilePath(e.GetReturnType()) || StringEvaluationType.IsString(e.GetReturnType()))) {
				throw new EvaluationTypeException("Invalid evaluation types for FilePathExpression: " + string.Join(", ", evaluations.Select(e => e.GetReturnType())));
			}
			else if (evaluations.All(e=>e.IsConstant)) {
				this.evaluation = null;
				this.value = EvaluatePath(evaluations, Environments.Empty(this.Context));
			}
			else {
				this.evaluation = evaluations.ToArray();
				this.value = null;
			}
		}
		public FilePathExpression(FilePath value, EvaluationContext context) {
			this.evaluation = null;
			this.value = value;
			this.Context = context;
		}
		/*
		[return: NotNullIfNotNull(nameof(value))]
		public static implicit operator FilePathExpression?(FilePath? value) {
			if(value is null) { return null; }
			return new FilePathExpression(value);
		}
		*/

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : evaluation.SelectMany(e => e.GetVariables());
		}

		public FilePath Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return EvaluatePath(evaluation!, environment);
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		private static FilePath EvaluatePath(IEnumerable<EvaluationNode> evaluations, IEnvironment environment) {
			EvaluationValue[] absVals = evaluations.Select(e => e.Evaluate(environment)).ToArray();

			if (absVals.Length == 1) {
				if (FilePathEvaluationType.TryGetFilePath(absVals[0], out FilePath? filePath)) {
					return filePath;
				}
				else if (StringEvaluationType.TryGetString(absVals[0], out string? fileString)) {
					return new FilePath(fileString);
				}
				else {
					throw new EvaluationTypeException("Invalid result type for FilePathExpression: " + absVals[0].Type.Name);
				}
			}
			else if (absVals.Length > 0) {
				string[] parts = absVals.Select(e => {
					if (FilePathEvaluationType.TryGetFilePath(e, out FilePath? path)) {
						return path.ToString();
					}
					else if (StringEvaluationType.TryGetString(e, out string? text)) {
						return text;
					}
					else {
						throw new EvaluationCalculationException("Invalid component type for file path expression");
					}
				}).ToArray();
				return new FilePath(parts);
			}
			else {
				throw new EvaluationTypeException("No values provided for file path expression.");
			}
		}

		public override string ToString() {
			if (IsConstant) {
				return $"FilePathExpression({value})";
			}
			else {
				return $"FilePathExpression({string.Join(", ", evaluation!.Select(e => e.ToString()))})";
			}
		}

	}

}
