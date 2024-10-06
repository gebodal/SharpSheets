using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Utilities {
	public static class DictionaryUtils {

		public static V? GetValueOrFallback<K, V>(this IReadOnlyDictionary<K, V> dict, K key, V? defaultValue) where K : notnull {
			if (dict.TryGetValue(key, out V? value)) {
				return value;
			}
			else {
				return defaultValue;
			}
		}

		/// <summary></summary>
		/// <exception cref="KeyNotFoundException"></exception>
		public static V TryKeys<K, V>(this IDictionary<K, V> source, IEnumerable<K> keys) where K : notnull {
			foreach (K key in keys) {
				if (source.TryGetValue(key, out V? value)) {
					return value;
				}
			}
			throw new KeyNotFoundException("None of the provided keys exist in the collection.");
		}

		public static V TryKeysOrDefault<K, V>(this IDictionary<K, V> source, IEnumerable<K> keys, V defaultValue) where K : notnull {
			foreach (K key in keys) {
				if (source.TryGetValue(key, out V? value)) {
					return value;
				}
			}
			return defaultValue;
		}

		public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<KeyValuePair<K, V>> source) where K : notnull {
			return source.ToDictionary(kv => kv.Key, kv => kv.Value);
		}

		public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<KeyValuePair<K, V>> source, IEqualityComparer<K> comparer) where K : notnull {
			return source.ToDictionary(kv => kv.Key, kv => kv.Value, comparer);
		}

		public static Dictionary<K, V> ToDictionaryAllowRepeats<E, K, V>(this IEnumerable<E> source, Func<E, K> keySelector, Func<E, V> valueSelector, bool overwrite) where K : notnull {
			Dictionary<K, V> result = new Dictionary<K, V>();
			foreach(E item in source) {
				K key = keySelector(item);
				if (overwrite || !result.ContainsKey(key)) {
					V value = valueSelector(item);
					result[key] = value;
				}
			}
			return result;
		}

		public static Dictionary<K, V> ToDictionaryAllowRepeats<E, K, V>(this IEnumerable<E> source, Func<E, K> keySelector, Func<E, V> valueSelector, IEqualityComparer<K> comparer, bool overwrite) where K : notnull {
			Dictionary<K, V> result = new Dictionary<K, V>(comparer);
			foreach (E item in source) {
				K key = keySelector(item);
				if (overwrite || !result.ContainsKey(key)) {
					V value = valueSelector(item);
					result[key] = value;
				}
			}
			return result;
		}

		public static Dictionary<K, E> ToDictionaryAllowRepeats<E, K>(this IEnumerable<E> source, Func<E, K> keySelector, bool overwrite) where K : notnull {
			Dictionary<K, E> result = new Dictionary<K, E>();
			foreach (E item in source) {
				K key = keySelector(item);
				if (overwrite || !result.ContainsKey(key)) {
					result[key] = item;
				}
			}
			return result;
		}

		public static Dictionary<K, E> ToDictionaryAllowRepeats<E, K>(this IEnumerable<E> source, Func<E, K> keySelector, IEqualityComparer<K> comparer, bool overwrite) where K : notnull {
			Dictionary<K, E> result = new Dictionary<K, E>(comparer);
			foreach (E item in source) {
				K key = keySelector(item);
				if (overwrite || !result.ContainsKey(key)) {
					result[key] = item;
				}
			}
			return result;
		}

		public static Dictionary<K, V> ToDictionaryAllowRepeats<K, V>(this IEnumerable<KeyValuePair<K, V>> source, bool overwrite) where K : notnull {
			return source.ToDictionaryAllowRepeats(kv => kv.Key, kv => kv.Value, overwrite);
		}

		public static Dictionary<K, V> ToDictionaryAllowRepeats<K, V>(this IEnumerable<KeyValuePair<K, V>> source, IEqualityComparer<K> comparer, bool overwrite) where K : notnull {
			return source.ToDictionaryAllowRepeats(kv => kv.Key, kv => kv.Value, comparer, overwrite);
		}

		public static string GetString<K, V>(this IDictionary<K, V> dict) where K : notnull {
			return "{" + string.Join(", ", dict.Select(kv => kv.Key.ToString() + ": " + kv.Value?.ToString())) + "}";
		}

		public static IEnumerable<K> GetKeys<K, V>(this IEnumerable<KeyValuePair<K, V>> source) {
			return source.Select(kv => kv.Key);
		}
		public static IEnumerable<V> GetValues<K, V>(this IEnumerable<KeyValuePair<K, V>> source) {
			return source.Select(kv => kv.Value);
		}

	}
}
