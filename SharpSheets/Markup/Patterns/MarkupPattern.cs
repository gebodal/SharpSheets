using SharpSheets.Evaluations;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using SharpSheets.Shapes;
using SharpSheets.Parsing;
using SharpSheets.Documentation;
using SharpSheets.Widgets;
using System.Collections;
using SharpSheets.Markup.Elements;
using SharpSheets.Markup.Parsing;
using SharpSheets.Exceptions;
using SharpSheets.Evaluations.Types;

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
		public EvaluationContext Context => rootElement.MarkupContext.TypeSystem;

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
			this.Variables = VariableBoxes.Create(patternArguments.Select(a => new EnvironmentVariableInfo(a.VariableName, a.Type, a.Description)), rootElement.MarkupContext.TypeSystem);

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

		public abstract MarkupBuilderDetails GetBuilderDetails();

		protected virtual IEnumerable<ArgumentDetails> GetArgumentDetails() {
			return GetArgumentDetails(patternArguments);
		}

		private static IEnumerable<ArgumentDetails> GetArgumentDetails(IEnumerable<IMarkupArgument> arguments) {
			foreach(IMarkupArgument arg in arguments) {
				if (arg is MarkupSingleArgument singleArg) {
					string argName = singleArg.ArgumentName.ToString().ToLowerInvariant();
					ArgumentType singleArgType = GetArgDocumentationType(singleArg);
					object? exampleValue = GetArgExampleValue(singleArg, singleArgType.DataType, singleArgType);

					if (typeof(IAreaShape).IsAssignableFrom(singleArgType.DataType)) {
						bool nameGiven = arguments.Any(a => a.ArgumentName.Equals(new EvaluationName("name")));
						foreach (ArgumentDetails shapeArg in DocumentationGenerator.GetAreaShapeArguments(argName, "", singleArgType.DataType, singleArg.Description, singleArg.IsOptional, singleArg.UseLocal, !nameGiven)) {
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
						foreach (ArgumentDetails detailArg in DocumentationGenerator.GetDetailArguments(argName, "", singleArgType.DataType, singleArg.Description, singleArg.IsOptional, singleArg.UseLocal)) {
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
						singleArg.Description is not null ? new DocumentationString(singleArg.Description) : null,
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
			EvaluationType? argElemType = arg.Type.IterationResult();

			if (arg.FromEntries) {
				// This is here to abide by SharpFactory conventions
				EvaluationType knownArgElemType = argElemType ?? throw new InvalidOperationException("Entries argument types must be iterable.");
				Type entriesListDataType = typeof(List<>).MakeGenericType(knownArgElemType.DataType); // TODO Should this just be the raw DataType?
				return new ArgumentType(DisplayType.FromEvaluation(knownArgElemType, DisplayTypeStructure.Entried), entriesListDataType);
			}
			else if (arg.Type.DataType == typeof(IWidget)) {
				return new ArgumentType(DisplayType.FromSystem<ChildHolder>(), arg.Type.DataType); // TODO Is this the right option now?
			}
			else if (arg.IsNumbered) {
				EvaluationType knownArgElemType = argElemType ?? throw new InvalidOperationException("Numbered argument types must be iterable.");
				if (knownArgElemType.DataType == typeof(IWidget)) {
					Type numberedDataType = typeof(Numbered<>).MakeGenericType(knownArgElemType.DataType); // TODO Should this just be the raw DataType?
					return new ArgumentType(DisplayType.FromSystem<ChildHolder>(DisplayTypeStructure.Numbered), numberedDataType);
				}
				else {
					Type numberedDataType = typeof(Numbered<>).MakeGenericType(knownArgElemType.DataType); // TODO Should this just be the raw DataType?
					return new ArgumentType(DisplayType.FromEvaluation(knownArgElemType, DisplayTypeStructure.Numbered), numberedDataType);
				}
			}
			else {
				return new ArgumentType(DisplayType.FromEvaluation(arg.Type), arg.Type.DataType); // TODO Is this the right option now?
			}
		}

		private static object? GetArgExampleValue(MarkupSingleArgument arg, Type argDataType, ArgumentType argType) {
			object? exampleValue = (arg.ExampleValue ?? arg.DefaultValue) ?? arg.Type.DefaultValue().Value;

			if (arg.IsNumbered) {
				Type numberedElementType = argDataType.GetGenericArguments().Single();
				Type numberedType = typeof(Numbered<>).MakeGenericType(numberedElementType);
				INumbered? numbered = (INumbered?)Activator.CreateInstance(numberedType) ?? throw new ArgumentException("Could not instantiate provided numbered type.");

				if (exampleValue is Array exampleArray) {
					for (int i=0; i<exampleArray.Length; i++) {
						numbered.Add(i, exampleArray.GetValue(i));
					}
				}
				else if (exampleValue is not null) {
					throw new ArgumentException($"Invalid example data type encountered: expected array (got {exampleValue.GetType().Name})");
				}

				return numbered;
			}
			else if (arg.FromEntries) {
				Type listElementType = argDataType.GetGenericArguments().Single();
				Type listType = typeof(List<>).MakeGenericType(listElementType);
				IList entries = Activator.CreateInstance(listType)! as IList ?? throw new ArgumentException("Could not instantiate provided entires type.");

				if (exampleValue is Array exampleArray) {
					foreach (object entry in exampleArray) {
						entries.Add(entry);
					}
				}
				else if(exampleValue is not null) {
					throw new ArgumentException($"Invalid example data type encountered: expected array (got {exampleValue.GetType().Name})");
				}

				return entries;
			}
			else {
				return exampleValue;
			}
		}

		public abstract object MakeExample(WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool diagnostic, out SharpParsingException[] buildErrors);

	}

	public interface IMarkupObject {
		MarkupPattern Pattern { get; }
		//Size ExampleSize { get; }
	}

	public class MarkupBuilderDetails : BuilderDetails {

		public MarkupPattern Pattern { get; }

		public MarkupBuilderDetails(MarkupPattern pattern, DisplayType displayType, DisplayType declaringType, ArgumentDetails[] arguments, DocumentationString? description) : base(displayType, declaringType, pattern.Name, pattern.FullName, arguments, description, pattern.exampleRect, pattern.exampleCanvas) {
			this.Pattern = pattern;
		}

		protected override BuilderDetails WithArguments(ArgumentDetails[] arguments) {
			return new MarkupBuilderDetails(Pattern, DisplayType, DeclaringType, arguments, Description);
		}

	}

}