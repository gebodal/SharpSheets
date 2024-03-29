﻿using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using SharpSheets.Parsing;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Fonts;
using SharpSheets.Colors;

namespace SharpSheets.Widgets {

	public readonly struct WidgetSetup {

		public class FontSettingCollection : ISharpArgsGrouping {
			public readonly FontSetting? regular;
			public readonly FontSetting? bold;
			public readonly FontSetting? italic;
			public readonly FontSetting? bolditalic;

			/// <summary>
			/// Constructor for FontCollection.
			/// </summary>
			/// <param name="regular">Standard font to use for text without formatting.</param>
			/// <param name="bold">Font to use for bold text.</param>
			/// <param name="italic">Font to use for italic text.</param>
			/// <param name="bolditalic">Font to use for bold-italic text.</param>
			public FontSettingCollection(FontSetting? regular = null, FontSetting? bold = null, FontSetting? italic = null, FontSetting? bolditalic = null) {
				this.regular = regular;
				this.bold = bold;
				this.italic = italic;
				this.bolditalic = bolditalic;
			}
		}

		public class FontGrouping : ISharpArgSupplemented {

			public FontSetting? Regular => GetSetting(fontPaths?.Regular);
			public FontSetting? Bold => GetSetting(fontPaths?.Bold);
			public FontSetting? Italic => GetSetting(fontPaths?.Italic);
			public FontSetting? BoldItalic => GetSetting(fontPaths?.BoldItalic);

			public readonly FontPathGrouping? fontPaths;
			public readonly FontTags? tags;

			/// <summary>
			/// Constructor for FontGroupingWithSetting.
			/// </summary>
			/// <param name="fonts">Font paths for this grouping.</param>
			/// <param name="tags">Font tags to use for this font grouping. This can include script,
			/// language system, and feature tags (if they are supported by the font
			/// specified).</param>
			public FontGrouping(FontPathGrouping? fonts = null, FontTags? tags = null) {
				this.fontPaths = fonts;
				this.tags = tags;
			}

			private FontSetting? GetSetting(FontPath? path) {
				if (path is null) { return null; }
				else { return new FontSetting(path, tags); }
			}

		}

		public static readonly float defaultLinewidth = 1f;
		public static readonly float defaultGutter = 0f;
		//public static readonly Dimension defaultSize = Dimension.Single;
		//public static readonly string defaultName = "NAME";
		//public static readonly string defaultLocalName = "NAME";
		public static readonly Color defaultForegroundColor = Color.Black;
		public static readonly Color defaultBackgroundColor = Color.White;
		public static readonly Color defaultMidtoneColor = Color.Gray;
		public static readonly Color defaultTextColor = Color.Black;

		public readonly float linewidth;
		public readonly Color foregroundColor;
		public readonly Color backgroundColor;
		public readonly Color midtoneColor;
		public readonly Color textColor;
		private readonly FontGrouping? fonts;
		private readonly FontSettingCollection fontOverrides;
		public readonly FontSettingGrouping finalFonts;
		public readonly float gutter;
		public readonly IDetail? gutterStyle;
		public readonly Dimension? size;
		public readonly Position? position;
		//public readonly bool isInset;
		//public readonly bool isAutoSize;
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
		/// <param name="font_">FontCollection object for overriding specific font styles for
		/// this widget and its children.</param>
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
					FontGrouping? font = null,
					FontSettingCollection? font_ = null,
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

			this.fonts = font;
			this.fontOverrides = font_ ?? new FontSettingCollection();
			this.finalFonts = new FontSettingGrouping(
				fontOverrides.regular ?? fonts?.Regular,
				fontOverrides.bold ?? fonts?.Bold,
				fontOverrides.italic ?? fonts?.Italic,
				fontOverrides.bolditalic ?? fonts?.BoldItalic
				);

			this.gutter = gutter;
			this.gutterStyle = gutter_;

			this.size = (_size == null && _position == null) ? Dimension.Single : _size;
			this.position = _position;

			//this.isInset = this.size == null && this.position != null;
			//this.isAutoSize = this.size.HasValue && this.size?.Relative == -1f && this.size?.Absolute == 0f && this.size?.Percent == 0f;

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

			/*
			if (setup.fonts != null) {
				graphicsState.SetFonts(setup.fonts);
			}
			if (setup.fontOverrides?.regular != null) {
				graphicsState.SetFont(TextFormat.REGULAR, setup.fontOverrides.regular);
			}
			if (setup.fontOverrides?.bold != null) {
				graphicsState.SetFont(TextFormat.BOLD, setup.fontOverrides.bold);
			}
			if (setup.fontOverrides?.italic != null) {
				graphicsState.SetFont(TextFormat.ITALIC, setup.fontOverrides.italic);
			}
			if (setup.fontOverrides?.bolditalic != null) {
				graphicsState.SetFont(TextFormat.BOLDITALIC, setup.fontOverrides.bolditalic);
			}
			*/

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

		/*
		private class WidgetSetupGraphicsState : ISharpGraphicsState {
			public float DefaultLineWidth { get; }
			public float LineWidth { get; }
			public Color StrokeColor { get; }
			public Color FillColor { get; }
			public Color ForegroundColor { get; }
			public Color BackgroundColor { get; }
			public Color MidtoneColor { get; }
			public Color TextColor { get; }
			//public float TextSize { get; }
			//public TextFormat TextFormat { get; }

			public FontPathGrouping Fonts { get; }

			public WidgetSetupGraphicsState(WidgetSetup setup) {
				DefaultLineWidth = setup.linewidth;
				LineWidth = setup.linewidth;
				StrokeColor = setup.foregroundColor;
				FillColor = setup.backgroundColor;
				ForegroundColor = setup.foregroundColor;
				BackgroundColor = setup.backgroundColor;
				MidtoneColor = setup.midtoneColor;
				//TextSize = setup.;
				//TextFormat = setup.;

				Fonts = setup.finalFonts;
			}

			public float GetAscent(string text, TextFormat format, float fontsize) {
				return FontInformation.GetAscent(text, Fonts, format, fontsize);
			}
			public float GetDescent(string text, TextFormat format, float fontsize) {
				return FontInformation.GetDescent(text, Fonts, format, fontsize);
			}
			public float GetWidth(string text, TextFormat format, float fontsize) {
				return FontInformation.GetWidth(text, Fonts, format, fontsize);
			}
		}
		*/

	}

}