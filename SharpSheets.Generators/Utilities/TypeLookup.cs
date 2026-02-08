using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Generators.Utilities {

	public class TypeLookup<T> {

		private readonly Dictionary<TypeData, T> fullValues;
		private readonly Dictionary<TypeData, T> compilerFullValues;
		private readonly Dictionary<TypeData, T> minimalValues;

		private readonly Dictionary<SpecialType, T> specialValues;

		public IEnumerable<T> Values => fullValues.Values;

		public TypeLookup(IEnumerable<(TypeData key, T value)> source) {
			this.fullValues = source.ToDictionary(kv => kv.key, kv => kv.value, FullTypeDataComparer.Instance);
			this.compilerFullValues = source.ToDictionary(kv => kv.key, kv => kv.value, CompilerFullTypeDataComparer.Instance);
			this.minimalValues = source.ToDictionary(kv => kv.key, kv => kv.value, MinimalTypeDataComparer.Instance);

			this.specialValues = source.Where(kv => kv.key.SpecialType != SpecialType.None).ToDictionary(kv => kv.key.SpecialType, kv => kv.value);
		}

		public T this[SpecialType special] {
			get {
				return specialValues[special];
			}
		}

		public bool TryGetValue(TypeData type, out T value) {
			if (fullValues.TryGetValue(type, out value)) {
				return true;
			}
			else if (compilerFullValues.TryGetValue(type, out value)) {
				return true;
			}
			else if (minimalValues.TryGetValue(type, out value)) {
				return true;
			}
			else {
				value = default!;
				return false;
			}
		}

		public bool ContainsKey(TypeData type) {
			return fullValues.ContainsKey(type) || compilerFullValues.ContainsKey(type) || minimalValues.ContainsKey(type);
		}

		private class MinimalTypeDataComparer : IEqualityComparer<TypeData> {
			public static MinimalTypeDataComparer Instance = new MinimalTypeDataComparer();
			private MinimalTypeDataComparer() { }
			public bool Equals(TypeData x, TypeData y) => x.Minimal == y.Minimal;
			public int GetHashCode(TypeData obj) => obj.Minimal.GetHashCode();
		}

		private class CompilerFullTypeDataComparer : IEqualityComparer<TypeData> {
			public static CompilerFullTypeDataComparer Instance = new CompilerFullTypeDataComparer();
			private CompilerFullTypeDataComparer() { }
			public bool Equals(TypeData x, TypeData y) => x.CompilerFullName == y.CompilerFullName;
			public int GetHashCode(TypeData obj) => obj.CompilerFullName.GetHashCode();
		}

		private class FullTypeDataComparer : IEqualityComparer<TypeData> {
			public static FullTypeDataComparer Instance = new FullTypeDataComparer();
			private FullTypeDataComparer() { }
			public bool Equals(TypeData x, TypeData y) => x.FullName == y.FullName;
			public int GetHashCode(TypeData obj) => obj.FullName.GetHashCode();
		}

	}

}
