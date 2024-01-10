using SharpSheets.Evaluations;
using SharpSheets.Exceptions;
using SharpSheets.Markup.Patterns;
using SharpSheets.Parsing;
using SharpSheets.Shapes;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpSheets.Markup.Parsing {

	public static class MarkupArgumentParsing {

		public static IEnvironment ParseArguments(IMarkupArgument[] markupArguments, MarkupValidation[] validations, IVariableBox variables, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, out List<SharpParsingException> argumentErrors) { // TODO out SharpParsingException[] errors?
			List<(object?, EnvironmentVariableInfo)> arguments = new List<(object?, EnvironmentVariableInfo)>();
			List<SharpParsingException> errors = new List<SharpParsingException>();

			foreach (IMarkupArgument arg in markupArguments) {
				if(ParseArgument(arg, out object? argValue, out DocumentSpan? argLocation, context, source, widgetFactory, shapeFactory, useExamples, ref errors)) {
					arguments.Add((argValue, new EnvironmentVariableInfo(arg.VariableName, arg.Type, arg.Description)));
				}
			}

			if (errors.Any(e => e is MissingParameterException)) {
				throw new SharpFactoryException(errors, $"Errors parsing pattern arguments.");
			}

			IEnvironment environment = SimpleEnvironments.Create(arguments, variables);

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

		private static bool ParseArgument(IMarkupArgument arg, out object? value, out DocumentSpan? location, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, ref List<SharpParsingException> errors) {
			if (arg is MarkupSingleArgument singleArg) {
				bool response = ParseSingleArgument(singleArg, out value, out location, context, source, widgetFactory, shapeFactory, useExamples, ref errors);
				response = response && ValidateArgValue(singleArg, value, location, context, ref errors);
				return response;
			}
			else if (arg is MarkupGroupArgument groupArg) {
				location = context.Location;
				bool response = ParseGroupArgument(groupArg, out Dictionary<EvaluationName, object?> result, context, source, widgetFactory, shapeFactory, useExamples, ref errors);
				value = result;
				return response;
			}
			else {
				throw new NotSupportedException($"Argument parsing not supported for argument objects of type {arg.GetType().Name}");
			}
		}

		private static bool ValidateArgValue(MarkupSingleArgument singleArg, object? value, DocumentSpan? location, IContext context, ref List<SharpParsingException> errors) {
			if (singleArg.Validation is not null) {
				if (!singleArg.Validation.Evaluate(SimpleEnvironments.Single(new EnvironmentVariableInfo(singleArg.VariableName, singleArg.Type, singleArg.Description), value))) {
					if (!string.IsNullOrWhiteSpace(singleArg.ValidationMessage)) {
						errors.Add(new SharpParsingException(location ?? context.Location, singleArg.ValidationMessage));
					}
					else {
						errors.Add(new SharpParsingException(location ?? context.Location, $"{value?.ToString() ?? "null"} is an invalid value for {singleArg.ArgumentName}. Must satisfy {{{singleArg.Validation}}}."));
					}
					return false;
				}
			}

			return true;
		}

		private static bool ParseSingleArgument(MarkupSingleArgument arg, out object? value, out DocumentSpan? location, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, ref List<SharpParsingException> errors) {
			if (useExamples && arg.ExampleValue != null) {
				value = arg.ExampleValue;
				location = null;
				return true;
			}
			else if (arg.IsNumbered) {
				Type numberedElementType = arg.Type.ElementType!.DataType;
				Type numberedType = typeof(Numbered<>).MakeGenericType(numberedElementType);
				INumbered? numbered = (INumbered?)Activator.CreateInstance(numberedType);

				if (numbered is null) {
					errors.Add(new SharpParsingException(context.Location, $"Could not initialize {numberedType} object."));
					value = null;
					location = null;
					return false;
				}

				object? elementDefaultValue;
				if (numberedElementType.IsValueType) {
					elementDefaultValue = Activator.CreateInstance(numberedElementType);
				}
				else {
					elementDefaultValue = null;
				}

				string parameterName = arg.ArgumentName.ToString();
				Regex paramRegex = new Regex(@"^" + Regex.Escape(parameterName) + @"(?<number>[1-9][0-9]*)(?:\.|$)", RegexOptions.IgnoreCase);

				int[] numbers = ((numberedElementType == typeof(IWidget)) switch {
					true => context.NamedChildren.GetKeys(),
					false => context.GetAllProperties(arg.UseLocal).Select(p => p.Name).Concat(context.GetAllFlags(arg.UseLocal).Select(p => p.Name))
				}).Select(n => paramRegex.Match(n))
						.Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value))
						.Distinct().OrderBy(i => i)
						.ToArray();

				for (int i = 0; i < numbers.Length; i++) {
					int num = numbers[i];
					int index = num - 1;
					string numberedName = parameterName + num.ToString();
					MarkupSingleArgument numberedArg = new MarkupSingleArgument(numberedName, arg.Type.ElementType,
						_optional: arg.IsOptional, _default: elementDefaultValue, _local: arg.UseLocal, _format: MarkupArgumentFormat.DEFAULT);

					if(ParseSingleArgument(numberedArg, out object? numberedResult, out _, context, source, widgetFactory, shapeFactory, useExamples, ref errors)) {
						numbered.Add(index, numberedResult);
					}
				}

				Array result = numbered.TakeContinuous(numbered.Length).ToArray(numberedElementType);

				value = result;
				location = null;
				return true;
			}
			else if (arg.FromEntries) {
				Type listElementType = arg.Type.ElementType!.DataType;
				Type listType = typeof(List<>).MakeGenericType(listElementType);
				IList? entries = (IList?)Activator.CreateInstance(listType);

				if (entries is null) {
					errors.Add(new SharpParsingException(context.Location, "Error instantiating entries list."));
					value = null;
					location = null;
					return false;
				}

				foreach (ContextValue<string> entry in context.GetEntries(context)) {
					try {
						//entries.Add(ValueParsing.Parse(entry.Value, listElementType));
						entries.Add(ParseValue(entry.Value, arg.Type.ElementType, source));
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
				value = result;
				location = context.Location;
				return true;
			}
			else if (typeof(IShape).IsAssignableFrom(arg.Type.DataType)) {
				if (shapeFactory is null) {
					throw new ArgumentNullException(nameof(shapeFactory), "Cannot construct shape, as no ShapeFactory instance provided.");
				}

				new NamedContext(context, arg.ArgumentName.ToString(), forceLocal: arg.UseLocal).HasProperty("style", arg.UseLocal, context, out DocumentSpan? styleLocation);
				IContext shapeContext = new NamedContext(context, arg.ArgumentName.ToString(), location: styleLocation, forceLocal: arg.UseLocal);
				string? contextName = context.GetProperty("name", true, context, null);
				Type? defaultStyle = ShapeFactory.GetDefaultStyle(arg.Type.DataType);
				if (defaultStyle is null) {
					errors.Add(new SharpParsingException(context.Location, $"Could not identify default style for {arg.Type.DataType}."));
					value = null;
					location = null;
					return false;
				}
				
				// TODO Should be catching exceptions from shapeFactory call
				IShape shape = shapeFactory.MakeShape(arg.Type.DataType, shapeContext, contextName, defaultStyle, source, out SharpParsingException[] shapeBuildErrors);
				
				errors.AddRange(shapeBuildErrors);
				value = shape;
				location = styleLocation;
				return true;
			}
			else if (arg.Type.DataType == typeof(IWidget)) {
				// This is a Named Child

				if (widgetFactory == null) {
					throw new SharpParsingException(context.Location, $"No WidgetFactory provided for constructing \"{arg.ArgumentName}\".");
				}

				string argName = arg.ArgumentName.ToString();

				IContext? childContext = context.GetNamedChild(argName);
				if (childContext != null) {
					IWidget child = widgetFactory.MakeWidget(typeof(Div), childContext, source, out SharpParsingException[] widgetBuildErrors);
					if (child is not Div) {
						errors.AddRange(widgetBuildErrors);
						errors.Add(new SharpParsingException(childContext.Location, "Invalid child element."));
						value = null;
						location = null;
						return false;
					}
					errors.AddRange(widgetBuildErrors);
					value = child;
					location = childContext.Location;
					return true;
				}
			}
			else if (arg.Type == EvaluationType.BOOL && context.HasFlag(arg.ArgumentName.ToString(), arg.UseLocal, context)) {
				bool flag = context.GetFlag(arg.ArgumentName.ToString(), arg.UseLocal, context, out DocumentSpan? flagLocation);
				//AddValue(arg, flag, flagLocation);
				value = flag;
				location = flagLocation;
				return true;
			}
			else if (context.HasProperty(arg.ArgumentName.ToString(), arg.UseLocal, context)) {
				string valueStr = context.GetProperty(arg.ArgumentName.ToString(), arg.UseLocal, context, null, out DocumentSpan? propertyLocation)!;
				try {
					//object result = Parsing.ValueParsing.Parse(value, arg.Type.SystemType);
					object? result = ParseValue(valueStr, arg.Type, source);

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
				value = arg.DefaultValue;
				location = null;
				return true;
			}
			else if (!arg.IsOptional) {
				//errors.Add(new SharpParsingException(context.Location, $"No value for required argument \"{arg.Name}\" for {Name}.")); // context.Location good here?
				errors.Add(new MissingParameterException(context.Location, arg.ArgumentName.ToString(), arg.Type.DisplayType, $"No value for required argument \"{arg.ArgumentName}\".")); // context.Location good here?
			}

			value = null;
			location = null;
			return false;
		}

		private static bool ParseGroupArgument(MarkupGroupArgument arg, out Dictionary<EvaluationName, object?> value, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, bool useExamples, ref List<SharpParsingException> errors) {
			Dictionary<EvaluationName, object?> result = new Dictionary<EvaluationName, object?>();
			
			foreach(IMarkupArgument childArg in arg.Args) {
				if(ParseArgument(childArg, out object? childValue, out _, new NamedContext(context, arg.ArgumentName.ToString()), source, widgetFactory, shapeFactory, useExamples, ref errors)) {
					result.Add(childArg.VariableName, childValue);
				}
			}

			value = result;
			return true;
		}

		public static object? ParseValue(string text, EvaluationType evaluationType, DirectoryPath source) {
			object? value = ValueParsing.Parse(text, evaluationType.DataType, source);

			ValidateData(value, evaluationType);

			return value;
		}

		private static void ValidateData(object? value, EvaluationType evaluationType) {
			if(value is null) {
				return;
			}
			else if (evaluationType.IsEnum) {
				if(value is string enumString && evaluationType.IsEnumValueDefined(enumString)) {
					return; // TODO Can we replace the value here with the exact string from the type?
				}
				else if (value is Enum) {
					return; // Is this still appropriate? Is it actually unhelpful now?
				}
				else {
					throw new FormatException($"{evaluationType.Name} must be one of the following: " + string.Join(", ", evaluationType.EnumNames));
				}
			}
			else if (evaluationType.IsArray || evaluationType.IsTuple) {
				if(EvaluationTypes.TryGetArray(value, out Array? array)) {
					foreach(object entry in array) {
						ValidateData(entry, evaluationType.ElementType);
					}
					return;
				}
				else {
					throw new FormatException($"Invalid data type (expected an {(evaluationType.IsArray ? "array" : "tuple")} but got {value.GetType()}).");
				}
			}
			else if (TupleUtils.IsTupleObject(value)) {
				foreach(TypeField field in evaluationType.Fields) {
					ValidateData(field.GetValue(value), field.Type);
				}
			}
		}

	}

}
