using SharpSheets.Markup.Parsing;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharpSheets.Documentation {

	public class ArgumentDoc {
		public readonly string name;
		public readonly DocumentationString? description;
		public readonly string? defaultValue;
		public readonly string? exampleValue;
		public readonly bool exclude;

		public ArgumentDoc(string name, DocumentationString? description, string? defaultValue, string? exampleValue, bool exclude) {
			this.name = name;
			this.description = description;
			this.defaultValue = defaultValue;
			this.exampleValue = exampleValue;
			this.exclude = exclude;
		}
	}

	public class BuilderDoc {
		public readonly string builderType;
		public readonly ArgumentDoc[] arguments;
		public readonly DocumentationString? description;
		public readonly string? size;
		public readonly string? canvas;

		private readonly Dictionary<string, ArgumentDoc> argDict;

		public BuilderDoc(string builderType, ArgumentDoc[] arguments, DocumentationString? description, string? size, string? canvas) {
			this.builderType = builderType;
			this.arguments = arguments;
			this.description = description;
			this.size = size;
			this.canvas = canvas;

			argDict = arguments.ToDictionary(p => p.name);
		}

		public ArgumentDoc? GetArgument(string argName) {
			return argDict.GetValueOrFallback(argName, null);
		}
	}

	public class TypeDoc {
		public readonly string type;
		public readonly DocumentationString? description;

		public TypeDoc(string type, DocumentationString? description) {
			this.type = type;
			this.description = description;
		}
	}

	public class EnumValDoc {
		public readonly string type;
		public readonly string name;
		public readonly DocumentationString? description;

		public EnumValDoc(string type, string name, DocumentationString? description) {
			this.type = type;
			this.name = name;
			this.description = description;
		}
	}

	public class EnumDoc {
		public readonly string type;
		public readonly EnumValDoc[] values;
		public readonly DocumentationString? description;

		public EnumDoc(string type, EnumValDoc[] values, DocumentationString? description) {
			this.type = type;
			this.values = values;
			this.description = description;
		}
	}

	public static class SharpDocumentation {

		private static readonly HashSet<Assembly> loadedAssemblies = new HashSet<Assembly>();

		private static readonly Dictionary<string, TypeDoc> typeSummaries = new Dictionary<string, TypeDoc>();
		private static readonly Dictionary<string, List<BuilderDoc>> typeBuilders = new Dictionary<string, List<BuilderDoc>>();

		private static readonly Dictionary<string, EnumDoc> enumSummaries = new Dictionary<string, EnumDoc>();

		private static IEnumerable<string> GetElementContents(XElement? element) {
			if (element == null) {
				yield break;
			}

			foreach (XNode node in element.Nodes()) {
				if(node is XText text) {
					yield return text.Value;
				}
				else if(node is XElement elem) {
					if(elem.Name == "paramref" && elem.Attribute("name") is XAttribute nameAttr) {
						yield return $"\"{nameAttr.Value}\"";
					}
					else if (elem.Name == "see" && elem.Attribute("cref") is XAttribute crefAttr) {
						string[] parts = crefAttr.Value[2..].Split('.');
						if (crefAttr.Value.StartsWith("F")) { // Enum value
							string value = string.Join(".", parts[^2..]);
							yield return $"{value}";
						}
						else if (crefAttr.Value.StartsWith("T")) { // Type name
							yield return $"{parts[^1]}";
						}
					}
				}
			}
		}

		private static string? GetElementValue(XElement? element) {
			if (element is null) {
				return null;
			}

			string content = string.Join("", GetElementContents(element));
			content = string.Join("\n\n", Regex.Split(content, @"\s*\n\n\s*").Select(s => Regex.Replace(s, @"\s+", " "))).Trim();

			if (string.IsNullOrWhiteSpace(content)) {
				return null;
			}
			else {
				return content;
			}
		}

		private static Type? GetTypeFromDocumentationString(string documentationString) {
			// Extract the type name from the documentation string
			string typeName = documentationString[2..]; // Remove the "T:" prefix

			// Further processing needed? (e.g. generics)

			string? assemblyName = Assembly.GetExecutingAssembly().FullName; // Assume currently executing assembly

			string fullTypeName = typeName + (assemblyName is not null ? ", " + assemblyName : "");

			Type? type = Type.GetType(fullTypeName);

			if(type is null && assemblyName is not null) {
				type = Type.GetType(typeName);
			}

			return type;
		}

		private static IEnumerable<IDocumentationSpan> GetElementSpanContents(XElement? element) {
			if (element == null) {
				yield break;
			}

			bool trimStart = true;

			foreach (XNode node in element.Nodes()) {
				if (node is XText text) {
					if (!string.IsNullOrEmpty(text.Value)) {
						string value = text.Value;
						if (trimStart) {
							value = value.TrimStart();
							trimStart = false;
						}
						string[] processed = Regex.Split(value, @"\s*\n\n\s*").Select(s => Regex.Replace(s, @"\s+", " ")).ToArray();
						for (int i=0; i<processed.Length; i++) {
							if (i > 0) {
								yield return new LineBreakSpan();
								processed[i] = processed[i].TrimStart();
							}
							yield return new TextSpan(processed[i]);
						}
					}
				}
				else if (node is XElement elem) {
					if (elem.Name == "paramref" && elem.Attribute("name") is XAttribute nameAttr) {
						yield return new ParameterSpan(nameAttr.Value);
					}
					else if (elem.Name == "see" && elem.Attribute("cref") is XAttribute crefAttr) {
						string[] parts = crefAttr.Value[2..].Split('.');
						if (crefAttr.Value.StartsWith("F")) { // Enum value
							string type = parts[^2];
							string value = parts[^1];
							yield return new EnumValueSpan(type, value);
						}
						else if (crefAttr.Value.StartsWith("T")) { // Type name
							yield return new TypeSpan(parts[^1], GetTypeFromDocumentationString(crefAttr.Value));
						}
					}
					else if(elem.Name == "br" || (elem.Name == "para" && elem.IsEmpty)) {
						yield return new LineBreakSpan();
						trimStart = true;
					}
				}
			}
		}

		private static DocumentationString? GetDocumentationString(XElement? element) {
			if (element is null) {
				return null;
			}

			IDocumentationSpan[] content = GetElementSpanContents(element).ToArray();

			if (content.Length == 0) {
				return null;
			}

			if (content[0] is TextSpan startText) {
				content[0] = new TextSpan(startText.Text.TrimStart());
			}
			if(content.Length > 1 && content[^1] is TextSpan endText) {
				content[^1] = new TextSpan(endText.Text.TrimEnd());
			}

			for(int i=1; i<content.Length; i++) {
				if (content[i] is LineBreakSpan && content[i-1] is TextSpan textSpan) {
					content[i - 1] = new TextSpan(textSpan.Text.TrimEnd());
				}
			}

			return new DocumentationString(content);
		}

		private static void GetMethodNameSections(string xmlName, out string methodName, out string[] methodArguments) {
			string[] nameParts = xmlName.Split(new char[] { '(' }, 2);
			if (nameParts.Length > 1) {
				nameParts[1] = nameParts[1].Substring(0, nameParts[1].Length - 1); // Remove bracket at end
			}

			methodName = nameParts[0];
			methodArguments = nameParts.Length > 1 ? nameParts[1].Split(',') : Array.Empty<string>();
		}

		public static DocumentationString? GetTypeDescription(Type type) {
			if (type.FullName is not null) {
				typeSummaries.TryGetValue(type.FullName.Replace("+", "."), out TypeDoc? doc);
				return doc?.description;
			}
			else {
				return null;
			}
		}

		public static BuilderDoc? GetBuilderDoc(MethodInfo builder) {
			Type builderType = FactoryBuilderAttribute.GetBuilderType(builder);
			List<BuilderDoc>? builders = typeBuilders.GetValueOrFallback((builderType.FullName ?? "").Replace("+", "."), null);
			if (builders != null) {
				HashSet<string> paramNameSet = new HashSet<string>(builder.GetParameters().Select(p => p.Name).WhereNotNull());
				return builders.FirstOrDefault(c => paramNameSet.SetEquals(c.arguments.Select(a => a.name)));
			}

			return null;
		}

		public static EnumDoc? GetEnumDoc(Type type) {
			// TODO Can we check for MarkupEnumType here?
			if (type is MarkupEnumType markupEnumType) {
				return markupEnumType.Documentation;
			}
			else if (enumSummaries.TryGetValue((type.FullName ?? "").Replace("+", "."), out EnumDoc? enumDoc)) {
				return enumDoc;
			}
			else if (type.IsEnum) {
				return new EnumDoc(type.Name, Enum.GetNames(type).Select(n => new EnumValDoc(type.Name, n, null)).ToArray(), null);

			}
			else {
				return null;
			}
			//return enumSummaries.GetValueOrDefault(type.FullName.Replace("+", "."), null);
		}

		public static EnumDoc? GetBuiltInEnumDocFromName(string name) {
			if (enumSummaries.TryGetValue(name, out EnumDoc? enumDoc)) {
				return enumDoc;
			}
			else if(TypeUtils.GetTypeByName(name, StringComparison.OrdinalIgnoreCase) is Type namedType) {
				return GetEnumDoc(namedType);
			}
			else {
				return null;
			}
		}

		/// <summary></summary>
		/// <exception cref="NotSupportedException"></exception>
		/// <exception cref="IOException"></exception>
		public static void LoadEmbeddedDocumentation(Assembly assembly) {

			if (!loadedAssemblies.Add(assembly)) { // If already present, we don't need to add it again
				return;
			}

			string assemblyName = assembly.GetName().Name ?? throw new NotSupportedException("Provided assembly has no name.");
			string? resourceName = assembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(assemblyName+".xml"));

			if (resourceName != null) {

				XDocument documentation;
				try {
					using (Stream stream = assembly.GetManifestResourceStream(resourceName)!) { // We know this resource exists
						documentation = XDocument.Load(stream);
					}
				}
				catch(BadImageFormatException e) {
					throw new FileNotFoundException($"Could not load documentation file for {assemblyName}.", e);
				}

				Dictionary<string, List<EnumValDoc>> enumValues = new Dictionary<string, List<EnumValDoc>>();

				foreach (XElement member in documentation.Descendants("member")) {
					string? fullName = member.Attribute("name")?.Value;
					if(fullName is null) { continue; }

					string name = fullName.Substring(2);

					//Console.WriteLine($"Member: {fullName}");

					if (fullName.StartsWith("T")) {
						DocumentationString? summary = GetDocumentationString(member.Descendants("summary").FirstOrDefault());
						typeSummaries.Add(name, new TypeDoc(name, summary));

						//string[] typeNameParts = name.Split('.');
						//string typeName = typeNameParts[typeNameParts.Length - 1];
						//Console.WriteLine($"Type: {typeName}");
						//Console.WriteLine($"Summary: {summary}");
					}
					else if (fullName.StartsWith("M") && GetMethodInfoFromXmlName(fullName) is MethodInfo methodInfo) {
						//Console.WriteLine($"Found: {methodInfo}");
						if (methodInfo.GetCustomAttribute<FactoryBuilderAttribute>() is FactoryBuilderAttribute builderAttr) {
							//Console.WriteLine("Has FactoryBuilderAttribute");

							Type builderType = FactoryBuilderAttribute.GetBuilderType(methodInfo);

							Dictionary<string, ParameterInfo> paramsDict = methodInfo.GetParameters().Where(p => p.Name is not null).ToDictionary(p => p.Name!);

							string typeFullName = builderType.FullName ?? builderType.Name;
							string typeName = builderType.Name;

							//Console.WriteLine($"Constructor for {typeName} ({typeFullName} -> {GetTypeByName(assembly, typeFullName)?.FullName ?? "None"})");

							List<ArgumentDoc> argumentDocs = new List<ArgumentDoc>();
							foreach (XElement param in member.Descendants("param")) {
								string? paramName = param.Attribute("name")?.Value;
								if (paramName is null) { continue; }
								ParameterInfo paramInfo = paramsDict[paramName];
								PropertyAttribute? propAttr = paramInfo?.GetCustomAttribute<PropertyAttribute>();
								//if(propAttr is not null) { Console.WriteLine($"Got attr for {methodInfo} {paramInfo}"); }

								DocumentationString? description = GetDocumentationString(param);
								//Console.WriteLine($"Parameter {paramName}: {description}");

								//XElement defaultValue = param.Descendants("default").FirstOrDefault();
								//string defaultStr = defaultValue != null ? defaultValue.Attribute("value")?.Value : null;
								string? defaultStr = propAttr?.Default; // param.Attribute("default")?.Value;
								string? exampleStr = propAttr?.Example; // param.Attribute("example")?.Value;
								//Console.WriteLine($"Default value: {defaultValue} => {defaultStr}");
								bool exclude = propAttr?.Exclude ?? false;  //string.Equals("true", param.Attribute("exclude")?.Value ?? "false", StringComparison.OrdinalIgnoreCase);

								argumentDocs.Add(new ArgumentDoc(paramName, description, defaultStr, exampleStr, exclude));
							}

							DocumentationString? summary = member.Descendants("summary").FirstOrDefault() is XElement summaryElem ? GetDocumentationString(summaryElem) : null;
							string? size = member.Descendants("size").FirstOrDefault() is XElement sizeElem ? GetElementValue(sizeElem) : null;
							string? canvas = member.Descendants("canvas").FirstOrDefault() is XElement canvasElem ? GetElementValue(canvasElem) : null;

							if (!typeBuilders.ContainsKey(typeFullName)) {
								typeBuilders.Add(typeFullName, new List<BuilderDoc>());
							}
							typeBuilders.GetValueOrFallback(typeFullName, null)?.Add(new BuilderDoc(typeFullName, argumentDocs.ToArray(), summary, size, canvas));

							//typeConstructors.Add(typeFullName, new ConstructorDoc(typeFullName, argumentDocs.ToArray(), summary));
						}
					}
					else if (fullName.StartsWith("F")) {
						// "F" for Field
						string[] nameParts = name.Split('.');
						string owningTypeName = string.Join('.', nameParts[..^1]);

						if (GetTypeFromXmlName(owningTypeName) is Type owningType && owningType.IsEnum) {
							// Enum
							string enumValue = nameParts[^1];
							string enumType = nameParts[^2];
							string enumFullType = owningTypeName;
							DocumentationString? summary = GetDocumentationString(member.Descendants("summary").FirstOrDefault());

							if (!enumValues.ContainsKey(enumFullType)) {
								enumValues.Add(enumFullType, new List<EnumValDoc>());
							}
							enumValues.GetValueOrFallback(enumFullType, null)?.Add(new EnumValDoc(enumType, enumValue, summary));
						}
					}
					//Console.WriteLine();
				}

				// Sort out found enum types
				foreach(KeyValuePair<string, List<EnumValDoc>> enumEntry in enumValues) {
					DocumentationString? typeDescription = null;
					if (typeSummaries.ContainsKey(enumEntry.Key)) {
						typeDescription = typeSummaries.GetValueOrFallback(enumEntry.Key, null)?.description;
						typeSummaries.Remove(enumEntry.Key);
					}
					string[] nameParts = enumEntry.Key.Split('.');
					string typeName = nameParts[^1];
					enumSummaries.Add(enumEntry.Key, new EnumDoc(typeName, enumEntry.Value.ToArray(), typeDescription));
				}
			}

		}

		private static readonly Regex genericXmlTypeNameRegex = new Regex(@"^(?<baseType>[A-Za-z0-9_\.]+)\{(?<innerType>.+)\}$");

		private static Type? GetTypeFromXmlName(string xmlName) {
			// try GetType first; if that fails, search loaded assemblies
			//Console.WriteLine($"Try get type: {xmlName}");
			Type? result;
			Match genericMatch = genericXmlTypeNameRegex.Match(xmlName);
			if (genericMatch.Success) {
				string baseTypeName = genericMatch.Groups["baseType"].Value;
				string[] innerTypeNames = SplitXmlTypes(genericMatch.Groups["innerType"].Value).ToArray();

				Type? baseType = GetTypeFromXmlName($"{baseTypeName}`{innerTypeNames.Length}");
				if (baseType is null) { return null; }

				Type?[] innerTypes = innerTypeNames.Select(n => GetTypeFromXmlName(n)).ToArray();
				if (innerTypes.Any(t => t is null)) { return null; }

				result = baseType.MakeGenericType(innerTypes!);
			}
			else {
				string[] parts = xmlName.Split('.');
				int nestedTypeLevel = 0;
				do {
					string tryName = string.Join('.', parts[..^nestedTypeLevel]) + (nestedTypeLevel > 0 ? ("+" + string.Join('+', parts[^nestedTypeLevel..])) : "");

					result = Type.GetType(tryName, throwOnError: false)
						?? AppDomain.CurrentDomain
							.GetAssemblies()
							.Select(a => a.GetType(tryName, throwOnError: false))
							.FirstOrDefault(t => t != null);
					
					nestedTypeLevel++;
				} while (result is null && nestedTypeLevel < parts.Length - 1);
			}
			//Console.WriteLine($"Got: {result?.ToString() ?? "null"}");
			return result;
		}

		private static IEnumerable<string> SplitXmlTypes(string paramsList) {
			int nesting = 0;
			int head = 0;

			for (int i = 0; i < paramsList.Length; i++) {
				if (paramsList[i] == '{') {
					nesting++;
				}
				else if (paramsList[i] == '}') {
					nesting--;
				}
				else if (paramsList[i] == ',' && nesting == 0) {
					yield return paramsList[head..i];
					head = i + 1;
				}
			}

			yield return paramsList[head..];
		}

		/// <summary>
		/// Given an XML-doc “member name” string like
		///     M:MyNamespace.MyType.MyMethod(System.String,System.Int32)
		/// returns the corresponding MethodInfo (or null).
		/// </summary>
		private static MethodInfo? GetMethodInfoFromXmlName(string xmlName) {
			// Thanks ChatGPT https://chatgpt.com/s/t_6873b49cf4748191b7416345bce7b54d

			//Console.WriteLine($"Asking for MethodInfo for {xmlName}");

			// strip the leading "M:"
			string sig = xmlName[2..];

			// split off parameters if any
			int paramsStart = sig.IndexOf('(');

			if (paramsStart < 0) {
				return null;
			}

			string typeAndMethod = sig[..paramsStart];
			string paramsList = sig.Substring(paramsStart + 1, sig.Length - paramsStart - 2);

			// split namespace.type from method name
			int lastDot = typeAndMethod.LastIndexOf('.');
			if (lastDot < 0) return null;

			string typeFullName = typeAndMethod[..lastDot];
			string methodName = typeAndMethod[(lastDot + 1)..];

			// locate the Type in any loaded assembly
			// Type? targetType = AppDomain.CurrentDomain
			// 	.GetAssemblies()
			// 	.Select(a => a.GetType(typeFullName, throwOnError: false))
			// 	.FirstOrDefault(t => t != null);
			Type? targetType = GetTypeFromXmlName(typeFullName);

			if (targetType == null)
				return null;

			//Console.WriteLine($"Got target type: {targetType}");

			// parse parameter type names
			Type?[] paramTypes = paramsList == ""
				? Type.EmptyTypes
				: SplitXmlTypes(paramsList)
					.Select(pn => GetTypeFromXmlName(pn))
					.ToArray();

			if (paramTypes.Any(p => p is null)) {
				return null;
			}

			//Console.WriteLine($"Got params list: {paramTypes.Length}");

			// finally get the MethodInfo
			return targetType.GetMethod(
				methodName,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
				binder: null,
				types: paramTypes!,
				modifiers: null
			);
		}

	}
}