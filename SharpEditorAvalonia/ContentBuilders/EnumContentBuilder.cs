using SharpSheets.Documentation;
using System.Collections.Generic;
using System.Linq;
using SharpEditorAvalonia.DataManagers;
using System.Diagnostics.CodeAnalysis;
using System;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia;

namespace SharpEditorAvalonia.ContentBuilders {

	public static class EnumContentBuilder {

		public static IEnumerable<TextBlock> MakeEnumBlocks(EnumDoc enumDoc, EnumValDoc? enumValDoc, Thickness textMargin, Thickness indentMargin) {
			//if (enumDoc != null || enumValDoc != null)

			TextBlock enumBlock = BaseContentBuilder.GetContentTextBlock(textMargin);
			enumBlock.Inlines?.Add(new Run(enumValDoc?.type ?? enumDoc.type) { Foreground = SharpEditorPalette.TypeBrush });
			if (enumValDoc != null) {
				enumBlock.Inlines?.Add(new Run("." + SharpValueHandler.GetEnumString(enumValDoc.name)) { });
			}
			yield return enumBlock;

			if (BaseContentBuilder.MakeDescriptionTextBlock(enumValDoc?.description, indentMargin) is TextBlock valDescriptionBlock) {
				//yield return BaseContentBuilder.GetContentTextBlock(enumValDoc.description, indentMargin);
				yield return valDescriptionBlock;
			}

			if (enumDoc != null) {
				yield return MakeEnumOptionsBlock(enumDoc, indentMargin);
			}
		}

		public static TextBlock MakeEnumOptionsBlock(EnumDoc enumDoc, Thickness margin) {
			TextBlock optionsBlock = BaseContentBuilder.GetContentTextBlock(margin);
			optionsBlock.Text = $"Possible values for {enumDoc.type}: " + string.Join(", ", enumDoc.values.Select(v => SharpValueHandler.GetEnumString(v.name)));
			return optionsBlock;
		}

		public static bool IsEnum(ArgumentType argumentType, [MaybeNullWhen(false)] out EnumDoc enumDoc) {
			Type? enumType = null;
			if (argumentType.DisplayType.IsEnum) {
				enumType = argumentType.DisplayType;
			}
			else if (Nullable.GetUnderlyingType(argumentType.DisplayType) is Type nulledType && nulledType.IsEnum) {
				enumType = nulledType;
			}

			if (enumType is Type foundType && SharpDocumentation.GetEnumDoc(foundType) is EnumDoc foundDoc) {
				enumDoc = foundDoc;
				return true;
			}
			else {
				enumDoc = null;
				return false;
			}
		}

	}

}
