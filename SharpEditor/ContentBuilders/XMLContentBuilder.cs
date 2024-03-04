using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Markup.Canvas;
using SharpSheets.Markup.Elements;
using SharpSheets.Markup.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SharpEditor.DataManagers;
using SharpSheets.Widgets;
using SharpSheets.Parsing;
using System.Windows;

namespace SharpEditor.ContentBuilders {

	public static class XMLContentBuilder {

		public static TextBlock GetXMLConstructorBlock(ConstructorDetails constructor, XMLElement element) {
			TextBlock block = BaseContentBuilder.GetContentTextBlock(default);
			//Paragraph block = new Paragraph();

			block.Inlines.Add(new Run("<" + SharpValueHandler.NO_BREAK_CHAR) { Foreground = SharpEditorPalette.MarkupPunctuationBrush });
			block.Inlines.Add(MakeXMLRun(constructor.Name, SharpEditorPalette.MarkupElementBrush, false));

			foreach (ArgumentDetails attributeArg in constructor.Arguments) {
				block.Inlines.Add(" ");

				block.Inlines.AddRange(GetXMLArgumentInlines(attributeArg, element));
			}

			if (element != null && element.EndTag == null) {
				block.Inlines.Add(new Run("/>") { Foreground = SharpEditorPalette.MarkupPunctuationBrush });
			}
			else {
				block.Inlines.Add(new Run(">") { Foreground = SharpEditorPalette.MarkupPunctuationBrush });
			}

			return block;
		}

		public static IEnumerable<Inline> MakeMarkupTypeHeader(ConstructorDetails constructor) {
			yield return new Run("<" + SharpValueHandler.NO_BREAK_CHAR) { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
			yield return XMLContentBuilder.MakeXMLRun(constructor.Name, SharpEditorPalette.MarkupElementBrush, false);
			yield return new Run(SharpValueHandler.NO_BREAK_CHAR + ">") { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
		}

		public static TextBlock GetXMLArgumentBlock(ArgumentDetails attributeArg, XMLElement element) {
			TextBlock block = BaseContentBuilder.GetContentTextBlock(default);

			block.Inlines.AddRange(GetXMLArgumentInlines(attributeArg, element));

			return block;
		}

		private static IEnumerable<Inline> GetXMLArgumentInlines(ArgumentDetails attributeArg, XMLElement element) {
			yield return GetTypeRun(attributeArg);
			yield return new Run(SharpValueHandler.NO_BREAK_CHAR + ":" + SharpValueHandler.NO_BREAK_CHAR + ":" + SharpValueHandler.NO_BREAK_CHAR) { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
			yield return MakeXMLRun(attributeArg.Name, SharpEditorPalette.MarkupAttributeBrush, attributeArg.UseLocal); // TODO Underline should represent non-optional arguments
			yield return new Run(SharpValueHandler.NO_BREAK_CHAR + "=" + SharpValueHandler.NO_BREAK_CHAR) { Foreground = SharpEditorPalette.MarkupPunctuationBrush };

			object attrValue;
			bool isDefault = true;
			if (element != null && element.HasAttribute(attributeArg.Name, !attributeArg.UseLocal)) {
				string attrText = element.GetAttribute1(attributeArg.Name, !attributeArg.UseLocal)!.Value.Value;
				attrValue = attrText;
				isDefault = false;
				yield return new Run("\"" + SharpValueHandler.NO_BREAK_CHAR) { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
				yield return new Run(ProcessValueString(attrText)); // { Foreground = SharpEditorDetails.MarkupBaseBrush }
				yield return new Run(SharpValueHandler.NO_BREAK_CHAR + "\"\u200B") { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
			}
			else if (attributeArg.DefaultValue != null) {
				attrValue = attributeArg.DefaultValue;
				yield return new Run("\"" + SharpValueHandler.NO_BREAK_CHAR) { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
				yield return new Run(ProcessValueString(attributeArg.DefaultValue.ToString())) { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
				yield return new Run(SharpValueHandler.NO_BREAK_CHAR + "\"\u200B") { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
			}
			else {
				attrValue = "none";
				yield return new Run("none") { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
			}

			if(GetAttributeType(attributeArg.Type) is Type attrType && (attrType.GetUnderlyingType() == typeof(SharpSheets.Colors.Color) || attrType.IsAssignableTo(typeof(ICanvasPaint)))) {
				Color? color = BaseContentBuilder.GetColorFromValue(attrValue);
				if (color.HasValue) {
					yield return new Run(SharpValueHandler.NO_BREAK_SPACE.ToString());
					yield return BaseContentBuilder.GetColorInline(color, isDefault, false);
				}
			}
		}

		public static Run MakeXMLRun(string text, Brush foreground, bool underline) {
			text = text.Replace("-", SharpValueHandler.NO_BREAK_CHAR + "-" + SharpValueHandler.NO_BREAK_CHAR);
			Run run = new Run(text);
			if (foreground != null) {
				run.Foreground = foreground;
			}
			if (underline) {
				run.TextDecorations = System.Windows.TextDecorations.Underline;
			}
			return run;
		}

		private static string ProcessValueString(string? value, int maxLength = 50, int noBreakLength = 10) {
			value = value ?? string.Empty; // TODO Is this a good fallback here?

			value = Regex.Replace(value, @"\s+", " ").Trim();
			if(value.Length <= noBreakLength) {
				value = value.Replace(' ', SharpValueHandler.NO_BREAK_SPACE);
			}
			maxLength = maxLength > 3 ? maxLength : 4;
			if (value.Length > maxLength) {
				value = value.Substring(0, maxLength - 3) + "...";
			}
			return value;
		}

		/// <summary>
		/// Resolve the resulting Type of an IExpression type. If <paramref name="type"/> is not an IExpression, return null.
		/// </summary>
		/// <param name="type">Type to resolve.</param>
		/// <returns></returns>
		public static Type? ResolveExpressionType(Type type) {
			Type? exprType = type.GetInterfacesOrSelf().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IExpression<>))?.GetGenericArguments()?.SingleOrDefault();
			return exprType;
		}

		public static Type? ResolveExpressionType(ArgumentType type) {
			return ResolveExpressionType(type.DisplayType);
		}

		public static Type GetAttributeType(ArgumentType type) {
			return ResolveExpressionType(type.DisplayType) ?? type.DisplayType;
		}

		public static string GetTypeName(Type type, out bool concrete) {
			string typeStr;
			concrete = false;

			if (type == typeof(DrawPointExpression[])) {
				typeStr = "DrawPoints";
			}
			/*
			else if(type == typeof(FloatExpression[])) {
				typeStr = "{" + SharpEditorDetails.GetTypeName(typeof(float)) + "[]}";
			}
			*/
			else if (type == typeof(SharpSheets.Markup.Elements.Path.DrawOperation[])) {
				typeStr = "PathData";
			}
			else if (type == typeof(ForEachExpression)) {
				typeStr = "ForEach";
			}
			else if (type == typeof(IShapeElement)) {
				typeStr = "PathSource";
			}
			else if (type.IsArray && type.GetElementType() is Type elementType) {
				string elementName = GetTypeName(elementType, out concrete);
				typeStr = elementName + "[]";
			}
			else if(type == typeof(XLengthExpression)) {
				typeStr = "X-Length";
			}
			else if (type == typeof(YLengthExpression)) {
				typeStr = "Y-Length";
			}
			else if (type == typeof(BoundingBoxLengthExpression)) {
				typeStr = "BBox-Length";
			}
			else if (ResolveExpressionType(type) is Type exprType) {
				typeStr = GetTypeName(exprType, out _);
			}
			else if (type == typeof(EvaluationNode)) {
				typeStr = "Expression";
			}
			else if(type == typeof(ClipPath)) {
				typeStr = "ClipPath";
			}
			else if(type == typeof(ICanvasPaint)) {
				typeStr = "Paint";
			}
			else if (type == typeof(MarkupPatternType)) {
				typeStr = "PatternType";
				concrete = true;
			}
			else if(type == typeof(EvaluationName) || type == typeof(EvaluationName?)) {
				typeStr = SharpValueHandler.GetTypeName(typeof(string));
				concrete = true;
			}
			else {
				typeStr = SharpValueHandler.GetTypeName(type);
				concrete = true;
			}

			return typeStr;
		}

		public static string GetTypeName(ArgumentType type, out bool concrete) {
			return GetTypeName(type.DisplayType, out concrete);
		}

		private static Run GetTypeRun(ArgumentDetails arg) {
			ArgumentType type = arg.Type;

			string typeStr = GetTypeName(type, out bool concrete);

			Run typeRun = new Run(typeStr) { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
			if (concrete) {
				//typeRun.TextDecorations = System.Windows.TextDecorations.Underline;
				typeRun.FontStyle = System.Windows.FontStyles.Italic;
				typeRun.FontWeight = System.Windows.FontWeights.Bold;
			}
			if(!arg.IsOptional) {
				typeRun.TextDecorations = TextDecorations.Underline;
			}

			return typeRun;
		}

	}

}
