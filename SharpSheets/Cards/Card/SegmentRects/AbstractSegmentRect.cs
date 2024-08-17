using SharpSheets.Layouts;
using SharpSheets.Widgets;
using System;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Layouts;

namespace SharpSheets.Cards.Card.SegmentRects {

	public abstract class AbstractSegmentRect<TConfig> : IFixedCardSegmentRect where TConfig : AbstractCardSegmentConfig {

		AbstractCardSegmentConfig IFixedCardSegmentRect.Config => Config;
		public TConfig Config { get; }
		public IFixedCardSegmentRect? Original { get; protected init; }

		public readonly ArrangementCollection<IWidget> outlines;
		public virtual bool Splittable { get; }
		public virtual bool AcceptRemaining => Config.acceptRemaining;

		protected int PartIndex { get; init; } = 0;
		protected int PartsCount { get; init; } = 1;

		public AbstractSegmentRect(TConfig config, ArrangementCollection<IWidget> outlines, bool splittable) {
			Config = config;
			this.outlines = outlines;
			Splittable = splittable;
		}

		protected IWidget GetOutline() {
			return outlines[PartIndex, PartsCount];
		}

		public abstract float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache);
		public abstract void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec);
		public abstract IPartialCardSegmentRects Split(int parts);

	}

}
