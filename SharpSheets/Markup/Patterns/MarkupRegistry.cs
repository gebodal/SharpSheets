using System.Collections.Generic;
using System.Linq;
using SharpSheets.Documentation;

namespace SharpSheets.Markup.Patterns {

	public interface IMarkupRegistry {
		IEnumerable<MarkupPattern> GetPatterns();
		MarkupPattern? GetPattern(PatternName name);

		IEnumerable<string> GetValidNames(HashSet<string> reservedNames);
		IEnumerable<string> GetMinimalNames(HashSet<string> reservedNames);
	}

	public static class MarkupRegistryUtils {

		public static T? GetPattern<T>(this IMarkupRegistry registry, PatternName name) where T : MarkupPattern {
			return registry.GetPattern(name) as T;
		}

		public static bool IsPattern<T>(this IMarkupRegistry registry, PatternName name) where T : MarkupPattern {
			return registry.GetPattern(name) is T;
		}

		public static IEnumerable<T> GetPatterns<T>(this IMarkupRegistry registry) where T : MarkupPattern {
			return registry.GetPatterns().OfType<T>();
		}

		public static ConstructorDetails? GetConstructor(this IMarkupRegistry registry, PatternName name) {
			return registry.GetPattern(name)?.GetConstructorDetails();
		}

		public static ConstructorDetails? GetConstructor<T>(this IMarkupRegistry registry, PatternName name) where T : MarkupPattern {
			return registry.GetPattern<T>(name)?.GetConstructorDetails();
		}

		public static IEnumerable<ConstructorDetails> GetAllConstructorDetails<T>(this IMarkupRegistry registry) where T : MarkupPattern {
			return registry.GetPatterns<T>().Select(p => p.GetConstructorDetails());
		}

		public static IEnumerable<string> GetValidNames<T>(this IMarkupRegistry registry, HashSet<string> reservedNames) where T : MarkupPattern {
			return registry.GetValidNames(reservedNames).Where(n => registry.IsPattern<T>(PatternName.Parse(n)));
		}

		public static IEnumerable<string> GetMinimalNames<T>(this IMarkupRegistry registry, HashSet<string> reservedNames) where T : MarkupPattern {
			return registry.GetMinimalNames(reservedNames).Where(n => registry.IsPattern<T>(PatternName.Parse(n)));
		}

		public static IEnumerable<KeyValuePair<string, ConstructorDetails>> GetValidConstructorNames<T>(this IMarkupRegistry registry, HashSet<string> reservedNames) where T : MarkupPattern {
			foreach(string name in registry.GetValidNames<T>(reservedNames)) {
				ConstructorDetails? constructorDetails = registry.GetConstructor(PatternName.Parse(name));
				if(constructorDetails != null) {
					yield return new KeyValuePair<string, ConstructorDetails>(name, constructorDetails);
				}
			}
		}

		public static IEnumerable<KeyValuePair<string, ConstructorDetails>> GetMinimalConstructorNames<T>(this IMarkupRegistry registry, HashSet<string> reservedNames) where T : MarkupPattern {
			foreach (string name in registry.GetMinimalNames<T>(reservedNames)) {
				ConstructorDetails? constructorDetails = registry.GetConstructor(PatternName.Parse(name));
				if (constructorDetails != null) {
					yield return new KeyValuePair<string, ConstructorDetails>(name, constructorDetails);
				}
			}
		}

	}

	public static class MarkupRegistry {

		public static IMarkupRegistry Empty { get; } = new ReadOnlyMarkupRegistry(Enumerable.Empty<MarkupPattern>());

		#region Read-only
		public static IMarkupRegistry ReadOnly(IEnumerable<MarkupPattern> values) {
			return new ReadOnlyMarkupRegistry(values);
		}

		private class ReadOnlyMarkupRegistry : IMarkupRegistry {

			//private readonly Dictionary<string, MarkupPattern> values;
			private readonly PatternNameDictionary<MarkupPattern> patterns;

			public ReadOnlyMarkupRegistry(IEnumerable<MarkupPattern> values) {
				//this.values = values.ToDictionary(p => p.FullName, StringComparer.InvariantCultureIgnoreCase);

				this.patterns = new PatternNameDictionary<MarkupPattern>();
				foreach (MarkupPattern pattern in values) {
					this.patterns.Add(PatternName.Parse(pattern.FullName), pattern);
				}
			}

			public IEnumerable<MarkupPattern> GetPatterns() {
				return patterns.Values;
			}

			public MarkupPattern? GetPattern(PatternName name) {
				//return values.GetValueOrDefault(name, null);
				if (patterns.TryGetValue(name, out MarkupPattern? pattern)) {
					return pattern;
				}
				else {
					return null;
				}
			}

			public IEnumerable<string> GetValidNames(HashSet<string> reservedNames) {
				return patterns.ValidNames(reservedNames);
			}

			public IEnumerable<string> GetMinimalNames(HashSet<string> reservedNames) {
				return patterns.MinimalNames(reservedNames);
			}
		}
		#endregion
	}
}
