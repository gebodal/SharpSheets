using System.Collections;
using SharpSheets.Evaluations;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Utilities;

namespace SharpSheets.Documentation {

	public enum DisplayTypeStructure { Single, Entried, Numbered }

	public sealed class DisplayType : IEquatable<DisplayType> {
		public Type? SystemType { get; }
		public EvaluationType? EvaluationType { get; }

		public string Name => SystemType?.Name ?? EvaluationType!.Name;

		public bool IsEnum => SystemType?.IsEnum ?? EvaluationType is EnumEvaluationType;
		public bool IsBool => SystemType is not null ? (SystemType == typeof(bool)) : (EvaluationType is BoolEvaluationType);
		
		public DisplayTypeStructure Structure { get; }

		public bool IsSingle => Structure == DisplayTypeStructure.Single;
		public bool IsEntried => Structure == DisplayTypeStructure.Entried;
		public bool IsNumbered => Structure == DisplayTypeStructure.Numbered;

		public string FullName => SystemType?.FullName ?? EvaluationType!.Name;

		private DisplayType(Type? systemType, EvaluationType? evaluationType, DisplayTypeStructure structure) {
			if (systemType is null && evaluationType is null) { throw new InvalidOperationException($"{nameof(DisplayType)} must be given at least one non-null type argument."); }
			SystemType = systemType ?? GetEvalBase(evaluationType);
			EvaluationType = evaluationType;
			Structure = structure;
		}

		public DisplayType AsSingle() => new DisplayType(SystemType, EvaluationType, DisplayTypeStructure.Single);

		private static readonly Dictionary<Type, DisplayType> systemTypeRegistry = new Dictionary<Type, DisplayType>();

		private static DisplayType FromSystem(Type type, DisplayTypeStructure structure = DisplayTypeStructure.Single) {
			if (systemTypeRegistry.TryGetValue(type, out DisplayType? existing)) {
				return existing;
			}
			else {
				DisplayType newInstance = new DisplayType(type, null, structure);
				systemTypeRegistry[type] = newInstance;
				return newInstance;
			}
		}

		public static DisplayType FromSystem<T>(DisplayTypeStructure structure = DisplayTypeStructure.Single) {
			return FromSystem(typeof(T), structure);
		}

		public static DisplayType FromEvaluation(EvaluationType evaluationType, DisplayTypeStructure structure = DisplayTypeStructure.Single) {
			return new DisplayType(null, evaluationType, structure);
		}

		public static DisplayType Create(Type systemType, EvaluationType? evaluationType = null, DisplayTypeStructure structure = DisplayTypeStructure.Single) {
			if (evaluationType is null) {
				return FromSystem(systemType, structure);
			}
			else {
				return new DisplayType(systemType, evaluationType, structure);
			}
		}

		private static bool IsEvalBase<T>(EvaluationType evalType) where T : notnull {
			if (evalType is SingleDataType<T>) {
				return true;
			}
			else if (evalType is EnumEvaluationType enumEval) {
				return enumEval.SystemType == typeof(T);
			}
			else {
				return false;
			}
		}

		private static bool IsSingleDataType(EvaluationType? type, [MaybeNullWhen(false)] out Type dataType) {
			if (type is null) {
				dataType = null;
				return false;
			}

			for (Type? t = type.GetType(); t != null; t = t.BaseType) {
				if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SingleDataType<>)) {
					dataType = type.DataType;
					return true;
				}
			}

			dataType = null;
			return false;
		}

		private static Type? GetEvalBase(EvaluationType? evalType) {
			if (evalType is EnumEvaluationType enumEval) {
				return enumEval.SystemType;
			}
			else if (IsSingleDataType(evalType, out Type? dataType)) {
				return dataType;
			}
			else {
				return null;
			}
		}

		public bool IsBase<T>() where T : notnull {
			if (SystemType is not null) {
				return SystemType == typeof(T);
			}
			else {
				return IsEvalBase<T>(EvaluationType!);
			}
		}

		public bool IsSimple<T>() where T : notnull {
			if (Structure != DisplayTypeStructure.Single) { return false; }
			else { return IsBase<T>(); }
		}

		public Type? GetBase() {
			if (SystemType is not null) {
				return SystemType;
			}
			else if (GetEvalBase(EvaluationType) is Type evalBase) {
				return evalBase;
			}
			else {
				return null;
			}
		}

		public Type? GetSingle() {
			if (Structure != DisplayTypeStructure.Single) { return null; }
			else { return GetBase(); }
		}

		public bool IsSequence<T>(out int length) where T : notnull {
			if (Structure != DisplayTypeStructure.Single) {
				length = -1;
				return false;
			}

			if (SystemType is not null) {
				length = -1;
				return SystemType.IsArray && SystemType.GetElementType() == typeof(T);
			}
			else if (EvaluationType is ArrayEvaluationType arrayEval) {
				length = -1;
				return IsEvalBase<T>(arrayEval.ElementType);
			}
			else if (EvaluationType is TupleEvaluationType tupleEval) {
				length = tupleEval.ElementCount;
				return IsEvalBase<T>(tupleEval.ElementType);
			}
			else {
				length = -1;
				return false;
			}
		}

		public bool IsSequence([MaybeNullWhen(false)] out DisplayType elementType, out int length) {
			if(Structure != DisplayTypeStructure.Single) {
				elementType = null;
				length = -1;
				return false;
			}

			if (SystemType is not null) {
				if (SystemType.IsArray && SystemType.GetElementType() is Type systemElemType) {
					elementType = Create(systemElemType);
					length = -1;
					return true;
				}
				else if (TupleUtils.IsTupleType(SystemType) && TupleUtils.GetTupleTypes(SystemType) is Type[] tupleTypes && tupleTypes.Distinct().Count() == 1) {
					elementType = Create(tupleTypes[0]);
					length = TupleUtils.GetTupleLength(SystemType);
					return true;
				}
			}
			else if (EvaluationType is ArrayEvaluationType arrayEval) {
				length = -1;
				elementType = FromEvaluation(arrayEval.ElementType);
				return true;
			}
			else if (EvaluationType is TupleEvaluationType tupleEval) {
				length = tupleEval.ElementCount;
				elementType = FromEvaluation(tupleEval.ElementType);
				return true;
			}

			length = -1;
			elementType = null;
			return false;
		}

		public bool IsTuple(out int length) {
			if (Structure != DisplayTypeStructure.Single) {
				length = -1;
				return false;
			}

			if (SystemType is not null) {
				if (TupleUtils.IsTupleType(SystemType)) {
					length = TupleUtils.GetTupleLength(SystemType);
					return true;
				}
			}
			else if (EvaluationType is TupleEvaluationType tupleEval) {
				length = tupleEval.ElementCount;
				return true;
			}

			length = -1;
			return false;
		}

		public bool IsDictionary([MaybeNullWhen(false)] out DisplayType keyType, [MaybeNullWhen(false)] out DisplayType valueType) {
			if (Structure != DisplayTypeStructure.Single) {
				keyType = null;
				valueType = null;
				return false;
			}

			if (EvaluationType is DictionaryEvaluationType dictEvalType) {
				keyType = FromEvaluation(dictEvalType.KeyType);
				valueType = FromEvaluation(dictEvalType.ElementType);
				return true;
			}
			else if (GetSingle() is Type dictType && dictType.IsAssignableTo(typeof(IDictionary)) && Utilities.TypeUtils.TryGetGenericTypeDefinition(dictType) is Type genericType) {
				if (genericType == typeof(IDictionary<,>) || genericType == typeof(Dictionary<,>) || genericType == typeof(OrderedDictionary<,>) || genericType == typeof(SortedDictionary<,>)) {
					Type[] args = dictType.GetGenericArguments();
					keyType = Create(args[0]);
					valueType = Create(args[1]);
					return true;
				}
			}

			keyType = null;
			valueType = null;
			return false;
		}

		public bool IsAssignableTo(Type targetType) {
			if (IsSingle && GetBase() is Type baseType) {
				return baseType.IsAssignableTo(targetType);
			}
			else {
				return false;
			}
		}

		public bool Equals(DisplayType? other) {
			if (other is null) { return false; }
			return (SystemType is null ? other.SystemType is null : SystemType.Equals(other.SystemType))
				&& (EvaluationType is null ? other.EvaluationType is null : EvaluationType.Equals(other.EvaluationType))
				&& Structure == other.Structure;
		}

		public static bool Equals(DisplayType? a, DisplayType? b) {
			if (a is null || b is null) { return a is null && b is null; }

			return a.Equals(b);
		}

		public override bool Equals(object? obj) {
			return Equals(obj as DisplayType);
		}

		public static bool operator==(DisplayType? a, DisplayType? b) {
			return Equals(a, b);
		}
		public static bool operator !=(DisplayType? a, DisplayType? b) {
			return !Equals(a, b);
		}

		public override int GetHashCode() {
			return HashCode.Combine(SystemType, EvaluationType, IsEntried, IsNumbered);
		}
	}

}