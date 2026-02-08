using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SharpSheets.Generators.Utilities;
using SharpSheets.Generators.Utilities.DataStructures;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using static SharpSheets.Generators.SharpSheetsConstants;

namespace SharpSheets.Generators {

	[Generator]
	public class FactoryGenerator : IIncrementalGenerator {

		public void Initialize(IncrementalGeneratorInitializationContext context) {

			IncrementalValuesProvider<FactorySpecification> factoriesSpecs = context.SyntaxProvider
				.ForAttributeWithMetadataNameSelectMany(
					FactoryAttribute,
					predicate: static (s, _) => s is ClassDeclarationSyntax,
					transform: static (ctx, ct) => GetFactorySpecs(ctx, ct));

			IncrementalValuesProvider<AvailableBuilder> availableBuilders = context.SyntaxProvider
				.ForAttributeWithMetadataName(
					FactoryBuilderAttribute,
					predicate: static (s, _) => s is MethodDeclarationSyntax,
					transform: static (ctx, ct) => GetAvailableBuilders(ctx, ct))
				.WhereNotNull();

			IncrementalValuesProvider<ParameterParser> availableParsers = context.SyntaxProvider
				.ForAttributeWithMetadataName(
					ParameterParserAttribute,
					predicate: static (s, _) => s is MethodDeclarationSyntax,
					transform: static (ctx, ct) => GetParameterParser(ctx, ct))
				.WhereNotNull();

			IncrementalValuesProvider<TypeData> requestedGeneratedParsers = context.CompilationProvider
				.SelectMany((comp, _) => {
					AttributeData[] genParserAttrs = comp.Assembly.GetAttributes(GenerateParameterParserAttribute).ToArray();
					return genParserAttrs
						.Select(a => (ITypeSymbol?)a.ConstructorArguments[0].Value)
						.WhereNotNull()
						.Select(t => TypeData.Create(t))
						.Distinct();
				});

			IncrementalValuesProvider<EnumComment> declaredEnums = context.SyntaxProvider
				.CreateSyntaxProvider(
					predicate: (node, ct) => node is EnumDeclarationSyntax enumNode && enumNode.IsPublic(),
					transform: (ctx, ct) => {
						EnumDeclarationSyntax enumDeclaration = (EnumDeclarationSyntax)ctx.Node;
						INamedTypeSymbol? enumSymbol = ctx.SemanticModel.GetDeclaredSymbol(enumDeclaration, ct) as INamedTypeSymbol;
						return enumSymbol is not null ? DocCommentReader.FromEnumSymbol(enumSymbol, ctx.SemanticModel.Compilation, ct) : null; // enumSymbol?.ToFullDisplayString();
					})
				.WhereNotNull();

			IncrementalValuesProvider<FactoryToGenerate> factoriesToGenerate = factoriesSpecs
				.Combine(availableBuilders.Collect())
				.Combine(context.CompilationProvider)
				.Flatten()
				.Select(static (i, ct) => FilterFactoriesToGenerate(i.Item1, i.Item2, i.Item3, ct))
				.WhereNotNull();

			IncrementalValuesProvider<EquatableArray<FactoryToGenerate>> factoryTypes = factoriesToGenerate.Collect()
				.SelectMany(static (fs, _) => GroupFactoryObjects(fs));

			IncrementalValueProvider<(EquatableArray<ParameterParser> neededParamParsers, EquatableArray<BuilderToGenerate> neededMiscBuilders, EquatableArray<TypeData> cannotGenerateParserTypes)> neededAndErrors = factoriesToGenerate
				.Collect()
				.Combine(availableBuilders.Collect())
				.Combine(availableParsers.Collect())
				.Combine(requestedGeneratedParsers.Collect())
				.Combine(context.CompilationProvider)
				.Flatten()
				.Select(static (i, ct) => FilterParamParsers(i.Item1, i.Item2, i.Item3, i.Item4, i.Item5, ct));

			(IncrementalValueProvider<EquatableArray<ParameterParser>> neededParamParsers, IncrementalValueProvider<EquatableArray<BuilderToGenerate>> neededMiscBuilders, IncrementalValueProvider<EquatableArray<TypeData>> cannotGenerateParserTypes) = neededAndErrors.Split();

			IncrementalValueProvider<(EquatableArray<ParameterParser> neededParamParsers, EquatableArray<BuilderToGenerate> neededMiscBuilders)> needed = neededParamParsers.Combine(neededMiscBuilders);

			IncrementalValueProvider<(EquatableArray<BuilderToGenerate> builders, EquatableArray<ParameterParser> parsers)> allBuildersParsers = needed
				.Combine(availableBuilders.Collect())
				.Combine(availableParsers.Collect())
				.Flatten()
				.Select(static (i, ct) => CollectAllBuildersParsers(i.Item1, i.Item2, i.Item3, i.Item4, ct));

			// Generate source code for each factory
			context.ExecuteGenerator(factoriesToGenerate.Combine(allBuildersParsers).Flatten(),
				static (source, cmp, ct) => GenerateFactoryCode(source.Item1, source.Item2, source.Item3, cmp, ct),
				static source => $"Factories.{source.Item1.Spec.Name}.{source.Item1.Spec.FactoryType.Name}.g.cs");

			// Generate type-level data for each factory type (default dictionary)
			context.ExecuteGenerator(factoryTypes,
				static (factories, ct) => GenerateFactoryTypeLevel(factories, ct),
				static factories => $"Factories.Data.{factories[0].Spec.Name}.g.cs");

			// Generate source code for additional parsers
			context.ExecuteGenerator(needed.Combine(availableParsers.Collect()).Flatten(),
				static (source, cmp, ct) => GenerateParamParsersCode(source.Item1, source.Item3, cmp, ct),
				"Factories.ParamParser.g.cs");

			// Generate source code for misc builders
			context.ExecuteGenerator(needed.Combine(availableParsers.Collect()).Flatten(),
				static (source, cmp, ct) => GenerateMiscBuildersCode(source.Item2, source.Item1.Concat(source.Item3).ToArray(), cmp, ct),
				"Factories.ParamBuilders.g.cs");

			// Generate documentation code for builders
			context.ExecuteGenerator(availableBuilders.Collect().Combine(availableParsers.Collect()).Combine(needed).Combine(factoriesToGenerate.Collect()).Flatten(),
				static (source, cmp, ct) => GenerateBuilderDocumentationCode(source.Item4, source.Item1, source.Item2, source.Item3.neededMiscBuilders, source.Item3.neededParamParsers, cmp, ct),
				"Documentation.Builders.g.cs");

			// Generate documentation code for enums
			context.ExecuteGenerator(declaredEnums.Collect(),
				static (declaredEnums, ct) => GenerateEnumDocumentationCode(declaredEnums, ct),
				"Documentation.Enums.g.cs");

			// Generate documentation linkup code for each factory
			context.ExecuteGenerator(factoriesToGenerate,
				static (factory, ct) => GenerateFactoryDocumentation(factory, ct),
				static factory => $"Factories.{factory.Spec.Name}.{factory.Spec.FactoryType.Name}.Documentation.g.cs");

		}

		private static EquatableArray<FactorySpecification> GetFactorySpecs(GeneratorAttributeSyntaxContext ctx, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

			if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol || ctx.TargetNode is not ClassDeclarationSyntax declarationNode) {
				// something went wrong
				return new EquatableArray<FactorySpecification>(Array.Empty<FactorySpecification>());
			}

			List<FactorySpecification> result = new List<FactorySpecification>();

			foreach(AttributeData attr in ctx.Attributes) {
				ct.ThrowIfCancellationRequested();

				ITypeSymbol? factoryTypeSymbol = (ITypeSymbol?)attr.ConstructorArguments[0].Value;
				if (factoryTypeSymbol is null) {
					// Malformed attribute
					continue;
				}

				ITypeSymbol[] requiredParams = attr.ConstructorArguments[1].Values.Select(t => (ITypeSymbol)t.Value!).ToArray();
				string[] requiredParamNames = attr.ConstructorArguments[2].Values.Select(t => (string)t.Value!).ToArray();
				bool[] excludeRequiredParams = attr.ConstructorArguments[3].Values.Select(t => (bool)t.Value!).ToArray();
				ITypeSymbol? defaultType = (ITypeSymbol?)attr.ConstructorArguments[4].Value;

				bool includeDocs = (bool?)attr.GetNamedArgument("IncludeDocs")?.Value ?? true;

				result.Add(FactorySpecification.Build(classSymbol, declarationNode, factoryTypeSymbol, requiredParams, requiredParamNames, excludeRequiredParams, defaultType, includeDocs));
			}

			return new EquatableArray<FactorySpecification>(result.ToArray());
		}

		private static IEnumerable<EquatableArray<FactoryToGenerate>> GroupFactoryObjects(IEnumerable<FactoryToGenerate> factories) {
			return factories.GroupBy(f => f.Spec.FullName).OrderBy(g => g.Key).Select(fs => new EquatableArray<FactoryToGenerate>(fs.ToArray()));
		}

		private static string GetBuilderMethodName(FactorySpecification spec, AvailableBuilder builder, bool isSingleton) {
			if (isSingleton) {
				return $"Build_{builder.TypeName}";
			}
			else {
				return $"Build_{spec.FactoryType.Name}_{builder.TypeName}";
			}
		}

		private static FactoryToGenerate? FilterFactoriesToGenerate(FactorySpecification spec, ImmutableArray<AvailableBuilder> availableBuilders, Compilation compilation, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

			INamedTypeSymbol? factoryBuildType = spec.FactoryType.GetSymbol(compilation)as INamedTypeSymbol;

			if (factoryBuildType is null) {
				return null;
			}

			AvailableBuilder[] factoryBuilders = FilterFactoryBuilders(spec, factoryBuildType, availableBuilders, compilation, ct).ToArray();

			bool isSingleton = factoryBuilders.Length == 1 && spec.FactoryType == factoryBuilders[0].BuilderType && spec.FactoryType == factoryBuilders[0].ConcreteBuilderType;

			BuilderToGenerate[] factoryBuildersToGenerate = factoryBuilders.Select(builder => {
				string builderMethodName = GetBuilderMethodName(spec, builder, isSingleton);
				return new BuilderToGenerate(builder, spec.RequiredParameters, spec.Namespace, spec.Name, builderMethodName);
			}).OrderBy(b => b.Builder.ConcreteBuilderType.Name).ToArray();

			ct.ThrowIfCancellationRequested();

			AvailableBuilder? defaultBuilder = factoryBuilders.FirstOrDefault(b => b.ConcreteBuilderType == spec.DefaultType);

			return new FactoryToGenerate(spec, defaultBuilder, new EquatableArray<BuilderToGenerate>(factoryBuildersToGenerate), isSingleton);
		}

		private static (EquatableArray<BuilderToGenerate>, EquatableArray<ParameterParser>) CollectAllBuildersParsers(EquatableArray<ParameterParser> neededParsers, EquatableArray<BuilderToGenerate> neededBuilders, ImmutableArray<AvailableBuilder> providedBuilders, ImmutableArray<ParameterParser> providedParsers, CancellationToken ct) {
			ct.ThrowIfCancellationRequested(); // Maybe not worth it...
			return (
					//new EquatableArray<AvailableBuilder>(providedBuilders.Concat(neededBuilders.Select(b => b.Builder)).ToArray()),
					new EquatableArray<BuilderToGenerate>(neededBuilders.ToArray()),
					new EquatableArray<ParameterParser>(providedParsers.Concat(neededParsers).ToArray())
				);
		}

		private static AvailableBuilder? GetAvailableBuilders(GeneratorAttributeSyntaxContext ctx, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

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

			ct.ThrowIfCancellationRequested();

			return AvailableBuilder.Build(methodSymbol, builderTypeSymbol, builderName);
		}

		private static ParameterParser? GetParameterParser(GeneratorAttributeSyntaxContext ctx, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

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
			if(methodSymbol.Parameters.Length == 2 && methodSymbol.Parameters[1].Type.ToFullDisplayString() != DirectoryPath) {
				// If a second parameter is provided, it must be a source path
				return null;
			}

			ITypeSymbol parserType = methodSymbol.ReturnType;

			bool needsSourceDirectory = methodSymbol.Parameters.Length == 2;

			ct.ThrowIfCancellationRequested();

			return ParameterParser.Build(methodSymbol, parserType, needsSourceDirectory);
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

		private static IEnumerable<AvailableBuilder> FilterFactoryBuilders(FactorySpecification factory, INamedTypeSymbol factoryBuildType, IEnumerable<AvailableBuilder> builders, Compilation compilation, CancellationToken ct) {
			foreach (AvailableBuilder builder in builders) {
				ct.ThrowIfCancellationRequested();

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

		static string? GenerateFactoryCode(FactoryToGenerate factory, EquatableArray<BuilderToGenerate> allBuilders, EquatableArray<ParameterParser> allParsers, Compilation compilation, CancellationToken ct) {
			// Need to know if we can build a given pattern name (dictionary lookup)
			// Need to actually build a given pattern
			// Need arguments: IContext context, DirectoryPath source, out SharpParsingException[] buildErrors
			// Can we have some mechanism for passing in known arguments? This should allow for optionally known args
			// Essentially replacing: object? SharpFactory.Build(MethodInfo builder, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, object[] firstParameters, out SharpParsingException[] buildErrors)
			// Can provide a list of known arguments in the attribute, which can be included in the generated method? (How would we name them?)
			// Should also allow for case with only 1 builder (i.e. no switch statement) - still need default case, though, as we still need to check for valid name

			ct.ThrowIfCancellationRequested();

			INamedTypeSymbol? factoryBuildType = factory.Spec.FactoryType.GetSymbol(compilation) as INamedTypeSymbol;

			if (factoryBuildType is null) {
				return null;
			}

			TypeLookup<ParameterParser> parserLookup = new TypeLookup<ParameterParser>(allParsers.Select(p => (p.ParserType, p))); // .ToDictionary(p => p.ParserType.FullName);
			TypeLookup<BuilderToGenerate> builderLookup = new TypeLookup<BuilderToGenerate>(allBuilders.Select(b => (b.Builder.BuilderType, b))); //.ToDictionary(b => b.Builder.BuilderType.FullName);

			string stringComparerName = "System.StringComparer.OrdinalIgnoreCase";

			bool isSingleton = factory.IsSingleton;
			bool factoryNeedsShapeFactory = NeedsShapeFactory(factory.Builders);
			bool factoryNeedsWidgetFactory = NeedsWidgetFactory(factory.Builders);

			string sourceDirArgName = "@source";

			ct.ThrowIfCancellationRequested();

			StringBuilder sb = new StringBuilder();
			sb.Append(@$"// <auto-generated/>
#nullable enable
#pragma warning disable

using System.Linq;
using SharpSheets.Parsing;

namespace {factory.Spec.Namespace} {{
	public{(factory.Spec.IsStatic ? " static" : "")}{(factory.Spec.IsPartial ? " partial" : "")} class {factory.Spec.Name} {{
");

			if (!isSingleton) {
				sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static {factory.Spec.FactoryType.Type}? Build_{factory.Spec.FactoryType.Name}(string buildName, SharpSheets.Parsing.IContext context{(factory.Spec.RequiredParameters.Count > 0 ? (", " + string.Join(", ", factory.Spec.RequiredParameters.Select(p => $"{p.Type.FullName} {p.Name}"))) : "")}, SharpSheets.Utilities.DirectoryPath {sourceDirArgName},{(factoryNeedsWidgetFactory ? " SharpSheets.Widgets.WidgetFactory widgetFactory," : "")}{(factoryNeedsShapeFactory ? " SharpSheets.Shapes.ShapeFactory shapeFactory," : "")} out SharpSheets.Exceptions.SharpParsingException[] buildErrors) {{
			switch (buildName.ToLowerInvariant()) {{");

				foreach (BuilderToGenerate builder in factory.Builders) {
					ct.ThrowIfCancellationRequested();

					bool builderNeedsShapeFactory = NeedsShapeFactory(builder);
					bool builderNeedsWidgetFactory = NeedsWidgetFactory(builder);

					sb.Append(@$"
				case ""{builder.Builder.Name.ToLowerInvariant()}"":");

					sb.Append(@$"
					return {builder.MethodName}(context{(factory.Spec.RequiredParameters.Count > 0 ? (", " + string.Join(", ", factory.Spec.RequiredParameters.Select(p => $"{p.Name}"))) : "")}, {sourceDirArgName},{(builderNeedsWidgetFactory ? " widgetFactory," : "")}{(builderNeedsShapeFactory ? " shapeFactory," : "")} out buildErrors);");
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
		}}");
				}
				sb.Append('\n');

				// Build default method
				if (factory.DefaultBuilder is not null) {
					bool defaultNeedsWidgetFactory = NeedsWidgetFactory(factory.DefaultBuilder);
					bool defaultNeedsShapeFactory = NeedsShapeFactory(factory.DefaultBuilder);

					sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static {factory.Spec.FactoryType.Type} Build_{factory.Spec.FactoryType.Name}_Default(SharpSheets.Parsing.IContext context{(factory.Spec.RequiredParameters.Count > 0 ? (", " + string.Join(", ", factory.Spec.RequiredParameters.Select(p => $"{p.Type.FullName} {p.Name}"))) : "")}, SharpSheets.Utilities.DirectoryPath {sourceDirArgName},{(factoryNeedsWidgetFactory ? " SharpSheets.Widgets.WidgetFactory widgetFactory," : "")}{(factoryNeedsShapeFactory ? " SharpSheets.Shapes.ShapeFactory shapeFactory," : "")} out SharpSheets.Exceptions.SharpParsingException[] buildErrors) {{
			return {GetBuilderMethodName(factory.Spec, factory.DefaultBuilder, false)}(context{(factory.Spec.RequiredParameters.Count > 0 ? (", " + string.Join(", ", factory.Spec.RequiredParameters.Select(p => p.Name))) : "")}, {sourceDirArgName},{(defaultNeedsWidgetFactory ? " widgetFactory," : "")}{(defaultNeedsShapeFactory ? " shapeFactory," : "")} out buildErrors);
		}}
");
				}

			} // End !isSingleton

			if (factory.DefaultBuilder != null) {
				BuilderParameter[] nonOptional = factory.DefaultBuilder.Parameters.TakeWhile(p => !p.IsOptional).ToArray();

				sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static {factory.Spec.FactoryType.Type} MakeDefault_{factory.Spec.FactoryType.Name}({(nonOptional.Length > 0 ? string.Join(", ", nonOptional.Select(p => $"{p.Type.FullName} {p.Name}")) : "")}) {{
			return {factory.DefaultBuilder.FullTypeName}.{factory.DefaultBuilder.MethodName}({(nonOptional.Length > 0 ? string.Join(", ", nonOptional.Select(p => p.Name)) : "")});
		}}
");
			}

			foreach (BuilderToGenerate builder in factory.Builders) {
				GenerateBuilderCode(sb, builder, parserLookup, builderLookup, true, compilation, ct);
				sb.Append('\n');
			}

			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
		}

		static string? GenerateFactoryTypeLevel(EquatableArray<FactoryToGenerate> factories, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();
			
			FactorySpecification factorySpec = factories[0].Spec;
			FactorySpecification[] specsWithDefaults = factories.Where(f => f.Spec.DefaultType is not null && !f.IsSingleton).Select(f => f.Spec).ToArray();
			FactoryToGenerate[] withDocs = factories.Where(f => f.Spec.IncludeDocs).ToArray();

			if (specsWithDefaults.Length == 0 && withDocs.Length == 0) {
				return null;
			}

			StringBuilder sb = new StringBuilder();
			sb.Append(@$"// <auto-generated/>
#nullable enable
#pragma warning disable

using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace {factorySpec.Namespace} {{
	public{(factorySpec.IsStatic ? " static" : "")}{(factorySpec.IsPartial ? " partial" : "")} class {factorySpec.Name} {{
");

			// Default type lookup
			if (specsWithDefaults.Length > 0) {
				string defaultLookupDictionaryVariable = $"__generated__factory_default_data";

				sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		{GeneratorMarkers.NeverEditorBrowsableAttr}
		private static readonly System.Collections.Generic.Dictionary<Type, Type> {defaultLookupDictionaryVariable} = new System.Collections.Generic.Dictionary<Type, Type>() {{");

				foreach (FactorySpecification spec in specsWithDefaults) {

					sb.Append(@$"
				{{ typeof({spec.FactoryType.Type}), typeof({spec.DefaultType!.Type}) }},");

				}

				sb.Append(@$"
			}};
");

				sb.Append(@$"
		public static bool TryGetDefault(Type type, [MaybeNullWhen(false)] out Type defaultType) => {defaultLookupDictionaryVariable}.TryGetValue(type, out defaultType);
");
			}

			ct.ThrowIfCancellationRequested();

			if (withDocs.Length > 0) {
				// All builder names/details
				sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static IEnumerable<KeyValuePair<string, SharpSheets.Documentation.BuilderDetails>> GetAllBuilderNames() => {(withDocs[0].IsSingleton ? $"new SharpSheets.Documentation.BuilderDetails[] {{ SharpSheets.Documentation.BuilderDocs.{GetDocumentationVariableName(withDocs[0].Builders[0].Builder)} }}" : $"{GetBuilderNamesMethodName(withDocs[0].Spec)}()")}");

				foreach (FactoryToGenerate fact in withDocs.Skip(1)) {
					if (fact.IsSingleton) {
						sb.Append(@$"
				.Append(new KeyValuePair<string, SharpSheets.Documentation.BuilderDetails>({fact.Builders[0].Builder.Name.ToRepr()}, SharpSheets.Documentation.BuilderDocs.{GetDocumentationVariableName(fact.Builders[0].Builder)}))");
					}
					else {
						sb.Append(@$"
				.Concat({GetBuilderNamesMethodName(fact.Spec)}())");
					}
				}

				sb.Append(@$";
");

				ct.ThrowIfCancellationRequested();
			}

			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
		}

		private static string GetBuilderNamesMethodName(FactorySpecification spec) {
			return $"Get{spec.FactoryType.Name}BuilderNames";
		}

		static string? GenerateFactoryDocumentation(FactoryToGenerate factory, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

			if(!factory.Spec.IncludeDocs) { return null; }

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

			if (factory.IsSingleton) {
				sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static SharpSheets.Documentation.BuilderDetails {factory.Spec.FactoryType.Name}Builder => SharpSheets.Documentation.BuilderDocs.{GetDocumentationVariableName(factory.Builders[0].Builder)};
");
			}
			else {
				string nameSetVariable = $"All{factory.Spec.FactoryType.Name}Names";
				string nameLookupDictionaryVariable = $"__generated__{factory.Spec.FactoryType.Name}_details_namelookup";
				string typeLookupDictionaryVariable = $"__generated__{factory.Spec.FactoryType.Name}_details_typelookup";

				// Name set
				sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}
		public static readonly System.Collections.Generic.IReadOnlySet<string> {nameSetVariable} = new System.Collections.Generic.HashSet<string>(StringComparer.InvariantCultureIgnoreCase) {{");

				foreach (BuilderToGenerate builder in factory.Builders) {
					sb.Append(@$"
				{builder.Builder.Name.ToRepr()},");
				}

				sb.Append(@$"
			}};
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

				ct.ThrowIfCancellationRequested();

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

		public static IEnumerable<KeyValuePair<string, SharpSheets.Documentation.BuilderDetails>> {GetBuilderNamesMethodName(factory.Spec)}() => {nameLookupDictionaryVariable}.Select(kv => new KeyValuePair<string, SharpSheets.Documentation.BuilderDetails>(kv.Key, kv.Value()));
");
			}

			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
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
			{ IBox, ("MakeBox", ShapeMakerArgs.ASPECT) },
			{ ILabelledBox, ("MakeLabelledBox", ShapeMakerArgs.ASPECT) },
			{ ITitledBox, ("MakeTitledBox", ShapeMakerArgs.ASPECT_NAME) },
			{ ITitleStyle, ("MakeTitleStyle", ShapeMakerArgs.NONE) },
			{ IEntriedShape, ("MakeEntried", ShapeMakerArgs.ASPECT) },
			{ IBar, ("MakeBar", ShapeMakerArgs.ASPECT) },
			{ IUsageBar, ("MakeUsageBar", ShapeMakerArgs.ASPECT) },
			{ IDetail, ("MakeDetail", ShapeMakerArgs.NONE) }
		};

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
			return builder.Parameters.Any(p => p.Type.Minimal.Contains(ChildHolder));
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
			return name.TrimStart('@').Trim('_').Replace("_", "-").ToLowerInvariant();
		}

		private static void GenerateBuilderCode(StringBuilder sb, BuilderToGenerate builder, TypeLookup<ParameterParser> parserLookup, TypeLookup<BuilderToGenerate> builderLookup, bool includeGeneratedAttributes, Compilation compilation, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

			bool builderNeedsShapeFactory = NeedsShapeFactory(builder.Builder);
			bool builderNeedsWidgetFactory = NeedsWidgetFactory(builder.Builder);

			bool returnNeedsNullableAnnotation = builder.Builder.ConcreteBuilderType.IsNullable || !builder.Builder.Parameters.Skip(builder.RequiredParameters.Count).All(p => p.IsOptional);

			string builderReturnTypeFull = $"{builder.Builder.ConcreteBuilderType.Type}{(returnNeedsNullableAnnotation ? "?" : "")}";
			string errorListVariableName = "@buildErrorsList";
			string builderErrorsParamVariable = "@buildErrorsParamList";
			bool needsBuilderErrorsParam = false;

			string sourceDirArgName = "@source";

			if (includeGeneratedAttributes) {
				sb.Append(@$"
		{GeneratorMarkers.GeneratedCodeAttr}
		{GeneratorMarkers.CompilerGeneratedAttr}");
			}

			(string fullType, string reqParamName)[] requiredFullParams = builder.Builder.Parameters.Where(p => !p.IsBuildErrors).Zip(builder.RequiredParameters).Where(i => !i.Item1.IsSourceDir).Select(i => (i.Item1.Type.FullName, i.Item2.Name)).ToArray();

			sb.Append(@$"
		public static {builderReturnTypeFull} {builder.MethodName}(SharpSheets.Parsing.IContext context{(requiredFullParams.Length > 0 ? (", " + string.Join(", ", requiredFullParams.Select(p => $"{p.fullType} {p.reqParamName}"))) : "")}, SharpSheets.Utilities.DirectoryPath {sourceDirArgName},{(builderNeedsWidgetFactory ? " SharpSheets.Widgets.WidgetFactory widgetFactory," : "")}{(builderNeedsShapeFactory ? " SharpSheets.Shapes.ShapeFactory shapeFactory," : "")} out SharpSheets.Exceptions.SharpParsingException[] buildErrors) {{
			System.Collections.Generic.List<SharpSheets.Exceptions.SharpParsingException> {errorListVariableName} = new System.Collections.Generic.List<SharpSheets.Exceptions.SharpParsingException>();
");
			// {builder.Builder.Namespace}, {builder.Builder.FullTypeName}, {builder.MethodName}, {builder.Builder.BuilderType}, {builder.Builder.Name}, {builder.Builder.Structure}");

			//List<string> paramVariables = new List<string>(requiredParams.Select(r => r.Name));
			List<string> paramVariables = new List<string>(builder.Builder.Parameters.Count);

			int count = 0;
			foreach ((int pIdx, BuilderParameter param) in builder.Builder.Parameters.Enumerate()) {
				ct.ThrowIfCancellationRequested();

				if (param.IsBuildErrors) {
					paramVariables.Add(builderErrorsParamVariable);
					needsBuilderErrorsParam = true;
					continue;
				}

				if (count < builder.RequiredParameters.Count) {
					paramVariables.Add(param.IsSourceDir ? sourceDirArgName : builder.RequiredParameters[count].Name);
					count++;
					continue;
				}
				else {
					count++;
				}

				if(param.IsSourceDir) {
					paramVariables.Add(sourceDirArgName);
					continue;
				}

				ITypeSymbol? paramResolvedType = param.Type.GetSymbol(compilation); // compilation.ResolveTypeKey(param.Type.FullName);
				ITypeSymbol? paramReducedType = paramResolvedType is not null ? compilation.ReduceParameterType(paramResolvedType) : null;

				ParameterParser? parser = parserLookup.TryGetValue(param.Type, out ParameterParser foundParser) ? foundParser : null;
				BuilderToGenerate? paramBuilder = builderLookup.TryGetValue(param.Type, out BuilderToGenerate foundBuilder) ? foundBuilder : null;

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
			bool {valueVariable};
			if (context.HasFlag(""{normParamName}"", {param.IsLocal.ToCodeString()}, context)) {{
				{valueVariable} = context.GetFlag(""{normParamName}"", {param.IsLocal.ToCodeString()}, context);
			}}
			else {{
				{valueVariable} = {param.DefaultValue ?? "false"};
			}}");
				}
				else if (paramResolvedType is INamedTypeSymbol namedListType && namedListType.IsGenericList(out ITypeSymbol listElemType) && compilation.ReduceParameterType(listElemType) is ITypeSymbol reducedListElemType && parserLookup.TryGetValue(TypeData.Create(reducedListElemType), out ParameterParser listElemParser)) {
					string reducedListTypeName = compilation.ReduceParameterType(namedListType).ToFullDisplayString();
					sb.Append(@$"
			{reducedListTypeName} {valueVariable} = new {reducedListTypeName}();
			foreach (ContextValue<string> @entry in context.GetEntries(context)) {{
				try {{
					{listElemType.ToFullDisplayString()} @entryParsed = {listElemParser.FullTypeName}.{listElemParser.MethodName}(@entry.Value{(listElemParser.NeedsSourceDirectory ? $", {sourceDirArgName}" : "")});
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

					if (!parserLookup.TryGetValue(TypeData.Create(reducedNumberedElemType), out ParameterParser? numberedElemParser)) {
						numberedElemParser = null;
					}

					if (numberedElemParser is null && reducedNumberedElemTypeName != ChildHolder) {
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

					if (reducedNumberedElemTypeName == ChildHolder) {
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

					if (reducedNumberedElemTypeName == ChildHolder) {
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
					SharpSheets.Widgets.Div? {entryConstructedChildVariable} = widgetFactory.MakeDiv({entryNonRecurseContextVariable}, {sourceDirArgName}, out SharpSheets.Exceptions.SharpParsingException[] {entryBuildErrorsVariable});
					if ({entryConstructedChildVariable} is SharpSheets.Widgets.Div {entryConcreteChildVariable}) {{
						{ChildHolder} {entryValueVariable} = new SharpSheets.Parsing.ChildHolder({entryConcreteChildVariable});
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
						{numberedElemFullTypeName} {entryValueVariable} = {numberedElemParser.FullTypeName}.{numberedElemParser.MethodName}({entryTextVariable}{(numberedElemParser.NeedsSourceDirectory ? $", {sourceDirArgName}" : "")});
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
						ParameterParser floatParser = parserLookup[SpecialType.System_Single];

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
						ParameterParser stringParser = parserLookup[SpecialType.System_String];

						sb.Append(@$"
			string? {paramNameVariable} = context.GetProperty(""name"", true, context, null, {stringParser.FullTypeName}.{stringParser.MethodName}, out DocumentSpan? {paramNameLocVariable});");
						shapeMakerArgNames.Add(paramNameVariable);
					}

					sb.Append(@$"
			{param.Type.FullName} {valueVariable} = shapeFactory.{shapeMaker.method}({paramContextVariable}{(shapeMakerArgNames.Count > 0 ? (", " + string.Join(", ", shapeMakerArgNames)) : "")}, {sourceDirArgName}, out SharpSheets.Exceptions.SharpParsingException[] {paramShapeBuildErrorsVariable});
			{errorListVariableName}.AddRange({paramShapeBuildErrorsVariable});");
				}
				else if (param.Type.Minimal == ChildHolder) {
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
				SharpSheets.Widgets.Div? {paramConstructedChildVariable} = widgetFactory.MakeDiv({paramNonRecurseContextVariable}, {sourceDirArgName}, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
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
					{valueVariable} = {parser.FullTypeName}.{parser.MethodName}({valueTextVariable}{(parser.NeedsSourceDirectory ? $", {sourceDirArgName}" : "")});
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
			{param.Type.FullName} {valueVariable} = {paramBuilder.FullTypeName}.{paramBuilder.MethodName}({paramContextVariable}, {sourceDirArgName}, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
			{errorListVariableName}.AddRange({paramBuildErrorsVariable});");
					}
					else if (paramBuilder.Builder.Structure == BuilderStructure.SUPPLEMENTED) {
						BuilderParameter firstArg = paramBuilder.Builder.Parameters.First(p => p.ParameterType == BuilderParameterType.Normal);
						string firstArgVariable = valueVariable + "_first";
						string firstArgTextVariable = firstArgVariable + "_str";
						string firstArgLocVariable = firstArgVariable + "_loc";
						string paramContextVariable = valueVariable + "_context";

						if (!parserLookup.TryGetValue(firstArg.Type, out ParameterParser? firstArgParser)) {
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
					{firstArgVariable} = {firstArgParser.FullTypeName}.{firstArgParser.MethodName}({firstArgTextVariable}{(firstArgParser.NeedsSourceDirectory ? $", {sourceDirArgName}" : "")});
				}}
				catch (System.FormatException e) {{
					{errorListVariableName}.Add(new SharpSheets.Exceptions.SharpParsingException({firstArgLocVariable}, e.Message, e));
					{firstArgVariable} = {firstArg.DefaultValue ?? "NULL"};
				}}
			}}
			else {{
				{firstArgVariable} = {firstArg.DefaultValue ?? "NULL"};
			}}
			SharpSheets.Parsing.IContext {paramContextVariable} = new SharpSheets.Parsing.NamedContext(context, ""{normParamName}"", forceLocal: {param.IsLocal.ToCodeString()});
			{param.Type.FullName} {valueVariable} = {paramBuilder.FullTypeName}.{paramBuilder.MethodName}({paramContextVariable}, {firstArgVariable}, {sourceDirArgName}, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
			{errorListVariableName}.AddRange({paramBuildErrorsVariable});");
						}
						else { // Non-optional first argument for nested supplementary arg
							sb.Append(@$"
			{param.Type.FullName} {valueVariable};
			if ({firstArgTextVariable} != null) {{
				try {{
					{firstArg.Type.FullName} {firstArgVariable} = {firstArgParser.FullTypeName}.{firstArgParser.MethodName}({firstArgTextVariable}{(firstArgParser.NeedsSourceDirectory ? $", {sourceDirArgName}" : "")});
					SharpSheets.Parsing.IContext {paramContextVariable} = new SharpSheets.Parsing.NamedContext(context, ""{normParamName}"", forceLocal: {param.IsLocal.ToCodeString()});
					{valueVariable} = {paramBuilder.FullTypeName}.{paramBuilder.MethodName}(context, {firstArgVariable}, {sourceDirArgName}, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
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
						// TODO Might need to include prefix separator here?
						sb.Append(@$"
			{param.Type.FullName} {valueVariable} = {paramBuilder.FullTypeName}.{paramBuilder.MethodName}(context, {sourceDirArgName}, out SharpSheets.Exceptions.SharpParsingException[] {paramBuildErrorsVariable});
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

		private static bool NeedsSource(ITypeSymbol symbol, TypeLookup<ParameterParser> parserLookup) {
			if (parserLookup.TryGetValue(TypeData.Create(symbol), out ParameterParser parser)) {
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

		private static ParameterParser GetParser(ITypeSymbol symbol, TypeLookup<ParameterParser> parserLookup) {
			if (parserLookup.TryGetValue(TypeData.Create(symbol), out ParameterParser parser)) {
				return parser;
			}
			else {
				bool needsSourceDirectory = NeedsSource(symbol, parserLookup);
				return ParameterParser.GetGeneratedParser(symbol, needsSourceDirectory);
			}
		}

		private static (EquatableArray<ParameterParser> neededParamParsers, EquatableArray<BuilderToGenerate> neededMiscBuilders, EquatableArray<TypeData> cannotGenerateParserTypes) FilterParamParsers(ImmutableArray<FactoryToGenerate> factories, ImmutableArray<AvailableBuilder> builders, ImmutableArray<ParameterParser> parsers, ImmutableArray<TypeData> requestedParsers, Compilation compilation, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

			TypeLookup<AvailableBuilder> builderLookup = new TypeLookup<AvailableBuilder>(builders.Select(b => (b.ConcreteBuilderType, b))); //.ToDictionary(b => b.ConcreteBuilderType.FullName);
			TypeLookup<ParameterParser> parserLookup = new TypeLookup<ParameterParser>(parsers.Select(p => (p.ParserType, p))); //.ToDictionary(p => p.ParserType.FullName);
			HashSet<string> factoryTypes = new HashSet<string>(factories.Select(f => f.Spec.FactoryType.FullName));

			HashSet<AvailableBuilder> explicitFactoryBuilders = new HashSet<AvailableBuilder>();

			// Need to know all builders that are actually used by a known factory
			foreach (FactoryToGenerate factory in factories) {
				explicitFactoryBuilders.UnionWith(factory.Builders.Select(b => b.Builder));
			}

			ct.ThrowIfCancellationRequested();

			// Breadth-first search for builders used by other builders
			HashSet<AvailableBuilder> additionalNonFactoryBuilders = new HashSet<AvailableBuilder>(); // Running list
			HashSet<AvailableBuilder> toAdd = new HashSet<AvailableBuilder>(explicitFactoryBuilders); // We know we're using these
			HashSet<AvailableBuilder> additionalBuilders = new HashSet<AvailableBuilder>(); // To find new ones we didn't previously know we'd need
			do {
				ct.ThrowIfCancellationRequested();

				// For all parameters in the builders we found we'd need
				foreach (AvailableBuilder addBuilder in toAdd) {
					foreach (BuilderParameter param in addBuilder.Parameters) {
						if (builderLookup.TryGetValue(param.Type, out AvailableBuilder foundBuilder)) {
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

			// Parser types that have been explicitly requested
			TypeData[] requestedParserTypes = requestedParsers.ToArray();

			List<TypeData> cannotGenerateParserTypes = new List<TypeData>();

			// Find the parsers we need to generate (i.e. that do not require builders, and are not explicitly defined for us, or have been explicitly requested)
			HashSet<string> parserTypesToImplement = new HashSet<string>();
			List<ParameterParser> parsersToImplement = new List<ParameterParser>();
			Queue<TypeData> typeQueue = new Queue<TypeData>(additionalNonFactoryBuilders
				.SelectMany(b => b.Parameters)
				.Concat(factories
					.SelectMany(f => f.Builders)
					.SelectMany(b => b.GetParams())
					.Where(bk => bk.kind == BuilderParamKind.None)
					.Select(bk => bk.param)
					)
				.Where(b => !b.IsBuildErrors) // We don't parse these
				.Select(p => p.Type)
				.Distinct()
				.Concat(requestedParserTypes));

			while (typeQueue.Count > 0) {
				ct.ThrowIfCancellationRequested();

				TypeData typeData = typeQueue.Dequeue();
				ITypeSymbol? type = compilation.ResolveTypeKey(typeData.Minimal);

				if (type is null) { continue; }

				ITypeSymbol reducedType = compilation.ReduceParameterType(type);

				if (reducedType is INamedTypeSymbol namedType) {
					// Don't want to parse List<> or Numbered<> directly, but we do want to parse their contents
					if (namedType.IsGenericList(out ITypeSymbol listElemType)) {
						typeQueue.Enqueue(TypeData.Create(listElemType));
						continue;
					}
					else if (namedType.IsGenericNumbered(out ITypeSymbol numberedElemType)) {
						typeQueue.Enqueue(TypeData.Create(numberedElemType));
						continue;
					}
				}

				string minimalType = typeData.Minimal;
				string fullType = typeData.FullName;

				if (builderLookup.ContainsKey(typeData) || parserLookup.ContainsKey(typeData) || factoryTypes.Contains(minimalType) || shapeMakerLookup.ContainsKey(minimalType) || minimalType == ChildHolder) {
					// Either this type requires a whole builder, or the parser is already defined for us
					continue;
				}

				if (parserTypesToImplement.Contains(minimalType)) {
					// We've already logged this parser
					continue;
				}

				if (reducedType is IArrayTypeSymbol arrayType) {
					typeQueue.Enqueue(TypeData.Create(arrayType));
				}
				else if (reducedType.IsTupleType && reducedType is INamedTypeSymbol tupleType) {
					typeQueue.Enqueue(tupleType.TupleElements.Select(f => TypeData.Create(f.Type)));
				}
				else if (!(reducedType is INamedTypeSymbol namedReducedType && namedReducedType.IsEnum())) {
					// We can only generate parsers for arrays, tuples, and enums
					// So here we've encountered a problem and need to stop
					cannotGenerateParserTypes.Add(typeData);
					continue;
					// TODO Can we report a diagnostic here?
				}

				parsersToImplement.Add(ParameterParser.GetGeneratedParser(reducedType, NeedsSource(reducedType, parserLookup)));
				parserTypesToImplement.Add(minimalType);
			}

			List<BuilderToGenerate> miscBuildersToGenerate = new List<BuilderToGenerate>();

			foreach(AvailableBuilder builder in additionalNonFactoryBuilders) {
				ct.ThrowIfCancellationRequested();

				ITypeSymbol? builderContainingType = compilation.ResolveTypeKey(builder.FullTypeName);

				if (builderContainingType is null) {
					continue;
				}

				string clarifiedBuilderName = builder.TypeName + "_" + builder.MethodName;

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

				ct.ThrowIfCancellationRequested();

				miscBuildersToGenerate.Add(new BuilderToGenerate(builder, new EquatableArray<RequiredParameter>(GetRequiredParameters(builder).ToArray()), "SharpSheets.Parsing", "ParameterBuilders", $"Build_{clarifiedBuilderName}"));
			}

			ct.ThrowIfCancellationRequested();

			return (
				new EquatableArray<ParameterParser>(parsersToImplement.OrderBy(p => p.ParserType.Name).ToArray()),
				new EquatableArray<BuilderToGenerate>(miscBuildersToGenerate.OrderBy(b => b.Builder.Name).ToArray()),
				new EquatableArray<TypeData>(cannotGenerateParserTypes.ToArray())
				);
		}

		public static readonly char[] arrayDelimiters = { ',', ';', '|' };

		private static string? GenerateParamParsersCode(EquatableArray<ParameterParser> neededParsers, ImmutableArray<ParameterParser> existingParsers, Compilation compilation, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

			TypeLookup<ParameterParser> parserLookup = new TypeLookup<ParameterParser>(existingParsers.Concat(neededParsers).Select(p => (p.ParserType, p))); //.ToDictionary(p => p.ParserType.FullName);

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

			string sourceDirArgName = "@source";

			ct.ThrowIfCancellationRequested();

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
				ct.ThrowIfCancellationRequested();

				int parseRank = typeSymbol.GetArrayOrTupleRank();
				bool needsSource = NeedsSource(typeSymbol, parserLookup);

				if (parser.FullTypeName != ParameterParsers) {
					continue;
				}

				if (parseRank > 0) {
					sb.Append(@$"
		public static {typeSymbol.ToFullDisplayString()} {parser.MethodName}(string value{(needsSource ? $", SharpSheets.Utilities.DirectoryPath {sourceDirArgName}" : "")}) {{
			string[] parts = SharpSheets.Parsing.StringParsing.SplitOnUnescaped(value, '{arrayDelimiters[parseRank - 1]}').Select(s => s.Trim()).ToArray();");

					if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol) {
						ParameterParser elemParser = GetParser(arrayTypeSymbol.ElementType, parserLookup);
						sb.Append(@$"
			{typeSymbol.ToFullDisplayString()} result = parts.Select(p => {elemParser.FullTypeName}.{elemParser.MethodName}(p{(elemParser.NeedsSourceDirectory ? $", {sourceDirArgName}" : "")})).ToArray();");
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
							ParameterParser fieldParser = GetParser(field.Type, parserLookup);
							if (f > 0) { sb.Append(','); }
							sb.Append(@$"
				{fieldParser.FullTypeName}.{fieldParser.MethodName}(parts[{f}]{(fieldParser.NeedsSourceDirectory ? $", {sourceDirArgName}" : "")})");
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
			#error No provided parser for required type {typeSymbol.ToFullDisplayString()}
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
				ct.ThrowIfCancellationRequested();

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

		private static string? GenerateMiscBuildersCode(EquatableArray<BuilderToGenerate> neededMiscBuilders, IList<ParameterParser> availableParsers, Compilation compilation, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

			TypeLookup<ParameterParser> parserLookup = new TypeLookup<ParameterParser>(availableParsers.Select(p => (p.ParserType, p))); //.ToDictionary(p => p.ParserType.FullName);
			TypeLookup<BuilderToGenerate> builderLookup = new TypeLookup<BuilderToGenerate>(neededMiscBuilders.Select(b => (b.Builder.BuilderType, b))); //.ToDictionary(p => p.Builder.BuilderType.FullName);

			ct.ThrowIfCancellationRequested();

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
				GenerateBuilderCode(sb, builder, parserLookup, builderLookup, false, compilation, ct);
				sb.Append('\n');
			}

			sb.Append(@"
	}
}

#pragma warning restore");

			return sb.ToString();
		}

		public static readonly string DocumentationSourceName = "DocumentationSource";

		private static string GetDocumentationVariableName(AvailableBuilder builder) {
			return builder.ConcreteBuilderType.Type.Replace('.', '_');
		}

		private static string? GenerateBuilderDocumentationCode(ImmutableArray<FactoryToGenerate> factories, ImmutableArray<AvailableBuilder> availableBuilders, ImmutableArray<ParameterParser> availableParsers, EquatableArray<BuilderToGenerate> generatedBuilders, EquatableArray<ParameterParser> generatedParsers, Compilation compilation, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

			SharpSheetsParameterResolverData resolverData = SharpSheetsParameterResolverData.Create(compilation, availableBuilders, availableParsers.Concat(generatedParsers), factories);
			//SharpSheetsParameterResolverData resolverData = SharpSheetsParameterResolverData.Create(compilation, availableBuilders.Concat(generatedBuilders.Select(b => b.Builder)), availableParsers.Concat(generatedParsers), factories);

			Dictionary<string, string> typeToDocsVariable = availableBuilders.ToDictionary(b => b.ConcreteBuilderType.FullName, GetDocumentationVariableName);

			ct.ThrowIfCancellationRequested();

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
				ct.ThrowIfCancellationRequested();

				IMethodSymbol? method = compilation.ResolveMethodSymbol(builder.FullTypeName, builder.MethodName).FirstOrDefault(m => m.Parameters.Length == builder.Parameters.Count);

				if (method is null) {
					continue;
				}

				BuilderComment methodComment = DocCommentReader.FromSymbol(method, resolverData);

				string concreteBuilderTypeMinimal = builder.ConcreteBuilderType.Type;
				string concreteBuilderTypeNameMinimal = builder.ConcreteBuilderType.Name;

				string docVariableName = GetDocumentationVariableName(builder);
				string docVariableLazyName = $"__generated__lazy_{docVariableName}";

				// BuilderDetails(Type displayType, Type declaringType, string name, string fullName, ArgumentDetails[] arguments, DocumentationString? description, Rectangle? size, Size? canvas)
				sb.Append(@$"
		{GeneratorMarkers.NeverEditorBrowsableAttr}
		private static readonly System.Lazy<SharpSheets.Documentation.BuilderDetails> {docVariableLazyName} = new System.Lazy<SharpSheets.Documentation.BuilderDetails>(() =>
			new SharpSheets.Documentation.BuilderDetails(
					displayType: {DisplayType_Simple(builder.BuilderType.Type)}, // typeof({builder.BuilderType.Type}),
					declaringType: {DisplayType_Simple(concreteBuilderTypeMinimal)}, // typeof({concreteBuilderTypeMinimal}),
					name: {builder.Name.ToRepr()},
					fullName: {builder.Name.ToRepr()},
					description: {methodComment?.Summary ?? "null"},
					size: {methodComment?.Size ?? "null"},
					canvas: {methodComment?.Canvas ?? "null"},
					example: {(methodComment?.Example is not null ? $"new System.Lazy<object>(() => {methodComment.Example})" : "null")},
					arguments: [");

				foreach (SharpSheetsParameterData param in SharpSheetsParameterResolver.GetArguments(builder, method, methodComment, resolverData)) {
					ct.ThrowIfCancellationRequested();

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
								){string.Join("", param.Prefixes.Select(p => $".Prefixed({p.prefix.ToRepr()}{(p.separator is not null ? $", {p.separator.ToRepr()}" : "")})"))},");
					}
					else {
						sb.Append(@$"
							.. {typeToDocsVariable[param.DeferArgsTo]}.Arguments");
						if (param.SkipDeferredArgs > 0) {
							sb.Append($"[{param.SkipDeferredArgs}..]");
						}
						if (param.Prefixes.Length > 0 || param.UseLocal) {
							sb.Append(".Select(a => a");
							if (param.Prefixes.Length > 0) {
								sb.Append(string.Join("", param.Prefixes.Select(p => $".Prefixed({p.prefix.ToRepr()}{(p.separator is not null ? $", {p.separator.ToRepr()}" : "")})")));
							}
							if (param.UseLocal) {
								sb.Append($".WithLocal(forceLocal: {param.UseLocal.ToCodeString()})");
							}
							sb.Append(")");
						}
						sb.Append(",");
					}
				}
				
				sb.Append(@$"
						]
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

		private static string? GenerateEnumDocumentationCode(ImmutableArray<EnumComment> declaredEnums, CancellationToken ct) {
			ct.ThrowIfCancellationRequested();

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

			foreach (EnumComment enumComment in declaredEnums) {
				ct.ThrowIfCancellationRequested();

				string docVariableName = enumComment.FullType.Replace('.', '_');

				allDocs.Add((enumComment.FullType, enumComment.Name, docVariableName));

				sb.Append(@$"
		public static readonly SharpSheets.Documentation.EnumDoc {docVariableName} = new SharpSheets.Documentation.EnumDoc(
				{enumComment.Name.ToRepr()},
				new EnumValDoc[] {{");

				foreach ((string valueName, string? valueDescription) in enumComment.Values) {
					sb.Append(@$"
					new SharpSheets.Documentation.EnumValDoc({enumComment.Name.ToRepr()}, {valueName.ToRepr()}, {valueDescription ?? "null"}),");
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

			foreach ((string fullTypeName, string typeName, string docVariableName) in allDocs.OrderBy(d => d.typeName)) {
				sb.Append(@$"
				{{ {fullTypeName.ToRepr()}, {docVariableName} }},");
			}

			sb.Append(@$"
			}};
");

			sb.Append(@"
		public static bool TryGetDocumentation(string typeName, out SharpSheets.Documentation.EnumDoc enumDoc) {
			return __generated__allDocs_names.TryGetValue(typeName, out enumDoc);
		}
");

			ct.ThrowIfCancellationRequested();

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

}
