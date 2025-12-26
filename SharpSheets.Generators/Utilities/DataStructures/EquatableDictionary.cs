using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SharpSheets.Generators.Utilities.DataStructures {

	public readonly struct EquatableDictionary<K, V> : IEquatable<EquatableDictionary<K, V>>, IImmutableDictionary<K, V> where K : IEquatable<K> where V : IEquatable<V> {

		private readonly ImmutableSortedDictionary<K,V>? _dict;

		public EquatableDictionary(IDictionary<K, V> dict) {
			ImmutableSortedDictionary<K, V>.Builder b = ImmutableSortedDictionary.CreateBuilder<K, V>();
			b.AddRange(dict);
			_dict = b.ToImmutable();
		}

		public int Count => _dict?.Count ?? 0;

		public V this[K key] {
			get {
				return _dict![key];
			}
		}

		public static bool operator ==(EquatableDictionary<K, V> left, EquatableDictionary<K, V> right) {
			return left.Equals(right);
		}

		public static bool operator !=(EquatableDictionary<K, V> left, EquatableDictionary<K, V> right) {
			return !left.Equals(right);
		}

		public bool Equals(EquatableDictionary<K, V> other) {
			if (_dict is null || other._dict == null) return _dict is null && other._dict is null;
			if(_dict.Count != other._dict.Count) return false;

			return _dict.SequenceEqual(other._dict);
		}

		public override bool Equals(object? obj) {
			return obj is EquatableDictionary<K, V> dict && Equals(dict);
		}

		public override int GetHashCode() {
			if (_dict is null)
				return 0;

			unchecked {
				int hash = 17;
				foreach (KeyValuePair<K, V> item in _dict) {
					hash = hash * 31 + (item.Key?.GetHashCode() ?? 0);
					hash = hash * 31 + (item.Value?.GetHashCode() ?? 0);
				}
				return hash;
			}
		}

		public IEnumerable<K> Keys => _dict?.Keys ?? Enumerable.Empty<K>();
		public IEnumerable<V> Values => _dict?.Values ?? Enumerable.Empty<V>();

		public IImmutableDictionary<K, V> Clear() {
			return ((IImmutableDictionary<K, V>?)_dict)?.Clear()!;
		}

		public IImmutableDictionary<K, V> Add(K key, V value) {
			return ((IImmutableDictionary<K, V>?)_dict)?.Add(key, value)!;
		}

		public IImmutableDictionary<K, V> AddRange(IEnumerable<KeyValuePair<K, V>> pairs) {
			return ((IImmutableDictionary<K, V>?)_dict)?.AddRange(pairs)!;
		}

		public IImmutableDictionary<K, V> SetItem(K key, V value) {
			return ((IImmutableDictionary<K, V>?)_dict)?.SetItem(key, value)!;
		}

		public IImmutableDictionary<K, V> SetItems(IEnumerable<KeyValuePair<K, V>> items) {
			return ((IImmutableDictionary<K, V>?)_dict)?.SetItems(items)!;
		}

		public IImmutableDictionary<K, V> RemoveRange(IEnumerable<K> keys) {
			return ((IImmutableDictionary<K, V>?)_dict)?.RemoveRange(keys)!;
		}

		public IImmutableDictionary<K, V> Remove(K key) {
			return ((IImmutableDictionary<K, V>?)_dict)?.Remove(key)!;
		}

		public bool Contains(KeyValuePair<K, V> pair) {
			return _dict?.Contains(pair) ?? false;
		}

		public bool TryGetKey(K equalKey, out K actualKey) {
			actualKey = default!;
			return _dict?.TryGetKey(equalKey, out actualKey) ?? false;
		}

		public bool ContainsKey(K key) {
			return _dict?.ContainsKey(key) ?? false;
		}

		public bool TryGetValue(K key, out V value) {
			value = default!;
			return _dict?.TryGetValue(key, out value!) ?? false;
		}

		public IEnumerator<KeyValuePair<K, V>> GetEnumerator() {
			return ((IEnumerable<KeyValuePair<K, V>>?)_dict)?.GetEnumerator() ?? Enumerable.Empty<KeyValuePair<K, V>>().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return ((IEnumerable?)_dict)?.GetEnumerator() ?? Enumerable.Empty<KeyValuePair<K, V>>().GetEnumerator();
		}
	}

}
