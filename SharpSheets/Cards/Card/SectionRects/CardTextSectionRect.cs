using SharpSheets.Layouts;
using SharpSheets.Widgets;
using System;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Layouts;

namespace SharpSheets.Cards.Card.SectionRects {

    public class CardTextSectionRect : IFixedCardSectionRect {

        AbstractCardSectionConfig IFixedCardSectionRect.Config => Config;
        public TextCardSectionConfig Config { get; }
        public IFixedCardSectionRect? Original { get; set; }

        public readonly ArrangementCollection<IWidget> outlines;
        public RichParagraphs Text { get; protected set; }
        public bool Splittable { get; }
        public bool AcceptRemaining => Config.acceptRemaining;

        public float ParagraphIndent => Config.paragraphIndent.Indent;
        public float ParagraphHanging => Config.paragraphIndent.Hanging;

        public Justification Justification => Config.justification;
        public Alignment Alignment => Config.alignment;
        public TextHeightStrategy HeightStrategy => Config.heightStrategy;

        protected bool IsParagraphStart { get; set; } = true;

        private int partIndex { get; set; } = 0;
        private int partsCount { get; set; } = 1;

        public CardTextSectionRect(TextCardSectionConfig config, ArrangementCollection<IWidget> outlines, RichParagraphs text, bool splittable) {
            Config = config;
            this.outlines = outlines;
            Text = text;
            Splittable = splittable;
        }

        private IWidget GetOutline() {
            return outlines[partIndex, partsCount];
        }

        internal ParagraphSpecification MakeSpec(ParagraphSpecification basis) {
            return new ParagraphSpecification(basis.LineSpacing, basis.ParagraphSpacing, ParagraphIndent, ParagraphHanging);
        }

        public virtual float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache) {
            IWidget outline = GetOutline();
            Rectangle exampleRect = new Rectangle(width, 10000f);
            Rectangle tempRect;
            if (outline != null) {
                tempRect = outline.RemainingRect(graphicsState, exampleRect) ?? exampleRect;
            }
            else {
                tempRect = exampleRect; // TODO I think this is good...?
            }

            ParagraphSpecification textSpec = MakeSpec(paragraphSpec);

            float textHeight = RichStringLayout.CalculateHeight(graphicsState, Text.GetLines(graphicsState, tempRect.Width, fontSize, textSpec, IsParagraphStart), fontSize, textSpec, HeightStrategy);

            float totalHeight = exampleRect.Height - tempRect.Height + textHeight;

            return totalHeight;
        }

        public virtual void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
            canvas.RegisterAreas(Original ?? this, rect, null, Array.Empty<Rectangle>());

            IWidget outline = GetOutline();
            ParagraphSpecification textSpec = MakeSpec(paragraphSpec);

            Rectangle remainingRect;
            if (outline != null) {
                canvas.SaveState();
                canvas.SetTextSize(fontSize);
                outline.Draw(canvas, rect, default);
                remainingRect = outline.RemainingRect(canvas, rect) ?? rect;
                //canvas.RegisterArea(Original ?? this, remainingRect);
                canvas.RestoreState();
            }
            else {
                remainingRect = rect;
            }

            canvas.SaveState();

            canvas.DrawRichText(remainingRect, Text.GetLines(canvas, remainingRect.Width, fontSize, textSpec, IsParagraphStart), fontSize, textSpec, Justification, Alignment, HeightStrategy, IsParagraphStart); // TODO Justification and Alignment should be set by user

            canvas.RestoreState();
        }

        public IPartialCardSectionRects Split(int parts) {
            if (Splittable) {
                return new CardTextSectionRectPieces(this, parts);
            }
            else {
                throw new InvalidOperationException($"Cannot split a {GetType().Name} with Splittable==false.");
            }
        }

        #region Card Text Pieces

        public class CardTextSectionRectPieces : IPartialCardSectionRects {
            public IFixedCardSectionRect Parent { get { return original; } }
            readonly CardTextSectionRect original;
            public int Boxes { get; set; }

            public bool PenaliseSplit { get; } = false;

            RichParagraphs? remainingLines;
            bool remainingIsParagraphStart;
            int boxCount;

            public CardTextSectionRectPieces(CardTextSectionRect original, int boxes) {
                this.original = original;
                Boxes = boxes;
            }

            public void Reset() {
                remainingLines = original.Text;
                remainingIsParagraphStart = true;
                boxCount = 0;
            }

            /*
			public void RemoveBox() {
				Boxes = Math.Max(0, Boxes - 1);
			}
			*/

            public IFixedCardSectionRect? FromAvailableHeight(ISharpGraphicsState graphicsState, float availableHeight, float width, float fontSize, ParagraphSpecification paragraphSpec, CardQueryCache cache, out float resultingHeight) {

                boxCount++;

                if (boxCount > Boxes) {
                    throw new CardLayoutException($"Too many requests for boxes for {GetType().Name} ({boxCount} > {Boxes})");
                }

                ParagraphSpecification textSpec = original.MakeSpec(paragraphSpec);

                RichParagraphs? boxText;
                bool splitInParagraph;

                if (boxCount < Boxes) {

                    if (remainingLines == null || remainingLines.TokenCount == 0 || availableHeight <= fontSize) {
                        resultingHeight = 0f;
                        return null;
                    }

                    Rectangle remainingRect = new Rectangle(width, availableHeight);
                    IWidget outline = original.outlines[boxCount - 1, Boxes];
                    if (outline != null) {
                        remainingRect = outline.RemainingRect(graphicsState, remainingRect) ?? remainingRect;
                    }

                    boxText = remainingLines.SplitByHeight(graphicsState, remainingRect.Width, remainingRect.Height, fontSize, textSpec, remainingIsParagraphStart, out remainingLines, out splitInParagraph);

                }
                else { // boxCount == parts -> This is the last available box, so everything must be put in here.
                    boxText = remainingLines; // RichString.Join((RichString)"\n", remainingLines);
                    remainingLines = null;
                    splitInParagraph = false;
                }

                CardTextSectionRect? nextBox;
                if (boxText != null) {
                    nextBox = new CardTextSectionRect(original.Config, original.outlines, boxText, false) {
                        Original = original,
                        IsParagraphStart = remainingIsParagraphStart,
                        partIndex = boxCount - 1,
                        partsCount = Boxes
                    };
                    resultingHeight = cache.GetMinimumHeight(nextBox, fontSize, textSpec, width); // fixedBox.CalculateMinimumHeight1(graphicsState, font, width); // TODO This seems inefficient here?
                }
                else {
                    nextBox = null;
                    resultingHeight = 0f;
                }

                remainingIsParagraphStart = !splitInParagraph;

                return nextBox;
            }

            public IPartialCardSectionRects Clone() {
                return new CardTextSectionRectPieces(original, Boxes);
            }
        }

        #endregion

    }

}