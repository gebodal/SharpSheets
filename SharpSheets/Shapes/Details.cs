using SharpSheets.Canvas;
using SharpSheets.Layouts;
using SharpSheets.Colors;

namespace SharpSheets.Shapes {

	/// <summary>
	/// A detail that draws nothing to the page. A useful default.
	/// </summary>
	public class Blank : DetailBase {

		/// <summary>
		/// Constructor for Blank.
		/// </summary>
		public Blank() { }

		public override void Draw(ISharpCanvas canvas, Rectangle rect) { }
	}

	/// <summary>
	/// A detail that draws a centred line in the detail area, oriented appropriately
	/// to the detail layout. The width and color of the line can be controlled separately,
	/// and may be offset from the ends of the detail area.
	/// </summary>
	public class LineDetail : DetailBase {

		protected readonly float offset;
		protected readonly float? width;
		protected readonly Color? color;

		/// <summary>
		/// Constructor for LineDetail.
		/// </summary>
		/// <param name="offset">An offset for the start and end of the line from the ends
		/// of the detail area.</param>
		/// <param name="width">The linewidth for the detail line. If no value is provided,
		/// the current linewidth will be used.</param>
		/// <param name="color">A color for the detail line. If no value is provided, the
		/// current foreground color will be used.</param>
		public LineDetail(float offset = 5f, float? width = null, Color? color = null) {
			this.offset = offset;
			this.width = width;
			this.color = color;
		}

		public override void Draw(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			if (color != null) {
				canvas.SetStrokeColor(color.Value);
			}
			if (width.HasValue) {
				canvas.SetLineWidth(width.Value);
			}

			if (this.Layout == Layout.COLUMNS) {
				canvas.MoveTo(rect.CentreX, rect.Bottom + offset).LineTo(rect.CentreX, rect.Top - offset);
			}
			else {
				canvas.MoveTo(rect.Left + offset, rect.CentreY).LineTo(rect.Right - offset, rect.CentreY);
			}

			canvas.Stroke();

			canvas.RestoreState();
		}
	}

	/// <summary>
	/// A detail that fills the detail area with a block color.
	/// </summary>
	public class FilledDetail : DetailBase {

		protected readonly Color? color;

		/// <summary>
		/// Constructor for FilledDetail.
		/// </summary>
		/// <param name="color">The color to fill the detail area with. If no
		/// value is provided, the current midtone color will be used.</param>
		public FilledDetail(Color? color = null) {
			this.color = color;
		}

		public override void Draw(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();
			canvas.SetFillColor(color ?? canvas.GetMidtoneColor());
			canvas.Rectangle(rect).Fill();
			canvas.RestoreState();
		}
	}

}