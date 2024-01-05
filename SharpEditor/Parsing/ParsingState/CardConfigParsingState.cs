using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Canvas;
using SharpSheets.Cards.CardConfigs;
using SharpEditor.DataManagers;
using SharpSheets.Cards.Card;
using SharpSheets.Cards.CardSubjects;
using System.Diagnostics.Eventing.Reader;
using SharpSheets.Widgets;
using System;

namespace SharpEditor {

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

		public override bool HasDesignerContent { get; } = true; // TODO Should have something here
		public override bool HasGeneratedContent { get; } = false;

		private CardSetConfig? CardSetConfig { get; set; }

		private CardCollection? examplesDocument;
		public override IDocumentContent? DrawableContent { get { return examplesDocument; } }

		public override CardConfigSpan Create(int startOffset, int length) {
			ValidateSpan(startOffset, length);
			return new CardConfigSpan(this, startOffset, length);
		}

		protected override SharpConfigColorizingTransformer<CardConfigSpan> MakeColorizer() {
			return new SharpConfigColorizingTransformer<CardConfigSpan>(this, SharpEditorRegistries.CardSetConfigFactoryInstance, SharpEditorRegistries.ShapeFactoryInstance, CardSetConfigFactory.ConditionArgument.Yield());
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
				if (GetVariableDefinitionBox(context)?.GetDefinition(name) is Definition definition) {
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
			else if (variableDefinitionBox is AbstractCardSectionConfig sectionConfig) {
				return sectionConfig.definitions;
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
			CardSetConfig? cardSetConfig = parser.ParseContent(origin, source, config, out CompilationResult configResult);
			CardCollection? examplesDocument;

			if (cardSetConfig != null && cardSetConfig.Examples.Count > 0) {

				Dictionary<object, IDocumentEntity> origins = new Dictionary<object, IDocumentEntity>(new IdentityEqualityComparer<object>());
				WidgetFactory trackedFactory = SharpEditorRegistries.WidgetFactoryInstance.TrackOrigins(origins);
				examplesDocument = CardCollectionParser.MakeCollection(cardSetConfig.Examples, trackedFactory, origins, out _);

				//Console.WriteLine($"Start number: {origins.Count}, reduced: {new HashSet<object>(origins.Values).Count}");

				if (configResult.origins is not null) {
					Dictionary<object, IDocumentEntity> procOrigins = new Dictionary<object, IDocumentEntity>(new IdentityEqualityComparer<object>()); // cardDrawing -> configEntry

					foreach (KeyValuePair<object, IDocumentEntity> entry in origins) { // cardRect -> cardSubject / cardWidget -> configEntry
						if (entry.Value is IContext context) {
							procOrigins.Add(entry.Key, context);
						}
						else {
							object? configObject = null;
							if (entry.Value is CardSubject subject) {
								configObject = subject.CardConfig;
							}
							else if (entry.Value is CardSection section) {
								configObject = section.SectionConfig;
							}
							else if (entry.Value is CardFeature feature) {
								configObject = feature.FeatureConfig;
							}

							if (configObject is not null && configResult.origins.TryGetValue(configObject, out IDocumentEntity? entity) && entity is IContext entryContext) {
								procOrigins.Add(entry.Key, entryContext);
							}
						}
					}

					results = configResult.WithOrigins(procOrigins);
				}
				else {
					// Default origins			
					results = configResult;
				}
			}
			else {
				examplesDocument = null;
				results = configResult;
			}

			return new CardConfigEditorResult(cardSetConfig, examplesDocument);
		}

	}

}
