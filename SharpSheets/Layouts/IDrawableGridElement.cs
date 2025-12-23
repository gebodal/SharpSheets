using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Colors;

namespace SharpSheets.Layouts {

	public interface IDrawableGridElement : IGridElement {

		/// <summary></summary>
		/// <exception cref="Exceptions.SharpDrawingException"></exception>
		/// <exception cref="InvalidRectangleException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		void Draw(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken);

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		Rectangle?[] DiagnosticRects(ISharpGraphicsState graphicsState, Rectangle available); // TODO This should be better integrated?

	}

	public static class DrawableElementUtils {

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="InvalidRectangleException"></exception>
		public static void DrawDiagnostics(ISharpCanvas canvas, IDrawableGridElement drawable, string name, Rectangle fullRect, Rectangle marginRect, Rectangle? remainingRect) {
			Rectangle?[] additionalRects;
			try {
				additionalRects = drawable.DiagnosticRects(canvas, marginRect);
			}
			catch (InvalidRectangleException) {
				additionalRects = Array.Empty<Rectangle?>();
			}

			canvas.SaveState().SetLineWidth(0.1f);

			canvas.SetStrokeColor(Color.Orange);
			for (int i = 0; i < additionalRects.Length; i++) {
				if (additionalRects[i] != null) {
					canvas.Rectangle(additionalRects[i]!).Stroke();
				}
			}

			canvas.SetStrokeColor(Color.Green);
			canvas.Rectangle(fullRect).Stroke();
			canvas.SetStrokeColor(Color.Blue);
			canvas.Rectangle(marginRect).Stroke();
			if (remainingRect != null) {
				canvas.SetStrokeColor(Color.Pink);
				canvas.Rectangle(remainingRect).Stroke();
			}

			Dimension? mySize = drawable.Size;
			if (mySize.HasValue && mySize?.Relative == -1 && mySize?.Absolute == 0 && mySize?.Percent == 0) {
				Size minRect = drawable.MinimumSize(canvas, drawable.Layout, (Size)marginRect); // TODO Is this the right layout?
				mySize = Dimension.FromPoints(drawable.Layout == LayoutDirection.COLUMNS ? minRect.Width : minRect.Height);
			}

			float maxFontsize = Math.Min(marginRect.Height / 20f, 6f);
			float minFontsize = Math.Min(maxFontsize / 50f, 0.1f);
			float fontSearchEps = (maxFontsize - minFontsize) / 50f;
			FontSizeSearchParams fontParams = new FontSizeSearchParams(minFontsize, maxFontsize, fontSearchEps);

			canvas.SetTextColor(Color.Blue);
			canvas.SetFonts(null); // Set fonts back to defaults
			//canvas.FitRichText(marginRect, $"{GetType().Name} ({localName}): {marginRect}, size = {mySize}", new FontSpecification(6f, 1.35f, 0f), 0.1f, 6f, 0.1f, 0, 0);
			canvas.FitRichText(marginRect, (RichString)$"{name}: {marginRect}, size = {mySize}", new ParagraphSpecification(1.35f, 0f, 0f, 0f), fontParams, Justification.LEFT, Alignment.BOTTOM, TextHeightStrategy.LineHeightDescent, false);
			canvas.RestoreState();
		}

	}

}
