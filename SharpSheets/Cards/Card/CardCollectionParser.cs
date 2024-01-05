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
			//subjectParser = new CardSubjectParser(defaultConfigsPath, shapeFactory);
			this.subjectParser = subjectParser;
			this.shapeFactory = shapeFactory;
			this.widgetFactory = widgetFactory;
		}

		public CardCollection Parse(DirectoryPath source, string description, out CompilationResult results) {
			CardSubjectDocument subjectsDocument = subjectParser.Parse(source, description, out CompilationResult subjectResults);
			//DynamicCard[][] cards = subjects.SelectFrom(s => DynamicCardFactory.CreateCard(s, out _));

			List<SharpParsingException> errors = new List<SharpParsingException>();
			Dictionary<object, IDocumentEntity> origins = new Dictionary<object, IDocumentEntity>();

			List<List<DynamicCard>> cards = new List<List<DynamicCard>>();
			foreach(CardSubjectSet subjectSet in subjectsDocument) {
				List<DynamicCard> cardSet = new List<DynamicCard>();
				foreach(CardSubject subject in subjectSet) {
					DynamicCard card = DynamicCardFactory.CreateCard(subject, origins, widgetFactory, out List<SharpParsingException> cardErrors);
					cardSet.Add(card);
					errors.AddRange(cardErrors);
				}
				cards.Add(cardSet);
			}

			results = new CompilationResult(
				subjectResults.rootEntity,
				origins,
				subjectResults.errors.Concat(errors).ToList(),
				subjectResults.usedLines,
				subjectResults.lineOwners,
				subjectResults.lineChildren,
				subjectResults.parents,
				subjectResults.dependencies
			);

			return new CardCollection(cards.Select(cs => cs.ToArray()).ToArray());
		}

		public static CardCollection MakeCollection(IEnumerable<CardSubject> subjects, WidgetFactory widgetFactory, Dictionary<object, IDocumentEntity>? origins, out SharpParsingException[] errors) {
			List<SharpParsingException> dynamicCardErrors = new List<SharpParsingException>();
			List<DynamicCard> cardSet = new List<DynamicCard>();
			foreach (CardSubject subject in subjects) {
				DynamicCard card = DynamicCardFactory.CreateCard(subject, origins, widgetFactory, out List<SharpParsingException> cardErrors);
				cardSet.Add(card);
				dynamicCardErrors.AddRange(cardErrors);
			}
			errors = dynamicCardErrors.ToArray();
			return new CardCollection(cardSet.ToArray().Yield().ToArray());
		}

		public CardCollection ParseContent(FilePath origin, DirectoryPath source, string description, out CompilationResult results) {
			return Parse(source, description, out results);
		}

		public object Parse(FilePath origin, DirectoryPath source, string description, out CompilationResult results) {
			return Parse(source, description, out results);
		}
	}

}