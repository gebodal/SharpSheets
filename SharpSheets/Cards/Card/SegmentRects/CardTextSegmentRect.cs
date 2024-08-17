using SharpSheets.Layouts;
using SharpSheets.Widgets;
using System;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Layouts;

namespace SharpSheets.Cards.Card.SegmentRects {

	public class CardTextSegmentRect : AbstractSegmentRect<TextCardSegmentConfig> {

		public RichParagraphs Text { get; protected set; }

		public float ParagraphIndent => Config.paragraphIndent.Indent;
		public float ParagraphHanging => Config.paragraphIndent.Hanging;

		public Justification Justification => Config.justification;
		public Alignment Alignment => Config.alignment;
		public TextHeightStrategy HeightStrategy => Config.heightStrategy;

		protected bool IsParagraphStart { get; set; } = true;

		public CardTextSegmentRect(TextCardSegmentConfig config, ArrangementCollection<IWidget> outlines, RichParagraphs text, bool splittable) : base(config, outlines, splittable) {
			Text = text;
		}

		internal ParagraphSpecification MakeSpec(ParagraphSpecification basis) {
			return new ParagraphSpecification(basis.LineSpacing, basis.ParagraphSpacing, ParagraphIndent, ParagraphHanging);
		}

		public override float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache) {
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

		public override void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
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

		public override IPartialCardSegmentRects Split(int parts) {
			if (Splittable) {
				return new CardTextSegmentRectPieces(this, parts);
			}
			else {
				throw new InvalidOperationException($"Cannot split a {GetType().Name} with Splittable==false.");
			}
		}

		#region Card Text Pieces

		public class CardTextSegmentRectPieces : IPartialCardSegmentRects {
			public IFixedCardSegmentRect Parent { get { return original; } }
			readonly CardTextSegmentRect original;
			public int Boxes { get; set; }

			public bool PenaliseSplit { get; } = false;

			RichParagraphs? remainingLines;
			bool remainingIsParagraphStart;
			int boxCount;

			public CardTextSegmentRectPieces(CardTextSegmentRect original, int boxes) {
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

			public IFixedCardSegmentRect? FromAvailableHeight(ISharpGraphicsState graphicsState, float availableHeight, float width, float fontSize, ParagraphSpecification paragraphSpec, CardQueryCache cache, out float resultingHeight) {

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
						try {
							remainingRect = outline.RemainingRect(graphicsState, remainingRect) ?? remainingRect;
						}
						catch (InvalidRectangleException) {
							resultingHeight = 0f;
							return null;
						}
					}

					boxText = remainingLines.SplitByHeight(graphicsState, remainingRect.Width, remainingRect.Height, fontSize, textSpec, remainingIsParagraphStart, out remainingLines, out splitInParagraph);

				}
				else { // boxCount == parts -> This is the last available box, so everything must be put in here.
					boxText = remainingLines; // RichString.Join((RichString)"\n", remainingLines);
					remainingLines = null;
					splitInParagraph = false;
				}

				CardTextSegmentRect? nextBox;
				if (boxText != null) {
					nextBox = new CardTextSegmentRect(original.Config, original.outlines, boxText, false) {
						Original = original,
						IsParagraphStart = remainingIsParagraphStart,
						PartIndex = boxCount - 1,
						PartsCount = Boxes
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

			public IPartialCardSegmentRects Clone() {
				return new CardTextSegmentRectPieces(original, Boxes);
			}
		}

		#endregion

	}

}