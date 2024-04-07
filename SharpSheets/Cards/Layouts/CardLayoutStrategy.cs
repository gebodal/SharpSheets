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
using SharpSheets.Cards.Card.SegmentRects;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Cards.Layouts {

    public class CardLayoutStrategy : AbstractLayoutStrategy {

		public override void Draw(ISharpDocument document, CardSetConfig config, DynamicCard[] cards, out List<SharpDrawingException> errors, out List<DynamicCard> errorCards, CancellationToken cancellationToken) {
			errors = new List<SharpDrawingException>();
			errorCards = new List<DynamicCard>();

			if (cancellationToken.IsCancellationRequested) { return; }

			PageSize pageSize = config.paper;
			Margins pageMargin = config.pageMargins;
			float cardGutter = config.cardGutter;
			int rows = (int)config.grid.rows;
			int columns = (int)config.grid.columns;

			AvailableSpace geometry = new AvailableSpace(pageSize, pageMargin, rows, columns, cardGutter);

			List<DynamicCard> cardList = new List<DynamicCard>();
			Dictionary<AbstractCard, CardArrangement> existingLayouts = new Dictionary<AbstractCard, CardArrangement>();

			foreach (DynamicCard card in cards) {
				CanvasStateImage modelCanvasState = new CanvasStateImage((Rectangle)pageSize, 1f);
				if (card.CardConfig.fonts != null) { modelCanvasState.SetFonts(card.CardConfig.fonts); }

				CardArrangement? arrangement = GetLayouts(card, modelCanvasState, (int)card.CardConfig.MaxCards, geometry.SampleRect, card.CardConfig.paragraphSpec, card.CardConfig.fontParams, cancellationToken);
				
				if (arrangement is null) { return; } // Should only happen if cancellation requested

				if (arrangement.Length > card.CardConfig.MaxCards) {
					errors.Add(new SharpDrawingException(card.Subject, "Card cannot be fit into a single row."));
					errorCards.Add(card);
				}
				else {
					existingLayouts.Add(arrangement.Card, arrangement);
					cardList.Add(arrangement.Card);
				}

				if (cancellationToken.IsCancellationRequested) { return; }
			}

			List<ISharpCanvas> pages = new List<ISharpCanvas>();
			ISharpCanvas GetPage(int page) {
				while (page >= pages.Count) {
					pages.Add(document.AddNewPage(pageSize));
					//Console.WriteLine($"Add page ({pages.Count} pages)");
				}
				return pages[page];
			}

			while(cardList.Count > 0) {

				CardArrangement? nextArrangement = null;
				CardIndex[][]? cardSpace = null;

				// Find next card that fits in the available space
				for (int c = 0; c < cardList.Count; c++) {
					if (cancellationToken.IsCancellationRequested) { return; }

					nextArrangement = existingLayouts[cardList[c]];
					cardSpace = geometry.GetAvailable(
						nextArrangement.Length,
						(int)nextArrangement.CardConfig.MaxCards,
						nextArrangement.CardConfig.multiCardLayout,
						nextArrangement.CardConfig.allowMultipage); // TODO Allow user control

					if (cardSpace is null) {
						nextArrangement = null;
					}
					else {
						break;
					}
				}

				if (nextArrangement is null || cardSpace is null) {
					throw new CardLayoutException("Could not process card layout.");
				}
				else {
					cardList.Remove(nextArrangement.Card);
					geometry.MarkUsed(cardSpace.SelectMany(idxs => idxs));

					//Console.WriteLine($"Draw arrangement for {nextArrangement.card.subject.Name.Value}: {cardSpace.Length} pages");

					int sectionsDrawn = 0;
					for (int p = 0; p < cardSpace.Length; p++) {
						//Console.WriteLine($"Start page ({p}), {cardSpace[p].Length} cards");

						if (cardSpace[p].Length == 0) { continue; }

						if (cancellationToken.IsCancellationRequested) { return; }

						Rectangle[] cardRects = cardSpace[p].Select(i => geometry[i.Row, i.Column]).ToArray();
						int pageNum = cardSpace[p].Select(i => i.Page).Distinct().Single();

						//Console.WriteLine($"Page {pageNum} ({p}), {cardSpace[p].Length} cards");

						ISharpCanvas canvas = GetPage(pageNum);
						DynamicCard card = nextArrangement.Card;

						canvas.SaveState();

						if (card.CardConfig.fonts != null) { canvas.SetFonts(card.CardConfig.fonts); }
						canvas.SetTextSize(nextArrangement.FontSize);

						card.DrawBackground(canvas, cardRects);

						for (int pageCardIdx = 0; pageCardIdx < cardRects.Length; pageCardIdx++) {
							if (cancellationToken.IsCancellationRequested) { break; }

							int cardIdx = sectionsDrawn + pageCardIdx;

							//Console.WriteLine($"Draw card {pageCardIdx} ({cardIdx}) at {cardRects[pageCardIdx]}");

							Draw(canvas, nextArrangement, nextArrangement.layouts[cardIdx], cardRects[pageCardIdx], cancellationToken);
						}

						canvas.RestoreState();

						sectionsDrawn += cardRects.Length;
					}
					
				}
			}

		}

		// Does not draw background
		private static void Draw(ISharpCanvas canvas, CardArrangement arrangement1, SingleCardLayout cardLayout, Rectangle rect1, CancellationToken cancellationToken) {

			if (cancellationToken.IsCancellationRequested) { return; }

			// TODO Are these checks actually necessary?
			if (arrangement1.sampleSize.Width != rect1.Width || arrangement1.sampleSize.Height != rect1.Height) {
				throw new ArgumentException($"Provided rect is not the same dimensions as layout sample size.");
			}

			SingleCardLayout layout = cardLayout;
			Rectangle fullRect = rect1;
			bool lastCard = layout.index == (arrangement1.Length - 1);

			DynamicCard card = arrangement1.Card;

			card.DrawOutline(canvas, fullRect, layout.index, arrangement1.Length);
			Rectangle featuresFullRect = card.RemainingRect(canvas, fullRect, layout.index, arrangement1.Length);

			Rectangle?[] segmentRects = GetSegmentRects(card, layout, featuresFullRect, lastCard, out Rectangle?[] segmentGutters);

			for (int gutterIdx = 0; gutterIdx < segmentGutters.Length; gutterIdx++) {
				if (segmentGutters[gutterIdx] is not null) {
					card.DrawGutter(canvas, segmentGutters[gutterIdx]!);
				}
			}

			for (int secIdx = 0; secIdx < layout.boxes.Length; secIdx++) {
				if (cancellationToken.IsCancellationRequested) { return; }
				if (segmentRects[secIdx] is not null) {
					try {
						layout.boxes[secIdx].Draw(canvas, segmentRects[secIdx]!, arrangement1.FontSize, arrangement1.paragraphSpec);
					}
					catch (InvalidRectangleException e) {
						canvas.LogError(new SharpDrawingException(layout.boxes[secIdx], "Invalid rectangle.", e));
						if (layout.boxes[secIdx].Config is AbstractCardSegmentConfig segmentConfig) {
							canvas.LogError(new SharpDrawingException(segmentConfig, "Invalid rectangle.", e));
						}
						if (segmentRects[secIdx]!.Width > 0 && segmentRects[secIdx]!.Height > 0) {
							new ErrorWidget(e, new WidgetSetup()).Draw(canvas, segmentRects[secIdx]!, cancellationToken);
						}
					}
				}
			}
		}

		private static Rectangle?[] GetSegmentRects(DynamicCard card, SingleCardLayout layout, Rectangle rect, bool lastCard, out Rectangle?[] gutters) { // lastCard = cardIdx == (layouts.Length - 1)
			float totalAvailableHeight = layout.areas.Select(r => r.Height).Sum();

			float[] heights = new float[layout.boxes.Length];
			float totalRemaining = 0f;
			for (int secIdx = 0; secIdx < layout.boxes.Length; secIdx++) {
				float remainingHeight = layout.areas[secIdx].Height - layout.minHeights[secIdx];

				totalRemaining += remainingHeight;
				heights[secIdx] = layout.areas[secIdx].Height - remainingHeight;
				//Console.WriteLine($"Rect {j} initial height: {layout.rects[j].Height} (remaining: {remainingHeight})");
			}

			Rectangle?[] segmentRects;
			Rectangle?[] segmentGutters;
			if (card.CropOnFinalCard && lastCard && heights.Length == 1 && heights[0] < totalAvailableHeight / 2) {
				segmentRects = new Rectangle?[] { Divisions.Row(rect, Dimension.FromPoints(totalAvailableHeight / 2), card.Gutter, out _, out _, true, Arrangement.FRONT, LayoutOrder.FORWARD) };
				segmentGutters = Array.Empty<Rectangle>();
			}
			else if (heights.All(f => f < totalAvailableHeight / heights.Length) && layout.boxes.All(b => b.AcceptRemaining)) {
				segmentRects = Divisions.Rows(rect, heights.Length, card.Gutter, out segmentGutters);
			}
			else {
				int numAccepting = layout.boxes.Where(b => b.AcceptRemaining).Count();
				for (int secIdx = 0; secIdx < heights.Length; secIdx++) {
					if (layout.boxes[secIdx].AcceptRemaining) {
						heights[secIdx] += totalRemaining / numAccepting;
					}
				}
				segmentRects = Divisions.Rows(rect, Dimension.FromPoints(heights), card.Gutter, card.Gutter, out _, out segmentGutters, out _, false, Arrangement.FRONT, LayoutOrder.FORWARD);
			}

			gutters = segmentGutters;
			return segmentRects;
		}

		#region Card Layouts Behaviour

		/// <summary>Find layouts for subject cards, given a maximum possible number of cards and assuming cards of a fixed size based on the sample provided.</summary>
		public static CardArrangement? GetLayouts(DynamicCard card, ISharpGraphicsState graphicsState, int maxCards, Rectangle sampleRect, ParagraphSpecification spec, FontSizeSearchParams searchParams, CancellationToken cancellationToken) {

			if (cancellationToken.IsCancellationRequested) { return null; }

			ICardSegmentRect[][][] possibleArrangements = GetPossibleArrangements(card, maxCards);

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

		private static ICardSegmentRect[][][] GetPossibleArrangements(AbstractCard card, int maxCards) {

			ICardSegmentRect[] boxes = GetSegmentRects(card, maxCards).ToArray();
			ICardSegmentRect[][][] possibleArrangements = Partitioning.GetAllPartitions(boxes, maxCards).ToArray();

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
				Dictionary<IFixedCardSegmentRect, IPartialCardSegmentRects> partials = new Dictionary<IFixedCardSegmentRect, IPartialCardSegmentRects>(); // Map from original to clone
				Dictionary<IPartialCardSegmentRects, int> partialCounts = new Dictionary<IPartialCardSegmentRects, int>();
				for (int j = 0; j < possibleArrangements[i].Length; j++) {
					for (int k = 0; k < possibleArrangements[i][j].Length; k++) {
						if (possibleArrangements[i][j][k] is IPartialCardSegmentRects partial) {
							if (!partials.ContainsKey(partial.Parent)) {
								partials.Add(partial.Parent, partial.Clone());
								partialCounts.Add(partials[partial.Parent], 0);
							}
							possibleArrangements[i][j][k] = partials[partial.Parent];
							partialCounts[partials[partial.Parent]] += 1;
						}
					}
				}
				foreach (KeyValuePair<IPartialCardSegmentRects, int> entry in partialCounts) {
					entry.Key.Boxes = partialCounts[entry.Key];
				}
			}

			// Sort possible arrangements by number of cards (ascending)
			Array.Sort(possibleArrangements, (a1, a2) => Comparer<int>.Default.Compare(a1.Length, a2.Length));

			return possibleArrangements;
		}

		private static IEnumerable<ICardSegmentRect> GetSegmentRects(AbstractCard card, int maxCards) {
			for (int i = 0; i < card.Segments.Length; i++) {
				IFixedCardSegmentRect box = card.Segments[i];
				if (box.Splittable) {
					IPartialCardSegmentRects partial = box.Split(maxCards);
					for (int j = 0; j < maxCards; j++) {
						yield return partial;
					}
				}
				else {
					yield return box;
				}
			}
		}

		private static CardArrangement? LayoutArrangementFontBisectionSearch(DynamicCard card, ISharpGraphicsState graphicsState, CardQueryCache cache, Rectangle sampleRect, ICardSegmentRect[][] arrangement, ParagraphSpecification spec, float maxFontSize, float fontEpsilon) {

			int calls = 1;

			Rectangle[] featureRects = new Rectangle[arrangement.Length];
			for (int i = 0; i < arrangement.Length; i++) {
				featureRects[i] = card.RemainingRect(graphicsState, sampleRect, i, arrangement.Length);
				// TODO This doesn't account for variable sized outlines!
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
				layouts[i] = new SingleCardLayout(i, (Size)featureRects[i], final.arrangement[i], boxSizes[i], final.boxHeights[i], finalUnusedHeight);
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
			public readonly IFixedCardSegmentRect[][] arrangement;
			public readonly float[][] boxHeights;
			public readonly float[] heightsSums;
			public readonly int penalisedSplitCount;
			public readonly float[] totalFixedHeights;
			public readonly bool validArrangement;

			public ArrangementParameters(float fontSize, ParagraphSpecification spec, IFixedCardSegmentRect[][] arrangement, float[][] boxHeights, int penalisedSplitCount, float[] totalFixedHeights, bool validArrangement) {
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
		private static ArrangementParameters GetParameters(ISharpGraphicsState graphicsState, CardQueryCache cache, Rectangle[] featureRects, ICardSegmentRect[][] arrangement, float cardGutter, float fontSize, ParagraphSpecification spec) {

			// We need to reset the partial boxes so we start taking pieces from the whole original.
			for (int i = 0; i < arrangement.Length; i++) {
				for (int j = 0; j < arrangement[i].Length; j++) {
					if (arrangement[i][j] is IPartialCardSegmentRects partial) {
						partial.Reset();
					}
				}
			}

			float[] gutterHeights = arrangement.Select(cardBoxes => (cardBoxes.Length - 1) * cardGutter).ToArray();

			float[][] boxHeights = new float[arrangement.Length][];
			IFixedCardSegmentRect?[][] finalArrangement = new IFixedCardSegmentRect[arrangement.Length][];
			float[] totalFixedHeights = new float[arrangement.Length];

			int penalisedSplitCount = 0;

			bool validArrangement = true;

			for (int card = 0; card < arrangement.Length; card++) {

				boxHeights[card] = new float[arrangement[card].Length];
				finalArrangement[card] = new IFixedCardSegmentRect[arrangement[card].Length];
				totalFixedHeights[card] = gutterHeights[card];
				for (int i = 0; i < arrangement[card].Length; i++) {
					// Populate array with entries we are certain of here
					if (arrangement[card][i] is IFixedCardSegmentRect box) {
						boxHeights[card][i] = cache.GetMinimumHeight(box, fontSize, spec, featureRects[card].Width);
						totalFixedHeights[card] += boxHeights[card][i];
						finalArrangement[card][i] = box;
					}
				}

				// Now move on to boxes of uncertain height (i.e. !IsWhole)
				for (int i = 0; i < arrangement[card].Length; i++) {
					if (arrangement[card][i] is IPartialCardSegmentRects partial) {
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

			IFixedCardSegmentRect[][] trimmedArrangement = new IFixedCardSegmentRect[arrangement.Length][];
			float[][] trimmedBoxHeights = new float[arrangement.Length][];

			// We need to remove null boxes from failed partials
			for (int card = 0; card < arrangement.Length; card++) {
				List<IFixedCardSegmentRect> boxes = new List<IFixedCardSegmentRect>();
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

		private class CardIndex {
			public readonly int Page;
			public readonly int Row;
			public readonly int Column;

			public CardIndex(int page, int row, int column) {
				Page = page;
				Row = row;
				Column = column;
			}
		}

		private class AvailableSpace {

			public int Rows => grid.GetLength(0);
			public int Columns => grid.GetLength(1);

			public int Count => Rows * Columns;

			public Rectangle SampleRect => grid[0, 0];

			private readonly Rectangle pageRect;
			private readonly Rectangle[,] grid;
			
			private readonly List<bool[,]> used;
			private int PageCount => used.Count;

			public Rectangle this[int r, int c] => grid[r, c];

			public AvailableSpace(Size pageSize, Margins pageMargin, int rows, int columns, float gutter) {
				this.pageRect = pageSize.AsRectangle();
				
				this.grid = Divisions.Grid(pageRect.Margins(pageMargin, false), rows, columns, gutter, gutter, out _, out _);

				used = new List<bool[,]>();
			}

			private int GetIndex(int row, int col) => row * Columns + col;

			private void AddPage() {
				bool[,] newPage = new bool[Rows, Columns];
				newPage.Fill(false); // Just to be safe
				used.Add(newPage);
			}

			public CardIndex[][]? GetAvailable(int count, int max, RectangleAllowance rectangle, bool allowMultipage) {
				if (rectangle != RectangleAllowance.NONE) {
					CardIndex[]? rectangular = GetAvailableRectangle(count, max, rectangle);
					if(rectangular is not null) {
						return new CardIndex[][] { rectangular };
					}
					else {
						return null;
					}
				}
				else {
					return GetAvailableSingles(count, allowMultipage);
				}
			}

			private CardIndex[][]? GetAvailableSingles(int count, bool allowMultipage) {

				List<CardIndex[]> pages = new List<CardIndex[]>();

				int page = 0;
				while (count > 0) {
					List<CardIndex> available = new List<CardIndex>();

					while (page >= PageCount) {
						AddPage();
					}

					for (int r = 0; r < Rows; r++) {
						for (int c = 0; c < Columns; c++) {
							if (!used[page][r, c]) {
								available.Add(new CardIndex(page, r, c));
							}
							else {
								available.Clear();
							}

							if (available.Count >= count) {
								pages.Add(available.ToArray());
								return pages.ToArray();
							}
						}
					}

					if (allowMultipage) {
						pages.Add(available.ToArray());
						count -= available.Count;
						page++;
					}
				}

				return null;
			}

			private CardIndex[]? GetAvailableRectangle(int count, int max, RectangleAllowance rectangle) {

				int bestWidth = 0;
				int bestArea = int.MaxValue;
				CardIndex[]? best = null;

				for (int p = 0; p < PageCount; p++) {
					for (int r = 0; r < Rows; r++) {
						for (int c = 0; c < Columns; c++) {
							if (!used[p][r, c] && (Rows - r) * (Columns - c) >= count) {
								if (GetRectAt(p, r, c, count, bestWidth, max, out int width, out int height) is CardIndex[] rect) {
									if (width * height <= bestArea && rectangle.Allowed(width, height)) {
										best = rect;
										bestWidth = width;
										bestArea = width * height;
									}
								}
							}
						}
					}
				}

				if (best is null) {
					AddPage();
					best = GetRectAt(PageCount - 1, 0, 0, count, 0, max, out _, out _);
				}

				return best;
			}

			private static IEnumerable<(int width, int height)> GetRects(int width, int height, int count, int max) {
				for (int h = 1; h <= height; h++) {
					for (int w = 1; w <= width; w++) {
						if (w * h >= count && w * h <= max) {
							yield return (w, h);
						}
					}
				}
			}

			private CardIndex[]? GetRectAt(int p, int r, int c, int count, int minWidth, int max, out int finalWidth, out int finalHeight) {
				// How many ways can we make a rectangle here?
				foreach((int width, int height) in GetRects(Columns - c, Rows - r, count, max)
					.Where(s => s.width > minWidth)
					.OrderBy(s => s.height)) {

					if (GetRectWith(p, r, c, width, height) is CardIndex[] rect) {
						finalWidth = width;
						finalHeight = height;
						return rect;
					}
				}

				finalWidth = -1;
				finalHeight = -1;
				return null;
			}

			private CardIndex[]? GetRectWith(int p, int r, int c, int w, int h) {
				List<CardIndex> available = new List<CardIndex>();
				for (int i = r; i < r + h; i++) {
					for (int j = c; j < c + w; j++) {
						if (!used[p][i, j]) {
							available.Add(new CardIndex(p, i, j));
						}
						else {
							return null;
						}
					}
				}
				return available.ToArray();
			}

			public void MarkUsed(IEnumerable<CardIndex> indexes) {
				foreach (CardIndex index in indexes) {
					while (index.Page >= PageCount) {
						AddPage();
					}

					//Console.WriteLine($"Mark used page {index.Page}, row {index.Row} column {index.Column}");
					used[index.Page][index.Row, index.Column] = true;
				}
			}

			/*
			public bool IsAvailable(int count) {
				int? running = null;
				for (int r = 0; r < Rows; r++) {
					for (int c = 0; c < Columns; c++) {
						if (!used[r, c]) {
							if (running.HasValue) { running = running.Value + 1; }
							else { running = 1; }
						}
						else {
							running = null;
						}

						if(running.HasValue && running.Value >= count) {
							return true;
						}
					}
				}

				return false;
			}
			*/

			/*
			private enum CardAdjacency {
				None,
				HorizontalAdjacent,
				VerticalAdjacent,
				BlockAdjacent,
				All
			}
			 
			public static IEnumerable<List<CardIndex>> GroupAdjacents(CardAdjacency adjacency, IEnumerable<CardIndex> source) {

				List<List<CardIndex>> allGroups = new List<List<CardIndex>>();

				foreach (IEnumerable<CardIndex> pageGroup in source.GroupBy(i => i.Page).Select(g => (IEnumerable<CardIndex>)g)) {

					List<CardIndex> cardIndices = pageGroup.OrderBy(i => i.Row).ThenBy(i => i.Column).ToList();

					List<List<CardIndex>> pageGroups = new List<List<CardIndex>>();

					for(int i = 0; i<cardIndices.Count; i++) {

						for(int g=0; g<pageGroups.Count; g++) {

						}

					}
					

					allGroups.AddRange(pageGroups);
				}
			}
			*/

		}

	}

	public enum RectangleAllowance { NONE, ANY, WIDE, TALL }

	public static class RectangleAllowanceUtils {

		public static bool Allowed(this RectangleAllowance allowance, int width, int height) {
			return allowance switch {
				RectangleAllowance.NONE => true,
				RectangleAllowance.ANY => true,
				RectangleAllowance.WIDE => width >= height,
				RectangleAllowance.TALL => height >= width,
				_ => throw new ArgumentException($"Invalid {nameof(RectangleAllowance)} value.", nameof(allowance))
			};
		}

	}

}
