using SharpSheets.Layouts;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using SharpSheets.Canvas;
using SharpSheets.Fonts;
using SharpSheets.Canvas.Text;
using SharpSheets.Parsing;

namespace SharpSheets.Cards.Card {

	/// <summary>
	/// 
	/// </summary>
	public sealed class CardText : SharpWidget {

		public class ParagraphDataArg : ISharpArgsGrouping {
			public readonly float Spacing;
			public readonly ParagraphIndent Indent;

			public ParagraphDataArg(float spacing = 0f, float indent = 0f, float hanging = 0f) {
				this.Spacing = spacing;
				this.Indent = new ParagraphIndent(indent, hanging);
			}
		}

		public class BulletArg : ISharpArgsGrouping {
			public readonly string? Symbol;
			public readonly FontSetting? FontPath;
			public readonly float FontSizeMultiplier;
			public readonly float Indent;
			public readonly float Offset;

			/// <summary>
			/// 
			/// </summary>
			/// <param name="symbol"></param>
			/// <param name="font"></param>
			/// <param name="size"></param>
			/// <param name="indent"></param>
			/// <param name="offset"></param>
			public BulletArg(string? symbol = null, FontSetting? font = null, float size = 1f, float indent = 0f, float offset = 0f) {
				Symbol = !string.IsNullOrWhiteSpace(symbol) ? symbol : null;
				FontPath = font;
				FontSizeMultiplier = Math.Max(0f, size);
				Indent = indent;
				Offset = offset;
			}
		}

		private readonly RichString text;
		private readonly Justification justification;
		private readonly Alignment alignment;
		private readonly TextHeightStrategy heightStrategy;
		private readonly float multiplier;

		public readonly BulletArg bullet;

		private readonly ParagraphSpecification paragraphSpec;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="setup"></param>
		/// <param name="text"></param>
		/// <param name="justification"></param>
		/// <param name="alignment"></param>
		/// <param name="heightStrategy"></param>
		/// <param name="lineSpacing"></param>
		/// <param name="paragraph"></param>
		/// <param name="multiplier"></param>
		/// <param name="bullet"></param>
		public CardText(
			WidgetSetup setup,
			List<RichString>? text = null,
			Justification justification = Justification.LEFT,
			Alignment alignment = Alignment.TOP,
			TextHeightStrategy heightStrategy = TextHeightStrategy.LineHeightBaseline,
			float lineSpacing = 1.35f,
			ParagraphDataArg? paragraph = null,
			float multiplier = 1f,
			BulletArg? bullet = null
		) : base(setup) {

			this.text = RichString.Join("\n", TextFormat.REGULAR, text ?? Enumerable.Empty<RichString>());
			this.justification = justification;
			this.alignment = alignment;
			this.heightStrategy = heightStrategy;
			paragraph ??= new ParagraphDataArg();
			this.paragraphSpec = new ParagraphSpecification(lineSpacing, paragraph.Spacing, paragraph.Indent);
			this.multiplier = multiplier;

			this.bullet = bullet ?? new BulletArg();
		}

		private float GetFontSize(ISharpGraphicsState graphicsState) {
			return multiplier * graphicsState.GetTextSize();
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			float finalFontSize = GetFontSize(canvas);

			RichString[][] lines = RichStringLineSplitting.SplitParagraphs(canvas, text, rect.Width, finalFontSize, paragraphSpec, true);

			if (!string.IsNullOrEmpty(bullet.Symbol)) {
				float firstLineY = RichStringLayout.GetStartY(canvas, rect, lines, finalFontSize, paragraphSpec, alignment, heightStrategy);

				float bulletFontSize = bullet.FontSizeMultiplier * finalFontSize;
				float bulletIndent = bullet.Indent;
				float bulletOffset = bullet.Offset * finalFontSize;

				canvas.SaveState();
				canvas.SetTextSize(bulletFontSize);
				canvas.SetFont(TextFormat.REGULAR, bullet.FontPath);
				canvas.DrawText(bullet.Symbol, rect.Left + bulletIndent, firstLineY + bulletOffset);
				canvas.RestoreState();
			}

			canvas.DrawRichText(rect, lines, finalFontSize, paragraphSpec, justification, alignment, heightStrategy, true);
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			float finalFontSize = GetFontSize(graphicsState);

			RichString[][] singleLineParagraphs = text.Split('\n').Select(s => new RichString[] { s }).ToArray();

			// TODO Does this work now?
			float width = Math.Min(availableSpace.Width, RichStringLayout.CalculateWidth(graphicsState, singleLineParagraphs, finalFontSize, paragraphSpec, true));
			float height = RichStringLayout.CalculateHeight(graphicsState, text, availableSpace.Width, finalFontSize, paragraphSpec, heightStrategy, true);

			return new Size(width, height);
		}
	}

}