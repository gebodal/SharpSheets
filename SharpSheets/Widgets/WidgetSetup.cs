using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using SharpSheets.Parsing;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Fonts;
using SharpSheets.Colors;

namespace SharpSheets.Widgets {

	public readonly struct WidgetSetup {

		public static readonly float defaultLinewidth = 1f;
		public static readonly float defaultGutter = 0f;
		//public static readonly Dimension defaultSize = Dimension.Single;
		public static readonly Color defaultForegroundColor = Color.Black;
		public static readonly Color defaultBackgroundColor = Color.White;
		public static readonly Color defaultMidtoneColor = Color.Gray;
		public static readonly Color defaultTextColor = Color.Black;

		public readonly float linewidth;
		public readonly Color foregroundColor;
		public readonly Color backgroundColor;
		public readonly Color midtoneColor;
		public readonly Color textColor;
		public readonly FontSettingGrouping finalFonts;
		public readonly float gutter;
		public readonly IDetail? gutterStyle;
		public readonly Dimension? size;
		public readonly Position? position;
		public readonly Margins margins;
		public readonly Layout layout;
		public readonly Arrangement arrangement;
		public readonly LayoutOrder order;
		public readonly bool diagnostic;

		/// <summary>
		/// Constructor for WidgetSetup.
		/// </summary>
		/// <param name="_margins" default="(0,0,0,0)">Margins to apply to the widget area before drawing.
		/// These margins will be factored into the minimum size of the widget if
		/// autosizing is used.</param>
		/// <param name="linewidth">Default line width for this widget and its children.</param>
		/// <param name="foreground" default="Black">Foreground color for this widget and its children.
		/// This color is commonly used for outlines and detailing.</param>
		/// <param name="background" default="White">Background color for this widget and its children.
		/// This color is commonly used to fill in background areas.</param>
		/// <param name="midtone" default="Gray">Midtone color for this widget and its children.
		/// This color is commonly used for secondary details.</param>
		/// <param name="textColor" default="Black">Text color for this widget and its children.</param>
		/// <param name="font">Fonts to use for text in this widget and its children.
		/// This value can be provided as 1 to 4
		/// font names or paths, specifying Regular, Bold, Italic, and Bold Italic font styles.
		/// Fonts are identified by name or path, searching first in the current directory,
		/// and then in the system font directory.</param>
		/// <param name="gutter">Spacing between this widget's children, measured in points.</param>
		/// <param name="gutter_">Gutter style for this widget and its children.
		/// This style is used to draw detailing in the spaces between child widgets.</param>
		/// <param name="_size" default="1">Size of the widget, either as a absolute dimension (pt, cm, in),
		/// a relative size (in percent or arbitrary units), or auto-sized (with "auto"). If a size is provided,
		/// position should not be used.</param>
		/// <param name="_position">Position the widget as an inset of its parents area.
		/// Position is specified by an anchor point, width, height, and x and y offsets.
		/// The lengths can be expressed as absolute lengths (pt, cm, in), or percentages of the
		/// overall widget size. If this value is set, the size parameter will be ignored.</param>
		/// <param name="layout">Specifies the arrangement of child widgets on the page,
		/// either as rows or columns.</param>
		/// <param name="arrangement">Specifies the arrangement of the widgets children 
		/// in the available space, indicating whether the children should be arranged
		/// centrally, or to one end of, the available space.</param>
		/// <param name="order">Specifies the order that the widgets children should be drawn in 
		/// across the available space, allowing children to be drawn in reverse document order.</param>
		/// <param name="_diagnostic">Flag to indicate that schematic information is to
		/// be drawn for this widget, to help with design and debugging.</param>
		public WidgetSetup(
					Margins _margins = default,
					float linewidth = 1f,
					Color? foreground = null,
					Color? background = null,
					Color? midtone = null,
					Color? textColor = null,
					FontArgument? font = null,
					float gutter = 0f,
					IDetail? gutter_ = null, // gutter_
					Dimension? _size = null,
					Position? _position = null,
					Layout layout = Layout.ROWS,
					Arrangement arrangement = Arrangement.FRONT,
					LayoutOrder order = LayoutOrder.FORWARD,
					bool _diagnostic = false
				) {

			this.linewidth = linewidth;
			this.foregroundColor = foreground ?? defaultForegroundColor;
			this.backgroundColor = background ?? defaultBackgroundColor;
			this.midtoneColor = midtone ?? defaultMidtoneColor;
			this.textColor = textColor ?? defaultTextColor;

			this.finalFonts = font?.Fonts ?? new FontSettingGrouping();

			this.gutter = gutter;
			this.gutterStyle = gutter_;

			this.size = (_size == null && _position == null) ? Dimension.Single : _size;
			this.position = _position;

			this.margins = _margins;
			this.layout = layout;
			this.arrangement = arrangement;
			this.order = order;

			this.diagnostic = _diagnostic;
		}

		public static WidgetSetup Empty { get; } = new WidgetSetup(_diagnostic: false);
		public static WidgetSetup Diagnostic { get; } = new WidgetSetup(_diagnostic: true);

		public static WidgetSetup ErrorSetup { get; } = new WidgetSetup(foreground: Colors.Color.Red, midtone: Colors.Color.Orange, background: Colors.Color.White);
		public static WidgetSetup MakeSizedErrorWidget(Margins margins, Dimension? size, Position? position) {
			return new WidgetSetup(
				_margins: margins, _size: size, _position: position,
				foreground: Colors.Color.Red, midtone: Colors.Color.Orange, background: Colors.Color.White
				);
		}

	}

	public static class WidgetSetupUtils {

		public static ISharpGraphicsState ApplySetup(this ISharpGraphicsState graphicsState, WidgetSetup setup) {

			graphicsState.SetDefaultLineWidth(setup.linewidth);
			graphicsState.SetForegroundColor(setup.foregroundColor).SetStrokeColor(setup.foregroundColor);
			graphicsState.SetBackgroundColor(setup.backgroundColor).SetFillColor(setup.backgroundColor);
			graphicsState.SetMidtoneColor(setup.midtoneColor);
			graphicsState.SetTextColor(setup.textColor);

			if (setup.finalFonts?.Regular != null) {
				graphicsState.SetFont(TextFormat.REGULAR, setup.finalFonts.Regular);
			}
			if (setup.finalFonts?.Bold != null) {
				graphicsState.SetFont(TextFormat.BOLD, setup.finalFonts.Bold);
			}
			if (setup.finalFonts?.Italic != null) {
				graphicsState.SetFont(TextFormat.ITALIC, setup.finalFonts.Italic);
			}
			if (setup.finalFonts?.BoldItalic != null) {
				graphicsState.SetFont(TextFormat.BOLDITALIC, setup.finalFonts.BoldItalic);
			}

			return graphicsState;
		}

	}

}