using SharpSheets.Layouts;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Documentation {

	public class ArgumentType : IEquatable<ArgumentType> {
		public DisplayType DisplayType { get; }
		public Type DataType { get; }

		public bool IsEnum => DisplayType.IsEnum;
		public bool IsEntried => DisplayType.IsEntried;
		public bool IsNumbered => DisplayType.IsNumbered;

		public ArgumentType(DisplayType displayType, Type dataType) {
			DisplayType = displayType;
			DataType = dataType;
		}

		public static ArgumentType Simple<T>() {
			return new ArgumentType(DisplayType.FromSystem<T>(), typeof(T));
		}

		public bool Equals(ArgumentType? other) {
			if (other is null) { return false; }
			return DisplayType.Equals(other.DisplayType) && DataType.Equals(other.DataType);
		}

		public override bool Equals(object? obj) {
			return Equals(obj as ArgumentType);
		}

		public override int GetHashCode() {
			return HashCode.Combine(DisplayType, DataType);
		}
	}

	[DebuggerDisplay("{Name} ({Type.Name})")]
	public class ArgumentDetails {

		public virtual string Name { get; }
		public ArgumentType Type { get; }
		public DocumentationString? Description { get; }
		public bool IsOptional { get; }
		public bool UseLocal { get; }
		public object? DefaultValue { get; }
		public object? ExampleValue { get; }
		public string? Implied { get; }

		public ArgumentDetails(string name, DocumentationString? description, ArgumentType type, bool isOptional, bool useLocal, object? defaultValue, object? exampleValue, string? implied) {
			this.Name = name;
			this.Description = description;
			this.Type = type;
			this.IsOptional = isOptional;
			this.UseLocal = useLocal;
			this.DefaultValue = defaultValue == System.DBNull.Value ? null : defaultValue;
			this.ExampleValue = exampleValue == System.DBNull.Value ? this.DefaultValue : exampleValue;
			this.Implied = implied;
		}

		public ArgumentDetails Prefixed(string prefix, string separator = ".") {
			return new PrefixedArgumentDetails(this, prefix, separator: separator);
		}

	}

	[DebuggerDisplay("{Name} ({Basis.Type.Name})")]
	public class PrefixedArgumentDetails : ArgumentDetails {
		public ArgumentDetails Basis { get; }

		private readonly string prefix;
		private readonly string separator;

		public override string Name => $"{prefix}{separator}{Basis.Name}";

		public PrefixedArgumentDetails(ArgumentDetails basis, string prefix, string separator = ".") : base(basis.Name, basis.Description, basis.Type, basis.IsOptional, basis.UseLocal, basis.DefaultValue, basis.ExampleValue, basis.Implied) {
			this.Basis = basis;
			this.prefix = prefix;
			this.separator = separator;
		}
	}

	[DebuggerDisplay("{Name} ({DeclaringType.Name}), Arg Count = {Arguments.Length}")]
	public class BuilderDetails {

		public DisplayType DisplayType { get; }
		public DisplayType DeclaringType { get; }
		public string Name { get; }
		public string FullName { get; }
		public ArgumentDetails[] Arguments { get; }
		public DocumentationString? Description { get; }
		public Rectangle? Rect { get; } // TODO Rename back to Size?
		public Size? Canvas { get; }
		public BuilderDetails(DisplayType displayType, DisplayType declaringType, string name, string fullName, ArgumentDetails[] arguments, DocumentationString? description, Rectangle? size, Size? canvas) {
			this.DisplayType = displayType;
			this.DeclaringType = declaringType;
			this.Name = name;
			this.FullName = fullName;
			this.Arguments = arguments;
			this.Description = description;
			this.Rect = size;
			this.Canvas = canvas;
		}

		public IEnumerable<BuilderArgumentDetails> BuilderArguments {
			get {
				return Arguments.Select(a => new BuilderArgumentDetails(this, a));
			}
		}

		public BuilderDetails WithArgument(ArgumentDetails argument, int index) {
			List<ArgumentDetails> newArgs = Arguments.ToList();
			newArgs.Insert(index, argument);
			return WithArguments(newArgs.ToArray());
		}

		protected virtual BuilderDetails WithArguments(ArgumentDetails[] arguments) {
			return new BuilderDetails(DisplayType, DeclaringType, Name, FullName, arguments, Description, Rect, Canvas);
		}

		public BuilderDetails WithAdditionalArguments(IEnumerable<ArgumentDetails> extraArgs) {
			return WithArguments(Arguments.Concat(extraArgs).ToArray());
		}

		public BuilderDetails WithAdditionalArguments(params ArgumentDetails[] extraArgs) {
			return WithAdditionalArguments((IEnumerable<ArgumentDetails>)extraArgs);
		}

		public BuilderDetails Prefixed(string prefix) {
			return WithArguments(Arguments.Select(a => a.Prefixed(prefix)).ToArray());
		}

	}

	public class BuilderArgumentDetails {
		public BuilderDetails Builder { get; }
		public ArgumentDetails Argument { get; }

		public BuilderArgumentDetails(BuilderDetails builder, ArgumentDetails argument) {
			this.Builder = builder;
			this.Argument = argument;
		}

		public DisplayType MethodDisplayType => Builder.DisplayType;
		public DisplayType DeclaringType => Builder.DeclaringType;
		public string BuilderName => Builder.Name;
		public DocumentationString? MethodDescription => Builder.Description;

		public string ArgumentName => Argument.Name;
		public ArgumentType ArgumentType => Argument.Type;
		public DocumentationString? ArgumentDescription => Argument.Description;
		public bool IsOptional => Argument.IsOptional;
		public bool UseLocal => Argument.UseLocal;
		public object? DefaultValue => Argument.DefaultValue;
		public string? Implied => Argument.Implied;
	}

	public interface ITypeCollection {
		bool ContainsKey(Type type);
		bool ContainsKey(string name);
	}

	public interface ITypeDetailsCollection : ITypeCollection, IEnumerable<BuilderDetails> {
		bool TryGetValue(Type type, [MaybeNullWhen(false)] out BuilderDetails builder);
		bool TryGetValue(string name, [MaybeNullWhen(false)] out BuilderDetails builder);

		IEnumerable<KeyValuePair<string, BuilderDetails>> GetBuilderNames();
	}

	public static class TypeDetailsCollectionUtils {
		public static BuilderDetails? Get(this ITypeDetailsCollection collection, Type type) {
			return collection.TryGetValue(type, out BuilderDetails? result) ? result : null;
		}
		public static BuilderDetails? Get(this ITypeDetailsCollection collection, string name) {
			return collection.TryGetValue(name, out BuilderDetails? result) ? result : null;
		}

		public static BuilderDetails Get(this ITypeDetailsCollection collection, Type type, BuilderDetails fallback) {
			return collection.TryGetValue(type, out BuilderDetails? result) ? result : fallback;
		}
		public static BuilderDetails Get(this ITypeDetailsCollection collection, string name, BuilderDetails fallback) {
			return collection.TryGetValue(name, out BuilderDetails? result) ? result : fallback;
		}

		public static IEnumerable<BuilderDetails> FindBuilders<T>(this ITypeDetailsCollection collection) {
			foreach (BuilderDetails c in collection) {
				if (c.DeclaringType.IsAssignableTo(typeof(T))) {
					yield return c;
				}
			}
		}
		public static IEnumerable<BuilderDetails> FindBuilders(this ITypeDetailsCollection collection, Type parentType) {
			foreach (BuilderDetails c in collection) {
				if (c.DeclaringType.IsAssignableTo(parentType)) {
					yield return c;
				}
			}
		}

		public static IEnumerable<KeyValuePair<string, BuilderDetails>> GetBuilderNames<T>(this ITypeDetailsCollection collection) {
			foreach (KeyValuePair<string, BuilderDetails> entry in collection.GetBuilderNames()) {
				if (entry.Value.DeclaringType.IsAssignableTo(typeof(T))) {
					yield return entry;
				}
			}
		}
		public static IEnumerable<KeyValuePair<string, BuilderDetails>> GetBuilderNames(this ITypeDetailsCollection collection, Type parentType) {
			foreach (KeyValuePair<string, BuilderDetails> entry in collection.GetBuilderNames()) {
				if (entry.Value.DeclaringType.IsAssignableTo(parentType)) {
					yield return entry;
				}
			}
		}
	}

	[DebuggerDisplay("Count = {typeCollection.Count} types, {nameCollection.Count} names")]
	public class TypeDetailsCollection : ITypeDetailsCollection {

		private readonly Dictionary<Type, BuilderDetails> typeCollection;
		private readonly Dictionary<string, BuilderDetails> nameCollection;

		public TypeDetailsCollection(IEqualityComparer<string> nameComparer) {
			typeCollection = new Dictionary<Type, BuilderDetails>();
			nameCollection = new Dictionary<string, BuilderDetails>(nameComparer); // StringComparer.InvariantCultureIgnoreCase
		}

		public TypeDetailsCollection(IEnumerable<BuilderDetails> values, IEqualityComparer<string> nameComparer) {
			typeCollection = values.Select(b => (builder: b, type: b.DeclaringType.GetSingle())).Where(bt => bt.type != null).ToDictionaryAllowRepeats(bt => bt.type!, bt => bt.builder, false);
			nameCollection = values.ToDictionaryAllowRepeats(c => c.FullName, nameComparer, false); // StringComparer.InvariantCultureIgnoreCase
		}

		public void Add(BuilderDetails builder) {
			if (builder.DeclaringType.GetSingle() is Type singleType) typeCollection.Add(singleType, builder);
			nameCollection.Add(builder.Name, builder);
		}

		public void AddRange(IEnumerable<BuilderDetails> builders) {
			foreach (BuilderDetails builder in builders) {
				Add(builder);
			}
		}

		public void Include(BuilderDetails builder) {
			if (builder.DeclaringType.GetSingle() is Type singleType && typeCollection.TryGetValue(singleType, out BuilderDetails? existing)) {
				if (existing != builder) throw new ArgumentException("Provided conflicting builder for existing type.");
			}
			else {
				Add(builder);
			}
		}

		public void UnionWith(IEnumerable<BuilderDetails> builders) {
			foreach (BuilderDetails builder in builders) {
				Include(builder);
			}
		}

		public bool ContainsKey(Type type) {
			return typeCollection.ContainsKey(type);
		}
		public bool ContainsKey(string name) {
			return nameCollection.ContainsKey(name);
		}

		public bool TryGetValue(Type type, [MaybeNullWhen(false)] out BuilderDetails builder) {
			return typeCollection.TryGetValue(type, out builder);
		}
		public bool TryGetValue(string name, [MaybeNullWhen(false)] out BuilderDetails builder) {
			return nameCollection.TryGetValue(name, out builder);
		}

		public IEnumerator<BuilderDetails> GetEnumerator() {
			return nameCollection.Values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public IEnumerable<KeyValuePair<string, BuilderDetails>> GetBuilderNames() {
			return nameCollection;
		}
	}

	public class BuilderComparer : IEqualityComparer<BuilderDetails> {
		public static BuilderComparer Instance { get; } = new BuilderComparer();

		private BuilderComparer() { }

		public bool Equals(BuilderDetails? x, BuilderDetails? y) {
			if(x == null || y == null) {
				return x is null && y is null;
			}
			return SharpDocuments.StringComparer.Equals(x.FullName, y.FullName) && x.Arguments.Length == y.Arguments.Length;
		}
		public int GetHashCode(BuilderDetails obj) {
			return HashCode.Combine(obj.FullName.ToLowerInvariant(), obj.DeclaringType.FullName, obj.Arguments.Length);
		}
	}

	public class ArgumentComparer : IEqualityComparer<ArgumentDetails>, IEqualityComparer<BuilderArgumentDetails> {
		public static ArgumentComparer Instance { get; } = new ArgumentComparer();

		private ArgumentComparer() { }

		public bool Equals(ArgumentDetails? x, ArgumentDetails? y) {
			if(x is null || y is null) {
				return x is null && y is null;
			}
			return x.Type == y.Type && SharpDocuments.StringComparer.Equals(x.Name, y.Name);
		}
		public int GetHashCode(ArgumentDetails obj) {
			return HashCode.Combine(obj.Type.DisplayType.FullName, obj.Type.DataType.FullName, obj.Name.ToLowerInvariant());
		}

		public bool Equals(BuilderArgumentDetails? x, BuilderArgumentDetails? y) {
			return Equals(x?.Argument, y?.Argument);
		}
		public int GetHashCode(BuilderArgumentDetails obj) {
			return GetHashCode(obj.Argument);
		}
	}

	public class BuilderDetailsUniqueNameEnumerator : IEnumerator<BuilderDetails> {

		object IEnumerator.Current => Current;
		public BuilderDetails Current {
			get {
				try {
					return builders[position];
				}
				catch (IndexOutOfRangeException) {
					throw new InvalidOperationException();
				}
			}
		}

		private int position;

		private readonly BuilderDetails[] builders;

		public BuilderDetailsUniqueNameEnumerator(IEnumerable<BuilderDetails> source, IEqualityComparer<string> nameComparer) {
			HashSet<string> encounteredNames = new HashSet<string>(nameComparer);
			List<BuilderDetails> builders = new List<BuilderDetails>();
			foreach(BuilderDetails builder in source) {
				if (!encounteredNames.Contains(builder.FullName)) {
					builders.Add(builder);
					encounteredNames.Add(builder.FullName);
				}
			}
			this.builders = builders.ToArray();
			this.position = -1;
		}

		public void Dispose() { }

		public bool MoveNext() {
			position++;
			return (position < builders.Length);
		}

		public void Reset() {
			position = -1;
		}
	}

}