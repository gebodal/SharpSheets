using SharpSheets.Layouts;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using SharpSheets.Canvas;
using SharpSheets.Fonts;
using SharpSheets.Canvas.Text;
using SharpSheets.Parsing;

namespace SharpSheets.Cards.Card {

	/// <summary>
	/// This widget is used for drawing text inside cards, where the font size is determined dynamically by the card
	/// layout engine. The final font size can be adjusted using a multiplier, but cannot be set directly. The text can be
	/// aligned within the available area, and can be drawn with a "bullet point" (the exact symbol for which can be
	/// specified). Indentation and line-spacing can also be specified.
	/// </summary>
	public sealed class CardText : SharpWidget {

		public class ParagraphDataArg : ISharpArgsGrouping {
			public readonly float Spacing;
			public readonly ParagraphIndent Indent;

			/// <summary>
			/// Constructor for ParagraphDataArg.
			/// </summary>
			/// <param name="spacing">The spacing to be used between paragraphs of text, measured in points.
			/// This spacing is in addition to any line spacing.</param>
			/// <param name="indent">The indentation length for the first line of text in a paragraph,
			/// measured in points.</param>
			/// <param name="hanging">The indentation length for each line after the first (whose indentation
			/// is controlled using <paramref name="indent"/>), measured in points.</param>
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
			/// Constructor for BulletArg.
			/// </summary>
			/// <param name="symbol">The text character(s) to use for the bullet point symbol.</param>
			/// <param name="font">The font with which the symbol character(s) will be drawn.</param>
			/// <param name="size">The font size for the symbol, as a factor of the main text font size.</param>
			/// <param name="indent">The indentation, in points, to use for the symbol.</param>
			/// <param name="offset">The offset for the symbol from the text baseline, as a factor of the text font size.</param>
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
		/// Constructor for CardText widget.
		/// </summary>
		/// <param name="setup"></param>
		/// <param name="text">The text to be displayed in this widget, which can be formatted as rich text.
		/// The provided entries will be treated as separate lines of text.</param>
		/// <param name="justification">The horizontal justification to use for the text within the available area.</param>
		/// <param name="alignment">The vertical alignment to use for the text within the available area.</param>
		/// <param name="heightStrategy">The height calculation strategy to use when arranging the text within the available area.</param>
		/// <param name="lineSpacing">This parameter sets the line spacing, which is the distance between successive
		/// text baselines, as amultiple of the drawing fontsize.</param>
		/// <param name="paragraph">Paragraph data for this widget.</param>
		/// <param name="multiplier">A multiplier for the font size, which will adjust the font size determined
		/// by the card layout algorithm when drawing the text for this widget.</param>
		/// <param name="bullet">Bullet data for this widget.</param>
		/// <size>0 0</size>
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