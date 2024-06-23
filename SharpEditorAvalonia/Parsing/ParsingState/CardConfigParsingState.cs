using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Canvas;
using SharpSheets.Cards.CardConfigs;
using SharpEditorAvalonia.DataManagers;
using SharpSheets.Cards.Card;
using SharpSheets.Cards.CardSubjects;
using System.Diagnostics.Eventing.Reader;
using SharpSheets.Widgets;
using System;

namespace SharpEditorAvalonia {

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

		private static IEnumerable<SharpSheets.Documentation.ArgumentDetails> SpecialArgs {
			get {
				return CardSetConfigFactory.ConditionArgument.Yield().Append(CardSetConfigFactory.ForEachArgument);
			}
		}

		protected override SharpConfigColorizingTransformer<CardConfigSpan> MakeColorizer() {
			return new SharpConfigColorizingTransformer<CardConfigSpan>(this, SharpEditorRegistries.CardSetConfigFactoryInstance, SharpEditorRegistries.ShapeFactoryInstance, SpecialArgs);
		}

		#region Loading Data

		protected override void LoadContent(ResultEntry result) {
			CardConfigEditorResult? content = result.content as CardConfigEditorResult;
			CardSetConfig = content?.Config;
			examplesDocument = content?.Cards;
			Origins = new ContextOrigins<CardConfigSpan>(this, result.results.rootEntity as IContext, result.results.origins?.ToDictionary(kv => kv.Key, kv => (IContext)kv.Value));
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
					object? resulting = Origins?.GetResulting(parentContext);
					if (resulting is IHasVariableDefinitionBox hasVariableBox) {
						return hasVariableBox.Variables;
					}
					if (resulting is IVariableDefinitionBox variableBox) {
						return variableBox;
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

		public object Parse(FilePath origin, DirectoryPath source, string config, out CompilationResult results) {
			return ParseContent(origin, source, config, out results);
		}

		public CardConfigEditorResult ParseContent(FilePath origin, DirectoryPath source, string config, out CompilationResult results) {
			CardSetConfig? cardSetConfig = parser.ParseContent(origin, source, config, out CompilationResult configCompResults);
			CardCollection? examplesDocument;

			if (cardSetConfig != null && cardSetConfig.Examples.Count > 0) {

				Dictionary<object, IDocumentEntity> origins = new Dictionary<object, IDocumentEntity>(new IdentityEqualityComparer<object>());
				Dictionary<object, ICardConfigComponent> configOrigins = new Dictionary<object, ICardConfigComponent>(new IdentityEqualityComparer<object>());
				WidgetFactory trackedFactory = SharpEditorRegistries.WidgetFactoryInstance.TrackOrigins(origins);
				examplesDocument = CardCollectionParser.MakeCollection(cardSetConfig.Examples, trackedFactory, origins, configOrigins, out _);

				//Console.WriteLine($"Start number: {origins.Count}, reduced: {new HashSet<object>(origins.Values).Count}");

				if (configCompResults.origins is not null) {
					Dictionary<object, IDocumentEntity> procOrigins = new Dictionary<object, IDocumentEntity>(new IdentityEqualityComparer<object>()); // cardDrawing -> configEntry

					// How much of this first loop is still necessary now? (With the ICardConfigComponent tracking, that is)
					foreach ((object drawnObj, IDocumentEntity docEntity)  in origins) { // cardRect -> cardSubject / cardWidget -> configEntry
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

							if (configObject is not null && configCompResults.origins.TryGetValue(configObject, out IDocumentEntity? entity) && entity is IContext entryContext) {
								procOrigins.Add(drawnObj, entryContext);
							}
						}
					}

					foreach((object drawnObj, ICardConfigComponent configComp) in configOrigins) {
						if (configCompResults.origins.TryGetValue(configComp, out IDocumentEntity? configEntity) && configEntity is IContext configContext) {
							procOrigins[drawnObj] = configContext;
						}
					}

					results = configCompResults.WithOrigins(procOrigins);
				}
				else {
					// Default origins			
					results = configCompResults;
				}
			}
			else {
				examplesDocument = null;
				results = configCompResults;
			}

			return new CardConfigEditorResult(cardSetConfig, examplesDocument);
		}

	}

}
