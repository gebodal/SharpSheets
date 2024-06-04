using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SharpEditorAvalonia.Documentation {

	public delegate UIElement DocumentationRefresh();

	public class DocumentationPage : Page {

		public DocumentationRefresh? RefreshAction { get; set; }

		private readonly DockPanel panel;

		public DocumentationPage() {
			panel = new DockPanel() { Margin = new Thickness(20.0), MaxWidth = 650 };
			ScrollViewer scrollViewer = new ScrollViewer() {
				Content = panel,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
			};
			Content = scrollViewer;
		}

		public void SetPageContent(UIElement content) {
			panel.Children.Clear();
			panel.Children.Add(content);
		}

		public void RefreshPage() {
			if (RefreshAction != null) {
				try {
					//(UIElement content, string title) = RefreshAction();
					UIElement content = RefreshAction();
					if (content != null) {
						SetPageContent(content);
						//Console.WriteLine("Successfully refreshed " + Title);
						//this.Title = title;
					}
				}
				catch (Exception e) {
					Console.WriteLine($"Error refreshing page \"{Title}\": " + e.Message);
				}
			}
		}

	}

}
