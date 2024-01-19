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

namespace SharpSheets.Cards.Card.SegmentRects {

    public abstract class CardTableSegmentRect : IFixedCardSegmentRect {

        AbstractCardSegmentConfig IFixedCardSegmentRect.Config => Config;
        public TableCardSegmentConfig Config { get; }
        public IFixedCardSegmentRect? Original { get; set; }

        public readonly ArrangementCollection<IWidget> outlines;
        public readonly (float column, float row) tableSpacing;
        public abstract float HeaderGutter { get; }
        public bool Splittable { get; private set; }
        public bool AcceptRemaining { get; private set; }

        private int partIndex { get; } = 0;
        private int partsCount { get; } = 1;

        public CardTableSegmentRect(TableCardSegmentConfig config, ArrangementCollection<IWidget> outlines, (float column, float row) tableSpacing, bool splittable, bool acceptRemaining) {
            Config = config;
            this.outlines = outlines;
            this.tableSpacing = tableSpacing;
            Splittable = splittable;
            AcceptRemaining = acceptRemaining;
        }

        private IWidget GetOutline() {
            return outlines[partIndex, partsCount];
        }

        protected abstract float GetHeaderHeight(float width, float fontSize, ParagraphSpecification paragraphSpec);
        /// <summary></summary>
        /// <exception cref="InvalidRectangleException"></exception>
        protected Rectangle? GetHeaderRect(Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec, out Rectangle? remainingRect) {
            float headerHeight = GetHeaderHeight(rect.Width, fontSize, paragraphSpec);
            if (headerHeight > 0) {
                return Divisions.Row(rect, Dimension.FromPoints(headerHeight), HeaderGutter, out remainingRect, out _, false, Arrangement.FRONT, LayoutOrder.FORWARD);
            }
            else {
                remainingRect = rect;
                return null;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="headerRect"></param>
        /// <param name="fontSize"></param>
        /// <param name="paragraphSpec"></param>
        /// <exception cref="InvalidOperationException"></exception>
        protected abstract void DrawHeader(ISharpCanvas canvas, Rectangle headerRect, float fontSize, ParagraphSpecification paragraphSpec);

        protected abstract Dimension[] GetRowDimensions(ISharpGraphicsState graphicsState, float width, float fontSize, ParagraphSpecification paragraphSpec);
        protected abstract float GetTableRowsMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width);

        /// <summary></summary>
        /// <exception cref="InvalidRectangleException"></exception>
        protected Rectangle?[] GetRowRects(ISharpGraphicsState graphicsState, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
            return Divisions.Rows(rect, GetRowDimensions(graphicsState, rect.Width, fontSize, paragraphSpec), tableSpacing.row, false, Arrangement.FRONT, LayoutOrder.FORWARD);
        }

        public virtual float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache) {
            IWidget outline = GetOutline();
            Rectangle exampleRect = new Rectangle(width, 10000f);
            Rectangle tempRect;
            if (outline != null) {
                tempRect = outline.RemainingRect(graphicsState, exampleRect) ?? exampleRect;
            }
            else {
                tempRect = exampleRect;
            }

            float headerHeight = GetHeaderHeight(width, fontSize, paragraphSpec);
            if (headerHeight > 0f) { headerHeight += HeaderGutter; }

            float tableHeight = GetTableRowsMinimumHeight(graphicsState, fontSize, paragraphSpec, tempRect.Width);

            float totalHeight = exampleRect.Height - tempRect.Height + tableHeight + headerHeight;

            return totalHeight;
        }

        public void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
            canvas.RegisterAreas(Original ?? this, rect, null, Array.Empty<Rectangle>());

            IWidget outline = GetOutline();

            if (outline != null) {
                outline.Draw(canvas, rect, default);
                rect = outline.RemainingRect(canvas, rect) ?? rect;
                //canvas.RegisterArea(Original ?? this, rect);
            }

            canvas.SaveState();

            Rectangle? headerRect = GetHeaderRect(rect, fontSize, paragraphSpec, out Rectangle? remainingRect);

            if (headerRect != null) {
                DrawHeader(canvas, headerRect, fontSize, paragraphSpec);
            }

            if (remainingRect != null) {
                Dimension[] columnDimensions = GetColumnDimensions(canvas, remainingRect.Width, fontSize, paragraphSpec);

                Rectangle?[] rowRects = GetRowRects(canvas, remainingRect, fontSize, paragraphSpec);
                for (int i = 0; i < rowRects.Length; i++) {
                    if (rowRects[i] is not null) {
                        DrawRow(i, canvas, rowRects[i]!, fontSize, paragraphSpec, columnDimensions);
                    }
                }
            }

            canvas.RestoreState();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <param name="canvas"></param>
        /// <param name="rect"></param>
        /// <param name="fontSize"></param>
        /// <param name="paragraphSpec"></param>
        /// <param name="columnDimensions"></param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="InvalidRectangleException"></exception>
        public abstract void DrawRow(int i, ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec, Dimension[] columnDimensions);
        protected abstract Dimension[] GetColumnDimensions(ISharpGraphicsState graphicsState, float width, float fontSize, ParagraphSpecification paragraphSpec);

        public IPartialCardSegmentRects Split(int parts) {
            throw new NotImplementedException(); // TODO Implement
        }
    }

    public class SimpleCardTableSegmentRect : CardTableSegmentRect {

        public override float HeaderGutter { get { return tableSpacing.row * 2f; } }
        private readonly float edgeOffset;

        private readonly RichString? title;
        private readonly RichString[,] entries;
        private readonly Color[] rowColors;

        private readonly Justification[] justifications;
        private readonly bool[] numberColumns;

        private static readonly Regex numberColumnRegex = new Regex(@"^[0-9\-\+\.]+$");

        private readonly TextHeightStrategy cellHeightStrategy = TextHeightStrategy.AscentDescent;

        public SimpleCardTableSegmentRect(TableCardSegmentConfig definition, ArrangementCollection<IWidget> outlines, RichString? title, RichString[,] tableEntries, (float, float) tableSpacing, float edgeOffset, Color[] rowColors, bool splittable, bool acceptRemaining) : base(definition, outlines, tableSpacing, splittable, acceptRemaining) {
            this.title = title;
            entries = tableEntries;
            this.rowColors = rowColors;

            this.edgeOffset = edgeOffset;

            numberColumns = new bool[entries.GetLength(1)];
            for (int j = 0; j < numberColumns.Length; j++) {
                numberColumns[j] = entries.GetColumn2D(j).Skip(1).Select(e => numberColumnRegex.IsMatch(e.Text)).All(b => b);
            }

            justifications = new Justification[entries.GetLength(1)];
            for (int j = 0; j < justifications.Length; j++) {
                justifications[j] = numberColumns[j] ? Justification.CENTRE : Justification.LEFT;
            }
        }

        protected override float GetHeaderHeight(float width, float fontSize, ParagraphSpecification paragraphSpec) {
            return 0f;
            /*
            if (title != null) {
                return fontSize + 2f;
            }
            else {
                return 0f;
            }
            */
        }

        protected override void DrawHeader(ISharpCanvas canvas, Rectangle headerRect, float fontSize, ParagraphSpecification paragraphSpec) {
            /*
            if (title != null) {
                canvas.DrawRichText(headerRect, title, fontSize + 2f, paragraphSpec, Justification.LEFT, Alignment.CENTRE, cellHeightStrategy, false);
            }
            */
        }

        public override void DrawRow(int i, ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec, Dimension[] columnDimensions) {
            Color color = rowColors[i % rowColors.Length];
            if (color.A > 0) {
                canvas.SaveState().SetFillColor(color);
                canvas.Rectangle(rect.Margins(tableSpacing.row / 2, 0, true)).Fill().RestoreState();
            }

            Rectangle?[] columnRects = GetColumnRects(rect, columnDimensions, ColumnGutter(canvas, fontSize));
            for (int j = 0; j < columnRects.Length; j++) {
                if (columnRects[j] != null) {
                    canvas.DrawRichText(columnRects[j]!, entries[i, j], fontSize, paragraphSpec, justifications[j], Alignment.CENTRE, cellHeightStrategy, true);
                }
            }
        }

        /// <summary></summary>
        /// <exception cref="InvalidRectangleException"></exception>
        private Rectangle?[] GetColumnRects(Rectangle fullRect, Dimension[] columnDimensions, float columnGutter) {
            Rectangle rect = fullRect.Margins(0, edgeOffset, false);
            return Divisions.Columns(rect, columnDimensions, columnGutter, false, Arrangement.FRONT, LayoutOrder.FORWARD); ;
        }

        protected override Dimension[] GetRowDimensions(ISharpGraphicsState graphicsState, float width, float fontSize, ParagraphSpecification paragraphSpec) {
            Dimension[] columnDimensions = GetColumnDimensions(graphicsState, width, fontSize, paragraphSpec);
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

        protected virtual float ColumnGutter(ISharpGraphicsState graphicsState, float fontSize) {
            return graphicsState.GetWidth(new RichString("M"), fontSize);
        }

        protected override Dimension[] GetColumnDimensions(ISharpGraphicsState graphicsState, float width, float fontSize, ParagraphSpecification paragraphSpec) {

            width -= 2 * edgeOffset; // To give some spacing at the sides
            float columnGutter = ColumnGutter(graphicsState, fontSize);

            float[] textWidths = new float[entries.GetLength(1)];
            for (int j = 0; j < entries.GetLength(1); j++) {
                textWidths[j] = entries.GetColumn2D(j).Select(s => graphicsState.GetWidth(s, fontSize)).Max();
            }
            float totalWidth = textWidths.Sum() + (entries.GetLength(1) - 1) * columnGutter; // Total width of text, including gutters

            if (totalWidth <= width) {
                return textWidths.Zip(numberColumns, (w, n) => n ? Dimension.FromPoints(w) : Dimension.FromRelative(w)).ToArray();
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

        protected override float GetTableRowsMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width) {
            return GetRowDimensions(graphicsState, width, fontSize, paragraphSpec).Select(d => d.Absolute).Sum() + (entries.GetLength(0) - 1) * tableSpacing.row;
        }
    }

}