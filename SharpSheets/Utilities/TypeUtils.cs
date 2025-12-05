using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace SharpSheets.Utilities {
	public static class TypeUtils {

		public static Type? TryGetGenericTypeDefinition(this Type type) {
			try {
				return type.IsGenericType ? type.GetGenericTypeDefinition() : null;
			}
			catch (SystemException) {
				return null;
			}
		}

		public static bool TryGetGenericTypeDefinition(this Type type, [MaybeNullWhen(false)] out Type genericType) {
			try {
				if (type.IsGenericType) {
					genericType = type.GetGenericTypeDefinition();
					return true;
				}
			}
			catch (SystemException) { }

			genericType = null;
			return false;
		}

		public static bool IsListType(this Type type, [MaybeNullWhen(false)] out Type elementType) {
			try {
				if (type.TryGetGenericTypeDefinition() is Type genericListType && genericListType == typeof(List<>)) {
					elementType = type.GetGenericArguments().Single();
					return true;
				}
			}
			catch (NotSupportedException) { } // This is not a well-constructed type object, so cannot be a List<>
			catch(InvalidOperationException) { } // Single() failed, and therefore this cannot be a List<>

			elementType = null;
			return false;
		}

		/// <summary>
		/// Determines whether the current type can be assigned to a variable of the specified type.
		/// </summary>
		/// <param name="type"> The current type, which is to be assigned to the specified type (i.e. proposed sub-type). </param>
		/// <param name="other"> The specified type, which is to be assigned the current type (i.e. proposed super-type). </param>
		/// <returns></returns>
		public static bool IsAssignableTo(this Type type, Type other) {
			return other.IsAssignableFrom(type);
		}

		/// <summary>
		/// Find and return the underlying type, if the current type is Nullable, otherwise return the current type.
		/// </summary>
		/// <param name="type">Current type to check for Nullable underlying type.</param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static Type GetUnderlyingType(this Type type) {
			if (type.TryGetGenericTypeDefinition() == typeof(Nullable<>)) {
				return Nullable.GetUnderlyingType(type) ?? throw new InvalidOperationException("Could not resolve nullable type.");
			}
			else {
				return type;
			}
		}

	}
}
