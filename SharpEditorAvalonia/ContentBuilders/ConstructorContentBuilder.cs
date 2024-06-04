using System;
using SharpSheets.Documentation;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System.Collections.Generic;
using Avalonia;
using SharpEditorAvalonia.DataManagers;
using Avalonia.Controls.Documents;

namespace SharpEditorAvalonia.ContentBuilders {

	public static class ConstructorContentBuilder {

		public static IEnumerable<Inline> MakeConstructorHeaderBlock(string name, string fullName, Type displayType, Type declaringType) {
			string? prefix = fullName;
			string displayName = name;
			if (prefix == displayName) {
				prefix = null;
			}
			else if (prefix.EndsWith(displayName)) {
				prefix = prefix[..^displayName.Length];
			}
			else {
				prefix = null;
				displayName = fullName;
			}

			yield return new Run(SharpValueHandler.GetTypeName(displayType)) { Foreground = SharpEditorPalette.TypeBrush };
			yield return new Run(SharpValueHandler.NO_BREAK_SPACE.ToString());

			if (!string.IsNullOrEmpty(prefix)) {
				yield return new Run(prefix) { Foreground = SharpEditorPalette.DefaultValueBrush };
			}

			yield return new Run(displayName) {
				Foreground = typeof(SharpWidget).IsAssignableFrom(declaringType) ? SharpEditorPalette.RectBrush : SharpEditorPalette.StyleBrush
			};
		}

		public static IEnumerable<Inline> MakeConstructorHeaderBlock(ConstructorDetails constructor) {
			return MakeConstructorHeaderBlock(constructor.Name, constructor.FullName, constructor.DisplayType, constructor.DeclaringType);
		}

		public static IEnumerable<Inline> GetArgumentDefaultInlines(ArgumentDetails arg, IContext? context) {
			if (arg.Type.DisplayType.TryGetGenericTypeDefinition() == typeof(List<>)) {
				yield break; // Don't print default if we're on the entries argument
			}

			string defaultValue = SharpValueHandler.GetValueString(arg.Type.DisplayType, arg.DefaultValue);
			object? currentValue = null;
			DocumentSpan? currentValueLocation = null;
			if (context != null) {
				if (arg.Type.DisplayType == typeof(bool)) {
					currentValue = context.GetFlag(arg.Name, arg.UseLocal, context, out currentValueLocation);
				}
				else if (arg.Implied != null) {
					currentValue = context.GetProperty($"{arg.Name}.{arg.Implied}", arg.UseLocal, context, null, out currentValueLocation);
				}
				else {
					currentValue = context.GetProperty(arg.Name, arg.UseLocal, context, null, out currentValueLocation);
				}
			}

			string currentValueString = SharpValueHandler.GetValueString(arg.Type.DisplayType, currentValue);

			bool printDefaultValue =
				(!currentValueLocation.HasValue || !SharpValueHandler.ValueStringsMatch(arg.Type.DisplayType, defaultValue, currentValueString))
				&& (arg.IsOptional || defaultValue != null)
				&& arg.DefaultValue != null;

			if (currentValueLocation.HasValue || printDefaultValue) {
				Run equalsRun = new Run(SharpValueHandler.NO_BREAK_SPACED_EQUALS);
				if (!currentValueLocation.HasValue) {
					equalsRun.Foreground = SharpEditorPalette.DefaultValueBrush;
				}
				yield return equalsRun;
			}

			if (currentValueLocation.HasValue) {
				//yield return new Run(SharpEditorDetails.NO_BREAK_SPACED_EQUALS + currentValueString);
				yield return BaseContentBuilder.GetValueInline(arg.Type.DisplayType, currentValue, false);
			}

			if (printDefaultValue) {
				Inline defaultInline = BaseContentBuilder.GetValueInline(arg.Type.DisplayType, arg.DefaultValue, true);

				if (currentValueLocation.HasValue) {
					Span defaultSpan = new Span();
					defaultSpan.Inlines.Add(new Run(SharpValueHandler.NO_BREAK_SPACE.ToString() + SharpValueHandler.NO_BREAK_CHAR + "[" + SharpValueHandler.NO_BREAK_CHAR) { Foreground = SharpEditorPalette.DefaultValueBrush });
					defaultSpan.Inlines.Add(defaultInline);
					defaultSpan.Inlines.Add(new Run(SharpValueHandler.NO_BREAK_CHAR + "]") { Foreground = SharpEditorPalette.DefaultValueBrush });
					yield return defaultSpan;
				}
				else {
					yield return defaultInline;
				}
			}
		}

	}

}
