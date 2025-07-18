using SharpSheets.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpEditor.Utilities;
using SharpSheets.Documentation;
using SharpEditor.ContentBuilders;
using SharpSheets.Markup.Patterns;
using SharpSheets.Markup.Parsing;
using SharpSheets.Markup.Elements;
using SharpSheets.Cards.CardConfigs;
using SharpEditor.DataManagers;
using static SharpEditor.ContentBuilders.BaseContentBuilder;
using static SharpEditor.Documentation.DocumentationBuilders.BaseDocumentationBuilder;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using SharpEditor.Parsing.ParsingState;
using SharpEditor.Parsing;

namespace SharpEditor.Documentation.DocumentationBuilders {

	// UIElement -> Control
	// FrameworkElement -> Control

	public static class DocumentationPageBuilder {

		public static DocumentationPage CreateHomePage(DocumentationWindow window) {
			return CreateDocumentationPageForNode(Documentation.DocumentationRoot, window);
		}

		public static DocumentationPage CreateDocumentationPageForNode(DocumentationNode documentationNode, DocumentationWindow window) {
			return MakePage(
				CreateDocumentationContentForNode(documentationNode, window),
				documentationNode.name,
				() => CreateDocumentationContentForNode(documentationNode, window));
		}

		private static Control CreateDocumentationContentForNode(DocumentationNode documentationNode, DocumentationWindow window) {

			StackPanel stack = new StackPanel();
			
			if (documentationNode.HasSubsections && GetContents(documentationNode, window) is Control contentsElement) {
				TextBlock contentsTitleBlock = MakeTitleBlock("Contents", 2);
				stack.Children.Add(contentsTitleBlock);

				contentsElement.Margin = SectionMargin;
				contentsElement.AddIndent(45);
				stack.Children.Add(contentsElement);
			}

			if (documentationNode.main != null) {
				stack.Children.AddRange(CreateDocumentationPageElements(documentationNode.main, window));
			}
			
			return stack;
		}

		private static TextBlock MakeContentsTextBlock(Inline inline) {
			TextBlock contentsBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1), Inlines = new InlineCollection() };
			contentsBlock.Inlines.Add(new Run("\u2013\u2002")); // { Foreground = Brushes.Gray });
			contentsBlock.Inlines.Add(inline);
			return contentsBlock;
		}

		private static Control? GetContents(DocumentationNode documentationNode, DocumentationWindow window) {
			StackPanel stack = new StackPanel() { Margin = ContentsBlockMargin };

			foreach (DocumentationNode node in documentationNode.nodes.Values.OrderBy(n => n.index)) {
				ClickableRun nodeClickable = new ClickableRun(node.name);
				nodeClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(node);
				TextBlock nodeBlock = MakeContentsTextBlock(nodeClickable);
				stack.Children.Add(nodeBlock);

				if (GetContents(node, window) is Control uiElement) {
					stack.Children.Add(uiElement);
				}
			}

			foreach (DocumentationFile file in documentationNode.files.Values.OrderBy(f => f.index)) {
				ClickableRun fileClickable = new ClickableRun(file.title);
				fileClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(file);
				TextBlock fileBlock = MakeContentsTextBlock(fileClickable);
				stack.Children.Add(fileBlock);
			}

			return stack.Children.Count > 0 ? stack : null;
		}

		public static DocumentationPage CreateDocumentationPage(DocumentationFile documentationFile, DocumentationWindow window) {
			return MakePage(
				CreateDocumentationPageContents(documentationFile, window),
				documentationFile.title,
				() => CreateDocumentationPageContents(documentationFile, window));
		}

		private static Control CreateDocumentationPageContents(DocumentationFile documentationFile, DocumentationWindow window) {
			StackPanel stack = new StackPanel();
			stack.Children.AddRange(CreateDocumentationPageElements(documentationFile, window));
			return stack;
		}

		private static IEnumerable<Control> CreateDocumentationPageElements(DocumentationFile documentationFile, DocumentationWindow window) {

			List<Control> contents = new List<Control>();

			foreach (DocumentationSection section in documentationFile.sections) {
				Control sectionElement = CreateDocumentationSection(section, window);
				contents.Add(sectionElement);
			}

			return contents;
		}

		private static Control CreateDocumentationSection(DocumentationSection documentationSection, DocumentationWindow window) {
			StackPanel stack = new StackPanel() { Margin = SectionMargin };

			TextBlock titleBlock = MakeTitleBlock(documentationSection.title, documentationSection.level);

			stack.Children.Add(titleBlock);

			foreach (IDocumentationSegment segment in documentationSection.segments) {
				Control segmentElement = CreateDocumentationSegment(segment, window);
				segmentElement.AddIndent(10);
				stack.Children.Add(segmentElement);
			}

			return stack;
		}

		private static Control CreateDocumentationSegment(IDocumentationSegment documentationSegment, DocumentationWindow window) {
			if (documentationSegment is DocumentationParagraph paragraph) {
				return MakeDocumentationParagraph(paragraph, window);
			}
			else if (documentationSegment is DocumentationContents contents) {
				return contents.contents switch {
					// Widgets
					DocumentationSectionContents.Widgets => GetBuilderLinks(SharpEditorRegistries.WidgetFactoryInstance, SharpEditorRegistries.WidgetFactoryInstance.Get, window),
					// Shapes
					DocumentationSectionContents.Shapes => GetBuilderLinks(SharpEditorRegistries.ShapeFactoryInstance, SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.Boxes => GetBuilderLinks(SharpEditorRegistries.ShapeFactoryInstance.FindBuilders<IBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.LabelledBoxes => GetBuilderLinks(SharpEditorRegistries.ShapeFactoryInstance.FindBuilders<ILabelledBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.TitledBoxes => GetBuilderLinks(SharpEditorRegistries.ShapeFactoryInstance.FindBuilders<ITitledBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.TitleStyles => GetBuilderLinks(SharpEditorRegistries.ShapeFactoryInstance.FindBuilders<ITitleStyledBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.EntriedShapes => GetBuilderLinks(SharpEditorRegistries.ShapeFactoryInstance.FindBuilders<IEntriedShape>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.Bars => GetBuilderLinks(SharpEditorRegistries.ShapeFactoryInstance.FindBuilders<IBar>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.UsageBars => GetBuilderLinks(SharpEditorRegistries.ShapeFactoryInstance.FindBuilders<IUsageBar>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.Details => GetBuilderLinks(SharpEditorRegistries.ShapeFactoryInstance.FindBuilders<IDetail>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					// Markup Elements
					DocumentationSectionContents.MarkupElements => GetBuilderLinks(MarkupDocumentation.MarkupBuilders, MarkupDocumentation.MarkupBuilders.Get, window),
					// Card configurations
					DocumentationSectionContents.CardConfigs => GetBuilderLinks(SharpEditorRegistries.CardSetConfigRegistryInstance, s => null, window), // TODO Can we improve the refreshAction here?
					DocumentationSectionContents.CardStructures => GetBuilderLinks(CardSetConfigFactory.ConfigBuilders, CardSetConfigFactory.ConfigBuilders.Get, window),
					// Environments
					DocumentationSectionContents.BasisEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Evaluations.BasisEnvironment.MakeInstance(SharpSheets.Evaluations.EvaluationContext.BasisContext), window),
					DocumentationSectionContents.BasisEnvironmentFunctions => EnvironmentPageBuilder.GetEnvironmentFunctionsContents(SharpSheets.Evaluations.BasisEnvironment.MakeInstance(SharpSheets.Evaluations.EvaluationContext.BasisContext), window),
					DocumentationSectionContents.MarkupEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(new SharpSheets.Markup.Canvas.MarkupEvaluationContext(MarkupEvaluationTypes.BaseContext).DrawingStateVariables(), window),
					DocumentationSectionContents.MarkupEnvironmentFunctions => EnvironmentPageBuilder.GetEnvironmentFunctionsContents(new SharpSheets.Markup.Canvas.MarkupEvaluationContext(MarkupEvaluationTypes.BaseContext).DrawingStateVariables(), window),
					DocumentationSectionContents.CardSubjectEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardSubjectEnvironments.BaseDefinitions, window),
					DocumentationSectionContents.CardOutlineEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardOutlinesEnvironments.BaseDefinitions, window),
					DocumentationSectionContents.CardSegmentEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardSegmentEnvironments.BaseDefinitions, window),
					DocumentationSectionContents.CardSegmentOutlineEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardSegmentOutlineEnvironments.BaseDefinitions, window),
					DocumentationSectionContents.CardFeatureEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardFeatureEnvironments.BaseDefinitions, window),
					// Fonts
					DocumentationSectionContents.FontFamilies => FontPageBuilder.GetFontFamiliesContents(window),
					DocumentationSectionContents.Fonts => FontPageBuilder.GetFontsContents(window),
					_ => throw new InvalidOperationException("Unknown DocumentationSectionContents value.")
				};
			}
			else if (documentationSegment is DocumentationCode code) {
				return MakeDocumentationCodeBlock(code);
			}

			throw new ArgumentException("Unknown IDocumentationSegment type.");
		}

		public static FontFamily GetCodeFontFamily() {
			if ((App.Current?.TryGetResource("EditorFont", out object? editorFont) ?? false) && editorFont is FontFamily codeFamily) {
				return codeFamily;
			}
			else {
				return new FontFamily("Consolas"); // Better fallback needed
			}
		}

		private static TextBlock MakeDocumentationParagraph(DocumentationParagraph documentationParagraph, DocumentationWindow window) {
			TextBlock text = new TextBlock() { TextWrapping = TextWrapping.Wrap, Margin = ParagraphMargin, Inlines = new InlineCollection() };
			foreach (DocumentationRun run in documentationParagraph.parts) {
				Inline inline;
				if (run.link != null) {
					ClickableRun clickable = new ClickableRun(run.text ?? run.link.location) {
						FontStyle = (run.format & MarkdownFormat.ITALIC) == MarkdownFormat.ITALIC ? FontStyle.Italic : FontStyle.Normal,
						FontWeight = (run.format & MarkdownFormat.BOLD) == MarkdownFormat.BOLD ? FontWeight.Bold : FontWeight.Normal
					};
					clickable.MouseLeftButtonDown += window.MakeNavigationDelegate(run.link);
					inline = clickable;
				}
				else if (run.IsCode) {
					TextBlock codeBlock = new TextBlock() {
						Text = run.text,
						FontFamily = GetCodeFontFamily()
					};
					Border codeBorder = new Border() {
						Child = codeBlock,
						//Background = CodeBackgroundBrush,
						Classes = { "codeBorder" },
						CornerRadius = new CornerRadius(3.0),
						Padding = new Thickness(3, 1, 3, 1)
					};
					inline = new InlineUIContainer(codeBorder) { BaselineAlignment = BaselineAlignment.Bottom };
				}
				else {
					inline = new Run(run.text) {
						FontStyle = (run.format & MarkdownFormat.ITALIC) == MarkdownFormat.ITALIC ? FontStyle.Italic : FontStyle.Normal,
						FontWeight = (run.format & MarkdownFormat.BOLD) == MarkdownFormat.BOLD ? FontWeight.Bold : FontWeight.Normal
					};
				}

				text.Inlines.Add(inline);
			}
			return text;
		}

		private static Control GetBuilderLinks(IEnumerable<BuilderDetails> details, Func<string, BuilderDetails?>? refreshAction, DocumentationWindow window) {
			StackPanel contentsStack = new StackPanel() { Margin = ParagraphMargin };

			foreach (IGrouping<string, BuilderDetails> builderGroup in details.GroupBy(c => c is MarkupBuilderDetails markupBuilder && !string.IsNullOrEmpty(markupBuilder.Pattern.Library) ? markupBuilder.Pattern.Library : "").OrderBy(g => g.Key)) {
				StackPanel groupStack = new StackPanel() { Margin = ParagraphMargin };
				if (!string.IsNullOrWhiteSpace(builderGroup.Key)) {
					contentsStack.Children.Add(new TextBlock() { Text = builderGroup.Key, Margin = TextBlockMargin });
					groupStack.AddIndent(10);
				}
				foreach (BuilderDetails builder in builderGroup.OrderBy(c => c.Name)) {
					ClickableRun builderClickable = new ClickableRun(GetBuilderPrintedName(builder));
					builderClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(builder, () => refreshAction?.Invoke(builder.FullName));
					groupStack.Children.Add(new TextBlock() {
						Margin = new Thickness(0, 1, 0, 1),
						Inlines = new InlineCollection() { builderClickable }
					});
				}
				contentsStack.Children.Add(groupStack);
			}
			return contentsStack;
		}

		private static Control GetBuilderLinks(IEnumerable<CardSetConfig> configs, Func<string, CardSetConfig?>? refreshAction, DocumentationWindow window) {
			StackPanel contentsStack = new StackPanel() { Margin = ParagraphMargin };

			foreach (CardSetConfig config in configs.OrderBy(d => d.Name)) {
				ClickableRun builderClickable = new ClickableRun(config.Name);
				builderClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(config, () => refreshAction?.Invoke(config.Name));
				contentsStack.Children.Add(new TextBlock() {
					Margin = new Thickness(0, 1, 0, 1),
					Inlines = new InlineCollection() { builderClickable }
				});
			}
			return contentsStack;
		}

		private static string GetBuilderPrintedName(BuilderDetails builder) {
			if (typeof(IMarkupElement).IsAssignableFrom(builder.DeclaringType)) {
				return "<" + builder.Name + ">";
			}
			else {
				return builder.Name;
			}
		}

		private static Control MakeDocumentationCodeBlock(DocumentationCode documentationCode) {
			string codeText = documentationCode.code; // .Replace("\t", "    ");

			TextDocument document = new TextDocument() { Text = codeText };
			TextArea textArea = new TextArea() {
				Document = document,
				FontFamily = GetCodeFontFamily()
			};

			IHighlightingDefinition? highlightingDefinition;
			IParsingState? parsingState;
			if (documentationCode.highlighting == DocumentType.MARKUP) {
				highlightingDefinition = SharpEditorPalette.BoxMarkupHighlighting;
				parsingState = new MarkupParsingState(document);
			}
			else if (documentationCode.highlighting == DocumentType.CARDCONFIG) {
				highlightingDefinition = SharpEditorPalette.CardConfigHighlighting;
				parsingState = new CardConfigParsingState(document);
			}
			else if (documentationCode.highlighting == DocumentType.CARDSUBJECT) {
				highlightingDefinition = SharpEditorPalette.CardSubjectHighlighting;
				parsingState = new CardSubjectParsingState(document);
			}
			else if (documentationCode.highlighting == DocumentType.UNKNOWN) {
				highlightingDefinition = null;
				parsingState = null;
			}
			else { // Default to sharp config
				highlightingDefinition = SharpEditorPalette.CharacterSheetHighlighting;
				parsingState = new CharacterSheetParsingState(document);
			}

			if(parsingState is not null) {
				List<AvaloniaEdit.Rendering.IVisualLineTransformer> parsingStateLineTransformers = parsingState.GetLineTransformers().ToList();
				foreach (AvaloniaEdit.Rendering.IVisualLineTransformer transformer in parsingStateLineTransformers) {
					textArea.TextView.LineTransformers.Add(transformer);
				}
			}

			//textArea.DefaultInputHandler.NestedInputHandlers.Remove(textArea.DefaultInputHandler.Editing);
			textArea.DefaultInputHandler.NestedInputHandlers.Remove(textArea.DefaultInputHandler.CaretNavigation);
			textArea.ReadOnlySectionProvider = ReadOnlySectionDocument.Instance;
			if (highlightingDefinition is not null) {
				textArea.TextView.LineTransformers.Insert(0, new HighlightingColorizer(highlightingDefinition));
			}

			textArea.SelectionCornerRadius = 1.0; // Can also adjust Selection Brush from here
			textArea.SelectionBorder = null;
			textArea.SelectionForeground = null;
			//textArea.SelectionBrush = new SolidColorBrush(Colors.LightSlateGray) { Opacity = 0.5 };

			ScrollViewer codeScroller = new ScrollViewer() {
				VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
				Content = textArea
			};

			Border codeBorder = new Border {
				Child = codeScroller,
				//Background = CodeBackgroundBrush,
				Classes = { "codeBorder" },
				Padding = new Thickness(10.0, 5.0, 10.0, 5.0),
				Margin = ParagraphMargin,
				CornerRadius = new CornerRadius(5.0)
			};

			return codeBorder;
		}

		class ReadOnlySectionDocument : IReadOnlySectionProvider {
			public static readonly ReadOnlySectionDocument Instance = new ReadOnlySectionDocument();

			public bool CanInsert(int offset) {
				return false;
			}

			public IEnumerable<ISegment> GetDeletableSegments(ISegment segment) {
				return Enumerable.Empty<ISegment>();
			}
		}

	}

}
