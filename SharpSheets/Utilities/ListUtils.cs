namespace SharpSheets.Utilities {

	public static class ListUtils {

		/// <summary></summary>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> or <paramref name="items"/> are null.</exception>
		/// <exception cref="NotSupportedException">Thrown when <paramref name="list"/> is a readonly <see cref="IList{T}"/>.</exception>
		public static void AddRange<T>(this IList<T> list, IEnumerable<T> items) {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (items == null) throw new ArgumentNullException(nameof(items));

            if (list is List<T> asList) {
                asList.AddRange(items);
            }
            else {
                foreach (T item in items) {
					list.Add(item);
                }
            }
        }

    }

}
