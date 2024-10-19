using SharpSheets.Canvas;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Cards.Definitions;
using SharpSheets.Evaluations;
using SharpSheets.Exceptions;
using SharpSheets.Layouts;
using SharpSheets.Parsing;
using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using SharpSheets.Cards.Card.SegmentRects;

namespace SharpSheets.Cards.Card {

	public static class DynamicCardFactory {

		public static DynamicCard CreateCard(CardSubject subject, ParseOrigins<IDocumentEntity>? origins, ParseOrigins<ICardConfigComponent>? configOrigins, WidgetFactory widgetFactory, out List<SharpParsingException> errors) {
			CardSetConfig cardSetConfig = subject.CardConfig.cardSetConfig;
			CardConfig cardConfig = subject.CardConfig;

			errors = new List<SharpParsingException>();

			(ArrangementCollection<IWidget> outlineCollection, ArrangementCollection<IWidget> backgroundCollection) = GetCardOutlineCollections(subject, widgetFactory, errors);

			List<IFixedCardSegmentRect> segmentRects = new List<IFixedCardSegmentRect>();
			foreach (CardSegment segment in subject) {
				segmentRects.Add(CreateRect(segment, errors, origins, configOrigins, widgetFactory));
			}

			IEnumerable<DynamicCardSegmentConfig> alwaysIncluded = cardConfig
				.AllCardSegments
				.Where(c => c.Value is DynamicCardSegmentConfig d && d.AlwaysInclude)
				.Where(c => c.Condition.Evaluate(subject.Environment))
				.Select(c => (DynamicCardSegmentConfig)c.Value); // .Reverse();
			bool hasAlwaysIncluded = false;
			foreach (DynamicCardSegmentConfig segmentConfig in alwaysIncluded) {
				IFixedCardSegmentRect segment = CreateDefaultRect(segmentConfig, subject.CardConfig, subject, errors, origins, configOrigins, widgetFactory);
				segmentRects.Add(segment);
				hasAlwaysIncluded = true;
			}

			IFixedCardSegmentRect[] orderedSegments = segmentRects.ToArray();
			if (hasAlwaysIncluded || orderedSegments.Any(s => s.Config?.atPosition != null)) {
				orderedSegments = OrderRects(orderedSegments);
			}

			DynamicCard dynamicCard = new DynamicCard(subject, backgroundCollection, outlineCollection, orderedSegments);
			if (origins != null) { origins.Add(dynamicCard, subject); }
			if (configOrigins != null) { configOrigins.Add(dynamicCard, subject.CardConfig); }
			return dynamicCard;
		}

		private static (ArrangementCollection<IWidget> outlines, ArrangementCollection<IWidget> backgrounds) GetCardOutlineCollections(CardSubject subject, WidgetFactory widgetFactory, List<SharpParsingException> errors) {
			CardSetConfig cardSetConfig = subject.CardConfig.cardSetConfig;
			CardConfig cardConfig = subject.CardConfig;

			ArrangementCollection<IWidget> outlineCollection = new ArrangementCollection<IWidget>((int)subject.CardConfig.MaxCards, new Div(new WidgetSetup()));
			ArrangementCollection<IWidget> backgroundCollection = new ArrangementCollection<IWidget>((int)subject.CardConfig.MaxCards, new Div(new WidgetSetup()));

			// Only the card number/total can vary, so just enumerate all possibilities and store them
			for (int cardCount = 1; cardCount <= subject.CardConfig.MaxCards; cardCount++) {
				for (int card = 0; card < cardCount; card++) {
					IEnvironment cardOutlinesEnvironment = Environments.Concat(
						CardOutlinesEnvironments.GetEnvironment(card, cardCount),
						subject.Environment,
						BasisEnvironment.Instance
						);

					if(ProcessCardArrangementOutline(subject, cardConfig.outlines, cardConfig.cardSetConfig.outlines, cardOutlinesEnvironment, cardSetConfig.Source, widgetFactory, errors, "outline", out IWidget? outlineRect)) {
						outlineCollection[card, cardCount] = outlineRect;
					}

					if (ProcessCardArrangementOutline(subject, cardConfig.backgrounds, cardConfig.cardSetConfig.backgrounds, cardOutlinesEnvironment, cardSetConfig.Source, widgetFactory, errors, "background", out IWidget? backgroundRect)) {
						backgroundCollection[card, cardCount] = backgroundRect;
					}
				}
			}

			return (outlineCollection, backgroundCollection);
		}

		private static bool ProcessCardArrangementOutline(CardSubject subject, ConditionalCollection<InterpolatedContext> primary, ConditionalCollection<InterpolatedContext> fallback, IEnvironment environment, DirectoryPath source, WidgetFactory widgetFactory, List<SharpParsingException> errors, string typeName, [MaybeNullWhen(false)] out IWidget result) {
			IContext? outlineContext;
			try {
				InterpolatedContext? outlineExprContext = primary.GetValue(environment) ?? fallback.GetValue(environment);
				if (outlineExprContext is not null) {
					outlineContext = outlineExprContext.Evaluate(environment, out SharpParsingException[] contextErrors);
					errors.AddRange(contextErrors);
				}
				else {
					outlineContext = null;
				}
			}
			catch (EvaluationException e) {
				errors.Add(new SharpParsingException(subject.Location, e.Message, e));
				outlineContext = null;
			}

			if (outlineContext != null) {
				IWidget outlineRect;
				try {
					outlineRect = widgetFactory.MakeWidget(typeof(Div), outlineContext, source, out SharpParsingException[] outlineErrors);
					errors.AddRange(outlineErrors.Select(e => e.AtLocation(subject.Location)));
				}
				catch (UndefinedVariableException e) {
					SharpParsingException variableError = new SharpParsingException(subject.Location, e.Message, e);
					errors.Add(variableError);
					outlineRect = new ErrorWidget($"Error parsing {typeName} rect", variableError, WidgetSetup.ErrorSetup);
				}
				catch (KeyNotFoundException e) {
					SharpParsingException keyError = new SharpParsingException(subject.Location, e.Message, e);
					errors.Add(keyError);
					outlineRect = new ErrorWidget($"Error parsing {typeName} rect", keyError, WidgetSetup.ErrorSetup);
				}

				result = outlineRect;
				return true;
			}

			result = null;
			return false;
		}

		private static IFixedCardSegmentRect[] OrderRects(IFixedCardSegmentRect[] rects) {
			List<Position> positions = new List<Position>();

			int staticCount = 0;
			foreach (IFixedCardSegmentRect rect in rects) {
				if (rect.Config?.atPosition is int[] position) {
					positions.Add(new Position(position, false));
				}
				else if (rect.Config is DynamicCardSegmentConfig dynamic && dynamic.AlwaysInclude) {
					positions.Add(new Position(new int[] { 0 }, false));
				}
				else {
					positions.Add(new Position(new int[] { staticCount }, true));
					staticCount++;
				}
			}

			return rects.Zip(positions).OrderBy(rp => rp.Second, new PositionComparer(staticCount)).Select(rp => rp.First).ToArray();
		}

		private readonly struct Position {
			public readonly int[] Index;
			public readonly bool Fallback;

			public int Length => Index.Length;

			public Position(int[] index, bool fallback) {
				Index = index;
				Fallback = fallback;
			}
		}

		private class PositionComparer : IComparer<Position> {

			public int Length { get; }

			public PositionComparer(int length) {
				Length = length;
			}

			public int Compare(Position a, Position b) {
				int minLength = Math.Min(a.Length, b.Length);

				for (int i = 0; i < minLength; i++) {
					int ai = a.Index[i], bi = b.Index[i];

					if (i == 0) {
						if (ai < 0) {
							ai = (Length + ai).Clamp(0, Length - 1);
						}
						if (bi < 0) {
							bi = (Length + bi).Clamp(0, Length - 1);
						}
					}

					if (ai != bi) {
						if ((ai < 0 && bi < 0) || (ai >= 0 && bi >= 0)) {
							return ai.CompareTo(bi);
						}
						else {
							return bi.CompareTo(ai);
						}
					}
				}

				if (a.Fallback != b.Fallback) {
					int af = a.Fallback ? 1 : 0, bf = b.Fallback ? 1 : 0;
					if (a.Index[0] < 0) {
						af += Length;
					}
					if (b.Index[0] < 0) {
						bf += Length;
					}
					return af.CompareTo(bf);
				}

				return a.Length.CompareTo(b.Length);
			}

		}

		private static IFixedCardSegmentRect CreateRect(CardSegment segment, List<SharpParsingException> errors, ParseOrigins<IDocumentEntity>? origins, ParseOrigins<ICardConfigComponent>? configOrigins, WidgetFactory widgetFactory) {
			AbstractCardSegmentConfig segmentConfig = segment.SegmentConfig;

			IFixedCardSegmentRect rect;
			try {
				if (segmentConfig is TextCardSegmentConfig textSegment) {
					rect = CreateTextRect(segment, textSegment, segment.Subject.CardConfig, errors, origins, configOrigins, widgetFactory);
				}
				else if (segmentConfig is ParagraphCardSegmentConfig paragraphSegment) {
					rect = CreateParagraphRect(segment, paragraphSegment, segment.Subject.CardConfig, errors, origins, configOrigins, widgetFactory);
				}
				else if (segmentConfig is TableCardSegmentConfig tableSegment) {
					rect = CreateTableRect(segment, tableSegment, segment.Subject.CardConfig, errors, origins, configOrigins, widgetFactory);
				}
				else if (segmentConfig is DynamicCardSegmentConfig dynamicSegment) {
					rect = CreateDynamicRect(segment, dynamicSegment, segment.Subject.CardConfig, errors, origins, configOrigins, widgetFactory);
				}
				else {
					throw new SharpParsingException(segment.Location, "Unknown segment type.");
				}
			}
			catch (SharpParsingException e) {
				rect = new CardErrorSegmentRect(e.Message);
				if (origins != null) { origins.Add(rect, segment); }
				if (configOrigins != null) { configOrigins.Add(rect, segment.SegmentConfig); }
				errors.Add(e);
			}

			return rect;
		}

		private static DynamicSegmentRect CreateDynamicRect(CardSegment segment, DynamicCardSegmentConfig segmentConfig, CardConfig cardConfig, List<SharpParsingException> errors, ParseOrigins<IDocumentEntity>? origins, ParseOrigins<ICardConfigComponent>? configOrigins, WidgetFactory widgetFactory) {

			List<WidgetCardRect> entries = new List<WidgetCardRect>();
			foreach (CardFeature feature in segment) {
				CardFeatureConfig? featureConfig = feature.FeatureConfig;
				if (featureConfig != null) {
					IEnvironment featureEnvironment = BasisEnvironment.Instance.AppendEnvironment(feature.Environment);
					IContext featureContext = featureConfig.Layout.Evaluate(featureEnvironment, out SharpParsingException[] featureContextErrors);
					errors.AddRange(featureContextErrors);
					IWidget featureContent = widgetFactory.MakeWidget(typeof(Div), featureContext, segmentConfig.parent.Source, out SharpParsingException[] featureErrors);
					errors.AddRange(featureErrors.Select(e => e.AtLocation(feature.Location)));

					Div featureDiv = new Div(new WidgetSetup(_size: Dimension.Automatic));
					featureDiv.AddChild(featureContent);
					WidgetCardRect featureRect = new WidgetCardRect(featureDiv);
					
					//WidgetCardRect featureRect = new WidgetCardRect((SharpWidget)featureContent);
					
					entries.Add(featureRect);

					if (origins != null) { origins.Add(featureRect, feature); }
					if (configOrigins != null) { configOrigins.Add(featureRect, featureConfig); }
				}
				else {
					errors.Add(new SharpParsingException(feature.Location, "No feature configuration found for dynamic segment feature."));
				}
			}

			ArrangementCollection<IWidget> segmentOutlines = GetOutlines(segment.Environment, segment.Location, segmentConfig, cardConfig, errors, widgetFactory);

			DynamicSegmentRect rect = new DynamicSegmentRect(segmentConfig, segmentOutlines, entries.ToArray(), segmentConfig.splittable);

			if (origins != null) { origins.Add(rect, segment); }
			if (configOrigins != null) { configOrigins.Add(rect, segmentConfig); }
			return rect;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private static CardTextSegmentRect CreateTextRect(CardSegment segment, TextCardSegmentConfig segmentConfig, CardConfig cardConfig, List<SharpParsingException> errors, ParseOrigins<IDocumentEntity>? origins, ParseOrigins<ICardConfigComponent>? configOrigins, WidgetFactory widgetFactory) {

			RichString delimiter = EvaluateText(segmentConfig.delimiter, segment.Environment, segment.Location, segmentConfig.regexFormats);
			RichString prefix = EvaluateText(segmentConfig.prefix, segment.Environment, segment.Location, segmentConfig.regexFormats);
			RichString tail = EvaluateText(segmentConfig.tail, segment.Environment, segment.Location, segmentConfig.regexFormats);
			RichString[] featureTexts = segment.Select(f => EvaluateText(segmentConfig.content, f.Environment, f.Location, segmentConfig.regexFormats)).ToArray();
			RichString allText = prefix + RichString.Join(delimiter, featureTexts) + tail;

			RichParagraphs segmentText = new RichParagraphs(allText.Split('\n'));

			ArrangementCollection<IWidget> segmentOutlines = GetOutlines(segment.Environment, segment.Location, segmentConfig, cardConfig, errors, widgetFactory);

			CardTextSegmentRect rect = new CardTextSegmentRect(segmentConfig, segmentOutlines, segmentText, segmentConfig.splittable);

			if (origins != null) { origins.Add(rect, segment); }
			if (configOrigins != null) { configOrigins.Add(rect, segmentConfig); }
			return rect;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private static CardParagraphSegmentRect CreateParagraphRect(CardSegment segment, ParagraphCardSegmentConfig segmentConfig, CardConfig cardConfig, List<SharpParsingException> errors, ParseOrigins<IDocumentEntity>? origins, ParseOrigins<ICardConfigComponent>? configOrigins, WidgetFactory widgetFactory) {

			CardFeatureText[] featuresTexts = segment.Select(f => GetCardFeatureText(segment, segmentConfig, f)).ToArray();
			if (origins != null) {
				foreach(CardFeatureText featureText in featuresTexts) {
					origins.Add(featureText.Feature, featureText.Feature);
				}
			}

			ArrangementCollection<IWidget> segmentOutlines = GetOutlines(segment.Environment, segment.Location, segmentConfig, cardConfig, errors, widgetFactory);

			CardParagraphSegmentRect rect = new CardParagraphSegmentRect(segmentConfig, segmentOutlines, featuresTexts, segmentConfig.splittable);

			if (origins != null) { origins.Add(rect, segment); }
			if (configOrigins != null) { configOrigins.Add(rect, segmentConfig); }
			return rect;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private static IFixedCardSegmentRect CreateTableRect(CardSegment segment, TableCardSegmentConfig segmentConfig, CardConfig cardConfig, List<SharpParsingException> errors, ParseOrigins<IDocumentEntity>? origins, ParseOrigins<ICardConfigComponent>? configOrigins, WidgetFactory widgetFactory) {
			IFixedCardSegmentRect rect;

			// TODO This logic needs correcting. Won't work properly with string escaping.
			RichString[][] tableEntries = segment.Enumerate().Select(f => DelimitedUtils.SplitDelimitedString(f.Item.Text.Value.Evaluate(f.Item.Environment), CardConfigConstants.TableDelimiter, true).Select(s => (f.Index == 0) ? new RichString(s, TextFormat.BOLD) : new RichString(s)).ToArray()).ToArray();
			
			if (tableEntries.Select(r => r.Length).Distinct().Count() != 1) {
				throw new SharpParsingException(segment.Location, "Table must have same number of columns in each row.");
			}

			if (origins != null) {
				foreach (CardFeature feature in segment) {
					origins.Add(feature, feature);
				}
			}

			RichString[,] tableEntriesMatrix = GetTableEntries(tableEntries);

			ArrangementCollection<IWidget> segmentOutlines = GetOutlines(segment.Environment, segment.Location, segmentConfig, cardConfig, errors, widgetFactory);

			rect = new NewCardTableSegmentRect(segmentConfig, segmentOutlines, tableEntriesMatrix, segment.ToArray<CardFeature>(), segmentConfig.splittable);

			if (origins != null) { origins.Add(rect, segment); }
			if (configOrigins != null) { configOrigins.Add(rect, segmentConfig); }
			return rect;
		}

		private static RichString[,] GetTableEntries(RichString[][] values) {
			// TODO Where exactly does this method belong? In CardTableSegmentRect?

			int rows = values.Length;
			int columns = values.Select(i => i.Length).MaxOrFallback(0);

			RichString[,] result = new RichString[rows, columns];
			for (int i = 0; i < rows; ++i) {
				for (int j = 0; j < values[i].Length; ++j) {
					result[i, j] = values[i][j];
				}
				for (int j = values[i].Length; j < columns; ++j) {
					result[i, j] = RichString.Empty;
				}
			}
			return result;
		}

		/// <summary>
		/// Produces a segment rect based on an "always at" <see cref="DynamicCardSegmentConfig"/>.
		/// </summary>
		/// <param name="segmentConfig"></param>
		/// <param name="cardConfig"></param>
		/// <param name="subject"></param>
		/// <param name="errors"></param>
		/// <param name="origins"></param>
		/// <param name="configOrigins"></param>
		/// <param name="widgetFactory"></param>
		/// <returns></returns>
		private static IFixedCardSegmentRect CreateDefaultRect(DynamicCardSegmentConfig segmentConfig, CardConfig cardConfig, CardSubject subject, List<SharpParsingException> errors, ParseOrigins<IDocumentEntity>? origins, ParseOrigins<ICardConfigComponent>? configOrigins, WidgetFactory widgetFactory) {

			List<WidgetCardRect> entries = new List<WidgetCardRect>();

			CardFeatureConfig? featureConfig;
			try {
				featureConfig = segmentConfig.cardFeatures.GetValue(subject.Environment);
			}
			catch (EvaluationException e) {
				errors.Add(new SharpParsingException(subject.Location, e.Message, e));
				featureConfig = null;
			}

			if (featureConfig != null) {
				IEnvironment featureEnvironment = BasisEnvironment.Instance.AppendEnvironment(subject.Environment);
				IContext featureContext = featureConfig.Layout.Evaluate(featureEnvironment, out SharpParsingException[] featureContextErrors);
				errors.AddRange(featureContextErrors);
				IWidget featureContent = widgetFactory.MakeWidget(typeof(Div), featureContext, segmentConfig.parent.Source, out SharpParsingException[] featureErrors);
				errors.AddRange(featureErrors.Select(e => e.AtLocation(subject.Location)));

				Div featureDiv = new Div(new WidgetSetup(_size: Dimension.Automatic));
				featureDiv.AddChild(featureContent);
				WidgetCardRect featureRect = new WidgetCardRect(featureDiv);
				entries.Add(featureRect);

				if (origins != null) { origins.Add(featureRect, subject); }
				if (configOrigins != null) { configOrigins.Add(featureRect, featureConfig); }
			}

			ArrangementCollection<IWidget> segmentOutlines = GetOutlines(subject.Environment, subject.Location, segmentConfig, cardConfig, errors, widgetFactory);

			DynamicSegmentRect rect = new DynamicSegmentRect(segmentConfig, segmentOutlines, entries.ToArray(), segmentConfig.splittable);
			if (origins != null) { origins.Add(rect, subject); }
			if (configOrigins != null) { configOrigins.Add(rect, segmentConfig); }
			return rect;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private static CardFeatureText GetCardFeatureText(CardSegment segment, ParagraphCardSegmentConfig segmentConfig, CardFeature feature) {
			IEnvironment textEnvironment = Environments.Concat(segment.Environment, feature.Environment);
			RichString text = EvaluateText(segmentConfig.content, textEnvironment, feature.Location, segmentConfig.regexFormats);
			RichParagraph paragraph = new RichParagraph(text);
			return new CardFeatureText(feature, paragraph);
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private static RichString EvaluateText(IExpression<string> expression, IEnvironment environment, DocumentSpan location, RegexFormats formatting) {
			try {
				string text = expression.Evaluate(environment);
				RichString richText = StringParsing.ParseRich(text); // new RichString(text);

				if (formatting != null) {
					richText = formatting.Apply(richText);
				}

				return richText;
			}
			catch (EvaluationException e) {
				throw new SharpParsingException(location, e.Message, e);
			}
			catch (FormatException e) {
				throw new SharpParsingException(location, e.Message, e);
			}
		}

		private static ArrangementCollection<IWidget> GetOutlines(IEnvironment environment, DocumentSpan location, AbstractCardSegmentConfig segmentConfig, CardConfig cardConfig, List<SharpParsingException> errors, WidgetFactory widgetFactory) {
			ArrangementCollection<IWidget> outlineCollection = new ArrangementCollection<IWidget>((int)cardConfig.MaxCards, new Div(new WidgetSetup()));

			// Only the part number/total can vary, so just enumerate all possibilities and store them
			for (int partsCount = 1; partsCount <= cardConfig.MaxCards; partsCount++) {
				for (int part = 0; part < partsCount; part++) {
					IEnvironment segmentOutlinesEnvironment = Environments.Concat(
						BasisEnvironment.Instance,
						environment,
						CardSegmentOutlineEnvironments.GetEnvironment(part, partsCount));

					IContext? outline;
					try {
						InterpolatedContext? outlineExp = segmentConfig.outlines.GetValue(segmentOutlinesEnvironment);
						if(outlineExp != null) {
							outline = outlineExp.Evaluate(segmentOutlinesEnvironment, out SharpParsingException[] contextErrors);
							errors.AddRange(contextErrors);
						}
						else {
							outline = null;
						}
					}
					catch(EvaluationException e) {
						errors.Add(new SharpParsingException(location, e.Message, e));
						outline = null;
					}

					if (outline != null) {
						IWidget outlineRect;
						try {
							outlineRect = widgetFactory.MakeWidget(typeof(Div), outline, segmentConfig.parent.Source, out SharpParsingException[] outlineErrors);
							errors.AddRange(outlineErrors.Select(e => e.AtLocation(location)));
						}
						catch (UndefinedVariableException e) {
							SharpParsingException varError = new SharpParsingException(location, e.Message, e);
							errors.Add(varError);
							outlineRect = new ErrorWidget("Error parsing outline rect", varError, WidgetSetup.ErrorSetup);
						}
						catch (KeyNotFoundException e) {
							SharpParsingException keyError = new SharpParsingException(location, e.Message, e);
							errors.Add(keyError);
							outlineRect = new ErrorWidget("Error parsing outline rect", keyError, WidgetSetup.ErrorSetup);
						}

						outlineCollection[part, partsCount] = outlineRect;
					}

				}
			}

			return outlineCollection;
		}
	}

}