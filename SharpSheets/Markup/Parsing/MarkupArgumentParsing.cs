using SharpSheets.Evaluations;
using SharpSheets.Evaluations.Types;
using SharpSheets.Exceptions;
using SharpSheets.Markup.Patterns;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System.Collections;
using System.Text.RegularExpressions;

namespace SharpSheets.Markup.Parsing {

	public static class MarkupArgumentParsing {

		public static IEnvironment ParseArguments(IMarkupArgument[] markupArguments, MarkupValidation[] validations, IVariableBox variables, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, out List<SharpParsingException> argumentErrors) { // TODO out SharpParsingException[] errors?
			List<(EvaluationValue, EnvironmentVariableInfo)> arguments = new List<(EvaluationValue, EnvironmentVariableInfo)>();
			List<SharpParsingException> errors = new List<SharpParsingException>();

			foreach (IMarkupArgument arg in markupArguments) {
				if(ParseArgument(arg, out EvaluationValue argValue, out DocumentSpan? argLocation, context, source, widgetFactory, shapeFactory, useExamples, ref errors)) {
					arguments.Add((argValue, new EnvironmentVariableInfo(arg.VariableName, arg.Type, arg.Description)));
				}
			}

			if (errors.Any(e => e is MissingParameterException)) {
				throw new SharpFactoryException(errors, $"Errors parsing pattern arguments.");
			}

			IEnvironment environment = Environments.Create(arguments, variables);

			foreach(MarkupValidation validation in validations) {
				try {
					if (!validation.Evaluate(environment)) {
						if (!string.IsNullOrWhiteSpace(validation.Message)) {
							errors.Add(new SharpParsingException(context.Location, validation.Message));
						}
						else {
							errors.Add(new SharpParsingException(context.Location, $"Invalid arguments. Must satisfy {{{validation.Test}}}."));
						}
					}
				}
				catch(EvaluationException e) {
					errors.Add(new SharpParsingException(context.Location, $"Error validation arguments: " + e.Message, e));
				}
			}

			argumentErrors = errors;
			return environment;
		}

		private static bool ParseArgument(IMarkupArgument arg, out EvaluationValue value, out DocumentSpan? location, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, ref List<SharpParsingException> errors) {
			if (arg is MarkupSingleArgument singleArg) {
				bool response = ParseSingleArgument(singleArg, out value, out location, context, source, widgetFactory, shapeFactory, useExamples, ref errors);
				response = response && ValidateArgValue(singleArg, value, location, context, ref errors);
				return response;
			}
			else if (arg is MarkupGroupArgument groupArg) {
				location = context.Location;
				bool response = ParseGroupArgument(groupArg, out Dictionary<EvaluationName, EvaluationValue> result, context, source, widgetFactory, shapeFactory, useExamples, ref errors);
				value = new EvaluationValue(result, groupArg.Type);
				return response;
			}
			else {
				throw new NotSupportedException($"Argument parsing not supported for argument objects of type {arg.GetType().Name}");
			}
		}

		private static bool ValidateArgValue(MarkupSingleArgument singleArg, EvaluationValue value, DocumentSpan? location, IContext context, ref List<SharpParsingException> errors) {
			if (singleArg.Validation is not null) {
				if (!singleArg.Validation.Evaluate(Environments.Single(new EnvironmentVariableInfo(singleArg.VariableName, singleArg.Type, singleArg.Description), value))) {
					if (!string.IsNullOrWhiteSpace(singleArg.ValidationMessage)) {
						errors.Add(new SharpParsingException(location ?? context.Location, singleArg.ValidationMessage));
					}
					else {
						errors.Add(new SharpParsingException(location ?? context.Location, $"{value.Value?.ToString() ?? "null"} is an invalid value for {singleArg.ArgumentName}. Must satisfy {{{singleArg.Validation}}}."));
					}
					return false;
				}
			}

			return true;
		}

		private static bool ParseSingleArgument(MarkupSingleArgument arg, out EvaluationValue value, out DocumentSpan? location, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, ref List<SharpParsingException> errors) {
			if (useExamples && arg.ExampleValue != null) {
				value = new EvaluationValue(arg.ExampleValue, arg.Type);
				location = null;
				return true;
			}
			else if (arg.IsNumbered) {
				EvaluationType numberedElementEvalType = arg.Type.IterationResult() ?? throw new InvalidOperationException("Numbered arguments must be of an iterable type.");
				Type numberedElementType = numberedElementEvalType.DataType;
				Type numberedType = typeof(Numbered<>).MakeGenericType(numberedElementType);
				INumbered? numbered = (INumbered?)Activator.CreateInstance(numberedType);

				if (numbered is null) {
					errors.Add(new SharpParsingException(context.Location, $"Could not initialize {numberedType} object."));
					value = default;
					location = null;
					return false;
				}

				object? elementDefaultValue;
				if (numberedElementType.IsValueType) {
					elementDefaultValue = numberedElementEvalType.DefaultValue().Value; // Activator.CreateInstance(numberedElementType);
				}
				else {
					elementDefaultValue = null;
				}

				string parameterName = arg.ArgumentName.ToString();
				Regex paramRegex = new Regex(@"^" + Regex.Escape(parameterName) + @"(?<number>[1-9][0-9]*)(?:\.|$)", RegexOptions.IgnoreCase);

				int[] numbers = ((numberedElementType == typeof(IWidget)) switch {
					true => context.GetAllNamedChildren(arg.UseLocal),
					false => context.GetAllProperties(arg.UseLocal).Select(p => p.Name).Concat(context.GetAllFlags(arg.UseLocal).Select(p => p.Name))
				}).Select(n => paramRegex.Match(n))
						.Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value))
						.Distinct().OrderBy(i => i)
						.ToArray();

				for (int i = 0; i < numbers.Length; i++) {
					int num = numbers[i];
					int index = num - 1;
					string numberedName = parameterName + num.ToString();
					MarkupSingleArgument numberedArg = new MarkupSingleArgument(numberedName, numberedElementEvalType,
						_optional: arg.IsOptional, _default: elementDefaultValue, _local: arg.UseLocal, _format: MarkupArgumentFormat.DEFAULT);

					if(ParseSingleArgument(numberedArg, out EvaluationValue numberedResult, out _, context, source, widgetFactory, shapeFactory, useExamples, ref errors)) {
						numbered.Add(index, numberedResult.Value);
					}
				}

				Array result = numbered.TakeContinuous(numbered.Length).ToArray(numberedElementType);

				value = new EvaluationValue(result, arg.Type);
				location = null;
				return true;
			}
			else if (arg.FromEntries) {
				EvaluationType listElementEvalType = arg.Type.IterationResult() ?? throw new InvalidOperationException("Entry arguments must be of an iterable type.");
				Type listElementType = listElementEvalType.DataType;
				Type listType = typeof(List<>).MakeGenericType(listElementType);
				IList? entries = (IList?)Activator.CreateInstance(listType);

				if (entries is null) {
					errors.Add(new SharpParsingException(context.Location, "Error instantiating entries list."));
					value = default;
					location = null;
					return false;
				}

				foreach (ContextValue<string> entry in context.GetEntries(context)) {
					try {
						//entries.Add(ValueParsing.Parse(entry.Value, listElementType));
						entries.Add(ParseValue(entry.Value, listElementEvalType, source).Value);
					}
					catch (FormatException e) {
						errors.Add(new SharpParsingException(entry.Location, e.Message, e));
					}
					catch (ArgumentException e) {
						errors.Add(new SharpParsingException(entry.Location, e.Message, e));
					}
				}
				Array result = entries.ToArray(listElementType);
				//AddValue(arg, result, context.Location);
				value = new EvaluationValue(result, arg.Type);
				location = context.Location;
				return true;
			}
			else if (typeof(IShape).IsAssignableFrom(arg.Type.DataType)) {
				if (shapeFactory is null) {
					throw new ArgumentNullException(nameof(shapeFactory), "Cannot construct shape, as no ShapeFactory instance provided.");
				}

				new NamedContext(context, arg.ArgumentName.ToString(), forceLocal: arg.UseLocal).HasProperty("style", arg.UseLocal, context, out DocumentSpan? styleLocation);
				IContext shapeContext = new NamedContext(context, arg.ArgumentName.ToString(), location: styleLocation, forceLocal: arg.UseLocal);
				Type? defaultStyle = ShapeFactory.GetDefaultStyle(arg.Type.DataType);
				if (defaultStyle is null) {
					errors.Add(new SharpParsingException(context.Location, $"Could not identify default style for {arg.Type.DataType}."));
					value = default;
					location = null;
					return false;
				}
				
				IShape shape = shapeFactory.MakeShape(arg.Type.DataType, shapeContext, /*contextName,*/ /*defaultStyle,*/ source, out SharpParsingException[] shapeBuildErrors);
				
				errors.AddRange(shapeBuildErrors);
				value = new EvaluationValue(shape, arg.Type);
				location = styleLocation;
				return true;
			}
			else if (arg.Type.DataType == typeof(IWidget)) {
				// This is a Named Child

				if (widgetFactory == null) {
					throw new SharpParsingException(context.Location, $"No WidgetFactory provided for constructing \"{arg.ArgumentName}\".");
				}

				string argName = arg.ArgumentName.ToString();

				IContext? childContext = context.GetNamedChild(argName, arg.UseLocal, context);
				if (childContext != null) {
					IWidget child = widgetFactory.MakeWidget(typeof(Div), childContext, source, out SharpParsingException[] widgetBuildErrors);
					if (child is not Div) {
						errors.AddRange(widgetBuildErrors);
						errors.Add(new SharpParsingException(childContext.Location, "Invalid child element."));
						value = default;
						location = null;
						return false;
					}
					errors.AddRange(widgetBuildErrors);
					value = new EvaluationValue(child, arg.Type);
					location = childContext.Location;
					return true;
				}
			}
			else if (BoolEvaluationType.IsBool(arg.Type) && context.HasFlag(arg.ArgumentName.ToString(), arg.UseLocal, context)) {
				bool flag = context.GetFlag(arg.ArgumentName.ToString(), arg.UseLocal, context, out DocumentSpan? flagLocation);
				//AddValue(arg, flag, flagLocation);
				value = new EvaluationValue(flag, arg.Type);
				location = flagLocation;
				return true;
			}
			else if (context.HasProperty(arg.ArgumentName.ToString(), arg.UseLocal, context)) {
				string valueStr = context.GetProperty(arg.ArgumentName.ToString(), arg.UseLocal, context, null, out DocumentSpan? propertyLocation)!;
				try {
					//object result = Parsing.ValueParsing.Parse(value, arg.Type.SystemType);
					EvaluationValue result = ParseValue(valueStr, arg.Type, source);

					//AddValue(arg, result, propertyLocation);
					value = result;
					location = propertyLocation;
					return true;
				}
				catch (FormatException e) {
					errors.Add(new SharpParsingException(propertyLocation, e.Message, e));
				}
				catch (ArgumentException e) {
					errors.Add(new SharpParsingException(propertyLocation, e.Message, e));
				}
				catch (NotImplementedException e) {
					errors.Add(new SharpParsingException(propertyLocation, e.Message, e));
				}
			}

			if (arg.DefaultValue != null) { // TODO "arg.IsOptional"?
				//AddValue(arg, arg.DefaultValue, null);
				value = new EvaluationValue(arg.DefaultValue, arg.Type);
				location = null;
				return true;
			}
			else if (!arg.IsOptional) {
				//errors.Add(new SharpParsingException(context.Location, $"No value for required argument \"{arg.Name}\" for {Name}.")); // context.Location good here?
				errors.Add(new MissingParameterException(context.Location, arg.ArgumentName.ToString(), arg.Type.DataType, $"No value for required argument \"{arg.ArgumentName}\".")); // context.Location good here?
			}

			value = default;
			location = null;
			return false;
		}

		private static bool ParseGroupArgument(MarkupGroupArgument arg, out Dictionary<EvaluationName, EvaluationValue> value, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, ref List<SharpParsingException> errors) {
			Dictionary<EvaluationName, EvaluationValue> result = new Dictionary<EvaluationName, EvaluationValue>();
			
			foreach(IMarkupArgument childArg in arg.Args) {
				if(ParseArgument(childArg, out EvaluationValue childValue, out _, new NamedContext(context, arg.ArgumentName.ToString()), source, widgetFactory, shapeFactory, useExamples, ref errors)) {
					result.Add(childArg.VariableName, childValue);
				}
			}

			value = result;
			return true;
		}

		public static EvaluationValue ParseValue(string text, EvaluationType evaluationType, DirectoryPath source) {
			//object? value = ValueParsing.Parse(text, evaluationType.DataType, source);
			//EvaluationValue result = new EvaluationValue(value, evaluationType);
			EvaluationValue result = evaluationType.ParseValue(text, source);

			ValidateData(result, evaluationType);

			return result;
		}

		private static void ValidateData(EvaluationValue value, EvaluationType type) {
			if (EnumEvaluationType.IsEnum(type, out EnumEvaluationType? enumType)) {
				if(enumType.TryGetEnumValue(value, out string? _)) {
					return;
				}
				else {
					throw new FormatException($"{enumType.Name} must be one of the following: " + string.Join(", ", enumType.EnumNames));
				}
			}
			else if (type.IterationResult() is EvaluationType elementType) {
				if (type.Iteration(value) is IEnumerable<EvaluationValue> iterations) {
					foreach (EvaluationValue entry in iterations) {
						ValidateData(entry, elementType);
					}
					return;
				}
				else {
					throw new FormatException($"Invalid data type (expected a collection, {type.Name}, but got {value.Value?.GetType().Name ?? "null"}).");
				}
			}
			else if (value.Value is not null && TupleUtils.IsTupleObject(value.Value)) { // What is going on here?
				foreach(TypeField field in type.Fields) {
					ValidateData(field.GetValue(value), field.Type);
				}
			}
		}

	}

}
