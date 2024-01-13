using SharpSheets.Cards.Card;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Cards.Card.SegmentRects;

namespace SharpSheets.Cards.Layouts {

    public class SingleCardLayout {

		public readonly Size sampleFeatureSize;

		public readonly IFixedCardSegmentRect[] boxes;
		public readonly Size[] areas;
		public readonly float[] minHeights;

		public readonly float unusedHeight;

		public int Length => boxes.Length;

		public SingleCardLayout(Size sampleFeatureRect, IFixedCardSegmentRect[] boxes, Size[] areas, float[] minHeights, float unusedHeight) {
			this.sampleFeatureSize = sampleFeatureRect;

			this.boxes = boxes;
			this.areas = areas;
			this.minHeights = minHeights;

			this.unusedHeight = unusedHeight;
		}

	}

	public class CardArrangement {

		public readonly AbstractCard card;
		public readonly Size sampleSize;
		public readonly float FontSize;
		public readonly ParagraphSpecification paragraphSpec;

		public readonly SingleCardLayout[] layouts;

		public readonly int penalisedSplitCount;

		public readonly int calls;

		public int Length => layouts.Length;

		public CardArrangement(AbstractCard card, Size sampleSize, float fontSize, ParagraphSpecification paragraphSpec, SingleCardLayout[] layouts, int penalisedSplitCount, int calls) {
			this.card = card;
			this.sampleSize = sampleSize;
			this.FontSize = fontSize;
			this.paragraphSpec = paragraphSpec;

			this.layouts = layouts;

			this.penalisedSplitCount = penalisedSplitCount;

			this.calls = calls;
		}

	}

	public class CardArrangementComparer : IEqualityComparer<ICardSegmentRect[][]> {

		public static bool BoxEquality(ICardSegmentRect b1, ICardSegmentRect b2) {
			if (b1 is IPartialCardSegmentRects p1 && b2 is IPartialCardSegmentRects p2) {
				return p1.Parent == p2.Parent;
			}
			return b1 == b2;
		}

		public bool Equals(ICardSegmentRect[][]? a, ICardSegmentRect[][]? b) {
			if(a is null || b is null) {
				return a is null && b is null;
			}
			else if (a.Length == b.Length) {
				for (int i = 0; i < a.Length; i++) {
					if (a[i].Length != b[i].Length) {
						return false;
					}
				}
				// So far we know a and b are the same shape
				for (int i = 0; i < a.Length; i++) {
					for (int j = 0; j < a[i].Length; j++) {
						if (!BoxEquality(a[i][j], b[i][j])) {
							return false;
						}
					}
				}
				return true;
			}
			else {
				return false;
			}
		}

		public int GetHashCode(ICardSegmentRect[][] obj) {
			return string.Join(", ", obj.Select(a => "[" + string.Join(", ", a.Select(b => b.GetType().Name)) + "]")).GetHashCode();

		}
	}

}
