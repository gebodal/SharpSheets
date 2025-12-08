using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using SharpEditor.DataManagers;
using System;

namespace SharpEditor.Utilities {

	// This should really be called "ClickableInline"
	public class ClickableRun : InlineUIContainer {

		public event EventHandler<PointerPressedEventArgs>? MouseLeftButtonDown;

		private readonly Border border;
		private readonly TextBlock textBlock;

		private IDisposable? parentForegroundSubscription;
		private IDisposable? selfForegroundSubscription;

		public string? Text {
			get => textBlock.Text;
			set => textBlock.Text = value;
		}

		public ClickableRun(string? text) : base() {
			IBrush? hyperlinkBrush = Application.Current?.GetResource<IBrush>(SharpEditorThemeManager.HyperlinkBrush);

			this.Foreground = hyperlinkBrush;

			textBlock = new TextBlock() {
				Text = text,
				Classes = { "hyperlink" },
				IsHitTestVisible = false
				// Foreground set below
			};

			border = new Border() {
				Child = textBlock,
				Padding = new Thickness(0),
				Margin = new Thickness(0),
				Background = Brushes.Transparent
			};

			border.PointerEntered += OnPointerChanged;
			border.PointerExited += OnPointerChanged;
			border.PointerPressed += OnPointerPressed;

			this.Child = border;

			this.AttachedToLogicalTree += OnAttachedToLogicalTree;
			this.DetachedFromLogicalTree += OnDetachedFromLogicalTree;

			UpdateForegroundFromNearestSource(hyperlinkBrush);
		}

		public ClickableRun() : this(null) { }

		private void OnAttachedToLogicalTree(object? sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e) {
			SubscribeToForegroundChanges();
		}

		private void OnDetachedFromLogicalTree(object? sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e) {
			// Clean up subscriptions when detaching
			UnsubscribeFromForegroundChanges();
		}

		private TextBlock? FindContainingTextBlock() {
			ILogical? parent = this.Parent;
			while (parent != null) {
				if (parent is TextBlock tb) {
					return tb;
				}

				parent = parent.LogicalParent;
			}
			return null;
		}

		private void SubscribeToForegroundChanges() {
			UnsubscribeFromForegroundChanges(); // Clear existing

			IBrush? hyperlinkBrush = Application.Current?.GetResource<IBrush>(SharpEditorThemeManager.HyperlinkBrush);

			TextBlock? containingTextBlock = FindContainingTextBlock();
			if (containingTextBlock != null) {
				if(containingTextBlock.IsSet(TextBlock.ForegroundProperty)) {
					textBlock.Foreground = containingTextBlock.Foreground ?? this.Foreground ?? hyperlinkBrush;
				}
				else {
					textBlock.Foreground = this.IsSet(ForegroundProperty) ? this.Foreground : hyperlinkBrush;
				}

				// Subscribe to changes on the containingTextBlock Foreground
				parentForegroundSubscription = containingTextBlock.GetObservable(TextBlock.ForegroundProperty)
					.Subscribe(f => {
						if (containingTextBlock.IsSet(TextBlock.ForegroundProperty)) {
							textBlock.Foreground = containingTextBlock.Foreground ?? this.Foreground ?? hyperlinkBrush;
						}
						else {
							textBlock.Foreground = this.IsSet(ForegroundProperty) ? this.Foreground : hyperlinkBrush;
						}
					});
			}
			else {
				// No containing TextBlock found
				textBlock.Foreground = this.Foreground ?? hyperlinkBrush;

				// Observe our own Foreground for changes and update
				selfForegroundSubscription = this.GetObservable(ForegroundProperty)
					.Subscribe(f => {
						textBlock.Foreground = this.IsSet(ForegroundProperty) ? this.Foreground : hyperlinkBrush;
					});
			}
		}

		private void UnsubscribeFromForegroundChanges() {
			parentForegroundSubscription?.Dispose();
			parentForegroundSubscription = null;

			selfForegroundSubscription?.Dispose();
			selfForegroundSubscription = null;
		}

		private void UpdateForegroundFromNearestSource(IBrush? fallback) {
			TextBlock? containingTextBlock = FindContainingTextBlock();
			if (containingTextBlock != null) {
				textBlock.Foreground = containingTextBlock.Foreground ?? this.Foreground ?? fallback;
			}
			else {
				textBlock.Foreground = this.Foreground ?? fallback;
			}
		}

		protected void OnPointerChanged(object? sender, PointerEventArgs e) {
			if (border.IsPointerOver) {
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
