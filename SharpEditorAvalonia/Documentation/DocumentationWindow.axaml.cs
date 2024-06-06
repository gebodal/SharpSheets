using SharpSheets.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpEditorAvalonia.Utilities;
using SharpSheets.Documentation;
using SharpEditorAvalonia.ContentBuilders;
using SharpSheets.Markup.Patterns;
using SharpSheets.Markup.Parsing;
using SharpSheets.Markup.Elements;
using System.ComponentModel;
using SharpSheets.Cards.CardConfigs;
using SharpEditorAvalonia.DataManagers;
using SharpEditorAvalonia.Documentation.DocumentationBuilders;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Shapes;

namespace SharpEditorAvalonia.Documentation {
	/// <summary>
	/// Interaction logic for DocumentationWindow.xaml
	/// </summary>
	public partial class DocumentationWindow : Window, INotifyPropertyChanged {

		public static readonly Brush HyperlinkColor = new SolidColorBrush(Color.FromRgb(142, 148, 251));

		protected readonly NavigationService<DocumentationPage> navigationService;

		public DocumentationWindow() {
			InitializeComponent();

			this.navigationService = new NavigationService<DocumentationPage>();

			this.Title = $"{SharpEditorData.GetEditorName()} Documentation";
			TitleTextBlock.MakeFontSizeRelative(1.25);
			
			// TODO Implement navigation properly
			//DocFrame.NavigationService.Navigated += NavigationFinished;
			DataContext = this;
			
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
			//DocFrame.NavigationService.Navigate(page);
			navigationService.Navigate(page);
			DocFrame.Children.Clear();
			if (navigationService.Current is DocumentationPage current) {
				DocFrame.Children.Add(current);
				TitleTextBlock.Text = (DocFrame.Children[0] as DocumentationPage)?.Title ?? "Documentation";
				//Console.WriteLine("Navigated");
			}
		}

		/*
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

		public void NavigateTo(ConstructorDetails constructor, Func<ConstructorDetails?>? refreshAction) {
			if (typeof(IMarkupElement).IsAssignableFrom(constructor.DeclaringType)) {
				NavigateTo(MarkupPageBuilder.GetMarkupElementPage(constructor, this, refreshAction));
			}
			else {
				NavigateTo(ConstructorPageBuilder.GetConstructorPage(constructor, this, refreshAction));
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

		*/

		#region Navigation DependencyProperties

		/*
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public bool CanGoBack {
			get {
				return (bool)GetValue(CanGoBackProperty);
			}
			set {
				SetValue(CanGoBackProperty, value);
				OnPropertyChanged(nameof(CanGoBack));
			}
		}

		public bool CanGoForward {
			get {
				return (bool)GetValue(CanGoForwardProperty);
			}
			set {
				SetValue(CanGoForwardProperty, value);
				NotifyPropertyChanged("CanGoForward");
			}
		}
		*/

		/*
		private void NavigationFinished(object? sender, System.Windows.Navigation.NavigationEventArgs e) {
			CanGoBack = DocFrame.NavigationService.CanGoBack;
			CanGoForward = DocFrame.NavigationService.CanGoForward;
			TitleTextBlock.Text = (DocFrame.Content as DocumentationPage)?.Title ?? "Documentation";
			//Console.WriteLine("Navigated: " + CanGoBack + ", " + CanGoForward);
		}
		*/

		#endregion Navigation DependencyProperties
		
		private void OnBackClick(object? sender, RoutedEventArgs e) => Back();
		private void OnNextClick(object? sender, RoutedEventArgs e) => Next();
		private void OnHomeClick(object? sender, RoutedEventArgs e) => Home();
		private void OnRefreshClick(object? sender, RoutedEventArgs e) => Refresh();
		
		private void Back() {
			//if (DocFrame.NavigationService.CanGoBack) {
			//	DocFrame.NavigationService.GoBack();
			//	//Console.WriteLine("Back");
			//}
			if (navigationService.CanGoBack) {
				navigationService.GoBack();
			}
		}

		private void Next() {
			//if (DocFrame.NavigationService.CanGoForward) {
			//	DocFrame.NavigationService.GoForward();
			//	//Console.WriteLine("Next");
			//}
			if (navigationService.CanGoForward) {
				navigationService.GoForward();
			}
		}

		private void Home() {
			////if (DocFrame.NavigationService.CanGoBack) {
			////	var entry = DocFrame.NavigationService.RemoveBackEntry();
			////	while (entry != null) {
			////		entry = DocFrame.NavigationService.RemoveBackEntry();
			////	}
			////}

			////while (DocFrame.NavigationService.CanGoBack) {
			////	Console.WriteLine("Go back");
			////	DocFrame.NavigationService.GoBack();
			////}
			////DocFrame.NavigationService.RemoveBackEntry();

			if (!(DocFrame.Children[0] is DocumentationPage page && page.Title == Documentation.DocumentationRoot.name)) {
				NavigateTo(DocumentationPageBuilder.CreateHomePage(this));
			}
		}

		private void Refresh() {
			if (DocFrame.Children[0] is DocumentationPage page) {
				page.RefreshPage();
			}
		}

		public EventHandler<PointerPressedEventArgs> MakeNavigationDelegate(ConstructorDetails constructor, Func<ConstructorDetails?>? refreshAction) {
			if (typeof(IMarkupElement).IsAssignableFrom(constructor.DeclaringType)) {
				return delegate { NavigateTo(MarkupPageBuilder.GetMarkupElementPage(constructor, this, refreshAction)); };
			}
			else {
				return delegate { NavigateTo(ConstructorPageBuilder.GetConstructorPage(constructor, this, refreshAction)); };
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
				if (SharpEditorRegistries.WidgetFactoryInstance.Get(link.location) is ConstructorDetails widgetConstructor) {
					return delegate { NavigateTo(ConstructorPageBuilder.GetConstructorPage(widgetConstructor, this, WidgetConstructorFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.SHAPE) {
				if (SharpEditorRegistries.ShapeFactoryInstance.Get(link.location) is ConstructorDetails shapeConstructor) {
					return delegate { NavigateTo(ConstructorPageBuilder.GetConstructorPage(shapeConstructor, this, ShapeConstructorFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.MARKUP) {
				if (MarkupDocumentation.MarkupConstructors.Get(link.location) is ConstructorDetails markupConstructor) {
					return delegate { NavigateTo(MarkupPageBuilder.GetMarkupElementPage(markupConstructor, this, MarkupConstructorFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.ENUM) {
				if (SharpDocumentation.GetBuiltInEnumDocFromName(link.location) is EnumDoc enumDoc) {
					return delegate { NavigateTo(EnumPageBuilder.GetEnumPage(enumDoc, this, BuiltInEnumDocFunc(link.location))); };
				}
			}
			else if (link.linkType == DocumentationLinkType.CARD) {
				if (CardSetConfigFactory.ConfigConstructors.Get(link.location) is ConstructorDetails cardElementConstructor) {
					return delegate { NavigateTo(ConstructorPageBuilder.GetConstructorPage(cardElementConstructor, this, CardElementConstructorFunc(link.location))); };
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
				else if (SharpEditorRegistries.WidgetFactoryInstance.Get(link) is ConstructorDetails widgetConstructor) {
					NavigateTo(ConstructorPageBuilder.GetConstructorPage(widgetConstructor, this, WidgetConstructorFunc(link)));
				}
				else if (SharpEditorRegistries.ShapeFactoryInstance.Get(link) is ConstructorDetails shapeConstructor) {
					NavigateTo(ConstructorPageBuilder.GetConstructorPage(shapeConstructor, this, ShapeConstructorFunc(link)));
				}
				else if (MarkupDocumentation.MarkupConstructors.Get(link) is ConstructorDetails markupConstructor) {
					NavigateTo(MarkupPageBuilder.GetMarkupElementPage(markupConstructor, this, MarkupConstructorFunc(link)));
				}
				else if (CardSetConfigFactory.ConfigConstructors.Get(link) is ConstructorDetails cardElementConstructor) {
					NavigateTo(ConstructorPageBuilder.GetConstructorPage(cardElementConstructor, this, CardElementConstructorFunc(link)));
				}
			};
		}

		private static Func<ConstructorDetails?> WidgetConstructorFunc(string name) {
			return () => SharpEditorRegistries.WidgetFactoryInstance.Get(name);
		}

		private static Func<ConstructorDetails?> ShapeConstructorFunc(string name) {
			return () => SharpEditorRegistries.ShapeFactoryInstance.Get(name);
		}

		private static Func<ConstructorDetails?> MarkupConstructorFunc(string name) {
			return () => MarkupDocumentation.MarkupConstructors.Get(name);
		}

		private static Func<EnumDoc?> BuiltInEnumDocFunc(string name) {
			return () => SharpDocumentation.GetBuiltInEnumDocFromName(name);
		}

		private static Func<ConstructorDetails?> CardElementConstructorFunc(string name) {
			return () => CardSetConfigFactory.ConfigConstructors.Get(name);
		}
	}

}
