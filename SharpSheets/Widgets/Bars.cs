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
	/*
	public class BarOld : SharpWidget {

		public class NameDetails : ISharpArgs {
			public readonly float fontSize;
			public readonly (float x, float y) offset;
			public readonly TextFormat format;
			public readonly Justification justification;
			public readonly Alignment alignment;
			public readonly TextHeightStrategy heightStrategy;
			public readonly Color? color;

			public NameDetails(float fontSize = 6f, (float x, float y) offset = default, TextFormat format = TextFormat.REGULAR, Justification justification = Justification.CENTRE, Alignment alignment = Alignment.CENTRE, TextHeightStrategy heightStrategy = TextHeightStrategy.AscentDescent, Color? color = null) {
				this.fontSize = fontSize;
				this.offset = offset;
				this.format = format;
				this.justification = justification;
				this.alignment = alignment;
				this.heightStrategy = heightStrategy;
				this.color = color;
			}
		}

		protected readonly string name;
		protected readonly string? tooltip;

		protected readonly IBar bar;
		protected readonly Dimension height;
		protected readonly NameDetails nameDetails;
		protected readonly CheckType? checkType;
		protected readonly bool rich;

		protected readonly Div? content;
		protected readonly Div? entry;

		/// <summary>
		/// Constructor for Bar widget.
		/// </summary>
		/// <param name="setup">Widget setup data.</param>
		/// <param name="name" example="Bar Name">Name to be printed on this bar, and used for field.</param>
		/// <param name="tooltip" example="Tooltip text.">Text to be used in field tooltip.</param>
		/// <param name="bar">Bar style to use for the widget.</param>
		/// <param name="height"></param>
		/// <param name="name_"></param>
		/// <param name="label" example="Label"></param>
		/// <param name="label_"></param>
		/// <param name="note" example="Note"></param>
		/// <param name="note_"></param>
		/// <param name="check"></param>
		/// <param name="rich"></param>
		/// <param name="content"></param>
		/// <param name="entry"></param>
		/// <size>100 20</size>
		public BarOld(
				WidgetSetup setup,
				string? name = null,
				string? tooltip = null,
				IBar? bar = null,
				Dimension? height = default,
				NameDetails? name_ = null,
				string? label = null, // TODO RichString?
				LabelDetails? label_ = null,
				string? note = null, // TODO RichString?
				LabelDetails? note_ = null,
				CheckType? check = null,
				bool rich = false,
				ChildHolder? content = null,
				ChildHolder? entry = null
			) : base(setup) {

			this.name = name ?? "Bar";

			this.tooltip = tooltip;

			this.bar = bar ?? new SimpleBar(-1);
			this.height = height ?? Dimension.FromRelative(1);

			if (label != null || note != null) {
				LabelledBar labelledBar = new LabelledBar(this.bar, label, label_, note, note_);
				this.height += Dimension.FromPoints(labelledBar.LabelHeight);
				this.bar = labelledBar;
			}

			this.nameDetails = name_ ?? new NameDetails();
			this.checkType = check;
			this.rich = rich;

			this.content = content?.Child;
			this.entry = entry?.Child;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			//Console.WriteLine("Found bar.");

			bar.Draw(canvas, rect, out Rectangle labelRect, out Rectangle entryRect);

			if (content != null) {
				content.Draw(canvas, labelRect, default);
			}
			else {
				canvas.SaveState();

				canvas.SetTextFormatAndSize(nameDetails.format, nameDetails.fontSize);
				if (nameDetails.color.HasValue) {
					canvas.SetTextColor(nameDetails.color.Value);
				}

				canvas.DrawText(new Rectangle(labelRect.X + nameDetails.offset.x, labelRect.Y + nameDetails.offset.y, labelRect.Width, labelRect.Height), name, nameDetails.justification, nameDetails.alignment, nameDetails.heightStrategy); // localName?

				canvas.RestoreState();
			}

			if (entry != null) {
				entry.Draw(canvas, entryRect, default);
			}
			else {
				if (checkType.HasValue) {
					canvas.CheckField(entryRect, name, tooltip, checkType.Value);
				}
				else {
					canvas.TextField(entryRect, name, tooltip, TextFieldType.STRING, "", TextFormat.REGULAR, 0f, false, rich, Justification.CENTRE);
				}
			}
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			// TODO We should try and take account of content and entry rects here, right?
			return new Size(0f, height.Absolute);
		}

		protected override Rectangle[] GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle rect) {
			return new Rectangle[] { bar.LabelRect(graphicsState, rect), bar.RemainingRect(graphicsState, rect) };
		}
	}

	public class BarsOld : SharpWidget {

		protected readonly IBar[] bars;
		protected readonly string[] barNames;
		protected readonly Dimension[] barHeights;
		protected readonly float fontSize;
		protected readonly TextFormat format;
		protected readonly Justification justification;
		protected readonly Alignment alignment;
		protected readonly bool rich;

		protected readonly CheckType[] checkTypes;
		protected readonly bool check;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="setup"></param>
		/// <param name="bar"></param>
		/// <param name="_name" example="Bar 1,Bar 2"></param>
		/// <param name="allLabelled"></param>
		/// <param name="height"></param>
		/// <param name="label" example="Label"></param>
		/// <param name="label_"></param>
		/// <param name="note" example="Note"></param>
		/// <param name="note_"></param>
		/// <param name="fontSize"></param>
		/// <param name="justification"></param>
		/// <param name="alignment"></param>
		/// <param name="check"></param>
		/// <param name="check_"></param>
		/// <param name="format"></param>
		/// <param name="rich"></param>
		/// <size>100 50</size>
		public BarsOld(
				WidgetSetup setup,
				IBar? bar = null,
				string[]? _name = null, // TODO RichString?
				bool allLabelled = false,
				Dimension? height = default,
				string? label = null, // TODO RichString?
				LabelDetails? label_ = null,
				string? note = null, // TODO RichString?
				LabelDetails? note_ = null,
				float fontSize = 6f,
				TextFormat format = TextFormat.REGULAR,
				Justification justification = Justification.CENTRE,
				Alignment alignment = Alignment.CENTRE,
				CheckType[]? check = null,
				bool check_ = false,
				bool rich = false
			) : base(setup) {

			barNames = _name ?? Array.Empty<string>(); // name.SplitAndTrim(';');

			Dimension barHeight = height ?? Dimension.Single; // height.IsZero ? 1f : height; // config.GetProperty("height", Dimension.FromRelative(1f), Dimension.Parse);

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

			this.fontSize = fontSize;
			this.format = format;
			this.justification = justification;
			this.alignment = alignment;
			this.rich = rich;

			this.check = check_ || check != null;
			this.checkTypes = (check == null || check.Length == 0) ? new CheckType[] { CheckType.CROSS } : check;

			if (checkTypes.Length != bars.Length) {
				CheckType[] checkMarks = new CheckType[bars.Length];
				for (int i = 0; i < bars.Length; i++) {
					checkMarks[i] = this.checkTypes[Math.Min(i, this.checkTypes.Length - 1)];
				}
				this.checkTypes = checkMarks;
			}
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			//Console.WriteLine("Found bar.");

			Rectangle?[] barRects = Divisions.Rows(rect, barHeights, Gutter, false, Arrangement.FRONT, LayoutOrder.FORWARD, DivisionStrategy.RELATIVE_RECTANGLES);

			for (int i = 0; i < barRects.Length; i++) {
				if (barRects[i] != null) {
					bars[i].Draw(canvas, barRects[i]!, out Rectangle labelRect, out Rectangle entryRect);

					canvas.SaveState();

					canvas.SetTextFormatAndSize(format, fontSize).SetFillColor(canvas.GetTextColor());

					//float nameWidth = canvas.GetWidth(barNames[i]);
					//float nameHeight = canvas.GetAscent(barNames[i]);

					//canvas.DrawText(barNames[i], labelRect.CentreX - (nameWidth / 2), labelRect.CentreY - (nameHeight / 2));
					canvas.DrawText(labelRect, barNames[i], justification, alignment, TextHeightStrategy.AscentBaseline);

					canvas.RestoreState();

					if (check) {
						canvas.CheckField(entryRect, barNames[i], null, checkTypes[i]); // TODO Tooltip?
					}
					else {
						canvas.TextField(entryRect, barNames[i], null, TextFieldType.STRING, "", TextFormat.REGULAR, 0f, false, rich, Justification.CENTRE); // TODO Tooltip?
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
			Rectangle[] labelRects = barRects.Zip(bars, (r, b) => r is not null ? b.LabelRect(graphicsState, r) : null).WhereNotNull().ToArray();
			Rectangle[] remainingRects = barRects.Zip(bars, (r, b) => r is not null ? b.RemainingRect(graphicsState, r) : null).WhereNotNull().ToArray();
			return labelRects.Concat(remainingRects).ToArray();
		}
	}

	public class SlotsBarOld : SharpWidget {

		protected readonly string name;
		protected readonly string localName;

		protected readonly IUsageBar bar;
		protected readonly TextFormat format;
		protected readonly bool rich;
		protected readonly float fontSize;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="setup"></param>
		/// <param name="name" example="Bar"></param>
		/// <param name="_name" example="Bar"></param>
		/// <param name="bar"></param>
		/// <param name="labels" example="Label 1,Label 2"></param>
		/// <param name="labels_"></param>
		/// <param name="note" example="Note"></param>
		/// <param name="note_"></param>
		/// <param name="fontSize"></param>
		/// <param name="format"></param>
		/// <param name="rich"></param>
		/// /// <size>100 25</size>
		public SlotsBarOld(
				WidgetSetup setup,
				string? name = null,
				string? _name = null,
				IUsageBar? bar = null,
				// TODO Height?
				string[]? labels = null, // TODO RichString?
				LabelDetails? labels_ = null,
				string? note = null, // TODO RichString?
				LabelDetails? note_ = null,
				float fontSize = 6f,
				TextFormat format = TextFormat.REGULAR,
				bool rich = false
			) : base(setup) {

			this.name = name ?? "SlotsBar";
			this.localName = _name ?? this.name;

			this.bar = bar ?? new SimpleUsageBar(-1);

			if (labels != null || note != null) {
				// TODO labels should probably be a Tuple "(string?, string?)"
				string? label1 = labels != null && labels.Length > 0 ? labels[0] : null;
				string? label2 = labels != null && labels.Length > 1 ? labels[1] : null;

				LabelledUsageBar labelledBar = new LabelledUsageBar(this.bar, label1, label2, labels_, note, note_);
				//this.height += Dimension.FromPoints(labelledBar.LabelHeight);
				this.bar = labelledBar;
			}

			this.fontSize = fontSize;
			this.format = format;
			this.rich = rich;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			//Console.WriteLine("Found slots bar.");

			bar.Draw(canvas, rect, out Rectangle labelRect, out Rectangle firstEntryRect, out Rectangle secondEntryRect);

			canvas.SaveState();
			canvas.SetTextFormatAndSize(format, fontSize).SetFillColor(canvas.GetTextColor());

			float nameWidth = canvas.GetWidth(localName);
			float nameHeight = canvas.GetAscent(localName);

			canvas.DrawText(localName, labelRect.CentreX - (nameWidth / 2), labelRect.CentreY - (nameHeight / 2));
			canvas.RestoreState();

			// TODO Tooltips? Could be based on labels, if available?
			canvas.TextField(firstEntryRect, $"slotsleft_{name}", null, TextFieldType.STRING, "", TextFormat.REGULAR, 0, false, rich, Justification.CENTRE);
			canvas.TextField(secondEntryRect, $"slotsright_{name}", null, TextFieldType.STRING, "", TextFormat.REGULAR, 0, false, rich, Justification.CENTRE);
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}

		protected override Rectangle[] GetDiagnosticRects(ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.EntryRects(graphicsState, rect);
		}
	}

	public class SlotsBarsOld : SharpWidget {

		protected readonly IUsageBar[] bars;
		protected readonly string[] barNames;
		protected readonly Dimension[] barHeights;
		protected readonly TextFormat format;
		protected readonly bool rich;
		protected readonly float fontSize;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="setup"></param>
		/// <param name="bar"></param>
		/// <param name="_name" example="Bar 1,Bar2"></param>
		/// <param name="allLabelled"></param>
		/// <param name="height"></param>
		/// <param name="labels" example="Label 1,Label 2"></param>
		/// <param name="labels_"></param>
		/// <param name="note" example="Note"></param>
		/// <param name="note_"></param>
		/// <param name="fontSize"></param>
		/// <param name="format"></param>
		/// <param name="rich"></param>
		/// /// <size>100 50</size>
		public SlotsBarsOld(
				WidgetSetup setup,
				IUsageBar? bar = null,
				string[]? _name = null,
				bool allLabelled = false,
				Dimension? height = default,
				string[]? labels = null, // TODO RichString?
				LabelDetails? labels_ = null,
				string? note = null, // TODO RichString?
				LabelDetails? note_ = null,
				float fontSize = 6f,
				TextFormat format = TextFormat.REGULAR,
				bool rich = false
			) : base(setup) {

			barNames = _name ?? Array.Empty<string>(); // name.SplitAndTrim(';');

			Dimension barHeight = height ?? Dimension.Single; // height.IsZero ? 1f : height; // config.GetProperty("height", Dimension.FromRelative(1f), Dimension.Parse);

			barHeights = ArrayUtils.MakeArray(barNames.Length, barHeight);

			bar ??= new SimpleUsageBar(-1);
			if (labels != null || note != null) {
				string? label1 = labels != null && labels.Length > 0 ? labels[0] : null;
				string? label2 = labels != null && labels.Length > 1 ? labels[1] : null;

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

			this.fontSize = fontSize;
			this.format = format;
			this.rich = rich;
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			//Console.WriteLine("Found slots bar.");

			Rectangle?[] barRects = Divisions.Rows(rect, barHeights, Gutter, false, Arrangement.FRONT, LayoutOrder.FORWARD, DivisionStrategy.RELATIVE_RECTANGLES);
			for (int i = 0; i < barNames.Length; i++) {
				if (barRects[i] is not null) {
					bars[i].Draw(canvas, barRects[i]!, out Rectangle labelRect, out Rectangle firstEntryRect, out Rectangle secondEntryRect);

					canvas.SaveState();
					canvas.SetTextFormatAndSize(format, fontSize).SetFillColor(canvas.GetTextColor());

					float nameWidth = canvas.GetWidth(barNames[i]);
					float nameHeight = canvas.GetAscent(barNames[i]);

					canvas.DrawText(barNames[i], labelRect.CentreX - (nameWidth / 2), labelRect.CentreY - (nameHeight / 2));
					canvas.RestoreState();

					canvas.TextField(firstEntryRect, $"slotstotal_{barNames[i]}", null, TextFieldType.STRING, "", TextFormat.REGULAR, 0, false, rich, Justification.CENTRE); // TODO Tooltip?
					canvas.TextField(secondEntryRect, $"slotsremaining_{barNames[i]}", null, TextFieldType.STRING, "", TextFormat.REGULAR, 0, false, rich, Justification.CENTRE); // TODO Tooltip?
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

	*/
}
