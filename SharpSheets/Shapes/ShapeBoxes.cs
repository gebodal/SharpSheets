using SharpSheets.Utilities;
using System;
using SharpSheets.Layouts;
using SharpSheets.Colors;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Exceptions;

namespace SharpSheets.Shapes {

	/// <summary>
	/// A heraldic shield style outline, with a pointed top and cutaway corners.
	/// An inner outline may also be drawn, and the fill and stroke colors specified.
	/// </summary>
	public class EaredShield : ShapeBox {

		protected readonly float? bevel;

		/// <summary>
		/// Constructor for EaredShield.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="bevel">If a value is provided, a secondary outline with half the
		/// stroke width will be drawn inside the shape outline, inset by the provided
		/// value.</param>
		public EaredShield(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, UFloat? bevel = null) : base(aspect, stroke, fill, dashes, dashOffset, Margins.Zero) {
			this.bevel = bevel?.Value;
		}

		public override Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return base.AspectRect(graphicsState, rect).Aspect(earedAspect);
		}

		protected static readonly float earedAspect = 0.77f;

		protected static readonly float earTopX = 0.11f;
		protected static readonly float earTopY = 0.88f;
		protected static readonly float earBottomX = 0f;
		protected static readonly float earBottomY = 0.76f;
		protected static readonly float bodyMidpointX = 0.05f;
		protected static readonly float bodyMidpointY = 0.42f;

		protected static readonly float topCurveX = 0.23f;
		protected static readonly float topCurveY = 0.88f;

		protected static readonly float earCurveX = 0.065f;
		protected static readonly float earCurveY = 0.81f;

		protected static readonly float bodyTopCurveY = 0.62f;
		protected static readonly float bodyBottomCurveY = 0.2f;

		protected static ISharpCanvas DrawShield(ISharpCanvas canvas, Rectangle rect) {
			canvas.MoveToRel(rect, 0.5f, 1f)
				.CurveToRel(rect, topCurveX, topCurveY, earTopX, earTopY)
				.CurveToRel(rect, earCurveX, earCurveY, earBottomX, earBottomY)
				.CurveToRel(rect, bodyMidpointX, bodyTopCurveY, bodyMidpointX, bodyMidpointY)
				.CurveToRel(rect, bodyMidpointX, bodyBottomCurveY, 0.5f, 0f)
				.CurveToRel(rect, 1f - bodyMidpointX, bodyBottomCurveY, 1f - bodyMidpointX, bodyMidpointY)
				.CurveToRel(rect, 1f - bodyMidpointX, bodyTopCurveY, 1f - earBottomX, earBottomY)
				.CurveToRel(rect, 1f - earCurveX, earCurveY, 1f - earTopX, earTopY)
				.CurveToRel(rect, 1f - topCurveX, topCurveY, 0.5f, 1f)
				.ClosePath();
			return canvas;
		}

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			float lineWidth = canvas.GetLineWidth();

			DrawShield(canvas, rect).FillStroke();

			if (bevel.HasValue && bevel.Value > 0) {
				canvas.SetLineWidth(0.5f * lineWidth);

				DrawShield(canvas, rect.Margins(bevel.Value, false));
				canvas.Stroke();
			}

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			if (bevel.HasValue) { rect = rect.Margins(bevel.Value, false); }
			rect = rect.Margins(graphicsState.GetLineWidth() / 2, false);
			return rect;
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			rect = rect.Margins(graphicsState.GetLineWidth() / 2, true);
			if (bevel.HasValue) { rect = rect.Margins(bevel.Value, true); }
			return rect.ContainAspect(earedAspect);
		}
	}

	/// <summary>
	/// A heraldic shield style outline, with a rounder profile and cutaway corners.
	/// An inner outline may also be drawn, and the fill and stroke colors specified.
	/// </summary>
	public class BadgeShield : ShapeBox {

		protected readonly float? bevel;

		/// <summary>
		/// Constructor for BadgeShield.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="bevel">If a value is provided, a secondary outline with half the
		/// stroke width will be drawn inside the shape outline, inset by the provided
		/// value.</param>
		public BadgeShield(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, float? bevel = null) : base(aspect, stroke, fill, dashes, dashOffset, Margins.Zero) {
			this.bevel = bevel;
		}

		public override Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return base.AspectRect(graphicsState, rect).Aspect(badgeAspect);
		}

		protected static readonly float badgeAspect = 0.93f;

		protected static readonly float pointTopY = 0.97f;
		protected static readonly float earTopX = 0.16f;
		protected static readonly float earBottomY = 0.85f;
		protected static readonly float bulgeCentreY = 0.39f;

		protected static readonly float topCurveX = 0.325f;
		protected static readonly float topCurveY = 0.92f;

		protected static readonly float inwardCurveUpperX = 0.11f;
		protected static readonly float inwardCurveUpperY = 0.66f;
		protected static readonly float inwardCurveLowerY = 0.56f;

		protected static readonly float bottomCurveUpperY = 0.15f;
		protected static readonly float bottomCurveLowerX = 0.37f;
		protected static readonly float bottomCurveLowerY = 0.13f;

		protected static ISharpCanvas DrawShield(ISharpCanvas canvas, Rectangle rect) {
			canvas.MoveToRel(rect, 0.5f, pointTopY)
				.CurveToRel(rect, topCurveX, topCurveY, earTopX, 1f) // Top curve left
				.LineToRel(rect, 0f, earBottomY) // Straight ear top left
				.CurveToRel(rect, inwardCurveUpperX, inwardCurveUpperY, 0f, inwardCurveLowerY, 0f, bulgeCentreY) // Inward curve left
				.CurveToRel(rect, 0f, bottomCurveUpperY, bottomCurveLowerX, bottomCurveLowerY, 0.5f, 0f) // Bulge curve left
				.CurveToRel(rect, 1f - bottomCurveLowerX, bottomCurveLowerY, 1f, bottomCurveUpperY, 1f, bulgeCentreY) // Bulge curve right
				.CurveToRel(rect, 1f, inwardCurveLowerY, 1f - inwardCurveUpperX, inwardCurveUpperY, 1f, earBottomY) // Inward curve right
				.LineToRel(rect, 1f - earTopX, 1f) // Straight ear top right
				.CurveToRel(rect, 1f - topCurveX, topCurveY, 0.5f, pointTopY) // Top curve right
				.ClosePath();
			return canvas;
		}

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			float lineWidth = canvas.GetLineWidth();

			DrawShield(canvas, rect).FillStroke();

			if (bevel.HasValue && bevel.Value > 0) {
				canvas.SetLineWidth(0.5f * lineWidth);

				DrawShield(canvas, rect.Margins(bevel.Value, false));
				canvas.Stroke();
			}

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			if (bevel.HasValue) { rect = rect.Margins(bevel.Value, false); }
			rect = rect.Margins(graphicsState.GetLineWidth() / 2, false);
			return rect;
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			rect = rect.Margins(graphicsState.GetLineWidth() / 2, true);
			if (bevel.HasValue) { rect = rect.Margins(bevel.Value, true); }
			return rect.ContainAspect(badgeAspect);
		}
	}

	/// <summary>
	/// A heraldic shield style outline, with a pointed bottom, and two cutaway
	/// curves along the top edge. An inner outline may also be drawn, and the
	/// fill and stroke colors specified.
	/// </summary>
	public class EngrailedShield : ShapeBox {

		protected readonly float? bevel;

		/// <summary>
		/// Constructor for EngrailedShield.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		/// <param name="bevel">If a value is provided, a secondary outline with half the
		/// stroke width will be drawn inside the shape outline, inset by the provided
		/// value.</param>
		public EngrailedShield(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f, float? bevel = null) : base(aspect, stroke, fill, dashes, dashOffset, Margins.Zero) {
			this.bevel = bevel;
		}

		public override Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return base.AspectRect(graphicsState, rect).Aspect(engrailedAspect);
		}

		protected static readonly float engrailedAspect = 0.83f;

		protected static readonly float topCurveX = 0.26f;
		protected static readonly float topCurveY = 0.92f;
		protected static readonly float sideCurveTopY = 0.35f;
		protected static readonly float sideCurveBottomX = 0.23f;
		protected static readonly float sideCurveBottomY = 0.16f;

		protected static ISharpCanvas DrawShield(ISharpCanvas canvas, Rectangle rect) {
			canvas.MoveToRel(rect, 0.5f, 1f)
				.CurveToRel(rect, 1f - topCurveX, topCurveY, 1f, 1f)
				.CurveToRel(rect, 1f, sideCurveTopY, 1f - sideCurveBottomX, sideCurveBottomY, 0.5f, 0f)
				.CurveToRel(rect, sideCurveBottomX, sideCurveBottomY, 0f, sideCurveTopY, 0f, 1f)
				.CurveToRel(rect, topCurveX, topCurveY, 0.5f, 1f)
				.ClosePath();
			return canvas;
		}

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			float lineWidth = canvas.GetLineWidth();

			DrawShield(canvas, rect).FillStroke();

			if (bevel.HasValue && bevel.Value > 0) {
				canvas.SetLineWidth(0.5f * lineWidth);

				DrawShield(canvas, rect.Margins(bevel.Value, false));
				canvas.Stroke();
			}

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			if (bevel.HasValue) { rect = rect.Margins(bevel.Value, false); }
			rect = rect.Margins(graphicsState.GetLineWidth() / 2, false);
			return rect;
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			rect = rect.Margins(graphicsState.GetLineWidth() / 2, true);
			if (bevel.HasValue) { rect = rect.Margins(bevel.Value, true); }
			return rect.ContainAspect(engrailedAspect);
		}
	}

	/// <summary>
	/// A simple geometric heart outline, with two semi-circular curves at the top,
	/// and a right-angled corner at the bottom.
	/// </summary>
	public class Heart : ShapeBox {

		/// <summary>
		/// Constructor for Heart.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		public Heart(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f) : base(aspect, stroke, fill, dashes, dashOffset, Margins.Zero) { }

		public override Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return base.AspectRect(graphicsState, rect).Aspect(heartAspect);
		}

		protected static readonly float heartAspect = 1.0938f;

		static void GetNumbers(Rectangle rect, out float radius, out float offset) {
			radius = (float)Math.Min(rect.Height / (1f + 3f / Math.Sqrt(2f)), rect.Width / (2f + Math.Sqrt(2f)));
			offset = radius * (float)Math.Sin(Math.PI / 4f);
		}

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			GetNumbers(rect, out float radius, out float offset);

			float startY = rect.Top - radius + offset;
			float arcBottomY = rect.Top - radius - offset;

			float bottomY = (float)(rect.Top - radius + (radius / Math.Sqrt(2)) - (Math.Sqrt(8) * radius));

			/*
			canvas.MoveTo(rect.CentreX, startY)
				.Arc(rect.CentreX, startY, rect.CentreX + 2 * offset, arcBottomY, radius, true, false)
				.LineTo(rect.CentreX, rect.Bottom)
				.LineTo(rect.CentreX - 2 * offset, arcBottomY)
				.Arc(rect.CentreX - 2 * offset, arcBottomY, rect.CentreX, startY, radius, true, false)
				.ClosePath();
			*/
			canvas.MoveTo(rect.CentreX - 2 * offset, arcBottomY)
				.ArcTo(rect.CentreX, startY, radius + 1e-4f, true, false)
				.ArcTo(rect.CentreX + 2 * offset, arcBottomY, radius + 1e-4f, true, false)
				.LineTo(rect.CentreX, bottomY)
				.ClosePath();

			canvas.FillStroke();

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			GetNumbers(rect, out float radius, out float offset);
			return rect.Margins(graphicsState.GetLineWidth() / 2, false).Margins(radius - offset, 0f, 0f, 0f, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			// TODO Does this even work?
			float radius = (float)Math.Max(rect.Height / (1f + 3f / Math.Sqrt(2f)), rect.Width / (2f + Math.Sqrt(2f)));
			float offset = radius * (float)Math.Sin(Math.PI / 4f);
			return rect.Margins(radius - offset, 0f, 0f, 0f, true).Margins(graphicsState.GetLineWidth() / 2, true).ContainAspect(heartAspect);
		}
	}


	/// <summary>
	/// An eye-shaped outline, with rounded corners at the sides.
	/// </summary>
	public class Eye : ShapeBox {

		/// <summary>
		/// Constructor for Eye.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="stroke">The stroke color for the outline. If no value is provided,
		/// the current foreground color will be used.</param>
		/// <param name="fill">The fill color for the shape. If no value is provided, the
		/// current background color will be used.</param>
		/// <param name="dashes">An array of dash lengths with which to draw the outline.
		/// The resulting line will be a series of "on" and "off" lengths, corresponding
		/// to the dash array. These lengths are measured in points.</param>
		/// <param name="dashOffset">An offset for the start of the dash pattern. This
		/// will shift the dash pattern along by a number of points equal to the value.</param>
		public Eye(float aspect, Color? stroke = null, Color? fill = null, float[]? dashes = null, float dashOffset = 0f) : base(aspect, stroke, fill, dashes, dashOffset, Margins.Zero) { }

		protected override void DrawShape(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			float smoothing = 0.1f;
			float curvature = 0.27f;

			canvas.MoveToRel(rect, 0.0f, 0.5f)
				.CurveToRel(rect, 0f, 0.5f + smoothing, 0.5f - curvature, 1f, 0.5f, 1f)
				.CurveToRel(rect, 0.5f + curvature, 1f, 1f, 0.5f + smoothing, 1f, 0.5f)
				.CurveToRel(rect, 1f, 0.5f - smoothing, 0.5f + curvature, 0f, 0.5f, 0f)
				.CurveToRel(rect, 0.5f - curvature, 0f, 0f, 0.5f - smoothing, 0f, 0.5f)
				.ClosePath();

			canvas.FillStroke();

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.MarginsRel(0.05f, 0.1f, 0.05f, 0.1f, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.MarginsRel(0.05f, 0.1f, 0.05f, 0.1f, true);
		}
	}

	/// <summary>
	/// Scroll outline, with folds and curls at both sides. The resulting shape
	/// is slightly randomized, to avoid uniformity on the page.
	/// </summary>
	public class Scroll : BoxBase {

		protected readonly float bevel;

		/// <summary>
		/// Constructor for Scroll.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="bevel">A sizing parameter for the details of this outline.</param>
		public Scroll(float aspect, float bevel = 5f) : base(aspect) {
			this.bevel = bevel;
		}

		protected Rectangle GetCentralSpaceRect(Rectangle fullRect) {
			return fullRect.Margins(0, Xmargin, Ymargin, Xmargin, false);
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			float curvatureSpacing = 0.4f * bevel;
			return GetCentralSpaceRect(fullRect).Margins(curvatureSpacing, curvatureSpacing, 0.5f * curvatureSpacing, curvatureSpacing, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle remainingRect) {
			float curvatureSpacing = 0.4f * bevel;
			return remainingRect.Margins(curvatureSpacing, curvatureSpacing, 0.5f * curvatureSpacing, curvatureSpacing, true)
				.Margins(0, Xmargin, Ymargin, Xmargin, true);
		}

		float FirstFoldYDrop { get { return 0.5f * bevel; } }
		//float FirstFoldWidth { get { return 1.25f * bevel; } }
		float FirstFoldWidth { get { return 2f * bevel; } }

		float SecondFoldYDrop { get { return 0.25f * bevel; } }
		float SecondFoldXShift { get { return 0.5f * bevel; } }
		float SecondFoldCurveDepth { get { return 0.2f * bevel; } }
		float SecondFoldDownCurveLength { get { return 1.0f * bevel; } }
		static float SecondFoldDownCurveFactor { get { return 0.33f; } }
		float SecondFoldUpCurveLength { get { return 0.65f * bevel; } }
		static float SecondFoldUpCurveFactor { get { return 1.0f; } }

		static float SecondFoldConnectorXFactor { get { return 1f; } }
		static float SecondFoldConnectorYFactor { get { return 1f; } }

		float UpperFoldLength { get { return 0.75f * bevel; } }
		float UpperFoldHeight { get { return 0.15f * bevel; } }

		float InnerFoldLength { get { return 0.4f * bevel; } }
		float InnerFoldDepth { get { return 0.2f * bevel; } }

		static float InnerFoldCurveFactor { get { return 1.4f; } }

		float Xmargin { get { return FirstFoldWidth / 2 - SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength; } }
		float Ymargin { get { return FirstFoldYDrop + SecondFoldYDrop + SecondFoldCurveDepth; } }

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle fullRect) {

			Random random = new Random((int)fullRect.X + (int)fullRect.Y);
			float Random(float min, float max) {
				return min + (max - min) * ((float)random.NextDouble());
			}

			canvas.SaveState();

			canvas.SetLineJoinStyle(LineJoinStyle.ROUND).SetLineCapStyle(LineCapStyle.ROUND);
			canvas.SetLineWidth(0.5f * canvas.GetDefaultLineWidth());

			Rectangle rect = GetCentralSpaceRect(fullRect);

			void DrawDetails(int leftRight) {
				float s = leftRight > 0 ? 1 : -1;

				Rectangle firstFold = Rectangle.RectangleAt(rect.RelX(leftRight), rect.CentreY - FirstFoldYDrop, FirstFoldWidth, rect.Height);

				float xBase = s > 0 ? firstFold.Right : firstFold.Left;
				float Base(float offset = 0f) {
					return xBase + s * offset;
				}

				// Back Curve
				canvas.SetFillColor(canvas.GetMidtoneColor().Darken(0.75f));
				canvas.MoveTo(Base(-SecondFoldXShift), firstFold.Bottom - SecondFoldYDrop) // Inner Bottom
					.CurveTo( // Middle Bottom
						Base(-SecondFoldXShift + SecondFoldDownCurveFactor * SecondFoldDownCurveLength),
						firstFold.Bottom - SecondFoldYDrop - SecondFoldCurveDepth,
						Base(-SecondFoldXShift + SecondFoldDownCurveLength),
						firstFold.Bottom - SecondFoldYDrop - SecondFoldCurveDepth)
					.CurveTo( // Outer Bottom
						Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveFactor * SecondFoldUpCurveLength), // +1.5f * bevel
						firstFold.Bottom - SecondFoldYDrop - SecondFoldCurveDepth,
						Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveFactor * SecondFoldUpCurveLength),
						firstFold.Bottom - SecondFoldYDrop,
						Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength), //+ 1.5f * bevel,
						firstFold.Bottom)
					.CurveTo(Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength - 0.5f * UpperFoldLength), firstFold.CentreY, Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength), firstFold.Top) // To Outer Top (outer side)
					.CurveTo( // Middle Top
						Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveFactor * SecondFoldUpCurveLength),
						firstFold.Top - SecondFoldYDrop,
						Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveFactor * SecondFoldUpCurveLength),
						firstFold.Top - SecondFoldYDrop - SecondFoldCurveDepth,
						Base(-SecondFoldXShift + SecondFoldDownCurveLength),
						firstFold.Top - SecondFoldYDrop - SecondFoldCurveDepth)
					.CurveTo( // Inner Top
						Base(-SecondFoldXShift + SecondFoldDownCurveFactor * SecondFoldDownCurveLength),
						firstFold.Top - SecondFoldYDrop - SecondFoldCurveDepth,
						Base(-SecondFoldXShift),
						firstFold.Top - SecondFoldYDrop)
					.ClosePath(); // Back to Inner Bottom (inner side)
				canvas.FillStroke();

				// Inner curve
				float innerCurveInnerX = Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength - UpperFoldLength);
				float innerCurveOuterX = Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength - UpperFoldLength + InnerFoldLength);
				canvas.SetFillColor(canvas.GetMidtoneColor().Darken(0.6f));
				canvas.MoveTo(innerCurveInnerX, firstFold.Bottom) // Inner Bottom
					.CurveTo( // Outer Bottom (bottom side)
						innerCurveInnerX,
						firstFold.Bottom - InnerFoldDepth,
						Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength - UpperFoldLength + InnerFoldCurveFactor * InnerFoldLength),
						firstFold.Bottom - InnerFoldDepth,
						Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength - UpperFoldLength + InnerFoldLength),
						firstFold.Bottom)
					.LineTo(Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength - UpperFoldLength + InnerFoldLength), firstFold.Top) // Outer Top (outer side)
					.CurveTo( // Inner Top (top side)
						Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength - UpperFoldLength + InnerFoldCurveFactor * InnerFoldLength),
						firstFold.Top - InnerFoldDepth,
						innerCurveInnerX,
						firstFold.Top - InnerFoldDepth,
						innerCurveInnerX,
						firstFold.Top)
					.CurveTo(MathUtils.Lerp(innerCurveInnerX, innerCurveOuterX, 0.5f), firstFold.CentreY, innerCurveInnerX, firstFold.Bottom) // Back to Inner Bottom (inner side)
					.ClosePath();
				//canvas.SetFillColorGray(0.5f).FillStroke(); // Darker midtone color?
				canvas.FillStroke();

				// Upper curve
				float upperCurveInnerX = Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength - UpperFoldLength);
				float upperCurveOuterX = Base(-SecondFoldXShift + SecondFoldDownCurveLength + SecondFoldUpCurveLength);
				canvas.SetFillColor(canvas.GetBackgroundColor());
				canvas.MoveTo(upperCurveOuterX, firstFold.Bottom) // Start at Outer Bottom
					.CurveTo(upperCurveOuterX, firstFold.Bottom + UpperFoldHeight, upperCurveInnerX, firstFold.Bottom + UpperFoldHeight, upperCurveInnerX, firstFold.Bottom) // To Inner Bottom (bottom side)
					.CurveTo(MathUtils.Lerp(upperCurveInnerX, upperCurveOuterX, Random(0.15f, 0.35f)), MathUtils.Lerp(firstFold.Bottom, firstFold.Top, Random(0.25f, 0.75f)), upperCurveInnerX, firstFold.Top) // To Inner Top (inner side)
					.CurveTo(upperCurveInnerX, firstFold.Top + UpperFoldHeight, upperCurveOuterX, firstFold.Top + UpperFoldHeight, upperCurveOuterX, firstFold.Top) // To Outer Top (top side)
					.CurveTo(MathUtils.Lerp(upperCurveOuterX, upperCurveInnerX, Random(0.15f, 0.35f)), MathUtils.Lerp(firstFold.Bottom, firstFold.Top, Random(0.25f, 0.75f)), upperCurveOuterX, firstFold.Bottom) // Back to Outer Bottom (outer side)
					.ClosePath();
				canvas.FillStroke();

				// Second fold connector
				canvas.SetFillColor(canvas.GetMidtoneColor().Darken(0.6f));
				canvas.MoveTo(Base(), firstFold.Bottom) // Start at Outer Bottom
					.CurveTo( // To Inner Bottom (bottom side)
						Base(-0.75f * SecondFoldXShift),
						firstFold.Bottom - 0.15f * SecondFoldYDrop,
						Base(-SecondFoldXShift),
						firstFold.Bottom - SecondFoldYDrop)
					.LineTo(Base(-SecondFoldXShift), firstFold.Top - SecondFoldYDrop) // To Inner Top (inner side)
					.CurveTo( // To Outer Top (top side)
						Base(-SecondFoldConnectorXFactor * SecondFoldXShift),
						firstFold.Top - SecondFoldConnectorYFactor * SecondFoldYDrop,
						Base(),
						firstFold.Top)
					.CurveTo(Base(-bevel * 0.7f), firstFold.CentreY, Base(), firstFold.Bottom) // Back to Outer Bottom (outer side)
					.ClosePath();
				canvas.FillStroke();

				// First fold
				//canvas.SetFillColorGray(0.9f) // Lighter midtone color?
				canvas.SetFillColor(canvas.GetMidtoneColor().Lighten(0.66f)); // TODO Is this right?
				canvas.MoveTo(Base(-firstFold.Width), firstFold.Bottom) // Start at Inner Bottom
					.LineTo(Base(-firstFold.Width), firstFold.Top) // To Inner Top (inner side)
					.CurveTo(Base(-firstFold.Width * Random(0.25f, 0.75f)), firstFold.Top - bevel * Random(0.1f, 0.3f), Base(), firstFold.Top) // To Outer Top (top side)
					.CurveTo(Base(-bevel * Random(0.4f, 0.65f)), MathUtils.Lerp(firstFold.Bottom, firstFold.Top, Random(0.25f, 0.75f)), Base(), firstFold.Bottom) // To Outer Bottom (outer side)
					.CurveTo(Base(-firstFold.Width * Random(0.25f, 0.45f)), firstFold.Bottom + bevel * Random(0f, 0.25f), Base(-firstFold.Width * Random(0.55f, 0.75f)), firstFold.Bottom + bevel * Random(0f, 0.25f), Base(-firstFold.Width), firstFold.Bottom) // Back to Inner Bottom (bottom side)
					.ClosePath().FillStroke();

				// First fold connector
				float connectorXBase = s > 0 ? rect.Right : rect.Left;
				float ConnectorBase(float offset = 0f) {
					return connectorXBase + s * offset;
				}
				float firstFoldCurveRandom = Random(0.45f, 0.75f);
				//canvas.SetFillColorGray(0.5f) // Darker midtone color?
				canvas.SetFillColor(canvas.GetMidtoneColor().Darken(0.6f));
				canvas.MoveToRel(rect, leftRight, 0) // Start at Inner Bottom
					.CurveTo(ConnectorBase(-firstFold.Width * 0.5f * firstFoldCurveRandom), MathUtils.Lerp(rect.Bottom, firstFold.Bottom, firstFoldCurveRandom) + bevel * Random(0f, 0.5f), Base(-firstFold.Width), firstFold.Bottom) // To Outer Bottom (bottom side)
					.LineToRel(firstFold, 1 - leftRight, 1) // To Outer Top (outer side)
					.LineToRel(rect, leftRight, 1) // To Inner Top (top side)
					.CurveTo(ConnectorBase(-0.55f * bevel), rect.CentreY, ConnectorBase(), rect.Bottom) // Allow for maximum curvature of central space side
					.ClosePath().FillStroke();
			}

			DrawDetails(0); // Left Side
			DrawDetails(1); // Right Side

			// Central space
			//canvas.SetFillColorGray(1f).Rectangle(rect).FillStroke();
			canvas.SetFillColor(canvas.GetBackgroundColor());
			canvas.MoveTo(rect.Left, rect.Bottom) // Start at Bottom Left
				.CurveTo(rect.Left + bevel * Random(0.25f, 0.5f), MathUtils.Lerp(rect.Bottom, rect.Top, Random(0.25f, 0.75f)), rect.Left, rect.Top) // To Top Left (left side)
				.CurveTo(MathUtils.Lerp(rect.Left, rect.Right, Random(0.25f, 0.75f)), rect.Top - bevel * Random(0.25f, 0.5f), rect.Right, rect.Top) // To Top Right (top side)
				.CurveTo(rect.Right - bevel * Random(0.25f, 0.5f), MathUtils.Lerp(rect.Bottom, rect.Top, Random(0.25f, 0.75f)), rect.Right, rect.Bottom) // To Bottom Right (right side)
				.CurveTo(MathUtils.Lerp(rect.Left, rect.Right, Random(0.25f, 0.75f)), rect.Bottom - bevel * Random(0.25f, 0.5f), rect.Left, rect.Bottom) // To Bottom Left (bottom side)
				.ClosePath();
			canvas.FillStroke();


			canvas.RestoreState();
		}
	}

	/// <summary>
	/// Pennant outline, with folds and triangular cutouts at both sides.
	/// The resulting shape is slightly randomized, to avoid uniformity
	/// on the page.
	/// </summary>
	public class Pennant : BoxBase {

		protected readonly float bevel;

		/// <summary>
		/// Constructor for Pennant.
		/// </summary>
		/// <param name="aspect">Aspect ratio for this box.</param>
		/// <param name="bevel">A sizing parameter for the details of this outline.</param>
		/// <size>100 20</size>
		public Pennant(float aspect, float bevel = 5f) : base(aspect) {
			this.bevel = bevel;
		}

		protected Rectangle GetCentralSpaceRect(Rectangle fullRect) {
			return fullRect.Margins(0, Xmargin, Ymargin, Xmargin, false);
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle fullRect) {
			float curvatureSpacing = 0.4f * bevel;
			return GetCentralSpaceRect(fullRect).Margins(curvatureSpacing, curvatureSpacing, 0.5f * curvatureSpacing, curvatureSpacing, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle remainingRect) {
			float curvatureSpacing = 0.4f * bevel;
			return remainingRect.Margins(curvatureSpacing, curvatureSpacing, 0.5f * curvatureSpacing, curvatureSpacing, true)
				.Margins(0, Xmargin, Ymargin, Xmargin, true);
		}

		float FirstFoldYDrop { get { return 0.5f * bevel; } }
		//float FirstFoldWidth { get { return 2f * bevel; } }
		float FirstFoldXBack { get { return 1f * bevel; } }
		float FirstFoldTotalWidth { get { return 4f * bevel; } }
		float CutoutLength { get { return 2f * bevel; } }

		float Xmargin { get { return FirstFoldTotalWidth - FirstFoldXBack; } }
		float Ymargin { get { return FirstFoldYDrop; } }

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle fullRect) {

			Random random = new Random((int)fullRect.X + (int)fullRect.Y);
			float Random(float min, float max) {
				return min + (max - min) * ((float)random.NextDouble());
			}

			canvas.SaveState();

			canvas.SetLineJoinStyle(LineJoinStyle.ROUND).SetLineCapStyle(LineCapStyle.ROUND);
			canvas.SetLineWidth(0.5f * canvas.GetDefaultLineWidth());

			Rectangle rect = GetCentralSpaceRect(fullRect);

			void DrawDetails(int leftRight) {
				float s = leftRight > 0 ? 1 : -1;

				//Rectangle firstFold = RectangleUtils.RectangleAt(rect.RelX(leftRight), rect.CentreY - FirstFoldYDrop, FirstFoldWidth, rect.Height);
				//Rectangle firstFold = new Rectangle(rect.RelX(leftRight) + s*(-FirstFoldXBack), rect.Y - FirstFoldYDrop, FirstFoldTotalWidth, rect.Height);
				Rectangle firstFold = Rectangle.RectangleFromBounding(rect.RelX(leftRight) + s * (-FirstFoldXBack), rect.Bottom - FirstFoldYDrop, rect.RelX(leftRight) + s * (FirstFoldTotalWidth - FirstFoldXBack), rect.Top - FirstFoldYDrop);

				float xBase = s > 0 ? firstFold.Right : firstFold.Left;
				float Base(float offset = 0f) {
					return xBase + s * offset;
				}

				// First fold
				float cutCentreY = MathUtils.Lerp(firstFold.Bottom, firstFold.Top, Random(0.45f, 0.55f));
				float topCutRandom = Random(0.25f, 0.75f);
				float bottomCutRandom = Random(0.25f, 0.75f);
				//canvas.SetFillColorGray(0.9f)
				canvas.SetFillColor(canvas.GetMidtoneColor().Lighten(0.66f)); // TODO Is this right?
				canvas.MoveTo(Base(-firstFold.Width), firstFold.Bottom) // Start at Inner Bottom
					.LineTo(Base(-firstFold.Width), firstFold.Top) // To Inner Top (inner side)
					.CurveTo(Base(-firstFold.Width * Random(0.25f, 0.35f)), firstFold.Top - bevel * Random(0f, 0.25f), Base(-firstFold.Width * Random(0.65f, 0.75f)), firstFold.Top - bevel * Random(0f, 0.25f), Base(), firstFold.Top) // To Outer Top (top side)
					.CurveTo(Base(-CutoutLength * topCutRandom), MathUtils.Lerp(firstFold.Top, cutCentreY, topCutRandom) + bevel * Random(-0.5f, 0.5f), Base(-CutoutLength), cutCentreY) // To Internal Point (outer side top)
					.CurveTo(Base(-CutoutLength * bottomCutRandom), MathUtils.Lerp(cutCentreY, firstFold.Bottom, bottomCutRandom) + bevel * Random(-0.5f, 0.5f), Base(), firstFold.Bottom) // To Outer Bottom (outer side bottom)
					.CurveTo(Base(-firstFold.Width * Random(0.25f, 0.45f)), firstFold.Bottom + bevel * Random(0f, 0.25f), Base(-firstFold.Width * Random(0.55f, 0.75f)), firstFold.Bottom + bevel * Random(0f, 0.25f), Base(-firstFold.Width), firstFold.Bottom) // Back to Inner Bottom (bottom side)
					.ClosePath().FillStroke();

				// First fold connector
				float connectorXBase = s > 0 ? rect.Right : rect.Left;
				float ConnectorBase(float offset = 0f) {
					return connectorXBase + s * offset;
				}
				float firstFoldCurveRandom = Random(0.45f, 0.75f);
				//canvas.SetFillColorGray(0.5f)
				canvas.SetFillColor(canvas.GetMidtoneColor().Darken(0.6f));
				canvas.MoveToRel(rect, leftRight, 0) // Start at Inner Bottom
					.CurveTo(ConnectorBase(-FirstFoldXBack * firstFoldCurveRandom), MathUtils.Lerp(rect.Bottom, firstFold.Bottom, firstFoldCurveRandom) + bevel * Random(0.1f, 0.3f), Base(-firstFold.Width), firstFold.Bottom) // To Outer Bottom (bottom side)
					.LineToRel(firstFold, 1 - leftRight, 1) // To Outer Top (outer side)
					.LineToRel(rect, leftRight, 1) // To Inner Top (top side)
					.CurveTo(ConnectorBase(-0.55f * bevel), rect.CentreY, ConnectorBase(), rect.Bottom) // Allow for maximum curvature of central space side
					.ClosePath().FillStroke();

			}

			DrawDetails(0); // Left Side
			DrawDetails(1); // Right Side

			// Central space
			//canvas.SetFillColorGray(1f).Rectangle(rect).FillStroke();
			canvas.SetFillColor(canvas.GetBackgroundColor());
			canvas.MoveTo(rect.Left, rect.Bottom) // Start at Bottom Left
				.CurveTo(rect.Left + bevel * Random(0.25f, 0.5f), MathUtils.Lerp(rect.Bottom, rect.Top, Random(0.25f, 0.75f)), rect.Left, rect.Top) // To Top Left (left side)
				.CurveTo(MathUtils.Lerp(rect.Left, rect.Right, Random(0.25f, 0.75f)), rect.Top - bevel * Random(0.25f, 0.5f), rect.Right, rect.Top) // To Top Right (top side)
				.CurveTo(rect.Right - bevel * Random(0.25f, 0.5f), MathUtils.Lerp(rect.Bottom, rect.Top, Random(0.25f, 0.75f)), rect.Right, rect.Bottom) // To Bottom Right (right side)
				.CurveTo(MathUtils.Lerp(rect.Left, rect.Right, Random(0.25f, 0.75f)), rect.Bottom - bevel * Random(0.25f, 0.5f), rect.Left, rect.Bottom) // To Bottom Left (bottom side)
				.ClosePath();
			canvas.FillStroke();

			canvas.RestoreState();
		}
	}

}
