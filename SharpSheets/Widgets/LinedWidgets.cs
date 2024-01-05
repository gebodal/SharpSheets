using SharpSheets.Layouts;
using SharpSheets.Colors;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Parsing;
using SharpSheets.Shapes;

namespace SharpSheets.Widgets {

	public abstract class AbstractLinedWidget : SharpWidget {

		public class FieldDetails : ISharpArgsGrouping {
			public readonly float? fontsize;
			public readonly TextFormat font;
			public readonly Justification justification;
			public readonly bool rich;
			public readonly Color? color;

			/// <summary>
			/// Constructor for field details.
			/// </summary>
			/// <param name="fontsize">The font size to use for field contents.
			/// A value of 0 indicates that the fields should autosize the contents to fit the available space.</param>
			/// <param name="font">Font format to use for the fields. This will use the appropriate font format from
			/// the current font selection.</param>
			/// <param name="justification">The horizontal justification for the fields, indicating if the field
			/// should be left, right, or centre justified.</param>
			/// <param name="rich">Flag to indicate that the fields should have rich text features enabled.</param>
			/// <param name="color">Color value for the field contents. Defaults to the current text color.</param>
			public FieldDetails(float? fontsize = null, TextFormat font = TextFormat.REGULAR, Justification justification = Justification.LEFT, bool rich = false, Color? color = null) {
				this.justification = justification;
				this.font = font;
				this.fontsize = fontsize;
				this.rich = rich;
				this.color = color;
			}
		}

		protected readonly string name;

		protected readonly Dimension height;
		protected readonly int? numRows;
		protected readonly (float column, float row) spacing; // x, y
		protected readonly float fontsize;
		protected readonly FieldDetails field;
		protected readonly bool underline;

		protected readonly float labelOffset;

		public AbstractLinedWidget(
				WidgetSetup setup, // gutter = 5f?
				string name,
				Dimension? height,
				int? numRows,
				(float column, float row) spacing,
				float fontsize,
				FieldDetails? field,
				bool underline,
				float labelOffset
			) : base(setup) {

			this.name = name ?? nameof(AbstractLinedWidget);

			this.height = height ?? Dimension.Single;
			//this.height = height ?? Dimension.FromPoints(fontsize); // Better?
			this.numRows = numRows <= 0 ? null : numRows;
			this.spacing = spacing;
			this.fontsize = fontsize;
			this.field = field ?? new FieldDetails();
			this.underline = underline;

			this.labelOffset = labelOffset;
		}

		protected Rectangle?[] GetRows(Rectangle rect, out Rectangle? remainingRect) {
			// TODO Should be able to adjust vertical alignment?
			if (numRows.HasValue) {
				return Divisions.Rows(rect, height, numRows.Value, spacing.row, Gutter, out remainingRect, out _, out _, false, Arrangement.FRONT, LayoutOrder.FORWARD);
			}
			else {
				if (height.Absolute == 0 && height.Percent == 0) {
					// No number or height specified. Don't guess at height/number, just return no rows.
					remainingRect = rect;
					return Array.Empty<Rectangle>();
				}

				remainingRect = null;
				return Divisions.FillRows(rect, height, spacing.row, out _, Arrangement.FRONT, LayoutOrder.FORWARD);
			}
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetRows(rect, out Rectangle? remainingRect);
			return remainingRect;
		}

		protected abstract float CalculateMinimumWidth(ISharpGraphicsState graphicsState, Size availableSpace);

		protected virtual float CalculateMinimumHeight(ISharpGraphicsState graphicsState, Size availableSpace) {
			return Divisions.CalculateTotalLength(height.Absolute > 0 ? height.Absolute : 0, numRows ?? 0, spacing.row); ;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			float minHeight = CalculateMinimumHeight(graphicsState, availableSpace);
			float minWidth = CalculateMinimumWidth(graphicsState, availableSpace);

			return new Size(minWidth, minHeight);
		}

	}

	/// <summary>
	/// This widget draw a series of lines, each with one or more fields, and text labels for those fields.
	/// The height of each line may be specified as an absolute or relative value, and the specific placement
	/// of fields and label may be controlled. The labels are provided as a series of entries to the widget,
	/// and can be used to place text before and after the fields. Optionally, a number of unlabelled "extra"
	/// lines may be appended to the bottom, containing only a single field in each line. Labels may contain
	/// embedded fields (to a maximum of one per label part), by surrounding a Dimension with question marks ("?").
	/// Each line will be underlined by default, but this can be specified.
	/// </summary>
	public class LinedDetails : AbstractLinedWidget {

		/// <summary>
		/// Indicates how the field start positions should be aligned horizontally
		/// aligned.
		/// </summary>
		public enum AlignFields {
			/// <summary>
			/// Indicates that no additional aligning should be performed, and the
			/// start positions for the fields should depend only on the label width.
			/// </summary>
			NONE,
			/// <summary>
			/// Indicates that rows with equal numbers of entries should have their
			/// fields begin at the same position as a group, such that each field
			/// has the right-most start location of any other field in a row with
			/// same number of entries and at the same index in the row.
			/// </summary>
			GROUPED,
			/// <summary>
			/// Indicates that, in addition to aligning as for <see cref="AlignFields.GROUPED"/>,
			/// that the first field in each row should be aligned with the first
			/// field in each other row, such that they each have the right-most
			/// value of any such field.
			/// </summary>
			ALLFIRST
		}

		protected readonly RichString[][][] details;
		protected readonly Dictionary<int, Dimension[]> widths;
		protected readonly float? detailSpacing;

		protected readonly uint extraRows;

		protected readonly AlignFields alignFields;
		protected readonly Alignment labelAlignment;

		/// <summary>
		/// Constructor for LinedDetails widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">The name for this widget, used for field names (not drawn to the document).</param>
		/// <param name="details" example="First (?30pt?);Second|Third,Comment|Fourth;Fifth;Sixth|Seventh|Eighth (?30pt?)">
		/// The labels to be used for each line. A line may comprise of 1 or more components. Each component
		/// may contain 1 or 2 parts. The first part will come before the field, and the second part (if present)
		/// will come after. Each entry will be a separate line. Addiitonally, each part may have up to one field
		/// contained within its text, by enclosing an absolute width in question marks ("?"). For example, the entry
		/// "Label (?40pt?)" would produce the text label "Label (", followed by a field with width 45pt, and then
		/// the text label ")". This field is in addition to the normal line field.
		/// </param>
		/// <param name="widths">The widths to use for each component. Each sub-array gives a list of n Dimensions,
		/// where each sub-array should have a different value for n (i.e. a different number of Dimension values).
		/// When a row is drawn to the document, the set of widths with a corresponding number of values will be
		/// used to determine the widths of the components. If there is not corresponding entry, then each component
		/// will be given an equal amount of space.</param>
		/// <param name="height" example="16pt">The height to use for each line (which will determine the field
		/// heights). This value is only meaningful if an absolute or percentage value is provided.</param>
		/// <param name="extra">A number of unlabelled lines to append to the end of the provided details.
		/// These lines will contain a single field, with no text labels.</param>
		/// <param name="spacing" default="3,3">The column and row spacing for this widget. The column spacing is used
		/// to horizontally separate components in a line, and the row spacing is used to vertically separate the lines.</param>
		/// <param name="detailSpacing">The horizontal spacing used to separate the parts of a component. If no
		/// value is provided, the width of a space at the current font size will be used.</param>
		/// <param name="fontsize" example="12">The fontsize to use for the text labels.</param>
		/// <param name="field">FieldDetails data for this widget.</param>
		/// <param name="underline">Flag to indicate whether the fields should be underlined. This line will
		/// use the current foreground color and linewidth.</param>
		/// <param name="alignFields">Indicates how the horizontal start of the fields should be aligned
		/// between lines.</param>
		/// <param name="labelAlignment">Indicates how the text labels should be aligned within the line areas.</param>
		/// <param name="labelOffset">A vertical offset to use for the text labels, after positioning with
		/// <paramref name="labelAlignment"/>, allowing for finer control of text positioning to account for font
		/// quirks.</param>
		/// <size>200 150</size>
		public LinedDetails(
				WidgetSetup setup,
				string? name = null,
				List<RichString[][]>? details = null,
				Dimension[][]? widths = null,
				Dimension? height = default,
				uint extra = 0,
				(float column, float row)? spacing = null,
				float? detailSpacing = null,
				float fontsize = 6f,
				FieldDetails? field = null,
				bool underline = true,
				AlignFields alignFields = AlignFields.NONE,
				Alignment labelAlignment = Alignment.BOTTOM,
				float labelOffset = 0f
			) : base(setup, name ?? nameof(LinedDetails), height, (details != null ? details.Count + (int)extra : null), spacing ?? (3f, 3f), fontsize, field, underline, labelOffset) {

			this.details = details?.ToArray() ?? Array.Empty<RichString[][]>();
			this.widths = (widths ?? Array.Empty<Dimension[]>()).ToDictionary(a => a.Length);
			this.detailSpacing = detailSpacing;

			this.extraRows = extra;

			this.alignFields = alignFields;
			this.labelAlignment = labelAlignment;
		}

		private class DetailPart {
			public Rectangle rect;
			public readonly RichString? text;
			public readonly string? fieldName;
			public readonly bool mainField;
			public readonly Justification justification;
			private DetailPart(Rectangle rect, RichString? text, string? fieldName, bool mainField, Justification justification) {
				this.rect = rect;
				this.text = text;
				this.fieldName = fieldName;
				this.mainField = mainField;
				this.justification = justification;
			}
			public static DetailPart Field(Rectangle rect, string fieldName, bool mainField, Justification justification) {
				return new DetailPart(rect, null, fieldName, mainField, justification);
			}
			public static DetailPart Text(Rectangle rect, RichString text) {
				return new DetailPart(rect, text, null, false, Justification.CENTRE);
			}
		}

		private readonly RichRegex spaceRegex = new RichRegex(new Regex(@"(?<!\\)\?(?<width>[^\?]+)\?"));

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			canvas.SaveState();

			canvas.SetTextSize(fontsize);

			Rectangle?[] rows = GetRows(rect, out _);
			int detailRowCount = Math.Min(details.Length, rows.Length);

			float columnSpacing = spacing.column; // Spacing between separate entries
			float partSpacing = detailSpacing ?? canvas.GetWidth(" ", TextFormat.REGULAR, fontsize); // Spacing within entries

			List<DetailPart>[][] detailParts = new List<DetailPart>[detailRowCount][];

			//canvas.SetFontAndSize(font, fontsize);

			//float lineWidth = 0.25f * canvas.GetDefaultLineWidth();
			float rowHeight = rows.Length > 0 && rows[0] is not null ? rows[0]!.Height : 0f;
			float textYoffset = (labelAlignment == Alignment.TOP ? rowHeight - fontsize : (labelAlignment == Alignment.BOTTOM ? 0f : (rowHeight - fontsize) / 2f)) + labelOffset;
			//canvas.SetLineWidth(lineWidth);

			for (int i = 0; i < detailRowCount; i++) {
				detailParts[i] = new List<DetailPart>[details[i].Length];
				if (rows[i] != null) {

					Rectangle?[] columns;
					if (widths.ContainsKey(details[i].Length)) {
						columns = Divisions.Columns(rows[i]!, widths[details[i].Length], columnSpacing, false, Arrangement.FRONT, LayoutOrder.FORWARD);
					}
					else {
						columns = Divisions.Columns(rows[i]!, details[i].Length, columnSpacing);
					}

					for (int j = 0; j < columns.Length; j++) {
						detailParts[i][j] = new List<DetailPart>();

						RichString[] parts = details[i][j];
						if (parts.Length > 2 || parts.Length == 0) {
							canvas.LogError(this, string.Format("Each lined detail component must have 1 or 2 parts, {0} provided (\"{1}\").", parts.Length, RichString.Join((RichString)", ", details[i][j]).Formatted));
							//throw new FormatException();
							parts = new RichString[] { RichString.Create("ERROR", TextFormat.REGULAR) };
						}

						Rectangle? sectionRect = columns[j];

						if(sectionRect is null) {
							canvas.LogError(this, $"No space for \"{parts[0].Formatted + (parts.Length > 1 ? "..." : "")}\" entry.");
							continue;
						}

						Rectangle totalLeftRect;
						Rectangle totalRightRect;

						Match matchLeft = spaceRegex.Match(parts[0]); // TODO This should allow for multiple matches!
						if (matchLeft.Success) {
							RichString[] split = spaceRegex.Split(parts[0], 2);
							Dimension splitSpacing = Dimension.ParseAbsolute(matchLeft.Groups["width"].Value);
							Rectangle splitSpace = new Rectangle(sectionRect.X + canvas.GetWidth(split[0], fontsize), sectionRect.Y, splitSpacing.Absolute, sectionRect.Height);
							totalLeftRect = new Rectangle(sectionRect.X, sectionRect.Y, canvas.GetWidth(split[0], fontsize) + splitSpacing.Absolute + canvas.GetWidth(split[1], fontsize), sectionRect.Height);

							//canvas.DrawRichText(split[0], sectionRect.X, sectionRect.Y + textYoffset);
							detailParts[i][j].Add(DetailPart.Text(sectionRect, split[0]));
							//if (underline) canvas.MoveToRel(splitSpace, 0, 0).LineToRel(splitSpace, 1, 0).Stroke();
							//canvas.TextField(splitSpace.Margins(0, columnSpacing / 2, 0, columnSpacing / 2, false), split[0].Text + "_Blank", null, TextFieldType.STRING, "", TextFormat.REGULAR, fontsize, false, false, Justification.CENTRE); // TODO Tooltip?
							detailParts[i][j].Add(DetailPart.Field(splitSpace, split[0].Text + "_Blank", false, Justification.CENTRE));
							//canvas.DrawRichText(split[1], sectionRect.X + canvas.GetWidth(split[0], fontsize) + splitSpacing.Absolute, sectionRect.Y + textYoffset);
							detailParts[i][j].Add(DetailPart.Text(new Rectangle(sectionRect.X + canvas.GetWidth(split[0], fontsize) + splitSpacing.Absolute, sectionRect.Y, sectionRect.Width, sectionRect.Height), split[1]));
						}
						else {
							RichString text = parts[0];
							totalLeftRect = new Rectangle(sectionRect.X, sectionRect.Y, canvas.GetWidth(text, fontsize), sectionRect.Height);
							canvas.DrawRichText(text, sectionRect.X, sectionRect.Y + textYoffset);
							detailParts[i][j].Add(DetailPart.Text(sectionRect, text));
						}

						List<DetailPart> append = new List<DetailPart>();
						if (parts.Length == 2) {
							Match matchRight = spaceRegex.Match(parts[1]);
							if (matchRight.Success) {
								RichString[] split = spaceRegex.Split(parts[1], 2); // Should allow for multiple matches?
								Dimension splitSpacing = Dimension.ParseAbsolute(matchRight.Groups["width"].Value);
								float totalWidth = canvas.GetWidth(split[0], fontsize) + splitSpacing.Absolute + canvas.GetWidth(split[1], fontsize);
								Rectangle splitSpace = new Rectangle(sectionRect.Right - totalWidth + canvas.GetWidth(split[0], fontsize), sectionRect.Y, splitSpacing.Absolute, sectionRect.Height);
								totalRightRect = new Rectangle(sectionRect.Right - totalWidth, sectionRect.Y, totalWidth, sectionRect.Height);

								//canvas.DrawRichText(split[0], sectionRect.X, sectionRect.Y + textYoffset);
								append.Add(DetailPart.Text(totalRightRect, split[0]));
								//if (underline) canvas.MoveToRel(splitSpace, 0, 0).LineToRel(splitSpace, 1, 0).Stroke();
								//canvas.TextField(splitSpace.Margins(0, columnSpacing / 2, 0, columnSpacing / 2, false), split[1].Text + "_Blank", null, TextFieldType.STRING, "", TextFormat.REGULAR, fontsize, false, false, Justification.CENTRE); // TODO Tooltip?
								append.Add(DetailPart.Field(splitSpace, split[1].Text + "_Blank", false, Justification.CENTRE));
								//canvas.DrawRichText(split[1], sectionRect.X + canvas.GetWidth(split[0], fontsize) + splitSpacing.Absolute, sectionRect.Y + textYoffset);
								append.Add(DetailPart.Text(new Rectangle(totalRightRect.X + canvas.GetWidth(split[0], fontsize) + splitSpacing.Absolute, totalRightRect.Y, totalRightRect.Width, totalRightRect.Height), split[1]));
							}
							else {
								RichString text = parts[1];
								float textWidth = canvas.GetWidth(text, fontsize);
								totalRightRect = new Rectangle(sectionRect.Right - textWidth, sectionRect.Y, textWidth, sectionRect.Height);
								//canvas.DrawRichText(text, sectionRect.Right - textWidth, sectionRect.Y + textYoffset);
								append.Add(DetailPart.Text(new Rectangle(sectionRect.Right - textWidth, sectionRect.Y, textWidth, sectionRect.Height), text));
							}
						}
						else {
							totalRightRect = new Rectangle(sectionRect.Right, sectionRect.Y, 0, sectionRect.Height);
						}

						Rectangle fieldRect = sectionRect.Margins(0, totalRightRect.Width > 0 ? totalRightRect.Width + partSpacing / 2 : 0f, 0, totalLeftRect.Width > 0 ? totalLeftRect.Width + partSpacing / 2 : 0f, false);
						string fieldName = parts[0].Length > 0 ? parts[0].Text : $"{name}_UnnamedSection_{i}_{j}";

						//if (underline) canvas.MoveToRel(fieldRect, 0, 0).LineToRel(fieldRect, 1, 0).Stroke();
						//canvas.TextField(fieldRect.Margins(0, columnSpacing / 2, 0, columnSpacing / 2, false), fieldName, null, TextFieldType.STRING, "", TextFormat.REGULAR, fontsize, false, false, justification); // TODO Tooltip?
						detailParts[i][j].Add(DetailPart.Field(fieldRect, fieldName, true, field.justification));

						detailParts[i][j].AddRange(append);


					}

					if (this.setup.diagnostic) {
						canvas.SaveState();
						canvas.SetStrokeColor(Color.Green);
						canvas.MoveTo(rows[i]!.Left, rows[i]!.Bottom).LineTo(rows[i]!.Right, rows[i]!.Bottom).Stroke();
						canvas.RestoreState();
					}
				}
			}

			if (alignFields != AlignFields.NONE) {
				Dictionary<int, float[]> startPositions = new Dictionary<int, float[]>();
				for (int i = 0; i < detailParts.Length; i++) {
					float[] startXs = detailParts[i].Select(dparts => dparts.First(dps => dps.mainField)).Select(mainField => mainField.rect.Left).ToArray();
					if (startPositions.TryGetValue(startXs.Length, out float[]? existing)) {
						for (int j = 0; j < startXs.Length; j++) {
							startXs[j] = Math.Max(existing[j], startXs[j]);
						}
					}
					startPositions[startXs.Length] = startXs;

					if(alignFields == AlignFields.ALLFIRST) {
						foreach(float[] positions in startPositions.Values) {
							positions[0] = Math.Max(positions[0], startXs[0]);
						}
					}
				}

				for (int i = 0; i < detailParts.Length; i++) {
					float[] startXs = startPositions[detailParts[i].Length];
					for (int j = 0; j < detailParts[i].Length; j++) {
						float startX = startXs[j];

						DetailPart? mainField = detailParts[i][j].FirstOrDefault(dp => dp.mainField);
						if (mainField != null) {
							mainField.rect = new Rectangle(startX, mainField.rect.Y, mainField.rect.Width - (startX - mainField.rect.X), mainField.rect.Height);
						}
					}
				}
			}

			Color fieldColor = field.color ?? canvas.GetTextColor();
			for (int i = 0; i < detailParts.Length; i++) {
				for (int j = 0; j < detailParts[i].Length; j++) {
					List<DetailPart> parts = detailParts[i][j];
					for (int p = 0; p < parts.Count; p++) {
						if (parts[p].fieldName != null) {
							if (underline) canvas.MoveToRel(parts[p].rect, 0, 0).LineToRel(parts[p].rect, 1, 0).Stroke();
							canvas.TextField(parts[p].rect.Margins(0, partSpacing / 2, 0, partSpacing / 2, false), parts[p].fieldName!, null, TextFieldType.STRING, "", field.font, field.fontsize ?? fontsize, fieldColor, false, field.rich, parts[p].justification); // TODO Tooltip?
						}
						else {
							canvas.DrawRichText(parts[p].text!, parts[p].rect.X, parts[p].rect.Y + textYoffset);
						}
					}
				}
			}

			for (int i = detailParts.Length; i < rows.Length; i++) {
				if (rows[i] != null) {
					if (underline) canvas.MoveToRel(rows[i]!, 0, 0).LineToRel(rows[i]!, 1, 0).Stroke();
					canvas.TextField(rows[i]!.Margins(0, partSpacing / 2, 0, partSpacing / 2, false), $"{name}_{i - detailParts.Length}", null, TextFieldType.STRING, "", field.font, field.fontsize ?? fontsize, fieldColor, false, field.rich, field.justification); // TODO Tooltip?
				}
			}

			canvas.RestoreState();
		}

		protected override float CalculateMinimumWidth(ISharpGraphicsState graphicsState, Size availableSpace) {
			float minWidth = details.Select(a => a.SelectMany(s => s).Select(s => graphicsState.GetWidth(spaceRegex.Replace(s, (RichString)""), fontsize)).Sum()).Max();

			// TODO The width should account for the "?10pt?" spacers too

			return minWidth;
		}

	}

	/// <summary>
	/// This widget draws a series of lines, with each line containing a single text field. The heights of each
	/// line may be specified as an absolute or relative value. Each line will be underlined by default, but
	/// this can be specified.
	/// </summary>
	public class LinedField : AbstractLinedWidget {

		/// <summary>
		/// Constructor for LinedField widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">The name for this widget, used for field names (not drawn to the document).</param>
		/// <param name="height">The height to use for each line (which will determine the field
		/// heights). This value is only meaningful if an absolute or percentage value is provided.
		/// If no value is provided for <paramref name="rows"/>, when the area will be filled with lines of this height.
		/// If no <paramref name="height"/> or <paramref name="rows"/> value is provided, then there will be a single
		/// row.</param>
		/// <param name="rows" example="6">The number of lines to draw. If no value is provided, then the available
		/// area will be filled with rows of the specified <paramref name="height"/>. If no <paramref name="height"/>
		/// or <paramref name="rows"/> is specified, then there will be a single row.</param>
		/// <param name="spacing" default="3, 3">The column and row spacing for this widget. The column spacing is
		/// unused for this widget type (and is included for compatibility with other lined widgets), with the row
		/// spacing being used to vertically separate the lines.</param>
		/// <param name="field">FieldDetails data for this widget.</param>
		/// <param name="underline">Flag to indicate whether the fields should be underlined. This line will
		/// use the current foreground color and linewidth.</param>
		/// <size>200 150</size>
		public LinedField(
				WidgetSetup setup, // gutter = 5f?
				string? name = null,
				Dimension? height = default,
				int? rows = null,
				(float column, float row)? spacing = null,
				FieldDetails? field = null,
				bool underline = true
			) : base(setup, name ?? nameof(LinedField), height, rows, spacing ?? (3f, 3f), 0f, field, underline, 0f) { }

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			canvas.SaveState();

			Rectangle?[] rows = GetRows(rect, out _);

			//float lineWidth = 0.25f * canvas.GetDefaultLineWidth();
			//canvas.SetLineWidth(lineWidth);

			float sideSpacing = canvas.GetWidth(" ", TextFormat.REGULAR, fontsize) / 2;

			Color fieldColor = field.color ?? canvas.GetTextColor();
			for (int i = 0; i < rows.Length; i++) {
				if (rows[i] != null) {
					if (underline) { canvas.MoveToRel(rows[i]!, 0, 0).LineToRel(rows[i]!, 1, 0).Stroke(); }
					canvas.TextField(rows[i]!.Margins(0, sideSpacing, 0, sideSpacing, false), $"{name}_row_{i}", null, TextFieldType.STRING, "", field.font, field.fontsize ?? fontsize, fieldColor, false, field.rich, field.justification); // TODO Tooltip?
				}
			}

			canvas.RestoreState();
		}

		protected override float CalculateMinimumWidth(ISharpGraphicsState graphicsState, Size availableSpace) {
			return 0f;
		}
	}

	/// <summary>
	/// This widget draws a series of lines, each containing one or more fields arranged in columns. Each column
	/// will be optionally headed by a text label. The size and placement of the column headers, and the column
	/// widths, can be specified. The height of each line may be specified as an absolte or relative value. Each
	/// line will be underlined by default, but this can be specified.
	/// </summary>
	public class LinedList : AbstractLinedWidget {

		protected readonly RichString[]? columns;
		protected readonly Dimension[] widths;
		protected readonly float titlespacing;
		protected readonly float titlefontsize;
		protected readonly Justification? titleJustification;

		/// <summary>
		/// Constructor for LinedList widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">The name for this widget, used for field names (not drawn to the document).</param>
		/// <param name="columns" example="First,Second,Third">The column headings to draw at the top of the
		/// area.</param>
		/// <param name="widths" default="1" example="2,1,1">The widths to use when drawing the columns.
		/// If fewer widths than column names are provided, then the excess will be ignored. If no columns
		/// are provided, then all widths will be used, and no column headings drawn.</param>
		/// <param name="height">The height to use for each line (which will determine the field
		/// heights). This value is only meaningful if an absolute or percentage value is provided.</param>
		/// <param name="rows" example="6">The number of lines to draw. If no value is provided, then the available
		/// area will be filled with rows of the specified <paramref name="height"/>. If no <paramref name="height"/>
		/// or <paramref name="rows"/> is specified, then there will be a single row.</param>
		/// <param name="spacing" default="3, 3">The column and row spacing for this widget. The column spacing is used
		/// to horizontally separate the columns, and the row spacing is used to vertically separate the lines.</param>
		/// <param name="titlespacing">The vertical spacing between the header text and the first line. If no value
		/// is provided, this will default to the row spacing.</param>
		/// <param name="titlefontsize" example="13">The fontsize to use for the header text.</param>
		/// <param name="titleJustification">The justification to use for the header text. If no value is provided,
		/// this will default to the field justification.</param>
		/// <param name="field">FieldDetails data for this widget.</param>
		/// <param name="underline">Flag to indicate whether the fields should be underlined. This line will
		/// use the current foreground color and linewidth.</param>
		/// <size>200 150</size>
		public LinedList(
				WidgetSetup setup, // gutter = 5f?
				string? name = null,
				RichString[]? columns = null,
				Dimension[]? widths = null,
				Dimension? height = default,
				int? rows = null,
				(float column, float row)? spacing = null,
				float? titlespacing = null,
				float titlefontsize = 6f,
				Justification? titleJustification = null,
				FieldDetails? field = null,
				bool underline = true
			) : base(setup, name ?? nameof(LinedList), height, rows, spacing ?? (3f, 3f), 0f, field, underline, 0f) {

			this.columns = (columns != null && columns.Length > 0) ? columns : null;

			if (this.columns != null) {
				this.widths = new Dimension[this.columns.Length];
				for (int i = 0; i < this.columns.Length; i++) {
					this.widths[i] = widths != null ? widths[i % widths.Length] : Dimension.Single;
				}
			}
			else {
				this.widths = widths ?? new Dimension[] { Dimension.Single };
			}

			this.titlespacing = titlespacing ?? this.spacing.row;
			this.titlefontsize = titlefontsize;
			this.titleJustification = titleJustification;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			canvas.SaveState();

			Rectangle? headerRow;
			Rectangle?[] rows;
			if (columns != null) {
				headerRow = Divisions.Row(rect, Dimension.FromPoints(titlefontsize), titlespacing, out Rectangle? rowArea, out _, true, Arrangement.FRONT, LayoutOrder.FORWARD);
				rows = rowArea != null ? GetRows(rowArea, out _) : Array.Empty<Rectangle?>();
			}
			else {
				headerRow = null;
				rows = GetRows(rect, out _);
			}

			//float lineWidth = 0.25f * canvas.GetDefaultLineWidth();
			//canvas.SetLineWidth(lineWidth);

			float sideSpacing = canvas.GetWidth(" ", TextFormat.REGULAR, fontsize) / 2;

			if (columns != null && headerRow != null) {
				Rectangle?[] headerRects = Divisions.Columns(headerRow, widths, spacing.column, true, Arrangement.FRONT, LayoutOrder.FORWARD);
				canvas.SetTextSize(titlefontsize);
				for (int i = 0; i < columns.Length; i++) {
					if (headerRects[i] != null) {
						canvas.DrawRichText(headerRects[i]!, columns[i], titleJustification ?? field.justification, Alignment.BOTTOM, TextHeightStrategy.FontsizeBaseline);
					}
					else {
						canvas.LogError(this, $"No space for \"{columns[i]}\" header.");
					}
				}
			}

			Color fieldColor = field.color ?? canvas.GetTextColor();
			for (int i = 0; i < rows.Length; i++) {
				if (rows[i] != null) {
					Rectangle?[] rowRects = Divisions.Columns(rows[i]!, widths, spacing.column, true, Arrangement.FRONT, LayoutOrder.FORWARD);
					for (int j = 0; j < rowRects.Length; j++) {
						if (rowRects[j] is not null) {
							string rowName = name + "_" + (columns != null ? columns[j].Text : "row") + "_" + (i + 1);
							if (underline) { canvas.MoveToRel(rowRects[j]!, 0, 0).LineToRel(rowRects[j]!, 1, 0).Stroke(); }
							canvas.TextField(rowRects[j]!.Margins(0, sideSpacing, 0, sideSpacing, false), rowName, null, TextFieldType.STRING, "", field.font, field.fontsize ?? fontsize, fieldColor, false, field.rich, field.justification); // TODO Tooltip?
						}
					}
				}
			}

			canvas.RestoreState();
		}

		protected override float CalculateMinimumHeight(ISharpGraphicsState graphicsState, Size availableSpace) {
			return (columns!=null ? titlefontsize + titlespacing : 0f) + base.CalculateMinimumHeight(graphicsState, availableSpace);
		}
		protected override float CalculateMinimumWidth(ISharpGraphicsState graphicsState, Size availableSpace) {
			if (columns != null) {
				float minWidth = Divisions.CalculateTotalLength(columns.Select(s => graphicsState.GetWidth(s, titlefontsize)), spacing.column);
				return minWidth;
			}
			else {
				float minWidth = Divisions.CalculateTotalLength(0f, widths.Length, spacing.column);
				return minWidth;
			}
		}
	}

	/// <summary>
	/// This widget draws a series of lines, each containing at least one text field and one check field,
	/// and may begin with either a text label or a second text field. The height of each line may be specified
	/// as an absolute or relative value, and the specific placement of fields and labels may be controlled.
	/// The labels are provided as a series of entries to the widget, and may optionally contain embedded fields
	/// (to a maximum of one per label), by surrounding a Dimension with question marks ("?"). There is also
	/// an option to append a number of unlabelled "extra" lines to the bottom, where the text label is replaced
	/// with a text field. Each line will be underlined by default, but this can be specified.
	/// </summary>
	public class LinedCheckList : AbstractLinedWidget {

		protected readonly RichString[] skills; // TODO RichString?
		protected readonly uint extraRows;

		protected readonly Dimension? width;

		protected readonly Shapes.IBox checkBoxStyle;
		protected readonly CheckType checkType;

		/// <summary>
		/// Constructor for LinedCheckList widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name">The name for this widget, used for field names (not drawn to the document).</param>
		/// <param name="entries" example="First,Second,Third,Fourth (?25pt?)">
		/// A list of labels for each line in the widget. Each label may have up to one field contained within its text,
		/// by enclosing an absolute width in question marks ("?"). For example, the entry "Label (?40pt?)" would produce
		/// the text label "Label (", followed by a field with width 45pt, and then the text label ")". This field is in
		/// addition to the normal text and check fields for each line.
		/// </param>
		/// <param name="height" example="15pt">The height to use for each line (which will determine the field and check
		/// heights). This value is only meaningful if an absolute or percentage value is provided.</param>
		/// <param name="extra">A number of unlabelled lines to append to the end of the provided list.
		/// These lines will contain a text field in place of the label, in addition to the usual fields.</param>
		/// <param name="spacing" default="3, 3" example="5,5">The column and row spacing for this widget. The column
		/// spacing is used to horizontally separate components in a line, and the row spacing is used to vertically
		/// separate the lines.</param>
		/// <param name="fontsize" example="12">The fontsize to use for the text labels.</param>
		/// <param name="field">FieldDetails data for this widget.</param>
		/// <param name="check" example="Simple">The outline to draw around the check fields in each line.</param>
		/// <param name="checkType">The check mark to use in the check fields.</param>
		/// <param name="width">The width for the text labels for each row. If no value is provided, the maximum width
		/// among the labels (including any additional field widths) will be used.</param>
		/// <param name="underline">Flag to indicate whether the fields should be underlined. This line will
		/// use the current foreground color and linewidth.</param>
		/// <param name="labelAlignment">Indicates how the text labels should be aligned within the line areas.</param>
		/// <param name="labelOffset">A vertical offset to use for the text labels, after positioning with
		/// <paramref name="labelAlignment"/>, allowing for finer control of text positioning to account for font
		/// quirks.</param>
		/// <size>200 100</size>
		public LinedCheckList(
				WidgetSetup setup,
				string? name = null,
				List<RichString>? entries = null,
				Dimension? height = default,
				uint extra = 0,
				(float column, float row)? spacing = null,
				float fontsize = 6f,
				FieldDetails? field = null,
				Shapes.IBox? check = null,
				CheckType checkType = CheckType.CHECK,
				Dimension? width = null,
				bool underline = true,
				Alignment labelAlignment = Alignment.BOTTOM,
				float labelOffset = 0f
			) : base(setup, name ?? nameof(LinedDetails), height, (entries != null ? entries.Count + (int)extra : null), spacing ?? (3f, 3f), fontsize, field, underline, labelOffset) {

			this.skills = entries != null ? entries.ToArray() : Array.Empty<RichString>();

			extraRows = extra;

			//this.width = config.HasProperty("width") ? config.GetProperty("width", Dimension.FromRelative(1), Dimension.Parse) : (Dimension?)null;
			if (width != null) {
				this.width = width.Value.IsZero ? Dimension.Single : width.Value;
			}
			else {
				this.width = null;
			}

			checkBoxStyle = check ?? new NoOutline(-1f, trim: Margins.Zero); // BoxFactory.GetBox(new NamedContext(config, "check"), 1, "shadowedbox");
			this.checkType = checkType;
		}

		readonly RichRegex spaceRegex = new RichRegex(new Regex("(?<!\\\\)\\?(?<width>[^\\?]+)\\?"));

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			canvas.SaveState();

			canvas.SetTextSize(fontsize);

			Rectangle?[] rows = GetRows(rect, out _);

			//float lineWidth = 0.25f * canvas.GetDefaultLineWidth();
			float textYoffset = labelOffset; // 4 * canvas.GetLineWidth();
			//canvas.SetLineWidth(lineWidth);

			//float columnSpacing = canvas.GetWidth(" ", TextFormat.REGULAR, fontsize);
			float columnSpacing = spacing.column;

			float GetWidth(RichString str) {
				Match match = spaceRegex.Match(str);
				if (match.Success) {
					RichString[] split = spaceRegex.Split(str, 2);
					//Console.WriteLine("split: " + split.Length + $" -> {split[0].Text} - {split[1].Text}");
					Dimension splitSpacing = Dimension.ParseAbsolute(match.Groups["width"].Value);
					return canvas.GetWidth(split[0], fontsize) + splitSpacing.Absolute + canvas.GetWidth(split[1], fontsize);
				}
				else {
					return canvas.GetWidth(str, fontsize);
				}
			}

			float maxTextWidth = skills.Select(s => GetWidth(s)).Max(); // skills.Select(s => s.GetWidth(canvas.fonts, fontsize)).Max();
			Dimension[] columnRatios = new Dimension[2];
			columnRatios[0] = width != null ? width.Value : Dimension.FromPoints(maxTextWidth);
			columnRatios[1] = Dimension.Single;

			for (int i = 0; i < rows.Length; i++) {
				if (rows[i] != null) {

					float checkBoxSize = 0.5f * rows[i]!.Height;
					//float checkBoxSpacing = 0.5f * checkBoxSize;
					Rectangle? checkRect = Divisions.Column(rows[i]!, Dimension.FromPoints(checkBoxSize), columnSpacing, out Rectangle? rowRect, out _, true, Arrangement.FRONT, LayoutOrder.BACKWARD);

					if (checkRect is not null) {
						checkRect = Rectangle.RectangleAt(checkRect.CentreX, checkRect.CentreY, checkBoxSize, checkBoxSize);
					}

					//Rectangle nameRect = new Rectangle(rowRect.X, rowRect.Y, maxTextWidth, rowRect.Height);
					//Rectangle fieldRect = rowRect.Margins(0, 0, 0, maxTextWidth + columnSpacing, false);

					if(rowRect == null) {
						continue;
					}

					Rectangle?[] columnRects = Divisions.Columns(rowRect, columnRatios, columnSpacing, false, Arrangement.FRONT, LayoutOrder.FORWARD);
					if (columnRects[0] is null || columnRects[1] is null) {
						continue;
					}

					string fieldName;

					if (i < skills.Length) {

						Rectangle totalNameRect;

						Match match = spaceRegex.Match(skills[i]);
						if (match.Success) {
							RichString[] split = spaceRegex.Split(skills[i], 2);
							Dimension splitSpacing = Dimension.ParseAbsolute(match.Groups["width"].Value);
							Rectangle splitSpace = new Rectangle(rowRect.X + canvas.GetWidth(split[0], fontsize), rowRect.Y, splitSpacing.Absolute, rowRect.Height);
							totalNameRect = new Rectangle(rowRect.X, rowRect.Y, canvas.GetWidth(split[0], fontsize) + splitSpacing.Absolute + canvas.GetWidth(split[1], fontsize), rowRect.Height);

							canvas.DrawRichText(split[0], rowRect.X, rowRect.Y + textYoffset);
							canvas.MoveToRel(splitSpace, 0, 0).LineToRel(splitSpace, 1, 0).Stroke();
							canvas.TextField(splitSpace.Margins(0, columnSpacing / 2, 0, columnSpacing / 2, false), split[0].Text + split[1].Text + "_Blank", null, TextFieldType.STRING, "", field.font, field.fontsize ?? 0f, false, false, Justification.CENTRE); // TODO Tooltip?
							canvas.DrawRichText(split[1], rowRect.X + canvas.GetWidth(split[0], fontsize) + splitSpacing.Absolute, rowRect.Y + textYoffset);

							fieldName = split[0].Text + split[1].Text;
						}
						else {
							canvas.DrawRichText(skills[i], rowRect.X, rowRect.Y + textYoffset);

							totalNameRect = new Rectangle(rowRect.X, rowRect.Y, canvas.GetWidth(skills[i], fontsize), rowRect.Height);

							fieldName = skills[i].Text;
						}

						if (columnRects[0]!.Width < totalNameRect.Width) {
							float difference = totalNameRect.Width - columnRects[0]!.Width;
							columnRects[0] = columnRects[0]!.Margins(0, difference, 0, 0, true);
							columnRects[1] = columnRects[1]!.Margins(0, 0, 0, difference, false);
						}


						//Rectangle fieldRectTotal = rowRect.Margins(0, 0, 0, skills[i].GetWidth(canvas.fonts, fontsize) + columnSpacing, false);
						Rectangle fieldRectTotal = new Rectangle(totalNameRect.Right + columnSpacing, rowRect.Y, rowRect.Width - columnSpacing - totalNameRect.Width, rowRect.Height);
						canvas.MoveToRel(fieldRectTotal, 0, 0).LineToRel(fieldRectTotal, 1, 0).Stroke();

					}
					else {
						//fieldRect = rowRect;
						//canvas.SaveState().SetLineWidth(lineWidth).MoveToRel(rowRect, 0, 0).LineToRel(rowRect, 1, 0).Stroke().RestoreState();
						canvas.MoveToRel(rowRect, 0, 0).LineToRel(rowRect, 1, 0).Stroke();
						fieldName = $"{name}_Unassigned_{i - skills.Length + 1}";
						canvas.TextField(columnRects[0]!, fieldName + "_Name", null, TextFieldType.STRING, "", field.font, field.fontsize ?? 0f, false, false, Justification.LEFT); // TODO Tooltip?
					}

					canvas.MoveToRel(columnRects[1]!, 0, 0).LineToRel(columnRects[1]!, 1, 0).Stroke();

					canvas.TextField(columnRects[1]!.Margins(0, columnSpacing / 2, 0, columnSpacing / 2, false), fieldName, null, TextFieldType.STRING, "", field.font, field.fontsize ?? 0f, false, false, field.justification); // TODO Tooltip?

					//canvas.Rectangle(checkRect).Stroke();
					if (checkRect is not null) {
						checkBoxStyle.Draw(canvas, checkRect);
						canvas.CheckField(checkBoxStyle.RemainingRect(canvas, checkRect), fieldName + "_check", null, CheckType.CHECK); // TODO Tooltip?
					}
					else {
						canvas.LogError(this, $"No space for \"{fieldName}\" check field.");
					}
				}
			}

			canvas.RestoreState();
		}

		protected override float CalculateMinimumWidth(ISharpGraphicsState graphicsState, Size availableSpace) {
			float minWidth = skills.Select(s => graphicsState.GetWidth(spaceRegex.Replace(s, (RichString)""), fontsize)).Max();

			// TODO The width should account for the "?10pt?" spacers too

			return minWidth;
		}
	}

}