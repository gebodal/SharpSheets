using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Canvas;
using SharpSheets.Cards.CardConfigs;
using SharpEditor.DataManagers;
using SharpSheets.Cards.Card;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Widgets;
using System;

namespace SharpEditor.Parsing.ParsingState {

	public sealed class CardConfigParsingState : SharpConfigParsingState<CardConfigSpan> {

		public CardConfigParsingState(TextDocument document) : base(document) { }

		public override IParser Parser { get; } = new CardConfigEditorParser(
				new CardSetConfigParser(
						SharpEditorRegistries.WidgetFactoryInstance,
						SharpEditorRegistries.ShapeFactoryInstance,
						SharpEditorRegistries.CardSetConfigFactoryInstance
					)
			);

		public override string Extension { get { return SharpEditorFileInfo.CardConfigExtension; } }
		public override string FileFilter { get { return SharpEditorFileInfo.CardConfigFileFilter1; } }
		public override string DefaultFilename { get { return "cardconfig"; } }

		public override bool HasDesignerContent { get; } = true;
		public override bool HasGeneratedContent { get; } = false;

		private CardSetConfig? CardSetConfig { get; set; }

		private CardCollection? examplesDocument;
		public override IDocumentContent? DrawableContent { get { return examplesDocument; } }

		public override CardConfigSpan Create(int startOffset, int length) {
			ValidateSpan(startOffset, length);
			return new CardConfigSpan(this, startOffset, length);
		}

		protected override SharpConfigColorizingTransformer<CardConfigSpan> MakeColorizer() {
			return new SharpConfigColorizingTransformer<CardConfigSpan>(this, SharpEditorRegistries.CardSetConfigFactoryInstance, SharpEditorRegistries.ShapeFactoryInstance);
		}

		#region Loading Data

		protected override void LoadContent(ResultEntry result) {
			CardConfigEditorResult? content = result.content as CardConfigEditorResult;
			CardSetConfig = content?.Config;
			examplesDocument = content?.Cards;
			Origins = new ContextOrigins<CardConfigSpan>(this, result.results.rootEntity as IContext, result.results.origins?.GetData().ToDictionary(kv => kv.Key, kv => (IContext)kv.Value));
		}

		static readonly Regex expressionRegex = new Regex(@"((?<!\\)\$\{|(?<!\\|\$)\{)[^\}\:]+(:[^\}\:]+)?(?<!\\)\}");

		protected override void CreateAdditionalSpans() {
			List<CardConfigSpan> newSpans = new List<CardConfigSpan>();
			foreach (CardConfigSpan span in this.ConfigSpans) {
				Match exprMatch = expressionRegex.Match(Document.GetText(span));
				if (exprMatch.Success) {
					CardConfigSpan exprSpan = Create(span.StartOffset + exprMatch.Index, exprMatch.Length);
					exprSpan.Type = SharpConfigSpanType.CUSTOM;
					exprSpan.Value = exprMatch.Value;
					exprSpan.IsExpression = true;
					newSpans.Add(exprSpan);
				}
			}
			foreach (CardConfigSpan i in newSpans) {
				Add(i);
			}
		}

		#endregion

		#region Accessors

		public IVariableDefinitionBox? GetVariableDefinitionBox(IContext? context) {
			if (context != null) {
				foreach(IContext parentContext in context.Yield().Concat(context.TraverseParents())) {
					List<object>? resulting = Origins?.GetResulting(parentContext);
					if (resulting is not null) {
						foreach (object resultingObj in resulting.WhereNotNull()) {
							if (resultingObj is IHasVariableDefinitionBox hasVariableBox) {
								return hasVariableBox.Variables;
							}
							if (resultingObj is IVariableDefinitionBox variableBox) {
								return variableBox;
							}
						}
					}
				}
			}
			return null;
		}
		public IVariableDefinitionBox? GetVariableDefinitionBox(DocumentLine line) {
			IContext? currentContext = GetContext(line);
			return GetVariableDefinitionBox(currentContext);
		}

		protected override Definition? GetDefinition(string name, IContext context) {
			if (CardSetConfig != null) {
				if (GetVariableDefinitionBox(context)?.TryGetDefinition(name, out Definition? definition) ?? false) {
					return definition;
				}
			}
			return null;
		}

		public DefinitionGroup? GetDefinitionGroup(IContext context) {
			// TODO Should this be IHasVariableDefinitionBox?
			IVariableDefinitionBox? variableDefinitionBox = GetVariableDefinitionBox(context);
			if (variableDefinitionBox is CardSetConfig cardSetConfig) {
				return cardSetConfig.definitions;
			}
			else if (variableDefinitionBox is CardConfig cardConfig) {
				return cardConfig.definitions;
			}
			else if (variableDefinitionBox is AbstractCardSegmentConfig segmentConfig) {
				return segmentConfig.definitions;
			}
			else if (variableDefinitionBox is CardFeatureConfig featureConfig) {
				return featureConfig.definitions;
			}
			else {
				return null;
			}
		}

		#endregion
	}

	public class CardConfigSpan : SharpConfigSpan {
		public bool IsExpression { get; set; }
		public CardConfigSpan(IParsingState service, int startOffset, int length) : base(service, startOffset, length) { }
	}

	public class CardConfigEditorResult {
		public readonly CardSetConfig? Config;
		public readonly CardCollection? Cards;

		public CardConfigEditorResult(CardSetConfig? config, CardCollection? cards) {
			Config = config;
			Cards = cards;
		}
	}

	public class CardConfigEditorParser : IParser<CardConfigEditorResult> {

		public readonly CardSetConfigParser parser;

		public CardConfigEditorParser(CardSetConfigParser parser) {
			this.parser = parser;
		}

		public CardConfigEditorResult ParseContent(FilePath origin, DirectoryPath source, string config, out CompilationResult results) {
			CardSetConfig? cardSetConfig = parser.ParseContent(origin, source, config, out CompilationResult configCompResults);
			CardCollection? examplesDocument;

			// Default values in case parse did not work or did not give proper results
			examplesDocument = null;
			results = configCompResults;

			if (cardSetConfig != null && cardSetConfig.Examples.Count > 0) {

				ParseOrigins<IDocumentEntity> origins = new ParseOrigins<IDocumentEntity>();
				ParseOrigins<ICardConfigComponent> configOrigins = new ParseOrigins<ICardConfigComponent>();
				WidgetFactory trackedFactory = SharpEditorRegistries.WidgetFactoryInstance.TrackOrigins(origins);
				examplesDocument = CardCollectionParser.MakeCollection(cardSetConfig.Examples, trackedFactory, origins, configOrigins, out _);

				//Console.WriteLine($"Start number: {origins.Count}, reduced: {new HashSet<object>(origins.Values).Count}");

				if (configCompResults.origins is not null) {
					/* TODO This whole thing is a mess now. Some of it isn't necessary, as far as I can tell
					 * and the rest is too complicated and confusing. Needs refactoring/just deleting.
					 */

					ParseOrigins<IDocumentEntity> procOrigins = new ParseOrigins<IDocumentEntity>(); // cardDrawing -> configEntry

					// How much of this first loop is still necessary now? (With the ICardConfigComponent tracking, that is)
					foreach ((object drawnObj, IDocumentEntity docEntity) in origins.GetData()) { // cardRect -> cardSubject / cardWidget -> configEntry
						if (docEntity is IContext context) {
							procOrigins.Add(drawnObj, context);
						}
						else {
							object? configObject = null;
							if (docEntity is CardSubject subject) {
								configObject = subject.CardConfig;
							}
							else if (docEntity is CardSegment section) {
								configObject = section.SegmentConfig;
							}
							else if (docEntity is CardFeature feature) {
								configObject = feature.FeatureConfig;
							}

							if (configObject is not null && configCompResults.origins.TryGetOrigin(configObject, out IDocumentEntity? entity) && entity is IContext entryContext) {
								procOrigins.Add(drawnObj, entryContext);
							}
						}
					}

					foreach((object drawnObj, ICardConfigComponent configComp) in configOrigins.GetData()) {
						if (configCompResults.origins.TryGetOrigin(configComp, out IDocumentEntity? configEntity) && configEntity is IContext configContext) {
							procOrigins.Set(drawnObj, configContext);
						}
					}

					foreach((object cardCompObj, IDocumentEntity cardConfigEntity) in configCompResults.origins.GetData()) {
						procOrigins.Set(cardCompObj, cardConfigEntity);
					}

					results = configCompResults.WithOrigins(procOrigins);
				}
			}

			return new CardConfigEditorResult(cardSetConfig, examplesDocument);
		}

	}

}
