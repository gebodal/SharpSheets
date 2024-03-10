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
using SharpEditor.Documentation.DocumentationBuilders;

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

		public void NavigateTo(DocumentationPage page) {
			//Console.WriteLine("Navigate to " + page.Title);
			DocFrame.NavigationService.Navigate(page);
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

		#endregion Navigation DependencyProperties

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
				NavigateTo(DocumentationPageBuilder.CreateHomePage(this));
			}
		}

		private void Refresh() {
			if(DocFrame.Content is DocumentationPage page) {
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

		public MouseButtonEventHandler MakeNavigationDelegate(ConstructorDetails constructor, Func<ConstructorDetails?>? refreshAction) {
			if (typeof(IMarkupElement).IsAssignableFrom(constructor.DeclaringType)) {
				return delegate { NavigateTo(MarkupPageBuilder.GetMarkupElementPage(constructor, this, refreshAction)); };
			}
			else {
				return delegate { NavigateTo(ConstructorPageBuilder.GetConstructorPage(constructor, this, refreshAction)); };
			}
		}
		public MouseButtonEventHandler MakeNavigationDelegate(EnumDoc enumDoc, Func<EnumDoc?>? refreshAction) {
			return delegate { NavigateTo(EnumPageBuilder.GetEnumPage(enumDoc, this, refreshAction)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(CardSetConfig cardSetConfig, Func<CardSetConfig?>? refreshAction) {
			return delegate { NavigateTo(CardConfigPageBuilder.GetCardSetConfigPage(cardSetConfig, this, refreshAction)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(FontName fontName) {
			return delegate { NavigateTo(FontPageBuilder.GetFontPage(fontName, this)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(FontFamilyName fontFamilyName) {
			return delegate { NavigateTo(FontPageBuilder.GetFontFamilyPage(fontFamilyName, this)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(DocumentationFile documentationFile) {
			return delegate { NavigateTo(DocumentationPageBuilder.CreateDocumentationPage(documentationFile, this)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(DocumentationNode documentationNode) {
			return delegate { NavigateTo(DocumentationPageBuilder.CreateDocumentationPageForNode(documentationNode, this)); };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(DocumentationLink link) {
			if (link.linkType == DocumentationLinkType.UNKNOWN) {
				// Just use the raw string if type unknown
				return MakeNavigationDelegate(link.location);
			}
			else if(link.linkType == DocumentationLinkType.WIDGET) {
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

			// Otherwise, do nothing
			return delegate { };
		}
		public MouseButtonEventHandler MakeNavigationDelegate(string link) {
			return delegate {
				if(Documentation.DocumentationRoot.GetNode(link) is DocumentationNode node) {
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
				else if(MarkupDocumentation.MarkupConstructors.Get(link) is ConstructorDetails markupConstructor) {
					NavigateTo(MarkupPageBuilder.GetMarkupElementPage(markupConstructor, this, MarkupConstructorFunc(link)));
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

	}

}
