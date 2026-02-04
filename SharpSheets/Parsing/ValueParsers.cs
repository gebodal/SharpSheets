using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Colors;
using SharpSheets.Fonts;
using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System.Globalization;
using System.Text.RegularExpressions;

[assembly: SharpSheets.Parsing.GenerateParameterParser(typeof(string[]))]

namespace SharpSheets.Parsing {

	public static class ValueBuilders {

		/// <summary>
		/// 
		/// </summary>
		/// <param name="buildErrors"></param>
		/// <param name="values"></param>
		/// <param name="top"></param>
		/// <param name="right"></param>
		/// <param name="bottom"></param>
		/// <param name="left"></param>
		/// <returns></returns>
		[FactoryBuilder(typeof(Margins))]
		[SupplementedArgumentBuilder]
		public static Margins BuildMargins([BuildErrors] IList<Exception> buildErrors, float[]? values = null, float? top = null, float? right = null, float? bottom = null, float? left = null) {
			float top_final, right_final, bottom_final, left_final;
			if(values is not null) {
				switch(values.Length) {
					case 1:
						top_final = right_final = bottom_final = left_final = values[0];
						break;
					case 2:
						top_final = bottom_final = values[0];
						right_final = left_final = values[1];
						break;
					case 4:
						top_final = values[0];
						right_final = values[1];
						bottom_final = values[2];
						left_final = values[3];
						break;
					default:
						top_final = right_final = bottom_final = left_final = 0f;
						buildErrors.Add(new ArgumentException($"Invalid number of values ({values.Length}), must be 1, 2, or 4.", nameof(values)));
						break;
				}
			}
			else {
				top_final = right_final = bottom_final = left_final = 0f;
			}

			return new Margins(top ?? top_final, right ?? right_final, bottom ?? bottom_final, left ?? left_final);
		}

		// This is a grouped argument, but if no parameters are present it shouldn't be built
		/// <summary>
		/// 
		/// </summary>
		/// <param name="anchor"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <returns></returns>
		[FactoryBuilder(typeof(Position?))]
		[GroupedArgumentBuilder]
		public static Position? BuildPosition(Anchor? anchor = null, Dimension? x = null, Dimension? y = null, Dimension? width = null, Dimension? height = null) {
			if (anchor.HasValue || x.HasValue || y.HasValue || width.HasValue || height.HasValue) {
				return new Position(anchor ?? Anchor.BOTTOMLEFT, x ?? Dimension.Zero, y ?? Dimension.Zero, width ?? Dimension.FromPercent(100), height ?? Dimension.FromPercent(100));
			}
			else {
				return null;
			}
		}

	}

	public static class ValueParsers {

		private static readonly char[] arrayDelimiters = { ',', ';', '|' };
		private static readonly string[] arraySeparators = new string[] { ", ", "; ", " | " };
		public static readonly int MaxArrayOrTupleRank = 3;
		public static char GetArrayDelimiter(int rank) {
			if (rank < 0 || rank >= arrayDelimiters.Length) { throw new InvalidOperationException($"Cannot get delimiter for array of rank {rank} (must be 0-{MaxArrayOrTupleRank})."); }
			return arrayDelimiters[rank - 1];
		}
		public static string GetArraySeparator(int rank) {
			if (rank < 0 || rank >= arrayDelimiters.Length) { throw new InvalidOperationException($"Cannot get separator for array of rank {rank} (must be 0-{MaxArrayOrTupleRank})."); }
			return arraySeparators[rank - 1];
		}

		[ParameterParser]
		public static int ParseInt(string value) {
			try { return int.Parse(value, CultureInfo.InvariantCulture); }
			catch (FormatException e) { throw new FormatException($"\"{value}\" is not an integer.", e); }
		}

		[ParameterParser]
		public static uint ParseUInt(string value) {
			try { return uint.Parse(value, CultureInfo.InvariantCulture); }
			catch (SystemException e) { throw new FormatException($"\"{value}\" is not a positive integer.", e); }
		}

		[ParameterParser]
		public static float ParseFloat(string value) {
			try { return float.Parse(value, CultureInfo.InvariantCulture); }
			catch (FormatException e) { throw new FormatException($"\"{value}\" is not a number.", e); }
		}

		[ParameterParser]
		public static UFloat ParseUFloat(string value) {
			return UFloat.Parse(value, CultureInfo.InvariantCulture);
		}

		[ParameterParser]
		public static UnitInterval ParseUnitInterval(string value) {
			return UnitInterval.Parse(value, CultureInfo.InvariantCulture);
		}

		[ParameterParser]
		public static double ParseDouble(string value) {
			try { return double.Parse(value, CultureInfo.InvariantCulture); }
			catch (FormatException e) { throw new FormatException($"\"{value}\" is not a number.", e); }
		}

		[ParameterParser]
		public static bool ParseBool(string value) {
			try { return bool.Parse(value); }
			catch (FormatException e) { throw new FormatException($"\"{value}\" is not a boolean.", e); }
		}

		[ParameterParser]
		public static string ParseString(string value) {
			return StringParsing.Parse(value);
		}

		[ParameterParser]
		public static char ParseChar(string value) {
			string strValue = StringParsing.Parse(value);
			if (strValue.Length != 1) {
				throw new FormatException($"Invalid character value: \"{value}\"");
			}
			return strValue[0];
		}

		[ParameterParser]
		public static RichString ParseRichString(string value) {
			//string escaped = StringParsing.Parse(value);
			//return new RichString(escaped);
			return StringParsing.ParseRich(value);
		}

		[ParameterParser]
		public static Dimension ParseDimension(string value) {
			return Dimension.Parse(value, CultureInfo.InvariantCulture);
		}

		[ParameterParser]
		public static Vector ParseVector(string value) {
			return Vector.Parse(value, CultureInfo.InvariantCulture);
		}

		/*
		[ParameterParser]
		public static Position ParsePosition(string value) {
			return Position.Parse(value, CultureInfo.InvariantCulture);
		}
		*/

		/*
		[ParameterParser]
		public static Margins ParseMargins(string value) {
			return Margins.Parse(value, CultureInfo.InvariantCulture);
		}
		*/

		[ParameterParser]
		public static Color ParseColor(string value) {
			return ColorUtils.Parse(value, CultureInfo.InvariantCulture);
		}

		[ParameterParser]
		public static PageSize ParsePageSize(string value) {
			return PageSize.ParsePageSize(value, CultureInfo.InvariantCulture);
		}

		[ParameterParser]
		public static FilePath ParseFilePath(string value, DirectoryPath source) {
			string path = PathSubSource(value, source);
			FilePath finalPath = new FilePath(source.Path, path);
			if (!finalPath.Exists) { throw new ArgumentException($"{finalPath.Path} does not exist."); } // Some other exception type?
			return finalPath;
		}

		[ParameterParser]
		public static DirectoryPath ParseDirectoryPath(string value, DirectoryPath source) {
			string path = PathSubSource(value, source);
			DirectoryPath finalPath = new DirectoryPath(source.Path, path);
			if (!finalPath.Exists) { throw new ArgumentException($"{finalPath.Path} does not exist."); } // Some other exception type?
			return finalPath;
		}

		[ParameterParser]
		public static FontPath ParseFontPath(string value, DirectoryPath source) {
			string path = PathSubSource(value, source);
			return FontPathParsing.Parse(path, source.Path); // Can throw FormatException
		}

		[ParameterParser]
		public static FontPathGrouping ParseFontPathGrouping(string value, DirectoryPath source) {
			string[] parts = value.SplitAndTrim(',').Select(p => PathSubSource(p, source)).ToArray();
			return FontPathParsing.ParseGrouping(parts, source.Path);
		}

		[ParameterParser]
		public static FontTags ParseFontTags(string value) {
			return FontTags.Parse(value);
		}

		[ParameterParser]
		public static CanvasImageData ParseCanvasImageData(string value, DirectoryPath source) {
			FilePath imagePath = ParseFilePath(value, source) ?? throw new FormatException("Could not resolve image path.");
			return new CanvasImageData(imagePath);
		}

		[ParameterParser]
		public static Regex? ParseRegex(string value) {
			if (string.Equals(value, "null", StringComparison.InvariantCultureIgnoreCase)) {
				return null;
			}
			else {
				return new Regex(value);
			}
		}

		/*
		else if (type.IsArray || TupleUtils.IsTupleType(type)) {
			return ParseArrayOrTuple(value, type, source);
		}
		*/

		/*
		else if (type.TryGetGenericTypeDefinition() is Type genericListType && genericListType == typeof(List<>)) {
			Type elementType = type.GetGenericArguments().Single();
			if (elementType.TryGetGenericTypeDefinition() is Type elementGenericType && elementGenericType == typeof(List<>)) {
				throw new InvalidOperationException("Cannot parse nested List objects.");
			}
			object? parseResult = ParseArrayOrTuple(value, elementType.MakeArrayType(), source);
			if (parseResult is Array arrayValues) {
				return ListUtils.ConvertArrayObjectToList(arrayValues, type);
			}
			else {
				throw new FormatException($"Could not parse {value} into {type} type.");
			}
		}
		*/

		/*
		else if (type.TryGetGenericTypeDefinition() is Type genericNumberedType && genericNumberedType == typeof(Numbered<>)) {
			Type elementType = type.GetGenericArguments().Single();
			object? parseResult = ParseArrayOrTuple(value, elementType.MakeArrayType(), source);
			if (parseResult is Array arrayValues) {
				return NumberedUtils.ConvertArrayObjectToNumbered(arrayValues, type);
			}
			else {
				throw new FormatException($"Could not parse {value} into {type} type.");
			}
		}
		*/

		/*
		else if (type.IsAssignableTo(typeof(IDictionary))) { // type.TryGetGenericTypeDefinition() is Type genericDictType && genericDictType == typeof(Dictionary<,>)
			return ParseDict(value, type, source);
		}
		*/

		/*
		else if (Nullable.GetUnderlyingType(type) is Type nulledType) {
			return Parse(value, nulledType, source);
		}
		*/

		/*
		else if (type.IsEnum) {
			return EnumUtils.ParseEnum(type, value);
		}
		*/

		/*
		else if (TryGetSimpleBuilder(type, out MethodInfo? constructor)) {
			return ParseValueDict(value, constructor, source);
		}
		*/

		#region Helpers
		public static readonly string SourceKeyword = "&SOURCE";
		private static readonly Regex sourceRegex = new Regex(Regex.Escape(SourceKeyword));
		private static string PathSubSource(string path, DirectoryPath source) {
			return sourceRegex.Replace(path, source.Path, 1);
		}
		#endregion Helpers

	}

}
