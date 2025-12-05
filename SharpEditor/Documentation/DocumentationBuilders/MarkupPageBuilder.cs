using SharpEditor.ContentBuilders;
using SharpSheets.Documentation;
using System;
using System.Collections.Generic;
using SharpEditor.DataManagers;
using static SharpEditor.ContentBuilders.BaseContentBuilder;
using static SharpEditor.Documentation.DocumentationBuilders.BaseDocumentationBuilder;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Documents;
using SharpEditor.Utilities;

namespace SharpEditor.Documentation.DocumentationBuilders {

	// UIElement -> Control
	// FrameworkElement -> Control

	public static class MarkupPageBuilder {

		public static DocumentationPage GetMarkupElementPage(BuilderDetails builder, DocumentationWindow window, Func<BuilderDetails?>? refreshAction) {
			if (builder == null) {
				return MakeErrorPage("Invalid builder.");
			}

			return MakePage(GetMarkupElementPageContent(builder, window), builder.Name, () => GetMarkupElementPageContent(refreshAction?.Invoke(), window));
		}

		private static Control GetMarkupElementPageContent(BuilderDetails? builder, DocumentationWindow window) {
			if (builder == null) {
				return MakeErrorContent("Invalid builder.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = GetContentTextBlock(XMLContentBuilder.MakeMarkupTypeHeader(builder), TextBlockMargin);
			headerBlock.MakeFontSizeRelative(TextBlockClass.H3);
			stack.Children.Add(headerBlock);

			if (MakeDescriptionTextBlock(builder.Description, window) is TextBlock descriptionBlock) {
				//TextBlock descriptionBlock = BaseContentBuilder.GetContentTextBlock(constructor.Description, IndentedMargin);
				stack.Children.Add(descriptionBlock);
			}

			if (builder.Arguments.Length > 0) {
				stack.Children.Add(MakeSeparator());

				foreach (BuilderArgumentDetails arg in builder.BuilderArguments) {
					stack.Children.Add(MakeSingleMarkupArgumentBlocks(arg, window).SetMargin(ParagraphSpacingMargin));
				}
			}

			return stack;
		}

		private static Control MakeSingleMarkupArgumentBlocks(BuilderArgumentDetails argument, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			DisplayType resolvedType = GetArgumentType(argument.ArgumentType, out bool isExpression);

			argPanel.Children.Add(MakeMarkupArgumentHeaderBlock(argument, resolvedType, isExpression, window));

			if (MakeDescriptionTextBlock(argument.ArgumentDescription, window) is TextBlock argDescriptionBlock) {
				//argPanel.Children.Add(BaseContentBuilder.GetContentTextBlock(argument.ArgumentDescription, ArgumentDetailsMargin));
				argPanel.Children.Add(argDescriptionBlock);
			}

			if (resolvedType.IsEnum && SharpDocumentation.GetEnumDoc(resolvedType) is EnumDoc enumDoc) {
				argPanel.Children.Add(EnumContentBuilder.MakeEnumOptionsBlock(enumDoc, ArgumentDetailsMargin));
			}

			if (!isExpression || !argument.IsOptional || argument.UseLocal) {
				List<string> notes = new List<string>();
				if (!isExpression) { notes.Add("Concrete value"); }
				if (!argument.IsOptional) { notes.Add("Required"); }
				if (argument.UseLocal) { notes.Add("Local"); }
				string finalNote = "(" + string.Join(", ", notes) + ")";
				argPanel.Children.Add(GetContentTextBlock(finalNote, ArgumentDetailsMargin));
			}

			return argPanel;
		}

		private static TextBlock MakeMarkupArgumentHeaderBlock(BuilderArgumentDetails argument, DisplayType resolvedType, bool isExpression, DocumentationWindow window) {
			TextBlock argumentBlock = GetContentTextBlock(TextBlockMargin);

			string typeName = XMLContentBuilder.GetTypeName(argument.ArgumentType, out _);

			Inline typeInline;
			if (resolvedType.IsEnum && SharpDocumentation.GetEnumDoc(resolvedType) is EnumDoc enumDoc) {
				ClickableRun enumClickable = new ClickableRun(typeName) { Foreground = SharpEditorPalette.TypeBrush };
				enumClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(enumDoc, null); // TODO Passing the type back in won't change anything for refresh
				typeInline = enumClickable;
			}
			else {
				typeInline = new Run(typeName) { Foreground = SharpEditorPalette.TypeBrush };
			}
			argumentBlock.Inlines?.Add(typeInline);

			argumentBlock.Inlines?.Add(new Run(SharpValueHandler.NO_BREAK_SPACE.ToString()));

			ArgumentDetails arg = argument.Argument;
			//while (arg != null && arg is PrefixedArgumentDetails prefixed) { arg = prefixed.Basis; } // What was this supposed to be doing?

			argumentBlock.Inlines?.Add(new Run(arg.Name) { });

			if (argument.Implied != null) {
				argumentBlock.Inlines?.Add(new Run("." + argument.Implied));
			}

			argumentBlock.Inlines?.AddRange(BuilderContentBuilder.GetArgumentDefaultInlines(argument.Argument, null));

			return argumentBlock;
		}

		private static DisplayType GetArgumentType(DisplayType argType, out bool isExpression) {
			if (XMLContentBuilder.ResolveExpressionType(argType) is DisplayType exprType) {
				isExpression = true;
				return exprType;
			}
			else {
				XMLContentBuilder.GetTypeName(argType, out bool concrete);
				isExpression = !concrete;
				return argType;
			}
		}

		private static DisplayType GetArgumentType(ArgumentType argType, out bool isExpression) {
			return GetArgumentType(argType.DisplayType, out isExpression);
		}

	}

}
