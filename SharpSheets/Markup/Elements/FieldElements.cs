using SharpSheets.Evaluations;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Markup.Canvas;

namespace SharpSheets.Markup.Elements {

	/// <summary>
	/// This element creates a text field in the document at the specified location.
	/// The properties of the text field may be set using the standard graphics
	/// style parameters.
	/// </summary>
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
		private readonly IntExpression maxLen;

		/// <summary>
		/// Constructor for TextField.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_x" default="0">The x-coordinate for the lower-left corner of the text field.</param>
		/// <param name="_y" default="0">The y-coordinate for the lower-left corner of the text field.</param>
		/// <param name="_width" default="$width">The width for the text field.</param>
		/// <param name="_height" default="$height">The height for the text field.</param>
		/// <param name="_name" default="NAME">The name to use for the field in the document.</param>
		/// <param name="_tooltip" default="null">The tooltip to use for the field in the document.</param>
		/// <param name="_field_type" default="STRING">The field type, which constrains the format of strings
		/// which may be entered into the text field.</param>
		/// <param name="_value" default="null">The default string value for the text field when first
		/// displayed in the document..</param>
		/// <param name="_multiline" default="false">A flag to indicate that the field should allow
		/// multiple lines of text. If true, the text will be top-aligned, and will allow multiple lines.
		/// If false, all text will be on a single, vertically-centered line.</param>
		/// <param name="_rich" default="false">A flag to indicate that the field should use rich text features.</param>
		/// <param name="_justification" default="LEFT">The justification for the text field.</param>
		/// <param name="_max_len" default="-1">The maximum allowed length for the text field contents, in characters.</param>
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
			IntExpression _max_len) {

			this.ID = _id;
			this.StyleSheet = styleSheet;

			this.rect = new RectangleExpression(_x, _y, _width, _height);
			this.name = _name ?? new StringExpression("NAME");
			this.tooltip = _tooltip;
			this.fieldType = _field_type;
			this.value = _value;
			this.multiline = _multiline;
			this.rich = _rich;
			this.justification = _justification;
			this.maxLen = _max_len;
		}

		public void Draw(MarkupCanvas canvas) {
			if (!StyleSheet.Enabled.Evaluate(canvas.Environment)) {
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
				maxLen);

			if (canvas.CollectingDiagnostics) { canvas.RegisterArea(this, rect); }
		}
	}

	/// <summary>
	/// This element creates a check field in the document at the specified location.
	/// The properties of the check field may be set using the standard graphics
	/// style parameters.
	/// </summary>
	public class CheckField : IDrawableElement {
		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly RectangleExpression rect;
		private readonly IExpression<string> name;
		private readonly IExpression<string>? tooltip;
		private readonly EnumExpression<CheckType> checkType;

		/// <summary>
		/// Constructor for CheckField.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_x" default="0">The x-coordinate for the lower-left corner of the check field.</param>
		/// <param name="_y" default="0">The y-coordinate for the lower-left corner of the check field.</param>
		/// <param name="_width" default="$width">The width for the check field.</param>
		/// <param name="_height" default="$height">The height for the check field.</param>
		/// <param name="_name" default="null">The name to use for the field in the document.</param>
		/// <param name="_tooltip" default="null">The tooltip to use for the field in the document.</param>
		/// <param name="_check_type" default="CROSS">The check type to use for the check field, which will
		/// determine the symbol displayed when the field is in the "On" state.</param>
		/// <exception cref="EvaluationException"></exception>
		public CheckField(string? _id, StyleSheet styleSheet,
			XLengthExpression _x, YLengthExpression _y, XLengthExpression _width, YLengthExpression _height,
			IExpression<string> _name,
			IExpression<string>? _tooltip,
			EnumExpression<CheckType> _check_type) {

			this.ID = _id;
			this.StyleSheet = styleSheet;

			this.rect = new RectangleExpression(_x, _y, _width, _height);
			this.name = _name ?? new StringExpression("NAME");
			this.tooltip = _tooltip;
			this.checkType = _check_type;
		}

		public void Draw(MarkupCanvas canvas) {
			if (!StyleSheet.Enabled.Evaluate(canvas.Environment)) {
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

	/// <summary>
	/// This element creates an image field in the document at the specified location.
	/// The image can be determined later, by the document user.
	/// </summary>
	public class ImageField : IDrawableElement {
		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly RectangleExpression rect;
		private readonly IExpression<string> name;
		private readonly IExpression<string>? tooltip;
		// TODO Needs a way to pass a default image

		/// <summary>
		/// Constructor for ImageField.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_x" default="0">The x-coordinate for the lower-left corner of the image field.</param>
		/// <param name="_y" default="0">The y-coordinate for the lower-left corner of the image field.</param>
		/// <param name="_width" default="$width">The width for the image field.</param>
		/// <param name="_height" default="$height">The height for the image field.</param>
		/// <param name="_name" default="NAME">The name to use for the field in the document.</param>
		/// <param name="_tooltip" default="null">The tooltip to use for the field in the document.</param>
		/// <exception cref="EvaluationException"></exception>
		public ImageField(string? _id, StyleSheet styleSheet,
			XLengthExpression _x, YLengthExpression _y, XLengthExpression _width, YLengthExpression _height,
			IExpression<string> _name,
			IExpression<string>? _tooltip) {

			this.ID = _id;
			this.StyleSheet = styleSheet;

			this.rect = new RectangleExpression(_x, _y, _width, _height);
			this.name = _name ?? new StringExpression("NAME");
			this.tooltip = _tooltip;
		}

		public void Draw(MarkupCanvas canvas) {
			if (!StyleSheet.Enabled.Evaluate(canvas.Environment)) {
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
