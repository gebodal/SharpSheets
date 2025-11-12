using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Generators {

	public static class Helpers {

		public static void Enqueue<T>(this Queue<T> queue, IEnumerable<T> values) {
			foreach (T val in values) {
				queue.Enqueue(val);
			}
		}

		public static IEnumerable<(int, T)> Enumerate<T>(this IEnumerable<T> source) {
			int counter = 0;
			foreach (T entry in source) {
				yield return (counter, entry);
				counter++;
			}
		}

		public static IEnumerable<(TFirst, TSecond)> Zip<TFirst, TSecond>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second) {
			return first.Zip(second, (i, j) => (i, j));
		}

		public static IEnumerable<TResult> Zip<TFirst, TSecond, TThird, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, IEnumerable<TThird> third, Func<TFirst, TSecond, TThird, TResult> resultSelector) {
			return first.Zip(second.Zip<TSecond, TThird, (TSecond, TThird)>(third, (i, j) => (i, j)), (f, st) => resultSelector(f, st.Item1, st.Item2));
		}

	}

}
