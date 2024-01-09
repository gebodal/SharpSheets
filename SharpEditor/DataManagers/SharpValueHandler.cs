using SharpSheets.Canvas;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Documentation;
using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Markup.Elements;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace SharpEditor.DataManagers {

	public static class SharpValueHandler {

		public const char NO_BREAK_CHAR = '\u2060'; //'\u2007';
		public const char NO_BREAK_SPACE = '\u00a0'; //'\u2007';
		public const string NO_BREAK_SPACED_EQUALS = "\u00a0=\u00a0";
		public const string NO_BREAK_SPACED_PIPE = "\u00a0|\u00a0";

		public static string GetTypeName(Type type) {
			if (type == typeof(float) || type == typeof(double)) {
				return "Number";
			}
			else if (type == typeof(UFloat)) {
				return "Positive Number".Replace(' ', NO_BREAK_SPACE);
			}
			else if (type == typeof(UnitInterval)) {
				return "Unit Interval".Replace(' ', NO_BREAK_SPACE);
			}
			else if (type == typeof(int)) {
				return "Integer";
			}
			else if (type == typeof(uint)) {
				return "Positive Integer".Replace(' ', NO_BREAK_SPACE);
			}
			else if (type == typeof(bool)) {
				return "Flag";
			}
			/*
			else if (type.GetElementType() is Type elementType) {
				return GetTypeName(elementType) + "\u2060[]"; // Should be a no-breaking zero-width joiner
			}
			else if (TupleUtils.IsTupleType(type)) {

			}
			*/
			else if (type.IsArray || TupleUtils.IsTupleType(type)) {
				return GetArrayTypeName(type, GetTypeName);
			}
			else if (Nullable.GetUnderlyingType(type) is Type nulledType) {
				return GetTypeName(nulledType);
			}
			else if(type == typeof(Numbered<ChildHolder>)) {
				return "(numbered children) Div".Replace(' ', NO_BREAK_SPACE); // TODO Need better name
			}
			else if (type.GetGenericArguments().FirstOrDefault() is Type listType && type.GetGenericTypeDefinition() == typeof(List<>)) {
				return ("(list of) " + GetTypeName(listType)).Replace(' ', NO_BREAK_SPACE);
			}
			else if (type.GetGenericArguments().FirstOrDefault() is Type numberedType && type.GetGenericTypeDefinition() == typeof(Numbered<>)) {
				return ("(numbered) " + GetTypeName(numberedType)).Replace(' ', NO_BREAK_SPACE);
			}
			/*
			else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ChildHolder<>)) {
				Type childType = type.GetGenericArguments().Single();
				return ("(child) " + GetTypeName(childType)).Replace(' ', NO_BREAK_SPACE);
			}
			*/
			else if (type == typeof(ChildHolder)) {
				return "(child) Div".Replace(' ', NO_BREAK_SPACE); // TODO Need better name
			}
			else if (type == typeof(IContainerShape)) {
				return "Container";
			}
			else if (type == typeof(IBox)) {
				return "Box";
			}
			else if (type == typeof(ILabelledBox)) {
				return "LabelledBox";
			}
			else if (type == typeof(ITitledBox)) {
				return "TitledBox";
			}
			else if (type == typeof(ITitleStyledBox)) {
				return "TitleStyle";
			}
			else if (type == typeof(IBar)) {
				return "Bar";
			}
			else if (type == typeof(IUsageBar)) {
				return "UsageBar";
			}
			else if (type == typeof(IDetail)) {
				return "DetailStyle";
			}
			else if (type == typeof(IWidget) || type == typeof(SharpWidget)) {
				return "Widget";
			}
			else if (typeof(AbstractCardSectionConfig).IsAssignableFrom(type)) {
				return "CardSectionConfig";
			}
			else if (type == typeof(IExpression<string>)) {
				return "StringExpression";
			}
			else if (type == typeof(IDrawableElement)) {
				return "DrawableElement";
			}
			else if (type == typeof(CanvasImageData)) {
				return "Image";
			}
			else if (type == typeof(object)) {
				return "Value";
			}
			else {
				return type.Name;
			}
		}

		public static string GetEnvironmentTypeName(EvaluationType? type) {
			return type is null ? "any" : type.Name;
			//return GetEnvironmentTypeName(type.DisplayType);
		}

		private static string GetEnvironmentTypeName(Type type) {
			if (type == typeof(bool)) {
				return "Bool";
			}
			else if (type.IsArray || TupleUtils.IsTupleType(type)) {
				return GetArrayTypeName(type, GetEnvironmentTypeName);
			}
			else {
				return GetTypeName(type);
			}
		}

		public static string GetTypeName(ArgumentType type) {
			return GetTypeName(type.DisplayType);
		}

		public static string GetTypeName(EvaluationType type) {
			return GetTypeName(type.DisplayType); // TODO Is this sufficient?
		}

		private static string GetArrayTypeName(Type type, Func<Type, string> typeNameGetter) {
			string name = GetArrayTypeName(type, out string postfix, typeNameGetter);
			return name + NO_BREAK_CHAR + postfix;
		}
		private static string GetArrayTypeName(Type type, out string postfix, Func<Type, string> typeNameGetter) {
			if (type.IsArray && type.GetElementType() is Type elementType) {
				string str = GetArrayTypeName(elementType, out string elemPost, typeNameGetter);
				postfix = "[]" + elemPost;
				return str;
			}
			else if (TupleUtils.IsTupleType(type)) {
				Type[] typeArgs = type.GetGenericArguments();
				if (typeArgs.Distinct().Count() == 1) {
					string str = GetArrayTypeName(typeArgs[0], out string itemPost, typeNameGetter);
					postfix = "[" + typeArgs.Length + "]" + itemPost;
					return str;
				}
				else {
					postfix = "";
					return "Tuple(" + string.Join(", ", typeArgs.Select(t => GetArrayTypeName(t, typeNameGetter))) + ")";
				}
			}
			else {
				postfix = "";
				return typeNameGetter(type);
			}
		}

		public static string GetValueString(Type type, object? value) {
			type = type.GetUnderlyingType();

			if (value == null) {
				return "None";
			}
			else if (type == typeof(Margins)) {
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
			else if (type == typeof(Position)) {
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
			else if (type == typeof(SharpSheets.Utilities.Vector)) {
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
			else if (type.IsArray) {
				if (value is Array array) {
					/*
					Type elementType = type.GetElementType();
					if (elementType.IsArray) {
						if (elementType.GetElementType().IsArray) {
							return "INVALID ARRAY RANK";
						}
						return string.Join("; ", array.Cast<object>().Select(v => GetValueString(elementType, v)));
					}
					else {
						return string.Join(", ", array.Cast<object>().Select(v => GetValueString(elementType, v)));
					}
					*/
					return ArrayToString(type, array);
				}
				else {
					return value?.ToString() ?? "[Invalid]";
				}
			}
			else if (TupleUtils.IsTupleType(type)) {
				if (TupleUtils.IsTupleObject(value, out Type? valueType)) {
					/*
					Type elementType = type.GetElementType();
					if (elementType.IsArray) {
						if (elementType.GetElementType().IsArray) {
							return "INVALID ARRAY RANK";
						}
						return string.Join("; ", array.Cast<object>().Select(v => GetValueString(elementType, v)));
					}
					else {
						return string.Join(", ", array.Cast<object>().Select(v => GetValueString(elementType, v)));
					}*/
					return ArrayToString(valueType, value);
				}
				else {
					return value?.ToString() ?? "[Invalid]";
				}
			}
			else if (type.IsEnum) {
				return GetEnumString(value);
			}
			else if (type == typeof(SharpSheets.Colors.Color)) {
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

		public static bool ValueStringsMatch(Type type, string a, string b) {
			if (type.IsEnum) {
				return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
			}
			else {
				return string.Equals(a, b);
			}
		}

		private static readonly int arrayMaxRank = 3;
		private static readonly string[] arraySeparators = new string[] { ", ", "; ", " | " };
		private static string ArrayToString(Type type, object? value) => ArrayToString(type, value, out _);
		private static string ArrayToString(Type type, object? value, out int rank) {
			if (value is Array array && type.GetElementType() is Type elemType) {
				List<string> parts = new List<string>();
				rank = 0;
				foreach (object i in array.Cast<object>()) {
					parts.Add(ArrayToString(elemType, i, out int iRank));
					rank = Math.Max(rank, iRank);
				}
				rank += 1;
				if (rank > arrayMaxRank) {
					//throw new ArgumentException($"Cannot process arrays with a rank above {arrayMaxRank}."); // TODO Better exception type?
					return "INVALID ARRAY RANK";
				}
				return string.Join(arraySeparators[rank - 1], parts);
			}
			else if (value is not null && TupleUtils.IsTupleType(value.GetType())) {
				List<string> parts = new List<string>();
				rank = 0;
				foreach (FieldInfo field in value.GetType().GetFields()) {
					object? i = field.GetValue(value);
					parts.Add(ArrayToString(field.FieldType, i, out int iRank));
					rank = Math.Max(rank, iRank);
				}
				rank += 1;
				if (rank > arrayMaxRank) {
					//throw new ArgumentException($"Cannot process arrays with a rank above {arrayMaxRank}."); // TODO Better exception type?
					return "INVALID TUPLE RANK";
				}
				return string.Join(arraySeparators[rank - 1], parts);
			}
			else {
				rank = 0;
				return GetValueString(type, value);
			}
		}

	}

}
