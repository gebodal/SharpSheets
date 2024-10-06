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

		public static Type[] GetInterfacesOrSelf(this Type type) {
			if (type.IsInterface) {
				return type.Yield().Concat(GetInterfacesRecursive(type)).Distinct().ToArray();
			}
			else {
				try {
					return type.GetInterfaces();
				}
				catch (TargetInvocationException) {
					return Array.Empty<Type>();
				}
			}
		}

		private static IEnumerable<Type> GetInterfacesRecursive(Type type) {
			Type[] interfaces;
			try {
				interfaces = type.GetInterfaces();
			}
			catch (TargetInvocationException) {
				interfaces = Array.Empty<Type>();
			}
			foreach(Type t in interfaces) {
				yield return t;
				foreach(Type nt in GetInterfacesRecursive(t)) {
					yield return nt;
				}
			}
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

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		public static bool IsGenericType(this Type type, Type genericType) {
			// TODO This needs checking to make sure it does what is intended

			if (!genericType.IsGenericType) {
				throw new ArgumentException("Provided type must be a raw generic type.");
			}

			if (genericType.IsInterface) {
				try {
					return type.GetInterfaces().Any(i => i.TryGetGenericTypeDefinition() == genericType);
				}
				catch (TargetInvocationException) {
					return false; // TODO Is this a good idea?
				}
			}
			else {
				return type.TryGetGenericTypeDefinition() == genericType;
			}
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		public static bool TryGetGenericArguments(this Type type, Type genericType, [MaybeNullWhen(false)] out Type[] genericArgs) {
			// TODO This needs checking to make sure it does what is intended

			if (!genericType.IsGenericType) {
				throw new ArgumentException("Provided type must be a raw generic type.");
			}

			Console.WriteLine($"TryGetGenericArguments({type}, {genericType})");

			Type? genericTypeDefinition = null;

			if (genericType.IsInterface) {
				try {
					Console.WriteLine("Try get interface type");
					genericTypeDefinition = type.GetInterfacesOrSelf().FirstOrDefault(i => i.TryGetGenericTypeDefinition() == genericType);
					Console.WriteLine($"Got interface type: {genericTypeDefinition}");
				}
				catch (TargetInvocationException) { }
			}
			else if (type.TryGetGenericTypeDefinition() is Type genericTypeDef) {
				genericTypeDefinition = genericTypeDef;
				Console.WriteLine($"Got generic type: {genericTypeDefinition}");
			}

			if (genericTypeDefinition != null) {
				genericArgs = genericTypeDefinition.GetGenericArguments();
				return true;
			}
			else {
				genericArgs = null;
				return false;
			}
		}

		public static IEnumerable<T> WhereGenericType<T>(this IEnumerable<T> source, Type genericType) where T : notnull {
			return source.Where(t => t.GetType().IsGenericType(genericType));
		}

		public static Type? GetTypeByName(string name, StringComparison comparison) {
			try {
				return AppDomain.CurrentDomain.GetAssemblies()
						.Reverse()
						.SelectMany(assembly => assembly.GetTypes())
						.FirstOrDefault(t => string.Equals(name, t.Name, comparison));
			}
			catch(AppDomainUnloadedException) {
				return null; // TODO Is this a good idea...?
			}
		}

	}
}
