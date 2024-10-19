using System.Collections.Generic;
using System;
using System.Linq;
using SharpSheets.Widgets;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Exceptions;

namespace SharpSheets.Cards.CardConfigs {
	public class CardSetConfigParser : IParser<CardSetConfig> {

		private readonly WidgetFactory widgetFactory;
		private readonly ShapeFactory shapeFactory;
		private readonly CardSetConfigFactory cardSetConfigFactory;

		public CardSetConfigParser(WidgetFactory widgetFactory, ShapeFactory shapeFactory, CardSetConfigFactory cardSetConfigFactory) {
			this.widgetFactory = widgetFactory;
			this.shapeFactory = shapeFactory;
			this.cardSetConfigFactory = cardSetConfigFactory;
		}

		private class CardSetConfigEntryStack : EntryStack {

			private readonly CardSetConfigFactory cardSetConfigFactory;

			public readonly string configName;
			public readonly FilePath origin;

			public readonly List<ContextValue<string>> archives;

			public CardSetConfigEntryStack(FilePath origin, DirectoryPath source, string configName, CardSetConfigFactory cardSetConfigFactory) : base(source, CardConfigConstants.CardConfigName) {
				this.cardSetConfigFactory = cardSetConfigFactory;
				
				this.configName = configName;
				this.origin = origin;

				archives = new List<ContextValue<string>>();
			}

			public void AddDefinition(DocumentSpan location, string definition) {
				Peek().AddDefinition(location, definition);
			}
			public void AddArchive(DocumentSpan location, string archive) {
				archives.Add(new ContextValue<string>(location, archive));
			}

			/// <summary></summary>
			/// <exception cref="InvalidOperationException"></exception>
			public (CardSetConfig?, VisitTrackingContext) GenerateConfig(VisitTrackingContext trackingContext, out ParseOrigins<IDocumentEntity> origins, out List<SharpParsingException> errors) {
				
				CardSetConfig? cardSetConfig = cardSetConfigFactory.MakeSetConfig(configName, trackingContext, archives, origin, source, out origins, out errors);
				return (cardSetConfig, trackingContext);
			}

			public void CollectConfigurationResults(out CardSetConfig? cardSetConfig, out CompilationResult results) {
				ParseOrigins<IDocumentEntity>? origins;
				List<SharpParsingException> buildErrors;
				VisitTrackingContext trackingContext = new VisitTrackingContext(rootEntry);
				//trackingContext.RefreshVisited(); // Should be unnecessary
				try {
					(cardSetConfig, trackingContext) = GenerateConfig(trackingContext, out origins, out buildErrors);
				}
				catch(InvalidOperationException e) {
					buildErrors = new List<SharpParsingException>() {
						new SharpParsingException(null, e.Message, e)
					};
					origins = null;
					cardSetConfig = null;
				}
				results = CompilationResult.CompileResult(trackingContext, origins, parsingExceptions, buildErrors, cardSetConfig?.archivePaths ?? new List<FilePath>(), archives.Select(a => a.Location.Line), LineOwnership.Empty);
			}
		}

		/// <summary></summary>
		/// <exception cref="DirectoryNotFoundException"></exception>
		private CardSetConfigEntryStack ParseConfig(FilePath origin, DirectoryPath source, string config) {

			if (!source.Exists) {
				throw new DirectoryNotFoundException($"Source directory does not exist: \"{source.Path}\"");
			}

			string configName = origin.FileNameWithoutExtension; // Is this the best way of doing this?

			CardSetConfigEntryStack entryStack = new CardSetConfigEntryStack(origin, source, configName, cardSetConfigFactory);

			foreach (SharpDocumentLine line in SharpDocumentLineParsing.SplitLines(config)) {

				if (line.IndentLevel < 0) {
					entryStack.parsingExceptions.Add(new SharpParsingException(line.Location, "Invalid indentation."));
					continue;
				}
				while (entryStack.Count > 0 && line.IndentLevel <= entryStack.CurrentIndentLevel) {
					entryStack.Pop();
				}

				if (line.LineType == LineType.DIV) {
					entryStack.Push(line.Location, line.IndentLevel, line.Content, false, false);
					// What about invalid divs? (i.e. empty properties)
				}
				else if (line.LineType == LineType.NAMEDCHILD) {
					entryStack.Push(line.Location, line.IndentLevel, line.Content, true, line.LocalOnly);
				}
				else if (line.LineType == LineType.DEFINITION) {
					entryStack.AddDefinition(line.Location, line.Content);
				}
				else if (line.LineType == LineType.PROPERTY) {
					if (SharpDocuments.StringEquals(line.Content, CardConfigConstants.ArchiveName)) {
						if (line.IndentLevel == 0) {
							entryStack.AddArchive(line.Location, line.Property!); // Property must not be null is LineType is PROPERTY
						}
						else {
							entryStack.parsingExceptions.Add(new SharpParsingException(line.Location, "Archives may only be specified at the root level of the document."));
						}
					}
					else {
						entryStack.SetProperty(line.Location, line.Content, line.PropertyLocation, line.Property!, line.LocalOnly); // Property must not be null is LineType is PROPERTY
					}
				}
				else if (line.LineType == LineType.ENTRY) {
					entryStack.AddEntry(line.Location, line.Content);
				}
				else if (line.LineType == LineType.FLAG) {
					if (line.IndentLevel > 0 && widgetFactory.IsWidget(line.Content)) { // flag is actually an empty SharpRect
						entryStack.Push(line.Location, line.IndentLevel, line.Content, false, false);
						entryStack.Pop();
					}
					else { // This really is just a flag
						entryStack.AddFlag(line.Location, line.Content, line.LocalOnly);
					}
				}
				else { // line.LineType == LineType.ERROR
					entryStack.parsingExceptions.Add(new SharpParsingException(line.Location, "Could not parse line."));
					continue;
				}

			}

			return entryStack;
		}

		public CardSetConfig? ParseContent(FilePath origin, DirectoryPath source, string config, out CompilationResult results) {
			CardSetConfigEntryStack parsed = ParseConfig(origin, source, config);
			parsed.CollectConfigurationResults(out CardSetConfig? cardSetConfig, out results);
			return cardSetConfig;
		}
	}

}