using SharpSheets.Canvas;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpSheets.Colors;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Exceptions;
using SharpSheets.Cards.Card.SectionRects;

namespace SharpSheets.Cards.Card
{

    public class CardCollection : IDocumentContent {

		private struct CardGrouping {
			public CardSetConfig config;
			public DynamicCard[] cards;
			public CardGrouping(CardSetConfig config, DynamicCard[] cards) {
				this.config = config;
				this.cards = cards;
			}
		}

		private class ConfigNameComparer : IEqualityComparer<CardConfig>, IEqualityComparer<CardSetConfig> {
			public static readonly ConfigNameComparer Instance = new ConfigNameComparer();

			public bool Equals(CardConfig? x, CardConfig? y) {
				if (x is null || y is null) {
					return x is null && y is null;
				}
				return x.cardSetConfig.name == y.cardSetConfig.name;
			}

			public int GetHashCode(CardConfig obj) {
				return obj.cardSetConfig.name.GetHashCode();
			}

			public bool Equals(CardSetConfig? x, CardSetConfig? y) {
				if (x is null || y is null) {
					return x is null && y is null;
				}
				return x.name == y.name;
			}

			public int GetHashCode(CardSetConfig obj) {
				return obj.name.GetHashCode();
			}
		}

		public bool HasContent { get { return cardGroupings.Length > 0 || errorCards.Length > 0; } }

		private readonly DynamicCard[] errorCards;
		private readonly CardGrouping[] cardGroupings;

		public IEnumerable<DynamicCard> Cards { get { return cardGroupings.SelectMany(g => g.cards).Concat(errorCards); } }

		public CardCollection(DynamicCard[][] cards) {
			List<CardGrouping> groupings = new List<CardGrouping>();
			List<DynamicCard> errorCards = new List<DynamicCard>();

			foreach(DynamicCard[] segment in cards) {
				List<CardSetConfig> configurations = new List<CardSetConfig>();
				Dictionary<CardSetConfig, List<DynamicCard>> separated = new Dictionary<CardSetConfig, List<DynamicCard>>(ConfigNameComparer.Instance);
				foreach(DynamicCard card in segment) {
					if (card.subject.CardConfig == null) {
						errorCards.Add(card);
					}
					else {
						if (!separated.ContainsKey(card.subject.CardConfig.cardSetConfig)) {
							configurations.Add(card.subject.CardConfig.cardSetConfig);
							separated.Add(card.subject.CardConfig.cardSetConfig, new List<DynamicCard>());
						}
						separated[card.subject.CardConfig.cardSetConfig].Add(card);
					}
				}
				foreach(CardSetConfig config in configurations) {
					groupings.Add(new CardGrouping(config, separated[config].ToArray()));
				}
			}

			this.cardGroupings = groupings.ToArray();
			this.errorCards = errorCards.ToArray();
		}

		public void DrawTo(ISharpDocument document, out SharpDrawingException[] errors, CancellationToken cancellationToken) {
			Draw(document, out List<SharpDrawingException> errorList, cancellationToken);
			errors = errorList.ToArray();
		}

		private void Draw(ISharpDocument document, out List<SharpDrawingException> errors, CancellationToken cancellationToken) {

			errors = new List<SharpDrawingException>();
			List<DynamicCard> allErrorCards = new List<DynamicCard>(errorCards);

			foreach (CardGrouping cardGrouping in cardGroupings) {
				if (cancellationToken.IsCancellationRequested) { return; }

				cardGrouping.config.layoutStrategy.Draw(document, cardGrouping.config, cardGrouping.cards, out List<SharpDrawingException> groupErrors, out List<DynamicCard> groupErrorCards, cancellationToken);

				errors.AddRange(groupErrors);
				allErrorCards.AddRange(groupErrorCards);
			}

			if (allErrorCards.Count > 0) {
				try {
					ISharpCanvas errorCanvas = document.AddNewPage(cardGroupings.LastOrDefault().config?.paper ?? PageSize.A4);
					errorCanvas.SetTextColor(Color.Red);
					string errorText = string.Join("\n", allErrorCards.Select(c => $"Error for card \"{c.subject.Name.Value}\""));
					errorCanvas.FitText(errorCanvas.CanvasRect.MarginsRel(0.05f, false), errorText, new ParagraphSpecification(1.35f, 0f, default), new FontSizeSearchParams(0.5f, 20f, 0.1f), Justification.LEFT, Alignment.TOP, TextHeightStrategy.LineHeightDescent, false);
				}
				catch (InvalidOperationException e) {
					errors.Add(new SharpDrawingException(this, "Could not report drawing error messages.", e));
				}
			}
		}
	}

	public class ErrorCard : AbstractCard {

		protected readonly string message;

		public override IFixedCardSectionRect[] Sections { get; } = Array.Empty<IFixedCardSectionRect>();

		public override bool CropOnFinalCard => true;

		public ErrorCard(string message) {
			this.message = message;
		}

		public override float Gutter { get { return 0f; } }

		public override void DrawHeader(ISharpCanvas canvas, Rectangle rect, int card, int totalCards) { }

		public override void DrawOutline(ISharpCanvas canvas, Rectangle[] rects, out IEnumerable<Rectangle> outlineRects) {
			canvas.SaveState();
			canvas.SetStrokeColor(Color.Red).SetFillColor(Color.Red);
			foreach (Rectangle rect in rects) {
				canvas.Rectangle(rect).Stroke();
				canvas.FitRichText(rect, (RichString)message, new ParagraphSpecification(1.35f, 0f, 0f, 0f), new FontSizeSearchParams(1f, 15f, 1f), Justification.LEFT, Alignment.TOP, TextHeightStrategy.LineHeightDescent, true);
			}
			canvas.RestoreState();
			outlineRects = rects;
		}

		public override void DrawGutter(ISharpCanvas canvas, Rectangle rect) { }

		public override Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle rect, int card, int totalCards) {
			return rect;
		}
	}

}