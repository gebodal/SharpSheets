using SharpSheets.Utilities;
using SharpSheets.Layouts;
using System;
using SharpSheets.Parsing;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Colors;

namespace SharpSheets.Shapes {

	public class LabelDetails : ISharpArgsGrouping {
		public readonly float fontSize;
		public readonly (float x, float y) offset;
		public readonly TextFormat format;
		public readonly Justification justification;
		public readonly Alignment alignment;
		public readonly Color? color;

		public static readonly float FontSizeDefault = 4f;
		public static readonly (float x, float y) OffsetDefault = (0f, 1f);
		public static readonly TextFormat FormatDefault = TextFormat.REGULAR;
		public static readonly Justification JustificationDefault = Justification.CENTRE;
		public static readonly Alignment AlignmentDefault = Alignment.TOP;
		public static readonly Color? ColorDefault = null;

		/// <summary>
		/// Constructor for LabelDetails.
		/// </summary>
		/// <param name="fontSize">The fontsize for this text label.</param>
		/// <param name="offset" default="0,1" example="0.0,3.0">The offset for this text label,
		/// relative to its initial position based on <paramref name="justification"/> and
		/// <paramref name="alignment"/>.</param>
		/// <param name="format">Font format to use for the text label. This will use the appropriate
		/// font format from the current font selection.</param>
		/// <param name="justification">Horizontal justification for the text label, indicating if
		/// the text should be left, right, or centre justified.</param>
		/// <param name="alignment">Vertical alignment for the text label, indicating if the text
		/// should be top, bottom, or centre aligned.</param>
		/// <param name="color">Text color for this text label.</param>
		public LabelDetails(float fontSize = 4f, (float x,float y)? offset = null, TextFormat format = TextFormat.REGULAR, Justification justification = Justification.CENTRE, Alignment alignment = Alignment.TOP, Color? color = null) {
			this.fontSize = fontSize;
			this.offset = offset ?? (0f, 1f);
			this.format = format;
			this.justification = justification;
			this.alignment = alignment;
			this.color = color;
		}
	}

	public class LabelledBar : BarBase {

		public readonly IBar bar;
		protected readonly string? label;
		protected readonly LabelDetails labelDetails;
		protected readonly string? note;
		protected readonly LabelDetails noteDetails;

		public LabelledBar(IBar bar, string? label, LabelDetails? labelDetails, string? note, LabelDetails? noteDetails) : base(bar.Aspect) {
			this.bar = bar;
			this.label = label;
			this.labelDetails = labelDetails ?? new LabelDetails();
			this.note = note;
			this.noteDetails = noteDetails ?? new LabelDetails();
		}

		public float TopLabelHeight {
			get {
				float labelTopHeight = labelDetails.alignment == Alignment.BOTTOM ? 0 : labelDetails.fontSize + labelDetails.offset.y;
				labelTopHeight = string.IsNullOrWhiteSpace(label) ? 0 : labelTopHeight;
				float noteTopHeight = noteDetails.alignment == Alignment.BOTTOM ? 0 : noteDetails.fontSize + noteDetails.offset.y;
				noteTopHeight = string.IsNullOrWhiteSpace(note) ? 0 : noteTopHeight;
				return Math.Max(0, Math.Max(labelTopHeight, noteTopHeight));
			}
		}

		public float BottomLabelHeight {
			get {
				float labelBottomHeight = labelDetails.alignment == Alignment.BOTTOM ? labelDetails.fontSize + labelDetails.offset.y : 0;
				labelBottomHeight = string.IsNullOrWhiteSpace(label) ? 0 : labelBottomHeight;
				float noteBottomHeight = noteDetails.alignment == Alignment.BOTTOM ? noteDetails.fontSize + noteDetails.offset.y : 0;
				noteBottomHeight = string.IsNullOrWhiteSpace(note) ? 0 : noteBottomHeight;
				return Math.Max(0, Math.Max(labelBottomHeight, noteBottomHeight));
			}
		}

		public float LabelHeight { get { return TopLabelHeight + BottomLabelHeight; } }

		private Rectangle GetBarRect(Rectangle rect) {
			return rect.Margins(TopLabelHeight, 0, BottomLabelHeight, 0, false);
		}

		Rectangle GetLabelTextRect(LabelDetails definition, Rectangle entryRect, Rectangle fullRect) {
			if (definition.alignment == Alignment.BOTTOM) {
				return new Rectangle(entryRect.Left, fullRect.Bottom - Math.Min(0, definition.fontSize + definition.offset.y), entryRect.Width, definition.fontSize);
			}
			else {
				return new Rectangle(entryRect.Left, fullRect.Top - definition.fontSize + Math.Min(0, definition.fontSize + definition.offset.y), entryRect.Width, definition.fontSize);
			}
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {

			bar.Draw(canvas, GetBarRect(rect), out Rectangle barLabelRect, out Rectangle remainingRect);

			Alignment TextAlignment(LabelDetails definition) {
				return Alignment.BOTTOM;
				//if (definition.alignment == Alignment.BOTTOM) return Alignment.BOTTOM;
				//else return Alignment.TOP;
			}

			canvas.SaveState();
			if (!string.IsNullOrWhiteSpace(label)) {
				Rectangle labelRect = GetLabelTextRect(labelDetails, remainingRect, rect);
				canvas.SetTextFormatAndSize(labelDetails.format, labelDetails.fontSize).SetTextColor(labelDetails.color ?? canvas.GetMidtoneColor().Darken(0.6f));
				canvas.DrawText(labelRect, label, labelDetails.justification, TextAlignment(labelDetails), TextHeightStrategy.FontsizeBaseline, offset: (labelDetails.offset.x, 0f));
				//canvas.SetStrokeColor(Color.Blue).SetLineWidth(0.2f).Rectangle(labelRect).Stroke();
			}
			if (!string.IsNullOrWhiteSpace(note)) {
				Rectangle noteRect = GetLabelTextRect(noteDetails, barLabelRect, rect);
				canvas.SetTextFormatAndSize(noteDetails.format, noteDetails.fontSize).SetTextColor(noteDetails.color ?? canvas.GetMidtoneColor().Darken(0.6f));
				canvas.DrawText(noteRect, note, noteDetails.justification, TextAlignment(noteDetails), TextHeightStrategy.FontsizeBaseline, offset: (noteDetails.offset.x, 0f));
				//canvas.SetStrokeColor(Color.Blue).SetLineWidth(0.2f).Rectangle(noteRect).Stroke();
			}
			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.RemainingRect(graphicsState, GetBarRect(rect));
		}

		protected override Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.LabelRect(graphicsState, GetBarRect(rect));
		}
	}

	public class LabelledUsageBar : UsageBarBase {

		public readonly IUsageBar bar;
		protected readonly string? label1;
		protected readonly string? label2;
		protected readonly LabelDetails labelDetails;
		protected readonly string? note;
		protected readonly LabelDetails noteDetails;

		public LabelledUsageBar(IUsageBar bar, string? label1, string? label2, LabelDetails? labelDetails, string? note, LabelDetails? noteDetails) : base(bar.Aspect) {
			this.bar = bar;
			this.label1 = label1;
			this.label2 = label2;
			this.labelDetails = labelDetails ?? new LabelDetails();
			this.note = note;
			this.noteDetails = noteDetails ?? new LabelDetails();
		}

		public static LabelledUsageBar CreateWithLabels(IUsageBar bar, string label1, string label2, float fontSize, (float x, float y) offset, TextFormat format, Justification justification, Alignment alignment, Color? color) {
			LabelDetails labelsDefinition = new LabelDetails(fontSize, offset, format, justification, alignment, color);
			return new LabelledUsageBar(bar, label1, label2, labelsDefinition, null, null);
		}

		public float TopLabelHeight {
			get {
				float labelTopHeight = labelDetails.alignment == Alignment.BOTTOM ? 0 : labelDetails.fontSize + labelDetails.offset.y;
				labelTopHeight = (!string.IsNullOrWhiteSpace(label1) || !string.IsNullOrWhiteSpace(label2)) ? labelTopHeight : 0;
				float noteTopHeight = noteDetails.alignment == Alignment.BOTTOM ? 0 : noteDetails.fontSize + noteDetails.offset.y;
				noteTopHeight = string.IsNullOrWhiteSpace(note) ? 0 : noteTopHeight;
				return Math.Max(0, Math.Max(labelTopHeight, noteTopHeight));
			}
		}

		public float BottomLabelHeight {
			get {
				float labelBottomHeight = labelDetails.alignment == Alignment.BOTTOM ? labelDetails.fontSize + labelDetails.offset.y : 0;
				labelBottomHeight = (!string.IsNullOrWhiteSpace(label1) || !string.IsNullOrWhiteSpace(label2)) ? labelBottomHeight : 0;
				float noteBottomHeight = noteDetails.alignment == Alignment.BOTTOM ? noteDetails.fontSize + noteDetails.offset.y : 0;
				noteBottomHeight = string.IsNullOrWhiteSpace(note) ? 0 : noteBottomHeight;
				return Math.Max(0, Math.Max(labelBottomHeight, noteBottomHeight));
			}
		}

		public float LabelHeight { get { return TopLabelHeight + BottomLabelHeight; } }

		private Rectangle GetBarRect(Rectangle rect) {
			return rect.Margins(TopLabelHeight, 0, BottomLabelHeight, 0, false);
		}

		private Rectangle GetLabelTextRect(LabelDetails definition, Rectangle entryRect, Rectangle fullRect) {
			if (definition.alignment == Alignment.BOTTOM) {
				return new Rectangle(entryRect.Left, fullRect.Bottom - Math.Min(0, definition.fontSize + definition.offset.y), entryRect.Width, definition.fontSize);
			}
			else {
				return new Rectangle(entryRect.Left, fullRect.Top - definition.fontSize + Math.Min(0, definition.fontSize + definition.offset.y), entryRect.Width, definition.fontSize);
			}
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {

			bar.Draw(canvas, GetBarRect(rect), out Rectangle barRect, out Rectangle firstEntryRect, out Rectangle secondEntryRect);

			Alignment TextAlignment(LabelDetails definition) {
				return Alignment.BOTTOM;
				//if (definition.alignment == Alignment.BOTTOM) return Alignment.BOTTOM;
				//else return Alignment.TOP;
			}

			canvas.SaveState();

			if(!string.IsNullOrWhiteSpace(label1)) {
				canvas.SetTextFormatAndSize(labelDetails.format, labelDetails.fontSize).SetTextColor(labelDetails.color ?? canvas.GetMidtoneColor().Darken(0.6f));
				Rectangle label1Rect = GetLabelTextRect(labelDetails, firstEntryRect, rect);
				canvas.DrawText(label1Rect, label1, labelDetails.justification, TextAlignment(labelDetails), TextHeightStrategy.FontsizeBaseline, offset: (labelDetails.offset.x, 0f));
			}
			if (!string.IsNullOrWhiteSpace(label2)) {
				canvas.SetTextFormatAndSize(labelDetails.format, labelDetails.fontSize).SetTextColor(labelDetails.color ?? canvas.GetMidtoneColor().Darken(0.6f));
				Rectangle label2Rect = GetLabelTextRect(labelDetails, secondEntryRect, rect);
				canvas.DrawText(label2Rect, label2, labelDetails.justification, TextAlignment(labelDetails), TextHeightStrategy.FontsizeBaseline, offset: (labelDetails.offset.x, 0f));
			}

			if (!string.IsNullOrWhiteSpace(note)) {
				Rectangle noteRect = GetLabelTextRect(noteDetails, barRect, rect);
				canvas.SetTextFormatAndSize(noteDetails.format, noteDetails.fontSize).SetTextColor(noteDetails.color ?? canvas.GetMidtoneColor().Darken(0.6f));
				canvas.DrawText(noteRect, note, noteDetails.justification, TextAlignment(noteDetails), TextHeightStrategy.FontsizeBaseline, offset: (noteDetails.offset.x, 0f));
				//canvas.SetStrokeColor(Color.Blue).SetLineWidth(0.2f).Rectangle(noteRect).Stroke();
			}

			canvas.RestoreState();
		}

		protected override Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.LabelRect(graphicsState, GetBarRect(rect));
		}

		protected override Rectangle GetFirstEntryRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.FirstEntryRect(graphicsState, GetBarRect(rect));
		}

		protected override Rectangle GetSecondEntryRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.SecondEntryRect(graphicsState, GetBarRect(rect));
		}
	}

}