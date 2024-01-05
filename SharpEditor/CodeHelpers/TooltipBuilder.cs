using SharpEditor.ContentBuilders;
using SharpSheets.Cards.Definitions;
using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SharpEditor.DataManagers;

namespace SharpEditor.CodeHelpers {

	public static class TooltipBuilder {

		public static Thickness TextBlockMargin { get; } = new Thickness(7, 4, 7, 4);
		public static Thickness IndentedMargin { get; } = new Thickness(7 + 15, 4, 7, 4);

		public static TextBlock GetToolTipTextBlock(string? text = null) {
			TextBlock block = BaseContentBuilder.GetContentTextBlock(text, TextBlockMargin);
			if (text != null) block.Text = text;
			return block;
		}

		public static TextBlock MakeIndentedBlock(string? text = null) {
			return BaseContentBuilder.GetContentTextBlock(text, IndentedMargin);
		}

		public static Separator MakeSeparator(double horizontalPadding = 10.0, double verticalPadding = 6.0) {
			return new Separator() {
				Foreground = Brushes.LightGray,
				Margin = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding)
			};
		}

		public static TextBlock? MakeDescriptionTextBlock(DocumentationString? descriptionString) {
			return BaseContentBuilder.MakeDescriptionTextBlock(descriptionString, IndentedMargin);
		}

		public static IEnumerable<FrameworkElement> MakeConstructorEntry(ConstructorDetails constructor, IContext? constructorContext, bool includeArguments, ITypeDetailsCollection? impliedConstructors) {
			yield return BaseContentBuilder.GetContentTextBlock(ConstructorContentBuilder.MakeConstructorHeaderBlock(constructor), TextBlockMargin);
			
			if (BaseContentBuilder.MakeDescriptionTextBlock(constructor.Description, IndentedMargin) is TextBlock descriptionBlock) {
				//yield return BaseContentBuilder.GetContentTextBlock(constructor.Description, IndentedMargin);
				yield return descriptionBlock;
			}

			if (!includeArguments) yield break;

			ArgumentDetails[] arguments = constructor.Arguments.GroupBy(a => $"{SharpValueHandler.GetTypeName(a.Type)} {a.Name}").Select(g => g.First()).ToArray(); // What breaks if we don't do this?
			TextBlock argumentBlock = BaseContentBuilder.GetContentTextBlock(GetArgumentListInlines(arguments, constructorContext), TextBlockMargin); // GetToolTipTextBlock();
			if (argumentBlock.Inlines.Count > 0) { yield return argumentBlock; }

			if (constructorContext != null && impliedConstructors != null) {
				ArgumentDetails[] impliedArguments = arguments.Where(a => a.Implied != null).ToArray();

				if (impliedArguments.Length > 0) {
					for (int i = 0; i < impliedArguments.Length; i++) {
						ArgumentDetails impliedArg = impliedArguments[i];

						object? currentValue = constructorContext.GetProperty($"{impliedArg.Name}.{impliedArg.Implied}", impliedArg.UseLocal, constructorContext, null, out _) ?? impliedArg.DefaultValue;

						ConstructorDetails? impliedConstructor = null;
						if (currentValue is string stringValue) {
							impliedConstructor = impliedConstructors.Get(stringValue);
						}
						else if (currentValue is Type typeValue) {
							impliedConstructor = impliedConstructors.Get(typeValue);
						}

						if (impliedConstructor != null && impliedConstructor.Arguments.Length > 0) {
							yield return MakeSeparator();

							List<Inline> headerInlines = new List<Inline>();
							headerInlines.AddRange(ConstructorContentBuilder.MakeConstructorHeaderBlock(impliedArg.Name, impliedArg.Name, impliedArg.Type.DisplayType, impliedArg.Type.DisplayType));
							headerInlines.AddRange(ConstructorContentBuilder.GetArgumentDefaultInlines(impliedArg, constructorContext));

							yield return BaseContentBuilder.GetContentTextBlock(headerInlines, TextBlockMargin);

							ArgumentDetails[] impliedConstructorArguments = impliedConstructor.Arguments.GroupBy(a => $"{SharpValueHandler.GetTypeName(a.Type)} {a.Name}").Select(g => g.First()).ToArray(); // What breaks if we don't do this?
							IContext impliedContext = new NamedContext(constructorContext, impliedArg.Name);
							TextBlock impliedArgumentBlock = BaseContentBuilder.GetContentTextBlock(GetArgumentListInlines(impliedConstructorArguments, impliedContext), TextBlockMargin); // GetToolTipTextBlock();
							if (impliedArgumentBlock.Inlines.Count > 0) { yield return impliedArgumentBlock; }
						}
					}
				}
			}
		}

		public static IEnumerable<Inline> GetArgumentListInlines(ArgumentDetails[] arguments, IContext? context) {
			for (int i = 0; i < arguments.Length; i++) {
				ArgumentDetails arg = arguments[i];

				if (i > 0) { yield return new Run(", "); }

				yield return new Run(SharpValueHandler.GetTypeName(arg.Type)) { Foreground = SharpEditorPalette.TypeBrush };
				yield return new Run(SharpValueHandler.NO_BREAK_SPACE + arg.Name);
				if (arg.Implied != null) {
					yield return new Run("." + arg.Implied);
				}

				foreach(Inline defaultInline in ConstructorContentBuilder.GetArgumentDefaultInlines(arg, context)) {
					yield return defaultInline;
				}
			}
		}

		public static TextBlock[] GetArgumentDescription(ConstructorArgumentDetails argument, IContext? context) {
			return MakeSingleArgumentBlocks(argument, context).ToArray();
		}

		public static TextBlock MakeArgumentHeaderBlock(ConstructorArgumentDetails argument, IContext? context, bool withMargin) {
			TextBlock argumentBlock = BaseContentBuilder.GetContentTextBlock(withMargin ? TextBlockMargin : default); // GetToolTipTextBlock(withMargin: withMargin);
			argumentBlock.Inlines.Add(new Run(SharpValueHandler.GetTypeName(argument.ArgumentType)) { Foreground = SharpEditorPalette.TypeBrush });
			argumentBlock.Inlines.Add(new Run(SharpValueHandler.NO_BREAK_SPACE + argument.ConstructorName) { Foreground = SharpEditorPalette.GetTypeBrush(argument.DeclaringType) });

			ArgumentDetails arg = argument.Argument;
			while (arg != null && arg is PrefixedArgumentDetails prefixed) { arg = prefixed.Basis; }

			argumentBlock.Inlines.Add(new Run("." + arg!.Name) { });

			if (argument.Implied != null) {
				argumentBlock.Inlines.Add(new Run("." + argument.Implied));
			}

			argumentBlock.Inlines.AddRange(ConstructorContentBuilder.GetArgumentDefaultInlines(argument.Argument, context));

			return argumentBlock;
		}

		public static IEnumerable<TextBlock> MakeSingleArgumentBlocks(ConstructorArgumentDetails argument, IContext? context) {
			yield return MakeArgumentHeaderBlock(argument, context, true);

			if (BaseContentBuilder.MakeDescriptionTextBlock(argument.ArgumentDescription, IndentedMargin) is TextBlock descriptionBlock) {
				//yield return BaseContentBuilder.GetContentTextBlock(argument.ArgumentDescription, IndentedMargin);
				yield return descriptionBlock;
			}
		}

		public static IEnumerable<TextBlock> MakeEnumBlocks(EnumDoc enumDoc, EnumValDoc? enumValDoc) {
			return EnumContentBuilder.MakeEnumBlocks(enumDoc, enumValDoc, TextBlockMargin, IndentedMargin);
		}
		public static TextBlock MakeEnumOptionsBlock(EnumDoc enumDoc, bool indented) {
			return EnumContentBuilder.MakeEnumOptionsBlock(enumDoc, indented ? IndentedMargin : TextBlockMargin);
		}

		public static FrameworkElement MakeCardDefinitionBlocks(Definition definition, DefinitionEnvironment? environment) {
			return DefinitionContentBuilder.MakeDefinitionElement(definition, environment, TextBlockMargin, IndentedMargin);
		}

		public static IEnumerable<TextBlock> MakeDefinitionEntries(IEnumerable<Definition> definitions, IEnvironment? evaluationEnvironment) {
			return DefinitionContentBuilder.MakeDefinitionEntries(definitions, evaluationEnvironment, TextBlockMargin);
		}
		public static TextBlock MakeDefinitionEntries(Definition definition, IEnvironment? evaluationEnvironment) {
			return DefinitionContentBuilder.MakeDefinitionEntries(definition, evaluationEnvironment, TextBlockMargin);
		}

		public static TextBlock GetVariableBoxEntries(IVariableBox variables, bool indented = true) {
			return EnvironmentsContentBuilder.GetVariableBoxEntries(variables, indented ? IndentedMargin : TextBlockMargin);
		}
	}

}
