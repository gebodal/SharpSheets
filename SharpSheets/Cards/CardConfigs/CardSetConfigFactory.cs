using SharpSheets.Evaluations;
using SharpSheets.Shapes;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Cards.Definitions;
using SharpSheets.Documentation;
using System.Collections;
using SharpSheets.Cards.CardSubjects;
using SharpSheets.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Cards.CardConfigs {

	[Factory(typeof(CardSetConfig), new Type[] { typeof(string), typeof(FilePath), typeof(DirectoryPath) }, new string[] { "configName", "origin", "source" }, new bool[] { true, true, true }, typeof(CardSetConfig), IncludeDocs = false)]
	[Factory(typeof(CardConfig), new Type[] { typeof(CardSetConfig) }, new string[] { "cardSetConfig" }, new bool[] { true }, typeof(CardConfig), IncludeDocs = false)]
	[Factory(typeof(DynamicCardSegmentConfig), new Type[] { typeof(ICardSegmentParent) }, new string[] { "parent" }, new bool[] { true }, typeof(DynamicCardSegmentConfig), IncludeDocs = false)]
	[Factory(typeof(TextCardSegmentConfig), new Type[] { typeof(ICardSegmentParent), typeof(IExpression<string>), typeof(IExpression<string>), typeof(IExpression<string>), typeof(IExpression<string>) }, new string[] { "parent", "content", "delimiter", "prefix", "tail" }, new bool[] { true, false, false, false, false }, typeof(TextCardSegmentConfig), IncludeDocs = false)]
	[Factory(typeof(ParagraphCardSegmentConfig), new Type[] { typeof(ICardSegmentParent), typeof(IExpression<string>) }, new string[] { "parent", "content" }, new bool[] { true, false }, typeof(ParagraphCardSegmentConfig), IncludeDocs = false)]
	[Factory(typeof(TableCardSegmentConfig), new Type[] { typeof(ICardSegmentParent) }, new string[] { "parent" }, new bool[] { true }, typeof(TableCardSegmentConfig), IncludeDocs = false)]
	[Factory(typeof(CardFeatureConfig), new Type[] { typeof(AbstractCardSegmentConfig) }, new string[] { "cardSegment" }, new bool[] { true }, typeof(CardFeatureConfig), IncludeDocs = false)]
	public partial class CardSetConfigFactory : ITypeDetailsCollection {

		private readonly WidgetFactory widgetFactory;
		private readonly ShapeFactory shapeFactory;
		private readonly IFileReader fileReader;

		public CardSetConfigFactory(WidgetFactory widgetFactory, ShapeFactory shapeFactory, IFileReader fileReader) {
			this.widgetFactory = widgetFactory;
			this.shapeFactory = shapeFactory;
			this.fileReader = fileReader;
		}

		#region Static Initialisation and Accessors

		public static readonly BuilderDetails CardSetConfigBuilder;
		public static readonly BuilderDetails CardConfigBuilder;
		public static readonly BuilderDetails DynamicSegmentConfigBuilder;
		public static readonly BuilderDetails TextSegmentConfigBuilder;
		public static readonly BuilderDetails ParagraphSegmentConfigBuilder;
		public static readonly BuilderDetails TableSegmentConfigBuilder;
		public static readonly BuilderDetails FeatureConfigBuilder;

		public static readonly ITypeDetailsCollection ConfigBuilders;
		public static readonly ITypeDetailsCollection SegmentConfigBuilders;

		public static readonly BuilderDetails BackgroundBuilder;
		public static readonly BuilderDetails OutlineBuilder;

		private static readonly Dictionary<Type, BuilderDetails> cardConfigBuildersByType;
		public static readonly Dictionary<string, BuilderDetails> cardConfigBuildersByName;
		public static readonly Dictionary<string, BuilderDetails> cardSegmentConfigBuildersByName;

		/// <summary></summary>
		/// <exception cref="TypeInitializationException"></exception>
		static CardSetConfigFactory() {
			BuilderDetails baselineCardConfigBuilder = BuilderDocs.SharpSheets_Cards_CardConfigs_CardConfig; // CardConfigBuilder;

			CardSetConfigBuilder = BuilderDocs.SharpSheets_Cards_CardConfigs_CardSetConfig; // CardSetConfigBuilder;
			CardConfigBuilder = BuilderDocs.SharpSheets_Cards_CardConfigs_CardConfig.WithAdditionalArguments(ConditionArgument);
			DynamicSegmentConfigBuilder = BuilderDocs.SharpSheets_Cards_CardConfigs_DynamicCardSegmentConfig.WithAdditionalArguments(ConditionArgument);
			TextSegmentConfigBuilder = BuilderDocs.SharpSheets_Cards_CardConfigs_TextCardSegmentConfig.WithAdditionalArguments(ConditionArgument);
			ParagraphSegmentConfigBuilder = BuilderDocs.SharpSheets_Cards_CardConfigs_ParagraphCardSegmentConfig.WithAdditionalArguments(ConditionArgument);
			TableSegmentConfigBuilder = BuilderDocs.SharpSheets_Cards_CardConfigs_TableCardSegmentConfig.WithAdditionalArguments(ConditionArgument);
			FeatureConfigBuilder = BuilderDocs.SharpSheets_Cards_CardConfigs_CardFeatureConfig.WithAdditionalArguments(ConditionArgument);

			// Append non-conflicting card config arguments to card set config builder, for clearer documentation
			CardSetConfigBuilder = CardSetConfigBuilder.WithAdditionalArguments(baselineCardConfigBuilder.Arguments.Where(cardArg => !cardArg.UseLocal && !CardSetConfigBuilder.Arguments.Any(cardSetArg => SharpDocuments.StringEquals(cardArg.Name, cardSetArg.Name))));

			BuilderDetails baseDivBuilder = WidgetFactory.DivBuilder;
			ArgumentDetails[] configDivArgs = ConditionArgument.Yield().Concat(baseDivBuilder.Arguments).ToArray();
			BackgroundBuilder = new BuilderDetails(typeof(SharpWidget), typeof(SharpWidget), "Background", "Background", configDivArgs,
				new DocumentationString("This element contains the content to be drawn as the card background, behind all other card content."),
				new SharpSheets.Layouts.Rectangle(0f, 0f), null);
			OutlineBuilder = new BuilderDetails(typeof(SharpWidget), typeof(SharpWidget), "Outline", "Outline", configDivArgs,
				new DocumentationString("This element contains the content to be drawn as an outline/background for a card element, behind that element's main content."),
				new SharpSheets.Layouts.Rectangle(0f, 0f), null);

			ConfigBuilders = new TypeDetailsCollection(
				new BuilderDetails[] {
					CardSetConfigBuilder,
					CardConfigBuilder,
					DynamicSegmentConfigBuilder,
					TextSegmentConfigBuilder,
					ParagraphSegmentConfigBuilder,
					TableSegmentConfigBuilder,
					FeatureConfigBuilder,
					BackgroundBuilder,
					OutlineBuilder
				}, SharpDocuments.StringComparer);

			SegmentConfigBuilders = new TypeDetailsCollection(
				new BuilderDetails[] {
					DynamicSegmentConfigBuilder,
					TextSegmentConfigBuilder,
					ParagraphSegmentConfigBuilder,
					TableSegmentConfigBuilder
				}, SharpDocuments.StringComparer);

			cardConfigBuildersByType = new Dictionary<Type, BuilderDetails>() {
				{ typeof(CardSetConfig), CardSetConfigBuilder },
				{ typeof(CardConfig), CardConfigBuilder },
				{ typeof(DynamicCardSegmentConfig), DynamicSegmentConfigBuilder },
				{ typeof(TextCardSegmentConfig), TextSegmentConfigBuilder },
				{ typeof(ParagraphCardSegmentConfig), ParagraphSegmentConfigBuilder },
				{ typeof(TableCardSegmentConfig), TableSegmentConfigBuilder },
				{ typeof(CardFeatureConfig), FeatureConfigBuilder }
			};
			cardConfigBuildersByName = new Dictionary<string, BuilderDetails>(
				cardConfigBuildersByType.ToDictionary(kv => kv.Value.Name, kv => kv.Value),
				SharpDocuments.StringComparer) {
				{BackgroundBuilder.Name, BackgroundBuilder },
				{OutlineBuilder.Name, OutlineBuilder }
			};
			cardSegmentConfigBuildersByName = new Dictionary<string, BuilderDetails>(
				SegmentConfigBuilders.ToDictionary(c => c.Name, c => c),
				SharpDocuments.StringComparer); ;
		}

		#endregion

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public CardSetConfig? MakeSetConfig(string configName, IContext context, IEnumerable<ContextValue<string>> archives, FilePath origin, DirectoryPath source, out ParseOrigins<IDocumentEntity> origins, out List<SharpParsingException> errors) {

			CardSetConfig? cardSetConfig;
			origins = new ParseOrigins<IDocumentEntity>();
			errors = new List<SharpParsingException>();

			try {
				cardSetConfig = Build_CardSetConfig(context, configName, origin, source, out SharpParsingException[] cardSetDefBuildErrors); // (CardSetConfig?)REMOVESharpFactory.Build(cardSetConfigBuilderInfo, context, source, widgetFactory, shapeFactory, new object[] { configName, origin, source }, out SharpParsingException[] cardSetDefBuildErrors);
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

			IVariableBox definitionVariables = CardEnvironments.Basis.AppendVariables(cardSetConfig.Variables);

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

			foreach (IContext background in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, BackgroundBuilder.Name))) {
				InterpolatedContext backgroundContext = MakeInterpolatedContext(background, outlinesVariables, errors);
				Conditional<InterpolatedContext> backgroundEntry = MakeCondition(background, backgroundContext, outlinesVariables, errors);
				cardSetConfig.backgrounds.Add(backgroundEntry);
				if (origins != null) { origins.Add(background, background); } // TODO Yes?
				DryRunParse(backgroundContext, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext outline in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, OutlineBuilder.Name))) {
				InterpolatedContext outlineContext = MakeInterpolatedContext(outline, outlinesVariables, errors);
				Conditional<InterpolatedContext> outlineEntry = MakeCondition(outline, outlineContext, outlinesVariables, errors);
				cardSetConfig.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // TODO Yes?
				DryRunParse(outlineContext, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext segment in context.Children.Where(c => cardSegmentConfigBuildersByName.ContainsKey(c.SimpleName))) {
				AbstractCardSegmentConfig? cardSegment = MakeSegment(segment, cardSetConfig, origins, errors);

				if (cardSegment != null) {
					Conditional<AbstractCardSegmentConfig> segmentConditional = MakeCondition(segment, cardSegment, cardSegment.Variables, errors);
					cardSetConfig.cardSetSegments.Add(segmentConditional);
				}
			}

			foreach (IContext card in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, CardConfigBuilder.Name))) {
				CardConfig? cardConfig = MakeConfig(card, cardSetConfig, origins, errors);

				if (cardConfig != null) {
					Conditional<CardConfig> cardConditional = MakeCondition(card, cardConfig, cardConfig.Variables, errors);
					cardSetConfig.cardConfigs.Add(cardConditional);
				}
			}

			if(cardSetConfig.cardConfigs.Count == 0) {
				// Create an empty card config to use as fallback
				IContext emptyCardContext = new EmptyChildContext(context, CardConfigBuilder.Name);
				CardConfig emptyCardConfig = MakeConfig(emptyCardContext, cardSetConfig, null, errors) ?? throw new InvalidOperationException("Failed to make backup empty card configuration.");
				cardSetConfig.cardConfigs.Add(new Conditional<CardConfig>(new BoolExpression(true, CardEnvironments.Context), emptyCardConfig));
				if (origins != null) { origins.Add(emptyCardConfig, context); }
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
				string[]? exampleNames = ParameterParsers.Parser_String1(examplesStr);
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
		private CardConfig? MakeConfig(IContext context, CardSetConfig cardSetConfig, ParseOrigins<IDocumentEntity>? origins, List<SharpParsingException> errors) {

			CardConfig? cardConfig;

			try {
				cardConfig = Build_CardConfig(context, cardSetConfig, cardSetConfig.Source, shapeFactory, out SharpParsingException[] cardDefBuildErrors); // (CardConfig?)REMOVESharpFactory.Build(cardConfigBuilderInfo, context, cardSetConfig.Source, widgetFactory, shapeFactory, new object[] { cardSetConfig }, out SharpParsingException[] cardDefBuildErrors);
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

			IVariableBox definitionVariables = CardEnvironments.Basis.AppendVariables(cardConfig.Variables);

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

			foreach (IContext background in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, BackgroundBuilder.Name))) {
				InterpolatedContext backgroundContext = MakeInterpolatedContext(background, outlinesVariables, errors);
				Conditional<InterpolatedContext> backgroundEntry = MakeCondition(background, backgroundContext, outlinesVariables, errors);
				cardConfig.backgrounds.Add(backgroundEntry);
				if (origins != null) { origins.Add(background, background); } // TODO Yes?
				DryRunParse(backgroundContext, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext outline in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, OutlineBuilder.Name))) {
				InterpolatedContext outlineContext = MakeInterpolatedContext(outline, outlinesVariables, errors);
				Conditional<InterpolatedContext> outlineEntry = MakeCondition(outline, outlineContext, outlinesVariables, errors);
				cardConfig.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // TODO Yes?
				DryRunParse(outlineContext, outlinesDryRunEnvironment, cardSetConfig.Source, errors);
			}

			foreach (IContext segment in context.Children.Where(c => cardSegmentConfigBuildersByName.ContainsKey(c.SimpleName))) {
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
		private AbstractCardSegmentConfig? MakeSegment(IContext context, ICardSegmentParent parent, ParseOrigins<IDocumentEntity>? origins, List<SharpParsingException> errors) {
			
			AbstractCardSegmentConfig? cardSegment;
			try {
				SharpParsingException[] cardSegmentBuildErrors;
				if (SharpDocuments.StringComparer.Equals(context.SimpleName, DynamicSegmentConfigBuilder.FullName)) {
					cardSegment = Build_DynamicCardSegmentConfig(context, parent, parent.Source, out cardSegmentBuildErrors);
				}
				else if (SharpDocuments.StringComparer.Equals(context.SimpleName, TableSegmentConfigBuilder.FullName)) {
					cardSegment = Build_TableCardSegmentConfig(context, parent, parent.Source, out cardSegmentBuildErrors);
				}
				else {
					// Text or Paragraphs segment
					IVariableBox featureVariables = CardFeatureEnvironments.GetVariables(parent);
					TextExpression? content = MakeTextProperty("content", context, featureVariables, errors);

					if (SharpDocuments.StringComparer.Equals(context.SimpleName, TextSegmentConfigBuilder.FullName)) {
						IVariableBox segmentVariables = CardSegmentEnvironments.GetVariables(parent); // BasisEnvironment.Instance.AppendVariables(CardSegmentEnvironments.BaseDefinitions);
						TextExpression? delimiter = MakeTextProperty("delimiter", context, segmentVariables, errors);
						TextExpression? prefix = MakeTextProperty("prefix", context, segmentVariables, errors);
						TextExpression? tail = MakeTextProperty("tail", context, segmentVariables, errors);

						cardSegment = Build_TextCardSegmentConfig(context, parent, content, delimiter, prefix, tail, parent.Source, out cardSegmentBuildErrors);
					}
					else if (SharpDocuments.StringComparer.Equals(context.SimpleName, ParagraphSegmentConfigBuilder.FullName)) {
						cardSegment = Build_ParagraphCardSegmentConfig(context, parent, content, parent.Source, out cardSegmentBuildErrors);
					}
					else {
						throw new InvalidOperationException($"Could not find segment builder for \"{context.SimpleName}\"");
					}
				}

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

			IVariableBox definitionVariables = CardEnvironments.Basis.AppendVariables(cardSegment.Variables);

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

			IVariableBox outlinesVariables = CardSegmentOutlineEnvironments.GetVariables(cardSegment);
			IEnvironment outlinesDryRunEnvironment = CardSegmentOutlineEnvironments.GetDryRun(cardSegment);
			foreach (IContext outline in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, OutlineBuilder.Name))) {
				InterpolatedContext outlineContext = MakeInterpolatedContext(outline, outlinesVariables, errors);
				Conditional<InterpolatedContext> outlineEntry = MakeCondition(outline, outlineContext, outlinesVariables, errors);
				cardSegment.outlines.Add(outlineEntry);
				if (origins != null) { origins.Add(outline, outline); } // TODO Yes?
				DryRunParse(outlineContext, outlinesDryRunEnvironment, parent.Source, errors);
			}

			if (cardSegment is DynamicCardSegmentConfig featuredSegment) {
				foreach (IContext feature in context.Children.Where(c => SharpDocuments.StringComparer.Equals(c.SimpleName, FeatureConfigBuilder.Name))) {
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
			}

			return cardSegment;
		}

		private CardFeatureConfig? MakeFeature(IContext context, DynamicCardSegmentConfig segmentConfig, DirectoryPath source, ParseOrigins<IDocumentEntity>? origins, List<SharpParsingException> errors) {
			CardFeatureConfig? cardFeature;
			
			try {
				cardFeature = Build_CardFeatureConfig(context, segmentConfig, source, out SharpParsingException[] cardFeatureBuildErrors); // (CardFeatureConfig?)REMOVESharpFactory.Build(featureConfigBuilderInfo, context, source, widgetFactory, shapeFactory, new object[] { segmentConfig }, out SharpParsingException[] cardFeatureBuildErrors);
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

			IVariableBox featureVariables = CardEnvironments.Basis.AppendVariables(cardFeature.Variables);

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
				return new Conditional<T>(new BoolExpression(false, CardEnvironments.Context), value);
			}

			variables = CardEnvironments.Basis.AppendVariables(variables);

			string? conditionStr = context.GetProperty(ConditionArgument.Name, true, context, null, out DocumentSpan? location);

			if(conditionStr == null) {
				return new Conditional<T>(new BoolExpression(true, CardEnvironments.Context), value);
			}

			try {
				BoolExpression condition = BoolExpression.Parse(conditionStr, variables);
				return new Conditional<T>(condition, value);
			}
			catch(Exception e) {
				errors.Add(new SharpParsingException(location, e.Message, e));
				return new Conditional<T>(new BoolExpression(false, CardEnvironments.Context), value);
			}
		}

		private static InterpolatedContext MakeInterpolatedContext(IContext context, IVariableBox variables, List<SharpParsingException> errors) {
			InterpolatedContext result = InterpolatedContext.Parse(context, variables, true, out SharpParsingException[] contextErrors, out _);
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

		private void DryRunParse(InterpolatedContext context, IEnvironment environment, DirectoryPath source, List<SharpParsingException> errors) {
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
			ArgumentType.Simple(typeof(BoolExpression)), true, true, "True", new BoolExpression(true, CardEnvironments.Context), null);

		public static readonly ArgumentDetails ForEachArgument = new ArgumentDetails(
			"foreach",
			new DocumentationString("A for-each expression, which will cause this element to be repeated for each " +
				"entry of a specified array of values, with each of those entries available as a variable in the " +
				"corresponding repetition. Must be of the pattern \"loopVar in arrayExpr\", where \"arrayExpr\" is " +
				"an expression which evaluates to an array, and \"loopVar\" is the name to use for the loop variable."),
			ArgumentType.Simple(typeof(ContextForEach)), true, true, null, null, null);

		private static BuilderDetails MakeConfigBuilder(BuilderDetails builder) {
			if (builder.DeclaringType == typeof(CardSetConfig)) {
				return builder; // The top-level card set config has no condition
			}
			else if (typeof(IWidget).IsAssignableFrom(builder.DeclaringType) && builder != OutlineBuilder && builder != BackgroundBuilder) {
				if(builder.Arguments.Length > 1 && ArgumentComparer.Instance.Equals(builder.Arguments[0], ConditionArgument) && ArgumentComparer.Instance.Equals(builder.Arguments[1], ForEachArgument)) {
					return builder;
				}
				else {
					return builder.WithArgument(ForEachArgument, 0).WithArgument(ConditionArgument, 0);
				}
			}
			else if(builder.Arguments.Length > 0 && ArgumentComparer.Instance.Equals(builder.Arguments[0], ConditionArgument)) {
				return builder;
			}
			else {
				return builder.WithArgument(ConditionArgument, 0);
			}
		}

		public bool ContainsKey(Type type) {
			return widgetFactory.ContainsKey(type) || cardConfigBuildersByType.ContainsKey(type);
		}

		public bool ContainsKey(string name) {
			return widgetFactory.ContainsKey(name) || cardConfigBuildersByName.ContainsKey(name);
		}

		public bool TryGetValue(Type type, [MaybeNullWhen(false)] out BuilderDetails builder) {
			if(widgetFactory.TryGetValue(type, out BuilderDetails? widgetBuilder)) {
				builder = MakeConfigBuilder(widgetBuilder);
				return true;
			}
			else if(cardConfigBuildersByType.TryGetValue(type, out BuilderDetails? cardBuilder)) {
				builder = cardBuilder;
				return true;
			}
			else {
				builder = null;
				return false;
			}
		}

		public bool TryGetValue(string name, [MaybeNullWhen(false)] out BuilderDetails builder) {
			if (widgetFactory.TryGetValue(name, out BuilderDetails? widgetBuilder)) {
				builder = MakeConfigBuilder(widgetBuilder);
				return true;
			}
			else if (cardConfigBuildersByName.TryGetValue(name, out BuilderDetails? cardBuilder)) {
				builder = cardBuilder;
				return true;
			}
			else {
				builder = null;
				return false;
			}
		}

		public IEnumerator<BuilderDetails> GetEnumerator() {
			return new BuilderDetailsUniqueNameEnumerator(
				widgetFactory.Select(c => MakeConfigBuilder(c)).Concat(cardConfigBuildersByName.Values),
				SharpDocuments.StringComparer);
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public IEnumerable<KeyValuePair<string, BuilderDetails>> GetBuilderNames() {
			return widgetFactory.GetBuilderNames()
				.Select(kv => new KeyValuePair<string, BuilderDetails>(kv.Key, MakeConfigBuilder(kv.Value)))
				.Concat(cardConfigBuildersByName);
		}

		#endregion
	}

}
