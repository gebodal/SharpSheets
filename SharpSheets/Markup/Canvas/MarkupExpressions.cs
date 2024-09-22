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

namespace SharpSheets.Markup.Canvas {

	public class ColorStopExpression : IExpression<ColorStop> {
		public FloatExpression Stop { get { return IsConstant ? value!.Stop : stop!; } }
		public ColorExpression Color { get { return IsConstant ? value!.Color : color!; } }

		public bool IsConstant { get { return value != null; } }

		private readonly FloatExpression? stop;
		private readonly ColorExpression? color;
		private readonly ColorStop? value;

		public ColorStopExpression(FloatExpression stop, ColorExpression color) {
			this.stop = stop;
			this.color = color;
			this.value = null;
		}
		public ColorStopExpression(ColorStop value) {
			this.stop = null;
			this.color = null;
			this.value = value;
		}
		public static implicit operator ColorStopExpression(ColorStop value) {
			return new ColorStopExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : stop!.GetVariables().Concat(color!.GetVariables());
		}

		public ColorStop Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return new ColorStop(stop!.Evaluate(environment).Clamp(0f, 1f), color!.Evaluate(environment));
			}
		}
	}

	public class DrawPointExpression : IExpression<DrawPoint> {
		public FloatExpression X { get { return value.HasValue ? value.Value.X : x!; } }
		public FloatExpression Y { get { return value.HasValue ? value.Value.Y : y!; } }

		public bool IsConstant { get { return value.HasValue; } }

		private readonly FloatExpression? x;
		private readonly FloatExpression? y;
		private readonly DrawPoint? value;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public DrawPointExpression(FloatExpression x, FloatExpression y) {
			if (x.IsConstant && y.IsConstant) {
				this.x = null;
				this.y = null;
				this.value = new DrawPoint(x.Evaluate(Environments.Empty), y.Evaluate(Environments.Empty));
			}
			else {
				this.x = x;
				this.y = y;
				this.value = null;
			}
		}
		public DrawPointExpression(DrawPoint value) {
			this.x = null;
			this.y = null;
			this.value = value;
		}
		public static implicit operator DrawPointExpression(DrawPoint value) {
			return new DrawPointExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : x!.GetVariables().Concat(y!.GetVariables());
		}

		public DrawPoint Evaluate(IEnvironment environment) {
			if (value.HasValue) {
				return value.Value;
			}
			else {
				return new DrawPoint(x!.Evaluate(environment), y!.Evaluate(environment));
			}
		}

		public static DrawPointExpression operator +(DrawPointExpression a, DrawPointExpression b) {
			return new DrawPointExpression(a.X + b.X, a.Y + b.Y);
		}
		public static DrawPointExpression operator -(DrawPointExpression a, DrawPointExpression b) {
			return new DrawPointExpression(a.X - b.X, a.Y - b.Y);
		}
	}

	public class VectorExpression : IExpression<Vector> {
		public FloatExpression X { get { return value.HasValue ? value.Value.X : x!; } }
		public FloatExpression Y { get { return value.HasValue ? value.Value.Y : y!; } }

		public bool IsConstant { get { return value.HasValue; } }

		private readonly FloatExpression? x;
		private readonly FloatExpression? y;
		private readonly Vector? value;

		public VectorExpression(FloatExpression x, FloatExpression y) {
			this.x = x;
			this.y = y;
			this.value = null;
		}
		public VectorExpression(Vector value) {
			this.x = null;
			this.y = null;
			this.value = value;
		}
		public static implicit operator VectorExpression(Vector value) {
			return new VectorExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : x!.GetVariables().Concat(y!.GetVariables());
		}

		public Vector Evaluate(IEnvironment environment) {
			if (value.HasValue) {
				return value.Value;
			}
			else {
				return new Vector(x!.Evaluate(environment), y!.Evaluate(environment));
			}
		}

		public static VectorExpression operator +(VectorExpression a, VectorExpression b) {
			return new VectorExpression(a.X + b.X, a.Y + b.Y);
		}
		public static VectorExpression operator -(VectorExpression a, VectorExpression b) {
			return new VectorExpression(a.X - b.X, a.Y - b.Y);
		}
	}

	public class SizeExpression : IExpression<Size> {
		public FloatExpression Width { get { return value != null ? value.Width : width!; } }
		public FloatExpression Height { get { return value != null ? value.Height : height!; } }

		public bool IsConstant { get { return value != null; } }

		private readonly FloatExpression? width;
		private readonly FloatExpression? height;
		private readonly Size? value;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public SizeExpression(FloatExpression width, FloatExpression height) {
			if (width.IsConstant && height.IsConstant) {
				this.width = null;
				this.height = null;
				this.value = EvaluateSize(Environments.Empty, width, height);
			}
			else {
				this.width = width;
				this.height = height;
				this.value = null;
			}
		}
		public SizeExpression(Size value) {
			this.width = null;
			this.height = null;
			this.value = value;
		}
		public static implicit operator SizeExpression(Size value) {
			return new SizeExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : width!.GetVariables().Concat(height!.GetVariables());
		}

		public Size Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return EvaluateSize(environment, width!, height!);
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		private static Size EvaluateSize(IEnvironment environment, FloatExpression width, FloatExpression height) {
			return new Size(width.Evaluate(environment), height.Evaluate(environment));
		}

		public override string ToString() {
			return $"SizeExpression({Width}, {Height})";
		}
	}

	public class RectangleExpression : IExpression<Rectangle> {
		public FloatExpression X { get { return value != null ? value.X : x!; } }
		public FloatExpression Y { get { return value != null ? value.Y : y!; } }
		public FloatExpression Width { get { return value != null ? value.Width : width!; } }
		public FloatExpression Height { get { return value != null ? value.Height : height!; } }

		public FloatExpression Left { get { return value != null ? value.Left : x!; } }
		public FloatExpression Bottom { get { return value != null ? value.Bottom : y!; } }
		public FloatExpression Right { get { return value != null ? value.Right : (x! + width!); } }
		public FloatExpression Top { get { return value != null ? value.Top : (y! + height!); } }

		public bool IsConstant { get { return value != null; } }

		private readonly FloatExpression? x;
		private readonly FloatExpression? y;
		private readonly FloatExpression? width;
		private readonly FloatExpression? height;
		private readonly Rectangle? value;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="_x" default="0"></param>
		/// <param name="_y" default="0"></param>
		/// <param name="_width" default="$width"></param>
		/// <param name="_height" default="$height"></param>
		/// <exception cref="EvaluationException"></exception>
		public RectangleExpression(FloatExpression _x, FloatExpression _y, FloatExpression _width, FloatExpression _height) {
			if(_x.IsConstant && _y.IsConstant && _width.IsConstant && _height.IsConstant) {
				this.x = null;
				this.y = null;
				this.width = null;
				this.height = null;
				this.value = EvaluateRect(Environments.Empty, _x, _y, _width, _height);
			}
			else {
				this.x = _x;
				this.y = _y;
				this.width = _width;
				this.height = _height;
				this.value = null;
			}
		}
		public RectangleExpression(Rectangle value) {
			this.x = null;
			this.y = null;
			this.width = null;
			this.height = null;
			this.value = value;
		}
		[return: NotNullIfNotNull(nameof(value))]
		public static implicit operator RectangleExpression?(Rectangle? value) {
			if (value is null) { return null; }
			return new RectangleExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : x!.GetVariables().Concat(y!.GetVariables()).Concat(width!.GetVariables()).Concat(height!.GetVariables());
		}

		public Rectangle Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return EvaluateRect(environment, x!, y!, width!, height!);
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		private static Rectangle EvaluateRect(IEnvironment environment, FloatExpression x, FloatExpression y, FloatExpression width, FloatExpression height) {
			return new Rectangle(x.Evaluate(environment), y.Evaluate(environment), width.Evaluate(environment), height.Evaluate(environment));
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
		public XLengthExpression(float value) : base(value) { }

		public static implicit operator XLengthExpression(float value) {
			return new XLengthExpression(value);
		}
	}

	public class YLengthExpression : FloatExpression {
		public YLengthExpression(EvaluationNode node) : base(node) { }
		public YLengthExpression(float value) : base(value) { }

		public static implicit operator YLengthExpression(float value) {
			return new YLengthExpression(value);
		}
	}

	public class BoundingBoxLengthExpression : FloatExpression {
		public BoundingBoxLengthExpression(EvaluationNode node) : base(node) { }
		public BoundingBoxLengthExpression(float value) : base(value) { }

		public static implicit operator BoundingBoxLengthExpression(float value) {
			return new BoundingBoxLengthExpression(value);
		}
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
		public RectangleExpression? Rect { get { return value != null ? value.Rect : rect; } }
		public MarginsExpression? Margins { get { return value != null ? value.Margins : margins; } }

		private IEnumerable<EvaluationName> RectVariables { get { return rect != null ? rect.GetVariables() : Enumerable.Empty<EvaluationName>(); } }
		private IEnumerable<EvaluationName> MarginsVariables { get { return margins != null ? margins.GetVariables() : Enumerable.Empty<EvaluationName>(); } }

		public bool IsConstant { get { return value != null; } }

		private readonly RectangleExpression? rect;
		private readonly MarginsExpression? margins;
		private readonly AreaRect? value;

		public AreaRectExpression(RectangleExpression? rect, MarginsExpression? margins) {
			this.rect = rect;
			this.margins = margins;
			this.value = null;
		}
		public AreaRectExpression(AreaRect value) {
			this.rect = null;
			this.margins = null;
			this.value = value;
		}
		public static implicit operator AreaRectExpression(AreaRect value) {
			return new AreaRectExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : RectVariables.Concat(MarginsVariables);
		}

		public AreaRect Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return new AreaRect(rect?.Evaluate(environment), margins?.Evaluate(environment));
			}
		}
	}

	public class TransformExpression : IExpression<Transform> {
		public bool IsConstant { get { return value != null; } }

		public FloatExpression A { get { return value != null ? value.a : a!; } }
		public FloatExpression B { get { return value != null ? value.b : b!; } }
		public FloatExpression C { get { return value != null ? value.c : c!; } }
		public FloatExpression D { get { return value != null ? value.d : d!; } }
		public FloatExpression E { get { return value != null ? value.e : e!; } }
		public FloatExpression F { get { return value != null ? value.f : f!; } }

		private readonly FloatExpression? a;
		private readonly FloatExpression? b;
		private readonly FloatExpression? c;
		private readonly FloatExpression? d;
		private readonly FloatExpression? e;
		private readonly FloatExpression? f;
		private readonly Transform? value;

		public TransformExpression(FloatExpression a, FloatExpression b, FloatExpression c, FloatExpression d, FloatExpression e, FloatExpression f) {
			this.a = a;
			this.b = b;
			this.c = c;
			this.d = d;
			this.e = e;
			this.f = f;
			this.value = null;
		}
		public TransformExpression(Transform value) {
			this.a = null;
			this.b = null;
			this.c = null;
			this.d = null;
			this.e = null;
			this.f = null;
			this.value = value;
		}
		[return: NotNullIfNotNull(nameof(value))]
		public static implicit operator TransformExpression?(Transform? value) {
			if (value is null) { return null; }
			return new TransformExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			if (IsConstant) {
				return Enumerable.Empty<EvaluationName>();
			}
			else {
				return new FloatExpression[] { a!, b!, c!, d!, e!, f! }.SelectMany(node => node.GetVariables());
			}
		}

		public static TransformExpression Identity() {
			return new TransformExpression(1, 0, 0, 1, 0, 0);
		}

		public static TransformExpression Matrix(FloatExpression a, FloatExpression b, FloatExpression c, FloatExpression d, FloatExpression e, FloatExpression f) {
			return new TransformExpression(a, b, c, d, e, f);
		}

		public static TransformExpression Translate(FloatExpression x, FloatExpression y) {
			return new TransformExpression(1, 0, 0, 1, x, y);
		}

		public static TransformExpression Scale(FloatExpression scaleX, FloatExpression scaleY) {
			return new TransformExpression(scaleX, 0, 0, scaleY, 0, 0);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression Rotate(FloatExpression theta) {
			FloatExpression cos = new FloatExpression(CosFunction.Instance.MakeNode(theta.Evaluation.Clone()));
			FloatExpression sin = new FloatExpression(SinFunction.Instance.MakeNode(theta.Evaluation.Clone()));
			return new TransformExpression(cos, sin, -sin, cos, 0, 0);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression Rotate(FloatExpression theta, FloatExpression x, FloatExpression y) {
			// For rotating about an arbitrary point
			// TODO Verify this is correct
			FloatExpression cos = new FloatExpression(CosFunction.Instance.MakeNode(theta.Evaluation.Clone()));
			FloatExpression sin = new FloatExpression(SinFunction.Instance.MakeNode(theta.Evaluation.Clone()));
			FloatExpression e = x - x * cos + y * sin;
			FloatExpression f = y - x * sin - y * cos;
			return new TransformExpression(cos, sin, -sin, cos, e, f);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression Skew(FloatExpression x, FloatExpression y) {
			FloatExpression tanX = new FloatExpression(TanFunction.Instance.MakeNode(x.Evaluation.Clone()));
			FloatExpression tanY = new FloatExpression(TanFunction.Instance.MakeNode(y.Evaluation.Clone()));
			return new TransformExpression(1, tanY, tanX, 1, 0, 0);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression SkewX(FloatExpression theta) {
			FloatExpression tan = new FloatExpression(TanFunction.Instance.MakeNode(theta.Evaluation.Clone()));
			return new TransformExpression(1, 0, tan, 1, 0, 0);
		}
		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public static TransformExpression SkewY(FloatExpression theta) {
			FloatExpression tan = new FloatExpression(TanFunction.Instance.MakeNode(theta.Evaluation.Clone()));
			return new TransformExpression(1, tan, 0, 1, 0, 0);
		}

		public Transform Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return Transform.Matrix(
					a!.Evaluate(environment),
					b!.Evaluate(environment),
					c!.Evaluate(environment),
					d!.Evaluate(environment),
					e!.Evaluate(environment),
					f!.Evaluate(environment)
					);
			}
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

	/*
	public class TextExpression : IExpression<string> {
		private readonly StringExpression[] parts;
		private readonly string value;
		private StringExpression[] Parts { get { return (value != null ? ((StringExpression)value).Yield() : parts).ToArray(); } }

		public bool IsConstant { get { return value != null; } }

		public TextExpression(StringExpression[] parts) {
			if (parts.All(p => p.IsConstant)) {
				this.parts = null;
				this.value = string.Join("", parts.Select(p => p.Evaluate(Environments.Empty)));
			}
			else {
				this.parts = parts;
				this.value = null;
			}
		}
		public TextExpression(string value) {
			this.parts = null;
			this.value = value;
		}

		public IEnumerable<string> GetVariables() {
			return IsConstant ? Enumerable.Empty<string>() : parts.SelectMany(p => p.GetVariables()).Distinct();
		}

		public string Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return string.Join("", parts.Select(p => p.Evaluate(environment)));
			}
		}

		public static TextExpression operator +(TextExpression a, TextExpression b) {
			return new TextExpression(a.Parts.Concat(b.Parts).ToArray());
		}

		public override string ToString() {
			return "[ " + (value != null ? ("\"" + value + "\"") : string.Join(", ", parts.Select(p => p.ToString()))) + " ]";
		}
	}
	*/

	public class NSliceValuesExpression : IExpression<NSliceValues> {

		public bool IsConstant { get { return value != null; } }

		private readonly FloatExpression[]? xs;
		private readonly FloatExpression[]? ys;
		private readonly NSliceValues? value;

		public NSliceValuesExpression(FloatExpression[] xs, FloatExpression[] ys) {
			this.xs = xs;
			this.ys = ys;
			this.value = null;
		}
		public NSliceValuesExpression(NSliceValues value) {
			this.xs = null;
			this.xs = null;
			this.value = value;
		}
		public static implicit operator NSliceValuesExpression(NSliceValues value) {
			return new NSliceValuesExpression(value);
		}

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

		public bool IsConstant { get { return value != null; } }

		public FloatExpression Top {
			get {
				if (value.HasValue) { return value.Value.Top; }
				else if (node is not null) { return new MarginAttributeNode(node, m => m.Top); }
				else { return top!; }
			}
		}
		public FloatExpression Right {
			get {
				if (value.HasValue) { return value.Value.Right; }
				else if (node is not null) { return new MarginAttributeNode(node, m => m.Right); }
				else { return right!; }
			}
		}
		public FloatExpression Bottom {
			get {
				if (value.HasValue) { return value.Value.Bottom; }
				else if (node is not null) { return new MarginAttributeNode(node, m => m.Bottom); }
				else { return bottom!; }
			}
		}
		public FloatExpression Left {
			get {
				if (value.HasValue) { return value.Value.Left; }
				else if (node is not null) { return new MarginAttributeNode(node, m => m.Left); }
				else { return left!; }
			}
		}

		private readonly FloatExpression? top;
		private readonly FloatExpression? right;
		private readonly FloatExpression? bottom;
		private readonly FloatExpression? left;
		private readonly EvaluationNode? node;
		private readonly Margins? value;

		public MarginsExpression(FloatExpression top, FloatExpression right, FloatExpression bottom, FloatExpression left) {
			this.top = top;
			this.right = right;
			this.bottom = bottom;
			this.left = left;
			this.node = null;
			this.value = null;
		}
		public MarginsExpression(EvaluationNode node) {
			this.top = null;
			this.right = null;
			this.bottom = null;
			this.left = null;
			this.node = node;
			this.value = null;

			if(node.ReturnType != MarkupEvaluationTypes.MARGINS) {
				throw new EvaluationTypeException($"{nameof(MarginsExpression)} expected node with return type {MarkupEvaluationTypes.MARGINS}.");
			}
		}
		public MarginsExpression(Margins value) {
			this.top = null;
			this.right = null;
			this.bottom = null;
			this.left = null;
			this.node = null;
			this.value = value;
		}
		public static implicit operator MarginsExpression(Margins value) {
			return new MarginsExpression(value);
		}

		public MarginsExpression(FloatExpression border) : this(border, border, border, border) { }
		public MarginsExpression(FloatExpression vertical, FloatExpression horizontal) : this(vertical, horizontal, vertical, horizontal) { }

		public IEnumerable<EvaluationName> GetVariables() {
			if (IsConstant) {
				return Enumerable.Empty<EvaluationName>();
			}
			else if(node is not null) {
				return node.GetVariables();
			}
			else {
				return top!.GetVariables().Concat(right!.GetVariables()).Concat(bottom!.GetVariables()).Concat(left!.GetVariables());
			}
		}

		public Margins Evaluate(IEnvironment environment) {
			if (value.HasValue) {
				return value.Value;
			}
			else if (node is not null) {
				object? result = node.Evaluate(environment);
				if(result is Margins margins) {
					return margins;
				}
				else {
					throw new EvaluationCalculationException($"Expected {nameof(Margins)} value.");
				}
			}
			else {
				return new Margins(top!.Evaluate(environment), right!.Evaluate(environment), bottom!.Evaluate(environment), left!.Evaluate(environment));
			}
		}

		public override string ToString() {
			if (value.HasValue) {
				return $"MarginsExpression({value.Value})";
			}
			else if (node is not null) {
				return $"MarginsExpression(node)";
			}
			else {
				return $"MarginsExpression(top: {Top}, right: {Right}, bottom: {Bottom}, left: {Left})";
			}
		}

		private class MarginAttributeNode : EvaluationNode {
			public override bool IsConstant => subject.IsConstant;

			public override EvaluationType ReturnType => EvaluationType.FLOAT;

			private readonly EvaluationNode subject;
			private readonly Func<Margins, float> action;

			public MarginAttributeNode(EvaluationNode subject, Func<Margins, float> action) : base() {
				if(subject.ReturnType != MarkupEvaluationTypes.MARGINS) {
					throw new EvaluationTypeException($"{nameof(subject)} must return a {nameof(Margins)} value.");
				}

				this.subject = subject;
				this.action = action;
			}

			public override object? Evaluate(IEnvironment environment) {
				object? result = subject.Evaluate(environment);

				if(result is Margins margins) {
					return action(margins);
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

		private readonly EvaluationNode? evaluation;
		private readonly PreserveAspectRatio? value;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public PreserveAspectRatioExpression(EvaluationNode evaluation) {
			if (evaluation.ReturnType != EvaluationType.STRING) {
				throw new EvaluationTypeException("Invalid evaluation type for PreserveAspectRatioExpression.");
			}
			else if (evaluation.IsConstant) {
				this.evaluation = null;
				this.value = Parse(evaluation, Environments.Empty);
			}
			else {
				this.evaluation = evaluation;
				this.value = null;
			}
		}
		public PreserveAspectRatioExpression(PreserveAspectRatio value) {
			this.evaluation = null;
			this.value = value;
		}
		public static implicit operator PreserveAspectRatioExpression(PreserveAspectRatio value) {
			return new PreserveAspectRatioExpression(value);
		}

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
			if(node.Evaluate(environment) is string value) {
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

	/*
	public class DimensionExpression : IExpression<Dimension> {

		public bool IsConstant { get { return value != null; } }

		private readonly FloatExpression number;
		private readonly StringExpression unit;
		private readonly Dimension? value;

		public DimensionExpression(FloatExpression number, StringExpression unit) {
			if (number.IsConstant && (unit == null || unit.IsConstant)) {
				this.number = null;
				this.unit = null;
				float numberVal = number.Evaluate(Environments.Empty);
				string unitVal = unit?.Evaluate(Environments.Empty);
				this.value = EvaluateParts(numberVal, unitVal);
			}
			else {
				this.number = number;
				this.unit = unit;
				this.value = null;
			}
		}
		public DimensionExpression(Dimension value) {
			this.number = null;
			this.unit = null;
			this.value = value;
		}
		public static implicit operator DimensionExpression(Dimension value) {
			return new DimensionExpression(value);
		}

		public IEnumerable<string> GetVariables() {
			return IsConstant ? Enumerable.Empty<string>() : number.GetVariables().Concat(unit.GetVariables());
		}

		public Dimension Evaluate(IEnvironment environment) {
			if (value.HasValue) {
				return value.Value;
			}
			else {
				float number = this.number.Evaluate(environment);
				string unit = this.unit?.Evaluate(environment);
				return EvaluateParts(number, unit);
			}
		}

		private Dimension EvaluateParts(float number, string unit) {
			if (unit == null || unit == "") {
				return Dimension.FromRelative(number);
			}
			else if (unit == "pc") {
				return Dimension.FromPercent(number);
			}
			else if (unit == "pt") {
				return Dimension.FromPoints(number);
			}
			else if (unit == "in") {
				return Dimension.FromInches(number);
			}
			else if (unit == "cm") {
				return Dimension.FromCentimetres(number);
			}
			else {
				throw new EvaluationCalculationException($"\"{unit}\" is not a valid unit for Dimension. Must be empty, or one of: pc, pt, in, or cm.");
			}
		}

		public override string ToString() {
			if (IsConstant) {
				return $"DimensionExpression({value})";
			}
			else {
				return $"DimensionExpression({number}, \"{unit}\")";
			}
		}
	}
	*/

	public class DimensionExpression : IExpression<Dimension> {

		public static EvaluationType DimensionType { get; } = EvaluationType.FromSystemType(typeof(Dimension));

		public bool IsConstant { get { return value != null; } }

		private readonly EvaluationNode? evaluation;
		private readonly Dimension? value;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public DimensionExpression(EvaluationNode evaluation) {
			if(evaluation.ReturnType != DimensionType) {
				throw new EvaluationTypeException("Invalid evaluation type for DimensionExpression.");
			}
			else if (evaluation.IsConstant) {
				this.evaluation = null;
				this.value = (Dimension)(evaluation.Evaluate(Environments.Empty) ?? throw new EvaluationCalculationException("Provided constant evaluation does not produce a value."));
			}
			else {
				this.evaluation = evaluation;
				this.value = null;
			}
		}
		public DimensionExpression(Dimension value) {
			this.evaluation = null;
			this.value = value;
		}
		public static implicit operator DimensionExpression(Dimension value) {
			return new DimensionExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : evaluation!.GetVariables();
		}

		public Dimension Evaluate(IEnvironment environment) {
			if (value.HasValue) {
				return value.Value;
			}
			else {
				return (Dimension)(evaluation!.Evaluate(environment) ?? throw new EvaluationCalculationException("Evaluation does not produce a value."));
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

		public EnumExpression<Anchor> Anchor { get { return value?.Anchor ?? anchor!; } }
		public DimensionExpression X { get { return value?.X ?? x!; } }
		public DimensionExpression Y { get { return value?.Y ?? y!; } }
		public DimensionExpression Width { get { return value?.Width ?? width!; } }
		public DimensionExpression Height { get { return value?.Height ?? height!; } }

		public readonly EnumExpression<Anchor>? anchor;
		// TODO Should these be FloatExpression values?
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
		}
		public PositionExpression(Position value) {
			this.anchor = null;
			this.x = null;
			this.y = null;
			this.width = null;
			this.height = null;
			this.value = value;
		}
		public static implicit operator PositionExpression(Position value) {
			return new PositionExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : anchor!.GetVariables().Concat(x!.GetVariables(), y!.GetVariables(), width!.GetVariables(), height!.GetVariables());
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

		public readonly EnumExpression<TextFormat>? startingFormat;
		public readonly StringExpression? text;
		private readonly RichString? value;

		public RichStringExpression(StringExpression text, EnumExpression<TextFormat>? startingFormat) {
			this.startingFormat = startingFormat;
			this.text = text;
			this.value = null;
		}
		public RichStringExpression(RichString value) {
			this.startingFormat = null;
			this.text = null;
			this.value = value;
		}
		public static implicit operator RichStringExpression(RichString value) {
			return new RichStringExpression(value);
		}

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

		private readonly IExpression<string> name;
		private readonly Dictionary<string, EvaluationNode> values;

		public ContextExpression(IExpression<string> name, Dictionary<string, EvaluationNode> values) {
			this.name = name;
			this.values = values;
		}

		public IContext Evaluate(IEnvironment environment) {
			try {
				string name = this.name.Evaluate(environment);
				Dictionary<string, object?> values = this.values.ToDictionary(kv => kv.Key, kv => kv.Value.Evaluate(environment), StringComparer.InvariantCultureIgnoreCase);

				Dictionary<string, string> properties = values.Where(kv => kv.Value is not bool).ToDictionary(kv => kv.Key, kv => ValueParsing.ToString(kv.Value));
				Dictionary<string, bool> flags = values.Where(kv => kv.Value is bool).ToDictionary(kv => kv.Key, kv => (bool)kv.Value!);

				return Context.Simple(name, properties, flags);
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

		public readonly FloatExpression? fixedLength;
		private readonly Length? value;

		public LengthExpression(FloatExpression fixedLength) {
			this.fixedLength = fixedLength;
			this.value = null;
		}
		public LengthExpression(Length value) {
			this.fixedLength = null;
			this.value = value;
		}
		[return: NotNullIfNotNull(nameof(value))]
		public static implicit operator LengthExpression?(Length? value) {
			if (value is null) { return null; }
			return new LengthExpression(value);
		}

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

		public bool IsConstant { get { return value != null; } }

		private readonly EvaluationNode[]? evaluation2;
		private readonly FilePath? value;

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		public FilePathExpression(EvaluationNode evaluation1, params EvaluationNode[] additional) {

			List<EvaluationNode> evaluations = evaluation1.Yield().Concat(additional).ToList();

			if (evaluations.Any(e => e.ReturnType != MarkupEvaluationTypes.FILE_PATH && e.ReturnType != EvaluationType.STRING)) {
				throw new EvaluationTypeException("Invalid evaluation types for FilePathExpression: " + string.Join(", ", evaluations.Select(e => e.ReturnType)));
			}
			else if (evaluations.All(e=>e.IsConstant)) {
				this.evaluation2 = null;
				this.value = EvaluatePath(evaluations, Environments.Empty);
			}
			else {
				this.evaluation2 = evaluations.ToArray();
				this.value = null;
			}
		}
		public FilePathExpression(FilePath value) {
			this.evaluation2 = null;
			this.value = value;
		}
		[return: NotNullIfNotNull(nameof(value))]
		public static implicit operator FilePathExpression?(FilePath? value) {
			if(value is null) { return null; }
			return new FilePathExpression(value);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return IsConstant ? Enumerable.Empty<EvaluationName>() : evaluation2!.SelectMany(e => e.GetVariables());
		}

		public FilePath Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else {
				return EvaluatePath(evaluation2!, environment);
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		private static FilePath EvaluatePath(IEnumerable<EvaluationNode> evaluations, IEnvironment environment) {
			object?[] absVals = evaluations.Select(e => e.Evaluate(environment)).ToArray();

			if (absVals.Length == 1) {
				if (absVals[0] is FilePath filePath) {
					return filePath;
				}
				else if (absVals[0] is string fileString) {
					return new FilePath(fileString);
				}
				else {
					throw new EvaluationTypeException("Invalid result type for FilePathExpression: " + (absVals[0]?.GetType().Name ?? "null"));
				}
			}
			else if (absVals.Length > 0) {
				string[] parts = absVals.Select(e => e switch {
					FilePath path => path.ToString(),
					string text => text,
					_ => throw new EvaluationCalculationException("Invalid component type for file path expression")
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
				return $"FilePathExpression({string.Join(", ", evaluation2!.Select(e => e.ToString()))})";
			}
		}

	}

}
