using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SharpSheets.Generators {

	[Generator]
	public class FactoryGenerator : IIncrementalGenerator {

		public void Initialize(IncrementalGeneratorInitializationContext context) {

			IncrementalValuesProvider<FactorySpecification> factoriesSpecs = context.SyntaxProvider
				.ForAttributeWithMetadataNameSelectMany(
					"SharpSheets.Parsing.FactoryAttribute",
					predicate: static (s, _) => s is ClassDeclarationSyntax,
					transform: static (ctx, _) => GetFactorySpecs(ctx));

			IncrementalValuesProvider<AvailableBuilder> availableBuilders = context.SyntaxProvider
				.ForAttributeWithMetadataName(
					"SharpSheets.Parsing.FactoryBuilderAttribute",
					predicate: static (s, _) => s is MethodDeclarationSyntax,
					transform: static (ctx, _) => GetAvailableBuilders(ctx))
				.WhereNotNull();

			IncrementalValuesProvider<ParameterParser> availableParsers = context.SyntaxProvider
				.ForAttributeWithMetadataName(
					"SharpSheets.Parsing.ParameterParserAttribute",
					predicate: static (s, _) => s is MethodDeclarationSyntax,
					transform: static (ctx, _) => GetParameterParser(ctx))
				.WhereNotNull();

			IncrementalValuesProvider<string> declaredEnums = context.SyntaxProvider
				.CreateSyntaxProvider(
					predicate: (node, ct) => node is EnumDeclarationSyntax,
					transform: (ctx, ct) => {
						EnumDeclarationSyntax enumDeclaration = (EnumDeclarationSyntax)ctx.Node;
						INamedTypeSymbol? enumSymbol = ctx.SemanticModel.GetDeclaredSymbol(enumDeclaration, ct) as INamedTypeSymbol;
						return enumSymbol?.ToFullDisplayString();
					})
				.WhereNotNull();

			IncrementalValueProvider<(EquatableArray<ParameterParser> neededParamParsers, EquatableArray<BuilderToGenerate> neededMiscBuilders)> needed = factoriesSpecs
				.Collect()
				.Combine(availableBuilders.Collect())
				.Combine(availableParsers.Collect())
				.Combine(context.CompilationProvider)
				.Flatten()
				.Select(static (i, _) => FilterParamParsers(i.Item1, i.Item2, i.Item3, i.Item4));

			IncrementalValuesProvider<FactoryToGenerate> factoriesToGenerate = factoriesSpecs
				.Combine(availableBuilders.Collect())
				.Combine(availableParsers.Collect())
				.Combine(needed)
				.Combine(context.CompilationProvider)
				.Flatten()
				.Select(static (i, _) => FilterFactoriesToGenerate(i.Item1, i.Item2, i.Item3, i.Item4.neededMiscBuilders, i.Item4.neededParamParsers, i.Item5))
				.WhereNotNull();

			IncrementalValueProvider<(EquatableArray<BuilderToGenerate> builders, EquatableArray<ParameterParser> parsers)> allBuildersParsers = needed
				.Combine(availableBuilders.Collect())
				.Combine(availableParsers.Collect())
				.Flatten()
				.Select(static (i, _) => CollectAllBuildersParsers(i.Item1, i.Item2, i.Item3, i.Item4));

			// Generate source code for each factory
			context.RegisterSourceOutput(factoriesToGenerate.Combine(allBuildersParsers).Combine(context.CompilationProvider).Flatten(),
				static (spc, source) => ExecuteFactory(source.Item1, source.Item2.builders, source.Item2.parsers, spc, source.Item3));

			// Generate source code for additional parsers
			context.RegisterSourceOutput(needed.Combine(availableParsers.Collect()).Combine(context.CompilationProvider).Flatten(),
				static (spc, source) => ExecuteParamParser(source.Item1, source.Item3, spc, source.Item4));

			// Generate source code for misc builders
			context.RegisterSourceOutput(needed.Combine(availableParsers.Collect()).Combine(context.CompilationProvider).Flatten(),
				static (spc, source) => ExecuteMiscBuilders(source.Item2, source.Item1.Concat(source.Item3).ToArray(), spc, source.Item4));

			// Generate documentation code for builders
			context.RegisterSourceOutput(availableBuilders.Collect().Combine(availableParsers.Collect()).Combine(needed).Combine(factoriesToGenerate.Collect()).Combine(context.CompilationProvider).Flatten(),
				static (spc, source) => ExecuteBuilderDocumentation(source.Item4, source.Item1, source.Item2, source.Item3.neededMiscBuilders, source.Item3.neededParamParsers, spc, source.Item5));

			// Generate documentation code for enums
			context.RegisterSourceOutput(declaredEnums.Collect().Combine(context.CompilationProvider),
				static (spc, source) => ExecuteEnumDocumentation(source.Left, spc, source.Right));

			// Generate documentation linkup code for each factory
			context.RegisterSourceOutput(factoriesToGenerate,
				static (spc, source) => ExecuteFactoryDocumentation(source, spc));
		}

		private static EquatableArray<FactorySpecification> GetFactorySpecs(GeneratorAttributeSyntaxContext ctx) {
			if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol || ctx.TargetNode is not TypeDeclarationSyntax declarationNode) {
				// something went wrong
				return new EquatableArray<FactorySpecification>(Array.Empty<FactorySpecification>());
			}

				List<FactorySpecification> result = new List<FactorySpecification>();

			foreach(AttributeData attr in ctx.Attributes) {
				ITypeSymbol? factoryTypeSymbol = (ITypeSymbol?)attr.ConstructorArguments[0].Value;
				if (factoryTypeSymbol is null) {
					// Malformed attribute
					continue;
				}

				ITypeSymbol[] requiredParams = attr.ConstructorArguments[1].Values.Select(t => (ITypeSymbol)t.Value!).ToArray();
				string[] requiredParamNames = attr.ConstructorArguments[2].Values.Select(t => (string)t.Value!).ToArray();
				bool[] excludeRequiredParams = attr.ConstructorArguments[3].Values.Select(t => (bool)t.Value!).ToArray();
				ITypeSymbol? defaultType = (ITypeSymbol?)attr.ConstructorArguments[4].Value;

				result.Add(FactorySpecification.Build(classSymbol, declarationNode, factoryTypeSymbol, requiredParams, requiredParamNames, excludeRequiredParams, defaultType, ctx.SemanticModel.Compilation));
			}

			return new EquatableArray<FactorySpecification>(result.ToArray());
		}

		public record class FactorySpecification {
			public readonly string Namespace;
			public readonly string Name;
			public readonly bool IsStatic;
			public readonly bool IsPartial;
			public readonly TypeData FactoryType;

			public readonly EquatableArray<RequiredParameter> RequiredParameters;

			public readonly TypeData? DefaultType;

			public FactorySpecification(string @namespace, string name, bool isStatic, bool isPartial, TypeData factoryType, IList<RequiredParameter> requiredParams, TypeData? defaultType) {
				Namespace = @namespace;
				Name = name;
				IsStatic = isStatic;
				IsPartial = isPartial;
				FactoryType = factoryType;

				RequiredParameters = new EquatableArray<RequiredParameter>(requiredParams.ToArray());

				DefaultType = defaultType;
			}

			public static FactorySpecification Build(INamedTypeSymbol classSymbol, TypeDeclarationSyntax declarationNode, ITypeSymbol factoryTypeSymbol, ITypeSymbol[] requiredParams, string[] requiredParamNames, bool[] excludeRequiredParams, ITypeSymbol? defaultType, Compilation compilation) {
				return new FactorySpecification(
					classSymbol.ContainingNamespace.ToFullDisplayString(),
					classSymbol.Name,
					classSymbol.IsStatic,
					declarationNode.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)),
					TypeData.Create(factoryTypeSymbol, compilation),
					requiredParams.Zip(requiredParamNames, excludeRequiredParams, (t, n, e) => RequiredParameter.Build(t, n, e, compilation)).ToArray(),
					defaultType is not null ? TypeData.Create(defaultType, compilation) : null
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

			public static RequiredParameter Build(ITypeSymbol paramType, string paramName, bool exclude, Compilation compilation) {
				return new RequiredParameter(
					paramName,
					TypeData.Create(paramType, compilation),
					exclude
				);
			}
		}

		private static FactoryToGenerate? FilterFactoriesToGenerate(FactorySpecification spec, ImmutableArray<AvailableBuilder> availableBuilders, ImmutableArray<ParameterParser> availableParsers, EquatableArray<BuilderToGenerate> neededBuilders, EquatableArray<ParameterParser> neededParsers, Compilation compilation) {
			INamedTypeSymbol? factoryBuildType = spec.FactoryType.GetSymbol(compilation)as INamedTypeSymbol;

			if (factoryBuildType is null) {
				return null;
			}

			AvailableBuilder[] factoryBuilders = FilterFactoryBuilders(spec, factoryBuildType, availableBuilders, compilation).ToArray();

			bool isSingleton = factoryBuilders.Length == 1 && spec.FactoryType == factoryBuilders[0].BuilderType && spec.FactoryType == factoryBuilders[0].ConcreteBuilderType;

			BuilderToGenerate[] factoryBuildersToGenerate = factoryBuilders.Select(builder => {
				string builderMethodName;
				if (isSingleton) {
					builderMethodName = $"Build_{builder.TypeName}";
				}
				else {
					builderMethodName = $"Build_{spec.FactoryType.Name}_{builder.TypeName}";
				}

				return new BuilderToGenerate(builder, spec.RequiredParameters, spec.Namespace, spec.Name, builderMethodName);
			}).OrderBy(b => b.Builder.ConcreteBuilderType.Name).ToArray();

			AvailableBuilder? defaultBuilder = factoryBuilders.FirstOrDefault(b => b.ConcreteBuilderType == spec.DefaultType);

			return new FactoryToGenerate(spec, defaultBuilder, new EquatableArray<BuilderToGenerate>(factoryBuildersToGenerate), isSingleton);
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

		private static (EquatableArray<BuilderToGenerate>, EquatableArray<ParameterParser>) CollectAllBuildersParsers(EquatableArray<ParameterParser> neededParsers, EquatableArray<BuilderToGenerate> neededBuilders, ImmutableArray<AvailableBuilder> providedBuilders, ImmutableArray<ParameterParser> providedParsers) {
			return (
					//new EquatableArray<AvailableBuilder>(providedBuilders.Concat(neededBuilders.Select(b => b.Builder)).ToArray()),
					new EquatableArray<BuilderToGenerate>(neededBuilders.ToArray()),
					new EquatableArray<ParameterParser>(providedParsers.Concat(neededParsers).ToArray())
				);
		}

		private static AvailableBuilder? GetAvailableBuilders(GeneratorAttributeSyntaxContext ctx) {
			if (ctx.TargetSymbol is not IMethodSymbol methodSymbol) {
				// something went wrong
				return null;
			}

			if (!methodSymbol.IsStatic || !methodSymbol.DeclaredAccessibility.HasFlag(Accessibility.Public)) {
				// Builder method must be public and static
				return null;
			}

			AttributeData attr = ctx.Attributes[0];
			ITypeSymbol? builderTypeSymbol = (ITypeSymbol?)attr.ConstructorArguments[0].Value;
			if (builderTypeSymbol is null) {
				// Malformed attribute
				return null;
			}

			string? builderName = attr.NamedArguments.Length > 0 ? (string?)attr.NamedArguments[0].Value.Value : null;

			return AvailableBuilder.Build(methodSymbol, builderTypeSymbol, builderName, ctx.SemanticModel.Compilation);
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
			public string Name => ProvidedName ?? TypeName; // Final name to be used for builder

			public readonly EquatableArray<BuilderParameter> Parameters;
			public bool CanCallWithEmptyArgs => Parameters.All(p => p.HasDefault);

			public readonly BuilderStructure Structure;

			public AvailableBuilder(string @namespace, string fullTypeName, string typeName, string methodName, TypeData buildType, TypeData concreteBuilderType, string? providedName, IList<BuilderParameter> parameters, BuilderStructure builderStructure) {
				Namespace = @namespace;
				FullTypeName = fullTypeName;
				TypeName = typeName;
				MethodName = methodName;

				BuilderType = buildType;

				ConcreteBuilderType = concreteBuilderType;

				ProvidedName = providedName;

				Parameters = new EquatableArray<BuilderParameter>(parameters.ToArray());

				this.Structure = builderStructure;
			}

			private static BuilderStructure GetBuilderStructure(IMethodSymbol methodSymbol) {
				if (methodSymbol.GetAttributes("SharpSheets.Parsing.GroupedArgumentBuilderAttribute").Any()) {
					return BuilderStructure.GROUPED;
				}
				else if (methodSymbol.GetAttributes("SharpSheets.Parsing.SupplementedArgumentBuilderAttribute").Any()) {
					return BuilderStructure.SUPPLEMENTED;
				}
				else if (methodSymbol.GetAttributes("SharpSheets.Parsing.ExpandedArgumentBuilderAttribute").FirstOrDefault() is AttributeData expandAttr) {
					if (((bool?)expandAttr.GetNamedArgument("Defer")?.Value) ?? false) {
						return BuilderStructure.EXPANDED_DEFERRED;
					}
					else {
						return BuilderStructure.EXPANDED;
					}
				}

				return BuilderStructure.NONE;
			}

			public static AvailableBuilder Build(IMethodSymbol methodSymbol, ITypeSymbol builderTypeSymbol, string? builderName, Compilation compilation) {
				return new AvailableBuilder(
					methodSymbol.ContainingType.ContainingNamespace.ToFullDisplayString(),
					methodSymbol.ContainingType.ToFullDisplayString(),
					methodSymbol.ContainingType.Name,
					methodSymbol.Name,
					TypeData.Create(builderTypeSymbol, compilation),
					TypeData.Create(methodSymbol.ReturnType, compilation),
					builderName,
					methodSymbol.Parameters.Select(p => BuilderParameter.Create(p, compilation)).ToArray(),
					GetBuilderStructure(methodSymbol)
				);
			}
		}

		public record class BuilderParameter {
			public readonly string Name;
			public readonly TypeData Type;
			public readonly string? DefaultValue;
			public bool HasDefault => DefaultValue is not null;
			public readonly bool IsLocal;
			public readonly bool IsOptional;
			public readonly bool IsBuildErrors;
			public readonly bool Exclude;

			public BuilderParameter(string name, TypeData type, string? defaultValue, bool isLocal, bool isOptional, bool isBuildErrors, bool exclude) {
				Name = name;
				Type = type;
				DefaultValue = defaultValue;
				IsLocal = isLocal;
				IsOptional = isOptional;
				IsBuildErrors = isBuildErrors;
				Exclude = exclude;
			}

			private static string? GetValueString(object? value, ITypeSymbol type) {
				if (value is bool boolean) {
					return boolean.ToCodeString();
				}
				else if (value is string text) {
					return text.ToRepr();
				}
				else if (value is float number) {
					return number.ToCodeString();
				}
				else if (type is INamedTypeSymbol namedType && namedType.IsEnum()) {
					return $"({namedType.ToFullDisplayString()}){value?.ToString()}";
				}
				else if (type.SpecialType == SpecialType.System_UInt32) {
					return $"{value?.ToString()}U";
				}
				else {
					return value?.ToString();
				}
			}

			private static string GetDefaultForType(ITypeSymbol type) {
				return $"default({type.ToFullDisplayString()})";
			}

			public static BuilderParameter Create(IParameterSymbol param, Compilation compilation) {
				AttributeData? buildErrorsAttr = param.GetAttributes("SharpSheets.Parsing.BuildErrorsAttribute").FirstOrDefault();
				AttributeData? propAttr = param.GetAttributes("SharpSheets.Parsing.PropertyAttribute", "SharpSheets.Parsing.LocalPropertyAttribute").FirstOrDefault();

				bool isBuildErrors = buildErrorsAttr is not null;
				bool isLocal = propAttr?.AttributeClass?.Name == "LocalPropertyAttribute";
				bool exclude = (bool?)propAttr?.GetNamedArgument("Exclude")?.Value ?? false;

				string? defaultValue = (!isBuildErrors && param.HasExplicitDefaultValue) ? (GetValueString(param.ExplicitDefaultValue, param.Type) ?? ((param.Type.IsValueType && param.Type.NullableAnnotation != NullableAnnotation.Annotated) ? GetDefaultForType(param.Type) : "null")) : null;

				return new BuilderParameter(
					param.Name,
					TypeData.Create(param.Type, compilation),
					defaultValue,
					isLocal,
					param.IsOptional,
					isBuildErrors,
					exclude);
			}
		}

		private static ParameterParser? GetParameterParser(GeneratorAttributeSyntaxContext ctx) {
			if (ctx.TargetSymbol is not IMethodSymbol methodSymbol) {
				// something went wrong
				return null;
			}
			if (!methodSymbol.IsStatic || !methodSymbol.DeclaredAccessibility.HasFlag(Accessibility.Public)) {
				// Parser method must be public and static
				return null;
			}
			if (!(methodSymbol.Parameters.Length == 1 || methodSymbol.Parameters.Length == 2)) {
				// Can only have 1 or 2 parameters
				return null;
			}
			if (methodSymbol.Parameters[0].Type.SpecialType != SpecialType.System_String) {
				// First parameter must be a string
				return null;
			}
			if(methodSymbol.Parameters.Length == 2 && methodSymbol.Parameters[1].Type.ToFullDisplayString() != "SharpSheets.Utilities.DirectoryPath") {
				// If a second parameter is provided, it must be a source path
				return null;
			}

			ITypeSymbol parserType = methodSymbol.ReturnType;

			bool needsSourceDirectory = methodSymbol.Parameters.Length == 2;

			return ParameterParser.Build(methodSymbol, parserType, needsSourceDirectory, ctx.SemanticModel.Compilation);
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

			private static ParameterParser Build(string fullTypeName, string methodName, ITypeSymbol parserType, bool needsSourceDirectory, Compilation compilation) {
				return new ParameterParser(
					fullTypeName,
					methodName,
					TypeData.Create(parserType, compilation),
					needsSourceDirectory
				);
			}

			public static ParameterParser Build(IMethodSymbol methodSymbol, ITypeSymbol parserType, bool needsSourceDirectory, Compilation compilation) {
				return Build(
					methodSymbol.ContainingType.ToFullDisplayString(),
					methodSymbol.Name,
					parserType,
					needsSourceDirectory,
					compilation
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

			public static ParameterParser GetGeneratedParser(ITypeSymbol parserType, Compilation compilation, bool needsSourceDirectory = false) {
				return Build(
					"SharpSheets.Parsing.ParameterParsers",
					$"Parser_{GetParserTypeName(parserType)}",
					parserType,
					needsSourceDirectory,
					compilation);
			}
		}

		private static bool RequiredParametersSatisfied(EquatableArray<RequiredParameter> required, EquatableArray<BuilderParameter> parameters) {
			if (parameters.Count < required.Count) { return false; }

			int count = 0;
			for (int p = 0; p < parameters.Count && count < required.Count; p++) {
				if (parameters[p].IsBuildErrors) {
					continue;
				}
				else if (required[count].Type.FullName != parameters[p].Type.Minimal) {
					return false;
				}
				count++;
			}

			if (count != required.Count) { return false; }

			return true;
		}

		private static IEnumerable<AvailableBuilder> FilterFactoryBuilders(FactorySpecification factory, INamedTypeSymbol factoryBuildType, IEnumerable<AvailableBuilder> builders, Compilation compilation) {
			foreach (AvailableBuilder builder in builders) {
				INamedTypeSymbol? builderType = builder.BuilderType.GetSymbol(compilation) as INamedTypeSymbol;

				if (builderType is null) {
					// Malformed somehow
					continue;
				}
				else if (!compilation.HasImplicitConversion(builderType, factoryBuildType)) {
					// Wrong builder type
					continue;
				}
				else if (!RequiredParametersSatisfied(factory.RequiredParameters, builder.Parameters)) {
					// Builder does not have required parameters
					continue;
				}

				yield return builder;
			}
		}

		static void ExecuteFactory(FactoryToGenerate factory, EquatableArray<BuilderToGenerate> allBuilders, EquatableArray<ParameterParser> allParsers, SourceProductionContext context, Compilation compilation) {
			// generate the source code and add it to the output
			string? result = GenerateFactoryCode(factory, allBuilders, allParsers, compilation);
			// Create a separate partial class file
			if (!string.IsNullOrEmpty(result)) {
				context.AddSource($"Factories.{factory.Spec.Name}.{factory.Spec.FactoryType.Name}.g.cs", SourceText.From(result!, Encoding.UTF8));
			}
		}

		static string? GenerateFactoryCode(FactoryToGenerate factory, EquatableArray<BuilderToGenerate> allBuilders, EquatableArray<ParameterParser> allParsers, Compilation compilation) {
			// Need to know if we can build a given pattern name (dictionary lookup)
			// Need to actually build a given pattern
			// Need arguments: IContext context, DirectoryPath source, out SharpParsingException[] buildErrors
			// Can we have some mechanism for passing in known arguments? This should allow for optionally known args
			// Essentially replacing: object? SharpFactory.Build(MethodInfo builder, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, object[] firstParameters, out SharpParsingException[] buildErrors)
			// Can provide a list of known arguments in the attribute, which can be included in the generated method? (How would we name them?)
			// Should also allow for case with only 1 builder (i.e. no switch statement) - still need default case, though, as we still need to check for valid name

			INamedTypeSymbol? factoryBuildType = factory.Spec.FactoryType.GetSymbol(compilation) as INamedTypeSymbol;

			if (factoryBuildType is null) {
				return null;
			}

			Dictionary<string, ParameterParser> parserLookup = allParsers.ToDictionary(p => p.ParserType.FullName);
			Dictionary<string, BuilderToGenerate> builderLookup = allBuilders.ToDictionary(b => b.Builder.BuilderType.FullName);

			string stringComparerName = "System.StringComparer.OrdinalIgnoreCase";

			bool isSingleton = factory.IsSingleton;
			bool factoryNeedsShapeFactory = NeedsShapeFactory(factory.Builders);
			bool factoryNeedsWidgetFactory = NeedsWidgetFactory(factory.Builders);

			StringBuilder sb = new StringBuilder();
			sb.Append(@$"// <auto-generated/>
#nullable enable
#pragma warning disable

using System.Linq;

namespace {factory.Spec.Namespace} {{
	public{(factory.Spec.IsStatic ? " static" : "")}{(factory.Spec.IsPartial ? " partial" : "")} class {factory.Spec.Name} {{
");

			if (!isSingleton) {
				sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static {factory.Spec.FactoryType.Type}? Build_{factory.Spec.FactoryType.Name}(string buildName, SharpSheets.Parsing.IContext context{(factory.Spec.RequiredParameters.Count > 0 ? (", " + string.Join(", ", factory.Spec.RequiredParameters.Select(p => $"{p.Type.FullName} {p.Name}"))) : "")}, SharpSheets.Utilities.DirectoryPath source,{(factoryNeedsWidgetFactory ? " SharpSheets.Widgets.WidgetFactory widgetFactory," : "")}{(factoryNeedsShapeFactory ? " SharpSheets.Shapes.ShapeFactory shapeFactory," : "")} out SharpSheets.Exceptions.SharpParsingException[] buildErrors) {{
			switch (buildName.ToLowerInvariant()) {{");

				foreach (BuilderToGenerate builder in factory.Builders) {
					bool builderNeedsShapeFactory = NeedsShapeFactory(builder);
					bool builderNeedsWidgetFactory = NeedsWidgetFactory(builder);

					sb.Append(@$"
				case ""{builder.Builder.Name.ToLowerInvariant()}"":");

					sb.Append(@$"
					return {builder.MethodName}(context{(factory.Spec.RequiredParameters.Count > 0 ? (", " + string.Join(", ", factory.Spec.RequiredParameters.Select(p => $"{p.Name}"))) : "")}, source,{(builderNeedsWidgetFactory ? " widgetFactory," : "")}{(builderNeedsShapeFactory ? " shapeFactory," : "")} out buildErrors);");
				}

				sb.Append(@$"
				default:
					buildErrors = new SharpSheets.Exceptions.SharpParsingException[] {{
							new SharpSheets.Exceptions.SharpParsingException(context.Location, $""Unrecognized {factory.Spec.FactoryType.Name} name: {{buildName}}"")
						}};
					return null;
			}}
		}}
");
			}

			foreach (BuilderToGenerate builder in factory.Builders) {
				GenerateBuilderCode(sb, builder, parserLookup, builderLookup, true, compilation);
				sb.Append('\n');
			}

			if (!isSingleton) {
				if (factory.Builders.Count > 1) {

					string knownTypesName = $"__generated__knownTypes_{factory.Spec.FactoryType.Name}";

					// Start HashSet
					sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		{GeneratorMarkers.NeverEditorBrowsableAttr}
		private static readonly System.Collections.Generic.HashSet<string> {knownTypesName} = new System.Collections.Generic.HashSet<string>({stringComparerName}) {{");

					foreach (BuilderToGenerate builder in factory.Builders) {
						sb.Append($"\n\t\t\t\t\"{builder.Builder.Name}\",");
					}

					// End HashSet
					sb.Append(@"
			};
");


					sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static bool CanBuild_{factory.Spec.FactoryType.Name}(string name) {{
			return {knownTypesName}.Contains(name);
		}}");
				}
				else if (factory.Builders.Count == 1) {
					sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static bool CanBuild_{factory.Spec.FactoryType.Name}(string name) {{
			return {stringComparerName}.Equals(name, ""{factory.Builders[0].Builder.Name}"");
		}}");
				}
				else {
					sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static bool CanBuild_{factory.Spec.FactoryType.Name}(string name) {{
			return false;
		}}
");
				}
			}

			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
		}

		static void ExecuteFactoryDocumentation(FactoryToGenerate factory, SourceProductionContext context) {
			// generate the source code and add it to the output
			string? result = GenerateFactoryDocumentation(factory);
			// Create a separate partial class file
			if (!string.IsNullOrEmpty(result)) {
				context.AddSource($"Factories.{factory.Spec.Name}.{factory.Spec.FactoryType.Name}.Documentation.g.cs", SourceText.From(result!, Encoding.UTF8));
			}
		}

		static string? GenerateFactoryDocumentation(FactoryToGenerate factory) {

			string nameLookupDictionaryVariable = $"__generated__{factory.Spec.FactoryType.Name}_details_namelookup";
			string typeLookupDictionaryVariable = $"__generated__{factory.Spec.FactoryType.Name}_details_typelookup";

			StringBuilder sb = new StringBuilder();
			sb.Append(@$"// <auto-generated/>
#nullable enable
#pragma warning disable

using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace {factory.Spec.Namespace} {{
	public{(factory.Spec.IsStatic ? " static" : "")}{(factory.Spec.IsPartial ? " partial" : "")} class {factory.Spec.Name} {{
");

			// Name lookup dictionary
			sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		{GeneratorMarkers.NeverEditorBrowsableAttr}
		private static readonly System.Collections.Generic.Dictionary<string, Func<SharpSheets.Documentation.BuilderDetails>> {nameLookupDictionaryVariable} = new System.Collections.Generic.Dictionary<string, Func<SharpSheets.Documentation.BuilderDetails>>(StringComparer.InvariantCultureIgnoreCase) {{");

			foreach (BuilderToGenerate builder in factory.Builders) {

				sb.Append(@$"
				{{ {builder.Builder.Name.ToRepr()}, () => SharpSheets.Documentation.BuilderDocs.{GetDocumentationVariableName(builder.Builder)} }},");

			}

			sb.Append(@$"
			}};
");

			// Type lookup dictionary
			sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		{GeneratorMarkers.NeverEditorBrowsableAttr}
		private static readonly System.Collections.Generic.Dictionary<Type, Func<SharpSheets.Documentation.BuilderDetails>> {typeLookupDictionaryVariable} = new System.Collections.Generic.Dictionary<Type, Func<SharpSheets.Documentation.BuilderDetails>>() {{");

			foreach (BuilderToGenerate builder in factory.Builders) {

				sb.Append(@$"
				{{ typeof({builder.Builder.ConcreteBuilderType.Type}), () => SharpSheets.Documentation.BuilderDocs.{GetDocumentationVariableName(builder.Builder)} }},");

			}

			sb.Append(@$"
			}};
");

			sb.Append(@$"
		public static bool Contains{factory.Spec.FactoryType.Name}Details(string name) => {nameLookupDictionaryVariable}.ContainsKey(name);
		public static bool Contains{factory.Spec.FactoryType.Name}Details(Type type) => {typeLookupDictionaryVariable}.ContainsKey(type);

		public static bool TryGet{factory.Spec.FactoryType.Name}Details(string name, [MaybeNullWhen(false)] out SharpSheets.Documentation.BuilderDetails builder) {{
			if ({nameLookupDictionaryVariable}.TryGetValue(name, out Func<SharpSheets.Documentation.BuilderDetails> getter)) {{
				builder = getter();
				return true;
			}}
			else {{
				builder = null;
				return false;
			}}
		}}

		public static bool TryGet{factory.Spec.FactoryType.Name}Details(Type type, [MaybeNullWhen(false)] out SharpSheets.Documentation.BuilderDetails builder) {{
			if ({typeLookupDictionaryVariable}.TryGetValue(type, out Func<SharpSheets.Documentation.BuilderDetails> getter)) {{
				builder = getter();
				return true;
			}}
			else {{
				builder = null;
				return false;
			}}
		}}

		public static IEnumerable<KeyValuePair<string, SharpSheets.Documentation.BuilderDetails>> Get{factory.Spec.FactoryType.Name}BuilderNames() => {nameLookupDictionaryVariable}.Select(kv => new KeyValuePair<string, SharpSheets.Documentation.BuilderDetails>(kv.Key, kv.Value()));
");



			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
		}

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
		}

		[Flags]
		public enum ShapeMakerArgs {
			NONE = 0b000,
			ASPECT = 0b001,
			//BOX = 0b010,
			NAME = 0b100,
			ASPECT_NAME = 0b101,
			//BOX_NAME = 0b110
		}
		// Bit unfortunate these are hard-coded
		private static readonly Dictionary<string, (string, ShapeMakerArgs)> shapeMakerLookup = new Dictionary<string, (string, ShapeMakerArgs)>() {
			{ "SharpSheets.Shapes.IContainerShape", ("MakeContainer", ShapeMakerArgs.ASPECT_NAME) },
			{ "SharpSheets.Shapes.IBox", ("MakeBox", ShapeMakerArgs.ASPECT) },
			{ "SharpSheets.Shapes.ILabelledBox", ("MakeLabelledBox", ShapeMakerArgs.ASPECT) },
			{ "SharpSheets.Shapes.ITitledBox", ("MakeTitledBox", ShapeMakerArgs.ASPECT_NAME) },
			//{ "SharpSheets.Shapes.ITitleStyledBox", ("MakeTitleStyle", ShapeMakerArgs.BOX_NAME) },
			{ "SharpSheets.Shapes.IEntriedShape", ("MakeEntried", ShapeMakerArgs.ASPECT) },
			{ "SharpSheets.Shapes.IBar", ("MakeBar", ShapeMakerArgs.ASPECT) },
			{ "SharpSheets.Shapes.IUsageBar", ("MakeUsageBar", ShapeMakerArgs.ASPECT) },
			{ "SharpSheets.Shapes.IDetail", ("MakeDetail", ShapeMakerArgs.NONE) }
		};

		private static readonly string childHolderType = "SharpSheets.Parsing.ChildHolder";

		public static bool NeedsShapeFactory(AvailableBuilder builder) {
			return builder.Parameters.Any(p => shapeMakerLookup.ContainsKey(p.Type.Minimal));
		}
		public static bool NeedsShapeFactory(BuilderToGenerate builder) {
			return NeedsShapeFactory(builder.Builder);
		}
		public static bool NeedsShapeFactory(IEnumerable<AvailableBuilder> builders) {
			return builders.Any(NeedsShapeFactory);
		}
		public static bool NeedsShapeFactory(IEnumerable<BuilderToGenerate> builders) {
			return builders.Any(NeedsShapeFactory);
		}

		public static bool NeedsWidgetFactory(AvailableBuilder builder) {
			return builder.Parameters.Any(p => p.Type.Minimal.Contains(childHolderType));
		}
		public static bool NeedsWidgetFactory(BuilderToGenerate builder) {
			return NeedsWidgetFactory(builder.Builder);
		}
		public static bool NeedsWidgetFactory(IEnumerable<AvailableBuilder> builders) {
			return builders.Any(NeedsWidgetFactory);
		}
		public static bool NeedsWidgetFactory(IEnumerable<BuilderToGenerate> builders) {
			return builders.Any(NeedsWidgetFactory);
		}

		public static string NormaliseParameterName(string name) {
			return name.Replace("_", "").ToLowerInvariant();
		}

		private static void GenerateBuilderCode(StringBuilder sb, BuilderToGenerate builder, Dictionary<string, ParameterParser> parserLookup, Dictionary<string, BuilderToGenerate> builderLookup, bool includeGeneratedAttributes, Compilation compilation) {
			bool builderNeedsShapeFactory = NeedsShapeFactory(builder.Builder);
			bool builderNeedsWidgetFactory = NeedsWidgetFactory(builder.Builder);

			bool returnNeedsNullableAnnotation = builder.Builder.ConcreteBuilderType.IsNullable || !builder.Builder.CanCallWithEmptyArgs;

			string builderReturnTypeFull = $"{builder.Builder.FullTypeName}{(returnNeedsNullableAnnotation ? "?" : "")}";
			string errorListVariableName = "@buildErrorsList";
			string builderErrorsParamVariable = "@buildErrorsParamList";
			bool needsBuilderErrorsParam = false;

			if (includeGeneratedAttributes) {
				sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}");
			}

			sb.Append(@$"
		public static {builderReturnTypeFull} {builder.MethodName}(SharpSheets.Parsing.IContext context{(builder.RequiredParameters.Count > 0 ? (", " + string.Join(", ", builder.RequiredParameters.Select(p => $"{p.Type.FullName} {p.Name}"))) : "")}, SharpSheets.Utilities.DirectoryPath source,{(builderNeedsWidgetFactory ? " SharpSheets.Widgets.WidgetFactory widgetFactory," : "")}{(builderNeedsShapeFactory ? " SharpSheets.Shapes.ShapeFactory shapeFactory," : "")} out SharpSheets.Exceptions.SharpParsingException[] buildErrors) {{
			System.Collections.Generic.List<SharpSheets.Exceptions.SharpParsingException> {errorListVariableName} = new System.Collections.Generic.List<SharpSheets.Exceptions.SharpParsingException>();
");
			// {builder.Builder.Namespace}, {builder.Builder.FullTypeName}, {builder.MethodName}, {builder.Builder.BuilderType}, {builder.Builder.Name}, {builder.Builder.Structure}");

			//List<string> paramVariables = new List<string>(requiredParams.Select(r => r.Name));
			List<string> paramVariables = new List<string>(builder.Builder.Parameters.Count);

			int count = 0;
			foreach ((int pIdx, BuilderParameter param) in builder.Builder.Parameters.Enumerate()) {
				if (param.IsBuildErrors) {
					paramVariables.Add(builderErrorsParamVariable);
					needsBuilderErrorsParam = true;
					continue;
				}

				if (count < builder.RequiredParameters.Count) {
					paramVariables.Add(builder.RequiredParameters[count].Name);
					count++;
					continue;
				}
				else {
					count++;
				}

				ITypeSymbol? paramResolvedType = param.Type.GetSymbol(compilation); // compilation.ResolveTypeKey(param.Type.FullName);
				ITypeSymbol? paramReducedType = paramResolvedType is not null ? compilation.ReduceParameterType(paramResolvedType) : null;

				if (!parserLookup.TryGetValue(param.Type.Minimal, out ParameterParser? parser)) {
					parser = null;
				}

				BuilderToGenerate? paramBuilder = builderLookup.TryGetValue(param.Type.Minimal, out BuilderToGenerate found) ? found : null;

				/*
				sb.Append(@$"
			//{(param.IsLocal ? " (local)" : "")} {param.FullType} {param.Name}{(param.DefaultValue is not null ? $" = {param.DefaultValue}" : "")}
				// {param.FullType}, {param.MinimalType}, {paramResolvedType?.ToFullDisplayString()}, {paramReducedType?.ToFullDisplayString()}
				// {(paramResolvedType is INamedTypeSymbol named1 && named1.IsGenericType ? named1.ConstructUnboundGenericType().ToFullDisplayString() : "NOT GENERIC")}
				// {param.MinimalType} => {(parser is not null ? ($"{parser.FullTypeName}.{parser.MethodName}") : "NO PARSER")}
				// {param.MinimalType} => {(paramBuilder is not null ? ($"{paramBuilder.Namespace}.{paramBuilder.TypeName}.{paramBuilder.MethodName}") : "NO BUILDER")}");
				*/
				
				string normParamName = NormaliseParameterName(param.Name);
				string valueVariable = $"{param.Name}_value";

				if (paramResolvedType is not null && paramResolvedType.SpecialType == SpecialType.System_Boolean) {
					sb.Append(@$"
			bool {valueVariable} = context.HasFlag(""{normParamName}"", {param.IsLocal.ToCodeString()}, context) ? context.GetFlag(""{normParamName}"", {param.IsLocal.ToCodeString()}, context) : {param.DefaultValue ?? "false"};");
				}
				else if (paramResolvedType is INamedTypeSymbol namedListType && namedListType.IsGenericList(out ITypeSymbol listElemType) && compilation.ReduceParameterType(listElemType) is ITypeSymbol reducedListElemType && parserLookup.TryGetValue(reducedListElemType.ToFullDisplayString(), out ParameterParser listElemParser)) {
					string reducedListTypeName = compilation.ReduceParameterType(namedListType).ToFullDisplayString();
					sb.Append(@$"
			{reducedListTypeName} {valueVariable} = new {reducedListTypeName}();
			foreach (ContextValue<string> @entry in context.GetEntries(context)) {{
				try {{
					{listElemType.ToFullDisplayString()} @entryParsed = {listElemParser.FullTypeName}.{listElemParser.MethodName}(@entry.Value{(listElemParser.NeedsSourceDirectory ? ", source" : "")});
					{valueVariable}.Add(@entryParsed);
				}}
				catch (System.FormatException e) {{
					{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException(@entry.Location, e.Message, e));
				}}
			}}");
				}
				else if (paramResolvedType is INamedTypeSymbol namedNumberedType && namedNumberedType.IsGenericNumbered(out ITypeSymbol numberedElemType) && compilation.ReduceParameterType(numberedElemType) is ITypeSymbol reducedNumberedElemType) {
					string reducedNumberedTypeName = compilation.ReduceParameterType(namedNumberedType).ToFullDisplayString();
					string reducedNumberedElemTypeName = reducedNumberedElemType.ToFullDisplayString();
					string numberedElemFullTypeName = numberedElemType.ToFullDisplayString();

					if (!parserLookup.TryGetValue(reducedNumberedElemTypeName, out ParameterParser? numberedElemParser)) {
						numberedElemParser = null;
					}

					if (numberedElemParser is null && reducedNumberedElemTypeName != childHolderType) {
						sb.Append("\nERROR;\n");
						continue;
					}

					string paramNumbersVariable = valueVariable + "_numbers";
					string paramRegexVariable = valueVariable + "_regex";

					sb.Append(@$"
			{reducedNumberedTypeName} {valueVariable} = new {reducedNumberedTypeName}();
			System.Text.RegularExpressions.Regex {paramRegexVariable} = new System.Text.RegularExpressions.Regex(@""^{System.Text.RegularExpressions.Regex.Escape(normParamName)}(?<number>[1-9][0-9]*)(?:\.|$)"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);");

					sb.Append(@$"
			int[] {paramNumbersVariable} = context");

					if (reducedNumberedElemTypeName == childHolderType) {
						sb.Append(@$"
				.GetAllNamedChildren({param.IsLocal.ToCodeString()})
				.Select(n => {paramRegexVariable}.Match(n))");
					}
					else if (numberedElemParser is not null) {
						sb.Append(@$"
				.GetAllProperties({param.IsLocal.ToCodeString()}).Select(p => p.Name)
				.Concat(context.GetAllFlags({param.IsLocal.ToCodeString()}).Select(p => p.Name))
				.Select(n => {paramRegexVariable}.Match(n))");
					}

					sb.Append(@$"
				.Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value))
				.Distinct().OrderBy(i => i)
				.ToArray();");

					sb.Append(@$"
			for (int i = 0; i < {paramNumbersVariable}.Length; i++) {{
				int num = {paramNumbersVariable}[i];
				int index = num - 1;
				string numberedName = ""{normParamName}"" + num.ToString();");

					if (reducedNumberedElemTypeName == childHolderType) {
						sb.Append(@$"
				// Try parse ChildHolder");

						string entryValueVariable = valueVariable + "_item";

						string entryChildContextVariable = entryValueVariable + "_childcontext";
						string entryNonRecurseContextVariable = entryValueVariable + "_nonrecurse";
						string entryConstructedChildVariable = entryValueVariable + "_constructed";
						string entryConcreteChildVariable = entryValueVariable + "_concrete";
						string entryBuildErrorsVariable = entryValueVariable + "_buildErrors";

						sb.Append(@$"
				IContext? {entryChildContextVariable} = context.GetNamedChild(numberedName, {param.IsLocal.ToCodeString()}, context);
				if ({entryChildContextVariable} != null) {{
					IContext {entryNonRecurseContextVariable} = new SharpSheets.Parsing.DisallowNamedChildContext({entryChildContextVariable});
					SharpSheets.Widgets.Div? {entryConstructedChildVariable} = widgetFactory.MakeDiv({entryNonRecurseContextVariable}, source, out SharpSheets.Exceptions.SharpParsingException[] {entryBuildErrorsVariable});
					if ({entryConstructedChildVariable} is SharpSheets.Widgets.Div {entryConcreteChildVariable}) {{
						{childHolderType} {entryValueVariable} = new SharpSheets.Parsing.ChildHolder({entryConcreteChildVariable});
						{valueVariable}.Add(index, {entryValueVariable});
					}}
					else {{
						{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException({entryChildContextVariable}.Location, $""Could not build numbered child: {{numberedName}}""));
					}}
					{errorListVariableName}.AddRange({entryBuildErrorsVariable});
				}}
				else {{
					{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException(context.Location, $""Unexpected error, could not find numbered child: {{numberedName}}""));
				}}");

					}
					else if (numberedElemParser is not null) {
						sb.Append(@$"
				// Try parse {numberedElemParser.ParserType.FullName}");

						string entryValueVariable = valueVariable + "_item";
						string entryTextVariable = entryValueVariable + "_str";
						string entryLocVariable = entryValueVariable + "_loc";
						sb.Append(@$"
			string? {entryTextVariable} = context.GetProperty(numberedName, {param.IsLocal.ToCodeString()}, context, null, out DocumentSpan? {entryLocVariable});
			if ({entryTextVariable} != null) {{
				try {{
					{numberedElemFullTypeName} {entryValueVariable} = {numberedElemParser.FullTypeName}.{numberedElemParser.MethodName}({entryTextVariable}{(numberedElemParser.NeedsSourceDirectory ? ", source" : "")});
					{valueVariable}.Add(index, {entryValueVariable});
				}}
				catch (System.FormatException e) {{
					{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException(@entry.Location, e.Message, e));
				}}
			}}
			else {{
				{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException(context.Location, $""Unexpected error, could not find numbered entry: {{numberedName}}""));
			}}");
					}

					sb.Append(@$"
			}}");

				}
				else if (shapeMakerLookup.TryGetValue(param.Type.Minimal, out (string method, ShapeMakerArgs args) shapeMaker)) {
					string paramContextVariable = valueVariable + "_context";
					string paramStyleLocVariable = valueVariable + "_style_loc";
					string paramShapeBuildErrorsVariable = valueVariable + "_buildErrors";

					sb.Append(@$"
			new SharpSheets.Parsing.NamedContext(context, ""{normParamName}"", forceLocal: {param.IsLocal.ToCodeString()}).HasProperty(""style"", {param.IsLocal.ToCodeString()}, context, out DocumentSpan? {paramStyleLocVariable});
			IContext {paramContextVariable} = new SharpSheets.Parsing.NamedContext(context, ""{normParamName}"", location: {paramStyleLocVariable}, forceLocal: {param.IsLocal.ToCodeString()});");


					List<string> shapeMakerArgNames = new List<string>();
					if (shapeMaker.args.HasFlag(ShapeMakerArgs.ASPECT)) {
						string paramAspectVariable = valueVariable + "_aspect";
						string paramAspectStrVariable = paramAspectVariable + "_str";
						string paramAspectLocVariable = paramAspectVariable + "_loc";
						ParameterParser floatParser = parserLookup["float"];

						sb.Append(@$"
			string? {paramAspectStrVariable} = {paramContextVariable}.GetProperty(""aspect"", true, {paramContextVariable}, null, out DocumentSpan? {paramAspectLocVariable}); 
			float {paramAspectVariable};
			if ({paramAspectStrVariable} != null) {{
				try {{
					{paramAspectVariable} = {floatParser.FullTypeName}.{floatParser.MethodName}({paramAspectStrVariable});
				}}
				catch (System.FormatException e) {{
					{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException({paramAspectLocVariable}, e.Message, e));
					{paramAspectVariable} = -1f;
				}}
			}}
			else {{
				{paramAspectVariable} = -1f;
			}}");
						shapeMakerArgNames.Add(paramAspectVariable);
					}
					/*
					if (shapeMaker.args.HasFlag(ShapeMakerArgs.BOX)) {
						string paramBoxVariable = valueVariable + "_box";

						shapeMakerArgNames.Add(paramBoxVariable);
					}
					*/
					if (shapeMaker.args.HasFlag(ShapeMakerArgs.NAME)) {
						string paramNameVariable = valueVariable + "_name";
						string paramNameLocVariable = paramNameVariable + "_loc";
						ParameterParser stringParser = parserLookup["string"];

						sb.Append(@$"
			string {paramNameVariable} = context.GetProperty(""name"", true, context, ""NAME"", {stringParser.FullTypeName}.{stringParser.MethodName}, out DocumentSpan? {paramNameLocVariable});");
						shapeMakerArgNames.Add(paramNameVariable);
					}

					sb.Append(@$"
			{param.Type.FullName} {valueVariable} = shapeFactory.{shapeMaker.method}({paramContextVariable}{(shapeMakerArgNames.Count > 0 ? (", " + string.Join(", ", shapeMakerArgNames)) : "")}, source, out SharpSheets.Exceptions.SharpParsingException[] {paramShapeBuildErrorsVariable});
			{errorListVariableName}.AddRange({paramShapeBuildErrorsVariable});");
				}
				else if (param.Type.Minimal == childHolderType) {
					string paramChildContextVariable = valueVariable + "_childcontext";
					string paramNonRecurseContextVariable = valueVariable + "_nonrecurse";
					string paramConstructedChildVariable = valueVariable + "_constructed";
					string paramConcreteChildVariable = valueVariable + "_concrete";
					string paramBuildErrorsVariable = valueVariable + "_buildErrors";

					sb.Append(@$"
			{param.Type.FullName} {valueVariable};
			IContext? {paramChildContextVariable} = context.GetNamedChild(""{normParamName}"", {param.IsLocal.ToCodeString()}, context);
			if ({paramChildContextVariable} != null) {{
				IContext {paramNonRecurseContextVariable} = new SharpSheets.Parsing.DisallowNamedChildContext({paramChildContextVariable});
				SharpSheets.Widgets.Div? {paramConstructedChildVariable} = widgetFactory.MakeDiv({paramNonRecurseContextVariable}, source, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
				if ({paramConstructedChildVariable} is SharpSheets.Widgets.Div {paramConcreteChildVariable}) {{
					{valueVariable} = new SharpSheets.Parsing.ChildHolder({paramConcreteChildVariable});
				}}
				else {{
					{valueVariable} = default;
					{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException({paramChildContextVariable}.Location, $""Could not build child: {normParamName}""));
				}}
				{errorListVariableName}.AddRange({paramBuildErrorsVariable});
			}}");

					if (!param.IsOptional) {
						sb.Append(@$"
			else {{
				{errorListVariableName}.Add(new SharpSheets.Exceptions.MissingParameterException(context.Location, {normParamName.ToRepr()}, typeof({param.Type.Minimal})));
				{valueVariable} = default; // Note: potentially uninitialized
			}}");
					}
					else {
						sb.Append(@$"
			else {{
				{valueVariable} = default;
			}}");
					}
				}
				else if (parser is not null) {
					string valueTextVariable = $"{param.Name}_str";
					string valueLocVariable = $"{param.Name}_loc";
					sb.Append(@$"
			string? {valueTextVariable} = context.GetProperty(""{normParamName}"", {param.IsLocal.ToCodeString()}, context, null, out DocumentSpan? {valueLocVariable});
			{param.Type.FullName} {valueVariable};
			if ({valueTextVariable} != null) {{
				try {{
					{valueVariable} = {parser.FullTypeName}.{parser.MethodName}({valueTextVariable}{(parser.NeedsSourceDirectory ? ", source" : "")});
				}}
				catch (System.FormatException e) {{
					{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException({valueLocVariable}, e.Message, e));
					{valueVariable} = {param.DefaultValue ?? "NULL"};
				}}
			}}
			else {{
				{valueVariable} = {param.DefaultValue ?? "NULL"};
			}}");
				}
				else if (paramBuilder is not null) {
					string paramBuildErrorsVariable = valueVariable + "_buildErrors";

					if (paramBuilder.Builder.Structure == BuilderStructure.GROUPED) {
						string paramContextVariable = valueVariable + "_context";

						sb.Append(@$"
			SharpSheets.Parsing.IContext {paramContextVariable} = new SharpSheets.Parsing.NamedContext(context, ""{normParamName}"", forceLocal: {param.IsLocal.ToCodeString()});
			{param.Type.FullName} {valueVariable} = {paramBuilder.FullTypeName}.{paramBuilder.MethodName}({paramContextVariable}, source, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
			{errorListVariableName}.AddRange({paramBuildErrorsVariable});");
					}
					else if (paramBuilder.Builder.Structure == BuilderStructure.SUPPLEMENTED) {
						BuilderParameter firstArg = paramBuilder.Builder.Parameters[0];
						string firstArgVariable = valueVariable + "_first";
						string firstArgTextVariable = firstArgVariable + "_str";
						string firstArgLocVariable = firstArgVariable + "_loc";
						string paramContextVariable = valueVariable + "_context";

						if (!parserLookup.TryGetValue(firstArg.Type.Minimal, out ParameterParser? firstArgParser)) {
							sb.Append("\nERROR;\n");
							continue;
						}

			// first arg optional: {firstArg.IsOptional.ToCodeString()}, first arg: {firstArg.FullType} {firstArg.Name} = {firstArg.DefaultValue ?? "NONE"}
						sb.Append(@$"
			string? {firstArgTextVariable} = context.GetProperty(""{normParamName}"", {param.IsLocal.ToCodeString()}, context, null, out DocumentSpan? {firstArgLocVariable});");
						if (firstArg.IsOptional) {
							sb.Append(@$"
			{firstArg.Type.FullName} {firstArgVariable};
			if ({firstArgTextVariable} != null) {{
				try {{
					{firstArgVariable} = {firstArgParser.FullTypeName}.{firstArgParser.MethodName}({firstArgTextVariable}{(firstArgParser.NeedsSourceDirectory ? ", source" : "")});
				}}
				catch (System.FormatException e) {{
					{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException({firstArgLocVariable}, e.Message, e));
					{firstArgVariable} = {firstArg.DefaultValue ?? "NULL"};
				}}
			}}
			else {{
				{firstArgVariable} = {param.DefaultValue ?? "NULL"};
			}}
			SharpSheets.Parsing.IContext {paramContextVariable} = new SharpSheets.Parsing.NamedContext(context, ""{normParamName}"", forceLocal: {param.IsLocal.ToCodeString()});
			{param.Type.FullName} {valueVariable} = {paramBuilder.FullTypeName}.{paramBuilder.MethodName}({paramContextVariable}, {firstArgVariable}, source, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
			{errorListVariableName}.AddRange({paramBuildErrorsVariable});");
						}
						else { // Non-optional first argument for nested supplementary arg
							sb.Append(@$"
			{param.Type.FullName} {valueVariable};
			if ({firstArgTextVariable} != null) {{
				try {{
					{firstArg.Type.FullName} {firstArgVariable} = {firstArgParser.FullTypeName}.{firstArgParser.MethodName}({firstArgTextVariable}{(firstArgParser.NeedsSourceDirectory ? ", source" : "")});
					SharpSheets.Parsing.IContext {paramContextVariable} = new SharpSheets.Parsing.NamedContext(context, ""{normParamName}"", forceLocal: {param.IsLocal.ToCodeString()});
					{valueVariable} = {paramBuilder.FullTypeName}.{paramBuilder.MethodName}(context, {firstArgVariable}, source, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
					{errorListVariableName}.AddRange({paramBuildErrorsVariable});
				}}
				catch (System.FormatException e) {{
					{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException({firstArgLocVariable}, e.Message, e));
					{valueVariable} = {param.DefaultValue ?? "NULL"};
				}}
			}}");
							if (param.IsOptional) {
								sb.Append(@$"
			else {{
				{valueVariable} = {param.DefaultValue ?? "NULL"};
			}}");
							}
							else {
								sb.Append(@$"
			else {{
				{errorListVariableName}.Add(new SharpSheets.Exceptions.MissingParameterException(context.Location, {normParamName.ToRepr()}, typeof({param.Type.Minimal})));
				{valueVariable} = default; // Note: potentially uninitialized
			}}");
							}
						}
					}
					else { // Some kind of expanded argument
						sb.Append(@$"
			{param.Type.FullName} {valueVariable} = {paramBuilder.FullTypeName}.{paramBuilder.MethodName}(context, source, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
			{errorListVariableName}.AddRange({paramBuildErrorsVariable});");
					}
				}

				sb.Append('\n');

				paramVariables.Add(valueVariable);
			}

			string builderCall = $"{builder.Builder.FullTypeName}.{builder.Builder.MethodName}({string.Join(", ", paramVariables)})";

			if (needsBuilderErrorsParam) {
				sb.Append(@$"
			System.Collections.Generic.List<System.Exception> {builderErrorsParamVariable} = new System.Collections.Generic.List<System.Exception>();
			{builderReturnTypeFull} @result = {builderCall};
			buildErrors = {errorListVariableName}.Concat({builderErrorsParamVariable}.Select(e => new SharpSheets.Exceptions.SharpParsingException(context.Location, e.Message, e))).ToArray();
			return @result;");
			}
			else {
				sb.Append(@$"
			buildErrors = {errorListVariableName}.ToArray();
			return {builderCall};");
			}

			sb.Append(@$"
		}}");

			// Finished generating builder code
		}

		private static bool NeedsSource(ITypeSymbol symbol, Dictionary<string, ParameterParser> parserLookup) {
			if (parserLookup.TryGetValue(symbol.ToFullDisplayString(), out ParameterParser parser)) {
				return parser.NeedsSourceDirectory;
			}
			else if (symbol is IArrayTypeSymbol arraySymbol) {
				return NeedsSource(arraySymbol.ElementType, parserLookup);
			}
			else if (symbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsTupleType) {
				return namedTypeSymbol.TupleElements.Select(e => NeedsSource(e.Type, parserLookup)).Any(n => n);
			}
			else {
				return false;
			}
		}

		private static ParameterParser GetParser(ITypeSymbol symbol, Dictionary<string, ParameterParser> parserLookup, Compilation compilation) {
			if (parserLookup.TryGetValue(symbol.ToFullDisplayString(), out ParameterParser parser)) {
				return parser;
			}
			else {
				bool needsSourceDirectory = NeedsSource(symbol, parserLookup);
				return ParameterParser.GetGeneratedParser(symbol, compilation, needsSourceDirectory);
			}
		}

		private static (EquatableArray<ParameterParser> neededParamParsers, EquatableArray<BuilderToGenerate> neededMiscBuilders) FilterParamParsers(ImmutableArray<FactorySpecification> factories, ImmutableArray<AvailableBuilder> builders, ImmutableArray<ParameterParser> parsers, Compilation compilation) {

			Dictionary<string, AvailableBuilder> builderLookup = builders.ToDictionary(b => b.ConcreteBuilderType.FullName);
			Dictionary<string, ParameterParser> parserLookup = parsers.ToDictionary(p => p.ParserType.FullName);
			Dictionary<string, FactorySpecification> factoryLookup = factories.ToDictionary(f => f.FactoryType.FullName);

			HashSet<AvailableBuilder> explicitFactoryBuilders = new HashSet<AvailableBuilder>();

			// Need to know all builders that are actually used by a known factory
			foreach (FactorySpecification factory in factories) {
				INamedTypeSymbol? factoryBuildType = factory.FactoryType.GetSymbol(compilation) as INamedTypeSymbol;
				if (factoryBuildType is null) {
					continue;
				}

				explicitFactoryBuilders.UnionWith(FilterFactoryBuilders(factory, factoryBuildType, builders, compilation));
			}

			// Breadth-first search for builders used by other builders
			HashSet<AvailableBuilder> additionalNonFactoryBuilders = new HashSet<AvailableBuilder>(); // Running list
			HashSet<AvailableBuilder> toAdd = new HashSet<AvailableBuilder>(explicitFactoryBuilders); // We know we're using these
			HashSet<AvailableBuilder> additionalBuilders = new HashSet<AvailableBuilder>(); // To find new ones we didn't previously know we'd need
			do {
				// For all parameters in the builders we found we'd need
				foreach (AvailableBuilder addBuilder in toAdd) {
					foreach (BuilderParameter param in addBuilder.Parameters) {
						if (builderLookup.TryGetValue(param.Type.Minimal, out AvailableBuilder foundBuilder)) {
							// Find all parameters which require a builder
							if (!explicitFactoryBuilders.Contains(foundBuilder) && !additionalNonFactoryBuilders.Contains(foundBuilder) && !toAdd.Contains(foundBuilder)) {
								// If we didn't know we needed this one, add it to our list for the next pass
								additionalBuilders.Add(foundBuilder);
							}
						}
					}
				}

				additionalNonFactoryBuilders.UnionWith(toAdd.Where(b => !explicitFactoryBuilders.Contains(b))); // Add the ones we previously knew we'd be using
				toAdd.Clear();

				(additionalBuilders, toAdd) = (toAdd, additionalBuilders); // Swap them over

			} while (toAdd.Count > 0); // Keep going if we found some we didn't know we needed already

			// Find the parsers we need to generate (i.e. that do not require builders, and are not explicitly defined for us)
			HashSet<string> parserTypesToImplement = new HashSet<string>();
			List<ParameterParser> parsersToImplement = new List<ParameterParser>();
			Queue<ITypeSymbol> typeQueue = new Queue<ITypeSymbol>(explicitFactoryBuilders.Concat(additionalNonFactoryBuilders)
				.SelectMany(b => b.Parameters)
				.Where(b => !b.IsBuildErrors) // We don't parse these
				.Select(p => p.Type.Minimal)
				.Distinct()
				.Select(compilation.ResolveTypeKey)
				.Where(t => t is not null).Select(t => t!));

			while (typeQueue.Count > 0) {
				ITypeSymbol type = typeQueue.Dequeue();

				if (type is INamedTypeSymbol namedType) {
					// Don't want to parse List<> or Numbered<> directly, but we do want to parse their contents
					if (namedType.IsGenericList(out ITypeSymbol listElemType)) {
						typeQueue.Enqueue(listElemType);
						continue;
					}
					else if (namedType.IsGenericNumbered(out ITypeSymbol numberedElemType)) {
						typeQueue.Enqueue(numberedElemType);
						continue;
					}
				}

				ITypeSymbol reducedType = compilation.ReduceParameterType(type);
				string minimalType = reducedType.ToFullDisplayString();

				if (builderLookup.ContainsKey(minimalType) || parserLookup.ContainsKey(minimalType) || factoryLookup.ContainsKey(minimalType) || shapeMakerLookup.ContainsKey(minimalType) || minimalType == childHolderType) {
					// Either this type requires a whole builder, or the parser is already defined for us
					continue;
				}

				if (!parserTypesToImplement.Add(minimalType)) {
					// We've already logged this parser
					continue;
				}

				parsersToImplement.Add(ParameterParser.GetGeneratedParser(reducedType, compilation, NeedsSource(reducedType, parserLookup)));

				if (type is IArrayTypeSymbol arrayType) {
					typeQueue.Enqueue(arrayType);
				}
				else if (type.IsTupleType && type is INamedTypeSymbol tupleType) {
					typeQueue.Enqueue(tupleType.TupleElements.Select(f => f.Type));
				}
			}

			List<BuilderToGenerate> miscBuildersToGenerate = new List<BuilderToGenerate>();

			foreach(AvailableBuilder builder in additionalNonFactoryBuilders) {
				ITypeSymbol? builderContainingType = compilation.ResolveTypeKey(builder.FullTypeName);

				if (builderContainingType is null) {
					continue;
				}

				string clarifiedBuilderName = builder.TypeName;

				INamedTypeSymbol? containingType = builderContainingType.ContainingType;
				while (containingType != null) {
					clarifiedBuilderName = containingType.Name + "_" + clarifiedBuilderName;
					containingType = containingType.ContainingType;
				}

				static IEnumerable<RequiredParameter> GetRequiredParameters(AvailableBuilder builder) {
					if (builder.Structure == BuilderStructure.SUPPLEMENTED) {
						for (int i = 0; i < builder.Parameters.Count; i++) {
							if (!builder.Parameters[i].IsBuildErrors) {
								yield return (RequiredParameter)builder.Parameters[i];
								yield break;
							}
						}
						// TODO If we get here, there's been an error. Do anything?
					}
				}

				miscBuildersToGenerate.Add(new BuilderToGenerate(builder, new EquatableArray<RequiredParameter>(GetRequiredParameters(builder).ToArray()), "SharpSheets.Parsing", "ParameterBuilders", $"Build_{clarifiedBuilderName}"));
			}

			return (
				new EquatableArray<ParameterParser>(parsersToImplement.OrderBy(p => p.ParserType.Name).ToArray()),
				new EquatableArray<BuilderToGenerate>(miscBuildersToGenerate.OrderBy(b => b.Builder.Name).ToArray())
				);
		}

		static void ExecuteParamParser(EquatableArray<ParameterParser> neededParsers, ImmutableArray<ParameterParser> parsers, SourceProductionContext context, Compilation compilation) {
			// generate the source code and add it to the output
			string? result = GenerateParamParsersCode(neededParsers, parsers, compilation);
			// Create a separate partial class file
			if (!string.IsNullOrEmpty(result)) {
				context.AddSource($"Factories.ParamParser.g.cs", SourceText.From(result!, Encoding.UTF8));
			}
		}

		private static readonly char[] arrayDelimiters = { ',', ';', '|' };

		private static string? GenerateParamParsersCode(EquatableArray<ParameterParser> neededParsers, ImmutableArray<ParameterParser> existingParsers, Compilation compilation) {
			
			Dictionary<string, ParameterParser> parserLookup = existingParsers.Concat(neededParsers).ToDictionary(p => p.ParserType.FullName);

			List<(ParameterParser parser, INamedTypeSymbol type)> enumTypes = new List<(ParameterParser, INamedTypeSymbol)>();
			List<(ParameterParser parser, ITypeSymbol type)> otherTypes = new List<(ParameterParser, ITypeSymbol)>();

			for (int i = 0; i < neededParsers.Count; i++) {
				ITypeSymbol? typeSymbol = neededParsers[i].ParserType.GetSymbol(compilation); // compilation.ResolveTypeKey(neededParsers[i].ParserType);

				if (typeSymbol is null) {
					continue;
				}

				if (typeSymbol is INamedTypeSymbol named && named.IsEnum()) {
					enumTypes.Add((neededParsers[i], named));
				}
				else {
					otherTypes.Add((neededParsers[i], typeSymbol));
				}
			}

			StringBuilder sb = new StringBuilder();
			sb.Append(@$"// <auto-generated/>
#nullable enable
#pragma warning disable

using System.Linq;

namespace SharpSheets.Parsing {{

	{GeneratorMarkers.GeneratedCodeAttr}
	{GeneratorMarkers.CompilerGeneratedAttr}
	public static class ParameterParsers {{

		//private static readonly char[] {nameof(arrayDelimiters)} = new char[] {{ {string.Join(", ", arrayDelimiters.Select(d => $"'{d}'"))} }};
");

			// Non-enum types
			foreach ((ParameterParser parser, ITypeSymbol typeSymbol) in otherTypes) {
				int parseRank = typeSymbol.GetArrayOrTupleRank();
				bool needsSource = NeedsSource(typeSymbol, parserLookup);

				if (parser.FullTypeName != "SharpSheets.Parsing.ParameterParsers") {
					continue;
				}

				if (parseRank > 0) {
					sb.Append(@$"
		public static {typeSymbol.ToFullDisplayString()} {parser.MethodName}(string value{(needsSource ? ", SharpSheets.Utilities.DirectoryPath source" : "")}) {{
			string[] parts = SharpSheets.Parsing.StringParsing.SplitOnUnescaped(value, '{arrayDelimiters[parseRank - 1]}').Select(s => s.Trim()).ToArray();");

					if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol) {
						ParameterParser elemParser = GetParser(arrayTypeSymbol.ElementType, parserLookup, compilation);
						sb.Append(@$"
			{typeSymbol.ToFullDisplayString()} result = parts.Select(p => {elemParser.FullTypeName}.{elemParser.MethodName}(p{(elemParser.NeedsSourceDirectory ? ", source" : "")})).ToArray();");
					}
					else if (typeSymbol is INamedTypeSymbol namedTypeSymbol) { // Must be Tuple type
						int tupleElements = namedTypeSymbol.TupleElements.Length;
						sb.Append(@$"
			if (parts.Length != {tupleElements}) {{
				throw new FormatException($""Incorrect number of values provided for tuple (expected {tupleElements}, got {{parts.Length}})."");
			}}
			{typeSymbol.ToFullDisplayString()} result = (");

						for (int f = 0; f < namedTypeSymbol.TupleElements.Length; f++) {
							IFieldSymbol field = namedTypeSymbol.TupleElements[f];
							ParameterParser fieldParser = GetParser(field.Type, parserLookup, compilation);
							if (f > 0) { sb.Append(','); }
							sb.Append(@$"
				{fieldParser.FullTypeName}.{fieldParser.MethodName}(parts[{f}]{(fieldParser.NeedsSourceDirectory ? ", source" : "")})");
						}

						sb.Append(@$"
				);");
					}

						sb.Append(@$"
			return result;
		}}
");
				}
				else {
					sb.Append(@$"
		public static {typeSymbol.ToFullDisplayString()} {parser.MethodName}(string value) {{
			return default;
		}}
");
				}
			}

			// Start enum region
			sb.Append(@"
		#region Enum Type Parsers
");

			// Enum types
			foreach ((ParameterParser parser, INamedTypeSymbol enumSymbol) in enumTypes) {

				string enumFullName = enumSymbol.ToFullDisplayString();
				IEnumerable<string> enumMemberNames = enumSymbol
					.GetMembers()
					.Where(static member => member.Kind is SymbolKind.Field)
					.Select(static symbol => symbol.Name);

				sb.Append(@$"
		public static {enumFullName} {parser.MethodName}(string value) {{
			switch (value.ToLowerInvariant().Trim()) {{");

				foreach (string memberName in enumMemberNames) {
					sb.Append(@$"
				case ""{memberName.ToLowerInvariant()}"":
					return {enumFullName}.{memberName};");
				}

				sb.Append(@$"
				default:
					throw new System.FormatException($""Invalid literal for {enumSymbol.Name} enum: \""{{value}}\"""");
			}}
		}}
");
			}

			// End enum region
			sb.Append(@"
		#endregion Enum Type Parsers
");

			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
		}


		static void ExecuteMiscBuilders(EquatableArray<BuilderToGenerate> neededMiscBuilders, IList<ParameterParser> availableParsers, SourceProductionContext context, Compilation compilation) {
			// generate the source code and add it to the output
			string? result = GenerateMiscBuildersCode(neededMiscBuilders, availableParsers, compilation);
			// Create a separate partial class file
			if (!string.IsNullOrEmpty(result)) {
				context.AddSource($"Factories.ParamBuilders.g.cs", SourceText.From(result!, Encoding.UTF8));
			}
		}

		private static string? GenerateMiscBuildersCode(EquatableArray<BuilderToGenerate> neededMiscBuilders, IList<ParameterParser> availableParsers, Compilation compilation) {
			
			Dictionary<string, ParameterParser> parserLookup = availableParsers.ToDictionary(p => p.ParserType.FullName);
			Dictionary<string, BuilderToGenerate> builderLookup = neededMiscBuilders.ToDictionary(p => p.Builder.BuilderType.FullName);

			StringBuilder sb = new StringBuilder();
			sb.Append(@$"// <auto-generated/>
#nullable enable
#pragma warning disable

using System.Linq;

namespace SharpSheets.Parsing {{

	{GeneratorMarkers.GeneratedCodeAttr}
	{GeneratorMarkers.CompilerGeneratedAttr}
	public static class ParameterBuilders {{
");

			foreach(BuilderToGenerate builder in neededMiscBuilders) {
				GenerateBuilderCode(sb, builder, parserLookup, builderLookup, false, compilation);

				sb.Append('\n');
			}

			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
		}

		static void ExecuteBuilderDocumentation(ImmutableArray<FactoryToGenerate> factories, ImmutableArray<AvailableBuilder> availableBuilders, ImmutableArray<ParameterParser> availableParsers, EquatableArray<BuilderToGenerate> generatedBuilders, EquatableArray<ParameterParser> generatedParsers, SourceProductionContext context, Compilation compilation) {
			// generate the source code and add it to the output
			string? result = GenerateBuilderDocumentationCode(factories, availableBuilders, availableParsers, generatedBuilders, generatedParsers, compilation);
			// Create a separate partial class file
			if (!string.IsNullOrEmpty(result)) {
				context.AddSource($"Documentation.Builders.g.cs", SourceText.From(result!, Encoding.UTF8));
			}
		}

		public static readonly string DocumentationSourceName = "DocumentationSource";

		private static string GetDocumentationVariableName(AvailableBuilder builder) {
			return builder.ConcreteBuilderType.Type.Replace('.', '_');
		}

		private static string? GenerateBuilderDocumentationCode(ImmutableArray<FactoryToGenerate> factories, ImmutableArray<AvailableBuilder> availableBuilders, ImmutableArray<ParameterParser> availableParsers, EquatableArray<BuilderToGenerate> generatedBuilders, EquatableArray<ParameterParser> generatedParsers, Compilation compilation) {

			SharpSheetsParameterResolverData resolverData = SharpSheetsParameterResolverData.Create(compilation, availableBuilders, availableParsers.Concat(generatedParsers), factories);
			//SharpSheetsParameterResolverData resolverData = SharpSheetsParameterResolverData.Create(compilation, availableBuilders.Concat(generatedBuilders.Select(b => b.Builder)), availableParsers.Concat(generatedParsers), factories);

			Dictionary<string, string> typeToDocsVariable = availableBuilders.ToDictionary(b => b.ConcreteBuilderType.FullName, GetDocumentationVariableName);

			StringBuilder sb = new StringBuilder();
			sb.Append(@$"// <auto-generated/>
#nullable enable
#pragma warning disable

namespace SharpSheets.Documentation {{

	{GeneratorMarkers.GeneratedCodeAttr}
	{GeneratorMarkers.CompilerGeneratedAttr}
	public static class BuilderDocs {{

		private static readonly SharpSheets.Utilities.DirectoryPath DocumentationSource = SharpSheets.Utilities.DirectoryPath.GetCurrentDirectory();
");

			foreach (AvailableBuilder builder in availableBuilders) {
				ITypeSymbol? builderType = builder.ConcreteBuilderType.GetSymbol(compilation); // compilation.ResolveTypeKey(builder.ConcreteBuilderType);

				if(builderType is null) {
					continue;
				}

				IMethodSymbol? method = compilation.ResolveMethodSymbol(builderType, builder.MethodName).FirstOrDefault(m => m.Parameters.Length == builder.Parameters.Count);

				if (method is null) {
					continue;
				}

				string? typeXml = builderType.GetDocumentationCommentXml(preferredCulture: null, expandIncludes: true)?.Replace("\r\n", "\n");
				string? methodXml = method.GetDocumentationCommentXml(preferredCulture: null, expandIncludes: true)?.Replace("\r\n", "\n");

				DocSummaryComment? typeComment = DocCommentReader.FromSymbol(builderType, resolverData.Compilation);
				DocMethodComment? methodComment = DocCommentReader.FromSymbol(method, resolverData.BuilderLookup, resolverData.ParserLookup, resolverData);

				string concreteBuilderTypeMinimal = builder.ConcreteBuilderType.Type;
				string concreteBuilderTypeNameMinimal = builder.ConcreteBuilderType.Name;

				string docVariableName = GetDocumentationVariableName(builder);
				string docVariableLazyName = $"__generated__lazy_{docVariableName}";

				// BuilderDetails(Type displayType, Type declaringType, string name, string fullName, ArgumentDetails[] arguments, DocumentationString? description, Rectangle? size, Size? canvas)
				sb.Append(@$"
		{GeneratorMarkers.NeverEditorBrowsableAttr}
		private static readonly System.Lazy<SharpSheets.Documentation.BuilderDetails> {docVariableLazyName} = new System.Lazy<SharpSheets.Documentation.BuilderDetails>(() =>
			new SharpSheets.Documentation.BuilderDetails(
					displayType: typeof({builder.BuilderType.Type}),
					declaringType: typeof({concreteBuilderTypeMinimal}),
					name: {concreteBuilderTypeNameMinimal.ToRepr()},
					fullName: {concreteBuilderTypeMinimal.ToRepr()},
					arguments: [");

				foreach (SharpSheetsParameterData param in SharpSheetsParameterResolver.GetArguments(builder, method, methodComment, resolverData)) {
					if (param.DeferArgsTo is null) {
						sb.Append(@$"
							new SharpSheets.Documentation.ArgumentDetails(
									name: {param.Name.ToRepr()},
									description: {param.Description ?? "null"},
									type: {param.ArgumentType},
									isOptional: {param.IsOptional.ToCodeString()},
									useLocal: {param.UseLocal.ToCodeString()},
									defaultValue: {param.DefaultValue ?? "null"},
									exampleValue: {param.ExampleValue ?? "null"},
									implied: {param.Implied.ToRepr()}
								){string.Join("", param.Prefixes.Select(p => $".Prefixed({p.ToRepr()})"))},");
					}
					else {
						sb.Append(@$"
							.. {typeToDocsVariable[param.DeferArgsTo]}.Arguments{(param.SkipDeferredArgs > 0 ? $"[{param.SkipDeferredArgs}..]" : "")}{(param.Prefixes.Length > 0 ? $".Select(a => a{string.Join("", param.Prefixes.Select(p => $".Prefixed({p.ToRepr()})"))})" : "")},");
					}
				}
				
				sb.Append(@$"
						],
					description: {typeComment?.Summary ?? "null"},
					size: {methodComment?.Size ?? "null"},
					canvas: {methodComment?.Canvas ?? "null"}
				));
		public static SharpSheets.Documentation.BuilderDetails {docVariableName} => {docVariableLazyName}.Value;");

				sb.Append('\n');
			}

			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
		}

		static void ExecuteEnumDocumentation(ImmutableArray<string> declaredEnums, SourceProductionContext context, Compilation compilation) {
			// generate the source code and add it to the output
			string? result = GenerateEnumDocumentationCode(declaredEnums, compilation);
			// Create a separate partial class file
			if (!string.IsNullOrEmpty(result)) {
				context.AddSource($"Documentation.Enums.g.cs", SourceText.From(result!, Encoding.UTF8));
			}
		}

		private static string? GenerateEnumDocumentationCode(ImmutableArray<string> declaredEnums, Compilation compilation) {
			List<(string fullType, string typeName, string docVariableName)> allDocs = new List<(string, string, string)>();

			StringBuilder sb = new StringBuilder();
			sb.Append(@$"// <auto-generated/>
#nullable enable
#pragma warning disable

namespace SharpSheets.Documentation {{

	{GeneratorMarkers.GeneratedCodeAttr}
	{GeneratorMarkers.CompilerGeneratedAttr}
	public static class EnumDocs {{
");

			foreach (string enumName in declaredEnums) {
				ITypeSymbol? enumSymbol = compilation.ResolveTypeKey(enumName);

				if (enumSymbol is not INamedTypeSymbol enumType || !enumType.IsEnum() || enumType.DeclaredAccessibility != Accessibility.Public) {
					continue;
				}

				DocSummaryComment? enumComment = DocCommentReader.FromSymbol(enumType, compilation);

				string docVariableName = enumType.ToFullDisplayString().Replace('.', '_');

				allDocs.Add((enumName, enumType.Name, docVariableName));

				sb.Append(@$"
		public static readonly SharpSheets.Documentation.EnumDoc {docVariableName} = new SharpSheets.Documentation.EnumDoc(
				{enumType.Name.ToRepr()},
				new EnumValDoc[] {{");

				foreach (IFieldSymbol enumField in enumType.GetDeclaredEnumMembers()) {
					DocSummaryComment? enumValComment = DocCommentReader.FromSymbol(enumField, compilation);

					sb.Append(@$"
					new SharpSheets.Documentation.EnumValDoc({enumType.Name.ToRepr()}, {enumField.Name.ToRepr()}, {enumValComment?.Summary ?? "null"}),");
				}

				sb.Append(@$"
					}},
				{enumComment?.Summary ?? "null"}
			);");

				sb.Append('\n');
			}

			sb.Append(@$"
		{GeneratorMarkers.NeverEditorBrowsableAttr}
		private static readonly Dictionary<string, SharpSheets.Documentation.EnumDoc> __generated__allDocs_names = new Dictionary<string, SharpSheets.Documentation.EnumDoc>(System.StringComparer.InvariantCultureIgnoreCase) {{");

			foreach ((_, string typeName, string docVariableName) in allDocs.OrderBy(d => d.typeName)) {
				sb.Append(@$"
				{{ {typeName.ToRepr()}, {docVariableName} }},");
			}

			sb.Append(@$"
			}};
");

			sb.Append(@"
		public static bool TryGetDocumentation(string typeName, out SharpSheets.Documentation.EnumDoc enumDoc) {
			return __generated__allDocs_names.TryGetValue(typeName, out enumDoc);
		}
");

			sb.Append(@$"
		{GeneratorMarkers.NeverEditorBrowsableAttr}
		private static readonly Dictionary<System.Type, SharpSheets.Documentation.EnumDoc> __generated__allDocs_types = new Dictionary<System.Type, SharpSheets.Documentation.EnumDoc>() {{");

			foreach ((string fullType, _, string docVariableName) in allDocs.OrderBy(d => d.typeName)) {
				sb.Append(@$"
				{{ typeof({fullType}), {docVariableName} }},");
			}

			sb.Append(@$"
			}};
");

			sb.Append(@"
		public static bool TryGetDocumentation(Type type, out SharpSheets.Documentation.EnumDoc enumDoc) {
			return __generated__allDocs_types.TryGetValue(type, out enumDoc);
		}
");

			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
		}

	}

	public static class GeneratorMarkers {

		public static readonly string GeneratedCodeAttr;
		public static readonly string CompilerGeneratedAttr;
		public static readonly string NeverEditorBrowsableAttr;

		static GeneratorMarkers() {
			string assemblyName = typeof(FactoryGenerator).Assembly.GetName().Name.ToString();
			string assemblyVersion = typeof(FactoryGenerator).Assembly.GetName().Version.ToString();

			GeneratedCodeAttr = $"[System.CodeDom.Compiler.GeneratedCode(\"{assemblyName}\", \"{assemblyVersion}\")]";
			CompilerGeneratedAttr = "[System.Runtime.CompilerServices.CompilerGenerated]";
			NeverEditorBrowsableAttr = "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]";
		}

	}

	public class IndentedStringBuilder {

		private readonly StringBuilder sb;
		private readonly int level;
		private bool newline = true;

		public IndentedStringBuilder(StringBuilder builder, int level) {
			this.sb = builder;
			this.level = level;
		}

		public IndentedStringBuilder Append(string str) {
			for(int i=0; i<str.Length; i++) {
				this.Append(str[i]);
			}
			return this;
		}

		public IndentedStringBuilder Append(char c) {
			if(c == '\n') {
				sb.Append(c);
				newline = true;
			}
			else if (newline) {
				sb.Append('\t', level);
				sb.Append(c);
				newline = false;
			}
			else {
				sb.Append(c);
			}

			return this;
		}

	}

}
