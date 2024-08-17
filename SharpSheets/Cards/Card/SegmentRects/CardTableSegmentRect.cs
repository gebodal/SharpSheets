using SharpSheets.Layouts;
using SharpSheets.Widgets;
using System;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Colors;
using System.Text.RegularExpressions;
using SharpSheets.Utilities;
using System.Linq;
using SharpSheets.Cards.Layouts;
using SharpSheets.Cards.CardSubjects;

namespace SharpSheets.Cards.Card.SegmentRects {

	public class NewCardTableSegmentRect : AbstractSegmentRect<TableCardSegmentConfig> {

		private readonly RichString[,] entries;
		private readonly CardFeature[] features;

		private Color[] RowColors => Config.tableColors;
		private readonly Justification[] justifications;
		private readonly bool[] numericColumns;
		private (float column, float row) TableSpacing => Config.tableSpacing;

		private static readonly Regex numberColumnRegex = new Regex(@"^[0-9\-\+\.]+$");
		public TextHeightStrategy CellHeightStrategy => Config.cellHeightStrategy;

		protected int StartRow { get; set; } = 0;

		public NewCardTableSegmentRect(TableCardSegmentConfig config, ArrangementCollection<IWidget> outlines, RichString[,] tableEntries, CardFeature[] features, bool splittable) : base(config, outlines, splittable) {
			entries = tableEntries;
			this.features = features;

			numericColumns = new bool[entries.GetLength(1)];
			justifications = new Justification[entries.GetLength(1)];
			for (int c = 0; c < entries.GetLength(1); c++) {
				numericColumns[c] = entries.GetColumn2D(c).Skip(1).Select(e => numberColumnRegex.IsMatch(e.Text)).All(b => b);
				justifications[c] = numericColumns[c] ? Justification.CENTRE : Justification.LEFT;
			}
		}

		public override float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache) {
			IWidget outline = GetOutline();
			Rectangle exampleRect = new Rectangle(width, 10000f);
			Rectangle tempRect;
			if (outline != null) {
				tempRect = outline.RemainingRect(graphicsState, exampleRect) ?? exampleRect;
			}
			else {
				tempRect = exampleRect;
			}

			float tableHeight = GetTableRowsMinimumHeight(graphicsState, fontSize, paragraphSpec, tempRect.Width);

			float totalHeight = exampleRect.Height - tempRect.Height + tableHeight;

			return totalHeight;
		}

		protected float GetTableRowsMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width) {
			return GetRowDimensions(graphicsState, entries, numericColumns, width, Config.edgeOffset, fontSize, paragraphSpec, CellHeightStrategy).Select(d => d.Absolute).Sum()
				+ (entries.GetLength(0) - 1) * TableSpacing.row // Inter-row spacing
				+ TableSpacing.row; // Spacing before and after table (half-width each, for filling in cell backgrounds)
		}

		protected static Dimension[] GetRowDimensions(ISharpGraphicsState graphicsState, RichString[,] entries, bool[] numericColumns, float width, float edgeOffset, float fontSize, ParagraphSpecification paragraphSpec, TextHeightStrategy cellHeightStrategy) {
			Dimension[] columnDimensions = GetColumnDimensions(graphicsState, entries, numericColumns, width, edgeOffset, fontSize, paragraphSpec);
			float columnGutter = ColumnGutter(graphicsState, fontSize);
			float[] columnWidths = Divisions.ToAbsolute(columnDimensions, width - 2 * edgeOffset, columnGutter, out _);

			float[,] entryHeights = new float[entries.GetLength(0), entries.GetLength(1)];
			for (int i = 0; i < entries.GetLength(0); i++) {
				for (int j = 0; j < entries.GetLength(1); j++) {
					entryHeights[i, j] = RichStringLayout.CalculateHeight(graphicsState, entries[i, j], columnWidths[j], fontSize, paragraphSpec, cellHeightStrategy, true);
				}
			}

			Dimension[] rowDimentions = new Dimension[entries.GetLength(0)];
			for (int i = 0; i < rowDimentions.Length; i++) {
				rowDimentions[i] = Dimension.FromPoints(entryHeights.GetRow2D(i).Max());
			}

			return rowDimentions;
		}

		protected static Dimension[] GetColumnDimensions(ISharpGraphicsState graphicsState, RichString[,] entries, bool[] numericColumns, float width, float edgeOffset, float fontSize, ParagraphSpecification paragraphSpec) {

			width -= 2 * edgeOffset; // To give some spacing at the sides
			float columnGutter = ColumnGutter(graphicsState, fontSize);

			float[] textWidths = new float[entries.GetLength(1)];
			for (int j = 0; j < entries.GetLength(1); j++) {
				textWidths[j] = entries.GetColumn2D(j).Select(s => graphicsState.GetWidth(s, fontSize)).Max();
			}
			float totalWidth = textWidths.Sum() + (entries.GetLength(1) - 1) * columnGutter; // Total width of text, including gutters

			if (totalWidth <= width) {
				return textWidths.Zip(numericColumns, (w, n) => n ? Dimension.FromPoints(w) : Dimension.FromRelative(w)).ToArray();
			}
			else {
				bool[] useFullWidth = ArrayUtils.MakeArray(entries.GetLength(1), true);
				while (totalWidth > width && useFullWidth.Any(b => b)) {
					// TODO Possibility for infinite loop here
					int largestIndex = 0;
					float largestWidth = 0f;
					for (int i = 0; i < textWidths.Length; i++) {
						if (useFullWidth[i] && textWidths[i] > largestWidth) {
							largestWidth = textWidths[i];
							largestIndex = i;
						}
					}
					useFullWidth[largestIndex] = false;
					totalWidth = textWidths.Zip(useFullWidth, (w, u) => u ? w : 0f).Sum() + (entries.GetLength(1) - 1) * columnGutter;
				}

				Dimension[] columnWidths = new Dimension[entries.GetLength(1)];
				for (int i = 0; i < columnWidths.Length; i++) {
					if (useFullWidth[i]) {
						columnWidths[i] = Dimension.FromPoints(textWidths[i]);
					}
					else {
						columnWidths[i] = Dimension.FromRelative(1f);
					}
				}

				return columnWidths;
			}
		}

		protected static float ColumnGutter(ISharpGraphicsState graphicsState, float fontSize) {
			return graphicsState.GetWidth(new RichString("M"), fontSize);
		}

		protected Color GetRowColor(int i) {
			return RowColors[(StartRow + i) % RowColors.Length];
		}

		public override void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
			canvas.RegisterAreas(Original ?? this, rect, null, Array.Empty<Rectangle>());

			IWidget outline = GetOutline();

			if (outline != null) {
				outline.Draw(canvas, rect, default);
				rect = outline.RemainingRect(canvas, rect) ?? rect;
				//canvas.RegisterArea(Original ?? this, rect);
			}

			rect = rect.Margins(TableSpacing.row / 2f, 0f, false); // To remove spacing for cell backgrounds

			canvas.SaveState();

			Dimension[] columnDimensions = GetColumnDimensions(canvas, entries, numericColumns, rect.Width, Config.edgeOffset, fontSize, paragraphSpec);

			Rectangle?[] rowRects = GetRowRects(canvas, rect, fontSize, paragraphSpec);
			for (int i = 0; i < rowRects.Length; i++) {
				if (rowRects[i] is not null) {
					DrawRow(i, canvas, rowRects[i]!, fontSize, paragraphSpec, columnDimensions);
					canvas.RegisterAreas(features[i], rowRects[i]!, null, Array.Empty<Rectangle>());
				}
			}

			canvas.RestoreState();
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		protected Rectangle?[] GetRowRects(ISharpGraphicsState graphicsState, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
			return Divisions.Rows(rect, GetRowDimensions(graphicsState, entries, numericColumns, rect.Width, Config.edgeOffset, fontSize, paragraphSpec, CellHeightStrategy), TableSpacing.row, false, Arrangement.FRONT, LayoutOrder.FORWARD);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		private static Rectangle?[] GetColumnRects(Rectangle fullRect, Dimension[] columnDimensions, float columnGutter, float edgeOffset) {
			Rectangle rect = fullRect.Margins(0, edgeOffset, false);
			return Divisions.Columns(rect, columnDimensions, columnGutter, false, Arrangement.FRONT, LayoutOrder.FORWARD); ;
		}

		public void DrawRow(int i, ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec, Dimension[] columnDimensions) {
			Color color = GetRowColor(i);
			if (color.A > 0) {
				canvas.SaveState().SetFillColor(color);
				canvas.Rectangle(rect.Margins(TableSpacing.row / 2, 0, true)).Fill().RestoreState();
			}

			Rectangle?[] columnRects = GetColumnRects(rect, columnDimensions, ColumnGutter(canvas, fontSize), Config.edgeOffset);
			for (int j = 0; j < columnRects.Length; j++) {
				if (columnRects[j] != null) {
					canvas.DrawRichText(columnRects[j]!, entries[i, j], fontSize, paragraphSpec, justifications[j], Alignment.CENTRE, CellHeightStrategy, true);
				}
			}
		}

		public override IPartialCardSegmentRects Split(int parts) {
			if (Splittable) {
				return new CardTableSegmentRectPieces(this, parts);
			}
			else {
				throw new InvalidOperationException($"Cannot split a {GetType().Name} with Splittable==false.");
			}
		}

		#region Card Table Pieces

		public class CardTableSegmentRectPieces : IPartialCardSegmentRects {
			public IFixedCardSegmentRect Parent { get { return original; } }
			readonly NewCardTableSegmentRect original;
			public int Boxes { get; set; }

			public bool PenaliseSplit { get; } = true;

			RichString[,]? remainingEntries;
			CardFeature[]? remainingFeatures;
			int startRow;
			int boxCount;

			public CardTableSegmentRectPieces(NewCardTableSegmentRect original, int boxes) {
				this.original = original;
				Boxes = boxes;
			}

			public void Reset() {
				remainingEntries = original.entries;
				remainingFeatures = original.features;
				startRow = 0;
				boxCount = 0;
			}

			/*
			public void RemoveBox() {
				Boxes = Math.Max(0, Boxes - 1);
			}
			*/

			public IFixedCardSegmentRect? FromAvailableHeight(ISharpGraphicsState graphicsState, float availableHeight, float width, float fontSize, ParagraphSpecification paragraphSpec, CardQueryCache cache, out float resultingHeight) {

				boxCount++;

				if (boxCount > Boxes) {
					throw new CardLayoutException($"Too many requests for boxes for {GetType().Name} ({boxCount} > {Boxes})");
				}

				RichString[,]? boxEntries;
				CardFeature[]? boxFeatures;

				if (boxCount < Boxes) {

					if (remainingEntries == null || remainingEntries.GetLength(0) == 0 || availableHeight <= fontSize) {
						resultingHeight = 0f;
						return null;
					}

					Rectangle remainingRect = new Rectangle(width, availableHeight);
					IWidget outline = original.outlines[boxCount - 1, Boxes];
					if (outline != null) {
						try {
							remainingRect = outline.RemainingRect(graphicsState, remainingRect) ?? remainingRect;
						}
						catch (InvalidRectangleException) {
							resultingHeight = 0f;
							return null;
						}
					}

					(boxEntries, boxFeatures) = SplitEntriesByHeight(graphicsState, remainingEntries!, remainingFeatures!, remainingRect.Width, remainingRect.Height, fontSize, paragraphSpec, out remainingEntries, out remainingFeatures);

				}
				else { // boxCount == parts -> This is the last available box, so everything must be put in here.
					boxEntries = remainingEntries;
					boxFeatures = remainingFeatures;
					remainingEntries = null;
				}

				NewCardTableSegmentRect? nextBox;
				if (boxEntries != null) {
					nextBox = new NewCardTableSegmentRect(original.Config, original.outlines, boxEntries, boxFeatures!, false) {
						Original = original,
						StartRow = startRow,
						PartIndex = boxCount - 1,
						PartsCount = Boxes
					};
					resultingHeight = cache.GetMinimumHeight(nextBox, fontSize, paragraphSpec, width); // fixedBox.CalculateMinimumHeight1(graphicsState, font, width); // TODO This seems inefficient here?
				}
				else {
					nextBox = null;
					resultingHeight = 0f;
				}

				startRow += boxEntries?.GetLength(0) ?? 0;

				return nextBox;
			}

			public (RichString[,]?, CardFeature[]?) SplitEntriesByHeight(ISharpGraphicsState graphicsState, RichString[,] entries, CardFeature[] features, float width, float height, float fontSize, ParagraphSpecification paragraphSpec, out RichString[,]? remainingEntries, out CardFeature[]? remainingFeatures) {

				Dimension[] rowDims = NewCardTableSegmentRect.GetRowDimensions(graphicsState, entries, original.numericColumns, width, original.Config.edgeOffset, fontSize, paragraphSpec, original.CellHeightStrategy);

				float rowsHeight1 = rowDims[0].Absolute;
				for (int i = 1; i < rowDims.Length; i++) {

					if (rowsHeight1 > height) {
						(RichString[,] first, RichString[,] second) = entries.Split(i - 1);
						remainingEntries = second.GetLength(0) > 0 ? second : null;
						remainingFeatures = remainingEntries != null ? features[^remainingEntries.GetLength(0)..] : null;

						RichString[,]? splitEntries = first.GetLength(0) > 0 ? first : null;
						CardFeature[]? splitFeatures = splitEntries != null ? features[..splitEntries.GetLength(0)] : null;

						return (splitEntries, splitFeatures);
					}

					rowsHeight1 += original.TableSpacing.row + rowDims[i].Absolute;
				}

				remainingEntries = null;
				remainingFeatures = null;
				return (entries, features);
			}

			public IPartialCardSegmentRects Clone() {
				return new CardTableSegmentRectPieces(original, Boxes);
			}
		}

		#endregion

	}

}