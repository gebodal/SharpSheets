using System;

namespace SharpSheets.Cards.Card {

	public class ArrangementCollection<T> where T : class {

		public int TotalCount { get; }
		private readonly T fallback;

		private readonly T?[][] entries;

		public ArrangementCollection(int totalCount, T fallback) {
			this.TotalCount = totalCount;
			this.fallback = fallback;

			this.entries = new T[this.TotalCount][];

			for (int i = 1; i <= this.TotalCount; i++) {
				this.entries[i - 1] = new T[i];

				for (int j = 0; j < i; j++) {
					this.entries[i - 1][j] = null;
				}
			}
		}

		public T this[int index, int count] {
			get {
				return GetValue(index, count);
			}
			set {
				Set(value, index, count);
			}
		}

		public void Set(T entry, int index, int count) {
			entries[count - 1][index] = entry;
		}

		public T GetValue(int index, int count) {
			T? entry = entries[count - 1][index];
			if (entry != null) {
				return entry;
			}
			else {
				return fallback;
			}
		}

	}

}
