﻿using SharpSheets.Layouts;
using SharpSheets.Shapes;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpSheets.Canvas;
using SharpSheets.Fonts;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Parsing;
using SharpSheets.Cards.Card.SegmentRects;
using SharpSheets.Cards.CardConfigs;

namespace SharpSheets.Cards.Card
{

    public class DynamicCard : AbstractCard {

		public CardSubject Subject { get; }
		public CardSetConfig CardSetConfig => Subject.CardConfig.cardSetConfig;
		public CardConfig CardConfig => Subject.CardConfig;

		public readonly IDetail? gutterStyle;
		public readonly ArrangementCollection<IWidget> backgrounds;
		public readonly ArrangementCollection<IWidget> outlines;

		public readonly bool joinSplitCards;

		public override bool CropOnFinalCard { get; }

		public DynamicCard(CardSubject subject, float gutter, bool joinSplitCards, bool cropOnFinalCard, ArrangementCollection<IWidget> backgrounds, IDetail? gutterStyle, ArrangementCollection<IWidget> outlines, IFixedCardSegmentRect[] segments) {
			this.Subject = subject;
			this.Gutter = gutter;
			this.joinSplitCards = joinSplitCards;
			this.backgrounds = backgrounds;
			this.gutterStyle = gutterStyle;
			this.outlines = outlines;
			this.Segments = segments;
			this.CropOnFinalCard = cropOnFinalCard;
		}

		public override IFixedCardSegmentRect[] Segments { get; }

		public override float Gutter { get; }

		protected static IWidget GetFrom(ArrangementCollection<IWidget> collection, int card, int totalCards) {
			//IWidget rect = collection.GetValue(DynamicCardEnvironments.CardNumberEnvironment(subject.Environment, card, totalCards));
			/*
			IWidget rect = collection.GetValue(subject.Environment.AppendDefinitionEnvironment(CardOutlinesEnvironments.GetEnvironment(card, totalCards)));
			if (rect == null) {
				rect = new Div(new WidgetSetup());
			}
			return rect;
			*/
			IWidget rect = collection.GetValue(card, totalCards);
			if(rect == null) {
				rect = new Div(new WidgetSetup());
			}
			return rect;
		}

		protected IWidget GetOutline(int card, int totalCards) {
			return GetFrom(outlines, card, totalCards);
		}

		protected IWidget GetBackground(int card, int totalCards) {
			return GetFrom(backgrounds, card, totalCards);
		}

		public override void DrawOutline(ISharpCanvas canvas, Rectangle rect, int card, int totalCards) {
			IWidget outline = GetOutline(card, totalCards);
			IWidget background = GetBackground(card, totalCards);
			Rectangle outlineRect = background.RemainingRect(canvas, rect) ?? rect; // TODO Yes? Should we not draw the outline instead?
			outline.Draw(canvas, outlineRect, default);
		}

		public override void DrawBackground(ISharpCanvas canvas, Rectangle[] rects) {
			List<Rectangle> outlineRects = new List<Rectangle>();

			if (joinSplitCards) {
				IWidget background = GetBackground(0, 1);
				Rectangle overallRect = Rectangle.GetCommonRectangle(rects);
				background.Draw(canvas, overallRect, default);
				outlineRects.Add(overallRect);
			}
			else {
				for (int i = 0; i < rects.Length; i++) {
					IWidget background = GetBackground(i, rects.Length);
					background.Draw(canvas, rects[i], default);
				}
				outlineRects.AddRange(rects);
			}

			foreach (Rectangle outlineRect in outlineRects) {
				canvas.RegisterAreas(this, outlineRect, null, Array.Empty<Rectangle>());
				canvas.RegisterAreas(Subject, outlineRect, null, Array.Empty<Rectangle>());
			}
		}

		public override void DrawGutter(ISharpCanvas canvas, Rectangle rect) {
			if (gutterStyle != null) {
				gutterStyle.Layout = Layout.ROWS;
				gutterStyle.Draw(canvas, rect);
			}
		}

		public override Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle rect, int card, int totalCards) {
			IWidget background = GetBackground(card, totalCards);
			IWidget outline = GetOutline(card, totalCards);

			// TODO Yes? Is this the right setup?
			rect = background?.RemainingRect(graphicsState, rect) ?? rect;
			rect = outline?.RemainingRect(graphicsState, rect) ?? rect;

			return rect;
		}

	}

	public sealed class CardText : SharpWidget {

		public class ParagraphDataArg : ISharpArgsGrouping {
			public readonly float Spacing;
			public readonly ParagraphIndent Indent;

			public ParagraphDataArg(float spacing = 0f, float indent = 0f, float hanging = 0f) {
				this.Spacing = spacing;
				this.Indent = new ParagraphIndent(indent, hanging);
			}
		}

		private readonly RichString text;
		private readonly Justification justification;
		private readonly Alignment alignment;
		private readonly TextHeightStrategy heightStrategy;
		//private readonly float lineSpacing;
		//private readonly float paragraphSpacing;
		private readonly float multiplier;

		public readonly FontSetting? dingbatsPath;
		public readonly string? bullet;
		public readonly (float x,float y) bulletOffset;

		private readonly ParagraphSpecification paragraphSpec;

		public CardText(
			WidgetSetup setup,
			List<RichString>? text = null,
			Justification justification = Justification.LEFT,
			Alignment alignment = Alignment.TOP,
			TextHeightStrategy heightStrategy = TextHeightStrategy.LineHeightBaseline,
			float lineSpacing = 1.35f,
			ParagraphDataArg? paragraph = null,
			float multiplier = 1f,
			FontSetting? dingbats = null,
			string? bullet = null,
			(float x,float y) bulletOffset = default
		) : base(setup) {

			this.text = RichString.Join("\n", TextFormat.REGULAR, text ?? Enumerable.Empty<RichString>());
			this.justification = justification;
			this.alignment = alignment;
			this.heightStrategy = heightStrategy;
			//this.lineSpacing = lineSpacing;
			//this.paragraphSpacing = paragraphSpacing;
			paragraph ??= new ParagraphDataArg();
			this.paragraphSpec = new ParagraphSpecification(lineSpacing, paragraph.Spacing, paragraph.Indent);
			this.multiplier = multiplier;

			this.dingbatsPath = dingbats;
			this.bullet = bullet;
			this.bulletOffset = bulletOffset;
		}

		private float GetFontSize(ISharpGraphicsState graphicsState) {
			return multiplier * graphicsState.GetTextSize();
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			float finalFontSize = GetFontSize(canvas);

			/*
			if(this.text.Text == "observing people or places") {
				Console.WriteLine($"\nCardText.DrawWidget (observing): {rect}, fontsize {finalFontSize}");
				Console.WriteLine(System.Environment.StackTrace);
			}
			if (this.text.Text.StartsWith("Age: ")) {
				Console.WriteLine($"\nCardText.DrawWidget (age): {rect}, fontsize {finalFontSize}");
				Console.WriteLine(System.Environment.StackTrace);
			}
			*/

			RichString[][] lines = RichStringLineSplitting.SplitParagraphs(canvas, text, rect.Width, finalFontSize, paragraphSpec, true);

			if (bullet != null && bullet.Length > 0) {
				float firstLineY = RichStringLayout.GetStartY(canvas, rect, lines, finalFontSize, paragraphSpec, alignment, heightStrategy);

				canvas.SaveState();
				canvas.SetFont(TextFormat.REGULAR, dingbatsPath);
				canvas.DrawText(bullet, rect.Left + bulletOffset.x, firstLineY + bulletOffset.y);
				canvas.RestoreState();
			}

			canvas.DrawRichText(rect, lines, finalFontSize, paragraphSpec, justification, alignment, heightStrategy, true);
			

			//canvas.SaveState();
			//canvas.SetLineWidth(0.6f).SetStrokeColor(Color.Goldenrod).Rectangle(rect).Stroke();
			//canvas.RestoreState();
		}

		protected override Rectangle? GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return null;
		}

		protected override Size GetMinimumContentSize(ISharpGraphicsState graphicsState, Size availableSpace) {
			float finalFontSize = GetFontSize(graphicsState);

			/*
			if (this.text.Text == "observing people or places") {
				Console.WriteLine($"\nCardText.GetMinimumContentSize (observing): {availableSpace}, fontsize {finalFontSize}");
				Console.WriteLine(System.Environment.StackTrace);
			}
			if (this.text.Text.StartsWith("Age: ")) {
				Console.WriteLine($"\nCardText.GetMinimumContentSize (age): {availableSpace}, fontsize {finalFontSize}");
				Console.WriteLine(System.Environment.StackTrace);
			}
			*/

			RichString[][] singleLineParagraphs = text.Split('\n').Select(s => new RichString[] { s }).ToArray();

			// TODO Does this work now?
			float width = Math.Min(availableSpace.Width, RichStringLayout.CalculateWidth(graphicsState, singleLineParagraphs, finalFontSize, paragraphSpec, true));
			float height = RichStringLayout.CalculateHeight(graphicsState, text, availableSpace.Width, finalFontSize, paragraphSpec, heightStrategy, true);

			return new Size(width, height);
		}
	}
}