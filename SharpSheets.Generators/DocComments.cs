using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using static SharpSheets.Generators.FactoryGenerator;

namespace SharpSheets.Generators {

	public record class ParamComment {
		public readonly string Name;
		/// <summary> DocumentationString constructor code. </summary>
		public readonly string? Description;
		/// <summary>
		/// Construction code for default value.
		/// </summary>
		public readonly string? DefaultValue;
		/// <summary>
		/// Construction code for example value.
		/// </summary>
		public readonly string? ExampleValue;
		public readonly bool Exclude;

		public ParamComment(string name, string? description, string? defaultValue, string? exampleValue, bool exclude) {
			Name = name;
			Description = description;
			DefaultValue = defaultValue;
			ExampleValue = exampleValue;
			Exclude = exclude;
		}
	}

	public sealed class BuilderComment {
		/// <summary> DocumentationString constructor code. </summary>
		public string? Summary { get; }
		public EquatableDictionary<string, ParamComment> Params { get; }
		/// <summary> DocumentationString constructor code. </summary>
		public string? Returns { get; }
		/// <summary> DocumentationString constructor code. </summary>
		public string? Remarks { get; }

		/// <summary> SharpSheets.Layouts.Rectangle constructor code. </summary>
		public string? Size { get; }
		/// <summary> SharpSheets.Layouts.Size constructor code. </summary>
		public string? Canvas { get; }

		public BuilderComment(
				string? summary,
				IEnumerable<ParamComment> @params,
				string? returns,
				string? remarks,
				string? size,
				string? canvas
			) {

			Summary = summary;
			Params = new EquatableDictionary<string, ParamComment>(@params.ToDictionary(p => p.Name));
			Returns = returns;
			Remarks = remarks;
			Size = size;
			Canvas = canvas;
		}
	}

	public record class SummaryComment {
		/// <summary> DocumentationString constructor code. </summary>
		public readonly string? Summary;

		public SummaryComment(string? summary) {
			Summary = summary;
		}
	}

	public record class EnumComment {
		public readonly string FullType;
		public readonly string Name;
		public readonly string? Summary;
		public readonly EquatableArray<(string name, string? description)> Values;

		public EnumComment(string fullType, string name, string? summary, IEnumerable<(string name, string? description)> values) {
			FullType = fullType;
			Name = name;
			Summary = summary;
			Values = new EquatableArray<(string, string?)>(values.ToArray());
		}
	}

	public static class DocCommentReader {

		public static BuilderComment? FromSymbol(IMethodSymbol symbol, Dictionary<string, AvailableBuilder> builderLookup, Dictionary<string, ParameterParser> parserLookup, SharpSheetsParameterResolverData resolverData, CancellationToken ct = default) {

			string? xml = symbol.GetDocumentationCommentXml(preferredCulture: null, expandIncludes: true, cancellationToken: ct);

			if (string.IsNullOrWhiteSpace(xml)) {
				return null;
			}

			try {
				XDocument doc = XDocument.Parse(xml);

				string? summary = GetDocumentationString(doc.Root.Element("summary"), resolverData.Compilation);
				string? returns = GetDocumentationString(doc.Root.Element("returns"), resolverData.Compilation);
				string? remarks = GetDocumentationString(doc.Root.Element("remarks"), resolverData.Compilation);
				string? size = GetRectangle(doc.Root.Element("size"));
				string? canvas = GetSize(doc.Root.Element("canvas"));

				Dictionary<string, IParameterSymbol> paramLookup = symbol.Parameters.ToDictionary(p => p.Name);

				List<ParamComment> paramDocs = new List<ParamComment>();
				IEnumerable<XElement> paramEls = doc.Root.Elements("param");
				foreach (XElement el in paramEls) {
					XAttribute nameAttr = el.Attribute("name");
					if (nameAttr != null) {
						string name = nameAttr.Value;
						string? text = GetDocumentationString(el, resolverData.Compilation);
						IParameterSymbol? paramSymbol = paramLookup.TryGetValue(name, out IParameterSymbol val) ? val : null;
						paramDocs.Add(new ParamComment(name, text, GetDefaultValue(paramSymbol, resolverData), GetExampleValue(paramSymbol, resolverData), GetExclude(paramSymbol)));
					}
				}

				return new BuilderComment(summary, paramDocs, returns, remarks, size, canvas);
			}
			catch (System.Xml.XmlException) {
				// Malformed XML
				return null;
			}
		}

		public static SummaryComment? FromSymbol(ISymbol symbol, Compilation compilation, CancellationToken ct = default) {

			string? xml = symbol.GetDocumentationCommentXml(preferredCulture: null, expandIncludes: true, cancellationToken: ct);

			if (string.IsNullOrWhiteSpace(xml)) {
				return null;
			}

			try {
				XDocument doc = XDocument.Parse(xml);

				string? summary = GetDocumentationString(doc.Root.Element("summary"), compilation);

				return new SummaryComment(summary);
			}
			catch (System.Xml.XmlException) {
				// Malformed XML
				return null;
			}
		}

		public static EnumComment? FromEnumSymbol(INamedTypeSymbol symbol, Compilation compilation, CancellationToken ct = default) {

			string? xml = symbol.GetDocumentationCommentXml(preferredCulture: null, expandIncludes: true, cancellationToken: ct);

			string? summary;
			try {
				XDocument doc = XDocument.Parse(xml);
				summary = GetDocumentationString(doc.Root.Element("summary"), compilation);
			}
			catch (System.Xml.XmlException) {
				// Malformed XML
				summary = null;
			}

			List<(string name, string? descriptions)> values = new List<(string, string?)>();

			foreach (IFieldSymbol enumField in symbol.GetDeclaredEnumMembers()) {
				SummaryComment? enumValComment = DocCommentReader.FromSymbol(enumField, compilation);

				values.Add((enumField.Name, enumValComment?.Summary));
			}

			if (summary is not null || values.Count > 0) {
				return new EnumComment(symbol.ToFullDisplayString(), symbol.Name, summary, values);
			}
			else {
				return null;
			}
		}

		public static string MakeDocumentationString(string? content) {
			return $"new SharpSheets.Documentation.DocumentationString({content ?? ""})";
		}

		public static string MakeDocumentationStringFromText(string? text) {
			return $"new SharpSheets.Documentation.DocumentationString({(text is not null ? GetTextSpan(text) : "")})";
		}

		private static string? GetDocumentationString(XElement? element, Compilation compilation) {
			if (element is null) {
				return null;
			}

			string[] content = GetElementSpanContents(element, compilation).ToArray();

			return content.Length == 0 ? null : MakeDocumentationString(string.Join(", ", content));
		}

		private static string? GetDocumentationValue(ITypeSymbol type, string? constant, SharpSheetsParameterResolverData resolverData) {
			if (resolverData.ParserLookup.TryGetValue(type.ToFullDisplayString(), out ParameterParser parser)) {
				if (constant is not null) {

					if (parser.ParserType.SpecialType != SpecialType.None) {
						if (parser.ParserType.SpecialType == SpecialType.System_Single) {
							try {
								return $"{float.Parse(constant, CultureInfo.InvariantCulture)}f";
							}
							catch (FormatException) { }
						}
						else if (parser.ParserType.SpecialType == SpecialType.System_Int32) {
							try {
								return $"{int.Parse(constant, CultureInfo.InvariantCulture)}";
							}
							catch (FormatException) { }
						}
						else if (parser.ParserType.SpecialType == SpecialType.System_UInt32) {
							try {
								return $"{uint.Parse(constant, CultureInfo.InvariantCulture)}U";
							}
							catch (SystemException) { }
						}
						else if (parser.ParserType.SpecialType == SpecialType.System_Boolean) {
							try {
								return bool.Parse(constant) ? "true" : "false";
							}
							catch (SystemException) { }
						}
						else if (parser.ParserType.SpecialType == SpecialType.System_String) {
							return constant.ToRepr();
						}
					}
					else if (parser.ParserType.FullName == "SharpSheets.Utilities.UFloat") {
						try {
							return $"new SharpSheets.Utilities.UFloat({float.Parse(constant, CultureInfo.InvariantCulture)}f)";
						}
						catch (FormatException) { }
					}
					else if (parser.ParserType.FullName == "SharpSheets.Layouts.Margins") {
						string[] content = constant.Trim().TrimStart('(').TrimEnd(')').Split(',').Select(i => i.Trim()).ToArray();

						float[] values = content.Select(i => {
							try {
								return float.Parse(i, CultureInfo.InvariantCulture);
							}
							catch (FormatException) {
								return float.NaN;
							}
						}).ToArray();

						if (values.All(i => !float.IsNaN(i))) {
							if (values.Length == 1 || values.Length == 2 || values.Length == 4) {
								return $"new SharpSheets.Layouts.Margins({string.Join(", ", values.Select(v => $"{v}f"))})";
							}
						}
					}
					else if (parser.ParserType.FullName == "SharpSheets.Layouts.Dimension") {
						if (string.Equals(constant, "auto", StringComparison.InvariantCultureIgnoreCase)) {
							return "SharpSheets.Layouts.Dimension.Automatic";
						}

						Regex dimensionRegex = new Regex(@"^(?<number>[\+\-]?[0-9]+\.[0-9]+|\.[0-9]+|[\+\-]?[0-9]+\.?)\s*(?<unit>pt|in|cm|mm|pc|\%)?$", RegexOptions.IgnoreCase);

						Match match = dimensionRegex.Match(constant.Trim());
						if (match.Success) {
							float number = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
							string unit = match.Groups[2].Value.ToLowerInvariant();
							if (unit == "pc" || unit == "%") {
								return $"SharpSheets.Layouts.Dimension.FromPercent({number}f)";
							}
							else if (unit == "pt") {
								return $"SharpSheets.Layouts.Dimension.FromPoints({number}f)";
							}
							else if (unit == "in") {
								return $"SharpSheets.Layouts.Dimension.FromInches({number}f)";
							}
							else if (unit == "cm") {
								return $"SharpSheets.Layouts.Dimension.FromCentimetres({number}f)";
							}
							else if (unit == "mm") {
								return $"SharpSheets.Layouts.Dimension.FromMillimetres({number}f)";
							}
							else {
								return $"SharpSheets.Layouts.Dimension.FromRelative({number}f)";
							}
						}
					}

					return $"{parser.CallingName}({constant.ToRepr()}{(parser.NeedsSourceDirectory ? $", {FactoryGenerator.DocumentationSourceName}" : "")})";
				}
				else {
					return null;
				}
			}
			else if (constant is not null && type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericList(out ITypeSymbol? listElemType)) {
				int rank = listElemType.GetArrayOrTupleRank() + 1;
				if (rank <= FactoryGenerator.arrayDelimiters.Length && resolverData.ParserLookup.TryGetValue(resolverData.Compilation.ReduceParameterType(listElemType).ToFullDisplayString(), out ParameterParser listElemParser)) {
					return $"SharpSheets.Parsing.StringParsing.SplitOnUnescaped({constant.ToRepr()}, '{arrayDelimiters[rank - 1]}').Select(s => s.Trim()).Select(p => {listElemParser.FullTypeName}.{listElemParser.MethodName}(p{(listElemParser.NeedsSourceDirectory ? $", {FactoryGenerator.DocumentationSourceName}" : "")})).ToList()";
				}
			}

			ITypeSymbol factoryType = SymbolEqualityComparer.Default.Equals(type, resolverData.ContainerShapeInterface) ? resolverData.BoxInterface : type;
			if (factoryType is INamedTypeSymbol namedType && resolverData.FactoryLookup.TryGetValue(namedType, out FactoryToGenerate factory)) {
				AvailableBuilder? builderToUse = (constant is not null && resolverData.BuilderLookup.Values.FirstOrDefault(b => b.BuilderType == factory.Spec.FactoryType && b.ConcreteBuilderType.Minimal.EndsWith(constant)) is AvailableBuilder requestedBuilder) ? requestedBuilder : factory.DefaultBuilder;
				
				if (builderToUse is not null) {
					return $"{builderToUse.FullTypeName}.{builderToUse.MethodName}()";
				}
			}

			if (constant is null) {
				return null;
			}

			return constant.ToRepr();
			//return $"ERROR|{type.ToFullDisplayString()}|";
			//return constant.ToRepr(); // "null"; // TODO Implement
		}

		private static string? GetDocumentationValue(IParameterSymbol? symbol, string valueName, SharpSheetsParameterResolverData resolverData) {
			if (symbol is null) { return null; }

			AttributeData? propertyAttr = symbol.GetAttributes("SharpSheets.Parsing.PropertyAttribute", "SharpSheets.Parsing.LocalPropertyAttribute").FirstOrDefault();

			//if (propertyAttr is null) { return null; }

			string? argVal = (string?)propertyAttr?.GetNamedArgument(valueName)?.Value;

			return GetDocumentationValue(resolverData.Compilation.ReduceParameterType(symbol.Type), argVal, resolverData);
		}

		private static string? GetDefaultValue(IParameterSymbol? symbol, SharpSheetsParameterResolverData resolverData) {
			return GetDocumentationValue(symbol, "Default", resolverData);
		}

		private static string? GetExampleValue(IParameterSymbol? symbol, SharpSheetsParameterResolverData resolverData) {
			return GetDocumentationValue(symbol, "Example", resolverData);
		}

		private static bool GetExclude(IParameterSymbol? symbol) {
			if (symbol is null) { return false; }

			AttributeData? propertyAttr = symbol.GetAttributes("SharpSheets.Parsing.PropertyAttribute", "SharpSheets.Parsing.LocalPropertyAttribute").FirstOrDefault();

			if (propertyAttr is null) { return false; }

			TypedConstant? excludeArg = propertyAttr.GetNamedArgument("Exclude");
			return ((bool?)excludeArg?.Value) ?? false;
		}

		private static string? GetRectangle(XElement? element) {
			if (element is null) {
				return null;
			}

			string content = string.Join(" ", element.Nodes().OfType<XText>().Select(n => n.Value));

			if (string.IsNullOrWhiteSpace(content)) { return null; }

			string[] parts = content.Trim().Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

			try {
				if (parts.Length == 2) {
					float width = float.Parse(parts[0]);
					float height = float.Parse(parts[1]);

					return $"new SharpSheets.Layouts.Rectangle(0f, 0f, {width.ToCodeString()}, {height.ToCodeString()})";
				}
				else if (parts.Length == 4) {
					float x = float.Parse(parts[0]);
					float y = float.Parse(parts[1]);
					float width = float.Parse(parts[2]);
					float height = float.Parse(parts[3]);

					return $"new SharpSheets.Layouts.Rectangle({x.ToCodeString()}, {y.ToCodeString()}, {width.ToCodeString()}, {height.ToCodeString()})";
				}
				else {
					return null;
				}
			}
			catch (FormatException) {
				return null;
			}
		}

		private static string? GetSize(XElement? element) {
			if (element is null) {
				return null;
			}

			string content = string.Join(" ", element.Nodes().OfType<XText>().Select(n => n.Value));

			if (string.IsNullOrWhiteSpace(content)) { return null; }

			string[] parts = content.Trim().Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

			try {
				if (parts.Length == 2) {
					float width = float.Parse(parts[0]);
					float height = float.Parse(parts[1]);

					return $"new SharpSheets.Layouts.Size({width.ToCodeString()}, {height.ToCodeString()})";
				}
				else {
					return null;
				}
			}
			catch (FormatException) {
				return null;
			}
		}

		private static ITypeSymbol? GetTypeFromDocumentationString(string documentationString, Compilation compilation) {
			// Extract the type name from the documentation string
			string typeName = documentationString.Substring(2); // Remove the "T:" prefix

			return compilation.ResolveTypeKey(typeName);
		}

		private static string GetLineBreakSpan() {
			return "new SharpSheets.Documentation.LineBreakSpan()";
		}

		private static string GetTextSpan(string text) {
			return $"new SharpSheets.Documentation.TextSpan({text.ToRepr()})";
		}

		private static string GetParameterSpan(string parameter) {
			return $"new SharpSheets.Documentation.ParameterSpan({FactoryGenerator.NormaliseParameterName(parameter).ToRepr()})";
		}

		private static string GetEnumValueSpan(string type, string value) {
			return $"new SharpSheets.Documentation.EnumValueSpan({type.ToRepr()}, {value.ToRepr()})";
		}

		private static string GetTypeSpan(ITypeSymbol? type) {
			if(type is null) {
				return "ERROR";
			}
			return $"new SharpSheets.Documentation.TypeSpan({type.Name.ToRepr()}, typeof({type.ToFullDisplayString()}))";
		}

		private static IEnumerable<string> GetElementSpanContents(XElement? element, Compilation compilation) {
			if (element == null) {
				yield break;
			}

			bool trimStart = true;

			XNode[] nodes = element.Nodes().Where(n => n is XText || n is XElement).ToArray();

			for (int n = 0; n < nodes.Length; n++) {
				XNode node = nodes[n];

				if (node is XText text) {
					if (!string.IsNullOrEmpty(text.Value)) {
						string value = text.Value;
						if (trimStart) {
							value = value.TrimStart();
							trimStart = false;
						}
						string[] processed = Regex.Split(value, @"\s*\n\n\s*").Select(s => Regex.Replace(s, @"\s+", " ")).ToArray();
						for (int i = 0; i < processed.Length; i++) {
							if (i > 0) {
								yield return GetLineBreakSpan();
								processed[i] = processed[i].TrimStart();
							}
							if (n == nodes.Length - 1 && i == processed.Length - 1) {
								processed[i] = processed[i].TrimEnd();
							}
							yield return GetTextSpan(processed[i]);
						}
					}
				}
				else if (node is XElement elem) {
					if (elem.Name == "paramref" && elem.Attribute("name") is XAttribute nameAttr) {
						yield return GetParameterSpan(nameAttr.Value);
					}
					else if (elem.Name == "see" && elem.Attribute("cref") is XAttribute crefAttr) {
						string[] parts = crefAttr.Value.Substring(2).Split('.');
						if (crefAttr.Value.StartsWith("F:")) { // Enum value
							string type = parts[parts.Length - 2];
							string value = parts[parts.Length - 1];
							yield return GetEnumValueSpan(type, value);
						}
						else if (crefAttr.Value.StartsWith("T:")) { // Type name
							yield return GetTypeSpan(GetTypeFromDocumentationString(crefAttr.Value, compilation));
						}
					}
					else if (elem.Name == "br" || (elem.Name == "para" && elem.IsEmpty)) {
						yield return GetLineBreakSpan();
						trimStart = true;
					}
				}
			}
		}

	}

	public record class SharpSheetsParameterResolverData {

		public readonly Compilation Compilation;

		public readonly Dictionary<string, AvailableBuilder> BuilderLookup;
		public readonly Dictionary<string, ParameterParser> ParserLookup;
		public readonly Dictionary<INamedTypeSymbol, FactoryToGenerate> FactoryLookup;
		public readonly Dictionary<string, FactoryToGenerate> FactoryNameLookup;

		public readonly INamedTypeSymbol ShapeInterface;
		public readonly INamedTypeSymbol AreaShapeInterface;
		public readonly INamedTypeSymbol ContainerShapeInterface;
		public readonly INamedTypeSymbol BoxInterface;
		public readonly INamedTypeSymbol TitleStyledBoxInterface;
		public readonly INamedTypeSymbol DetailInterface;
		public readonly INamedTypeSymbol WidgetInterface;
		public readonly INamedTypeSymbol WidgetSetupType;

		private SharpSheetsParameterResolverData(Compilation compilation, Dictionary<string, AvailableBuilder> builderLookup, Dictionary<string, ParameterParser> parserLookup, Dictionary<INamedTypeSymbol, FactoryToGenerate> factoryLookup, INamedTypeSymbol shapeInterface, INamedTypeSymbol areaShapeInterface, INamedTypeSymbol containerShapeInterface, INamedTypeSymbol boxInterface, INamedTypeSymbol titleStyledBoxInterface, INamedTypeSymbol detailInterface, INamedTypeSymbol widgetInterface, INamedTypeSymbol widgetSetupType) {
			Compilation = compilation;

			BuilderLookup = builderLookup;
			ParserLookup = parserLookup;
			FactoryLookup = factoryLookup;

			FactoryNameLookup = FactoryLookup.ToDictionary(kv => kv.Key.ToFullDisplayString(), kv => kv.Value);

			ShapeInterface = shapeInterface;
			AreaShapeInterface = areaShapeInterface;
			ContainerShapeInterface = containerShapeInterface;
			BoxInterface = boxInterface;
			TitleStyledBoxInterface = titleStyledBoxInterface;
			DetailInterface = detailInterface;
			WidgetInterface = widgetInterface;
			WidgetSetupType = widgetSetupType;
		}

		public FactoryToGenerate? GetFactory(INamedTypeSymbol? concreteType) {
			if (concreteType is null) { return null; }

			if (FactoryLookup.TryGetValue(concreteType, out FactoryToGenerate exactMatch)) {
				return exactMatch;
			}

			(INamedTypeSymbol type, FactoryToGenerate factory)? result = null;

			foreach (KeyValuePair<INamedTypeSymbol, FactoryToGenerate> factory in FactoryLookup) {
				if (Compilation.HasImplicitConversion(concreteType, factory.Key)) {
					if (result is null) {
						result = (factory.Key, factory.Value);
					}
					else if (result is not null && Compilation.HasImplicitConversion(factory.Key, result.Value.type)) {
						result = (factory.Key, factory.Value);
					}
				}
			}

			return result?.factory;
		}

		public static SharpSheetsParameterResolverData Create(Compilation compilation, IEnumerable<AvailableBuilder> availableBuilders, IEnumerable<ParameterParser> availableParsers, IEnumerable<FactoryToGenerate> factories) {
			Dictionary<string, AvailableBuilder> builderLookup = availableBuilders.ToDictionary(b => b.ConcreteBuilderType.FullName);
			Dictionary<string, ParameterParser> parserLookup = availableParsers.ToDictionary(b => b.ParserType.FullName);
			Dictionary<INamedTypeSymbol, FactoryToGenerate> factoryLookup = factories.ToDictionary<FactoryToGenerate, INamedTypeSymbol>(f => f.Spec.FactoryType.GetSymbol(compilation) as INamedTypeSymbol ?? throw new InvalidOperationException($"Cannot resolve {f.Spec.FactoryType.FullName} symbol."), SymbolEqualityComparer.Default);

			INamedTypeSymbol shapeInterface = compilation.GetTypeByMetadataName("SharpSheets.Shapes.IShape") ?? throw new InvalidOperationException("No IShape type.");
			INamedTypeSymbol areaShapeInterface = compilation.GetTypeByMetadataName("SharpSheets.Shapes.IAreaShape") ?? throw new InvalidOperationException("No IAreaShape type.");
			INamedTypeSymbol containerShapeInterface = compilation.GetTypeByMetadataName("SharpSheets.Shapes.IContainerShape") ?? throw new InvalidOperationException("No IAreaShape type.");
			INamedTypeSymbol boxInterface = compilation.GetTypeByMetadataName("SharpSheets.Shapes.IBox") ?? throw new InvalidOperationException("No IBox type.");
			INamedTypeSymbol titleStyledBoxInterface = compilation.GetTypeByMetadataName("SharpSheets.Shapes.ITitleStyledBox") ?? throw new InvalidOperationException("No IContainerShape type.");
			INamedTypeSymbol detailInterface = compilation.GetTypeByMetadataName("SharpSheets.Shapes.IDetail") ?? throw new InvalidOperationException("No IDetail type.");
			
			INamedTypeSymbol widgetInterface = compilation.GetTypeByMetadataName("SharpSheets.Widgets.IWidget") ?? throw new InvalidOperationException("No WidgetSetup type.");
			INamedTypeSymbol widgetSetupType = compilation.GetTypeByMetadataName("SharpSheets.Widgets.WidgetSetup") ?? throw new InvalidOperationException("No WidgetSetup type.");

			return new SharpSheetsParameterResolverData(compilation, builderLookup, parserLookup, factoryLookup, shapeInterface, areaShapeInterface, containerShapeInterface, boxInterface, titleStyledBoxInterface, detailInterface, widgetInterface, widgetSetupType);
		}
	}

	public record class SharpSheetsParameterData {

		public readonly string Name;
		public readonly string? Description; // Constructor for DocumentationString
		public readonly string ArgumentType; // Constructor/Builder for ArgumentType
		public readonly bool IsOptional;
		public readonly bool UseLocal;
		public readonly string? DefaultValue; // Construction for correct object type
		public readonly string? ExampleValue; // Construction for correct object type
		public readonly string? Implied;
		public readonly (string prefix, string? separator)[] Prefixes; // Sequence of prefixes to use with .Prefixed("prefix") calls

		public readonly string? DeferArgsTo;
		public readonly int SkipDeferredArgs;

		private SharpSheetsParameterData(string name, string? description, string argumentType, bool isOptional, bool useLocal, string? defaultValue, string? exampleValue, string? implied, (string prefix, string? sep)[]? prefixes, string? deferArgsTo, int skipDeferredArgs) {
			Name = name;
			Description = description;
			ArgumentType = argumentType;
			IsOptional = isOptional;
			UseLocal = useLocal;
			DefaultValue = defaultValue;
			ExampleValue = exampleValue;
			Implied = implied;
			Prefixes = prefixes ?? Array.Empty<(string, string?)>();
			DeferArgsTo = deferArgsTo;
			SkipDeferredArgs = skipDeferredArgs;
		}

		public SharpSheetsParameterData(string name, string? description, string argumentType, bool isOptional, bool useLocal, string? defaultValue, string? exampleValue, string? implied)
			: this(name, description, argumentType, isOptional, useLocal, defaultValue, exampleValue, implied, null, null, 0) { }

		public SharpSheetsParameterData Prefixed(string prefix, string? sep) {
			return new SharpSheetsParameterData(
					Name, Description,
					ArgumentType,
					IsOptional, UseLocal,
					DefaultValue, ExampleValue,
					Implied, Prefixes.Concat(new (string, string?)[] { (prefix, sep) }).ToArray(),
					DeferArgsTo, SkipDeferredArgs
				);
		}

		public static SharpSheetsParameterData MakeDeferredArgs(TypeData deferArgsTo, int skip, bool useLocal, (string prefix, string? sep)? prefix) {
			SharpSheetsParameterData result = new SharpSheetsParameterData(
					"deferred", null,
					deferArgsTo.FullName,
					false, useLocal,
					null, null,
					null, null,
					deferArgsTo.FullName, skip
				);
			if (prefix.HasValue && !string.IsNullOrEmpty(prefix.Value.prefix)) {
				result = result.Prefixed(prefix.Value.prefix, prefix.Value.sep);
			}
			return result;
		}
	}

	public static class SharpSheetsParameterResolver {

		public static IEnumerable<SharpSheetsParameterData> GetArguments(AvailableBuilder builder, IMethodSymbol method, BuilderComment? builderDoc, SharpSheetsParameterResolverData resolverData, string? prefix = null) {
			Compilation compilation = resolverData.Compilation;

			INamedTypeSymbol? builderTypeSymbol = builder.BuilderType.GetSymbol(compilation) as INamedTypeSymbol; // compilation.ResolveTypeKey(builder.BuilderType) as INamedTypeSymbol;
			FactoryToGenerate? builderFactory = resolverData.GetFactory(builderTypeSymbol);

			Queue<SharpSheetsParameterData> deferredParams = new Queue<SharpSheetsParameterData>();

			int paramIdx = 0; // For excluding buildErrors
			foreach ((BuilderParameter builderParam, IParameterSymbol paramSymbol) in builder.Parameters.Zip(method.Parameters)) {
				if (builderParam.IsBuildErrors) { continue; }

				bool exclude = builderParam.Exclude || (paramIdx < (builderFactory?.Spec.RequiredParameters.Count ?? 0) && (builderFactory?.Spec.RequiredParameters[paramIdx].Exclude ?? false));
				paramIdx++;

				if (exclude) { continue; }

				ParamComment? paramDoc = builderDoc?.Params.TryGetValue(paramSymbol.Name, out ParamComment pd) ?? false ? pd : null;
				string parameterName = NormaliseParameterName(paramSymbol.Name);
				bool useLocal = builderParam.IsLocal;

				if (compilation.HasImplicitConversion(paramSymbol.Type, resolverData.AreaShapeInterface)) {
					bool nameGiven = builder.Parameters.Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, "name"));
					foreach (SharpSheetsParameterData shapeArg in GetAreaShapeArguments(parameterName, prefix, builderParam.Type.Minimal, paramDoc, builderParam.IsOptional, useLocal, !nameGiven, resolverData)) {
						yield return shapeArg;
					}
				}
				else if (compilation.HasImplicitConversion(paramSymbol.Type, resolverData.DetailInterface)) {
					foreach (SharpSheetsParameterData detailArg in GetDetailArguments(parameterName, prefix, builderParam.Type.Minimal, paramDoc, builderParam.IsOptional, useLocal, resolverData)) {
						yield return detailArg;
					}
				}
				else if (resolverData.BuilderLookup.TryGetValue(builderParam.Type.Minimal, out AvailableBuilder nestedBuilder)) { // (typeof(ISharpArgsGrouping).IsAssignableFrom(param.ParameterType) || SharpFactory.IsParsableStruct(param.ParameterType)) {

					ITypeSymbol? nestedBuilderType = nestedBuilder.ConcreteBuilderType.GetSymbol(compilation); // compilation.ResolveTypeKey(nestedBuilder.ConcreteBuilderType);
					if (nestedBuilderType is null) {
						continue;
					}
					IMethodSymbol? nestedBuilderMethod = compilation.ResolveMethodSymbol(nestedBuilderType, nestedBuilder.MethodName).FirstOrDefault(m => m.Parameters.Length == nestedBuilder.Parameters.Count);
					if (nestedBuilderMethod is null) {
						continue;
					}

					BuilderComment? nestedBuilderDoc = DocCommentReader.FromSymbol(nestedBuilderMethod, resolverData.BuilderLookup, resolverData.ParserLookup, resolverData);

					string nestedPrefix = (!string.IsNullOrEmpty(prefix) ? prefix + "." : "") + parameterName;
					(string, string?)? expandedPrefix = nestedBuilder.PrefixSep is not null ? (nestedPrefix, nestedBuilder.PrefixSep) : (prefix is not null ? (prefix, null) : null);

					if (nestedBuilder.Structure == BuilderStructure.GROUPED) {
						yield return SharpSheetsParameterData.MakeDeferredArgs(nestedBuilder.ConcreteBuilderType, 0, useLocal, (nestedPrefix, null));
					}
					else if (nestedBuilder.Structure == BuilderStructure.SUPPLEMENTED) {
						ParamComment? firstArgDoc = (nestedBuilderDoc?.Params.TryGetValue(nestedBuilder.Parameters[0].Name, out ParamComment dpc) ?? false) ? dpc : null;
						yield return GetSingleArg(nestedBuilder.Parameters[0], parameterName, prefix, useLocal, paramDoc?.Description, firstArgDoc);
						yield return SharpSheetsParameterData.MakeDeferredArgs(nestedBuilder.ConcreteBuilderType, 1, useLocal, (nestedPrefix, null));
					}
					else if (nestedBuilder.Structure == BuilderStructure.EXPANDED) {
						yield return SharpSheetsParameterData.MakeDeferredArgs(nestedBuilder.ConcreteBuilderType, 0, useLocal, expandedPrefix);
					}
					else if (nestedBuilder.Structure == BuilderStructure.EXPANDED_DEFERRED) {
						deferredParams.Enqueue(SharpSheetsParameterData.MakeDeferredArgs(nestedBuilder.ConcreteBuilderType, 0, useLocal, expandedPrefix)); // Save these until last
					}
				}
				else {
					yield return GetSingleArg(builderParam, parameterName, prefix, useLocal, paramDoc?.Description, paramDoc);
				}
			}

			while (deferredParams.Count > 0) {
				yield return deferredParams.Dequeue();
			}
		}

		private static SharpSheetsParameterData GetSingleArg(BuilderParameter param, string parameterName, string? prefix, bool useLocal, string? descriptionContent, ParamComment? argDoc) {
			string name = (!string.IsNullOrEmpty(prefix) ? prefix + "." : "") + parameterName;
			string? defaultValue = argDoc?.DefaultValue ?? param.DefaultValue;
			string? exampleValue = argDoc?.ExampleValue;
			return new SharpSheetsParameterData(name, descriptionContent, ArgumentTypeSimple(param.Type.CompilerFullName), param.IsOptional, useLocal, defaultValue, exampleValue, null);
		}

		public static IEnumerable<SharpSheetsParameterData> GetAreaShapeArguments(string parameterName, string? prefix, string argumentType, ParamComment? argDoc, bool isOptional, bool useLocal, bool includeNameArg, SharpSheetsParameterResolverData resolverData) {
			string name = (!string.IsNullOrEmpty(prefix) ? prefix + "." : "") + parameterName;

			yield return new SharpSheetsParameterData(name, argDoc?.Description, ArgumentTypeSimple(argumentType), isOptional, useLocal, argDoc?.DefaultValue, argDoc?.ExampleValue, "style");

			if (argumentType == "SharpSheets.Shapes.IContainerShape") {
				if (includeNameArg) {
					yield return new SharpSheetsParameterData("name", DocCommentReader.MakeDocumentationStringFromText("Text to use for shape titles."), ArgumentTypeSimple("string"), true, true, "NAME".ToRepr(), "NAME".ToRepr(), null);
				}

				AvailableBuilder? defaultTitleStyleBuilder = resolverData.FactoryNameLookup["SharpSheets.Shapes.ITitleStyledBox"].DefaultBuilder;
				string titleStyleDefaultValue = defaultTitleStyleBuilder?.Name.ToRepr() ?? "ERROR";
				//string exampleTitleStyle = defaultTitleStyleBuilder is not null ? $"{defaultTitleStyleBuilder.FullTypeName}.{defaultTitleStyleBuilder.MethodName}()" : "ERROR";
				// Very much not a fan, but this should really be fixed by re-working the title style approach
				string exampleTitleStyle = "(SharpSheets.Shapes.ITitleStyledBox)SharpSheets.Shapes.ShapeFactory.GetDefaultShape(typeof(SharpSheets.Shapes.ITitleStyledBox))!";

				yield return new SharpSheetsParameterData("title", DocCommentReader.MakeDocumentationStringFromText($"Title style to be used with {name} if a name is provided."), ArgumentTypeSimple("SharpSheets.Shapes.ITitleStyledBox"), true, false, titleStyleDefaultValue, exampleTitleStyle, "style").Prefixed(name, null);
			}
		}

		public static IEnumerable<SharpSheetsParameterData> GetDetailArguments(string parameterName, string? prefix, string argumentType, ParamComment? argDoc, bool isOptional, bool useLocal, SharpSheetsParameterResolverData resolverData) {
			string name = (!string.IsNullOrEmpty(prefix) ? prefix + "." : "") + parameterName;

			//AvailableBuilder? defaultDetailBuilder = resolverData.FactoryNameLookup["SharpSheets.Shapes.IDetail"].DefaultBuilder;
			//string styleDefaultValue = defaultDetailBuilder?.Name.ToRepr() ?? "ERROR";
			//string exampleDetail = defaultDetailBuilder is not null ? $"{defaultDetailBuilder.FullTypeName}.{defaultDetailBuilder.MethodName}()" : "ERROR";

			string? styleDefaultValue = argDoc?.DefaultValue;
			string? exampleDetail = argDoc?.ExampleValue;

			yield return new SharpSheetsParameterData(name, argDoc?.Description, ArgumentTypeSimple(argumentType), isOptional, useLocal, styleDefaultValue, exampleDetail, "style");
		}

		public static string ArgumentTypeSimple(string typeName) {
			return $"SharpSheets.Documentation.ArgumentType.Simple(typeof({typeName}))";
		}

	}

}