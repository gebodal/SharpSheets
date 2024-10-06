using System.Collections.Generic;
using System;
using System.Linq;
using SharpSheets.Shapes;
using System.Reflection;
using System.Collections;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Documentation;
using SharpSheets.Markup.Patterns;
using SharpSheets.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SharpSheets.Widgets {

	public interface IWidgetFactory {
		IWidget MakeWidget(string type, IContext context, DirectoryPath source, out List<SharpParsingException> errors, WidgetSetup? knownSetup = null);
		IWidget MakeWidget(Type type, IContext context, DirectoryPath source, out List<SharpParsingException> errors, WidgetSetup? knownSetup = null);
		Page MakePage(IContext context, DirectoryPath source, out List<SharpParsingException> errors, WidgetSetup? knownSetup = null);
	}

	public class WidgetFactory : ITypeDetailsCollection {

		private readonly IMarkupRegistry customWidgets;
		private readonly ShapeFactory? shapeFactory;

		private Dictionary<object, IDocumentEntity>? origins = null;

		public WidgetFactory(IMarkupRegistry customWidgets, ShapeFactory? shapeFactory) {
			this.customWidgets = customWidgets;
			this.shapeFactory = shapeFactory;
		}

		#region Static Initialisation and Accessors

		private static readonly Dictionary<Type, ConstructorInfo> widgetConstructorsByType;
		private static readonly Dictionary<string, ConstructorInfo> widgetConstructorsByName;
		private static readonly Dictionary<Type, string> widgetTypeNames;
		private static readonly HashSet<string> staticWidgetNames;

		public static readonly ConstructorInfo widgetSetupConstructor;
		public static readonly ConstructorDoc widgetSetupConstructorDoc;

		/// <summary></summary>
		/// <exception cref="TypeInitializationException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="ReflectionTypeLoadException"></exception>
		/// <exception cref="NotSupportedException"></exception>
		/// <exception cref="IOException"></exception>
		static WidgetFactory() {
			SharpDocumentation.LoadEmbeddedDocumentation(typeof(SharpWidget).Assembly);

			widgetConstructorsByType = SharpFactory.GetConstructors(typeof(SharpWidget), typeof(WidgetSetup));
			widgetConstructorsByName = widgetConstructorsByType.ToDictionary(kv => kv.Key.Name, kv => kv.Value, SharpDocuments.StringComparer);

			widgetSetupConstructor = typeof(WidgetSetup).GetConstructors().First();
			widgetSetupConstructorDoc = SharpDocumentation.GetConstructorDoc(widgetSetupConstructor) ?? throw new TypeInitializationException(nameof(WidgetFactory), null);

			widgetTypeNames = widgetConstructorsByType.ToDictionary(kv => kv.Value.DeclaringType!, kv => kv.Key.Name);

			staticWidgetNames = new HashSet<string>(widgetConstructorsByName.Keys, SharpDocuments.StringComparer);
		}

		private static TypeDetailsCollection? _allStaticConstructorDetails;
		private static TypeDetailsCollection AllStaticConstructorDetails {
			get {
				if (_allStaticConstructorDetails == null) {
					List<ConstructorDetails> constructors = new List<ConstructorDetails>();
					foreach (KeyValuePair<string, ConstructorInfo> entry in widgetConstructorsByName) {
						constructors.Add(DocumentationGenerator.GetConstructorDetails(typeof(SharpWidget), entry.Value, entry.Key));
					}
					_allStaticConstructorDetails = new TypeDetailsCollection(constructors, SharpDocuments.StringComparer);
				}
				return _allStaticConstructorDetails;
			}
		}

		private static ConstructorDetails? _widgetSetupConstructor;
		public static ConstructorDetails WidgetSetupConstructor {
			get {
				if (_widgetSetupConstructor == null) {
					_widgetSetupConstructor = DocumentationGenerator.GetConstructorDetails(typeof(WidgetSetup), widgetSetupConstructor, "WidgetSetup");
				}
				return _widgetSetupConstructor;
			}
		}

		private static ConstructorDetails? _divConstructor;
		public static ConstructorDetails DivConstructor {
			get {
				if (_divConstructor == null) {
					if(!AllStaticConstructorDetails.TryGetValue(typeof(Div), out _divConstructor)) {
						throw new NotSupportedException("Constructor for Div not found."); // This should never happen
					}
				}
				return _divConstructor;
			}
		}

		public static ConstructorInfo? GetConstructorInfo(Type type) {
			return widgetConstructorsByType.TryGetValue(type, out ConstructorInfo? constructor) ? constructor : null;
		}

		private static ConstructorInfo? GetConstructorInfo(string name) {
			return widgetConstructorsByName.TryGetValue(name, out ConstructorInfo? constructor) ? constructor : null;
		}

		private static IEnumerable<Regex> GetNamedChildren(ConstructorInfo constructor) {
			foreach (ParameterInfo parameter in constructor.GetParameters()) {
				if (parameter.Name is not null) {
					string paramName = SharpFactory.NormaliseParameterName(parameter.Name);
					if (parameter.ParameterType == typeof(ChildHolder)) {
						yield return new Regex(@"^" + Regex.Escape(paramName) + @"$", RegexOptions.IgnoreCase);
					}
					else if (parameter.ParameterType == typeof(Numbered<ChildHolder>)) {
						yield return new Regex(@"^" + Regex.Escape(paramName) + @"[0-9]+$", RegexOptions.IgnoreCase);
					}
				}
			}
		}

		private static readonly Regex integerRegex = new Regex(@"^[0-9]+$");

		private static bool IsNamedChild(ConstructorInfo constructor, string child) {
			foreach(ParameterInfo parameter in constructor.GetParameters()) {
				if (parameter.Name is not null) {
					string paramName = SharpFactory.NormaliseParameterName(parameter.Name);
					if (parameter.ParameterType == typeof(ChildHolder)) {
						if (SharpDocuments.StringEquals(paramName, child)) {
							return true;
						}
					}
					else if (parameter.ParameterType == typeof(Numbered<ChildHolder>)) {
						if (child.Length > paramName.Length && SharpDocuments.StringEquals(paramName, child[..paramName.Length])) {
							if (integerRegex.IsMatch(child[paramName.Length..])) {
								return true;
							}
						}
					}
				}
			}

			return false;
		}

		#endregion

		private IWidget ConstructWidget(ConstructorInfo constructor, IContext context, DirectoryPath source, WidgetSetup? knownSetup, out SharpParsingException[] buildErrors) {
			if (knownSetup.HasValue) {
				return (IWidget)SharpFactory.Construct(constructor, context, source, this, shapeFactory, new object[] { knownSetup.Value }, out buildErrors);
			}
			else {
				return (IWidget)SharpFactory.Construct(constructor, context, source, this, shapeFactory, Array.Empty<object>(), out buildErrors);
			}
		}

		private IWidget ConstructWidget(MarkupWidgetPattern pattern, IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return pattern.MakeWidget(context, source, this, shapeFactory, out buildErrors);
		}

		// Dictionary<object, IDocumentEntity> origins
		public IWidget MakeWidget(string type, IContext context, DirectoryPath source, out SharpParsingException[] buildErrors, WidgetSetup? knownSetup = null) {
			IWidget widget;
			List<SharpParsingException> errors = new List<SharpParsingException>();
			
			try {
				if (type == null) {
					throw new SharpParsingException(context.Location, $"No widget type provided.");
				}
				else if (widgetConstructorsByName.TryGetValue(type, out ConstructorInfo? widgetConstructor)) {
					widget = ConstructWidget(widgetConstructor, context, source, knownSetup, out SharpParsingException[] widgetBuildErrors);
					errors.AddRange(widgetBuildErrors);
				}
				else if (GetCustomWidgetPattern(type) is MarkupWidgetPattern pattern) {
					widget = ConstructWidget(pattern, context, source, out SharpParsingException[] markupBuildErrors);
					errors.AddRange(markupBuildErrors);
				}
				else {
					throw new SharpParsingException(context.Location, $"Unrecognized widget type: {type}");
				}
			}
			catch (MissingParameterException e) {
				widget = MakeErrorWidget($"{context.SimpleName}: " + $"No value for required parameter \"{e.ParameterName}\".", e, context, source, out SharpParsingException[] errorBuildErrors);
				errors.Add(e);
				errors.AddRange(errorBuildErrors);
			}
			catch (SharpParsingException e) {
				widget = MakeErrorWidget(e.Message, e, context, source, out SharpParsingException[] errorBuildErrors);
				errors.Add(e);
				errors.AddRange(errorBuildErrors);
			}
			catch (SharpFactoryException e) {
				widget = MakeErrorWidget(e.Message, e, context, source, out SharpParsingException[] errorBuildErrors);
				errors.AddRange(e.Errors);
				errors.AddRange(errorBuildErrors);
			}

			// origins will be null in the base class, but can be made not-null in child classes
			if (this.origins != null) { this.origins.Add(widget, context); }

			foreach (IContext child in context.Children) {
				widget.AddChild(MakeWidget(child.SimpleName, child, source, out SharpParsingException[] childErrors, null));
				errors.AddRange(childErrors);
			}

			// Do this here as the widget may decide if it provides a remaining area based on presence of children
			if (!widget.ProvidesRemaining) {
				foreach(IContext child in context.Children) {
					errors.Add(new SharpParsingException(child.Location, "Parent does not provide an area for this widget."));
				}
			}

			buildErrors = errors.ToArray();
			return widget;
		}

		private ErrorWidget MakeErrorWidget(string message, Exception e, IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			WidgetSetup setup = WidgetSetup.ErrorSetup;

			buildErrors = Array.Empty<SharpParsingException>();
			if (context != null) {
				try {
					object constructed = SharpFactory.Construct(widgetSetupConstructor, context, source, this, shapeFactory, Array.Empty<object>(), out SharpParsingException[] setupBuildErrors);
					if (constructed is WidgetSetup constuctedSetup) {
						setup = WidgetSetup.MakeSizedErrorWidget(constuctedSetup.margins, constuctedSetup.size, constuctedSetup.position);
					}
					buildErrors = setupBuildErrors;
				}
				catch (Exception) { }
			}

			return new ErrorWidget(message, e, setup);
		}

		public Page MakePage(IContext context, DirectoryPath source, out SharpParsingException[] buildErrors, WidgetSetup? knownSetup = null) {
			List<SharpParsingException> errors = new List<SharpParsingException>();
			IWidget pageWidget = MakeWidget("Page", context, source, out SharpParsingException[] baseErrors, knownSetup);
			errors.AddRange(baseErrors);
			if(pageWidget is Page page) {
				buildErrors = errors.ToArray();
				return page;
			}
			else {
				Page errorPage = new Page(WidgetSetup.Empty);
				errorPage.AddChild(MakeErrorWidget("Page Error", new SharpParsingException(context.Location, "Unknown error parsing page widget."), context, source, out SharpParsingException[] errorBuildErrors));
				errors.AddRange(errorBuildErrors);
				buildErrors = errors.ToArray();
				return errorPage;
			}
		}

		public IWidget MakeWidget(Type type, IContext context, DirectoryPath source, out SharpParsingException[] buildErrors, WidgetSetup? knownSetup = null) {
			string typeName = widgetTypeNames.GetValueOrDefault(type, "");
			return MakeWidget(typeName, context, source, out buildErrors, knownSetup);
		}

		#region Example Widget

		public IWidget MakeExample(string type, DirectoryPath source, bool diagnostic, out List<SharpParsingException> errors) {
			IWidget widget;
			errors = new List<SharpParsingException>();

			SharpParsingException[]? buildErrors;
			try {
				WidgetSetup exampleSetup = new WidgetSetup(gutter: 8f, _diagnostic: diagnostic);

				if (type == null) {
					throw new SharpParsingException(DocumentSpan.Imaginary, $"No widget type provided.");
				}
				else if (widgetConstructorsByName.TryGetValue(type, out ConstructorInfo? widgetConstructor)) {
					//throw new SharpParsingException(DocumentSpan.Imaginary, "Cannot create example of built-in widget type.");
					if (AllStaticConstructorDetails.TryGetValue(type, out ConstructorDetails? constructor)) {
						IContext context = new ConstructorContext(constructor, new Dictionary<string, object>());
						widget = ConstructWidget(widgetConstructor, context, source, exampleSetup, out buildErrors);
					}
					else {
						throw new SharpParsingException(DocumentSpan.Imaginary, $"No matching constructor details found for {type}.");
					}
				}
				else if (GetCustomWidgetPattern(type) is MarkupWidgetPattern pattern && pattern.MakeExample(this, shapeFactory, exampleSetup, out buildErrors) is IWidget markupWidget) {
					widget = markupWidget;
				}
				else {
					throw new SharpParsingException(DocumentSpan.Imaginary, $"Unrecognized widget type: {type}");
				}
			}
			catch(InvalidCastException e) {
				widget = MakeErrorWidget(e.Message, e, Context.Empty, source, out buildErrors);
				errors.Add(new SharpParsingException(DocumentSpan.Imaginary, "Invalid widget type.", e));
			}
			catch (MissingParameterException e) {
				widget = MakeErrorWidget($"Example: " + $"No value for required parameter \"{e.ParameterName}\".", e, Context.Empty, source, out buildErrors);
				errors.Add(e);
			}
			catch (SharpParsingException e) {
				widget = MakeErrorWidget(e.Message, e, Context.Empty, source, out buildErrors);
				errors.Add(e);
			}
			catch (SharpFactoryException e) {
				widget = MakeErrorWidget(e.Message, e, Context.Empty, source, out buildErrors);
				errors.AddRange(e.Errors);
			}

			if(buildErrors != null) {
				errors.AddRange(buildErrors);
			}

			return widget;
		}

		#endregion

		#region Widget Information

		public bool IsWidget(string name) => ContainsKey(name);

		public bool IsNamedChild(string parent, string child) {
			if (GetConstructorInfo(parent) is ConstructorInfo constructor) {
				return IsNamedChild(constructor, child);
			}
			else if (GetCustomWidgetPattern(parent) is MarkupWidgetPattern pattern) {
				return pattern.HasNamedChild(child);
			}
			else {
				return false;
			}
		}

		public IEnumerable<Regex> GetNamedChildren(string parentType) {
			if (GetConstructorInfo(parentType) is ConstructorInfo constructor) {
				return GetNamedChildren(constructor);
			}
			else if (GetCustomWidgetPattern(parentType) is MarkupWidgetPattern pattern) {
				return pattern.GetNamedChildren();
			}
			else {
				return Enumerable.Empty<Regex>();
			}
		}

		#endregion

		#region Custom Style Patterns

		private IEnumerable<string> GetAllCustomWidgetNames() {
			if (customWidgets != null) {
				//return customWidgets.GetAllPatterns().Where(kv => kv.Value is MarkupWidgetPattern).GetKeys();
				return customWidgets.GetValidNames<MarkupWidgetPattern>(staticWidgetNames);
			}
			else {
				return Enumerable.Empty<string>();
			}
		}

		private bool IsCustomWidgetPattern(string name) {
			if (customWidgets == null || name == null) {
				return false;
			}
			return customWidgets.IsPattern<MarkupWidgetPattern>(PatternName.Parse(name));
		}

		private ConstructorDetails? GetCustomWidgetConstructor(string name) {
			if (customWidgets == null || name == null) {
				return null;
			}
			return customWidgets.GetConstructor<MarkupWidgetPattern>(PatternName.Parse(name));
		}

		private MarkupWidgetPattern? GetCustomWidgetPattern(string name) {
			if (customWidgets == null || name == null) {
				return null;
			}
			return customWidgets.GetPattern<MarkupWidgetPattern>(PatternName.Parse(name));
		}

		#endregion

		#region Type Utilities

		public string[] GetAllNames() {
			return widgetConstructorsByName.Keys.Concat(GetAllCustomWidgetNames()).Distinct().ToArray();
		}

		public bool ContainsKey(Type type) {
			return widgetConstructorsByType.ContainsKey(type);
		}

		public bool ContainsKey(string name) {
			return widgetConstructorsByName.ContainsKey(name) || IsCustomWidgetPattern(name);
		}

		public bool TryGetValue(Type type, [MaybeNullWhen(false)] out ConstructorDetails constructor) {
			return AllStaticConstructorDetails.TryGetValue(type, out constructor);
		}

		public bool TryGetValue(string name, [MaybeNullWhen(false)] out ConstructorDetails constructor) {
			if (AllStaticConstructorDetails.TryGetValue(name, out constructor)) {
				return true;
			}
			else {
				constructor = GetCustomWidgetConstructor(name);
				return constructor is ConstructorDetails;
			}
		}

		public IEnumerator<ConstructorDetails> GetEnumerator() {
			if (customWidgets != null) {
				return new ConstructorDetailsUniqueNameEnumerator(
					AllStaticConstructorDetails.Concat(customWidgets.GetAllConstructorDetails<MarkupWidgetPattern>()),
					SharpDocuments.StringComparer);
			}
			else {
				return AllStaticConstructorDetails.GetEnumerator();
			}
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public IEnumerable<KeyValuePair<string, ConstructorDetails>> GetConstructorNames() {
			return AllStaticConstructorDetails.Select(c => new KeyValuePair<string, ConstructorDetails>(c.FullName, c))
				.Concat(customWidgets.GetMinimalConstructorNames<MarkupWidgetPattern>(staticWidgetNames));
		}

		#endregion

		#region Origin-Tracking

		private class OriginTrackingFactory : WidgetFactory {
			public OriginTrackingFactory(WidgetFactory factory, Dictionary<object, IDocumentEntity> origins) : base(factory.customWidgets, factory.shapeFactory) {
				this.origins = origins;
			}
		}

		public WidgetFactory TrackOrigins(Dictionary<object, IDocumentEntity> origins) {
			return new OriginTrackingFactory(this, origins);
		}

		#endregion

	}

}