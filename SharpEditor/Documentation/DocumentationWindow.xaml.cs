using SharpSheets.Shapes;
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
using System.Windows.Interop;
using System.Windows.Forms;
using SharpSheets.Markup.Parsing;
using SharpSheets.Markup.Elements;
using System.ComponentModel;
using SharpSheets.Cards.CardConfigs;
using SharpEditor.DataManagers;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace SharpEditor.Documentation {
	/// <summary>
	/// Interaction logic for DocumentationWindow.xaml
	/// </summary>
	public partial class DocumentationWindow : Window, INotifyPropertyChanged {

		public static readonly Brush HyperlinkColor = new SolidColorBrush(Color.FromRgb(142, 148, 251));

		public DocumentationWindow() {
			InitializeComponent();

			this.Title = $"{SharpEditorData.GetEditorName()} Documentation";

			DocFrame.NavigationService.Navigated += NavigationFinished;
			DataContext = this;

			NavigateTo(CreateHomePage());
		}

		public void SetPosition(bool onRight, double width) {
			// TODO This breaks on Windows 11, for some reason
			// Is there a better OS agnostic version of this?
			/*
			WindowInteropHelper windowInteropHelper = new WindowInteropHelper(this);
			Screen screen = System.Windows.Forms.Screen.FromHandle(windowInteropHelper.Handle);

			System.Drawing.Rectangle workingArea = screen.WorkingArea;

			this.Height = workingArea.Height;
			this.Width = workingArea.Width * width;

			this.Top = workingArea.Top;

			if (onRight) {
				this.Left = workingArea.Right - this.Width;
			}
			else {
				this.Left = workingArea.Left;
			}
			*/
		}

		void BrowseBackExecuted(object target, ExecutedRoutedEventArgs e) {
			Back();
			e.Handled = true;
		}
		void BrowseForwardExecuted(object target, ExecutedRoutedEventArgs e) {
			Next();
			e.Handled = true;
		}
		void BrowseHomeExecuted(object target, ExecutedRoutedEventArgs e) {
			Home();
			e.Handled = true;
		}
		void RefreshExecuted(object target, ExecutedRoutedEventArgs e) {
			Refresh();
			e.Handled = true;
		}

		void NavigationCommandCanExecute(object? sender, CanExecuteRoutedEventArgs e) {
			e.CanExecute = true;
		}

		public bool IsClosed { get; private set; } = false;
		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);

			IsClosed = true;
		}

		public void NavigateTo(DocumentationPage1 page) {
			//Console.WriteLine("Navigate to " + page.Title);
			DocFrame.NavigationService.Navigate(page);
		}

		public void NavigateTo(ConstructorDetails constructor, Func<ConstructorDetails?>? refreshAction) {
			if (typeof(IMarkupElement).IsAssignableFrom(constructor.DeclaringType)) {
				NavigateTo(DocumentationBuilder.GetMarkupElementPage(constructor, this, refreshAction));
			}
			else {
				NavigateTo(DocumentationBuilder.GetConstructorPage(constructor, this, refreshAction));
			}
		}

		public void NavigateTo(CardSetConfig cardSetConfig, Func<CardSetConfig>? refreshAction) {
			NavigateTo(DocumentationBuilder.GetCardSetConfigPage(cardSetConfig, this, refreshAction));
		}

		#region Navigation DependencyProperties

		public static readonly DependencyProperty CanGoBackProperty =
			DependencyProperty.Register("CanGoBack", typeof(bool),
			typeof(DocumentationWindow), new UIPropertyMetadata());

		public bool CanGoBack {
			get {
				return (bool)GetValue(CanGoBackProperty);
			}
			set {
				SetValue(CanGoBackProperty, value);
				NotifyPropertyChanged("CanGoBack");
			}
		}

		public static readonly DependencyProperty CanGoForwardProperty =
			DependencyProperty.Register("CanGoForward", typeof(bool),
			typeof(DocumentationWindow), new UIPropertyMetadata());

		public bool CanGoForward {
			get {
				return (bool)GetValue(CanGoForwardProperty);
			}
			set {
				SetValue(CanGoForwardProperty, value);
				NotifyPropertyChanged("CanGoForward");
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public void NotifyPropertyChanged(string propertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private void NavigationFinished(object? sender, System.Windows.Navigation.NavigationEventArgs e) {
			CanGoBack = DocFrame.NavigationService.CanGoBack;
			CanGoForward = DocFrame.NavigationService.CanGoForward;
			//Console.WriteLine("Navigated: " + CanGoBack + ", " + CanGoForward);
		}

		#endregion

		private void OnBackClick(object? sender, RoutedEventArgs e) => Back();
		private void OnNextClick(object? sender, RoutedEventArgs e) => Next();
		private void OnHomeClick(object? sender, RoutedEventArgs e) => Home();
		private void OnRefreshClick(object? sender, RoutedEventArgs e) => Refresh();

		private void Back() {
			if (DocFrame.NavigationService.CanGoBack) {
				DocFrame.NavigationService.GoBack();
				//Console.WriteLine("Back");
			}
		}

		private void Next() {
			if (DocFrame.NavigationService.CanGoForward) {
				DocFrame.NavigationService.GoForward();
				//Console.WriteLine("Next");
			}
		}

		private void Home() {
			/*
			if (DocFrame.NavigationService.CanGoBack) {
				var entry = DocFrame.NavigationService.RemoveBackEntry();
				while (entry != null) {
					entry = DocFrame.NavigationService.RemoveBackEntry();
				}
			}
			*/

			/*
			while (DocFrame.NavigationService.CanGoBack) {
				Console.WriteLine("Go back");
				DocFrame.NavigationService.GoBack();
			}
			DocFrame.NavigationService.RemoveBackEntry();
			*/

			if (!(DocFrame.Content is Page page && page.Title == Documentation.DocumentationRoot.name)) {
				NavigateTo(CreateHomePage());
			}
		}

		private void Refresh() {
			if(DocFrame.Content is DocumentationPage1 page) {
				page.RefreshPage();
				/*
				try {
					UIElement refreshed = page.RefreshAction();
					if (refreshed != null) {
						page.SetPageContent(refreshed);
					}
					//DocFrame.NavigationService.Content = page.RefreshAction();

				}
				catch (Exception e) {
					Console.WriteLine($"Error refreshing page \"{page.Title}\": " + e.Message);
				}
				*/
			}
		}

		private DocumentationPage1 CreateHomePage() => CreateDocumentationPageForNode(Documentation.DocumentationRoot);

		private DocumentationPage1 CreateDocumentationPageForNode(DocumentationNode documentationNode) {
			return DocumentationBuilder.MakePage(
				CreateDocumentationContentForNode(documentationNode),
				documentationNode.name,
				() => CreateDocumentationContentForNode(documentationNode));
		}

		private UIElement CreateDocumentationContentForNode(DocumentationNode documentationNode) {

			StackPanel stack = new StackPanel();

			if (documentationNode.HasSubsections && GetContents(documentationNode) is FrameworkElement contentsElement) {
				TextBlock contentsTitleBlock = MakeTitleBlock("Contents", 2);
				stack.Children.Add(contentsTitleBlock);

				contentsElement.Margin = DocumentationBuilder.SectionMargin;
				BaseContentBuilder.AddIndent(contentsElement, 45);
				stack.Children.Add(contentsElement);
			}

			if (documentationNode.main != null) {
				stack.Children.AddRange(CreateDocumentationPageElements(documentationNode.main));
			}

			return stack;
		}

		private TextBlock MakeContentsTextBlock(Inline inline) {
			TextBlock contentsBlock = new TextBlock(inline) { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,1,0,1) };
			return contentsBlock;
		}

		private FrameworkElement? GetContents(DocumentationNode documentationNode) {
			StackPanel stack = new StackPanel() { Margin = DocumentationBuilder.ContentsBlockMargin };

			foreach (DocumentationFile file in documentationNode.files.Values.OrderBy(f => f.index)) {
				ClickableRun fileClickable = new ClickableRun(file.title);
				fileClickable.MouseLeftButtonDown += MakeNavigationDelegate(file);
				TextBlock fileBlock = MakeContentsTextBlock(fileClickable);
				stack.Children.Add(fileBlock);
			}

			foreach (DocumentationNode node in documentationNode.nodes.Values.OrderBy(n => n.index)) {
				ClickableRun nodeClickable = new ClickableRun(node.name);
				nodeClickable.MouseLeftButtonDown += MakeNavigationDelegate(node);
				TextBlock nodeBlock = MakeContentsTextBlock(nodeClickable);
				stack.Children.Add(nodeBlock);

				if (GetContents(node) is UIElement uiElement) {
					stack.Children.Add(uiElement);
				}
			}

			return stack.Children.Count > 0 ? stack : null;
		}

		private DocumentationPage1 CreateDocumentationPage(DocumentationFile documentationFile) {
			return DocumentationBuilder.MakePage(
				CreateDocumentationPageContents(documentationFile),
				documentationFile.title,
				() => CreateDocumentationPageContents(documentationFile));
		}

		private UIElement CreateDocumentationPageContents(DocumentationFile documentationFile) {
			StackPanel stack = new StackPanel();
			stack.Children.AddRange(CreateDocumentationPageElements(documentationFile));
			return stack;
		}

		private IEnumerable<FrameworkElement> CreateDocumentationPageElements(DocumentationFile documentationFile) {

			List<FrameworkElement> contents = new List<FrameworkElement>();

			//contents.Add(new TextBlock() { Text = (string.IsNullOrWhiteSpace(documentationFile.location) ? "root" : documentationFile.location) + ", " + documentationFile.title });

			foreach (DocumentationSection section in documentationFile.sections) {
				FrameworkElement sectionElement = CreateDocumentationSection(section);
				contents.Add(sectionElement);
			}

			return contents;
		}

		private TextBlock MakeTitleBlock(string title, int level) {
			TextBlock titleBlock = new TextBlock() { Text = title, TextWrapping = TextWrapping.Wrap, Margin = DocumentationBuilder.TitleMargin };

			BaseContentBuilder.MakeFontSizeRelative(titleBlock, level switch {
				int i when i <= 0 => 3.0,
				1 => 2.5,
				2 => 2.0,
				3 => 1.5,
				_ => 1.25
			});

			return titleBlock;
		}

		private FrameworkElement CreateDocumentationSection(DocumentationSection documentationSection) {
			StackPanel stack = new StackPanel() { Margin = DocumentationBuilder.SectionMargin };

			TextBlock titleBlock = MakeTitleBlock(documentationSection.title, documentationSection.level);

			stack.Children.Add(titleBlock);

			foreach (IDocumentationSegment segment in documentationSection.segments) {
				FrameworkElement segmentElement = CreateDocumentationSegment(segment);
				BaseContentBuilder.AddIndent(segmentElement, 10);
				stack.Children.Add(segmentElement);
			}

			return stack;
		}

		private FrameworkElement CreateDocumentationSegment(IDocumentationSegment documentationSegment) {
			if(documentationSegment is DocumentationParagraph paragraph) {
				return MakeDocumentationParagraph(paragraph);
			}
			else if(documentationSegment is DocumentationContents contents) {
				return contents.contents switch {
					// Widgets
					DocumentationSectionContents.Widgets => GetConstructorLinks(SharpEditorRegistries.WidgetFactoryInstance, SharpEditorRegistries.WidgetFactoryInstance.Get),
					// Shapes
					DocumentationSectionContents.Shapes => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance, SharpEditorRegistries.ShapeFactoryInstance.Get),
					DocumentationSectionContents.Boxes => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<IBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get),
					DocumentationSectionContents.LabelledBoxes => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<ILabelledBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get),
					DocumentationSectionContents.TitledBoxes => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<ITitledBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get),
					DocumentationSectionContents.TitleStyles => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<ITitleStyledBox>(), SharpEditorRegistries.ShapeFactoryInstance.Get),
					DocumentationSectionContents.Bars => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<IBar>(), SharpEditorRegistries.ShapeFactoryInstance.Get),
					DocumentationSectionContents.UsageBars => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<IUsageBar>(), SharpEditorRegistries.ShapeFactoryInstance.Get),
					DocumentationSectionContents.Details => GetConstructorLinks(SharpEditorRegistries.ShapeFactoryInstance.FindConstructors<IDetail>(), SharpEditorRegistries.ShapeFactoryInstance.Get),
					// Markup Elements
					DocumentationSectionContents.MarkupElements => GetConstructorLinks(MarkupDocumentation.MarkupConstructors, MarkupDocumentation.MarkupConstructors.Get),
					// Card configurations
					DocumentationSectionContents.CardConfigs => GetConstructorLinks(SharpEditorRegistries.CardSetConfigRegistryInstance, s => null), // TODO Can we improve the refreshAction here?
					DocumentationSectionContents.CardStructures => GetConstructorLinks(CardSetConfigFactory.ConfigConstructors, CardSetConfigFactory.ConfigConstructors.Get),
					// Environments
					DocumentationSectionContents.BasisEnvironmentVariables => DocumentationBuilder.GetEnvironmentVariablesContents(SharpSheets.Evaluations.BasisEnvironment.Instance, this),
					DocumentationSectionContents.BasisEnvironmentFunctions => DocumentationBuilder.GetEnvironmentFunctionsContents(SharpSheets.Evaluations.BasisEnvironment.Instance, this),
					DocumentationSectionContents.MarkupEnvironmentVariables => DocumentationBuilder.GetEnvironmentVariablesContents(SharpSheets.Markup.Canvas.MarkupEnvironments.DrawingStateVariables, this),
					DocumentationSectionContents.MarkupEnvironmentFunctions => DocumentationBuilder.GetEnvironmentFunctionsContents(SharpSheets.Markup.Canvas.MarkupEnvironments.DrawingStateVariables, this),
					DocumentationSectionContents.CardSubjectEnvironmentVariables => DocumentationBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardSubjectEnvironments.BaseDefinitions, this),
					DocumentationSectionContents.CardOutlineEnvironmentVariables => DocumentationBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardOutlinesEnvironments.BaseDefinitions, this),
					DocumentationSectionContents.CardSectionEnvironmentVariables => DocumentationBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardSegmentEnvironments.BaseDefinitions, this),
					DocumentationSectionContents.CardSectionOutlineEnvironmentVariables => DocumentationBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardSegmentOutlineEnvironments.BaseDefinitions, this),
					DocumentationSectionContents.CardFeatureEnvironmentVariables => DocumentationBuilder.GetEnvironmentVariablesContents(SharpSheets.Cards.Definitions.CardFeatureEnvironments.BaseDefinitions, this),
					_ => throw new InvalidOperationException("Unknown DocumentationSectionContents value.")
				};
			}
			else if(documentationSegment is DocumentationCode code) {
				return MakeDocumentationCodeBlock(code);
			}

			throw new ArgumentException("Unknown IDocumentationSegment type.");
		}

		public static readonly Brush CodeBackgroundBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30)); // new SolidColorBrush(Color.FromRgb(114, 114, 118));
		public static readonly FontFamily CodeFontFamily = new FontFamily("Consolas");

		private TextBlock MakeDocumentationParagraph(DocumentationParagraph documentationParagraph) {
			TextBlock text = new TextBlock() { TextWrapping = TextWrapping.Wrap, Margin = DocumentationBuilder.ParagraphMargin };
			foreach (DocumentationRun run in documentationParagraph.parts) {
				Inline inline;
				if (run.link != null) {
					inline = new ClickableRun(run.text ?? run.link.location) {
						FontStyle = (run.format & MarkdownFormat.ITALIC) == MarkdownFormat.ITALIC ? FontStyles.Italic : FontStyles.Normal,
						FontWeight = (run.format & MarkdownFormat.BOLD) == MarkdownFormat.BOLD ? FontWeights.Bold : FontWeights.Normal
					};
					inline.MouseLeftButtonDown += MakeNavigationDelegate(run.link);
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

		private FrameworkElement GetConstructorLinks(IEnumerable<ConstructorDetails> details, Func<string, ConstructorDetails?>? refreshAction) {
			StackPanel contentsStack = new StackPanel() { Margin = DocumentationBuilder.ParagraphMargin };

			foreach (IGrouping<string, ConstructorDetails> constructorGroup in details.GroupBy(c => c is MarkupConstructorDetails markupConstructor && !string.IsNullOrEmpty(markupConstructor.Pattern.Library) ? markupConstructor.Pattern.Library : "").OrderBy(g => g.Key)) {
				StackPanel groupStack = new StackPanel() { Margin = DocumentationBuilder.ParagraphMargin };
				if (!string.IsNullOrWhiteSpace(constructorGroup.Key)) {
					contentsStack.Children.Add(new TextBlock() { Text = constructorGroup.Key, Margin = DocumentationBuilder.TextBlockMargin });
					BaseContentBuilder.AddIndent(groupStack, 10);
				}
				foreach (ConstructorDetails constructor in constructorGroup.OrderBy(c => c.Name)) {
					ClickableRun constructorClickable = new ClickableRun(GetConstructorPrintedName(constructor));
					constructorClickable.MouseLeftButtonDown += MakeNavigationDelegate(constructor, () => refreshAction?.Invoke(constructor.FullName));
					groupStack.Children.Add(new TextBlock(constructorClickable) { Margin = new Thickness(0, 1, 0, 1) });
				}
				contentsStack.Children.Add(groupStack);
			}
			return contentsStack;
		}

		private FrameworkElement GetConstructorLinks(IEnumerable<CardSetConfig> configs, Func<string, CardSetConfig?>? refreshAction) {
			StackPanel contentsStack = new StackPanel() { Margin = DocumentationBuilder.ParagraphMargin };

			foreach (CardSetConfig config in configs.OrderBy(d => d.name)) {
				ClickableRun constructorClickable = new ClickableRun(config.name);
				constructorClickable.MouseLeftButtonDown += MakeNavigationDelegate(config, () => refreshAction?.Invoke(config.name));
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

		private FrameworkElement MakeDocumentationCodeBlock1(DocumentationCode documentationCode) {
			string codeText = documentationCode.code; // .Replace("\t", "    ");

			//TextBlock codeBlock = new TextBlock() { Text = codeText, TextWrapping = TextWrapping.Wrap, FontFamily = SystemFonts.MessageFontFamily };

			System.Windows.Controls.TextBox codeBox = new System.Windows.Controls.TextBox() { Foreground = Brushes.White, Text = codeText, TextWrapping = TextWrapping.Wrap, BorderThickness = default, IsReadOnly = true, Background = Brushes.Transparent };
			
			Border codeBorder = new Border {
				Child = codeBox,
				Background = CodeBackgroundBrush,
				Padding = new Thickness(10.0, 5.0, 10.0, 5.0),
				Margin = DocumentationBuilder.ParagraphMargin,
				CornerRadius = new CornerRadius(5.0)
			};

			return codeBorder;
		}

		private FrameworkElement MakeDocumentationCodeBlock(DocumentationCode documentationCode) {
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
			else if(documentationCode.highlighting == DocumentType.UNKNOWN) {
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
				Margin = DocumentationBuilder.ParagraphMargin,
				CornerRadius = new CornerRadius(5.0)
			};

			return codeBorder;
		}

		private void CodeScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
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

		public MouseButtonEventHandler MakeNavigationDelegate(ConstructorDetails constructor, Func<ConstructorDetails?>? refreshAction) {
			if (typeof(IMarkupElement).IsAssignableFrom(constructor.DeclaringType)) {
				return delegate { NavigateTo(DocumentationBuilder.GetMarkupElementPage(constructor, this, refreshAction)); };
			}
			else {
				return delegate { NavigateTo(DocumentationBuilder.GetConstructorPage(constructor, this, refreshAction)); };
			}
		}
		public MouseButtonEventHandler MakeNavigationDelegate(EnumDoc enumDoc, Func<EnumDoc?>? refreshAction) {
			return delegate { NavigateTo(DocumentationBuilder.GetEnumPage(enumDoc, this, refreshAction)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(CardSetConfig cardSetConfig, Func<CardSetConfig?>? refreshAction) {
			return delegate { NavigateTo(DocumentationBuilder.GetCardSetConfigPage(cardSetConfig, this, refreshAction)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(DocumentationFile documentationFile) {
			return delegate { NavigateTo(CreateDocumentationPage(documentationFile)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(DocumentationNode documentationNode) {
			return delegate { NavigateTo(CreateDocumentationPageForNode(documentationNode)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(DocumentationLink link) {
			if (link.linkType == DocumentationLinkType.UNKNOWN) {
				// Just use the raw string if type unknown
				return MakeNavigationDelegate(link.location);
			}
			else if(link.linkType == DocumentationLinkType.WIDGET) {
				if (SharpEditorRegistries.WidgetFactoryInstance.Get(link.location) is ConstructorDetails widgetConstructor) {
					return delegate { NavigateTo(DocumentationBuilder.GetConstructorPage(widgetConstructor, this, WidgetConstructorFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.SHAPE) {
				if (SharpEditorRegistries.ShapeFactoryInstance.Get(link.location) is ConstructorDetails shapeConstructor) {
					return delegate { NavigateTo(DocumentationBuilder.GetConstructorPage(shapeConstructor, this, ShapeConstructorFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.MARKUP) {
				if (MarkupDocumentation.MarkupConstructors.Get(link.location) is ConstructorDetails markupConstructor) {
					return delegate { NavigateTo(DocumentationBuilder.GetMarkupElementPage(markupConstructor, this, MarkupConstructorFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.ENUM) {
				if (SharpDocumentation.GetBuiltInEnumDocFromName(link.location) is EnumDoc enumDoc) {
					return delegate { NavigateTo(DocumentationBuilder.GetEnumPage(enumDoc, this, BuiltInEnumDocFunc(link.location))); };
				}
			}

			// Otherwise, do nothing
			return delegate { };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(string link) {
			return delegate {
				if(Documentation.DocumentationRoot.GetNode(link) is DocumentationNode node) {
					NavigateTo(CreateDocumentationPageForNode(node));
				}
				else if (Documentation.DocumentationRoot.GetFile(link) is DocumentationFile file) {
					NavigateTo(CreateDocumentationPage(file));
				}
				else if (SharpEditorRegistries.WidgetFactoryInstance.Get(link) is ConstructorDetails widgetConstructor) {
					NavigateTo(DocumentationBuilder.GetConstructorPage(widgetConstructor, this, WidgetConstructorFunc(link)));
				}
				else if (SharpEditorRegistries.ShapeFactoryInstance.Get(link) is ConstructorDetails shapeConstructor) {
					NavigateTo(DocumentationBuilder.GetConstructorPage(shapeConstructor, this, ShapeConstructorFunc(link)));
				}
				else if(MarkupDocumentation.MarkupConstructors.Get(link) is ConstructorDetails markupConstructor) {
					NavigateTo(DocumentationBuilder.GetMarkupElementPage(markupConstructor, this, MarkupConstructorFunc(link)));
				}
			};
		}
		
		private Func<ConstructorDetails?> WidgetConstructorFunc(string name) {
			return () => SharpEditorRegistries.WidgetFactoryInstance.Get(name);
		}

		private Func<ConstructorDetails?> ShapeConstructorFunc(string name) {
			return () => SharpEditorRegistries.ShapeFactoryInstance.Get(name);
		}

		private Func<ConstructorDetails?> MarkupConstructorFunc(string name) {
			return () => MarkupDocumentation.MarkupConstructors.Get(name);
		}

		private Func<EnumDoc?> BuiltInEnumDocFunc(string name) {
			return () => SharpDocumentation.GetBuiltInEnumDocFromName(name);
		}

	}

}
