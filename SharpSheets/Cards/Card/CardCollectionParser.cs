using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Exceptions;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Cards.Card {

	public class CardCollectionParser : IParser<CardCollection> {

		private readonly CardSubjectParser subjectParser;
		private readonly ShapeFactory shapeFactory;
		private readonly WidgetFactory widgetFactory;

		public CardCollectionParser(CardSubjectParser subjectParser, WidgetFactory widgetFactory, ShapeFactory shapeFactory) {
			this.subjectParser = subjectParser;
			this.shapeFactory = shapeFactory;
			this.widgetFactory = widgetFactory;
		}

		public CardCollection Parse(CardSubjectDocument subjectsDocument, out SharpParsingException[] dynamicCardErrors, out ParseOrigins<IDocumentEntity> origins) {
			List<SharpParsingException> errors = new List<SharpParsingException>();
			origins = new ParseOrigins<IDocumentEntity>();

			List<List<DynamicCard>> cards = new List<List<DynamicCard>>();
			foreach (CardSubjectSet subjectSet in subjectsDocument) {
				List<DynamicCard> cardSet = new List<DynamicCard>();
				foreach (CardSubject subject in subjectSet) {
					DynamicCard card = DynamicCardFactory.CreateCard(subject, origins, null, widgetFactory, out List<SharpParsingException> cardErrors);
					cardSet.Add(card);
					errors.AddRange(cardErrors);
				}
				cards.Add(cardSet);
			}

			dynamicCardErrors = errors.ToArray();

			return new CardCollection(cards.Select(cs => cs.ToArray()).ToArray());
		}

		public CardCollection Parse(DirectoryPath source, string description, out CompilationResult results) {
			CardSubjectDocument subjectsDocument = subjectParser.Parse(source, description, out CompilationResult subjectResults);

			CardCollection result = Parse(subjectsDocument, out SharpParsingException[] errors, out ParseOrigins<IDocumentEntity> origins);

			results = new CompilationResult(
				subjectResults.rootEntity,
				origins,
				subjectResults.errors.Concat(errors).ToList(),
				subjectResults.usedLines,
				subjectResults.lineOwners,
				//subjectResults.parents,
				subjectResults.dependencies
			);

			return result;
		}

		public static CardCollection MakeCollection(IEnumerable<CardSubject> subjects, WidgetFactory widgetFactory, ParseOrigins<IDocumentEntity>? origins, ParseOrigins<ICardConfigComponent>? configOrigins, out SharpParsingException[] errors) {
			List<SharpParsingException> dynamicCardErrors = new List<SharpParsingException>();
			List<DynamicCard> cardSet = new List<DynamicCard>();
			foreach (CardSubject subject in subjects) {
				DynamicCard card = DynamicCardFactory.CreateCard(subject, origins, configOrigins, widgetFactory, out List<SharpParsingException> cardErrors);
				cardSet.Add(card);
				dynamicCardErrors.AddRange(cardErrors);
			}
			errors = dynamicCardErrors.ToArray();
			return new CardCollection(cardSet.ToArray().Yield().ToArray());
		}

		public CardCollection ParseContent(FilePath origin, DirectoryPath source, string description, out CompilationResult results) {
			return Parse(source, description, out results);
		}
	}

}