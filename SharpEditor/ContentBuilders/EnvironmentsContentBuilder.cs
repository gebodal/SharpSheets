using SharpSheets.Evaluations;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SharpEditor.DataManagers;
using SharpSheets.Utilities;

namespace SharpEditor.ContentBuilders {

	public static class EnvironmentsContentBuilder {

		public static TextBlock GetVariableBoxEntries(IVariableBox variables, Thickness margin) {
			TextBlock variablesBlock = BaseContentBuilder.GetContentTextBlock(margin); // indent ? MakeIndentedBlock() : GetToolTipTextBlock();

			bool first = true;
			foreach (KeyValuePair<EvaluationName, EvaluationType> returnType in variables.GetReturnTypes()) {
				if (first) { first = false; }
				else { variablesBlock.Inlines.Add(new Run(", ")); }

				variablesBlock.Inlines.AddRange(VariableInlines(returnType.Key.ToString(), returnType.Value));
			}

			return variablesBlock;
		}

		private static IEnumerable<Inline> VariableInlines(string name, EvaluationType type) {
			if (type.DisplayType != null && typeof(Dictionary<EvaluationName, object>).IsAssignableFrom(type.DisplayType)) {
				// TODO What on earth is this doing?
				bool first = true;
				foreach (TypeField field in type.FieldNames.Select(f => type.GetField(f)).WhereNotNull()) {
					if (first) { first = false; }
					else { yield return new Run(", "); }

					foreach(Inline inline in VariableInlines(name + "." + field.Name, field.Type)) {
						yield return inline;
					}
				}
			}
			else {
				yield return new Run(SharpValueHandler.GetTypeName(type)) { Foreground = SharpEditorPalette.TypeBrush };
				yield return new Run(SharpValueHandler.NO_BREAK_SPACE.ToString());
				yield return new Run(name);
			}
		}

	}

}
