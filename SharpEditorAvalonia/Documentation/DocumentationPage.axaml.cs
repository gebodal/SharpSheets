using Avalonia.Controls;
using System;

namespace SharpEditorAvalonia.Documentation {

	public delegate Control DocumentationRefresh();

	public partial class DocumentationPage : UserControl {

		public DocumentationRefresh? RefreshAction { get; set; }

		public string? Title { get; set; }

		public DocumentationPage() {
			InitializeComponent();
		}

		public void SetPageContent(Control content) {
			Panel.Children.Clear();
			Panel.Children.Add(content);
		}

		public void RefreshPage() {
			if (RefreshAction != null) {
				try {
					//(UIElement content, string title) = RefreshAction();
					Control content = RefreshAction();
					if (content != null) {
						SetPageContent(content);
						//Console.WriteLine("Successfully refreshed " + Title);
						//this.Title = title;
					}
				}
				catch (Exception e) {
					Console.WriteLine($"Error refreshing page \"{Title ?? "Untitled"}\": " + e.Message);
				}
			}
		}

	}

}
