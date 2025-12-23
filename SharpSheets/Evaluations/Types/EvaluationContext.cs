using SharpSheets.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Evaluations.Types {

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

}
