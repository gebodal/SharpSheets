using System.Collections.Generic;
using System;
using System.Linq;
using SharpSheets.Layouts;
using SharpSheets.Colors;
using SharpSheets.Widgets;
using SharpSheets.Shapes;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;
using SharpSheets.Utilities;
using SharpSheets.Fonts;
using SharpSheets.Exceptions;
using SixLabors.ImageSharp;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Parsing {

	public static class SharpFactory {

		/// <summary></summary>
		/// <exception cref="ReflectionTypeLoadException"></exception>
		public static Dictionary<Type, ConstructorInfo> GetConstructors(Type supertype, params Type[] requiredConstructorArguments) {
			Dictionary<Type, ConstructorInfo> types = typeof(SharpFactory).Assembly // AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
				.GetTypes()
				.Where(t => t.IsClass && !t.IsAbstract && supertype.IsAssignableFrom(t)) // t.IsSubclassOf(supertype)
				.Select(t => t.GetConstructors().Where(c => c.GetParameters().Zip(requiredConstructorArguments, (p, a) => p.ParameterType == a).All()).FirstOrDefault())
				.Where(c => c != null && c.DeclaringType != null)
				.ToDictionary(c => c!.DeclaringType!, c => c!);
			return types;
		}

		public static bool IsParsableStruct(Type type) {
			return type.IsValueType
				&& Nullable.GetUnderlyingType(type) is null // Shouldn't there be a way to deal with these better?
				&& !type.IsEnum
				&& !type.IsPrimitive
				&& type != typeof(UFloat)
				&& type != typeof(Dimension)
				&& type != typeof(Vector)
				&& type != typeof(Position)
				&& type != typeof(Colors.Color)
				&& type != typeof(Margins)
				&& !TupleUtils.IsTupleType(type);
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		/// <exception cref="SharpFactoryException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="SystemException"></exception>
		public static object? CreateParameter(string parameterName, Type parameterType, bool useLocal, bool isOptional, object? defaultValue, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, out SharpParsingException[] buildErrors) {
			if (parameterType == typeof(WidgetSetup)) {
				//IContext setupContext = typeof(IWidget).IsAssignableFrom(declaringType) ? context : new NamedContext(context, parameterName, forceLocal: useLocal);
				return (WidgetSetup)Construct(WidgetFactory.widgetSetupConstructor, context, source, widgetFactory, shapeFactory, Array.Empty<object>(), out buildErrors);
			}
			else if (parameterType == typeof(ChildHolder)) {
				if (widgetFactory == null) { throw new SharpParsingException(context.Location, $"No WidgetFactory provided for constructing \"{parameterName}\"."); }
				IContext? childContext = context.GetNamedChild(parameterName);
				if (childContext != null) {
					IWidget child = widgetFactory.MakeWidget(typeof(Div), childContext, source, out buildErrors);
					if (child is not Div) {
						List<SharpParsingException> childErrors = new List<SharpParsingException>(buildErrors) {
							new SharpParsingException(childContext.Location, "Invalid child element.")
						};
						throw new SharpFactoryException(childErrors, $"Errors found parsing named child \"{parameterName}\".");
					}
					return new ChildHolder((Div)child); // Activator.CreateInstance(parameterType, child);
				}
				else if (!isOptional) {
					throw new MissingParameterException(context.Location, parameterName, typeof(Div), $"No entry for required named child \"{parameterName}\" ({parameterType.Name}) provided.");
				}
			}
			else if (typeof(IWidget).IsAssignableFrom(parameterType)) {
				// For widgets created as parameters, the setup is taken from the parent, not a unique context for the parameter
				if (widgetFactory == null) { throw new SharpParsingException(context.Location, $"No WidgetFactory provided for constructing \"{parameterName}\"."); }
				List<SharpParsingException> widgetErrors = new List<SharpParsingException>();
				WidgetSetup setup = (WidgetSetup)Construct(WidgetFactory.widgetSetupConstructor, context, source, widgetFactory, shapeFactory, Array.Empty<object>(), out SharpParsingException[] setupBuildErrors);
				widgetErrors.AddRange(setupBuildErrors);
				IContext widgetContext = new NamedContext(context, parameterName, forceLocal: useLocal);
				IWidget widget = widgetFactory.MakeWidget(parameterType, widgetContext, source, out SharpParsingException[] widgetBuildErrors, setup);
				widgetErrors.AddRange(widgetBuildErrors);
				if(widget is not null) {
					buildErrors = widgetErrors.ToArray();
					return widget;
				}
				else {
					throw new SharpFactoryException(widgetErrors, $"Errors found parsing argument \"{parameterName}\".");
				}
			}
			else if (typeof(IShape).IsAssignableFrom(parameterType)) {
				/*
				IContext shapeContext;

				DocumentSpan? dictLocation = null;
				Exception dictEx = null;
				if (context.HasProperty(parameterName, useLocal, context) &&
					DictionaryContext.TryGetContext(
						parameterName,
						context.GetProperty(parameterName, useLocal, context, DictionaryContext.EmptyContextString, out dictLocation),
						out IContext dictShapeContext, out dictEx)
					) {

					shapeContext = dictShapeContext;
				}
				else {
					new NamedContext(context, parameterName, forceLocal: useLocal).HasProperty("style", useLocal, context, out DocumentSpan? styleLocation);
					shapeContext = new NamedContext(context, parameterName, location: styleLocation, forceLocal: useLocal);
				}

				if (dictEx != null) {
					throw new SharpParsingException(dictLocation, dictEx.Message, dictEx);
				}
				*/

				if (shapeFactory is null) {
					throw new ArgumentNullException(nameof(shapeFactory), "Cannot construct shape, as no ShapeFactory instance provided.");
				}

				new NamedContext(context, parameterName, forceLocal: useLocal).HasProperty("style", useLocal, context, out DocumentSpan? styleLocation);
				IContext shapeContext = new NamedContext(context, parameterName, location: styleLocation, forceLocal: useLocal);

				string? contextNameProperty = context.GetProperty("name", true, context, null, out DocumentSpan? nameLocation);
				try {
					string? contextName = contextNameProperty != null ? ValueParsing.Parse<string>(contextNameProperty, source) : null;
					return shapeFactory.MakeShape(parameterType, shapeContext, contextName, source, out buildErrors);
				}
				catch (FormatException e) {
					throw new SharpParsingException(nameLocation ?? context.Location, e.Message, e);
				}
			}
			else if(parameterType.IsNumbered(out Type? numberedElementType)) {
				try {
					Type numberedType = typeof(Numbered<>).MakeGenericType(numberedElementType);
					INumbered numbered = (INumbered?)Activator.CreateInstance(numberedType) ?? throw new SharpParsingException(context.Location, $"Could not initialize {parameterType} object.");
					
					object? elementDefaultValue;
					if (numberedElementType.IsValueType) {
						elementDefaultValue = Activator.CreateInstance(numberedElementType);
					}
					else {
						elementDefaultValue = null;
					}

					Regex paramRegex = new Regex(@"^" + Regex.Escape(parameterName) + @"(?<number>[1-9][0-9]*)(?:\.|$)", RegexOptions.IgnoreCase);

					int[] numbers = ((numberedElementType == typeof(ChildHolder)) switch {
						true => context.NamedChildren.GetKeys(),
						false => context.GetAllProperties(useLocal).Select(p => p.Name).Concat(context.GetAllFlags(useLocal).Select(p => p.Name))
					}).Select(n => paramRegex.Match(n))
						.Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value))
						.Distinct().OrderBy(i => i)
						.ToArray();

					List<SharpParsingException> fatalErrors = new List<SharpParsingException>();
					List<SharpParsingException> allParamErrors = new List<SharpParsingException>();
					for (int i = 0; i < numbers.Length; i++) {
						int num = numbers[i];
						int index = num - 1;
						string numberedName = parameterName + num.ToString();

						try {
							numbered.Add(index, CreateParameter(numberedName, numberedElementType, useLocal, isOptional, elementDefaultValue, context, source, widgetFactory, shapeFactory, out SharpParsingException[] paramErrors));
							allParamErrors.AddRange(paramErrors);
						}
						catch (SharpParsingException e) {
							//throw e;
							fatalErrors.Add(e);
						}
						catch (SharpFactoryException e) {
							fatalErrors.AddRange(e.Errors);
						}
					}
					if (fatalErrors.Count > 0) { throw new SharpFactoryException(fatalErrors, $"Errors parsing numbered entries."); }

					buildErrors = allParamErrors.ToArray();
					return numbered;
				}
				catch(TargetInvocationException e) {
					throw new SharpParsingException(context.Location, $"Error instantiating Numbered parameter \"{parameterName}\".", e);
				}
			}
			else if (parameterType.TryGetGenericTypeDefinition() == typeof(List<>)) {
				try {
					Type listElementType = parameterType.GetGenericArguments().Single();
					Type listType = typeof(List<>).MakeGenericType(listElementType);
					IList entries = (IList?)Activator.CreateInstance(listType) ?? throw new SharpParsingException(context.Location, $"Could not initialize {listType} object.");
					
					List<SharpParsingException> errors = new List<SharpParsingException>();
					foreach (ContextValue<string> entry in context.GetEntries(context)) {
						try {
							entries.Add(ValueParsing.Parse(entry.Value, listElementType, source));
						}
						catch (FormatException e) {
							errors.Add(new SharpParsingException(entry.Location, e.Message, e));
						}
					}
					//if (errors.Count > 0) { throw new SharpFactoryException(errors, $"Errors parsing entries."); } // $"Errors parsing entries for {declaringType.Name}."
					buildErrors = errors.ToArray();
					return entries;
				}
				catch(TargetInvocationException e) {
					throw new SharpParsingException(context.Location, $"Error instantiating entries parameter \"{parameterName}\".", e);
				}
			}
			else if (typeof(ISharpArgsGrouping).IsAssignableFrom(parameterType) || IsParsableStruct(parameterType)) {
				// We are dealing with a struct or class not covered by Parse
				// TODO Do we still want to accept structs here? Classes are probably a better way to go...
				ConstructorInfo constructor = ValueParsing.GetSimpleConstructor(parameterType);
				IContext argContext = new NamedContext(context, parameterName, forceLocal: useLocal);
				return Construct(constructor, argContext, source, widgetFactory, shapeFactory, Array.Empty<object>(), out buildErrors);
			}
			else {
				// From here on, we are dealing with types that rely on a single line in the configuration

				bool isProvided = parameterType == typeof(bool) ? context.HasFlag(parameterName, useLocal, context) : context.HasProperty(parameterName, useLocal, context);
				if (!isProvided && !isOptional) {
					throw new MissingParameterException(context.Location, parameterName, parameterType, $"No value for required parameter \"{parameterName}\" ({parameterType.Name}) provided.");
				}

				if (parameterType == typeof(bool)) {
					if (context.HasFlag(parameterName, useLocal, context)) {
						buildErrors = Array.Empty<SharpParsingException>();
						return context.GetFlag(parameterName, useLocal, context);
					}
					// No else needed - can just use default value (as required parameters already checked)
				}
				else {
					string? value = context.GetProperty(parameterName, useLocal, context, null, out DocumentSpan? location);

					if (value != null) {
						try {
							buildErrors = Array.Empty<SharpParsingException>();

							object? parsed;
							if (typeof(ISharpDictArg).IsAssignableFrom(parameterType)) {
								ConstructorInfo dictConstructor = ValueParsing.GetSimpleConstructor(parameterType);
								parsed = ValueParsing.ParseValueDict(value, dictConstructor, source);
							}
							else {
								parsed = ValueParsing.Parse(value, parameterType, source);
							}

							// This feels very ugly here.
							if(parsed is FontPath parsedFontPath && !parsedFontPath.IsKnownEmbeddable) {
								buildErrors = new SharpParsingException[] {
									new FontLicenseWarningException(location ?? context.Location, "This font may have licensing restrictions.")
								};
							}

							return parsed;
						}
						catch (FormatException e) {
							throw new SharpParsingException(location, e.Message, e);
						}
						catch (ArgumentException e) {
							throw new SharpParsingException(location, e.Message, e);
						}
						catch (NotImplementedException) {
							throw new SharpParsingException(location, $"No parser for parameter \"{parameterName}\" (of type {parameterType}) provided.");
						}
					}
					// else just use default value
				}
			}

			buildErrors = Array.Empty<SharpParsingException>();
			return defaultValue;
		}

		public static string NormaliseParameterName(string name) {
			return name.Replace("_", "").ToLowerInvariant();
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="SharpFactoryException"></exception>
		private static object?[] GatherParameters(ConstructorInfo constructor, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, object[] firstParameters, out SharpParsingException[] buildErrors) {
			ParameterInfo[] parameterList = constructor.GetParameters();
			object?[] parameters = new object?[parameterList.Length];

			for (int i = 0; i < firstParameters.Length; i++) {
				if (firstParameters[i] == null && (!parameterList[i].ParameterType.IsValueType || Nullable.GetUnderlyingType(parameterList[i].ParameterType) != null)) {
					// If the first parameter is null, check that it can be null
					parameters[i] = firstParameters[i];
				}
				else if (parameterList[i].ParameterType.IsAssignableFrom(firstParameters[i].GetType())) {
					parameters[i] = firstParameters[i];
				}
				else {
					throw new InvalidOperationException($"Provided parameter {i} ({firstParameters[i].GetType().Name}) does not match expected type {parameterList[i].ParameterType.Name}.");
				}
			}

			List<SharpParsingException> errors = new List<SharpParsingException>();

			bool nonOptionalFailed = false;
			for (int i = firstParameters.Length; i < parameterList.Length; i++) {
				string? baseParamName = parameterList[i].Name;
				if (baseParamName is null) { continue; }

				string parameterName = NormaliseParameterName(baseParamName);
				Type parameterType = parameterList[i].ParameterType;
				bool useLocal = baseParamName[0] == '_';

				bool success = false;

				try {
					parameters[parameterList[i].Position] = CreateParameter(
						parameterName,
						parameterType,
						useLocal,
						parameterList[i].IsOptional,
						parameterList[i].HasDefaultValue ? parameterList[i].DefaultValue : Type.Missing,
						context,
						source,
						widgetFactory,
						shapeFactory,
						out SharpParsingException[] paramBuildErrors);
					success = true;
					errors.AddRange(paramBuildErrors);
				}
				// TODO catch (SharpInitializationException e) { }
				catch (SharpParsingException e) {
					//throw e;
					errors.Add(e);
				}
				catch (SharpFactoryException e) {
					errors.AddRange(e.Errors);
				}
				catch (ArgumentNullException e) {
					errors.Add(new SharpParsingException(context.Location, e.Message, e));
				}
				catch(SystemException e) {
					errors.Add(new SharpParsingException(context.Location, e.Message, e));
				}

				if (!success) {
					if (parameterList[i].IsOptional && parameterList[i].HasDefaultValue) { // Do we need to check both?
						parameters[parameterList[i].Position] = parameterList[i].DefaultValue;
					}
					else {
						nonOptionalFailed = true;
					}
				}
			}

			if (nonOptionalFailed) {
				// There must be errors (errors.Count > 0) for us to be in this state
				throw new SharpFactoryException(errors, $"Errors found while parsing {constructor.DeclaringType?.Name ?? "UNKNOWN"} at line {context.Location.Line}.\n" + string.Join("\n", errors.Select(e => e.Message)));
			}

			buildErrors = errors.ToArray();
			return parameters;
		}

		/// <summary></summary>
		/// <exception cref="SharpParsingException"></exception>
		/// <exception cref="SharpFactoryException"></exception>
		public static object Construct(ConstructorInfo constructor, IContext context, DirectoryPath source, WidgetFactory? widgetFactory, ShapeFactory? shapeFactory, object[] firstParameters, out SharpParsingException[] buildErrors) {
			try {
				object?[] parameters = GatherParameters(constructor, context, source, widgetFactory, shapeFactory, firstParameters, out buildErrors);

				return constructor.Invoke(parameters);
			}
			catch (TargetInvocationException e) {
				if (e.InnerException is SharpInitializationException sharpInitializationException) {
					throw MakeFactoryException(sharpInitializationException, context, constructor); 
				}
				else if (e.InnerException != null) { 
					throw new SharpParsingException(context.Location, e.InnerException.Message, e); 
				}
				else { 
					throw new SharpParsingException(context.Location, $"Error found while constructing {constructor.DeclaringType?.Name ?? "UNKNOWN"} at line {context.Location.Line}.");
				}
			}
			catch(InvalidOperationException e) {
				throw new SharpParsingException(context.Location, e.Message, e);
			}
			catch(SystemException e) {
				throw new SharpParsingException(context.Location, e.Message, e);
			}
			catch(TargetParameterCountException e) {
				throw new SharpParsingException(context.Location, e.Message, e); // TODO Should we catch this here, or let it bubble up? (As it's really an application error, not a parsing one)
			}
		}

		private static SharpFactoryException MakeFactoryException(SharpInitializationException exception, IContext context, ConstructorInfo constructor) {
			List<SharpParsingException> initializationErrors = new List<SharpParsingException>();
			foreach (string arg in exception.Arguments) {
				Match match = Regex.Match(arg, @"(?<parameter>[A-Za-z_][A-Za-z0-9_]+)(\<(?<index>[0-9]+)\>)?");
				if (match.Success && constructor.GetParameters().Where(p => p.Name == match.Groups["parameter"].Value).FirstOrDefault() is ParameterInfo param) {
					if(param.Name is null) { continue; }
					string parameterName = NormaliseParameterName(param.Name);
					bool useLocal = param.Name[0] == '_';
					DocumentSpan? location = null;
					if (param.ParameterType == typeof(bool)) {
						context.HasFlag(parameterName, useLocal, context, out location);
					}
					else if (param.ParameterType.TryGetGenericTypeDefinition() == typeof(List<>)) {
						try {
							int index = int.Parse(match.Groups["index"].Value); // TODO This can be dealt with better inside SharpInitializationException
							location = context.GetEntries(context).ToList()[index].Location;
						}
						catch(FormatException) { } // Just ignore
						catch (SharpParsingException) { } // Just ignore
					}
					else {
						context.HasProperty(parameterName, useLocal, context, out location);
					}
					initializationErrors.Add(new SharpParsingException(location ?? context.Location, exception.Message, exception));
				}
				else {
					initializationErrors.Add(new SharpParsingException(context.Location, exception.Message, exception));
				}
			}
			return new SharpFactoryException(initializationErrors, exception.Message);
		}
	}

	public class ChildHolder {
		public Div Child { get; }
		public ChildHolder(Div child) {
			this.Child = child;
		}
	}

	/// <summary>
	/// Implementing this interface indicates that this object is to be interpreted as nested arguments by SharpFactory.
	/// </summary>
	public interface ISharpArgsGrouping { }

	/// <summary>
	/// Implementing this interface indicates that this object can be constructed as a dict expression by SharpFactory.
	/// </summary>
	public interface ISharpDictArg { }

	public interface INumbered : IEnumerable<KeyValuePair<int, object?>> {
		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		void Add(int index, object? value);

		bool HasEntry(int index);
		object? MinEntry();

		object? Get(int index);

		int NumEntries { get; }
		int Length { get; }
	}

	public class Numbered<T> : INumbered {

		private int maxIndex; // "int?" better?
		private readonly Dictionary<int, T?> entries;

		public Numbered() {
			maxIndex = -1;
			this.entries = new Dictionary<int, T?>();
		}

		public Numbered(IEnumerable<T> source) : this() {
			int index = 0;
			foreach(T value in source) {
				Add(index, value);
				index++;
			}
		}

		/// <summary></summary>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public void Add(int index, T? entry) {
			if(index < 0) {
				throw new ArgumentOutOfRangeException(nameof(index), "Indexes for Numbered entries must not be less than zero.");
			}
			entries.Add(index, entry);
			if (index > maxIndex) { maxIndex = index; }
		}

		void INumbered.Add(int index, object? value) {
			if(value is null) {
				Add(index, default);
			}
			else if (value is T entry) {
				Add(index, entry);
			}
			else {
				throw new ArgumentException($"Invalid value type of {value?.GetType().Name ?? "UNKNOWN"} for Numbered<{typeof(T).Name}>.");
			}
		}

		IEnumerator<KeyValuePair<int, object?>> IEnumerable<KeyValuePair<int, object?>>.GetEnumerator() => entries.OrderBy(kv => kv.Key).Select(kv => new KeyValuePair<int, object?>(kv.Key, kv.Value)).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this).GetEnumerator();

		public bool HasEntry(int index) {
			return entries.ContainsKey(index);
		}

		bool INumbered.HasEntry(int index) => HasEntry(index);

		/// <summary></summary>
		/// <exception cref="KeyNotFoundException"></exception>
		public T? this[int index] {
			get {
				if(entries.TryGetValue(index, out T? entry)) {
					return entry;
				}
				else {
					throw new KeyNotFoundException($"No entry in Numbered<{typeof(T).Name}> for entry {index}.");
				}
			}
		}

		object? INumbered.Get(int index) => this[index];

		public T? MinEntry() {
			return entries.OrderBy(kv => kv.Key).Select(kv => kv.Value).FirstOrDefault();
		}

		object? INumbered.MinEntry() => MinEntry();

		public int NumEntries => entries.Count;
		//public int MaxIndex { get { return maxIndex; } }
		public int Length { get { return maxIndex + 1; } }
	}

	public static class NumberedUtils {

		public static bool IsNumbered(this Type type, [MaybeNullWhen(false)] out Type elementType) {
			if(type.TryGetGenericTypeDefinition() == typeof(Numbered<>)) {
				elementType = type.GetGenericArguments().Single();
				return true;
			}
			else {
				elementType = null;
				return false;
			}
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public static INumbered ConvertArrayObjectToNumbered(Array array, Type numberedType) {
			if (numberedType.TryGetGenericTypeDefinition() != typeof(Numbered<>)) {
				throw new ArgumentException("Invalid Numbered<> type provided.");
			}

			INumbered numbered = MakeNumbered(numberedType, out Type numberedElementType);

			for (int i=0; i<array.Length; i++) {
				object? entry = array.GetValue(i);
				if (entry == null || numberedElementType.IsAssignableFrom(entry.GetType())) {
					numbered.Add(i, entry);
				}
			}

			return numbered;
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		private static INumbered MakeNumbered(Type numberedType, out Type elementType) {
			Type numberedElementType;
			try {
				numberedElementType = numberedType.GetGenericArguments().Single();
			}
			catch (InvalidOperationException e) {
				throw new InvalidOperationException($"Invalid {nameof(INumbered)} type provided.", e);
			}
			catch (NotSupportedException e) {
				throw new InvalidOperationException($"Invalid {nameof(INumbered)} type provided.", e);
			}
			elementType = numberedElementType;

			INumbered? numbered;
			try {
				numbered = (INumbered?)Activator.CreateInstance(numberedType);
			}
			catch (TargetInvocationException e) {
				throw new InvalidOperationException($"Could not instantiate {nameof(INumbered)} instance.", e);
			}
			catch (SystemException e) {
				throw new InvalidOperationException($"Could not instantiate {nameof(INumbered)} instance.", e);
			}

			if (numbered is null) {
				throw new InvalidOperationException($"Could not initialize {numberedType} object.");
			}

			return numbered;
		}

		public static IEnumerable<T?> TakeContinuous<T>(this Numbered<T> source, int n) {
			T? value = source.MinEntry();
			for (int i = 0; i < n; i++) {
				if (source.HasEntry(i)) {
					value = source[i];
				}
				yield return value;
			}
		}

		public static IEnumerable<object?> TakeContinuous(this INumbered source, int n) {
			object? value = source.MinEntry();
			for (int i = 0; i < n; i++) {
				if (source.HasEntry(i)) {
					value = source.Get(i);
				}
				yield return value;
			}
		}

	}

}