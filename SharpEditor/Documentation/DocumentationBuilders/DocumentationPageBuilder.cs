﻿using SharpSheets.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SharpEditor.Utilities;
using SharpSheets.Documentation;
using SharpEditor.ContentBuilders;
using SharpSheets.Markup.Patterns;
using SharpSheets.Markup.Parsing;
using SharpSheets.Markup.Elements;
using SharpSheets.Cards.CardConfigs;
using SharpEditor.DataManagers;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using static SharpEditor.ContentBuilders.BaseContentBuilder;
using static SharpEditor.Documentation.DocumentationBuilders.BaseDocumentationBuilder;

namespace SharpEditor.Documentation.DocumentationBuilders {

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

		private static UIElement CreateDocumentationContentForNode(DocumentationNode documentationNode, DocumentationWindow window) {

			StackPanel stack = new StackPanel();

			if (documentationNode.HasSubsections && GetContents(documentationNode, window) is FrameworkElement contentsElement) {
				TextBlock contentsTitleBlock = MakeTitleBlock("Contents", 2);
				stack.Children.Add(contentsTitleBlock);

				contentsElement.Margin = SectionMargin;
				AddIndent(contentsElement, 45);
				stack.Children.Add(contentsElement);
			}

			if (documentationNode.main != null) {
				stack.Children.AddRange(CreateDocumentationPageElements(documentationNode.main, window));
			}

			return stack;
		}

		private static TextBlock MakeContentsTextBlock(Inline inline) {
			TextBlock contentsBlock = new TextBlock(inline) { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1) };
			return contentsBlock;
		}

		private static FrameworkElement? GetContents(DocumentationNode documentationNode, DocumentationWindow window) {
			StackPanel stack = new StackPanel() { Margin = ContentsBlockMargin };

			foreach (DocumentationFile file in documentationNode.files.Values.OrderBy(f => f.index)) {
				ClickableRun fileClickable = new ClickableRun(file.title);
				fileClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(file);
				TextBlock fileBlock = MakeContentsTextBlock(fileClickable);
				stack.Children.Add(fileBlock);
			}

			foreach (DocumentationNode node in documentationNode.nodes.Values.OrderBy(n => n.index)) {
				ClickableRun nodeClickable = new ClickableRun(node.name);
				nodeClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(node);
				TextBlock nodeBlock = MakeContentsTextBlock(nodeClickable);
				stack.Children.Add(nodeBlock);

				if (GetContents(node, window) is UIElement uiElement) {
					stack.Children.Add(uiElement);
				}
			}

			return stack.Children.Count > 0 ? stack : null;
		}

		public static DocumentationPage CreateDocumentationPage(DocumentationFile documentationFile, DocumentationWindow window) {
			return MakePage(
				CreateDocumentationPageContents(documentationFile, window),
				documentationFile.title,
				() => CreateDocumentationPageContents(documentationFile, window));
		}

		private static UIElement CreateDocumentationPageContents(DocumentationFile documentationFile, DocumentationWindow window) {
			StackPanel stack = new StackPanel();
			stack.Children.AddRange(CreateDocumentationPageElements(documentationFile, window));
			return stack;
		}

		private static IEnumerable<FrameworkElement> CreateDocumentationPageElements(DocumentationFile documentationFile, DocumentationWindow window) {

			List<FrameworkElement> contents = new List<FrameworkElement>();

			//contents.Add(new TextBlock() { Text = (string.IsNullOrWhiteSpace(documentationFile.location) ? "root" : documentationFile.location) + ", " + documentationFile.title });

			foreach (DocumentationSection section in documentationFile.sections) {
				FrameworkElement sectionElement = CreateDocumentationSection(section, window);
				contents.Add(sectionElement);
			}

			return contents;
		}

		private static FrameworkElement CreateDocumentationSection(DocumentationSection documentationSection, DocumentationWindow window) {
			StackPanel stack = new StackPanel() { Margin = SectionMargin };

			TextBlock titleBlock = MakeTitleBlock(documentationSection.title, documentationSection.level);

			stack.Children.Add(titleBlock);

			foreach (IDocumentationSegment segment in documentationSection.segments) {
				FrameworkElement segmentElement = CreateDocumentationSegment(segment, window);
				AddIndent(segmentElement, 10);
				stack.Children.Add(segmentElement);
			}

			return stack;
		}

		private static FrameworkElement CreateDocumentationSegment(IDocumentationSegment documentationSegment, DocumentationWindow window) {
			if (documentationSegment is DocumentationParagraph paragraph) {
				return MakeDocumentationParagraph(paragraph, window);
			}
			else if (documentationSegment is DocumentationContents contents) {
				return contents.contents switch {
					// Widgets
					DocumentationSectionContents.Widgets => GetConstructorLinks(SharpEditorRegistries.WidgetFactoryInstance, SharpEditorRegistries.WidgetFactoryInstance.Get, window),
					// Shapes
					DocumentationSectionContents.Shapes => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance, SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.Boxes => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<IBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.LabelledBoxes => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<ILabelledBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.TitledBoxes => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<ITitledBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.TitleStyles => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<ITitleStyledBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.Bars => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<IBar>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.UsageBars => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<IUsageBar>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					DocumentationSectionContents.Details => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<IDetail>(), SharpEditorRegistries.ShapeFactoryInstance.Get, window),
					// Markup Elements
					DocumentationSectionContents.MarkupElements => GetConstructorLinks(MarkupDocumentation.MarkupConstructors, MarkupDocumentation.MarkupConstructors.Get, window),
					// Card configurations
					DocumentationSectionContents.CardConfigs => GetConstructorLinks(SharpEditorRegistries.CardSetConfigRegistryInstance, s => null, window), // TODO Can we improve the refreshAction here?
					DocumentationSectionContents.CardStructures => GetConstructorLinks(CardSetConfigFactory.ConfigConstructors, CardSetConfigFactory.ConfigConstructors.Get, window),
					// Environments
					DocumentationSectionContents.BasisEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Evaluations.BasisEnvironment.Instance, window),
					DocumentationSectionContents.BasisEnvironmentFunctions => EnvironmentPageBuilder.GetEnvironmentFunctionsContents(SharpSheets.Evaluations.BasisEnvironment.Instance, window),
					DocumentationSectionContents.MarkupEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Markup.Canvas.MarkupEnvironments.DrawingStateVariables, window),
					DocumentationSectionContents.MarkupEnvironmentFunctions => EnvironmentPageBuilder.GetEnvironmentFunctionsContents(SharpSheets.Markup.Canvas.MarkupEnvironments.DrawingStateVariables, window),
					DocumentationSectionContents.CardSubjectEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardSubjectEnvironments.BaseDefinitions, window),
					DocumentationSectionContents.CardOutlineEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardOutlinesEnvironments.BaseDefinitions, window),
					DocumentationSectionContents.CardSegmentEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardSegmentEnvironments.BaseDefinitions, window),
					DocumentationSectionContents.CardSegmentOutlineEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardSegmentOutlineEnvironments.BaseDefinitions, window),
					DocumentationSectionContents.CardFeatureEnvironmentVariables => EnvironmentPageBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardFeatureEnvironments.BaseDefinitions, window),
					_ => throw new InvalidOperationException("Unknown DocumentationSectionContents value.")
				};
			}
			else if (documentationSegment is DocumentationCode code) {
				return MakeDocumentationCodeBlock(code);
			}

			throw new ArgumentException("Unknown IDocumentationSegment type.");
		}

		public static readonly Brush CodeBackgroundBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30)); // new SolidColorBrush(Color.FromRgb(114, 114, 118));
		public static readonly FontFamily CodeFontFamily = new FontFamily("Consolas");

		private static TextBlock MakeDocumentationParagraph(DocumentationParagraph documentationParagraph, DocumentationWindow window) {
			TextBlock text = new TextBlock() { TextWrapping = TextWrapping.Wrap, Margin = ParagraphMargin };
			foreach (DocumentationRun run in documentationParagraph.parts) {
				Inline inline;
				if (run.link != null) {
					inline = new ClickableRun(run.text ?? run.link.location) {
						FontStyle = (run.format & MarkdownFormat.ITALIC) == MarkdownFormat.ITALIC ? FontStyles.Italic : FontStyles.Normal,
						FontWeight = (run.format & MarkdownFormat.BOLD) == MarkdownFormat.BOLD ? FontWeights.Bold : FontWeights.Normal
					};
					inline.MouseLeftButtonDown += window.MakeNavigationDelegate(run.link);
				}
				else if (run.IsCode) {
					/*
					inline = new Run(run.text) {
						Background = CodeBackgroundBrush,
						FontFamily = CodeFontFamily
					};
					*/
					TextBlock codeBlock = new TextBlock() {
						Text = run.text,
						FontFamily = CodeFontFamily
					};
					Border codeBorder = new Border() {
						Child = codeBlock,
						Background = CodeBackgroundBrush,
						CornerRadius = new CornerRadius(3.0),
						Padding = new Thickness(3, 1, 3, 1)
					};
					inline = new InlineUIContainer(codeBorder) { BaselineAlignment = BaselineAlignment.Bottom };
				}
				else {
					inline = new Run(run.text) {
						FontStyle = (run.format & MarkdownFormat.ITALIC) == MarkdownFormat.ITALIC ? FontStyles.Italic : FontStyles.Normal,
						FontWeight = (run.format & MarkdownFormat.BOLD) == MarkdownFormat.BOLD ? FontWeights.Bold : FontWeights.Normal
					};
				}

				text.Inlines.Add(inline);
			}
			return text;
		}

		private static FrameworkElement GetConstructorLinks(IEnumerable<ConstructorDetails> details, Func<string, ConstructorDetails?>? refreshAction, DocumentationWindow window) {
			StackPanel contentsStack = new StackPanel() { Margin = ParagraphMargin };

			foreach (IGrouping<string, ConstructorDetails> constructorGroup in details.GroupBy(c => c is MarkupConstructorDetails markupConstructor && !string.IsNullOrEmpty(markupConstructor.Pattern.Library) ? markupConstructor.Pattern.Library : "").OrderBy(g => g.Key)) {
				StackPanel groupStack = new StackPanel() { Margin = ParagraphMargin };
				if (!string.IsNullOrWhiteSpace(constructorGroup.Key)) {
					contentsStack.Children.Add(new TextBlock() { Text = constructorGroup.Key, Margin = TextBlockMargin });
					AddIndent(groupStack, 10);
				}
				foreach (ConstructorDetails constructor in constructorGroup.OrderBy(c => c.Name)) {
					ClickableRun constructorClickable = new ClickableRun(GetConstructorPrintedName(constructor));
					constructorClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(constructor, () => refreshAction?.Invoke(constructor.FullName));
					groupStack.Children.Add(new TextBlock(constructorClickable) { Margin = new Thickness(0, 1, 0, 1) });
				}
				contentsStack.Children.Add(groupStack);
			}
			return contentsStack;
		}

		private static FrameworkElement GetConstructorLinks(IEnumerable<CardSetConfig> configs, Func<string, CardSetConfig?>? refreshAction, DocumentationWindow window) {
			StackPanel contentsStack = new StackPanel() { Margin = ParagraphMargin };

			foreach (CardSetConfig config in configs.OrderBy(d => d.name)) {
				ClickableRun constructorClickable = new ClickableRun(config.name);
				constructorClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(config, () => refreshAction?.Invoke(config.name));
				contentsStack.Children.Add(new TextBlock(constructorClickable) { Margin = new Thickness(0, 1, 0, 1) });
			}
			return contentsStack;
		}

		private static string GetConstructorPrintedName(ConstructorDetails constructor) {
			if (typeof(IMarkupElement).IsAssignableFrom(constructor.DeclaringType)) {
				return "<" + constructor.Name + ">";
			}
			else {
				return constructor.Name;
			}
		}

		private static FrameworkElement MakeDocumentationCodeBlock1(DocumentationCode documentationCode) {
			string codeText = documentationCode.code; // .Replace("\t", "    ");

			//TextBlock codeBlock = new TextBlock() { Text = codeText, TextWrapping = TextWrapping.Wrap, FontFamily = SystemFonts.MessageFontFamily };

			System.Windows.Controls.TextBox codeBox = new System.Windows.Controls.TextBox() { Foreground = Brushes.White, Text = codeText, TextWrapping = TextWrapping.Wrap, BorderThickness = default, IsReadOnly = true, Background = Brushes.Transparent };

			Border codeBorder = new Border {
				Child = codeBox,
				Background = CodeBackgroundBrush,
				Padding = new Thickness(10.0, 5.0, 10.0, 5.0),
				Margin = ParagraphMargin,
				CornerRadius = new CornerRadius(5.0)
			};

			return codeBorder;
		}

		private static FrameworkElement MakeDocumentationCodeBlock(DocumentationCode documentationCode) {
			string codeText = documentationCode.code; // .Replace("\t", "    ");

			/*
			TextEditor textEditor = new TextEditor {
				IsReadOnly = true,
				Text = codeText,
				Encoding = System.Text.Encoding.UTF8,
				Focusable = false,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
			};
			textEditor.TextArea.Focusable = false;
			textEditor.IsHitTestVisible = false;

			return textEditor;
			*/

			TextDocument document = new TextDocument() { Text = codeText };
			TextArea textArea = new TextArea() {
				Document = document,
				FontFamily = CodeFontFamily
			};

			IHighlightingDefinition? highlightingDefinition;
			if (documentationCode.highlighting == DocumentType.MARKUP) {
				highlightingDefinition = SharpEditorPalette.BoxMarkupHighlighting;
			}
			else if (documentationCode.highlighting == DocumentType.CARDCONFIG) {
				highlightingDefinition = SharpEditorPalette.CardConfigHighlighting;
			}
			else if (documentationCode.highlighting == DocumentType.CARDSUBJECT) {
				highlightingDefinition = SharpEditorPalette.CardSubjectHighlighting;
			}
			else if (documentationCode.highlighting == DocumentType.UNKNOWN) {
				highlightingDefinition = null;
			}
			else { // Default to sharp config
				highlightingDefinition = SharpEditorPalette.CharacterSheetHighlighting;
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
			textArea.SelectionBrush = new SolidColorBrush(Colors.LightSlateGray) { Opacity = 0.5 };

			ScrollViewer codeScroller = new ScrollViewer() {
				VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
			};

			codeScroller.PreviewMouseWheel += CodeScroller_PreviewMouseWheel;
			codeScroller.Content = textArea;

			Border codeBorder = new Border {
				Child = codeScroller,
				Background = CodeBackgroundBrush,
				Padding = new Thickness(10.0, 5.0, 10.0, 5.0),
				Margin = ParagraphMargin,
				CornerRadius = new CornerRadius(5.0)
			};

			return codeBorder;
		}

		private static void CodeScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
			if (sender is ScrollViewer && !e.Handled) {
				e.Handled = true;
				var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
				eventArg.RoutedEvent = UIElement.MouseWheelEvent;
				eventArg.Source = sender;
				var parent = ((System.Windows.Controls.Control)sender).Parent as UIElement;
				parent?.RaiseEvent(eventArg);

			}
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
