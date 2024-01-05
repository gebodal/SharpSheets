using SharpSheets.Canvas;
using SharpSheets.Exceptions;
using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using System.Linq;

namespace SharpSheets.Shapes {

	public abstract class AbstractSimpleBar : BarBase {

		protected readonly bool flip;
		protected readonly UnitInterval? entryFraction;
		protected readonly UFloat emphasis;
		protected readonly bool includeEmphasis;

		public AbstractSimpleBar(float aspect, bool flip, UnitInterval? entryFraction, UFloat emphasis, bool includeEmphasis) : base(aspect) {
			this.flip = flip;
			this.entryFraction = entryFraction;
			this.emphasis = emphasis;
			this.includeEmphasis = includeEmphasis;
		}

		protected void GetRects(Rectangle rect, out Rectangle barRect, out Rectangle entryRect) {
			if (includeEmphasis) {
				rect = rect.Margins(emphasis, 0, false);
			}

			Dimension entryWidth = entryFraction == null ? Dimension.FromPoints(rect.Height + 2 * emphasis) : Dimension.FromRelative(((float)entryFraction.Value).Clamp(0f, 1f));
			Dimension barWidth = entryFraction == null ? Dimension.Single : Dimension.FromRelative(1 - ((float)entryFraction.Value).Clamp(0f, 1f));
			Dimension[] widths = new Dimension[] { barWidth, entryWidth };
			if (flip) { widths = widths.Reverse().ToArray(); }
			Rectangle[] rects = ValidateRects(Divisions.Columns(rect, widths, 0f, false, Arrangement.FRONT, LayoutOrder.BACKWARD), "Could not get bar rects.");

			if (flip) {
				entryRect = rects[0];
				barRect = rects[1];
			}
			else {
				barRect = rects[0];
				entryRect = rects[1];
			}
			entryRect = entryRect.Margins(emphasis, 0, true);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			GetRects(rect, out Rectangle barRect, out Rectangle entryRect);

			if (flip) {
				barRect = barRect.Margins(0, entryRect.Width / 2, 0, 0, true);
			}
			else {
				barRect = barRect.Margins(0, 0, 0, entryRect.Width / 2, true);
			}

			canvas.Rectangle(barRect).FillStroke();
			DrawEntryRect(canvas, entryRect);
		}

		protected abstract void DrawEntryRect(ISharpCanvas canvas, Rectangle entryRect);

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetRects(rect, out _, out Rectangle entryRect);
			return entryRect.Margins(graphicsState.GetLineWidth() / 2, false);
		}

		protected override Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetRects(rect, out Rectangle barRect, out _);
			return barRect.Margins(graphicsState.GetLineWidth() / 2, false);
		}
	}

	/// <summary>
	/// A simple bar using a rectangular outline. The relative sizes of the label and entry areas
	/// can be controlled, the entry area can be emphasised, and the bar can be flipped horizontally.
	/// </summary>
	public class SimpleBar : AbstractSimpleBar {

		/// <summary>
		/// Constructor for SimpleBar.
		/// </summary>
		/// <param name="aspect">The aspect ratio for this bar.</param>
		/// <param name="flip">Flag to indicate that the bar should be flipped horizontally.
		/// By default, the label will be drawn on the right.</param>
		/// <param name="entryFraction">Fraction of the bar width which should be given to the
		/// entry.</param>
		/// <param name="emphasis">The amount by which the entry outline should extend beyond the
		/// label area outline, above and below. Depending on the <paramref name="includeEmphasis"/>
		/// flag, this may or may not be included in height calculations for the bar.</param>
		/// <param name="includeEmphasis">Flag to indicate that the emphasis should be included
		/// in calculations of the bar height.</param>
		public SimpleBar(float aspect, bool flip = false, UnitInterval? entryFraction = null, UFloat emphasis = default, bool includeEmphasis = true) : base(aspect, flip, entryFraction, emphasis, includeEmphasis) { }
		
		protected override void DrawEntryRect(ISharpCanvas canvas, Rectangle entryRect) {
			canvas.Rectangle(entryRect).FillStroke();
		}

	}

	/// <summary>
	/// A simple bar using a rectangular outline for the label area and an elliptical outline for the
	/// entry area. The relative sizes of the label and entry areas can be controlled, the entry area
	/// can be emphasised, and the bar can be flipped horizontally.
	/// </summary>
	public class EllipseBar : AbstractSimpleBar {

		/// <summary>
		/// Constructor for EllipseBar.
		/// </summary>
		/// <param name="aspect">The aspect ratio for this bar.</param>
		/// <param name="flip">Flag to indicate that the bar should be flipped horizontally.
		/// By default, the label will be drawn on the right.</param>
		/// <param name="entryFraction">Fraction of the bar width which should be given to the
		/// entry.</param>
		/// <param name="emphasis">The amount by which the entry outline should extend beyond the
		/// label area outline, above and below. Depending on the <paramref name="includeEmphasis"/>
		/// flag, this may or may not be included in height calculations for the bar.</param>
		/// <param name="includeEmphasis">Flag to indicate that the emphasis should be included
		/// in calculations of the bar height.</param>
		public EllipseBar(float aspect, bool flip = false, UnitInterval? entryFraction = null, UFloat emphasis = default, bool includeEmphasis = true) : base(aspect, flip, entryFraction, emphasis, includeEmphasis) { }
		
		protected override void DrawEntryRect(ISharpCanvas canvas, Rectangle entryRect) {
			canvas.EllipseFrom(entryRect).FillStroke();
		}

	}

	/// <summary>
	/// A bar using two box styles for the label and entry areas. The relative sizes of the label
	/// and entry areas can be controlled, the entry area can be emphasised, and the bar can be
	/// flipped horizontally.
	/// </summary>
	public class BoxBar : BarBase {

		protected IBox barBox;
		protected IBox entryBox;

		protected readonly float? entryfraction;
		protected readonly float emphasis;
		protected readonly bool includeEmphasis;
		protected readonly bool flip;

		/// <summary>
		/// Constructor for BarBox.
		/// </summary>
		/// <param name="aspect">The aspect ratio for this bar.</param>
		/// <param name="barBox">Box style to use for the label area outline. This outline will
		/// extend past the edge of the entry area, with the intention that it appears to protrude
		/// from the entry outline.</param>
		/// <param name="entryBox">Box style to use for the entry are outline.</param>
		/// <param name="entryfraction">Fraction of the bar width which should be given to the
		/// entry.</param>
		/// <param name="emphasis">The amount by which the entry outline should extend beyond the
		/// label area outline, above and below. Depending on the <paramref name="includeEmphasis"/>
		/// flag, this may or may not be included in height calculations for the bar.</param>
		/// <param name="includeEmphasis">Flag to indicate that the emphasis should be included
		/// in calculations of the bar height.</param>
		/// <param name="flip">Flag to indicate that the bar should be flipped horizontally.
		/// By default, the label will be drawn on the right.</param>
		public BoxBar(float aspect, IBox? barBox = null, IBox? entryBox = null, float? entryfraction = null, float emphasis = 0f, bool includeEmphasis = false, bool flip = false) : base(aspect) {
			this.barBox = barBox ?? new Simple(-1f);
			this.entryBox = entryBox ?? new Simple(-1f);
			this.entryfraction = entryfraction;
			this.emphasis = emphasis;
			this.includeEmphasis = includeEmphasis;
			this.flip = flip;
		}

		protected void GetRects(Rectangle rect, out Rectangle barRect, out Rectangle entryRect) {

			if (!includeEmphasis) {
				rect = rect.Margins(emphasis, 0, true);
			}

			float entryWidth = entryfraction != null ? rect.Width * entryfraction.Value : rect.Height;

			if (flip) {
				entryRect = new Rectangle(rect.Right - entryWidth, rect.Y, entryWidth, rect.Height);
			}
			else {
				entryRect = new Rectangle(rect.X, rect.Y, entryWidth, rect.Height);
			}

			barRect = rect.Margins(emphasis, 0, false);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			GetRects(rect, out Rectangle barRect, out Rectangle entryRect);

			canvas.SaveState();
			if (flip) {
				canvas.Rectangle(barRect.Margins(-rect.Height, entryRect.Width / 2, -rect.Height, -entryRect.Width, false));
			}
			else {
				canvas.Rectangle(barRect.Margins(-rect.Height, -entryRect.Width, -rect.Height, entryRect.Width / 2, false));
			}
			canvas.Clip().EndPath();
			barBox.Draw(canvas, barRect);
			canvas.RestoreState();

			entryBox.Draw(canvas, entryRect);
		}

		protected override Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetRects(rect, out Rectangle barRect, out Rectangle entryRect);
			Rectangle barRemaining = barBox.RemainingRect(graphicsState, barRect);
			if (flip) {
				return barRemaining.Margins(0, entryRect.Width, 0, 0, false);
			}
			else {
				return barRemaining.Margins(0, 0, 0, entryRect.Width, false);
			}
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetRects(rect, out _, out Rectangle entryRect);
			return entryBox.RemainingRect(graphicsState, entryRect);
		}
	}

}