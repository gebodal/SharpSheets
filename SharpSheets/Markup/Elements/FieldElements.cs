using SharpSheets.Evaluations;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Markup.Canvas;
using SharpSheets.Parsing;

namespace SharpSheets.Markup.Elements {

	public class TextField : IDrawableElement {
		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly RectangleExpression rect;
		private readonly IExpression<string> name;
		private readonly IExpression<string>? tooltip;
		private readonly EnumExpression<TextFieldType> fieldType;
		private readonly IExpression<string>? value;
		private readonly BoolExpression multiline;
		private readonly BoolExpression rich;
		private readonly EnumExpression<Justification> justification;
		private readonly EnumExpression<FieldRotation> rotation;
		private readonly IntExpression maxLen;

		/// <exception cref="EvaluationException"></exception>
		public TextField(string? _id, StyleSheet styleSheet,
			XLengthExpression _x, YLengthExpression _y, XLengthExpression _width, YLengthExpression _height,
			IExpression<string> _name,
			IExpression<string>? _tooltip,
			EnumExpression<TextFieldType> _field_type,
			IExpression<string>? _value,
			BoolExpression _multiline,
			BoolExpression _rich,
			EnumExpression<Justification> _justification,
			EnumExpression<FieldRotation> _rotation,
			IntExpression _max_len) {

			this.ID = _id;
			this.StyleSheet = styleSheet;

			this.rect = new RectangleExpression(_x, _y, _width, _height);
			this.name = _name;
			this.tooltip = _tooltip;
			this.fieldType = _field_type;
			this.value = _value;
			this.multiline = _multiline;
			this.rich = _rich;
			this.justification = _justification;
			this.rotation = _rotation;
			this.maxLen = _max_len;
		}

		/// <summary>
		/// This element creates a text field in the document at the specified location.
		/// The properties of the text field may be set using the standard graphics
		/// style parameters.
		/// </summary>
		/// <param name="id">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="x">The x-coordinate for the lower-left corner of the text field.</param>
		/// <param name="y">The y-coordinate for the lower-left corner of the text field.</param>
		/// <param name="width">The width for the text field.</param>
		/// <param name="height">The height for the text field.</param>
		/// <param name="name">The name to use for the field in the document.</param>
		/// <param name="tooltip">The tooltip to use for the field in the document.</param>
		/// <param name="field_type">The field type, which constrains the format of strings
		/// which may be entered into the text field.</param>
		/// <param name="value">The default string value for the text field when first
		/// displayed in the document..</param>
		/// <param name="multiline">A flag to indicate that the field should allow
		/// multiple lines of text. If true, the text will be top-aligned, and will allow multiple lines.
		/// If false, all text will be on a single, vertically-centered line.</param>
		/// <param name="rich">A flag to indicate that the field should use rich text features.</param>
		/// <param name="justification">The justification for the text field.</param>
		/// <param name="rotation">The rotation for the content of the text field, relative to the document page.</param>
		/// <param name="max_len">The maximum allowed length for the text field contents, in characters.</param>
		[FactoryBuilder(typeof(TextField), Name = "textField")]
		public static TextField Build(
				[LocalProperty(Default = "null")] string? id, StyleSheet styleSheet,
				[LocalProperty(Default = "0")] XLengthExpression x, [LocalProperty(Default = "0")] YLengthExpression y,
				[LocalProperty(Default = "$width")] XLengthExpression width, [LocalProperty(Default = "$height")] YLengthExpression height,
				[LocalProperty(Default = "NAME")] IExpression<string> name,
				[LocalProperty(Default = "null")] IExpression<string>? tooltip,
				[LocalProperty(Default = "STRING")] EnumExpression<TextFieldType> field_type,
				[LocalProperty(Default = "null")] IExpression<string>? value,
				[LocalProperty(Default = "false")] BoolExpression multiline,
				[LocalProperty(Default = "false")] BoolExpression rich,
				[LocalProperty(Default = "LEFT")] EnumExpression<Justification> justification,
				[LocalProperty(Default = "UP")] EnumExpression<FieldRotation> rotation,
				[LocalProperty(Default = "-1")] IntExpression max_len
			) {

			return new TextField(id, styleSheet, x, y, width, height, name, tooltip, field_type, value, multiline, rich, justification, rotation, max_len);
		}

		public void Draw(MarkupCanvas canvas) {
			if (!StyleSheet.IsEnabled(canvas.Environment)) {
				return;
			}

			// Does this work?
			if (StyleSheet.DrawingCoords != null) {
				canvas.SetDrawingCoords(StyleSheet.DrawingCoords);
			}

			canvas.TextField(
				rect,
				name,
				tooltip,
				fieldType,
				value,
				StyleSheet.FontStyle,
				StyleSheet.FontSize,
				StyleSheet.TextColor,
				multiline,
				rich,
				justification,
				rotation,
				maxLen);

			if (canvas.CollectingDiagnostics) { canvas.RegisterArea(this, rect); }
		}
	}

	public class CheckField : IDrawableElement {
		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly RectangleExpression rect;
		private readonly IExpression<string> name;
		private readonly IExpression<string>? tooltip;
		private readonly EnumExpression<CheckType> checkType;

		/// <exception cref="EvaluationException"></exception>
		public CheckField(string? _id, StyleSheet styleSheet,
			XLengthExpression _x, YLengthExpression _y, XLengthExpression _width, YLengthExpression _height,
			IExpression<string> _name,
			IExpression<string>? _tooltip,
			EnumExpression<CheckType> _check_type) {

			this.ID = _id;
			this.StyleSheet = styleSheet;

			this.rect = new RectangleExpression(_x, _y, _width, _height);
			this.name = _name;
			this.tooltip = _tooltip;
			this.checkType = _check_type;
		}

		/// <summary>
		/// This element creates a check field in the document at the specified location.
		/// The properties of the check field may be set using the standard graphics
		/// style parameters.
		/// </summary>
		/// <param name="id">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="x">The x-coordinate for the lower-left corner of the check field.</param>
		/// <param name="y">The y-coordinate for the lower-left corner of the check field.</param>
		/// <param name="width">The width for the check field.</param>
		/// <param name="height">The height for the check field.</param>
		/// <param name="name">The name to use for the field in the document.</param>
		/// <param name="tooltip">The tooltip to use for the field in the document.</param>
		/// <param name="check_type">The check type to use for the check field, which will
		/// determine the symbol displayed when the field is in the "On" state.</param>
		[FactoryBuilder(typeof(CheckField), Name = "checkField")]
		public static CheckField Build(
				[LocalProperty(Default = "null")] string? id, StyleSheet styleSheet,
				[LocalProperty(Default = "0")] XLengthExpression x, [LocalProperty(Default = "0")] YLengthExpression y,
				[LocalProperty(Default = "v")] XLengthExpression width, [LocalProperty(Default = "$height")] YLengthExpression height,
				[LocalProperty(Default = "NAME")] IExpression<string> name,
				[LocalProperty(Default = "null")] IExpression<string>? tooltip,
				[LocalProperty(Default = "CROSS")] EnumExpression<CheckType> check_type
			) {

			return new CheckField(id, styleSheet, x, y, width, height, name, tooltip, check_type);
		}

		public void Draw(MarkupCanvas canvas) {
			if (!StyleSheet.IsEnabled(canvas.Environment)) {
				return;
			}

			// Does this work?
			if (StyleSheet.DrawingCoords != null) {
				canvas.SetDrawingCoords(StyleSheet.DrawingCoords);
			}

			canvas.CheckField(rect, name, tooltip, checkType, StyleSheet.TextColor);

			if (canvas.CollectingDiagnostics) { canvas.RegisterArea(this, rect); }
		}
	}

	public class ImageField : IDrawableElement {
		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly RectangleExpression rect;
		private readonly IExpression<string> name;
		private readonly IExpression<string>? tooltip;
		// TODO Needs a way to pass a default image

		/// <exception cref="EvaluationException"></exception>
		public ImageField(string? _id, StyleSheet styleSheet,
			XLengthExpression _x, YLengthExpression _y, XLengthExpression _width, YLengthExpression _height,
			IExpression<string> _name,
			IExpression<string>? _tooltip) {

			this.ID = _id;
			this.StyleSheet = styleSheet;

			this.rect = new RectangleExpression(_x, _y, _width, _height);
			this.name = _name;
			this.tooltip = _tooltip;
		}

		/// <summary>
		/// This element creates an image field in the document at the specified location.
		/// The image can be determined later, by the document user.
		/// </summary>
		/// <param name="id">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="x">The x-coordinate for the lower-left corner of the image field.</param>
		/// <param name="y">The y-coordinate for the lower-left corner of the image field.</param>
		/// <param name="width">The width for the image field.</param>
		/// <param name="height">The height for the image field.</param>
		/// <param name="name">The name to use for the field in the document.</param>
		/// <param name="tooltip">The tooltip to use for the field in the document.</param>
		[FactoryBuilder(typeof(ImageField), Name = "imageField")]
		public static ImageField Build(
				[LocalProperty(Default = "null")] string? id, StyleSheet styleSheet,
				[LocalProperty(Default = "0")] XLengthExpression x, [LocalProperty(Default = "0")] YLengthExpression y,
				[LocalProperty(Default = "$width")] XLengthExpression width, [LocalProperty(Default = "$height")] YLengthExpression height,
				[LocalProperty(Default = "NAME")] IExpression<string> name,
				[LocalProperty(Default = "null")] IExpression<string>? tooltip
			) {

			return new ImageField(id, styleSheet, x, y, width, height, name, tooltip);
		}

		public void Draw(MarkupCanvas canvas) {
			if (!StyleSheet.IsEnabled(canvas.Environment)) {
				return;
			}

			// Does this work?
			if (StyleSheet.DrawingCoords != null) {
				canvas.SetDrawingCoords(StyleSheet.DrawingCoords);
			}

			try {
				canvas.ImageField(rect, name, tooltip);
			}
			catch (IOException e) {
				canvas.LogError(this, "Could not create image field.", e);
			}

			if (canvas.CollectingDiagnostics) { canvas.RegisterArea(this, rect); }
		}
	}

}
