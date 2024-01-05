using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPDF.Utilities {

	internal static class Extensions {

		public static V? GetValueOrFallback<K, V>(this IReadOnlyDictionary<K, V> dict, K key, V? defaultValue) {
			if (dict.TryGetValue(key, out V? value)) {
				return value;
			}
			else {
				return defaultValue;
			}
		}

	}

}
