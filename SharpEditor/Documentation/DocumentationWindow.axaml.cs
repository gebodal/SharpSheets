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
using System.ComponentModel;
using SharpSheets.Cards.CardConfigs;
using SharpEditor.DataManagers;
using SharpEditor.Documentation.DocumentationBuilders;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Shapes;
using System.Windows.Input;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.Input;

namespace SharpEditor.Documentation {
	/// <summary>
	/// Interaction logic for DocumentationWindow.xaml
	/// </summary>
	public partial class DocumentationWindow : Window, INotifyPropertyChanged {

		protected readonly NavigationService<DocumentationPage> navigationService;

		public DocumentationWindow() {
			this.navigationService = new NavigationService<DocumentationPage>();
			InitialiseCommands();

			InitializeComponent();

			DataContext = this;

			this.Title = $"{SharpEditorData.GetEditorName()} Documentation";
			TitleTextBlock.MakeFontSizeRelative(TextBlockClass.H5);

			this.AddHandler(Window.PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel, false);
			
			navigationService.NavigationFinished += OnNavigated;
			
			// Finally, navigate to home page
			NavigateTo(DocumentationPageBuilder.CreateHomePage(this));
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

		public bool IsClosed { get; private set; } = false;
		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);

			IsClosed = true;
		}

		public void NavigateTo(DocumentationPage page) {
			//Console.WriteLine("Navigate to " + page.Title);
			navigationService.Navigate(page);
		}

		private void OnNavigated(object? sender, EventArgs e) {
			//Console.WriteLine("OnNavigated");
			DocFrame.Children.Clear();
			if (navigationService.Current is DocumentationPage current) {
				DocFrame.Children.Add(current);
				TitleTextBlock.Text = (DocFrame.Children[0] as DocumentationPage)?.Title ?? "Documentation";
				//Console.WriteLine("Navigated");
			}
		}

		private void Home() {
			if (!(DocFrame.Children[0] is DocumentationPage page && page.Title == Documentation.DocumentationRoot.name)) {
				NavigateTo(DocumentationPageBuilder.CreateHomePage(this));
			}
		}

		private void Refresh() {
			if (DocFrame.Children[0] is DocumentationPage page) {
				page.RefreshPage();
			}
		}

		private bool CanRefresh() {
			return DocFrame.Children[0] is DocumentationPage page && page.CanRefresh;
		}

		public void NavigateTo(BuilderDetails builder, Func<BuilderDetails?>? refreshAction) {
			if (typeof(IMarkupElement).IsAssignableFrom(builder.DeclaringType)) {
				NavigateTo(MarkupPageBuilder.GetMarkupElementPage(builder, this, refreshAction));
			}
			else {
				NavigateTo(BuilderPageBuilder.GetBuilderPage(builder, this, refreshAction));
			}
		}

		public void NavigateTo(CardSetConfig cardSetConfig, Func<CardSetConfig>? refreshAction) {
			NavigateTo(CardConfigPageBuilder.GetCardSetConfigPage(cardSetConfig, this, refreshAction));
		}

		public void NavigateTo(EnumDoc enumDoc, Func<EnumDoc>? refreshAction) {
			NavigateTo(EnumPageBuilder.GetEnumPage(enumDoc, this, refreshAction));
		}

		public void NavigateTo(FontName fontName) {
			NavigateTo(FontPageBuilder.GetFontPage(fontName, this));
		}
		public void NavigateTo(FontFamilyName fontFamilyName) {
			NavigateTo(FontPageBuilder.GetFontFamilyPage(fontFamilyName, this));
		}

		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(BuilderDetails builder, Func<BuilderDetails?>? refreshAction) {
			if (typeof(IMarkupElement).IsAssignableFrom(builder.DeclaringType)) {
				return delegate { NavigateTo(MarkupPageBuilder.GetMarkupElementPage(builder, this, refreshAction)); };
			}
			else {
				return delegate { NavigateTo(BuilderPageBuilder.GetBuilderPage(builder, this, refreshAction)); };
			}
		}
		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(EnumDoc enumDoc, Func<EnumDoc?>? refreshAction) {
			return delegate { NavigateTo(EnumPageBuilder.GetEnumPage(enumDoc, this, refreshAction)); };
		}
		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(CardSetConfig cardSetConfig, Func<CardSetConfig?>? refreshAction) {
			return delegate { NavigateTo(CardConfigPageBuilder.GetCardSetConfigPage(cardSetConfig, this, refreshAction)); };
		}
		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(FontName fontName) {
			return delegate { NavigateTo(FontPageBuilder.GetFontPage(fontName, this)); };
		}
		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(FontFamilyName fontFamilyName) {
			return delegate { NavigateTo(FontPageBuilder.GetFontFamilyPage(fontFamilyName, this)); };
		}
		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(OpenTypeFontSetting fontSetting) {
			return delegate { NavigateTo(FontPageBuilder.GetOpenTypeFeaturesPage(fontSetting, this)); };
		}
		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(DocumentationFile documentationFile) {
			return delegate { NavigateTo(DocumentationPageBuilder.CreateDocumentationPage(documentationFile, this)); };
		}
		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(DocumentationNode documentationNode) {
			return delegate { NavigateTo(DocumentationPageBuilder.CreateDocumentationPageForNode(documentationNode, this)); };
		}
		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(DocumentationLink link) {
			if (link.linkType == DocumentationLinkType.UNKNOWN) {
				// Just use the raw string if type unknown
				return MakeNavigationDelegate(link.location);
			}
			else if (link.linkType == DocumentationLinkType.WIDGET) {
				if (SharpEditorRegistries.WidgetFactoryInstance.Get(link.location) is BuilderDetails widgetBuilder) {
					return delegate { NavigateTo(BuilderPageBuilder.GetBuilderPage(widgetBuilder, this, WidgetBuilderFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.SHAPE) {
				if (SharpEditorRegistries.ShapeFactoryInstance.Get(link.location) is BuilderDetails shapeBuilder) {
					return delegate { NavigateTo(BuilderPageBuilder.GetBuilderPage(shapeBuilder, this, ShapeBuilderFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.MARKUP) {
				if (MarkupDocumentation.MarkupBuilders.Get(link.location) is BuilderDetails markupBuilder) {
					return delegate { NavigateTo(MarkupPageBuilder.GetMarkupElementPage(markupBuilder, this, MarkupBuilderFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.ENUM) {
				if (SharpDocumentation.GetBuiltInEnumDocFromName(link.location) is EnumDoc enumDoc) {
					return delegate { NavigateTo(EnumPageBuilder.GetEnumPage(enumDoc, this, BuiltInEnumDocFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.CARD) {
				if (CardSetConfigFactory.ConfigBuilders.Get(link.location) is BuilderDetails cardElementBuilder) {
					return delegate { NavigateTo(BuilderPageBuilder.GetBuilderPage(cardElementBuilder, this, CardElementBuilderFunc(link.location))); };
				}
			}

			// Otherwise, do nothing
			return delegate { };
		}
		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(string link) {
			return delegate {
				if (Documentation.DocumentationRoot.GetNode(link) is DocumentationNode node) {
					NavigateTo(DocumentationPageBuilder.CreateDocumentationPageForNode(node, this));
				}
				else if (Documentation.DocumentationRoot.GetFile(link) is DocumentationFile file) {
					NavigateTo(DocumentationPageBuilder.CreateDocumentationPage(file, this));
				}
				else if (SharpEditorRegistries.WidgetFactoryInstance.Get(link) is BuilderDetails widgetBuilder) {
					NavigateTo(BuilderPageBuilder.GetBuilderPage(widgetBuilder, this, WidgetBuilderFunc(link)));
				}
				else if (SharpEditorRegistries.ShapeFactoryInstance.Get(link) is BuilderDetails shapeBuilder) {
					NavigateTo(BuilderPageBuilder.GetBuilderPage(shapeBuilder, this, ShapeBuilderFunc(link)));
				}
				else if (MarkupDocumentation.MarkupBuilders.Get(link) is BuilderDetails markupBuilder) {
					NavigateTo(MarkupPageBuilder.GetMarkupElementPage(markupBuilder, this, MarkupBuilderFunc(link)));
				}
				else if (CardSetConfigFactory.ConfigBuilders.Get(link) is BuilderDetails cardElementBuilder) {
					NavigateTo(BuilderPageBuilder.GetBuilderPage(cardElementBuilder, this, CardElementBuilderFunc(link)));
				}
			};
		}

		private static Func<BuilderDetails?> WidgetBuilderFunc(string name) {
			return () => SharpEditorRegistries.WidgetFactoryInstance.Get(name);
		}

		private static Func<BuilderDetails?> ShapeBuilderFunc(string name) {
			return () => SharpEditorRegistries.ShapeFactoryInstance.Get(name);
		}

		private static Func<BuilderDetails?> MarkupBuilderFunc(string name) {
			return () => MarkupDocumentation.MarkupBuilders.Get(name);
		}

		private static Func<EnumDoc?> BuiltInEnumDocFunc(string name) {
			return () => SharpDocumentation.GetBuiltInEnumDocFromName(name);
		}

		private static Func<BuilderDetails?> CardElementBuilderFunc(string name) {
			return () => CardSetConfigFactory.ConfigBuilders.Get(name);
		}

		#region Commands

		public ICommand BackCommand { get; private set; }
		public ICommand ForwardCommand { get; private set; }

		public ICommand HomeCommand { get; private set; }
		public ICommand RefreshCommand { get; private set; }

		public ICommand LineUpCommand { get; private set; }
		public ICommand LineDownCommand { get; private set; }
		public ICommand PageUpCommand { get; private set; }
		public ICommand PageDownCommand { get; private set; }

		[MemberNotNull(nameof(BackCommand), nameof(ForwardCommand),
			nameof(HomeCommand), nameof(RefreshCommand),
			nameof(LineUpCommand), nameof(LineDownCommand),
			nameof(PageUpCommand), nameof(PageDownCommand))]
		private void InitialiseCommands() {
			BackCommand = new RelayCommand(BackExecuted, CanGoBack);
			ForwardCommand = new RelayCommand(ForwardExecuted, CanGoForward);

			HomeCommand = new RelayCommand(Home);
			RefreshCommand = new RelayCommand(Refresh);

			LineUpCommand = new RelayCommand(LineUpExecuted);
			LineDownCommand = new RelayCommand(LineDownExecuted);
			PageUpCommand = new RelayCommand(PageUpExecuted);
			PageDownCommand = new RelayCommand(PageDownExecuted);

			navigationService.PropertyChanged += OnNavigationServiceUpdate;
		}

		private void OnNavigationServiceUpdate(object? sender, PropertyChangedEventArgs e) {
			(BackCommand as RelayCommand)?.NotifyCanExecuteChanged();
			(ForwardCommand as RelayCommand)?.NotifyCanExecuteChanged();
		}

		private void BackExecuted() {
			navigationService.GoBack();
		}
		private bool CanGoBack() {
			return navigationService.CanGoBack;
		}

		private void ForwardExecuted() {
			navigationService.GoForward();
		}
		private bool CanGoForward() {
			return navigationService.CanGoForward;
		}

		private void LineUpExecuted() {
			if (DocFrame.Children[0] is DocumentationPage page) {
				page.LineUp();
			}
		}
		private void LineDownExecuted() {
			if (DocFrame.Children[0] is DocumentationPage page) {
				page.LineDown();
			}
		}

		private void PageUpExecuted() {
			if (DocFrame.Children[0] is DocumentationPage page) {
				page.PageUp();
			}
		}
		private void PageDownExecuted() {
			if (DocFrame.Children[0] is DocumentationPage page) {
				page.PageDown();
			}
		}

		#endregion Commands

		#region Pointer events

		private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e) {
			PointerPoint point = e.GetCurrentPoint(sender as Control);
			if (point.Properties.IsXButton1Pressed) {
				navigationService.GoBack();
				e.Handled = true;
			}
			else if (point.Properties.IsXButton2Pressed) {
				navigationService.GoForward();
				e.Handled = true;
			}
		}

		#endregion Pointer events

	}

}
