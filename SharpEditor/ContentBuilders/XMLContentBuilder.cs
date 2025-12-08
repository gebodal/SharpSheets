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
using SharpEditor.DataManagers;
using SharpSheets.Widgets;
using SharpSheets.Parsing;
using System.Windows;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using SharpEditor.Utilities;

namespace SharpEditor.ContentBuilders {

	public static class XMLContentBuilder {

		public static TextBlock GetXMLBuilderBlock(BuilderDetails builder, XMLElement element) {
			TextBlock block = BaseContentBuilder.GetContentTextBlock(default);
			//Paragraph block = new Paragraph();

			block.Inlines?.Add(new Run("<" + SharpValueHandler.NO_BREAK_CHAR) { Foreground = SharpEditorPalette.MarkupPunctuationBrush });
			block.Inlines?.Add(MakeXMLRun(builder.Name, SharpEditorPalette.MarkupElementBrush, false));

			foreach (ArgumentDetails attributeArg in builder.Arguments) {
				block.Inlines?.Add(" ");

				block.Inlines?.AddRange(GetXMLArgumentInlines(attributeArg, element));
			}

			if (element != null && element.EndTag == null) {
				block.Inlines?.Add(new Run("/>") { Foreground = SharpEditorPalette.MarkupPunctuationBrush });
			}
			else {
				block.Inlines?.Add(new Run(">") { Foreground = SharpEditorPalette.MarkupPunctuationBrush });
			}

			return block;
		}

		public static IEnumerable<Inline> MakeMarkupTypeHeader(BuilderDetails builder) {
			yield return new Run("<" + SharpValueHandler.NO_BREAK_CHAR) { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
			yield return XMLContentBuilder.MakeXMLRun(builder.Name, SharpEditorPalette.MarkupElementBrush, false);
			yield return new Run(SharpValueHandler.NO_BREAK_CHAR + ">") { Foreground = SharpEditorPalette.MarkupPunctuationBrush };
		}

		public static TextBlock GetXMLArgumentBlock(ArgumentDetails attributeArg, XMLElement element) {
			TextBlock block = BaseContentBuilder.GetContentTextBlock(default);

			block.Inlines?.AddRange(GetXMLArgumentInlines(attributeArg, element));

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

			if(GetAttributeType(attributeArg.Type) is DisplayType attrType && (attrType.IsSimple<SharpSheets.Colors.Color>() || attrType.IsAssignableTo(typeof(ICanvasPaint)))) {
				Color? color = BaseContentBuilder.GetColorFromValue(attrValue);
				if (color.HasValue) {
					yield return new Run(SharpValueHandler.NO_BREAK_SPACE.ToString());
					yield return new ColorInline(color, isDefault, false);
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
				run.TextDecorations = TextDecorations.Underline; // System.Windows.TextDecorations.Underline;
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
		public static DisplayType? ResolveExpressionType(DisplayType type) {
			if (type.GetSingle() is Type systemType && ExpressionUsageMap.TryGetExpressionType(systemType, out Type? expressionResultType)) {
				return DisplayType.Create(expressionResultType);
			}
			else {
				return null;
			}
		}

		public static DisplayType? ResolveExpressionType(ArgumentType type) {
			return ResolveExpressionType(type.DisplayType);
		}

		public static DisplayType GetAttributeType(ArgumentType type) {
			return ResolveExpressionType(type.DisplayType) ?? type.DisplayType;
		}

		public static string GetTypeName(DisplayType type, out bool concrete) {
			string typeStr;
			concrete = false;

			if (type.IsSequence<DrawPointExpression>(out _)) {
				typeStr = "DrawPoints";
			}
			/*
			else if(type == typeof(FloatExpression[])) {
				typeStr = "{" + SharpEditorDetails.GetTypeName(typeof(float)) + "[]}";
			}
			*/
			else if (type.IsSequence<SharpSheets.Markup.Elements.Path.DrawOperation>(out _)) {
				typeStr = "PathData";
			}
			else if (type.IsSimple<ForEachExpression>()) {
				typeStr = "ForEach";
			}
			else if (type.IsSimple<IShapeElement>()) {
				typeStr = "PathSource";
			}
			else if (type.IsSequence(out DisplayType? elementType, out _)) {
				string elementName = GetTypeName(elementType, out concrete);
				typeStr = elementName + "[]";
			}
			else if(type.IsSimple<XLengthExpression>()) {
				typeStr = "X-Length";
			}
			else if (type.IsSimple<YLengthExpression>()) {
				typeStr = "Y-Length";
			}
			else if (type.IsSimple<BoundingBoxLengthExpression>()) {
				typeStr = "BBox-Length";
			}
			else if (ResolveExpressionType(type) is DisplayType exprType) {
				typeStr = GetTypeName(exprType, out _);
			}
			else if (type.IsSimple<EvaluationNode>()) {
				typeStr = "Expression";
			}
			else if(type.IsSimple<ClipPath>()) {
				typeStr = "ClipPath";
			}
			else if(type.IsSimple<ICanvasPaint>()) {
				typeStr = "Paint";
			}
			else if (type.IsSimple<MarkupPatternType>()) {
				typeStr = "PatternType";
				concrete = true;
			}
			else if(type.IsSimple<EvaluationName>()) { // || type.IsSimple<Nullable<EvaluationName>>()
				typeStr = SharpValueHandler.GetTypeName(DisplayType.FromSystem<string>());
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
				typeRun.FontStyle = FontStyle.Italic; // System.Windows.FontStyles.Italic;
				typeRun.FontWeight = FontWeight.Bold; // System.Windows.FontWeights.Bold;
			}
			if(!arg.IsOptional) {
				typeRun.TextDecorations = TextDecorations.Underline;
			}

			return typeRun;
		}

	}

}
