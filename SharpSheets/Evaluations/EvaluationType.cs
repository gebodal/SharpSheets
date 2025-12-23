using SharpSheets.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Collections;
using SharpSheets.Parsing;
using System.Collections.Specialized;
using System.Globalization;
using SharpSheets.Evaluations.Nodes;
using System.Text.RegularExpressions;

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

		public IEnumerable<EvaluationType> GetRegisteredTypes() => typesList;

		public T GetType<T>() where T : EvaluationType {
			if(types.TryGetValue(typeof(T), out EvaluationType? type)) {
				return type as T ?? throw new EvaluationTypeException($"Invalid instance of {typeof(T).Name} registered for this context.");
			}
			else {
				throw new EvaluationTypeException($"No registered instance of {typeof(T).Name} in this context.");
			}
		}

		public EvaluationValue GetValue<T>(object? value) where T : EvaluationType {
			return GetType<T>().MakeValue(value);
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

		public string ToEvaluationString() {
			return Type.GetEvaluationString(this);
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

	public sealed class MetaEvaluationType : EvaluationType {

		public override string Name { get; } = "type";

		public override Type DataType { get; } = typeof(EvaluationType);

		public MetaEvaluationType(EvaluationContext context) : base(context) { }

		protected override object ParseValueData(string text, DirectoryPath source) {
			if (Context.TryGetType(text, out EvaluationType? type)) {
				return type;
			}
			else {
				throw new FormatException($"Unrecognized evaluation type name: \"{text}\".");
			}
		}

		public override string GetEvaluationString(EvaluationValue value) {
			return value.Value is EvaluationType evalType ? evalType.Name : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
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

		protected abstract string GetEvaluationSingleString(T value);
		public override string GetEvaluationString(EvaluationValue value) {
			return value.Value is T data ? GetEvaluationSingleString(data) : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
		}

		protected abstract T DefaultValueDataSingle();

		protected sealed override object? DefaultValueData() {
			return DefaultValueDataSingle();
		}
	}

	public sealed partial class FloatEvaluationType : SingleDataType<float> {

		public override string Name { get; } = "float";

		public FloatEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MINVALUE", this, t => new EvaluationValue(float.MinValue, this)));
			AddStaticField(new TypeField("MAXVALUE", this, t => new EvaluationValue(float.MaxValue, this)));

			AddMethod(new NumericClampMethod(this));
		}

		protected override float ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseFloat(text);
		}

		protected override string GetEvaluationSingleString(float value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override float DefaultValueDataSingle() {
			return 0f;
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

		public override IEnvironmentFunction GetTypeFunction() => FloatCastFunction.Instance;

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

		public partial class FloatCastFunction : AbstractFunction {

			public static readonly FloatCastFunction Instance = new FloatCastFunction();
			private FloatCastFunction() { }

			public override EvaluationName Name { get; } = "float";
			public override string? Description { get; } = "Converts the argument into a floating point number. Strings will be parsed, bools and integers will be cast, and floating point numbers will be unchanged.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null))
				);
	}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType argType = args[0].GetReturnType();
				if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType)) {
					return context.GetType<FloatEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to float.");
				}
			}

			private static EvaluationValue MakeResult(float result, EvaluationContext context) {
				return new EvaluationValue(result, context.GetType<FloatEvaluationType>());
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue a = args[0].Evaluate(environment);

				if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
					return MakeResult(aFloat, environment.Context);
				}
				else if (IntEvaluationType.TryGetInt(a, out int aInt)) {
					return MakeResult((float)aInt, environment.Context);
				}
				else if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
					return MakeResult(aBool ? 1f : 0f, environment.Context);
				}
				else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
					return MakeResult(Parse(aStr), environment.Context);
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {a.Type} to float.");
				}
			}

			[GeneratedRegex(@"^\s*[\-\+]?\s*([0-9]+(\.[0-9]*)?|\.[0-9]+)\s*$")]
			private static partial Regex FloatRegex();
			[GeneratedRegex(@"^\s*(?<numer>[\-\+]?\s*([0-9]+(\.[0-9]*)?|\.[0-9]+))\s*\/\s*(?<denom>([0-9]+(\.[0-9]*)?|\.[0-9]+))\s*")]
			private static partial Regex FracRegex();
			/// <summary></summary>
			/// <exception cref="EvaluationCalculationException"></exception>
			private static float Parse(string str) {
				try {
					Match match;
					if (FloatRegex().IsMatch(str)) {
						return float.Parse(str.Replace(" ", ""));
					}
					else if ((match = FracRegex().Match(str)).Success) {
						float numer = float.Parse(match.Groups["numer"].Value.Replace(" ", ""));
						float denom = float.Parse(match.Groups["denom"].Value.Replace(" ", ""));
						return numer / denom;
					}
				}
				catch (FormatException) { }

				throw new EvaluationCalculationException($"Provided string is not a valid float: \"{str}\"");
			}

			/// <summary></summary>
			/// <exception cref="EvaluationCalculationException"></exception>
			/// <exception cref="EvaluationTypeException"></exception>
			/// <exception cref="EvaluationProcessingException"></exception>
			public static EvaluationNode MakeFloatCastNode(EvaluationNode argument) {
				EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, argument.Context);
				node.SetArguments(argument);
				return node.Simplify();
			}
		}
	}

	public sealed class UFloatEvaluationType : SingleDataType<UFloat> {

		public override string Name { get; } = "ufloat";

		public UFloatEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MAXVALUE", this, t => new EvaluationValue(UFloat.MaxValue, this)));

			AddMethod(new NumericClampMethod(this));
		}

		protected override UFloat ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseUFloat(text);
		}

		protected override string GetEvaluationSingleString(UFloat value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override UFloat DefaultValueDataSingle() {
			return UFloat.Zero;
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

			AddMethod(new NumericClampMethod(this));
		}

		protected override int ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseInt(text);
		}

		protected override string GetEvaluationSingleString(int value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override int DefaultValueDataSingle() {
			return 0;
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

		public override IEnvironmentFunction? GetTypeFunction() => IntCastFunction.Instance;

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

		public class IntCastFunction : AbstractFunction {

			public static readonly IntCastFunction Instance = new IntCastFunction();
			private IntCastFunction() { }

			public override EvaluationName Name { get; } = "int";
			public override string? Description { get; } = "Converts the argument into an integer. Strings will be parsed, floats will be rounded down, bools cast, and integers unchanged.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null))
				);
	}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType argType = args[0].GetReturnType();
				if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType)) {
					return context.GetType<IntEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to integer.");
				}
			}

			private static EvaluationValue MakeResult(int result, EvaluationContext context) {
				return new EvaluationValue(result, context.GetType<IntEvaluationType>());
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue a = args[0].Evaluate(environment);

				if (IntEvaluationType.TryGetInt(a, out int aInt)) {
					return MakeResult(aInt, environment.Context);
				}
				else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
					return MakeResult((int)aFloat, environment.Context);
				}
				else if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
					return MakeResult(aBool ? 1 : 0, environment.Context);
				}
				else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
					if (int.TryParse(aStr, out int parsed)) {
						return MakeResult(parsed, environment.Context);
					}
					else {
						throw new EvaluationCalculationException($"Provided string is not a valid int: \"{aStr}\"");
					}
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {a.Type} to integer.");
				}
			}

			public static EvaluationNode MakeIntCastNode(EvaluationNode argument) {
				EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, argument.Context);
				node.SetArguments(argument);
				return node.Simplify();
			}
		}
	}

	public sealed class UIntEvaluationType : SingleDataType<uint> {

		public override string Name { get; } = "uint";

		public UIntEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("MAXVALUE", this, t => new EvaluationValue(uint.MaxValue, this)));

			AddMethod(new NumericClampMethod(this));
		}

		protected override uint ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseUInt(text);
		}

		protected override string GetEvaluationSingleString(uint value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override uint DefaultValueDataSingle() {
			return 0U;
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

		protected override string GetEvaluationSingleString(bool value) {
			return value ? "true" : "false";
		}

		protected override bool DefaultValueDataSingle() {
			return false;
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

		public override IEnvironmentFunction GetTypeFunction() => BoolCastFunction.Instance;

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

		public class BoolCastFunction : AbstractFunction {

			public static readonly BoolCastFunction Instance = new BoolCastFunction();
			private BoolCastFunction() { }

			public override EvaluationName Name { get; } = "bool";
			public override string? Description { get; } = "Converts the argument into a boolean value. Strings will be parsed, floats and integers will be compared to zero, and bools will be unchanged.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null))
				);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType argType = args[0].GetReturnType();
				if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType)) {
					return context.GetType<BoolEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to boolean.");
				}
			}

			private static EvaluationValue MakeResult(bool result, EvaluationContext context) {
				return new EvaluationValue(result, context.GetType<BoolEvaluationType>());
	}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue a = args[0].Evaluate(environment);

				if (BoolEvaluationType.TryGetBool(a, out bool aBool)) {
					return MakeResult(aBool, environment.Context);
				}
				else if (IntEvaluationType.TryGetInt(a, out int aInt)) {
					return MakeResult(aInt != 0, environment.Context);
				}
				else if (FloatEvaluationType.TryGetFloat(a, out float aFloat)) {
					return MakeResult(aFloat != 0f, environment.Context);
				}
				else if (StringEvaluationType.TryGetString(a, out string? aStr)) {
					if (bool.TryParse(aStr, out bool parsed)) {
						return MakeResult(parsed, environment.Context);
					}
					else {
						throw new EvaluationCalculationException($"Provided string is not a valid boolean: \"{aStr}\"");
					}
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {a.Type} to boolean.");
				}
			}
		}

	}

	public sealed class StringEvaluationType : SingleDataType<string> {

		public override string Name { get; } = "str";

		public StringEvaluationType(EvaluationContext context) : base(context) {
			AddField(new TypeField("length", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(((string)value.Value!).Length, value.Type.Context.GetType<IntEvaluationType>())));
			AddMethod(new StringRepeatMethod(this));
			AddMethod(new StringJoinMethod(this));
			AddMethod(new StringContainsMethod(this));
		}

		protected override string ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseString(text);
		}

		protected override string GetEvaluationSingleString(string value) {
			//return $"\"{value}\"";
			return value;
		}

		protected override string DefaultValueDataSingle() {
			return string.Empty;
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

		public override IEnvironmentFunction GetTypeFunction() => StringCastFunction.Instance;

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

		public class StringCastFunction : AbstractFunction {

			public static readonly StringCastFunction Instance = new StringCastFunction();
			private StringCastFunction() { }

			public override EvaluationName Name { get; } = "str";
			public override string? Description { get; } = "Converts the argument into a string value. Real values will be serialised as decimal numbers, bools as true/false, enums as the value name, and strings will be unchanged.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<FloatEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<IntEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<BoolEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("value", context.GetType<StringEvaluationType>(), null)),
					new EnvironmentFunctionArgList(new EnvironmentFunctionArg("enumVal", null, null))
				);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType argType = args[0].GetReturnType();
				if (FloatEvaluationType.IsReal(argType) || BoolEvaluationType.IsBool(argType) || StringEvaluationType.IsString(argType) || EnumEvaluationType.IsEnum(argType)) {
					return context.GetType<StringEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"Cannot cast value of type {argType} to string.");
				}
			}

			private static EvaluationValue MakeResult(string result, EvaluationContext context) {
				return new EvaluationValue(result, context.GetType<StringEvaluationType>());
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue a = args[0].Evaluate(environment);

				if (FloatEvaluationType.IsReal(a.Type) || BoolEvaluationType.IsBool(a.Type) || StringEvaluationType.IsString(a.Type)) {
					return MakeResult(a.Value?.ToString() ?? "", environment.Context); // Sensible fallback?
				}
				else if (EnumEvaluationType.IsEnum(a.Type)) {
					if (a.Value is null) {
						throw new EvaluationCalculationException("Cannot convert null enum value to string.");
					}
					return MakeResult((a.ToString() ?? throw new EvaluationCalculationException("Could not resolve enum name.")).ToUpperInvariant(), environment.Context);
				}
				else {
					throw new EvaluationCalculationException($"Cannot cast variable of type {a.Type} to string.");
				}
			}

			/// <summary></summary>
			/// <exception cref="EvaluationCalculationException"></exception>
			/// <exception cref="EvaluationTypeException"></exception>
			/// <exception cref="EvaluationProcessingException"></exception>
			public static EvaluationNode MakeStringCastNode(EvaluationNode argument) {
				EnvironmentFunctionNode node = new EnvironmentFunctionNode(Instance, argument.Context);
				node.SetArguments(argument);
				return node.Simplify();
			}
		}

		public class StringRepeatMethod : AbstractSingleArgMethod {
			public override EvaluationName Name { get; } = "repeat";
			public override string? Description { get; } = "Repeat the string content a given number of times.";

			protected override EnvironmentFunctionArg GetArgument() {
				return new EnvironmentFunctionArg("count", ReceiverType.Context.GetType<IntEvaluationType>(), "The number of times the string should be repeated.");
	}

			protected override string? Warning { get; } = null;

			public StringRepeatMethod(StringEvaluationType receiverType) : base(receiverType) { }

			public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode arg) {
				EvaluationType receiverType = receiver.GetReturnType();
				EvaluationType argType = arg.GetReturnType();
				return (IsString(receiverType) && IntEvaluationType.IsIntegral(argType)) ? receiver.Context.GetType<StringEvaluationType>() : throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {argType}.");
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode arg) {
				EvaluationValue r = receiver.Evaluate(environment);
				EvaluationValue a = arg.Evaluate(environment);

				if (TryGetString(r, out string? str) && IntEvaluationType.TryGetInt(a, out int count)) {
					return new EvaluationValue(string.Join("", str.Yield().Repeat(count)), environment.GetType<StringEvaluationType>());
				}
				else {
					throw new EvaluationTypeException($"Mathematical functions are not defined for value of type {a.Type}.");
				}
			}
		}

		public class StringJoinMethod : AbstractSingleArgMethod {
			public override EvaluationName Name { get; } = "join";
			public override string? Description { get; } = "Join the provided values together using the current string as a separator.";

			protected override EnvironmentFunctionArg GetArgument() {
				return new EnvironmentFunctionArg("values", ReceiverType.Context.GetType<StringEvaluationType>().MakeArray(), "The string values to be joined together.");
			}

			protected override string? Warning { get; } = null;

			public StringJoinMethod(StringEvaluationType receiverType) : base(receiverType) { }

			public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode arg) {
				EvaluationType receiverType = receiver.GetReturnType();
				EvaluationType argType = arg.GetReturnType();
				return (IsString(receiverType) && argType.IterationResult() is EvaluationType argIterType && IsString(argIterType)) ? receiver.Context.GetType<StringEvaluationType>() : throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {argType}.");
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode arg) {
				EvaluationValue r = receiver.Evaluate(environment);
				EvaluationValue a = arg.Evaluate(environment);

				if (TryGetString(r, out string? separator) && a.Type.Iteration(a)?.ToArray() is EvaluationValue[] values) {
					string[] parts = values.Select(v => TryGetString(v, out string? s) ? s : throw new EvaluationCalculationException($"Invalid data type for {Name} argument.")).ToArray();
					string result = string.Join(separator, parts);
					return new EvaluationValue(result, environment.GetType<StringEvaluationType>());
				}
				else {
					throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {a.Type}.");
				}
			}
		}

		public class StringContainsMethod : AbstractSingleArgMethod {
			public override EvaluationName Name { get; } = "contains";
			public override string? Description { get; } = "Returns true of the string contains the provided substring, otherwise false.";

			protected override EnvironmentFunctionArg GetArgument() {
				return new EnvironmentFunctionArg("substring", ReceiverType.Context.GetType<StringEvaluationType>(), "The substring to check for.");
			}

			protected override string? Warning { get; } = null;

			public StringContainsMethod(StringEvaluationType receiverType) : base(receiverType) { }

			public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode arg) {
				EvaluationType receiverType = receiver.GetReturnType();
				EvaluationType argType = arg.GetReturnType();
				if (IsString(receiverType) && IsString(argType)) {
					return ReceiverType.Context.GetType<BoolEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {argType}.");
				}
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode arg) {
				EvaluationValue str = receiver.Evaluate(environment);
				EvaluationValue value = arg.Evaluate(environment);

				if (TryGetString(str, out string? baseStr) && TryGetString(value, out string? substring)) {
					return str.Type.Context.MakeValue<BoolEvaluationType>(baseStr.Contains(substring));
				}
				else {
					throw new EvaluationCalculationException($"Cannot calculate {ReceiverType}.{Name} for argument of type {value.Type}.");
				}
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

		internal ArrayEvaluationType(EvaluationContext context, EvaluationType elementType) : base(context, elementType) {
			this.DataType = this.ElementType.DataType.MakeArrayType();

			AddField(new TypeField("length", Context.GetType<IntEvaluationType>(), value => new EvaluationValue(((Array)value.Value!).Length, value.Type.Context.GetType<IntEvaluationType>())));
			AddMethod(new ArrayContainsMethod(this));
		}

		protected override string GetMyCollectionBrackets() {
			return "[]";
		}

		protected override object ParseValueData(string text, DirectoryPath source) {
			int rank = GetRank();

			if (rank > ValueParsers.MaxArrayOrTupleRank) { throw new FormatException($"Cannot parse array with rank {rank} (max {ValueParsers.MaxArrayOrTupleRank})."); }

			return MakeArray(ElementType, StringParsing.SplitOnUnescaped(text, ValueParsers.GetArrayDelimiter(rank)).Select(v => ElementType.ParseValue(v.Trim(), source)).ToArray()).Value!;
		}

		public override string GetEvaluationString(EvaluationValue value) {
			return TryGetArray(value, out Array? array) ? ValueSerialization.ToString(array) : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
		}

		protected override object? DefaultValueData() {
			return MakeArray(ElementType, Array.Empty<EvaluationValue>()).Value;
		}

		private static bool TryGetArray(EvaluationValue value, [MaybeNullWhen(false)] out Array array) {
			if (value.Value is Array objArray) {
				array = objArray;
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
				try {
				return new EvaluationValue(array.GetValue(indexFinal), ElementType);
			}
				catch (IndexOutOfRangeException) {
					throw new EvaluationCalculationException($"Index out of range for array indexer ({indexVal} not in {array.Length}).");
				}
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
			return DataType == other.DataType;
		}

		protected override int GetTypeHashCode() {
			return DataType.GetHashCode();
		}

		public class ArrayContainsMethod : AbstractSingleArgMethod {
			public override EvaluationName Name { get; } = "contains";
			public override string? Description { get; } = "Returns true of the array contains the provided value, otherwise false.";

			protected override EnvironmentFunctionArg GetArgument() {
				return new EnvironmentFunctionArg("value", arrayReceiver.ElementType, "The value to check for presence in the array.");
	}

			protected override string? Warning { get; } = null;

			private readonly ArrayEvaluationType arrayReceiver;

			public ArrayContainsMethod(ArrayEvaluationType receiverType) : base(receiverType) {
				arrayReceiver = receiverType;
			}

			public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode arg) {
				EvaluationType receiverType = receiver.GetReturnType();
				EvaluationType argType = arg.GetReturnType();
				if (receiverType.IterationResult() is EvaluationType iterableType && EvaluationOps.EqualResult(iterableType, argType) is EvaluationType equalsType && BoolEvaluationType.IsBool(equalsType)) {
					return ReceiverType.Context.GetType<BoolEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for argument of type {argType}.");
				}
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode arg) {
				EvaluationValue iterable = receiver.Evaluate(environment);
				EvaluationValue value = arg.Evaluate(environment);

				if (EvaluationOps.Iteration(iterable)?.ToArray() is EvaluationValue[] values) {
					for (int i = 0; i < values.Length; i++) {
						EvaluationValue equals = EvaluationOps.Equal(values[i], value) ?? throw new EvaluationCalculationException($"Cannot compares values of types {values[i].Type} and {value.Type}.");
						if (BoolEvaluationType.TryGetBool(equals, out bool isMatch) && isMatch) {
							return iterable.Type.Context.MakeValue<BoolEvaluationType>(true);
						}
					}
					return iterable.Type.Context.MakeValue<BoolEvaluationType>(false);
				}
				else {
					throw new EvaluationCalculationException($"Cannot calculate {ReceiverType}.{Name} for argument of type {value.Type}.");
				}
			}

		}

	}

	public class TupleEvaluationType : SequentialCollectionEvaluationType {

		public override Type DataType { get; }

		public int ElementCount { get; }

		internal TupleEvaluationType(EvaluationContext context, EvaluationType elementType, int elementCount) : base(context, elementType) {
			this.DataType = this.ElementType.DataType.MakeArrayType();

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

		public override string GetEvaluationString(EvaluationValue value) {
			return TryGetTuple(value, out Array? tupleValues) ? ValueSerialization.ToString(tupleValues) : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
		}

		protected override object? DefaultValueData() {
			EvaluationValue[] defaults = new EvaluationValue[ElementCount];
			EvaluationValue defaultElemValue = ElementType.DefaultValue();
			for (int i = 0; i < ElementCount; i++) {
				defaults[i] = defaultElemValue;
			}
			return MakeTuple(ElementType, defaults).Value;
		}

		private static bool TryGetTuple(EvaluationValue value, [MaybeNullWhen(false)] out Array array) {
			if (value.Value is Array objArray) {
				array = objArray;
				return true;
			}
			else {
				array = null;
				return false;
			}
		}

		public static EvaluationValue MakeTuple(EvaluationType elementType, IList<EvaluationValue> values) {
			return MakeTupleFromData(elementType, values.Select(v => v.Value).ToArray());
		}

		public static EvaluationValue MakeTupleFromData(EvaluationType elementType, object?[] values) {
			Array final = Array.CreateInstance(elementType.DataType, values.Length);
			Array.Copy(values, final, final.Length);
			return new EvaluationValue(final, elementType.MakeTuple(values.Length));
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
			if (TryGetTuple(subject, out Array? tupleData) && IntEvaluationType.TryGetInt(index, out int indexVal)) {
				if (tupleData.Length != ElementCount) { throw new EvaluationTypeException($"Invalid tuple data (expected {ElementCount} entries, found {tupleData.Length})."); }
				int indexFinal = EvaluationTypeHelpers.GetIndex(indexVal, ElementCount);
				return new EvaluationValue(tupleData.GetValue(indexFinal), ElementType);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IterationResult() => ElementType;
		public override IEnumerable<EvaluationValue>? Iteration(EvaluationValue subject) {
			if (TryGetTuple(subject, out Array? tupleData)) {
				if (tupleData.Length != ElementCount) { throw new EvaluationTypeException($"Invalid tuple data (expected {ElementCount} entries, found {tupleData.Length})."); }
				EvaluationValue[] values = new EvaluationValue[ElementCount];
				for (int i = 0; i < ElementCount; i++) {
					values[i] = new EvaluationValue(tupleData.GetValue(i), ElementType);
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
				&& DataType == other.DataType;
		}

		protected override int GetTypeHashCode() {
			return DataType.GetHashCode();
		}

	}

	public class DictionaryEvaluationType : CollectionEvaluationType {

		public EvaluationType KeyType { get; }

		public override Type DataType { get; } = typeof(OrderedDictionary);

		internal DictionaryEvaluationType(EvaluationContext context, EvaluationType keyType, EvaluationType elementType) : base(context, elementType) {
			this.KeyType = keyType;

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

		private static readonly char[] dictPartsEscapedChars = new char[] { ',' };
		private static readonly char[] dictEntryEscapedChars = new char[] { ':' };
		public override string GetEvaluationString(EvaluationValue value) {
			if (value.Type is DictionaryEvaluationType dictType) {
				List<string> parts = new List<string>();
				foreach (EvaluationValue keyValue in dictType.Iteration(value) ?? Enumerable.Empty<EvaluationValue>()) {
					EvaluationValue valueValue = dictType.Indexer(value, keyValue) ?? throw new EvaluationCalculationException($"Cannot extract value from dictionary ({Name}).");
					parts.Add($"{Escaping.Escape(keyValue.ToEvaluationString(), dictEntryEscapedChars)}: {Escaping.Escape(valueValue.ToEvaluationString(), dictEntryEscapedChars)}");
				}
				return string.Join(", ", parts.Select(static p => Escaping.Escape(p, dictPartsEscapedChars)));
			}
			else {
				throw new EvaluationCalculationException($"Invalid data for {Name}.");
			}
		}

		protected override object? DefaultValueData() {
			return MakeDictionary(KeyType, ElementType, Array.Empty<(EvaluationValue, EvaluationValue)>()).Value;
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

		public static EvaluationValue MakeDictionary(EvaluationType keyType, EvaluationType elementType, IList<(EvaluationValue key, EvaluationValue value)> entries) {
			EvaluationType dictType = new DictionaryEvaluationType(keyType.Context, keyType, elementType);
			IDictionary dict = new OrderedDictionary();

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
			return DataType.GetHashCode();
		}

	}

	public class EnumEvaluationType : EvaluationType {

		public override string Name { get; }

		public Type SystemType { get; }

		public override Type DataType { get; } = typeof(string);

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

		public override string GetEvaluationString(EvaluationValue value) {
			return TryGetEnumValue(value, out string? enumValue) ? enumValue : throw new EvaluationCalculationException($"Invalid data type for {Name}.");
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

		public static bool IsEnum<T>(EvaluationType type) where T : System.Enum {
			return type is EnumEvaluationType enumEvalType && enumEvalType.SystemType == typeof(T);
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
			else if(value.Value is string stringData && enumNames.TryGetValue(stringData, out string? enumName)) {
				enumValue = enumName;
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

	public class NumericClampMethod : AbstractMethod {
		public override EvaluationName Name { get; } = "clamp";
		public override string? Description { get; } = "Returns the current value clamped between a given minimum and maximum value.";

		public NumericClampMethod(EvaluationType receiverType) : base(receiverType) { }

		public override EnvironmentFunctionArguments GetArguments() {
			return new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("min", ReceiverType, null),
					new EnvironmentFunctionArg("max", ReceiverType, null)
				)
			);
		}

		public override EvaluationType GetReturnType(EvaluationNode receiver, EvaluationNode[] args) {
			EvaluationType receiverType = receiver.GetReturnType();
			if (ReceiverType != receiverType) {
				throw new EvaluationTypeException($"Invalid receiver type for {ReceiverType}.{Name}: {receiverType}.");
			}

			EvaluationType[] argTypes = args.Select(a => a.GetReturnType()).ToArray();
			if (argTypes.Length == 2 && argTypes.All(t => ReceiverType.CanImplicitCastFrom(t))) {
				EvaluationType? lessThanType = EvaluationOps.LessThanEqualResult(ReceiverType, argTypes[0]);
				EvaluationType? greaterThanType = EvaluationOps.GreaterThanEqualResult(ReceiverType, argTypes[1]);
				if (lessThanType is not null && BoolEvaluationType.IsBool(lessThanType) && greaterThanType is not null && BoolEvaluationType.IsBool(greaterThanType)) {
					return ReceiverType;
				}
			}

			throw new EvaluationTypeException($"{ReceiverType}.{Name} is not defined for arguments of types {string.Join(", ", argTypes.Select(t => t.ToString()))}.");
		}

		public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode receiver, EvaluationNode[] args) {
			EvaluationValue value = receiver.Evaluate(environment);
			EvaluationValue[] argValues = args.Select(a => a.Evaluate(environment)).ToArray();

			if (argValues.Length == 2 && ReceiverType.Cast(value) is EvaluationValue castValue) {
				EvaluationValue? lessThan = EvaluationOps.LessThan(castValue, argValues[0]);
				if (!lessThan.HasValue || !BoolEvaluationType.TryGetBool(lessThan.Value, out bool lessThanBool)) {
					throw new EvaluationCalculationException($"Invalid first argument for {ReceiverType}.{Name}.");
				}
				
				if (lessThanBool) {
					if (ReceiverType.Cast(argValues[0]) is EvaluationValue minVal) {
						return minVal;
					}
					else {
						throw new EvaluationCalculationException($"Invalid first argument for {ReceiverType}.{Name}.");
					}
				}

				EvaluationValue? greaterThan = EvaluationOps.GreaterThan(castValue, argValues[1]);
				if (!greaterThan.HasValue || !BoolEvaluationType.TryGetBool(greaterThan.Value, out bool greaterThanBool)) {
					throw new EvaluationCalculationException($"Invalid second argument for {ReceiverType}.{Name}.");
				}

				if (greaterThanBool) {
					if (ReceiverType.Cast(argValues[1]) is EvaluationValue maxVal) {
						return maxVal;
					}
					else {
						throw new EvaluationCalculationException($"Invalid first argument for {ReceiverType}.{Name}.");
					}
				}

				return castValue;
			}
			else {
				throw new EvaluationCalculationException($"Cannot calculate {ReceiverType}.{Name} for value of type {value.Type} with arguments {string.Join(", ", argValues.Select(v => v.Type.ToString()))}.");
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
