using SharpSheets.Evaluations;
using SharpSheets.Shapes;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Documentation;
using System.Collections;
using SharpSheets.Cards.Card;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Cards.CardConfigs {
	public class CardSetConfigFactory : ITypeDetailsCollection {

		private readonly WidgetFactory widgetFactory;
		private readonly ShapeFactory shapeFactory;
		private readonly IFileReader fileReader;

		public CardSetConfigFactory(WidgetFactory widgetFactory, ShapeFactory shapeFactory, IFileReader fileReader) {
			this.widgetFactory = widgetFactory;
			this.shapeFactory = shapeFactory;
			this.fileReader = fileReader;
		}

		#region Static Initialisation and Accessors

		private static readonly ConstructorInfo cardSetConfigConstructorInfo;
		private static readonly ConstructorInfo cardConfigConstructorInfo;
		//private static readonly ConstructorInfo segmentConfigConstructorInfo;
		private static readonly ConstructorInfo dynamicSegmentConfigConstructorInfo;
		private static readonly ConstructorInfo textSegmentConfigConstructorInfo;
		private static readonly ConstructorInfo paragraphSegmentConfigConstructorInfo;
		private static readonly ConstructorInfo tableSegmentConfigConstructorInfo;
		private static readonly ConstructorInfo featureConfigConstructorInfo;

		private static readonly Dictionary<Type, ConstructorInfo> segmentConstructorsByType;
		private static readonly Dictionary<string, ConstructorInfo> segmentConstructorsByName;

		public static readonly ConstructorDetails CardSetConfigConstructor;
		public static readonly ConstructorDetails CardConfigConstructor;
		//public static readonly ConstructorDetails SegmentConfigConstructor;
		public static readonly ConstructorDetails DynamicSegmentConfigConstructor;
		public static readonly ConstructorDetails TextSegmentConfigConstructor;
		public static readonly ConstructorDetails ParagraphSegmentConfigConstructor;
		public static readonly ConstructorDetails TableSegmentConfigConstructor;
		public static readonly ConstructorDetails FeatureConfigConstructor;

		public static readonly ITypeDetailsCollection ConfigConstructors;
		public static readonly ITypeDetailsCollection SegmentConfigConstructors;

		public static readonly ConstructorDetails OutlineConstructor;
		public static readonly ConstructorDetails HeaderConstructor;

		private static readonly Dictionary<Type, ConstructorDetails> cardConfigConstructorsByType;
		public static readonly Dictionary<string, ConstructorDetails> cardConfigConstructorsByName;
		public static readonly Dictionary<string, ConstructorDetails> cardSegmentConfigConstructorsByName;

		/// <summary></summary>
		/// <exception cref="TypeInitializationException"></exception>
		static CardSetConfigFactory() {
			cardSetConfigConstructorInfo = typeof(CardSetConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(CardSetConfig)} constructor.", null);
			cardConfigConstructorInfo = typeof(CardConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(CardConfig)} constructor.", null);
			//segmentConfigConstructorInfo = typeof(CardSegmentConfig).GetConstructors().First();
			dynamicSegmentConfigConstructorInfo = typeof(DynamicCardSegmentConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(DynamicCardSegmentConfig)} constructor.", null);
			textSegmentConfigConstructorInfo = typeof(TextCardSegmentConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(TextCardSegmentConfig)} constructor.", null);
			paragraphSegmentConfigConstructorInfo = typeof(ParagraphCardSegmentConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(ParagraphCardSegmentConfig)} constructor.", null);
			tableSegmentConfigConstructorInfo = typeof(TableCardSegmentConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(TableCardSegmentConfig)} constructor.", null);
			featureConfigConstructorInfo = typeof(CardFeatureConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(CardFeatureConfig)} constructor.", null);

			CardSetConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(CardSetConfig), cardSetConfigConstructorInfo, "CardsConfiguration");
			CardConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(CardConfig), cardConfigConstructorInfo, "Card");
			//SegmentConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(CardSegmentConfig), segmentConfigConstructorInfo, "CardSegment");
			DynamicSegmentConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(DynamicCardSegmentConfig), dynamicSegmentConfigConstructorInfo, "Segment");
			TextSegmentConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(TextCardSegmentConfig), textSegmentConfigConstructorInfo, "TextBlock");
			ParagraphSegmentConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(ParagraphCardSegmentConfig), paragraphSegmentConfigConstructorInfo, "Paragraphs");
			TableSegmentConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(TableCardSegmentConfig), tableSegmentConfigConstructorInfo, "Table");
			FeatureConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(CardFeatureConfig), featureConfigConstructorInfo, "Feature");

			segmentConstructorsByType = new Dictionary<Type, ConstructorInfo>() {
				{ typeof(DynamicCardSegmentConfig), dynamicSegmentConfigConstructorInfo },
				{ typeof(TextCardSegmentConfig), textSegmentConfigConstructorInfo },
				{ typeof(ParagraphCardSegmentConfig), paragraphSegmentConfigConstructorInfo },
				{ typeof(TableCardSegmentConfig), tableSegmentConfigConstructorInfo }
			};
			segmentConstructorsByName = new Dictionary<string, ConstructorInfo>(SharpDocuments.StringComparer) {
				{ DynamicSegmentConfigConstructor.Name, dynamicSegmentConfigConstructorInfo },
				{ TextSegmentConfigConstructor.Name, textSegmentConfigConstructorInfo },
				{ ParagraphSegmentConfigConstructor.Name, paragraphSegmentConfigConstructorInfo },
				{ TableSegmentConfigConstructor.Name, tableSegmentConfigConstructorInfo }
			};

			ConstructorDetails baseDivConstructor = WidgetFactory.DivConstructor;
			ArgumentDetails[] configDivArgs = ConditionArgument.Yield().Concat(baseDivConstructor.Arguments).ToArray();
			OutlineConstructor = new ConstructorDetails(typeof(SharpWidget), typeof(SharpWidget), "Outline", "Outline", configDivArgs, new DocumentationString("Outline description."), new SharpSheets.Layouts.Rectangle(0f, 0f), null);
			HeaderConstructor = new ConstructorDetails(typeof(SharpWidget), typeof(SharpWidget), "Header", "Header", configDivArgs, new DocumentationString("Header description."), new SharpSheets.Layouts.Rectangle(0f, 0f), null);

			ConfigConstructors = new TypeDetailsCollection(
				new ConstructorDetails[] {
					CardSetConfigConstructor,
					CardConfigConstructor,
					//SegmentConfigConstructor,
					DynamicSegmentConfigConstructor,
					TextSegmentConfigConstructor,
					ParagraphSegmentConfigConstructor,
					TableSegmentConfigConstructor,
					FeatureConfigConstructor,
					OutlineConstructor,
					HeaderConstructor
				}, SharpDocuments.StringComparer);

			SegmentConfigConstructors = new TypeDetailsCollection(
				new ConstructorDetails[] {
					//SegmentConfigConstructor,
					DynamicSegmentConfigConstructor,
					TextSegmentConfigConstructor,
					ParagraphSegmentConfigConstructor,
					TableSegmentConfigConstructor
				}, SharpDocuments.StringComparer);

			cardConfigConstructorsByType = new Dictionary<Type, ConstructorDetails>() {
				{ typeof(CardSetConfig), CardSetConfigConstructor },
				{ typeof(CardConfig), CardConfigConstructor },
				//{ typeof(CardSegmentConfig), SegmentConfigConstructor },
				{ typeof(DynamicCardSegmentConfig), DynamicSegmentConfigConstructor },
				{ typeof(TextCardSegmentConfig), TextSegmentConfigConstructor },
				{ typeof(ParagraphCardSegmentConfig), ParagraphSegmentConfigConstructor },
				{ typeof(TableCardSegmentConfig), TableSegmentConfigConstructor },
				{ typeof(CardFeatureConfig), FeatureConfigConstructor }
			};
			cardConfigConstructorsByName = new Dictionary<string, ConstructorDetails>(
				cardConfigConstructorsByType.ToDictionary(kv => kv.Value.Name, kv => kv.Value),
				SharpDocuments.StringComparer) {
				{OutlineConstructor.Name, OutlineConstructor },
				{HeaderConstructor.Name, HeaderConstructor }
			};
			cardSegmentConfigConstructorsByName = new Dictionary<string, ConstructorDetails>(
				SegmentConfigConstructors.ToDictionary(c => c.Name, c => c),
				SharpDocuments.StringComparer); ;
		}

		#endregion

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public CardSetConfig? MakeSetConfig(string configName, IContext context, IEnumerable<ContextValue<string>> archives, FilePath origin, DirectoryPath source, out Dictionary<object, IDocumentEntity> origins, out List<SharpParsingException> errors) {

			CardSetConfig? cardSetConfig;
			origins = new Dictionary<object, IDocumentEntity>(new IdentityEqualityComparer<object>());
			errors = new List<SharpParsingException>();

			try {
				cardSetConfig = (CardSetConfig)SharpFactory.Construct(cardSetConfigConstructorInfo, context, source, widgetFactory, shapeFactory, new object[] { configName, origin, source }, out SharpParsingException[] cardSetDefBuildErrors);
				errors.AddRange(cardSetDefBuildErrors);
			}
			catch (SharpParsingException e) {
				cardSetConfig = null;
				errors.Add(e);
			}
			catch (SharpFactoryException e) {
				cardSetConfig = null;
				errors.AddRange(e.Errors);
			}

			if (cardSetConfig == null) { return null; }
			if (origins != null) { origins.Add(cardSetConfig, context); }

			IVariableBox definitionVariables = BasisEnvironment.Instance.AppendVariables(cardSetConfig.Variables);

			foreach (ContextValue<string> definitionValue in context.GetDefinitions(context)) {
				try {
					Definition definition = Definition.Parse(definitionValue.Value, definitionVariables);
					cardSetConfig.definitions.Add(definition);
				}
				catch (Exception e) { // TODO More specific?
					errors.Add(new SharpParsingException(definitionValue.Location, e.Message, e));
				}
			}

			IVariableBox outlinesVariables = CardOutlinesEnvironments.GetVariables(cardSetConfig);
			IEnvironment outlinesDryRunEnvironment = CardOutlinesEnvironments.GetDryRun(cardSetConfig);

			foreach (IContext outline in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, OutlineConstructor.Name))) {
				InterpolatedContext outlineContext = MakeInterpolatedContext(outline, outlinesVariables, errors);
				Conditional<InterpolatedContext> outlineEntry = MakeCondition(outline, outlineContext, outlinesVariables, errors);
				cardSetConfig.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // TODO Yes?
				DryRunParse(outlineContext, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext header in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, HeaderConstructor.Name))) {
				InterpolatedContext headerContext = MakeInterpolatedContext(header, outlinesVariables, errors);
				Conditional<InterpolatedContext> headerEntry = MakeCondition(header, headerContext, outlinesVariables, errors);
				cardSetConfig.headers.Add(headerEntry);
				if (origins != null) { origins.Add(header, header); } // TODO Yes?
				DryRunParse(headerContext, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext segment in context.Children.Where(c => cardSegmentConfigConstructorsByName.ContainsKey(c.SimpleName))) {
				AbstractCardSegmentConfig? cardSegment = MakeSegment(segment, cardSetConfig, origins, errors);

				if (cardSegment != null) {
					Conditional<AbstractCardSegmentConfig> segmentConditional = MakeCondition(segment, cardSegment, cardSegment.Variables, errors);
					cardSetConfig.cardSetSegments.Add(segmentConditional);
				}
			}

			foreach (IContext card in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, CardConfigConstructor.Name))) {
				CardConfig? cardConfig = MakeConfig(card, cardSetConfig, origins, errors);

				if (cardConfig != null) {
					Conditional<CardConfig> cardConditional = MakeCondition(card, cardConfig, cardConfig.Variables, errors);
					cardSetConfig.cardConfigs.Add(cardConditional);
				}
			}

			if(cardSetConfig.cardConfigs.Count == 0) {
				// Create an empty card config to use as fallback
				IContext emptyCardContext = new EmptyChildContext(context, CardConfigConstructor.Name);
				CardConfig emptyCardConfig = MakeConfig(emptyCardContext, cardSetConfig, origins, errors) ?? throw new InvalidOperationException("Failed to make backup empty card configuration.");
				cardSetConfig.cardConfigs.Add(new Conditional<CardConfig>(true, emptyCardConfig));
			}

			// Load archives (do this at the end so CardConfig is fully initialised)
			CardSubjectParser archiveParser = new CardSubjectParser(cardSetConfig);
			foreach (ContextValue<string> archive in archives) {
				FilePath archivePath = new FilePath(source.Path, archive.Value);
				if (archivePath.Exists && archivePath.GetDirectory() is DirectoryPath archiveSource) {
					try {
						cardSetConfig.archivePaths.Add(archivePath);

						string archiveText = fileReader.ReadAllText(archivePath.Path);

						CardSubjectDocument archiveSubjectsDocument = archiveParser.Parse(archiveSource, archiveText, out CompilationResult archiveResults);
						errors.AddRange(archiveResults.errors.Select(e => new SharpParsingException(archive.Location, "Archive error: " + e.Message, e.InnerException)));
						cardSetConfig.AddRangeToArchive(archiveSubjectsDocument.AllSubjects());
					}
					catch(IOException e) {
						errors.Add(new SharpParsingException(archive.Location, $"Could not load archive at: {archivePath.Path}", e));
					}
				}
				else {
					errors.Add(new SharpParsingException(archive.Location, "Provided archive does not exist."));
				}
			}

			if(context.GetProperty(CardConfigConstants.ExamplesName, false, context, null, out DocumentSpan? exampleLocation) is string examplesStr) {
				string[]? exampleNames = ValueParsing.Parse<string[]>(examplesStr, source);
				if(exampleNames is not null) {
					foreach(string exampleName in exampleNames) {
						if(cardSetConfig.TryGetArchived(exampleName, out CardSubject? example)) {
							cardSetConfig.AddExample(example);
						}
						else {
							errors.Add(new SharpParsingException(exampleLocation, $"Could not find example \"{exampleName}\"."));
						}
					}
				}
				else {
					errors.Add(new SharpParsingException(exampleLocation, "Could not parse examples list."));
				}
			}

			return cardSetConfig;
		}

		// IContext context, CardConfig cardConfig, DirectoryPath source, Dictionary<object, IDocumentEntity> origins, List<SharpParsingException> errors
		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		private CardConfig? MakeConfig(IContext context, CardSetConfig cardSetConfig, Dictionary<object, IDocumentEntity>? origins, List<SharpParsingException> errors) {

			CardConfig? cardConfig;

			try {
				cardConfig = (CardConfig)SharpFactory.Construct(cardConfigConstructorInfo, context, cardSetConfig.Source, widgetFactory, shapeFactory, new object[] { cardSetConfig }, out SharpParsingException[] cardDefBuildErrors);
				errors.AddRange(cardDefBuildErrors);
			}
			catch (SharpParsingException e) {
				cardConfig = null;
				errors.Add(e);
			}
			catch (SharpFactoryException e) {
				cardConfig = null;
				errors.AddRange(e.Errors);
			}

			if (cardConfig == null) { return null; }
			if (origins != null) { origins.Add(cardConfig, context); }

			IVariableBox definitionVariables = BasisEnvironment.Instance.AppendVariables(cardConfig.Variables);

			foreach (ContextValue<string> definitionValue in context.GetDefinitions(context)) {
				try {
					Definition definition = Definition.Parse(definitionValue.Value, definitionVariables);
					cardConfig.definitions.Add(definition);
				}
				catch (Exception e) { // TODO More specific?
					errors.Add(new SharpParsingException(definitionValue.Location, e.Message, e));
				}
			}

			IVariableBox outlinesVariables = CardOutlinesEnvironments.GetVariables(cardConfig);
			IEnvironment outlinesDryRunEnvironment = CardOutlinesEnvironments.GetDryRun(cardConfig); // DynamicCardEnvironments.CardNumberDryRun(CardSubjectEnvironments.GetDryRun(cardConfig));

			foreach (IContext outline in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, OutlineConstructor.Name))) {
				InterpolatedContext outlineContext = MakeInterpolatedContext(outline, outlinesVariables, errors);
				Conditional<InterpolatedContext> outlineEntry = MakeCondition(outline, outlineContext, outlinesVariables, errors);
				cardConfig.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // TODO Yes?
				DryRunParse(outlineContext, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext header in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, HeaderConstructor.Name))) {
				InterpolatedContext headerContext = MakeInterpolatedContext(header, outlinesVariables, errors);
				Conditional<InterpolatedContext> headerEntry = MakeCondition(header, headerContext, outlinesVariables, errors);
				cardConfig.headers.Add(headerEntry);
				if (origins != null) { origins.Add(header, header); } // TODO Yes?
				DryRunParse(headerContext, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext segment in context.Children.Where(c => cardSegmentConfigConstructorsByName.ContainsKey(c.SimpleName))) {
				AbstractCardSegmentConfig? cardSegment = MakeSegment(segment, cardConfig, origins, errors);

				if (cardSegment != null) {
					Conditional<AbstractCardSegmentConfig> segmentConditional = MakeCondition(segment, cardSegment, cardSegment.Variables, errors);
					cardConfig.AddSegment(segmentConditional);
				}
			}

			return cardConfig;
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		private AbstractCardSegmentConfig? MakeSegment(IContext context, ICardSegmentParent parent, Dictionary<object, IDocumentEntity>? origins, List<SharpParsingException> errors) {
			//ConstructorInfo constructorInfo = segmentConstructorsByType[typeof(T)];
			ConstructorInfo constructorInfo = segmentConstructorsByName.GetValueOrFallback(context.SimpleName, null) ?? throw new InvalidOperationException($"Could not find segment constructor for \"{context.SimpleName}\"");
			Type segmentType = constructorInfo.DeclaringType ?? throw new InvalidOperationException("Could not resolve corresponding card segment constructor.");

			AbstractCardSegmentConfig? cardSegment;

			try {
				List<object> requiredArgs = new List<object>() { parent };
				if (segmentType == typeof(TextCardSegmentConfig) || segmentType == typeof(ParagraphCardSegmentConfig)) {
					IVariableBox featureVariables = CardFeatureEnvironments.GetVariables(parent); // BasisEnvironment.Instance.AppendVariables(CardFeatureEnvironments.BaseDefinitions); // TODO CardFeatureEnvironments.GetTextVariables(parent);?
					TextExpression? content = MakeTextProperty("content", context, featureVariables, errors);
					requiredArgs.Add(content ?? new TextExpression(""));
				}
				if(segmentType == typeof(TextCardSegmentConfig)) {
					IVariableBox segmentVariables = CardSegmentEnvironments.GetVariables(parent); // BasisEnvironment.Instance.AppendVariables(CardSegmentEnvironments.BaseDefinitions);
					TextExpression? delimiter = MakeTextProperty("delimiter", context, segmentVariables, errors);
					requiredArgs.Add(delimiter ?? new TextExpression(""));
					TextExpression? prefix = MakeTextProperty("prefix", context, segmentVariables, errors);
					requiredArgs.Add(prefix ?? new TextExpression(""));
					TextExpression? tail = MakeTextProperty("tail", context, segmentVariables, errors);
					requiredArgs.Add(tail ?? new TextExpression(""));
				}
				cardSegment = (AbstractCardSegmentConfig)SharpFactory.Construct(constructorInfo, context, parent.Source, widgetFactory, shapeFactory, requiredArgs.ToArray(), out SharpParsingException[] cardSegmentBuildErrors);
				errors.AddRange(cardSegmentBuildErrors);
			}
			catch (SharpParsingException e) {
				cardSegment = null;
				errors.Add(e);
			}
			catch (SharpFactoryException e) {
				cardSegment = null;
				errors.AddRange(e.Errors);
			}

			if (cardSegment == null) { return null; }

			if (origins != null) { origins.Add(cardSegment, context); }

			IVariableBox definitionVariables = BasisEnvironment.Instance.AppendVariables(cardSegment.Variables);

			// If not dynamic, or is dynamic and not always included
			// AlwaysInclude segments cannot have their own Definitions specified
			if (cardSegment is not DynamicCardSegmentConfig dynamic || !dynamic.AlwaysInclude) { 
				foreach (ContextValue<string> definitionValue in context.GetDefinitions(context)) {
					try {
						Definition definition = Definition.Parse(definitionValue.Value, definitionVariables);
						cardSegment.definitions.Add(definition);
					}
					catch (Exception e) {
						errors.Add(new SharpParsingException(definitionValue.Location, e.Message, e));
					}
				}
			}

			/*
			IEnvironment segmentEnvironment = CardSegmentEnvironments.GetDryRun(cardSegment);
			foreach (IContext outline in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, OutlineConstructor.Name))) {
				Conditional<IContext> outlineEntry = MakeCondition(outline, outline, cardSegment.Variables, errors);
				cardSegment.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // Yes?
				DryRunRect(outline, segmentEnvironment, cardConfig.source, errors);
			}
			*/

			IVariableBox outlinesVariables = CardSegmentOutlineEnvironments.GetVariables(cardSegment);
			IEnvironment outlinesDryRunEnvironment = CardSegmentOutlineEnvironments.GetDryRun(cardSegment);
			foreach (IContext outline in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, OutlineConstructor.Name))) {
				InterpolatedContext outlineContext = MakeInterpolatedContext(outline, outlinesVariables, errors);
				Conditional<InterpolatedContext> outlineEntry = MakeCondition(outline, outlineContext, outlinesVariables, errors);
				cardSegment.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // TODO Yes?
				DryRunParse(outlineContext, outlinesDryRunEnvironment, parent.Source, errors);
			}

			if (cardSegment is DynamicCardSegmentConfig featuredSegment) {
				foreach (IContext feature in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, FeatureConfigConstructor.Name))) {
					CardFeatureConfig? cardFeature = MakeFeature(feature, featuredSegment, parent.Source, origins, errors);

					if (cardFeature is not null) {
						Conditional<CardFeatureConfig> segmentFeatureConditional = MakeCondition(feature, cardFeature, cardFeature.Variables, errors);
						featuredSegment.cardFeatures.Add(segmentFeatureConditional);

						DryRunParse(cardFeature.Layout, CardFeatureEnvironments.GetDryRun(cardFeature), parent.Source, errors);
					}
				}

				if(featuredSegment.cardFeatures.Count == 0) {
					errors.Add(new SharpParsingException(context.Location, "Segment must have at least one valid feature."));
					return null;
				}

				/*
				if(featuredSegment.cardFeatures.Count == 1 && !featuredSegment.cardFeatures.First().Condition.IsTrue && origins.TryGetValue(featuredSegment.cardFeatures.First().Value, out IDocumentEntity featureEntity)) {
					errors.Add(new SharpParsingException(featureEntity.Location, "Only one feature is provided, and it has a condition."));
				}
				*/
			}

			return cardSegment;
		}

		private CardFeatureConfig? MakeFeature(IContext context, DynamicCardSegmentConfig segmentConfig, DirectoryPath source, Dictionary<object, IDocumentEntity>? origins, List<SharpParsingException> errors) {
			CardFeatureConfig? cardFeature;
			
			try {
				cardFeature = (CardFeatureConfig)SharpFactory.Construct(featureConfigConstructorInfo, context, source, widgetFactory, shapeFactory, new object[] { segmentConfig }, out SharpParsingException[] cardFeatureBuildErrors);
				errors.AddRange(cardFeatureBuildErrors);
			}
			catch (SharpParsingException e) {
				cardFeature = null;
				errors.Add(e);
			}
			catch (SharpFactoryException e) {
				cardFeature = null;
				errors.AddRange(e.Errors);
			}

			if (cardFeature == null) { return null; }
			if (origins != null) { origins.Add(cardFeature, context); }

			IVariableBox featureVariables = BasisEnvironment.Instance.AppendVariables(cardFeature.Variables);

			foreach (ContextValue<string> definitionValue in context.GetDefinitions(context)) {
				try {
					Definition definition = Definition.Parse(definitionValue.Value, featureVariables);
					cardFeature.definitions.Add(definition);
				}
				catch (Exception e) {
					errors.Add(new SharpParsingException(definitionValue.Location, e.Message, e));
				}
			}

			InterpolatedContext featureContext = MakeInterpolatedContext(context, featureVariables, errors);
			cardFeature.Layout = featureContext;

			return cardFeature;
		}

		private static Conditional<T> MakeCondition<T>(IContext context, T value, IVariableBox variables, List<SharpParsingException> errors) {
			if (variables == null) {
				// TODO Error here?
				return new Conditional<T>(false, value);
			}

			variables = BasisEnvironment.Instance.AppendVariables(variables);

			string? conditionStr = context.GetProperty(ConditionArgument.Name, true, context, null, out DocumentSpan? location);

			if(conditionStr == null) {
				return new Conditional<T>(true, value);
			}

			try {
				BoolExpression condition = BoolExpression.Parse(conditionStr, variables);
				return new Conditional<T>(condition, value);
			}
			catch(Exception e) {
				errors.Add(new SharpParsingException(location, e.Message, e));
				return new Conditional<T>(false, value);
			}
		}

		private InterpolatedContext MakeInterpolatedContext(IContext context, IVariableBox variables, List<SharpParsingException> errors) {
			InterpolatedContext result = InterpolatedContext.Parse(context, variables, true, out SharpParsingException[] contextErrors);
			errors.AddRange(contextErrors);
			return result;
		}

		private static TextExpression? MakeTextProperty(string property, IContext context, IVariableBox variables, List<SharpParsingException> errors) {
			if (variables == null) {
				// TODO Error here?
				return null;
			}
			string? propertyStr = context.GetProperty(property, true, context, null, out DocumentSpan? location);
			if (propertyStr == null) {
				return null;
			}

			try {
				TextExpression propertyExpr = Interpolation.Parse(propertyStr, variables, true);
				return propertyExpr;
			}
			catch (Exception e) {
				errors.Add(new SharpParsingException(location, e.Message, e));
				return null;
			}
		}

		/*
		private static void CheckCondition(InterpolatedContext context, IVariableBox variables, List<SharpParsingException> errors) {
			string? conditionStr = context.GetProperty(ConditionArgument.Name, true, context, null, out DocumentSpan? conditionLocation);
			if (conditionStr != null) {
				try {
					BoolExpression.Parse(conditionStr, variables);
				}
				catch (Exception e) {
					errors.Add(new SharpParsingException(conditionLocation, "Error parsing condition: " + e.Message, e));
				}
			}
		}
		*/

		private void DryRunParse(InterpolatedContext context, IEnvironment environment, DirectoryPath source, List<SharpParsingException> errors) {
			/*
			CheckCondition(context, environment, errors);
			foreach(IContext child in context.TraverseChildren(true)) {
				CheckCondition(child, environment, errors);
			}
			*/

			try {
				widgetFactory.MakeWidget(typeof(Div), new LazyInterpolatedContext(context.OriginalContext, environment), source, out SharpParsingException[] rectErrors);
				errors.AddRange(rectErrors);
			}
			catch(UndefinedVariableException e) {
				errors.Add(new SharpParsingException(context.Location, e.Message, e));
			}
			catch(KeyNotFoundException e) {
				errors.Add(new SharpParsingException(context.Location, "There is a missing key.", e));
			}
		}

		#region Type Details

		public static readonly ArgumentDetails ConditionArgument = new ArgumentDetails(
			"condition",
			new DocumentationString("A boolean expression used to determine if this part of the configuration " +
				"should be used, based on the card subject data. If no expression is provided, it is assumed to be true."),
			ArgumentType.Simple(typeof(BoolExpression)), true, true, "True", new BoolExpression(true), null);

		public static readonly ArgumentDetails ForEachArgument = new ArgumentDetails(
			"foreach",
			new DocumentationString("A for-each expression, which will cause this element to be repeated for each " +
				"entry of a specified array of values, with each of those entries available as a variable in the " +
				"corresponding repetition. Must be of the pattern \"loopVar in arrayExpr\", where \"arrayExpr\" is " +
				"an expression which evaluates to an array, and \"loopVar\" is the name to use for the loop variable."),
			ArgumentType.Simple(typeof(ContextForEach)), true, true, null, null, null);

		private static ConstructorDetails MakeConfigConstructor(ConstructorDetails constructor) {
			if (constructor.DeclaringType == typeof(CardConfig)) {
				return constructor; // The top-level card config has no condition
			}
			else if (typeof(IWidget).IsAssignableFrom(constructor.DeclaringType) && constructor != OutlineConstructor && constructor != HeaderConstructor) {
				if(constructor.Arguments.Length > 1 && ArgumentComparer.Instance.Equals(constructor.Arguments[0], ConditionArgument) && ArgumentComparer.Instance.Equals(constructor.Arguments[1], ForEachArgument)) {
					return constructor;
				}
				else {
					return constructor.WithArgument(ForEachArgument, 0).WithArgument(ConditionArgument, 0);
				}
			}
			else if(constructor.Arguments.Length > 0 && ArgumentComparer.Instance.Equals(constructor.Arguments[0], ConditionArgument)) {
				return constructor;
			}
			else {
				return constructor.WithArgument(ConditionArgument, 0);
			}
		}

		public bool ContainsKey(Type type) {
			return widgetFactory.ContainsKey(type) || cardConfigConstructorsByType.ContainsKey(type);
		}

		public bool ContainsKey(string name) {
			return widgetFactory.ContainsKey(name) || cardConfigConstructorsByName.ContainsKey(name);
		}

		public bool TryGetValue(Type type, [MaybeNullWhen(false)] out ConstructorDetails constructor) {
			if(widgetFactory.TryGetValue(type, out ConstructorDetails? widgetConstructor)) {
				constructor = MakeConfigConstructor(widgetConstructor);
				return true;
			}
			else if(cardConfigConstructorsByType.TryGetValue(type, out ConstructorDetails? cardConstructor)) {
				constructor = MakeConfigConstructor(cardConstructor);
				return true;
			}
			else {
				constructor = null;
				return false;
			}
		}

		public bool TryGetValue(string name, [MaybeNullWhen(false)] out ConstructorDetails constructor) {
			if (widgetFactory.TryGetValue(name, out ConstructorDetails? widgetConstructor)) {
				constructor = MakeConfigConstructor(widgetConstructor);
				return true;
			}
			else if (cardConfigConstructorsByName.TryGetValue(name, out ConstructorDetails? cardConstructor)) {
				constructor = MakeConfigConstructor(cardConstructor);
				return true;
			}
			else {
				constructor = null;
				return false;
			}
		}

		public IEnumerator<ConstructorDetails> GetEnumerator() {
			return new ConstructorDetailsUniqueNameEnumerator(
				widgetFactory.Select(c=>MakeConfigConstructor(c)).Concat(cardConfigConstructorsByName.Values),
				SharpDocuments.StringComparer);
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public IEnumerable<KeyValuePair<string, ConstructorDetails>> GetConstructorNames() {
			return widgetFactory.GetConstructorNames()
				.Select(kv => new KeyValuePair<string, ConstructorDetails>(kv.Key, MakeConfigConstructor(kv.Value)))
				.Concat(cardConfigConstructorsByName);
		}

		#endregion
	}

	public class LazyInterpolatedContext : IContext {

		public IContext OriginalContext { get; }
		public IEnvironment Environment { get; }

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		public LazyInterpolatedContext(IContext originalContext, IEnvironment environment) {
			this.OriginalContext = originalContext ?? throw new ArgumentNullException(nameof(originalContext));
			//this.Environment = environment ?? throw new ArgumentNullException(nameof(environment));
			this.Environment = GetFullEnvironment(originalContext, environment ?? throw new ArgumentNullException(nameof(environment)));
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		[return: NotNullIfNotNull(nameof(original))]
		private string? Replace(string? original, DocumentSpan? location) {
			if (original == null) { return null; }
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

		public IContext? Parent => OriginalContext.Parent; // TODO This is still not right...
		public IEnumerable<IContext> Children {
			get {
				return OriginalContext.Children
					.Select(c => new LazyInterpolatedContext(c, Environment));
			}
		}
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren {
			get {
				return OriginalContext.NamedChildren
					.Select(kv => new KeyValuePair<string, IContext>(kv.Key, new LazyInterpolatedContext(kv.Value, Environment)));
			}
		}

		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		/*
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
		*/

		private static IEnvironment GetFullEnvironment(IContext context, IEnvironment initial) {
			string? foreachStr = context.GetProperty("foreach", true, null, null, out _);
			if (foreachStr is not null) {
				try {
					ContextForEach forEach = ContextForEach.Parse(foreachStr, initial);
					return initial.AppendEnvironment(new DryRunEnvironment(SimpleVariableBoxes.Single(forEach.LoopVariable)));
				}
				catch (EvaluationException) { }
				catch (FormatException) { }
			}
			return initial;
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
			if (namedChild == null) {
				return null;
			}
			else {
				return new LazyInterpolatedContext(namedChild, Environment);
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
