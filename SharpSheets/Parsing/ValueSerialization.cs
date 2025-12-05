using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System.Text.RegularExpressions;
using SharpSheets.Canvas.Text;
using SharpSheets.Colors;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SharpSheets.Parsing {

	public static class ValueSerialization {

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
		/// <exception cref="InvalidOperationException">Thrown if <paramref name="value"/> is an array with rank greater than 3.</exception>
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
			else if (value is char charVal) {
				return charVal.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is RichString richString) {
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
			}
			else if (value is Margins margins) {
				return string.Format(CultureInfo.InvariantCulture, "{{top:{0}, right:{1}, bottom:{2}, left:{3}}}", margins.Top, margins.Right, margins.Bottom, margins.Left);
			}
			else if (value is Color color) {
				return color.ToHexString();
			}
			else if (value is PageSize pageSize) {
				return pageSize.ToString(CultureInfo.InvariantCulture);
			}
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

		private static string ArrayToString(object value) => ArrayToString(value, out _);
		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
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
				return string.Join(ValueParsers.GetArraySeparator(rank), parts);
			}
			else if (value is ITuple tupleValue) {
				List<string> parts = new List<string>();
				rank = 0;
				for (int i = 0; i < tupleValue.Length; i++) {
					parts.Add(ArrayToString(tupleValue[i], out int iRank));
					rank = Math.Max(rank, iRank);
				}
				rank += 1;
				return string.Join(ValueParsers.GetArraySeparator(rank), parts);
			}
			else {
				rank = 0;
				return ToString(value);
			}
		}

	}

}
