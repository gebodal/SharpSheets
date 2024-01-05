using SharpSheets.Canvas;
using SharpSheets.Fonts;
using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Card;
using SharpSheets.Exceptions;
using SharpSheets.Widgets;
using SharpSheets.Cards.Card.SectionRects;

namespace SharpSheets.Cards.Layouts
{

    public class CardLayoutStrategy : AbstractLayoutStrategy {

		public override void Draw(ISharpDocument document, CardSetConfig config, DynamicCard[] cards, out List<SharpDrawingException> errors, out List<DynamicCard> errorCards, CancellationToken cancellationToken) {
			errors = new List<SharpDrawingException>();
			errorCards = new List<DynamicCard>();

			if (cancellationToken.IsCancellationRequested) { return; }

			PageSize pageSize = config.paper;
			Margins pageMargin = config.pageMargins;
			float cardGutter = config.cardGutter;
			int rows = config.rows;
			int columns = config.columns;
			//FontPathGrouping fonts = config.fonts;

			//int maxCards = Math.Min(config.MaxCards, columns); // TODO This should be more fluid. Can we extend to multiple row cards?

			List<DynamicCard> cardList = new List<DynamicCard>(cards);

			Rectangle pageRect = new Rectangle(0, 0, pageSize.Width, pageSize.Height);
			Rectangle[][] grid = Divisions.Grid(pageRect.Margins(pageMargin, false), rows, columns, cardGutter, cardGutter, out _, out _).ToJaggedArray();
			Rectangle sampleRect = grid[0][0];

			Dictionary<AbstractCard, CardArrangement?> existingLayouts = new Dictionary<AbstractCard, CardArrangement?>();

			while (cardList.Count > 0) {
				if (cancellationToken.IsCancellationRequested) { return; }

				ISharpCanvas canvas = document.AddNewPage(pageSize);

				for (int row = 0; row < grid.Length; row++) {
					if (cancellationToken.IsCancellationRequested) { return; }

					Rectangle[] available = grid[row];
					while (available.Length > 0) {
						if (cancellationToken.IsCancellationRequested) { return; }

						IEnumerator<DynamicCard> cardEnumerator = cardList.ToList().GetEnumerator();

						DynamicCard? currentCard = null;
						CardArrangement? nextLayout = null;

						// Find next card that fits in this row
						while (nextLayout == null && cardEnumerator.MoveNext()) {
							if (cancellationToken.IsCancellationRequested) { return; }

							currentCard = cardEnumerator.Current;

							if (currentCard.subject.CardConfig.fonts != null) { canvas.SetFonts(currentCard.subject.CardConfig.fonts); }

							if (!existingLayouts.ContainsKey(currentCard)) {
								existingLayouts.Add(currentCard, GetLayouts(currentCard, new CanvasStateImage(canvas), currentCard.subject.CardConfig.MaxCards, sampleRect, currentCard.subject.CardConfig.paragraphSpec, currentCard.subject.CardConfig.fontParams, cancellationToken));
							}
							if (cancellationToken.IsCancellationRequested) { return; }

							nextLayout = existingLayouts[currentCard];

							if (nextLayout is not null && nextLayout.Length > available.Length) {
								if (nextLayout.Length > currentCard.subject.CardConfig.MaxCards) {
									errors.Add(new SharpDrawingException(currentCard.subject, "Card cannot be fit into a single row."));
									cardList.Remove(currentCard);
									errorCards.Add(currentCard);
								}

								nextLayout = null;
							}
						}

						if (nextLayout == null) {
							break;
						}
						else if(currentCard != null) {
							//Console.WriteLine($"nextLayout.Length = {nextLayout.Length}");

							canvas.SaveState();
							canvas.SetTextSize(nextLayout.FontSize);
							Draw(canvas, currentCard, nextLayout, available.Take(nextLayout.Length).ToArray(), cancellationToken);
							canvas.RestoreState();
							if (cancellationToken.IsCancellationRequested) { return; }

							available = available.Skip(nextLayout.Length).ToArray();
							cardList.Remove(currentCard);
						}
						else {
							throw new InvalidOperationException("Invalid card drawing state."); // This should never happen
						}
					}
				}
			}
		}

		public static void Draw(ISharpCanvas canvas, DynamicCard card, CardArrangement arrangement, Rectangle[] rects, CancellationToken cancellationToken) {

			//Console.WriteLine($"Draw card from layouts.");

			if (cancellationToken.IsCancellationRequested) { return; }

			// TODO Are these checks actually necessary?
			if (arrangement.Length != rects.Length) {
				throw new ArgumentException($"Same number of layouts and rects must be provided. {arrangement.Length} (layouts) != {rects.Length} (rects)");
			}
			if (rects.Any(r => arrangement.sampleSize.Width != r.Width || arrangement.sampleSize.Height != r.Height)) {
				throw new ArgumentException($"Provided rects are not all same dimensions as layout sample size.");
			}

			card.DrawOutline(canvas, rects, out IEnumerable<Rectangle> outlineRects);

			foreach (Rectangle rect in outlineRects) {
				canvas.RegisterAreas(card, rect, null, Array.Empty<Rectangle>());
				canvas.RegisterAreas(card.subject, rect, null, Array.Empty<Rectangle>());
			}

			for (int cardIdx = 0; cardIdx < arrangement.Length; cardIdx++) {
				if (cancellationToken.IsCancellationRequested) { return; }

				//Console.WriteLine($"Draw layout {i}");
				SingleCardLayout layout = arrangement.layouts[cardIdx];
				Rectangle fullRect = rects[cardIdx];
				bool lastCard = cardIdx == (arrangement.Length - 1);

				card.DrawHeader(canvas, fullRect, cardIdx, arrangement.Length);
				Rectangle featuresFullRect = card.RemainingRect(canvas, fullRect, cardIdx, arrangement.Length);

				Rectangle?[] sectionRects = GetSectionRects(card, layout, featuresFullRect, lastCard, out Rectangle?[] sectionGutters);

				for (int gutterIdx = 0; gutterIdx < sectionGutters.Length; gutterIdx++) {
					if (sectionGutters[gutterIdx] is not null) {
						card.DrawGutter(canvas, sectionGutters[gutterIdx]!);
					}
				}

				for (int secIdx = 0; secIdx < layout.boxes.Length; secIdx++) {
					if (cancellationToken.IsCancellationRequested) { return; }
					if (sectionRects[secIdx] is not null) {
						try {
							layout.boxes[secIdx].Draw(canvas, sectionRects[secIdx]!, arrangement.FontSize, arrangement.paragraphSpec);
						}
						catch(InvalidRectangleException e) {
							canvas.LogError(new SharpDrawingException(layout.boxes[secIdx], "Invalid rectangle.", e));
							if (layout.boxes[secIdx].Config is AbstractCardSectionConfig sectionConfig) {
								canvas.LogError(new SharpDrawingException(sectionConfig, "Invalid rectangle.", e));
							}
							if (sectionRects[secIdx]!.Width > 0 && sectionRects[secIdx]!.Height > 0) {
								new ErrorWidget(e, new WidgetSetup()).Draw(canvas, sectionRects[secIdx]!, cancellationToken);
							}
						}
					}
				}
			}
		}

		private static Rectangle?[] GetSectionRects(DynamicCard card, SingleCardLayout layout, Rectangle rect, bool lastCard, out Rectangle?[] gutters) { // lastCard = cardIdx == (layouts.Length - 1)
			float totalAvailableHeight = layout.areas.Select(r => r.Height).Sum();

			float[] heights = new float[layout.boxes.Length];
			float totalRemaining = 0f;
			for (int secIdx = 0; secIdx < layout.boxes.Length; secIdx++) {
				float remainingHeight = layout.areas[secIdx].Height - layout.minHeights[secIdx];

				totalRemaining += remainingHeight;
				heights[secIdx] = layout.areas[secIdx].Height - remainingHeight;
				//Console.WriteLine($"Rect {j} initial height: {layout.rects[j].Height} (remaining: {remainingHeight})");
			}

			Rectangle?[] sectionRects;
			Rectangle?[] sectionGutters;
			if (card.CropOnFinalCard && lastCard && heights.Length == 1 && heights[0] < totalAvailableHeight / 2) {
				sectionRects = new Rectangle?[] { Divisions.Row(rect, Dimension.FromPoints(totalAvailableHeight / 2), card.Gutter, out _, out _, true, Arrangement.FRONT, LayoutOrder.FORWARD) };
				sectionGutters = Array.Empty<Rectangle>();
			}
			else if (heights.All(f => f < totalAvailableHeight / heights.Length) && layout.boxes.All(b => b.AcceptRemaining)) {
				sectionRects = Divisions.Rows(rect, heights.Length, card.Gutter, out sectionGutters);
			}
			else {
				int numAccepting = layout.boxes.Where(b => b.AcceptRemaining).Count();
				for (int secIdx = 0; secIdx < heights.Length; secIdx++) {
					if (layout.boxes[secIdx].AcceptRemaining) {
						heights[secIdx] += totalRemaining / numAccepting;
					}
				}
				sectionRects = Divisions.Rows(rect, Dimension.FromPoints(heights), card.Gutter, card.Gutter, out _, out sectionGutters, out _, false, Arrangement.FRONT, LayoutOrder.FORWARD);
			}

			gutters = sectionGutters;
			return sectionRects;
		}

		#region Card Layouts Behaviour

		/// <summary>Find layouts for subject cards, given a maximum possible number of cards and assuming cards of a fixed size based on the sample provided.</summary>
		public static CardArrangement? GetLayouts(AbstractCard card, ISharpGraphicsState graphicsState, int maxCards, Rectangle sampleRect, ParagraphSpecification spec, FontSizeSearchParams searchParams, CancellationToken cancellationToken) {

			if (cancellationToken.IsCancellationRequested) { return null; }

			ICardSectionRect[][][] possibleArrangements = GetPossibleArrangements(card, maxCards);

			CardArrangement? bestLayout = null;
			CardQueryCache cache = new CardQueryCache(graphicsState);

			//Console.WriteLine($"{possibleArrangements.Length} arrangements to try");

			for (int arrangementNum = 0; arrangementNum < possibleArrangements.Length; arrangementNum++) {
				if (cancellationToken.IsCancellationRequested) { return bestLayout; }

				if (bestLayout != null && bestLayout.FontSize > searchParams.Min && possibleArrangements[arrangementNum].Length > bestLayout.Length) {
					continue; // I think this won't ever disregard possible improvements...
				}

				CardArrangement? arrangementLayout = LayoutArrangementFontBisectionSearch(card, graphicsState, cache, sampleRect, possibleArrangements[arrangementNum], spec, searchParams.Max, searchParams.Eps);

				//Console.WriteLine(string.Join(" | ", arrangementLayout.layouts.Select(l => string.Join(", ", l.boxes.Select(b => b.GetType().Name)))));

				if (IsBetterLayout(bestLayout, arrangementLayout, searchParams)) {
					bestLayout = arrangementLayout;
				}
			}

			return bestLayout;
		}

		private static bool IsBetterLayout(CardArrangement? oldLayout, CardArrangement? newLayout, FontSizeSearchParams searchParams) {
			//Console.WriteLine($"Existing: font size {oldLayout?.FontSize ?? -1f}, length {oldLayout?.Length ?? -1}, split {oldLayout?.penalisedSplitCount ?? -1}; New: font size {newLayout?.FontSize ?? -1f}, length {newLayout?.Length ?? -1f}, split {newLayout?.penalisedSplitCount ?? -1}");
			if(newLayout == null) {
				return false;
			}
			else if (newLayout.FontSize < 0) {
				//Console.WriteLine("Cannot be better");
				return false;
			}
			else if (oldLayout == null || (oldLayout.FontSize < searchParams.Min && newLayout.FontSize > oldLayout.FontSize)) {
				//Console.WriteLine($"Better by no contest ({(oldLayout == null ? "null" : "oldLayout")} == null || ({oldLayout?.FontSize ?? -1f} < {searchParams.Min} && {newLayout?.FontSize ?? -1f} > {oldLayout?.FontSize ?? -1f}))");
				return true; // No contest
			}
			else {
				if (newLayout.FontSize > oldLayout.FontSize && newLayout.Length <= oldLayout.Length) {
					//Console.WriteLine($"Better by bigger font ({newLayout.FontSize} > {oldLayout.FontSize} && {newLayout.Length} <= {oldLayout.Length})");
					return true; // Bigger font
				}
				else if (newLayout.FontSize >= searchParams.Min && newLayout.Length < oldLayout.Length) {
					//Console.WriteLine($"Better by fewer cards ({newLayout.FontSize} >= {searchParams.Min} && {newLayout.Length} < {oldLayout.Length})");
					return true; // Fewer cards
				}
				else if (newLayout.FontSize >= oldLayout.FontSize && newLayout.Length <= oldLayout.Length && newLayout.penalisedSplitCount < oldLayout.penalisedSplitCount) {
					//Console.WriteLine($"Better by fewer splits ({newLayout.FontSize} >= {oldLayout.FontSize} && {newLayout.Length} <= {oldLayout.Length} && {newLayout.penalisedSplitCount} < {oldLayout.penalisedSplitCount})");
					return true; // Fewer splits
				}
				else if (newLayout.FontSize >= oldLayout.FontSize && newLayout.Length <= oldLayout.Length && FrontLoading(newLayout) > FrontLoading(oldLayout) && newLayout.penalisedSplitCount <= oldLayout.penalisedSplitCount) {
					//Console.WriteLine($"Better by frontloading ({newLayout.FontSize} >= {oldLayout.FontSize} && {newLayout.Length} <= {oldLayout.Length} && {FrontLoading(newLayout)} > {FrontLoading(oldLayout)} && {newLayout.penalisedSplitCount} <= {oldLayout.penalisedSplitCount})");
					return true; // Better frontloading
				}
			}
			//Console.WriteLine("Not better");
			return false;
		}

		private static int FrontLoading(CardArrangement arrangement) {
			int score = 0;
			for (int i = 0; i < arrangement.Length - 1; i++) {
				score += arrangement.layouts[i].Length;
				for (int j = i + 1; j < arrangement.Length; j++) {
					score -= arrangement.layouts[j].Length;
				}
			}
			return score;
		}

		private static ICardSectionRect[][][] GetPossibleArrangements(AbstractCard card, int maxCards) {

			ICardSectionRect[] boxes = GetSectionRects(card, maxCards).ToArray();
			ICardSectionRect[][][] possibleArrangements = Partitioning.GetAllPartitions(boxes, maxCards).ToArray();

			// Remove sequential duplicates
			for (int i = 0; i < possibleArrangements.Length; i++) { // Over possible arrangements
				for (int j = 0; j < possibleArrangements[i].Length; j++) { // Over cards in this arrangement
					possibleArrangements[i][j] = possibleArrangements[i][j].RemoveRepeats(CardArrangementComparer.BoxEquality).ToArray();
				}
			}

			possibleArrangements = possibleArrangements.Distinct(new CardArrangementComparer()).ToArray();

			// We need to ensure that each arrangement has a separate instance of any IPartialCardBoxes objects,
			// so that RemoveBox doesn't carry over between arrangements.
			for (int i = 0; i < possibleArrangements.Length; i++) { // Over possible arrangements
				Dictionary<IFixedCardSectionRect, IPartialCardSectionRects> partials = new Dictionary<IFixedCardSectionRect, IPartialCardSectionRects>(); // Map from original to clone
				Dictionary<IPartialCardSectionRects, int> partialCounts = new Dictionary<IPartialCardSectionRects, int>();
				for (int j = 0; j < possibleArrangements[i].Length; j++) {
					for (int k = 0; k < possibleArrangements[i][j].Length; k++) {
						if (possibleArrangements[i][j][k] is IPartialCardSectionRects partial) {
							if (!partials.ContainsKey(partial.Parent)) {
								partials.Add(partial.Parent, partial.Clone());
								partialCounts.Add(partials[partial.Parent], 0);
							}
							possibleArrangements[i][j][k] = partials[partial.Parent];
							partialCounts[partials[partial.Parent]] += 1;
						}
					}
				}
				foreach (KeyValuePair<IPartialCardSectionRects, int> entry in partialCounts) {
					entry.Key.Boxes = partialCounts[entry.Key];
				}
			}

			// Sort possible arrangements by number of cards (ascending)
			Array.Sort(possibleArrangements, (a1, a2) => Comparer<int>.Default.Compare(a1.Length, a2.Length));

			return possibleArrangements;
		}

		private static IEnumerable<ICardSectionRect> GetSectionRects(AbstractCard card, int maxCards) {
			for (int i = 0; i < card.Sections.Length; i++) {
				IFixedCardSectionRect box = card.Sections[i];
				if (box.Splittable) {
					IPartialCardSectionRects partial = box.Split(maxCards);
					for (int j = 0; j < maxCards; j++) {
						yield return partial;
					}
				}
				else {
					yield return box;
				}
			}
		}

		private static CardArrangement? LayoutArrangementFontBisectionSearch(AbstractCard card, ISharpGraphicsState graphicsState, CardQueryCache cache, Rectangle sampleRect, ICardSectionRect[][] arrangement, ParagraphSpecification spec, float maxFontSize, float fontEpsilon) {

			int calls = 1;

			Rectangle[] featureRects = new Rectangle[arrangement.Length];
			for (int i = 0; i < arrangement.Length; i++) {
				featureRects[i] = card.RemainingRect(graphicsState, sampleRect, i, arrangement.Length);
				// TODO This doesn't account for variable sized headers!
			}

			graphicsState.SaveState();

			/*
			void Print(string message, ArrangementParameters p) {
				Console.WriteLine(message + $"{p.spec.FontSize}; " + string.Join(", ", p.totalFixedHeights.Zip(featureRects, (f, r) => $"{f} ({r.Height})")));
				Console.WriteLine(string.Join(" | ", p.boxHeights.Select(bh => string.Join(", ", bh.Select(h => h.ToString())))));
			}
			*/

			graphicsState.SetTextSize(maxFontSize);
			ArrangementParameters max = GetParameters(graphicsState, cache, featureRects, arrangement, card.Gutter, maxFontSize, spec);
			//Print("max: ", max);

			ArrangementParameters final;

			// In the simplest case, all boxes fit into their respective cards at maximum font size
			if (max.totalFixedHeights.Zip(featureRects, (t, r) => t <= r.Height).All()) {
				final = max;
			}
			else {
				float minFontSize = 0.5f;
				graphicsState.SetTextSize(minFontSize);
				ArrangementParameters min = GetParameters(graphicsState, cache, featureRects, arrangement, card.Gutter, minFontSize, spec);
				//Print("min: ", min);

				while (max.fontSize - min.fontSize >= fontEpsilon) {
					calls++;
					float midFontSize = (max.fontSize + min.fontSize) / 2f;
					graphicsState.SetTextSize(midFontSize);
					ArrangementParameters midpoint = GetParameters(graphicsState, cache, featureRects, arrangement, card.Gutter, midFontSize, spec);
					//Print("mid: ", midpoint);

					if (midpoint.totalFixedHeights.Zip(featureRects, (t, r) => t <= r.Height).All()) {
						min = midpoint;
					}
					else {
						max = midpoint;
					}
				}

				final = min;
			}

			//Print("final: ", final);

			graphicsState.RestoreState();

			if (!final.validArrangement) {
				return null; // TODO Is this the right approach?
			}

			Size[][] boxSizes = GetBoxSizes(final.boxHeights, featureRects, card.Gutter);

			SingleCardLayout[] layouts = new SingleCardLayout[arrangement.Length];
			for (int i = 0; i < layouts.Length; i++) {
				float finalUnusedHeight = featureRects[i].Height - final.totalFixedHeights[i];
				layouts[i] = new SingleCardLayout((Size)featureRects[i], final.arrangement[i], boxSizes[i], final.boxHeights[i], finalUnusedHeight);
			}
			return new CardArrangement(card, (Size)sampleRect, final.fontSize, final.spec, layouts, final.penalisedSplitCount, calls);
		}

		private static Size[][] GetBoxSizes(float[][] boxHeights, Rectangle[] featureRects, float gutter) {
			Size[][] boxSizes = new Size[boxHeights.Length][];

			for (int i = 0; i < boxHeights.Length; i++) {
				Dimension[] dims = boxHeights[i].Select(h => Dimension.FromPoints(h) + Dimension.FromRelative(1f)).ToArray();
				Rectangle?[] rects = Divisions.Rows(featureRects[i], dims, gutter, false, Arrangement.FRONT, LayoutOrder.FORWARD);

				boxSizes[i] = new Size[boxHeights[i].Length];
				for (int j = 0; j < boxHeights[i].Length; j++) {
					boxSizes[i][j] = (Size?)rects[j] ?? throw new InvalidRectangleException("Cannot get box size.");
				}
			}

			return boxSizes;
		}

		private class ArrangementParameters {

			public readonly float fontSize;
			public readonly ParagraphSpecification spec;
			public readonly IFixedCardSectionRect[][] arrangement;
			public readonly float[][] boxHeights;
			public readonly float[] heightsSums;
			public readonly int penalisedSplitCount;
			public readonly float[] totalFixedHeights;
			public readonly bool validArrangement;

			public ArrangementParameters(float fontSize, ParagraphSpecification spec, IFixedCardSectionRect[][] arrangement, float[][] boxHeights, int penalisedSplitCount, float[] totalFixedHeights, bool validArrangement) {
				this.fontSize = fontSize;
				this.spec = spec;
				this.arrangement = arrangement;
				this.boxHeights = boxHeights;
				this.penalisedSplitCount = penalisedSplitCount;

				this.heightsSums = this.boxHeights.Select(hs => hs.Sum()).ToArray();

				this.totalFixedHeights = totalFixedHeights;

				this.validArrangement = validArrangement;
			}
		}

		/// <summary>
		/// This method takes an arrangement and a fontsize, and
		/// calculates how large all the boxes must be at that fontsize,
		/// along with fixing the contents of partially filled boxes.
		/// </summary>
		/// <param name="graphicsState"></param>
		/// <param name="cache"></param>
		/// <param name="featureRects"></param>
		/// <param name="arrangement"></param>
		/// <param name="cardGutter"></param>
		/// <param name="fontSize"></param>
		/// <param name="spec"></param>
		/// <returns></returns>
		private static ArrangementParameters GetParameters(ISharpGraphicsState graphicsState, CardQueryCache cache, Rectangle[] featureRects, ICardSectionRect[][] arrangement, float cardGutter, float fontSize, ParagraphSpecification spec) {

			// We need to reset the partial boxes so we start taking pieces from the whole original.
			for (int i = 0; i < arrangement.Length; i++) {
				for (int j = 0; j < arrangement[i].Length; j++) {
					if (arrangement[i][j] is IPartialCardSectionRects partial) {
						partial.Reset();
					}
				}
			}

			float[] gutterHeights = arrangement.Select(cardBoxes => (cardBoxes.Length - 1) * cardGutter).ToArray();

			float[][] boxHeights = new float[arrangement.Length][];
			IFixedCardSectionRect?[][] finalArrangement = new IFixedCardSectionRect[arrangement.Length][];
			float[] totalFixedHeights = new float[arrangement.Length];

			int penalisedSplitCount = 0;

			bool validArrangement = true;

			for (int card = 0; card < arrangement.Length; card++) {

				boxHeights[card] = new float[arrangement[card].Length];
				finalArrangement[card] = new IFixedCardSectionRect[arrangement[card].Length];
				totalFixedHeights[card] = gutterHeights[card];
				for (int i = 0; i < arrangement[card].Length; i++) {
					// Populate array with entries we are certain of here
					if (arrangement[card][i] is IFixedCardSectionRect box) {
						boxHeights[card][i] = cache.GetMinimumHeight(box, fontSize, spec, featureRects[card].Width);
						totalFixedHeights[card] += boxHeights[card][i];
						finalArrangement[card][i] = box;
					}
				}

				// Now move on to boxes of uncertain height (i.e. !IsWhole)
				for (int i = 0; i < arrangement[card].Length; i++) {
					if (arrangement[card][i] is IPartialCardSectionRects partial) {
						finalArrangement[card][i] = partial.FromAvailableHeight(graphicsState, featureRects[card].Height - totalFixedHeights[card], featureRects[card].Width, fontSize, spec, cache, out float partialHeight);
						if (finalArrangement[card][i] != null) {
							boxHeights[card][i] = partialHeight;
							totalFixedHeights[card] += boxHeights[card][i];
							if (partial.PenaliseSplit) {
								penalisedSplitCount++;
							}
						}
						else {
							// Something went wrong, and the partial rect is invalid
							// This means that the box count is now wrong, and this arrangement is invalid
							validArrangement = false;
							// TODO How to respond to this properly?
						}
					}
				}
			}

			IFixedCardSectionRect[][] trimmedArrangement = new IFixedCardSectionRect[arrangement.Length][];
			float[][] trimmedBoxHeights = new float[arrangement.Length][];

			// We need to remove null boxes from failed partials
			for (int card = 0; card < arrangement.Length; card++) {
				List<IFixedCardSectionRect> boxes = new List<IFixedCardSectionRect>();
				List<float> heights = new List<float>();
				for (int i = 0; i < arrangement[card].Length; i++) {
					if (finalArrangement[card][i] != null) {
						boxes.Add(finalArrangement[card][i]!);
						heights.Add(boxHeights[card][i]);
					}
				}
				trimmedArrangement[card] = boxes.ToArray();
				trimmedBoxHeights[card] = heights.ToArray();
			}

			return new ArrangementParameters(fontSize, spec, trimmedArrangement, trimmedBoxHeights, penalisedSplitCount, totalFixedHeights, validArrangement);
		}

		#endregion
	}

}
