using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using SharpEditorAvalonia.DataManagers;
using SharpSheets.Documentation;
using System.Text.RegularExpressions;
using System.Reflection;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Data;

namespace SharpEditorAvalonia.ContentBuilders {

	// FrameworkElement -> Control
	// UIElement -> Control

	public static class BaseContentBuilder {

		public static TextBlock GetContentTextBlock(Thickness margin) {
			return new TextBlock() {
				Inlines = new InlineCollection(),
				TextWrapping = TextWrapping.Wrap,
				Margin = margin,
				//HorizontalAlignment = HorizontalAlignment.Center
			};
		}

		public static TextBlock GetContentTextBlock(string? text, Thickness margin) {
			TextBlock block = GetContentTextBlock(margin);
			if (text is not null) { block.Text = text; }
			return block;
		}

		public static TextBlock GetContentTextBlock(string? text, Thickness margin, double sizeRatio) {
			TextBlock block = GetContentTextBlock(margin);
			if (text is not null) { block.Text = text; }
			MakeFontSizeRelative(block, sizeRatio);
			return block;
		}

		public static TextBlock GetContentTextBlock(IEnumerable<Inline> inlines, Thickness margin) {
			TextBlock block = GetContentTextBlock(margin);
			block.Inlines?.AddRange(inlines);
			return block;
		}

		public static T MakeFontSizeRelative<T>(this T block, double ratio) where T : TextBlock {
			//block.LayoutTransform = new ScaleTransform(ratio, ratio);
			block.RenderTransform = new ScaleTransform(ratio, ratio); // TODO Is this going to work?
			block.RenderTransformOrigin = new RelativePoint(0, 1, RelativeUnit.Relative);
			return block;
		}

		public static T AddMargin<T>(this T element, Thickness margin) where T : Control {
			Thickness initial = element.Margin;
			element.Margin = initial + margin;
			return element;
		}

		public static T AddIndent<T>(this T element, double indent) where T : Control {
			Thickness margin = element.Margin;
			element.Margin = margin + new Thickness(indent, 0, 0, 0);
			return element;
		}

		private static Color ConvertColor(SharpSheets.Colors.Color color) {
			return Color.FromArgb(color.A, color.R, color.G, color.B);
		}

		public static Color? GetColorFromValue(object? value) {
			Color? color = null;
			if (value is SharpSheets.Colors.Color drawingColor) {
				color = ConvertColor(drawingColor);
			}
			else if (value is string colorStr) {
				try {
					color = ConvertColor(ColorUtils.Parse(colorStr));
				}
				catch (FormatException) { }
			}
			return color;
		}

		public static Inline GetValueInline(Type type, object? value, bool isDefault) { // TextBlock parent
			string currentValueString = SharpValueHandler.GetValueString(type, value);
			Run valueRun = new Run(currentValueString);

			if (isDefault) {
				valueRun.Foreground = SharpEditorPalette.DefaultValueBrush;
			}
			else if (SharpEditorPalette.GetValueBrush(type) is Brush valueBrush) {
				valueRun.Foreground = valueBrush;
			}

			if (type.GetUnderlyingType() == typeof(SharpSheets.Colors.Color)) {
				Color? color = GetColorFromValue(value);

				Span valueSpan = new Span();
				valueSpan.Inlines.Add(valueRun);
				valueSpan.Inlines.Add(new Run(SharpValueHandler.NO_BREAK_SPACE.ToString()));
				
				valueSpan.Inlines.Add(GetColorInline(color, isDefault, false));

				return valueSpan;
			}

			// If no other additions are to be made, simply return the value inline
			return valueRun;
		}

		public static Inline GetColorInline(Color? color, bool isDefault, bool unknown) {
			Control finalColorSymbol;

			Rectangle colorBlock = new Rectangle() { Width = 10.0, Height = 10.0, Fill = (!unknown && color.HasValue) ? new SolidColorBrush(color.Value) : Brushes.Transparent };
			if (unknown || isDefault || !color.HasValue || color.Value.A < 100) {
				colorBlock.Stroke = SharpEditorPalette.DefaultValueBrush;
				colorBlock.StrokeThickness = 0.5;
			}

			if (unknown) {
				Brush lineBrush = SharpEditorPalette.DefaultValueBrush;
				Canvas canvas = new Canvas() { Height = 10.0, Width = 10.0 };
				canvas.Children.Add(new Line() { StartPoint = new Point(5.0, 0.0), EndPoint = new Point(5.0, 10.0), Stroke = lineBrush });
				canvas.Children.Add(new Line() { StartPoint = new Point(0.0, 5.0), EndPoint = new Point(10.0, 5.0), Stroke = lineBrush });
				canvas.Children.Add(colorBlock);
				finalColorSymbol = canvas;
			}
			else if (!color.HasValue || color.Value.A == 0) {
				Brush lineBrush = color.HasValue ? SharpEditorPalette.DefaultValueBrush : new SolidColorBrush(Colors.Red);
				Canvas canvas = new Canvas() { Height = 10.0, Width = 10.0 };
				canvas.Children.Add(new Line() { StartPoint = new Point(0.0, 0.0), EndPoint = new Point(10.0, 10.0), Stroke = lineBrush });
				canvas.Children.Add(new Line() { StartPoint = new Point(0.0, 10.0), EndPoint = new Point(10.0, 0.0), Stroke = lineBrush });
				canvas.Children.Add(colorBlock);
				finalColorSymbol = canvas;
			}
			else {
				finalColorSymbol = colorBlock;
			}

			DockPanel panel = new DockPanel() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
			Viewbox viewbox = new Viewbox() { Child = finalColorSymbol, Stretch = Stretch.Uniform, Margin = new Thickness(0.5, 0.0, 0.5, 1.0) };

			/*
			// Is this binding thing actually helping?
			Binding parentFontSizeBinding = new Binding("FontSize") { Source = parent, RelativeSource = RelativeSource. };
			viewbox.SetBinding(Viewbox.HeightProperty, parentFontSizeBinding);
			*/

			// TODO Is this right now?
			Binding parentFontSizeBinding = new Binding("FontSize", BindingMode.OneWay) {
				RelativeSource = new RelativeSource() {
					Mode = RelativeSourceMode.FindAncestor,
					AncestorLevel = 1,
					AncestorType = typeof(TextBlock)
				},
			};
			viewbox.Bind(Viewbox.HeightProperty, parentFontSizeBinding);

			panel.Children.Add(viewbox);

			//Canvas canvas = new Canvas() { VerticalAlignment = VerticalAlignment.Center };
			//canvas.Children.Add(new Rectangle() { Height = 6, Width = 6, Fill = new SolidColorBrush(color.Value), VerticalAlignment = VerticalAlignment.Center });

			return new InlineUIContainer(panel) { BaselineAlignment = BaselineAlignment.Bottom };
		}

		public class DocumentationInlineProcessor : IDocumentationSpanVisitor<Inline> {
			public static readonly DocumentationInlineProcessor Instance = new DocumentationInlineProcessor();

			protected DocumentationInlineProcessor() { }

			public Inline Visit(TextSpan span) {
				string content = span.Text;
				content = Regex.Replace(content, @"\-\-\-", "\u2014");
				content = Regex.Replace(content, @"\-\-", "\u2013");
				return new Run(content);
			}

			public Inline Visit(LineBreakSpan span) {
				return new Run("\n\n"); // Hacky, but it works
			}

			public virtual Inline Visit(TypeSpan span) {
				if (span.Type is not null && typeof(SharpSheets.Markup.Elements.IMarkupElement).IsAssignableFrom(span.Type)) {

					if (!SharpSheets.Markup.Parsing.MarkupDocumentation.MarkupConstructors.TryGetValue(span.Type, out ConstructorDetails? markupTypeConstructor)) {
						SharpSheets.Markup.Parsing.MarkupDocumentation.MarkupConstructors.TryGetValue(span.Name, out markupTypeConstructor);
					}

					if (markupTypeConstructor is not null) {
						Span markupTypeSpan = new Span();
						markupTypeSpan.Inlines.AddRange(XMLContentBuilder.MakeMarkupTypeHeader(markupTypeConstructor));
						return markupTypeSpan;
					}
				}

				return new Run(span.Type is not null ? SharpValueHandler.GetTypeName(span.Type) : span.Name) {
					Foreground = SharpEditorPalette.GetTypeBrush(span.Type)
				};
			}

			public virtual Inline Visit(ParameterSpan span) {
				return new Run("\"" + span.Parameter + "\"");
			}

			protected virtual Inline EnumTypeInline(EnumValueSpan span) {
				return new Run(span.Type) { Foreground = SharpEditorPalette.TypeBrush };
			}

			public Inline Visit(EnumValueSpan span) {
				Span enumSpan = new Span();
				enumSpan.Inlines.Add(EnumTypeInline(span));
				enumSpan.Inlines.Add(new Run("." + span.Value));
				return enumSpan;
			}
		}

		public static TextBlock? MakeDescriptionTextBlock(DocumentationString? descriptionString, Thickness margin, DocumentationInlineProcessor? processor = null) {
			if (descriptionString is null || descriptionString.Spans.Length == 0) {
				return null;
			}

			DocumentationInlineProcessor finalProcessor = processor ?? DocumentationInlineProcessor.Instance;

			TextBlock descriptionBlock = BaseContentBuilder.GetContentTextBlock(margin);

			descriptionBlock.Inlines?.AddRange(descriptionString.Process(finalProcessor));

			return descriptionBlock;
		}

	}

}
