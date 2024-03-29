﻿using SharpSheets.Documentation;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SharpEditor.DataManagers;
using SharpEditor.Documentation;

namespace SharpEditor.ContentBuilders {

	public static class EnumContentBuilder {

		public static IEnumerable<TextBlock> MakeEnumBlocks(EnumDoc enumDoc, EnumValDoc? enumValDoc, Thickness textMargin, Thickness indentMargin) {
			//if (enumDoc != null || enumValDoc != null)

			TextBlock enumBlock = BaseContentBuilder.GetContentTextBlock(textMargin);
			enumBlock.Inlines.Add(new Run(enumValDoc?.type ?? enumDoc.type) { Foreground = SharpEditorPalette.TypeBrush });
			if (enumValDoc != null) {
				enumBlock.Inlines.Add(new Run("." + SharpValueHandler.GetEnumString(enumValDoc.name)) { });
			}
			yield return enumBlock;

			// TODO Should MakeDescriptionTextBlock be in a more generic place? We're building Tooltip content here, not documentation pages
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

	}

}
