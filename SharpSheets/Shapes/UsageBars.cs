using SharpSheets.Canvas;
using SharpSheets.Colors;
using SharpSheets.Exceptions;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;
using SharpSheets.Parsing;

namespace SharpSheets.Shapes {
	
	public class SimpleUsageBar : UsageBarBase {

		protected readonly bool flip;
		protected readonly float emphasis;
		protected readonly bool includeEmphasis;

		public SimpleUsageBar(float aspect, bool flip = false, UFloat emphasis = default, bool includeEmphasis = true) : base(aspect) {
			this.flip = flip;
			this.emphasis = emphasis;
			this.includeEmphasis = includeEmphasis;
		}

		/// <summary>
		/// A simple usage bar style, using connected rectangular outlines for the
		/// entry and label areas. The entry areas can be emphasised, and the bar can
		/// be flipped horizontally. The width of the entry areas is determined by the
		/// height of the bar and the emphasis, if any.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this bar.</param>
		/// <param name="flip">Flag to indicate that the bar should be flipped horizontally.
		/// By default, the label will be drawn on the right.</param>
		/// <param name="emphasis">The amount by which the entry outlines should extend beyond the
		/// label area outline, above and below. Depending on the <paramref name="includeEmphasis"/>
		/// flag, this may or may not be included in height calculations for the bar.</param>
		/// <param name="includeEmphasis">Flag to indicate that the emphasis should be included
		/// in calculations of the bar height.</param>
		[FactoryBuilder(typeof(IUsageBar))]
		public static SimpleUsageBar Build(float aspect = -1f, bool flip = false, UFloat emphasis = default, bool includeEmphasis = true) {
			return new SimpleUsageBar(aspect, flip, emphasis, includeEmphasis);
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
		protected readonly UnitInterval leftFraction;
		protected readonly Margins entryTrim;
		protected readonly Color? slashColor;
		protected readonly string slash;
		protected readonly float? slashSize;

		public SlashedUsageBar(float aspect, IBar bar, UnitInterval leftFraction, Margins entryTrim = default, Color? slashColor = null, string slash = "/", float? slashSize = null) : base(aspect) {
			this.bar = bar;
			this.leftFraction = leftFraction;
			this.entryTrim = entryTrim;
			this.slashColor = slashColor;
			this.slash = slash;
			this.slashSize = slashSize;
		}

		/// <summary>
		/// This usage bar uses another bar style, and creates two entry slots by adding a separator string
		/// (by default a forward slash, '/') in the bar remaining area. There are settings for adjusting
		/// the separator string, color, and size, along with the relative size of the two entry slots.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this usage bar.</param>
		/// <param name="base">The base bar to decorate with a separator.</param>
		/// <param name="leftFraction">The fractional size (0-1) of the left entry slot, relative to the
		/// overall available area (after the slash width is accounted for).</param>
		/// <param name="entryTrim">A trim to apply to the base bar remaining area before calculating
		/// the slot areas.</param>
		/// <param name="slash">Text for the separator.</param>
		/// <param name="slashColor">An optional color for the separator string. If not provided, defaults
		/// to the current midtone color.</param>
		/// <param name="slashSize">Font size for the separator string.</param>
		[FactoryBuilder(typeof(IUsageBar))]
		public static SlashedUsageBar Build(float aspect = -1f,
				[Property(Example = "SimpleBar(flip: true, entryFraction: new SharpSheets.Utilities.UnitInterval(0.5f))")] IBar? @base = null,
				[Property(Default = "0.5")] UnitInterval? leftFraction = null, Margins entryTrim = default,
				string slash = "/", Color? slashColor = null, float? slashSize = null
			) {

			return new SlashedUsageBar(aspect, @base ?? new SimpleBar(aspect), leftFraction ?? new UnitInterval(0.5f), entryTrim, slashColor, slash, slashSize);
		}

		protected float GetSlashFontSize(ISharpGraphicsState graphicsState, Rectangle rect) {
			return slashSize ?? (bar.RemainingRect(graphicsState, rect).Height * 0.9f);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			bar.Draw(canvas, rect);

			canvas.SaveState();
			float slashFontSize = GetSlashFontSize(canvas, rect);
			canvas.SetTextColor(slashColor ?? canvas.GetMidtoneColor());
			canvas.SetTextFormatAndSize(TextFormat.REGULAR, slashFontSize);
			canvas.DrawText(GetSlotRects(canvas, rect, slashFontSize)[1], slash, Justification.CENTRE, Alignment.CENTRE, TextHeightStrategy.AscentBaseline);
			canvas.RestoreState();
		}

		protected override Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.LabelRect(graphicsState, rect);
		}

		private Rectangle[] GetSlotRects(ISharpGraphicsState graphicsState, Rectangle rect, float slashFontSize) {
			Rectangle slotsRect = bar.RemainingRect(graphicsState, rect).Margins(entryTrim, false);
			float slashWidth = graphicsState.GetWidth(slash, TextFormat.REGULAR, slashFontSize);
			Rectangle[] rects = ValidateRects(Divisions.Columns(slotsRect, new Dimension[] { Dimension.FromRelative(leftFraction.Value), Dimension.FromPoints(slashWidth), Dimension.FromRelative(1f - leftFraction.Value) }, slashWidth / 2, false, Arrangement.FRONT, LayoutOrder.FORWARD), $"Could not get {nameof(SlashedUsageBar)} bar rects.");
			return rects;
		}

		protected override Rectangle GetFirstEntryRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetSlotRects(graphicsState, rect, GetSlashFontSize(graphicsState, rect))[0];
		}

		protected override Rectangle GetSecondEntryRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetSlotRects(graphicsState, rect, GetSlashFontSize(graphicsState, rect))[2];
		}

	}

}
