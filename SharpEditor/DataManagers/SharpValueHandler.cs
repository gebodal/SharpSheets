using SharpSheets.Canvas;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Definitions;
using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Types;
using SharpSheets.Layouts;
using SharpSheets.Markup.Elements;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SharpEditor.DataManagers {

	public static class SharpValueHandler {

		public const char NO_BREAK_CHAR = '\u2060'; //'\u2007';
		public const char NO_BREAK_SPACE = '\u00a0'; //'\u2007';
		public const string NO_BREAK_SPACED_EQUALS = "\u00a0=\u00a0";
		public const string NO_BREAK_SPACED_PIPE = "\u00a0|\u00a0";

		public static string GetTypeName(DisplayType type) {
			if (type.IsSimple<float>() || type.IsSimple<double>()) {
				return "Number";
			}
			else if (type.IsSimple<UFloat>()) {
				return "Positive Number".Replace(' ', NO_BREAK_SPACE);
			}
			else if (type.IsSimple<UnitInterval>()) {
				return "Unit Interval".Replace(' ', NO_BREAK_SPACE);
			}
			else if (type.IsSimple<int>()) {
				return "Integer";
			}
			else if (type.IsSimple<uint>()) {
				return "Positive Integer".Replace(' ', NO_BREAK_SPACE);
			}
			else if (type.IsSimple<bool>()) {
				return "Flag";
			}
			else if (type.IsSequence(out _, out _) || type.IsTuple(out _) || type.IsDictionary(out _, out _)) {
				return GetCollectionTypeName(type, GetTypeName);
			}
			else if (type.IsNumbered && type.IsBase<ChildHolder>()) {
				return "(numbered children) Div".Replace(' ', NO_BREAK_SPACE); // TODO Need better name
			}
			else if (type.IsEntried) {
				return ("(list of) " + GetTypeName(type.AsSingle())).Replace(' ', NO_BREAK_SPACE);
			}
			else if (type.IsNumbered) {
				return ("(numbered) " + GetTypeName(type.AsSingle())).Replace(' ', NO_BREAK_SPACE);
			}
			else if (IsNamedChild(type)) {
				return "(child) Div".Replace(' ', NO_BREAK_SPACE); // TODO Need better name
			}
			else if (type.IsSimple<IBox>()) {
				return "Box";
			}
			else if (type.IsSimple<ILabelledBox>()) {
				return "LabelledBox";
			}
			else if (type.IsSimple<ITitledBox>()) {
				return "TitledBox";
			}
			else if (type.IsSimple<ITitleStyle>()) {
				return "TitleStyle";
			}
			else if (type.IsSimple<IEntriedShape>()) {
				return "EntriedShape";
			}
			else if (type.IsSimple<IBar>()) {
				return "Bar";
			}
			else if (type.IsSimple<IUsageBar>()) {
				return "UsageBar";
			}
			else if (type.IsSimple<IDetail>()) {
				return "DetailStyle";
			}
			else if (type.IsAssignableTo(typeof(IWidget))) {
				return "Widget";
			}
			else if (type.IsAssignableTo(typeof(AbstractCardSegmentConfig))) {
				return "SegmentConfig";
			}
			else if (type.IsAssignableTo(typeof(CardFeatureConfig))) {
				return "FeatureConfig";
			}
			else if (type.GetSingle() is Type typeSingle && ExpressionUsageMap.TryGetExpressionType(typeSingle, out Type? expressionType)) {
				return $"(expression) {GetTypeName(DisplayType.Create(expressionType))}";
			}
			else if (type.IsSimple<IDrawableElement>()) {
				return "DrawableElement";
			}
			else if (type.IsSimple<CanvasImageData>()) {
				return "Image";
			}
			else if (type.IsSimple<SharpSheets.Fonts.FontPath>()) {
				return "Font";
			}
			else if (type.IsSimple<SharpSheets.Fonts.FontPathGrouping>()) {
				return "FontGroup";
			}
			else if (type.IsSimple<object>()) {
				return "Value";
			}
			else if (type.GetSingle() is Type otherSingleType) {
				return otherSingleType.Name;
			}
			else {
				return type.Name;
			}
		}

		public static string GetEnvironmentTypeName(EvaluationType? type) {
			return type is null ? "any" : type.Name;
			//return GetEnvironmentTypeName(type.DisplayType);
		}

		public static string GetEnvironmentTypeName(DefinitionType type) {
			return GetEnvironmentTypeName(type.ReturnType);
		}

		public static string GetTypeName(ArgumentType type) {
			return GetTypeName(type.DisplayType);
		}

		public static string GetTypeName(EvaluationType type) {
			return GetTypeName(DisplayType.FromEvaluation(type)); // TODO Is this sufficient?
		}

		public static bool IsNamedChild(Type type) {
			return type == typeof(ChildHolder);
		}
		public static bool IsNamedChild(DisplayType type) {
			return type.IsSimple<ChildHolder>();
		}
		public static bool IsNamedChild(ArgumentType type) {
			return IsNamedChild(type.DisplayType);
		}

		private static string GetCollectionTypeName(DisplayType type, Func<DisplayType, string> typeNameGetter) {
			string name = GetCollectionTypeName(type, out string postfix, typeNameGetter);
			return name + NO_BREAK_CHAR + postfix;
		}
		private static string GetCollectionTypeName(DisplayType type, out string postfix, Func<DisplayType, string> typeNameGetter) {
			if (type.IsSequence(out DisplayType? elementType, out int length)) {
				string str = GetCollectionTypeName(elementType, out string elemPost, typeNameGetter);
				postfix = "[" + ( length > 0 ? length.ToString() : "") + "]" + elemPost;
				return str;
			}
			else if (type.GetSingle() is Type tupleType && TupleUtils.IsTupleType(tupleType)) {
				Type[] typeArgs = tupleType.GetGenericArguments();
				if (typeArgs.Distinct().Count() == 1) {
					string str = GetCollectionTypeName(DisplayType.Create(typeArgs[0]), out string itemPost, typeNameGetter);
					postfix = "[" + typeArgs.Length + "]" + itemPost;
					return str;
				}
				else {
					postfix = "";
					return "Tuple(" + string.Join(", ", typeArgs.Select(t => GetCollectionTypeName(DisplayType.Create(t), typeNameGetter))) + ")";
				}
			}
			else if (type.IsDictionary(out DisplayType? dictKeyType, out DisplayType? dictValueType)) {
				string str = GetCollectionTypeName(dictValueType, out string elemPost, typeNameGetter);
				postfix = "[" + typeNameGetter(dictKeyType) + "]" + elemPost;
				return str;
			}
			else {
				postfix = "";
				return typeNameGetter(type);
			}
		}

		public static string GetValueString(DisplayType type, object? value) {
			if (value == null) {
				return "None";
			}
			else if (type.IsSimple<Margins>()) {
				if (value is Margins margins) {
					return $"({margins.Top},{margins.Right},{margins.Bottom},{margins.Left})";
				}
				else if (value is string marginsStr) {
					try {
						Margins parsed = Margins.Parse(marginsStr, CultureInfo.InvariantCulture);
						return $"({parsed.Top},{parsed.Right},{parsed.Bottom},{parsed.Left})";
					}
					catch (FormatException) {
						return marginsStr;
					}
				}
			}
			else if (type.IsSimple<Position>()) {
				if (value is Position position) {
					return $"{{Anchor: {position.Anchor}, X: {position.X}, Y: {position.Y}, Width: {position.Width}, Height: {position.Height}}}";
				}
				else if (value is string positionStr) {
					try {
						Position parsed = Position.Parse(positionStr, CultureInfo.InvariantCulture);
						return $"{{Anchor: {parsed.Anchor}, X: {parsed.X}, Y: {parsed.Y}, Width: {parsed.Width}, Height: {parsed.Height}}}";
					}
					catch (FormatException) {
						return positionStr;
					}
					catch (ArgumentException) {
						return positionStr;
					}
				}
				else {
					return "Error Position.";
				}
			}
			else if (type.IsSimple<SharpSheets.Utilities.Vector>()) {
				if (value is SharpSheets.Utilities.Vector vector) {
					return $"({vector.X},{vector.Y})";
				}
				else if (value is string vectorStr) {
					try {
						SharpSheets.Utilities.Vector parsed = SharpSheets.Utilities.Vector.Parse(vectorStr, CultureInfo.InvariantCulture);
						return $"({parsed.X},{parsed.Y})";
					}
					catch (FormatException) {
						return vectorStr;
					}
				}
			}
			else if (type.IsSimple<SharpSheets.Fonts.FontTags>()) {
				if (value is SharpSheets.Fonts.FontTags fontTags) {
					return fontTags.ToString();
				}
				else if (value is string fontTagsStr) {
					try {
						SharpSheets.Fonts.FontTags parsed = SharpSheets.Fonts.FontTags.Parse(fontTagsStr);
						return parsed.ToString();
					}
					catch (FormatException) {
						return fontTagsStr;
					}
				}
			}
			else if (type.IsSequence(out _, out _)) {
				if (value is Array array) {
					return ArrayToString(type, array);
				}
				else {
					return value?.ToString() ?? "[Invalid]";
				}
			}
			else if (type.IsTuple(out _)) {
				if (TupleUtils.IsTupleObject(value, out _)) {
					return ArrayToString(type, value);
				}
				else {
					return value?.ToString() ?? "[Invalid]";
				}
			}
			else if (type.IsDictionary(out _, out _)) {
				if (value is IDictionary dict) {
					return DictionaryToString(type, dict);
				}
				else {
					return value?.ToString() ?? "[Invalid]";
				}
			}
			else if (type.IsEnum) {
				return GetEnumString(value);
			}
			else if (type.IsSimple<SharpSheets.Colors.Color>()) {
				if (value is SharpSheets.Colors.Color color) {
					if (color.IsNamedColor) {
						return color.Name;
					}
					else {
						return color.ToHexString();
					}
				}
				else {
					return value?.ToString() ?? "[Invalid]";
				}
			}
			else if (value is EvaluationValue evalVal) {
				return GetValueString(type, evalVal.Value);
			}
			else if (value is IShape shape) {
				return shape.DisplayName;
			}
			else {
				return value?.ToString() ?? "[Invalid]";
			}

			return "[Invalid]";
		}

		public static string GetEnumString(object value) {
			if (value is Enum enumVal) {
				return enumVal.ToString().ToUpper();
			}
			else if (value is string enumStr) {
				return enumStr.ToUpper();
			}
			else {
				return "[Invalid]";
			}
		}

		private static bool ValueStringsMatch(bool isEnum, string a, string b) {
			if (isEnum) {
				return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
			}
			else {
				return string.Equals(a, b);
			}
		}

		public static bool ValueStringsMatch(Type type, string a, string b) {
			return ValueStringsMatch(type.IsEnum, a, b);
		}

		public static bool ValueStringsMatch(DisplayType type, string a, string b) {
			return ValueStringsMatch(type.IsEnum, a, b);
		}

		private static string JoinCollectionParts(IList<string> parts, string sep) {
			if (parts.Count == 0) {
				return "empty";
			}
			else {
				return string.Join(sep, parts);
			}
		}

		private static readonly int arrayMaxRank = 3;
		private static readonly string[] arraySeparators = new string[] { ", ", "; ", " | " };
		private static string ArrayToString(DisplayType type, object? value) => ArrayToString(type, value, out _);
		private static string ArrayToString(DisplayType type, object? value, out int rank) {
			if (value is Array array && type.IsSequence(out DisplayType? elementType, out int length)) {
				List<string> parts = new List<string>();
				rank = 0;
				foreach (object i in array.Cast<object>()) {
					parts.Add(ArrayToString(elementType, i, out int iRank));
					rank = Math.Max(rank, iRank);
				}
				rank += 1;
				if (rank > arrayMaxRank) {
					//throw new ArgumentException($"Cannot process arrays with a rank above {arrayMaxRank}."); // TODO Better exception type?
					return "INVALID ARRAY RANK";
				}
				return JoinCollectionParts(parts, arraySeparators[rank - 1]);
			}
			else if (value is ITuple tupleValue) {
				List<string> parts = new List<string>();
				rank = 0;
				for (int i = 0; i < tupleValue.Length; i++) {
					object? v = tupleValue[i];
					if (v?.GetType() is Type elemType) {
						parts.Add(ArrayToString(DisplayType.Create(elemType), v, out int iRank));
						rank = Math.Max(rank, iRank);
					}
					else {
						parts.Add(GetValueString(DisplayType.Create(typeof(void)), null));
					}
				}
				rank += 1;
				if (rank > arrayMaxRank) {
					//throw new ArgumentException($"Cannot process arrays with a rank above {arrayMaxRank}."); // TODO Better exception type?
					return "INVALID TUPLE RANK";
				}
				return JoinCollectionParts(parts, arraySeparators[rank - 1]);
			}
			else {
				rank = 0;
				return GetValueString(type, value);
			}
		}

		private static string DictionaryToString(DisplayType type, IDictionary dict) {
			if (type.IsDictionary(out DisplayType? dictKeyType, out DisplayType? dictValueType)) {
				List<string> parts = new List<string>();

				foreach (object key in dict.Keys.Cast<object>()) {

					object? keyValue = dict[key];

					string keyStr = GetValueString(dictKeyType, key);
					string valueStr = GetValueString(dictValueType, keyValue);

					parts.Add($"{keyStr}: {valueStr}");
				}

				return JoinCollectionParts(parts, ", ");
			}
			else {
				return "INVALID DICTIONARY OBJECT";
			}
		}

	}

}
