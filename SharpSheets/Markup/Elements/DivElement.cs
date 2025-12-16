using SharpSheets.Canvas;
using SharpSheets.Colors;
using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Exceptions;
using SharpSheets.Layouts;
using SharpSheets.Markup.Canvas;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace SharpSheets.Markup.Elements {

	public readonly struct DivSetup {
		public readonly FloatExpression? gutter;
		public readonly DimensionExpression? size;
		public readonly FloatExpression? minWidth;
		public readonly FloatExpression? minHeight;
		public readonly PositionExpression? position;
		public readonly MarginsExpression? margins;
		public readonly EnumExpression<LayoutDirection>? layout;
		public readonly EnumExpression<Arrangement>? arrangement;
		public readonly EnumExpression<LayoutOrder>? order;

		public readonly BoolExpression? provideRemaining; // TODO Is this still used?

		public readonly Size? canvasArea;
		public readonly FloatExpression? aspectRatio;
		public readonly BoolExpression? enabled;

		public readonly IntExpression? repeat;
		public readonly ForEachExpression? forEach;

		public readonly FilePath source;

		/// <summary>
		/// Constructor for DivSetup.
		/// </summary>
		/// <param name="source">The source file for this Div element.</param>
		/// <param name="gutter" default="0">The spacing between the child div elements,
		/// measured in points.</param>
		/// <param name="_size" default="1">Size of the div, either as a absolute dimension (e.g. pt, cm, in),
		/// a relative size (in percent or arbitrary units), or auto-sized (with "auto"). Note that if
		/// specific positioning is provided, <paramref name="_size"/> will be ignored.</param>
		/// /// <param name="_min_width">The minimum width for this element, for use when determining
		/// minimum size.</param>
		/// <param name="_min_height">The minimum height for this element, for use when determining
		/// minimum size.</param>
		/// <param name="_position">The position for the <see cref="DivElement"/>. Note that if
		/// <paramref name="_position"/> is provided, then <paramref name="_size"/> will be
		/// ignored.</param>
		/// <param name="_margins" default="0,0,0,0">The margins for the div area, which will
		/// be applied after the element has been positioned using the grid layout. These margins
		/// will be factored into the minimum size of the element if autosizing is used.</param>
		/// <param name="layout" default="rows">Specifies the arrangement of child elements within
		/// the div area, either as rows or columns.</param>
		/// <param name="arrangement" default="FRONT">Specifies the arrangement of the elements
		/// children in the available space, indicating whether the children should be arranged
		/// centrally, or to one end of, the available space.</param>
		/// <param name="order" default="FORWARD">Specifies the order that the elements children
		/// should be drawn in across the available space, allowing children to be drawn in reverse
		/// document order.</param>
		/// <param name="_provide_remaining" default="false">Indicates that this element provides some
		/// remaining area for use with the grid layout system.</param>
		/// <param name="_canvas">[EXPERIMENTAL] Specifies a size for a canvas that will represent the element area.</param>
		/// <param name="_aspect_ratio" default="-1">Specifies an aspect ratio for the element area.
		/// This aspect ratio will be applied after the element area has been determined using the
		/// grid layout system.</param>
		/// <param name="_enabled" default="true">A flag to indicate whether this element and its children
		/// should be rendered in the pattern.</param>
		/// <param name="_repeat">Indicates the number of times this element should be repeated in
		/// the pattern. Note that such repetitions will each individually repeat any <paramref name="_for_each"/>
		/// attributes which may be specified.</param>
		/// <param name="_for_each">Specifies that the element should be repeated a number of times
		/// based on some collection of elements, with one repetition for each element in that collection.
		/// Note that if a value is specified for <paramref name="_repeat"/>, then this for-each statement
		/// will be repeated as a whole <paramref name="_repeat"/> times.</param>
		public DivSetup(
			FilePath source,
			FloatExpression? gutter = null,
			DimensionExpression? _size = null,
			FloatExpression? _min_width = null,
			FloatExpression? _min_height = null,
			PositionExpression? _position = null,
			MarginsExpression? _margins = null,
			EnumExpression<LayoutDirection>? layout = null,
			EnumExpression<Arrangement>? arrangement = null,
			EnumExpression<LayoutOrder>? order = null,
			BoolExpression? _provide_remaining = null,
			Size? _canvas = null,
			FloatExpression? _aspect_ratio = null,
			BoolExpression? _enabled = null,
			IntExpression? _repeat = null,
			ForEachExpression? _for_each = null
		) {
			this.gutter = gutter;
			this.size = _size;
			this.minWidth = _min_width;
			this.minHeight = _min_height;
			this.position = _position;
			this.margins = _margins;
			this.layout = layout;
			this.arrangement = arrangement;
			this.order = order;
			this.provideRemaining = _provide_remaining;
			this.canvasArea = _canvas;
			this.aspectRatio = _aspect_ratio;
			this.enabled = _enabled;
			this.repeat = _repeat;
			this.forEach = _for_each;
			this.source = source;
		}

		/// <param name="source">The source file for this Div element.</param>
		/// <param name="gutter">The spacing between the child div elements,
		/// measured in points.</param>
		/// <param name="size">Size of the div, either as a absolute dimension (e.g. pt, cm, in),
		/// a relative size (in percent or arbitrary units), or auto-sized (with "auto"). Note that if
		/// specific positioning is provided, <paramref name="size"/> will be ignored.</param>
		/// <param name="min_width">The minimum width for this element, for use when determining
		/// minimum size.</param>
		/// <param name="min_height">The minimum height for this element, for use when determining
		/// minimum size.</param>
		/// <param name="position">The position for the <see cref="DivElement"/>. Note that if
		/// <paramref name="position"/> is provided, then <paramref name="size"/> will be
		/// ignored.</param>
		/// <param name="margins">The margins for the div area, which will
		/// be applied after the element has been positioned using the grid layout. These margins
		/// will be factored into the minimum size of the element if autosizing is used.</param>
		/// <param name="layout">Specifies the arrangement of child elements within
		/// the div area, either as rows or columns.</param>
		/// <param name="arrangement">Specifies the arrangement of the elements
		/// children in the available space, indicating whether the children should be arranged
		/// centrally, or to one end of, the available space.</param>
		/// <param name="order">Specifies the order that the elements children
		/// should be drawn in across the available space, allowing children to be drawn in reverse
		/// document order.</param>
		/// <param name="provide_remaining">Indicates that this element provides some
		/// remaining area for use with the grid layout system.</param>
		/// <param name="canvas">[EXPERIMENTAL] Specifies a size for a canvas that will represent the element area.</param>
		/// <param name="aspect_ratio">Specifies an aspect ratio for the element area.
		/// This aspect ratio will be applied after the element area has been determined using the
		/// grid layout system.</param>
		/// <param name="enabled">A flag to indicate whether this element and its children
		/// should be rendered in the pattern.</param>
		/// <param name="repeat">Indicates the number of times this element should be repeated in
		/// the pattern. Note that such repetitions will each individually repeat any <paramref name="for_each"/>
		/// attributes which may be specified.</param>
		/// <param name="for_each">Specifies that the element should be repeated a number of times
		/// based on some collection of elements, with one repetition for each element in that collection.
		/// Note that if a value is specified for <paramref name="repeat"/>, then this for-each statement
		/// will be repeated as a whole <paramref name="repeat"/> times.</param>
		[FactoryBuilder(typeof(DivSetup))]
		[ExpandedArgumentBuilder(Defer = true)]
		public static DivSetup Build(
				[Property(Exclude = true)] FilePath source,
				[Property(Default = "0")] FloatExpression? gutter = null,
				[LocalProperty(Default = "1")] DimensionExpression? size = null,
				[LocalProperty] FloatExpression? min_width = null,
				[LocalProperty] FloatExpression? min_height = null,
				[LocalProperty] PositionExpression? position = null,
				[LocalProperty(Default = "0,0,0,0")] MarginsExpression? margins = null,
				[Property(Default = "rows")] EnumExpression<LayoutDirection>? layout = null,
				[Property(Default = "FRONT")] EnumExpression<Arrangement>? arrangement = null,
				[Property(Default = "FORWARD")] EnumExpression<LayoutOrder>? order = null,
				[LocalProperty(Default = "false")] BoolExpression? provide_remaining = null,
				[LocalProperty] Size? canvas = null,
				[LocalProperty(Default = "-1")] FloatExpression? aspect_ratio = null,
				[LocalProperty(Default = "true")] BoolExpression? enabled = null,
				[LocalProperty] IntExpression? repeat = null,
				[LocalProperty] ForEachExpression? for_each = null
			) {

			return new DivSetup(source, gutter, size, min_width, min_height, position, margins, layout, arrangement, order, provide_remaining, canvas, aspect_ratio, enabled, repeat, for_each);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public IEnumerable<IEnvironment> ForEachEnvironments(IEnvironment outerEnvironment, bool includeOriginal) {
			if (forEach != null) {
				return forEach.EvaluateEnvironments(outerEnvironment, includeOriginal);
			}
			else {
				return Environments.Empty(outerEnvironment.Context).Yield();
			}
		}

		public IVariableBox ForEachVariables(EvaluationContext context) {
			if (forEach != null) {
				return VariableBoxes.Single(forEach.Variable, context);
			}
			else {
				return VariableBoxes.Empty(context);
			}
		}
	}

	/// <summary>
	/// This element represents a division of the pattern space, according to the
	/// grid layout system. Each division may be positioned within the available
	/// space exactly, or allowed to follow the grid layout system. Each division
	/// may also be repeated, either a stated number of times, or based on some
	/// collection of data. Divisions may contain other graphical elements, which
	/// will be drawn in the area assigned to the division.
	/// </summary>
	public class DivElement : IIdentifiableMarkupElement {

		public readonly MarkupEvaluationContext MarkupContext;

		/// <summary>
		/// These are the variables inherited from the Div parents (this does not include canvas variables)
		/// </summary>
		private readonly IVariableBox outerContext;

		/// <summary>
		/// These are the declared variables belonging to this Div (not including any belonging to parents or children, or those derived from for-each attributes).
		/// </summary>
		private readonly MarkupVariable[] markupVariables;

		/// <summary>
		/// These are all variables available to this Div: variables declared for this div, variables from for-each statements, and variables from its parent (this does not include canvas variables).
		/// </summary>
		public IVariableBox Variables { get; }

		public string? ID { get; }

		public readonly DivSetup setup;

		private readonly List<SlicingValuesElement> slicingValueElements;
		public IEnumerable<SlicingValuesElement> SlicingValuesElements => slicingValueElements;

		private readonly List<IIdentifiableMarkupElement> elements;

		/// <summary>
		/// Constructor for DivElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="setup"> The DivSetup values for this element. </param>
		/// <param name="outerContext"> The variables inherited from this Divs parents (not including canvas variables). </param>
		/// <param name="markupContext"></param>
		/// <param name="variables"> The variables declared with this Div. </param>
		public DivElement(string? _id, DivSetup setup, IVariableBox outerContext, MarkupEvaluationContext markupContext, IEnumerable<MarkupVariable> variables) {
			this.ID = _id;
			this.setup = setup;
			this.slicingValueElements = new List<SlicingValuesElement>();
			this.elements = new List<IIdentifiableMarkupElement>();
			this.outerContext = outerContext;
			this.markupVariables = variables.ToArray(); // variables.ToDictionary(v => v.Name, StringComparer.InvariantCultureIgnoreCase);
			this.Variables = this.outerContext.AppendVariables(MarkupVariable.MakeVariableBox(this.markupVariables, this.outerContext.Context)).AppendVariables(setup.ForEachVariables(outerContext.Context));

			this.MarkupContext = markupContext;
		}

		/// <param name="id" default="null">A unique name for this element.</param>
		/// <param name="setup"> The DivSetup values for this element. </param>
		/// <param name="outerContext"> The variables inherited from this Divs parents (not including canvas variables). </param>
		/// <param name="markupContext"></param>
		/// <param name="variables"> The variables declared with this Div. </param>
		[FactoryBuilder(typeof(DivElement), Name = "div")]
		public static DivElement Build(
				[LocalProperty(Default = "null")] string? id, DivSetup setup,
				[Property(Exclude = true)] IVariableBox outerContext,
				[Property(Exclude = true)] MarkupEvaluationContext markupContext,
				[Property(Exclude = true)] IEnumerable<MarkupVariable> variables
			) {

			return new DivElement(id, setup, outerContext, markupContext, variables);
		}

		public virtual void AddElement(IIdentifiableMarkupElement element) {
			this.elements.Add(element);
		}

		public void AddSlicingValues(SlicingValuesElement slicingValuesElem) {
			if (slicingValuesElem != null) {
				this.slicingValueElements.Add(slicingValuesElem);
			}
		}

		protected NSliceValuesExpression? GetSlicingValues(IEnvironment environment) {
			return slicingValueElements.Where(e => e?.Enabled.Evaluate(environment) ?? false).Select(e => e.NSliceValues).FirstOrDefault();
		}

		protected virtual DrawableDivElement CreateDrawable(IEnvironment evaluationEnvironment, IEnvironment finalDivEnvironment, MarkupCanvasGraphicsData graphicsData, ShapeFactory? shapeFactory, DirectoryPath source, Dimension? size, Position? position, Margins margins, LayoutDirection layout, Arrangement arrangement, LayoutOrder order, float gutter, float aspectRatio, NSliceValuesExpression? slicingValues, (float? width, float? height) minSize, bool provideRemaining, bool diagnostic) {
			return new DrawableDivElement(this, finalDivEnvironment, size, position, margins, layout, arrangement, order, gutter, aspectRatio, slicingValues, minSize, provideRemaining, diagnostic);
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		private List<DrawableDivElement> EvaluateAllDrawables(MarkupCanvasGraphicsData graphicsData, IEnvironment outerEnvironment, ShapeFactory? shapeFactory, bool diagnostic) {

			List<DrawableDivElement> components = new List<DrawableDivElement>();

			IEnvironment graphicsEnvironment = MarkupContext.MakeGraphicsStateEnvironment(graphicsData); // How does this interact with changing canvas variables?
			IEnvironment fullDivEnvironment = outerEnvironment.AppendEnvironment(Variables.ToEnvironment()); // (Is this necessary?)
			IEnvironment outerGraphicsEnvironment = Environments.Concat(graphicsEnvironment, fullDivEnvironment);

			int repeat = setup.repeat?.Evaluate(fullDivEnvironment) ?? 1;
			for (int i = 0; i < repeat; i++) {
				if(setup.enabled != null && setup.enabled.CanCompute(Variables.AppendVariables(MarkupContext.GraphicsStateVariables()))) {
					try {
						if (!setup.enabled.Evaluate(outerGraphicsEnvironment)) {
							continue; // Short cirsuit checking later steps if possible
						}
					}
					catch(EvaluationException) { }
				}

				foreach (IEnvironment forEachEnv in setup.ForEachEnvironments(outerGraphicsEnvironment, false)) {

					IEnvironment finalDivEnvironment = Environments.Concat(forEachEnv, fullDivEnvironment);
					IEnvironment evaluationEnvironment = Environments.Concat(finalDivEnvironment, graphicsEnvironment);

					if (setup.enabled?.Evaluate(evaluationEnvironment) ?? true) {

						Dimension? size = setup.size?.Evaluate(evaluationEnvironment);
						Position? position = setup.position?.Evaluate(evaluationEnvironment);
						Margins margins = setup.margins?.Evaluate(evaluationEnvironment) ?? Margins.Zero;
						LayoutDirection layout = setup.layout?.Evaluate(evaluationEnvironment) ?? LayoutDirection.ROWS;
						Arrangement arrangement = setup.arrangement?.Evaluate(evaluationEnvironment) ?? Arrangement.FRONT;
						LayoutOrder order = setup.order?.Evaluate(evaluationEnvironment) ?? LayoutOrder.FORWARD;
						float gutter = setup.gutter?.Evaluate(evaluationEnvironment) ?? 0f;

						float aspectRatio = setup.aspectRatio?.Evaluate(evaluationEnvironment) ?? -1f;
						NSliceValuesExpression? slicingValues = GetSlicingValues(evaluationEnvironment);
						(float? width, float? height) minSize = (setup.minWidth?.Evaluate(evaluationEnvironment), setup.minHeight?.Evaluate(evaluationEnvironment));
						bool provideRemaining = setup.provideRemaining?.Evaluate(evaluationEnvironment) ?? false;

						// The environment we pass here shouldn't include the MarkupCanvas environment
						//DrawableDivElement drawable = new DrawableDivElement(this, finalDivEnvironment, size, position, margins, layout, gutter, aspectRatio, provideRemaining);
						DrawableDivElement drawable = CreateDrawable(evaluationEnvironment, finalDivEnvironment, graphicsData, shapeFactory, setup.source.GetDirectory()!, size, position, margins, layout, arrangement, order, gutter, aspectRatio, slicingValues, minSize, provideRemaining, diagnostic);

						foreach (IIdentifiableMarkupElement element in elements) {
							if (element is DivElement divElement) {
								List<DrawableDivElement> divDrawables = divElement.EvaluateAllDrawables(graphicsData, finalDivEnvironment, shapeFactory, diagnostic);
								drawable.AddElements(divDrawables);
							}
							else if (element is AreaElement areaElement) {
								if (areaElement.Enabled.Evaluate(finalDivEnvironment)) {
									string areaName = areaElement.Name.Evaluate(finalDivEnvironment);
									drawable.AddAreaElement(areaName, areaElement);
								}
							}
							else if (element is DiagnosticElement diagnosticElement) {
								if (diagnosticElement.Enabled.Evaluate(finalDivEnvironment)) {
									drawable.AddDiagnosticElement(diagnosticElement);
								}
							}
							else if (element is IDrawableElement) {
								drawable.AddElement(element);
							}
						}

						components.Add(drawable);
					}
				}
			}

			return components;
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public DrawableDivElement? GetDrawable(MarkupCanvasGraphicsData graphicsData, IEnvironment outerEnvironment, ShapeFactory? shapeFactory, bool diagnostic) {
			// TODO Need to catch errors here
			List<DrawableDivElement> components = this.EvaluateAllDrawables(graphicsData, outerEnvironment, shapeFactory, diagnostic);
			if (components.Count == 0) {
				return null;
			}
			else if (components.Count == 1) {
				return components[0];
			}
			else {
				// TODO Check this works in all cases
				DrawableDivElement singular = new DrawableDivElement(this, outerEnvironment, null, null, Margins.Zero, LayoutDirection.ROWS, Arrangement.FRONT, LayoutOrder.FORWARD, 0f, -1f, null, (null, null), true, diagnostic);
				singular.AddElements(components);
				return singular;
			}
		}

	}

	public class DrawableDivElement : IIdentifiableMarkupElement, IDrawableGridElement {

		public string? ID => pattern.ID;

		protected readonly DivElement pattern;

		/// <summary>
		/// This environment represents the values belonging to the Div from the hierarchy (itself and its parents), but not those belonging to the canvas.
		/// </summary>
		private readonly IEnvironment environment;

		private readonly List<IIdentifiableMarkupElement> elements;
		private readonly Dictionary<string, AreaElement> areas;
		private readonly List<DiagnosticElement> diagnosticAreas;
		
		private readonly List<DrawableDivElement> children;

		public Dimension? Size { get; }
		public Position? Position { get; }
		public Margins Margins { get; }
		public LayoutDirection Layout { get; }
		public Arrangement Arrangement { get; }
		public LayoutOrder Order { get; }
		public float Gutter { get; }
		public IReadOnlyList<IGridElement> Children => children.ToList<IGridElement>();

		public float AspectRatio { get; }

		private readonly NSliceValuesExpression? slicingValues;
		private readonly (float? width, float? height) minSize;

		// TODO This isn't really being used properly anymore
		protected readonly bool divProvideRemaining;
		public virtual bool ProvidesRemaining { get { return divProvideRemaining || Children.Any(c => c.ProvidesRemaining); } }

		protected readonly bool diagnostic;

		/// <summary>
		/// Constructor for DrawableDivElement.
		/// </summary>
		/// <param name="pattern"> The pattern that this drawable element is derived from. </param>
		/// <param name="environment"> An environment containing the values belonging to the originating Div from the hierarchy (itself and its parents), not including canvas values. </param>
		/// <param name="size"> The size of this Div in the grid layout. </param>
		/// <param name="position"> The position of this Div in the layout. </param>
		/// <param name="margins"> The margins of this Div in the layout. </param>
		/// <param name="layout"> The layout for this Divs children in the grid layout. </param>
		/// <param name="arrangement"> The arrangement for this Divs children in the grid layout. </param>
		/// <param name="order"> The ordering for this Divs children in the grid layout. </param>
		/// <param name="gutter"> The gutter spacing for this Divs children in the grid layout. </param>
		/// <param name="aspectRatio"> The aspect ratio for this Div in the layout. </param>
		/// <param name="slicingValues"> The slicing values expression for this Div for laying out shapes in the Markup engine. </param>
		/// <param name="minSize"> The minimum size (if provided) to use for this Div. </param>
		/// <param name="provideRemaining"> A flag to indicate if this Div should provide remaining area in the grid layout. </param>
		/// <param name="diagnostic"> A flag to indicate that this Div should draw and record diagnostic information when drawn to the canvas. </param>
		public DrawableDivElement(DivElement pattern, IEnvironment environment, Dimension? size, Position? position, Margins margins, LayoutDirection layout, Arrangement arrangement, LayoutOrder order, float gutter, float aspectRatio, NSliceValuesExpression? slicingValues, (float? width, float? height) minSize, bool provideRemaining, bool diagnostic) {
			this.pattern = pattern;
			this.environment = environment;
			this.elements = new List<IIdentifiableMarkupElement>();
			this.areas = new Dictionary<string, AreaElement>();
			this.diagnosticAreas = new List<DiagnosticElement>();
			this.children = new List<DrawableDivElement>();
			this.Size = size;
			this.Position = position;
			this.Margins = margins;
			this.Layout = layout;
			this.Arrangement = arrangement;
			this.Order = order;
			this.Gutter = gutter;
			this.AspectRatio = aspectRatio;
			this.slicingValues = slicingValues;
			this.minSize = minSize;
			this.divProvideRemaining = provideRemaining;
			this.diagnostic = diagnostic;
		}

		public virtual void AddElement(IIdentifiableMarkupElement element) {
			if(element is DrawableDivElement || element is IDrawableElement) {
				this.elements.Add(element);

				if (element is DrawableDivElement drawableDiv) {
					children.Add(drawableDiv);
				}
			}
			else {
				throw new InvalidOperationException("Provided element is not drawable.");
			}
		}
		public void AddElements(IEnumerable<DrawableDivElement> elements) {
			foreach (DrawableDivElement element in elements) {
				this.AddElement(element);
			}
		}

		public virtual void AddAreaElement(string name, AreaElement area) {
			if (!string.IsNullOrWhiteSpace(name)) {
				areas[name] = area;
			}
		}

		public virtual void AddDiagnosticElement(DiagnosticElement diagnosticElement) {
			diagnosticAreas.Add(diagnosticElement);
		}

		public virtual Size? MinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			if (minSize.width.HasValue && minSize.height.HasValue) { return new Layouts.Size(minSize.width.Value, minSize.height.Value); }

			Size? gridMinimum = GridElements.OverallMinimumSize(children.ToList<IGridElement>(), graphicsState, availableSpace, Layout, Gutter);

			if (gridMinimum is not null || minSize.width.HasValue || minSize.height.HasValue) {
				return new Size(minSize.width ?? gridMinimum?.Width ?? 0f, minSize.height ?? gridMinimum?.Height ?? 0f);
				//return new Size(Math.Max(minSize.width ?? 0f, gridMinimum?.Width ?? 0f), Math.Max(minSize.height ?? 0f, gridMinimum?.Height ?? 0f));
			}

			return null;
		}

		public virtual Rectangle? ContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			if (divProvideRemaining || children.Count > 0) {
				return rect;
			}
			else {
				return null;
			}
			//return rect; // TODO This is incomplete. How do we indicate when to stop?
		}

		public virtual bool AreaExists(string name) {
			if (areas.ContainsKey(name)) {
				return true;
			}
			foreach (DrawableDivElement child in children) {
				if (child.AreaExists(name)) {
					return true;
				}
			}
			return false;
		}

		public virtual IEnumerable<string> GetAreas() {
			return areas.Keys.Concat(children.SelectMany(c => c.GetAreas())).Distinct();
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		/// <exception cref="EvaluationException"></exception>
		public virtual Rectangle? GetNamedArea(string name, ISharpGraphicsState graphicsState, Rectangle rect) {
			if (!AreaExists(name)) {
				return null;
			}
			else if (areas.TryGetValue(name, out AreaElement? area)) {
				return GetRect(graphicsState, rect, area.Area); // TODO Need to be very careful about the order in which Margins and Aspect are being applied here
			}

			Rectangle?[] childRects = GridElements.GetElementRects(this, children.ToList<IGridElement>(), graphicsState, rect, out _, out _, out _, out _, out _);
			for (int i=0; i<children.Count; i++) {
				if (children[i].AreaExists(name) && childRects[i] is not null) {
					return children[i].GetNamedArea(name, graphicsState, childRects[i]!);
				}
			}

			return null;
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="InvalidRectangleException"></exception>
		public virtual Rectangle? GetFullFromNamedArea(string name, ISharpGraphicsState graphicsState, Rectangle rect) {
			if (!AreaExists(name)) {
				return null;
			}
			else if (areas.TryGetValue(name, out AreaElement? area)) {
				return InvertGetRect(graphicsState, rect, area.Area); // TODO Need to be very careful about the order in which Margins and Aspect are being applied here
			}

			// TODO Need to verify that this works
			foreach (DrawableDivElement child in children) {
				if (child.AreaExists(name)) {
					if (((IGridElement)child).IsInset()) {
						// This should be possible for relative-sized inset areas
						throw new InvalidOperationException("Cannot compute full rect from inset area."); // TODO Should have custom exception type?
					}
					if (!child.Size.HasValue) {
						throw new InvalidOperationException("Cannot compute full rect from div without known size."); // TODO Should have custom exception type?
					}

					Rectangle? fullRect = child.GetFullFromNamedArea(name, graphicsState, rect);

					if (fullRect is not null) {
						float finalLength = Divisions.InferLength(
							children.SelectNotNull(c => c.Size).ToArray(),
							Gutter,
							child.Size.Value,
							(Layout == LayoutDirection.ROWS) ? fullRect.Height : fullRect.Width);

						Rectangle finalRect = new Rectangle(
							(Layout == LayoutDirection.ROWS) ? fullRect.Width : finalLength,
							(Layout == LayoutDirection.COLUMNS) ? fullRect.Height : finalLength
							);

						return InvertApplyAspect(finalRect.Margins(Margins, true));
					}
				}
			}

			return null;
		}

		public virtual void Draw(ISharpCanvas canvas, Rectangle fullRect, CancellationToken cancellationToken) {

			if (cancellationToken.IsCancellationRequested) { return; }

			Rectangle rect = ApplyAspect(fullRect);

			Rectangle?[] childRects = GridElements.GetElementRects(this, children.ToList<IGridElement>(), canvas, rect, out Rectangle drawingRect, out _, out _, out _, out _);

			if (diagnostic) {
				canvas.RegisterAreas(pattern, fullRect, drawingRect, Array.Empty<Rectangle>());
			}

			int childIdx = 0;
			for (int i = 0; i < elements.Count; i++) {

				if (cancellationToken.IsCancellationRequested) { return; }

				if (elements[i] is DrawableDivElement child) {
					try {
						child.Draw(canvas, childRects[childIdx] ?? throw new InvalidRectangleException("Not enough space for div element."), cancellationToken);
						childIdx += 1;
					}
					catch (InvalidRectangleException e) { // TODO What other exception types?
						SharpDrawingException drawingException = new SharpDrawingException(child.pattern, e.Message, e);
						canvas.LogError(drawingException);
						throw drawingException;
					}
				}
				else if (elements[i] is IDrawableElement drawable) {
					MarkupCanvas? markupCanvas = null;
					try {
						markupCanvas = MarkupCanvas.Open(canvas, drawingRect, pattern.setup.canvasArea, slicingValues, environment, pattern.MarkupContext, diagnostic);
						//if (pattern.setup.drawingCoords != null) markupCanvas.SetDrawingCoords(pattern.setup.drawingCoords);
						foreach(IEnvironment forEachEnv in drawable.StyleSheet.GetForEachEnvironments(markupCanvas.Environment)) {
							markupCanvas.SaveEnvironment();
							markupCanvas.ApplyEnvironment(forEachEnv);
							drawable.Draw(markupCanvas);
							markupCanvas.RestoreEnvironment();
						}
						markupCanvas.Close(out SharpDrawingException[] errors);
						foreach(SharpDrawingException ex in errors) { canvas.LogError(drawable, ex.Message, ex); }
					}
					catch (Exception e) { // TODO This is way too broad
						if(markupCanvas != null && !markupCanvas.IsClosed) {
							try {
								markupCanvas.Close(out SharpDrawingException[] errors);
								foreach (SharpDrawingException ex in errors) { canvas.LogError(drawable, ex.Message, ex); }
							}
							catch(MarkupCanvasStateException me) {
								throw new InvalidOperationException(me.Message, me);
							}
						}

						SharpDrawingException drawingException = new SharpDrawingException(drawable, "Drawing error: " + e.Message, e);
						canvas.LogError(drawingException);
						throw drawingException;
					}
				}
			}

			if (diagnostic) {
				try {
					if (slicingValues != null && GetSlicing(canvas, fullRect) is NSliceScaling vals) {
						canvas.SaveState();
						canvas.SetStrokeDash(new StrokeDash(new float[] { 1f, 4f }, 2f));
						canvas.SetStrokeColor(Color.Blue);
						canvas.SetFillColor(Color.Blue.WithOpacity(0.08f));

						for (int i = 0; i < vals.availableXLengths.Length; i++) {
							if (i % 2 == 0) {
								float start = fullRect.Left + (i > 0 ? vals.availableXSlices[i - 1] : 0f);
								canvas.Rectangle(start, fullRect.Bottom, vals.availableXLengths[i], fullRect.Height).Fill();
							}
						}

						for (int i = 0; i < vals.availableYLengths.Length; i++) {
							if (i % 2 == 0) {
								float start = fullRect.Bottom + (i > 0 ? vals.availableYSlices[i - 1] : 0f);
								canvas.Rectangle(fullRect.Left, start, fullRect.Width, vals.availableYLengths[i]).Fill();
							}
						}

						canvas.RestoreState();
					}

					if (areas.Count > 0) {
						foreach (AreaElement area in areas.Values) {
							Rectangle areaRect = GetRect(canvas, fullRect, area.Area);
							canvas.RegisterAreas(area, areaRect, null, Array.Empty<Rectangle>());
						}
					}
					if (diagnosticAreas.Count > 0) {
						foreach (DiagnosticElement diagnosticElem in diagnosticAreas) {
							Rectangle diagnosticRect = GetRect(canvas, fullRect, diagnosticElem.Area);
							canvas.RegisterAreas(diagnosticElem, diagnosticRect, null, Array.Empty<Rectangle>());
						}
					}
				}
				catch (EvaluationException) { }
			}
		}

		public Rectangle[] DiagnosticRects(ISharpGraphicsState graphicsState, Rectangle available) {
			return GetDiagnosticRects(graphicsState, available).ToArray();
		}

		protected IEnumerable<Rectangle> GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle rect) {
			foreach (DiagnosticElement diagnostic in diagnosticAreas) {
				yield return GetRect(graphicsState, rect, diagnostic.Area);
			}

			Rectangle?[] childRects = GridElements.GetElementRects(this, children.ToList<IGridElement>(), graphicsState, rect, out _, out _, out _, out _, out _);
			for (int i = 0; i < children.Count; i++) {
				if (childRects[i] is not null) {
					foreach (Rectangle childDiagnostic in children[i].GetDiagnosticRects(graphicsState, childRects[i]!)) {
						yield return childDiagnostic;
					}
				}
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationCalculationException"></exception>
		private NSliceScaling? GetSlicing(ISharpCanvas canvas, Rectangle fullRect) {
			Rectangle rect = ApplyAspect(fullRect);
			MarkupGeometry markupGeometry = MarkupGeometry.CreateGeometry(canvas.GetSnapshot(), rect, pattern.setup.canvasArea, slicingValues, environment, pattern.MarkupContext, out _);
			NSliceScaling? slicing = markupGeometry.Slicing;

			return slicing;
		}

		protected Rectangle ApplyAspect(Rectangle rect) {
			return rect.Aspect(AspectRatio);
		}

		protected Rectangle InvertApplyAspect(Rectangle rect) {
			return rect.ContainAspect(AspectRatio);
		}

		private IVariableBox AreaMarginsOnlyComputeVariables => VariableBoxes.Concat(environment, pattern.MarkupContext.GraphicsStateVariables());
		private IEnvironment AreaMarginsOnlyComputeEnvironment(ISharpGraphicsState graphicsState) => Environments.Concat(environment, pattern.MarkupContext.MakeGraphicsStateEnvironment(graphicsState.GetMarkupData()));
		
		private IVariableBox AreaFullComputeVariables => pattern.MarkupContext.InferenceDrawingStateVariables().AppendVariables(environment);

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		private Rectangle GetRect(ISharpGraphicsState graphicsState, Rectangle fullRect, AreaRectExpression rectExpression) {
			Rectangle availableRect = ApplyAspect(fullRect).Margins(Margins, false);

			if (rectExpression != null && rectExpression.Rect == null && rectExpression.Margins != null && rectExpression.Margins.CanCompute(AreaMarginsOnlyComputeVariables)) {
				IEnvironment marginEnv = AreaMarginsOnlyComputeEnvironment(graphicsState);
				Margins evaluatedMargins = rectExpression.Margins.Evaluate(marginEnv);
				Rectangle marginedRect = availableRect.Margins(evaluatedMargins, false);

				return marginedRect;
			}
			else if (rectExpression != null) {
				MarkupGeometry markupGeometry = MarkupGeometry.CreateGeometry(graphicsState.GetSnapshot(), availableRect, pattern.setup.canvasArea, slicingValues, environment, pattern.MarkupContext, out IEnvironment geometryEnv);
				
				//AreaRect evaluatedAreaRect = rectExpression.Evaluate(markupGeometry.Environment);
				Rectangle evaluatedRect = (rectExpression.Rect ?? pattern.MarkupContext.WholeAreaRectExpression).Evaluate(geometryEnv);
				Margins evaluatedMargins = rectExpression.Margins?.Evaluate(geometryEnv) ?? Margins.Zero;

				Rectangle transformedRect = markupGeometry.TransformRectangle(new RectangleExpression(evaluatedRect, geometryEnv.Context), geometryEnv);
				Rectangle positionedRect = new Rectangle(availableRect.X + transformedRect.X, availableRect.Y + transformedRect.Y, transformedRect.Width, transformedRect.Height);
				Rectangle marginedRect = positionedRect.Margins(evaluatedMargins, false);

				return marginedRect;
			}

			else {
				return availableRect;
			}
		}

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="InvalidRectangleException"></exception>
		private Rectangle InvertGetRect(ISharpGraphicsState graphicsState, Rectangle availableRect, AreaRectExpression remainingRectExpr) {
			if (remainingRectExpr != null) {
				//bool canCompute = remainingRectExpr?.CanCompute(AreaComputeVariables) ?? false; // This must be true for this to work -> We would encounter recursion if the expression contains canvas variables
				Size? canvasArea = pattern.setup.canvasArea;

				if(remainingRectExpr.Rect == null && remainingRectExpr.Margins != null && remainingRectExpr.CanCompute(AreaMarginsOnlyComputeVariables)) {
					IEnvironment marginEnv = AreaMarginsOnlyComputeEnvironment(graphicsState);
					Margins evaluatedMargins = remainingRectExpr.Margins.Evaluate(marginEnv);
					Rectangle marginedRect = availableRect.Margins(evaluatedMargins, true);

					return InvertApplyAspect(marginedRect.Margins(Margins, true));
				}
				else if (canvasArea != null && remainingRectExpr.CanCompute(AreaFullComputeVariables)) {
					MarkupGeometry markupGeometry = MarkupGeometry.CreateGeometry(graphicsState.GetSnapshot(), (Rectangle)canvasArea, canvasArea, slicingValues, environment, pattern.MarkupContext, out IEnvironment geometryEnv);

					//AreaRect remainingAreaRect = remainingRectExpr.Evaluate(markupGeometry.Environment);
					Rectangle remainingRect = (remainingRectExpr.Rect ?? pattern.MarkupContext.WholeAreaRectExpression).Evaluate(geometryEnv);
					Margins remainingMargins = remainingRectExpr.Margins?.Evaluate(geometryEnv) ?? Margins.Zero;

					Rectangle marginedRect = availableRect.Margins(remainingMargins, true);
					Rectangle fullRect = markupGeometry.InferCanvasArea(remainingRect, marginedRect);

					return InvertApplyAspect(fullRect.Margins(Margins, true)); // Take account of any enforced aspect
				}
				else if (canvasArea != null) {
					throw new InvalidRectangleException("Recursion encountered. Cannot compute required area for this rect.");
				}
				else {
					throw new InvalidRectangleException("A canvas area is required for inferring full rect.");
				}
			}
			else {
				return InvertApplyAspect(availableRect.Margins(Margins, true));
			}
		}
	}

	/// <summary>
	/// This element indicates a named area within a pattern that may be used by
	/// other document elements -- for example, the remaining area inside an outline.
	/// Each area, therefore, defines a region of the pattern, and a name. The available
	/// names depend on the pattern type the area belongs to, and not all patterns use
	/// all possible names.
	/// </summary>
	public class AreaElement : IIdentifiableMarkupElement {

		public string? ID { get; }

		public IExpression<string> Name { get; }
		public AreaRectExpression Area { get; }

		public BoolExpression Enabled { get; } // TODO This needs properly implementing in DivElement

		/// <summary>
		/// Constructor for AreaElement.
		/// </summary>
		/// <param name="id" default="null">A unique name for this element.</param>
		/// <param name="_name" default="null">The name for this area, identifying its
		/// function in the pattern. The available names depend on the pattern type.</param>
		/// <param name="_x" default="0">The x-coordinate of this area.</param>
		/// <param name="_y" default="0">The y-coordinate of this area.</param>
		/// <param name="_width" default="$width">The width of this area.</param>
		/// <param name="_height" default="$height">The height of this area.</param>
		/// <param name="margin" default="0,0,0,0">A margin to be applied to this area,
		/// after the initial layout using <paramref name="_x"/>, <paramref name="_y"/>,
		/// <paramref name="_width"/>, and <paramref name="_height"/>. This will likely
		/// mean that the final width and height are not equal to <paramref name="_width"/>
		/// and <paramref name="_height"/>.</param>
		/// <param name="enabled" default="true">A flag to indicate whether this area should
		/// be available in the pattern.</param>
		/// <param name="markupContext" exclude="True"></param>
		public AreaElement(string? id, IExpression<string> _name, FloatExpression? _x, FloatExpression? _y, FloatExpression? _width, FloatExpression? _height, MarginsExpression? margin, BoolExpression enabled, MarkupEvaluationContext markupContext) {
			this.ID = id;
			this.Name = _name;
			RectangleExpression? rect =
				(_x is not null || _y is not null || _width is not null || _height is not null)
				? new RectangleExpression(
					_x ?? new FloatExpression(0f, markupContext.TypeSystem),
					_y ?? new FloatExpression(0f, markupContext.TypeSystem),
					_width ?? markupContext.WidthExpression,
					_height ?? markupContext.HeightExpression)
				: null;
			this.Area = new AreaRectExpression(rect, margin, markupContext.TypeSystem);
			this.Enabled = enabled;
		}

		/// <param name="id">A unique name for this element.</param>
		/// <param name="name">The name for this area, identifying its
		/// function in the pattern. The available names depend on the pattern type.</param>
		/// <param name="x">The x-coordinate of this area.</param>
		/// <param name="y">The y-coordinate of this area.</param>
		/// <param name="width">The width of this area.</param>
		/// <param name="height">The height of this area.</param>
		/// <param name="margin">A margin to be applied to this area,
		/// after the initial layout using <paramref name="x"/>, <paramref name="y"/>,
		/// <paramref name="width"/>, and <paramref name="height"/>. This will likely
		/// mean that the final width and height are not equal to <paramref name="width"/>
		/// and <paramref name="height"/>.</param>
		/// <param name="enabled">A flag to indicate whether this area should
		/// be available in the pattern.</param>
		/// <param name="markupContext"></param>
		[FactoryBuilder(typeof(AreaElement), Name = "area")]
		public static AreaElement Build(
				[Property(Default = "null")] string? id,
				[LocalProperty(Default = "null")] IExpression<string> name,
				[LocalProperty(Default = "0")] FloatExpression? x,
				[LocalProperty(Default = "0")] FloatExpression? y,
				[LocalProperty(Default = "$width")] FloatExpression? width,
				[LocalProperty(Default = "$height")] FloatExpression? height,
				[Property(Default = "0,0,0,0")] MarginsExpression? margin,
				[Property(Default = "true")] BoolExpression enabled,
				[Property(Exclude = true)] MarkupEvaluationContext markupContext
			) {

			return new AreaElement(id, name, x, y, width, height, margin, enabled, markupContext);
		}

	}

	/// <summary>
	/// This element indicates an area in the pattern which is of use or interest to designers
	/// utilising the pattern. It will not be rendered when the pattern is used, but may be
	/// displayed in the designer while a document is being edited.
	/// </summary>
	public class DiagnosticElement : IIdentifiableMarkupElement {

		public string? ID { get; }

		public AreaRectExpression Area { get; }

		public BoolExpression Enabled { get; } // TODO This needs properly implementing in DivElement

		/// <summary>
		/// Constructor for DiagnosticElement.
		/// </summary>
		/// <param name="id" default="null">A unique name for this element.</param>
		/// <param name="_x" default="0">The x-coordinate of this area.</param>
		/// <param name="_y" default="0">The y-coordinate of this area.</param>
		/// <param name="_width" default="$width">The width of this area.</param>
		/// <param name="_height" default="$height">The height of this area.</param>
		/// <param name="enabled" default="true">A flag to indicate whether this area should
		/// be shown with the pattern.</param>
		/// <param name="markupContext" exclude="True"></param>
		public DiagnosticElement(string? id, FloatExpression? _x, FloatExpression? _y, FloatExpression? _width, FloatExpression? _height, BoolExpression enabled, MarkupEvaluationContext markupContext) {
			this.ID = id;
			RectangleExpression? rect =
				(_x is not null || _y is not null || _width is not null || _height is not null)
				? new RectangleExpression(
					_x ?? new FloatExpression(0f, markupContext.TypeSystem),
					_y ?? new FloatExpression(0f, markupContext.TypeSystem),
					_width ?? markupContext.WidthExpression,
					_height ?? markupContext.HeightExpression)
				: null;
			this.Area = new AreaRectExpression(rect, new MarginsExpression(Margins.Zero, markupContext.TypeSystem), markupContext.TypeSystem);
			this.Enabled = enabled;
		}

		/// <param name="id">A unique name for this element.</param>
		/// <param name="x">The x-coordinate of this area.</param>
		/// <param name="y">The y-coordinate of this area.</param>
		/// <param name="width">The width of this area.</param>
		/// <param name="height">The height of this area.</param>
		/// <param name="enabled">A flag to indicate whether this area should
		/// be shown with the pattern.</param>
		/// <param name="markupContext" exclude="True"></param>
		[FactoryBuilder(typeof(DiagnosticElement), Name = "diagnostic")]
		public static DiagnosticElement Build(
				[Property(Default = "null")] string? id,
				[Property(Default = "0")] FloatExpression? x,
				[Property(Default = "0")] FloatExpression? y,
				[Property(Default = "$width")] FloatExpression? width,
				[Property(Default = "$height")] FloatExpression? height,
				[Property(Default = "true")] BoolExpression enabled,
				[Property(Exclude = true)] MarkupEvaluationContext markupContext
			) {

			return new DiagnosticElement(id, x, y, width, height, enabled, markupContext);
		}
	}
}
