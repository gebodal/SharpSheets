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
		//private static readonly ConstructorInfo sectionConfigConstructorInfo;
		private static readonly ConstructorInfo dynamicSectionConfigConstructorInfo;
		private static readonly ConstructorInfo textSectionConfigConstructorInfo;
		private static readonly ConstructorInfo paragraphSectionConfigConstructorInfo;
		private static readonly ConstructorInfo tableSectionConfigConstructorInfo;
		private static readonly ConstructorInfo featureConfigConstructorInfo;

		private static readonly Dictionary<Type, ConstructorInfo> sectionConstructorsByType;
		private static readonly Dictionary<string, ConstructorInfo> sectionConstructorsByName;

		public static readonly ConstructorDetails CardSetConfigConstructor;
		public static readonly ConstructorDetails CardConfigConstructor;
		//public static readonly ConstructorDetails SectionConfigConstructor;
		public static readonly ConstructorDetails DynamicSectionConfigConstructor;
		public static readonly ConstructorDetails TextSectionConfigConstructor;
		public static readonly ConstructorDetails ParagraphSectionConfigConstructor;
		public static readonly ConstructorDetails TableSectionConfigConstructor;
		public static readonly ConstructorDetails FeatureConfigConstructor;

		public static readonly ITypeDetailsCollection ConfigConstructors;
		public static readonly ITypeDetailsCollection SectionConfigConstructors;

		public static readonly ConstructorDetails OutlineConstructor;
		public static readonly ConstructorDetails HeaderConstructor;

		private static readonly Dictionary<Type, ConstructorDetails> cardConfigConstructorsByType;
		public static readonly Dictionary<string, ConstructorDetails> cardConfigConstructorsByName;
		public static readonly Dictionary<string, ConstructorDetails> cardSectionConfigConstructorsByName;

		/// <summary></summary>
		/// <exception cref="TypeInitializationException"></exception>
		static CardSetConfigFactory() {
			cardSetConfigConstructorInfo = typeof(CardSetConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(CardSetConfig)} constructor.", null);
			cardConfigConstructorInfo = typeof(CardConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(CardConfig)} constructor.", null);
			//sectionConfigConstructorInfo = typeof(CardSectionConfig).GetConstructors().First();
			dynamicSectionConfigConstructorInfo = typeof(DynamicCardSectionConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(DynamicCardSectionConfig)} constructor.", null);
			textSectionConfigConstructorInfo = typeof(TextCardSectionConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(TextCardSectionConfig)} constructor.", null);
			paragraphSectionConfigConstructorInfo = typeof(ParagraphCardSectionConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(ParagraphCardSectionConfig)} constructor.", null);
			tableSectionConfigConstructorInfo = typeof(TableCardSectionConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(TableCardSectionConfig)} constructor.", null);
			featureConfigConstructorInfo = typeof(CardFeatureConfig).GetConstructors().FirstOrDefault() ?? throw new TypeInitializationException($"Cannot find {nameof(CardFeatureConfig)} constructor.", null);

			CardSetConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(CardSetConfig), cardSetConfigConstructorInfo, "CardSetConfig");
			CardConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(CardConfig), cardConfigConstructorInfo, "CardConfig");
			//SectionConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(CardSectionConfig), sectionConfigConstructorInfo, "CardSection");
			DynamicSectionConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(DynamicCardSectionConfig), dynamicSectionConfigConstructorInfo, "CardSection");
			TextSectionConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(TextCardSectionConfig), textSectionConfigConstructorInfo, "CardTextSection");
			ParagraphSectionConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(ParagraphCardSectionConfig), paragraphSectionConfigConstructorInfo, "CardParagraphSection");
			TableSectionConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(TableCardSectionConfig), tableSectionConfigConstructorInfo, "CardTableSection");
			FeatureConfigConstructor = DocumentationGenerator.GetConstructorDetails(typeof(CardFeatureConfig), featureConfigConstructorInfo, "Feature");

			sectionConstructorsByType = new Dictionary<Type, ConstructorInfo>() {
				{ typeof(DynamicCardSectionConfig), dynamicSectionConfigConstructorInfo },
				{ typeof(TextCardSectionConfig), textSectionConfigConstructorInfo },
				{ typeof(ParagraphCardSectionConfig), paragraphSectionConfigConstructorInfo },
				{ typeof(TableCardSectionConfig), tableSectionConfigConstructorInfo }
			};
			sectionConstructorsByName = new Dictionary<string, ConstructorInfo>(SharpDocuments.StringComparer) {
				{ DynamicSectionConfigConstructor.Name, dynamicSectionConfigConstructorInfo },
				{ TextSectionConfigConstructor.Name, textSectionConfigConstructorInfo },
				{ ParagraphSectionConfigConstructor.Name, paragraphSectionConfigConstructorInfo },
				{ TableSectionConfigConstructor.Name, tableSectionConfigConstructorInfo }
			};

			ConstructorDetails baseDivConstructor = WidgetFactory.DivConstructor;
			ArgumentDetails[] configDivArgs = ConditionArgument.Yield().Concat(baseDivConstructor.Arguments).ToArray();
			OutlineConstructor = new ConstructorDetails(typeof(SharpWidget), typeof(SharpWidget), "Outline", "Outline", configDivArgs, new DocumentationString("Outline description."), new SharpSheets.Layouts.Rectangle(0f, 0f), null);
			HeaderConstructor = new ConstructorDetails(typeof(SharpWidget), typeof(SharpWidget), "Header", "Header", configDivArgs, new DocumentationString("Header description."), new SharpSheets.Layouts.Rectangle(0f, 0f), null);

			ConfigConstructors = new TypeDetailsCollection(
				new ConstructorDetails[] {
					CardSetConfigConstructor,
					CardConfigConstructor,
					//SectionConfigConstructor,
					DynamicSectionConfigConstructor,
					TextSectionConfigConstructor,
					ParagraphSectionConfigConstructor,
					TableSectionConfigConstructor,
					FeatureConfigConstructor,
					OutlineConstructor,
					HeaderConstructor
				}, SharpDocuments.StringComparer);

			SectionConfigConstructors = new TypeDetailsCollection(
				new ConstructorDetails[] {
					//SectionConfigConstructor,
					DynamicSectionConfigConstructor,
					TextSectionConfigConstructor,
					ParagraphSectionConfigConstructor,
					TableSectionConfigConstructor
				}, SharpDocuments.StringComparer);

			cardConfigConstructorsByType = new Dictionary<Type, ConstructorDetails>() {
				{ typeof(CardSetConfig), CardSetConfigConstructor },
				{ typeof(CardConfig), CardConfigConstructor },
				//{ typeof(CardSectionConfig), SectionConfigConstructor },
				{ typeof(DynamicCardSectionConfig), DynamicSectionConfigConstructor },
				{ typeof(TextCardSectionConfig), TextSectionConfigConstructor },
				{ typeof(ParagraphCardSectionConfig), ParagraphSectionConfigConstructor },
				{ typeof(TableCardSectionConfig), TableSectionConfigConstructor },
				{ typeof(CardFeatureConfig), FeatureConfigConstructor }
			};
			cardConfigConstructorsByName = new Dictionary<string, ConstructorDetails>(
				cardConfigConstructorsByType.ToDictionary(kv => kv.Value.Name, kv => kv.Value),
				SharpDocuments.StringComparer) {
				{OutlineConstructor.Name, OutlineConstructor },
				{HeaderConstructor.Name, HeaderConstructor }
			};
			cardSectionConfigConstructorsByName = new Dictionary<string, ConstructorDetails>(
				SectionConfigConstructors.ToDictionary(c => c.Name, c => c),
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
				Conditional<IContext> outlineEntry = MakeCondition(outline, outline, outlinesVariables, errors);
				cardSetConfig.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // TODO Yes?
				DryRunRect(outline, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext header in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, HeaderConstructor.Name))) {
				Conditional<IContext> headerEntry = MakeCondition(header, header, outlinesVariables, errors);
				cardSetConfig.headers.Add(headerEntry);
				if (origins != null) { origins.Add(header, header); } // TODO Yes?
				DryRunRect(header, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext section in context.Children.Where(c => cardSectionConfigConstructorsByName.ContainsKey(c.SimpleName))) {
				AbstractCardSectionConfig? cardSection = MakeSection(section, cardSetConfig, origins, errors);

				if (cardSection != null) {
					Conditional<AbstractCardSectionConfig> sectionConditional = MakeCondition(section, cardSection, cardSection.Variables, errors);
					cardSetConfig.cardSections.Add(sectionConditional);
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
				Conditional<IContext> outlineEntry = MakeCondition(outline, outline, outlinesVariables, errors);
				cardConfig.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // TODO Yes?
				DryRunRect(outline, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext header in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, HeaderConstructor.Name))) {
				Conditional<IContext> headerEntry = MakeCondition(header, header, outlinesVariables, errors);
				cardConfig.headers.Add(headerEntry);
				if (origins != null) { origins.Add(header, header); } // TODO Yes?
				DryRunRect(header, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext section in context.Children.Where(c => cardSectionConfigConstructorsByName.ContainsKey(c.SimpleName))) {
				AbstractCardSectionConfig? cardSection = MakeSection(section, cardConfig, origins, errors);

				if (cardSection != null) {
					Conditional<AbstractCardSectionConfig> sectionConditional = MakeCondition(section, cardSection, cardSection.Variables, errors);
					cardConfig.cardSections.Add(sectionConditional);
				}
			}

			return cardConfig;
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		private AbstractCardSectionConfig? MakeSection(IContext context, ICardSectionParent parent, Dictionary<object, IDocumentEntity>? origins, List<SharpParsingException> errors) {
			//ConstructorInfo constructorInfo = sectionConstructorsByType[typeof(T)];
			ConstructorInfo constructorInfo = sectionConstructorsByName.GetValueOrFallback(context.SimpleName, null) ?? throw new InvalidOperationException($"Could not find section constructor for \"{context.SimpleName}\"");
			Type sectionType = constructorInfo.DeclaringType ?? throw new InvalidOperationException("Could not resolve corresponding card section constructor.");

			AbstractCardSectionConfig? cardSection;

			try {
				List<object> requiredArgs = new List<object>() { parent };
				if (sectionType == typeof(TextCardSectionConfig) || sectionType == typeof(ParagraphCardSectionConfig)) {
					IVariableBox featureVariables = BasisEnvironment.Instance.AppendVariables(CardFeatureEnvironments.BaseDefinitions);
					TextExpression? content = MakeTextProperty("content", context, featureVariables, errors);
					requiredArgs.Add(content ?? new TextExpression(""));
				}
				if(sectionType == typeof(TextCardSectionConfig)) {
					IVariableBox sectionVariables = BasisEnvironment.Instance.AppendVariables(CardSectionEnvironments.BaseDefinitions);
					TextExpression? delimiter = MakeTextProperty("delimiter", context, sectionVariables, errors);
					requiredArgs.Add(delimiter ?? new TextExpression(""));
					TextExpression? prefix = MakeTextProperty("prefix", context, sectionVariables, errors);
					requiredArgs.Add(prefix ?? new TextExpression(""));
					TextExpression? tail = MakeTextProperty("tail", context, sectionVariables, errors);
					requiredArgs.Add(tail ?? new TextExpression(""));
				}
				cardSection = (AbstractCardSectionConfig)SharpFactory.Construct(constructorInfo, context, parent.Source, widgetFactory, shapeFactory, requiredArgs.ToArray(), out SharpParsingException[] cardSectionBuildErrors);
				errors.AddRange(cardSectionBuildErrors);
			}
			catch (SharpParsingException e) {
				cardSection = null;
				errors.Add(e);
			}
			catch (SharpFactoryException e) {
				cardSection = null;
				errors.AddRange(e.Errors);
			}

			if (cardSection == null) { return null; }

			if (origins != null) { origins.Add(cardSection, context); }

			IVariableBox definitionVariables = BasisEnvironment.Instance.AppendVariables(cardSection.Variables);

			// If not dynamic, or is dynamic and not always included
			// AlwaysInclude sections cannot have their own Definitions specified
			if (cardSection is not DynamicCardSectionConfig dynamic || !dynamic.AlwaysInclude) { 
				foreach (ContextValue<string> definitionValue in context.GetDefinitions(context)) {
					try {
						Definition definition = Definition.Parse(definitionValue.Value, definitionVariables);
						cardSection.definitions.Add(definition);
					}
					catch (Exception e) {
						errors.Add(new SharpParsingException(definitionValue.Location, e.Message, e));
					}
				}
			}

			/*
			IEnvironment sectionEnvironment = CardSectionEnvironments.GetDryRun(cardSection);
			foreach (IContext outline in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, OutlineConstructor.Name))) {
				Conditional<IContext> outlineEntry = MakeCondition(outline, outline, cardSection.Variables, errors);
				cardSection.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // Yes?
				DryRunRect(outline, sectionEnvironment, cardConfig.source, errors);
			}
			*/

			IVariableBox outlinesVariables = CardSectionOutlineEnvironments.GetVariables(cardSection);
			IEnvironment outlinesDryRunEnvironment = CardSectionOutlineEnvironments.GetDryRun(cardSection);
			foreach (IContext outline in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, OutlineConstructor.Name))) {
				Conditional<IContext> outlineEntry = MakeCondition(outline, outline, outlinesVariables, errors);
				cardSection.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // TODO Yes?
				DryRunRect(outline, outlinesDryRunEnvironment, parent.Source, errors);
			}

			if (cardSection is DynamicCardSectionConfig featuredSection) {
				foreach (IContext feature in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, FeatureConfigConstructor.Name))) {
					CardFeatureConfig? cardFeature = MakeFeature(feature, featuredSection, parent.Source, origins, errors);

					if (cardFeature is not null) {
						Conditional<CardFeatureConfig> sectionFeatureConditional = MakeCondition(feature, cardFeature, cardFeature.Variables, errors);
						featuredSection.cardFeatures.Add(sectionFeatureConditional);

						DryRunRect(feature, CardFeatureEnvironments.GetDryRun(cardFeature), parent.Source, errors);
					}
				}

				if(featuredSection.cardFeatures.Count == 0) {
					errors.Add(new SharpParsingException(context.Location, "Section must have at least one valid feature."));
					return null;
				}

				/*
				if(featuredSection.cardFeatures.Count == 1 && !featuredSection.cardFeatures.First().Condition.IsTrue && origins.TryGetValue(featuredSection.cardFeatures.First().Value, out IDocumentEntity featureEntity)) {
					errors.Add(new SharpParsingException(featureEntity.Location, "Only one feature is provided, and it has a condition."));
				}
				*/
			}

			return cardSection;
		}

		private CardFeatureConfig? MakeFeature(IContext context, DynamicCardSectionConfig sectionConfig, DirectoryPath source, Dictionary<object, IDocumentEntity>? origins, List<SharpParsingException> errors) {
			CardFeatureConfig? cardFeature;

			try {
				cardFeature = (CardFeatureConfig)SharpFactory.Construct(featureConfigConstructorInfo, context, source, widgetFactory, shapeFactory, new object[] { sectionConfig, context }, out SharpParsingException[] cardFeatureBuildErrors);
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

			IVariableBox definitionVariables = BasisEnvironment.Instance.AppendVariables(cardFeature.Variables);

			foreach (ContextValue<string> definitionValue in context.GetDefinitions(context)) {
				try {
					Definition definition = Definition.Parse(definitionValue.Value, definitionVariables);
					cardFeature.definitions.Add(definition);
				}
				catch (Exception e) {
					errors.Add(new SharpParsingException(definitionValue.Location, e.Message, e));
				}
			}

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

		private static void CheckCondition(IContext context, IVariableBox variables, List<SharpParsingException> errors) {
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

		private void DryRunRect(IContext context, IEnvironment environment, DirectoryPath source, List<SharpParsingException> errors) {
			CheckCondition(context, environment, errors);
			foreach(IContext child in context.TraverseChildren(true)) {
				CheckCondition(child, environment, errors);
			}

			try {
				widgetFactory.MakeWidget(typeof(Div), new InterpolateContext(context, environment, false), source, out SharpParsingException[] rectErrors);
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
			new DocumentationString("A boolean expression used to determine if this part of the configuration should be used, based on the card subject data. If no expression is provided, it is assumed to be true."),
			ArgumentType.Simple(typeof(BoolExpression)), true, true, "True", new BoolExpression(true), null);
		private static ConstructorDetails MakeConfigConstructor(ConstructorDetails constructor) {
			if(constructor.Arguments.Length > 0 && constructor.Arguments[0].Name == ConditionArgument.Name && constructor.Arguments[0].Description == ConditionArgument.Description && constructor.Arguments[0].Type == ConditionArgument.Type) {
				return constructor;
			}
			else if(constructor.DeclaringType == typeof(CardConfig)) {
				return constructor; // The top-level card config has no condition
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

}
