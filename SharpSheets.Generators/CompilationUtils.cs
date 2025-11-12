using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SharpSheets.Generators {
	public static class CompilationUtils {

		// Remove tuple names and nullable wrappers
		public static ITypeSymbol ReduceParameterType(this Compilation compilation, ITypeSymbol type) {
			if (type == null) return null!;

			if (!type.IsValueType && type.NullableAnnotation == NullableAnnotation.Annotated) {
				return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
			}

			if (type is INamedTypeSymbol named) {
				if (named.IsValueType && named.NullableAnnotation == NullableAnnotation.Annotated && named.TypeArguments.Length == 1) {
					ITypeSymbol underlying = named.TypeArguments[0];
					//ITypeSymbol strippedUnderlying = ReduceParameterType(underlying, compilation);
					return ReduceParameterType(compilation, underlying);

					/*
					// If underlying changed (we removed tuple names), rewrap in Nullable<>
					if (!SymbolEqualityComparer.Default.Equals(strippedUnderlying, underlying)) {
						// Get the Nullable<T> generic definition and construct it with the new underlying type.
						return compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(strippedUnderlying);
					}

					// no change required
					return type;
					*/
				}
				else if (named.IsTupleType) {
					ImmutableArray<ITypeSymbol> elementTypes = named.TupleElements
						.Select(e => e.Type)
						.Select(t => ReduceParameterType(compilation, t))
						.ToImmutableArray();

					// Create an unnamed tuple type symbol with those element types
					return compilation.CreateTupleTypeSymbol(elementTypes);
				}
			}

			// Not a tuple -> return the original symbol unchanged
			return type;
		}

		private static readonly Dictionary<string, SpecialType> s_knownPrimitiveMap = new(StringComparer.Ordinal) {
			["bool"] = SpecialType.System_Boolean, ["System.Boolean"] = SpecialType.System_Boolean,
			["byte"] = SpecialType.System_Byte, ["System.Byte"] = SpecialType.System_Byte,
			["sbyte"] = SpecialType.System_SByte, ["System.SByte"] = SpecialType.System_SByte,
			["short"] = SpecialType.System_Int16, ["System.Int16"] = SpecialType.System_Int16,
			["ushort"] = SpecialType.System_UInt16, ["System.UInt16"] = SpecialType.System_UInt16,
			["int"] = SpecialType.System_Int32, ["System.Int32"] = SpecialType.System_Int32,
			["uint"] = SpecialType.System_UInt32, ["System.UInt32"] = SpecialType.System_UInt32,
			["long"] = SpecialType.System_Int64, ["System.Int64"] = SpecialType.System_Int64,
			["ulong"] = SpecialType.System_UInt64, ["System.UInt64"] = SpecialType.System_UInt64,
			["float"] = SpecialType.System_Single, ["System.Single"] = SpecialType.System_Single,
			["double"] = SpecialType.System_Double, ["System.Double"] = SpecialType.System_Double,
			["decimal"] = SpecialType.System_Decimal, ["System.Decimal"] = SpecialType.System_Decimal,
			["char"] = SpecialType.System_Char, ["System.Char"] = SpecialType.System_Char,
			["object"] = SpecialType.System_Object, ["System.Object"] = SpecialType.System_Object,
			["string"] = SpecialType.System_String, ["System.String"] = SpecialType.System_String,
			["void"] = SpecialType.System_Void, ["System.Void"] = SpecialType.System_Void
		};

		public static bool TryGetSpecialType(string typeName, out SpecialType specialType) {
			return s_knownPrimitiveMap.TryGetValue(typeName, out specialType);
		}

		public static ITypeSymbol? ResolveTypeKey(this Compilation compilation, string key) {
			if (string.IsNullOrWhiteSpace(key)) return null;

			// strip leading "global::" if present (common when using FullyQualifiedFormat strings)
			if (key.StartsWith("global::", StringComparison.Ordinal)) {
				key = key.Substring("global::".Length);
			}

			// 1) Array: look for simple "Type[]" notation (single-dim). Recursively resolve element.
			if (key.EndsWith("[]", StringComparison.Ordinal)) {
				string elemKey = key.Substring(0, key.Length - 2);
				ITypeSymbol? elemType = ResolveTypeKey(compilation, elemKey);
				if (elemType == null) return null;
				return compilation.CreateArrayTypeSymbol(elemType, 1);
			}

			// 2) Nullable-ish form: "T?" -> construct Nullable<T>
			if (key.EndsWith("?", StringComparison.Ordinal)) {
				string innerKey = key.Substring(0, key.Length - 1);
				ITypeSymbol? innerType = ResolveTypeKey(compilation, innerKey);
				if (innerType == null) return null;

				return innerType.WithNullableAnnotation(NullableAnnotation.Annotated);

				/*
				INamedTypeSymbol nullableDef = compilation.GetSpecialType(SpecialType.System_Nullable_T);
				if (nullableDef == null || nullableDef.Kind == SymbolKind.ErrorType) return null;
				return nullableDef.Construct(innerType);
				*/
			}

			// 3) tuple "(T1, T2, ...)" possibly with names
			if (IsTupleLike(key)) {
				// strip surrounding parentheses
				string inner = key.Substring(1, key.Length - 2);
				ImmutableArray<ITypeSymbol>.Builder elementTypes = ImmutableArray.CreateBuilder<ITypeSymbol>();
				foreach (string part in SplitTopLevel(inner, ',')) {
					string p = part.Trim();
					if (string.IsNullOrEmpty(p)) return null;

					// handle possible "type name" like "int rows" -> strip trailing identifier
					string typeOnly = StripTrailingIdentifierIfPresent(p);
					ITypeSymbol? resolved = ResolveTypeKey(compilation, typeOnly);
					if (resolved == null) return null;
					elementTypes.Add(resolved);
				}

				if (elementTypes.Count == 0) {
					return null;
				}

				return compilation.CreateTupleTypeSymbol(elementTypes.ToImmutable());
			}

			// 4) generics with angle brackets: Outer<Arg1,Arg2>
			int lt = key.IndexOf('<');
			if (lt >= 0 && key.EndsWith(">", StringComparison.Ordinal)) {
				string outerName = key.Substring(0, lt).Trim();
				string argsText = key.Substring(lt + 1, key.Length - lt - 2);
				string[] argParts = SplitTopLevel(argsText, ',')
							   .Select(s => s.Trim())
							   .Where(s => s.Length > 0)
							   .ToArray();

				List<ITypeSymbol> argSymbols = new List<ITypeSymbol>();
				foreach (string? ap in argParts) {
					ITypeSymbol? sym = ResolveTypeKey(compilation, ap);
					if (sym == null) return null;
					argSymbols.Add(sym);
				}

				// Try to find generic definition by appending arity backtick
				string defName = $"{outerName}`{argSymbols.Count}";
				INamedTypeSymbol? def = compilation.GetTypeByMetadataName(defName) ?? compilation.GetTypeByMetadataName(outerName);

				if (def == null) return null;
				if (def.TypeParameters.Length != argSymbols.Count) {
					// mismatch in arity; still attempt Construct if possible or fail
					// prefer original def if it matches arity
					INamedTypeSymbol? altDef = compilation.GetTypeByMetadataName(outerName + "`" + def.TypeParameters.Length);
					if (altDef != null && altDef.TypeParameters.Length == argSymbols.Count)
						def = altDef;
				}

				// construct (if def is generic)
				if (def is INamedTypeSymbol nd && nd.IsGenericType) {
					return nd.Construct(argSymbols.ToArray());
				}

				// not generic? fallback to def
				return def;
			}

			// 3) Primitive / special types (map aliases)
			if (s_knownPrimitiveMap.TryGetValue(key, out SpecialType special)) {
				INamedTypeSymbol t = compilation.GetSpecialType(special);
				return t.Kind == SymbolKind.ErrorType ? null : t;
			}

			// 4) Fallback: try GetTypeByMetadataName for named types (including generics with arity like `Type`1)
			//    The caller should store metadata-style names (Namespace.Outer+Inner`1) when possible.
			INamedTypeSymbol? named = compilation.GetTypeByMetadataName(key);
			if (named != null) return named;

			// If all else fails, try and construct a nested type
			int ld = key.LastIndexOf('.');
			if (ld > 0) {
				StringBuilder nb = new StringBuilder(key); // Nested type builder
				nb[ld] = '+';
				return ResolveTypeKey(compilation, nb.ToString());
			}

			return null;
		}

		// detect a tuple-looking string "(...)" at top-level
		private static bool IsTupleLike(string s) {
			s = s.Trim();
			return s.Length >= 2 && s[0] == '(' && s[s.Length - 1] == ')';
		}

		// split on top-level separators (ignore separators inside <>, (), [])
		private static IEnumerable<string> SplitTopLevel(string s, char separator) {
			if (string.IsNullOrEmpty(s)) {
				yield break;
			}

			StringBuilder sb = new StringBuilder();
			int depthAngle = 0, depthPar = 0, depthBracket = 0;
			for (int i = 0; i < s.Length; i++) {
				char c = s[i];
				if (c == '<') { depthAngle++; sb.Append(c); continue; }
				if (c == '>') { depthAngle = Math.Max(0, depthAngle - 1); sb.Append(c); continue; }
				if (c == '(') { depthPar++; sb.Append(c); continue; }
				if (c == ')') { depthPar = Math.Max(0, depthPar - 1); sb.Append(c); continue; }
				if (c == '[') { depthBracket++; sb.Append(c); continue; }
				if (c == ']') { depthBracket = Math.Max(0, depthBracket - 1); sb.Append(c); continue; }

				if (c == separator && depthAngle == 0 && depthPar == 0 && depthBracket == 0) {
					yield return sb.ToString();
					sb.Clear();
					continue;
				}

				sb.Append(c);
			}

			yield return sb.ToString();
		}

		// Try to strip a trailing identifier (element name) from a tuple element like "int rows" => "int"
		// but do not break types that include spaces inside generics etc.
		private static string StripTrailingIdentifierIfPresent(string s) {
			s = s.Trim();
			if (s.Length == 0) return s;

			// find last whitespace at top level (not inside <>, (), [])
			int lastSpaceIndex = -1;
			int depthAngle = 0, depthPar = 0, depthBracket = 0;
			for (int i = 0; i < s.Length; i++) {
				char c = s[i];
				if (c == '<') { depthAngle++; continue; }
				if (c == '>') { depthAngle = Math.Max(0, depthAngle - 1); continue; }
				if (c == '(') { depthPar++; continue; }
				if (c == ')') { depthPar = Math.Max(0, depthPar - 1); continue; }
				if (c == '[') { depthBracket++; continue; }
				if (c == ']') { depthBracket = Math.Max(0, depthBracket - 1); continue; }
				if (char.IsWhiteSpace(c) && depthAngle == 0 && depthPar == 0 && depthBracket == 0) {
					lastSpaceIndex = i;
				}
			}

			if (lastSpaceIndex <= 0) return s; // no trailing space at top level

			string nameCandidate = s.Substring(lastSpaceIndex + 1);
			// name must be a simple identifier (letters, digits, underscore)
			if (nameCandidate.Length > 0 && nameCandidate.All(ch => char.IsLetterOrDigit(ch) || ch == '_')) {
				return s.Substring(0, lastSpaceIndex).TrimEnd();
			}

			return s;
		}

		public static IEnumerable<IMethodSymbol> ResolveMethodSymbol(this Compilation compilation, ITypeSymbol typeSymbol, string methodName) {
			ImmutableArray<ISymbol> typeMembers = typeSymbol.GetMembers(methodName);
			IEnumerable<IMethodSymbol> methodCandidates = typeMembers.OfType<IMethodSymbol>();

			return methodCandidates;
		}

		public static IEnumerable<IMethodSymbol> ResolveMethodSymbol(this Compilation compilation, string typeName, string methodName) {
			ITypeSymbol? typeSymbol = ResolveTypeKey(compilation, typeName);

			if (typeSymbol == null) {
				return Enumerable.Empty<IMethodSymbol>();
			}

			return ResolveMethodSymbol(compilation, typeSymbol, methodName);
		}

	}

}
