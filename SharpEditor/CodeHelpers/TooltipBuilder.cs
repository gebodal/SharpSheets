using SharpEditor.ContentBuilders;
using SharpSheets.Cards.Definitions;
using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System.Collections.Generic;
using System;
using System.Linq;
using SharpEditor.DataManagers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using Avalonia.Layout;

namespace SharpEditor.CodeHelpers {

	public static class TooltipBuilder {

		public static Thickness TextBlockMargin { get; } = new Thickness(7, 4, 7, 4);
		public static Thickness IndentedMargin { get; } = new Thickness(7 + 15, 4, 7, 4);

		public static TextBlock GetToolTipTextBlock(string? text = null) {
			TextBlock block = BaseContentBuilder.GetContentTextBlock(text, TextBlockMargin);
			return block;
		}

		public static TextBlock MakeIndentedBlock(string? text = null) {
			return BaseContentBuilder.GetContentTextBlock(text, IndentedMargin);
		}

		public static Separator MakeSeparator(double horizontalPadding = 10.0, double verticalPadding = 6.0) {
			return new Separator() {
				Margin = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding)
			};
		}

		public static TextBlock? MakeDescriptionTextBlock(DocumentationString? descriptionString) {
			return BaseContentBuilder.MakeDescriptionTextBlock(descriptionString, IndentedMargin);
		}

		public static IEnumerable<Control> MakeBuilderEntry(BuilderDetails builder, IContext? builderContext, bool includeArguments, ITypeDetailsCollection? impliedBuilders) {
			yield return BaseContentBuilder.GetContentTextBlock(BuilderContentBuilder.MakeBuilderHeaderBlock(builder), TextBlockMargin);
			
			if (BaseContentBuilder.MakeDescriptionTextBlock(builder.Description, IndentedMargin) is TextBlock descriptionBlock) {
				//yield return BaseContentBuilder.GetContentTextBlock(constructor.Description, IndentedMargin);
				yield return descriptionBlock;
			}

			if (!includeArguments) yield break;

			ArgumentDetails[] arguments = builder.Arguments.GroupBy(a => $"{SharpValueHandler.GetTypeName(a.Type)} {a.Name}").Select(g => g.First()).ToArray(); // What breaks if we don't do this?
			TextBlock argumentBlock = BaseContentBuilder.GetContentTextBlock(GetArgumentListInlines(arguments, builderContext), TextBlockMargin); // GetToolTipTextBlock();
			if (argumentBlock.Inlines?.Count > 0) { yield return argumentBlock; }

			if (builderContext != null && impliedBuilders != null) {
				ArgumentDetails[] impliedArguments = arguments.Where(a => a.Implied != null).ToArray();

				if (impliedArguments.Length > 0) {
					for (int i = 0; i < impliedArguments.Length; i++) {
						ArgumentDetails impliedArg = impliedArguments[i];

						object? currentValue = builderContext.GetProperty($"{impliedArg.Name}.{impliedArg.Implied}", impliedArg.UseLocal, builderContext, null, out _) ?? impliedArg.DefaultValue;

						BuilderDetails? impliedBuilder = null;
						if (currentValue is string stringValue) {
							impliedBuilder = impliedBuilders.Get(stringValue);
						}
						else if (currentValue is Type typeValue) {
							impliedBuilder = impliedBuilders.Get(typeValue);
						}

						if (impliedBuilder != null && impliedBuilder.Arguments.Length > 0) {
							yield return MakeSeparator();

							List<Inline> headerInlines = new List<Inline>();
							headerInlines.AddRange(BuilderContentBuilder.MakeBuilderHeaderBlock(impliedArg.Name, impliedArg.Name, impliedArg.Type.DisplayType));
							headerInlines.AddRange(BuilderContentBuilder.GetArgumentDefaultInlines(impliedArg, builderContext));

							yield return BaseContentBuilder.GetContentTextBlock(headerInlines, TextBlockMargin);

							ArgumentDetails[] impliedBuilderArguments = impliedBuilder.Arguments.GroupBy(a => $"{SharpValueHandler.GetTypeName(a.Type)} {a.Name}").Select(g => g.First()).ToArray(); // What breaks if we don't do this?
							IContext impliedContext = new NamedContext(builderContext, impliedArg.Name);
							TextBlock impliedArgumentBlock = BaseContentBuilder.GetContentTextBlock(GetArgumentListInlines(impliedBuilderArguments, impliedContext), TextBlockMargin); // GetToolTipTextBlock();
							if (impliedArgumentBlock.Inlines?.Count > 0) { yield return impliedArgumentBlock; }
						}
					}
				}
			}
		}

		public static Inline GetArgumentNameInline(ArgumentDetails argument) {
			string name = argument.Name;
			if (SharpValueHandler.IsNamedChild(argument.Type)) {
				name = "&" + name;
			}
			if(argument.Implied is not null) {
				name += "." + argument.Implied;
			}
			Run run = new Run(name);
			if (argument.UseLocal) { run.TextDecorations = TextDecorations.Underline; }
			return run;
		}

		public static IEnumerable<Inline> GetArgumentListInlines(ArgumentDetails[] arguments, IContext? context) {
			for (int i = 0; i < arguments.Length; i++) {
				ArgumentDetails arg = arguments[i];

				if (i > 0) { yield return new Run(", "); }

				yield return new Run(SharpValueHandler.GetTypeName(arg.Type)) { Foreground = SharpEditorPalette.TypeBrush };
				yield return new Run(SharpValueHandler.NO_BREAK_SPACE.ToString());
				yield return GetArgumentNameInline(arg);

				foreach(Inline defaultInline in BuilderContentBuilder.GetArgumentDefaultInlines(arg, context)) {
					yield return defaultInline;
				}
			}
		}

		public static TextBlock[] GetArgumentDescription(BuilderArgumentDetails argument, IContext? context) {
			return MakeSingleArgumentBlocks(argument, context).ToArray();
		}

		public static TextBlock MakeArgumentHeaderBlock(BuilderArgumentDetails argument, IContext? context, bool withMargin) {
			TextBlock argumentBlock = BaseContentBuilder.GetContentTextBlock(withMargin ? TextBlockMargin : default); // GetToolTipTextBlock(withMargin: withMargin);
			argumentBlock.Inlines?.Add(new Run(SharpValueHandler.GetTypeName(argument.ArgumentType)) { Foreground = SharpEditorPalette.TypeBrush });
			argumentBlock.Inlines?.Add(new Run(SharpValueHandler.NO_BREAK_SPACE + argument.BuilderName) { Foreground = SharpEditorPalette.GetTypeBrush(argument.DeclaringType) });

			ArgumentDetails arg = argument.Argument;
			//while (arg is PrefixedArgumentDetails prefixed) { arg = prefixed.Basis; }

			argumentBlock.Inlines?.Add(new Run("."));
			argumentBlock.Inlines?.Add(GetArgumentNameInline(arg));

			argumentBlock.Inlines?.AddRange(BuilderContentBuilder.GetArgumentDefaultInlines(argument.Argument, context));

			return argumentBlock;
		}

		public static IEnumerable<TextBlock> MakeSingleArgumentBlocks(BuilderArgumentDetails argument, IContext? context) {
			yield return MakeArgumentHeaderBlock(argument, context, true);

			if (BaseContentBuilder.MakeDescriptionTextBlock(argument.ArgumentDescription, IndentedMargin) is TextBlock descriptionBlock) {
				//yield return BaseContentBuilder.GetContentTextBlock(argument.ArgumentDescription, IndentedMargin);
				yield return descriptionBlock;
			}

			if (argument.UseLocal) {
				yield return BaseContentBuilder.GetContentTextBlock("(Local)", IndentedMargin);
			}
		}

		public static IEnumerable<Control> MakeMultipleArgumentBlocks(IEnumerable<BuilderArgumentDetails> allArgs, IContext? context) {
			foreach (IGrouping<DocumentationString?, BuilderArgumentDetails> descriptionGrouping in allArgs.GroupBy(t => t.ArgumentDescription)) {
				StackPanel argumentList = new StackPanel() {
					Orientation = Orientation.Vertical,
					Margin = TooltipBuilder.TextBlockMargin
				};
				foreach (BuilderArgumentDetails builderArg in descriptionGrouping.GroupBy(t => new { Type = SharpValueHandler.GetTypeName(t.ArgumentType), t.BuilderName, t.ArgumentName, t.Implied }).Select(g => g.First())) {
					argumentList.Children.Add(TooltipBuilder.MakeArgumentHeaderBlock(builderArg, context, false));
				}
				yield return argumentList;

				if (TooltipBuilder.MakeDescriptionTextBlock(descriptionGrouping.Key) is TextBlock descriptionBlock) {
					//yield return TooltipBuilder.MakeIndentedBlock(descriptionGrouping.Key);
					yield return descriptionBlock;
				}

				if (descriptionGrouping.Select(a => a.ArgumentType.DisplayType).First() is DisplayType argDisplayType && argDisplayType.IsEnum && SharpDocumentation.GetEnumDoc(argDisplayType) is EnumDoc enumDoc) {
					yield return TooltipBuilder.MakeEnumOptionsBlock(enumDoc, true);
				}

				if (descriptionGrouping.Select(a => a.UseLocal).First()) {
					yield return TooltipBuilder.MakeIndentedBlock("(Local)");
				}
			}
		}
		

		public static IEnumerable<TextBlock> MakeEnumBlocks(EnumDoc enumDoc, EnumValDoc? enumValDoc) {
			return EnumContentBuilder.MakeEnumBlocks(enumDoc, enumValDoc, TextBlockMargin, IndentedMargin);
		}
		public static TextBlock MakeEnumOptionsBlock(EnumDoc enumDoc, bool indented) {
			return EnumContentBuilder.MakeEnumOptionsBlock(enumDoc, indented ? IndentedMargin : TextBlockMargin);
		}

		public static Control MakeCardDefinitionBlocks(Definition definition, IEnvironment? environment) {
			return DefinitionContentBuilder.MakeDefinitionElement(definition, environment, TextBlockMargin, IndentedMargin);
		}

		public static IEnumerable<TextBlock> MakeDefinitionEntries(IEnumerable<Definition> definitions, IEnvironment? evaluationEnvironment) {
			return DefinitionContentBuilder.MakeDefinitionEntries(definitions, evaluationEnvironment, TextBlockMargin);
		}
		public static TextBlock MakeDefinitionEntries(Definition definition, IEnvironment? evaluationEnvironment) {
			return DefinitionContentBuilder.MakeDefinitionEntries(definition, evaluationEnvironment, TextBlockMargin);
		}

		public static TextBlock MakeDefinitionsBlock(IEnumerable<Definition> definitions, IEnvironment? evaluationEnvironment) {
			return DefinitionContentBuilder.MakeDefinitionsBlock(definitions, evaluationEnvironment, TextBlockMargin);
		}

		public static TextBlock GetVariableBoxEntries(IVariableBox variables, bool indented = true) {
			return EnvironmentsContentBuilder.GetVariableBoxEntries(variables, indented ? IndentedMargin : TextBlockMargin);
		}
	}

}
