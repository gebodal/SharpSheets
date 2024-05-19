using SharpSheets.Exceptions;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Parsing {

	public interface IContext : IDocumentEntity {

		new IContext? Parent { get; }
		new IEnumerable<IContext> Children { get; }

		/// <summary>
		///  
		/// </summary>
		/// <param name="key"></param>
		/// <param name="origin"></param>
		/// <param name="isLocalRequest"></param>
		/// <param name="property"></param>
		/// <param name="location"></param>
		/// <returns></returns>
		/// <exception cref="SharpParsingException"></exception>
		bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location);
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="key"></param>
		/// <param name="origin"></param>
		/// <param name="isLocalRequest"></param>
		/// <param name="flag"></param>
		/// <param name="location"></param>
		/// <returns></returns>
		/// <exception cref="SharpParsingException"></exception>
		bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="origin"></param>
		/// <param name="isLocalRequest"></param>
		/// <param name="namedChild"></param>
		/// <returns></returns>
		/// <exception cref="SharpParsingException"></exception>
		bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="origin"></param>
		/// <returns></returns>
		/// <exception cref="SharpParsingException"></exception>
		IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="origin"></param>
		/// <returns></returns>
		/// <exception cref="SharpParsingException"></exception>
		IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="origin"></param>
		/// <returns></returns>
		/// <exception cref="SharpParsingException"></exception>
		IEnumerable<ContextValue<string>> GetEntries(IContext? origin);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="origin"></param>
		/// <returns></returns>
		/// <exception cref="SharpParsingException"></exception>
		IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin);

		/// <summary>
		/// Named children local to this context.
		/// </summary>
		IEnumerable<KeyValuePair<string, IContext>> NamedChildren { get; }

	}

	public static class ContextSearchUtils {

		private static bool TryGetPropertyRecurse(IContext context, string key, IContext? origin, [MaybeNullWhen(false)] out string value, out DocumentSpan? location, bool forceLocal, bool firstCall) {
			if (context.TryGetLocalProperty(key, origin, firstCall, out value, out location)) {
				return true;
			}
			else if (!forceLocal && context.Parent != null) {
				return TryGetPropertyRecurse(context.Parent, key, origin, out value, out location, forceLocal, false);
			}
			else {
				value = null;
				location = null;
				return false;
			}
		}
		public static bool TryGetProperty(this IContext context, string key, bool local, IContext? origin, [MaybeNullWhen(false)] out string value, out DocumentSpan? location) {
			return TryGetPropertyRecurse(context, key, origin, out value, out location, local, true);
		}

		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static string? GetProperty(this IContext context, string key, bool local, IContext? origin, string? defaultValue, out DocumentSpan? location) {
			if (TryGetProperty(context, key, local, origin, out string? value, out location)) {
				return value;
			}
			else {
				location = null;
				return defaultValue;
			}
		}

		public static bool HasProperty(this IContext context, string key, bool local, IContext? origin, out DocumentSpan? location) {
			return TryGetProperty(context, key, local, origin, out _, out location);
		}

		private static bool TryGetFlagRecurse(IContext context, string key, IContext? origin, out bool value, out DocumentSpan? location, bool forceLocal, bool firstCall) {
			if(context.TryGetLocalFlag(key, origin, firstCall, out value, out location)) {
				return true;
			}
			else if (!forceLocal && context.Parent != null) {
				return TryGetFlagRecurse(context.Parent, key, origin, out value, out location, forceLocal, false);
			}
			else {
				value = false;
				location = null;
				return false;
			}
		}
		public static bool TryGetFlag(this IContext context, string key, bool local, IContext? origin, out bool value, out DocumentSpan? location) {
			return TryGetFlagRecurse(context, key, origin, out value, out location, local, true);
		}

		public static bool GetFlag(this IContext context, string key, bool local, IContext? origin, out DocumentSpan? location) {
			if (TryGetFlag(context, key, local, origin, out bool value, out location)) {
				return value;
			}
			else {
				location = null;
				return false;
			}
		}

		public static bool HasFlag(this IContext context, string key, bool local, IContext? origin, out DocumentSpan? location) {
			return TryGetFlag(context, key, local, origin, out _, out location);
		}

		private static bool TryGetNamedChildRecurse(IContext context, string key, IContext? origin, [MaybeNullWhen(false)] out IContext namedChild, bool forceLocal, bool firstCall) {
			if(context.TryGetLocalNamedChild(key, origin, firstCall, out namedChild)) {
				return true;
			}
			else if (!forceLocal && context.Parent != null) {
				return TryGetNamedChildRecurse(context.Parent, key, origin, out namedChild, forceLocal, false);
			}
			else {
				namedChild = null;
				return false;
			}
		}
		public static bool TryGetNamedChild(this IContext context, string key, bool local, IContext? origin, [MaybeNullWhen(false)] out IContext namedChild) {
			return TryGetNamedChildRecurse(context, key, origin, out namedChild, local, true);
		}

		public static IContext? GetNamedChild(this IContext context, string key, bool local, IContext? origin) {
			if (TryGetNamedChild(context, key, local, origin, out IContext? namedChild)) {
				return namedChild;
			}
			else {
				return null;
			}
		}
	}

	[System.Diagnostics.DebuggerDisplay("({Location.Offset}, {Location.Line}, {Location.Column}, {Location.Length}): {Value}")]
	public readonly struct ContextValue<T> {
		public DocumentSpan Location { get; }
		public T Value { get; }
		public ContextValue(DocumentSpan location, T value) {
			this.Location = location;
			this.Value = value;
		}

		public override int GetHashCode() {
			return HashCode.Combine(Location, Value);
		}

		public override bool Equals(object? obj) {
			if (obj is ContextValue<T> cv) {
				if (Location.Equals(cv.Location)) {
					if (Value is null || cv.Value is null) {
						return Value is null && cv.Value is null;
					}
					else {
						return Value.Equals(cv.Value);
					}
				}
			}
			return false;
		}

		public static bool operator ==(ContextValue<T> value1, ContextValue<T> value2) {
			return value1.Equals(value2);
		}
		public static bool operator !=(ContextValue<T> value1, ContextValue<T> value2) {
			return !value1.Equals(value2);
		}

		public ContextValue<V> Apply<V>(Func<T, V> selector) {
			return new ContextValue<V>(Location, selector(Value));
		}
	}

	[System.Diagnostics.DebuggerDisplay("({Location.Offset}, {Location.Line}, {Location.Column}, {Location.Length}): {Name}, ({ValueLocation.Offset}, {ValueLocation.Line}, {ValueLocation.Column}, {ValueLocation.Length}): {Value}")]
	public readonly struct ContextProperty<T> {
		public DocumentSpan Location { get; }
		public string Name { get; }
		public DocumentSpan ValueLocation { get; }
		public T Value { get; }
		public ContextProperty(DocumentSpan location, string name, DocumentSpan valueLocation, T value) {
			this.Location = location;
			this.Name = name;
			this.ValueLocation = valueLocation;
			this.Value = value;
		}

		public override int GetHashCode() {
			return HashCode.Combine(Location, Name, ValueLocation, Value);
		}

		public ContextProperty<V> Apply<V>(Func<T,V> selector) {
			return new ContextProperty<V>(Location, Name, ValueLocation, selector(Value));
		}
	}

	public static class Context {

		public static IContext Empty { get; } = new EmptyContext();

		private class EmptyContext : IContext {
			
			public IContext? Parent => null;
			public IEnumerable<IContext> Children => Enumerable.Empty<IContext>();
			public IEnumerable<KeyValuePair<string, IContext>> NamedChildren => Enumerable.Empty<KeyValuePair<string, IContext>>();

			public string SimpleName => "";
			public string DetailedName => "";
			public string FullName => "";

			public DocumentSpan Location { get; } = DocumentSpan.Imaginary;
			public int Depth => 0; // TODO Is this the best value? -1?

			IDocumentEntity? IDocumentEntity.Parent => Parent;
			IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

			public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
				property = null;
				location = null;
				return false;
			}

			public bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location) {
				flag = false;
				location = null;
				return false;
			}

			public bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild) {
				namedChild = null;
				return false;
			}

			public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) => Enumerable.Empty<ContextProperty<string>>();
			public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) => Enumerable.Empty<ContextProperty<bool>>();
			public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) => Enumerable.Empty<ContextValue<string>>();
			public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) => Enumerable.Empty<ContextValue<string>>();
		}

		public static IContext Simple(string name, Dictionary<string, string> properties, Dictionary<string, bool> flags) {
			return new SimpleContext(name, properties, flags);
		}

		private class SimpleContext : IContext {

			public IContext? Parent { get; } = null;
			public IEnumerable<IContext> Children => Enumerable.Empty<IContext>();
			public IEnumerable<KeyValuePair<string, IContext>> NamedChildren => Enumerable.Empty<KeyValuePair<string, IContext>>();
			
			public string SimpleName { get; }
			public string DetailedName => SimpleName;
			public string FullName => SimpleName;
			
			public DocumentSpan Location { get; } = DocumentSpan.Imaginary;
			public int Depth => 0; // TODO Is this the best value? -1?
			
			IDocumentEntity? IDocumentEntity.Parent => Parent;
			IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;
			
			public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) => Enumerable.Empty<ContextValue<string>>();
			public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) => Enumerable.Empty<ContextValue<string>>();

			private readonly Dictionary<string, string> properties;
			private readonly Dictionary<string, bool> flags;

			public SimpleContext(string name, Dictionary<string, string> properties, Dictionary<string, bool> flags) {
				this.SimpleName = name;
				this.properties = new Dictionary<string, string>(properties, SharpDocuments.StringComparer);
				this.flags = new Dictionary<string, bool>(flags, SharpDocuments.StringComparer);
			}

			public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
				if(properties.TryGetValue(key, out property)) {
					location = DocumentSpan.Imaginary;
					return true;
				}
				else {
					property = null;
					location = null;
					return false;
				}
			}

			public bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location) {
				if (flags.TryGetValue(key, out flag)) {
					location = DocumentSpan.Imaginary;
					return true;
				}
				else {
					flag = false;
					location = null;
					return false;
				}
			}

			public bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild) {
				namedChild = null;
				return false;
			}

			public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) {
				return properties
					.Select(kv => new ContextProperty<string>(DocumentSpan.Imaginary, kv.Key, DocumentSpan.Imaginary, kv.Value));
			}

			public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) {
				return flags
					.Select(kv => new ContextProperty<bool>(DocumentSpan.Imaginary, kv.Key, DocumentSpan.Imaginary, kv.Value));
			}
		}
	}

	public static class ContextUtils {

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static string? GetProperty(this IContext context, string key, bool local, IContext? origin, string? defaultValue) {
			return context.GetProperty(key, local, origin, defaultValue, out _);
		}

		public static bool HasProperty(this IContext context, string key, bool local, IContext? origin) {
			return context.HasProperty(key, local, origin, out _);
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		public static bool GetFlag(this IContext context, string flag, bool local, IContext? origin) {
			return context.GetFlag(flag, local, origin, out _);
		}

		public static bool HasFlag(this IContext context, string flag, bool local, IContext? origin) {
			return context.HasFlag(flag, local, origin, out _);
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		public static T GetProperty<T>(this IContext context, string key, bool local, IContext? origin, T defaultValue, Func<string, T> parser, out DocumentSpan? location) {
			string? value = context.GetProperty(key, local, origin, null, out location);
			T result;
			if (value is null) {
				result = defaultValue;
			}
			else {
				try {
					result = parser(value);
				}
				catch (FormatException e) {
					throw new SharpParsingException(location, e.Message, e);
				}
			}
			return result;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		public static T GetProperty<T>(this IContext context, string key, bool local, IContext? origin, T defaultValue, Func<string, T> parser) {
			return context.GetProperty(key, local, origin, defaultValue, parser, out _);
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		public static T? GetProperty<T>(this IContext context, string key, bool local, IContext? origin, Func<string, T> parser, out DocumentSpan? location) where T : struct {
			string? value = context.GetProperty(key, local, origin, null, out location);
			T? result = null;
			if (value is not null) {
				try {
					result = parser(value);
				}
				catch (FormatException e) {
					throw new SharpParsingException(location, e.Message, e);
				}
			}
			return result;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		public static T? GetProperty<T>(this IContext context, string key, bool local, IContext? origin, Func<string, T> parser) where T : struct {
			return context.GetProperty(key, local, origin, parser, out _);
		}

		public static IEnumerable<IContext> TraverseChildren(this IContext context, bool includeNamedChildren) {
			foreach (IContext child in context.Children) {
				yield return child;
				foreach (IContext c in child.TraverseChildren(includeNamedChildren)) {
					yield return c;
				}
			}

			if (includeNamedChildren) {
				foreach (IContext child in context.NamedChildren.Select(nc => nc.Value)) {
					yield return child;
					foreach (IContext c in child.TraverseChildren(includeNamedChildren)) {
						yield return c;
					}
				}
			}
		}

		public static IEnumerable<IContext> TraverseParents(this IContext context) {
			IContext? currentParent = context.Parent;
			while (currentParent != null) {
				yield return currentParent;
				currentParent = currentParent.Parent;
			}
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		public static IEnumerable<string> GetAllNamedChildren(this IContext context, bool local) {
			foreach (string localNamedChild in context.NamedChildren.GetKeys()) {
				yield return localNamedChild;
			}

			if (!local) {
				foreach (IContext parent in context.TraverseParents()) {
					foreach (string parentNamedChild in parent.NamedChildren.GetKeys()) {
						yield return parentNamedChild;
					}
				}
			}
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		public static IEnumerable<ContextProperty<string>> GetAllProperties(this IContext context, bool local) {
			foreach(ContextProperty<string> localProperty in context.GetLocalProperties(null)) {
				yield return localProperty;
			}

			if (!local) {
				foreach(IContext parent in context.TraverseParents()) {
					foreach (ContextProperty<string> parentProperty in parent.GetLocalProperties(null)) {
						yield return parentProperty;
					}
				}
			}
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		public static IEnumerable<ContextProperty<bool>> GetAllFlags(this IContext context, bool local) {
			foreach (ContextProperty<bool> localProperty in context.GetLocalFlags(null)) {
				yield return localProperty;
			}

			if (!local) {
				foreach (IContext parent in context.TraverseParents()) {
					foreach (ContextProperty<bool> parentProperty in parent.GetLocalFlags(null)) {
						yield return parentProperty;
					}
				}
			}
		}
	}

	public class NamedContext : IContext {

		protected readonly IContext originalContext;
		protected readonly string name;
		protected readonly string prefix;
		protected readonly DocumentSpan? location;

		protected readonly bool forceLocal;

		public string SimpleName { get { return originalContext.SimpleName; } }
		public string DetailedName { get { return originalContext.DetailedName + $"[{name}]"; } }
		public string FullName { get { return originalContext.FullName + $"[{name}]"; } }

		public DocumentSpan Location { get { return location ?? originalContext.Location; } }
		public int Depth { get { return originalContext.Depth; } } // TODO Is this correct?

		public IContext? Parent { get { return originalContext.Parent is not null ? new NamedContext(originalContext.Parent, name, location: null) : null; } }
		// TODO Is this right? No children for named context?
		public IEnumerable<IContext> Children { get { return Enumerable.Empty<IContext>(); } }
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren { get { return Enumerable.Empty<KeyValuePair<string, IContext>>(); } }

		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		public NamedContext(IContext originalContext, string name, DocumentSpan? location = null, bool forceLocal = false) {
			this.originalContext = originalContext;
			this.name = string.IsNullOrWhiteSpace(name) ? "" : name; // (name != null && name.Length > 0) ? name : (name ?? "");
			this.prefix = this.name.Length > 0 ? this.name + "." : "";
			this.location = location;

			this.forceLocal = forceLocal;
		}

		protected string MakeKey(string key) {
			return prefix + key;
		}

		protected string StripKey(string key) {
			return (prefix.Length > 0 && IsNamed(key)) ? key.Substring(prefix.Length) : key;
		}

		protected bool IsNamed(string key) {
			return prefix.Length == 0 || key.StartsWith(prefix, SharpDocuments.StringComparison);
		}

		protected IEnumerable<ContextProperty<T>> StripKeys<T>(IEnumerable<ContextProperty<T>> source) {
			return source.Select(p => new ContextProperty<T>(p.Location, StripKey(p.Name), p.ValueLocation, p.Value));
		}

		public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) {
			return StripKeys(originalContext.GetLocalProperties(origin).Where(p => IsNamed(p.Name)));
		}

		public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) {
			return StripKeys(originalContext.GetLocalFlags(origin).Where(p => IsNamed(p.Name)));
		}

		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) {
			return originalContext.GetEntries(origin);
		}

		public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) {
			return originalContext.GetDefinitions(origin);
		}

		public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
			if ((isLocalRequest || !forceLocal) && originalContext.TryGetLocalProperty(MakeKey(key), origin, isLocalRequest, out property, out location)) {
				return true;
			}
			else {
				property = null;
				location = null;
				return false;
			}
		}

		public bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location) {
			if ((isLocalRequest || !forceLocal) && originalContext.TryGetLocalFlag(MakeKey(key), origin, isLocalRequest, out flag, out location)) {
				return true;
			}
			else {
				flag = false;
				location = null;
				return false;
			}
		}

		public bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild) {
			if ((isLocalRequest || !forceLocal) && originalContext.TryGetLocalNamedChild(MakeKey(name), origin, isLocalRequest, out namedChild)) {
				return true;
			}
			else {
				namedChild = null;
				return false;
			}
		}
	}

	public class EmptyChildContext : IContext {

		protected readonly string name;
		protected readonly DocumentSpan? location;

		public string SimpleName { get { return name; } }
		public DocumentSpan Location { get { return location ?? Parent.Location; } }
		public int Depth { get { return Parent.Depth + 1; } }

		public IContext Parent { get; }
		public IEnumerable<IContext> Children { get { return Enumerable.Empty<IContext>(); } }
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren { get { return Enumerable.Empty<KeyValuePair<string, IContext>>(); } }

		IDocumentEntity IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		public string DetailedName { get { return $"<-1>{SimpleName}({Depth})"; } }
		public string FullName { get { return $"{Parent.FullName}.{DetailedName}"; } }

		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		public EmptyChildContext(IContext parent, string name, DocumentSpan? location = null) {
			this.Parent = parent ?? throw new ArgumentNullException(nameof(parent));
			this.name = string.IsNullOrWhiteSpace(name) ? "_EMPTY_" : name;
			this.location = location;
		}

		public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) => Enumerable.Empty<ContextProperty<string>>();
		public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) => Enumerable.Empty<ContextProperty<bool>>();
		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) => Enumerable.Empty<ContextValue<string>>();
		public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) => Enumerable.Empty<ContextValue<string>>();

		public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
			property = null;
			location = null;
			return false;
		}

		public bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location) {
			flag = false;
			location = null;
			return false;
		}

		public bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild) {
			namedChild = null;
			return false;
		}
	}

	public class DisallowNamedChildContext : IContext {

		private readonly IContext original;
		private readonly IContext disallowed;

		public DisallowNamedChildContext(IContext original) : this(original, original) { }

		private DisallowNamedChildContext(IContext original, IContext disallowed) {
			this.original = original;
			this.disallowed = disallowed;
		}

		public DisallowNamedChildContext? Parent => original.Parent is null ? null : new DisallowNamedChildContext(original.Parent, disallowed);
		public IEnumerable<IContext> Children => original.Children.Select(c => new DisallowNamedChildContext(c, disallowed));
		public IEnumerable<KeyValuePair<string, IContext>> NamedChildren => original.NamedChildren.Where(nc => nc.Value.DetailedName != disallowed.DetailedName).Select(nc => new KeyValuePair<string, IContext>(nc.Key, new DisallowNamedChildContext(nc.Value, disallowed)));

		public string SimpleName => original.SimpleName;
		public string DetailedName => original.DetailedName;
		public string FullName => original.FullName;

		public DocumentSpan Location => original.Location;
		public int Depth => original.Depth;

		IContext? IContext.Parent => Parent;
		IDocumentEntity? IDocumentEntity.Parent => Parent;
		IEnumerable<IDocumentEntity> IDocumentEntity.Children => Children;

		public IEnumerable<ContextProperty<string>> GetLocalProperties(IContext? origin) => original.GetLocalProperties(origin);
		public IEnumerable<ContextProperty<bool>> GetLocalFlags(IContext? origin) => original.GetLocalFlags(origin);
		public IEnumerable<ContextValue<string>> GetEntries(IContext? origin) => original.GetEntries(origin);
		public IEnumerable<ContextValue<string>> GetDefinitions(IContext? origin) => original.GetDefinitions(origin);

		public bool TryGetLocalProperty(string key, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out string property, out DocumentSpan? location) {
			return original.TryGetLocalProperty(key, origin, isLocalRequest, out property, out location);
		}

		public bool TryGetLocalFlag(string key, IContext? origin, bool isLocalRequest, out bool flag, out DocumentSpan? location) {
			return original.TryGetLocalFlag(key, origin, isLocalRequest, out flag, out location);
		}

		public bool TryGetLocalNamedChild(string name, IContext? origin, bool isLocalRequest, [MaybeNullWhen(false)] out IContext namedChild) {
			if (original.TryGetLocalNamedChild(name, origin, isLocalRequest, out IContext? originalNamedChild) && originalNamedChild.DetailedName != disallowed.DetailedName) {
				namedChild = originalNamedChild;
				return true;
			}
			else {
				namedChild = null;
				return false;
			}
		}
	}

}
