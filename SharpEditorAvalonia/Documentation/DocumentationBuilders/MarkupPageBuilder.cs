using SharpEditorAvalonia.ContentBuilders;
using SharpSheets.Documentation;
using System;
using System.Collections.Generic;
using SharpEditorAvalonia.DataManagers;
using static SharpEditorAvalonia.ContentBuilders.BaseContentBuilder;
using static SharpEditorAvalonia.Documentation.DocumentationBuilders.BaseDocumentationBuilder;

namespace SharpEditorAvalonia.Documentation.DocumentationBuilders {

	public static class MarkupPageBuilder {

		public static DocumentationPage GetMarkupElementPage(ConstructorDetails constructor, DocumentationWindow window, Func<ConstructorDetails?>? refreshAction) {
			if (constructor == null) {
				return MakeErrorPage("Invalid constructor.");
			}

			return MakePage(GetMarkupElementPageContent(constructor, window), constructor.Name, () => GetMarkupElementPageContent(refreshAction?.Invoke(), window));
		}

		private static UIElement GetMarkupElementPageContent(ConstructorDetails? constructor, DocumentationWindow window) {
			if (constructor == null) {
				return MakeErrorContent("Invalid constructor.");
			}

			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock headerBlock = GetContentTextBlock(XMLContentBuilder.MakeMarkupTypeHeader(constructor), TextBlockMargin);
			headerBlock.MakeFontSizeRelative(2.0);
			stack.Children.Add(headerBlock);

			if (MakeDescriptionTextBlock(constructor.Description, window) is TextBlock descriptionBlock) {
				//TextBlock descriptionBlock = BaseContentBuilder.GetContentTextBlock(constructor.Description, IndentedMargin);
				stack.Children.Add(descriptionBlock);
			}

			if (constructor.Arguments.Length > 0) {
				stack.Children.Add(MakeSeparator());

				foreach (ConstructorArgumentDetails arg in constructor.ConstructorArguments) {
					stack.Children.Add(MakeSingleMarkupArgumentBlocks(arg, window).SetMargin(ParagraphSpacingMargin));
				}
			}

			return stack;
		}

		private static FrameworkElement MakeSingleMarkupArgumentBlocks(ConstructorArgumentDetails argument, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			Type resolvedType = GetArgumentType(argument.ArgumentType, out bool isExpression);

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

		private static TextBlock MakeMarkupArgumentHeaderBlock(ConstructorArgumentDetails argument, Type resolvedType, bool isExpression, DocumentationWindow window) {
			TextBlock argumentBlock = GetContentTextBlock(TextBlockMargin);

			string typeName = XMLContentBuilder.GetTypeName(argument.ArgumentType, out _);

			Run typeRun;
			if (resolvedType.IsEnum && SharpDocumentation.GetEnumDoc(resolvedType) is EnumDoc enumDoc) {
				ClickableRun enumClickable = new ClickableRun(typeName) { Foreground = SharpEditorPalette.TypeBrush };
				enumClickable.MouseLeftButtonDown += window.MakeNavigationDelegate(enumDoc, null); // TODO Passing the type back in won't change anything for refresh
				typeRun = enumClickable;
			}
			else {
				typeRun = new Run(typeName) { Foreground = SharpEditorPalette.TypeBrush };
			}
			argumentBlock.Inlines.Add(typeRun);

			argumentBlock.Inlines.Add(new Run(SharpValueHandler.NO_BREAK_SPACE.ToString()));

			ArgumentDetails arg = argument.Argument;
			//while (arg != null && arg is PrefixedArgumentDetails prefixed) { arg = prefixed.Basis; } // What was this supposed to be doing?

			argumentBlock.Inlines.Add(new Run(arg.Name) { });

			if (argument.Implied != null) {
				argumentBlock.Inlines.Add(new Run("." + argument.Implied));
			}

			argumentBlock.Inlines.AddRange(ConstructorContentBuilder.GetArgumentDefaultInlines(argument.Argument, null));

			return argumentBlock;
		}

		private static Type GetArgumentType(Type argType, out bool isExpression) {
			if (XMLContentBuilder.ResolveExpressionType(argType) is Type exprType) {
				isExpression = true;
				return exprType;
			}
			else {
				XMLContentBuilder.GetTypeName(argType, out bool concrete);
				isExpression = !concrete;
				return argType;
			}
		}

		private static Type GetArgumentType(ArgumentType argType, out bool isExpression) {
			return GetArgumentType(argType.DisplayType, out isExpression);
		}

	}

}
