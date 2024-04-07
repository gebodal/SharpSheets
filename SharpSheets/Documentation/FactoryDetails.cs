using SharpSheets.Layouts;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Documentation {

	public class ArgumentType : IEquatable<ArgumentType> {
		public Type DisplayType { get; }
		public Type DataType { get; }

		public bool IsEnum => DisplayType.IsEnum;
		public bool IsList => DisplayType.TryGetGenericTypeDefinition() == typeof(List<>);
		public bool IsNumbered => DisplayType.TryGetGenericTypeDefinition() == typeof(Numbered<>);

		public ArgumentType(Type displayType, Type dataType) {
			DisplayType = displayType;
			DataType = dataType;
		}

		public static ArgumentType Simple(Type type) {
			return new ArgumentType(type, type);
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

			// Dealing with ExampleValue
			if (exampleValue != System.DBNull.Value && exampleValue != null) {
				if (type.DataType.IsAssignableFrom(exampleValue.GetType())) {
					this.ExampleValue = exampleValue;
				}
				else {
					throw new ArgumentException($"Invalid data type for exampleValue: {exampleValue.GetType().FullName} (expected {type.DataType.FullName})");
				}
			}
			else {
				if (type.DataType.IsValueType && !(type.DataType.TryGetGenericTypeDefinition() is Type generic && generic == typeof(Nullable<>))) {
					throw new ArgumentException($"Invalid null value for exampleValue: expected {type.DataType.FullName}");
				}
				else {
					ExampleValue = null;
				}
			}
		}

		public ArgumentDetails Prefixed(string prefix) {
			return new PrefixedArgumentDetails(this, prefix);
		}
	}

	[DebuggerDisplay("{Name} ({Basis.Type.Name})")]
	public class PrefixedArgumentDetails : ArgumentDetails {
		public ArgumentDetails Basis { get; }

		private readonly string prefix;

		public override string Name => $"{prefix}.{Basis.Name}";

		public PrefixedArgumentDetails(ArgumentDetails basis, string prefix) : base(basis.Name, basis.Description, basis.Type, basis.IsOptional, basis.UseLocal, basis.DefaultValue, basis.ExampleValue, basis.Implied) {
			this.Basis = basis;
			this.prefix = prefix;
		}
	}

	[DebuggerDisplay("{Name} ({DeclaringType.Name}), Arg Count = {Arguments.Length}")]
	public class ConstructorDetails {
		public Type DisplayType { get; }
		public Type DeclaringType { get; }
		public string Name { get; }
		public string FullName { get; }
		public ArgumentDetails[] Arguments { get; }
		public DocumentationString? Description { get; }
		public Rectangle? Rect { get; } // TODO Rename back to Size?
		public Size? Canvas { get; }
		public ConstructorDetails(Type displayType, Type declaringType, string name, string fullName, ArgumentDetails[] arguments, DocumentationString? description, Rectangle? size, Size? canvas) {
			this.DisplayType = displayType;
			this.DeclaringType = declaringType;
			this.Name = name;
			this.FullName = fullName;
			this.Arguments = arguments;
			this.Description = description;
			this.Rect = size;
			this.Canvas = canvas;
		}

		public IEnumerable<ConstructorArgumentDetails> ConstructorArguments {
			get {
				return Arguments.Select(a => new ConstructorArgumentDetails(this, a));
			}
		}

		public ConstructorDetails WithArgument(ArgumentDetails argument, int index) {
			List<ArgumentDetails> newArgs = Arguments.ToList();
			newArgs.Insert(index, argument);
			return WithArguments(newArgs.ToArray());
		}

		protected virtual ConstructorDetails WithArguments(ArgumentDetails[] arguments) {
			return new ConstructorDetails(DisplayType, DeclaringType, Name, FullName, arguments, Description, Rect, Canvas);
		}

		public ConstructorDetails WithAdditionalArguments(IEnumerable<ArgumentDetails> extraArgs) {
			return WithArguments(Arguments.Concat(extraArgs).ToArray());
		}

		public ConstructorDetails Prefixed(string prefix) {
			return WithArguments(Arguments.Select(a => a.Prefixed(prefix)).ToArray());
		}

	}

	public class ConstructorArgumentDetails {
		public ConstructorDetails Constructor { get; }
		public ArgumentDetails Argument { get; }

		public ConstructorArgumentDetails(ConstructorDetails constructor, ArgumentDetails argument) {
			this.Constructor = constructor;
			this.Argument = argument;
		}

		public Type MethodDisplayType => Constructor.DisplayType;
		public Type DeclaringType => Constructor.DeclaringType;
		public string ConstructorName => Constructor.Name;
		public DocumentationString? MethodDescription => Constructor.Description;

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

	public interface ITypeDetailsCollection : ITypeCollection, IEnumerable<ConstructorDetails> {
		bool TryGetValue(Type type, [MaybeNullWhen(false)] out ConstructorDetails constructor);
		bool TryGetValue(string name, [MaybeNullWhen(false)] out ConstructorDetails constructor);

		IEnumerable<KeyValuePair<string, ConstructorDetails>> GetConstructorNames();
	}

	public static class TypeDetailsCollectionUtils {
		public static ConstructorDetails? Get(this ITypeDetailsCollection collection, Type type) {
			return collection.TryGetValue(type, out ConstructorDetails? result) ? result : null;
		}
		public static ConstructorDetails? Get(this ITypeDetailsCollection collection, string name) {
			return collection.TryGetValue(name, out ConstructorDetails? result) ? result : null;
		}

		public static ConstructorDetails Get(this ITypeDetailsCollection collection, Type type, ConstructorDetails fallback) {
			return collection.TryGetValue(type, out ConstructorDetails? result) ? result : fallback;
		}
		public static ConstructorDetails Get(this ITypeDetailsCollection collection, string name, ConstructorDetails fallback) {
			return collection.TryGetValue(name, out ConstructorDetails? result) ? result : fallback;
		}

		public static IEnumerable<ConstructorDetails> FindConstructors<T>(this ITypeDetailsCollection collection) {
			//return collection.Where(c => typeof(T).IsAssignableFrom(c.DeclaringType));
			foreach (ConstructorDetails c in collection) {
				if (typeof(T).IsAssignableFrom(c.DeclaringType)) {
					yield return c;
				}
			}
		}
		public static IEnumerable<ConstructorDetails> FindConstructors(this ITypeDetailsCollection collection, Type parentType) {
			//return collection.Where(c => parentType.IsAssignableFrom(c.DeclaringType));
			foreach (ConstructorDetails c in collection) {
				if (parentType.IsAssignableFrom(c.DeclaringType)) {
					yield return c;
				}
			}
		}

		public static IEnumerable<KeyValuePair<string, ConstructorDetails>> GetConstructorNames<T>(this ITypeDetailsCollection collection) {
			//return collection.Where(c => typeof(T).IsAssignableFrom(c.DeclaringType));
			foreach (KeyValuePair<string, ConstructorDetails> entry in collection.GetConstructorNames()) {
				if (typeof(T).IsAssignableFrom(entry.Value.DeclaringType)) {
					yield return entry;
				}
			}
		}
		public static IEnumerable<KeyValuePair<string, ConstructorDetails>> GetConstructorNames(this ITypeDetailsCollection collection, Type parentType) {
			//return collection.Where(c => parentType.IsAssignableFrom(c.DeclaringType));
			foreach (KeyValuePair<string, ConstructorDetails> entry in collection.GetConstructorNames()) {
				if (parentType.IsAssignableFrom(entry.Value.DeclaringType)) {
					yield return entry;
				}
			}
		}
	}

	[DebuggerDisplay("Count = {typeCollection.Count} types, {nameCollection.Count} names")]
	public class TypeDetailsCollection : ITypeDetailsCollection {

		private readonly Dictionary<Type, ConstructorDetails> typeCollection;
		private readonly Dictionary<string, ConstructorDetails> nameCollection;

		public TypeDetailsCollection(IEqualityComparer<string> nameComparer) {
			typeCollection = new Dictionary<Type, ConstructorDetails>();
			nameCollection = new Dictionary<string, ConstructorDetails>(nameComparer); // StringComparer.InvariantCultureIgnoreCase
		}

		public TypeDetailsCollection(IEnumerable<ConstructorDetails> values, IEqualityComparer<string> nameComparer) {
			typeCollection = values.Where(c => c.DeclaringType != null).ToDictionaryAllowRepeats(c => c.DeclaringType, false);
			nameCollection = values.ToDictionaryAllowRepeats(c => c.FullName, nameComparer, false); // StringComparer.InvariantCultureIgnoreCase
		}

		//public ConstructorDetails this[Type type] { get { return typeCollection[type]; } }
		//public ConstructorDetails this[string name] { get { return nameCollection[name]; } }

		public void Add(ConstructorDetails constructor) {
			if (constructor.DeclaringType != null) typeCollection.Add(constructor.DeclaringType, constructor);
			nameCollection.Add(constructor.Name, constructor);
		}

		public void AddRange(IEnumerable<ConstructorDetails> constructors) {
			foreach (ConstructorDetails constructor in constructors) {
				Add(constructor);
			}
		}

		public void Include(ConstructorDetails constructor) {
			if (constructor.DeclaringType != null && typeCollection.TryGetValue(constructor.DeclaringType, out ConstructorDetails? existing)) {
				if (existing != constructor) throw new ArgumentException("Provided conflicting constructor for existing type.");
			}
			else {
				Add(constructor);
			}
		}

		public void UnionWith(IEnumerable<ConstructorDetails> constructors) {
			foreach (ConstructorDetails constructor in constructors) {
				Include(constructor);
			}
		}

		public bool ContainsKey(Type type) {
			return typeCollection.ContainsKey(type);
		}
		public bool ContainsKey(string name) {
			return nameCollection.ContainsKey(name);
		}

		public bool TryGetValue(Type type, [MaybeNullWhen(false)] out ConstructorDetails constructor) {
			return typeCollection.TryGetValue(type, out constructor);
		}
		public bool TryGetValue(string name, [MaybeNullWhen(false)] out ConstructorDetails constructor) {
			return nameCollection.TryGetValue(name, out constructor);
		}

		public IEnumerator<ConstructorDetails> GetEnumerator() {
			return nameCollection.Values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public IEnumerable<KeyValuePair<string, ConstructorDetails>> GetConstructorNames() {
			return nameCollection;
		}
	}

	public class ConstructorComparer : IEqualityComparer<ConstructorDetails> {
		public static ConstructorComparer Instance { get; } = new ConstructorComparer();

		private ConstructorComparer() { }

		public bool Equals(ConstructorDetails? x, ConstructorDetails? y) {
			if(x == null || y == null) {
				return x is null && y is null;
			}
			return SharpDocuments.StringComparer.Equals(x.FullName, y.FullName) && x.Arguments.Length == y.Arguments.Length;
		}
		public int GetHashCode(ConstructorDetails obj) {
			//return $"{obj.FullName.ToLowerInvariant()} {obj.DeclaringType.FullName} {obj.Arguments.Length}".GetHashCode();
			return HashCode.Combine(obj.FullName.ToLowerInvariant(), obj.DeclaringType.FullName, obj.Arguments.Length);
		}
	}

	public class ArgumentComparer : IEqualityComparer<ArgumentDetails>, IEqualityComparer<ConstructorArgumentDetails> {
		public static ArgumentComparer Instance { get; } = new ArgumentComparer();

		private ArgumentComparer() { }

		public bool Equals(ArgumentDetails? x, ArgumentDetails? y) {
			if(x is null || y is null) {
				return x is null && y is null;
			}
			return x.Type == y.Type && SharpDocuments.StringComparer.Equals(x.Name, y.Name);
		}
		public int GetHashCode(ArgumentDetails obj) {
			//return $"{obj.Type.DisplayType.FullName} {obj.Type.DataType.FullName} {obj.Name.ToLowerInvariant()}".GetHashCode();
			return HashCode.Combine(obj.Type.DisplayType.FullName, obj.Type.DataType.FullName, obj.Name.ToLowerInvariant());
		}

		public bool Equals(ConstructorArgumentDetails? x, ConstructorArgumentDetails? y) {
			return Equals(x?.Argument, y?.Argument);
		}
		public int GetHashCode(ConstructorArgumentDetails obj) {
			return GetHashCode(obj.Argument);
		}
	}

	public class ConstructorDetailsUniqueNameEnumerator : IEnumerator<ConstructorDetails> {

		object IEnumerator.Current => Current;
		public ConstructorDetails Current {
			get {
				try {
					return constructors[position];
				}
				catch (IndexOutOfRangeException) {
					throw new InvalidOperationException();
				}
			}
		}

		private int position;

		private readonly ConstructorDetails[] constructors;

		public ConstructorDetailsUniqueNameEnumerator(IEnumerable<ConstructorDetails> source, IEqualityComparer<string> nameComparer) {
			HashSet<string> encounteredNames = new HashSet<string>(nameComparer);
			List<ConstructorDetails> constructors = new List<ConstructorDetails>();
			foreach(ConstructorDetails constructor in source) {
				if (!encounteredNames.Contains(constructor.FullName)) {
					constructors.Add(constructor);
					encounteredNames.Add(constructor.FullName);
				}
			}
			this.constructors = constructors.ToArray();
			this.position = -1;
		}

		public void Dispose() { }

		public bool MoveNext() {
			position++;
			return (position < constructors.Length);
		}

		public void Reset() {
			position = -1;
		}
	}

}