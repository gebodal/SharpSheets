using SharpSheets.Layouts;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.Layouts;
using SharpSheets.Cards.CardConfigs;

namespace SharpSheets.Cards.Card.SectionRects {

    public class DynamicSectionRect : IFixedCardSectionRect {

        AbstractCardSectionConfig IFixedCardSectionRect.Config => Config;
        public DynamicCardSectionConfig Config { get; }
        public IFixedCardSectionRect? Original { get; set; }

        public readonly ArrangementCollection<IWidget> outlines;
        public readonly WidgetCardRect[] entries;
        public readonly float gutter;
        private readonly bool _splittable;
        public bool Splittable { get { return _splittable && entries.Length > 0; } }
        public bool AcceptRemaining { get; private set; }
        public readonly bool equalSizeFeatures;
        public readonly bool spaceFeatures;

        private int partIndex { get; set; } = 0;
        private int partsCount { get; set; } = 1;

        public DynamicSectionRect(DynamicCardSectionConfig config, ArrangementCollection<IWidget> outlines, WidgetCardRect[] entries, float gutter, bool splittable, bool acceptRemaining, bool equalSizeFeatures, bool spaceFeatures) {
            Config = config;
            this.outlines = outlines;
            this.entries = entries;
            this.gutter = gutter;
            _splittable = splittable;
            AcceptRemaining = acceptRemaining;
            this.equalSizeFeatures = equalSizeFeatures;
            this.spaceFeatures = spaceFeatures;
        }

        private IWidget GetOutline() {
            return outlines[partIndex, partsCount];
        }

        private static float tempRectHeight = 10000f;
        public float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache) {
            Rectangle? tempRect = GetOutline().RemainingRect(graphicsState, new Rectangle(width, tempRectHeight));

            if (tempRect == null) {
                throw new CardLayoutException("Could not get section area rect.");
            }

            //Console.WriteLine($"DynamicSectionRect.CalculateMinimumHeight: total {width}, remaining {tempRect.Width}, gutter {gutter}");

            float entriesHeight;
            if (equalSizeFeatures) {
                entriesHeight = Divisions.CalculateTotalLength(entries.Select(e => cache.GetMinimumHeight(e, fontSize, paragraphSpec, tempRect.Width)).Max(), entries.Length, gutter);
            }
            else {
                entriesHeight = Divisions.CalculateTotalLength(entries.Select(e => cache.GetMinimumHeight(e, fontSize, paragraphSpec, tempRect.Width)), gutter);
            }

            float totalHeight = tempRectHeight - tempRect.Height + entriesHeight;

            return totalHeight;
        }

        public void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
            IWidget outline = GetOutline();

            canvas.SaveState();
            canvas.SetTextSize(fontSize);
            outline.Draw(canvas, rect, default);
            Rectangle remainingRect = outline.RemainingRect(canvas, rect) ?? rect;
            canvas.RestoreState();

            //Console.WriteLine($"DynamicSectionRect.Draw: total {rect.Width}, remaining {remainingRect.Width}, gutter {gutter}");

            canvas.RegisterAreas(Original ?? this, rect, null, Array.Empty<Rectangle>());
            //canvas.RegisterArea(Original ?? this, remainingRect);

            if (entries.Length > 0) {
                CardQueryCache tempCache = new CardQueryCache(canvas);

                Dimension[] rowHeights;
                if (equalSizeFeatures) {
                    Dimension rowHeight = Dimension.FromPoints(entries.Select(e => tempCache.GetMinimumHeight(e, fontSize, paragraphSpec, remainingRect.Width)).Max());
                    rowHeights = ArrayUtils.MakeArray(entries.Length, rowHeight);
                    //Console.WriteLine($"Using equal size features, row height = {rowHeight}");
                }
                else {
                    rowHeights = entries.Select(e => Dimension.FromPoints(tempCache.GetMinimumHeight(e, fontSize, paragraphSpec, remainingRect.Width))).ToArray();
                    //Console.WriteLine($"Using unequal size features, row heights = " + string.Join(", ", rowHeights.Select(h => h.ToString())));
                }

                if (spaceFeatures) {
                    for (int i = 0; i < rowHeights.Length; i++) {
                        rowHeights[i] += Dimension.Single;
                    }
                }

                Rectangle?[] entryRects = Divisions.Rows(remainingRect, rowHeights, gutter, false, Arrangement.FRONT, LayoutOrder.FORWARD);

                for (int i = 0; i < entries.Length; i++) {
                    if (entryRects[i] != null) {
                        entries[i].Draw(canvas, entryRects[i]!, fontSize, paragraphSpec);
                    }

                    //canvas.SaveState();
                    //canvas.SetLineWidth(0.4f).SetStrokeColor(Color.Purple).Rectangle(entryRects[i]).Stroke();
                    //canvas.RestoreState();
                }
            }
        }

        public IPartialCardSectionRects Split(int parts) {
            if (Splittable) {
                return new DynamicSectionRectPieces(this, parts);
            }
            else {
                throw new InvalidOperationException("Cannot split a DynamicSectionRect with Splittable==false.");
            }
        }

        public class DynamicSectionRectPieces : IPartialCardSectionRects {
            public IFixedCardSectionRect Parent { get { return original; } }
            private readonly DynamicSectionRect original;
            public int Boxes { get; set; }

            public bool PenaliseSplit { get; } = true;

            private WidgetCardRect[]? remainingEntries;
            private int boxCount;

            public DynamicSectionRectPieces(DynamicSectionRect original, int boxes) {
                this.original = original;
                Boxes = boxes;
            }

            public void Reset() {
                remainingEntries = original.entries;
                boxCount = 0;
            }

            private class WithHeight {
                public WidgetCardRect Rect { get; }
                public float MinimumHeight { get; }
                public WithHeight(WidgetCardRect rect, float minimumHeight) {
                    Rect = rect;
                    MinimumHeight = minimumHeight;
                }
            }

            public IFixedCardSectionRect? FromAvailableHeight(ISharpGraphicsState graphicsState, float availableHeight, float width, float fontSize, ParagraphSpecification paragraphSpec, CardQueryCache cache, out float resultingHeight) {

                boxCount++;

                if (boxCount > Boxes) {
                    throw new CardLayoutException($"Too many requests for boxes for DynamicSectionRectPieces ({boxCount} > {Boxes})");
                }

                WidgetCardRect[]? boxEntries;

                if (boxCount < Boxes) {

                    if (remainingEntries is null || remainingEntries.Length == 0 || availableHeight <= fontSize) {
                        resultingHeight = 0f;
                        return null;
                    }

                    Rectangle remainingRect;
                    try {
                        Rectangle availableRect = new Rectangle(width, availableHeight);
                        remainingRect = original.outlines[boxCount - 1, Boxes].RemainingRect(graphicsState, availableRect) ?? availableRect;
                    }
                    catch (InvalidRectangleException) {
                        resultingHeight = 0f;
                        return null;
                    }

                    List<WithHeight> boxEntryRects = remainingEntries.Select(e => new WithHeight(e, cache.GetMinimumHeight(e, fontSize, paragraphSpec, remainingRect.Width))).ToList();
                    List<WithHeight> remaining = new List<WithHeight>();

                    float CalculateTotalHeight(List<WithHeight> entries) {
                        float entriesHeight;
                        if (original.equalSizeFeatures) {
                            #pragma warning disable GJT0001 // entries will always have Count>0
                            entriesHeight = Divisions.CalculateTotalLength(entries.Select(w => w.MinimumHeight).Max(), entries.Count, original.gutter);
                            #pragma warning restore GJT0001 // Unhandled thrown exception from statement
                        }
                        else {
                            entriesHeight = Divisions.CalculateTotalLength(entries.Select(w => w.MinimumHeight), original.gutter);
                        }
                        return availableHeight - remainingRect.Height + entriesHeight;
                    }

                    while (boxEntryRects.Count > 0 && CalculateTotalHeight(boxEntryRects) > remainingRect.Height) {
                        #pragma warning disable GJT0001 // entries will always have Count>0
                        remaining.Add(boxEntryRects.Last());
                        #pragma warning restore GJT0001 // Unhandled thrown exception from statement
                        boxEntryRects.RemoveAt(boxEntryRects.Count - 1);
                    }

                    boxEntries = boxEntryRects.Select(w => w.Rect).ToArray();
                    remainingEntries = remaining.Select(w => w.Rect).Reverse().ToArray();
                }
                else { // boxCount == parts -> This is the last available box, so everything must be put in here.
                    boxEntries = remainingEntries;
                    remainingEntries = null;
                }

                if (boxEntries is not null && boxEntries.Length > 0) {
                    DynamicSectionRect fixedBox = new DynamicSectionRect(
                        original.Config,
                        original.outlines,
                        boxEntries,
                        original.gutter,
                        false,
                        original.AcceptRemaining,
                        original.equalSizeFeatures,
                        original.spaceFeatures) { Original = original, partIndex = boxCount - 1, partsCount = Boxes };
                    resultingHeight = cache.GetMinimumHeight(fixedBox, fontSize, paragraphSpec, width);
                    return fixedBox;
                }
                else {
                    resultingHeight = 0f;
                    return null;
                }
            }

            public IPartialCardSectionRects Clone() {
                return new DynamicSectionRectPieces(original, Boxes);
            }
        }
    }

    public class WidgetCardRect : IFixedCardSectionRect {

        public AbstractCardSectionConfig? Config { get; } = null;
        public IFixedCardSectionRect Original { get { return this; } }

        public readonly IWidget content;
        public bool Splittable { get { return false; } }
        public bool AcceptRemaining { get { return false; } }

        public WidgetCardRect(IWidget content) {
            this.content = content;
        }

        public virtual float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache) {
            graphicsState.SaveState();
            graphicsState.SetTextSize(fontSize);

            //Console.WriteLine($"WidgetCardRect.CalculateMinimumHeight: total {width}, gutter {content.Gutter}, child count {content.Children.Count}");

            //Rectangle remainingRect = content.RemainingRect(canvas, new Rectangle(width, 10000f));
            //float totalHeight = 10000f - (remainingRect?.Height ?? 0f);

            Size minimumSize = content.MinimumSize(graphicsState, Layout.ROWS, new Size(width, 10000f));
            float totalHeight = minimumSize.Height;

            graphicsState.RestoreState();

            return totalHeight;
        }

        public virtual void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
            canvas.RegisterAreas(this, rect, null, Array.Empty<Rectangle>());

            //Console.WriteLine($"WidgetCardRect.Draw: total {rect.Width}, gutter {content.Gutter}, child count {content.Children.Count}");

            canvas.SaveState();
            canvas.SetTextSize(fontSize);
            content.Draw(canvas, rect, default);
            canvas.RestoreState();

            //canvas.SaveState();
            //canvas.SetLineWidth(0.5f).SetStrokeColor(Color.Yellow).Rectangle(rect).Stroke();
            //canvas.RestoreState();
        }

        public IPartialCardSectionRects Split(int parts) {
            throw new NotSupportedException("Cannot split SimpleCardRect.");
        }
    }

}