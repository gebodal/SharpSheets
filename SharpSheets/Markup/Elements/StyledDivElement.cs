using SharpSheets.Canvas;
using SharpSheets.Colors;
using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Markup.Canvas;
using SharpSheets.Parsing;
using SharpSheets.Canvas.Text;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using SharpSheets.Exceptions;
using SharpSheets.Evaluations.Nodes;

namespace SharpSheets.Markup.Elements {

	public static class StyledDivUtils {

		public static bool IsValidChildName(Type shapeType, string name) {
			if (string.Equals(name, "remaining") && typeof(IFramedArea).IsAssignableFrom(shapeType)) {
				return true;
			}
			else if (string.Equals(name, "label") && typeof(ILabelledArea).IsAssignableFrom(shapeType)) {
				return true;
			}
			else if (Regex.IsMatch(name, @"entry[0-9]+") && typeof(IEntriedArea).IsAssignableFrom(shapeType)) {
				return true;
			}

			return false;
		}

		public static Rectangle GetChildRect(IShape shape, string name, ISharpGraphicsState graphicsState, Rectangle fullRect) {
			if (string.Equals(name, "remaining") && shape is IFramedArea frame) {
				return frame.RemainingRect(graphicsState, fullRect);
			}
			else if (string.Equals(name, "label") && shape is ILabelledArea labelled) {
				return labelled.LabelRect(graphicsState, fullRect);
			}
			else if (name.StartsWith("entry") && shape is IEntriedArea entried) {
				try {
					int index = int.Parse(name[5..]) - 1; // -1 to account for zero-indexing for IEntriedArea shapes
					return entried.EntryRect(graphicsState, index, fullRect);
				}
				catch (FormatException) { }
			}

			throw new NotSupportedException($"Invalid child rect name for {shape.GetType().Name} shape: \"{name}\"");
		}

	}

	public abstract class KeyedChildrenDivElement : DivElement {

		protected readonly List<KeyValuePair<string, DivElement>> namedChildren;

		public KeyedChildrenDivElement(string? id, DivSetup setup, IVariableBox outerContext, IEnumerable<MarkupVariable> variables) :base(id, setup, outerContext, variables) {
			this.namedChildren = new List<KeyValuePair<string, DivElement>>();
		}

		public void AddNamedChild(string name, DivElement child) {
			namedChildren.Add(new KeyValuePair<string, DivElement>(name, child));
		}

		public override sealed void AddElement(IIdentifiableMarkupElement element) {
			// Does nothing, as this Div cannot have non-named children
			throw new InvalidOperationException($"{this.GetType().Name} can only accept named child elements.");
		}

	}

	public abstract class StyledDivElement<T> : KeyedChildrenDivElement where T : IShape {

		public readonly ContextExpression? shapeContext;
		public readonly IExpression<T?>? href;
		public readonly IExpression<string>? titleText; // Title to draw on any titled box styles

		/// <summary>
		/// Constructor for StyledDivElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="setup">The DivSetup values for this element.</param>
		/// <param name="_shapeContext">The shape context for this element.</param>
		/// <param name="_href" default="null">The shape to use as the style for this element.</param>
		/// <param name="_name" default="null">The name to use for this shape, if a name is accepted by the shape type.</param>
		/// <param name="outerContext">The variables inherited from this Divs parents (not including canvas variables).</param>
		/// <param name="variables">The variables declared with this Div.</param>
		public StyledDivElement(string? _id, DivSetup setup, ContextExpression? _shapeContext, IExpression<T?>? _href, IExpression<string>? _name, IVariableBox outerContext, IEnumerable<MarkupVariable> variables)
			: base(_id, setup, outerContext, variables) {

			this.shapeContext = _shapeContext;
			this.href = _href;
			this.titleText = _name;
		}

		protected virtual T GetShape(IEnvironment environment, ShapeFactory? shapeFactory, DirectoryPath source, out SharpParsingException[] buildErrors) {
			if(href is not null) {
				T evaluated = href.Evaluate(environment) ?? throw new EvaluationCalculationException("Could not evaluate shape.");
				buildErrors = Array.Empty<SharpParsingException>();
				return evaluated;
			}
			else {
				if(shapeFactory is null) {
					throw new ArgumentNullException(nameof(shapeFactory));
				}

				IContext context = this.shapeContext?.Evaluate(environment) ?? Context.Empty;
				string? name = this.titleText?.Evaluate(environment);
				return (T)shapeFactory.MakeShape(typeof(T), context, name, source, out buildErrors);
			}
			/*
			else {
				T evaluated = href.Evaluate(environment);
				if (evaluated != null) {
					return evaluated;
				}
				else {
					throw new ArgumentNullException("Reference shape evaluation produces null result for styled div element.");
				}
			}
			*/
		}

		protected override DrawableDivElement CreateDrawable(IEnvironment evaluationEnvironment, IEnvironment finalDivEnvironment, MarkupCanvasGraphicsData graphicsData, ShapeFactory? shapeFactory, DirectoryPath source, Dimension? size, Position? position, Margins margins, Layout layout, Arrangement arrangement, LayoutOrder order, float gutter, float aspectRatio, NSliceValuesExpression? slicingValues, bool provideRemaining, bool diagnostic) {
			T shape = GetShape(evaluationEnvironment, shapeFactory, source, out _); // TODO Should we pass these errors up the chain somehow?
			DrawableStyledDivElement<T> drawable = new DrawableStyledDivElement<T>(this, shape, finalDivEnvironment, size, position, margins, layout, arrangement, order, gutter, aspectRatio, slicingValues, diagnostic);

			foreach (KeyValuePair<string, DivElement> namedChild in namedChildren) {
				string name = namedChild.Key;
				DivElement child = namedChild.Value;

				DrawableDivElement? drawableChild = child.GetDrawable(graphicsData, finalDivEnvironment, shapeFactory, diagnostic);

				if (drawableChild != null) {
					drawable.AddNamedChild(name, drawableChild);
				}
			}

			return drawable;
		}
	}

	/// <summary>
	/// This element draws a box style in its assigned area. The area is assigned
	/// in the same way as a &lt;div&gt; element, and this element obeys all the same
	/// rules for positioning and graphics state.
	/// <para/>
	/// The remaining area of the box may be painted using a &lt;remaining&gt; child
	/// element.
	/// </summary>
	public class BoxStyledDivElement : StyledDivElement<IBox> {

		/// <summary>
		/// Constructor for BoxStyledDivElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="setup">The DivSetup values for this element.</param>
		/// <param name="_shapeContext">The shape context for this element.</param>
		/// <param name="_href" default="null">The shape to use as the style for this element.</param>
		/// <param name="_name" default="null">The name to use for this shape, if a name is accepted by the shape type.</param>
		/// <param name="outerContext">The variables inherited from this Divs parents (not including canvas variables).</param>
		/// <param name="variables">The variables declared with this Div.</param>
		public BoxStyledDivElement(string? _id, DivSetup setup, ContextExpression? _shapeContext, IExpression<IBox?>? _href, IExpression<string>? _name, IVariableBox outerContext, IEnumerable<MarkupVariable> variables)
			: base(_id, setup, _shapeContext, _href, _name, outerContext, variables) { }

	}

	/// <summary>
	/// This element draws a labelled box style in its assigned area. The area is assigned
	/// in the same way as a &lt;div&gt; element, and this element obeys all the same
	/// rules for positioning and graphics state.
	/// <para/>
	/// The remaining area of the box may be painted using a &lt;remaining&gt; child
	/// element, and the label using a &lt;label&gt; child element.
	/// </summary>
	public class LabelledBoxStyledDivElement : StyledDivElement<ILabelledBox> {

		/// <summary>
		/// Constructor for LabelledBoxStyledDivElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="setup">The DivSetup values for this element.</param>
		/// <param name="_shapeContext">The shape context for this element.</param>
		/// <param name="_href" default="null">The shape to use as the style for this element.</param>
		/// <param name="_name" default="null">The name to use for this shape, if a name is accepted by the shape type.</param>
		/// <param name="outerContext">The variables inherited from this Divs parents (not including canvas variables).</param>
		/// <param name="variables">The variables declared with this Div.</param>
		public LabelledBoxStyledDivElement(string? _id, DivSetup setup, ContextExpression? _shapeContext, IExpression<ILabelledBox?>? _href, IExpression<string>? _name, IVariableBox outerContext, IEnumerable<MarkupVariable> variables)
			: base(_id, setup, _shapeContext, _href, _name, outerContext, variables) { }

	}

	/// <summary>
	/// This element draws a titled box style in its assigned area. The area is assigned
	/// in the same way as a &lt;div&gt; element, and this element obeys all the same
	/// rules for positioning and graphics state.
	/// <para/>
	/// The remaining area of the box may be painted using a &lt;remaining&gt; child
	/// element.
	/// </summary>
	public class TitledBoxStyledDivElement : StyledDivElement<ITitledBox> {

		/// <summary>
		/// Constructor for BoxStyledDivElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="setup">The DivSetup values for this element.</param>
		/// <param name="_shapeContext">The shape context for this element.</param>
		/// <param name="_href" default="null">The shape to use as the style for this element.</param>
		/// <param name="_name" default="null">The name to use for this shape, if a name is accepted by the shape type.</param>
		/// <param name="outerContext">The variables inherited from this Divs parents (not including canvas variables).</param>
		/// <param name="variables">The variables declared with this Div.</param>
		public TitledBoxStyledDivElement(string? _id, DivSetup setup, ContextExpression? _shapeContext, IExpression<ITitledBox?>? _href, IExpression<string>? _name, IVariableBox outerContext, IEnumerable<MarkupVariable> variables)
			: base(_id, setup, _shapeContext, _href, _name, outerContext, variables) { }

	}

	/// <summary>
	/// This element draws a bar style in its assigned area. The area is assigned
	/// in the same way as a &lt;div&gt; element, and this element obeys all the same
	/// rules for positioning and graphics state.
	/// <para/>
	/// The remaining area of the bar may be painted using a &lt;remaining&gt; child
	/// element, and the label using a &lt;label&gt; child element.
	/// </summary>
	public class BarStyledDivElement : StyledDivElement<IBar> {

		/// <summary>
		/// Constructor for BarStyledDivElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="setup">The DivSetup values for this element.</param>
		/// <param name="_shapeContext">The shape context for this element.</param>
		/// <param name="_href" default="null">The shape to use as the style for this element.</param>
		/// <param name="_name" default="null">The name to use for this shape, if a name is accepted by the shape type.</param>
		/// <param name="outerContext">The variables inherited from this Divs parents (not including canvas variables).</param>
		/// <param name="variables">The variables declared with this Div.</param>
		public BarStyledDivElement(string? _id, DivSetup setup, ContextExpression? _shapeContext, IExpression<IBar?>? _href, IExpression<string>? _name, IVariableBox outerContext, IEnumerable<MarkupVariable> variables)
			: base(_id, setup, _shapeContext, _href, _name, outerContext, variables) { }

	}

	/// <summary>
	/// This element draws a usage bar style in its assigned area. The area is assigned
	/// in the same way as a &lt;div&gt; element, and this element obeys all the same
	/// rules for positioning and graphics state.
	/// <para/>
	/// The label area of the bar may be painted using a &lt;label&gt; child
	/// element, and the entry areas using &lt;entry1&gt; and &lt;entry2&gt;
	/// child elements.
	/// </summary>
	public class LabelledUsageBarStyledDivElement : StyledDivElement<IUsageBar> {

		private readonly StringExpression? label1; // TODO TextExpression? IExpression<string>?
		private readonly StringExpression? label2;
		private readonly LabelDetailsExpression? labelDetails;
		private readonly TextExpression? note; // TODO StringExpression?
		private readonly LabelDetailsExpression? noteDetails;

		/// <summary>
		/// Constructor for LabelledUsageBarStyledDivElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="setup">The DivSetup values for this element.</param>
		/// <param name="_shapeContext">The shape context for this element.</param>
		/// <param name="_href" default="null">The shape to use as the style for this element.</param>
		/// <param name="_name" default="null">The name to use for this shape, if a name is accepted by the shape type.</param>
		/// <param name="_label1" default="null">A label to be drawn by the first entry area.</param>
		/// <param name="_label2" default="null">A label to be drawn by the second entry area.</param>
		/// <param name="labels_">Label layout details for this usage bar.</param>
		/// <param name="_note" default="null">A note to be drawn by the label area.</param>
		/// <param name="note_">Note layout details for this usage bar.</param>
		/// <param name="outerContext">The variables inherited from this Divs parents (not including canvas variables).</param>
		/// <param name="variables">The variables declared with this Div.</param>
		public LabelledUsageBarStyledDivElement(string? _id, DivSetup setup,
			ContextExpression? _shapeContext, IExpression<IUsageBar?>? _href,
			IExpression<string>? _name,
			StringExpression? _label1,
			StringExpression? _label2,
			LabelDetailsExpression? labels_,
			TextExpression? _note,
			LabelDetailsExpression? note_,
			IVariableBox outerContext, IEnumerable<MarkupVariable> variables)
		: base(_id, setup, _shapeContext, _href, _name, outerContext, variables) {

			this.label1 = _label1;
			this.label2 = _label2;
			this.labelDetails = labels_;
			this.note = _note;
			this.noteDetails = note_;
		}

		protected override IUsageBar GetShape(IEnvironment environment, ShapeFactory? shapeFactory, DirectoryPath source, out SharpParsingException[] buildErrors) {
			IUsageBar usageBar = base.GetShape(environment, shapeFactory, source, out buildErrors);

			string? label1 = this.label1?.Evaluate(environment);
			string? label2 = this.label2?.Evaluate(environment);
			LabelDetails? labelDetails = this.labelDetails?.Evaluate(environment);
			string? note = this.note?.Evaluate(environment);
			LabelDetails? noteDetails = this.noteDetails?.Evaluate(environment);

			return new LabelledUsageBar(usageBar, label1, label2, labelDetails, note, noteDetails);
		}
	}

	/// <summary>
	/// This element draws a detail style in its assigned area. The area is assigned
	/// in the same way as a &lt;div&gt; element, and this element obeys all the same
	/// rules for positioning and graphics state.
	/// <para/>
	/// This styled element has no named areas where content can be drawn.
	/// </summary>
	public class DetailStyledDivElement : StyledDivElement<IDetail> {

		/// <summary>
		/// Constructor for BoxStyledDivElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="setup">The DivSetup values for this element.</param>
		/// <param name="_shapeContext">The shape context for this element.</param>
		/// <param name="_href" default="null">The shape to use as the style for this element.</param>
		/// <param name="_name" default="null">The name to use for this shape, if a name is accepted by the shape type.</param>
		/// <param name="outerContext">The variables inherited from this Divs parents (not including canvas variables).</param>
		/// <param name="variables">The variables declared with this Div.</param>
		public DetailStyledDivElement(string? _id, DivSetup setup, ContextExpression? _shapeContext, IExpression<IDetail?>? _href, IExpression<string>? _name, IVariableBox outerContext, IEnumerable<MarkupVariable> variables)
			: base(_id, setup, _shapeContext, _href, _name, outerContext, variables) { }

	}

	public class DrawableStyledDivElement<T> : DrawableDivElement where T : IShape {

		readonly T shape;

		private readonly List<string> childrenProvidedOrder;
		private readonly Dictionary<string, DrawableDivElement> namedChildren;

		//public override IList<IGridElement> Children => childrenProvidedOrder.Select(n => namedChildren[n]).ToList<IGridElement>();
		public override bool ProvidesRemaining { get { return divProvideRemaining || namedChildren.Values.Any(c => c.ProvidesRemaining); } }

		public DrawableStyledDivElement(StyledDivElement<T> pattern, T shape, IEnvironment environment, Dimension? size, Position? position, Margins margins, Layout layout, Arrangement arrangement, LayoutOrder order, float gutter, float aspectRatio, NSliceValuesExpression? slicingValues, bool drawConstructionLines)
			: base(pattern, environment, size, position, margins, layout, arrangement, order, gutter, aspectRatio, slicingValues, false, drawConstructionLines) {

			this.shape = shape;

			this.childrenProvidedOrder = new List<string>();
			this.namedChildren = new Dictionary<string, DrawableDivElement>();
		}

		public void AddNamedChild(string name, DrawableDivElement child) {
			// Only use first instance of a given child provided
			if (child != null && !childrenProvidedOrder.Contains(name)) {
				childrenProvidedOrder.Add(name);
				namedChildren.Add(name, child);
			}
		}

		public override void AddElement(IIdentifiableMarkupElement element) {
			throw new InvalidOperationException($"{this.GetType().Name} can only have named child elements.");
		}

		public override Size MinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {

			if(shape is IFramedContainerArea framedContainer && namedChildren.TryGetValue("remaining", out DrawableDivElement? remainingDiv)) {
				Rectangle remainingRect = framedContainer.RemainingRect(graphicsState, (Rectangle)availableSpace);
				Size minimumContent = remainingDiv.MinimumContentSize(graphicsState, (Size)remainingRect) ?? new Size(0f, 0f);
				return framedContainer.FullSize(graphicsState, minimumContent);
			}
			else {
				Size[] mins = namedChildren.Values.Select(c => c.MinimumContentSize(graphicsState, availableSpace)).WhereNotNull().ToArray();
				return mins.Length > 0 ? new Size(mins.Max(r => r.Width), mins.Max(r => r.Height)) : new Size(0f, 0f);
			}

			//throw new NotImplementedException();
			//return base.MinimumContentRect(canvas, availableSpace);
		}

		public override bool AreaExists(string name) {
			return namedChildren.Values.Any(c => c.AreaExists(name));
		}

		public override IEnumerable<string> GetAreas() {
			return namedChildren.Values.SelectMany(c => c.GetAreas()).Distinct();
		}

		public override Rectangle? GetNamedArea(string name, ISharpGraphicsState graphicsState, Rectangle fullRect) {
			Rectangle rect = ApplyAspect(fullRect);
			foreach (KeyValuePair<string, DrawableDivElement> child in namedChildren) {
				if (child.Value.AreaExists(name)) {
					Rectangle childRect = StyledDivUtils.GetChildRect(shape, child.Key, graphicsState, rect);
					return child.Value.GetNamedArea(name, graphicsState, childRect);
				}
			}

			return null;
		}

		public override Rectangle? GetFullFromNamedArea(string name, ISharpGraphicsState graphicsState, Rectangle rect) {
			if (string.Equals(name, "remaining") && shape is IFramedContainerArea framedContainer) {
				foreach (DrawableDivElement child in namedChildren.Values) {
					if (child.AreaExists(name) && child.GetFullFromNamedArea(name, graphicsState, rect) is Rectangle childArea) {
						return framedContainer.FullRect(graphicsState, childArea);
					}
				}
			}
			//throw new ArgumentException($"Cannot infer full size for \"{name}\" area for {shape.GetType().Name}."); // LayoutException?
			return null;
		}

		public override void Draw(ISharpCanvas canvas, Rectangle fullRect, CancellationToken cancellationToken) {

			Rectangle rect = ApplyAspect(fullRect);

			if (diagnostic) {
				//canvas.RegisterArea(pattern, fullRect);
				//canvas.RegisterArea(pattern, rect);
				canvas.RegisterAreas(pattern, fullRect, rect, Array.Empty<Rectangle>());
			}

			shape.Draw(canvas, rect);

			foreach (string name in childrenProvidedOrder) {
				if (namedChildren.TryGetValue(name, out DrawableDivElement? child)) {
					Rectangle childRect = StyledDivUtils.GetChildRect(shape, name, canvas, rect);
					child.Draw(canvas, childRect, cancellationToken);
				}
			}

		}

		public override Rectangle? ContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			if(ProvidesRemaining && shape is IFramedArea framedArea) {
				return framedArea.RemainingRect(graphicsState, ApplyAspect(rect));
			}
			else {
				return null;
			}
		}

	}

	public class ShapeReferenceExpression<T> : IExpression<T?> where T : IShape {
		public virtual bool IsConstant { get { return value != null; } }

		protected readonly EvaluationNode? evaluation;
		//protected readonly EvaluationName? argumentName;
		private readonly T? value;

		public ShapeReferenceExpression(EvaluationNode evaluation) {
			this.evaluation = evaluation;
			this.value = default;
		}
		public ShapeReferenceExpression(T shape) {
			this.evaluation = null;
			this.value = shape;
		}

		public virtual IEnumerable<EvaluationName> GetVariables() {
			return evaluation is null ? Enumerable.Empty<EvaluationName>() : evaluation.GetVariables();
		}

		public virtual T? Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			/*
			else if (environment.IsVariable(argumentName!.Value) && environment.GetVariable(argumentName!.Value) is T envVar) {
				return envVar;
			}
			else {
				return default;
			}
			*/
			else if (evaluation is not null && evaluation.Evaluate(environment) is T result) {
				return result;
			}
			else {
				return default;
			}
		}

		public override string ToString() {
			return $"ShapeReferenceExpression<{typeof(T).Name}>";
		}
	}

	public class LabelDetailsExpression : IExpression<LabelDetails> {

		public bool IsConstant { get; } = false;

		public readonly FloatExpression? fontSize;
		public readonly (FloatExpression? x, FloatExpression? y) offset;
		public readonly EnumExpression<TextFormat>? format;
		public readonly EnumExpression<Justification>? justification;
		public readonly EnumExpression<SharpSheets.Canvas.Text.Alignment>? alignment;
		public readonly ColorExpression? color;

		/// <summary>
		/// Constructor for LabelDetailsExpression.
		/// </summary>
		/// <param name="fontsize">The fontsize for the label text.</param>
		/// <param name="x_offset">The x-offset for the label from its initial position.</param>
		/// <param name="y_offset">The y-offset for the label from its initial position.</param>
		/// <param name="font_style">The font style for the label text.</param>
		/// <param name="justification">The horizontal justification for the label text.</param>
		/// <param name="alignment">The vertical alignment for the label text.</param>
		/// <param name="color">The color for the label text.</param>
		public LabelDetailsExpression(FloatExpression? fontsize, FloatExpression? x_offset, FloatExpression? y_offset, EnumExpression<TextFormat>? font_style, EnumExpression<Justification>? justification, EnumExpression<SharpSheets.Canvas.Text.Alignment>? alignment, ColorExpression? color) {
			this.fontSize = fontsize;
			this.offset = (x_offset, y_offset);
			this.format = font_style;
			this.justification = justification;
			this.alignment = alignment;
			this.color = color;
		}

		public LabelDetails Evaluate(IEnvironment environment) {
			float fontSize = this.fontSize?.Evaluate(environment) ?? LabelDetails.FontSizeDefault;
			(float,float) offset = (this.offset.x?.Evaluate(environment) ?? LabelDetails.OffsetDefault.x, this.offset.y?.Evaluate(environment) ?? LabelDetails.OffsetDefault.y);
			TextFormat format = this.format?.Evaluate(environment) ?? LabelDetails.FormatDefault;
			Justification justification = this.justification?.Evaluate(environment) ?? LabelDetails.JustificationDefault;
			SharpSheets.Canvas.Text.Alignment alignment = this.alignment?.Evaluate(environment) ?? LabelDetails.AlignmentDefault;
			Color? color = this.color?.Evaluate(environment) ?? LabelDetails.ColorDefault;

			return new LabelDetails(fontSize, offset, format, justification, alignment, color);
		}

		public IEnumerable<EvaluationName> GetVariables() {
			return EnumerableUtils.ConcatOrNothing(
					fontSize?.GetVariables(),
					offset.x?.GetVariables(),
					offset.y?.GetVariables(),
					format?.GetVariables(),
					justification?.GetVariables(),
					alignment?.GetVariables(),
					color?.GetVariables())
				.Distinct();
		}
	}

}
