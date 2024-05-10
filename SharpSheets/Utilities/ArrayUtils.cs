using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Utilities {
	public static class ArrayUtils {

		public static T[] MakeArray<T>(int n, T fill) {
			T[] array = new T[n];
			for (int i = 0; i < array.Length; i++) {
				array[i] = fill;
			}
			return array;
		}

		public static T[] MakeArray<T>(this T fill, int n) {
			return MakeArray(n, fill);
		}
		public static T[] MakeArray<T>(this T item) {
			return MakeArray(1, item);
		}

		public static S[] SelectFrom<T, S>(this T[] array, Func<T, S> selector) {
			return array.Select(selector).ToArray();
		}

		public static S[][] SelectFrom<T, S>(this T[][] array, Func<T, S> selector) {
			return array.Select(sub => sub.Select(selector).ToArray()).ToArray();
		}

		public static S[][][] SelectFrom<T, S>(this T[][][] array, Func<T, S> selector) {
			return array.Select(subarray => subarray.Select(subsub => subsub.Select(selector).ToArray()).ToArray()).ToArray();
		}

		public static IEnumerable<T> GetRow2D<T>(this T[,] array, int i) {
			for (int j = 0; j < array.GetLength(1); j++) {
				yield return array[i, j];
			}
		}
		public static IEnumerable<T> GetColumn2D<T>(this T[,] array, int j) {
			for (int i = 0; i < array.GetLength(0); i++) {
				yield return array[i, j];
			}
		}

		/// <summary></summary>
		/// <exception cref="NotSupportedException">Thrown when an array of the provided <paramref name="elementType"/> cannot be instantiated.</exception>
		/// <exception cref="InvalidCastException">Thrown when any entries in <paramref name="collection"/> cannot be cast to <paramref name="elementType"/>.</exception>
		public static Array ToArray(this IEnumerable collection, Type elementType) {
			IList values;
			if (collection is IList list) { // Most arrays etc. implement IList
				values = list;
			}
			else {
				values = new ArrayList();
				foreach (object nextMember in collection) {
#pragma warning disable GJT0001 // Unhandled thrown exception from statement
					values.Add(nextMember);
#pragma warning restore GJT0001 // Unhandled thrown exception from statement
				}
			}

			Array retval = Array.CreateInstance(elementType, values.Count);
			int idx = 0;
			foreach (object entry in collection) {
				if (elementType.IsAssignableFrom(entry.GetType())) {
					retval.SetValue(entry, idx);
				}
				else {
					throw new InvalidCastException($"Cannot cast object of type {(entry == null ? "null" : entry.GetType().Name)} to {elementType}.");
				}
				++idx;
			}
			return retval;
		}

		public static T[][] ToJaggedArray<T>(this T[,] twoDimensionalArray) {
			int rowsFirstIndex = twoDimensionalArray.GetLowerBound(0);
			int rowsLastIndex = twoDimensionalArray.GetUpperBound(0);
			int numberOfRows = rowsLastIndex + 1;

			int columnsFirstIndex = twoDimensionalArray.GetLowerBound(1);
			int columnsLastIndex = twoDimensionalArray.GetUpperBound(1);
			int numberOfColumns = columnsLastIndex + 1;

			T[][] jaggedArray = new T[numberOfRows][];
			for (int i = rowsFirstIndex; i <= rowsLastIndex; i++) {
				jaggedArray[i] = new T[numberOfColumns];

				for (int j = columnsFirstIndex; j <= columnsLastIndex; j++) {
					jaggedArray[i][j] = twoDimensionalArray[i, j];
				}
			}
			return jaggedArray;
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static T[,] To2DArray<T>(this T[][] source) {
			try {
				int firstDim = source.Length;
				int secondDim = source.GroupBy(row => row.Length).Single().Key; // throws InvalidOperationException if source is not rectangular

				T[,] result = new T[firstDim, secondDim];
				for (int i = 0; i < firstDim; ++i) {
					for (int j = 0; j < secondDim; ++j) {
						result[i, j] = source[i][j];
					}
				}
				return result;
			}
			catch (InvalidOperationException) {
				throw new InvalidOperationException("The given jagged array is not rectangular.");
			}
		}

		public static void Fill<T>(this T[,] array, T value) {
			for (int i = 0; i < array.GetLength(0); i++) {
				for (int j = 0; j < array.GetLength(1); j++) {
					array[i, j] = value;
				}
			}
		}

		public static (T[,], T[,]) Split<T>(this T[,] array, int atIndex) {
			if (atIndex >= array.GetLength(0)) {
				throw new IndexOutOfRangeException();
			}

			T[,] first = new T[atIndex, array.GetLength(1)];
			T[,] second = new T[array.GetLength(0) - atIndex, array.GetLength(1)];

			for (int i = 0; i < atIndex; i++) {
				for (int j = 0; j < array.GetLength(1); j++) {
					first[i, j]	= array[i, j];
				}
			}

			for (int i = atIndex; i < array.GetLength(0); i++) {
				for (int j = 0; j < array.GetLength(1); j++) {
					second[i - atIndex, j] = array[i, j];
				}
			}

			return (first, second);
		}

	}
}
