using SharpSheets.Evaluations;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Shapes;
using SharpSheets.Parsing;
using SharpSheets.Documentation;
using SharpSheets.Widgets;
using System.Collections;
using SharpSheets.Markup.Elements;
using SharpSheets.Markup.Parsing;
using SharpSheets.Exceptions;

namespace SharpSheets.Markup.Patterns {

	public abstract class MarkupPattern : IMarkupElement {

		public virtual bool ValidPattern { get { return true; } }

		public string? Library { get; }
		public string Name { get; }
		public string FullName => (Library != null ? Library + "." : "") + Name;
		public string? Description { get; }

		protected readonly IMarkupArgument[] patternArguments;
		private readonly MarkupValidation[] argumentValidations;
		//private readonly MarkupVariable[] patternVariables;
		//public readonly Dictionary<string, IMarkupVariable> allVariables;

		public IVariableBox Variables { get; }

		public readonly Rectangle? exampleRect; // TODO Rename to exampleSize?
		public readonly Size? exampleCanvas;

		public readonly DivElement rootElement;

		public readonly FilePath source;
		public readonly DirectoryPath sourceDirectory;

		public MarkupPattern(
			string? library,
			string name,
			string? description,
			IMarkupArgument[] arguments,
			MarkupValidation[] validations,
			Rectangle? exampleSize,
			Size? exampleCanvas,
			DivElement rootElement,
			FilePath source
			) {

			this.Library = library;
			this.Name = name;
			this.Description = description;

			this.patternArguments = arguments;
			this.argumentValidations = validations;
			//this.patternVariables = variables;
			//allVariables = this.arguments.ToDictionary<IMarkupVariable, string>(v => v.Name, StringComparer.InvariantCultureIgnoreCase);

			// TODO Should rootElement also be providing variables here?
			//this.Variables = VariableBoxes.Simple(patternArguments.ToDictionary(a => a.Name, a => a.Type), patternVariables.ToDictionary(v => v.Name, v => v.Evaluation));
			this.Variables = SimpleVariableBoxes.Create(patternArguments.Select(a => new EnvironmentVariableInfo(a.VariableName, a.Type, a.Description)));

			this.exampleRect = exampleSize;
			this.exampleCanvas = exampleCanvas;

			this.rootElement = rootElement;

			this.source = source;
			this.sourceDirectory = source.GetDirectory() ?? throw new ArgumentException("Could not resolve pattern source directory."); // TODO DirectoryNotFoundException?
		}

		protected IEnvironment ParseArguments(IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, out SharpParsingException[] errors) {
			IEnvironment result = MarkupArgumentParsing.ParseArguments(patternArguments, argumentValidations, Variables, context, source, widgetFactory, shapeFactory, useExamples, out List<SharpParsingException> argErrors);
			errors = argErrors.ToArray();
			return result;
		}

		public abstract MarkupConstructorDetails GetConstructorDetails();

		protected virtual IEnumerable<ArgumentDetails> GetArgumentDetails() {
			return GetArgumentDetails(patternArguments);
		}

		private static IEnumerable<ArgumentDetails> GetArgumentDetails(IEnumerable<IMarkupArgument> arguments) {
			foreach(IMarkupArgument arg in arguments) {
				if (arg is MarkupSingleArgument singleArg) {
					string argName = singleArg.ArgumentName.ToString().ToLowerInvariant();
					ArgumentType singleArgType = GetArgDocumentationType(singleArg);
					object? exampleValue = GetArgExampleValue(singleArg, singleArgType.DataType);
					DocumentationString? singleArgDesc = singleArg.Description is not null ? new DocumentationString(singleArg.Description) : null;

					if (typeof(IAreaShape).IsAssignableFrom(singleArgType.DataType)) {
						ArgumentDoc standInDoc = new ArgumentDoc(argName, singleArgDesc, null, null, false);
						bool nameGiven = arguments.Any(a => a.ArgumentName.Equals(new EvaluationName("name")));
						foreach (ArgumentDetails shapeArg in DocumentationGenerator.GetAreaShapeArguments(argName, "", singleArgType.DisplayType, standInDoc, singleArg.IsOptional, singleArg.UseLocal, !nameGiven)) {
							yield return new ArgumentDetails(
								shapeArg.Name,
								shapeArg.Description,
								shapeArg.Type,
								shapeArg.IsOptional,
								shapeArg.UseLocal,
								shapeArg.DefaultValue,
								exampleValue, // Need to replace example value in default arg
								shapeArg.Implied);
						}
					}
					else if (typeof(IDetail).IsAssignableFrom(singleArgType.DataType)) {
						ArgumentDoc standInDoc = new ArgumentDoc(argName, singleArgDesc, null, null, false);
						foreach (ArgumentDetails detailArg in DocumentationGenerator.GetDetailArguments(argName, "", singleArgType.DisplayType, standInDoc, singleArg.IsOptional, singleArg.UseLocal)) {
							yield return new ArgumentDetails(
								detailArg.Name,
								detailArg.Description,
								detailArg.Type,
								detailArg.IsOptional,
								detailArg.UseLocal,
								detailArg.DefaultValue,
								exampleValue, // Need to replace example value in default arg
								detailArg.Implied);
						}
					}

					yield return new ArgumentDetails(
						argName,
						singleArgDesc,
						singleArgType,
						singleArg.IsOptional,
						singleArg.UseLocal,
						singleArg.DefaultValue,
						exampleValue,
						null);
				}
				else if(arg is MarkupGroupArgument groupArg) {
					foreach(ArgumentDetails childArg in GetArgumentDetails(groupArg.Args)) {
						yield return childArg.Prefixed(groupArg.ArgumentName.ToString().ToLowerInvariant());
					}
				}
			}
		}

		private static ArgumentType GetArgDocumentationType(MarkupSingleArgument arg) {
			if (arg.FromEntries) {
				// This is here to abide by SharpFactory conventions
				Type entriesListDisplayType = typeof(List<>).MakeGenericType(arg.Type.ElementType!.DisplayType);
				Type entriesListDataType = typeof(List<>).MakeGenericType(arg.Type.ElementType.DataType); // TODO Should this just be the raw DataType?
				return new ArgumentType(entriesListDisplayType, entriesListDataType);
			}
			else if(arg.Type.DataType == typeof(IWidget)) {
				return new ArgumentType(typeof(ChildHolder), arg.Type.DataType); // TODO Is this the right option now?
			}
			else if (arg.IsNumbered && arg.Type.ElementType!.DataType == typeof(IWidget)) {
				Type numberedDisplayType = typeof(Numbered<>).MakeGenericType(typeof(ChildHolder));
				Type numberedDataType = typeof(Numbered<>).MakeGenericType(arg.Type.ElementType.DataType); // TODO Should this just be the raw DataType?
				return new ArgumentType(numberedDisplayType, numberedDataType);
			}
			else if (arg.IsNumbered) {
				Type numberedDisplayType = typeof(Numbered<>).MakeGenericType(arg.Type.ElementType!.DisplayType);
				Type numberedDataType = typeof(Numbered<>).MakeGenericType(arg.Type.ElementType.DataType); // TODO Should this just be the raw DataType?
				return new ArgumentType(numberedDisplayType, numberedDataType);
			}
			else {
				return new ArgumentType(arg.Type.DisplayType, arg.Type.DataType); // TODO Is this the right option now?
			}
		}

		private static object? GetArgExampleValue(MarkupSingleArgument arg, Type argType) {
			object? exampleValue = arg.ExampleValue ?? arg.DefaultValue;

			if (arg.IsNumbered) {
				Type numberedElementType = argType.GetGenericArguments().Single();
				Type numberedType = typeof(Numbered<>).MakeGenericType(numberedElementType);
				INumbered? numbered = (INumbered?)Activator.CreateInstance(numberedType) ?? throw new ArgumentException("Could not instantiate provided numbered type.");

				if (exampleValue is Array exampleArray) {
					for (int i=0; i<exampleArray.Length; i++) {
						object? processed = ProcessExampleValue(exampleArray.GetValue(i), numberedElementType);
						numbered.Add(i, processed);
					}
				}
				else if (exampleValue is not null) {
					throw new ArgumentException($"Invalid example data type encountered: expected array (got {exampleValue.GetType().Name})");
				}

				return numbered;
			}
			else if (arg.FromEntries) {
				Type listElementType = argType.GetGenericArguments().Single();
				Type listType = typeof(List<>).MakeGenericType(listElementType);
				IList entries = Activator.CreateInstance(listType)! as IList ?? throw new ArgumentException("Could not instantiate provided entires type.");

				if (exampleValue is Array exampleArray) {
					foreach (object entry in exampleArray) {
						object? processed = ProcessExampleValue(entry, listElementType);
						entries.Add(processed);
					}
				}
				else if(exampleValue is not null) {
					throw new ArgumentException($"Invalid example data type encountered: expected array (got {exampleValue.GetType().Name})");
				}

				return entries;
			}
			else {
				return ProcessExampleValue(exampleValue, argType);
			}
		}

		private static object? ProcessExampleValue(object? value, Type type) {
			// TODO Need to decide what kind of exception this is going to throw on failure.
			// Is ArgumentException the most sensible?
			// Where exactly are the exceptions being caught?

			if (type.IsEnum && value is string strValue) {
				if (type is MarkupEnumType) {
					return Activator.CreateInstance(type); // TODO What the hell is going on here?
				}
				else {
					return EnumUtils.ParseEnum(type, strValue);
				}
			}
			else if (value is null && type.IsValueType) {
				return Activator.CreateInstance(type);
			}
			else if(value is null) {
				return value;
			}
			else if (type.IsArray) {
				if(value is Array array) {
					Type elementType = type.GetElementType()!;
					List<object?> values = new List<object?>();
					foreach(object entry in array) {
						object? processed = ProcessExampleValue(entry, elementType);
						values.Add(processed);
					}
					return EvaluationTypes.MakeArray(elementType, values);
				}
				else {
					throw new ArgumentException($"Array value expected (got {(value?.GetType()?.Name ?? "null")}).");
				}
			}
			else if (TupleUtils.IsTupleType(type)) {
				if (TupleUtils.IsTupleObject(value)) {
					Type[] elemTypes = type.GenericTypeArguments;
					
					if(TupleUtils.GetTupleLength(value) != elemTypes.Length) {
						throw new ArgumentException($"ValueTuple of length {elemTypes.Length} expected (got {TupleUtils.GetTupleLength(value)}).");
					}

					object?[] processed = new object[elemTypes.Length];
					for(int i=0; i<elemTypes.Length; i++) {
						processed[i] = ProcessExampleValue(TupleUtils.Index(value, i), elemTypes[i]);
					}

					return TupleUtils.CreateTuple(type, processed);
				}
				else {
					throw new ArgumentException($"ValueTuple value expected (got {value.GetType().Name}).");
				}
			}
			else if(type.IsAssignableFrom(value.GetType())) { // Already checked "value is null"
				return value;
			}
			else {
				throw new ArgumentException($"Invalid data type: {(value?.GetType()?.Name ?? "null")} (expected {type.Name})");
			}
		}

		public abstract object MakeExample(WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool diagnostic, out SharpParsingException[] buildErrors);

	}

	public interface IMarkupObject {
		MarkupPattern Pattern { get; }
		//Size ExampleSize { get; }
	}

	public class MarkupConstructorDetails : ConstructorDetails {

		public MarkupPattern Pattern { get; }

		public MarkupConstructorDetails(MarkupPattern pattern, Type displayType, Type declaringType, ArgumentDetails[] arguments, DocumentationString? description) : base(displayType, declaringType, pattern.Name, pattern.FullName, arguments, description, pattern.exampleRect, pattern.exampleCanvas) {
			this.Pattern = pattern;
		}

		protected override ConstructorDetails WithArguments(ArgumentDetails[] arguments) {
			return new MarkupConstructorDetails(Pattern, DisplayType, DeclaringType, arguments, Description);
		}

	}

}