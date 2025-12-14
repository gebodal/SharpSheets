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
		public readonly bool IsValueType;

		public readonly SpecialType SpecialType;

		public readonly bool IsEnum;

		public string FullName {
			get {
				if (IsValueType) {
					return IsNullable ? $"Nullable<{Type}>" : Type;
				}
				else {
					return Type + (IsNullable ? "?" : "");
				}
			}
		}

		public string CompilerFullName {
			get {
				return (IsValueType && IsNullable) ? $"Nullable<{Minimal}>" : Minimal;
			}
		}

		public string StrippedFullName {
			get {
				if (IsValueType) {
					return IsNullable ? $"Nullable<{Minimal}>" : Minimal;
				}
				else {
					return Minimal + (IsNullable ? "?" : "");
				}
			}
		}

		public TypeData(string type, string name, string minimal, bool nullable, SpecialType specialType, bool isEnum, bool isValueType) {
			Type = type;
			Name = name;
			Minimal = minimal;
			IsNullable = nullable;
			SpecialType = specialType;
			IsEnum = isEnum;
			IsValueType = isValueType;
		}

		public static TypeData Create(ITypeSymbol symbol) { // Compilation compilation
			string fullName = symbol.ToFullDisplayString();
			string nonNullable = TypeNameUtils.RemoveNullable(fullName, out bool nullable);
			return new TypeData(
					type: nonNullable,
					name: symbol.Name,
					minimal: TypeNameUtils.ReduceParameterTypeName(nonNullable),
					nullable: nullable,
					specialType: symbol.SpecialType,
					isEnum: symbol is INamedTypeSymbol named && named.IsEnum(),
					isValueType: symbol.IsValueType
				);
		}

		public ITypeSymbol? GetSymbol(Compilation compilation) {
			return compilation.ResolveTypeKey(FullName);
		}

		public TypeData WithNullable(bool nullable) {
			return new TypeData(Type, Name, Minimal, nullable, SpecialType, IsEnum, IsValueType);
		}

	}

	public record class CompilationData {

		public readonly EquatableDictionary<string, FactorySpecification> Factories; // Factory type -> Factory
		public readonly EquatableDictionary<string, AvailableBuilder> Builders; // Concrete type -> Builder
		public readonly EquatableDictionary<string, ParameterParser> Parsers; // Parser type -> Parser

		public readonly EquatableDictionary<string, EquatableArray<string>> KnownFactoryOwnership; // Concrete type -> Factories containing

	}

}
