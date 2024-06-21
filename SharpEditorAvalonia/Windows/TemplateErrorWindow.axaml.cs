using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SharpEditorAvalonia.Utilities;
using SharpSheets.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpEditorAvalonia.Windows {

	public partial class TemplateErrorWindow : Window {

		private readonly AppController controller;

		public TemplateErrorWindow(AppController controller) {
			InitializeComponent();

			this.controller = controller;

			this.controller.TemplateErrorsUpdated += OnTemplateErrorsUpdated;
		}

		public TemplateErrorWindow() {
			// Dummy constructor to resolve this issue:
			// Avalonia warning AVLN:0005: XAML resource "avares://SystemTrayMenu/UserInterface/Menu.axaml" won't be reachable via runtime loader, as no public constructor was found
			// See: https://github.com/AvaloniaUI/Avalonia/issues/11312
			throw new NotImplementedException();
		}

		public bool IsClosed { get; private set; } = false;
		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);

			this.controller.TemplateErrorsUpdated -= OnTemplateErrorsUpdated;
			IsClosed = true;
		}

		private void OnTemplateErrorsUpdated(object? sender, EventArgs e) {
			SetErrors(this.controller.TemplateErrors);
		}

		public void SetErrors(IEnumerable<TemplateError> errors) {
			this.ErrorPanel.Children.Clear();

			List<TemplateError> errorsList = errors.ToList();
			if (errorsList.Count > 0) {
				TextBlock countBlock = new TextBlock() {
					TextWrapping = TextWrapping.Wrap,
					Text = $"{errorsList.Count} errors in templates:"
				};
				this.ErrorPanel.Children.Add(countBlock);

				foreach (TemplateError error in errorsList) {
					Control errorElem = GetErrorElement(error);
					this.ErrorPanel.Children.Add(errorElem);
				}
			}
			else {
				TextBlock successBlock = new TextBlock() {
					TextWrapping = TextWrapping.Wrap,
					Text = "No errors to display. All parsed successfully."
				};
				this.ErrorPanel.Children.Add(successBlock);
			}
		}

		private Control GetErrorElement(TemplateError error) {
			TextBlock errorBlock = new TextBlock() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 5.0), Inlines = new InlineCollection() };

			string filename = System.IO.Path.GetFileName(error.filePath);
			ClickableRun fileButton = new ClickableRun(filename) { Foreground = Brushes.CornflowerBlue };
			fileButton.MouseLeftButtonDown += delegate (object? sender, PointerPressedEventArgs e) {
				SharpDocumentEditor editor = controller.window.OpenEditorDocument(error.filePath, true);
				if (error.location.Offset > 0) {
					editor.ScrollTo(error.location.Offset);
					editor.ScrollTo(error.location.Offset); // Not sure why, but this needs to be called twice to work
				}
				controller.window.Activate();
				editor.FocusEditor();
				e.Handled = true;
			};

			errorBlock.Inlines.Add(fileButton);
			errorBlock.Inlines.Add(new Run(": "));
			errorBlock.Inlines.Add(new Run(error.error.Message));

			StackPanel panel = new StackPanel() {
				Orientation = Orientation.Vertical,
				Margin = new Thickness(10, 8, 10, 8)
			};
			panel.Children.Add(errorBlock);

			Border bordered = new Border() {
				BorderThickness = new Thickness(0.0),
				CornerRadius = new CornerRadius(10.0),
				Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
				Margin = new Thickness(0, 5, 0, 5)
			};
			bordered.Child = panel;

			return bordered;
		}

	}

	public readonly struct TemplateError {

		public readonly string filePath;
		public readonly DocumentSpan location;
		public readonly Exception error;

		public TemplateError(string filePath, DocumentSpan location, Exception error) {
			this.filePath = filePath;
			this.location = location;
			this.error = error;
		}

	}

}
