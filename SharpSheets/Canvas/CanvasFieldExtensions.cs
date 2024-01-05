using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas {

	public static class CanvasFieldExtensions {

		public static ISharpGraphicsState AppendFieldPrefix(this ISharpGraphicsState graphicsState, string prefix) {
			graphicsState.SetFieldPrefix(graphicsState.GetFieldPrefix() + prefix);
			return graphicsState;
		}

		public static string? TextField(this ISharpCanvas canvas, Layouts.Rectangle rect, string name, string? tooltip, TextFieldType fieldType, string? value, TextFormat font, float fontSize, bool multiline, bool rich, Justification justification, int maxLen = -1) {
			return canvas.TextField(rect, name, tooltip, fieldType, value, font, fontSize, canvas.GetTextColor(), multiline, rich, justification, maxLen);
		}

		public static string? CheckField(this ISharpCanvas canvas, Layouts.Rectangle rect, string name, string? tooltip, CheckType checkType) {
			return canvas.CheckField(rect, name, tooltip, checkType, canvas.GetTextColor());
		}


		private static string NormaliseFieldName(string name) {
			name = name.Trim();

			StringBuilder builder = new StringBuilder(name.Length + 1);
			for (int i = 0; i < name.Length; i++) {
				char c = name[i];
				if (char.IsLetter(c)) {
					builder.Append(c);
				}
				else if (char.IsDigit(c)) {
					if (builder.Length == 0) {
						builder.Append('_');
						builder.Append(c);
					}
					else {
						builder.Append(c);
					}
				}
				else if ((char.IsWhiteSpace(c) || c == '\n' || c == '_' || char.IsPunctuation(c)) && (builder.Length == 0 || builder[builder.Length - 1] != '_')) {
					builder.Append('_');
				}
			}

			if (builder.Length == 0) {
				return "FIELD";
			}
			else {
				return builder.ToString().ToUpperInvariant().Trim('_');
			}
		}

		public static string GetAvailableFieldName(this ISharpCanvas canvas, string name) {
			int i = 1;
			string normalisedName = NormaliseFieldName(canvas.GetFieldPrefix() + name);
			string finalName = normalisedName;
			while (canvas.DocumentFieldNames.Contains(finalName)) {
				finalName = $"{normalisedName}_{i}";
				i++;
			}
			return finalName;
		}

	}

}
