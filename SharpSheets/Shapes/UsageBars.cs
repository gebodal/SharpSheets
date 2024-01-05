using SharpSheets.Canvas;
using SharpSheets.Colors;
using SharpSheets.Exceptions;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;

namespace SharpSheets.Shapes {

	/// <summary>
	/// A simple usage bar style, using connected rectangular outlines for the
	/// entry and label areas. The entry areas can be emphasised, and the bar can
	/// be flipped horizontally. The width of the entry areas is determined by the
	/// height of the bar and the emphasis, if any.
	/// </summary>
	public class SimpleUsageBar : UsageBarBase {

		protected readonly bool flip;
		protected readonly float emphasis;
		protected readonly bool includeEmphasis;

		/// <summary>
		/// Constructor for SimpleUsageBar.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this bar.</param>
		/// <param name="flip">Flag to indicate that the bar should be flipped horizontally.
		/// By default, the label will be drawn on the right.</param>
		/// <param name="emphasis">The amount by which the entry outlines should extend beyond the
		/// label area outline, above and below. Depending on the <paramref name="includeEmphasis"/>
		/// flag, this may or may not be included in height calculations for the bar.</param>
		/// <param name="includeEmphasis">Flag to indicate that the emphasis should be included
		/// in calculations of the bar height.</param>
		/// <exception cref="SharpInitializationException"></exception>
		public SimpleUsageBar(float aspect, bool flip = false, UFloat emphasis = default, bool includeEmphasis = true) : base(aspect) {
			this.flip = flip;
			this.emphasis = emphasis;
			this.includeEmphasis = includeEmphasis;
		}

		protected void GetRects(Rectangle rect, out Rectangle barRect, out Rectangle firstEntry, out Rectangle secondEntry) {
			if (includeEmphasis) {
				rect = rect.Margins(emphasis, 0, false);
			}

			Dimension entryWidth = Dimension.FromPoints(rect.Height + 2 * emphasis);
			Dimension barWidth = Dimension.Single;
			Dimension[] widths = new Dimension[] { barWidth, entryWidth, entryWidth };
			Rectangle[] rects = ValidateRects(Divisions.Columns(rect, widths, 0f, false, Arrangement.FRONT, flip ? LayoutOrder.BACKWARD : LayoutOrder.FORWARD), "Could not get usage bar rects.");

			if (flip) {
				barRect = rects[0];
				firstEntry = rects[2];
				secondEntry = rects[1];
			}
			else {
				barRect = rects[0];
				firstEntry = rects[1];
				secondEntry = rects[2];
			}
			firstEntry = firstEntry.Margins(emphasis, 0, true);
			secondEntry = secondEntry.Margins(emphasis, 0, true);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			GetRects(rect, out Rectangle barRect, out Rectangle firstEntry, out Rectangle secondEntry);

			if (flip) {
				barRect = barRect.Margins(0, 0, 0, secondEntry.Width / 2, true);
			}
			else {
				barRect = barRect.Margins(0, firstEntry.Width / 2, 0, 0, true);
			}

			canvas.Rectangle(barRect).FillStroke();
			canvas.Rectangle(firstEntry).FillStroke();
			canvas.Rectangle(secondEntry).FillStroke();
		}

		protected override Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetRects(rect, out Rectangle barRect, out _, out _);
			return barRect.Margins(graphicsState.GetLineWidth() / 2, false);
		}

		protected override Rectangle GetFirstEntryRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetRects(rect, out _, out Rectangle firstEntryRect, out _);
			return firstEntryRect.Margins(graphicsState.GetLineWidth() / 2, false);
		}

		protected override Rectangle GetSecondEntryRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetRects(rect, out _, out _, out Rectangle secondEntryRect);
			return secondEntryRect.Margins(graphicsState.GetLineWidth() / 2, false);
		}
	}

	public class SlashedUsageBar : UsageBarBase {

		protected readonly IBar bar;
		protected readonly Color? slashColor;

		public SlashedUsageBar(IBar bar, float aspect, Color? slashColor = null) : base(aspect) {
			this.bar = bar;
			this.slashColor = slashColor;
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			bar.Draw(canvas, rect);

			canvas.SaveState();
			//ParagraphSpecification font = new ParagraphSpecification(bar.RemainingRect(canvas, rect).Height * 0.9f, 1f, 0f, 0f, 0f);
			float fontSize = bar.RemainingRect(canvas, rect).Height * 0.9f;
			canvas.SetTextColor(slashColor ?? canvas.GetMidtoneColor());
			canvas.SetTextFormatAndSize(TextFormat.REGULAR, fontSize);
			canvas.DrawText(GetSlotRects(canvas, rect)[1], "/", Justification.CENTRE, Alignment.CENTRE, TextHeightStrategy.AscentBaseline);
			canvas.RestoreState();
		}

		protected override Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.LabelRect(graphicsState, rect);
		}

		private Rectangle[] GetSlotRects(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle slotsRect = bar.RemainingRect(graphicsState, rect).MarginsRel(0f, 0.05f, false);
			float slashWidth = graphicsState.GetWidth("/", TextFormat.REGULAR, slotsRect.Height * 0.9f); // new FontSpecification(slotsRect.Height * 0.9f, 1f, 0f).GetWidth("/");
			Rectangle[] rects = ValidateRects(Divisions.Columns(slotsRect, new Dimension[] { Dimension.Single, Dimension.FromPoints(slashWidth), Dimension.Single }, slashWidth / 2, false, Arrangement.FRONT, LayoutOrder.FORWARD), $"Could not get {nameof(SlashedUsageBar)} bar rects.");
			return rects;
		}

		protected override Rectangle GetFirstEntryRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetSlotRects(graphicsState, rect)[0];
		}

		protected override Rectangle GetSecondEntryRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetSlotRects(graphicsState, rect)[2];
		}

	}
}