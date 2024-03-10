using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Utilities {

	internal static class Extensions {

		public static V? GetValueOrFallback<K, V>(this IReadOnlyDictionary<K, V> dict, K key, V? defaultValue) {
			if (dict.TryGetValue(key, out V? value)) {
				return value;
			}
			else {
				return defaultValue;
			}
		}

		public static void InsertCount<T>(this IList<T> list, int index, T value, int count) {
			for (int i = 0; i < count; i++) {
				list.Insert(index, value);
			}
		}

	}

}
