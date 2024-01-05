using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

		/// <summary>
		/// Convert an <see cref="Array"/> object into a <see cref="List{T}"/> of the appropriate type.
		/// If the 
		/// </summary>
		/// <param name="array">The array to convert.</param>
		/// <param name="listType">The generic <see cref="List{T}"/> type that should be produced.</param>
		/// <returns>A list object containing all the elements of the array.</returns>
		/// <exception cref="ArgumentException">If the provided <paramref name="listType"/> is not an example of <see cref="List{T}"/>.</exception>
		/// <exception cref="InvalidOperationException">Thrown when the provided <paramref name="listType"/> cannot be properly instantiated.</exception>
		public static IList ConvertArrayObjectToList(Array array, Type listType) {
            if (listType.TryGetGenericTypeDefinition() != typeof(List<>)) {
                throw new ArgumentException("Invalid list type provided.");
            }


			Type listElementType;
			IList? entries;

            try {
                listElementType = listType.GetGenericArguments().Single();
                entries = (IList?)Activator.CreateInstance(listType);
            }
            catch(SystemException e) {
				throw new InvalidOperationException($"Could not instantiate list object of the provided type: {listType}", e);
			}
            catch(TargetInvocationException e) {
				throw new InvalidOperationException($"Could not instantiate list object of the provided type: {listType}", e);
			}

            if(entries is null) {
                throw new InvalidOperationException($"Could not instantiate list object of the provided type: {listType}");
            }

            try {
                foreach (object entry in array) {
                    if (entry == null || listElementType.IsAssignableFrom(entry.GetType())) {
                        entries.Add(entry);
                    }
                }
            }
            catch(NotSupportedException e) {
				throw new InvalidOperationException("Could not populate list.", e);
			}

            return entries;
        }

    }

}
