using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using static SharpSheets.Generators.FactoryGenerator;

namespace SharpSheets.Generators {

	public record class TypeData {

		public readonly string Type;
		public readonly string Name;
		public readonly string Minimal;
		public readonly bool IsNullable;

		public readonly SpecialType SpecialType;

		public readonly bool IsEnum;

		public string FullName => Type + (IsNullable ? "?" : "");

		public TypeData(string type, string name, string minimal, bool nullable, SpecialType specialType, bool isEnum) {
			Type = type;
			Name = name;
			Minimal = minimal;
			IsNullable = nullable;
			SpecialType = specialType;
			IsEnum = isEnum;
		}

		public static TypeData Create(ITypeSymbol symbol) { // Compilation compilation
			string fullName = symbol.ToFullDisplayString();
			return new TypeData(
					fullName.TrimEnd('?'),
					symbol.Name,
					TypeNameUtils.ReduceParameterTypeName(fullName), //compilation.ReduceParameterType(symbol).ToFullDisplayString(),
					fullName.EndsWith("?"),
					symbol.SpecialType,
					symbol is INamedTypeSymbol named && named.IsEnum()
				);
		}

		public ITypeSymbol? GetSymbol(Compilation compilation) {
			return compilation.ResolveTypeKey(FullName);
		}

	}

}
