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
using SharpSheets.Cards.Card.SectionRects;

namespace SharpSheets.Cards.Card
{

    public static class DynamicCardFactory {

		public static DynamicCard CreateCard(CardSubject subject, Dictionary<object, IDocumentEntity>? origins, WidgetFactory widgetFactory, out List<SharpParsingException> errors) {
			CardSetConfig cardSetConfig = subject.CardConfig.cardSetConfig;
			CardConfig cardConfig = subject.CardConfig;

			errors = new List<SharpParsingException>();

			(ArrangementCollection<IWidget> headerCollection, ArrangementCollection<IWidget> outlineCollection) = GetCardOutlineCollections(subject, widgetFactory, errors);

			List<IFixedCardSectionRect> sectionRects = new List<IFixedCardSectionRect>();
			foreach (CardSection section in subject) {
				sectionRects.Add(CreateRect(section, errors, origins, widgetFactory));
			}

			IEnumerable<DynamicCardSectionConfig> alwaysIncluded = cardConfig
				.cardSections.Concat(cardConfig.cardSetConfig.cardSections)
				.Where(c => c.Value is DynamicCardSectionConfig d && d.AlwaysInclude)
				.Where(c => c.Condition.Evaluate(subject.Environment))
				.Select(c => (DynamicCardSectionConfig)c.Value); // .Reverse();
			bool hasAlwaysIncluded = false;
			foreach (DynamicCardSectionConfig sectionConfig in alwaysIncluded) {
				IFixedCardSectionRect section = CreateDefaultRect(sectionConfig, subject.CardConfig, subject, errors, origins, widgetFactory);
				sectionRects.Add(section);
				hasAlwaysIncluded = true;
			}

			IFixedCardSectionRect[] orderedSections = sectionRects.ToArray();
			if (hasAlwaysIncluded || orderedSections.Any(s => s.Config?.atPosition != null)) {
				orderedSections = OrderRects(orderedSections);
			}

			DynamicCard dynamicCard = new DynamicCard(subject, cardConfig.gutter, cardConfig.joinSplitCards, cardConfig.cropOnFinalCard, outlineCollection, cardConfig.gutterStyle, headerCollection, orderedSections);
			if (origins != null) { origins.Add(dynamicCard, subject); }
			return dynamicCard;
		}

		private static (ArrangementCollection<IWidget> headers, ArrangementCollection<IWidget> outlines) GetCardOutlineCollections(CardSubject subject, WidgetFactory widgetFactory, List<SharpParsingException> errors) {
			CardSetConfig cardSetConfig = subject.CardConfig.cardSetConfig;
			CardConfig cardConfig = subject.CardConfig;

			ArrangementCollection<IWidget> headerCollection = new ArrangementCollection<IWidget>(subject.CardConfig.MaxCards, new Div(new WidgetSetup()));
			ArrangementCollection<IWidget> outlineCollection = new ArrangementCollection<IWidget>(subject.CardConfig.MaxCards, new Div(new WidgetSetup()));

			// Only the card number/total can vary, so just enumerate all possibilities and store them
			for (int cardCount = 1; cardCount <= subject.CardConfig.MaxCards; cardCount++) {
				for (int card = 0; card < cardCount; card++) {
					IEnvironment cardOutlinesEnvironment = subject.Environment.AppendDefinitionEnvironment(CardOutlinesEnvironments.GetEnvironment(card, cardCount)); // DynamicCardEnvironments.CardNumberEnvironment(subject.Environment, card, totalCards);

					if(ProcessCardArrangementOutline(subject, cardConfig.headers, cardConfig.cardSetConfig.headers, cardOutlinesEnvironment, cardSetConfig.Source, widgetFactory, errors, "header", out IWidget? headerRect)) {
						headerCollection[card, cardCount] = headerRect;
					}

					if (ProcessCardArrangementOutline(subject, cardConfig.outlines, cardConfig.cardSetConfig.outlines, cardOutlinesEnvironment, cardSetConfig.Source, widgetFactory, errors, "outline", out IWidget? outlineRect)) {
						outlineCollection[card, cardCount] = outlineRect;
					}
				}
			}

			return (headerCollection, outlineCollection);
		}

		private static bool ProcessCardArrangementOutline(CardSubject subject, ConditionalCollection<IContext> primary, ConditionalCollection<IContext> fallback, IEnvironment environment, DirectoryPath source, WidgetFactory widgetFactory, List<SharpParsingException> errors, string typeName, [MaybeNullWhen(false)] out IWidget result) {
			IContext? outlineContext;
			try {
				outlineContext = primary.GetValue(environment);
				if (outlineContext == null) {
					outlineContext = fallback.GetValue(environment);
				}
			}
			catch (EvaluationException e) {
				errors.Add(new SharpParsingException(subject.Location, e.Message, e));
				outlineContext = null;
			}

			if (outlineContext != null) {
				IWidget outlineRect;
				try {
					outlineRect = widgetFactory.MakeWidget(typeof(Div), new InterpolateContext(outlineContext, environment, true), source, out SharpParsingException[] outlineErrors);
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

		private static IFixedCardSectionRect[] OrderRects(IFixedCardSectionRect[] rects) {
			List<Position> positions = new List<Position>();

			int staticCount = 0;
			foreach (IFixedCardSectionRect rect in rects) {
				if (rect.Config?.atPosition is int[] position) {
					positions.Add(new Position(position, false));
				}
				else if (rect.Config is DynamicCardSectionConfig dynamic && dynamic.AlwaysInclude) {
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

		private static IFixedCardSectionRect CreateRect(CardSection section, List<SharpParsingException> errors, Dictionary<object, IDocumentEntity>? origins, WidgetFactory widgetFactory) {
			AbstractCardSectionConfig sectionConfig = section.SectionConfig;

			IFixedCardSectionRect rect;
			try {
				if (sectionConfig is TextCardSectionConfig textSection) {
					rect = CreateTextRect(section, textSection, section.Subject.CardConfig, errors, origins, widgetFactory);
				}
				else if (sectionConfig is ParagraphCardSectionConfig paragraphSection) {
					rect = CreateParagraphRect(section, paragraphSection, section.Subject.CardConfig, errors, origins, widgetFactory);
				}
				else if (sectionConfig is TableCardSectionConfig tableSection) {
					rect = CreateTableRect(section, tableSection, section.Subject.CardConfig, errors, origins, widgetFactory);
				}
				else if (sectionConfig is DynamicCardSectionConfig dynamicSection) {
					rect = CreateDynamicRect(section, dynamicSection, section.Subject.CardConfig, errors, origins, widgetFactory);
				}
				else {
					throw new SharpParsingException(section.Location, "Unknown section type.");
				}
			}
			catch (SharpParsingException e) {
				rect = new CardErrorSectionRect(e.Message);
				if (origins != null) { origins.Add(rect, section); }
				errors.Add(e);
			}

			return rect;
		}

		private static DynamicSectionRect CreateDynamicRect(CardSection section, DynamicCardSectionConfig sectionConfig, CardConfig cardConfig, List<SharpParsingException> errors, Dictionary<object, IDocumentEntity>? origins, WidgetFactory widgetFactory) {

			ArrangementCollection<IWidget> sectionOutlines = GetOutlines(section.Environment, section.Location, sectionConfig, cardConfig, errors, widgetFactory);

			List<WidgetCardRect> entries = new List<WidgetCardRect>();
			foreach (CardFeature feature in section) {
				CardFeatureConfig? featureConfig = feature.FeatureConfig;
				if (featureConfig != null) {
					IWidget featureContent = widgetFactory.MakeWidget(typeof(Div), new InterpolateContext(featureConfig.layout, feature.Environment, true), sectionConfig.parent.Source, out SharpParsingException[] featureErrors);
					errors.AddRange(featureErrors.Select(e => e.AtLocation(feature.Location)));

					Div featureDiv = new Div(new WidgetSetup(_size: Dimension.Automatic));
					featureDiv.AddChild(featureContent);
					WidgetCardRect featureRect = new WidgetCardRect(featureDiv);
					
					//WidgetCardRect featureRect = new WidgetCardRect((SharpWidget)featureContent);
					
					entries.Add(featureRect);

					if (origins != null) { origins.Add(featureRect, feature); }
				}
				else {
					errors.Add(new SharpParsingException(feature.Location, "No feature configuration found for dynamic section feature."));
				}
			}

			DynamicSectionRect rect = new DynamicSectionRect(sectionConfig, sectionOutlines, entries.ToArray(), sectionConfig.gutter, sectionConfig.splittable, sectionConfig.acceptRemaining, sectionConfig.equalSizeFeatures, sectionConfig.spaceFeatures);

			if (origins != null) { origins.Add(rect, section); }
			return rect;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private static CardTextSectionRect CreateTextRect(CardSection section, TextCardSectionConfig sectionConfig, CardConfig cardConfig, List<SharpParsingException> errors, Dictionary<object, IDocumentEntity>? origins, WidgetFactory widgetFactory) {

			ArrangementCollection<IWidget> sectionOutlines = GetOutlines(section.Environment, section.Location, sectionConfig, cardConfig, errors, widgetFactory);

			RichString delimiter = EvaluateText(sectionConfig.delimiter, section.Environment, section.Location, sectionConfig.regexFormats);
			RichString prefix = EvaluateText(sectionConfig.prefix, section.Environment, section.Location, sectionConfig.regexFormats);
			RichString tail = EvaluateText(sectionConfig.tail, section.Environment, section.Location, sectionConfig.regexFormats);
			RichString[] featureTexts = section.Select(f => EvaluateText(sectionConfig.content, f.Environment, f.Location, sectionConfig.regexFormats)).ToArray();
			RichString allText = prefix + RichString.Join(delimiter, featureTexts) + tail;

			RichParagraphs sectionText = new RichParagraphs(allText.Split('\n'));
			CardTextSectionRect rect = new CardTextSectionRect(sectionConfig, sectionOutlines, sectionText, sectionConfig.splittable);

			if (origins != null) { origins.Add(rect, section); }
			return rect;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private static CardParagraphSectionRect CreateParagraphRect(CardSection section, ParagraphCardSectionConfig sectionConfig, CardConfig cardConfig, List<SharpParsingException> errors, Dictionary<object, IDocumentEntity>? origins, WidgetFactory widgetFactory) {
			ArrangementCollection<IWidget> sectionOutlines = GetOutlines(section.Environment, section.Location, sectionConfig, cardConfig, errors, widgetFactory);

			//RichString[] featureTexts = section.Select(f => EvaluateText(sectionConfig.content, f.Environment, f.Location, sectionConfig.regexFormats)).ToArray();
			//RichParagraphs sectionText = new RichParagraphs(featureTexts); // TODO What happens if a feature text contains a newline character?

			CardFeatureText[] featuresTexts = section.Select(f => GetCardFeatureText(sectionConfig, f)).ToArray();
			if (origins != null) {
				foreach(CardFeatureText featureText in featuresTexts) {
					origins.Add(featureText.Feature, featureText.Feature);
				}
			}

			CardParagraphSectionRect rect = new CardParagraphSectionRect(sectionConfig, sectionOutlines, featuresTexts, sectionConfig.splittable);

			if (origins != null) { origins.Add(rect, section); }
			return rect;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private static IFixedCardSectionRect CreateTableRect(CardSection section, TableCardSectionConfig sectionConfig, CardConfig cardConfig, List<SharpParsingException> errors, Dictionary<object, IDocumentEntity>? origins, WidgetFactory widgetFactory) {

			ArrangementCollection<IWidget> sectionOutlines = GetOutlines(section.Environment, section.Location, sectionConfig, cardConfig, errors, widgetFactory);

			IFixedCardSectionRect rect;

			// TODO This logic needs correcting. Won't work properly with string escaping.
			RichString[][] tableEntries = section.Enumerate().Select(f => DelimitedUtils.SplitDelimitedString(f.Item.Text.Value.Evaluate(f.Item.Environment), ',', true).Select(s => (f.Index == 0) ? new RichString(s, TextFormat.BOLD) : new RichString(s)).ToArray()).ToArray();
			
			if (tableEntries.Select(r => r.Length).Distinct().Count() != 1) {
				throw new SharpParsingException(section.Location, "Table must have same number of columns in each row.");
			}

			RichString[,] tableEntriesMatrix = GetTableEntries(tableEntries);

			RichString? tableTitle = !string.IsNullOrWhiteSpace(section.Heading.Value) ? new RichString(section.Heading.Value) : null;
			rect = new SimpleCardTableSectionRect(sectionConfig, sectionOutlines, tableTitle, tableEntriesMatrix, sectionConfig.tableSpacing, sectionConfig.edgeOffset, sectionConfig.tableColors, sectionConfig.splittable, sectionConfig.acceptRemaining);

			if (origins != null) { origins.Add(rect, section); }
			return rect;
		}

		private static RichString[,] GetTableEntries(RichString[][] values) {
			// TODO Where exactly does this method belong? In CardTableSectionRect?

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
		/// Produces a section based on an "always at" <see cref="DynamicCardSectionConfig"/>.
		/// </summary>
		/// <param name="sectionConfig"></param>
		/// <param name="cardConfig"></param>
		/// <param name="subject"></param>
		/// <param name="errors"></param>
		/// <param name="origins"></param>
		/// <param name="widgetFactory"></param>
		/// <returns></returns>
		private static IFixedCardSectionRect CreateDefaultRect(DynamicCardSectionConfig sectionConfig, CardConfig cardConfig, CardSubject subject, List<SharpParsingException> errors, Dictionary<object, IDocumentEntity>? origins, WidgetFactory widgetFactory) {

			ArrangementCollection<IWidget> sectionOutlines = GetOutlines(subject.Environment, subject.Location, sectionConfig, cardConfig, errors, widgetFactory);

			List<WidgetCardRect> entries = new List<WidgetCardRect>();

			CardFeatureConfig? featureConfig;
			try {
				featureConfig = sectionConfig.cardFeatures.GetValue(subject.Environment);
			}
			catch (EvaluationException e) {
				errors.Add(new SharpParsingException(subject.Location, e.Message, e));
				featureConfig = null;
			}

			if (featureConfig != null) {
				IWidget featureContent = widgetFactory.MakeWidget(typeof(Div), new InterpolateContext(featureConfig.layout, subject.Environment, true), sectionConfig.parent.Source, out SharpParsingException[] featureErrors);
				errors.AddRange(featureErrors.Select(e => e.AtLocation(subject.Location)));

				Div featureDiv = new Div(new WidgetSetup(_size: Dimension.Automatic));
				featureDiv.AddChild(featureContent);
				WidgetCardRect featureRect = new WidgetCardRect(featureDiv);
				entries.Add(featureRect);

				if (origins != null) { origins.Add(featureRect, subject); }
			}

			DynamicSectionRect rect = new DynamicSectionRect(sectionConfig, sectionOutlines, entries.ToArray(), sectionConfig.gutter, sectionConfig.splittable, sectionConfig.acceptRemaining, sectionConfig.equalSizeFeatures, sectionConfig.spaceFeatures);
			if (origins != null) { origins.Add(rect, subject); }
			return rect;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private static CardFeatureText GetCardFeatureText(ParagraphCardSectionConfig sectionConfig, CardFeature feature) {
			RichString text = EvaluateText(sectionConfig.content, feature.Environment, feature.Location, sectionConfig.regexFormats);
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

		private static ArrangementCollection<IWidget> GetOutlines(IEnvironment environment, DocumentSpan location, AbstractCardSectionConfig sectionConfig, CardConfig cardConfig, List<SharpParsingException> errors, WidgetFactory widgetFactory) {
			ArrangementCollection<IWidget> outlineCollection = new ArrangementCollection<IWidget>(cardConfig.MaxCards, new Div(new WidgetSetup()));

			// Only the part number/total can vary, so just enumerate all possibilities and store them
			for (int partsCount = 1; partsCount <= cardConfig.MaxCards; partsCount++) {
				for (int part = 0; part < partsCount; part++) {
					IEnvironment sectionOutlinesEnvironment = environment.AppendEnvironment(CardSectionOutlineEnvironments.GetEnvironment(part, partsCount));

					IContext? outline;
					try {
						outline = sectionConfig.outlines.GetValue(sectionOutlinesEnvironment);
					}
					catch(EvaluationException e) {
						errors.Add(new SharpParsingException(location, e.Message, e));
						outline = null;
					}

					if (outline != null) {
						IWidget outlineRect;
						try {
							outlineRect = widgetFactory.MakeWidget(typeof(Div), new InterpolateContext(outline, sectionOutlinesEnvironment, true), sectionConfig.parent.Source, out SharpParsingException[] outlineErrors);
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

	public class InterpolateContext : IContext {

		public IContext OriginalContext { get; }
		public IEnvironment Environment { get; }
		public bool PruneChildren { get; }

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		public InterpolateContext(IContext originalContext, IEnvironment environment, bool pruneChildren) {
			this.OriginalContext = originalContext ?? throw new ArgumentNullException(nameof(originalContext));
			this.Environment = environment ?? throw new ArgumentNullException(nameof(environment));
			this.PruneChildren = pruneChildren;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		[return: NotNullIfNotNull(nameof(original))]
		private string? Replace(string? original, DocumentSpan? location) {
			if(original == null) { return null; }
			// Find any variables/expressions ($-prefixed or ${}-wrapped) and replace
			try {
				TextExpression expr = Interpolation.Parse(original, Environment, true);
				string result = expr.Evaluate(Environment); // Environment.Interpolate(original, true);
				return result;
			}
			catch (EvaluationException e) {
				throw new SharpParsingException(location, e.Message, e);
			}
		}

		public string SimpleName => OriginalContext.SimpleName;
		public string DetailedName => OriginalContext.DetailedName;
		public string FullName => OriginalContext.FullName;
		public DocumentSpan Location => OriginalContext.Location;
		public int Depth => OriginalContext.Depth;

		public IContext? Parent { get { return OriginalContext.Parent is not null ? new InterpolateContext(OriginalContext.Parent, Environment, PruneChildren) : null; } } // TODO Is this k'sha?
		public IEnumerable<IContext> Children {
			get {
				IEnumerable<IContext> candidates = PruneChildren ? OriginalContext.Children.Where(EvaluateCondition) : OriginalContext.Children;
				return candidates.Select(c => new InterpolateContext(c, Environment, PruneChildren));
			}
		}
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren {
			get {
				IEnumerable<KeyValuePair<string, IContext>> candidates = PruneChildren ? OriginalContext.NamedChildren.Where(kv => EvaluateCondition(kv.Value)) : OriginalContext.NamedChildren;
				return candidates.Select(kv => new KeyValuePair<string, IContext>(kv.Key, new InterpolateContext(kv.Value, Environment, PruneChildren)));
			}
		}

		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		private bool EvaluateCondition(IContext context) {
			string? conditionStr = context.GetProperty("condition", true, context, null, out DocumentSpan? location);
			if (conditionStr == null) {
				return true;
			}
			else {
				try {
					BoolExpression condition = BoolExpression.Parse(conditionStr, Environment);
					object evaluation = condition.Evaluate(Environment);
					if (evaluation is bool result) { return result; }
				}
				catch (EvaluationException) { }
				throw new SharpParsingException(location, "Invalid condition.");
			}
		}

		public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) {
			return OriginalContext.GetLocalProperties(origin)
				.Select(p => new ContextProperty<string>(p.Location, p.Name, p.ValueLocation, Replace(p.Value, p.ValueLocation)));
		}

		public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) {
			return OriginalContext.GetLocalFlags(origin);
		}

		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) {
			return OriginalContext.GetEntries(origin).Select(e => new ContextValue<string>(e.Location, Replace(e.Value, e.Location)));
		}

		public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) {
			return OriginalContext.GetDefinitions(origin); // TODO Is this right?
		}

		public bool GetFlag(string flag, bool local, IContext? origin, out DocumentSpan? location) {
			return OriginalContext.GetFlag(flag, local, origin, out location);
		}

		public IContext? GetNamedChild(string name) {
			IContext? namedChild = OriginalContext.GetNamedChild(name);
			if(namedChild == null) {
				return null;
			}
			else if (PruneChildren) {
				return EvaluateCondition(namedChild) ? new InterpolateContext(namedChild, Environment, PruneChildren) : null;
			}
			else {
				return new InterpolateContext(namedChild, Environment, PruneChildren);
			}
		}

		public string? GetProperty(string key, bool local, IContext? origin, string? defaultValue, out DocumentSpan? location) {
			string? value = OriginalContext.GetProperty(key, local, origin, defaultValue, out location);
			return Replace(value, location);
		}

		public bool HasFlag(string flag, bool local, IContext? origin, out DocumentSpan? location) {
			return OriginalContext.HasFlag(flag, local, origin, out location);
		}

		public bool HasProperty(string key, bool local, IContext? origin, out DocumentSpan? location) {
			return OriginalContext.HasProperty(key, local, origin, out location);
		}
	}
}