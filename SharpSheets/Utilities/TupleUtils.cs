using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

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

		public static bool IsTupleObject(object obj) {
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

		public static Type MakeGenericTupleType(params Type[] types) {
			if (types.Length == 0) {
				throw new ArgumentException("Empty list of types provided.");
			}
			else if (types.Length == 1) {
				return typeof(ValueTuple<>).MakeGenericType(types);
			}
			else if (types.Length == 2) {
				return typeof(ValueTuple<,>).MakeGenericType(types);
			}
			else if (types.Length == 3) {
				return typeof(ValueTuple<,,>).MakeGenericType(types);
			}
			else if (types.Length == 4) {
				return typeof(ValueTuple<,,,>).MakeGenericType(types);
			}
			else if (types.Length == 5) {
				return typeof(ValueTuple<,,,,>).MakeGenericType(types);
			}
			else if (types.Length == 6) {
				return typeof(ValueTuple<,,,,,>).MakeGenericType(types);
			}
			else if (types.Length == 7) {
				return typeof(ValueTuple<,,,,,,>).MakeGenericType(types);
			}
			else {
				Type[] genericArgs = new Type[8];
				Array.Copy(types, genericArgs, 7);

				Type[] remainingArgs = new Type[types.Length - 7];
				Array.Copy(types, 7, remainingArgs, 0, remainingArgs.Length);

				genericArgs[7] = MakeGenericTupleType(remainingArgs);

				return typeof(ValueTuple<,,,,,,,>).MakeGenericType(genericArgs);
			}
		}

		public static Type MakeGenericTupleType(Type type, int n) {
			return MakeGenericTupleType(type.Yield(n).ToArray());
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

		public static object CreateTuple(Type tupleType, object?[] values) {
			if (tupleType.TryGetGenericTypeDefinition() is Type genericType && ValueTupleGenericTypes.Contains(genericType)) {
				
				object?[] tupleValues;

				if (genericType == typeof(ValueTuple<,,,,,,,>) && tupleType.GenericTypeArguments[7] is Type tRest && IsTupleType(tRest)) {
					tupleValues = new object[8];
					Array.Copy(values, tupleValues, 7);

					object[] remainingValues = new object[values.Length - 7];
					Array.Copy(values, 7, remainingValues, 0, remainingValues.Length);

					tupleValues[7] = CreateTuple(tRest, remainingValues);
				}
				else {
					tupleValues = values;
				}

				ConstructorInfo? tupleConstructor = tupleType.GetConstructor(tupleType.GenericTypeArguments);

				if(tupleConstructor is null) {
					throw new InvalidOperationException("Could not get constructor for tuple type " + tupleType);
				}

				return tupleConstructor.Invoke(tupleValues);
			}

			throw new ArgumentException("Non-tuple type provided.");
		}

		private static readonly string[] fieldNames = new string[] { "Item1", "Item2", "Item3", "Item4", "Item5", "Item6", "Item7", "Rest" };
		public static IEnumerable<object> Iterate(object tupleObject) {
			Type tupleType = tupleObject.GetType();

			if (!IsTupleType(tupleType)) {
				throw new ArgumentException("Non-tuple object provided.");
			}

			int itemCount = tupleType.GenericTypeArguments.Length;

			for (int i = 0; i < itemCount; i++) {
				object value = GetFieldValue(tupleType, tupleObject, i); // tupleType.GetField(fieldNames[i])?.GetValue(tupleObject) ?? throw new ArgumentException($"Could not access field {i} of provided tuple value.");
				if (i == 7) {
					foreach (object nested in Iterate(value)) {
						yield return nested;
					}
				}
				else {
					yield return value;
				}
			}
		}

		public static object Index(object tupleObject, int index) {
			if(index < 0) {
				throw new IndexOutOfRangeException($"Invalid index {index}.");
			}

			Type tupleType = tupleObject.GetType();

			if (!IsTupleType(tupleType)) {
				throw new ArgumentException("Non-tuple object provided.");
			}


			if(index > 6) {
				object value = GetFieldValue(tupleType, tupleObject, 7); // tupleType.GetField(fieldNames[7])?.GetValue(tupleObject) ?? throw new ArgumentException($"Could not access field of provided tuple value.");
				return Index(value, index - 7);
			}
			else {
				int itemCount = tupleType.GenericTypeArguments.Length;
				if (index >= itemCount) {
					throw new IndexOutOfRangeException("Index out of range.");
				}
				return GetFieldValue(tupleType, tupleObject, index); // tupleType.GetField(fieldNames[index]).GetValue(tupleObject);
			}
		}

		private static object GetFieldValue(Type tupleType, object tupleObject, int index) {
			return tupleType.GetField(fieldNames[index])?.GetValue(tupleObject) ?? throw new InvalidOperationException($"Could not access field {index} of provided tuple value of type: {tupleType}");
		}

	}

}
