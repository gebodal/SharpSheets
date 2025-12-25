using SharpSheets.Layouts;
using SharpSheets.Colors;
using SharpSheets.Canvas;
using SharpSheets.Parsing;

namespace SharpSheets.Shapes {

	/// <summary>
	/// A box that draws no outline, but can optionally utilise a margin around the
	/// edge when calculating the remaining area.
	/// </summary>
	public class NoOutline : BoxBase {

		protected readonly Margins trim;

		public NoOutline(float aspect, Margins trim = default) : base(aspect) {
			this.trim = trim;
		}

		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="trim">A margin around the inside of the shape area to trim
		/// when calculating the remaining area.</param>
		[FactoryBuilder(typeof(IBox))]
		public static NoOutline Build(float aspect = -1f, Margins trim = default) {
			return new NoOutline(aspect, trim);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) { }

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			//return rect;
			return rect.Margins(trim, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			//return rect;
			return rect.Margins(trim, true);
		}
	}

	/// <summary>
	/// A box that simply draws a single line along the bottom edge, the color of which
	/// may be specified. The line may also be offset from the edge of the shape area.
	/// </summary>
	public class UnderlineBox : BoxBase {

		private readonly Color? stroke;
		private readonly float offset;

		public UnderlineBox(float aspect, Color? stroke = null, float offset = 0f) : base(aspect) {
			this.stroke = stroke;
			this.offset = offset;
		}

		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the line.</param>
		/// <param name="offset">A value by which to offset the start and end points
		/// of the line from the sides of the shape area.</param>
		[FactoryBuilder(typeof(IBox))]
		public static UnderlineBox Build(float aspect, Color? stroke = null, float offset = 0f) {
			return new UnderlineBox(aspect, stroke, offset);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			if (stroke.HasValue) {
				canvas.SaveState();
				canvas.SetStrokeColor(stroke.Value);
			}

			Rectangle drawRect = rect.Margins(0f, offset, false);
			canvas.MoveToRel(drawRect, 0, 0).LineToRel(drawRect, 1, 0).Stroke();

			if (stroke.HasValue) {
				canvas.RestoreState();
			}
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.Margins(0, 0, graphicsState.GetLineWidth() / 2f, 0, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.Margins(0, 0, graphicsState.GetLineWidth() / 2f, 0, true);
		}
	}

	/// <summary>
	/// A simple rectangular box with a "shadow" towards the bottom right.
	/// </summary>
	public class ShadowedBox : BoxBase {

		protected readonly float shadow;

		public ShadowedBox(float aspect, float shadow = 1f) : base(aspect) {
			this.shadow = shadow;
		}

		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="shadow">The length of the shadow, in points.</param>
		[FactoryBuilder(typeof(IBox))]
		public static ShadowedBox Build(float aspect, float shadow = 1f) {
			return new ShadowedBox(aspect, shadow);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			canvas.SetFillColor(canvas.GetForegroundColor());

			float eps = canvas.GetLineWidth() / 2;

			canvas.MoveTo(rect.Left - eps, rect.Top + eps)
				.LineTo(rect.Left - eps, rect.Bottom - eps)
				.LineTo(rect.Left + shadow - eps, rect.Bottom - shadow - eps)
				.LineTo(rect.Right + shadow + eps, rect.Bottom - shadow - eps)
				.LineTo(rect.Right + shadow + eps, rect.Top - shadow + eps)
				.LineTo(rect.Right + eps, rect.Top + eps)
				.LineTo(rect.Left - eps, rect.Top + eps)
				.ClosePath();
			canvas.FillStroke();

			canvas.SetFillColor(canvas.GetBackgroundColor());

			canvas.Rectangle(rect).FillStroke();

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			float eps = graphicsState.GetLineWidth() / 2;
			return rect.Margins(eps, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			float eps = graphicsState.GetLineWidth() / 2;
			return rect.Margins(eps, true);
		}
	}

	/// <summary>
	/// A simple circular box with a "shadow" towards the bottom right.
	/// </summary>
	public class ShadowedCircle : BoxBase {

		protected readonly float shadow;

		public ShadowedCircle(float aspect, float shadow = 1f) : base(aspect) {
			this.shadow = shadow;
		}

		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="shadow">The length of the shadow, in points.</param>
		[FactoryBuilder(typeof(IBox))]
		public static ShadowedCircle Build(float aspect = -1f, float shadow = 1f) {
			return new ShadowedCircle(aspect, shadow);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			canvas.SetFillColor(canvas.GetForegroundColor());

			float radius = rect.Aspect(1f).Width / 2;
			float eps = canvas.GetLineWidth() / 2;
			float epsrad = radius + eps;

			float xy45 = (float)Math.Sqrt(epsrad * epsrad / 2);

			canvas.MoveTo(rect.CentreX + xy45, rect.CentreY + xy45)
				.LineTo(rect.CentreX + xy45 + shadow, rect.CentreY + xy45 - shadow)
				//.Arc(rect.CentreX + xy45 + bevel, rect.CentreY + xy45 - bevel, rect.CentreX - xy45 + bevel, rect.CentreY - xy45 - bevel, epsrad, true, false) // ARC
				.LineTo(rect.CentreX - xy45 + shadow, rect.CentreY - xy45 - shadow)
				.LineTo(rect.CentreX - xy45, rect.CentreY - xy45)
				.ClosePath();
			canvas.Fill();
			canvas.CircleAt(rect.CentreX + shadow, rect.CentreY - shadow, radius).FillStroke();

			canvas.SetFillColor(canvas.GetBackgroundColor());

			canvas.CircleAt(rect, radius).FillStroke();

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			float eps = graphicsState.GetLineWidth() / 2;
			return rect.Margins(eps, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			float eps = graphicsState.GetLineWidth() / 2;
			return rect.Margins(eps, true);
		}
	}

	#region Subsections

	/// <summary>
	/// A simple background box, consisting of a bevelled area filled with the
	/// current midtone color, and a trim detail in the durrent background box
	/// following the shape outline.
	/// </summary>
	public class SimpleBackground : BoxBase {

		protected readonly float bevel;

		public SimpleBackground(float aspect, float bevel = 8f) : base(aspect) {
			this.bevel = bevel;
		}

		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="bevel">The size of the margin for the remaining area,
		/// which also dictates the size of the outline bevels.</param>
		[FactoryBuilder(typeof(IBox))]
		public static SimpleBackground Build(float aspect, float bevel = 8f) {
			return new SimpleBackground(aspect, bevel);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			canvas.SetFillColor(canvas.GetMidtoneColor());
			canvas.BevelledRectangle(rect, 0.65f * bevel).Fill();

			canvas.SetStrokeColor(canvas.GetBackgroundColor());
			canvas.BevelledRectangle(rect.Margins(0.5f * bevel, false), 0.35f * bevel).Stroke();

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.Margins(bevel, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.Margins(bevel, true);
		}
	}

	#endregion

}