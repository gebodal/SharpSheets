using SharpSheets.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Collections;
using SharpSheets.Parsing;

namespace SharpSheets.Evaluations {

	public static class EvaluationContextUtils {

		public static bool TryGetLeastUpperBoundType(this EvaluationContext context, IEnumerable<EvaluationType> types, [NotNullWhen(true)] out EvaluationType? lubType) {
			EvaluationType? lub = null;
			foreach (EvaluationType type in types) {
				if (lub is null) {
					lub = type;
				}
				else if (context.TryGetLeastUpperBoundType(lub, type, out EvaluationType? newLub)) {
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

		public static bool IsType(this EvaluationContext context, EvaluationName word) {
			return context.TryGetType(word, out _);
		}

		public static bool IsKeyword(this EvaluationContext context, EvaluationName word) {
			return Evaluation.IsLangKeyword(word) || context.IsType(word);
		}

	}

	public sealed class EvaluationContext {

		public static readonly EvaluationContext BasisContext = Create().Build();

		private bool initialised = false;

		private Dictionary<(EvaluationType, EvaluationType), EvaluationType> promotions = null!;

		private readonly List<EvaluationType> typesList = new List<EvaluationType>();
		private readonly Dictionary<Type, EvaluationType> types = new Dictionary<Type, EvaluationType>();
		private readonly Dictionary<EvaluationName, EvaluationType> typesByName = new Dictionary<EvaluationName, EvaluationType>();

		private EvaluationContext() { }

		public T GetType<T>() where T : EvaluationType {
			if(types.TryGetValue(typeof(T), out EvaluationType? type)) {
				return type as T ?? throw new EvaluationTypeException($"Invalid instance of {typeof(T).Name} registered for this context.");
			}
			else {
				throw new EvaluationTypeException($"No registered instance of {typeof(T).Name} in this context.");
			}
		}

		public EvaluationType GetType(EvaluationName name) {
			if (typesByName.TryGetValue(name, out EvaluationType? type)) {
				return type;
			}
			else {
				throw new EvaluationTypeException($"No type with name \"{name}\" in this context.");
			}
		}

		public EvaluationType GetSystemType<T>() {
			if (types.TryGetValue(typeof(T), out EvaluationType? type)) {
				return type;
			}
			else {
				throw new EvaluationTypeException($"No registered evaluation type for system type {typeof(T).Name} in this context.");
			}
		}

		private EvaluationContext SetType<T>(T type) where T : EvaluationType {
			return SetType(typeof(T), type);
		}

		private EvaluationContext SetSystemType<T>(EvaluationType type) {
			return SetType(typeof(T), type);
		}

		private EvaluationContext SetDataType<T, S>(T type) where T : SingleDataType<S> where S : notnull {
			SetType<T>(type);
			types.Add(typeof(S), type);
			return this;
		}

		private EvaluationContext SetType(Type systemType, EvaluationType type) {
			if (initialised) { throw new InvalidOperationException($"Attempting to edit an already initialised context."); }
			typesList.Add(type);
			types.Add(systemType, type);
			typesByName.Add(type.Name, type);
			return this;
		}

		public bool TryGetType<T>([NotNullWhen(true)] out T? type) where T : EvaluationType {
			if (types.TryGetValue(typeof(T), out EvaluationType? foundType) && foundType is T match) {
				type = match;
				return true;
			}
			else {
				type = null;
				return false;
			}
		}

		public bool TryGetType(EvaluationName name, [NotNullWhen(true)] out EvaluationType? type) {
			return typesByName.TryGetValue(name, out type);
		}

		public bool TryGetSystemType<T>([NotNullWhen(true)] out EvaluationType? type) {
			if (types.TryGetValue(typeof(T), out EvaluationType? foundType)) {
				type = foundType;
				return true;
			}
			else {
				type = null;
				return false;
			}
		}

		public bool TryGetSystemType(Type systemType, [NotNullWhen(true)] out EvaluationType? type) {
			if (types.TryGetValue(systemType, out EvaluationType? foundType)) {
				type = foundType;
				return true;
			}
			else {
				type = null;
				return false;
			}
		}

		public EvaluationValue MakeValue<T>(object? value) where T : EvaluationType {
			return new EvaluationValue(value, GetType<T>());
		}

		public EvaluationValue MakeValue<S>(S value) {
			return new EvaluationValue(value, GetSystemType<S>());
		}

		private void Initialise() {
			promotions = FindPromotions(typesList);
			initialised = true;
		}

		/*
		public static EvaluationContext Build(Action<EvaluationContext> registration) {
			EvaluationContext context = new EvaluationContext();

			context.SetType<MetaEvaluationType>(new MetaEvaluationType(context));
			context.SetType<IntEvaluationType>(new IntEvaluationType(context));
			context.SetType<UIntEvaluationType>(new UIntEvaluationType(context));
			context.SetType<FloatEvaluationType>(new FloatEvaluationType(context));
			context.SetType<UFloatEvaluationType>(new UFloatEvaluationType(context));
			context.SetType<BoolEvaluationType>(new BoolEvaluationType(context));
			context.SetType<StringEvaluationType>(new StringEvaluationType(context));

			registration(context);
			context.Initialise();
			return context;
		}
		*/

		public static Builder Create() {
			EvaluationContext context = new EvaluationContext();

			context.SetType<MetaEvaluationType>(new MetaEvaluationType(context));
			context.SetDataType<IntEvaluationType, int>(new IntEvaluationType(context));
			context.SetDataType<UIntEvaluationType, uint>(new UIntEvaluationType(context));
			context.SetDataType<FloatEvaluationType, float>(new FloatEvaluationType(context));
			context.SetDataType<UFloatEvaluationType, UFloat>(new UFloatEvaluationType(context));
			context.SetDataType<BoolEvaluationType, bool>(new BoolEvaluationType(context));
			context.SetDataType<StringEvaluationType, string>(new StringEvaluationType(context));

			return new Builder(context);
		}

		public class Builder {

			private readonly EvaluationContext context;
			private bool built;

			internal Builder(EvaluationContext context) {
				this.context = context;
				this.built = false;
			}

			public T SetType<T>(Func<EvaluationContext, T> typeBuilder) where T : EvaluationType {
				if (built) { throw new InvalidOperationException("Cannot adjust already-built context builder."); }
				T type = typeBuilder(context);
				context.SetType<T>(type);
				return type;
			}

			public T SetSystemType<S, T>(Func<EvaluationContext, T> typeBuilder) where T : EvaluationType {
				if (built) { throw new InvalidOperationException("Cannot adjust already-built context builder."); }
				T type = typeBuilder(context);
				context.SetSystemType<S>(type);
				return type;
			}

			public T SetDataType<T, S>(Func<EvaluationContext, T> typeBuilder) where T : SingleDataType<S> where S : notnull {
				if (built) { throw new InvalidOperationException("Cannot adjust already-built context builder."); }
				T type = typeBuilder(context);
				context.SetDataType<T, S>(type);
				return type;
			}

			public T SetType<T>(Type systemType, Func<EvaluationContext, T> typeBuilder) where T : EvaluationType {
				if (built) { throw new InvalidOperationException("Cannot adjust already-built context builder."); }
				T type = typeBuilder(context);
				context.SetType(systemType, type);
				return type;
			}

			public bool IsDefined<T>() where T : EvaluationType {
				return context.TryGetType<T>(out _);
			}

			public bool IsDefined(string name) {
				return context.TryGetType(name, out _);
			}

			public bool IsSystemTypeDefined<S>() {
				return context.TryGetSystemType<S>(out _);
			}

			public EvaluationContext Build() {
				built = true;
				context.Initialise();
				return context;
			}

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
			else if (a is CollectionEvaluationType aCollection && aCollection.CanImplicitCastFrom(b, out EvaluationType? aLubCollection)) {
				lubType = aLubCollection;
				return true;
			}
			else if (b is CollectionEvaluationType bCollection && bCollection.CanImplicitCastFrom(a, out EvaluationType? bLubCollection)) {
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

		private static Dictionary<(EvaluationType, EvaluationType), EvaluationType> FindPromotions(IEnumerable<EvaluationType> providedTypes) {
			EvaluationType[] types = providedTypes.Distinct().ToArray();
			SymmetricMatrix<bool> known = new SymmetricMatrix<bool>(types.Length);
			SymmetricMatrix<int> directConversions = new SymmetricMatrix<int>(types.Length);

			//Console.Write("Types: ");
			//for(int i=0; i<types.Length; i++) {
			//	Console.Write($"{i}: {types[i].Name}, ");
			//}
			//Console.WriteLine();
			//int maxLen = types.Max(t => t.Name.Length);

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

			//void PrintState(SymmetricMatrix<int> knownPromotions) {
			//	Console.WriteLine("\n");

			//	Console.Write(string.Format($"{{0,{maxLen + 2}}}", ""));
			//	for (int i = 0; i < types.Length; i++) {
			//		Console.Write(string.Format($"{{0,{maxLen + 2}}}", types[i].Name));
			//	}
			//	Console.WriteLine();
			//	for (int j = 0; j < types.Length; j++) {
			//		Console.Write(string.Format($"{{0,{maxLen + 2}}}", types[j].Name));
			//		for (int i = 0; i < types.Length; i++) {
			//			int knownConv = knownPromotions[i, j];
			//			Console.Write(string.Format($"{{0,{maxLen + 2}}}", knownConv >= 0 ? types[knownConv].Name : ""));
			//		}
			//		Console.WriteLine();
			//	}
			//}

			//PrintState(directConversions);

			HashSet<int> CollectColumn(int b) {
				HashSet<int> columnTypes = new HashSet<int>();
				for (int a = 0; a < types.Length; a++) {
					if (a != b && known[a, b] && directConversions[a, b] >= 0 && directConversions[a, b] != b) {
						columnTypes.Add(directConversions[a, b]);
					}
				}
				return columnTypes;
			}

			// Step 2: Fill in unknown
			SymmetricMatrix<int> finalPromotions = directConversions.Copy();
			// Should this process be repeated until there are no changes?
			for (int a = 0; a < types.Length; a++) {
				for (int b = a; b < types.Length; b++) {
					if (!known[a, b]) {
						//HashSet<int> allParents = new HashSet<int>();
						//CollectColumn(a, allParents);
						//CollectColumn(b, allParents);
						HashSet<int> allParents = CollectColumn(a);
						allParents.IntersectWith(CollectColumn(b));

						//Console.WriteLine($"Consider {types[a].Name}/{types[b].Name}: {string.Join(", ", allParents.Select(p => types[p].Name))}");

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

			//PrintState(finalPromotions);

			Dictionary<(EvaluationType, EvaluationType), EvaluationType> promotions = new Dictionary<(EvaluationType, EvaluationType), EvaluationType>();
			for (int a = 0; a < types.Length; a++) {
				for (int b = 0; b < types.Length; b++) {
					if (finalPromotions[a, b] >= 0) {
						promotions[(types[a], types[b])] = types[finalPromotions[a, b]];
					}
				}
			}

			return promotions;
		}

		public static bool Equals(EvaluationContext a, EvaluationContext b) {
			return ReferenceEquals(a, b);
		}

		public override bool Equals(object? obj) {
			return obj is EvaluationContext other && Equals(this, other);
		}

		public override int GetHashCode() {
			return base.GetHashCode(); // TODO Will this work?
		}

	}

	public readonly struct EvaluationValue {
		public readonly object? Value;
		public readonly EvaluationType Type;

		public EvaluationValue(object? value, EvaluationType type) {
			Value = value;
			Type = type;
		}

		public override string ToString() {
			return (Value?.ToString() ?? "") + $" {{{Type}}}";
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

		public EvaluationContext Context { get; }

		public abstract string Name { get; }

		public abstract Type DataType { get; }
		public abstract Type DisplayType { get; }

		private readonly Dictionary<EvaluationName, TypeField> fields = new Dictionary<EvaluationName, TypeField>();
		public IEnumerable<EvaluationName> FieldNames { get { return fields.Keys; } }
		public IEnumerable<TypeField> Fields { get { return fields.Values; } }

		private readonly Dictionary<EvaluationName, TypeField> staticFields = new Dictionary<EvaluationName, TypeField>();
		public IEnumerable<EvaluationName> StaticFieldNames { get { return staticFields.Keys; } }
		public IEnumerable<TypeField> StaticFields { get { return staticFields.Values; } }

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

		public override string Name { get; } = "type";

		public override Type DataType { get; } = typeof(Type);
		public override Type DisplayType => DataType;

		public MetaEvaluationType(EvaluationContext context) : base(context) { }

		protected override object ParseValueData(string text, DirectoryPath source) {
			if (Context.TryGetType(text, out EvaluationType? type)) {
				return type;
			}
			else {
				throw new FormatException($"Unrecognized evaluation type name: \"{text}\".");
			}
		}

		protected override object? DefaultValueData() {
			return default(Type);
		}

		protected override bool EqualTypeData(EvaluationType other) {
			return Name == other.Name
				&& DataType == other.DataType;
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(Name, DataType);
		}
	}

	public abstract class SingleDataType<T> : EvaluationType where T : notnull {

		public sealed override Type DataType { get; } = typeof(T);
		public sealed override Type DisplayType => DataType;

		protected SingleDataType(EvaluationContext context) : base(context) { }

		protected sealed override bool EqualTypeData(EvaluationType other) {
			return Name == other.Name
				&& DataType == other.DataType;
		}

		protected sealed override int GetTypeHashCode() {
			return HashCode.Combine(Name, DataType);
		}

		/// <exception cref="FormatException"/>
		protected abstract T ParseValueDataSingle(string text, DirectoryPath source);
		protected override sealed object ParseValueData(string text, DirectoryPath source) {
			return ParseValueDataSingle(text, source);
		}

		protected override object? DefaultValueData() {
			return default(T);
		}
	}

	public sealed class FloatEvaluationType : SingleDataType<float> {

		public override string Name { get; } = "float";

		public FloatEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MINVALUE", this, t => new EvaluationValue(float.MinValue, this)));
			AddStaticField(new TypeField("MAXVALUE", this, t => new EvaluationValue(float.MaxValue, this)));
		}

		protected override float ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseFloat(text);
		}

		public static bool IsReal(EvaluationType type) {
			return type is FloatEvaluationType || UFloatEvaluationType.IsPositiveReal(type) || IntEvaluationType.IsIntegral(type);
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
			if (value.Type is FloatEvaluationType && value.Value is float floatVal) {
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
				return new EvaluationValue(value, this);
			}
			else {
				return null;
			}
		}

		private EvaluationValue? UnaryArithmetic(EvaluationValue operand, Func<float, float> operation) {
			if (TryGetFloat(operand, out float operandVal)) {
				return new EvaluationValue(operation(operandVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryArithmeticResult(EvaluationType other) {
			if (IsReal(other)) { // Another float-like
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperation(EvaluationValue left, EvaluationValue right, Func<float, float, float> operation) {
			if (TryGetFloat(left, out float leftVal) && TryGetFloat(right, out float rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperation(EvaluationValue left, EvaluationValue right, Func<float, float, bool> operation) {
			if (TryGetFloat(left, out float leftVal) && TryGetFloat(right, out float rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsReal(other)) { // Another float-like
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => this;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => +a);

		public override EvaluationType? NegResult() => this;
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => -a);

		public override EvaluationType? AddResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RAddResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a + b);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a + b);

		public override EvaluationType? SubResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RSubResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Sub(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a - b);
		public override EvaluationValue? RSub(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a - b);

		public override EvaluationType? MulResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RMulResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a * b);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a * b);

		public override EvaluationType? DivResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RDivResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a / b);
		public override EvaluationValue? RDiv(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a / b);

		public override EvaluationType? ModResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RModResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Mod(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a % b);
		public override EvaluationValue? RMod(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a % b);

		public override EvaluationType? PowResult(EvaluationType right) => BinaryArithmeticResult(right);
		public override EvaluationType? RPowResult(EvaluationType left) => BinaryArithmeticResult(left);
		public override EvaluationValue? Pow(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => (float)Math.Pow(a, b));
		public override EvaluationValue? RPow(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => (float)Math.Pow(a, b));

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a < b);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a <= b);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a > b);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a >= b);

		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a == b);

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryOperation(left, right, (a, b) => a != b);
	}

	public sealed class UFloatEvaluationType : SingleDataType<UFloat> {

		public override string Name { get; } = "ufloat";

		public UFloatEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MAXVALUE", this, t => new EvaluationValue(UFloat.MaxValue, this)));
		}

		protected override UFloat ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseUFloat(text);
		}

		public static bool IsPositiveReal(EvaluationType type) {
			return type is UFloatEvaluationType || UIntEvaluationType.IsPositiveIntegral(type);
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
			if (value.Type is UFloatEvaluationType && value.Value is UFloat uFloatVal) {
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
				return new EvaluationValue(value, this);
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

		private EvaluationType? BinaryArithmeticResult(EvaluationType other, bool closed) {
			if (IsPositiveReal(other)) {
				return closed ? this : Context.GetType<FloatEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationClosed(EvaluationValue left, EvaluationValue right, Func<UFloat, UFloat, UFloat> operation) {
			if (TryGetUFloat(left, out UFloat leftVal) && TryGetUFloat(right, out UFloat rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationNotClosed(EvaluationValue left, EvaluationValue right, Func<UFloat, UFloat, float> operation) {
			if (TryGetUFloat(left, out UFloat leftVal) && TryGetUFloat(right, out UFloat rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<FloatEvaluationType>());
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsPositiveReal(other)) { // Another UFloat-like
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<UFloat, UFloat, bool> operation) {
			if (TryGetUFloat(left, out UFloat leftVal) && TryGetUFloat(right, out UFloat rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => this;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => a, this);

		public override EvaluationType? NegResult() => Context.GetType<FloatEvaluationType>();
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => -a.Value, Context.GetType<FloatEvaluationType>());

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

	public sealed class IntEvaluationType : SingleDataType<int> {

		public override string Name { get; } = "int";

		public IntEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MINVALUE", this, t => new EvaluationValue(int.MinValue, this)));
			AddStaticField(new TypeField("MAXVALUE", this, t => new EvaluationValue(int.MaxValue, this)));
		}

		protected override int ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseInt(text);
		}

		public static bool IsIntegral(EvaluationType type) {
			return type is IntEvaluationType || UIntEvaluationType.IsPositiveIntegral(type);
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
			if (value.Type is IntEvaluationType && value.Value is int intVal) {
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
				return new EvaluationValue(value, this);
			}
			else {
				return null;
			}
		}

		private EvaluationValue? UnaryArithmetic(EvaluationValue operand, Func<int, int> operation) {
			if (TryGetInt(operand, out int operandVal)) {
				return new EvaluationValue(operation(operandVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryArithmeticResult(EvaluationType other) {
			if (IsIntegral(other)) { // Another int-like
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationAny(EvaluationValue left, EvaluationValue right, Func<int, int, int> operation) {
			if (TryGetInt(left, out int leftVal) && TryGetInt(right, out int rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsIntegral(other)) { // Another float-like
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<int, int, bool> operation) {
			if (TryGetInt(left, out int leftVal) && TryGetInt(right, out int rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => this;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => +a);

		public override EvaluationType? NegResult() => this;
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

	public sealed class UIntEvaluationType : SingleDataType<uint> {

		public override string Name { get; } = "uint";

		public UIntEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MAXVALUE", this, t => new EvaluationValue(uint.MaxValue, this)));
		}

		protected override uint ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseUInt(text);
		}

		public static bool IsPositiveIntegral(EvaluationType type) {
			return type is UIntEvaluationType || BoolEvaluationType.IsBool(type);
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
			if (value.Type is UIntEvaluationType && value.Value is uint uintVal) {
				number = uintVal;
				return true;
			}
			else if(BoolEvaluationType.TryGetBool(value, out bool boolean)) {
				number = boolean ? 1u : 0u;
				return true;
			}

			number = 0U;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsPositiveIntegral(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetUInt(other, out uint value)) {
				return new EvaluationValue(value, this);
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

		private EvaluationType? BinaryArithmeticResult(EvaluationType other, bool closed) {
			if (IsPositiveIntegral(other)) {
				return closed ? this : Context.GetType<IntEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationClosed(EvaluationValue left, EvaluationValue right, Func<uint, uint, uint> operation) {
			if (TryGetUInt(left, out uint leftVal) && TryGetUInt(right, out uint rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryOperationNotClosed(EvaluationValue left, EvaluationValue right, Func<uint, uint, int> operation) {
			if (TryGetUInt(left, out uint leftVal) && TryGetUInt(right, out uint rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<IntEvaluationType>());
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsPositiveIntegral(other)) { // Another uint-like
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<uint, uint, bool> operation) {
			if (TryGetUInt(left, out uint leftVal) && TryGetUInt(right, out uint rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? PosResult() => this;
		public override EvaluationValue? Pos(EvaluationValue operand) => UnaryArithmetic(operand, (a) => a, this);

		public override EvaluationType? NegResult() => Context.GetType<IntEvaluationType>();
		public override EvaluationValue? Neg(EvaluationValue operand) => UnaryArithmetic(operand, (a) => (int)(-a), Context.GetType<IntEvaluationType>());

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

	public sealed class BoolEvaluationType : SingleDataType<bool> {

		public override string Name { get; } = "bool";

		public BoolEvaluationType(EvaluationContext context) : base(context) { }

		protected override bool ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseBool(text);
		}

		public static bool IsBool(EvaluationType other) {
			return other is BoolEvaluationType;
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
			if (value.Type is BoolEvaluationType && value.Value is bool boolVal) {
				boolean = boolVal;
				return true;
			}

			boolean = false;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsBool(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetBool(other, out bool value)) {
				return new EvaluationValue(value, this);
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

		private EvaluationValue? UnaryOperationAny(EvaluationValue operand, Func<bool, bool> operation) {
			if (TryGetBool(operand, out bool value)) {
				return new EvaluationValue(operation(value), this);
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsBool(other)) { // Another bool
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<bool, bool, bool> operation) {
			if (TryGetBool(left, out bool leftVal) && TryGetBool(right, out bool rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), this);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? InvertResult() => this;
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

	public sealed class StringEvaluationType : SingleDataType<string> {

		public override string Name { get; } = "str";

		public StringEvaluationType(EvaluationContext context) : base(context) {
			AddField(new TypeField("length", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(((string)value.Value!).Length, value.Type.Context.GetType<IntEvaluationType>())));
		}

		protected override string ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseString(text);
		}

		public static bool IsString(EvaluationType other) {
			return other is StringEvaluationType;
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
			if (value.Type is StringEvaluationType && value.Value is string stringVal) {
				str = stringVal;
				return true;
			}

			str = null;
			return false;
		}

		public override bool CanImplicitCastFrom(EvaluationType other) => IsString(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetString(other, out string? value)) {
				return new EvaluationValue(value, this);
			}
			else {
				return null;
			}
		}

		private EvaluationType? AddResultAny(EvaluationType other) {
			if (IsString(other)) { // Another string
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? AddAny(EvaluationValue left, EvaluationValue right) {
			if (TryGetString(left, out string? leftVal) && TryGetString(right, out string? rightVal)) {
				return new EvaluationValue(leftVal + rightVal, this);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? AddResult(EvaluationType right) => AddResultAny(right);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => AddAny(left, right);
		public override EvaluationType? RAddResult(EvaluationType left) => AddResultAny(left);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => AddAny(left, right);

		private EvaluationType? MulResultAny(EvaluationType other) {
			if (IntEvaluationType.IsIntegral(other)) { // An int-like
				return this;
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
				return new EvaluationValue(RepeatString(stringVal, intVal), this);
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) {
			if (IntEvaluationType.TryGetInt(left, out int intVal) && TryGetString(right, out string? stringVal)) {
				return new EvaluationValue(RepeatString(stringVal, intVal), this);
			}
			else {
				return null;
			}
		}

		private EvaluationType? BinaryComparisonResult(EvaluationType other) {
			if (IsString(other)) { // Another string
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<string, string, bool> operation) {
			if (TryGetString(left, out string? leftVal) && TryGetString(right, out string? rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? LessThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => string.CompareOrdinal(a, b) < 0);

		public override EvaluationType? LessThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? LessThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => string.CompareOrdinal(a, b) <= 0);

		public override EvaluationType? GreaterThanResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThan(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => string.CompareOrdinal(a, b) > 0);

		public override EvaluationType? GreaterThanEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? GreaterThanEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => string.CompareOrdinal(a, b) >= 0);


		public override EvaluationType? EqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => a.Equals(b));

		public override EvaluationType? NotEqualResult(EvaluationType other) => BinaryComparisonResult(other);
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => BinaryComparisonAny(left, right, (a, b) => !a.Equals(b));

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (IntEvaluationType.IsIntegral(index)) {
				return this;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if(TryGetString(subject, out string? subjectVal) && IntEvaluationType.TryGetInt(index, out int indexVal)) {
				int indexFinal = EvaluationTypeHelpers.GetIndex(indexVal, subjectVal.Length);
				return new EvaluationValue(subjectVal[indexFinal].ToString(), this);
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
			if (TryGetString(subject, out string? subjectVal) && IntEvaluationType.TryGetInt(start, out int startVal) && IntEvaluationType.TryGetInt(end, out int endVal)) {
				EvaluationTypeHelpers.GetSliceIndexes(startVal, endVal, subjectVal.Length, out int startFinal, out int endFinal);
				return new EvaluationValue(subjectVal[startFinal..endFinal].ToString(), this);
			}
			else {
				return null;
			}
		}

	}

	public abstract class CollectionEvaluationType : EvaluationType {

		public EvaluationType ElementType { get; }

		protected CollectionEvaluationType(EvaluationContext context, EvaluationType elementType) : base(context) {
			this.ElementType = elementType;
		}

		public override string Name {
			get {
				StringBuilder sb = new StringBuilder();
				EvaluationType baseType = this;
				while(baseType is CollectionEvaluationType subCollection) {
					baseType = subCollection.ElementType;
					sb.Append(subCollection.GetMyCollectionBrackets());
				}
				return baseType.Name + sb.ToString();

				//return ElementType.Name + GetCollectionBrackets();
			}
		}

		/*
		private string GetCollectionBrackets() {
			return GetMyCollectionBrackets() + (ElementType is CollectionEvaluationType subCollection ? subCollection.GetCollectionBrackets() : "");
		}
		*/

		protected abstract string GetMyCollectionBrackets();

		public override bool CanImplicitCastFrom(EvaluationType other) => base.CanImplicitCastFrom(other); // All collection casting requires more information
		public abstract bool CanImplicitCastFrom(EvaluationType other, [NotNullWhen(true)] out EvaluationType? lubType);

	}

	public abstract class SequentialCollectionEvaluationType : CollectionEvaluationType {

		protected SequentialCollectionEvaluationType(EvaluationContext context, EvaluationType elementType) : base(context, elementType) { }

		protected int GetRank() {
			return 1 + (ElementType is SequentialCollectionEvaluationType sequentialElem ? sequentialElem.GetRank() : 0);
		}

	}

	public class ArrayEvaluationType : SequentialCollectionEvaluationType {

		public override Type DataType { get; }
		public override Type DisplayType { get; }

		internal ArrayEvaluationType(EvaluationContext context, EvaluationType elementType) : base(context, elementType) {
			this.DataType = this.ElementType.DataType.MakeArrayType(1);
			this.DisplayType = this.ElementType.DisplayType.MakeArrayType(1);

			AddField(new TypeField("length", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(((Array)value.Value!).Length, value.Type.Context.GetType<IntEvaluationType>())));
		}

		protected override string GetMyCollectionBrackets() {
			return "[]";
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			int rank = GetRank();

			if (rank > ValueParsers.MaxArrayOrTupleRank) { throw new FormatException($"Cannot parse array with rank {rank} (max {ValueParsers.MaxArrayOrTupleRank})."); }

			return MakeArray(ElementType, StringParsing.SplitOnUnescaped(text, ValueParsers.GetArrayDelimiter(rank)).Select(v => ElementType.ParseValue(v.Trim(), source)).ToArray()).Value!;
		}

		protected override object? DefaultValueData() {
			return MakeArray(ElementType, Array.Empty<EvaluationValue>());
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
			return MakeArrayFromData(elementType, values.Select(v => v.Value).ToArray());
		}

		public static EvaluationValue MakeArrayFromData(EvaluationType elementType, object?[] values) {
			Array final = Array.CreateInstance(elementType.DataType, values.Length);
			Array.Copy(values, final, final.Length);
			return new EvaluationValue(final, elementType.MakeArray());
		}

		public override bool CanImplicitCastFrom(EvaluationType other, [NotNullWhen(true)] out EvaluationType? lubType) {
			if(other is SequentialCollectionEvaluationType collection && Context.TryGetLeastUpperBoundType(ElementType, collection.ElementType, out EvaluationType? lubElement)) {
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
				return MakeArray(ElementType, result);
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

	public class TupleEvaluationType : SequentialCollectionEvaluationType {

		public override Type DataType { get; }
		public override Type DisplayType { get; }

		public int ElementCount { get; }

		internal TupleEvaluationType(EvaluationContext context, EvaluationType elementType, int elementCount) : base(context, elementType) {
			this.DataType = TupleUtils.MakeGenericTupleType(this.ElementType.DataType, elementCount);
			this.DisplayType = TupleUtils.MakeGenericTupleType(this.ElementType.DisplayType, elementCount);

			this.ElementCount = elementCount;

			AddField(new TypeField("length", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(elementCount, value.Type.Context.GetType<IntEvaluationType>())));
		}

		protected override string GetMyCollectionBrackets() {
			return $"[{ElementCount}]";
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			int rank = GetRank();

			if (rank > ValueParsers.MaxArrayOrTupleRank) { throw new FormatException($"Cannot parse tuple with rank {rank} (max {ValueParsers.MaxArrayOrTupleRank})."); }

			string[] parts = StringParsing.SplitOnUnescaped(text, ValueParsers.GetArrayDelimiter(rank));

			if (parts.Length != ElementCount) { throw new FormatException($"Invalid number of elements for tuple of length {ElementCount} (got {parts.Length})."); }

			return MakeTuple(ElementType, parts.Select(v => ElementType.ParseValue(v.Trim(), source)).ToArray()).Value!;
		}

		protected override object? DefaultValueData() {
			EvaluationValue[] defaults = new EvaluationValue[ElementCount];
			EvaluationValue defaultElemValue = ElementType.DefaultValue();
			for (int i = 0; i < ElementCount; i++) {
				defaults[i] = defaultElemValue;
			}
			return MakeTuple(ElementType, defaults);
		}

		public static EvaluationValue MakeTuple(EvaluationType elementType, IList<EvaluationValue> values) {
			Type tupleType = TupleUtils.MakeGenericTupleType(elementType.DataType, values.Count);
			return new EvaluationValue(TupleUtils.CreateTuple(tupleType, values.Select(v => v.Value).ToArray()), elementType.MakeTuple(values.Count));
		}

		public override bool CanImplicitCastFrom(EvaluationType other, [NotNullWhen(true)] out EvaluationType? lubType) {
			if (other is TupleEvaluationType otherTuple && ElementCount == otherTuple.ElementCount && Context.TryGetLeastUpperBoundType(ElementType, otherTuple.ElementType, out EvaluationType? lubElement)) {
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

	public class DictionaryEvaluationType : CollectionEvaluationType {

		public EvaluationType KeyType { get; }

		public override Type DataType { get; }
		public override Type DisplayType { get; }

		internal DictionaryEvaluationType(EvaluationContext context, EvaluationType keyType, EvaluationType elementType) : base(context, elementType) {
			this.KeyType = keyType;

			this.DataType = MakeDictionaryType(this.KeyType.DataType, this.ElementType.DataType);
			this.DisplayType = MakeDictionaryType(this.KeyType.DisplayType, this.ElementType.DisplayType);

			AddField(new TypeField("keys", KeyType.MakeArray(), value => ArrayEvaluationType.MakeArrayFromData(KeyType, ((IDictionary)value.Value!).Keys.Cast<object>().ToArray())));
			AddField(new TypeField("values", ElementType.MakeArray(), value => ArrayEvaluationType.MakeArrayFromData(ElementType, ((IDictionary)value.Value!).Values.Cast<object?>().ToArray())));
			AddField(new TypeField("count", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(((IDictionary)value.Value!).Count, value.Type.Context.GetType<IntEvaluationType>())));
		}

		protected override string GetMyCollectionBrackets() {
			return $"[{KeyType.Name}]";
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			string[] parts = Escaping.SplitUnescaped(text, ',');

			List<(EvaluationValue key, EvaluationValue value)> entries = new List<(EvaluationValue, EvaluationValue)>();

			foreach (string part in parts) {
				string[] keyValue = Escaping.SplitUnescaped(part, ':', 2, StringSplitOptions.TrimEntries);

				if (keyValue.Length != 2) { throw new FormatException("Invalid dictionary entry literal value."); }

				EvaluationValue key = KeyType.ParseValue(keyValue[0], source);
				EvaluationValue value = ElementType.ParseValue(keyValue[1], source);

				entries.Add((key, value));
			}

			return MakeDictionary(KeyType, ElementType, entries).Value!;
		}

		protected override object? DefaultValueData() {
			return MakeDictionary(KeyType, ElementType, Array.Empty<(EvaluationValue, EvaluationValue)>());
		}

		private static bool TryGetDictionary(EvaluationValue value, [MaybeNullWhen(false)] out IDictionary dict, [MaybeNullWhen(false)] out DictionaryEvaluationType dictType) {
			if (value.Type is DictionaryEvaluationType dictEvalType && value.Value is IDictionary dictObj) {
				dict = dictObj;
				dictType = dictEvalType;
				return true;
			}
			else {
				dict = null;
				dictType = null;
				return false;
			}
		}

		private static Type MakeDictionaryType(Type keyType, Type valueType) {
			return typeof(OrderedDictionary<,>).MakeGenericType(keyType, valueType);
		}

		public static EvaluationValue MakeDictionary(EvaluationType keyType, EvaluationType elementType, IList<(EvaluationValue key, EvaluationValue value)> entries) {
			EvaluationType dictType = new DictionaryEvaluationType(keyType.Context, keyType, elementType);
			IDictionary dict = (IDictionary)(Activator.CreateInstance(dictType.DataType)!);

			foreach((EvaluationValue key, EvaluationValue value) in entries) {
				dict.Add(
					key.Value ?? throw new EvaluationCalculationException("Invalid null value provided for dictionary key."),
					value.Value
					);
			}

			return new EvaluationValue(dict, dictType);
		}

		public override bool CanImplicitCastFrom(EvaluationType other, [NotNullWhen(true)] out EvaluationType? lubType) {
			if(other is DictionaryEvaluationType dict
				&& Context.TryGetLeastUpperBoundType(KeyType, dict.KeyType, out EvaluationType? lubKey)
				&& Context.TryGetLeastUpperBoundType(ElementType, dict.ElementType, out EvaluationType? lubElement)
				) {

				lubType = new DictionaryEvaluationType(Context, lubKey, lubElement);
				return true;
			}
			else {
				lubType = null;
				return false;
			}
		}

		public override EvaluationValue? Cast(EvaluationValue other) {
			if(TryGetDictionary(other, out IDictionary? dict, out DictionaryEvaluationType? dictType)) {
				List<(EvaluationValue key, EvaluationValue value)> result = new List<(EvaluationValue key, EvaluationValue value)>();

				foreach(object key in dict.Keys) {
					result.Add((
						KeyType.Cast(new EvaluationValue(key, dictType.KeyType)) ?? throw new EvaluationTypeException($"Cannot cast dictionary key of type {dictType.KeyType} to {KeyType}."),
						ElementType.Cast(new EvaluationValue(dict[key], dictType.ElementType)) ?? throw new EvaluationTypeException($"Cannot cast dictionary value of type {dictType.ElementType} to {ElementType}.")
						));
				}

				return MakeDictionary(KeyType, ElementType, result);
			}
			else {
				return null;
			}
		}

		// TODO Implement?
		/*
		public override EvaluationType? EqualResult(EvaluationType other) => null
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) => null;

		public override EvaluationType? NotEqualResult(EvaluationType other) => null;
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) => null;
		*/

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (KeyType.CanImplicitCastFrom(index)) {
				return ElementType;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if (TryGetDictionary(subject, out IDictionary? dict, out _) && KeyType.Cast(index) is EvaluationValue indexCast) {
				object key = indexCast.Value ?? throw new EvaluationCalculationException("Invalid null dictionary key.");
				object? value = dict.Contains(key) ? dict[key] : throw new EvaluationCalculationException($"No value for key in dictionary: {key}");
				return new EvaluationValue(value, ElementType);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IterationResult() => KeyType;
		public override IEnumerable<EvaluationValue>? Iteration(EvaluationValue subject) {
			if (TryGetDictionary(subject, out IDictionary? dict, out _)) {
				List<EvaluationValue> result = new List<EvaluationValue>();
				foreach (object key in dict.Keys) {
					result.Add(new EvaluationValue(key, KeyType));
				}
				return result;
			}
			else {
				return null;
			}
		}

		protected override bool EqualTypeData(EvaluationType other) {
			return other is DictionaryEvaluationType otherDict
				&& otherDict.KeyType == KeyType
				&& otherDict.ElementType == ElementType;
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(DataType, DisplayType);
		}

	}

	public class EnumEvaluationType : EvaluationType {

		public override string Name { get; }

		protected readonly Type SystemType;

		public override Type DataType { get; } = typeof(string);
		public override Type DisplayType => SystemType;

		private readonly IReadOnlyDictionary<string, string> enumNames;
		private readonly string defaultEnumName;
		private readonly int enumNamesHash;

		public IEnumerable<string> EnumNames => enumNames.Keys;

		internal EnumEvaluationType(EvaluationContext context, string name, IEnumerable<string> enumValues, Type systemType) : base(context) {
			this.Name = name;

			this.SystemType = systemType;

			string[] enumNamesVals = enumValues.Select(s => s.ToUpperInvariant()).Distinct().ToArray();

			if(enumNamesVals.Length == 0) { throw new ArgumentException("Enum evaluation type must be provided with at least one enum value name.", nameof(enumValues)); }

			this.defaultEnumName = enumNamesVals[0];
			this.enumNames = enumNamesVals.ToDictionary(s => s, StringComparer.InvariantCultureIgnoreCase);
			enumNamesHash = GetEnumNamesHashCode(this.enumNames.Keys);

			foreach (string enumName in this.enumNames.Keys.Order()) {
				AddStaticField(new TypeField(enumName, this, t => new EvaluationValue(enumName, this)));
			}
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			if (enumNames.TryGetValue(text.Trim(), out string? foundName)) {
				return foundName;
			}
			else {
				throw new FormatException($"Invalid literal for enum of type {Name}: \"{text}\".");
			}
		}

		protected override object? DefaultValueData() {
			return defaultEnumName;
		}

		public static EnumEvaluationType FromSystemType<T>(EvaluationContext context) where T : Enum {
			return new EnumEvaluationType(context, typeof(T).Name, Enum.GetNames(typeof(T)), typeof(T));
		}
		public static EnumEvaluationType FromSystemType(EvaluationContext context, Type type) {
			if (!type.IsEnum) { throw new ArgumentException("System type must be an enum type.", nameof(type)); }
			return new EnumEvaluationType(context, type.Name, Enum.GetNames(type), type);
		}

		public static bool IsEnum(EvaluationType type) {
			return type is EnumEvaluationType;
		}

		public static bool IsEnum(EvaluationType type, [NotNullWhen(true)] out EnumEvaluationType? enumType) {
			enumType = type as EnumEvaluationType;
			return enumType is not null;
		}

		public bool IsEnumValueDefined(string name) {
			return enumNames.ContainsKey(name);
		}

		public static bool TryGetEnumValue<T>(EvaluationValue value, [NotNullWhen(true)] out T? enumValue) where T : struct {
			if (value.Value is T enumData) {
				enumValue = enumData;
				return true;
			}
			else if (value.Value is string stringData && Enum.TryParse(stringData, true, out T parsed)) {
				enumValue = parsed;
				return true;
			}
			else {
				enumValue = default;
				return false;
			}
		}

		public bool TryGetEnumValue(EvaluationValue value, [NotNullWhen(true)] out string? enumValue) {
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

		public bool TryGetEnumValue(EvaluationValue value, [NotNullWhen(true)] out Enum? enumValue) {
			if (value.Value is Enum enumData && enumData.GetType() == SystemType) {
				enumValue = enumData;
				return true;
			}
			else if (value.Value is string stringData && Enum.TryParse(SystemType, stringData, true, out object? parsed) && parsed is Enum parsedEnum) {
				enumValue = parsedEnum;
				return true;
			}
			else {
				enumValue = null;
				return false;
			}
		}

		// No implicit casting for enums
		//public override bool CanImplicitCastFrom(EvaluationType other) => false;
		//public override EvaluationValue? Cast(EvaluationValue other) => null; // Should this convert string values if possible?

		private EvaluationType? EqualityResultAny(EvaluationType other) {
			if (other == this) { // Is the same as us
				return Context.GetType<BoolEvaluationType>();
			}
			else if(StringEvaluationType.IsString(other)) { // Is a string value
				return Context.GetType<BoolEvaluationType>();
			}
			else {
				return null;
			}
		}

		private EvaluationValue? BinaryComparisonAny(EvaluationValue left, EvaluationValue right, Func<string, string, bool> operation) {
			if (TryGetEnumValue(left, out string? leftVal) && TryGetEnumValue(right, out string? rightVal)) {
				return new EvaluationValue(operation(leftVal, rightVal), Context.GetType<BoolEvaluationType>());
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
				&& EnumNames.SequenceEqual(otherEnum.EnumNames);
		}

		protected override int GetTypeHashCode() {
			return HashCode.Combine(Name, SystemType, enumNamesHash);
		}

		public static int GetEnumNamesHashCode(IEnumerable<string> names) {
			unchecked {
				int hash = 0;
				foreach (string s in names.Order()) {
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

		private readonly Func<string, DirectoryPath, object>? Parser;
		private readonly object? defaultValue;

		public CustomEvaluationType(EvaluationContext context, string name, IEnumerable<TypeField> fields, IEnumerable<TypeField> staticFields, Type systemType, Func<string, DirectoryPath, object>? parser, object? defaultValue) : base(context) {
			this.Name = name;
			this.DataType = systemType;

			this.Parser = parser;
			this.defaultValue = defaultValue;

			foreach (TypeField field in fields) { AddField(field); }
			foreach (TypeField staticField in staticFields) { AddStaticField(staticField); }
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			if(Parser is null) { throw new FormatException($"Cannot parse data of type {Name}."); }

			return Parser(text, source);
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
