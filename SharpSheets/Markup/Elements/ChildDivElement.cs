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
using SharpSheets.Widgets;

namespace SharpSheets.Markup.Elements {

	/// <summary>
	/// 
	/// </summary>
	public class ChildDivElement : DivElement {

		public readonly IExpression<IWidget?>? href;

		/// <summary>
		/// Constructor for ChildDivElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="setup">The DivSetup values for this element.</param>
		/// <param name="_href" default="null">The widget to use as the content for this element.</param>
		/// <param name="outerContext">The variables inherited from this Divs parents (not including canvas variables).</param>
		/// <param name="variables">The variables declared with this Div.</param>
		public ChildDivElement(string? _id, DivSetup setup, IExpression<IWidget?>? _href, IVariableBox outerContext, IEnumerable<MarkupVariable> variables)
			: base(_id, setup, outerContext, variables) {

			this.href = _href;
		}

		public override sealed void AddElement(IIdentifiableMarkupElement element) {
			// Does nothing, as this Div cannot have children
			throw new InvalidOperationException($"{nameof(ChildDivElement)} cannot accept any child elements.");
		}

		protected IWidget? GetContent(IEnvironment environment) {
			return href?.Evaluate(environment);
		}

		protected override DrawableDivElement CreateDrawable(IEnvironment evaluationEnvironment, IEnvironment finalDivEnvironment, MarkupCanvasGraphicsData graphicsData, ShapeFactory? shapeFactory, DirectoryPath source, Dimension? size, Position? position, Margins margins, Layout layout, Arrangement arrangement, LayoutOrder order, float gutter, float aspectRatio, NSliceValuesExpression? slicingValues, bool provideRemaining, bool diagnostic) {
			IWidget? content = GetContent(evaluationEnvironment);
			DrawableChildDivElement drawable = new DrawableChildDivElement(this, content, finalDivEnvironment, size, position, margins, layout, arrangement, order, gutter, aspectRatio, slicingValues, provideRemaining, diagnostic);
			return drawable;
		}
	}

	public class DrawableChildDivElement : DrawableDivElement {

		readonly IWidget? content;

		public DrawableChildDivElement(ChildDivElement pattern, IWidget? content, IEnvironment environment, Dimension? size, Position? position, Margins margins, Layout layout, Arrangement arrangement, LayoutOrder order, float gutter, float aspectRatio, NSliceValuesExpression? slicingValues, bool provideRemaining, bool drawConstructionLines)
			: base(pattern, environment, size, position, margins, layout, arrangement, order, gutter, aspectRatio, slicingValues, provideRemaining, drawConstructionLines) {

			this.content = content;
		}

		public override void AddElement(IIdentifiableMarkupElement element) {
			throw new InvalidOperationException($"{nameof(DrawableChildDivElement)} cannot accept any child elements.");
		}

		public override Size? MinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			return content?.MinimumContentSize(graphicsState, availableSpace);
		}

		public override void Draw(ISharpCanvas canvas, Rectangle fullRect, CancellationToken cancellationToken) {
			Rectangle rect = ApplyAspect(fullRect);
			_ = GridElements.GetElementRects(this, Children, canvas, rect, out Rectangle drawingRect, out _, out _, out _, out _);

			if (diagnostic) {
				//canvas.RegisterArea(pattern, fullRect);
				//canvas.RegisterArea(pattern, rect);
				canvas.RegisterAreas(pattern, fullRect, drawingRect, Array.Empty<Rectangle>());
			}

			content?.Draw(canvas, drawingRect, cancellationToken);
		}

		public override Rectangle? ContainerArea(ISharpGraphicsState graphicsState, Rectangle fullArea) {
			if(ProvidesRemaining) {
				return content is not null ? content.ContainerArea(graphicsState, fullArea) : fullArea;
			}
			else {
				return null;
			}
		}

	}

	public class WidgetReferenceExpression : IExpression<IWidget?> {
		public virtual bool IsConstant { get { return value != null; } }

		protected readonly EvaluationNode? evaluation;
		private readonly IWidget? value;

		public WidgetReferenceExpression(EvaluationNode evaluation) {
			this.evaluation = evaluation;
			this.value = default;
		}
		public WidgetReferenceExpression(IWidget widget) {
			this.evaluation = null;
			this.value = widget;
		}

		public virtual IEnumerable<EvaluationName> GetVariables() {
			return evaluation is null ? Enumerable.Empty<EvaluationName>() : evaluation.GetVariables();
		}

		public virtual IWidget? Evaluate(IEnvironment environment) {
			if (value != null) {
				return value;
			}
			else if (evaluation is not null && evaluation.Evaluate(environment) is IWidget result) {
				return result;
			}
			else {
				return default;
			}
		}

		public override string ToString() {
			return $"WidgetReferenceExpression";
		}
	}

}
