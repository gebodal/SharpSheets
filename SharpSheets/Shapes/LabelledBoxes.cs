using SharpSheets.Utilities;
using System;
using SharpSheets.Layouts;
using SharpSheets.Colors;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Exceptions;

namespace SharpSheets.Shapes {

	/// <summary>
	/// A simple labelled box that uses connected rectangular outlines for the remaining
	/// and label areas. The size and placement of the label area around the outside of
	/// the shape can be specified, along with margins for each of the areas separately.
	/// </summary>
	public class SimpleLabelledBox : LabelledBoxBase {

		protected readonly Direction placement;
		protected readonly float labelSize;
		protected readonly Margins labelTrim;
		protected readonly Margins boxTrim;

		/// <summary>
		/// Constructor for SimpleLabelledBox.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="placement">The placement of the label area around the shape area,
		/// as a cardinal direction.</param>
		/// <param name="labelSize">The size of the label area, measured from the edge of the
		/// shape area, in points.</param>
		/// <param name="labelTrim">Padding for the inside of the label area.</param>
		/// <param name="boxTrim">Padding for the inside of the remaining area.</param>
		public SimpleLabelledBox(float aspect, Direction placement = Direction.SOUTH, float labelSize = 10f, Margins labelTrim = default, Margins boxTrim = default) : base(aspect) {
			this.placement = placement;
			this.labelSize = labelSize;
			this.labelTrim = labelTrim;
			this.boxTrim = boxTrim;
		}

		private Size ProxyLabelSize() {
			return new Size(labelSize, labelSize).Margins(labelTrim, true);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			canvas.SetFillColor(canvas.GetBackgroundColor());
			canvas.SetStrokeColor(canvas.GetForegroundColor());

			canvas.Rectangle(rect).FillStroke();

			Size proxyLabelSize = ProxyLabelSize();

			if (placement == Direction.NORTH || placement == Direction.SOUTH) {
				float yPos = placement == Direction.NORTH ? rect.Top - proxyLabelSize.Height : rect.Bottom + proxyLabelSize.Height;

				canvas.MoveTo(rect.Left, yPos).LineTo(rect.Right, yPos).Stroke();
			}
			else { // placement == Direction.EAST || placement == Direction.WEST
				float xPos = placement == Direction.EAST ? rect.Right - proxyLabelSize.Width : rect.Left + proxyLabelSize.Width;

				canvas.MoveTo(xPos, rect.Bottom).LineTo(xPos, rect.Top).Stroke();
			}

			//canvas.Rectangle(rect).Stroke();

			canvas.RestoreState();
		}

		protected override Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle labelRect;
			Size proxyLabelSize = ProxyLabelSize();
			if (placement == Direction.NORTH) {
				labelRect = new Rectangle(rect.X, rect.Top - proxyLabelSize.Height, rect.Width, proxyLabelSize.Height);
			}
			else if (placement == Direction.SOUTH) {
				labelRect = new Rectangle(rect.X, rect.Bottom, rect.Width, proxyLabelSize.Height);
			}
			else if (placement == Direction.EAST) {
				labelRect = new Rectangle(rect.Right - proxyLabelSize.Width, rect.Bottom, proxyLabelSize.Width, rect.Height);
			}
			else { // placement == Direction.WEST
				labelRect = new Rectangle(rect.Left, rect.Bottom, proxyLabelSize.Width, rect.Height);
			}
			return labelRect.Margins(labelTrim, false);
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle remainingRect;
			Size proxyLabelSize = ProxyLabelSize();
			if (placement == Direction.NORTH) {
				remainingRect = rect.Margins(proxyLabelSize.Height, 0f, 0f, 0f, false);
			}
			else if (placement == Direction.SOUTH) {
				remainingRect = rect.Margins(0f, 0f, proxyLabelSize.Height, 0f, false);
			}
			else if (placement == Direction.EAST) {
				remainingRect = rect.Margins(0f, proxyLabelSize.Width, 0f, 0f, false);
			}
			else { // placement == Direction.WEST
				remainingRect = rect.Margins(0f, 0f, 0f, proxyLabelSize.Width, false);
			}
			return remainingRect.Margins(boxTrim, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			rect = rect.Margins(boxTrim, true);

			Rectangle fullRect;
			Size proxyLabelSize = ProxyLabelSize();
			if (placement == Direction.NORTH) {
				fullRect = rect.Margins(proxyLabelSize.Height, 0f, 0f, 0f, true);
			}
			else if (placement == Direction.SOUTH) {
				fullRect = rect.Margins(0f, 0f, proxyLabelSize.Height, 0f, true);
			}
			else if (placement == Direction.EAST) {
				fullRect = rect.Margins(0f, proxyLabelSize.Width, 0f, 0f, true);
			}
			else { // placement == Direction.WEST
				fullRect = rect.Margins(0f, 0f, 0f, proxyLabelSize.Width, true);
			}

			return fullRect;
		}
	}

	/// <summary>
	/// A simple statistic box style. A larger rectangular area with a small oval
	/// at the base, with some detailing.
	/// </summary>
	public class StatBoxSimple : LabelledBoxBase {

		protected readonly float bevel;

		/// <summary>
		/// Constructor for StatBoxSimple.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="bevel">A scaling for the details of the outline. This
		/// also affects the size of the remaining and label areas.</param>
		/// <size>50 65</size>
		public StatBoxSimple(float aspect, float bevel = 5f) : base(aspect) {
			this.bevel = bevel;
		}

		protected static void Measurements(float rectWidth, out float ellipseWidth, out float ellipseHeight, out float ellipseOffset, out float boxBaseOffset) {
			ellipseWidth = rectWidth / 1.75f;
			ellipseHeight = rectWidth * 0.4f;
			ellipseOffset = ellipseHeight / 5;
			boxBaseOffset = (ellipseHeight / 2) - ellipseOffset;
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			Measurements(rect.Width, out float ellipseWidth, out float ellipseHeight, out float ellipseOffset, out float boxBaseOffset);

			canvas.SetFillColor(canvas.GetBackgroundColor());

			canvas.MoveTo(rect.X, boxBaseOffset + rect.Y)
				.LineTo(rect.X + rect.Width, boxBaseOffset + rect.Y)
				.LineTo(rect.X + rect.Width, rect.Y + rect.Height - bevel)
				.LineTo(rect.X + rect.Width - bevel, rect.Y + rect.Height)
				.LineTo(rect.X + bevel, rect.Y + rect.Height)
				.LineTo(rect.X, rect.Y + rect.Height - bevel)
				.ClosePath();

			canvas.FillStroke();

			Rectangle outerEllipseRect = Rectangle.RectangleAt(rect.X + rect.Width / 2, boxBaseOffset + rect.Y + ellipseOffset, ellipseWidth, ellipseHeight);
			Rectangle innerEllipseRect = outerEllipseRect.Margins(3, 3, 3, 3, false);

			canvas.SetFillColor(canvas.GetMidtoneColor());
			canvas.EllipseFrom(outerEllipseRect);
			canvas.FillStroke();
			canvas.SetFillColor(canvas.GetBackgroundColor());
			canvas.EllipseFrom(innerEllipseRect);
			canvas.Fill();

			canvas.RestoreState();
		}

		protected override Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Measurements(rect.Width, out float ellipseWidth, out float ellipseHeight, out float ellipseOffset, out float boxBaseOffset);
			Rectangle outerEllipseRect = Rectangle.RectangleAt(rect.X + rect.Width / 2, boxBaseOffset + rect.Y + ellipseOffset, ellipseWidth, ellipseHeight);
			return outerEllipseRect.Margins(3, 3, 3, 3, false);
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Measurements(rect.Width, out _, out float ellipseHeight, out _, out _);
			return rect.Margins(bevel / 2, bevel, ellipseHeight + 3, bevel, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle fullWithoutEllipse = rect.Margins(bevel / 2, bevel, 0, bevel, true);

			Measurements(fullWithoutEllipse.Width, out _, out float ellipseHeight, out _, out _);

			return fullWithoutEllipse.Margins(0, 0, ellipseHeight + 3, 0, true);
		}
	}

}
