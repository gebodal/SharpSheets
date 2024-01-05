using SharpSheets.Layouts;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace SharpSheets.Documentation {

	public static class DocumentationGenerator {

		private static readonly ArgumentDetails aspectRatioArg = new ArgumentDetails("aspect", new DocumentationString("Aspect ratio for this shape."), ArgumentType.Simple(typeof(float)), true, true, -1f, -1f, null);

		private static readonly DirectoryPath DocumentationSource = DirectoryPath.GetCurrentDirectory();

		static DocumentationGenerator() {
			SharpDocumentation.LoadEmbeddedDocumentation(typeof(SharpWidget).Assembly);
		}

		private static IEnumerable<ArgumentDetails> GetArguments(ConstructorInfo constructor, ConstructorDoc? constructorDoc, string prefix = "", bool ignoreWidgetSetup = false) {
			bool addWidgetSetupArgs = false;

			int skip = 0;
			// TODO These hard codings are not ideal. Better to have a general registry of types and required arguments? (would that even work?)
			if (typeof(IShape).IsAssignableFrom(constructor.DeclaringType)) {
				skip = ShapeFactory.GetRequiredArguments(constructor.DeclaringType)?.Length ?? 0; // TODO This feels iffy now we're using interfaces

				// This is ugly.
				if (!typeof(ITitleStyledBox).IsAssignableFrom(constructor.DeclaringType) && !typeof(IDetail).IsAssignableFrom(constructor.DeclaringType)) {
					yield return aspectRatioArg;
				}
			}

			ParameterInfo[] parameters = constructor.GetParameters().Skip(skip).ToArray();
			foreach (ParameterInfo param in parameters) {

				if (param.Name is null) { continue; }

				string parameterName = SharpFactory.NormaliseParameterName(param.Name);
				bool useLocal = param.Name[0] == '_';

				ArgumentDoc? argDoc = constructorDoc?.GetArgument(param.Name);

				if (argDoc?.exclude ?? false) { // param.ParameterType == typeof(IContext) || param.ParameterType == typeof(CardFeatureConfig) || param.ParameterType == typeof(CardSectionConfig) || param.ParameterType == typeof(CardConfig)
					continue;
					// This clause is here to ignore certain arguments in the CardConfig/etc classes
					// Is there a better way of doing this?
				}
				else if (param.ParameterType == typeof(WidgetSetup) && typeof(SharpWidget).IsAssignableFrom(constructor.DeclaringType)) {
					addWidgetSetupArgs = true; // Save these until last
				}
				else if (typeof(SharpWidget).IsAssignableFrom(param.ParameterType)) {

					ConstructorInfo nestedConstructor = WidgetFactory.GetConstructorInfo(param.ParameterType) ?? throw new ArgumentException($"Could not find {nameof(ConstructorInfo)} for parameter type.");
					ConstructorDoc? nestedConstructorDoc = SharpDocumentation.GetConstructorDoc(nestedConstructor);

					string nestedPrefix = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
					foreach (ArgumentDetails p in GetArguments(nestedConstructor, nestedConstructorDoc, nestedPrefix, true)) {
						yield return p;
					}
				}
				else if (typeof(IAreaShape).IsAssignableFrom(param.ParameterType)) {
					bool nameGiven = parameters.Any(p => p.Name is not null && SharpDocuments.StringEquals(p.Name, "name"));
					foreach (ArgumentDetails shapeArg in GetAreaShapeArguments(parameterName, prefix, param.ParameterType, argDoc, param.IsOptional, useLocal, !nameGiven)) {
						yield return shapeArg;
					}
				}
				else if (typeof(IDetail).IsAssignableFrom(param.ParameterType)) {
					foreach (ArgumentDetails detailArg in GetDetailArguments(parameterName, prefix, param.ParameterType, argDoc, param.IsOptional, useLocal)) {
						yield return detailArg;
					}
				}
				else if (typeof(ISharpArgsGrouping).IsAssignableFrom(param.ParameterType) || SharpFactory.IsParsableStruct(param.ParameterType)) {
					ConstructorInfo nestedConstructor = ValueParsing.GetSimpleConstructor(param.ParameterType);
					ConstructorDoc? nestedConstructorDoc = SharpDocumentation.GetConstructorDoc(nestedConstructor);

					//Console.WriteLine($"Arguments for {param.ParameterType.FullName}");

					string nestedPrefix = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
					foreach (ArgumentDetails p in GetArguments(nestedConstructor, nestedConstructorDoc, nestedPrefix, false)) {
						//Console.WriteLine($"{p.Name}: {p.Description ?? "None"}");
						yield return p;
					}
				}
				else {
					string name = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
					DocumentationString? description = NormaliseDescription(argDoc?.description);
					object? defaultValue = argDoc?.defaultValue ?? param.DefaultValue;
					if (param.IsOptional && defaultValue == null && param.ParameterType.IsValueType) { defaultValue = Activator.CreateInstance(param.ParameterType); }
					object? exampleValue = GetExampleValue(param, argDoc);
					//Console.WriteLine($"Parameter \"{name}\": default = {defaultValue?.ToString() ?? "null"} ({defaultValue?.GetType()}) ({argumentDoc?.defaultValue ?? "null"})");
					yield return new ArgumentDetails(name, description, ArgumentType.Simple(param.ParameterType), param.IsOptional, useLocal, defaultValue, exampleValue, null);
				}
			}

			if (addWidgetSetupArgs && !ignoreWidgetSetup) {
				foreach (ArgumentDetails p in GetWidgetSetupArguments(prefix)) {
					yield return p;
				}
			}
		}

		private static object? GetExampleValue(ParameterInfo param, ArgumentDoc? argDoc) {
			if (argDoc != null && argDoc.exampleValue != null) {
				return ValueParsing.Parse(argDoc.exampleValue, param.ParameterType, DocumentationSource);
			}
			else if (param.ParameterType.IsValueType && (param.DefaultValue == null || param.DefaultValue == System.DBNull.Value)) {
				return Activator.CreateInstance(param.ParameterType);
			}
			else {
				return param.DefaultValue;
			}
		}

		private static Rectangle? GetExampleSize(ConstructorDoc? constructorDoc) {
			if (constructorDoc?.size is string sizeStr) {
				string[] parts = sizeStr.Trim().Split(' ');
				if (parts.Length == 2) {
					float width = float.Parse(parts[0]);
					float height = float.Parse(parts[1]);

					return new Rectangle(0f, 0f, width, height);
				}
				else if (parts.Length == 4) {
					float x = float.Parse(parts[0]);
					float y = float.Parse(parts[1]);
					float width = float.Parse(parts[2]);
					float height = float.Parse(parts[3]);

					return new Rectangle(x, y, width, height);
				}
				else {
					throw new FormatException("Invalid format for documentation size.");
				}
			}

			// Otherwise
			return null;
		}

		private static Size? GetExampleCanvas(ConstructorDoc? constructorDoc) {
			if (constructorDoc?.canvas is string canvasStr) {
				string[] parts = canvasStr.Trim().Split(' ');
				if (parts.Length == 2) {
					float width = float.Parse(parts[0]);
					float height = float.Parse(parts[1]);

					return new Size(width, height);
				}
				else {
					throw new FormatException("Invalid format for documentation canvas.");
				}
			}

			// Otherwise
			return null;
		}

		public static IEnumerable<ArgumentDetails> GetWidgetSetupArguments(string prefix = "") {
			foreach (ArgumentDetails p in GetArguments(WidgetFactory.widgetSetupConstructor, WidgetFactory.widgetSetupConstructorDoc, prefix)) {
				yield return p;
			}
		}

		public static IEnumerable<ArgumentDetails> GetAreaShapeArguments(string parameterName, string prefix, Type argumentType, ArgumentDoc? argDoc, bool isOptional, bool useLocal, bool includeNameArg) {
			string name = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
			//string styleName = name + ".style";
			DocumentationString? styleDescription = NormaliseDescription(argDoc?.description);
			//object styleDefaultValue = styleArgDoc?.defaultValue ?? param.DefaultValue;

			string? styleDefaultValue = ShapeFactory.GetDefaultStyle(argumentType)?.Name;

			//Type argumentType = param.ParameterType == typeof(IContainerShape) ? typeof(IBox) : param.ParameterType;

			IShape? exampleValue;
			if (argDoc?.exampleValue is string exampleStyle) {
				exampleValue = ShapeFactory.GetExampleShape(argumentType, exampleStyle);
			}
			else {
				exampleValue = ShapeFactory.GetDefaultShape(argumentType);
			}

			yield return new ArgumentDetails(name, styleDescription, ArgumentType.Simple(argumentType), isOptional, useLocal, styleDefaultValue, exampleValue, "style");
			// TODO What about nested parameters here? (Some AbstractShape types have required arguments?)
			// How about a placeholder argument with an "ImpliedParameters" flag?
			// TODO Implied arguments

			//yield return new ArgumentDetails("aspect", "Aspect ratio for rect shape.", typeof(float), true, true, -1f, null);
			//yield return new ArgumentDetails(name, "Aspect ratio for rect shape.", typeof(float), true, true, -1f, "aspect"); // This should belong to the shape
			if (argumentType == typeof(IContainerShape)) {
				if (includeNameArg) {
					yield return new ArgumentDetails("name", new DocumentationString("Text to use for shape titles."), ArgumentType.Simple(typeof(string)), true, true, "NAME", "NAME", null);
				}

				string titleStyleDefaultValue = ShapeFactory.GetDefaultStyle(typeof(ITitleStyledBox))!.Name;
				ITitleStyledBox exampleTitleStyle = (ITitleStyledBox)ShapeFactory.GetDefaultShape(typeof(ITitleStyledBox))!;
				yield return new ArgumentDetails("title", new DocumentationString($"Title style to be used with {name} if a name is provided."), ArgumentType.Simple(typeof(ITitleStyledBox)), true, false, titleStyleDefaultValue, exampleTitleStyle, "style").Prefixed(name);
			}
		}

		public static IEnumerable<ArgumentDetails> GetDetailArguments(string parameterName, string prefix, Type argumentType, ArgumentDoc? argDoc, bool isOptional, bool useLocal) {
			string name = (prefix.Length > 0 ? prefix + "." : "") + parameterName;
			//string styleName = name + ".style";
			DocumentationString? styleDescription = NormaliseDescription(argDoc?.description);
			//object styleDefaultValue = styleArgDoc?.defaultValue ?? param.DefaultValue;
			string? styleDefaultValue = ShapeFactory.GetDefaultStyle(argumentType)?.Name;
			IDetail exampleDetail = (IDetail)ShapeFactory.GetDefaultShape(typeof(IDetail))!;
			yield return new ArgumentDetails(name, styleDescription, ArgumentType.Simple(argumentType), isOptional, useLocal, styleDefaultValue, exampleDetail, "style");
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ConstructorDetails GetConstructorDetails(Type displayType, ConstructorInfo constructor, string name) {
			ConstructorDoc? constructorDoc = SharpDocumentation.GetConstructorDoc(constructor);
			if(constructor.DeclaringType is null) {
				throw new InvalidOperationException("Provided constructor has no declaring type.");
			}
			return new ConstructorDetails(
					displayType,
					constructor.DeclaringType,
					name, // constructor.DeclaringType.Name,
					name,
					GetArguments(constructor, constructorDoc).ToArray(),
					NormaliseDescription(SharpDocumentation.GetTypeDescription(constructor.DeclaringType)),
					GetExampleSize(constructorDoc),
					GetExampleCanvas(constructorDoc));
		}

		private class SharpDocumentationSpanProcessor : IDocumentationSpanVisitor<IDocumentationSpan> {
			public static readonly SharpDocumentationSpanProcessor Instance = new SharpDocumentationSpanProcessor();
			private SharpDocumentationSpanProcessor() { }

			public IDocumentationSpan Visit(TextSpan span) => span;
			public IDocumentationSpan Visit(LineBreakSpan span) => span;
			public IDocumentationSpan Visit(TypeSpan span) => span;
			public IDocumentationSpan Visit(EnumValueSpan span) => span;

			public IDocumentationSpan Visit(ParameterSpan span) {
				return new ParameterSpan(SharpFactory.NormaliseParameterName(span.Parameter));
			}
		}

		[return: NotNullIfNotNull(nameof(description))]
		private static DocumentationString? NormaliseDescription(DocumentationString? description) {
			if (description is null) { return null; }

			return description.Convert(SharpDocumentationSpanProcessor.Instance);
		}

	}

}
