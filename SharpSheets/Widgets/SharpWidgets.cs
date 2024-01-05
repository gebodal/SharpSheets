using System.Collections.Generic;
using System;
using System.Linq;
using SharpSheets.Shapes;
using SharpSheets.Colors;
using System.Threading;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using SharpSheets.Parsing;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Exceptions;

namespace SharpSheets.Widgets {

	public abstract class DivisionWidget : SharpWidget {
		public DivisionWidget(WidgetSetup setup) : base(setup) { }

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) { }
		protected override Rectangle GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) { return rect; }
	}

	/// <summary>
	/// The basic division widget, which simply arranges and draws its children, with no additional styling or graphics.
	/// This widget will draw any gutter style specified, and obeys auto-sizing conventions normally.
	/// </summary>
	public class Div : DivisionWidget {
		/// <summary>
		/// Constructor for Div widget.
		/// </summary>
		/// <param name="setup"> Widget setup object. </param>
		public Div(WidgetSetup setup) : base(setup) { }
	}
	/// <summary>
	/// This widget functions identically to the <see cref="Div"/> widget, and is simply included to allow more
	/// clarity in configuration files (to display the intent for a widget in the arrangement).
	/// </summary>
	public class Row : DivisionWidget {
		public Row(WidgetSetup setup) : base(setup) { }
	}
	/// <summary>
	/// This widget functions identically to the <see cref="Div"/> widget, and is simply included to allow more
	/// clarity in configuration files (to display the intent for a widget in the arrangement).
	/// </summary>
	public class Column : DivisionWidget {
		public Column(WidgetSetup setup) : base(setup) { }
	}

	/// <summary>
	/// This widget draws nothing to the page, and also does not draw any children (indeed,
	/// this widget should not have any children).
	/// It is included as a placeholder and null-widget, and to be used where at least
	/// one child is required, but no drawing is desireable.
	/// </summary>
	public sealed class Empty : SharpWidget {
		public Empty(WidgetSetup setup) : base(setup) { }

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) { }
		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) { return null; }
	}

	/// <summary>
	/// This widget is used for drawing a standard document section, with an outline and title which may be specified
	/// by the user. If this widget has no children, it will draw a multiline text field in the remaining area
	/// after its outline and title have been drawn (the details of this field may be adjusted using the widget parameters).
	/// </summary>
	public class Section : SharpWidget {

		public class FieldDetails : ISharpArgsGrouping {
			public readonly string? tooltip;
			public readonly float? lined;
			public readonly float? lineWidth;
			public readonly Justification justification;
			public readonly bool singleline;
			public readonly TextFormat font;
			public readonly float fontsize;
			public readonly bool rich;

			/// <summary>
			/// Constructor for field details.
			/// </summary>
			/// <param name="tooltip">Tooltip string to use for the field, which can be used to provide additional
			/// information in the final document for accesibility and usability purposes.</param>
			/// <param name="lined">If provided, the text field in this widget will not be interactive in the final document,
			/// and will instead be drawn as a lined area, with a line spacing equal to the value given
			/// (measured in points). The lines will be drawn at the default line width, unless the field linewidth parameter
			/// is set.</param>
			/// <param name="linewidth">The line width to use if a <paramref name="lined"/> parameter is specified. If not
			/// specified, the current line width will be used.</param>
			/// <param name="justification">Justification for the field, indicating if the field should be left, right,
			/// or centre justified.</param>
			/// <param name="singleline">Flag to indicate that the field for this widget should be a single line field.</param>
			/// <param name="font">Font format to use for the text field. This will use the appropriate font format from
			/// the current font selection.</param>
			/// <param name="fontsize">Font size for the text field.
			/// A value of 0 or less indicates that the field in the final document should autosize the text.</param>
			/// <param name="rich">Flag to indicate that the text field should have rich text features enabled.</param>
			public FieldDetails(string? tooltip = null, float? lined = null, float? linewidth = null, Justification justification = Justification.LEFT, bool singleline = false, TextFormat font = TextFormat.REGULAR, float fontsize = 0f, bool rich = false) {
				this.tooltip = tooltip;
				this.lined = lined;
				this.lineWidth = linewidth;
				this.justification = justification;
				this.singleline = singleline;
				this.font = font;
				this.fontsize = Math.Max(0f, fontsize);
				this.rich = rich;
			}
		}

		protected readonly IContainerShape outline;
		protected readonly Margins frame;

		protected readonly string fieldName;

		protected readonly FieldDetails fieldDetails;


		/// <summary>
		/// Constructor for Section widget.
		/// </summary>
		/// <param name="setup"> Widget setup object. </param>
		/// <param name="name"> The name for this section, used for titles and field names. </param>
		/// <param name="outline" example="Simple"> Outline style to place around this widget,
		/// which will be drawn before any child widgets are drawn. </param>
		/// <param name="_frame"> Margins to apply to the remaining area after the outline is drawn.
		/// This can be used to separate the children from the outline, if desired. This extra spacing will
		/// be factored into any autosizing calculations.</param>
		/// <param name="field">Field details for this widget.</param>
		public Section(
				WidgetSetup setup,
				string? name = null,
				IContainerShape? outline = null,
				Margins _frame = default,
				FieldDetails? field = null
			) : base(setup) {

			this.outline = outline ?? new NoOutline(-1, trim: Margins.Zero);
			this.frame = _frame;

			fieldName = name ?? "Section";

			this.fieldDetails = field ?? new FieldDetails();
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			outline.Draw(canvas, rect, out Rectangle remainingRect);

			if (remainingRect == null) {
				throw new SharpLayoutException(this, "No space to draw Section contents.");
			}

			remainingRect = remainingRect.Margins(frame, false);

			if (children.Count == 0) {
				if (fieldDetails.lined.HasValue) {
					canvas.SaveState();
					canvas.SetStrokeColor(canvas.GetMidtoneColor()).SetLineWidth(fieldDetails.lineWidth ?? canvas.GetLineWidth());
					int numLines = 2 + (int)Math.Ceiling(remainingRect.Height / fieldDetails.lined.Value);
					Rectangle?[] lines = Divisions.Rows(remainingRect, Dimension.FromPoints(fieldDetails.lined.Value), numLines, 0, out _, true, Arrangement.FRONT, LayoutOrder.FORWARD); // TODO Adjust alignment?
					for (int i = 0; i < lines.Length; i++) {
						if (lines[i] != null) {
							canvas.MoveTo(remainingRect.Left, lines[i]!.Bottom)
								.LineTo(remainingRect.Right, lines[i]!.Bottom);
							canvas.Stroke();
						}
					}
					canvas.RestoreState();
				}
				else {
					canvas.TextField(remainingRect, fieldName, fieldDetails.tooltip, TextFieldType.STRING, "", fieldDetails.font, fieldDetails.fontsize, !fieldDetails.singleline, fieldDetails.rich, fieldDetails.justification);
				}
			}
		}

		protected override Rectangle GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle remainingRect = outline.RemainingRect(graphicsState, rect);
			return remainingRect.Margins(frame, false);
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			Size? contentSize = base.GetMinimumContentSize(graphicsState, outline.RemainingSize(graphicsState, availableSpace).Margins(frame, false));
			Size framedSize = (contentSize ?? new Size(0f, 0f)).Margins(frame, true);
			return outline.FullSize(graphicsState, framedSize);
		}

		protected override Rectangle?[] GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle available) {
			Rectangle remainingRect = outline.RemainingRect(graphicsState, available);
			Rectangle frameRect = remainingRect.Margins(frame, false);
			return new Rectangle?[] { remainingRect, (frameRect != remainingRect ? frameRect : null) };
		}
	}

	/// <summary>
	/// This widget is used for drawing a standard document box containing (by default) a single-line text field,
	/// with an outline and title which may be specified by the user.
	/// If this widget has no children, it will draw a single-line text field in the remaining area
	/// after its outline and title have been drawn (the details of this field may be adjusted using the widget parameters).
	/// </summary>
	public class Box : SharpWidget {

		public class FieldDetails : ISharpArgsGrouping {
			public readonly string? tooltip;
			public readonly Justification justification;
			public readonly CheckType? checkType;
			public readonly TextFormat font;
			public readonly float fontsize;
			public readonly bool rich;

			/// <summary>
			/// Constructor for field details.
			/// </summary>
			/// <param name="tooltip">Tooltip string to use for the field, which can be used to provide additional
			/// information in the final document for accesibility and usability purposes.</param>
			/// <param name="justification">Justification for the field, indicating if the field should be left, right,
			/// or centre justified.</param>
			/// <param name="check">If a check mark type is provided, the field will be a check field, rather than
			/// a text field, with the appropriate check mark used to indicate the "On" state.</param>
			/// <param name="font">Font format to use for the text field. This will use the appropriate font format from
			/// the current font selection.</param>
			/// <param name="fontsize">Font size for the text field.
			/// A value of 0 or less indicates that the field in the final document should autosize the text.</param>
			/// <param name="rich">Flag to indicate that the text field should have rich text features enabled.</param>
			public FieldDetails(string? tooltip = null, Justification justification = Justification.CENTRE, CheckType? check = null, TextFormat font = TextFormat.REGULAR, float fontsize = 0f, bool rich = false) {
				this.tooltip = tooltip;
				this.justification = justification;
				this.checkType = check;
				this.font = font;
				this.fontsize = fontsize;
				this.rich = rich;
			}
		}

		protected readonly string name;
		protected readonly IContainerShape outline;
		protected readonly Margins frame;

		protected readonly FieldDetails fieldDetails;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="setup"> Widget setup object. </param>
		/// <param name="name"> The name for this box, used for titles and field names. </param>
		/// <param name="outline" example="Simple"> Outline style to place around this widget,
		/// which will be drawn before any child widgets are drawn. </param>
		/// <param name="_frame"> Margins to apply to the remaining area after the outline is drawn.
		/// This can be used to separate the children from the outline, if desired. This extra spacing will
		/// be factored into any autosizing calculations.</param>
		/// <param name="field">Field details for this widget.</param>
		public Box(
				WidgetSetup setup,
				string? name = null,
				IContainerShape? outline = null,
				Margins _frame = default,
				FieldDetails? field = null
			) : base(setup) {

			this.name = name ?? "Box";
			this.outline = outline ?? new NoOutline(-1, trim: Margins.Zero);
			this.frame = _frame;

			this.fieldDetails = field ?? new FieldDetails();
		}

		/// <summary></summary>
		/// <exception cref="SharpLayoutException"></exception>
		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			outline.Draw(canvas, rect, out Rectangle remainingRect);

			if (remainingRect == null) {
				throw new SharpLayoutException(this, "No space to draw Box contents.");
			}

			remainingRect = remainingRect.Margins(frame, false);

			if (children.Count == 0) {
				if (fieldDetails.checkType.HasValue) {
					canvas.CheckField(remainingRect, name, fieldDetails.tooltip, fieldDetails.checkType.Value);
				}
				else {
					canvas.TextField(remainingRect, name, fieldDetails.tooltip, TextFieldType.STRING, "", fieldDetails.font, fieldDetails.fontsize, false, fieldDetails.rich, fieldDetails.justification);
				}
			}
		}

		protected override Rectangle GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle remainingRect = outline.RemainingRect(graphicsState, rect);
			return remainingRect.Margins(frame, false);
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			Size? contentSize = base.GetMinimumContentSize(graphicsState, (Size)outline.RemainingRect(graphicsState, (Rectangle)availableSpace).Margins(frame, false));
			Size framedSize = (contentSize ?? new Size(0f, 0f)).Margins(frame, true);
			return (Size)outline.FullRect(graphicsState, (Rectangle)framedSize);
		}

		protected override Rectangle?[] GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle available) {
			Rectangle remainingRect = outline.RemainingRect(graphicsState, available);
			Rectangle frameRect = remainingRect.Margins(frame, false);
			return new Rectangle?[] { remainingRect, (frameRect != remainingRect ? frameRect : null) };
		}
	}

	/// <summary>
	/// This widget can be used to draw any LabelledBox shape with a text label in the label area of that shape.
	/// The label can be positioned within the label area, and it's parameters adjusted. Alternatively, a "content"
	/// child may be used to replace the text label with some other specified widget content.
	/// If the widget has no children, then a text field will be placed in the labelled box remaining area.
	/// If the widget does have children, then these will be drawn in the remaining area instead.
	/// If the labelled box style supports calculation of the full area from a content size, then this widget
	/// will support auto-sizing based on any child widgets.
	/// </summary>
	public class Labelled : SharpWidget {

		public class LabelParams : ISharpArgsGrouping {
			public readonly float fontSize;
			public readonly TextFormat format;
			public readonly Justification justification;
			public readonly Alignment alignment;
			public readonly TextHeightStrategy heightStrategy;
			public readonly (float x, float y) offset;
			/// <summary>
			/// Constructor for label parameters.
			/// </summary>
			/// <param name="fontSize">Font size to use for the label text.</param>
			/// <param name="format">Font format to use for the label text. This will use the appropriate font
			/// format from the current font selection.</param>
			/// <param name="justification">The horizontal justification to use for the label text, relative to the label area.</param>
			/// <param name="alignment">The vertical alignment to use for the label text, relative to the label area.</param>
			/// <param name="heightStrategy">The height calculation strategy to use when arranging the label text within the label area.</param>
			/// <param name="offset">An offset for the label text, after positioning using <paramref name="justification"/> and
			/// <paramref name="alignment"/> This is provided as an x,y pair of numbers, measured in points. The positive
			/// directions are rightwards and upwards. This can be used to make specific adjustments, to accomodate quirks of
			/// specific fonts.</param>
			public LabelParams(float fontSize = 6f, TextFormat format = TextFormat.REGULAR, Justification justification = Justification.CENTRE, Alignment alignment = Alignment.CENTRE, TextHeightStrategy heightStrategy = TextHeightStrategy.AscentBaseline, (float x, float y) offset = default) {
				this.fontSize = fontSize;
				this.format = format;
				this.justification = justification;
				this.alignment = alignment;
				this.heightStrategy = heightStrategy;
				this.offset = offset;
			}
		}

		public class FieldDetails : ISharpArgsGrouping {
			public readonly string? tooltip;
			public readonly Justification justification;
			public readonly CheckType? checkType;
			public readonly TextFormat font;
			public readonly float fontsize;
			public readonly bool multiline;
			public readonly bool rich;

			/// <summary>
			/// Constructor for field details.
			/// </summary>
			/// <param name="tooltip">Tooltip string to use for the field, which can be used to provide additional
			/// information in the final document for accesibility and usability purposes.</param>
			/// <param name="justification">Justification for the field, indicating if the field should be left, right,
			/// or centre justified.</param>
			/// <param name="check">If a check mark type is provided, the field will be a check field, rather than
			/// a text field, with the appropriate check mark used to indicate the "On" state.</param>
			/// <param name="font">Font format to use for the text field. This will use the appropriate font format from
			/// the current font selection.</param>
			/// <param name="fontsize">Font size for the text field.
			/// A value of 0 or less indicates that the field in the final document should autosize the text.</param>
			/// <param name="multiline">Flag to indicate that the field for this widget should be a multiline field.</param>
			/// <param name="rich">Flag to indicate that the text field should have rich text features enabled.</param>
			public FieldDetails(string? tooltip = null, Justification justification = Justification.CENTRE, CheckType? check = null, TextFormat font = TextFormat.REGULAR, float fontsize = 0f, bool multiline = false, bool rich = false) {
				this.tooltip = tooltip;
				this.justification = justification;
				this.checkType = check;
				this.font = font;
				this.fontsize = fontsize;
				this.multiline = multiline;
				this.rich = rich;
			}
		}

		protected readonly ILabelledBox outline;
		protected readonly Margins frame;

		protected readonly LabelParams labelParams;
		protected readonly string label;
		protected readonly string fieldName;

		protected readonly FieldDetails fieldDetails;

		protected readonly Div? content;

		/// <summary>
		/// Constructor for Labelled widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="outline">LabelledBox style to draw for this widget.
		/// This shape will be used to calculate the remaining and label areas.
		/// It will also be drawn before any content or label is drawn.</param>
		/// <param name="_frame">Margins to apply to the remaining area after the labelled box is drawn.
		/// This can be used to separate the field, or any children, from the outline if desired.
		/// This extra spacing will be factored into any autosizing calculations.</param>
		/// <param name="label">The text label to be drawn in the label area of the
		/// labelled box. This positioning and style of this label can be adjusted using
		/// the other settings.</param>
		/// <param name="label__">Label details for this widget.</param>
		/// <param name="field">Field details for this widget.</param>
		/// <param name="content">If provided, this child content will be drawn in place
		/// of the label text in the label area of the labelled box.</param>
		public Labelled(
				WidgetSetup setup,
				ILabelledBox? outline = null,
				Margins _frame = default,
				string? label = null, // TODO RichString?
				LabelParams? label__ = null,
				FieldDetails? field = null,
				ChildHolder? content = null
			) : base(setup) {

			this.outline = outline ?? new SimpleLabelledBox(-1);
			this.frame = _frame;

			this.labelParams = label__ ?? new LabelParams();
			this.label = label ?? "LABEL";
			this.fieldName = label ?? this.GetType().Name;

			this.fieldDetails = field ?? new FieldDetails();

			this.content = content?.Child;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			outline.Draw(canvas, rect, out Rectangle labelRect, out Rectangle remainingRect);

			remainingRect = remainingRect.Margins(frame, false);

			if (content != null) {
				content.Draw(canvas, labelRect, default);
			}
			else {
				canvas.SaveState();
				canvas.SetTextFormatAndSize(labelParams.format, labelParams.fontSize).SetFillColor(canvas.GetForegroundColor());
				canvas.DrawText(labelRect, label, labelParams.justification, labelParams.alignment, labelParams.heightStrategy, (labelParams.offset.x, labelParams.offset.y));
				canvas.RestoreState();
			}

			if (children.Count == 0) {
				if (fieldDetails.checkType.HasValue) {
					canvas.CheckField(remainingRect, fieldName, fieldDetails.tooltip, fieldDetails.checkType.Value);
				}
				else {
					canvas.TextField(remainingRect, fieldName, fieldDetails.tooltip, TextFieldType.STRING, "", fieldDetails.font, fieldDetails.fontsize, fieldDetails.multiline, fieldDetails.rich, fieldDetails.justification);
				}
			}
		}

		protected override Rectangle GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return outline.RemainingRect(graphicsState, rect).Margins(frame, false);
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			Size? contentSize = base.GetMinimumContentSize(graphicsState, outline.RemainingSize(graphicsState, availableSpace).Margins(frame, false));
			Size framedSize = (contentSize ?? new Size(0f, 0f)).Margins(frame, true);
			return outline.FullSize(graphicsState, framedSize);
		}
	}

	/// <summary>
	/// This widget is used to draw known text to the page, which may be dynamically resized to fit the available
	/// space. The text can be positioned inside the available area, and formatted using either rich text, or with
	/// the "format" parameter. The size of this widget can be dynamically inferred from either "fontsize" or "minfontsize".
	/// </summary>
	public class Text : SharpWidget {

		public class ParagraphDataArgs : ISharpArgsGrouping {
			public readonly float Spacing;
			public readonly ParagraphIndent Indent;

			/// <summary>
			/// Constructor for ParagraphDataArgs.
			/// </summary>
			/// <param name="spacing">The spacing to be used between paragraphs of text, measured in points.
			/// This spacing is in addition to any line spacing.</param>
			/// <param name="indent">The indentation length for the first line of text in a paragraph,
			/// measured in points.</param>
			/// <param name="hanging">The indentation length for each line after the first (whose indentation
			/// is controlled using <paramref name="indent"/>), measured in points.</param>
			public ParagraphDataArgs(float spacing = 0f, float indent = 0f, float hanging = 0f) {
				this.Spacing = spacing;
				this.Indent = new ParagraphIndent(indent, hanging);
			}
		}

		protected readonly RichString text;

		protected readonly float fontSize;
		protected readonly ParagraphSpecification paragraphSpec;

		protected readonly float minfontsize;
		protected readonly float maxfontsize;
		protected readonly float epsilon;

		protected readonly Justification justification;
		protected readonly Alignment alignment;
		protected readonly TextHeightStrategy heightStrategy;

		protected readonly (float x, float y) offset;

		protected readonly bool fit;
		protected readonly bool singleline;

		/// <summary>
		/// Constructor for Text widget.
		/// </summary>
		/// <param name="setup"></param>
		/// <param name="text" example="Lorem ipsum dolor sit amet\, consectetur adipiscing elit\, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.">
		/// The text to be displayed in this widget, which can be formatted as rich text.
		/// The provided entries will be treated as separate lines of text.
		/// </param>
		/// <param name="fontSize">The fontsize at which to draw the provided text.
		/// A fontsize of 0 indicates that the fontsize should be adjusted such that
		/// the text fit the available area (this can also be achieved by setting the <paramref name="fit"/> flag).
		/// This parameter is ignored if <paramref name="fit"/> is true.</param>
		/// <param name="format">This parameter can be used to set the default format
		/// for the text, to be used in conjuction with any rich text formatting. For example, if this parameter
		/// is set to <see cref="TextFormat.BOLD"/>, then the text "Testing _Testing_" would be interpreted as "*Testing _Testing_*" </param>
		/// <param name="lineSpacing">This parameter sets the line spacing, which is the distance between successive
		/// text baselines, measured in multiples of the current fontsize.</param>
		/// <param name="paragraph">Paragraph data for this widget.</param>
		/// <param name="minfontsize" example="1">This is the minimum fontsize to be used when fitting the text
		/// to the available area. It must have a value greater than zero. This parameter is ignored when <paramref name="fit"/>
		/// is false and <paramref name="fontSize"/> is greater than zero.</param>
		/// <param name="maxfontsize" example="400">This is the maximum fontsize to be used when fitting the
		/// text to the available area. It must have a value greater than zero. If not provided, the maximum fontsize will
		/// be the maximum of <paramref name="fontSize"/> and <paramref name="minfontsize"/>. This parameter is ignored when
		/// <paramref name="fit"/> is false and <paramref name="fontSize"/> is greater than zero.</param>
		/// <param name="epsilon">This is the smallest change in fontsize to be considered when fitting the text size
		/// to the available area. This parameter is ignored when <paramref name="fit"/> is false and <paramref name="fontSize"/>
		/// is greater than zero.</param>
		/// <param name="justification">The horizontal justification to use for the text within the available area.</param>
		/// <param name="alignment">The vertical alignment to use for the text within the available area.</param>
		/// <param name="heightStrategy">The height calculation strategy to use when arranging the text within the available area.</param>
		/// <param name="fit" example="true">Flag to indicate that the font size should be adjusted to fit the text to the
		/// available area.</param>
		/// <param name="offset">An offset for the text, after positioning using <paramref name="justification"/> and
		/// <paramref name="alignment"/>. This is provided as an x,y pair of numbers, measured in points. The positive directions
		/// are rightwards and upwards. This can be used to make specific adjustments, to accomodate quirks of specific fonts.</param>
		/// <param name="singleline">Flag to indicate that the text should be written on a single line. No line breaks will
		/// be added, regardless of the fontsize.</param>
		/// <size>50 50</size>
		public Text(
				WidgetSetup setup,
				List<RichString>? text = null,
				float fontSize = -1f,
				TextFormat? format = null,
				float lineSpacing = 1.35f,
				ParagraphDataArgs? paragraph = null,
				float minfontsize = 1f,
				float? maxfontsize = null,
				float epsilon = 0.25f,
				Justification justification = Justification.LEFT,
				Alignment alignment = Alignment.TOP,
				TextHeightStrategy heightStrategy = TextHeightStrategy.FontsizeBaseline,
				bool fit = false,
				(float x, float y) offset = default,
				bool singleline = false
			) : base(setup) {

			this.text = RichString.Join("\n", TextFormat.REGULAR, text ?? Enumerable.Empty<RichString>());

			if (format.HasValue) {
				this.text = this.text.ApplyFormat(format.Value);
			}

			this.fontSize = fontSize;

			paragraph ??= new ParagraphDataArgs();
			this.paragraphSpec = new ParagraphSpecification(lineSpacing, paragraph.Spacing, paragraph.Indent);

			this.minfontsize = minfontsize;
			this.maxfontsize = maxfontsize ?? Math.Max(this.fontSize, this.minfontsize);
			this.epsilon = epsilon;
			if(this.epsilon <= 0) {
				throw new ArgumentException("Value for epsilon must be greater than zero.");
			}

			this.justification = justification;
			this.alignment = alignment;
			this.heightStrategy = heightStrategy;

			this.offset = offset;

			this.fit = fit;
			this.singleline = singleline;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			rect = new Rectangle(rect.X + offset.x, rect.Y + offset.y, rect.Width, rect.Height);

			if (fit || fontSize <= 0) {
				FontSizeSearchParams searchParams = new FontSizeSearchParams(minfontsize, maxfontsize, epsilon);
				if (singleline) {
					canvas.FitRichTextLine(rect, text, searchParams, justification, alignment, heightStrategy);
				}
				else {
					canvas.FitRichText(rect, text, paragraphSpec, new FontSizeSearchParams(minfontsize, maxfontsize, epsilon), justification, alignment, heightStrategy, true);
				}
			}
			else {
				canvas.DrawRichText(rect, text, fontSize, paragraphSpec, justification, alignment, heightStrategy, true);
			}
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			float minimumFontSize = (fontSize <= 0f || fit) ? minfontsize : fontSize;

			RichString[][] singleLineParagraphs = text.Split('\n').Select(s => new RichString[] { s }).ToArray();

			float width = Math.Min(availableSpace.Width, RichStringLayout.CalculateWidth(graphicsState, singleLineParagraphs, fontSize, paragraphSpec, true));
			float height = RichStringLayout.CalculateHeight(graphicsState, text, availableSpace.Width, fontSize, paragraphSpec, heightStrategy, true);

			return new Size(width, height);
		}
	}

	// Unified bars should have a FieldDetails argument, so that the fields can be adjusted

	public class BarNameDetails : ISharpArgsGrouping {
		public readonly float fontSize;
		public readonly (float x, float y) offset;
		public readonly TextFormat format;
		public readonly Justification justification;
		public readonly Alignment alignment;
		public readonly TextHeightStrategy heightStrategy;
		public readonly Color? color;

		/// <summary>
		/// Constructor for BarNameDetails.
		/// </summary>
		/// <param name="fontSize">The fontsize for the bar names, measured in points.</param>
		/// <param name="offset">An offset for the name text, after positioning using <paramref name="justification"/> and
		/// <paramref name="alignment"/>. This is provided as an x,y pair of numbers, measured in points. The positive directions
		/// are rightwards and upwards. This can be used to make specific adjustments, to accomodate quirks of specific fonts.</param>
		/// <param name="format">Font format to use for the name text. This will use the appropriate font
		/// format from the current font selection.</param>
		/// <param name="justification">The horizontal justification to use for the name text, relative to the bar label area.</param>
		/// <param name="alignment">The vertical alignment to use for the name text, relative to the bar label area.</param>
		/// <param name="heightStrategy">The height calculation strategy to use when arranging the label text within the label area.</param>
		/// <param name="color">The color to use for the bar name text. The default is the current text color.</param>
		public BarNameDetails(float fontSize = 6f, (float x, float y) offset = default, TextFormat format = TextFormat.REGULAR, Justification justification = Justification.CENTRE, Alignment alignment = Alignment.CENTRE, TextHeightStrategy heightStrategy = TextHeightStrategy.AscentBaseline, Color? color = null) {
			this.fontSize = fontSize;
			this.offset = offset;
			this.format = format;
			this.justification = justification;
			this.alignment = alignment;
			this.heightStrategy = heightStrategy;
			this.color = color;
		}
	}

	/// <summary>
	/// This widget draws one or more bars in the document, arranged vertically. The spacing
	/// and size of these bars can be specified, and if an absolute value is given for the bar
	/// height, the size of the widget can be automatically calculated. It is also possible to
	/// specify content for the label and entry areas of the bar in the form of named children
	/// of this widget, however these children are not included in autosizing calculations.
	/// Additionally, annotations may be placed on the label and entry areas of the bars as
	/// labels and notes, whose parameters may be specified. By default, if no named children
	/// are provided, the bar name is written as text in the bar label area, and a field (either
	/// a text field or check field, depending on whether the check mark parameters have been
	/// specified) is placed in the bar remaining area.
	/// </summary>
	public class Bars : SharpWidget {

		protected readonly IBar[] bars;
		protected readonly string[] barNames;
		protected readonly string[]? barTooltips;
		protected readonly Dimension[] barHeights;

		protected readonly BarNameDetails nameDetails;

		protected readonly bool rich;

		protected readonly CheckType[] checkTypes;
		protected readonly bool checkMarks;

		protected readonly Div? content;
		protected readonly Div? entry;

		/// <summary>
		/// Constructor for Bars widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="bar">Bar style to draw for this widget.
		/// This shape will be used to calculate the label and entry areas.
		/// It will also be drawn before any label or entry is drawn.</param>
		/// <param name="_name" example="Bar 1,Bar 2">The names to use for the bars.
		/// The number of names will determine the number of bars drawn in the document.
		/// These names will be used in the bar labels (unless a <paramref name="content"/> child is given)
		/// and for field names.</param>
		/// <param name="_tooltip">Tooltip strings to use for the bar fields. If the number
		/// of tooltip strings does not match the number of bars, some tooltips will be ignored,
		/// or some fields will have no tooltip, as appropriate.</param>
		/// <param name="height">The height that each bar is to be drawn at. This is used
		/// in auto-sizing calculations. The default is 1 relative unit.</param>
		/// <param name="label" example="Label">A label to be drawn by the bar entry area.
		/// By default this will only be drawn for the first bar, but will be drawn by each
		/// bar if <paramref name="allLabelled"/> is specified.</param>
		/// <param name="note" example="Note">A note to be drawn by the bar label area.
		/// By default this will only be drawn for the first bar, but will be drawn by each
		/// bar if <paramref name="allLabelled"/> is specified.</param>
		/// <param name="allLabelled">Flag to indicate that the note and label should be drawn
		/// for each bar, not just the first one.</param>
		/// <param name="name_">Name details data for this widget.</param>
		/// <param name="label_">Label details data for this widget.</param>
		/// <param name="note_">Note details data for this widget.</param>
		/// <param name="checkMarks">A flag to indicate that the bar entry fields should be
		/// check fields, rather than text fields. This flag is unnecessary if a value is provided
		/// for <paramref name="check"/>.</param>
		/// <param name="check">The check mark to use for the bar fields (the fields will be check
		/// fields if a value is given for this parameter). Check marks can be specified for each
		/// bar separately. If the number of bars does not match the number of checkmarks, then the
		/// last checkmark will be used for any remaining bars, or remaining checkmarks will be ignored,
		/// as appropriate.</param>
		/// <param name="rich">Flag to indicate that any text field should have rich text features enabled.</param>
		/// <param name="content">If provided, this child will be drawn in place
		/// of the name text in the label area of the bar.</param>
		/// <param name="entry">If provided, this child will be drawn in place
		/// of the field in the remaining area of the bar.</param>
		/// <size>100 50</size>
		public Bars(
				WidgetSetup setup,
				IBar? bar = null,
				string[]? _name = null, // TODO RichString?
				string[]? _tooltip = null,
				Dimension? height = default,
				string? label = null, // TODO RichString?
				string? note = null, // TODO RichString?
				bool allLabelled = false,
				BarNameDetails? name_ = null,
				LabelDetails? label_ = null,
				LabelDetails? note_ = null,
				bool checkMarks = false,
				CheckType[]? check = null,
				bool rich = false,
				ChildHolder? content = null,
				ChildHolder? entry = null
			) : base(setup) {

			barNames = (_name != null && _name.Length > 0) ? _name : new string[] { "NAME" };
			barTooltips = (_tooltip != null && _tooltip.Length > 0) ? _tooltip : null;

			Dimension barHeight = height ?? Dimension.Single;

			barHeights = ArrayUtils.MakeArray(barNames.Length, barHeight);

			bar ??= new SimpleBar(-1);
			if (label != null || note != null) {
				LabelledBar labelledBar = new LabelledBar(bar, label, label_, note, note_);
				if (allLabelled) {
					bars = ArrayUtils.MakeArray(barNames.Length, labelledBar);
					for (int i = 0; i < barHeights.Length; i++) {
						barHeights[i] += Dimension.FromPoints(labelledBar.LabelHeight);
					}
				}
				else {
					bars = ArrayUtils.MakeArray(barNames.Length, bar);
					bars[0] = labelledBar;
					barHeights[0] += Dimension.FromPoints(labelledBar.LabelHeight);
				}
			}
			else {
				bars = ArrayUtils.MakeArray(barNames.Length, bar);
			}

			this.nameDetails = name_ ?? new BarNameDetails();

			this.rich = rich;

			this.checkMarks = checkMarks || check != null;
			this.checkTypes = (check == null || check.Length == 0) ? new CheckType[] { CheckType.CROSS } : check;

			if (checkTypes.Length != bars.Length) {
				CheckType[] checkMarkTypes = new CheckType[bars.Length];
				for (int i = 0; i < bars.Length; i++) {
					checkMarkTypes[i] = this.checkTypes[Math.Min(i, this.checkTypes.Length - 1)];
				}
				this.checkTypes = checkMarkTypes;
			}

			this.content = content?.Child;
			this.entry = entry?.Child;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			
			Rectangle?[] barRects = Divisions.Rows(rect, barHeights, Gutter, false, Arrangement, LayoutOrder.FORWARD, DivisionStrategy.RELATIVE_RECTANGLES);

			for (int i = 0; i < barRects.Length; i++) {
				if (barRects[i] is Rectangle barRect) {
					bars[i].Draw(canvas, barRect, out Rectangle labelRect, out Rectangle entryRect);

					if (content != null) {
						content.Draw(canvas, labelRect, cancellationToken);
					}
					else {
						canvas.SaveState();

						canvas.SetTextFormatAndSize(nameDetails.format, nameDetails.fontSize);
						if (nameDetails.color.HasValue) {
							canvas.SetTextColor(nameDetails.color.Value);
						}
						canvas.SetFillColor(canvas.GetTextColor());

						canvas.DrawText(labelRect, barNames[i], nameDetails.justification, nameDetails.alignment, nameDetails.heightStrategy, nameDetails.offset);

						canvas.RestoreState();
					}

					if (entry != null) {
						entry.Draw(canvas, entryRect, cancellationToken);
					}
					else {
						string? fieldTooltip = (barTooltips != null && i < barTooltips.Length && !string.IsNullOrWhiteSpace(barTooltips[i])) ? barTooltips[i] : null;
						if (checkMarks) {
							canvas.CheckField(entryRect, barNames[i], fieldTooltip, checkTypes[i]);
						}
						else {
							canvas.TextField(entryRect, barNames[i], fieldTooltip, TextFieldType.STRING, "", TextFormat.REGULAR, 0f, false, rich, Justification.CENTRE);
						}
					}
				}
				else {
					canvas.LogError(this, $"No space for \"{barNames[i]}\" bar.");
				}
			}
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			// Can we take account of content and entry sizes here?
			return new Size(0f, Divisions.CalculateTotalLength(barHeights.Select(d => d.Absolute).ToArray(), Gutter));
		}

		protected override Rectangle[] GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle?[] barRects = Divisions.Rows(rect, barHeights, Gutter, false, Arrangement.FRONT, LayoutOrder.FORWARD, DivisionStrategy.RELATIVE_RECTANGLES);
			Rectangle[] labelRects = barRects.Zip(bars, (r, b) => r is not null ? b.LabelRect(graphicsState, r) : null).WhereNotNull().ToArray();
			Rectangle[] remainingRects = barRects.Zip(bars, (r, b) => r is not null ? b.RemainingRect(graphicsState, r) : null).WhereNotNull().ToArray();
			return labelRects.Concat(remainingRects).ToArray();
		}

	}

	/// <summary>
	/// This widget draws one or more usage bars in the document, arranged vertically. The spacing
	/// and size of these bars can be specified, and if an absolute value is given for the bar
	/// height, the size of the widget can be automatically calculated. It is also possible to
	/// specify content for the label and entry areas of the usage bar in the form of named children
	/// of this widget, however these children are not included in autosizing calculations.
	/// Additionally, annotations may be placed on the label and entry areas of the bars as
	/// labels and notes, whose parameters may be specified. By default, if no named children
	/// are provided, the bar name is written as text in the bar label area, and text fields
	/// are placed in the bar entry areas.
	/// </summary>
	public class SlotsBars : SharpWidget {

		protected readonly IUsageBar[] bars;
		protected readonly string[] barNames;
		protected readonly Dimension[] barHeights;

		protected readonly BarNameDetails nameDetails;

		protected readonly bool rich;

		protected readonly Div? content;
		protected readonly Div? entry1;
		protected readonly Div? entry2;

		/// <summary>
		/// Constructor for SlotsBars widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="bar">UsageBar style to draw for this widget.
		/// This shape will be used to calculate the label and entry areas.
		/// It will also be drawn before any label or entry is drawn.</param>
		/// <param name="_name" example="Bar 1,Bar 2">The names to use for the bars.
		/// The number of names will determine the number of bars drawn in the document.
		/// These names will be used in the bar labels (unless a <paramref name="content"/> child is given)
		/// and for field names.</param>
		/// <param name="height">The height that each bar is to be drawn at. This is used
		/// in auto-sizing calculations. The default is 1 relative unit.</param>
		/// <param name="labels" example="Label 1, Label 2">A pair of labels to be drawn by the bar entry areas.
		/// By default these will only be drawn for the first bar, but will be drawn by each
		/// bar if <paramref name="allLabelled"/> is specified.</param>
		/// <param name="note" example="Note">A note to be drawn by the bar label area.
		/// By default this will only be drawn for the first bar, but will be drawn by each
		/// bar if <paramref name="allLabelled"/> is specified.</param>
		/// <param name="allLabelled">Flag to indicate that the note and labels should be drawn
		/// for each bar, not just the first one.</param>
		/// <param name="name_">Name details data for this widget.</param>
		/// <param name="labels_">Label details data for this widget.</param>
		/// <param name="note_">Note details data for this widget.</param>
		/// <param name="rich">Flag to indicate that any text fields should have rich text features enabled.</param>
		/// <param name="content">If provided, this child will be drawn in place
		/// of the name text in the label area of the bar.</param>
		/// <param name="entry1">If provided, this child will be drawn in place
		/// of the field in the first entry area of the bar.</param>
		/// <param name="entry2">If provided, this child will be drawn in place
		/// of the field in the second entry area of the bar.</param>
		/// /// <size>100 50</size>
		public SlotsBars(
				WidgetSetup setup,
				IUsageBar? bar = null,
				string[]? _name = null,
				Dimension? height = default,
				(string label1, string label2)? labels = null, // TODO RichString?
				string? note = null, // TODO RichString?
				bool allLabelled = false,
				BarNameDetails? name_ = null,
				LabelDetails? labels_ = null,
				LabelDetails? note_ = null,
				bool rich = false,
				ChildHolder? content = null,
				ChildHolder? entry1 = null,
				ChildHolder? entry2 = null
			) : base(setup) {

			barNames = (_name != null && _name.Length > 0) ? _name : new string[] { "NAME" };

			Dimension barHeight = height ?? Dimension.Single;

			barHeights = ArrayUtils.MakeArray(barNames.Length, barHeight);

			bar ??= new SimpleUsageBar(-1);
			if (labels != null || note != null) {
				string? label1 = !string.IsNullOrWhiteSpace(labels?.label1) ? labels.Value.label1 : null;
				string? label2 = !string.IsNullOrWhiteSpace(labels?.label2) ? labels.Value.label2 : null;

				LabelledUsageBar labelledBar = new LabelledUsageBar(bar, label1, label2, labels_, note, note_);
				if (allLabelled) {
					bars = ArrayUtils.MakeArray(barNames.Length, labelledBar);
					for (int i = 0; i < barHeights.Length; i++) {
						barHeights[i] += Dimension.FromPoints(labelledBar.LabelHeight);
					}
				}
				else {
					bars = ArrayUtils.MakeArray(barNames.Length, labelledBar.bar);
					bars[0] = labelledBar;
					barHeights[0] += Dimension.FromPoints(labelledBar.LabelHeight);
				}
			}
			else {
				bars = ArrayUtils.MakeArray(barNames.Length, bar);
			}

			this.nameDetails = name_ ?? new BarNameDetails();

			this.rich = rich;

			this.content = content?.Child;
			this.entry1 = entry1?.Child;
			this.entry2 = entry2?.Child;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			Rectangle?[] barRects = Divisions.Rows(rect, barHeights, Gutter, false, Arrangement, LayoutOrder.FORWARD, DivisionStrategy.RELATIVE_RECTANGLES);
			
			for (int i = 0; i < barRects.Length; i++) {
				if (barRects[i] is Rectangle barRect) {
					bars[i].Draw(canvas, barRect, out Rectangle labelRect, out Rectangle entry1Rect, out Rectangle entry2Rect);

					if (content != null) {
						content.Draw(canvas, labelRect, cancellationToken);
					}
					else {
						canvas.SaveState();
						canvas.SetTextFormatAndSize(nameDetails.format, nameDetails.fontSize);
						if (nameDetails.color.HasValue) {
							canvas.SetTextColor(nameDetails.color.Value);
						}
						canvas.SetFillColor(canvas.GetTextColor());

						canvas.DrawText(labelRect, barNames[i], nameDetails.justification, nameDetails.alignment, nameDetails.heightStrategy, nameDetails.offset);

						canvas.RestoreState();
					}

					if(entry1 != null) {
						entry1.Draw(canvas, entry1Rect, cancellationToken);
					}
					else {
						canvas.TextField(entry1Rect, $"{barNames[i]}_entry1", null, TextFieldType.STRING, "", TextFormat.REGULAR, 0f, false, rich, Justification.CENTRE); // TODO Tooltip?
					}

					if (entry2 != null) {
						entry2.Draw(canvas, entry2Rect, cancellationToken);
					}
					else {
						canvas.TextField(entry2Rect, $"{barNames[i]}_entry2", null, TextFieldType.STRING, "", TextFormat.REGULAR, 0f, false, rich, Justification.CENTRE); // TODO Tooltip?
					}
				}
				else {
					canvas.LogError(this, $"No space for \"{barNames[i]}\" bar.");
				}
			}
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			return new Size(0f, Divisions.CalculateTotalLength(barHeights.Select(d => d.Absolute).ToArray(), Gutter));
		}

		protected override Rectangle[] GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle?[] barRects = Divisions.Rows(rect, barHeights, Gutter, false, Arrangement.FRONT, LayoutOrder.FORWARD, DivisionStrategy.RELATIVE_RECTANGLES);
			Rectangle[] labelRects = barRects.Zip(bars, (r, b) => r is not null ? b.EntryRect(graphicsState, 0, r) : null).WhereNotNull().ToArray();
			Rectangle[] remainingRects = barRects.Zip(bars, (r, b) => r is not null ? b.EntryRect(graphicsState, 1, r) : null).WhereNotNull().ToArray();
			return labelRects.Concat(remainingRects).ToArray();
		}
	}

	/// <summary>
	/// This widget can be used to create a checklist, a vertically arranged list of text entries
	/// with a check field for each entry. The outline, style, and position for the check fields can be adjusted,
	/// and the format and positioning of the text can be specified. The check marks will be vertically positioned 
	/// in the centre of each row, and the text can be positioned relative to the row area.
	/// </summary>
	public class CheckList : SharpWidget {

		// TODO Would be good if this widget could accomodate multi-line text entries, with an appropriately placed check field

		/// <summary>
		/// Indicates the position of a check field relative to a text entry.
		/// </summary>
		public enum CheckPosition {
			/// <summary>
			/// Indicates that the check mark should be to the left of the text entry.
			/// </summary>
			LEFT,
			/// <summary>
			/// Indicates that the check mark should be to the right of the text entry.
			/// </summary>
			RIGHT
		}

		protected readonly RichString[] list;
		protected readonly string? name;
		protected readonly Dimension height;
		protected readonly float fontsize;
		protected readonly float textOffset;
		protected readonly float separation;
		protected readonly float spacing;

		protected readonly Justification justification;
		protected readonly Alignment alignment;
		protected readonly TextHeightStrategy heightStrategy;

		protected readonly float? checkSize;
		protected readonly IBox checkBoxStyle;
		protected readonly LayoutOrder checkRectOrder;
		protected readonly CheckType checkType;
		protected readonly Color? checkColor;


		/// <summary>
		/// Constructor for CheckList widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="list" example="Item 1, Item 2, Item 3">The list of text entries to be included.
		/// The number of entries dictates the number of lines, and number of check fields.
		/// Each entry will be drawn as a single line of text.</param>
		/// <param name="name">A name to prepend to the check fields.</param>
		/// <param name="height" example="10pt">The height of each row. The default is 1 relative unit.</param>
		/// <param name="fontsize" example="8">The fontsize for the text entries, measured in points.</param>
		/// <param name="textOffset">A vertical offset for the text entries, after positioning using <paramref name="alignment"/>
		/// and <paramref name="heightStrategy"/>. This can be useful for fine-tuning positioning, and to account for the specific
		/// of certain fonts.</param>
		/// <param name="separation" example="10">The separation between the check field outlines and the text entries.
		/// Measured in points.</param>
		/// <param name="spacing">The spacing between the rows, measured in points.</param>
		/// <param name="justification">The horizonta; justification for the text entries in the row area.</param>
		/// <param name="alignment" example="CENTRE">The vertical alignment of the text entries within the row area.</param>
		/// <param name="heightStrategy" example="AscentBaseline">The height strategy to use when determing the vertical placement
		/// of the text entries.</param>
		/// <param name="checkSize" example="6">The size of the check mark outlines, measured in points. If no value is provided,
		/// this will default to the row height. Each check mark outline will be positioned vertically centred in each row.</param>
		/// <param name="check" example="Circle">The outline to use for the check marks.</param>
		/// <param name="checkPosition">Indicates on which side the check mark should be drawn, left or right.</param>
		/// <param name="checkType">The check symbol to use when the check fields are in the "On" state.</param>
		/// <param name="checkColor">An optional color for the check field symbol. If no value is provided,
		/// the current text color will be used.</param>
		/// <size>60 80</size>
		public CheckList(
				WidgetSetup setup,
				List<RichString>? list = null,
				string? name = null,
				Dimension? height = null,
				float fontsize = 6f,
				float textOffset = 0f,
				float separation = 3f,
				float spacing = 3f,
				Justification justification = Justification.LEFT,
				Alignment alignment = Alignment.BOTTOM,
				TextHeightStrategy heightStrategy = TextHeightStrategy.FontsizeBaseline,
				float? checkSize = null,
				IBox? check = null,
				CheckPosition checkPosition = CheckPosition.LEFT,
				CheckType checkType = CheckType.CIRCLE,
				Color? checkColor = null
			) : base(setup) {

			this.list = list != null ? list.ToArray() : Array.Empty<RichString>();
			this.name = name;
			this.height = height ?? Dimension.Single;
			this.fontsize = fontsize;
			this.textOffset = textOffset;
			this.separation = separation;
			this.spacing = spacing;

			this.justification = justification;
			this.alignment = alignment;
			this.heightStrategy = heightStrategy;

			this.checkSize = checkSize;
			checkBoxStyle = check ?? new Circle(1f);
			this.checkRectOrder = checkPosition == CheckPosition.LEFT ? LayoutOrder.FORWARD : LayoutOrder.BACKWARD;
			this.checkType = checkType;
			this.checkColor = checkColor;
		}

		protected Rectangle?[] GetRows(Rectangle rect, out Rectangle? remainingRect) {
			return Divisions.Rows(rect, height, list.Length, spacing, Gutter, out remainingRect, out _, out _, false, Arrangement, LayoutOrder.FORWARD);
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			canvas.SaveState();

			Rectangle?[] rows = GetRows(rect, out _);

			canvas.SetTextSize(fontsize);

			Color finalCheckColor = checkColor ?? canvas.GetTextColor();

			for (int i = 0; i < rows.Length; i++) {
				if (rows[i] is Rectangle row) {
					float checkBoxSize = checkSize ?? row.Height;
					Rectangle? checkBoxRect = Divisions.Column(row, Dimension.FromPoints(checkBoxSize), separation, out Rectangle? rowRect, out _, true, Arrangement.FRONT, checkRectOrder);
					
					if (checkBoxRect is not null) {
						checkBoxRect = Rectangle.RectangleAt(checkBoxRect.CentreX, checkBoxRect.CentreY, checkBoxSize, checkBoxSize);

						checkBoxStyle.Draw(canvas, checkBoxRect, out Rectangle checkMarkRect);
						canvas.CheckField(checkMarkRect, (!string.IsNullOrWhiteSpace(name) ? (name + "_") : "") + list[i].Text, null, checkType, finalCheckColor);
					}

					if (rowRect is not null) {
						canvas.DrawRichText(rowRect, list[i], justification, alignment, heightStrategy);
					}
				}
			}

			canvas.RestoreState();
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetRows(rect, out Rectangle? remainingRect);
			return remainingRect;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			float minWidth = height.Absolute + separation + list.Min(s => graphicsState.GetWidth(s, fontsize));
			float minHeight = list.Length * height.Absolute + (list.Length - 1) * spacing;

			if(children.Count > 0) {
				Size? baseSize = base.GetMinimumContentSize(graphicsState, availableSpace);
				minWidth = Math.Max(minWidth, baseSize?.Width ?? 0f);
				minHeight += Gutter + (baseSize?.Height ?? 0f);
			}

			return new Size(minWidth, minHeight);
		}
	}

	/// <summary>
	/// This widget creates a text field in document, whose parameters and default value can be specified.
	/// </summary>
	public class Field : SharpWidget {

		protected readonly string name;
		protected readonly string? tooltip;

		protected readonly float? aspect;
		protected readonly string value;
		protected readonly float fontsize;
		protected readonly TextFormat format;
		protected readonly bool multiline;
		protected readonly bool rich;
		protected readonly bool lined;
		protected readonly Justification justification;
		protected readonly TextFieldType type;

		/// <summary>
		/// Constructor for Field widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">The name for this text field.</param>
		/// <param name="tooltip">Tooltip string to use for the field, which can be used to provide additional
		/// information in the final document for accesibility and usability purposes.</param>
		/// <param name="aspect">The aspect ratio for this field. If none is provided, the field will conform
		/// the the size and aspect ratio of the provided area. If provided, the final field area will be the
		/// largest rectangle of that aspect ratio that can fit inside the provided area.</param>
		/// <param name="value">The default text value for this field.</param>
		/// <param name="fontsize">Font size for this text field.
		/// A value of 0 or less indicates that the field in the final document should autosize the text.</param>
		/// <param name="format">Font format to use for the text field. This will use the appropriate font format from
		/// the current font selection.</param>
		/// <param name="singleline">Flag to indicate that the field should be a single line field.</param>
		/// <param name="rich">Flag to indicate that the text field should have rich text features enabled.</param>
		/// <param name="lined">Flag to indicate that the field should not be an interactive field, but should instead
		/// be drawn as a lined area, with line spacing equal to the fontsize. If the fontsize is zero, a line
		/// spacing of 15 points.</param>
		/// <param name="justification">Justification for the field, indicating if the field should be left, right,
		/// or centre justified.</param>
		/// <param name="type">The content type of this field, indicating if the field should constrain
		/// it's value to a floating point or integer number.</param>
		/// <size>0 0</size>
		public Field(
				WidgetSetup setup,
				string? name = null,
				string? tooltip = null,
				float? aspect = null,
				string value = "",
				float fontsize = 0f,
				TextFormat format = TextFormat.REGULAR,
				bool singleline = false,
				bool rich = false,
				bool lined = false,
				Justification justification = Justification.LEFT,
				TextFieldType type = TextFieldType.STRING
			) : base(setup) {

			this.name = name ?? "Field";
			this.tooltip = tooltip;

			this.aspect = aspect;
			this.value = value;
			this.fontsize = fontsize;
			this.format = format;
			multiline = !singleline;
			this.rich = rich;
			this.lined = lined;
			this.justification = justification;
			this.type = type;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			if (aspect.HasValue) { rect = rect.Aspect(aspect.Value); }
			if (lined) {
				canvas.SaveState();
				canvas.SetStrokeColor(canvas.GetMidtoneColor()).SetLineWidth(canvas.GetLineWidth() / 4f);
				float lineHeight = fontsize > 0 ? fontsize : 15f;
				int numLines = 2 + (int)Math.Ceiling(rect.Height / lineHeight);
				Rectangle?[] lines = Divisions.Rows(rect, Dimension.FromPoints(lineHeight), numLines, 0, out _, true, Arrangement.FRONT, LayoutOrder.FORWARD);
				for (int i = 0; i < lines.Length; i++) {
					if (lines[i] != null) {
						canvas.MoveTo(rect.Left, lines[i]!.Bottom)
							.LineTo(rect.Right, lines[i]!.Bottom);
						canvas.Stroke();
					}
				}
				canvas.RestoreState();
			}
			else {
				canvas.TextField(rect, name, tooltip, type, value, format, fontsize, multiline, rich, justification);
			}
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}
	}

	/// <summary>
	/// This widget creates a check field in the document, whose style and parameters can be specified.
	/// </summary>
	public class CheckField : SharpWidget {

		protected readonly string name;
		protected readonly string? tooltip;

		protected readonly float? aspect;
		protected readonly CheckType check;
		protected readonly Color? color;

		/// <summary>
		/// Constructor for CheckField widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">The name for this check field.</param>
		/// <param name="tooltip">Tooltip string to use for the field, which can be used to provide additional
		/// information in the final document for accesibility and usability purposes.</param>
		/// <param name="aspect">The aspect ratio for this field. If none is provided, the field will conform
		/// the the size and aspect ratio of the provided area. If provided, the final field area will be the
		/// largest rectangle of that aspect ratio that can fit inside the provided area.</param>
		/// <param name="check">The check symbol to use when this field is in the "On" state.</param>
		/// <param name="color">An optional color for the check field symbol. If no value is provided,
		/// the current text color will be used.</param>
		/// <size>0 0</size>
		public CheckField(
				WidgetSetup setup,
				string? name = null,
				string? tooltip = null,
				float? aspect = null,
				CheckType check = CheckType.CROSS,
				Color? color = null
			) : base(setup) {

			this.name = name ?? "CheckBox";
			this.tooltip = tooltip;

			this.aspect = aspect;
			this.check = check;
			this.color = color;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			if (aspect.HasValue) { rect = rect.Aspect(aspect.Value); }
			canvas.CheckField(rect, name, tooltip, check, color ?? canvas.GetTextColor());
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}
	}

	/// <summary>
	/// This widget creates an image field in the document, which may have a default image and aspect ratio specified.
	/// </summary>
	public class ImageField : SharpWidget {

		protected readonly string name;
		protected readonly string? tooltip;

		protected readonly CanvasImageData? placeholder;
		protected readonly float aspect;

		/// <summary>
		/// Constructor for ImageField widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">The name for this image field.</param>
		/// <param name="tooltip">Tooltip string to use for the field, which can be used to provide additional
		/// information in the final document for accesibility and usability purposes.</param>
		/// <param name="placeholder">A path to a placeholder image to use for this image field (which will
		/// be visible in the document before any other image is selected).</param>
		/// <param name="aspect">The aspect ratio for this field. If none is provided, the field will conform
		/// the the size and aspect ratio of the provided area. If provided, the final field area will be the
		/// largest rectangle of that aspect ratio that can fit inside the provided area.</param>
		/// <size>0 0</size>
		public ImageField(
				WidgetSetup setup,
				string? name = null,
				string? tooltip = null,
				CanvasImageData? placeholder = null,
				float aspect = -1
			) : base(setup) {
			this.name = name ?? "ImageField";
			this.tooltip = tooltip;
			this.placeholder = placeholder;
			this.aspect = aspect;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			canvas.ImageField(rect.Aspect(aspect), name, tooltip, placeholder);
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}
	}

	/*
	public class Subtitle : SharpWidget {

		protected readonly string localName;

		protected readonly float fontsize;
		protected readonly TextFormat format;
		protected readonly Vector titleOffset;
		protected readonly float titleSpacing;
		protected readonly Justification titleJustification;

		protected readonly Titled subtitle;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="setup"></param>
		/// <param name="_name" example="Subtitle"></param>
		/// <param name="_fontsize"></param>
		/// <param name="format"></param>
		/// <param name="titleOffset"></param>
		/// <param name="titleSpacing"></param>
		/// <param name="titleJustification"></param>
		public Subtitle(
				WidgetSetup setup,
				string? _name = null, // TODO RichString?
				float _fontsize = 6f,
				TextFormat format = TextFormat.REGULAR,
				Vector? titleOffset = null,
				float titleSpacing = 3f,
				Justification titleJustification = Justification.CENTRE
			) : base(setup) {

			localName = _name ?? "Subtitle";

			fontsize = _fontsize;
			this.format = format;
			this.titleOffset = titleOffset ?? new Vector(0f, 3f);
			this.titleSpacing = titleSpacing;
			this.titleJustification = titleJustification;

			subtitle = new Titled(new NoOutline(-1, 0f), this.localName, TitlePosition.BOTTOM, Layout.ROWS, Direction.NORTH, this.format, this.fontsize, this.titleOffset, this.titleSpacing, this.titleJustification);
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			//Console.WriteLine("Found subtitle.");
			subtitle.Draw(canvas, rect);
		}

		protected override Rectangle GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return subtitle.RemainingRect(graphicsState, rect);
		}
	}
	*/

	/// <summary>
	/// This widget creates an area with an annotated field at the top. If the
	/// widget has no children, then the remaining area will be filled with a
	/// text field.
	/// </summary>
	public class TopEntry : SharpWidget {

		protected readonly string name;

		protected readonly string? text;
		protected readonly TextFormat format;
		protected readonly float fontSize;
		protected readonly float headerHeight;
		protected readonly float headerSpacing;
		protected readonly (float x, float y) textOffset;
		protected readonly bool rich;

		/// <summary>
		/// Constructor for TopEntry.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">The name for this widget, which will be used as
		/// the basis of the field names (but will not be drawn).</param>
		/// <param name="text" example="Text">The text to draw as the annotation
		/// for the top field. If no text is provided, the top text will be left
		/// blank, but the field will still be added.</param>
		/// <param name="_format">The format for the annotation text and the
		/// field text.</param>
		/// <param name="fontSize">The fontsize for the annotation text and field
		/// text.</param>
		/// <param name="height">A height for the top annotation and field. Defaults
		/// to 1.5 times <paramref name="fontSize"/>.</param>
		/// <param name="spacing">The spacing between the top annotation and the
		/// remaining area for the widget. Defaults to 0.5 times
		/// <paramref name="fontSize"/>.</param>
		/// <param name="textOffset">An offset for the annotation text.</param>
		/// <param name="rich">Flag to indicate that the fields should use
		/// rich text features.</param>
		public TopEntry(
				WidgetSetup setup,
				string? name = null,
				string? text = null, // TODO RichString?
				TextFormat _format = TextFormat.REGULAR,
				float fontSize = 6.5f,
				float? height = null,
				float? spacing = null,
				(float x, float y) textOffset = default,
				bool rich = false
			) : base(setup) {

			this.name = name ?? "TopEntry";
			this.text = text;
			format = _format;
			this.fontSize = fontSize; // 6.5f;
			this.textOffset = textOffset;
			this.rich = rich;

			this.headerHeight = height ?? (fontSize * 1.5f);
			this.headerSpacing = spacing ?? (fontSize * 0.5f);
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			canvas.SaveState();

			float entryNameWidth = text != null ? canvas.GetWidth(text, format, fontSize) : 0f;

			Color entryColor = canvas.GetMidtoneColor().Darken(0.6f);

			if (text != null) {
				canvas.SaveState();
				canvas.SetTextColor(entryColor);
				canvas.SetTextFormatAndSize(format, fontSize);
				canvas.DrawText(text, rect.Left + textOffset.x, rect.Top - headerHeight + textOffset.y);
				canvas.RestoreState();
			}

			float entryStart = rect.X + (entryNameWidth > 0 ? entryNameWidth + canvas.GetWidth(" ", format, fontSize) * 2 : 0);
			float entryWidth = rect.Width - (entryStart - rect.X);
			Rectangle entryFieldRect = new Rectangle(entryStart, rect.Top - headerHeight, entryWidth, fontSize * 1.5f);
			canvas.TextField(entryFieldRect, $"{name}_{text ?? "top"}", null, TextFieldType.STRING, "", format, fontSize, false, rich, Justification.LEFT); // TODO Tooltip?

			canvas.SetStrokeColor(entryColor);
			canvas.MoveToRel(entryFieldRect, 0, 0).LineToRel(entryFieldRect, 1, 0).Stroke();

			canvas.RestoreState();

			if (children.Count == 0 ) {
				Rectangle remainingRect = GetContainerArea(canvas, rect);
				canvas.TextField(remainingRect, name, null, TextFieldType.STRING, "", TextFormat.REGULAR, 0, false, rich, Justification.CENTRE); // TODO Tooltip?
			}
		}

		protected override Rectangle GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle remainingRect = rect.Margins(headerHeight + headerSpacing, 0f, 0f, 0f, false);
			return remainingRect;
		}
	}

	/// <summary>
	/// This widget divides the available area into a series of subdivisions, in a number of rows and columns,
	/// with an outline around each subdivision. Each column may optionally have a title, the format of which can be specified.
	/// The relative widths of columns can also be specified. Each subdivision contains a text field. The number of columns
	/// will be the maximum length of "columns" or "widths", with missing column names being empty, and missing widths
	/// being set to 1 relative unit. If an absolute row height is specified, then the widget's size can be "auto", as a full
	/// height may be calculated.
	/// </summary>
	public class Subdivided : SharpWidget {

		// TODO This should follow similar conventions to the LinedWidgets, with row spacing and the like
		// TODO This should probably have a FieldDetails argument, to allow for better customization

		protected readonly string name;

		protected readonly string[]? columnNames;
		protected readonly Dimension[] columnRatios;
		protected readonly Justification[] justifications;

		protected readonly float headerFontSize;
		protected readonly float headerSpacing;

		protected readonly (float x, float y) spacing;
		protected readonly Dimension rowHeight;
		protected readonly int nRows;
		protected readonly bool rich;

		protected readonly IBox boxStyle;

		/// <summary>
		/// Constructor for Subdivided widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">A base name to use when naming the subdivision text fields.</param>
		/// <param name="columns" example="Column 1,Column 2">A list of names for the columns.</param>
		/// <param name="headerFontSize">The font size for the column headers.</param>
		/// <param name="headerSpacing">The spacing between the column headers and the top of the subdivisions.</param>
		/// <param name="widths" example="2,1">The widths of the columns.</param>
		/// <param name="justification" default="LEFT">The justifcations for the column, which will be used for column headers and fields.</param>
		/// <param name="_spacing" default="5,5">The spacing between the subdivisions, as a pair of numbers,
		/// for column and row spacing, respectively. Measured in points.</param>
		/// <param name="_height" default="1">The height of each row (not including the spacing). The default is 1 relative unit.
		/// If an absolute value is specified, then the widget size may be calculated for auto-sizing.</param>
		/// <param name="_rows" example="5">The number of rows to draw.</param>
		/// <param name="division" example="Simple">The outline style for each division.</param>
		/// <param name="rich">Flag to indicate that the subdivision fields should have rich text features enabled.</param>
		public Subdivided(
				WidgetSetup setup,
				string? name = null,
				string[]? columns = null, // TODO RichString?
				float headerFontSize = 5f,
				float headerSpacing = 3f,
				Dimension[]? widths = null,
				Justification[]? justification = null,
				(float x, float y)? _spacing = null,
				Dimension? _height = default,
				int _rows = -1,
				IBox? division = null,
				bool rich = false
			) : base(setup) {

			this.name = name ?? "Subdivided";

			int numColumns = Math.Max(columns?.Length ?? 1, widths?.Length ?? 1);

			// columnNames can be null, and should be null if its length is zero
			if (columns == null || columns.Length == 0) {
				columnNames = null;
			}
			else {
				columnNames = new string[numColumns];
				int c = 0;
				for (; c < columns.Length; c++) {
					columnNames[c] = columns[c];
				}
				for (; c < columnNames.Length; c++) {
					columnNames[c] = "";
				}
			}

			// Ensure columnRatios has a value
			if (widths == null) {
				if (columnNames != null) {
					columnRatios = ArrayUtils.MakeArray(columnNames.Length, Dimension.Single);
				}
				else {
					columnRatios = new Dimension[] { Dimension.Single };
				}
			}
			else {
				columnRatios = new Dimension[numColumns];
				int c = 0;
				for (; c < widths.Length; c++) {
					columnRatios[c] = widths[c];
				}
				for (; c < columnRatios.Length; c++) {
					columnRatios[c] = Dimension.Single;
				}
			}

			justifications = new Justification[numColumns];
			for (int j = 0; j < numColumns; j++) {
				justifications[j] = (justification != null && j < justification.Length) ? justification[j % justification.Length] : Justification.LEFT;
			}

			/*
			columnNames = columns; // config.GetProperty("columns", "").SplitAndTrim(',').ToArray();
			columnRatios = widths; // config.GetProperty("widths", "").SplitAndTrim(',').WhereNotEmpty().Select(Dimension.Parse).ToArray();
			justifications = justification ?? new Justification[0]; ; // config.GetProperty("justification", "").SplitAndTrim(',').WhereNotEmpty().Select(int.Parse).ToArray();

			// Ensure that we have equal numbers of column names and ratios, if names have been provided
			if (columnNames != null && columnRatios.Length != columnNames.Length) {
				columnRatios = ArrayUtils.MakeArray(columnNames.Length, Dimension.FromRelative(1f));
			}

			// Ensure that we have equal numbers of column ratios and justifications
			if (justifications.Length == 1) {
				justifications = columnRatios.Select(c => justifications[0]).ToArray();
			}
			else if (justifications.Length != columnRatios.Length) {
				justifications = columnRatios.Select(c => Justification.LEFT).ToArray();
			}
			*/

			this.headerFontSize = headerFontSize;
			this.headerSpacing = headerSpacing;

			spacing = _spacing ?? (5f, 5f);
			rowHeight = _height ?? Dimension.Single;
			//this.columnGutter = columnGutter ?? (2f / 3f) * spacing; // config.GetProperty("columngutter", (2f / 3f) * spacing);
			nRows = _rows;
			this.rich = rich;

			this.boxStyle = division ?? new NoOutline(-1f, trim: Margins.Zero);
		}

		//private float HeaderFontSize { get { return 5f; } }
		//private float HeaderSpacing { get { return 3f; } }

		/// <summary></summary>
		/// <exception cref="SharpDrawingException"></exception>
		/// <exception cref="InvalidRectangleException"></exception>
		protected void GetRects(Rectangle rect, out Rectangle?[]? headers, out Rectangle?[]? rows, out Rectangle? remainingRect) {
			Rectangle? workspaceRect = rect;

			if (columnNames != null) {
				Rectangle? headerRect = Divisions.Row(rect, Dimension.FromPoints(headerFontSize), headerSpacing, out workspaceRect, out _, true, Arrangement.FRONT, LayoutOrder.FORWARD);

				if (headerRect == null) {
					throw new SharpDrawingException(this, $"No header space for Subdivided rect. Header Rectangle null.");
				}

				headers = Divisions.Columns(headerRect, columnRatios, spacing.x, false, Arrangement.FRONT, LayoutOrder.FORWARD);
			}
			else {
				headers = null;
			}

			if (workspaceRect is not null) {
				if (nRows > 0) {
					//Console.WriteLine($"Creating {nRows} rows at size {rowHeight}.");
					rows = Divisions.Rows(workspaceRect, rowHeight, nRows, spacing.y, spacing.y, out remainingRect, out _, out _, false, Arrangement.FRONT, LayoutOrder.FORWARD);
				}
				else {
					//Console.WriteLine("Filling space: " + string.Join(", ", Divisions.Fill(workspaceRect.Height, rowHeight, spacing).Select(i=>i.ToString())));
					rows = Divisions.Rows(workspaceRect, Divisions.Fill(workspaceRect.Height, rowHeight, spacing.y), spacing.y, spacing.y, out remainingRect, out _, out _, true, Arrangement.FRONT, LayoutOrder.FORWARD);
				}
			}
			else {
				rows = null;
				remainingRect = null;
			}
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			canvas.SaveState();

			GetRects(rect, out Rectangle?[]? headers, out Rectangle?[]? rows, out Rectangle? remainingRect);

			if (columnNames != null && headers != null) {
				canvas.SaveState();

				canvas.SetTextColor(canvas.GetMidtoneColor().Darken(0.6f));
				canvas.SetTextFormatAndSize(TextFormat.REGULAR, headerFontSize);

				for (int i = 0; i < columnNames.Length; i++) {
					if (headers[i] is not null) {
						canvas.DrawText(headers[i]!, columnNames[i], justifications[i], 0, TextHeightStrategy.AscentBaseline);
					}
					else {
						canvas.LogError(this, $"No space for \"{columnNames[i]}\" header.");
					}
				}

				canvas.RestoreState();
			}

			//canvas.SetFillColor(canvas.GetMidtoneColor());

			if (rows != null) {
				for (int i = 0; i < rows.Length; i++) {
					if (rows[i] != null) {
						Rectangle?[] columns = Divisions.Columns(rows[i]!, columnRatios, spacing.x, false, Arrangement.FRONT, LayoutOrder.FORWARD); // TODO Should be able to adjust alignment?

						for (int j = 0; j < columnRatios.Length; j++) {
							//canvas.CorneredRectangle(columns[j], 3f).Fill();
							if (columns[j] is not null) {
								this.boxStyle.Draw(canvas, columns[j]!, out Rectangle cellRemaining);
								canvas.TextField(cellRemaining, $"{name}_{i + 1}_{(columnNames != null ? columnNames[j] : "COLUMN")}", null, TextFieldType.STRING, "", TextFormat.REGULAR, 0, false, rich, justifications[j]); // TODO Tooltip?
							}
						}
					}
				}
			}

			canvas.RestoreState();

			if (children.Count == 0 && remainingRect != null) {
				canvas.TextField(remainingRect, name, null, TextFieldType.STRING, "", TextFormat.REGULAR, 0, true, rich, 0); // TODO Tooltip?
			}
			// else nothing
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			/*
			if (children.Count == 0) {
				return null;
			}
			else {
				GetRects(rect, out _, out _, out Rectangle remainingRect);
				return remainingRect;
			}
			*/
			GetRects(rect, out _, out _, out Rectangle? remainingRect);
			return remainingRect;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			return new Size(0f, (columnNames != null ? (headerFontSize + headerSpacing) : 0f) + Divisions.CalculateTotalLength(rowHeight.Absolute, nRows, spacing.y));
		}
	}

	/// <summary>
	/// This widget draws a specified content multiple times in a grid layout, with a specified number of rows and columns.
	/// The repeated content will be drawn on a two-dimensional, rectangular grid, with equal-sized elements.
	/// Gutter details may be drawn in between the repeated elements, if desired.
	/// </summary>
	public class Repeat : SharpWidget {

		/// <summary>
		/// Different arrangements of gutters between elements in a two-dimensional grid.
		/// </summary>
		public enum GutterLayout {
			/// <summary>
			/// No gutters to be drawn between elements.
			/// </summary>
			NONE,
			/// <summary>
			/// Gutter details should be drawn between columns (i.e. vertical gutters).
			/// </summary>
			COLUMNS,
			/// <summary>
			/// Gutter details should be drawn between rows (i.e. horizontal gutters).
			/// </summary>
			ROWS
		}

		public readonly struct Spacing {
			public readonly float? horizontal;
			public readonly float? vertical;
			/// <summary>
			/// 
			/// </summary>
			/// <param name="horizontal">Horizontal spacing between elements, measured in points.</param>
			/// <param name="vertical">Vertical spacing between elements, measured in points.</param>
			public Spacing(float? horizontal = null, float? vertical = null) {
				this.horizontal = horizontal;
				this.vertical = vertical;
			}
		}

		protected readonly Div? content;

		protected readonly string? name;
		protected readonly int rows;
		protected readonly int columns;
		protected readonly float horizontalSpacing;
		protected readonly float verticalSpacing;

		protected readonly GutterLayout gutterLayout;

		/// <summary>
		/// Constructor for Repeat widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">A name to be appended to all child form fields, to distinguish between repeated fields.</param>
		/// <param name="_rows">The number of repeated rows to draw.</param>
		/// <param name="_columns">The number of repeated columns to draw.</param>
		/// <param name="spacing">The default spacing to use for the repeated elements, measured in points.</param>
		/// <param name="spacing_">Spacing data for this widget.</param>
		/// <param name="content">The content to be repeated in each grid element.</param>
		/// <param name="gutterLayout">The gutter layout to use when drawing gutter details between the grid elements.
		/// The details can either be drawn between rows, between columns, or not be drawn at all.</param>
		/// <size>0 0</size>
		public Repeat(
				WidgetSetup setup,
				string? name = null,
				int _rows = 1,
				int _columns = 1,
				float? spacing = null,
				Spacing spacing_ = default,
				ChildHolder? content = null,
				GutterLayout gutterLayout = GutterLayout.NONE
			) : base(setup) {

			this.name = name;
			rows = _rows;
			columns = _columns;

			if (rows < 1 || columns < 1) { throw new ArgumentException("\"rows\" and \"columns\" must have values >= 1."); } // SharpInitializationException

			float defaultSpacing = spacing ?? Gutter;
			horizontalSpacing = spacing_.horizontal ?? defaultSpacing;
			verticalSpacing = spacing_.vertical ?? defaultSpacing;

			this.content = content?.Child;

			this.gutterLayout = gutterLayout;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			Rectangle[] rects = Divisions.Grid(rect, rows, columns, horizontalSpacing, verticalSpacing, out Rectangle[] rowGutters, out Rectangle[] columnGutters).Flatten().ToArray();

			// Deal with gutters depending on Layout instead of custom GutterLayout
			if (setup.gutterStyle is IDetail gutterStyle) {
				if (gutterLayout == GutterLayout.COLUMNS) {
					gutterStyle.Layout = Layout.COLUMNS;
					for (int i = 0; i < columnGutters.Length; i++) {
						gutterStyle.Draw(canvas, columnGutters[i]);
					}
				}
				else if (gutterLayout == GutterLayout.ROWS) {
					gutterStyle.Layout = Layout.ROWS;
					for (int i = 0; i < rowGutters.Length; i++) {
						gutterStyle.Draw(canvas, rowGutters[i]);
					}
				}
			}

			if (content != null) {
				for (int i = 0; i < rects.Length; i++) {

					if (cancellationToken.IsCancellationRequested) { return; }

					canvas.SaveState();
					canvas.AppendFieldPrefix($"{(!string.IsNullOrWhiteSpace(name) ? name : "repeat")}_{i + 1}_");
					content.Draw(canvas, rects[i], default);
					canvas.RestoreState();
				}
			}
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			if (content != null) {
				return null;
			}
			else {
				return rect;
			}
		}

		protected override Size? GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			if (content != null) {
				Size baseAvailable = (Size)Divisions.Grid(availableSpace.AsRectangle(), rows, columns, horizontalSpacing, verticalSpacing, out _, out _).Flatten().First();

				Size contentMinCols = content.MinimumSize(graphicsState, Layout.COLUMNS, baseAvailable);
				Size contentMinRows = content.MinimumSize(graphicsState, Layout.ROWS, baseAvailable);

				float width = Divisions.CalculateTotalLength(contentMinCols.Width, columns, horizontalSpacing);
				float height = Divisions.CalculateTotalLength(contentMinRows.Height, rows, verticalSpacing);

				return new Size(width, height);
			}
			else {
				return base.GetMinimumContentSize(graphicsState, availableSpace);
			}
		}

		protected override Rectangle?[] GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle available) {
			Rectangle[] rects = Divisions.Grid(available, rows, columns, horizontalSpacing, verticalSpacing, out _, out _).Flatten().ToArray();

			return rects;
		}
	}

	/// <summary>
	/// This widget draws an image, taken from an image file, to the document, within the available area,
	/// with the option of specifying an aspect ratio for the drawn image.
	/// </summary>
	public class Image : SharpWidget {

		protected readonly CanvasImageData filename;
		protected readonly float? imageAspect;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="_file">The image file path, relative to the current configuration file.</param>
		/// <param name="_aspect">An optional aspect ratio to use for the image when drawing to the document.
		/// If this is not specified, the images intrinsic aspect ratio will be used.</param>
		/// <size>0 0</size>
		public Image(
				WidgetSetup setup,
				CanvasImageData? _file = null, // Should be requirement?
				float? _aspect = null
			) : base(setup) {

			filename = _file ?? throw new ArgumentNullException(nameof(_file));
			imageAspect = _aspect;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			canvas.AddImage(filename, rect, imageAspect);
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}
	}
}