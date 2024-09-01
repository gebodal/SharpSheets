using SharpSheets.Layouts;
using SharpSheets.Shapes;
using SharpSheets.Widgets;
using SharpSheets.Canvas;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Cards.Card.SegmentRects;
using SharpSheets.Cards.CardConfigs;

namespace SharpSheets.Cards.Card {

	public class DynamicCard : AbstractCard {

		public CardSubject Subject { get; }
		public CardSetConfig CardSetConfig => Subject.CardConfig.cardSetConfig;
		public CardConfig CardConfig => Subject.CardConfig;

		public IDetail? GutterStyle => CardConfig.gutterStyle;
		public readonly ArrangementCollection<IWidget> backgrounds;
		public readonly ArrangementCollection<IWidget> outlines;

		public bool JoinSplitCards => CardConfig.joinSplitCards;

		public override bool CropOnFinalCard => CardConfig.cropOnFinalCard;

		public override float Gutter => CardConfig.gutter;

		public DynamicCard(CardSubject subject, ArrangementCollection<IWidget> backgrounds, ArrangementCollection<IWidget> outlines, IFixedCardSegmentRect[] segments) {
			this.Subject = subject;
			this.backgrounds = backgrounds;
			this.outlines = outlines;
			this.Segments = segments;
		}

		public override IFixedCardSegmentRect[] Segments { get; }


		protected static IWidget GetFrom(ArrangementCollection<IWidget> collection, int card, int totalCards) {
			//IWidget rect = collection.GetValue(DynamicCardEnvironments.CardNumberEnvironment(subject.Environment, card, totalCards));
			/*
			IWidget rect = collection.GetValue(subject.Environment.AppendDefinitionEnvironment(CardOutlinesEnvironments.GetEnvironment(card, totalCards)));
			if (rect == null) {
				rect = new Div(new WidgetSetup());
			}
			return rect;
			*/
			IWidget rect = collection.GetValue(card, totalCards);
			if (rect == null) {
				rect = new Div(new WidgetSetup());
			}
			return rect;
		}

		protected IWidget GetOutline(int card, int totalCards) {
			return GetFrom(outlines, card, totalCards);
		}

		protected IWidget GetBackground(int card, int totalCards) {
			return GetFrom(backgrounds, card, totalCards);
		}

		public override void DrawOutline(ISharpCanvas canvas, Rectangle rect, int card, int totalCards) {
			IWidget outline = GetOutline(card, totalCards);
			IWidget background = GetBackground(card, totalCards);
			Rectangle outlineRect = background.RemainingRect(canvas, rect) ?? rect;
			outline.Draw(canvas, outlineRect, default);
		}

		public override void DrawBackground(ISharpCanvas canvas, Rectangle[] rects) {
			List<Rectangle> outlineRects = new List<Rectangle>();

			if (JoinSplitCards) {
				IWidget background = GetBackground(0, 1);
				Rectangle overallRect = Rectangle.GetCommonRectangle(rects);
				background.Draw(canvas, overallRect, default);
				outlineRects.Add(overallRect);
			}
			else {
				for (int i = 0; i < rects.Length; i++) {
					IWidget background = GetBackground(i, rects.Length);
					background.Draw(canvas, rects[i], default);
				}
				outlineRects.AddRange(rects);
			}

			foreach (Rectangle outlineRect in outlineRects) {
				canvas.RegisterAreas(this, outlineRect, null, Array.Empty<Rectangle>());
				canvas.RegisterAreas(Subject, outlineRect, null, Array.Empty<Rectangle>());
			}
		}

		public override void DrawGutter(ISharpCanvas canvas, Rectangle rect) {
			if (GutterStyle != null) {
				GutterStyle.Layout = Layout.ROWS;
				GutterStyle.Draw(canvas, rect);
			}
		}

		public override Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle rect, int card, int totalCards) {
			IWidget background = GetBackground(card, totalCards);
			IWidget outline = GetOutline(card, totalCards);

			// TODO Yes? Is this the right setup?
			rect = background?.RemainingRect(graphicsState, rect) ?? rect;
			rect = outline?.RemainingRect(graphicsState, rect) ?? rect;

			return rect;
		}

	}
}