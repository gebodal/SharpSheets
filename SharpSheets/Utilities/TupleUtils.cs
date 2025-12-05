using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SharpSheets.Utilities {

	public class TupleUtils {

		private static readonly HashSet<Type> ValueTupleGenericTypes = new HashSet<Type> {
				typeof(ValueTuple<>), typeof(ValueTuple<,>),
				typeof(ValueTuple<,,>), typeof(ValueTuple<,,,>),
				typeof(ValueTuple<,,,,>), typeof(ValueTuple<,,,,,>),
				typeof(ValueTuple<,,,,,,>), typeof(ValueTuple<,,,,,,,>)
			};

		public static bool IsTupleType(Type type) {
			return type.TryGetGenericTypeDefinition() is Type genericType && ValueTupleGenericTypes.Contains(genericType);
		}

		public static bool IsTupleObject(object? obj) {
			return obj is null ? false : IsTupleType(obj.GetType());
		}

		public static bool IsTupleObject(object? obj, [MaybeNullWhen(false)] out Type tupleType) {
			if(obj is null) {
				tupleType = null;
				return false;
			}

			Type objType = obj.GetType();

			if (IsTupleType(objType)) {
				tupleType = objType;
				return true;
			}
			else {
				tupleType = null;
				return false;
			}
		}

		public static int GetTupleLength(Type type) {
			if (type.TryGetGenericTypeDefinition() is Type genericType) {
				if (genericType == typeof(ValueTuple<>)) {
					return 1;
				}
				else if (genericType == typeof(ValueTuple<,>)) {
					return 2;
				}
				else if (genericType == typeof(ValueTuple<,,>)) {
					return 3;
				}
				else if (genericType == typeof(ValueTuple<,,,>)) {
					return 4;
				}
				else if (genericType == typeof(ValueTuple<,,,,>)) {
					return 5;
				}
				else if (genericType == typeof(ValueTuple<,,,,,>)) {
					return 6;
				}
				else if (genericType == typeof(ValueTuple<,,,,,,>)) {
					return 7;
				}
				else if (genericType == typeof(ValueTuple<,,,,,,,>)) {
					if (IsTupleType(type.GenericTypeArguments[7])) {
						return 7 + GetTupleLength(type.GenericTypeArguments[7]);
					}
					else {
						return 8;
					}
				}
			}

			throw new ArgumentException("Provided type is not a recognized tuple type.");
		}

		public static int GetTupleLength(object tupleObject) {
			return GetTupleLength(tupleObject.GetType());
		}

		public static Type[] GetTupleTypes(Type tupleType) {
			if (!IsTupleType(tupleType)) {
				throw new ArgumentException("Non-tuple object provided.");
			}

			Type[] valueTypes = tupleType.GenericTypeArguments;

			if(valueTypes.Length == 8 && tupleType.GenericTypeArguments[7] is Type tRest && IsTupleType(tRest)) {
				List<Type> allTypes = new List<Type>();
				for(int i=0; i<7; i++) { allTypes.Add(valueTypes[i]); }
				allTypes.AddRange(GetTupleTypes(tRest));
				return allTypes.ToArray();
			}
			else {
				return valueTypes;
			}
		}

		public static IEnumerable<object?> Iterate(ITuple tupleObject) {
			int itemCount = tupleObject.Length;

			for (int i = 0; i < itemCount; i++) {
				object? value = tupleObject[i];
				if (i == 7 && value is ITuple nextedTuple) {
					foreach (object? nestedValue in Iterate(nextedTuple)) {
						yield return nestedValue;
					}
				}
				else {
					yield return value;
				}
			}
		}

		public static object? Index(ITuple tupleObject, int index) {
			if (index < 0) {
				throw new IndexOutOfRangeException($"Invalid index {index}.");
			}

			if (index > 6 && tupleObject[7] is ITuple nestedTuple) {
				return Index(nestedTuple, index - 7);
			}
			else {
				int itemCount = tupleObject.Length;
				if (index >= itemCount) {
					throw new IndexOutOfRangeException("Index out of range.");
				}
				return tupleObject[index];
			}
		}

	}

}
