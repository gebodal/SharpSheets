using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SharpEditor {

	public class EditorTabItem {

		SharpEditorWindow window;

		public UIElement Header { get; set; }
		public SharpDocumentEditor Content { get; set; }

		private readonly TextBlock headerText;

		public EditorTabItem(SharpEditorWindow window, SharpDocumentEditor editor) {
			this.window = window;
			this.Content = editor;

			StackPanel stackPanel = new StackPanel() {
				Margin = new Thickness(5, 0, 5, 0),
				Orientation = Orientation.Horizontal,
				Height = 22,
				Width = double.NaN,
				VerticalAlignment = VerticalAlignment.Center
			};

			headerText = new TextBlock() {
				Text = editor.Header,
				ToolTip = editor.CurrentFilePath ?? editor.CurrentFileName,
				Margin = new Thickness(10, 0, 10, 0),
				Width = double.NaN,
				VerticalAlignment = VerticalAlignment.Center,
				Foreground = new SolidColorBrush(Colors.White)
			};

			stackPanel.Children.Add(headerText);

			// Close button to remove the tab
			TabCloseButton closeButton = new TabCloseButton() {
				Width = 20,
				Height = 20,
				VerticalAlignment = VerticalAlignment.Center
			};
			closeButton.Click +=
				(sender, e) => {
					window.CloseTab(this);
				};
			stackPanel.Children.Add(closeButton);

			// Set the header
			Header = stackPanel;

			stackPanel.MouseDown += StackPanel_MouseDown;

			editor.UpdateHeader += DocumentHeaderChanged;
		}

		private void StackPanel_MouseDown(object? sender, System.Windows.Input.MouseButtonEventArgs e) {
			if (e.ChangedButton == System.Windows.Input.MouseButton.Middle && e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) {
				window.CloseTab(this);
				e.Handled = true;
			}
		}

		private void DocumentHeaderChanged(object? sender, UpdateHeaderEventArgs e) {
			headerText.Text = Content.Header;
			headerText.ToolTip = Content.CurrentFilePath ?? Content.CurrentFileName;
		}
	}

}