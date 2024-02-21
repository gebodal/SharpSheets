using SharpSheets.Canvas;
using SharpSheets.Evaluations;
using SharpSheets.Markup.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;

namespace SharpSheets.Markup.Elements {

	public class StyleSheet { // This is actually a CSS style sheet for SVG elements, I think

		// Baseline stuff? (For text drawing)

		public ClipPath? ClipPath { get; }
		public EnumExpression<AreaRule>? ClipRule { get; }
		//public ColorExpression? Color { get; set; } // TODO This really only exists to provide a default value for other colors
		// Direction? (For text direction)
		public ICanvasPaint? Fill { get; }
		public FloatExpression? FillOpacity { get; }
		public EnumExpression<AreaRule>? FillRule { get; }
		// Filter?
		//public FontPathGrouping FontFamily { get; set; } // Is this the right type?
		public FloatExpression? FontSize { get; }
		// Font Stretch?
		public EnumExpression<TextFormat>? FontStyle { get; }
		// Font weight - is included in TextFormat for us?
		// Letter spacing?
		//public Marker MarkerEnd { get; set; }
		//public Marker MarkerMid { get; set; }
		//public Marker MarkerStart { get; set; }
		//public Mask Mask { get; set; } // Cannot render a mask like this in a PDF, I think (Maybe with a softmask?)
		//public FloatExpression? Opacity { get;  }
		//public EnumExpression<Overflow> Overflow { get; set; }
		//public ColorExpression StopColor { get; set; }
		//public FloatExpression StopOpacity { get; set; }
		public ICanvasPaint? Stroke { get; }
		public FloatExpression[]? StrokeDashArray { get; }
		public FloatExpression? StrokeDashOffset { get; }
		public EnumExpression<LineCapStyle>? StrokeLineCap { get; }
		public EnumExpression<LineJoinStyle>? StrokeLineJoin { get; }
		public FloatExpression? StrokeMiterLimit { get; }
		public FloatExpression? StrokeOpacity { get; }
		public FloatExpression? StrokeWidth { get; }
		public EnumExpression<TextAnchor>? TextAnchor { get; }
		//public EnumExpression<TextBaseline>? TextBaseline { get; set; }
		public ColorExpression? TextColor { get; }
		// Text decoration?
		public TransformExpression? Transform { get; }
		// Word spacing?
		// Writing mode? (Left-to-Right or Right-to-Left, e.g. for Arabic)

		public EnumExpression<DrawingCoords>? DrawingCoords { get; } // TODO Should this be removed?
		public BoolExpression Enabled { get; } = true;

		public ForEachExpression? ForEach { get; } = null;

		public StyleSheet() { }

		/// <summary>
		/// Constructor for StyleSheet.
		/// </summary>
		/// <param name="_clip_path">A reference to some clipping geometry that will be used to
		/// clip the current element.</param>
		/// <param name="clip_rule">The clipping rule to be used when a clipping path is specified.</param>
		/// <param name="fill" default="$background">A paint used to fill any shape geometries.
		/// This can either be a reference to a paint element, or a color value. If "none", a
		/// there will be no fill. The default is the current background colour.</param>
		/// <param name="fill_opacity">When the <paramref name="fill"/> is specified as a color value,
		/// this attribute may be used to modify the opacity of that solid color. This will
		/// override any A value provided with ARGB values.</param>
		/// <param name="fill_rule" default="NonZero">The fill rule to be used when filling shape
		/// geometries with the <paramref name="fill"/> paint.</param>
		/// <param name="font_size">The fontsize to use for any text.</param>
		/// <param name="font_style" default="REGULAR">Font format to use for any text. This will
		/// use the appropriate font format from the current font selection.</param>
		/// <param name="stroke">A paint used to stroke any shape geometry paths.
		/// This can either be a reference to a paint element, or a color value. If "none", a
		/// there will be no path stroking. The default is "none".</param>
		/// <param name="stroke_dasharray">An array of dash lengths with which to stroke any shape
		/// geometry paths. The resulting stroke will be a series of "on" and "off" lengths,
		/// corresponding to the dash array. These lengths are measured in points.</param>
		/// <param name="stroke_dashoffset">An offset for the start of the stroke dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="stroke_linecap">The shape to be used at the ends of open shape geometry paths
		/// (and dashes, if any) when they are stroked.</param>
		/// <param name="stroke_linejoin">The way in which the outer edges of two connected shape
		/// geometry paths segments should be joined.</param>
		/// <param name="stroke_miterlimit">The limit on the ratio of the miter length to the stroke
		/// width to use when stroke paths are joined with mitres. A bevel join will be used if
		/// the ratio is exceeded.</param>
		/// <param name="stroke_opacity">When the <paramref name="stroke"/> is specified as a color
		/// value, this attribute may be used to modify the opacity of that solid color. This will
		/// override any A value provided with ARGB values.</param>
		/// <param name="stroke_width">The line width to use when stroking any shape geometries.</param>
		/// <param name="text_anchor" default="START">When drawing text, this attribute will be used
		/// to determine how a text chunk should be aligned, relative to the current start point
		/// of the text layout.</param>
		/// <param name="text_color" default="$textcolor">The color to use when filling text glyphs.</param>
		/// <param name="_transform">The transform to apply to this element before rendering. This can be
		/// expressed as a series of affine transformations. If no value is provided, the transform
		/// is the Identity transformation.</param>
		/// <param name="drawing_coords">[Experimental Feature] Affects the drawing coordinates
		/// used for this element.</param>
		/// <param name="_enabled" default="true">A flag to indicate whether this element should be
		/// rendered and included in layouts.</param>
		/// <param name="_for_each">Specifies that the element should be repeated a number of times
		/// based on some collection, with one repetition for each entry in that collection, with that
		/// entry being available as an environment variable to the element.</param>
		public StyleSheet(
			ClipPath? _clip_path,
			EnumExpression<AreaRule>? clip_rule,
			ICanvasPaint? fill,
			FloatExpression? fill_opacity,
			EnumExpression<AreaRule>? fill_rule,
			FloatExpression? font_size,
			EnumExpression<TextFormat>? font_style,
			//FloatExpression? _opacity,
			ICanvasPaint? stroke,
			FloatExpression[]? stroke_dasharray,
			FloatExpression? stroke_dashoffset,
			EnumExpression<LineCapStyle>? stroke_linecap,
			EnumExpression<LineJoinStyle>? stroke_linejoin,
			FloatExpression? stroke_miterlimit,
			FloatExpression? stroke_opacity,
			FloatExpression? stroke_width,
			EnumExpression<TextAnchor>? text_anchor,
			//EnumExpression<TextBaseline> text_baseline,
			ColorExpression? text_color,
			TransformExpression? _transform,
			EnumExpression<DrawingCoords>? drawing_coords,
			BoolExpression _enabled,
			ForEachExpression? _for_each
			) {

			ClipPath = _clip_path;
			ClipRule = clip_rule;
			//Color = color;
			Fill = fill;
			FillOpacity = fill_opacity;
			FillRule = fill_rule;
			FontSize = font_size;
			FontStyle = font_style;
			//Opacity = _opacity;
			Stroke = stroke;
			StrokeDashArray = stroke_dasharray;
			StrokeDashOffset = stroke_dashoffset;
			StrokeLineCap = stroke_linecap;
			StrokeLineJoin = stroke_linejoin;
			StrokeMiterLimit = stroke_miterlimit;
			StrokeOpacity = stroke_opacity;
			StrokeWidth = stroke_width;
			TextAnchor = text_anchor;
			//TextBaseline = text_baseline;
			TextColor = text_color;
			Transform = _transform;

			DrawingCoords = drawing_coords;
			Enabled = _enabled;

			ForEach = _for_each;
		}

		public StyleSheet Update(
			ClipPath? clip_path = null,
			EnumExpression<AreaRule>? clip_rule = null,
			ICanvasPaint? fill = null,
			FloatExpression? fill_opacity = null,
			EnumExpression<AreaRule>? fill_rule = null,
			FloatExpression? font_size = null,
			EnumExpression<TextFormat>? font_style = null,
			ICanvasPaint? stroke = null,
			FloatExpression[]? stroke_dasharray = null,
			FloatExpression? stroke_dashoffset = null,
			EnumExpression<LineCapStyle>? stroke_linecap = null,
			EnumExpression<LineJoinStyle>? stroke_linejoin = null,
			FloatExpression? stroke_miterlimit = null,
			FloatExpression? stroke_opacity = null,
			FloatExpression? stroke_width = null,
			EnumExpression<TextAnchor>? text_anchor = null,
			ColorExpression? text_color = null,
			TransformExpression? transform = null,
			EnumExpression<DrawingCoords>? drawing_coords = null,
			BoolExpression? enabled = null,
			ForEachExpression? for_each = null
			) {

			return new StyleSheet(
				_clip_path: clip_path ?? ClipPath,
				clip_rule: clip_rule ?? ClipRule,
				fill: fill ?? Fill,
				fill_opacity: fill_opacity ?? FillOpacity,
				fill_rule: fill_rule ?? FillRule,
				font_size: font_size ?? FontSize,
				font_style: font_style ?? FontStyle,
				stroke: stroke ?? Stroke,
				stroke_dasharray: stroke_dasharray ?? StrokeDashArray,
				stroke_dashoffset: stroke_dashoffset ?? StrokeDashOffset,
				stroke_linecap: stroke_linecap ?? StrokeLineCap,
				stroke_linejoin: stroke_linejoin ?? StrokeLineJoin,
				stroke_miterlimit: stroke_miterlimit ?? StrokeMiterLimit,
				stroke_opacity: stroke_opacity ?? StrokeOpacity,
				stroke_width: stroke_width ?? StrokeWidth,
				text_anchor: text_anchor ?? TextAnchor,
				text_color: text_color ?? TextColor,
				_transform: transform ?? Transform,
				drawing_coords: drawing_coords ?? DrawingCoords,
				_enabled: enabled ?? Enabled,
				_for_each: for_each ?? ForEach
			);
		}

		public IEnumerable<IEnvironment> GetForEachEnvironments(IEnvironment outerEnvironment) {
			if (ForEach != null) {
				return ForEach.EvaluateEnvironments(outerEnvironment, false);
			}
			else {
				return Environments.Empty.Yield();
			}
		}

		public IVariableBox GetVariables(IVariableBox outerVariables) {
			if (ForEach != null) {
				return outerVariables.AppendVariables(SimpleVariableBoxes.Single(ForEach.Variable));
			}
			else {
				return outerVariables;
			}
		}

		public StyleSheet WithoutForEach() {
			return new StyleSheet(
				_clip_path: ClipPath,
				clip_rule: ClipRule,
				fill: Fill,
				fill_opacity: FillOpacity,
				fill_rule: FillRule,
				font_size: FontSize,
				font_style: FontStyle,
				stroke: Stroke,
				stroke_dasharray: StrokeDashArray,
				stroke_dashoffset: StrokeDashOffset,
				stroke_linecap: StrokeLineCap,
				stroke_linejoin: StrokeLineJoin,
				stroke_miterlimit: StrokeMiterLimit,
				stroke_opacity: StrokeOpacity,
				stroke_width: StrokeWidth,
				text_anchor: TextAnchor,
				text_color: TextColor,
				_transform: Transform,
				drawing_coords: DrawingCoords,
				_enabled: Enabled,
				_for_each: null
			);
		}
	}

}
