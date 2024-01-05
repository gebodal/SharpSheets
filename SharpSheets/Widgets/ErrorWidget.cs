using System;
using SharpSheets.Colors;
using System.Threading;
using SharpSheets.Layouts;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Exceptions;

namespace SharpSheets.Widgets {

	public sealed class ErrorWidget : SharpWidget {

		private readonly string message;
		private readonly Exception error;

		public ErrorWidget(string message, Exception error, WidgetSetup setup) : base(setup) { // new WidgetSetup(default, 1.35f, Color.Red, Color.White, Color.Orange, Color.Black, null, null, 0f, null, Dimension.Single, null, Layout.ROWS, Arrangement.FRONT)
			this.message = message;
			this.error = error;
		}

		public ErrorWidget(Exception error, WidgetSetup setup) : this(error.Message, error, setup) { }

		public override void Draw(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			canvas.SaveState();

			canvas.SetStrokeColor(Color.Red).SetLineWidth(2 * canvas.GetDefaultLineWidth());
			canvas.Rectangle(rect).Stroke();
			canvas.MoveTo(rect.Left, rect.Top)
				.LineTo(rect.Right, rect.Bottom)
				.Stroke();
			canvas.MoveTo(rect.Right, rect.Top)
				.LineTo(rect.Left, rect.Bottom)
				.Stroke();

			canvas.SetTextFormatAndSize(TextFormat.REGULAR, 12f);
			ParagraphSpecification paraSpec = new ParagraphSpecification(1.35f, 0f, default);
			FontSizeSearchParams searchParams = new FontSizeSearchParams(0.1f, 12f, 0.1f);
			canvas.FitText(rect.Margins(canvas.GetLineWidth(), false), message, paraSpec, searchParams, Justification.LEFT, Alignment.TOP, TextHeightStrategy.LineHeightDescent, false);

			canvas.RestoreState();

			if(error is SharpParsingException || error is SharpFactoryException || error is SharpInitializationException) {
				// Do nothing, this error has already been logged during parsing
			}
			else if (error != null) {
				canvas.LogError(this, message, error);
			}
			else {
				canvas.LogError(this, message);
			}
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {
			//throw new NotImplementedException();
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			// This may be one of the few times we want to include the base minimum rect, as this widget wants to still represent its children
			return Layouts.Size.Combine(base.GetMinimumContentSize(graphicsState, availableSpace) ?? new Size(0f, 0f), new Size(0, graphicsState.GetTextSize()));
		}
	}

}
