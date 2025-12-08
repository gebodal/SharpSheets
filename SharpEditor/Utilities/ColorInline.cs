using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using System;
using Avalonia.Layout;
using Avalonia.Controls.Shapes;
using SharpEditor.DataManagers;

namespace SharpEditor.Utilities {

	public class ColorInline : InlineUIContainer {

		private static readonly double fallbackSize = 10.0;

		private readonly Viewbox viewbox;

		private IDisposable? ancestorFontSizeSubscription;
		private IDisposable? selfFontSizeSubscription;

		// Public factory-like constructor (similar to your GetColorInline parameters)
		public ColorInline(Color? color, bool isDefault, bool unknown) {
			viewbox = new Viewbox {
				Child = BuildSymbol(color, isDefault, unknown),
				Stretch = Stretch.Uniform,
				Margin = new Thickness(0.5, 0.0, 0.5, 1.0),
				// Give an initial size; will be overridden by subscriptions when available.
				Height = fallbackSize
			};

			DockPanel panel = new DockPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
			panel.Children.Add(viewbox);

			this.Child = panel;
			this.BaselineAlignment = BaselineAlignment.Bottom;

			// Manage subscription lifecycle
			this.AttachedToLogicalTree += OnAttached;
			this.DetachedFromLogicalTree += OnDetached;

			// Apply a reasonable initial font-size-based height: prefer an explicit parent if present later.
			viewbox.Height = fallbackSize;
		}

		private static Canvas BuildSymbol(Color? color, bool isDefault, bool unknown) {
			Rectangle colorBlock = new Rectangle {
				Width = 10.0,
				Height = 10.0,
				Fill = (!unknown && color.HasValue) ? new SolidColorBrush(color.Value) : Brushes.Transparent
			};

			if (unknown || isDefault || !color.HasValue || color.Value.A < 100) {
				colorBlock.Stroke = SharpEditorPalette.DefaultValueBrush;
				colorBlock.StrokeThickness = 0.5;
			}

			Canvas canvas = new Canvas { Height = 10.0, Width = 10.0 };

			if (unknown) {
				Brush lineBrush = SharpEditorPalette.DefaultValueBrush;
				canvas.Children.Add(new Line { StartPoint = new Point(5.0, 0.0), EndPoint = new Point(5.0, 10.0), Stroke = lineBrush });
				canvas.Children.Add(new Line { StartPoint = new Point(0.0, 5.0), EndPoint = new Point(10.0, 5.0), Stroke = lineBrush });
				canvas.Children.Add(colorBlock);
			}
			else if (!color.HasValue || color.Value.A == 0) {
				Brush lineBrush = color.HasValue ? SharpEditorPalette.DefaultValueBrush : new SolidColorBrush(Colors.Red);
				canvas.Children.Add(new Line { StartPoint = new Point(0.0, 0.0), EndPoint = new Point(10.0, 10.0), Stroke = lineBrush });
				canvas.Children.Add(new Line { StartPoint = new Point(0.0, 10.0), EndPoint = new Point(10.0, 0.0), Stroke = lineBrush });
				canvas.Children.Add(colorBlock);
			}
			else {
				canvas.Children.Add(colorBlock);
			}

			return canvas;
		}

		private void OnAttached(object? sender, LogicalTreeAttachmentEventArgs e) {
			SubscribeToFontSize();
		}

		private void OnDetached(object? sender, LogicalTreeAttachmentEventArgs e) {
			UnsubscribeFromFontSize();
		}

		private void SubscribeToFontSize() {
			UnsubscribeFromFontSize();

			// Try to find a containing TextBlock (logical/visual search).
			TextBlock? tb = FindContainingTextBlock();

			if (tb != null) {
				// If ancestor has a set FontSize, prefer it; otherwise prefer our own FontSize if set,
				// and finally use a numeric fallback.
				void ApplyFromAncestor() {
					if (tb.IsSet(TextBlock.FontSizeProperty)) {
						viewbox.Height = tb.FontSize;
					}
					else if (this.IsSet(FontSizeProperty)) {
						viewbox.Height = (double)this.GetValue(FontSizeProperty)!;
					}
					else {
						viewbox.Height = fallbackSize;
					}
				}

				// Initial application
				ApplyFromAncestor();

				// Subscribe to ancestor font-size changes (GetObservable fires immediately with current value)
				ancestorFontSizeSubscription = tb.GetObservable(TextBlock.FontSizeProperty)
					.Subscribe(_ => ApplyFromAncestor());

				// Also subscribe to our own FontSize changes so explicit changes on this Inline are picked up
				// when the ancestor doesn't provide a value.
				selfFontSizeSubscription = this.GetObservable(FontSizeProperty)
					.Subscribe(_ => ApplyFromAncestor());
			}
			else {
				// No ancestor TextBlock: use our own FontSize if set, otherwise numeric fallback
				if (this.IsSet(FontSizeProperty)) {
					viewbox.Height = (double)this.GetValue(FontSizeProperty)!;
				}
				else {
					viewbox.Height = fallbackSize;
				}

				// Subscribe to our own FontSize changes
				selfFontSizeSubscription = this.GetObservable(FontSizeProperty)
					.Subscribe(f => {
						// f may be null if unset, so fallback numeric used
						if (f is double d) {
							viewbox.Height = d;
						}
						else {
							viewbox.Height = fallbackSize;
						}
					});
			}
		}

		private void UnsubscribeFromFontSize() {
			ancestorFontSizeSubscription?.Dispose();
			ancestorFontSizeSubscription = null;

			selfFontSizeSubscription?.Dispose();
			selfFontSizeSubscription = null;
		}

		private TextBlock? FindContainingTextBlock() {
			ILogical? logical = this;
			while (logical != null) {
				if (logical is TextBlock tbLogical) {
					return tbLogical;
				}
				logical = logical.LogicalParent;
			}
			return null;
		}
	}

}
