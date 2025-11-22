using SharpSheets.Utilities;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Parsing {

	public interface INumbered : IEnumerable<KeyValuePair<int, object?>> {
		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		void Add(int index, object? value);

		bool HasEntry(int index);
		object? MinEntry();

		int MinIndex();
		int MaxIndex();

		object? Get(int index);

		int NumEntries { get; }
		int Length { get; }
	}

	public class Numbered<T> : INumbered {

		private int maxIndex; // "int?" better?
		private readonly Dictionary<int, T?> entries;

		public Numbered() {
			maxIndex = -1;
			this.entries = new Dictionary<int, T?>();
		}

		public Numbered(IEnumerable<T> source) : this() {
			int index = 0;
			foreach (T value in source) {
				Add(index, value);
				index++;
			}
		}

		/// <summary></summary>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public void Add(int index, T? entry) {
			if (index < 0) {
				throw new ArgumentOutOfRangeException(nameof(index), "Indexes for Numbered entries must not be less than zero.");
			}
			entries.Add(index, entry);
			if (index > maxIndex) { maxIndex = index; }
		}

		void INumbered.Add(int index, object? value) {
			if (value is null) {
				Add(index, default);
			}
			else if (value is T entry) {
				Add(index, entry);
			}
			else {
				throw new ArgumentException($"Invalid value type of {value?.GetType().Name ?? "UNKNOWN"} for Numbered<{typeof(T).Name}>.");
			}
		}

		IEnumerator<KeyValuePair<int, object?>> IEnumerable<KeyValuePair<int, object?>>.GetEnumerator() => entries.OrderBy(kv => kv.Key).Select(kv => new KeyValuePair<int, object?>(kv.Key, kv.Value)).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this).GetEnumerator();

		public bool HasEntry(int index) {
			return entries.ContainsKey(index);
		}

		bool INumbered.HasEntry(int index) => HasEntry(index);

		/// <summary></summary>
		/// <exception cref="KeyNotFoundException"></exception>
		public T? this[int index] {
			get {
				if (entries.TryGetValue(index, out T? entry)) {
					return entry;
				}
				else {
					throw new KeyNotFoundException($"No entry in Numbered<{typeof(T).Name}> for entry {index}.");
				}
			}
		}

		object? INumbered.Get(int index) => this[index];

		public T? MinEntry() {
			return entries.OrderBy(kv => kv.Key).Select(kv => kv.Value).FirstOrDefault();
		}

		public int MinIndex() => entries.Keys.Min();
		public int MaxIndex() => entries.Keys.Max();

		object? INumbered.MinEntry() => MinEntry();

		public int NumEntries => entries.Count;
		//public int MaxIndex { get { return maxIndex; } }
		public int Length { get { return maxIndex + 1; } }
	}

	public static class NumberedUtils {

		public static bool IsNumbered(this Type type, [MaybeNullWhen(false)] out Type elementType) {
			if (type.TryGetGenericTypeDefinition() == typeof(Numbered<>)) {
				elementType = type.GetGenericArguments().Single();
				return true;
			}
			else {
				elementType = null;
				return false;
			}
		}

		public static IEnumerable<T?> TakeContinuous<T>(this Numbered<T> source, int n) {
			T? value = source.MinEntry();
			for (int i = 0; i < n; i++) {
				if (source.HasEntry(i)) {
					value = source[i];
				}
				yield return value;
			}
		}

		public static IEnumerable<object?> TakeContinuous(this INumbered source, int n) {
			object? value = source.MinEntry();
			for (int i = 0; i < n; i++) {
				if (source.HasEntry(i)) {
					value = source.Get(i);
				}
				yield return value;
			}
		}

	}

}
