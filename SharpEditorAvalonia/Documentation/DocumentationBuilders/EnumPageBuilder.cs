﻿using SharpSheets.Documentation;
using System;
using SharpEditorAvalonia.DataManagers;
using static SharpEditorAvalonia.ContentBuilders.BaseContentBuilder;
using static SharpEditorAvalonia.Documentation.DocumentationBuilders.BaseDocumentationBuilder;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Documents;
using SharpEditorAvalonia.ContentBuilders;

namespace SharpEditorAvalonia.Documentation.DocumentationBuilders {

	// UIElement -> Control

	public static class EnumPageBuilder {

		public static DocumentationPage GetEnumPage(EnumDoc enumDoc, DocumentationWindow window, Func<EnumDoc?>? refreshAction) {
			if (enumDoc == null) {
				return MakeErrorPage("Invalid enum documentation.");
			}

			return MakePage(GetEnumPageContent(enumDoc, window), enumDoc.type, () => GetEnumPageContent(refreshAction?.Invoke(), window));
		}

		private static Control GetEnumPageContent(EnumDoc? enumDoc, DocumentationWindow window) {
			if (enumDoc == null) {
				return MakeErrorContent("Invalid enum documentation.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = GetContentTextBlock(TextBlockMargin);
			headerBlock.Text = enumDoc.type;
			headerBlock.Foreground = SharpEditorPalette.TypeBrush;
			headerBlock.MakeFontSizeRelative(TextBlockClass.H3);
			stack.Children.Add(headerBlock);

			if (MakeDescriptionTextBlock(enumDoc.description, window) is TextBlock descriptionBlock) {
				//TextBlock descriptionBlock = BaseContentBuilder.GetContentTextBlock(enumDoc.description, IndentedMargin);
				stack.Children.Add(descriptionBlock);
			}

			stack.Children.Add(MakeSeparator());

			foreach (EnumValDoc enumVal in enumDoc.values) {
				StackPanel argPanel = new StackPanel() { Margin = ParagraphSpacingMargin, Orientation = Orientation.Vertical };

				TextBlock nameBlock = GetContentTextBlock(TextBlockMargin);
				nameBlock.Inlines?.Add(new Run(enumVal.type ?? enumDoc.type) { Foreground = SharpEditorPalette.TypeBrush });
				nameBlock.Inlines?.Add(new Run("." + enumVal.name));
				argPanel.Children.Add(nameBlock);

				if (MakeDescriptionTextBlock(enumVal.description, window) is TextBlock valDescriptionBlock) {
					//argPanel.Children.Add(BaseContentBuilder.GetContentTextBlock(enumVal.description, ArgumentDetailsMargin));
					argPanel.Children.Add(valDescriptionBlock);
				}

				stack.Children.Add(argPanel);
			}

			return stack;
		}

	}

}
