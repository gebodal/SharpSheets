using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SharpSheets.Canvas.Text;
using SharpSheets.Colors;
using SharpSheets.Fonts;
using SharpSheets.Canvas;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SharpSheets.Parsing {

	// For Tuple parsing at some point: https://stackoverflow.com/a/43127612/11002708

	/// <summary>
	/// A class containing methods for parsing <see cref="string"/> values into <see cref="object"/> values,
	/// according to the SharpSheets style conventions.
	/// </summary>
	public static class ValueParsing {

		/// <summary>
		/// Parse a string into an <see cref="object"/> of the appropriate type,
		/// using the SharpSheets syntax conventions. Arrays are divided first by
		/// unescaped commas, followed by unescaped semi-colons, and finally unescaped pipe characters ('|').
		/// </summary>
		/// <param name="value">The value string to be parsed (must not be <see langword="null"/>).</param>
		/// <param name="type">The <see cref="Type"/> of the object to be created.</param>
		/// <param name="source">The source directory for this value (used primarily for parsing path types).</param>
		/// <returns>An <see cref="object"/> of a type corresponding to <paramref name="type"/>.</returns>
		/// <exception cref="FormatException">Thrown when <paramref name="value"/> cannot be parsed into the desired type.</exception>
		/// <exception cref="ArgumentException">Thrown when <paramref name="type"/> is a path type, and <paramref name="value"/> is not an existing path.</exception>
		/// <exception cref="NotSupportedException">Thrown when there is no corresponding parser for <paramref name="type"/>.</exception>
		/// <exception cref="SystemException"></exception>
		public static object? Parse(string value, Type type, DirectoryPath source) {
			if (type == typeof(int)) {
				try { return int.Parse(value, CultureInfo.InvariantCulture); }
				catch (FormatException e) { throw new FormatException($"\"{value}\" is not an integer.", e); }
			}
			else if (type == typeof(uint)) {
				try { return uint.Parse(value, CultureInfo.InvariantCulture); }
				catch (SystemException e) { throw new FormatException($"\"{value}\" is not a positive integer.", e); }
			}
			else if (type == typeof(float)) {
				try { return float.Parse(value, CultureInfo.InvariantCulture); }
				catch (FormatException e) { throw new FormatException($"\"{value}\" is not a number.", e); }
			}
			else if (type == typeof(UFloat)) {
				return UFloat.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (type == typeof(UnitInterval)) {
				return UnitInterval.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (type == typeof(double)) {
				try { return double.Parse(value, CultureInfo.InvariantCulture); }
				catch (FormatException e) { throw new FormatException($"\"{value}\" is not a number.", e); }
			}
			else if (type == typeof(bool)) {
				try { return bool.Parse(value); }
				catch (FormatException e) { throw new FormatException($"\"{value}\" is not a boolean.", e); }
			}
			else if (type == typeof(string)) {
				return StringParsing.Parse(value);
			}
			else if (type == typeof(char)) {
				string strValue = StringParsing.Parse(value);
				if (strValue.Length != 1) {
					throw new FormatException($"Invalid character value: \"{value}\"");
				}
				return strValue[0];
			}
			else if (type == typeof(RichString)) {
				//string escaped = StringParsing.Parse(value);
				//return new RichString(escaped);
				return StringParsing.ParseRich(value);
			}
			else if (type == typeof(Dimension)) {
				return Dimension.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (type == typeof(Vector)) {
				return Vector.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (type == typeof(Position)) {
				return Position.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (type == typeof(Margins)) {
				return Margins.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (type == typeof(Color)) {
				return ColorUtils.Parse(value, CultureInfo.InvariantCulture);
			}
			else if (type == typeof(PageSize)) {
				return PageSize.ParsePageSize(value, CultureInfo.InvariantCulture);
			}
			else if (typeof(SystemPath).IsAssignableFrom(type)) {
				string path = PathSubSource(value, source);
				SystemPath finalPath;
				if (type == typeof(FilePath)) {
					finalPath = new FilePath(source.Path, path);
				}
				else if (type == typeof(DirectoryPath)) {
					finalPath = new DirectoryPath(source.Path, path);
				}
				else {
					finalPath = new SystemPath(source.Path, path);
				}
				if (!finalPath.Exists) { throw new ArgumentException($"{finalPath.Path} does not exist."); } // Some other exception type?
				return finalPath;
			}
			else if (type == typeof(FontPath)) {
				string path = PathSubSource(value, source);
				return FontPathParsing.Parse(path, source.Path); // Can throw FormatException
			}
			else if (type == typeof(FontPathGrouping)) {
				string[] parts = value.SplitAndTrim(',').Select(p => PathSubSource(p, source)).ToArray();
				return FontPathParsing.ParseGrouping(parts, source.Path);
			}
			else if(type == typeof(CanvasImageData)) {
				FilePath imagePath = Parse<FilePath>(value, source) ?? throw new FormatException("Could not resolve image path.");
				return new CanvasImageData(imagePath);
			}
			else if (type.IsArray || TupleUtils.IsTupleType(type)) {
				return ParseArrayOrTuple(value, type, source);
			}
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
			else if (Nullable.GetUnderlyingType(type) is Type nulledType) {
				return Parse(value, nulledType, source);
			}
			else if (type.IsEnum) {
				return EnumUtils.ParseEnum(type, value);
			}
			else if (type == typeof(Regex)) {
				if (string.Equals(value, "null", StringComparison.InvariantCultureIgnoreCase)) {
					return null;
				}
				else {
					return new Regex(value);
				}
			}
			else if (TryGetSimpleConstructor(type, out ConstructorInfo? constructor)) {
				return ParseValueDict(value, constructor, source);
			}
			else {
				throw new NotSupportedException($"Parser for arguments of type {type.Name} not supported.");
			}
		}

		/// <summary>
		/// Parse a string into an object of type <typeparamref name="T"/>,
		/// using the SharpSheets syntax conventions. Arrays are divided first by
		/// unescaped commas, followed by unescaped semi-colons, and finally unescaped pipe characters ('|').
		/// </summary>
		/// <typeparam name="T">The type into which the <paramref name="value"/> text should be parsed.</typeparam>
		/// <param name="value">The value string to be parsed.</param>
		/// <param name="source">The source directory for this value (used primarily for parsing path types).</param>
		/// <returns>An object of a type corresponding to <typeparamref name="T"/>.</returns>
		/// <exception cref="FormatException">Thrown when <paramref name="value"/> cannot be parsed into the desired type.</exception>
		/// <exception cref="ArgumentException">Thrown when <typeparamref name="T"/> is a path type, and <paramref name="value"/> is not an existing path.</exception>
		/// <exception cref="NotImplementedException">Thrown when there is no corresponding parser for type <typeparamref name="T"/>.</exception>
		/// <exception cref="SystemException">Thrown when a system error has occured trying to construct an object.</exception>
		public static T? Parse<T>(string value, DirectoryPath source) {
			return (T?)Parse(value, typeof(T), source);
		}

		/// <summary>
		/// Convert an object into an <see cref="string"/> with the appropriare formatting,
		/// using the SharpSheets syntax conventions. Arrays are divided first by
		/// unescaped commas, followed by unescaped semi-colons.
		/// </summary>
		/// <param name="value">The object to be converted.</param>
		/// <returns>
		/// An <see cref="string"/> representation of <paramref name="value"/>,
		/// according to the SharpSheets syntax conventions.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
		/// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is an array with rank greater than 2.</exception>
		/// <exception cref="NotSupportedException">Thrown when there is no corresponding serialization for <paramref name="value"/>.</exception>
		/// <exception cref="FormatException">Thrown when <paramref name="value"/> is a badly formatted <see cref="string"/> or <see cref="RichString"/>.</exception>
		public static string ToString(object? value) {
			if(value == null) {
				throw new ArgumentNullException(nameof(value), "Cannot convert null value to string.");
			}

			if (value is int intVal) {
				return intVal.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is uint uintVal) {
				return uintVal.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is float floatVal) {
				return floatVal.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is UFloat uFloatVal) {
				return uFloatVal.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is UnitInterval unitIntervalVal) {
				return unitIntervalVal.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is double doubleVal) {
				return doubleVal.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is bool boolVal) {
				return boolVal.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is string stringVal) {
				return StringParsing.Escape(stringVal);
			}
			else if (value is RichString richString) {
				//return richString.Formatted; // This will not be escaped correctly
				return StringParsing.EscapeRich(richString);
			}
			else if (value is Dimension dimension) {
				return dimension.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is Vector vector) {
				return vector.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is Position position) {
				return string.Format(CultureInfo.InvariantCulture, "{{anchor:{0}, x:{1}, y:{2}, width:{3}, height:{4}}}", position.Anchor, position.X, position.Y, position.Width, position.Height);
				//return $"{{anchor:{position.Anchor},x:{position.X},y:{position.Y},width:{position.Width},height:{position.Height}}}";
			}
			else if (value is Margins margins) {
				return string.Format(CultureInfo.InvariantCulture, "{{top:{0}, right:{1}, bottom:{2}, left:{3}}}", margins.Top, margins.Right, margins.Bottom, margins.Left);
				//return $"{{top:{margins.Top},right:{margins.Right},bottom:{margins.Bottom},left:{margins.Left}}}";
			}
			else if (value is Color color) {
				return color.ToHexString();
			}
			else if (value is PageSize pageSize) {
				return pageSize.ToString(CultureInfo.InvariantCulture);
			}
			/*
			else if (value is Array array) {
				Type elementType = array.GetType().GetElementType();
				if (elementType.IsArray) {
					if (elementType.GetElementType().IsArray) {
						throw new InvalidOperationException("Cannot process arrays with a rank above 2."); // TODO Better exception type?
					}
					return string.Join(";", array.Cast<Array>().Select(a => StringParsing.EnsureEscaped(ValueParsing.ToString(a), ';')));
				}
				else {
					return string.Join(",", array.Cast<object>().Select(v => StringParsing.EnsureEscaped(ValueParsing.ToString(v), ',')));
				}
			}
			*/
			else if (value is Array || TupleUtils.IsTupleType(value.GetType())) {
				return ArrayToString(value);
			}
			else if (value is INumbered numbered) {
				object?[] values = numbered.Select(kv => kv.Value).ToArray();
				return ArrayToString(values);
			}
			else if (value is Enum enumValue) {
				return enumValue.ToString();
			}
			else if (value is Regex regex) {
				return regex.ToString();
			}

			throw new NotSupportedException($"String format for values of type {value.GetType().Name} not supported.");
		}

		public static bool TryGetSimpleConstructor(Type type, [MaybeNullWhen(false)] out ConstructorInfo constructor) {
			constructor = type.GetConstructors().FirstOrDefault(c => c.IsPublic && c.GetParameters().Length > 0);
			return constructor != null;
		}
		public static ConstructorInfo GetSimpleConstructor(Type type) {
			if (TryGetSimpleConstructor(type, out ConstructorInfo? constructor)) {
				return constructor;
			}
			else {
				throw new InvalidOperationException($"No simple constructor implemented for {type.Name}");
			}
		}

		private static readonly Regex dictRegex = new Regex(@"(?<!\\)\{(?<dict>.+)(?<!\\)\}");
		private static readonly Regex commaSplitRegex = new Regex(@"(?<!\\)\,");
		private static readonly Regex argRegex = new Regex(@"
				^(
					(?<property>[a-z][a-z0-9]*) \s* \: \s* (?<value>.+)
					|
					(?<flag>\!?[a-z][a-z0-9]*)
				)$
			", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static object ParseValueDict(string value, ConstructorInfo constructor, DirectoryPath source) {
			Match dictMatch = dictRegex.Match(value);
			if (dictMatch.Success) {
				value = dictMatch.Groups["dict"].Value;
			}
			else {
				throw new FormatException($"Badly formatted dictionary string for {value.GetType().Name}.");
			}

			//ConstructorInfo constructor = GetSimpleConstructor(type);
			ParameterInfo[] parameterList = constructor.GetParameters();

			if (parameterList.Any(p => p.ParameterType.IsArray)) {
				throw new NotSupportedException("Array argument parsing not supported for dictionary-style initialization.");
			}

			Dictionary<string, string> properties = new Dictionary<string, string>(SharpDocuments.StringComparer);
			Dictionary<string, bool> flags = new Dictionary<string, bool>(SharpDocuments.StringComparer);
			string[] argStrings = commaSplitRegex.Split(value);
			foreach (string argStr in argStrings) {
				Match match = argRegex.Match(argStr.Trim());

				if (match.Groups["flag"].Success) {
					string flag = match.Groups["flag"].Value;
					bool flagVal = true;
					if (flag.StartsWith("!")) {
						flagVal = false;
						flag = flag.Substring(1);
					}
					if (flags.ContainsKey(flag)) {
						throw new FormatException("Repeated argument in dictionary.");
					}
					flags.Add(flag, flagVal);
				}
				else if (match.Groups["property"].Success) {
					string property = match.Groups["property"].Value;
					string argVal = match.Groups["value"].Value.Replace("\\,", ",");
					if (properties.ContainsKey(property)) {
						throw new FormatException("Repeated argument in dictionary.");
					}
					properties.Add(property, argVal);
				}
				else {
					throw new FormatException("Badly formatted argument dictionary.");
				}
			}

			object?[] parameters = new object?[parameterList.Length];
			for (int i = 0; i < parameterList.Length; i++) {
				ParameterInfo parameter = parameterList[i];
				if(parameter.Name is null) { continue; }
				string parameterName = parameter.Name;
				Type parameterType = parameterList[i].ParameterType;

				parameters[i] = parameter.HasDefaultValue ? parameter.DefaultValue : Type.Missing; // Default value

				if (parameterType == typeof(bool) && flags.TryGetValue(parameterName, out bool flagVal)) {
					parameters[i] = flagVal;
				}
				else if (properties.TryGetValue(parameterName, out string? parameterStr)) {
					parameters[i] = ValueParsing.Parse(parameterStr, parameterType, source);
				}
				else if (!parameter.IsOptional) {
					throw new FormatException($"No value for required dictionary parameter \"{parameterName}\" ({parameterType.Name}) provided.");
				}
			}

			try {
				return constructor.Invoke(parameters);
			}
			catch (TargetInvocationException e) {
				throw new InvalidOperationException($"Error found while constructing {constructor.DeclaringType?.Name ?? "UNKNOWN"} from dictionary.", e);
			}
		}

		public static readonly string SourceKeyword = "&SOURCE";
		private static readonly Regex sourceRegex = new Regex(Regex.Escape(SourceKeyword));
		private static string PathSubSource(string path, DirectoryPath source) {
			return sourceRegex.Replace(path, source.Path, 1);
		}

		#region Array or Tuple Parsing

		private static readonly int arrayMaxRank = 3;
		private static readonly char[] arrayDelimiters = new char[] { ',', ';', '|' };
		private static readonly string[] arraySeparators = new string[] { ", ", "; ", " | " };

		private static int GetArrayOrTupleRank(Type type) {
			if (type.IsArray && type.GetElementType() is Type elementType) {
				//Type elementType = type.GetElementType();
				return 1 + GetArrayOrTupleRank(elementType);
			}
			else if (TupleUtils.IsTupleType(type)) {
				return 1 + type.GetGenericArguments().Select(GetArrayOrTupleRank).Max();
			}
			else {
				return 0;
			}
		}

		private static object? ParseArrayOrTuple(string value, Type type, DirectoryPath source) {
			int rank = GetArrayOrTupleRank(type);
			if (rank > arrayMaxRank) {
				throw new InvalidOperationException($"Cannot parse arrays with rank higher than {arrayMaxRank}.");
			}
			return ParseArrayOrTupleValue(value, type, rank, source);
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		private static object? ParseArrayOrTupleValue(string value, Type type, int rank, DirectoryPath source) {
			if (rank == 0) {
				return Parse(value, type, source);
			}
			else {
				char delim = arrayDelimiters[rank - 1];

				if (type.IsArray && type.GetElementType() is Type elementType) {
					//Type elementType = type.GetElementType();

					return SplitArrayOn(value, delim)
						.Select(v => ParseArrayOrTupleValue(v, elementType, rank - 1, source))
						.ToArray(elementType);
				}
				else if (TupleUtils.IsTupleType(type)) {
					Type[] itemTypes = type.GetGenericArguments();

					object?[] items = new object?[itemTypes.Length];

					string[] values = SplitArrayOn(value, delim);

					if (values.Length != itemTypes.Length) {
						throw new FormatException($"Incorrect number of values provided for tuple (expected {itemTypes.Length}, got {values.Length}).");
					}

					for (int i = 0; i < itemTypes.Length; i++) {
						items[i] = ParseArrayOrTupleValue(values[i], itemTypes[i], GetArrayOrTupleRank(itemTypes[i]), source);
					}

					return Activator.CreateInstance(type, items);
				}

				throw new FormatException("Invalid array or tuple type/rank.");
			}
		}

		private static string[] SplitArrayOn(string text, char delimiter) {
			return StringParsing.SplitOnUnescaped(text, delimiter).Select(s => s.Trim()).ToArray();
		}

		private static string ArrayToString(object value) => ArrayToString(value, out _);
		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="FormatException"></exception>
		private static string ArrayToString(object? value, out int rank) {
			if(value is null) {
				throw new ArgumentNullException(nameof(value), "Cannot convert null values to strings.");
			}
			else if (value is Array array) {
				List<string> parts = new List<string>();
				rank = 0;
				foreach (object i in array.Cast<object>()) {
					parts.Add(ArrayToString(i, out int iRank));
					rank = Math.Max(rank, iRank);
				}
				rank += 1;
				if (rank > arrayMaxRank) {
					throw new InvalidOperationException($"Cannot process arrays with a rank above {arrayMaxRank}."); // TODO Better exception type?
				}
				return string.Join(arraySeparators[rank - 1], parts);
			}
			else if (TupleUtils.IsTupleType(value.GetType())) {
				List<string> parts = new List<string>();
				rank = 0;
				foreach (object? i in value.GetType().GetFields().Select(f => f.GetValue(value))) {
					parts.Add(ArrayToString(i, out int iRank));
					rank = Math.Max(rank, iRank);
				}
				rank += 1;
				if (rank > arrayMaxRank) {
					throw new InvalidOperationException($"Cannot process arrays with a rank above {arrayMaxRank}."); // TODO Better exception type?
				}
				return string.Join(arraySeparators[rank - 1], parts);
			}
			else {
				rank = 0;
				return ToString(value);
			}
		}

		/*
		private static Array ParseArray(string value, Type type) {
			Type elementType = type.GetElementType();
			if (elementType.IsArray) {
				Type innerElementType = elementType.GetElementType();
				if (innerElementType.IsArray) {
					Type innerInnerElementType = innerElementType.GetElementType();
					if (innerInnerElementType.IsArray) {
						throw new NotImplementedException("Cannot parse arrays with rank higher than 3.");
					}
					else {
						return SplitArrayRank3(value)
							.Select(outer => outer.Select(inner => inner.Select(v => Parse(v, innerInnerElementType)).ToArray(innerInnerElementType)).ToArray(innerElementType))
							.ToArray(elementType);
					}
				}
				else {
					return SplitArrayRank2(value) //.SelectFrom(v => Parse(v, innerElementType)); // Cannot do this, because we need to specify array type for object
						.Select(sub => sub.Select(v => Parse(v, innerElementType)).ToArray(innerElementType))
						.ToArray(elementType);
				}
			}
			else {
				return SplitArrayRank1(value)
					.SelectFrom(v => Parse(v, elementType))
					.ToArray(elementType);
			}
		}
		*/

		/*
		private static string[] SplitArrayRank1(string text) {
			//return text.SplitAndTrim(',').Select(UnescapeArrayString).ToArray();
			//return StringParsing.SplitOnUnescaped(text, ',').Select(s => s.Trim()).ToArray();
			return SplitArrayOn(text, ',');
		}

		private static string[][] SplitArrayRank2(string text) {
			//return text.SplitAndTrim(';').Select(p => p.SplitAndTrim(',').Select(UnescapeArrayString).ToArray()).ToArray();
			//return StringParsing.SplitOnUnescaped(text, ';').Select(s => SplitArrayRank1(s.Trim())).ToArray();
			return SplitArrayOn(text, ';').Select(SplitArrayRank1).ToArray();
		}

		private static string[][][] SplitArrayRank3(string text) {
			//return text.SplitAndTrim(';').Select(p => p.SplitAndTrim(',').Select(UnescapeArrayString).ToArray()).ToArray();
			//return StringParsing.SplitOnUnescaped(text, '|').Select(s => SplitArrayRank2(s.Trim())).ToArray();
			return SplitArrayOn(text, '|').Select(SplitArrayRank2).ToArray();
		}

		private static object ParseTuple(string value, Type type) {
			Type[] itemTypes = type.GetGenericArguments();
			
			object[] items = new object[itemTypes.Length];

			string[] values = SplitArrayRank1(value);

			if(values.Length != itemTypes.Length) {
				throw new FormatException($"Incorrect number of values provided to tuple (expected {itemTypes.Length}, got {values.Length}).");
			}

			for(int i=0; i<itemTypes.Length; i++) {
				items[i] = Parse(values[i], itemTypes[i]);
			}

			return Activator.CreateInstance(type, items);
		}
		*/

		#endregion

	}

}
