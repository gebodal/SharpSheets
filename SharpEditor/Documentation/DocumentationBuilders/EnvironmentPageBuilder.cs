using SharpEditor.ContentBuilders;
using SharpSheets.Documentation;
using SharpSheets.Utilities;
using System;
using SharpSheets.Cards.Definitions;
using SharpEditor.DataManagers;
using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using System.Linq;
using static SharpEditor.ContentBuilders.BaseContentBuilder;
using static SharpEditor.Documentation.DocumentationBuilders.BaseDocumentationBuilder;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Documents;
using Avalonia;

namespace SharpEditor.Documentation.DocumentationBuilders {

	// UIElement -> Control
	// FrameworkElement -> Control

	public static class EnvironmentPageBuilder {

		public static Control GetEnvironmentVariablesContents(IVariableBox variables, DocumentationWindow window) {
			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			if (variables is DefinitionGroup definitionGroup) {
				foreach (Definition definition in definitionGroup.OrderBy(d => d.name)) {
					Control varElem = MakeEnvironmentDefinitionBlock(definition, window);
					varElem.AddMargin(ParagraphSpacingMargin);
					stack.Children.Add(varElem);
				}
			}
			else {
				foreach (EnvironmentVariableInfo varInfo in variables.GetVariables().OrderBy(v => v.Name)) {
					Control varElem = MakeEnvironmentVariableBlock(varInfo, window);
					varElem.AddMargin(ParagraphSpacingMargin);
					stack.Children.Add(varElem);
				}
			}

			return stack;
		}

		public static Control GetEnvironmentFunctionsContents(IVariableBox variables, DocumentationWindow window) {
			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			foreach (IEnvironmentFunctionInfo funcInfo in variables.GetFunctionInfos().OrderBy(f => f.Name.ToString(), StringComparer.OrdinalIgnoreCase)) {
				Control funcElem = MakeEnvironmentFunctionBlock(funcInfo, window);
				funcElem.AddMargin(ParagraphSpacingMargin);
				stack.Children.Add(funcElem);
			}

			/*
			foreach ((IEnvironmentFunctionInfo funcInfo, EnvironmentFunctionArgList args) in markupVariables.GetFunctionInfos().SelectMany(f => f.Args.Select(a => (f, a))).OrderBy(i => i.f.Name.ToString(), StringComparer.OrdinalIgnoreCase).ThenBy(i => i.a.Arguments.Length)) {
				FrameworkElement funcElem = MakeEnvironmentFunctionBlock(funcInfo, args, window);
				funcElem.AddMargin(ParagraphSpacingMargin);
				stack.Children.Add(funcElem);
			}
			*/

			return stack;
		}

		private static Control MakeEnvironmentDefinitionBlock(Definition definition, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			string typeName = SharpValueHandler.GetEnvironmentTypeName(definition.Type);

			TextBlock titleBlock = GetContentTextBlock(TextBlockMargin);
			titleBlock.Inlines?.Add(new Run(definition.name.ToString()) { Foreground = SharpEditorPalette.EnvironmentNameBrush });
			foreach (EvaluationName alias in definition.aliases) {
				titleBlock.Inlines?.Add(new Run(" | "));
				titleBlock.Inlines?.Add(new Run(alias.ToString()) { Foreground = SharpEditorPalette.EnvironmentNameBrush });
			}
			//titleBlock.Inlines.Add(new Run(":" + SharpValueHandler.NO_BREAK_SPACE.ToString()));
			titleBlock.Inlines?.Add(new Run(": "));
			titleBlock.Inlines?.Add(new Run(typeName) { Foreground = SharpEditorPalette.EnvironmentTypeBrush });

			argPanel.Children.Add(titleBlock);

			if (definition.description is not null && MakeDescriptionTextBlock(new DocumentationString(definition.description), window) is TextBlock descriptionBlock) {
				argPanel.Children.Add(descriptionBlock);
			}

			return argPanel;
		}

		private static Control MakeEnvironmentFunctionBlock(IEnvironmentFunctionInfo functionInfo, EnvironmentFunctionArgList args, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock titleBlock = GetContentTextBlock(TextBlockMargin);

			titleBlock.Inlines?.Add(new Run(functionInfo.Name.ToString()) { Foreground = SharpEditorPalette.EnvironmentNameBrush });
			titleBlock.Inlines?.Add(new Run("("));

			for (int a = 0; a < args.Arguments.Length; a++) {
				if (a > 0) {
					//titleBlock.Inlines.Add(new Run("," + SharpValueHandler.NO_BREAK_SPACE.ToString()));
					titleBlock.Inlines?.Add(new Run(", "));
				}

				string? argTypeName = SharpValueHandler.GetEnvironmentTypeName(args.Arguments[a].ArgType);

				titleBlock.Inlines?.Add(new Run(args.Arguments[a].Name.ToString()) { Foreground = SharpEditorPalette.EnvironmentNameBrush });
				titleBlock.Inlines?.Add(new Run(":" + SharpValueHandler.NO_BREAK_SPACE.ToString()));
				titleBlock.Inlines?.Add(new Run(argTypeName) { Foreground = SharpEditorPalette.EnvironmentTypeBrush });
			}

			titleBlock.Inlines?.Add(new Run(")"));

			titleBlock.MakeFontSizeRelative(TextBlockClass.H7);

			argPanel.Children.Add(titleBlock);

			argPanel.Children.Add(new Separator() {
				Margin = new Thickness(0, 0.0, 0, 4.0)
			});

			if (functionInfo.Description is not null && MakeDescriptionTextBlock(new DocumentationString(functionInfo.Description), window) is TextBlock descriptionBlock) {
				argPanel.Children.Add(descriptionBlock);
			}

			return argPanel;
		}

		private static Control MakeEnvironmentFunctionBlock(IEnvironmentFunctionInfo functionInfo, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock titleBlock = GetContentTextBlock(TextBlockMargin);

			EnvironmentFunctionArgList[] funcArgLists = functionInfo.Args.OrderBy(a => a.Arguments.Length).ToArray();

			for (int i = 0; i < funcArgLists.Length; i++) {
				EnvironmentFunctionArgList args = funcArgLists[i];

				if (i > 0) { titleBlock.Inlines?.Add(new LineBreak()); }

				titleBlock.Inlines?.Add(new Run(functionInfo.Name.ToString()) { Foreground = SharpEditorPalette.EnvironmentNameBrush });
				titleBlock.Inlines?.Add(new Run("("));

				for (int a = 0; a < args.Arguments.Length; a++) {
					if (a > 0) {
						//titleBlock.Inlines.Add(new Run("," + SharpValueHandler.NO_BREAK_SPACE.ToString()));
						titleBlock.Inlines?.Add(new Run(", "));
					}

					string? argTypeName = SharpValueHandler.GetEnvironmentTypeName(args.Arguments[a].ArgType);

					titleBlock.Inlines?.Add(new Run(args.Arguments[a].Name.ToString()) { Foreground = SharpEditorPalette.EnvironmentNameBrush });
					titleBlock.Inlines?.Add(new Run(":" + SharpValueHandler.NO_BREAK_SPACE.ToString()));
					titleBlock.Inlines?.Add(new Run(argTypeName) { Foreground = SharpEditorPalette.EnvironmentTypeBrush });
				}

				if (args.IsParams) {
					titleBlock.Inlines?.Add(new Run(SharpValueHandler.NO_BREAK_SPACE.ToString() + "..." + SharpValueHandler.NO_BREAK_SPACE.ToString()));
				}

				titleBlock.Inlines?.Add(new Run(")"));
			}

			titleBlock.MakeFontSizeRelative(TextBlockClass.H7);

			argPanel.Children.Add(titleBlock);

			argPanel.Children.Add(new Separator() {
				Margin = new Thickness(0, 0.0, 0, 4.0)
			});

			if (functionInfo.Description is not null && MakeDescriptionTextBlock(new DocumentationString(functionInfo.Description), window) is TextBlock descriptionBlock) {
				argPanel.Children.Add(descriptionBlock);
			}

			return argPanel;
		}

		private static Control MakeEnvironmentVariableBlock(EnvironmentVariableInfo variableInfo, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			string typeName = SharpValueHandler.GetEnvironmentTypeName(variableInfo.EvaluationType);

			TextBlock titleBlock = GetContentTextBlock(TextBlockMargin);
			titleBlock.Inlines?.Add(new Run(variableInfo.Name.ToString()) { Foreground = SharpEditorPalette.EnvironmentNameBrush });
			//titleBlock.Inlines.Add(new Run(":" + SharpValueHandler.NO_BREAK_SPACE.ToString()));
			titleBlock.Inlines?.Add(new Run(": "));
			titleBlock.Inlines?.Add(new Run(typeName) { Foreground = SharpEditorPalette.EnvironmentTypeBrush });

			argPanel.Children.Add(titleBlock);

			if (variableInfo.Description is not null && MakeDescriptionTextBlock(new DocumentationString(variableInfo.Description), window) is TextBlock descriptionBlock) {
				argPanel.Children.Add(descriptionBlock);
			}

			return argPanel;
		}

	}

}
