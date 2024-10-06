using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using SharpSheets.Colors;
using SharpSheets.Shapes;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Layouts;
using SharpSheets.Utilities;

namespace SharpSheets.Cards.Card.SegmentRects {

    public interface ICardSegmentRect { }

    public interface IFixedCardSegmentRect : ICardSegmentRect {

        AbstractCardSegmentConfig? Config { get; }

        IFixedCardSegmentRect? Original { get; }

        bool AcceptRemaining { get; }

        // TODO Does ParagraphSpecification need to be an argument here? (It should be immutable now, and could be a property of the object)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="graphicsState"></param>
        /// <param name="fontSize"></param>
        /// <param name="paragraphSpec"></param>
        /// <param name="width"></param>
        /// <param name="cache"></param>
        /// <returns></returns>
        /// <exception cref="CardLayoutException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="InvalidRectangleException"></exception>
        float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache);

        bool Splittable { get; }

        /// <summary></summary>
        /// <param name="parts"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        IPartialCardSegmentRects Split(int parts);

        // TODO Does ParagraphSpecification need to be an argument here? (It should be immutable now, and could be a property of the object)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="rect"></param>
        /// <param name="fontSize"></param>
        /// <param name="paragraphSpec"></param>
        /// <exception cref="Exceptions.SharpDrawingException"></exception>
        /// <exception cref="InvalidRectangleException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec);
    }

    public interface IPartialCardSegmentRects : ICardSegmentRect {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="graphicsState"></param>
        /// <param name="availableHeight"></param>
        /// <param name="width"></param>
        /// <param name="fontSize"></param>
        /// <param name="paragraphSpec"></param>
        /// <param name="cache"></param>
        /// <param name="resultingHeight"></param>
        /// <returns></returns>
        /// <exception cref="CardLayoutException"></exception>
        /// <exception cref="InvalidRectangleException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        IFixedCardSegmentRect? FromAvailableHeight(ISharpGraphicsState graphicsState, float availableHeight, float width, float fontSize, ParagraphSpecification paragraphSpec, CardQueryCache cache, out float resultingHeight);
        int Boxes { get; set; }
        void Reset();

        bool PenaliseSplit { get; }

        IFixedCardSegmentRect Parent { get; }

        IPartialCardSegmentRects Clone();
    }

    public class CardQueryCache {
        private readonly Dictionary<CardQuery, float> cache;

        private readonly ISharpGraphicsState graphicsState;

        public int TotalCalls { get; private set; }
        public int NumCalculations { get; private set; }

        public CardQueryCache(ISharpGraphicsState graphicsState) {
            this.graphicsState = graphicsState;

            cache = new Dictionary<CardQuery, float>();

            TotalCalls = 0;
            NumCalculations = 0;
        }

        /// <summary></summary>
        /// <exception cref="CardLayoutException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="InvalidRectangleException"></exception>
        public float GetMinimumHeight(IFixedCardSegmentRect rect, float fontSize, ParagraphSpecification paragraphSpec, float width) {
            TotalCalls++;
            CardQuery query = new CardQuery(rect, fontSize, paragraphSpec, width);
            if (cache.TryGetValue(query, out float minimumHeight)) {
                return minimumHeight;
            }
            else {
                NumCalculations++;
                float calculatedMinimumHeight = rect.CalculateMinimumHeight(graphicsState, fontSize, paragraphSpec, width, this);
                cache[query] = calculatedMinimumHeight;
                return calculatedMinimumHeight;
            }
        }
    }

    public readonly struct CardQuery {
        public IFixedCardSegmentRect Rect { get; }
        public float FontSize { get; }
        public ParagraphSpecification ParagraphSpec { get; }
        public float Width { get; }

        public float LineSpacing => ParagraphSpec.LineSpacing;
        public float ParagraphSpacing => ParagraphSpec.ParagraphSpacing;
        public float Indent => ParagraphSpec.Indent;
        public float HangingIndent => ParagraphSpec.HangingIndent;

        public CardQuery(IFixedCardSegmentRect rect, float fontSize, ParagraphSpecification paragraphSpec, float width) {
            Rect = rect;
            FontSize = fontSize;
            ParagraphSpec = paragraphSpec;
            Width = width;
        }

        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 31 + Rect.GetHashCode();
                hash = hash * 31 + FontSize.GetHashCode();
                hash = hash * 31 + ParagraphSpec.GetHashCode();
                hash = hash * 31 + Width.GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object? obj) {
            if (obj is CardQuery query) {
                return Rect == query.Rect && Width == query.Width && FontSize == query.FontSize && ParagraphSpec.Equals(query.ParagraphSpec);
            }
            else {
                return false;
            }
        }
    }

    public class CardErrorSegmentRect : IFixedCardSegmentRect {

        public AbstractCardSegmentConfig? Config { get; } = null;
        public IFixedCardSegmentRect Original { get { return this; } }

        public bool Splittable { get; } = false;
        public bool AcceptRemaining { get; } = false;

        public readonly string Message;

        public CardErrorSegmentRect(string message) {
            Message = message;
        }

        public virtual float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache) {
            return 2 * fontSize;
        }

        public virtual void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
            canvas.RegisterAreas(this, rect, null, Array.Empty<Rectangle>());

            canvas.SaveState();
            new Simple(-1, stroke: Color.Red, trim: new Margins(0.5f * fontSize)).Draw(canvas, rect, out Rectangle remainingRect);
            canvas.SetTextColor(Color.Red);
            canvas.FitText(remainingRect, Message, new ParagraphSpecification(1f, 0f, default), new FontSizeSearchParams(0.05f, canvas.GetTextSize(), 0.1f), Justification.LEFT, Alignment.TOP, TextHeightStrategy.LineHeightDescent, false);
            canvas.RestoreState();
        }

        public IPartialCardSegmentRects Split(int parts) {
            throw new NotSupportedException($"Cannot split a {nameof(CardErrorSegmentRect)}.");
        }
    }

}