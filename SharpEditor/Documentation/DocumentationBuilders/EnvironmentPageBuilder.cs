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

			foreach (IEnvironmentFunctionInfo funcInfo in variables.GetFunctionInfos().Concat(variables.Context.GetRegisteredTypes().SelectNotNull(t => t.GetTypeFunction())).OrderBy(f => f.Name.ToString(), StringComparer.OrdinalIgnoreCase)) {
				Control funcElem = MakeEnvironmentFunctionBlock(funcInfo, variables.Context, window);
				funcElem.AddMargin(ParagraphSpacingMargin);
				stack.Children.Add(funcElem);
			}

			return stack;
		}

		public static Control GetEnvironmentTypesContents(EvaluationContext context, DocumentationWindow window) {
			StackPanel stack = new StackPanel() { Orientation = Orientation.Vertical };

			foreach (EvaluationType type in context.GetRegisteredTypes().OrderBy(t => t.Name.ToString(), StringComparer.OrdinalIgnoreCase)) {
				Control typeElem = MakeEvaluationTypeBlock(type, window);
				typeElem.AddMargin(ParagraphSpacingMargin);
				stack.Children.Add(typeElem);
			}

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

		private static TextBlock MakeFunctionArgListsTextBlock(EvaluationName name, EnvironmentFunctionArgList[] funcArgLists) {
			TextBlock titleBlock = GetContentTextBlock(TextBlockMargin);

			for (int i = 0; i < funcArgLists.Length; i++) {
				EnvironmentFunctionArgList args = funcArgLists[i];

				if (i > 0) { titleBlock.Inlines?.Add(new LineBreak()); }

				titleBlock.Inlines?.Add(new Run(name.ToString()) { Foreground = SharpEditorPalette.EnvironmentNameBrush });
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

			return titleBlock;
		}

		private static TextBlock MakeFunctionArgListsTextBlock(IEnvironmentFunctionInfo functionInfo, EvaluationContext context) {
			EnvironmentFunctionArgList[] funcArgLists = functionInfo.GetArguments(context).OrderBy(a => a.Arguments.Length).ToArray();
			return MakeFunctionArgListsTextBlock(functionInfo.Name, funcArgLists);
		}

		private static TextBlock MakeFunctionArgListsTextBlock(IMethod method) {
			EnvironmentFunctionArgList[] funcArgLists = method.GetArguments().OrderBy(a => a.Arguments.Length).ToArray();
			return MakeFunctionArgListsTextBlock(method.Name, funcArgLists);
		}

		private static Control MakeEnvironmentFunctionBlock(IEnvironmentFunctionInfo functionInfo, EvaluationContext context, DocumentationWindow window) {
			StackPanel funcPanel = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock titleBlock = MakeFunctionArgListsTextBlock(functionInfo, context);
			titleBlock.MakeFontSizeRelative(TextBlockClass.H7);
			funcPanel.Children.Add(titleBlock);

			funcPanel.Children.Add(new Separator() {
				Margin = new Thickness(0, 0.0, 0, 4.0)
			});

			if (functionInfo.Description is not null && MakeDescriptionTextBlock(new DocumentationString(functionInfo.Description), window) is TextBlock descriptionBlock) {
				funcPanel.Children.Add(descriptionBlock);
			}

			return funcPanel;
		}

		private static Control MakeEvaluationMethodBlock(IMethod method, bool isStatic, DocumentationWindow window) {
			StackPanel methodPanel = new StackPanel() { Orientation = Orientation.Vertical };

			TextBlock titleBlock = MakeFunctionArgListsTextBlock(method);
			titleBlock.MakeFontSizeRelative(TextBlockClass.H7);
			methodPanel.Children.Add(titleBlock);

			methodPanel.Children.Add(new Separator() {
				Margin = new Thickness(0, 0.0, 0, 4.0)
			});

			if (method.Description is not null && MakeDescriptionTextBlock(new DocumentationString(method.Description), window) is TextBlock descriptionBlock) {
				methodPanel.Children.Add(descriptionBlock);
			}

			return methodPanel;
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

		private static Control MakeEvaluationTypeFieldBlock(EvaluationType type, TypeField field, bool isStatic, DocumentationWindow window) {
			StackPanel argPanel = new StackPanel() { Orientation = Orientation.Vertical };

			string evalTypeName = SharpValueHandler.GetEnvironmentTypeName(type);
			string fieldTypeName = SharpValueHandler.GetEnvironmentTypeName(field.Type);

			TextBlock titleBlock = GetContentTextBlock(TextBlockMargin);
			if (isStatic) {
				titleBlock.Inlines?.Add(new Run(evalTypeName) { Foreground = SharpEditorPalette.EnvironmentTypeBrush });
				titleBlock.Inlines?.Add(new Run("."));
			}
			titleBlock.Inlines?.Add(new Run(field.Name.ToString()) { Foreground = SharpEditorPalette.EnvironmentNameBrush });
			//titleBlock.Inlines.Add(new Run(":" + SharpValueHandler.NO_BREAK_SPACE.ToString()));
			titleBlock.Inlines?.Add(new Run(": "));
			titleBlock.Inlines?.Add(new Run(fieldTypeName) { Foreground = SharpEditorPalette.EnvironmentTypeBrush });

			argPanel.Children.Add(titleBlock);

			if (field.Description is not null && MakeDescriptionTextBlock(new DocumentationString(field.Description), window) is TextBlock descriptionBlock) {
				argPanel.Children.Add(descriptionBlock);
			}

			return argPanel;
		}

		private static Control MakeEvaluationTypeBlock(EvaluationType type, DocumentationWindow window) {
			StackPanel typePanel = new StackPanel() { Orientation = Orientation.Vertical };

			string typeName = SharpValueHandler.GetEnvironmentTypeName(type);

			TextBlock titleBlock = GetContentTextBlock(TextBlockMargin);
			titleBlock.Inlines?.Add(new Run(typeName) { Foreground = SharpEditorPalette.EnvironmentTypeBrush });

			typePanel.Children.Add(titleBlock);

			if (type.Fields.Any() || type.Methods.Any() || type.StaticFields.Any() || type.StaticMethods.Any()) {
				typePanel.Children.Add(new Separator() {
					Margin = new Thickness(0, 0.0, 0, 4.0)
				});

				/*
				if (type.Description is not null && MakeDescriptionTextBlock(new DocumentationString(variableInfo.Description), window) is TextBlock descriptionBlock) {
					argPanel.Children.Add(descriptionBlock);
				}
				*/

				// I'm not sure about this ordering, it's a purely aesthetic choice.

				foreach (TypeField field in type.StaticFields) {
					Control fieldElem = MakeEvaluationTypeFieldBlock(type, field, true, window);
					fieldElem.AddMargin(IndentedMargin);
					typePanel.Children.Add(fieldElem);
				}

				foreach (TypeField field in type.Fields) {
					Control fieldElem = MakeEvaluationTypeFieldBlock(type, field, false, window);
					fieldElem.AddMargin(IndentedMargin);
					typePanel.Children.Add(fieldElem);
				}

				foreach (IMethod method in type.Methods) {
					Control funcElem = MakeEvaluationMethodBlock(method, false, window);
					funcElem.AddMargin(IndentedMargin);
					typePanel.Children.Add(funcElem);
				}

				foreach (IMethod method in type.StaticMethods) {
					Control funcElem = MakeEvaluationMethodBlock(method, true, window);
					funcElem.AddMargin(IndentedMargin);
					typePanel.Children.Add(funcElem);
				}
			}

			return typePanel;
		}

	}

}
