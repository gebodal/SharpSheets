using SharpSheets.Utilities;
using SharpSheets.Evaluations.Nodes;

namespace SharpSheets.Evaluations.Types {

	public class TypeField {

		public EvaluationName Name { get; }
		public string Description { get; }
		public EvaluationType Type { get; }
		private readonly Func<EvaluationValue, EvaluationValue> evaluator;

		public TypeField(EvaluationName name, string description, EvaluationType type, Func<EvaluationValue, EvaluationValue> evaluator) {
			Name = name;
			Description = description;
			Type = type;
			this.evaluator = evaluator;
		}

		public EvaluationValue GetValue(EvaluationValue subject) {
			return evaluator(subject);
		}

	}

	public abstract class EvaluationType : IEquatable<EvaluationType> {

		public EvaluationContext Context { get; }

		public abstract string Name { get; }

		public abstract Type DataType { get; }

		private readonly Dictionary<EvaluationName, TypeField> fields = new Dictionary<EvaluationName, TypeField>();
		public IEnumerable<EvaluationName> FieldNames { get { return fields.Keys; } }
		public IEnumerable<TypeField> Fields { get { return fields.Values; } }

		private readonly Dictionary<EvaluationName, TypeField> staticFields = new Dictionary<EvaluationName, TypeField>();
		public IEnumerable<EvaluationName> StaticFieldNames { get { return staticFields.Keys; } }
		public IEnumerable<TypeField> StaticFields { get { return staticFields.Values; } }

		private readonly Dictionary<EvaluationName, IMethod> methods = new Dictionary<EvaluationName, IMethod>();
		public IEnumerable<EvaluationName> MethodNames { get { return methods.Keys; } }
		public IEnumerable<IMethod> Methods { get { return methods.Values; } }

		private readonly Dictionary<EvaluationName, IMethod> staticMethods = new Dictionary<EvaluationName, IMethod>();
		public IEnumerable<EvaluationName> StaticMethodNames { get { return staticMethods.Keys; } }
		public IEnumerable<IMethod> StaticMethods { get { return staticMethods.Values; } }

		protected EvaluationType(EvaluationContext context) {
			this.Context = context;
		}

		public EvaluationValue MakeValue(object? value) {
			return new EvaluationValue(value, this);
		}

		/// <exception cref="FormatException"/>
		protected abstract object ParseValueData(string text, DirectoryPath source);

		/// <exception cref="FormatException"/>
		public EvaluationValue ParseValue(string text, DirectoryPath source) {
			return MakeValue(ParseValueData(text, source));
		}

		public abstract string GetEvaluationString(EvaluationValue value);

		/// <exception cref="EvaluationCalculationException"/>
		protected abstract object? DefaultValueData();

		/// <exception cref="EvaluationCalculationException"/>
		public EvaluationValue DefaultValue() {
			return MakeValue(DefaultValueData());
		}

		protected void AddField(TypeField field) {
			fields.Add(field.Name, field);
		}
		protected void AddStaticField(TypeField field) {
			staticFields.Add(field.Name, field);
		}

		protected void AddMethod(IMethod method) {
			methods.Add(method.Name, method);
		}
		protected void AddStaticMethod(IMethod method) {
			staticMethods.Add(method.Name, method);
		}

		public static bool SharedContext(EvaluationType a, EvaluationType b) {
			return ReferenceEquals(a.Context, b.Context);
		}
		public bool SharedContext(EvaluationType other) {
			return SharedContext(this, other);
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
		public virtual bool CanImplicitCastFrom(EvaluationType other) {
			return this == other;
		}
		public virtual EvaluationValue? Cast(EvaluationValue other) {
			//Console.WriteLine($"Casting {other.Type} to {this}: {other} ({this == other.Type})");
			return this == other.Type ? other : null;
		}

		public bool IsField(EvaluationName field) {
			return fields.ContainsKey(field);
		}
		public TypeField? GetField(EvaluationName field) {
			return fields.GetValueOrFallback(field, null);
		}

		public bool IsStaticField(EvaluationName field) {
			return staticFields.ContainsKey(field);
		}
		public TypeField? GetStaticField(EvaluationName field) {
			return staticFields.GetValueOrFallback(field, null);
		}

		public bool IsMethod(EvaluationName method) {
			return methods.ContainsKey(method);
		}
		public IMethod? GetMethod(EvaluationName method) {
			return methods.GetValueOrFallback(method, null);
		}

		public bool IsStaticMethod(EvaluationName method) {
			return staticMethods.ContainsKey(method);
		}
		public IMethod? GetStaticMethod(EvaluationName method) {
			return staticMethods.GetValueOrFallback(method, null);
		}

		public virtual IEnvironmentFunction? GetTypeFunction() => null;

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
			return new ArrayEvaluationType(Context, this);
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
			return new TupleEvaluationType(Context, this, size);
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
			return ReferenceEquals(Context, other.Context) && EqualTypeData(other) && FieldsEqual(other);
		}

		public override bool Equals(object? obj) {
			return Equals(obj as EvaluationType);
		}

		public override int GetHashCode() {
			// TODO This should include a hash based on the Context identity
			// System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode( object obj )  ????
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

		private static EvaluationType? GetPromoted(EvaluationType a, EvaluationType b) {
			// TODO Should we check contexts match?
			return a.Context.TryGetLeastUpperBoundType(a, b, out EvaluationType? promoted) ? promoted : null;
		}

		public static EvaluationType? AddResult(EvaluationType left, EvaluationType right) => left.AddResult(right) ?? right.RAddResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.AddResult(promoted) : null);
		public static EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => left.Type.Add(left, right) ?? right.Type.RAdd(left, right) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.Add(left, right) : null);

		public static EvaluationType? SubResult(EvaluationType left, EvaluationType right) => left.SubResult(right) ?? right.RSubResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.SubResult(promoted) : null);
		public static EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => left.Type.Sub(left, right) ?? right.Type.RSub(left, right) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.Sub(left, right) : null);

		public static EvaluationType? MulResult(EvaluationType left, EvaluationType right) => left.MulResult(right) ?? right.RMulResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.MulResult(promoted) : null);
		public static EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => left.Type.Mul(left, right) ?? right.Type.RMul(left, right) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.Mul(left, right) : null);

		public static EvaluationType? DivResult(EvaluationType left, EvaluationType right) => left.DivResult(right) ?? right.RDivResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.DivResult(promoted) : null);
		public static EvaluationValue? PerformDiv(EvaluationValue left, EvaluationValue right) => left.Type.Div(left, right) ?? right.Type.RDiv(left, right) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.Div(left, right) : null);

		public static EvaluationType? ModResult(EvaluationType left, EvaluationType right) => left.ModResult(right) ?? right.RModResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.ModResult(promoted) : null);
		public static EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => left.Type.Mod(left, right) ?? right.Type.RMod(left, right) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.Mod(left, right) : null);

		public static EvaluationType? PowResult(EvaluationType left, EvaluationType right) => left.PowResult(right) ?? right.RPowResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.PowResult(promoted) : null);
		public static EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => left.Type.Pow(left, right) ?? right.Type.RPow(left, right) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.Pow(left, right) : null);

		public static EvaluationType? LessThanResult(EvaluationType left, EvaluationType right) => left.LessThanResult(right) ?? right.GreaterThanResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.LessThanResult(promoted) : null);
		public static EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => left.Type.LessThan(left, right) ?? right.Type.GreaterThan(right, left) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.LessThan(left, right) : null);

		public static EvaluationType? LessThanEqualResult(EvaluationType left, EvaluationType right) => left.LessThanEqualResult(right) ?? right.GreaterThanEqualResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.LessThanEqualResult(promoted) : null);
		public static EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => left.Type.LessThanEqual(left, right) ?? right.Type.GreaterThanEqual(right, left) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.LessThanEqual(left, right) : null);

		public static EvaluationType? GreaterThanResult(EvaluationType left, EvaluationType right) => left.GreaterThanResult(right) ?? right.LessThanResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.GreaterThanResult(promoted) : null);
		public static EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => left.Type.GreaterThan(left, right) ?? right.Type.LessThan(right, left) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.GreaterThan(left, right) : null);

		public static EvaluationType? GreaterThanEqualResult(EvaluationType left, EvaluationType right) => left.GreaterThanEqualResult(right) ?? right.LessThanEqualResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.GreaterThanEqualResult(promoted) : null);
		public static EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => left.Type.GreaterThanEqual(left, right) ?? right.Type.LessThanEqual(right, left) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.GreaterThanEqual(left, right) : null);

		public static EvaluationType? EqualResult(EvaluationType left, EvaluationType right) => left.EqualResult(right) ?? right.EqualResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.EqualResult(promoted) : null);
		public static EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => left.Type.Equal(left, right) ?? right.Type.Equal(right, left) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.Equal(left, right) : null);

		public static EvaluationType? NotEqualResult(EvaluationType left, EvaluationType right) => left.NotEqualResult(right) ?? right.NotEqualResult(left) ?? (GetPromoted(left, right) is EvaluationType promoted ? promoted.NotEqualResult(promoted) : null);
		public static EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => left.Type.NotEqual(left, right) ?? right.Type.NotEqual(right, left) ?? (GetPromoted(left.Type, right.Type) is EvaluationType promoted ? promoted.NotEqual(left, right) : null);

		public static EvaluationType? IterationResult(EvaluationType type) => type.IterationResult();
		public static IEnumerable<EvaluationValue>? Iteration(EvaluationValue value) => value.Type.Iteration(value);

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

	public class CustomEvaluationType<T> : EvaluationType {
		
		public override string Name { get; }

		public override Type DataType { get; }

		private readonly Func<string, DirectoryPath, T>? Parser;
		private readonly Func<T, string>? Serializer;
		private readonly T defaultValue;

		public CustomEvaluationType(EvaluationContext context, string name, IEnumerable<TypeField> fields, IEnumerable<TypeField> staticFields, Func<string, DirectoryPath, T>? parser, Func<T, string>? serializer, T defaultValue) : base(context) {
			this.Name = name;
			this.DataType = typeof(T);

			this.Parser = parser;
			this.Serializer = serializer;
			this.defaultValue = defaultValue;

			foreach (TypeField field in fields) { AddField(field); }
			foreach (TypeField staticField in staticFields) { AddStaticField(staticField); }
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			if(Parser is null) { throw new FormatException($"Cannot parse data of type {Name}."); }

			return Parser(text, source)!;
		}

		public override string GetEvaluationString(EvaluationValue value) {
			if (Serializer is not null && value.Value is T customSerializerValue) {
				return Serializer(customSerializerValue);
			}
			else if (value.Value is T customValue) {
				return customValue?.ToString() ?? Name;
			}
			else {
				throw new EvaluationCalculationException($"Invalid data type for {Name}.");
			}
		}

		protected override object? DefaultValueData() {
			return defaultValue;
		}

		// TODO This needs some implementation?
		public override bool CanImplicitCastFrom(EvaluationType other) => base.CanImplicitCastFrom(other);
		public override EvaluationValue? Cast(EvaluationValue other) => base.Cast(other);

		protected override bool EqualTypeData(EvaluationType other) {
			return Name == other.Name
				&& DataType == other.DataType;
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(Name, DataType);
		}

	}

}
