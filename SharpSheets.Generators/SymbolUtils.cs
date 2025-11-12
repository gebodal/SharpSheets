using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace SharpSheets.Generators {

	public static class SymbolUtils {

		public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, params string[] classNames) {
			return symbol.GetAttributes().Where(a => classNames.Contains(a.AttributeClass?.ToFullDisplayString()));
		}

		public static TypedConstant? GetNamedArgument(this AttributeData attr, string name) {
			KeyValuePair<string, TypedConstant> candidate = attr.NamedArguments.FirstOrDefault(kv => kv.Key == name);
			return candidate.Key == name ? candidate.Value : null;
		}

		public static int GetArrayRank(this ITypeSymbol type) {
			if (type is IArrayTypeSymbol array) {
				return 1 + GetArrayRank(array.ElementType);
			}
			else {
				return 0;
			}
		}

		public static int GetArrayOrTupleRank(this ITypeSymbol type) {
			if (type is IArrayTypeSymbol array) {
				return 1 + GetArrayRank(array.ElementType);
			}
			else if (type.IsTupleType && type is INamedTypeSymbol named) {
				return 1 + named.TupleElements
					.Select(e => e.Type.GetArrayOrTupleRank())
					.Max();
			}
			else {
				return 0;
			}
		}

		public static bool IsEnum(this INamedTypeSymbol named) {
			return named.TypeKind == TypeKind.Enum;
		}

		public static IEnumerable<IFieldSymbol> GetDeclaredEnumMembers(this INamedTypeSymbol enumSymbol) {
			if (!IsEnum(enumSymbol)) {
				// Not an enum
				return Enumerable.Empty<IFieldSymbol>();
			}

			return enumSymbol.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(f =>
					f.IsStatic &&
					f.HasConstantValue &&
					!f.IsImplicitlyDeclared // && SymbolEqualityComparer.Default.Equals(f.ContainingType, enumSymbol)
					);
		}

		public static bool IsGenericList(this INamedTypeSymbol named) {
			return named.IsGenericType && named.ConstructUnboundGenericType().ToFullDisplayString() == "System.Collections.Generic.List<>";
		}

		public static bool IsGenericList(this INamedTypeSymbol named, out ITypeSymbol elemType) {
			if (IsGenericList(named)) {
				elemType = named.TypeArguments[0];
				return true;
			}
			else {
				elemType = null!;
				return false;
			}
		}

		public static bool IsGenericNumbered(this INamedTypeSymbol named) {
			return named.IsGenericType && named.ConstructUnboundGenericType().ToFullDisplayString() == "SharpSheets.Parsing.Numbered<>";
		}

		public static bool IsGenericNumbered(this INamedTypeSymbol named, out ITypeSymbol elemType) {
			if (IsGenericNumbered(named)) {
				elemType = named.TypeArguments[0];
				return true;
			}
			else {
				elemType = null!;
				return false;
			}
		}

		private static readonly SymbolDisplayFormat fullFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);
		public static string ToFullDisplayString(this ISymbol symbol) {
			return symbol.ToDisplayString(fullFormat) + (symbol is ITypeSymbol type && !type.IsValueType && type.NullableAnnotation == NullableAnnotation.Annotated ? "?" : "");
		}

	}

}
