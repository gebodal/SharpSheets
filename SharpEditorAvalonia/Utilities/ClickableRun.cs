using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using System;

namespace SharpEditorAvalonia.Utilities {

	public class ClickableRun : InlineUIContainer {

		public static readonly Brush HyperlinkColor = new SolidColorBrush(Color.FromRgb(142, 148, 251));

		public event EventHandler<PointerPressedEventArgs>? MouseLeftButtonDown;

		private readonly TextBlock textBlock;

		public ClickableRun(string? text) : base() {
			this.Foreground = HyperlinkColor;

			textBlock = new TextBlock() {
				Text = text,
				Foreground = HyperlinkColor
			};

			textBlock.PointerEntered += OnPointerChanged;
			textBlock.PointerExited += OnPointerChanged;

			textBlock.PointerPressed += OnPointerPressed;

			this.Child = textBlock;
		}

		public ClickableRun() : this(null) { }

		protected void OnPointerChanged(object? sender, PointerEventArgs e) {
			if (textBlock.IsPointerOver) {
				textBlock.TextDecorations = Avalonia.Media.TextDecorations.Underline;
				textBlock.Cursor = new Cursor(StandardCursorType.Arrow);
			}
			else {
				textBlock.TextDecorations = null;
				textBlock.Cursor = Cursor.Default;
			}
		}

		private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
			PointerPoint point = e.GetCurrentPoint(sender as Control);
			if (point.Properties.IsLeftButtonPressed) {
				MouseLeftButtonDown?.Invoke(sender, e);
				e.Handled = true;
			}
		}

	}

}
