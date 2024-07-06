using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SharpEditor.ContentBuilders;
using SharpEditor.Utilities;
using SharpSheets.Documentation;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using static SharpEditor.ContentBuilders.BaseContentBuilder;

namespace SharpEditor.Documentation.DocumentationBuilders {

	// UIElement -> Control

	public static class BaseDocumentationBuilder {

		public static Thickness TextBlockMargin { get; } = new Thickness(7, 4, 7, 4);
		public static Thickness IndentedMargin { get; } = new Thickness(7 + 15, 4, 7, 4);
		public static Thickness ArgumentDetailsMargin { get; } = new Thickness(7 + 15 + 10, 4, 7, 4);

		public static Thickness TitleMargin { get; } = new Thickness(7, 4, 7, 10);
		public static Thickness SectionMargin { get; } = new Thickness(0, 0, 0, 30);
		public static Thickness ParagraphMargin { get; } = new Thickness(7, 4, 7, 10);
		public static Thickness ParagraphSpacingMargin { get; } = new Thickness(0, 0, 0, 10);

		public static Thickness ContentsBlockMargin { get; } = new Thickness(30, 0, 0, 0);
		public static Thickness CanvasMargin { get; } = new Thickness(7, 20, 7, 20);

		public static DocumentationPage MakePage(Control content, string name, DocumentationRefresh? refreshAction) {
			DocumentationPage page = new DocumentationPage() {
				RefreshAction = refreshAction,
				Title = name,
				//Foreground = Brushes.White,
				//FontSize = 14
			};
			page.SetPageContent(content);
			return page;
		}

		public static DocumentationPage MakeErrorPage(string message) {
			return MakePage(MakeErrorContent(message), "Error", null);
		}

		public static Control MakeErrorContent(string message) {
			return new TextBlock() { Text = message };
		}

		public static TextBlock MakeTitleBlock(IEnumerable<Inline> inlines, int level, TextWrapping textWrapping = TextWrapping.Wrap) {
			TextBlock titleBlock = new TextBlock() { TextWrapping = textWrapping, Margin = TitleMargin, Inlines = new InlineCollection() };
			titleBlock.Inlines.AddRange(inlines);

			titleBlock.MakeFontSizeRelative(level switch {
				int i when i <= 0 => TextBlockClass.H1, // 3.0
				1 => TextBlockClass.H2, // 2.5
				2 => TextBlockClass.H3, // 2.0
				3 => TextBlockClass.H4, // 1.5
				4 => TextBlockClass.H5, // 1.25
				5 => TextBlockClass.H6, // 1.15
				_ => TextBlockClass.H7 // 1.1
			});

			return titleBlock;
		}

		public static TextBlock MakeTitleBlock(string title, int level, TextWrapping textWrapping = TextWrapping.Wrap) {
			return MakeTitleBlock(new Run(title).Yield(), level, textWrapping);
		}

		public static TextBlock MakeTitleBlock(int level, TextWrapping textWrapping = TextWrapping.Wrap) {
			return MakeTitleBlock(Enumerable.Empty<Inline>(), level, textWrapping);
		}

		public static Grid MakeExternalLinkHeader(TextBlock headerBlock, string tooltip, out Button linkButton, DocumentationWindow window) {
			Grid headerGrid = new Grid() {
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			headerGrid.RowDefinitions.Add(new RowDefinition());

			Grid.SetColumn(headerBlock, 0);
			Grid.SetRow(headerBlock, 0);
			headerGrid.Children.Add(headerBlock);

			Uri bitmapUri = ResourceUtilities.GetAssetUri("Images/ExternalLink.png");
			Bitmap bitmap = new Bitmap(AssetLoader.Open(bitmapUri));

			linkButton = new Button {
				//Width = 32,
				//Height = 32,
				//HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				Content = new Image() {
					Source = bitmap,
					VerticalAlignment = VerticalAlignment.Bottom,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					Margin = new Thickness(3),
					Width = 26,
					Height = 26
				},
				//Background = Brushes.Transparent,
				//Style = window.FindResource("SubtleButton") as Style, // TODO How to deal with this?
				Opacity = 0.5
			};
			ToolTip.SetTip(linkButton, tooltip);

			Grid.SetColumn(linkButton, 1);
			Grid.SetRow(linkButton, 0);
			headerGrid.Children.Add(linkButton);

			return headerGrid;
		}

		private class DocumentationPageInlineProcessor : DocumentationInlineProcessor {
			private readonly DocumentationWindow window; // TODO Use this to make clickable links to types and enums

			public DocumentationPageInlineProcessor(DocumentationWindow window) : base() {
				this.window = window;
			}

			public override Inline Visit(TypeSpan span) {
				return base.Visit(span);
			}
			protected override Inline EnumTypeInline(EnumValueSpan span) {
				return base.EnumTypeInline(span);
			}
		}

		public static TextBlock? MakeDescriptionTextBlock(DocumentationString? descriptionString, DocumentationWindow window) {
			return BaseContentBuilder.MakeDescriptionTextBlock(descriptionString, IndentedMargin, new DocumentationPageInlineProcessor(window));
		}

		public static Separator MakeSeparator(double horizontalPadding = 15.0, double verticalPadding = 15.0) {
			return new Separator() {
				Foreground = Brushes.LightGray,
				Margin = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding)
			};
		}

	}
}
