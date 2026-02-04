using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpSheets.Generators.Utilities;
using SharpSheets.Generators.Utilities.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using static SharpSheets.Generators.SharpSheetsConstants;

namespace SharpSheets.Generators {

	public record class FactorySpecification {
		public readonly string Namespace;
		public readonly string Name;
		public readonly bool IsStatic;
		public readonly bool IsPartial;
		public readonly TypeData FactoryType;

		public readonly EquatableArray<RequiredParameter> RequiredParameters;

		public readonly TypeData? DefaultType;

		public readonly bool IncludeDocs;

		public string FullName => $"{Namespace}.{Name}";

		public FactorySpecification(string @namespace, string name, bool isStatic, bool isPartial, TypeData factoryType, IList<RequiredParameter> requiredParams, TypeData? defaultType, bool includeDocs) {
			Namespace = @namespace;
			Name = name;
			IsStatic = isStatic;
			IsPartial = isPartial;
			FactoryType = factoryType;

			RequiredParameters = new EquatableArray<RequiredParameter>(requiredParams.ToArray());

			DefaultType = defaultType;

			IncludeDocs = includeDocs;
		}

		public static FactorySpecification Build(INamedTypeSymbol classSymbol, ClassDeclarationSyntax declarationNode, ITypeSymbol factoryTypeSymbol, ITypeSymbol[] requiredParams, string[] requiredParamNames, bool[] excludeRequiredParams, ITypeSymbol? defaultType, bool includeDocs) {
			return new FactorySpecification(
				classSymbol.ContainingNamespace.ToFullDisplayString(),
				classSymbol.Name,
				classSymbol.IsStatic,
				declarationNode.IsPartial(),
				TypeData.Create(factoryTypeSymbol),
				requiredParams.Zip(requiredParamNames, excludeRequiredParams, (t, n, e) => RequiredParameter.Build(t, n, e)).ToArray(),
				defaultType is not null ? TypeData.Create(defaultType) : null,
				includeDocs
			);
		}
	}

	public class RequiredParameter : IEquatable<RequiredParameter> {
		public readonly string Name;
		public readonly TypeData Type;
		public readonly bool Exclude;

		public RequiredParameter(string name, TypeData type, bool exclude) {
			Name = name;
			Type = type;
			Exclude = exclude;
		}

		public static explicit operator RequiredParameter(BuilderParameter p) {
			return new RequiredParameter(p.Name, p.Type, false);
		}

		public bool Equals(RequiredParameter other) {
			return Type == other.Type;
		}

		public override bool Equals(object obj) {
			return obj is RequiredParameter other && Equals(other);
		}

		public override int GetHashCode() {
			return Type.GetHashCode();
		}

		public static bool operator ==(RequiredParameter a, RequiredParameter b) {
			return a.Equals(b);
		}
		public static bool operator !=(RequiredParameter a, RequiredParameter b) {
			return !a.Equals(b);
		}

		public static RequiredParameter Build(ITypeSymbol paramType, string paramName, bool exclude) {
			return new RequiredParameter(
				paramName,
				TypeData.Create(paramType),
				exclude
			);
		}
	}

	public record class FactoryToGenerate {
		public readonly FactorySpecification Spec;
		public readonly AvailableBuilder? DefaultBuilder;
		public readonly EquatableArray<BuilderToGenerate> Builders;
		public readonly bool IsSingleton;

		public FactoryToGenerate(FactorySpecification spec, AvailableBuilder? defaultBuilder, EquatableArray<BuilderToGenerate> builders, bool isSingleton) {
			Spec = spec;
			DefaultBuilder = defaultBuilder;
			Builders = builders;
			IsSingleton = isSingleton;
		}
	}

	public enum BuilderStructure { NONE, GROUPED, SUPPLEMENTED, EXPANDED, EXPANDED_DEFERRED }

	public record class AvailableBuilder {
		public readonly string Namespace; // Of builder method containing type
		public readonly string FullTypeName; // Of the builder method containing type
		public readonly string TypeName; // Of the builder method containing type
		public readonly string MethodName; // Of the builder method

		public readonly TypeData BuilderType; // The stated type in the builder attribute

		public readonly TypeData ConcreteBuilderType; // The actual return type of the builder method

		public readonly string? ProvidedName; // An override name of the builder
		public string Name => ProvidedName ?? ConcreteBuilderType.Name; // Final name to be used for builder

		public readonly EquatableArray<BuilderParameter> Parameters;
		public bool CanCallWithEmptyArgs => Parameters.All(p => p.HasDefault);

		public readonly BuilderStructure Structure;
		public readonly string? PrefixSep;

		public string CallingName => $"{FullTypeName}.{MethodName}";

		public AvailableBuilder(string @namespace, string fullTypeName, string typeName, string methodName, TypeData buildType, TypeData concreteBuilderType, string? providedName, IList<BuilderParameter> parameters, BuilderStructure builderStructure, string? prefixSep) {
			Namespace = @namespace;
			FullTypeName = fullTypeName;
			TypeName = typeName;
			MethodName = methodName;

			BuilderType = buildType;

			ConcreteBuilderType = concreteBuilderType;

			ProvidedName = providedName;

			Parameters = new EquatableArray<BuilderParameter>(parameters.ToArray());

			Structure = builderStructure;
			PrefixSep = prefixSep;
		}

		private static (BuilderStructure structure, string? prefixSep) GetBuilderStructure(IMethodSymbol methodSymbol) {
			if (methodSymbol.GetAttributes(GroupedArgumentBuilderAttribute).Any()) {
				return (BuilderStructure.GROUPED, null);
			}
			else if (methodSymbol.GetAttributes(SupplementedArgumentBuilderAttribute).Any()) {
				return (BuilderStructure.SUPPLEMENTED, null);
			}
			else if (methodSymbol.GetAttributes(ExpandedArgumentBuilderAttribute).FirstOrDefault() is AttributeData expandAttr) {
				string? prefixSep = (string?)expandAttr.GetNamedArgument("PrefixSep")?.Value;
				if (((bool?)expandAttr.GetNamedArgument("Defer")?.Value) ?? false) {
					return (BuilderStructure.EXPANDED_DEFERRED, prefixSep);
				}
				else {
					return (BuilderStructure.EXPANDED, prefixSep);
				}
			}

			return (BuilderStructure.NONE, null);
		}

		public static AvailableBuilder Build(IMethodSymbol methodSymbol, ITypeSymbol builderTypeSymbol, string? builderName) {
			(BuilderStructure structure, string? prefixSep) = GetBuilderStructure(methodSymbol);
			return new AvailableBuilder(
				methodSymbol.ContainingType.ContainingNamespace.ToFullDisplayString(),
				methodSymbol.ContainingType.ToFullDisplayString(),
				methodSymbol.ContainingType.Name,
				methodSymbol.Name,
				TypeData.Create(builderTypeSymbol),
				TypeData.Create(methodSymbol.ReturnType),
				builderName,
				methodSymbol.Parameters.Select(p => BuilderParameter.Create(p)).ToArray(),
				structure, prefixSep
			);
		}

		public IMethodSymbol? GetMethodSymbol(Compilation compilation) {
			return compilation.ResolveMethodSymbol(FullTypeName, MethodName).FirstOrDefault(m => m.Parameters.Length == Parameters.Count);
		}
	}

	public enum BuilderParameterType { Normal, BuildErrors, SourceDir }

	public record class BuilderParameter {
		public readonly string Name;
		public readonly TypeData Type;
		public readonly string? DefaultValue;
		public bool HasDefault => DefaultValue is not null;
		public readonly bool IsLocal;
		public readonly bool IsOptional;
		public readonly BuilderParameterType ParameterType;
		public readonly bool Exclude;

		public bool IsBuildErrors => ParameterType == BuilderParameterType.BuildErrors;
		public bool IsSourceDir => ParameterType == BuilderParameterType.SourceDir;

		public BuilderParameter(string name, TypeData type, string? defaultValue, bool isLocal, bool isOptional, BuilderParameterType parameterType, bool exclude) {
			Name = name;
			Type = type;
			DefaultValue = defaultValue;
			IsLocal = isLocal;
			IsOptional = isOptional;
			ParameterType = parameterType;
			Exclude = exclude;
		}

		private static string GetDefaultForType(ITypeSymbol type) {
			return $"default({type.ToFullDisplayString()})";
		}

		public static BuilderParameter Create(IParameterSymbol param) {
			AttributeData? buildErrorsAttr = param.GetAttributes(BuildErrorsAttribute).FirstOrDefault();
			AttributeData? sourceDirAttr = param.GetAttributes(SourceDirectoryAttribute).FirstOrDefault();
			AttributeData? propAttr = param.GetAttributes(PropertyAttribute, LocalPropertyAttribute).FirstOrDefault();

			BuilderParameterType paramType = buildErrorsAttr is not null ? BuilderParameterType.BuildErrors : (sourceDirAttr is not null ? BuilderParameterType.SourceDir : BuilderParameterType.Normal);
			bool isLocal = propAttr?.AttributeClass?.Name == LocalPropertyAttribute_Name;
			bool exclude = (bool?)propAttr?.GetNamedArgument("Exclude")?.Value ?? false;

			TypeData typeData = TypeData.Create(param.Type);

			string? defaultValue = (paramType == BuilderParameterType.Normal && param.HasExplicitDefaultValue) ? (param.GetExplicitDefaultalueString() ?? ((typeData.IsValueType && !typeData.IsNullable) ? GetDefaultForType(param.Type) : "null")) : null;

			return new BuilderParameter(
				param.Name,
				typeData,
				defaultValue,
				isLocal,
				param.IsOptional,
				paramType,
				exclude);
		}
	}

	public record class ParameterParser {
		public readonly string FullTypeName;
		public readonly string MethodName;

		public readonly TypeData ParserType;

		public readonly bool NeedsSourceDirectory;

		public string CallingName => $"{FullTypeName}.{MethodName}";

		public ParameterParser(string fullTypeName, string methodName, TypeData parserType, bool needsSourceDirectory) {
			FullTypeName = fullTypeName;
			MethodName = methodName;

			ParserType = parserType;

			NeedsSourceDirectory = needsSourceDirectory;
		}

		private static ParameterParser Build(string fullTypeName, string methodName, ITypeSymbol parserType, bool needsSourceDirectory) {
			return new ParameterParser(
				fullTypeName,
				methodName,
				TypeData.Create(parserType),
				needsSourceDirectory
			);
		}

		public static ParameterParser Build(IMethodSymbol methodSymbol, ITypeSymbol parserType, bool needsSourceDirectory) {
			return Build(
				methodSymbol.ContainingType.ToFullDisplayString(),
				methodSymbol.Name,
				parserType,
				needsSourceDirectory
			);
		}

		public static string GetParserTypeName(ITypeSymbol symbol, bool needsExplicitRank = true) {
			if (symbol is IArrayTypeSymbol arrayTypeSymbol) {
				return GetParserTypeName(arrayTypeSymbol.ElementType, false) + (needsExplicitRank ? symbol.GetArrayRank().ToString() : "");
			}
			else if (symbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsTupleType) {
				return "_" + string.Join("_", namedTypeSymbol.TupleElements.Select(f => GetParserTypeName(f.Type, true))) + "_";
			}
			else {
				return symbol.Name;
			}
		}

		public static ParameterParser GetGeneratedParser(ITypeSymbol parserType, bool needsSourceDirectory = false) {
			return Build(
				ParameterParsers,
				$"Parser_{GetParserTypeName(parserType)}",
				parserType,
				needsSourceDirectory
			);
		}
	}

	[Flags]
	public enum BuilderParamKind { None = 0b0000, Required = 0b0001, BuildErrors = 0b0010, Source = 0b0100 }

	public record class BuilderToGenerate {
		public readonly AvailableBuilder Builder;
		public readonly EquatableArray<RequiredParameter> RequiredParameters;

		public readonly string Namespace;
		public readonly string TypeName;

		public string FullTypeName => $"{Namespace}.{TypeName}";

		public readonly string MethodName;

		public BuilderToGenerate(AvailableBuilder builder, EquatableArray<RequiredParameter> requiredParameters, string @namespace, string typeName, string methodName) {
			Builder = builder;
			RequiredParameters = requiredParameters;
			Namespace = @namespace;
			TypeName = typeName;
			MethodName = methodName;
		}

		public IEnumerable<(BuilderParameter param, BuilderParamKind kind)> GetParams() {
			int count = 0;
			foreach ((int pIdx, BuilderParameter param) in Builder.Parameters.Enumerate()) {
				if (param.IsBuildErrors) {
					yield return (param, BuilderParamKind.BuildErrors);
					continue;
				}

				if (count < RequiredParameters.Count) {
					yield return (param, param.IsSourceDir ? (BuilderParamKind.Required | BuilderParamKind.Source) : BuilderParamKind.Required);
					count++;
					continue;
				}
				else {
					count++;
				}

				if (param.IsSourceDir) {
					yield return (param, BuilderParamKind.Source);
					continue;
				}

				yield return (param, BuilderParamKind.None);
			}
		}

		public IMethodSymbol? GetMethodSymbol(Compilation compilation) {
			return Builder.GetMethodSymbol(compilation);
		}
	}

}
