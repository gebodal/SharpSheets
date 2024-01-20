using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Utilities {
	public static class EnumerableUtils {

		/// <summary>
		/// Wraps this object instance into an IEnumerable&lt;T&gt;
		/// consisting of a single item.
		/// </summary>
		/// <typeparam name="T"> Type of the object. </typeparam>
		/// <param name="item"> The instance that will be wrapped. </param>
		/// <returns> An IEnumerable&lt;T&gt; consisting of a single item. </returns>
		public static IEnumerable<T> Yield<T>(this T item) {
			yield return item;
		}

		public static IEnumerable<T> Yield<T>(this T item, int repeat) {
			for(int i=0; i<repeat; i++) {
				yield return item;
			}
		}

		public static IEnumerable<T> Yield<T>(params T[] items) {
			for (int i = 0; i < items.Length; i++) {
				yield return items[i];
			}
		}

		public static IEnumerable<T> Flatten<T>(this T[,] array) {
			for (int i = 0; i < array.GetLength(0); i++) {
				for (int j = 0; j < array.GetLength(1); j++) {
					yield return array[i, j];
				}
			}
		}

		public static IEnumerable<T> Flatten<T>(this T[][] array) {
			for (int i = 0; i < array.Length; i++) {
				for (int j = 0; j < array[i].Length; j++) {
					yield return array[i][j];
				}
			}
		}

		public static IEnumerable<T> Flatten<T>(this T[][][] array) {
			for (int i = 0; i < array.Length; i++) {
				for (int j = 0; j < array[i].Length; j++) {
					for (int k = 0; k < array[i][j].Length; k++) {
						yield return array[i][j][k];
					}
				}
			}
		}

		public static IEnumerable<T> Repeat<T>(this IEnumerable<T> source, int count) {
			if (count < 1) {
				yield break;
			}

			List<T> entries = new List<T>();
			foreach (T entry in source) {
				yield return entry;
				entries.Add(entry);
			}

			for (int i = 1; i < count; i++) {
				for (int j = 0; j < entries.Count; j++) {
					yield return entries[j];
				}
			}
		}

		/*
		public static IEnumerable<ValueTuple<R, S>> Zip<R, S>(this IEnumerable<R> source, IEnumerable<S> other) {
			if (source is null || other is null) {
				throw new ArgumentNullException();
			}

			return source.Zip(other, (r, s) => new ValueTuple<R, S>(r, s));
		}
		*/

		/// <summary>
		/// Remove sequential repetitions from an enumerable.
		/// </summary>
		/// <typeparam name="T">The type of this enumerable.</typeparam>
		/// <param name="source">The <see cref="IEnumerable{T}"/> to query.</param>
		/// <param name="equality">A function to compare the equality of two items.</param>
		/// <returns></returns>
		public static IEnumerable<T> RemoveRepeats<T>(this IEnumerable<T> source, Func<T, T, bool> equality) {
			IEnumerator<T> iter = source.GetEnumerator();
			T? current = default;
			bool first = true;
			while (iter.MoveNext()) {
				if (first || !equality(current!, iter.Current)) {
					current = iter.Current;
					yield return current;
				}
				first = false;
			}
		}

		/// <summary>
		/// Remove sequential repetitions from an enumerable,
		/// using the default <see cref="EqualityComparer{T}"/> for the item type.
		/// </summary>
		/// <typeparam name="T">The type of this enumerable.</typeparam>
		/// <param name="source">The <see cref="IEnumerable{T}"/> to query.</param>
		/// <returns></returns>
		public static IEnumerable<T> RemoveRepeats<T>(this IEnumerable<T> source) {
			return source.RemoveRepeats(EqualityComparer<T>.Default.Equals);
		}

		/// <summary>
		/// Filters a sequence of strings, removing items that are null or <see cref="string.Empty"/>, or are comprised exclusively of white-space characters.
		/// </summary>
		/// <param name="source">An <see cref="IEnumerable{String}"/> to query.</param>
		/// <returns></returns>
		public static IEnumerable<string> WhereNotEmpty(this IEnumerable<string> source) {
			return source.Where(s => !string.IsNullOrWhiteSpace(s));
		}

		public static bool All(this IEnumerable<bool> source) {
			return source.All(b => b);
		}

		public static T FirstOrDefault<T>(this IEnumerable<T> source, Func<T, bool> predicate, T defaultValue) {
			try {
				return source.First(predicate);
			}
			catch (InvalidOperationException) { // This is probably quite slow. Any alternative?
				return defaultValue;
			}
		}

		public static IEnumerable<T> SkipLastN<T>(this IEnumerable<T> source, int n) {
			IEnumerator<T> it = source.GetEnumerator();
			Queue<T> cache = new Queue<T>(n + 1);

			bool hasRemainingItems;
			do {
				if (hasRemainingItems = it.MoveNext()) {
					cache.Enqueue(it.Current);
					if (cache.Count > n) { yield return cache.Dequeue(); }
				}
			} while (hasRemainingItems);
		}

		public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class {
			return source.Where(i => i != null).Select(i => i!);
		}

		public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : struct {
			return source.Where(i => i != null).Select(i => i!.Value);
		}

		public static IEnumerable<T> Concat<T>(params IEnumerable<T>[] sources) {
			for (int i = 0; i < sources.Length; i++) {
				foreach (T elem in sources[i]) {
					yield return elem;
				}
			}
		}

		public static IEnumerable<T> Concat<T>(this IEnumerable<T> source, params IEnumerable<T>[] additional) {
			foreach (T orig in source) {
				yield return orig;
			}
			for (int i = 0; i < additional.Length; i++) {
				foreach (T elem in additional[i]) {
					yield return elem;
				}
			}
		}

		public static IEnumerable<T> ConcatOrNothing<T>(this IEnumerable<T> source, IEnumerable<T>? other) {
			if(other is not null) {
				return source.Concat(other);
			}
			else {
				return source;
			}
		}

		/*
		public static IEnumerable<T> ConcatOrNothing<T>(this IEnumerable<T> source, params IEnumerable<T>?[] additional) {
			foreach (T orig in source) {
				yield return orig;
			}
			for (int i = 0; i < additional.Length; i++) {
				if (additional[i] is IEnumerable<T> other) {
					foreach (T elem in other) {
						yield return elem;
					}
				}
			}
		}
		*/

		public static IEnumerable<T> ConcatOrNothing<T>(params IEnumerable<T>?[] additional) {
			for (int i = 0; i < additional.Length; i++) {
				if (additional[i] is IEnumerable<T> other) {
					foreach (T elem in other) {
						yield return elem;
					}
				}
			}
		}

		/*
		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public static T MinBy<T, K>(this IEnumerable<T> source, Func<T, K> keySelector) where K : IComparable<K> {
			return source.MinBy(keySelector, Comparer<K>.Default);
		}

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public static T MinBy<T, K>(this IEnumerable<T> source, Func<T, K> keySelector, IComparer<K> comparer) {
			if (source == null) { throw new ArgumentNullException(nameof(source)); }
			T? min = default;
			K? minKey = default;
			bool first = true;
			foreach (T elem in source) {
				if (first) {
					min = elem;
					minKey = keySelector(elem);
					first = false;
				}
				else {
					K elemKey = keySelector(elem);
					if (comparer.Compare(minKey, elemKey) > 0) {
						min = elem;
						minKey = elemKey;
					}
				}
			}
			if (first) { throw new InvalidOperationException("IEnumerable is empty."); }
			return min!;
		}

		/// <summary></summary>
		/// /// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public static T MaxBy<T, K>(this IEnumerable<T> source, Func<T, K> keySelector) where K : IComparable<K> {
			return source.MaxBy(keySelector, Comparer<K>.Default);
		}

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public static T MaxBy<T, K>(this IEnumerable<T> source, Func<T, K> keySelector, IComparer<K> comparer) {
			if (source == null) { throw new ArgumentNullException(nameof(source)); }
			T? max = default;
			K? maxKey = default;
			bool first = true;
			foreach (T elem in source) {
				if (first) {
					max = elem;
					maxKey = keySelector(elem);
					first = false;
				}
				else {
					K elemKey = keySelector(elem);
					if (comparer.Compare(maxKey, elemKey) < 0) {
						max = elem;
						maxKey = elemKey;
					}
				}
			}
			if (first) { throw new InvalidOperationException("IEnumerable is empty."); }
			return max!;
		}
		*/

		public static T MaxOrFallback<T>(this IEnumerable<T> source, T defaultValue) {
			if (source.Any()) {
				return source.Max() ?? defaultValue;
			}
			else {
				return defaultValue;
			}
		}

		public static IEnumerable<(int Index, T Item)> Enumerate<T>(this IEnumerable<T> source) {
			int counter = 0;
			foreach (T elem in source) {
				yield return (counter, elem);
				counter++;
			}
		}

		public static IEnumerable<T> DistinctPreserveOrder<T>(this IEnumerable<T> source) {
			HashSet<T> seen = new HashSet<T>();
			foreach(T value in source) {
				if (!seen.Contains(value)) {
					seen.Add(value);
					yield return value;
				}
			}
		}

		public static IEnumerable<T> DistinctPreserveOrder<T>(this IEnumerable<T> source, IEqualityComparer<T> equalityComparer) {
			HashSet<T> seen = new HashSet<T>(equalityComparer);
			foreach (T value in source) {
				if (!seen.Contains(value)) {
					seen.Add(value);
					yield return value;
				}
			}
		}

	}
}
