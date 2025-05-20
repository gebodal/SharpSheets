using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SharpSheets.Colors;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SharpSheets.Evaluations {

	/*
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

			systemTypeRegistry = new Dictionary<Type, EvaluationType>(SystemTypeEqualityComparer.Instance) {
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
				.Append(new TypeField("length", INT, obj => ((Array)obj).Length))
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
				.Append(new TypeField("length", INT, obj => elementCount))
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
		}

		public bool ValidDataType(Type dataType) {
			// TODO Is this complete?
			if(this.DataType == dataType || (this == FLOAT && EvaluationTypes.IsReal(dataType)) || (this == INT && EvaluationTypes.IsIntegral(dataType))) {
				return true;
			}
			else if (this.IsArray && dataType.IsArray && dataType.GetElementType() is Type dataElementType) {
				return this.ElementType.ValidDataType(dataElementType);
			}
			else {
				return false;
			}
		}

		private class SystemTypeEqualityComparer : IEqualityComparer<Type> {
			public static readonly SystemTypeEqualityComparer Instance = new SystemTypeEqualityComparer();

			private SystemTypeEqualityComparer() { }

			public bool Equals(Type? x, Type? y) {
				return x == y;
			}

			public int GetHashCode([DisallowNull] Type obj) {
				return HashCode.Combine(obj.GetHashCode(), obj.Name); // TODO Is this sufficient?
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

		public static bool IsIntegral(Type dataType) {
			return dataType == typeof(int) || dataType == typeof(uint); ;
		}

		public static bool IsReal(this EvaluationType type) {
			return type.IsIntegral() || type.DataType == typeof(float) || type.DataType == typeof(UFloat);
		}

		public static bool IsReal(Type dataType) {
			return IsIntegral(dataType) || dataType == typeof(float) || dataType == typeof(UFloat);
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
	*/

	public static class EvaluationTypes {

		public static EvaluationType META => MetaEvaluationType.Instance;

		public static EvaluationType INT => IntEvaluationType.Instance;
		public static EvaluationType UINT => UIntEvaluationType.Instance;
		public static EvaluationType FLOAT => FloatEvaluationType.Instance;
		public static EvaluationType UFLOAT => UFloatEvaluationType.Instance;
		public static EvaluationType BOOL => BoolEvaluationType.Instance;
		public static EvaluationType STRING => StringEvaluationType.Instance;
		//public static EvaluationType COLOR;

		public static EvaluationTypeSystem BaseTypeSystem { get; } = EvaluationTypeSystem.Create(INT, UINT, FLOAT, UFLOAT, BOOL, STRING);

	}


	public sealed class EvaluationContext {

		private EvaluationTypeSystem? typeSystem;
		public EvaluationTypeSystem TypeSystem => typeSystem!;

		private readonly Dictionary<Type, EvaluationType> types = new Dictionary<Type, EvaluationType>();

		private EvaluationContext() { }

		public EvaluationType GetType<T>() where T : EvaluationType {
			return types.TryGetValue(typeof(T), out EvaluationType? type) ? type : throw new EvaluationTypeException($"No registered instance of {typeof(T).Name} in this context.");
		}

		private void SetType(Type key, EvaluationType type) {
			types.Add(key, type);
		}

		private void Initialise(IEnumerable<(Type, EvaluationType)> registered) {
			foreach((Type key, EvaluationType type) in registered) {
				SetType(key, type);
			}

			typeSystem = EvaluationTypeSystem.Create(types.Values);
		}

		public static EvaluationContext Build(Func<EvaluationContext, IEnumerable<(Type, EvaluationType)>> registration) {
			EvaluationContext context = new EvaluationContext();

			/*
			context.META = new MetaEvaluationType(context);
			context.INT = new IntEvaluationType(context);
			context.UINT = new UIntEvaluationType(context);
			context.FLOAT = new FloatEvaluationType(context);
			context.UFLOAT = new UFloatEvaluationType(context);
			context.BOOL = new BoolEvaluationType(context);
			context.STRING = new StringEvaluationType(context);
			*/

			context.SetType(typeof(MetaEvaluationType), new MetaEvaluationType(context));

			(Type, EvaluationType)[] registered = registration(context).ToArray();
			context.Initialise(registered);
			return context;
		}

	}






	public readonly struct EvaluationValue {
		public readonly object? Value;
		public readonly EvaluationType Type;

		public EvaluationValue(object? value, EvaluationType type) {
			Value = value;
			Type = type;
		}
	}

	public class TypeField {

		public EvaluationName Name { get; }
		public EvaluationType Type { get; }
		private readonly Func<EvaluationValue, EvaluationValue> evaluator;

		public TypeField(EvaluationName name, EvaluationType type, Func<EvaluationValue, EvaluationValue> evaluator) {
			Name = name;
			Type = type;
			this.evaluator = evaluator;
		}

		public EvaluationValue GetValue(EvaluationValue subject) {
			return evaluator(subject);
		}

	}

	public abstract class EvaluationType : IEquatable<EvaluationType> {

		public abstract string Name { get; }

		public abstract Type DataType { get; }
		public abstract Type DisplayType { get; }

		private readonly IReadOnlyDictionary<EvaluationName, TypeField> fields;
		public IEnumerable<EvaluationName> FieldNames { get { return fields.Keys; } }
		public IEnumerable<TypeField> Fields { get { return fields.Values; } }

		private readonly IReadOnlyDictionary<EvaluationName, TypeField> staticFields;
		public IEnumerable<EvaluationName> StaticFieldNames { get { return staticFields.Keys; } }
		public IEnumerable<TypeField> StaticFields { get { return staticFields.Values; } }

		protected EvaluationType(IEnumerable<TypeField> fields, IEnumerable<TypeField> staticFields) {
			this.fields = fields.ToDictionary(f => f.Name);
			this.staticFields = staticFields.ToDictionary(f => f.Name);
		}


		/// <summary>
		/// Indicates whether the other type can be implicitly cast into the
		/// current type.
		/// </summary>
		/// <param name="other">A potential sub-type of the current type to
		/// be tested.</param>
		/// <returns><see langword="true"/> if <paramref name="other"/> can be
		/// implicitly cast into the current type, otherwise
		/// <see langword="false"/>.</returns>
		public abstract bool CanImplicitCastFrom(EvaluationType other);
		public abstract EvaluationValue? Cast(EvaluationValue other);

		/*
		public bool IsField(EvaluationName field) {
			return fields.ContainsKey(field);
		}
		*/

		public TypeField? GetField(EvaluationName field) {
			return fields.GetValueOrFallback(field, null);
		}

		public bool IsStaticField(EvaluationName field) {
			return staticFields.ContainsKey(field);
		}

		public TypeField? GetStaticField(EvaluationName field) {
			return staticFields.GetValueOrFallback(field, null);
		}

		public override string ToString() {
			return Name;
		}

		public virtual EvaluationType? PosResult() => null;
		public virtual EvaluationValue? Pos(EvaluationValue operand) => null;

		public virtual EvaluationType? NegResult() => null;
		public virtual EvaluationValue? Neg(EvaluationValue operand) => null;

		public virtual EvaluationType? InvertResult() => null;
		public virtual EvaluationValue? Invert(EvaluationValue operand) => null;

		public virtual EvaluationType? AddResult(EvaluationType right) => null;
		public virtual EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => null;
		public virtual EvaluationType? RAddResult(EvaluationType left) => null;
		public virtual EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? SubResult(EvaluationType right) => null;
		public virtual EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => null;
		public virtual EvaluationType? RSubResult(EvaluationType left) => null;
		public virtual EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? MulResult(EvaluationType right) => null;
		public virtual EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => null;
		public virtual EvaluationType? RMulResult(EvaluationType left) => null;
		public virtual EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? DivResult(EvaluationType right) => null;
		public virtual EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => null;
		public virtual EvaluationType? RDivResult(EvaluationType left) => null;
		public virtual EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? ModResult(EvaluationType right) => null;
		public virtual EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => null;
		public virtual EvaluationType? RModResult(EvaluationType left) => null;
		public virtual EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? PowResult(EvaluationType right) => null;
		public virtual EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => null;
		public virtual EvaluationType? RPowResult(EvaluationType left) => null;
		public virtual EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? LessThanResult(EvaluationType other) => null;
		public virtual EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? LessThanEqualResult(EvaluationType other) => null;
		public virtual EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? GreaterThanResult(EvaluationType other) => null;
		public virtual EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? GreaterThanEqualResult(EvaluationType other) => null;
		public virtual EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? EqualResult(EvaluationType other) => null;
		public virtual EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? NotEqualResult(EvaluationType other) => null;
		public virtual EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => null;

		/*
		public virtual EvaluationType? AndResult(EvaluationType other) => null;
		public virtual EvaluationValue? And(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? OrResult(EvaluationType other) => null;
		public virtual EvaluationValue? Or(EvaluationValue left, EvaluationValue right) => null;

		public virtual EvaluationType? XorResult(EvaluationType other) => null;
		public virtual EvaluationValue? Xor(EvaluationValue left, EvaluationValue right) => null;
		*/

		public virtual EvaluationType? IndexerResult(EvaluationType index) => null;
		public virtual EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) => null;
		public virtual EvaluationType? IndexerSliceResult(EvaluationType start, EvaluationType end) => null;
		public virtual EvaluationValue? IndexerSlice(EvaluationValue subject, EvaluationValue start, EvaluationValue end) => null;

		public virtual EvaluationType? IterationResult() => null; // Gives the element type resulting from iterating over a value of this type
		public virtual IEnumerable<EvaluationValue>? Iteration(EvaluationValue subject) => null;

		/*
		public virtual bool CanTruth() => false;
		public virtual bool? Truth() => null;
		*/

		//object.__call__(self[, args...])

		public EvaluationType MakeArray() {
			return new ArrayEvaluationType(this, Enumerable.Empty<TypeField>());
		}

		public EvaluationType MakeArray(int rank) {
			if (rank < 0) { throw new ArgumentException($"Invalid array rank. Value must be greater than zero. {rank} provided."); }
			EvaluationType result = this;
			for (int i = 0; i < rank; i++) {
				result = result.MakeArray();
			}
			return result;
		}

		public EvaluationType MakeTuple(int size) {
			return new TupleEvaluationType(this, size, Enumerable.Empty<TypeField>());
		}

		public bool Equals(EvaluationType? other) {
			if (other is null) {
				return false;
			}
			else if (ReferenceEquals(this, other)) {
				return true;
			}
			else if (GetType() != other.GetType()) {
				return false;
			}

			// Subclass compares subclass data
			return EqualTypeData(other) && FieldsEqual(other);
		}

		public override bool Equals(object? obj) {
			return Equals(obj as EvaluationType);
		}

		public override int GetHashCode() {
			return HashCode.Combine(GetType(), GetTypeHashCode());
		}

		private bool FieldsEqual(EvaluationType other) {
			// Check if same fields are present
			if(!new HashSet<EvaluationName>(fields.Keys).SetEquals(new HashSet<EvaluationName>(other.fields.Keys))) {
				return false;
			}
			// Check that all fields have the same return types
			foreach ((EvaluationName name, TypeField field) in fields) {
				if (other.fields[name].Type != field.Type) {
					return false;
				}
			}
			// Everything must be the same
			return true;
		}

		// Subclasses implement child-specific field comparisons
		protected abstract bool EqualTypeData(EvaluationType other);
		// Subclasses implement child-specific field hashes
		protected abstract int GetTypeHashCode();

		public static bool Equals(EvaluationType? a, EvaluationType? b) {
			if (a is null || b is null) {
				return a is null && b is null;
			}
			else {
				return a.Equals(b);
			}
		}

		public static bool operator ==(EvaluationType? a, EvaluationType? b) {
			return Equals(a, b);
		}
		public static bool operator !=(EvaluationType? a, EvaluationType? b) {
			return !Equals(a, b);
		}

	}

	public static class EvaluationOps {

		private static EvaluationType? GetPromoted(EvaluationTypeSystem typeSystem, EvaluationType a, EvaluationType b) {
			return typeSystem.TryGetLeastUpperBoundType(a, b, out EvaluationType? promoted) ? promoted : null;
		}

		public static EvaluationType? AddResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.AddResult(right) ?? right.RAddResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.AddResult(promoted) : null);
		public static EvaluationValue? Add(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.Add(left, right) ?? right.Type.RAdd(left, right) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.Add(left, right) : null);

		public static EvaluationType? SubResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.SubResult(right) ?? right.RSubResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.SubResult(promoted) : null);
		public static EvaluationValue? Sub(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.Sub(left, right) ?? right.Type.RSub(left, right) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.Sub(left, right) : null);

		public static EvaluationType? MulResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.MulResult(right) ?? right.RMulResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.MulResult(promoted) : null);
		public static EvaluationValue? Mul(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.Mul(left, right) ?? right.Type.RMul(left, right) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.Mul(left, right) : null);

		public static EvaluationType? DivResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.DivResult(right) ?? right.RDivResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.DivResult(promoted) : null);
		public static EvaluationValue? PerformDiv(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.Div(left, right) ?? right.Type.RDiv(left, right) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.Div(left, right) : null);

		public static EvaluationType? ModResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.ModResult(right) ?? right.RModResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.ModResult(promoted) : null);
		public static EvaluationValue? Mod(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.Mod(left, right) ?? right.Type.RMod(left, right) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.Mod(left, right) : null);

		public static EvaluationType? PowResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.PowResult(right) ?? right.RPowResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.PowResult(promoted) : null);
		public static EvaluationValue? Pow(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.Pow(left, right) ?? right.Type.RPow(left, right) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.Pow(left, right) : null);

		public static EvaluationType? LessThanResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.LessThanResult(right) ?? right.GreaterThanResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.LessThanResult(promoted) : null);
		public static EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.LessThan(left, right) ?? right.Type.GreaterThan(right, left) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.LessThan(left, right) : null);

		public static EvaluationType? LessThanEqualResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.LessThanEqualResult(right) ?? right.GreaterThanEqualResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.LessThanEqualResult(promoted) : null);
		public static EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.LessThanEqual(left, right) ?? right.Type.GreaterThanEqual(right, left) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.LessThanEqual(left, right) : null);

		public static EvaluationType? GreaterThanResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.GreaterThanResult(right) ?? right.LessThanResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.GreaterThanResult(promoted) : null);
		public static EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.GreaterThan(left, right) ?? right.Type.LessThan(right, left) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.GreaterThan(left, right) : null);

		public static EvaluationType? GreaterThanEqualResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.GreaterThanEqualResult(right) ?? right.LessThanEqualResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.GreaterThanEqualResult(promoted) : null);
		public static EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.GreaterThanEqual(left, right) ?? right.Type.LessThanEqual(right, left) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.GreaterThanEqual(left, right) : null);

		public static EvaluationType? EqualResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.EqualResult(right) ?? right.EqualResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.EqualResult(promoted) : null);
		public static EvaluationValue? Equal(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.Equal(left, right) ?? right.Type.Equal(right, left) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.Equal(left, right) : null);

		public static EvaluationType? NotEqualResult(EvaluationType left, EvaluationType right, EvaluationTypeSystem typeSystem) => left.NotEqualResult(right) ?? right.NotEqualResult(left) ?? (GetPromoted(typeSystem, left, right) is EvaluationType promoted ? promoted.NotEqualResult(promoted) : null);
		public static EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right, EvaluationTypeSystem typeSystem) => left.Type.NotEqual(left, right) ?? right.Type.NotEqual(right, left) ?? (GetPromoted(typeSystem, left.Type, right.Type) is EvaluationType promoted ? promoted.NotEqual(left, right) : null);

	}

	public static class EvaluationTypeHelpers {

		public static int GetIndex(int index, int length) {
			if (index < 0) index += length;
			return index;
		}

		public static void GetSliceIndexes(int a, int b, int length, out int start, out int end) {
			if (a < 0) a += length;
			if (b < 0) b += length;

			start = a.Clamp(0, length);
			end = b.Clamp(start, length);
		}

	}

	public sealed class MetaEvaluationType : EvaluationType {

		public static readonly EvaluationType Instance = new MetaEvaluationType();

		public override string Name { get; } = "type";

		public override Type DataType { get; } = typeof(Type);
		public override Type DisplayType => DataType;

		private MetaEvaluationType() : base(Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>()) { }

		public override bool CanImplicitCastFrom(EvaluationType other) => false;
		public override EvaluationValue? Cast(EvaluationValue other) => null;

		protected override bool EqualTypeData(EvaluationType other) {
			return Name == other.Name
				&& DataType == other.DataType;
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(Name, DataType);
		}
	}

	public abstract class SingleDataType : EvaluationType {

		public sealed override Type DisplayType => DataType;

		protected SingleDataType(IEnumerable<TypeField> fields, IEnumerable<TypeField> staticFields) : base(fields, staticFields) { }

		protected sealed override bool EqualTypeData(EvaluationType other) {
			return Name == other.Name
				&& DataType == other.DataType;
		}

		protected sealed override int GetTypeHashCode() {
			return HashCode.Combine(Name, DataType);
		}

	}

	public sealed class FloatEvaluationType : SingleDataType {

		public static readonly FloatEvaluationType Instance = new FloatEvaluationType();

		public override string Name { get; } = "float";
		public override Type DataType { get; } = typeof(float);

		private static readonly TypeField MinField = new TypeField("MINVALUE", Instance, t => new EvaluationValue(float.MinValue, Instance));
		private static readonly TypeField MaxField = new TypeField("MAXVALUE", Instance, t => new EvaluationValue(float.MaxValue, Instance));

		private FloatEvaluationType() : base(Enumerable.Empty<TypeField>(), new TypeField[] { MinField, MaxField }) { }

		public static bool IsReal(EvaluationType type) {
			return type == Instance || UFloatEvaluationType.IsPositiveReal(type) || IntEvaluationType.IsIntegral(type);
		}

		public static bool AllReal(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsReal(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetFloat(EvaluationValue value, out float number) {
			if (value.Value is null) {
				number = 0f;
				return false;
			}
			else if (value.Type == Instance && value.Value is float floatVal) {
				number = floatVal;
				return true;
			}
			else if (UFloatEvaluationType.TryGetUFloat(value, out UFloat uFloatVal)) {
				number = uFloatVal;
				return true;
			}
			else if (IntEvaluationType.TryGetInt(value, out int intVal)) {
				number = intVal;
				return true;
			}

			number = 0f;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsReal(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetFloat(other, out float value)) {
				return new EvaluationValue(value, Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? UnaryArithmetic(EvaluationValue operand, Func<float, float> operation) {
			if (TryGetFloat(operand, out float operandVal)) {
				return new EvaluationValue(operation(operandVal), Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryArithmeticResult(EvaluationType other) {
			if (IsReal(other)) { // Another float-like
				return Instance;
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryOperationAny<R>(EvaluationValue left, EvaluationValue right, Func<float, float, R> operation) {
			if (TryGetFloat(left, out float leftVal) && TryGetFloat(right, out float rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsReal(other)) { // Another float-like
				return BoolEvaluationType.Instance;
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => Instance;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => +a);

		public override EvaluationType? NegResult() => Instance;
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => -a);

		public override EvaluationType? AddResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RAddResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a + b);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a + b);

		public override EvaluationType? SubResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RSubResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a - b);
		public override EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a - b);

		public override EvaluationType? MulResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RMulResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a * b);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a * b);

		public override EvaluationType? DivResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RDivResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a / b);
		public override EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a / b);

		public override EvaluationType? ModResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RModResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a % b);
		public override EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a % b);

		public override EvaluationType? PowResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RPowResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => (float)Math.Pow(a, b));
		public override EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => (float)Math.Pow(a, b));

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a < b);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a <= b);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a > b);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a >= b);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a != b);
	}

	public sealed class UFloatEvaluationType : SingleDataType {

		public static readonly UFloatEvaluationType Instance = new UFloatEvaluationType();

		public override string Name { get; } = "ufloat";
		public override Type DataType { get; } = typeof(UFloat);

		private static readonly TypeField MaxField = new TypeField("MAXVALUE", Instance, t => new EvaluationValue(UFloat.MaxValue, Instance));

		private UFloatEvaluationType() : base(Enumerable.Empty<TypeField>(), new TypeField[] { MaxField }) { }

		public static bool IsPositiveReal(EvaluationType type) {
			return type == Instance || UIntEvaluationType.IsPositiveIntegral(type);
		}

		public static bool AllPositiveReal(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsPositiveReal(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetUFloat(EvaluationValue value, out UFloat number) {
			if (value.Type == Instance && value.Value is UFloat uFloatVal) {
				number = uFloatVal;
				return true;
			}
			else if (UIntEvaluationType.TryGetUInt(value, out uint uintVal)) {
				number = new UFloat((float)uintVal);
				return true;
			}

			number = UFloat.Zero;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsPositiveReal(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetUFloat(other, out UFloat value)) {
				return new EvaluationValue(value, Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? UnaryArithmetic<T>(EvaluationValue operand, Func<UFloat, T> operation, EvaluationType resultType) {
			if (TryGetUFloat(operand, out UFloat operandVal)) {
				return new EvaluationValue(operation(operandVal), resultType);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryArithmeticResult(EvaluationType other, bool closed) {
			if (IsPositiveReal(other)) {
				return closed ? Instance : FloatEvaluationType.Instance;
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryOperationClosed(EvaluationValue left, EvaluationValue right, Func<UFloat, UFloat, UFloat> operation) {
			if (TryGetUFloat(left, out UFloat leftVal) && TryGetUFloat(right, out UFloat rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryOperationNotClosed(EvaluationValue left, EvaluationValue right, Func<UFloat, UFloat, float> operation) {
			if (TryGetUFloat(left, out UFloat leftVal) && TryGetUFloat(right, out UFloat rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), FloatEvaluationType.Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsPositiveReal(other)) { // Another UFloat-like
				return BoolEvaluationType.Instance;
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<UFloat, UFloat, bool> operation) {
			if (TryGetUFloat(left, out UFloat leftVal) && TryGetUFloat(right, out UFloat rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Instance);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => Instance;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => a, Instance);

		public override EvaluationType? NegResult() => FloatEvaluationType.Instance;
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => -a.Value, FloatEvaluationType.Instance);

		public override EvaluationType? AddResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RAddResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a + b);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a + b);

		public override EvaluationType? SubResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RSubResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		public override EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => a - b);
		public override EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => a - b);

		public override EvaluationType? MulResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RMulResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a * b);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a * b);

		public override EvaluationType? DivResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RDivResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a / b);
		public override EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a / b);

		public override EvaluationType? ModResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RModResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		// Closed or not closed?
		public override EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => a % b);
		public override EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => a % b);

		public override EvaluationType? PowResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RPowResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		// Closed or not closed?
		public override EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (float)Math.Pow(a, b));
		public override EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (float)Math.Pow(a, b));

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a < b);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a <= b);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a > b);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a >= b);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a != b);
	}

	public sealed class IntEvaluationType : SingleDataType {

		public static readonly IntEvaluationType Instance = new IntEvaluationType();

		public override string Name { get; } = "int";
		public override Type DataType { get; } = typeof(int);

		private static readonly TypeField MinField = new TypeField("MINVALUE", Instance, t => new EvaluationValue(int.MinValue, Instance));
		private static readonly TypeField MaxField = new TypeField("MAXVALUE", Instance, t => new EvaluationValue(int.MaxValue, Instance));

		private IntEvaluationType() : base(Enumerable.Empty<TypeField>(), new TypeField[] { MinField, MaxField }) { }

		public static bool IsIntegral(EvaluationType type) {
			return type == Instance || UIntEvaluationType.IsPositiveIntegral(type);
		}

		public static bool AllIntegral(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsIntegral(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetInt(EvaluationValue value, out int number) {
			if (value.Value is null) {
				number = 0;
				return false;
			}
			else if (value.Type == Instance && value.Value is int intVal) {
				number = intVal;
				return true;
			}
			else if (UIntEvaluationType.TryGetUInt(value, out uint uintVal)) {
				number = (int)uintVal;
				return true;
			}

			number = 0;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsIntegral(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetInt(other, out int value)) {
				return new EvaluationValue(value, Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? UnaryArithmetic(EvaluationValue operand, Func<int, int> operation) {
			if (TryGetInt(operand, out int operandVal)) {
				return new EvaluationValue(operation(operandVal), Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryArithmeticResult(EvaluationType other) {
			if (IsIntegral(other)) { // Another int-like
				return Instance;
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryOperationAny<R>(EvaluationValue left, EvaluationValue right, Func<int, int, R> operation) {
			if (TryGetInt(left, out int leftVal) && TryGetInt(right, out int rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsIntegral(other)) { // Another float-like
				return BoolEvaluationType.Instance;
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => Instance;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => +a);

		public override EvaluationType? NegResult() => Instance;
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => -a);

		public override EvaluationType? AddResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RAddResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a + b);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a + b);

		public override EvaluationType? SubResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RSubResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a - b);
		public override EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a - b);

		public override EvaluationType? MulResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RMulResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a * b);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a * b);

		public override EvaluationType? DivResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RDivResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a / b);
		public override EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a / b);

		public override EvaluationType? ModResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RModResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a % b);
		public override EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a % b);

		public override EvaluationType? PowResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RPowResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => (int)Math.Pow(a, b));
		public override EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => (int)Math.Pow(a, b));

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a < b);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a <= b);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a > b);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a >= b);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryOperationAny(left, right, (a, b) => a != b);
	}

	public sealed class UIntEvaluationType : SingleDataType {

		public static readonly UIntEvaluationType Instance = new UIntEvaluationType();

		public override string Name { get; } = "uint";
		public override Type DataType { get; } = typeof(uint);

		private static readonly TypeField MaxField = new TypeField("MAXVALUE", Instance, t => new EvaluationValue(uint.MaxValue, Instance));

		private UIntEvaluationType() : base(Enumerable.Empty<TypeField>(), new TypeField[] { MaxField }) { }

		public static bool IsPositiveIntegral(EvaluationType type) {
			return type == Instance;
		}

		public static bool AllPositiveIntegral(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsPositiveIntegral(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetUInt(EvaluationValue value, out uint number) {
			if (value.Type == Instance && value.Value is uint uintVal) {
				number = uintVal;
				return true;
			}

			number = 0U;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsPositiveIntegral(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetUInt(other, out uint value)) {
				return new EvaluationValue(value, Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? UnaryArithmetic<T>(EvaluationValue operand, Func<uint, T> operation, EvaluationType resultType) {
			if (TryGetUInt(operand, out uint operandVal)) {
				return new EvaluationValue(operation(operandVal), resultType);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryArithmeticResult(EvaluationType other, bool closed) {
			if (IsPositiveIntegral(other)) {
				return closed ? Instance : IntEvaluationType.Instance;
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryOperationClosed(EvaluationValue left, EvaluationValue right, Func<uint, uint, uint> operation) {
			if (TryGetUInt(left, out uint leftVal) && TryGetUInt(right, out uint rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryOperationNotClosed(EvaluationValue left, EvaluationValue right, Func<uint, uint, int> operation) {
			if (TryGetUInt(left, out uint leftVal) && TryGetUInt(right, out uint rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), FloatEvaluationType.Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsPositiveIntegral(other)) { // Another UFloat-like
				return BoolEvaluationType.Instance;
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<uint, uint, bool> operation) {
			if (TryGetUInt(left, out uint leftVal) && TryGetUInt(right, out uint rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Instance);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => Instance;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => a, Instance);

		public override EvaluationType? NegResult() => FloatEvaluationType.Instance;
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => (int)(-a), IntEvaluationType.Instance);

		public override EvaluationType? AddResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RAddResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a + b);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a + b);

		public override EvaluationType? SubResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RSubResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		public override EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)a - (int)b);
		public override EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)a - (int)b);

		public override EvaluationType? MulResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RMulResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a * b);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a * b);

		public override EvaluationType? DivResult(EvaluationType right) => BinaryArithmeticResult(right, true);
		public override EvaluationType? RDivResult(EvaluationType left) => BinaryArithmeticResult(left, true);
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a / b);
		public override EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => BinaryOperationClosed(left, right, (a, b) => a / b);

		public override EvaluationType? ModResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RModResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		// Closed or not closed?
		public override EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)a % (int)b);
		public override EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)a % (int)b);

		public override EvaluationType? PowResult(EvaluationType right) => BinaryArithmeticResult(right, false);
		public override EvaluationType? RPowResult(EvaluationType left) => BinaryArithmeticResult(left, false);
		// Closed or not closed?
		public override EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)Math.Pow(a, b));
		public override EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => BinaryOperationNotClosed(left, right, (a, b) => (int)Math.Pow(a, b));

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a < b);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a <= b);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a > b);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a >= b);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a != b);
	}

	public sealed class BoolEvaluationType : SingleDataType {

		public static readonly BoolEvaluationType Instance = new BoolEvaluationType();

		public override string Name { get; } = "bool";
		public override Type DataType { get; } = typeof(bool);

		private BoolEvaluationType() : base(Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>()) { }

		public static bool IsBool(EvaluationType other) {
			return other == Instance;
		}

		public static bool AllBool(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsBool(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetBool(EvaluationValue value, out bool boolean) {
			if (value.Type == Instance && value.Value is bool boolVal) {
				boolean = boolVal;
				return true;
			}

			boolean = false;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsBool(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetBool(other, out bool value)) {
				return new EvaluationValue(value, Instance);
			}
			else {
				return null;
			}
		}

		public static bool TryCastBool(EvaluationValue value, out bool boolean) {
			if (TryGetBool(value, out bool boolVal)) {
				boolean = boolVal;
				return true;
			}
			else if (IntEvaluationType.TryGetInt(value, out int intVal)) {
				boolean = intVal != 0;
				return true;
			}
			else if (FloatEvaluationType.TryGetFloat(value, out float floatVal)) {
				boolean = floatVal != 0f;
				return true;
			}

			boolean = false;
			return false;
		}

		private static EvaluationValue? UnaryOperationAny(EvaluationValue operand, Func<bool, bool> operation) {
			if (TryGetBool(operand, out bool value)) {
				return new EvaluationValue(operation(value), Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsBool(other)) { // Another bool
				return Instance;
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<bool, bool, bool> operation) {
			if (TryGetBool(left, out bool leftVal) && TryGetBool(right, out bool rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Instance);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? InvertResult() => Instance;
		public override EvaluationValue? Invert(EvaluationValue operand) => UnaryOperationAny(operand, (a) => !a);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a != b);

		/*
		public override EvaluationType? AndResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? And(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a && b);

		public override EvaluationType? OrResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Or(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a || b);

		public override EvaluationType? XorResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Xor(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a ^ b);
		*/

	}

	public sealed class StringEvaluationType : SingleDataType {

		public static readonly StringEvaluationType Instance = new StringEvaluationType();

		private static readonly TypeField LengthField = new TypeField("length", IntEvaluationType.Instance, value => new EvaluationValue(((string)value.Value!).Length, IntEvaluationType.Instance));

		public override string Name { get; } = "str";
		public override Type DataType { get; } = typeof(string);

		private StringEvaluationType() : base(LengthField.Yield(), Enumerable.Empty<TypeField>()) { }

		public static bool IsString(EvaluationType other) {
			return other == Instance;
		}

		public static bool AllString(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsString(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetString(EvaluationValue value, [NotNullWhen(true)] out string? str) {
			if (value.Type == Instance && value.Value is string stringVal) {
				str = stringVal;
				return true;
			}

			str = null;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsString(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetString(other, out string? value)) {
				return new EvaluationValue(value, Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? AddResultAny(EvaluationType other) {
			if (IsString(other)) { // Another string
				return Instance;
			}
			// TODO Check if other can be converted to string?
			else {
				return null;
			}
		}

		private static EvaluationValue? AddAny(EvaluationValue left, EvaluationValue right) {
			if (TryGetString(left, out string? leftVal) && TryGetString(right, out string? rightVal)) {
				return new EvaluationValue(leftVal + rightVal, Instance);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? AddResult(EvaluationType right) => AddResultAny(right);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => AddAny(left, right);
		public override EvaluationType? RAddResult(EvaluationType left) => AddResultAny(left);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => AddAny(left, right);

		private static EvaluationType? MulResultAny(EvaluationType other) {
			if (IntEvaluationType.IsIntegral(other)) { // An int-like
				return Instance;
			}
			else {
				return null;
			}
		}

		private static string RepeatString(string str, int count) {
			return new StringBuilder(str.Length * count).Insert(0, str, Math.Max(0, count)).ToString();
		}

		public override EvaluationType? MulResult(EvaluationType right) => MulResultAny(right);
		public override EvaluationType? RMulResult(EvaluationType left) => MulResultAny(left);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) {
			if (TryGetString(left, out string? stringVal) && IntEvaluationType.TryGetInt(right, out int intVal)) {
				return new EvaluationValue(RepeatString(stringVal, intVal), Instance);
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) {
			if (IntEvaluationType.TryGetInt(left, out int intVal) && TryGetString(right, out string? stringVal)) {
				return new EvaluationValue(RepeatString(stringVal, intVal), Instance);
			}
			else {
				return null;
			}
		}

		private static EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsString(other)) { // Another string
				return BoolEvaluationType.Instance;
			}
			else {
				return null;
			}
		}

		private static EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<string, string, bool> operation) {
			if (TryGetString(left, out string? leftVal) && TryGetString(right, out string? rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Instance);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a.Equals(b));

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => !a.Equals(b));

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (IntEvaluationType.IsIntegral(index)) {
				return Instance;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if(TryGetString(subject, out string? subjectVal) && IntEvaluationType.TryGetInt(index, out int indexVal)) {
				int indexFinal = EvaluationTypeHelpers.GetIndex(indexVal, subjectVal.Length);
				return new EvaluationValue(subjectVal[indexFinal].ToString(), Instance);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IndexerSliceResult(EvaluationType start, EvaluationType end) {
			if (IntEvaluationType.IsIntegral(start) && IntEvaluationType.IsIntegral(end)) {
				return Instance;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? IndexerSlice(EvaluationValue subject, EvaluationValue start, EvaluationValue end) {
			if (TryGetString(subject, out string? subjectVal) && IntEvaluationType.TryGetInt(start, out int startVal) && IntEvaluationType.TryGetInt(end, out int endVal)) {
				EvaluationTypeHelpers.GetSliceIndexes(startVal, endVal, subjectVal.Length, out int startFinal, out int endFinal);
				return new EvaluationValue(subjectVal[startFinal..endFinal].ToString(), Instance);
			}
			else {
				return null;
			}
		}

	}

	public abstract class CollectionEvaluationType : EvaluationType {

		public EvaluationType ElementType { get; }

		protected CollectionEvaluationType(EvaluationType elementType, IEnumerable<TypeField> fields, IEnumerable<TypeField> staticFields) : base(fields, staticFields) {
			this.ElementType = elementType;
		}

		public override string Name {
			get {
				return ElementType.Name + GetCollectionBrackets();
			}
		}

		private string GetCollectionBrackets() {
			return GetMyCollectionBrackets() + (ElementType is CollectionEvaluationType subCollection ? subCollection.GetCollectionBrackets() : "");
		}

		protected abstract string GetMyCollectionBrackets();

		public override bool CanImplicitCastFrom(EvaluationType other) => false; // All collection casting requires more information
		public abstract bool CanImplicitCastFrom(EvaluationType other, EvaluationTypeSystem typeSystem, [NotNullWhen(true)] out EvaluationType? lubType);

	}

	public class ArrayEvaluationType : CollectionEvaluationType {

		private static readonly TypeField LengthField = new TypeField("length", IntEvaluationType.Instance, value => new EvaluationValue(((Array)value.Value!).Length, IntEvaluationType.Instance));

		public override Type DataType { get; }
		public override Type DisplayType { get; }

		internal ArrayEvaluationType(EvaluationType elementType, IEnumerable<TypeField> fields) : base(elementType, fields.Append(LengthField), Enumerable.Empty<TypeField>()) {
			this.DataType = this.ElementType.DataType.MakeArrayType(1);
			this.DisplayType = this.ElementType.DisplayType.MakeArrayType(1);
		}

		protected override string GetMyCollectionBrackets() {
			return "[]";
		}

		private static bool TryGetArray(EvaluationValue value, [MaybeNullWhen(false)] out Array array) {
			if (value.Value is Array objArray) {
				array = objArray;
				return true;
			}
			else if (TupleUtils.IsTupleObject(value.Value, out Type? tupleType)) {
				object[] values = new object[TupleUtils.GetTupleLength(tupleType)];
				for (int i = 0; i < values.Length; i++) {
					values[i] = TupleUtils.Index(value.Value!, i);
				}
				array = values;
				return true;
			}
			else {
				array = null;
				return false;
			}
		}

		public static EvaluationValue MakeArray(EvaluationType elementType, IList<EvaluationValue> values) {
			Array final = Array.CreateInstance(elementType.DataType, values.Count);
			Array.Copy(values.Select(v => v.Value).ToArray(), final, final.Length);
			return new EvaluationValue(final, elementType.MakeArray());
		}

		public override bool CanImplicitCastFrom(EvaluationType other, EvaluationTypeSystem typeSystem, [NotNullWhen(true)] out EvaluationType? lubType) {
			if(other is CollectionEvaluationType collection && typeSystem.TryGetLeastUpperBoundType(ElementType, collection.ElementType, out EvaluationType? lubElement)) {
				lubType = lubElement.MakeArray();
				return true;
			}
			else {
				lubType = null;
				return false;
			}
		}

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (other.Type.Iteration(other)?.ToArray() is EvaluationValue[] otherValues) {
				EvaluationValue[] result = new EvaluationValue[otherValues.Length];
				for (int i = 0; i < otherValues.Length; i++) {
					result[i] = ElementType.Cast(otherValues[i]) ?? throw new EvaluationTypeException($"Cannot cast element of type {otherValues[i].Type} to {ElementType}.");
				}
				return MakeArray(ElementType, result);
			}
			else {
				return null;
			}
		}

		// TODO Implement?
		/*
		public override EvaluationType? AddResult(EvaluationType right) => null;
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => null;
		public override EvaluationType? RAddResult(EvaluationType left) => null;
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => null;
		*/

		private EvaluationType? MulResultAny(EvaluationType other) {
			if (IntEvaluationType.IsIntegral(other)) {
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? PerformMul(EvaluationValue arrayValue, EvaluationValue intValue) {
			if (IntEvaluationType.TryGetInt(intValue, out int multiplier) && arrayValue.Type.Iteration(arrayValue)?.ToArray() is EvaluationValue[] values) {
				/*
				for (int v = 0; v < values.Length; v++) {
					values[v] = ElementType.Cast(values[v]) ?? throw new EvaluationTypeException($"Cannot convert element value of type {values[v].Type} to {ElementType}.");
				}
				*/

				int repeats = Math.Max(0, multiplier);
				List<EvaluationValue> result = new List<EvaluationValue>();
				for (int r = 0; r < repeats; r++) {
					result.AddRange(values);
				}
				return MakeArray(ElementType, values);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? MulResult(EvaluationType right) => MulResultAny(right);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => PerformMul(left, right);
		public override EvaluationType? RMulResult(EvaluationType left) => MulResultAny(left);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => PerformMul(right, left);

		// TODO Implement?
		/*
		public override EvaluationType? EqualResult(EvaluationType other) => null
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => null;

		public override EvaluationType? NotEqualResult(EvaluationType other) => null;
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => null;
		*/

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (IntEvaluationType.IsIntegral(index)) {
				return ElementType;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if (TryGetArray(subject, out Array? array) && IntEvaluationType.TryGetInt(index, out int indexVal)) {
				int indexFinal = EvaluationTypeHelpers.GetIndex(indexVal, array.Length);
				return new EvaluationValue(array.GetValue(indexFinal), ElementType);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IndexerSliceResult(EvaluationType start, EvaluationType end) {
			if (IntEvaluationType.IsIntegral(start) && IntEvaluationType.IsIntegral(end)) {
				return this;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? IndexerSlice(EvaluationValue subject, EvaluationValue start, EvaluationValue end) {
			if (TryGetArray(subject, out Array? array) && IntEvaluationType.TryGetInt(start, out int startVal) && IntEvaluationType.TryGetInt(end, out int endVal)) {
				EvaluationTypeHelpers.GetSliceIndexes(startVal, endVal, array.Length, out int startFinal, out int endFinal);

				EvaluationValue[] result = new EvaluationValue[endFinal - startFinal];
				for (int idx = startFinal; idx < endFinal; idx++) {
					result[idx - startFinal] = new EvaluationValue(array.GetValue(idx), ElementType);
				}

				return MakeArray(ElementType, result);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IterationResult() => ElementType;
		public override IEnumerable<EvaluationValue>? Iteration(EvaluationValue subject) {
			if (TryGetArray(subject, out Array? array)) {
				List<EvaluationValue> result = new List<EvaluationValue>();
				foreach (object? i in array) {
					result.Add(new EvaluationValue(i, ElementType));
				}
				return result;
			}
			else {
				return null;
			}
		}

		protected override bool EqualTypeData(EvaluationType other) {
			// This should be sufficient
			return DataType == other.DataType
				&& DisplayType == other.DisplayType;
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(DataType, DisplayType);
		}

	}

	public class TupleEvaluationType : CollectionEvaluationType {

		public override Type DataType { get; }
		public override Type DisplayType { get; }

		public int ElementCount { get; }

		internal TupleEvaluationType(EvaluationType elementType, int elementCount, IEnumerable<TypeField> fields) : base(elementType, fields.Append(MakeLengthField(elementCount)), Enumerable.Empty<TypeField>()) {
			this.DataType = TupleUtils.MakeGenericTupleType(this.ElementType.DataType, elementCount);
			this.DisplayType = TupleUtils.MakeGenericTupleType(this.ElementType.DisplayType, elementCount);

			this.ElementCount = elementCount;
		}

		private static TypeField MakeLengthField(int elementCount) {
			return new TypeField("length", IntEvaluationType.Instance, value => new EvaluationValue(elementCount, IntEvaluationType.Instance));
		}

		protected override string GetMyCollectionBrackets() {
			return $"[{ElementCount}]";
		}

		public static EvaluationValue MakeTuple(EvaluationType elementType, IList<EvaluationValue> values) {
			Type tupleType = TupleUtils.MakeGenericTupleType(elementType.DataType, values.Count);
			return new EvaluationValue(TupleUtils.CreateTuple(tupleType, values.Select(v => v.Value).ToArray()), elementType.MakeTuple(values.Count));
		}

		public override bool CanImplicitCastFrom(EvaluationType other, EvaluationTypeSystem typeSystem, [NotNullWhen(true)] out EvaluationType? lubType) {
			if (other is TupleEvaluationType otherTuple && ElementCount == otherTuple.ElementCount && typeSystem.TryGetLeastUpperBoundType(ElementType, otherTuple.ElementType, out EvaluationType? lubElement)) {
				lubType = lubElement.MakeTuple(ElementCount);
				return true;
			}
			else {
				lubType = null;
				return false;
			}
		}

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (other.Type.Iteration(other)?.ToArray() is EvaluationValue[] otherValues && otherValues.Length == ElementCount) {
				EvaluationValue[] result = new EvaluationValue[otherValues.Length];
				for (int i = 0; i < otherValues.Length; i++) {
					result[i] = ElementType.Cast(otherValues[i]) ?? throw new EvaluationTypeException($"Cannot cast element of type {otherValues[i].Type} to {ElementType}.");
				}
				return MakeTuple(ElementType, result);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (IntEvaluationType.IsIntegral(index)) {
				return ElementType;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if (IntEvaluationType.TryGetInt(index, out int indexVal)) {
				return new EvaluationValue(TupleUtils.Index(subject.Value!, indexVal), ElementType);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IterationResult() => ElementType;
		public override IEnumerable<EvaluationValue>? Iteration(EvaluationValue subject) {
			if (TupleUtils.IsTupleObject(subject.Value, out Type? tupleType)) {
				EvaluationValue[] values = new EvaluationValue[TupleUtils.GetTupleLength(tupleType)];
				for (int i = 0; i < values.Length; i++) {
					values[i] = new EvaluationValue(TupleUtils.Index(subject.Value!, i), ElementType);
				}
				return values;
			}
			else {
				return null;
			}
		}

		protected override bool EqualTypeData(EvaluationType other) {
			return other is TupleEvaluationType otherTuple
				&& ElementCount == otherTuple.ElementCount
				&& DataType == other.DataType
				&& DisplayType == other.DisplayType;
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(ElementCount, DataType, DisplayType);
		}

	}

	public class EnumEvaluationType : EvaluationType {

		public override string Name { get; }

		protected readonly Type SystemType;

		public override Type DataType { get; } = typeof(string);
		public override Type DisplayType => SystemType;

		public IReadOnlySet<string> EnumNames { get; }
		private readonly int enumNamesHash;

		// TODO This should have a static field for returning an array of the enum values

		internal EnumEvaluationType(string name, IEnumerable<string> enumValues, Type systemType) : base(Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>()) {
			this.Name = name;

			this.SystemType = systemType;

			this.EnumNames = new HashSet<string>(enumValues.Select(s => s.ToUpperInvariant()), StringComparer.InvariantCultureIgnoreCase);
			enumNamesHash = GetEnumNamesHashCode(this.EnumNames);
		}

		public static bool IsEnum(EvaluationType type) {
			return type is EnumEvaluationType;
		}

		public bool IsEnumValueDefined(string name) {
			return EnumNames?.Contains(name) ?? false;
		}

		protected bool TryGetEnumValue(EvaluationValue value, [NotNullWhen(true)] out string? enumValue) {
			if(value.Value is Enum enumData && enumData.GetType() == SystemType) {
				enumValue = enumData.ToString();
				return true;
			}
			else if(value.Value is string stringData) {
				enumValue = stringData;
				return true;
			}
			else {
				enumValue = null;
				return false;
			}
		}

		// No implicit casting for enums
		public override bool CanImplicitCastFrom(EvaluationType other) => false;
		public override EvaluationValue? Cast(EvaluationValue other) => null; // Should this convert string values if possible?

		private EvaluationType? EqualityResultAny(EvaluationType other) {
			if (other == this) { // Is the same as us
				return BoolEvaluationType.Instance;
			}
			else if(other == StringEvaluationType.Instance) { // Is a string value
				return BoolEvaluationType.Instance;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<string, string, bool> operation) {
			if (TryGetEnumValue(left, out string? leftVal) && TryGetEnumValue(right, out string? rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		private static bool EnumValuesEqual(string left, string right) {
			return string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase);
		}

		public override EvaluationType? EqualResult(EvaluationType other) => EqualityResultAny(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, EnumValuesEqual);

		public override EvaluationType? NotEqualResult(EvaluationType other) => EqualityResultAny(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, EnumValuesEqual);

		protected override bool EqualTypeData(EvaluationType other) {
			return other is EnumEvaluationType otherEnum
				&& Name == otherEnum.Name
				&& SystemType == otherEnum.SystemType
				&& EnumNames.SetEquals(otherEnum.EnumNames);
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(Name, SystemType, enumNamesHash);
		}

		public static int GetEnumNamesHashCode(IReadOnlySet<string> set) {
			unchecked {
				int hash = 0;
				foreach (string s in set) {
					int h = s?.GetHashCode() ?? 0;
					hash ^= (h ^ int.MinValue) * -0x61C88647; // 0x9E3779B9 as signed
				}
				return hash;
			}
		}

	}

	public class CustomEvaluationType : EvaluationType {
		
		public override string Name { get; }

		public override Type DataType { get; }
		public override Type DisplayType => DataType;

		public CustomEvaluationType(string name, IEnumerable<TypeField> fields, IEnumerable<TypeField> staticFields, Type systemType) : base(fields, staticFields) {
			this.Name = name;
			this.DataType = systemType;
		}

		// TODO This needs some implementation?
		public override bool CanImplicitCastFrom(EvaluationType other) => false;
		public override EvaluationValue? Cast(EvaluationValue other) => null;

		protected override bool EqualTypeData(EvaluationType other) {
			return Name == other.Name
				&& DataType == other.DataType;
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(Name, DataType);
		}

	}

	public class EvaluationTypeSystem {

		public static EvaluationTypeSystem Create(params EvaluationType[] providedTypes) {
			return Create((IEnumerable<EvaluationType>)providedTypes);
		}

		public static EvaluationTypeSystem Create(IEnumerable<EvaluationType> providedTypes) {
			EvaluationType[] types = providedTypes.Distinct().ToArray();
			SymmetricMatrix<bool> known = new SymmetricMatrix<bool>(types.Length);
			SymmetricMatrix<int> directConversions = new SymmetricMatrix<int>(types.Length);

			// Step 1: Find known type conversions
			for (int super = 0; super < types.Length; super++) {
				for (int sub = super; sub < types.Length; sub++) {
					directConversions[super, sub] = -1;
					known[super, sub] = false;

					if (super == sub) {
						// Trivial case
						directConversions[super, sub] = super;
						known[super, sub] = true;
					}
					else {
						if (types[super].CanImplicitCastFrom(types[sub])) {
							directConversions[super, sub] = super;
							known[super, sub] = true;
						}
						if (types[sub].CanImplicitCastFrom(types[super])) {
							if (!known[sub, super]) {
								directConversions[super, sub] = sub;
							}
							else {
								// Implicit type conversion is known to be ill-defined
								directConversions[super, sub] = -1;
							}
							known[super, sub] = true;
						}
					}
				}
			}

			void CollectColumn(int b, HashSet<int> collection) {
				for (int a = 0; a < types.Length; a++) {
					if (a != b && known[a, b] && directConversions[a, b] >= 0) {
						collection.Add(directConversions[a, b]);
					}
				}
			}

			// Step 2: Fill in unknown
			SymmetricMatrix<int> finalPromotions = directConversions.Copy();
			// Should this process be repeated until there are no changes?
			for (int a = 1; a < types.Length; a++) {
				for (int b = a; b < types.Length; b++) {
					if (!known[a, b]) {
						HashSet<int> allParents = new HashSet<int>();
						CollectColumn(a, allParents);
						CollectColumn(b, allParents);

						if (allParents.Count > 1) {
							foreach (int parent in allParents.ToList()) {
								foreach (int child in allParents.ToList()) {
									if (allParents.Contains(parent) && allParents.Contains(child)) {
										if (directConversions[parent, child] == parent) {
											allParents.Remove(parent);
										}
									}
								}
							}
						}

						if (allParents.Count == 1) {
							finalPromotions[a, b] = allParents.First();
							known[a, b] = true;
						}
						else {
							known[a, b] = true; // Either there are no parents, or too many
						}
					}
				}
			}

			Dictionary<(EvaluationType, EvaluationType), EvaluationType> promotions = new Dictionary<(EvaluationType, EvaluationType), EvaluationType>();
			for (int a = 0; a < types.Length; a++) {
				for (int b = 0; b < types.Length; b++) {
					if (finalPromotions[a,b] >= 0) {
						promotions[(types[a], types[b])] = types[finalPromotions[a, b]];
					}
				}
			}

			return new EvaluationTypeSystem(types, promotions);
		}

		private readonly EvaluationType[] types;
		public IEnumerable<EvaluationType> Types => types;

		private readonly Dictionary<(EvaluationType, EvaluationType),  EvaluationType> promotions;

		private EvaluationTypeSystem(EvaluationType[] types, Dictionary<(EvaluationType, EvaluationType), EvaluationType> promotions) {
			this.types = types;
			this.promotions = promotions;
		}

		public bool TryGetLeastUpperBoundType(EvaluationType a, EvaluationType b, [NotNullWhen(true)] out EvaluationType? lubType) {
			if (a.Equals(b)) {
				lubType = a;
				return true;
			}
			else if (promotions.TryGetValue((a, b), out EvaluationType? promoted)) {
				lubType = promoted;
				return true;
			}
			// Dislike that these have to be special-cased like this here, but it would need a generic system to solve, and we're not doing that just yet
			else if(a is CollectionEvaluationType aCollection && aCollection.CanImplicitCastFrom(b, this, out EvaluationType? aLubCollection)) {
				lubType = aLubCollection;
				return true;
			}
			else if(b is CollectionEvaluationType bCollection && bCollection.CanImplicitCastFrom(a, this, out EvaluationType? bLubCollection)) {
				lubType = bLubCollection;
				return true;
			}
			else if (a.CanImplicitCastFrom(b)) {
				lubType = a;
				return true;
			}
			else if (b.CanImplicitCastFrom(a)) {
				lubType = b;
				return true;
			}

			lubType = null;
			return false;
		}

		public bool TryGetLeastUpperBoundType(IEnumerable<EvaluationType> types, [NotNullWhen(true)] out EvaluationType? lubType) {
			EvaluationType? lub = null;
			foreach (EvaluationType type in types) {
				if (lub is null) {
					lub = type;
				}
				else if (TryGetLeastUpperBoundType(lub, type, out EvaluationType? newLub)) {
					lub = newLub;
				}
				else {
					lubType = null;
					return false;
				}
			}

			if (lub is not null) {
				lubType = lub;
				return true;
			}
			else {
				lubType = null;
				return false;
			}
		}

	}

	public class SymmetricMatrix<T> {

		public int Size { get; }

		private readonly T[] content;

		public T this[int i, int j] {
			get {
				return content[GetIndex(i, j)];
			}
			set {
				content[GetIndex(i, j)] = value;
			}
		}

		public SymmetricMatrix(int size) {
			this.Size = size;

			content = new T[(Size * (Size + 1)) / 2];
		}

		private int GetIndex(int i, int j) {
			if (i <= j) {
				return i * Size - (i - 1) * i / 2 + j - i;
			}
			else {
				return j * Size - (j - 1) * j / 2 + i - j;
			}
		}

		public SymmetricMatrix<T> Copy() {
			SymmetricMatrix<T> result = new SymmetricMatrix<T>(Size);
			for (int i = 0; i < content.Length; i++) {
				result.content[i] = content[i];
			}
			return result;
		}

	}

}
