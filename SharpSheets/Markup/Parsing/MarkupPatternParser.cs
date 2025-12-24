using SharpSheets.Evaluations;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using SharpSheets.Colors;
using System.Text.RegularExpressions;
using SharpSheets.Shapes;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Markup.Patterns;
using SharpSheets.Markup.Elements;
using SharpSheets.Markup.Canvas;
using SharpSheets.Evaluations.Nodes;
using SharpSheets.Exceptions;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Documentation;
using SharpSheets.Evaluations.Types;

namespace SharpSheets.Markup.Parsing {

	public class MarkupPatternParser : IParser<List<MarkupPattern>> {

		public ShapeFactory ShapeFactory { get; set; }

		public MarkupPatternParser() {
			this.ShapeFactory = new ShapeFactory(MarkupRegistry.Empty);
		}

		public MarkupPatternParser(ShapeFactory shapeFactory) {
			this.ShapeFactory = shapeFactory;
		}

		public List<MarkupPattern> ParseContent(FilePath origin, DirectoryPath source, string xml, out CompilationResult results) {
			ParsedPatternDocument parsedDocument = ParsedPatternDocument.ParseDocument(xml, origin, source, ShapeFactory);
			results = parsedDocument.GetCompilationResult();
			return parsedDocument.patterns;
		}

		private class ParsedPatternDocument {

			public readonly ShapeFactory shapeFactory;

			public readonly FilePath origin;
			public readonly DirectoryPath source;
			public readonly List<MarkupPattern> patterns;
			public readonly Dictionary<DocumentSpan, List<SharpParsingException>> errors;
			private XMLElement? root;

			private readonly HashSet<XMLNode> visitedNodes;
			private readonly Dictionary<XMLNode, HashSet<string>> visitedAttributes;

			private readonly ParseOrigins<IDocumentEntity> origins;

			private ParsedPatternDocument(FilePath origin, DirectoryPath source, ShapeFactory shapeFactory) {
				this.shapeFactory = shapeFactory;

				this.origin = origin;
				this.source = source;
				this.patterns = new List<MarkupPattern>();
				this.errors = new Dictionary<DocumentSpan, List<SharpParsingException>>();
				this.visitedNodes = new HashSet<XMLNode>();
				this.visitedAttributes = new Dictionary<XMLNode, HashSet<string>>();

				this.origins = new ParseOrigins<IDocumentEntity>();
			}

			public static ParsedPatternDocument ParseDocument(string xml, FilePath origin, DirectoryPath source, ShapeFactory shapeFactory) {
				ParsedPatternDocument document = new ParsedPatternDocument(origin, source, shapeFactory);
				document.RunParsing(xml);
				//foreach (SharpParsingException error in document.errors.Values.SelectMany(c=>c)) { Console.WriteLine($"({error.Location?.Line ?? -1}, {error.Location?.Column ?? -1}, {error.Location?.Length ?? -1}): " + error.Message + (error.InnerException != null ? " (" + error.InnerException.Message + ")" : "")); }
				return document;
			}

			/// <summary></summary>
			/// <exception cref="InvalidOperationException"></exception>
			public CompilationResult GetCompilationResult() {
				if(root is null) {
					throw new InvalidOperationException("Compilation result cannot be collected before document parsing is completed.");
				}
				// TODO Should the unusedLines arguments be unusedLocations instead? (DocumentSpan as opposed to int?)
				return new CompilationResult(root, origins, errors.Values.SelectMany(e => e).ToList(), null, null, new List<FilePath>());
			}

			private void RunParsing(string xml) {
				patterns.Clear();
				errors.Clear();

				root = XMLParsing.Parse(xml, out List<SharpParsingException> xmlErrors, false); // document.Root;

				if (xmlErrors != null && xmlErrors.Count > 0) {
					AddErrors(xmlErrors);
					return;
				}
				else if(root is null) {
					return; // This shouldn't happen
				}

				// Collect pattern elements in document root
				string? fileName = origin.FileNameWithoutExtension;
				if (root.Name == "pattern") {
					RunPatternParsing(root, fileName);
				}
				else if (root.Name == "patternLibrary") {
					string? libraryName = root.HasAttribute("name", false) ? null : fileName;
					RunLibraryParsing(root, libraryName);
				}
				else {
					LogError(root, "Invalid root node.");
				}

				// Identify and log errors for unused nodes
				LogUnused(root);
			}

			private void RunPatternParsing(XMLElement patternElem, string? libraryName) {
				MarkupPattern pattern = MakePattern(patternElem, libraryName);
				if (pattern != null) {
					patterns.Add(pattern);
					LogOrigin(patternElem, pattern);
				}
				LogVisit(patternElem);
			}

			private void RunLibraryParsing(XMLElement libraryElem, string? libraryName) {
				libraryName = GetAttribute(libraryElem, "name", true, s => (libraryName != null ? libraryName + "." : "") + MarkupValueParsing.ParseLibraryName(s), libraryName);
				if (libraryName != null && libraryName.Length == 0) { libraryName = null; }
				foreach (XMLElement elem in libraryElem.Elements) {
					if (elem.Name == "patternLibrary") {
						RunLibraryParsing(elem, libraryName);
					}
					else if (elem.Name == "pattern") {
						RunPatternParsing(elem, libraryName);
					}
					else {
						LogError(elem, "Invalid child of root node.");
					}
				}
				LogVisit(libraryElem);
			}

			#region Book-keeping and Error Handling

			private void LogVisit(XMLNode node) {
				visitedNodes.Add(node.Original);
			}
			private void LogVisit(XMLElement elem, string attributeName) {
				if (!visitedAttributes.TryGetValue(elem.Original, out HashSet<string>? visited)) {
					visited = new HashSet<string>(XMLElement.AttributeNameComparer);
					visitedAttributes.Add(elem.Original, visited);
					LogVisit(elem);
				}
				visited.Add(attributeName);
			}
			private bool CheckVisited(XMLNode node) {
				return visitedNodes.Contains(node.Original);
			}

			/// <summary>
			/// Returns true if the node in question, or one of it's descendants, has been marked as visited.
			/// Returns false and logs an error if it has not.
			/// </summary>
			/// <param name="node">Node to check.</param>
			/// <returns></returns>
			private bool LogUnused(XMLNode node) {

				bool childUsed = false;
				foreach(XMLNode child in node.Children) {
					childUsed |= LogUnused(child);
				}

				bool visited = CheckVisited(node);
				bool hasErrors = errors.ContainsKey(GetXMLNodeErrorLocation(node));

				if (hasErrors) {
					return true;
				}
				else if (childUsed || visited) {

					if(node is XMLElement elem) {
						HashSet<string>? elemAttrsSeen = visitedAttributes.GetValueOrFallback(elem, null);
						foreach (ContextProperty<string> attr in elem.Attributes) {
							if (!XMLElement.AttributeNameComparer.Equals(attr.Name, "id") && !(elemAttrsSeen?.Contains(attr.Name) ?? false) && !errors.ContainsKey(attr.ValueLocation)) {
								LogError(attr.Location, $"Unused attribute value \"{attr.Name}\".");
							}
						}
					}

					return true;
				}
				else {
					if (node is XMLText) {
						LogError(node, "Unused text node.");
					}
					else if (node is XMLElement element) {
						LogError(node, "Unused node: " + element.Name);
					}
					else if(node is not XMLComment) {
						LogError(node, "Unused node.");
					}

					return false;
				}
			}

			private static DocumentSpan GetXMLNodeErrorLocation(XMLNode node) {
				return node switch {
					XMLElement elem => elem.NameValue.Location,
					_ => node.Location
				};
			}

			private void LogOrigin(XMLNode origin, IIdentifiableMarkupElement element) {
				this.origins.Add(element, origin.Original);
			}
			private void LogOrigin(XMLNode origin, MarkupPattern pattern) {
				this.origins.Add(pattern, origin.Original);
			}

			private MarkupParsingException LogError(MarkupParsingException error) {
				if (!errors.ContainsKey(error.Location)) { errors.Add(error.Location, new List<SharpParsingException>()); }
				errors.GetValueOrFallback(error.Location, null)?.Add(error);
				return error;
			}
			private MarkupParsingException LogError(DocumentSpan location, string message, Exception? innerException = null) {
				if (!errors.ContainsKey(location)) { errors.Add(location, new List<SharpParsingException>()); }
				MarkupParsingException error = new MarkupParsingException(location, message, innerException);
				return LogError(error);
			}
			private MarkupParsingException LogError(XMLNode node, string message, Exception? innerException = null) {
				return LogError(GetXMLNodeErrorLocation(node), message, innerException);
			}
			private MarkupParsingException LogError<T>(ContextProperty<T> contextProperty, string message, Exception? innerException = null) {
				return LogError(contextProperty.ValueLocation, message, innerException);
			}
			private MarkupParsingException LogError(XMLNode node, Exception error) {
				return LogError(node, error.Message, error);
			}

			private void AddError(SharpParsingException error) {
				if (error.Location.HasValue) {
					DocumentSpan location = error.Location.Value;
					if (!errors.ContainsKey(location)) { errors.Add(location, new List<SharpParsingException>()); }
					errors.GetValueOrFallback(location, null)?.Add(error);
				}
			}
			private void AddErrors(IEnumerable<SharpParsingException> errorCollection) {
				foreach(SharpParsingException error in errorCollection) {
					AddError(error);
				}
			}

			private void InvalidChildNode(XMLNode node) {
				if (node is XMLElement elem) {
					InvalidChildElement(elem);
				}
				else if (node is XMLText) {
					LogError(node, "Invalid text node.");
				}
				else if (node is not XMLComment) {
					LogError(node, "Invalid child node.");
				}
			}

			private void InvalidChildElement(XMLElement elem) {
				if (elem.Parent is XMLElement parent) {
					LogError(elem, $"<{elem.Name}> is not a valid child element of <{parent.Name}>.");
				}
				else {
					LogError(elem, "Invalid child element.");
				}
			}

			private void AssertLeafNode(XMLElement elem, bool allowTextNodes = false) {
				foreach(XMLNode childNode in elem.Children) {
					if (!((allowTextNodes && childNode is XMLText) || childNode is XMLComment)) {
						InvalidChildNode(childNode);
					}
				}
			}

			#endregion

			#region Structural Elements

			private IVariableBox? GetPatternVariables(ContextProperty<MarkupPatternType> typeAttr, EvaluationContext context) {
				if (typeAttr.Value == MarkupPatternType.BOX) {
					return PatternData.GetPatternVariables<MarkupBoxPattern>(context);
				}
				else if (typeAttr.Value == MarkupPatternType.LABELLEDBOX) {
					return PatternData.GetPatternVariables<MarkupLabelledBoxPattern>(context);
				}
				else if (typeAttr.Value == MarkupPatternType.TITLESTYLE) {
					return PatternData.GetPatternVariables<MarkupTitleStyledBoxPattern>(context);
				}
				else if (typeAttr.Value == MarkupPatternType.TITLEDBOX) {
					return PatternData.GetPatternVariables<MarkupTitledBoxPattern>(context);
				}
				else if (typeAttr.Value == MarkupPatternType.ENTRIEDSHAPE) {
					return PatternData.GetPatternVariables<MarkupEntriedShapePattern>(context);
				}
				else if (typeAttr.Value == MarkupPatternType.BAR) {
					return PatternData.GetPatternVariables<MarkupBarPattern>(context);
				}
				else if (typeAttr.Value == MarkupPatternType.USAGEBAR) {
					return PatternData.GetPatternVariables<MarkupUsageBarPattern>(context);
				}
				else if (typeAttr.Value == MarkupPatternType.DETAIL) {
					return PatternData.GetPatternVariables<MarkupDetailPattern>(context);
				}
				else if (typeAttr.Value == MarkupPatternType.WIDGET) {
					return PatternData.GetPatternVariables<MarkupWidgetPattern>(context);
				}
				else {
					LogError(typeAttr, $"Unrecognised pattern type: {typeAttr.Value}");
					return null;
				}
			}

			private MarkupPattern MakePattern(XMLElement patternElem, string? libraryName) {
				if (RequiredAttribute(patternElem, "type", false, s => EnumUtils.ParseEnum<MarkupPatternType>(s), out ContextProperty<MarkupPatternType> typeAttr)) {
					IVariableBox? patternVariables = GetPatternVariables(typeAttr, MarkupEvaluationTypes.BaseContext);
					if(patternVariables is null) {
						return MakeErrorPattern(patternElem, libraryName, new Exception("Invalid pattern type."));
					}

					PatternElementDetails details;
					try {
						details = CollectPatternElementDetails(patternElem, source, patternVariables);
					}
					catch (SharpParsingException e) {
						LogError(patternElem, "Error parsing pattern node: " + e.Message, e);
						return MakeErrorPattern(patternElem, libraryName, e);
					}
					catch (FormatException e) {
						LogError(patternElem, "Error parsing pattern node: " + e.Message, e);
						return MakeErrorPattern(patternElem, libraryName, e);
					}
					catch (Exception e) {
						// This maybe shouldn't stay here?
						LogError(patternElem, "Fatal error parsing pattern node: " + e.Message, e);
						return MakeErrorPattern(patternElem, libraryName, e);
					}

					if(details.name is null) {
						SharpParsingException error = LogError(patternElem, "Patterns must have a name to be valid.");
						return MakeErrorPattern(patternElem, libraryName, error);
					}

					string name = details.name;
					string? description = details.description;
					IMarkupArgument[] arguments = details.arguments;
					MarkupValidation[] validations = details.validations;
					Rectangle? exampleSize = details.exampleSize;
					Size? exampleCanvas = details.exampleCanvas;
					DivElement rootElement = details.rootDiv;

					MarkupPattern result;

					MarkupPatternType type = typeAttr.Value;
					if (type == MarkupPatternType.BOX) {
						result = new MarkupBoxPattern(libraryName, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, origin);
					}
					else if (type == MarkupPatternType.LABELLEDBOX) {
						result = new MarkupLabelledBoxPattern(libraryName, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, origin);
					}
					else if (type == MarkupPatternType.TITLESTYLE) {
						result = new MarkupTitleStyledBoxPattern(libraryName, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, origin);
					}
					else if (type == MarkupPatternType.TITLEDBOX) {
						result = new MarkupTitledBoxPattern(libraryName, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, origin);
					}
					else if (type == MarkupPatternType.ENTRIEDSHAPE) {
						result = new MarkupEntriedShapePattern(libraryName, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, origin);
					}
					else if (type == MarkupPatternType.BAR) {
						result = new MarkupBarPattern(libraryName, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, origin);
					}
					else if (type == MarkupPatternType.USAGEBAR) {
						result = new MarkupUsageBarPattern(libraryName, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, origin);
					}
					else if (type == MarkupPatternType.DETAIL) {
						result = new MarkupDetailPattern(libraryName, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, origin);
					}
					else if (type == MarkupPatternType.WIDGET) {
						result = new MarkupWidgetPattern(libraryName, name, description, arguments, validations, exampleSize, exampleCanvas, rootElement, origin);
					}
					else {
						Exception typeError = LogError(typeAttr, $"Unrecognised pattern type: {type}");
						result = MakeErrorPattern(patternElem, libraryName, typeError);
					}

					return result;
				}

				return MakeErrorPattern(patternElem, libraryName, new MarkupParsingException(patternElem.NameValue.Location, "Pattern must have a type to be valid."));
			}

			private ErrorPattern MakeErrorPattern(XMLElement patternElem, string? libraryName, Exception error) {
				string name = GetAttribute(patternElem, "name", false, s => s, null) ?? "ErrorPattern";
				Rectangle? exampleSize = GetAttribute(patternElem, "example-size", false, MarkupValueParsing.ParseConcreteRectangle, null);
				Size? exampleCanvas = GetAttribute(patternElem, "example-canvas", false, MarkupValueParsing.ParseConcreteSize, null);
				return new ErrorPattern(libraryName, name, error, exampleSize, exampleCanvas, origin);
			}

			private readonly struct PatternElementDetails {
				public readonly string? name;
				public readonly Rectangle? exampleSize;
				public readonly Size? exampleCanvas;
				public readonly string? description;
				public readonly IMarkupArgument[] arguments;
				public readonly MarkupValidation[] validations;
				public readonly DivElement rootDiv;

				public PatternElementDetails(string? name, Rectangle? exampleSize, Size? exampleCanvas, string? description, IMarkupArgument[] arguments, MarkupValidation[] validations, DivElement rootDiv) {
					this.name = name;
					this.exampleSize = exampleSize;
					this.exampleCanvas = exampleCanvas;
					this.description = description;
					this.arguments = arguments;
					this.validations = validations;
					this.rootDiv = rootDiv;
				}
			}

			private PatternElementDetails CollectPatternElementDetails(XMLElement patternElement, DirectoryPath source, IVariableBox patternVariables) {

				string? name = GetAttribute(patternElement, "name", false, s => s, null);
				Rectangle? exampleSize = GetAttribute(patternElement, "example-size", false, MarkupValueParsing.ParseConcreteRectangle, null);
				Size? exampleCanvas = GetAttribute(patternElement, "example-canvas", false, MarkupValueParsing.ParseConcreteSize, null);

				string? description;
				XMLElement? desc = patternElement.FindElement("desc");
				if (desc is not null) {
					XMLText[] descText = desc.Children.OfType<XMLText>().ToArray();
					description = string.Join(" ", descText.Select(t => t.Text)).Trim();
					LogVisit(desc);
					foreach (XMLText textNode in descText) {
						LogVisit(textNode);
					}
				}
				else {
					description = null;
				}

				// First pass through arguments to collect any new types
				EvaluationContext.Builder contextBuilder = MarkupEvaluationTypes.Create();
				foreach (XMLElement argElem in GetVariableElements(patternElement, VariableType.ARGUMENT, true)) {
					CollectArgumentTypes(argElem, contextBuilder);
				}

				MarkupEvaluationContext patternContext = new MarkupEvaluationContext(contextBuilder.Build());

				// Second pass through arguments to actually build markup argument instances using constructed EvaluationContext
				List<IMarkupArgument> arguments = new List<IMarkupArgument>();
				HashSet<EvaluationName> argNames = new HashSet<EvaluationName>();
				Dictionary<EvaluationName, EnvironmentVariableInfo> varTypes = new Dictionary<EvaluationName, EnvironmentVariableInfo>();
				bool entriesUsed = false;
				foreach (XMLElement argElem in GetVariableElements(patternElement, VariableType.ARGUMENT, true)) {
					IMarkupArgument? argument = MakeArgument(argElem, false, patternContext, out ContextProperty<EvaluationName> argName, out ContextProperty<EvaluationName>? varName);
					if (argument != null) {
						if (patternVariables.IsVariable(argument.VariableName)) {
							LogError(varName ?? argName, "This variable name is already taken by a pattern argument.");
						}
						else if (argNames.Contains(argument.ArgumentName)) {
							LogError(argName, "An argument with this name already exists.");
						}
						else if (varTypes.ContainsKey(argument.VariableName)) {
							LogError(varName ?? argName, "A variable with this name already exists.");
						}
						else if(argument.FromEntries && entriesUsed) {
							LogError(argElem, "Can only have one argument derived from context entries.");
						}
						else {
							arguments.Add(argument);
							argNames.Add(argument.ArgumentName);
							varTypes.Add(argument.VariableName, new EnvironmentVariableInfo(argument.VariableName, argument.Type, argument.Description));
							entriesUsed |= argument.FromEntries;
						}
					}
					LogVisit(argElem);
				}

				// TODO This is kinda ugly? Can we make this kind of setup cleaner?
				IVariableBox topLevelPatternVariables = VariableBoxes.Concat(patternContext.TypeSystem, patternVariables, VariableBoxes.Create(varTypes.Values, patternContext.TypeSystem));
				
				IVariableBox validationVariables = BasisEnvironment.MakeInstance(patternContext.TypeSystem).AppendVariables(topLevelPatternVariables);
				List<MarkupValidation> validations = new List<MarkupValidation>();
				foreach (XMLElement validationElem in GetVariableElements(patternElement, VariableType.VALIDATION, true)) {
					MarkupValidation? validation = MakeValidation(validationElem, validationVariables);
					if (validation != null) {
						validations.Add(validation);
					}
					LogVisit(validationElem);
				}
				
				// TODO Need to deal with errors from here
				DivElement rootDiv = MakeDivElement(patternElement, patternElement, topLevelPatternVariables, patternContext, new Dictionary<XMLElement, IIdentifiableMarkupElement>(), source);
				
				LogVisit(patternElement);
				LogOrigin(patternElement, rootDiv);

				return new PatternElementDetails(name, exampleSize, exampleCanvas, description, arguments.ToArray(), validations.ToArray(), rootDiv);
			}

			private enum VariableType { ARGUMENT, VARIABLE, VALIDATION };
			private static IEnumerable<XMLElement> GetVariableElements(XMLElement element, VariableType varType, bool allowArgsElem) {
				foreach (XMLElement child in element.Elements) {
					if (allowArgsElem && child.Name == "args") {
						foreach (XMLElement argChild in GetVariableElements(child, varType, false)) {
							yield return argChild;
						}
					}
					else if(varType == VariableType.ARGUMENT && (child.Name == "arg" || child.Name == "grouparg")) {
						yield return child;
					}
					else if (varType == VariableType.VARIABLE && child.Name == "var") {
						yield return child;
					}
					else if (varType == VariableType.VALIDATION && child.Name == "validation") {
						yield return child;
					}
				}
			}

			private DivSetup GetDivSetup(XMLElement divElem, IVariableBox outerContext, MarkupEvaluationContext markupContext, out string? id, out MarkupVariable[] divVariables) {
				// Get div id (which must be a concrete string)
				id = GetAttribute(divElem, "id", false, s => s, null);

				// Get foreach expression for this div, using outer context variables
				ForEachExpression? forEach = GetAttribute(divElem, "for-each", false, s => ForEachExpression.Parse(s, outerContext), null);

				IVariableBox variables = outerContext;
				if (forEach != null) {
					// If for-each provided, add it's variable to the collection used for parsing MarkupVariable entries
					variables = variables.AppendVariables(new EnvironmentVariableInfo[] { forEach.Variable });
				}

				// Collect variables from this div (not arguments, as they belong solely to the pattern-level, and are dealt with separately)
				divVariables = XElementVariables.GetMarkupVariables(this, divElem, variables, markupContext);

				// Now collect outer-context variables (including canvas drawing variables) and this div's MarkupVariables (and for-each loop variable) into a single VariableBox for parsing the div attributes
				//IVariableBox setupVariables = variables.AppendVariables(MarkupEnvironments.DrawingStateVariables).AppendVariables(VariableBoxes.Simple(divVariables.ToDictionary(v => v.Name, v => v.Type)));
				// We don't need to add drawing variables here, as they're added to the VariableBox at a higher level
				IVariableBox setupVariables = variables.AppendVariables(divVariables.Select(v => new EnvironmentVariableInfo(v.Name, v.Type, null)));

				// Get setup information for this div, using complete variables (including foreach loop variable)
				FloatExpression? gutter = GetAttribute(divElem, "gutter", true, s => FloatExpression.Parse(s, setupVariables), null);
				DimensionExpression size = GetAttribute(divElem, "size", false, s => MarkupValueParsing.ParseDimension(s, setupVariables), new DimensionExpression(Dimension.Single, setupVariables.Context));
				FloatExpression? minWidth = GetAttribute(divElem, "min-width", false, s => FloatExpression.Parse(s, setupVariables), null);
				FloatExpression? minHeight = GetAttribute(divElem, "min-height", false, s => FloatExpression.Parse(s, setupVariables), null);
				PositionExpression? position = MakePositionExpression(divElem, setupVariables);
				MarginsExpression? margins = GetAttribute(divElem, "margins", false, s => MarkupValueParsing.ParseMargins(s, setupVariables), null);
				EnumExpression<LayoutDirection>? layout = GetAttribute(divElem, "layout", true, s => MarkupValueParsing.ParseEnum<LayoutDirection>(s, setupVariables), null);
				EnumExpression<Arrangement>? arrangement = GetAttribute(divElem, "arrangement", true, s => MarkupValueParsing.ParseEnum<Arrangement>(s, setupVariables), null);
				EnumExpression<LayoutOrder>? order = GetAttribute(divElem, "order", true, s => MarkupValueParsing.ParseEnum<LayoutOrder>(s, setupVariables), null);
				BoolExpression provideRemaining = GetAttribute(divElem, "provide-remaining", false, s => BoolExpression.Parse(s, setupVariables), new BoolExpression(false, setupVariables.Context));
				Size? canvasArea = GetAttribute(divElem, "canvas", false, MarkupValueParsing.ParseConcreteSize, null); // TODO Does this need a default value?
				FloatExpression? aspectRatio = GetAttribute(divElem, "aspect-ratio", false, s => FloatExpression.Parse(s, setupVariables), null);
				BoolExpression enabled = GetAttribute(divElem, "enabled", false, s => BoolExpression.Parse(s, setupVariables), new BoolExpression(true, setupVariables.Context));
				IntExpression? repeat = GetAttribute(divElem, "repeat", false, s => IntExpression.Parse(s, setupVariables), null);
				//EnumExpression<DrawingCoords> drawingCoords = GetAttribute(elem, "coords", true, s => ParseEnum<DrawingCoords>(s, variables), null);
				//BoolExpression keepAspectRatio = GetAttribute(elem, "keepAspectRatio", false, ParseSimpleBool, false);

				return new DivSetup(origin, gutter, size, minWidth, minHeight, position, margins, layout, arrangement, order, provideRemaining, canvasArea, aspectRatio, enabled, repeat, forEach);
				//setup = MakeDivSetup(divElem, setupVariables);
			}

			private DivElement MakeDivElement(XMLElement root, XMLElement divElem, IVariableBox outerContext, MarkupEvaluationContext markupContext, Dictionary<XMLElement, IIdentifiableMarkupElement> constructed, DirectoryPath source) {

				IVariableBox variables = markupContext.DrawingStateVariables().AppendVariables(outerContext);

				// Get setup information for this div element
				DivSetup setup = GetDivSetup(divElem, variables, markupContext, out string? id, out MarkupVariable[] divVariables);

				// Make div object, using outer-context variables and this div's MarkupVariables
				DivElement divElement = new DivElement(id, setup, outerContext, markupContext, divVariables);

				// Create full VariableBox, including all information from this div and canvas drawing variables
				IVariableBox fullDrawingVariables = markupContext.DrawingStateVariables().AppendVariables(divElement.Variables);

				IVariableBox parentParseVariables = BasisEnvironment.MakeInstance(divElement.Variables.Context).AppendVariables(divElement.Variables);

				// Begin constructing child nodes
				foreach (XMLElement childElem in divElem.Elements) {
					try {
						if (childElem.Name == "args" || childElem.Name == "defs") {
							// Ignore
						}
						else if (childElem.Name == "div") {
							// This child is constructed as a standard div
							DivElement child = MakeDivElement(root, childElem, divElement.Variables, markupContext, constructed, source);
							if (child != null) {
								divElement.AddElement(child);
								LogOrigin(childElem, child);
							}
							LogVisit(childElem);
						}
						else if(childElem.Name == "child") {
							// This child is child div
							DivElement child = MakeChildDivElement(childElem, divElement.Variables, markupContext);
							if (child != null) {
								divElement.AddElement(child);
								LogOrigin(childElem, child);
							}
							LogVisit(childElem);
						}
						else if (MarkupParsingConstants.IsReferenceElement(childElem.Name)) {
							// This child is a styled div, and we leave the MakeStyledDivElement method to determine which kind
							DivElement? styledChild = MakeStyledDivElement(root, childElem, divElement.Variables, markupContext, constructed, source);
							if (styledChild != null) {
								divElement.AddElement(styledChild);
								LogOrigin(childElem, styledChild);
							}
							LogVisit(childElem);
						}
						else if (childElem.Name == "slicing") {
							if (setup.canvasArea != null) {
								SlicingValuesElement? slicingValuesElem = MakeSlicingValuesElement(childElem, parentParseVariables, fullDrawingVariables, markupContext);
								if (slicingValuesElem != null) {
									divElement.AddSlicingValues(slicingValuesElem);
								}
							}
							else {
								LogError(childElem, "Slicing elements can only be used if the containing Div element has a canvas area specified.");
							}
							LogVisit(childElem);
						}
						else if (childElem.Name == "area") {
							AreaElement? areaElement = MakeAreaElement(childElem, parentParseVariables, fullDrawingVariables, markupContext);
							if (areaElement != null) {
								divElement.AddElement(areaElement);
								LogOrigin(childElem, areaElement);
							}
							LogVisit(childElem);
						}
						else if (childElem.Name == "diagnostic") {
							DiagnosticElement? diagnosticElement = MakeDiagnosticElement(childElem, parentParseVariables, fullDrawingVariables, markupContext);
							if (diagnosticElement != null) {
								divElement.AddElement(diagnosticElement);
								LogOrigin(childElem, diagnosticElement);
							}
							LogVisit(childElem);
						}
						else if (MarkupParsingConstants.IsRenderedElement(childElem.Name)) {
							IIdentifiableMarkupElement? part = MakeElement(root, childElem, constructed, new HashSet<XMLElement>() { divElem }, fullDrawingVariables, markupContext, source);
							if (part is IDrawableElement drawable) {
								divElement.AddElement(drawable);
								LogOrigin(childElem, drawable);
							}
							else {
								LogError(childElem, "Error parsing drawable element.");
							}
							LogVisit(childElem);
						}
						else if (!CheckVisited(childElem) && !MarkupParsingConstants.IsNonRenderedElement(childElem.Name)) {
							InvalidChildElement(childElem);
						}
					}
					catch(FormatException e) {
						LogError(childElem, e);
					}
					catch(SharpParsingException e) {
						LogError(childElem, e);
					}
				}

				LogVisit(divElem);

				return divElement;
			}

			private ChildDivElement MakeChildDivElement(XMLElement divElem, IVariableBox outerContext, MarkupEvaluationContext markupContext) {

				IVariableBox variables = markupContext.DrawingStateVariables().AppendVariables(outerContext);

				// Get setup information for this div element
				DivSetup setup = GetDivSetup(divElem, variables, markupContext, out string? id, out MarkupVariable[] divVariables);

				EvaluationNode? href = GetAttribute(divElem, "href", false, s => Evaluation.Parse(s, variables), null);
				WidgetReferenceExpression? hrefExpr = href is not null ? new WidgetReferenceExpression(href) : null;

				ChildDivElement childDivElement = new ChildDivElement(id, setup, hrefExpr, variables, markupContext, divVariables);

				LogVisit(divElem);

				return childDivElement;
			}

			private KeyedChildrenDivElement? MakeStyledDivElement(XMLElement root, XMLElement divElem, IVariableBox outerContext, MarkupEvaluationContext markupContext, Dictionary<XMLElement, IIdentifiableMarkupElement> constructed, DirectoryPath source) {

				// Get regex which determines which child elements are valid children
				Regex? namedChildRegex = MarkupParsingConstants.GetReferenceElementChildrenRegex(divElem.Name);

				IVariableBox variables = markupContext.DrawingStateVariables().AppendVariables(outerContext);

				// Get setup information for this div element
				DivSetup setup = GetDivSetup(divElem, variables, markupContext, out string? id, out MarkupVariable[] divVariables);

				// TODO This is so very hacky. Why can this not be a proper expression?
				//string? href = GetAttribute(divElem, "href", false, s => (string.IsNullOrWhiteSpace(s) ? null : s), null);
				EvaluationNode? href = GetAttribute(divElem, "href", false, s => Evaluation.Parse(s, variables), null);

				ContextExpression? shapeContext = MakeContextExpression(divElem, variables, "outline");
				TextExpression? titleText = GetAttribute(divElem, "name", false, s => Interpolation.Parse(s, variables, false), null);

				KeyedChildrenDivElement divElement;

				// TODO There are shape types to be added here
				if (divElem.Name == "box") {
					ShapeReferenceExpression<IBox>? hrefExpr = href is not null ? new ShapeReferenceExpression<IBox>(href) : null;

					BoxStyledDivElement boxDivElement = new BoxStyledDivElement(id, setup, shapeContext, hrefExpr, titleText, variables, markupContext, divVariables);
					divElement = boxDivElement;
				}
				else if (divElem.Name == "labelledBox") {
					ShapeReferenceExpression<ILabelledBox>? hrefExpr = href is not null ? new ShapeReferenceExpression<ILabelledBox>(href) : null;

					LabelledBoxStyledDivElement labelledBoxDivElement = new LabelledBoxStyledDivElement(id, setup, shapeContext, hrefExpr, titleText, variables, markupContext, divVariables);
					divElement = labelledBoxDivElement;
				}
				else if (divElem.Name == "titledBox") {
					ShapeReferenceExpression<ITitledBox>? hrefExpr = href is not null ? new ShapeReferenceExpression<ITitledBox>(href) : null;

					TitledBoxStyledDivElement titledBoxDivElement = new TitledBoxStyledDivElement(id, setup, shapeContext, hrefExpr, titleText, variables, markupContext, divVariables);
					divElement = titledBoxDivElement;
				}
				else if (divElem.Name == "bar") {
					ShapeReferenceExpression<IBar>? hrefExpr = href is not null ? new ShapeReferenceExpression<IBar>(href) : null;

					BarStyledDivElement barDivElement = new BarStyledDivElement(id, setup, shapeContext, hrefExpr, titleText, variables, markupContext, divVariables);
					divElement = barDivElement;
				}
				else if (divElem.Name == "usageBar") {
					ShapeReferenceExpression<IUsageBar>? hrefExpr = href is not null ? new ShapeReferenceExpression<IUsageBar>(href) : null;

					// TODO Why are these not TextExpressions?
					StringExpression? label1 = GetAttribute(divElem, "label1", false, s => StringExpression.Parse(s, variables), null);
					StringExpression? label2 = GetAttribute(divElem, "label2", false, s => StringExpression.Parse(s, variables), null);
					
					LabelDetailsExpression labelDetails = GetLabelDetails(divElem, variables, "labels");
					TextExpression? note = GetAttribute(divElem, "note", false, s => Interpolation.Parse(s, variables, false), null);
					LabelDetailsExpression noteDetails = GetLabelDetails(divElem, variables, "note");

					LabelledUsageBarStyledDivElement usageBarDivElement = new LabelledUsageBarStyledDivElement(
						id, setup, shapeContext, hrefExpr, titleText,
						label1, label2, labelDetails, note, noteDetails,
						variables, markupContext, divVariables);
					divElement = usageBarDivElement;
				}
				else if (divElem.Name == "detail") {
					ShapeReferenceExpression<IDetail>? hrefExpr = href is not null ? new ShapeReferenceExpression<IDetail>(href) : null;
					EnumExpression<LayoutDirection> direction = GetAttribute(divElem, "direction", false, s => MarkupValueParsing.ParseEnum<LayoutDirection>(s, variables), new EnumExpression<LayoutDirection>(LayoutDirection.ROWS, variables.Context));

					DetailStyledDivElement detailDivElement = new DetailStyledDivElement(id, setup, shapeContext, hrefExpr, titleText, direction, variables, markupContext, divVariables);
					divElement = detailDivElement;
				}
				else {
					LogError(divElem, "Unrecognised styled div type."); // This should never be reached
					return null;
				}

				foreach (XMLElement childElem in divElem.Elements) {
					if (namedChildRegex?.IsMatch(childElem.Name) ?? false) {
						DivElement child = MakeDivElement(root, childElem, divElement.Variables, markupContext, constructed, source);
						if (child != null) {
							divElement.AddNamedChild(childElem.Name, child);
							LogOrigin(childElem, child);
						}
						LogVisit(childElem);
					}
					else if (childElem.Name == "args" || childElem.Name == "defs") {
						// Ignore
					}
					else {
						InvalidChildElement(childElem);
					}
				}

				LogVisit(divElem);

				return divElement;
			}

			private IIdentifiableMarkupElement? MakeElement(XMLElement root, XMLElement elem, Dictionary<XMLElement, IIdentifiableMarkupElement> constructed, ISet<XMLElement> disallowed, IVariableBox initialVariables, MarkupEvaluationContext markupContext, DirectoryPath source) {
				// TODO Need to deal with errors in this method

				if (constructed.TryGetValue(elem, out IIdentifiableMarkupElement? existing)) { return existing; }

				IIdentifiableMarkupElement? finalElement = null;

				StyleSheet styleSheet = GetStyleSheet(root, elem, constructed, new HashSet<XMLElement>(disallowed) { elem }, initialVariables, markupContext, source, out IVariableBox variables);
				string? id = GetAttribute(elem, "id", false, s => s, null);
				//string classname = GetAttribute(elem, "class", false, s => s, null);

				if (elem.Name == "line") {
					finalElement = new Line(
						id, styleSheet,
						GetAttribute(elem, "x1", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "y1", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "x2", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "y2", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)));
					AssertLeafNode(elem);
				}
				else if (elem.Name == "rect") {
					finalElement = new Rect(
						id, styleSheet,
						GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "width", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "height", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "rx", false, s => FloatExpression.Parse(s, variables), null),
						GetAttribute(elem, "ry", false, s => FloatExpression.Parse(s, variables), null));
					AssertLeafNode(elem);
				}
				else if (elem.Name == "circle") {
					finalElement = new Elements.Circle(
						id, styleSheet,
						GetAttribute(elem, "cx", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "cy", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "r", false, s => FloatExpression.Parse(s, variables), new FloatExpression(0f, variables.Context)));
					AssertLeafNode(elem);
				}
				else if (elem.Name == "ellipse") {
					finalElement = new Elements.Ellipse(
						id, styleSheet,
						GetAttribute(elem, "cx", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "cy", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "rx", false, s => FloatExpression.Parse(s, variables), new FloatExpression(0f, variables.Context)),
						GetAttribute(elem, "ry", false, s => FloatExpression.Parse(s, variables), new FloatExpression(0f, variables.Context)));
					AssertLeafNode(elem);
				}
				else if (elem.Name == "polyline") {
					finalElement = new Polyline(id, styleSheet, GetAttribute(elem, "points", false, s => MarkupValueParsing.ParseDrawPoints(s, variables), Array.Empty<DrawPointExpression>()));
					AssertLeafNode(elem);
				}
				else if (elem.Name == "polygon") {
					finalElement = new Polygon(id, styleSheet, GetAttribute(elem, "points", false, s => MarkupValueParsing.ParseDrawPoints(s, variables), Array.Empty<DrawPointExpression>()));
					AssertLeafNode(elem);
				}
				else if (elem.Name == "path") {
					ContextProperty<string>? pathDataAttr = GetAttribute(elem, "d", false);
					finalElement = Elements.Path.Parse(id, styleSheet, pathDataAttr?.Value, variables, out (int index, int length, Exception ex)[] pathErrors);
					if (pathErrors != null && pathErrors.Length > 0 && pathDataAttr.HasValue) {
						foreach ((int index, int length, Exception ex) in pathErrors) {
							SharpParsingException offsetError = new SharpParsingException(
									new DocumentSpan(
										pathDataAttr.Value.ValueLocation.Offset + index + 1, // Why is the 1 necessary?
										pathDataAttr.Value.ValueLocation.Line,
										pathDataAttr.Value.ValueLocation.Column + index,
										length
										),
									ex.Message, ex
									);

							AddError(offsetError);
						}
					}
					AssertLeafNode(elem);
				}
				else if (elem.Name == "g") {
					List<IDrawableElement> gElements = new List<IDrawableElement>();
					foreach (XMLElement gElem in elem.Elements) {
						try {
							IIdentifiableMarkupElement? gBoxElem = MakeElement(root, gElem, constructed, new HashSet<XMLElement>(disallowed) { elem }, variables, markupContext, source); // TODO Deal with errors?
							if (gBoxElem is IDrawableElement gShapeElem) {
								gElements.Add(gShapeElem);
								LogOrigin(gElem, gShapeElem);
								LogVisit(gElem);
							}
							else {
								InvalidChildElement(gElem);
							}
						}
						catch (FormatException e) {
							LogError(gElem, e);
						}
						catch (SharpParsingException e) {
							LogError(gElem, e);
						}
					}
					finalElement = new Grouping(id, styleSheet, gElements);
				}
				else if (elem.Name == "text" || elem.Name == "textPath") {
					List<ITextPiece> pieces = new List<ITextPiece>();
					foreach (XMLNode node in elem.Children) {
						try {
							if (node is XMLElement textPieceElem && (textPieceElem.Name == "tspan" || textPieceElem.Name == "textPath")) {
								IIdentifiableMarkupElement? boxElem = MakeElement(root, textPieceElem, constructed, new HashSet<XMLElement>(disallowed) { elem }, variables, markupContext, source); // TODO Deal with errors?
								if (boxElem is ITextPiece textPiece) {
									pieces.Add(textPiece);
									LogOrigin(textPieceElem, boxElem);
								}
								else {
									InvalidChildElement(textPieceElem);
								}
								LogVisit(textPieceElem);
							}
							else if (node is XMLText textNode) {
								pieces.Add(new TSpan(null, styleSheet.WithoutForEach(), null, null, null, null, ParseText(textNode.Yield(), variables)));
								LogVisit(textNode);
							}
							else if (node is not XMLComment) {
								InvalidChildNode(node);
							}
						}
						catch (FormatException e) {
							LogError(node, e);
						}
						catch (SharpParsingException e) {
							LogError(node, e);
						}
					}

					if (elem.Name == "text") {
						finalElement = new Elements.Text(id, styleSheet,
							GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
							GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
							// Should dx and dy really be lengths?
							GetAttribute(elem, "dx", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), null),
							GetAttribute(elem, "dy", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), null),
							pieces
							);
					}
					else { // elem.Name.LocalName == "textPath"
						string? pathUrl = GetAttribute(elem, "path", false, s => s, null);
						IIdentifiableMarkupElement? path = EvaluateURL(root, pathUrl, true, constructed, new HashSet<XMLElement>(disallowed) { elem }, variables, markupContext, source);

						if (!pieces.All(p => p is TSpan)) {
							LogError(elem, "All child nodes of textPath must be tspan elements."); // Odd way of doing this. Shouldn't the error be on the child?
						}

						if (path is IShapeElement pathShape) {
							finalElement = new TextPath(id, styleSheet,
								pathShape,
								GetAttribute(elem, "startOffset", false, s => MarkupValueParsing.ParsePercentOrLength(s, variables), new LengthExpression(Length.Zero, variables.Context)),
								GetAttribute(elem, "side", false, s => MarkupValueParsing.ParseEnum<PathSide>(s, variables), new EnumExpression<PathSide>(PathSide.LEFT, variables.Context)),
								GetAttribute(elem, "continue", false, s => MarkupValueParsing.ParseEnum<ContinueStyle>(s, variables), new EnumExpression<ContinueStyle>(ContinueStyle.NONE, variables.Context)),
								pieces.OfType<TSpan>()
								);
						}
						else {
							LogError(elem, "Invalid path specified for textPath.");
						}
					}
				}
				else if (elem.Name == "textRect") {
					List<TSpan> tspans = new List<TSpan>();
					foreach (XMLNode node in elem.Children) {
						try {
							if (node is XMLElement textPieceElem && textPieceElem.Name == "tspan") {
								IIdentifiableMarkupElement? boxElem = MakeElement(root, textPieceElem, constructed, new HashSet<XMLElement>(disallowed) { elem }, variables, markupContext, source); // TODO Deal with errors?
								if (boxElem is TSpan tspan) {
									tspans.Add(tspan);
									LogVisit(textPieceElem);
								}
								else {
									InvalidChildElement(textPieceElem);
								}
							}
							else if (node is XMLText textNode) {
								tspans.Add(new TSpan(null, styleSheet.WithoutForEach(), null, null, null, null, ParseText(textNode.Yield(), variables)));
								LogVisit(textNode);
							}
							else if (node is not XMLComment) {
								InvalidChildNode(node);
							}
						}
						catch (FormatException e) {
							LogError(node, e);
						}
						catch (SharpParsingException e) {
							LogError(node, e);
						}
					}
					finalElement = new TextRect(id, styleSheet,
						GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "width", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), markupContext.WidthExpression),
						GetAttribute(elem, "height", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), markupContext.HeightExpression),
						GetAttribute(elem, "fit-text", false, s => BoolExpression.Parse(s, variables), new BoolExpression(false, variables.Context)),
						GetAttribute(elem, "min-font-size", true, s => FloatExpression.Parse(s, variables), null),
						GetAttribute(elem, "max-font-size", true, s => FloatExpression.Parse(s, variables), null),
						GetAttribute(elem, "justification", true, s => MarkupValueParsing.ParseEnum<Justification>(s, variables), new EnumExpression<Justification>(Justification.LEFT, variables.Context)),
						GetAttribute(elem, "alignment", true, s => MarkupValueParsing.ParseEnum<SharpSheets.Canvas.Text.Alignment>(s, variables), new EnumExpression<SharpSheets.Canvas.Text.Alignment>(SharpSheets.Canvas.Text.Alignment.BOTTOM, variables.Context)),
						GetAttribute(elem, "height-strategy", true, s => MarkupValueParsing.ParseEnum<TextHeightStrategy>(s, variables), new EnumExpression<TextHeightStrategy>(TextHeightStrategy.LineHeightBaseline, variables.Context)),
						GetAttribute(elem, "line-spacing", true, s => FloatExpression.Parse(s, variables), new FloatExpression(1.0f, variables.Context)),
						GetAttribute(elem, "paragraph-spacing", true, s => FloatExpression.Parse(s, variables), new FloatExpression(0.0f, variables.Context)),
						GetAttribute(elem, "single-line", false, s => BoolExpression.Parse(s, variables), new BoolExpression(false, variables.Context)),
						tspans
						);
				}
				else if (elem.Name == "tspan") {
					//string textContent = string.Join("", GetTextNodes(elem).Select(n => n.Value));
					finalElement = new TSpan(id, styleSheet,
						GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), null),
						GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), null),
						GetAttribute(elem, "dx", false, s => FloatExpression.Parse(s, variables), null),
						GetAttribute(elem, "dy", false, s => FloatExpression.Parse(s, variables), null),
						ParseText(GetTextNodes(elem), variables)
						);
					AssertLeafNode(elem, true);
				}
				else if (elem.Name == "image") {
					if (RequiredAttribute(elem, "file", false, s => MarkupValueParsing.ParseFilePath(s, source, variables), out ContextProperty<FilePathExpression> imagePath)) {
						if (imagePath.Value != null) {
							try {
								//ImageData imageData = new ImageData(new FilePath(imagePath.Value.Path));

								finalElement = new Image(
									id, styleSheet,
									GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
									GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
									GetAttribute(elem, "width", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), markupContext.WidthExpression),
									GetAttribute(elem, "height", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), markupContext.HeightExpression),
									imagePath.Value,
									GetAttribute(elem, "preserveAspectRatio", false, s => MarkupValueParsing.ParsePreserveAspectRatio(s, variables), new PreserveAspectRatioExpression(PreserveAspectRatio.Default, variables.Context)));
							}
							catch (System.IO.FileNotFoundException e) {
								LogError(imagePath, "Provided filepath not found.", e);
							}
							catch (ArgumentException e) {
								LogError(imagePath, "Could not open provided filepath", e);
							}
						}
						else {
							LogError(imagePath, "Invalid filepath value.");
						}
					}
					AssertLeafNode(elem);
				}
				else if (elem.Name == "clipPath") {
					List<IShapeElement> clipElements = new List<IShapeElement>();
					foreach (XMLElement clipElem in elem.Elements) {
						try {
							IIdentifiableMarkupElement? gBoxElem = MakeElement(root, clipElem, constructed, new HashSet<XMLElement>(disallowed) { elem }, variables, markupContext, source); // TODO Deal with errors?
							if (gBoxElem is IShapeElement gShapeElem) {
								clipElements.Add(gShapeElem);
							}
							else {
								InvalidChildElement(clipElem);
							}
							LogVisit(clipElem);
						}
						catch (FormatException e) {
							LogError(clipElem, e);
						}
						catch (SharpParsingException e) {
							LogError(clipElem, e);
						}
					}
					finalElement = new ClipPath(id, styleSheet, clipElements);
				}
				else if (elem.Name == "symbol") {
					List<IShapeElement> symbolElements = new List<IShapeElement>();
					foreach (XMLElement symbolElem in elem.Elements) {
						try {
							IIdentifiableMarkupElement? gBoxElem = MakeElement(root, symbolElem, constructed, new HashSet<XMLElement>(disallowed) { elem }, variables, markupContext, source); // TODO Deal with errors?
							if (gBoxElem is IShapeElement gShapeElem) {
								symbolElements.Add(gShapeElem);
							}
							else {
								InvalidChildElement(symbolElem);
							}
							LogVisit(symbolElem);
						}
						catch (FormatException e) {
							LogError(symbolElem, e);
						}
						catch (SharpParsingException e) {
							LogError(symbolElem, e);
						}
					}
					finalElement = new Symbol(
						id, styleSheet,
						GetAttribute(elem, "viewBox", false, s => MarkupValueParsing.ParseRectangle(s, variables), null),
						GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "width", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "height", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "preserveAspectRatio", false, s => MarkupValueParsing.ParsePreserveAspectRatio(s, variables), new PreserveAspectRatioExpression(PreserveAspectRatio.Default, variables.Context)),
						symbolElements);
				}
				else if (elem.Name == "use") {

					if (RequiredAttribute(elem, "href", false, s => s, out ContextProperty<string> urlAttr)) {
						XMLElement? referenced = EvalualteURLElement(root, urlAttr.Value, new HashSet<XMLElement>(disallowed) { elem });
						if (referenced != null) {
							LogVisit(referenced);
							foreach (XMLNode referencedNode in referenced.TraverseNodes()) {
								LogVisit(referencedNode);
							}

							Dictionary<string, ContextProperty<string>> replacementAttributes = new Dictionary<string, ContextProperty<string>>(referenced.Attributes.ToDictionary(a => a.Name));

							if (referenced.Name == "symbol") {
								if (GetAttribute(elem, "width", false) is ContextProperty<string> widthAttr) {
									replacementAttributes["width"] = widthAttr;
								}
								if (GetAttribute(elem, "height", false) is ContextProperty<string> heightAttr) {
									replacementAttributes["height"] = heightAttr;
								}
							}
							foreach (ContextProperty<string> attr in elem.Attributes) {
								if (MarkupParsingConstants.IsIgnoredUseCloneAttribute(attr.Name)) {
									continue;
								}
								else if (!referenced.HasAttribute(attr.Name, false)) { // This doesn't seem right...
									replacementAttributes[attr.Name] = attr;
									LogVisit(elem, attr.Name);
								}
							}

							XMLElement cloned = referenced.Clone(elem, replacementAttributes);

							IIdentifiableMarkupElement? clonedElement = MakeElement(root, cloned, constructed, new HashSet<XMLElement>(disallowed) { elem }, variables, markupContext, source);

							if (clonedElement is IDrawableElement drawableHref) {
								TransformExpression translation = TransformExpression.Translate(
									GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
									GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)));
								TransformExpression finalTransform = translation * (styleSheet.Transform ?? TransformExpression.Identity(variables.Context));

								StyleSheet finalStyleSheet = styleSheet.Update(transform: finalTransform);

								finalElement = new Grouping(id, finalStyleSheet, new IDrawableElement[] { drawableHref });
								//LogOrigin(referenced, finalElement); // To link the original element to the created copy // Why doesn't this work?
								LogOrigin(cloned, drawableHref);
							}
							else {
								LogError(urlAttr, $"Invalid reference element for \"use\" (must be a drawable element).");
							}
						}
						else {
							LogError(urlAttr, $"\"{urlAttr.Value}\" could not be found.");
						}
					}
				}
				else if (elem.Name == "textField") {
					finalElement = new TextField(id, styleSheet,
						GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "width", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), markupContext.WidthExpression),
						GetAttribute(elem, "height", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), markupContext.HeightExpression),
						GetAttribute<IExpression<string>>(elem, "name", false, s => Interpolation.Parse(s, variables, false), new StringExpression("NAME", variables.Context)),
						GetAttribute(elem, "tooltip", false, s => Interpolation.Parse(s, variables, false), null),
						GetAttribute(elem, "field-type", false, s => MarkupValueParsing.ParseEnum<TextFieldType>(s, variables), new EnumExpression<TextFieldType>(TextFieldType.STRING, variables.Context)),
						GetAttribute(elem, "value", false, s => Interpolation.Parse(s, variables, false), null),
						GetAttribute(elem, "multiline", false, s => BoolExpression.Parse(s, variables), new BoolExpression(false, variables.Context)),
						GetAttribute(elem, "rich", false, s => BoolExpression.Parse(s, variables), new BoolExpression(false, variables.Context)),
						GetAttribute(elem, "justification", false, s => MarkupValueParsing.ParseEnum<Justification>(s, variables), new EnumExpression<Justification>(Justification.LEFT, variables.Context)),
						GetAttribute(elem, "rotation", false, s => MarkupValueParsing.ParseEnum<FieldRotation>(s, variables), new EnumExpression<FieldRotation>(FieldRotation.UP, variables.Context)),
						GetAttribute(elem, "max-len", false, s => IntExpression.Parse(s, variables), new IntExpression(-1, variables.Context))
						);
					AssertLeafNode(elem, true); // TODO Shouldn't we do something with text nodes here?
				}
				else if (elem.Name == "checkField") {
					// TODO There should be some way of specifying whether the field should be checked by default
					finalElement = new CheckField(id, styleSheet,
						GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "width", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), markupContext.WidthExpression),
						GetAttribute(elem, "height", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), markupContext.HeightExpression),
						GetAttribute<IExpression<string>>(elem, "name", false, s => Interpolation.Parse(s, variables, false), new StringExpression("NAME", variables.Context)),
						GetAttribute(elem, "tooltip", false, s => Interpolation.Parse(s, variables, false), null),
						GetAttribute(elem, "check-type", false, s => MarkupValueParsing.ParseEnum<CheckType>(s, variables), new EnumExpression<CheckType>(CheckType.CROSS, variables.Context))
						);
					AssertLeafNode(elem);
				}
				else if (elem.Name == "imageField") {
					// TODO Default image?
					finalElement = new ImageField(id, styleSheet,
						GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "width", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), markupContext.WidthExpression),
						GetAttribute(elem, "height", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), markupContext.HeightExpression),
						GetAttribute<IExpression<string>>(elem, "name", false, s => Interpolation.Parse(s, variables, false), new StringExpression("NAME", variables.Context)),
						GetAttribute(elem, "tooltip", false, s => Interpolation.Parse(s, variables, false), null)
						);
					AssertLeafNode(elem);
				}
				else {
					LogError(elem, "Unrecognized element type.");
				}

				if (finalElement != null) {
					constructed.Add(elem, finalElement);
				}
				return finalElement;
			}

			//private static readonly SolidPaint defaultFillPaint = new SolidPaint(null, variables.MarkupContext.BackgroundExpression);
			//private static readonly SolidPaint defaultTextPaint = new SolidPaint(null, variables.MarkupContext.TextColorExpression);

			private StyleSheet GetStyleSheet(XMLElement root, XMLElement elem, Dictionary<XMLElement, IIdentifiableMarkupElement> constructed, ISet<XMLElement> disallowed, IVariableBox initialVariables, MarkupEvaluationContext markupContext, DirectoryPath source, out IVariableBox finalVariables) {

				// Get foreach expression for this div, using outer context variables
				ForEachExpression? forEach = GetAttribute(elem, "for-each", false, s => ForEachExpression.Parse(s, initialVariables), null);

				IVariableBox variables;
				if (forEach != null) {
					// If for-each provided, add it's variable to the collection used for parsing
					variables = initialVariables.AppendVariables(forEach.Variable);
				}
				else {
					variables = initialVariables;
				}

				StyleSheet styleSheet = new StyleSheet(
					_clip_path: GetClipPath(root, elem, constructed, disallowed, variables, markupContext, source),
					clip_rule: GetAttribute(elem, "clip-rule", true, s => MarkupValueParsing.ParseEnum<AreaRule>(s, variables), null),
					//Color = GetAttribute(elem, "color", true, s => MarkupValueParsing.ParseColor(s, variables), null),
					//Fill = GetAttribute(elem, "fill", true, s => GetPaint(root, s, variables), new SolidPaint(Color.Black)),
					fill: GetPaint(root, elem, "fill", true, disallowed, variables, markupContext, new SolidPaint(null, markupContext.BackgroundExpression)), // elem.GetAttribute("fill", true) is ContextProperty<string> fillAttr ? GetPaint(root, fillAttr, variables) : new SolidPaint(Color.Black),
					fill_opacity: GetAttribute(elem, "fill-opacity", true, s => FloatExpression.Parse(s, variables), null),
					fill_rule: GetAttribute(elem, "fill-rule", true, s => MarkupValueParsing.ParseEnum<AreaRule>(s, variables), null),
					//FontFamily = null, // TODO How to get this?
					font_size: GetAttribute(elem, "font-size", true, s => FloatExpression.Parse(s, variables), null), // TODO Special parsing? (e.g. em?)
					font_style: GetAttribute(elem, "font-style", true, s => MarkupValueParsing.ParseEnum<TextFormat>(s, variables), new EnumExpression<TextFormat>(TextFormat.REGULAR, variables.Context)),
					//MarkerEnd = null,
					//MarkerMid = null,
					//MarkerStart = null,
					//_opacity: GetAttribute(elem, "opacity", false, s => FloatExpression.Parse(s, variables), null),
					//Overflow = GetAttribute(elem, "overflow", false, s => MarkupValueParsing.ParseEnum<Overflow>(s, variables), null),
					//StopColor = GetAttribute(elem, "stop-color", false, s => ParseColor(s), null), // Not inherited
					//StopOpacity = GetAttribute(elem, "stop-opacity", false, s => FloatExpression.Parse(s, variables), null), // Not inherited
					stroke: GetPaint(root, elem, "stroke", true, disallowed, variables, markupContext, null), // GetAttribute(elem, "stroke", true, s => MarkupValueParsing.ParseColor(s, variables), null),
					stroke_dasharray: GetAttribute(elem, "stroke-dasharray", true, s => MarkupValueParsing.ParseSVGNumbers(s, variables), null),
					stroke_dashoffset: GetAttribute(elem, "stroke-dashoffset", true, s => FloatExpression.Parse(s, variables), null),
					stroke_linecap: GetAttribute(elem, "stroke-linecap", true, s => MarkupValueParsing.ParseEnum<LineCapStyle>(s, variables), null), // Default: CanvasConstants.LineCapStyle.BUTT
					stroke_linejoin: GetAttribute(elem, "stroke-linejoin", true, s => MarkupValueParsing.ParseEnum<LineJoinStyle>(s, variables), null), // Default: CanvasConstants.LineJoinStyle.MITER
					stroke_miterlimit: GetAttribute(elem, "stroke-miterlimit", true, s => FloatExpression.Parse(s, variables), null),
					stroke_opacity: GetAttribute(elem, "stroke-opacity", true, s => FloatExpression.Parse(s, variables), null),
					stroke_width: GetAttribute(elem, "stroke-width", true, s => FloatExpression.Parse(s, variables), null),
					text_anchor: GetAttribute(elem, "text-anchor", true, s => MarkupValueParsing.ParseEnum<TextAnchor>(s, variables), new EnumExpression<TextAnchor>(TextAnchor.Start, variables.Context)),
					//TextBaseline = GetAttribute(elem, "text-baseline", true, s => MarkupValueParsing.ParseEnum<TextBaseline>(s, variables), TextBaseline.Bottom),
					text_color: GetAttribute(elem, "text-color", true, s => MarkupValueParsing.ParseColor(s, variables), markupContext.TextColorExpression),
					_transform: GetAttribute(elem, "transform", false, s => MarkupValueParsing.ParseTransform(s, variables), null),
					drawing_coords: GetAttribute(elem, "coords", true, s => MarkupValueParsing.ParseEnum<DrawingCoords>(s, variables), null),
					_enabled: GetAttribute(elem, "enabled", false, s => BoolExpression.Parse(s, variables), new BoolExpression(true, variables.Context)),
					_for_each: forEach
				);

				finalVariables = variables;
				return styleSheet;
			}

			#endregion

			#region IMarkupVariable

			//private static readonly Regex variableRegex = new Regex(@"[a-z][a-z0-9]*", RegexOptions.IgnoreCase);
			private bool ValidateVariableName(XMLElement element, IVariableBox variables, bool isArg, bool isNested, out ContextProperty<EvaluationName> nameAttribute, out ContextProperty<EvaluationName>? variableNameAttribute) {
				string GetVarTypeString() {
					return isArg ? "argument" : "variable";
				}

				bool success = false;
				nameAttribute = default;
				variableNameAttribute = null;

				ContextProperty<string>? nameAttr = GetAttribute(element, "name", false);
				if (nameAttr.HasValue) {
					string name = nameAttr.Value.Value;
					if (EvaluationName.IsValid(name)) {
						EvaluationName evalName = new EvaluationName(name);
						nameAttribute = MakeProperty(nameAttr.Value, evalName);
						success = true;
					}
					else {
						LogError(nameAttr.Value, $"Invalid {GetVarTypeString()} name (must start with a letter, and comprise only letters and numbers).");
					}
				}
				else {
					LogError(element, GetVarTypeString().ToTitleCase() + "s must have a name.");
				}

				ContextProperty<string>? varNameAttr = GetAttribute(element, "variable", false);
				if (varNameAttr.HasValue) {
					string varName = varNameAttr.Value.Value;
					if (EvaluationName.IsValid(varName)) {
						EvaluationName evalVarName = new EvaluationName(varName);
						variableNameAttribute = MakeProperty(varNameAttr.Value, evalVarName);
					}
					else {
						LogError(varNameAttr.Value, $"Invalid {GetVarTypeString()} name (must start with a letter, and comprise only letters and numbers).");
					}
				}

				if (success && !isNested) {
					// We found names, and so we must check that the variable name we're using doesn't conflict with keywords

					ContextProperty<EvaluationName> varName = variableNameAttribute ?? nameAttribute;
					bool foundError = false;

					if (Evaluation.IsLangKeyword(varName.Value)) {
						LogError(varName, $"Variable name \"{varName.Value}\" conflicts with evaluation keyword.");
						foundError = true;
					}
					else if (variables.Context.IsType(varName.Value)) {
						LogError(varName, $"Variable name \"{varName.Value}\" conflicts with type name.");
						foundError = true;
					}
					else if (variables.IsVariable(varName.Value)) {
						LogError(varName, $"Variable name \"{varName.Value}\" conflicts with existing variable name.");
						foundError = true;
					}

					if (foundError) {
						if (variableNameAttribute.HasValue) {
							variableNameAttribute = null;
						}
						success = false;
					}
				}

				return success;
			}

			private MarkupValidation? MakeValidation(XMLElement elem, IVariableBox variables) {
				if (RequiredAttribute(elem, "test", false, t => BoolExpression.Parse(t, variables), out ContextProperty<BoolExpression> validation)) {
					string? message = GetAttribute(elem, "message", false, s => s, null);

					return new MarkupValidation(validation.Value, message);
				}

				return null;
			}

			private List<EvaluationType> CollectArgumentTypes(XMLElement elem, EvaluationContext.Builder contextBuilder) {
				if (elem.Name != "arg" && elem.Name != "grouparg") {
					throw new InvalidOperationException("Invalid argument element tag."); // This should never happen
				}

				List<EvaluationType> collected = new List<EvaluationType>();

				if (elem.Name == "arg" && GetAttribute(elem, "type", false) is ContextProperty<string> typeProp) {
					List<XMLElement> options = new List<XMLElement>();
					foreach (XMLElement opt in elem.FindElements("option")) {
						if (RequiredAttribute(opt, "name", false, s => s, out ContextProperty<string> optNameAttr)) {
							GetAttribute(opt, "desc", false);
							options.Add(opt);
						}
						LogVisit(opt);
					}

					if (options.Count > 0) {
						if (contextBuilder.IsDefined(typeProp.Value)) {
							LogError(typeProp, "Cannot name a custom enum after an existing type.");
							return collected; // Exit early
						}

						string? description = GetAttribute(elem, "desc", false, s => s, null);

						Type customEnumType = MakeCustomEnumType(typeProp.Value, description, options.ToArray());

						EnumEvaluationType evalType = contextBuilder.SetType(customEnumType, ctx => EnumEvaluationType.FromSystemType(ctx, customEnumType));

						collected.Add(evalType);
					}
				}
				else { // elem.Name == "grouparg"
					foreach (XMLElement child in elem.Elements.Where(e => e.Name == "arg" || e.Name == "grouparg")) {
						foreach (EvaluationType childType in CollectArgumentTypes(child, contextBuilder)) {
							collected.Add(childType);
						}

						//LogVisit(child);
					}
				}

				return collected;
			}

			private static MarkupEnumType MakeCustomEnumType(string typeName, string? description, XMLElement[] options) {
				EnumValDoc[] enumVals = options.Select(opt => {
					string? valName = opt.GetAttribute1("name", false)?.Value;
					if (valName is null) { return null; }
					string? valDoc = opt.GetAttribute1("desc", false)?.Value;
					return new EnumValDoc(typeName, valName, !string.IsNullOrWhiteSpace(valDoc) ? new DocumentationString(valDoc) : null);
				}).WhereNotNull().ToArray();

				return new MarkupEnumType(typeName, description, enumVals);
			}

			private IMarkupArgument? MakeArgument(XMLElement elem, bool isNested, MarkupEvaluationContext context, out ContextProperty<EvaluationName> nameAttribute, out ContextProperty<EvaluationName>? varNameAttribute) {
				if(elem.Name != "arg" && elem.Name != "grouparg") {
					throw new InvalidOperationException("Invalid argument element tag."); // This should never happen
				}

				IVariableBox argExistingVariables = context.DrawingStateVariables();

				if (ValidateVariableName(elem, argExistingVariables, true, isNested, out nameAttribute, out varNameAttribute)) {
					if (elem.Name == "arg") {
						return MakeSingleArgument(elem, context, nameAttribute, varNameAttribute, true);
					}
					else { // elem.Name == "grouparg"
						return MakeGroupArgument(elem, context, nameAttribute, varNameAttribute);
					}
				}

				nameAttribute = default;
				return null;
			}

			private static IVariableBox MakeValidateVariables(ContextProperty<EvaluationName> name, ContextProperty<EvaluationName>? variableName, EvaluationType type) {
				return VariableBoxes.Concat(
					VariableBoxes.Single(
						new EnvironmentVariableInfo(variableName?.Value ?? name.Value, type, null)
						, type.Context),
					BasisEnvironment.MakeInstance(MarkupEvaluationTypes.BaseContext)
					);
			}
			private static IEnvironment MakeValidateEnvironment(ContextProperty<EvaluationName> name, ContextProperty<EvaluationName>? variableName, EvaluationType type, object? exampleValue) {
				return Environments.Concat(
					Environments.Single(
						new EnvironmentVariableInfo((variableName ?? name).Value.ToString(), type, null),
						new EvaluationValue(exampleValue, type)
						),
					BasisEnvironment.MakeInstance(MarkupEvaluationTypes.BaseContext)
					);
			}

			private MarkupSingleArgument? MakeSingleArgument(XMLElement elem, MarkupEvaluationContext context, ContextProperty<EvaluationName> name, ContextProperty<EvaluationName>? variableName, bool allowEntries) {

				string? description = GetAttribute(elem, "desc", false, s => s, null);
				if (RequiredAttribute(elem, "type", false, s => MarkupEvaluationTypes.ParseArgumentType(s, description, context.TypeSystem), out ContextProperty<EvaluationType> typeAttr)) {
					EvaluationType type = typeAttr.Value;
					object? defaultValue = GetAttribute(elem, "default", false, s => EvaluateArgDefault(s, type), null);
					bool isOptional = GetAttribute(elem, "optional", false, s => MarkupValueParsing.ParseConcreteBool(s), false) || elem.HasAttribute("default", false);
					object? exampleValue = GetAttribute(elem, "example", false, s => EvaluateArgDefault(s, type), null);
					BoolExpression? validationExpr = GetAttribute(elem, "validate", false, v => BoolExpression.Parse(v, MakeValidateVariables(name, variableName, type)), null);
					string? validationMessage = GetAttribute(elem, "validate-message", false, s => s, null);
					bool useLocal = GetAttribute(elem, "local", false, s => MarkupValueParsing.ParseConcreteBool(s), false);
					MarkupArgumentFormat format = GetAttribute(elem, "format", false, s => EnumUtils.ParseEnum<MarkupArgumentFormat>(s), MarkupArgumentFormat.DEFAULT);

					if (defaultValue != null && validationExpr != null && !ArgValidationResult(elem, validationExpr, defaultValue, type, name, variableName)) {
						defaultValue = null;
						if (GetAttribute(elem, "default", false) is ContextProperty<string> attr) {
							LogError(attr, $"Default for {name.Value} is not a valid value.");
							if (!string.IsNullOrWhiteSpace(validationMessage)) {
								LogError(attr, validationMessage);
							}
						}
					}

					if (exampleValue != null && validationExpr != null && !ArgValidationResult(elem, validationExpr, exampleValue, type, name, variableName)) {
						exampleValue = null;
						if (GetAttribute(elem, "example", false) is ContextProperty<string> attr) {
							LogError(attr, $"Example for {name.Value} is not a valid value.");
							if (!string.IsNullOrWhiteSpace(validationMessage)) {
								LogError(attr, validationMessage);
							}
						}
					}

					if (!allowEntries && format == MarkupArgumentFormat.ENTRIES) {
						format = MarkupArgumentFormat.DEFAULT;
						if (GetAttribute(elem, "format", false) is ContextProperty<string> attr) {
							LogError(attr, "Entries not allowed for this argument.");
						}
					}

					if((format == MarkupArgumentFormat.ENTRIES || format == MarkupArgumentFormat.NUMBERED) && type is not ArrayEvaluationType) { // TODO Is this check right?
						if (GetAttribute(elem, "format", false) is ContextProperty<string> attr) {
							if (format == MarkupArgumentFormat.ENTRIES) {
								LogError(attr, $"Values taken from context entries must be parsed into a variable length array, not {type.Name}.");
							}
							else { // format == MarkupArgumentFormat.NUMBERED
								LogError(attr, $"Numbered argument types must be parsed into a variable length array, not {type.Name}.");
							}
						}
						format = MarkupArgumentFormat.DEFAULT;
					}

					MarkupSingleArgument argument = new MarkupSingleArgument(name.Value, type, variableName?.Value, description, isOptional, defaultValue, exampleValue, validationExpr, validationMessage, useLocal, format);

					return argument;
				}

				return null;
			}

			private bool ArgValidationResult(XMLElement elem, BoolExpression validationExpr, object exampleValue, EvaluationType argType, ContextProperty<EvaluationName> name, ContextProperty<EvaluationName>? varName) {
				try {
					IEnvironment validationEnvironment = MakeValidateEnvironment(name, varName, argType, exampleValue);
					return validationExpr.Evaluate(validationEnvironment);
				}
				catch(EvaluationException e) {
					LogError(elem, "Error encountered while processing validation expression.", e);
					return false;
				}
			}

			private MarkupGroupArgument? MakeGroupArgument(XMLElement elem, MarkupEvaluationContext context, ContextProperty<EvaluationName> groupname, ContextProperty<EvaluationName>? variableName) {

				string? description = GetAttribute(elem, "desc", false, s => s, null);

				List<IMarkupArgument> args = new List<IMarkupArgument>();
				HashSet<EvaluationName> argNames = new HashSet<EvaluationName>();
				HashSet<EvaluationName> varNames = new HashSet<EvaluationName>();

				foreach (XMLElement child in elem.Elements.Where(e => e.Name == "arg" || e.Name == "grouparg")) {
					if (MakeArgument(child, true, context, out ContextProperty<EvaluationName> argName, out ContextProperty<EvaluationName>? argVarName) is IMarkupArgument childArg) {
						if (argNames.Contains(childArg.ArgumentName)) {
							LogError(argName, "An argument with this name already exists in this grouping.");
						}
						else if (varNames.Contains(childArg.VariableName)) {
							LogError(argName, "An argument with this variable name already exists in this grouping.");
						}
						else {
							args.Add(childArg);
							argNames.Add(childArg.ArgumentName);
							varNames.Add(childArg.VariableName);
						}
					}

					LogVisit(child);
				}

				if(args.Count == 0) {
					LogError(elem, "No valid child arguments found for grouping.");
					return null;
				}

				EvaluationType groupArgType = MarkupEvaluationTypes.MakeGroupType(
					(variableName ?? groupname).Value.ToString(),
					args,
					context.TypeSystem);

				return new MarkupGroupArgument(groupname.Value, groupArgType, variableName?.Value, description, args);
			}

			private object? EvaluateArgDefault(string text, EvaluationType argType) {
				if (text == null) {
					return null;
				}
				else if (typeof(IShape).IsAssignableFrom(argType.DataType)) {
					return shapeFactory.MakeExample(argType.DataType, text, source, out _);
				}
				else if(argType.IterationResult() is EvaluationType elementType && typeof(IShape).IsAssignableFrom(elementType.DataType)) {
					if(ParameterParsers.Parser_String1(text) is string[] parts) {
						return parts.Select(p => shapeFactory.MakeExample(elementType.DataType, p, source, out _)).ToArray();
					}
					else {
						return null;
					}
				}
				else {
					return MarkupArgumentParsing.ParseValue(text, argType, source).Value;
				}
			}

			private class XElementVariables : IVariableBox {

				public static MarkupVariable[] GetMarkupVariables(ParsedPatternDocument document, XMLElement elem, IVariableBox outerContext, MarkupEvaluationContext markupContext) {

					Dictionary<EvaluationName, XMLElement> validElements = new Dictionary<EvaluationName, XMLElement>();

					IVariableBox validationExistingVariables = markupContext.DrawingStateVariables();

					foreach (XMLElement varElem in GetVariableElements(elem, VariableType.VARIABLE, true)) {
						if(document.ValidateVariableName(varElem, validationExistingVariables, false, false, out ContextProperty<EvaluationName> nameAttr, out _)) {
							if (validElements.ContainsKey(nameAttr.Value) || outerContext.IsVariable(nameAttr.Value)) {
								document.LogError(varElem, "There already exists a variable with this name.");
							}
							else if (outerContext.Context.IsType(nameAttr.Value)) {
								document.LogError(varElem, "Variable name conflicts with type name.");
							}
							else if (Evaluation.IsLangKeyword(nameAttr.Value)) {
								document.LogError(varElem, "Variable name conflicts with evaluation keyword.");
							}
							else {
								validElements.Add(nameAttr.Value, varElem);
								document.LogVisit(varElem);
							}
						}
					}

					XElementVariables elementVariables = new XElementVariables(document, validElements, outerContext, markupContext);
					foreach(KeyValuePair<EvaluationName, XMLElement> validElem in validElements) {
						try {
							elementVariables.GetVariable(validElem.Key, out _);
						}
						catch (EvaluationException e) {
							document.LogError(validElem.Value, "Error parsing variable.", e);
						}
					}

					return elementVariables.alreadyChecked.Values.ToArray();
				}

				private readonly ParsedPatternDocument document;
				private readonly IVariableBox existing;

				private readonly Dictionary<EvaluationName, XMLElement> variableElements;

				private readonly HashSet<EvaluationName> checking;
				private readonly Dictionary<EvaluationName, MarkupVariable> alreadyChecked;

				public bool IsEmpty => variableElements.Count == 0 && existing.IsEmpty;
				public EvaluationContext Context => existing.Context;

				private XElementVariables(ParsedPatternDocument document, Dictionary<EvaluationName, XMLElement> variableElements, IVariableBox existing, MarkupEvaluationContext markupContext) {
					this.document = document;
					this.existing = existing.AppendVariables(markupContext.DrawingStateVariables());
					this.variableElements = variableElements;
					this.checking = new HashSet<EvaluationName>();
					alreadyChecked = new Dictionary<EvaluationName, MarkupVariable>();
				}

				/// <summary></summary>
				/// <exception cref="EvaluationProcessingException"></exception>
				/// <exception cref="UndefinedVariableException"></exception>
				private void GetVariable(EvaluationName key, out MarkupVariable variable) {
					if (alreadyChecked.TryGetValue(key, out MarkupVariable? existingVariable)) {
						variable = existingVariable;
						return;
					}

					if(variableElements.TryGetValue(key, out XMLElement? elem)) {
						if (checking.Contains(key)) { throw new EvaluationProcessingException("Recursion loop encountered."); } // TODO Should this be a more specific exception type?
						checking.Add(key);

						EvaluationName name = document.GetAttribute(elem, "name", false, s => new EvaluationName(s), default);
						ContextProperty<string>? valueAttr = document.GetAttribute(elem, "value", false);
						if (!valueAttr.HasValue || string.IsNullOrWhiteSpace(valueAttr.Value.Value)) {
							document.LogError(elem, "Variables must have a value expression.");
							checking.Remove(key);
							throw new UndefinedVariableException(key);
						}

						EvaluationNode evaluation;
						try {
							evaluation = Evaluation.Parse(valueAttr.Value.Value, this);
						}
						catch (EvaluationException e) {
							document.LogError(valueAttr.Value, "Error parsing variable expression: " + e.Message, e);
							checking.Remove(key);
							throw new UndefinedVariableException(key);
						}

						variable = new MarkupVariable(name, evaluation);

						checking.Remove(key);
						alreadyChecked.Add(key, variable);
					}
					else {
						throw new UndefinedVariableException(key);
					}
				}

				public IEnumerable<EnvironmentVariableInfo> GetVariables() {
					throw new NotSupportedException();
					//return existing.GetVariables().Concat(variableElements.Keys).Distinct();
				}

				public bool TryGetVariableInfo(EvaluationName key, [MaybeNullWhen(false)] out EnvironmentVariableInfo variableInfo) {
					if (existing.TryGetVariableInfo(key, out variableInfo)) {
						return true;
					}
					else {
						try {
							GetVariable(key, out MarkupVariable variable);
							variableInfo = new EnvironmentVariableInfo(key, variable.Type, null);
							return true;
						}
						catch (EvaluationProcessingException) {
							throw new UndefinedVariableException(key);
						}
					}
				}

				public bool TryGetNode(EvaluationName key, [MaybeNullWhen(false)] out EvaluationNode node) {
					if (existing.TryGetNode(key, out node)) {
						return true;
					}
					else {
						try {
							GetVariable(key, out MarkupVariable variable);
							node = variable?.Evaluation;
							return node is not null;
						}
						catch (UndefinedVariableException) {
							node = null;
							return false;
						}
						catch (EvaluationProcessingException) {
							node = null;
							return false;
						}
					}
				}

				public bool TryGetFunctionInfo(EvaluationName name, [MaybeNullWhen(false)] out IEnvironmentFunctionInfo functionInfo) {
					return existing.TryGetFunctionInfo(name, out functionInfo);
				}
				public IEnumerable<IEnvironmentFunctionInfo> GetFunctionInfos() {
					return existing.GetFunctionInfos();
				}
			}

			#endregion

			#region SVG Elements

			private ClipPath? GetClipPath(XMLElement root, XMLElement elem, Dictionary<XMLElement, IIdentifiableMarkupElement> constructed, ISet<XMLElement> disallowed, IVariableBox variables, MarkupEvaluationContext markupContext, DirectoryPath source) {
				ClipPath? clipPath = null;

				if (GetAttribute(elem, "clip-path", false) is ContextProperty<string> clipPathAttr) {
					if (EvaluateURL(root, clipPathAttr.Value, false, constructed, disallowed, variables, markupContext, source) is IIdentifiableMarkupElement identified) {
						if (identified is ClipPath identifiedClipPath) {
							clipPath = identifiedClipPath;
						}
						else if (identified is IShapeElement shapeElem) {
							clipPath = new ClipPath(null, new StyleSheet(), new IShapeElement[] { shapeElem });
						}
						else if (identified != null) {
							LogError(clipPathAttr, "Specified element is not a clipPath.");
						}
					}
					// TODO Parse simple shapes
					else {
						LogError(clipPathAttr, "Could not evaluate clip-path string.");
					}
				}

				return clipPath;
			}

			private ICanvasPaint? GetPaint(XMLElement root, XMLElement element, string name, bool inheritable, ISet<XMLElement> disallowed, IVariableBox variables, MarkupEvaluationContext markupContext, ICanvasPaint? defaultPaint) {

				if (GetAttribute(element, name, inheritable) is ContextProperty<string> paintAttr) {
					if (EvalualteURLElement(root, paintAttr.Value, disallowed) is XMLElement elem) {
						try {
							string? id = GetAttribute(elem, "id", false, s => s, null);

							if (elem.Name == "solidPaint") {
								if (GetAttribute(elem, "color", false) is ContextProperty<string> solidColorAttr) {
									if (TryParseAttribute<ColorExpression>(name, solidColorAttr, s => MarkupValueParsing.ParseColor(s, variables), out ColorExpression? colorExpr)) {
										LogVisit(elem);
										return new SolidPaint(id, colorExpr);
									}
								}

								return defaultPaint;
							}
							else {
								List<ColorStopExpression> stops = new List<ColorStopExpression>();
								foreach (XMLElement stop in elem.FindElements("stop")) {
									stops.Add(new ColorStopExpression(
										GetAttribute(stop, "offset", false, s => MarkupValueParsing.ParsePercentage(s, variables), new FloatExpression(0f, variables.Context)),
										GetAttribute(stop, "stop-color", false, s => MarkupValueParsing.ParseColor(s, variables), new ColorExpression(Color.Black, variables.Context))));
									LogVisit(stop);
								}

								if (elem.Name == "linearGradient") {
									LogVisit(elem);
									return new LinearGradient(id,
										GetAttribute(elem, "x1", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
										GetAttribute(elem, "y1", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
										GetAttribute(elem, "x2", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), markupContext.WidthExpression),
										GetAttribute(elem, "y2", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
										stops.ToArray()
										);
								}
								else if (elem.Name == "radialGradient") {
									LogVisit(elem);
									return new RadialGradient(id,
										GetAttribute(elem, "cx", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), markupContext.CentreXExpression),
										GetAttribute(elem, "cy", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), markupContext.CentreYExpression),
										GetAttribute(elem, "r", false, s => MarkupValueParsing.ParseBoundingBoxLength(s, variables, markupContext), new BoundingBoxLengthExpression(variables.Context.MakeValue<FloatEvaluationType>(0.5f) * markupContext.BoundingBoxLengthNode)),
										GetAttribute(elem, "fx", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), markupContext.CentreXExpression),
										GetAttribute(elem, "fy", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), markupContext.CentreYExpression),
										GetAttribute(elem, "fr", false, s => MarkupValueParsing.ParseBoundingBoxLength(s, variables, markupContext), new BoundingBoxLengthExpression(0.0f, variables.Context)),
										stops.ToArray()
										);
								}
							}

							// Fallback option if this element does not have a valid paint element name
							LogError(paintAttr, "Invalid paint element.");
							return null;
						}
						catch(EvaluationException e) {
							LogError(elem, "Error parsing paint element.", e);
							return null;
						}
					}
					else if (paintAttr.Value == "none" || paintAttr.Value == "transparent") {
						LogVisit(element);
						return null;
					}
					else if (TryParseAttribute<ColorExpression>(name, paintAttr, s => MarkupValueParsing.ParseColor(s, variables), out ColorExpression? colorExpr)) {
						LogVisit(element);
						return new SolidPaint(null, colorExpr);
					}
				}

				return defaultPaint; // new SolidPaint(MarkupEnvironments.BackgroundExpression);
			}

			#endregion

			#region Utilities

			private static XMLElement? EvalualteURLElement(XMLElement root, string? url, ISet<XMLElement> disallowed) {
				if (url == null) { return null; }
				url = XMLParsing.NormaliseURL(url);
				if (url != null) {
					XMLElement? evaluated = root.TraverseChildren().OfType<XMLElement>().FirstOrDefault(e => e.GetAttribute1("id", false)?.Value == url);

					if (evaluated != null && !disallowed.Contains(evaluated)) {
						return evaluated;
					}
				}
				return null;
			}

			private IIdentifiableMarkupElement? EvaluateURL(XMLElement root, string? url, bool forceCopy, Dictionary<XMLElement, IIdentifiableMarkupElement> constructed, ISet<XMLElement> disallowed, IVariableBox variables, MarkupEvaluationContext markupContext, DirectoryPath source) {

				XMLElement? elem = EvalualteURLElement(root, url, disallowed);

				if (!forceCopy && elem != null && constructed.TryGetValue(elem, out IIdentifiableMarkupElement? existing)) {
					return existing;
				}
				else if (elem != null) {
					IIdentifiableMarkupElement? result = MakeElement(root, elem, constructed, disallowed, variables, markupContext, source);
					LogVisit(elem);
					return result;
				}

				return null;
			}

			#endregion

			#region Properties

			#region Basic Properties

			/*
			/// <summary></summary>
			/// <exception cref="EvaluationException"></exception>
			private DrawPointExpression GetDrawPoint(XMLElement elem, string x, string y, IVariableBox variables, MarkupEvaluationContext markupContext, DrawPointExpression defaultValue) {
				return new DrawPointExpression(
					GetAttribute(elem, x, false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), defaultValue.X),
					GetAttribute(elem, y, false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), defaultValue.Y)
					);
			}
			*/

			/// <summary></summary>
			/// <exception cref="EvaluationException"></exception>
			private RectangleExpression? GetRectangle(XMLElement elem, bool allowNull, IVariableBox variables, MarkupEvaluationContext markupContext) {
				if (!allowNull || elem.HasAttribute("x", false) || elem.HasAttribute("y", false) || elem.HasAttribute("width", false) || elem.HasAttribute("height", false)) {
					return new RectangleExpression(
						GetAttribute(elem, "x", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), new XLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "y", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), new YLengthExpression(0f, variables.Context)),
						GetAttribute(elem, "width", false, s => MarkupValueParsing.ParseXLength(s, variables, markupContext), markupContext.WidthExpression),
						GetAttribute(elem, "height", false, s => MarkupValueParsing.ParseYLength(s, variables, markupContext), markupContext.HeightExpression)
						);
				}
				else {
					return null;
				}
			}

			private XMLText[] GetTextNodes(XMLElement elem) {
				XMLText[] nodes = elem.Children.OfType<XMLText>().ToArray();
				foreach (XMLText node in nodes) {
					LogVisit(node);
				}
				return nodes;
			}

			private TextExpression ParseText(IEnumerable<XMLText> textNodes, IVariableBox variables) {
				// TODO This needs improving and interfacing with MarkupValueParsing

				TextExpression result = new TextExpression("", variables.Context);

				foreach(XMLText node in textNodes) {
					try {
						TextExpression nodeText = Interpolation.Parse(node.Text, variables, false);
						result += nodeText;
					}
					catch (FormatException e) {
						LogError(node, "Error parsing text.", e);
					}
					catch (EvaluationException e) {
						LogError(node, "Error parsing text: " + e.Message, e);
					}
				}

				return result;
			}

			private PositionExpression? MakePositionExpression(XMLElement posElem, IVariableBox variables) {
				if (posElem == null) {
					return null;
				}
				else {

					EnumExpression<Anchor>? anchor = GetAttribute(posElem, "anchor", false, s => MarkupValueParsing.ParseEnum<Anchor>(s, variables), null);
					DimensionExpression? x = GetAttribute(posElem, "x", false, s => MarkupValueParsing.ParseDimension(s, variables), null);
					DimensionExpression? y = GetAttribute(posElem, "y", false, s => MarkupValueParsing.ParseDimension(s, variables), null);
					DimensionExpression? width = GetAttribute(posElem, "width", false, s => MarkupValueParsing.ParseDimension(s, variables), null);
					DimensionExpression? height = GetAttribute(posElem, "height", false, s => MarkupValueParsing.ParseDimension(s, variables), null);

					if (anchor != null || x != null || y != null || width != null || height != null) {
						return new PositionExpression(anchor, x, y, width, height);
					}
					else {
						return null;
					}
				}
			}

			#endregion

			private ContextExpression? MakeContextExpression(XMLElement elem, IVariableBox variables, string contextName) {
				if (elem == null) {
					return null;
				}

				Dictionary<string, EvaluationNode> values = new Dictionary<string, EvaluationNode>();

				foreach (ContextProperty<string> attribute in elem.Attributes) {
					// Using a name.variable system
					if (attribute.Name.StartsWith(contextName + ".")) {
						string valueName = attribute.Name[(contextName.Length + 1)..];
						if (!string.IsNullOrEmpty(valueName)) {
							/*
							try {
								EvaluationNode node = Evaluation.Parse(attribute.Value, variables);
								values[valueName] = node;
								LogVisit(elem, attribute.Name);
							}
							catch (EvaluationException e) {
								LogError(attribute, "Error parsing context value expression.", e);
							}
							*/
							EvaluationNode node;
							try {
								node = Evaluation.Parse(attribute.Value, variables);
							}
							catch (EvaluationException) {
								// TODO Is this an acceptable fallback?
								node = variables.Context.MakeValue<StringEvaluationType>(attribute.Value);
							}
							values[valueName] = node;
							LogVisit(elem, attribute.Name);
						}
					}
				}

				return new ContextExpression(elem.Name, values, variables.Context);
			}

			private LabelDetailsExpression GetLabelDetails(XMLElement elem, IVariableBox variables, string labelName) {
				return new LabelDetailsExpression(
						GetAttribute(elem, labelName + "-fontsize", false, s => FloatExpression.Parse(s, variables), null),
						GetAttribute(elem, labelName + "-x-offset", false, s => FloatExpression.Parse(s, variables), null),
						GetAttribute(elem, labelName + "-y-offset", false, s => FloatExpression.Parse(s, variables), null),
						GetAttribute(elem, labelName + "-font-style", false, s => EnumExpression<TextFormat>.Parse(s, variables), null),
						GetAttribute(elem, labelName + "-justification", false, s => EnumExpression<Justification>.Parse(s, variables), null),
						GetAttribute(elem, labelName + "-alignment", false, s => EnumExpression<SharpSheets.Canvas.Text.Alignment>.Parse(s, variables), null),
						GetAttribute(elem, labelName + "-color", false, s => ColorExpression.Parse(s, variables), null),
						variables.Context);
			}

			#region Arrangement Properties

			private SlicingValuesElement? MakeSlicingValuesElement(XMLElement slicingElem, IVariableBox parentVariables, IVariableBox fullDrawingVariables, MarkupEvaluationContext markupContext) {
				if (slicingElem == null) { return null; }

				MarginsExpression? border = GetAttribute(slicingElem, "border", false, s => MarkupValueParsing.ParseMargins(s, fullDrawingVariables), null);

				FloatExpression[]? xs = GetAttribute(slicingElem, "xs", false, s => MarkupValueParsing.ParseSVGNumbers(s, fullDrawingVariables), null);
				FloatExpression[]? ys = GetAttribute(slicingElem, "ys", false, s => MarkupValueParsing.ParseSVGNumbers(s, fullDrawingVariables), null);

				string? id = GetAttribute(slicingElem, "id", false, s => s, null);
				BoolExpression enabled = GetAttribute(slicingElem, "enabled", false, s => BoolExpression.Parse(s, parentVariables), new BoolExpression(true, parentVariables.Context));

				return new SlicingValuesElement(id, xs, ys, border, enabled, markupContext);
			}

			private AreaElement? MakeAreaElement(XMLElement areaElem, IVariableBox parentVariables, IVariableBox fullDrawingVariables, MarkupEvaluationContext markupContext) {
				if (areaElem == null) {
					return null;
				}
				else if (RequiredAttribute(areaElem, "name", false, s => MarkupValueParsing.ParseText(s, parentVariables), out ContextProperty<TextExpression> nameAttr)) {
					try {
						string? childID = GetAttribute(areaElem, "id", false, s => s, null);

						TextExpression areaName = nameAttr.Value; // TODO Check against list of allowed area names? Will probaby need index adding to AreaElement (remaingRectElementNames)

						RectangleExpression? rect = GetRectangle(areaElem, true, fullDrawingVariables, markupContext);
						MarginsExpression margins = GetAttribute(areaElem, "margin", false, s => MarkupValueParsing.ParseMargins(s, fullDrawingVariables), new MarginsExpression(Margins.Zero, fullDrawingVariables.Context));
						BoolExpression enabled = GetAttribute(areaElem, "enabled", false, s => BoolExpression.Parse(s, parentVariables), new BoolExpression(true, parentVariables.Context));

						return new AreaElement(childID, areaName, rect?.X, rect?.Y, rect?.Width, rect?.Height, margins, enabled, markupContext);
					}
					catch (EvaluationException e) {
						LogError(areaElem, "Error parsing area element.", e);
						return null;
					}
				}
				else {
					return null;
				}
			}

			private DiagnosticElement? MakeDiagnosticElement(XMLElement diagnosticElem, IVariableBox parentVariables, IVariableBox fullDrawingVariables, MarkupEvaluationContext markupContext) {
				if (diagnosticElem == null) {
					return null;
				}

				try {
					string? childID = GetAttribute(diagnosticElem, "id", false, s => s, null);

					RectangleExpression? rect = GetRectangle(diagnosticElem, true, fullDrawingVariables, markupContext);
					BoolExpression enabled = GetAttribute(diagnosticElem, "enabled", false, s => BoolExpression.Parse(s, parentVariables), new BoolExpression(true, parentVariables.Context));

					//Console.WriteLine("Diagnostic created");
					return new DiagnosticElement(childID, rect?.X, rect?.Y, rect?.Width, rect?.Height, enabled, markupContext);
				}
				catch (EvaluationException e) {
					LogError(diagnosticElem, "Error parsing diagnostic element.", e);
					return null;
				}
			}

			#endregion

			#endregion

			#region XML Utilities

			private ContextProperty<string>? GetAttribute(XMLElement elem, string name, bool inheritable) {
				LogVisit(elem, name);
				return elem.GetAttribute1(name, inheritable);
			}

			private T GetAttribute<T>(XMLElement elem, string name, bool inheritable, Func<string, T> parser, T defaultValue) {
				ContextProperty<string>? attribute = GetAttribute(elem, name, inheritable);

				if (attribute.HasValue && TryParseAttribute(name, attribute.Value, parser, out T? result)) {
					return result;
				}
				else {
					return defaultValue;
				}
			}

			// TODO Is this practical?
			/*
			private IExpression<V> GetAttribute<V>(XMLElement elem, string name, bool inheritable, Func<string, IExpression<V>> parser, V defaultValue, EvaluationContext context) {
				ContextProperty<string>? attribute = GetAttribute(elem, name, inheritable);

				if (attribute.HasValue && TryParseAttribute(name, attribute.Value, parser, out IExpression<V>? result)) {
					return result;
				}
				else {
					return new ConstantExpression<V>(defaultValue, context);
				}
			}
			*/

			private bool RequiredAttribute<T>(XMLElement elem, string name, bool inheritable, Func<string, T> parser, out ContextProperty<T> required) {
				ContextProperty<string>? attribute = GetAttribute(elem, name, inheritable);

				if (attribute.HasValue) {
					if(TryParseAttribute(name, attribute.Value, parser, out T? result)) {
						required = MakeProperty(attribute.Value, result);
						return true;
					}
				}
				else {
					LogError(elem, $"{elem.Name} must have a {name} attribute.");
				}

				required = default;
				return false;
			}

			private bool TryParseAttribute<T>(string attributeName, ContextProperty<string> attribute, Func<string, T> parser, [MaybeNullWhen(false)] out T result) {
				try {
					result = parser(attribute.Value);
					return true;
				}
				catch (FormatException e) {
					LogError(attribute, $"Badly formatted {TypeName(typeof(T))} for attribute \"{attributeName}\".", e);
					LogError(attribute, e.Message);
				}
				catch (EvaluationException e) {
					LogError(attribute, $"Could not parse expression for attribute \"{attributeName}\" into {TypeName(typeof(T))}.", e);
					LogError(attribute, e.Message);
				}
				catch(MarkupParsingException e) {
					LogError(e);
				}
				// Catch other errors?

				result = default;
				return false;
			}

			private static string TypeName(Type type) {
				// TODO Implement

				/*
				if (typeof(IExpression<>).IsAssignableFrom(type)) {
					return "EXPRESSION " + TypeName(type.GetGenericArguments()[0]);
				}
				*/

				if (ExpressionUsageMap.TryGetExpressionType(type, out Type? expressionType)) {
					return TypeName(expressionType) + " expression";
				}
				/*
				if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IExpression<>))) {
					return TypeName(type.GetGenericArguments()[0]) + " expression";
				}
				*/
				else if(type == typeof(float)) {
					return "float";
				}
				else if(type == typeof(int)) {
					return "int";
				}
				else if(type == typeof(object)) {
					return "value";
				}
				else {
					return type.Name;
				}
			}

			/*
			private bool RequiredAttribute(XMLElement elem, string name, out ContextProperty<string> required) {
				if (elem.GetAttribute(name) is ContextProperty<string> attr && attr.Value != null) {
					required = attr;
					return true;
				}
				else {
					AddError(elem, $"{elem.Name} must have a {name} attribute.");
					required = default;
					return false;
				}
			}
			*/

			private static ContextProperty<T> MakeProperty<T>(ContextProperty<string> property, T newValue) {
				return new ContextProperty<T>(property.Location, property.Name, property.ValueLocation, newValue);
			}

			#endregion
		}

	}

	public class MarkupParsingException : SharpParsingException {
		public new DocumentSpan Location => base.Location!.Value;
		public MarkupParsingException(DocumentSpan location, string message, Exception? innerException) : base(location, message, innerException) { }
		public MarkupParsingException(DocumentSpan location, string message) : base(location, message) { }

		public override object Clone() {
			return new MarkupParsingException(Location, Message, InnerException);
		}
	}

}