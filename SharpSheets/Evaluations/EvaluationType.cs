using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SharpSheets.Colors;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Evaluations {

	public class TypeField {
		
		public EvaluationName Name { get; }
		public EvaluationType Type { get; }
		private readonly Func<object, object?> evaluator;

		public TypeField(EvaluationName name, EvaluationType type, Func<object, object?> evaluator) {
			Name = name;
			Type = type;
			this.evaluator = evaluator;
		}

		public object? GetValue(object obj) {
			return evaluator(obj);
		}

	}

	public class EvaluationType {

		public string Name {
			get {
				return baseName + GetArrayBrackets();
			}
		}
		private readonly string baseName;

		private string GetArrayBrackets() {
			return ElementType == null ? "" : ((ElementCount.HasValue ? $"[{ElementCount.Value}]" : "[]") + ElementType.GetArrayBrackets());
		}

		//public Type SystemType { get; }
		public Type DataType { get; }
		public Type DisplayType { get; }

		private readonly Dictionary<EvaluationName, TypeField> fields;
		public IEnumerable<EvaluationName> FieldNames { get { return fields.Keys; } }
		public IEnumerable<TypeField> Fields { get { return fields.Values; } }

		public EvaluationType? ElementType { get; }
		public int? ElementCount { get; } // TODO Is this fully integrated now? Does the Evaluations namespace contain functions that need to respond to this?
		[MemberNotNullWhen(true, nameof(ElementType))]
		public bool IsArray { get { return ElementType != null && !ElementCount.HasValue; } }
		[MemberNotNullWhen(true, nameof(ElementType), nameof(ElementCount))]
		public bool IsTuple { get { return ElementType != null && ElementCount.HasValue; } }

		public int Rank { get { return ElementType != null ? 1 + ElementType.Rank : 0; } }

		private readonly HashSet<string>? enumValues = null;
		public IReadOnlyCollection<string>? EnumNames => enumValues;
		[MemberNotNullWhen(true, nameof(EnumNames))]
		public bool IsEnum { get { return enumValues != null; } }

		public static readonly EvaluationType INT;
		public static readonly EvaluationType UINT;
		public static readonly EvaluationType FLOAT;
		public static readonly EvaluationType UFLOAT;
		public static readonly EvaluationType BOOL;
		public static readonly EvaluationType COLOR;
		public static readonly EvaluationType STRING;

		private static readonly Dictionary<Type, EvaluationType> systemTypeRegistry;

		static EvaluationType() {
			INT = new EvaluationType("int", Array.Empty<TypeField>(), typeof(int));
			UINT = new EvaluationType("uint", Array.Empty<TypeField>(), typeof(uint));
			FLOAT = new EvaluationType("float", Array.Empty<TypeField>(), typeof(float));
			UFLOAT = new EvaluationType("ufloat", Array.Empty<TypeField>(), typeof(UFloat));
			BOOL = new EvaluationType("bool", Array.Empty<TypeField>(), typeof(bool));
			COLOR = new EvaluationType("color", Array.Empty<TypeField>(), typeof(Color));
			STRING = new EvaluationType("string", new TypeField[] { new TypeField("length", INT, obj => ((string)obj).Length) }, typeof(string));

			systemTypeRegistry = new Dictionary<Type, EvaluationType>() {
				{ typeof(int), INT },
				{ typeof(uint), UINT },
				{ typeof(float), FLOAT },
				{ typeof(UFloat), UFLOAT },
				{ typeof(bool), BOOL },
				{ typeof(Color), COLOR },
				{ typeof(string), STRING }
			};
		}

		private EvaluationType(string name, IEnumerable<TypeField> fields, Type systemType) {
			this.baseName = name;
			this.fields = fields.ToDictionary(f => f.Name);
			this.DataType = systemType;
			this.DisplayType = systemType;
			this.ElementType = null;
			this.ElementCount = null;
			this.enumValues = null;
		}

		private EvaluationType(EvaluationType elementType, IEnumerable<TypeField>? fields) {
			this.baseName = elementType.baseName;
			this.fields = (fields ?? Enumerable.Empty<TypeField>())
				.Append(new TypeField("length", this, obj => ((Array)obj).Length))
				.ToDictionary(f => f.Name);
			this.DataType = elementType.DataType.MakeArrayType(1);
			this.DisplayType = elementType.DisplayType.MakeArrayType(1);
			this.ElementType = elementType;
			this.ElementCount = null;
			this.enumValues = null;
		}

		private EvaluationType(EvaluationType elementType, int elementCount, IEnumerable<TypeField>? fields) {
			this.baseName = elementType.baseName;
			this.fields = (fields ?? Enumerable.Empty<TypeField>())
				.Append(new TypeField("length", this, obj => elementCount))
				.ToDictionary(f => f.Name);
			this.DataType = TupleUtils.MakeGenericTupleType(elementType.DataType, elementCount);
			this.DisplayType = TupleUtils.MakeGenericTupleType(elementType.DisplayType, elementCount);
			this.ElementType = elementType;
			this.ElementCount = elementCount;
			this.enumValues = null;
		}

		private EvaluationType(string name, IEnumerable<string> enumValues, Type systemType) {
			this.baseName = name;
			this.fields = new Dictionary<EvaluationName, TypeField>();
			this.DataType = typeof(string);
			this.DisplayType = systemType;
			this.ElementType = null;
			this.ElementCount = null;
			this.enumValues = new HashSet<string>(enumValues.Select(s => s.ToUpperInvariant()), StringComparer.InvariantCultureIgnoreCase);
		}

		public bool IsField(EvaluationName field) {
			return fields.ContainsKey(field);
		}

		public TypeField? GetField(EvaluationName field) {
			return fields.GetValueOrFallback(field, null);
		}

		public bool IsEnumValueDefined(string name) {
			return enumValues?.Contains(name) ?? false;
		}

		public EvaluationType MakeArray() {
			return new EvaluationType(this, null);
		}

		public EvaluationType MakeArray(int rank) {
			if (rank < 0) { throw new ArgumentException($"Invalid array rank. Value must be greater than zero. {rank} provided."); }
			EvaluationType result = this;
			for (int i = 0; i < rank; i++) {
				result = new EvaluationType(result, null);
			}
			return result;
		}

		public EvaluationType MakeTuple(int size) {
			return new EvaluationType(this, size, null);
		}

		public override string ToString() {
			return Name;
		}

		public static EvaluationType FromSystemType(Type type, bool includeProperties = true) {
			if (type == typeof(int)) { return INT; } // The int key seems necessary, as the default comparer for Types gets int mixed up with enums
			else if (type == typeof(uint)) { return UINT; }
			else if (type == typeof(float)) { return FLOAT; }
			else if (type == typeof(UFloat)) { return UFLOAT; }
			else if (type == typeof(bool)) { return BOOL; }
			else if (type == typeof(string)) { return STRING; }
			else if (type == typeof(Color)) { return COLOR; }
			else if (systemTypeRegistry.TryGetValue(type, out EvaluationType? registered)) {
				return registered;
			}
			else if (type.IsArray) {
				if (type.GetArrayRank() != 1) {
					throw new ArgumentException("Can only accept rank 1 arrays.");
				}
				Type elementType = type.GetElementType() ?? throw new ArgumentException($"Could not resolve element type for {type}");
				EvaluationType newType = FromSystemType(elementType, includeProperties).MakeArray();
				systemTypeRegistry.Add(type, newType);
				return newType;
			}
			else if (type.IsEnum) {
				EvaluationType newType = new EvaluationType(type.Name, Enum.GetNames(type), type);
				systemTypeRegistry.Add(type, newType);
				return newType;
			}
			else if (TupleUtils.IsTupleType(type)) {
				Type[] tupleElems = type.GenericTypeArguments;
				Type[] distinctTupleElems = tupleElems.Distinct().ToArray();
				if(distinctTupleElems.Length == 1) {
					EvaluationType newType = FromSystemType(distinctTupleElems[0], includeProperties).MakeTuple(tupleElems.Length);
					systemTypeRegistry.Add(type, newType);
					return newType;
				}
				else {
					throw new ArgumentException("Cannot support multi-type tuples.");
				}
			}
			else {
				EvaluationType newType = new EvaluationType(
					type.Name,
					!includeProperties ? Enumerable.Empty<TypeField>() : type.GetProperties()
						.Where(p => p.CanRead && p.GetGetMethod(false) is MethodInfo getMethod && !getMethod.IsStatic && p.PropertyType != type)
						.Select(p => new TypeField(p.Name.ToLowerInvariant(), FromSystemType(p.PropertyType), obj => p.GetValue(obj))),
					type);

				systemTypeRegistry.Add(type, newType);
				return newType;
			}
		}

		public static EvaluationType FromData(object value, bool includeProperties = true) {
			return FromSystemType(value.GetType(), includeProperties);
		}

		/*
		public static EvaluationType MakeEnum(string name, IEnumerable<string> values) {
			return new EvaluationType(name, values, null);
		}
		*/

		public static EvaluationType CustomType(string name, IEnumerable<TypeField> fields, Type systemType) {
			if (string.IsNullOrWhiteSpace(name)) {
				throw new ArgumentException((name != null ? $"\"{name}\"" : "<null>") + " is not a valid type name.");
			}
			if(systemType == null) {
				throw new ArgumentException("Must provide a valid system type.");
			}
			return new EvaluationType(name, fields, systemType);
		}

		public static bool Equals(EvaluationType? a, EvaluationType? b) {
			if (a is null || b is null) {
				return a is null && b is null;
			}
			else {
				return ReferenceEquals(a, b) || (
						a.baseName == b.baseName &&
						a.DataType == b.DataType &&
						a.DisplayType == b.DisplayType &&
						a.ElementType == b.ElementType &&
						a.ElementCount == b.ElementCount &&
						SetEquals(a.enumValues, b.enumValues)
					);
			}
		}

		private static bool SetEquals<T>(HashSet<T>? a, HashSet<T>? b) {
			if (a == null && b == null) {
				return true;
			}
			else if (a != null && b != null) {
				return a.SetEquals(b);
			}
			else {
				return false;
			}
		}

		public static bool operator ==(EvaluationType? a, EvaluationType? b) {
			return Equals(a, b);
		}
		public static bool operator !=(EvaluationType? a, EvaluationType? b) {
			return !Equals(a, b);
		}

		public override bool Equals(object? obj) {
			if (obj is EvaluationType evalType) {
				return Equals(this, evalType);
			}
			else {
				return false;
			}
		}

		public override int GetHashCode() {
			HashCode hash = new HashCode();
			hash.Add(baseName);
			hash.Add(DataType);
			hash.Add(DisplayType);
			if (ElementType != null) { hash.Add(ElementType); }
			if (ElementCount != null) { hash.Add(ElementCount.Value); }
			return hash.ToHashCode();

			/*
			int hashCode = -222562764;
			hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(baseName);
			hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(DataType);
			hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(DisplayType);
			if (ElementType != null) { hashCode = hashCode * -1521134295 + EqualityComparer<EvaluationType>.Default.GetHashCode(ElementType); }
			if (ElementCount != null) { hashCode = hashCode * -1521134295 + EqualityComparer<int?>.Default.GetHashCode(ElementCount); }
			return hashCode;
			*/
		}
		/*
		public override int GetHashCode() {
			return (baseName + "." + (SystemType?.FullName ?? "CUSTOM") + (ElementType != null ? ElementType.baseName + ElementType.GetHashCode() : "SINGLE") + (ElementCount.HasValue ? ElementCount.Value.ToString() : "Any")).GetHashCode();
		}
		*/

		public bool ValidDataType(Type dataType) {
			// TODO Is this complete?
			if(this.DataType == dataType) {
				return true;
			}
			else if (this.IsArray && dataType.IsArray && dataType.GetElementType() is Type dataElementType) {
				return this.ElementType.ValidDataType(dataElementType);
			}
			else {
				return false;
			}
		}
	}

	public static class EvaluationTypes {

		public static object MakeArray(Type elementType, IList<object?> values) {
			Array final = Array.CreateInstance(elementType, values.Count);
			Array.Copy(values.ToArray(), final, final.Length);
			return final;
		}

		public static object MakeTuple(Type elementType, IList<object?> values) {
			Type tupleType = TupleUtils.MakeGenericTupleType(elementType, values.Count);
			return TupleUtils.CreateTuple(tupleType, values.ToArray());
		}

		public static bool TryGetArray(object? obj, [MaybeNullWhen(false)] out Array array) {
			if (obj is Array objArray) {
				array = objArray;
				return true;
			}
			else if (TupleUtils.IsTupleObject(obj, out Type? tupleType)) {
				object[] values = new object[TupleUtils.GetTupleLength(tupleType)];
				for (int i = 0; i < values.Length; i++) {
					values[i] = TupleUtils.Index(obj!, i);
				}
				array = values;
				return true;
			}
			else {
				array = null;
				return false;
			}
		}

		public static bool IsIntegral(this EvaluationType type) {
			return type.DataType == typeof(int) || type.DataType == typeof(uint); ;
		}

		public static bool IsReal(this EvaluationType type) {
			return type.IsIntegral() || type.DataType == typeof(float) || type.DataType == typeof(UFloat);
		}

		public static bool TryGetReal(object? arg, out float value) {
			if (TryGetIntegral(arg, out int i)) {
				value = i;
				return true;
			}
			else if (arg is float f) {
				value = f;
				return true;
			}
			else if (arg is UFloat u) {
				value = u.Value;
				return true;
			}
			else {
				value = 0f;
				return false;
			}
		}

		public static bool TryGetIntegral(object? arg, out int value) {
			if (arg is int i) {
				value = i;
				return true;
			}
			else if (arg is uint u) {
				value = (int)u;
				return true;
			}
			else {
				value = 0;
				return false;
			}
		}

		public static EvaluationType? FindCommonNumericType(params EvaluationType[] types) {
			if(types.Length == 0) {
				return null;
			}
			else if (!types[0].IsReal()) {
				throw new EvaluationTypeException("Cannot find common numeric type.");
			}

			EvaluationType commonType = types[0];

			for(int i=1; i<types.Length; i++) {
				if (!types[i].IsReal()) {
					throw new EvaluationTypeException("Cannot find common numeric type.");
				}

				if (types[i] != commonType) {
					if (commonType.IsIntegral() && types[i].IsIntegral()) {
						commonType = EvaluationType.INT;
					}
					else {
						commonType = EvaluationType.FLOAT;
					}
				}
			}

			return commonType;
		}

		public static bool TryGetCompatibleType(EvaluationType a, EvaluationType b, [MaybeNullWhen(false)] out EvaluationType compatible) {
			if (a == b) {
				compatible = a;
				return true;
			}
			else if (a.IsIntegral() && b.IsIntegral()) {
				compatible = EvaluationType.INT;
				return true;
			}
			else if (a.IsReal() && b.IsReal()) {
				compatible = EvaluationType.FLOAT;
				return true;
			}
			else if (a.IsArray && b.IsArray && TryGetCompatibleType(a.ElementType, b.ElementType, out EvaluationType? compatibleElement)) {
				compatible = compatibleElement.MakeArray();
				return true;
			}
			else {
				compatible = null;
				return false;
			}
		}

		public static bool IsCompatibleType(EvaluationType type, EvaluationType other) {
			return TryGetCompatibleType(type, other, out EvaluationType? compatible) && type == compatible;
		}

		/// <summary></summary>
		/// <param name="compatible"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <exception cref="EvaluationCalculationException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		public static object GetCompatibleValue(EvaluationType compatible, object? value) {
			if (value is null) {
				throw new EvaluationCalculationException($"Cannot convert null value to {compatible}.");
			}
			else if (compatible.DataType == value.GetType()) {
				return value;
			}
			else if (compatible == EvaluationType.INT && TryGetIntegral(value, out int intVal)) {
				return intVal;
			}
			else if (compatible == EvaluationType.FLOAT && TryGetReal(value, out float floatVal)) {
				return floatVal;
			}
			else if (compatible.IsArray && value is Array arrayValue) {
				List<object?> compatibleValues = new List<object?>();

				foreach (object? i in arrayValue) {
					compatibleValues.Add(GetCompatibleValue(compatible.ElementType, i));
				}

				return MakeArray(compatible.ElementType.DataType, compatibleValues);
			}
			else {
				throw new EvaluationTypeException($"Cannot convert {value.GetType().Name} value to {compatible} value.");
			}
		}

		public static object[] VerifyArray(object?[] array) {
			object[] result = new object[array.Length];
			for (int i = 0; i < array.Length; i++) {
				result[i] = array[i] ?? throw new EvaluationCalculationException("Null values not allowed in this context.");
			}
			return result;
		}

	}

}
