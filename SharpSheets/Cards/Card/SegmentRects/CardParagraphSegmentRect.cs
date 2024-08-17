using SharpSheets.Layouts;
using SharpSheets.Widgets;
using System;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Layouts;
using SharpSheets.Cards.CardSubjects;
using System.Linq;
using System.Collections.Generic;

namespace SharpSheets.Cards.Card.SegmentRects {

	public class CardFeatureText {

		public CardFeature Feature { get; }
		public RichParagraph Paragraph { get; }

		public bool IsListItem => Feature.IsListItem;

		public CardFeatureText(CardFeature feature, RichParagraph paragraph) {
			Feature = feature;
			Paragraph = paragraph;
		}
	}

	public class CardParagraphSegmentRect : AbstractSegmentRect<ParagraphCardSegmentConfig> {

		public ParagraphIndent ParagraphIndent => Config.paragraphIndent;
		public ParagraphIndent ListIndent => Config.listIndent;

		public Justification Justification => Config.justification;
		public Alignment Alignment => Config.alignment;
		public TextHeightStrategy HeightStrategy => Config.heightStrategy;

		public readonly CardFeatureText[] Features;
		//public RichParagraphs FullText { get; protected set; }

		protected bool IsParagraphStart { get; set; } = true;

		public CardParagraphSegmentRect(ParagraphCardSegmentConfig config, ArrangementCollection<IWidget> outlines, CardFeatureText[] features, bool splittable) : base(config, outlines, splittable) {
			Features = features;

			//FullText = new RichParagraphs(features.Select(f => f.Text).ToArray());
		}

		public ParagraphSpecification MakeSpec(ParagraphSpecification basis, bool listItem) {
			return new ParagraphSpecification(basis.LineSpacing, basis.ParagraphSpacing, listItem ? ListIndent : ParagraphIndent);
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

			float textHeight = 0f;
			for (int i = 0; i < Features.Length; i++) {
				ParagraphSpecification textSpec = MakeSpec(paragraphSpec, Features[i].IsListItem);

				RichString[] lines = Features[i].Paragraph.GetLines(graphicsState, tempRect.Width, fontSize, textSpec, i != 0 || IsParagraphStart);
				float featureHeight = RichStringLayout.CalculateHeight(graphicsState, new RichString[][] { lines }, fontSize, textSpec, HeightStrategy);

				//Console.WriteLine($"Feature height (calcmin): {featureHeight} ({lines.Length} lines)");

				textHeight += featureHeight;
				if (i != Features.Length - 1) {
					textHeight += textSpec.ParagraphSpacing;
				}
			}

			float totalHeight = exampleRect.Height - tempRect.Height + textHeight;

			//Console.WriteLine($"Calculate min: {totalHeight}, length {Features.Select(f => f.Paragraph.TokenCount).Sum()}");

			return totalHeight;
		}

		public override void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
			canvas.RegisterAreas(Original ?? this, rect, null, Array.Empty<Rectangle>());

			IWidget outline = GetOutline();

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

			List<ParagraphSpecification> paragraphSpecs = new List<ParagraphSpecification>();
			List<RichString[][]> featureLines = new List<RichString[][]>();
			List<float> featureHeights = new List<float>();

			for (int i = 0; i < Features.Length; i++) {
				ParagraphSpecification featureSpec = MakeSpec(paragraphSpec, Features[i].IsListItem);
				RichString[] lines = Features[i].Paragraph.GetLines(canvas, remainingRect.Width, fontSize, featureSpec, i != 0 || IsParagraphStart);
				RichString[][] paragraph = new RichString[][] { lines };
				float height = RichStringLayout.CalculateHeight(canvas, paragraph, fontSize, featureSpec, HeightStrategy);

				paragraphSpecs.Add(featureSpec);
				featureLines.Add(paragraph);
				featureHeights.Add(height);
			}

			float totalHeight = featureHeights.Sum() + paragraphSpec.ParagraphSpacing * (featureHeights.Count - 1);
			//float yPosition = RichStringLayout.GetStartY(remainingRect, totalHeight, paragraphSpec.FontSize, this.Alignment);

			float areaY;
			if (Alignment == Alignment.TOP) {
				areaY = remainingRect.Top - totalHeight;
			}
			else if (Alignment == Alignment.CENTRE) {
				areaY = remainingRect.CentreY - totalHeight / 2f;
			}
			else { // Alignment == Alignment.Bottom
				areaY = remainingRect.Bottom;
			}
			Rectangle area = new Rectangle(remainingRect.X, areaY, remainingRect.Width, totalHeight);

			float yPos = area.Top;

			for (int i = 0; i < Features.Length; i++) {
				Rectangle featureRect = new Rectangle(remainingRect.X, yPos - featureHeights[i], remainingRect.Width, featureHeights[i]);

				canvas.RegisterAreas(Features[i].Feature, featureRect, null, Array.Empty<Rectangle>());

				if (Features[i].IsListItem && (i != 0 || IsParagraphStart) && Config.bullet != null && Config.bullet.Symbol.Length > 0) {
					//float firstLineY = RichStringLayout.GetStartY(featureRect, featureHeights[i], paragraphSpecs[i].FontSize, SharpSheets.Canvas.Text.Alignment.TOP);
					float firstLineY = RichStringLayout.GetStartY(canvas, featureRect, featureLines[i], fontSize, paragraphSpecs[i], Alignment.TOP, HeightStrategy);

					float bulletFontSize = Config.bullet.FontSizeMultiplier * fontSize;
					float bulletIndent = Config.bullet.Indent;
					float bulletOffset = Config.bullet.Offset * fontSize;

					canvas.SaveState();
					canvas.SetTextSize(bulletFontSize);
					canvas.SetFont(TextFormat.REGULAR, Config.bullet.FontPath);
					canvas.DrawText(Config.bullet.Symbol, featureRect.Left + bulletIndent, firstLineY + bulletOffset);
					canvas.RestoreState();
				}

				canvas.DrawRichText(featureRect, featureLines[i], fontSize, paragraphSpecs[i], Justification, Alignment.TOP, HeightStrategy, i != 0 || IsParagraphStart);

				yPos -= featureHeights[i] + paragraphSpecs[i].ParagraphSpacing;
			}
		}

		public override IPartialCardSegmentRects Split(int parts) {
			if (Splittable) {
				return new CardParagraphSegmentRectPieces(this, parts);
			}
			else {
				throw new InvalidOperationException($"Cannot split a {GetType().Name} with Splittable==false.");
			}
		}

		#region Card Paragraph Pieces

		public class CardParagraphSegmentRectPieces : IPartialCardSegmentRects {
			public IFixedCardSegmentRect Parent { get { return original; } }
			readonly CardParagraphSegmentRect original;
			public int Boxes { get; set; }

			public bool PenaliseSplit { get; } = false;

			CardFeatureText[]? remainingFeatures;
			bool remainingIsParagraphStart;
			int boxCount;

			public CardParagraphSegmentRectPieces(CardParagraphSegmentRect original, int boxes) {
				this.original = original;
				Boxes = boxes;
			}

			public void Reset() {
				remainingFeatures = original.Features;
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

				CardFeatureText[]? boxFeatures;
				bool splitInParagraph = false;

				if (boxCount < Boxes) {

					if (remainingFeatures == null || remainingFeatures.Length == 0 || availableHeight <= fontSize) {
						resultingHeight = 0f;
						return null;
					}

					IWidget outline = original.outlines[boxCount - 1, Boxes];
					Rectangle remainingRect = new Rectangle(width, availableHeight);
					if (outline != null) {
						try {
							remainingRect = outline.RemainingRect(graphicsState, remainingRect) ?? remainingRect;
						}
						catch (InvalidRectangleException) {
							resultingHeight = 0f;
							return null;
						}
					}

					float remainingHeight = remainingRect.Height;

					List<CardFeatureText> fitFeatures = new List<CardFeatureText>();
					List<CardFeatureText> overflowFeatures = new List<CardFeatureText>();

					int i = 0;
					for (; i < remainingFeatures.Length; i++) {
						ParagraphSpecification textSpec = original.MakeSpec(paragraphSpec, remainingFeatures[i].IsListItem);
						bool indent = i != 0 || remainingIsParagraphStart;

						RichParagraph? first = remainingFeatures[i].Paragraph.SplitByHeight(graphicsState, remainingRect.Width, remainingHeight, fontSize, textSpec, indent, out RichParagraph? overflowText);

						if (overflowText != null) {
							if (first != null) {
								splitInParagraph = true;
								fitFeatures.Add(new CardFeatureText(remainingFeatures[i].Feature, first));

								/*
								RichString[] lines = first.GetLines(graphicsState, remainingRect.Width, textSpec, indent);
								float featureHeight = RichStringUtils.CalculateHeight(new RichString[][] { lines }, textSpec);
								remainingHeight -= featureHeight + (i != remainingFeatures.Length - 1 ? textSpec.ParagraphSpacing : 0f);
								Console.WriteLine($"Feature height (fromavail-fin): {featureHeight} ({lines.Length} lines)");
								*/
							}
							overflowFeatures.Add(new CardFeatureText(remainingFeatures[i].Feature, overflowText));

							// Finish loop now
							break;
						}
						else {
							fitFeatures.Add(remainingFeatures[i]);
							RichString[] lines = remainingFeatures[i].Paragraph.GetLines(graphicsState, remainingRect.Width, fontSize, textSpec, indent);
							float featureHeight = RichStringLayout.CalculateHeight(graphicsState, new RichString[][] { lines }, fontSize, textSpec, original.HeightStrategy);
							remainingHeight -= featureHeight + (i != remainingFeatures.Length - 1 ? textSpec.ParagraphSpacing : 0f);
							//Console.WriteLine($"Feature height (fromavail-cont): {featureHeight} ({lines.Length} lines)");
						}
					}
					i++;
					for (; i < remainingFeatures.Length; i++) {
						overflowFeatures.Add(remainingFeatures[i]);
					}

					remainingFeatures = overflowFeatures.ToArray();

					boxFeatures = fitFeatures.ToArray();

					/*
					float finalHeight = remainingRect.Height - remainingHeight;
					if (original.outline != null) {
						finalHeight += availableHeight - remainingRect.Height;
					}
					Console.WriteLine($"Paragraph: {finalHeight} / {availableHeight}, length: {boxFeatures.Select(f => f.Paragraph.TokenCount).Sum()}");
					*/
					//Console.WriteLine(string.Join(" \n ", boxFeatures.Select(f => f.Paragraph.ToString())));
				}
				else { // boxCount == parts -> This is the last available box, so everything must be put in here.
					boxFeatures = remainingFeatures;
					remainingFeatures = null;
					//splitInParagraph = false;
				}

				CardParagraphSegmentRect? nextBox;
				if (boxFeatures != null && boxFeatures.Length > 0) {
					nextBox = new CardParagraphSegmentRect(original.Config, original.outlines, boxFeatures, false) {
						Original = original,
						IsParagraphStart = remainingIsParagraphStart,
						PartIndex = boxCount - 1,
						PartsCount = Boxes
					};
					resultingHeight = cache.GetMinimumHeight(nextBox, fontSize, paragraphSpec, width);
				}
				else {
					nextBox = null;
					resultingHeight = 0f;
				}

				//Console.WriteLine($"Paragraph final: {resultingHeight} / {availableHeight}, length: {nextBox?.Features.Select(f => f.Paragraph.TokenCount).Sum() ?? -1}");
				//Console.WriteLine(string.Join(" \\n ", nextBox.Features.Select(f => f.Paragraph.ToString())));

				remainingIsParagraphStart = !splitInParagraph;

				return nextBox;
			}

			public IPartialCardSegmentRects Clone() {
				return new CardParagraphSegmentRectPieces(original, Boxes);
			}
		}

		#endregion
	}

}