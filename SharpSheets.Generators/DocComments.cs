using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharpSheets.Generators.Utilities;
using SharpSheets.Generators.Utilities.DataStructures;
using static SharpSheets.Generators.FactoryGenerator;
using static SharpSheets.Generators.SharpSheetsConstants;

namespace SharpSheets.Generators {

	public record class ParamValue {
		/// <summary> Construction code for example value. </summary>
		public string Value { get; }
		/// <summary> Indicates that this value can definitely be constructed as the correct type. </summary>
		public bool IsValid { get; }

		public ParamValue(string value, bool isValid) {
			Value = value;
			IsValid = isValid;
		}
	}

	public record class ParamComment {
		public readonly string Name;
		/// <summary> DocumentationString constructor code. </summary>
		public readonly string? Description;
		/// <summary>
		/// Construction code for default value.
		/// </summary>
		public readonly ParamValue? DefaultValue;
		/// <summary>
		/// Construction code for example value.
		/// </summary>
		public readonly ParamValue? ExampleValue;
		public readonly bool Exclude;

		public ParamComment(string name, string? description, ParamValue? defaultValue, ParamValue? exampleValue, bool exclude) {
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

		/// <summary> Construction code for example value. </summary>
		public string? Example { get; }

		public BuilderComment(
				string? summary,
				IEnumerable<ParamComment> @params,
				string? returns,
				string? remarks,
				string? size,
				string? canvas,
				string? example
			) {

			Summary = summary;
			Params = new EquatableDictionary<string, ParamComment>(@params.ToDictionary(p => p.Name));
			Returns = returns;
			Remarks = remarks;
			Size = size;
			Canvas = canvas;
			Example = example;
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

		public static BuilderComment FromSymbol(IMethodSymbol symbol, SharpSheetsParameterResolverData resolverData, CancellationToken ct = default) {

			string? xml = symbol.GetDocumentationCommentXml(preferredCulture: null, expandIncludes: true, cancellationToken: ct);

			XDocument? doc;
			try {
				doc = !string.IsNullOrWhiteSpace(xml) ? XDocument.Parse(xml) : null;
			}
			catch (System.Xml.XmlException) {
				// Malformed XML
				doc = null;
			}

			string? summary = GetDocumentationString(doc?.Root.Element("summary"), resolverData.Compilation);
			string? returns = GetDocumentationString(doc?.Root.Element("returns"), resolverData.Compilation);
			string? remarks = GetDocumentationString(doc?.Root.Element("remarks"), resolverData.Compilation);
			string? size = GetRectangle(doc?.Root.Element("size"));
			string? canvas = GetSize(doc?.Root.Element("canvas"));

			Dictionary<string, XElement> paramElemLookup = new Dictionary<string, XElement>();
			foreach (XElement paramElem in (doc?.Root.Elements("param") ?? Enumerable.Empty<XElement>())) {
				XAttribute nameAttr = paramElem.Attribute("name");
				if (nameAttr != null) {
					string name = nameAttr.Value;
					paramElemLookup.Add(name, paramElem);
				}
			}

			List<ParamComment> paramDocs = new List<ParamComment>();
			for (int i = 0; i < symbol.Parameters.Length; i++) {
				IParameterSymbol paramSymbol = symbol.Parameters[i];
				XElement? paramElem = paramElemLookup.TryGetValue(paramSymbol.Name, out XElement val) ? val : null;
				string? text = GetDocumentationString(paramElem, resolverData.Compilation);
				paramDocs.Add(new ParamComment(paramSymbol.Name, text, GetDefaultValue(paramSymbol, resolverData), GetExampleValue(paramSymbol, resolverData), GetExclude(paramSymbol)));
			}

			string? example = GetBuilderExample(symbol, paramDocs);

			return new BuilderComment(summary, paramDocs, returns, remarks, size, canvas, example);
		}

		public static SummaryComment FromSymbol(ISymbol symbol, Compilation compilation, CancellationToken ct = default) {

			string? xml = symbol.GetDocumentationCommentXml(preferredCulture: null, expandIncludes: true, cancellationToken: ct);

			XDocument? doc;
			try {
				doc = !string.IsNullOrWhiteSpace(xml) ? XDocument.Parse(xml) : null;
			}
			catch (System.Xml.XmlException) {
				// Malformed XML
				doc = null;
			}

			string? summary = GetDocumentationString(doc?.Root.Element("summary"), compilation);

			return new SummaryComment(summary);
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
				SummaryComment enumValComment = FromSymbol(enumField, compilation);

				values.Add((enumField.Name, enumValComment.Summary));
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

		private static string? GetBuilderExample(IMethodSymbol symbol, List<ParamComment> paramDocs) {
			Dictionary<string, ParamComment> paramComments = paramDocs.ToDictionary(c => c.Name);

			List<string> args = new List<string>();

			static string EscapeParamName(IParameterSymbol paramSymbol) {
				string name = paramSymbol.Name;
				bool needsEscape = SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None;
				return needsEscape ? $"@{name}" : name;
			}

			static string? GetParamExampleValue(IParameterSymbol paramSymbol, ParamComment comment) {
				if (comment.ExampleValue is not null && comment.ExampleValue.IsValid) { return comment.ExampleValue.Value; }
				else if (comment.DefaultValue is not null && comment.DefaultValue.IsValid) { return comment.DefaultValue.Value; }
				else { return null; }
			}

			foreach(IParameterSymbol param in symbol.Parameters) {
				if (paramComments.TryGetValue(param.Name, out ParamComment? comment) && GetParamExampleValue(param, comment) is string exampleValue) {
					args.Add($"{EscapeParamName(param)}: {exampleValue}");
				}
				else if (!param.IsOptional) {
					return null;
				}
			}

			return $"{symbol.ContainingType.ToFullDisplayString()}.{symbol.ToFullDisplayString()}({string.Join(", ", args)})";
		}

		private static readonly Regex documentationValueConstructorRegex = new Regex(@"
				^
				(?<type>[a-z_][a-z0-9_]*)
				\s*
				(
					\(
						(?<args>.*)
					\)
					\s*
				)?
				$
			", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

		private static ParamValue? GetDocumentationValue(ITypeSymbol type, string? constant, SharpSheetsParameterResolverData resolverData) {
			if (resolverData.ParserLookup.TryGetValue(TypeData.Create(type), out ParameterParser parser)) {
				if (constant is not null) {

					if (parser.ParserType.SpecialType != SpecialType.None) {
						if (parser.ParserType.SpecialType == SpecialType.System_Single) {
							try {
								return new ParamValue($"{float.Parse(constant, CultureInfo.InvariantCulture)}f", true);
							}
							catch (FormatException) { }
						}
						else if (parser.ParserType.SpecialType == SpecialType.System_Int32) {
							try {
								return new ParamValue($"{int.Parse(constant, CultureInfo.InvariantCulture)}", true);
							}
							catch (FormatException) { }
						}
						else if (parser.ParserType.SpecialType == SpecialType.System_UInt32) {
							try {
								return new ParamValue($"{uint.Parse(constant, CultureInfo.InvariantCulture)}U", true);
							}
							catch (SystemException) { }
						}
						else if (parser.ParserType.SpecialType == SpecialType.System_Boolean) {
							try {
								return new ParamValue(bool.Parse(constant) ? "true" : "false", true);
							}
							catch (SystemException) { }
						}
						else if (parser.ParserType.SpecialType == SpecialType.System_String) {
							return new ParamValue(constant.ToRepr(), true);
						}
					}
					else if (parser.ParserType.FullName == UFloat) {
						try {
							return new ParamValue($"new SharpSheets.Utilities.UFloat({float.Parse(constant, CultureInfo.InvariantCulture)}f)", true);
						}
						catch (FormatException) { }
					}
					else if (parser.ParserType.FullName == UnitInterval) {
						try {
							return new ParamValue($"new SharpSheets.Utilities.UnitInterval({float.Parse(constant, CultureInfo.InvariantCulture)}f)", true);
						}
						catch (FormatException) { }
					}
					else if (parser.ParserType.FullName == Margins) {
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
								return new ParamValue($"new SharpSheets.Layouts.Margins({string.Join(", ", values.Select(v => $"{v}f"))})", true);
							}
						}
					}
					else if (parser.ParserType.FullName == Dimension) {
						if (string.Equals(constant, "auto", StringComparison.InvariantCultureIgnoreCase)) {
							return new ParamValue(Dimension_Automatic, true);
						}

						Regex dimensionRegex = new Regex(@"^(?<number>[\+\-]?[0-9]+\.[0-9]+|\.[0-9]+|[\+\-]?[0-9]+\.?)\s*(?<unit>pt|in|cm|mm|pc|\%)?$", RegexOptions.IgnoreCase);

						Match match = dimensionRegex.Match(constant.Trim());
						if (match.Success) {
							float number = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
							string unit = match.Groups[2].Value.ToLowerInvariant();
							if (unit == "pc" || unit == "%") {
								return new ParamValue(Dimension_FromPercent(number), true);
							}
							else if (unit == "pt") {
								return new ParamValue(Dimension_FromPoints(number), true);
							}
							else if (unit == "in") {
								return new ParamValue(Dimension_FromInches(number), true);
							}
							else if (unit == "cm") {
								return new ParamValue(Dimension_FromCentimetres(number), true);
							}
							else if (unit == "mm") {
								return new ParamValue(Dimension_FromMillimetres(number), true);
							}
							else {
								return new ParamValue(Dimension_FromRelative(number), true);
							}
						}
					}

					return new ParamValue($"{parser.CallingName}({constant.ToRepr()}{(parser.NeedsSourceDirectory ? $", {FactoryGenerator.DocumentationSourceName}" : "")})", true);
				}
				else {
					return null;
				}
			}
			else if (constant is not null && type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericList(out ITypeSymbol? listElemType)) {
				int rank = listElemType.GetArrayOrTupleRank() + 1;
				if (rank <= FactoryGenerator.arrayDelimiters.Length && resolverData.ParserLookup.TryGetValue(TypeData.Create(resolverData.Compilation.ReduceParameterType(listElemType)), out ParameterParser listElemParser)) {
					return new ParamValue($"SharpSheets.Parsing.StringParsing.SplitOnUnescaped({constant.ToRepr()}, '{arrayDelimiters[rank - 1]}').Select(s => s.Trim()).Select(p => {listElemParser.FullTypeName}.{listElemParser.MethodName}(p{(listElemParser.NeedsSourceDirectory ? $", {FactoryGenerator.DocumentationSourceName}" : "")})).ToList()", true);
				}
			}

			if (type is INamedTypeSymbol namedType && resolverData.FactoryLookup.TryGetValue(namedType, out FactoryToGenerate factory)) {
				Match? constructorMatch = constant is not null ? documentationValueConstructorRegex.Match(constant) : null;
				bool hasConstructorMatch = constructorMatch?.Success ?? false;
				string? constructorName = hasConstructorMatch ? constructorMatch!.Groups["type"].Value : constant;
				string constructorArgs = hasConstructorMatch ? (constructorMatch!.Groups["args"].Value ?? "").Trim() : "";

				AvailableBuilder? builderToUse = (constructorName is not null && resolverData.BuilderLookup.Values.FirstOrDefault(b => b.BuilderType == factory.Spec.FactoryType && b.ConcreteBuilderType.Minimal.EndsWith(constructorName)) is AvailableBuilder requestedBuilder) ? requestedBuilder : factory.DefaultBuilder;

				if (builderToUse is not null && (!string.IsNullOrWhiteSpace(constructorArgs) || builderToUse.Parameters.All(p => p.IsOptional))) {
					return new ParamValue($"{builderToUse.FullTypeName}.{builderToUse.MethodName}({constructorArgs})", true);
				}
			}

			if (constant is null) {
				return null;
			}

			return new ParamValue(constant.ToRepr(), false);
		}

		private static ParamValue? GetDocumentationValue(IParameterSymbol? symbol, string valueName, SharpSheetsParameterResolverData resolverData) {
			if (symbol is null) { return null; }

			AttributeData? propertyAttr = symbol.GetAttributes(PropertyAttribute, LocalPropertyAttribute).FirstOrDefault();

			//if (propertyAttr is null) { return null; }

			string? argVal = (string?)propertyAttr?.GetNamedArgument(valueName)?.Value;

			return GetDocumentationValue(resolverData.Compilation.ReduceParameterType(symbol.Type), argVal, resolverData);
		}

		private static ParamValue? GetDefaultValue(IParameterSymbol? symbol, SharpSheetsParameterResolverData resolverData) {
			return GetDocumentationValue(symbol, "Default", resolverData);
		}

		private static ParamValue? GetExampleValue(IParameterSymbol? symbol, SharpSheetsParameterResolverData resolverData) {
			return GetDocumentationValue(symbol, "Example", resolverData);
		}

		private static bool GetExclude(IParameterSymbol? symbol) {
			if (symbol is null) { return false; }

			AttributeData? propertyAttr = symbol.GetAttributes(PropertyAttribute, LocalPropertyAttribute).FirstOrDefault();

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

		public readonly TypeLookup<AvailableBuilder> BuilderLookup;
		public readonly TypeLookup<ParameterParser> ParserLookup;
		public readonly Dictionary<INamedTypeSymbol, FactoryToGenerate> FactoryLookup;
		public readonly Dictionary<string, FactoryToGenerate> FactoryNameLookup;

		public readonly INamedTypeSymbol ShapeInterface;
		public readonly INamedTypeSymbol AreaShapeInterface;
		public readonly INamedTypeSymbol BoxInterface;
		public readonly INamedTypeSymbol TitleStyleInterface;
		public readonly INamedTypeSymbol DetailInterface;
		public readonly INamedTypeSymbol WidgetInterface;
		public readonly INamedTypeSymbol WidgetSetupType;

		private SharpSheetsParameterResolverData(Compilation compilation, TypeLookup<AvailableBuilder> builderLookup, TypeLookup<ParameterParser> parserLookup, Dictionary<INamedTypeSymbol, FactoryToGenerate> factoryLookup, INamedTypeSymbol shapeInterface, INamedTypeSymbol areaShapeInterface, INamedTypeSymbol boxInterface, INamedTypeSymbol titleStyleInterface, INamedTypeSymbol detailInterface, INamedTypeSymbol widgetInterface, INamedTypeSymbol widgetSetupType) {
			Compilation = compilation;

			BuilderLookup = builderLookup;
			ParserLookup = parserLookup;
			FactoryLookup = factoryLookup;

			FactoryNameLookup = FactoryLookup.ToDictionary(kv => kv.Key.ToFullDisplayString(), kv => kv.Value);

			ShapeInterface = shapeInterface;
			AreaShapeInterface = areaShapeInterface;
			BoxInterface = boxInterface;
			TitleStyleInterface = titleStyleInterface;
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
			TypeLookup<AvailableBuilder> builderLookup = new TypeLookup<AvailableBuilder>(availableBuilders.Select(b => (b.ConcreteBuilderType, b))); //.ToDictionary(b => b.ConcreteBuilderType.FullName);
			TypeLookup<ParameterParser> parserLookup = new TypeLookup<ParameterParser>(availableParsers.Select(p => (p.ParserType, p))); //.ToDictionary(b => b.ParserType.FullName);
			Dictionary<INamedTypeSymbol, FactoryToGenerate> factoryLookup = factories.ToDictionary<FactoryToGenerate, INamedTypeSymbol>(f => f.Spec.FactoryType.GetSymbol(compilation) as INamedTypeSymbol ?? throw new InvalidOperationException($"Cannot resolve {f.Spec.FactoryType.FullName} symbol."), SymbolEqualityComparer.Default);

			INamedTypeSymbol shapeInterface = compilation.GetTypeByMetadataName(IShape) ?? throw new InvalidOperationException("No IShape type.");
			INamedTypeSymbol areaShapeInterface = compilation.GetTypeByMetadataName(IAreaShape) ?? throw new InvalidOperationException("No IAreaShape type.");
			//INamedTypeSymbol containerShapeInterface = compilation.GetTypeByMetadataName(IContainerShape) ?? throw new InvalidOperationException("No IContainerShape type.");
			INamedTypeSymbol boxInterface = compilation.GetTypeByMetadataName(IBox) ?? throw new InvalidOperationException("No IBox type.");
			INamedTypeSymbol titleStyleInterface = compilation.GetTypeByMetadataName(ITitleStyle) ?? throw new InvalidOperationException("No ITitleStyledBox type.");
			INamedTypeSymbol detailInterface = compilation.GetTypeByMetadataName(IDetail) ?? throw new InvalidOperationException("No IDetail type.");
			
			INamedTypeSymbol widgetInterface = compilation.GetTypeByMetadataName(IWidget) ?? throw new InvalidOperationException("No WidgetSetup type.");
			INamedTypeSymbol widgetSetupType = compilation.GetTypeByMetadataName(WidgetSetup) ?? throw new InvalidOperationException("No WidgetSetup type.");

			return new SharpSheetsParameterResolverData(compilation, builderLookup, parserLookup, factoryLookup, shapeInterface, areaShapeInterface, boxInterface, titleStyleInterface, detailInterface, widgetInterface, widgetSetupType);
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

				if (compilation.HasImplicitConversion(paramSymbol.Type, resolverData.ShapeInterface)) {
					foreach (SharpSheetsParameterData shapeArg in GetShapeArguments(parameterName, prefix, builderParam.Type.Minimal, paramDoc, builderParam.IsOptional, useLocal, resolverData)) {
						yield return shapeArg;
					}
				}
				else if (resolverData.BuilderLookup.TryGetValue(builderParam.Type, out AvailableBuilder nestedBuilder)) { // (typeof(ISharpArgsGrouping).IsAssignableFrom(param.ParameterType) || SharpFactory.IsParsableStruct(param.ParameterType)) {

					IMethodSymbol? nestedBuilderMethod = compilation.ResolveMethodSymbol(nestedBuilder.FullTypeName, nestedBuilder.MethodName).FirstOrDefault(m => m.Parameters.Length == nestedBuilder.Parameters.Count);
					if (nestedBuilderMethod is null) {
						continue;
					}

					BuilderComment? nestedBuilderDoc = DocCommentReader.FromSymbol(nestedBuilderMethod, resolverData);

					string nestedPrefix = (!string.IsNullOrEmpty(prefix) ? prefix + "." : "") + parameterName;
					(string, string?)? expandedPrefix = nestedBuilder.PrefixSep is not null ? (nestedPrefix, nestedBuilder.PrefixSep) : (prefix is not null ? (prefix, null) : null);

					if (nestedBuilder.Structure == BuilderStructure.GROUPED) {
						yield return SharpSheetsParameterData.MakeDeferredArgs(nestedBuilder.ConcreteBuilderType, 0, useLocal, (nestedPrefix, null));
					}
					else if (nestedBuilder.Structure == BuilderStructure.SUPPLEMENTED) {
						BuilderParameter firstNormal = nestedBuilder.Parameters.First(p => p.ParameterType == BuilderParameterType.Normal);
						ParamComment? firstArgDoc = (nestedBuilderDoc?.Params.TryGetValue(firstNormal.Name, out ParamComment dpc) ?? false) ? dpc : null;
						yield return GetSingleArg(firstNormal, parameterName, prefix, useLocal, paramDoc?.Description, firstArgDoc);
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
			string? defaultValue = argDoc?.DefaultValue?.Value ?? param.DefaultValue;
			string? exampleValue = argDoc?.ExampleValue?.Value;
			return new SharpSheetsParameterData(name, descriptionContent, GetArgumentType(param.Type.CompilerFullName), param.IsOptional, useLocal, defaultValue, exampleValue, null);
		}

		public static IEnumerable<SharpSheetsParameterData> GetShapeArguments(string parameterName, string? prefix, string argumentType, ParamComment? argDoc, bool isOptional, bool useLocal, SharpSheetsParameterResolverData resolverData) {
			string name = (!string.IsNullOrEmpty(prefix) ? prefix + "." : "") + parameterName;

			yield return new SharpSheetsParameterData(name, argDoc?.Description, ArgumentType_Simple(argumentType), isOptional, useLocal, argDoc?.DefaultValue?.Value, argDoc?.ExampleValue?.Value, "style");
		}

		private static readonly Regex listRegex = new Regex(@"^System\.Collections\.Generic\.List<(?<elemType>.+)>$");
		private static readonly Regex numberedRegex = new Regex(@"^SharpSheets\.Parsing\.Numbered<(?<elemType>.+)>$");
		private static string GetArgumentType(string typeName) {
			if (listRegex.Match(typeName) is Match listMatch && listMatch.Success) {
				string elemType = listMatch.Groups[1].Value;
				return ArgumentType_Structured(typeName, elemType, "Entried");
			}
			else if (numberedRegex.Match(typeName) is Match numberedMatch && numberedMatch.Success) {
				string elemType = numberedMatch.Groups[1].Value;
				return ArgumentType_Structured(typeName, elemType, "Numbered");
			}
			else {
				return ArgumentType_Simple(typeName);
			}
		}

	}

}