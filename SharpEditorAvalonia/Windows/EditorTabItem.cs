using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace SharpEditorAvalonia.Windows {

	public class EditorTabItem {

		private readonly SharpEditorWindow window;

		public Control Header { get; set; }
		public SharpDocumentEditor Content { get; set; }

		private readonly TextBlock headerText;

		public EditorTabItem(SharpEditorWindow window, SharpDocumentEditor editor) {
			this.window = window;
			this.Content = editor;

			Border border = new Border() {
				Padding = new Thickness(2)
			};

			Grid grid = new Grid() {
				RowDefinitions = { new RowDefinition(GridLength.Auto) },
				ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
				IsHitTestVisible = true,
				Background = Brushes.Transparent // Apparently this is needed for hit testing
			};

			headerText = new TextBlock() {
				Text = editor.Header,
				Margin = new Thickness(10, 0, 10, 0),
				Width = double.NaN,
				FontSize = 8,
				VerticalAlignment = VerticalAlignment.Center,
				Foreground = new SolidColorBrush(Colors.White)
			};
			ToolTip.SetTip(headerText, editor.CurrentFilePath ?? editor.CurrentFileName);

			// Close button to remove the tab
			/*
			TabCloseButton closeButton = new TabCloseButton() {
				Width = 20,
				Height = 20,
				VerticalAlignment = VerticalAlignment.Center
			};
			*/
			Button closeButton = new Button() {
				Content = "Close",
				FontSize = 8,
				Height = 20,
				VerticalAlignment = VerticalAlignment.Center
			};
			closeButton.Click +=
				(sender, e) => {
					window.CloseTab(this);
				};

			Grid.SetColumn(headerText, 0);
			Grid.SetColumn(closeButton, 1);
			grid.Children.Add(headerText);
			grid.Children.Add(closeButton);

			border.Child = grid;

			// Set the header
			Header = border;

			border.PointerPressed += HeaderPanel_PointerPressed;

			editor.UpdateHeader += DocumentHeaderChanged;
		}

		private void HeaderPanel_PointerPressed(object? sender, PointerPressedEventArgs e) {
			PointerPoint point = e.GetCurrentPoint(sender as Control);

			if (point.Properties.IsMiddleButtonPressed) {
				window.CloseTab(this);
				e.Handled = true;
			}
		}

		private void DocumentHeaderChanged(object? sender, UpdateHeaderEventArgs e) {
			headerText.Text = Content.Header;
			//headerText.ToolTip = Content.CurrentFilePath ?? Content.CurrentFileName;
			ToolTip.SetTip(headerText, Content.CurrentFilePath ?? Content.CurrentFileName);
		}
	}

}