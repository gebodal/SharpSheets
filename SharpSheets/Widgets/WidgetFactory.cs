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

	public class WidgetFactory : ITypeDetailsCollection {

		private readonly IMarkupRegistry customWidgets;
		private readonly ShapeFactory? shapeFactory;

		private ParseOrigins<IDocumentEntity>? origins = null;

		public WidgetFactory(IMarkupRegistry customWidgets, ShapeFactory? shapeFactory) {
			this.customWidgets = customWidgets;
			this.shapeFactory = shapeFactory;
		}

		#region Static Initialisation and Accessors

		private static readonly Dictionary<Type, MethodInfo> widgetBuildersByType;
		private static readonly Dictionary<string, MethodInfo> widgetBuildersByName;
		private static readonly Dictionary<Type, string> widgetTypeNames;
		private static readonly HashSet<string> staticWidgetNames;

		public static readonly MethodInfo widgetSetupBuilder;
		public static readonly BuilderDoc widgetSetupBuilderDoc;

		/// <summary></summary>
		/// <exception cref="TypeInitializationException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="ReflectionTypeLoadException"></exception>
		/// <exception cref="NotSupportedException"></exception>
		/// <exception cref="IOException"></exception>
		static WidgetFactory() {
			SharpDocumentation.LoadEmbeddedDocumentation(typeof(SharpWidget).Assembly);

			widgetBuildersByType = SharpFactory.GetBuilders(typeof(IWidget), typeof(WidgetSetup));
			widgetBuildersByName = widgetBuildersByType.ToDictionary(kv => kv.Key.Name, kv => kv.Value, SharpDocuments.StringComparer);

			widgetSetupBuilder = SharpFactory.GetBuilder(typeof(WidgetSetup)) ?? throw new InvalidOperationException("Cannot find WidgetSetup builder method.");
			widgetSetupBuilderDoc = SharpDocumentation.GetBuilderDoc(widgetSetupBuilder) ?? throw new TypeInitializationException(nameof(WidgetFactory), null);

			widgetTypeNames = widgetBuildersByType.ToDictionary(kv =>  FactoryBuilderAttribute.GetBuilderType(kv.Value), kv => kv.Key.Name);

			staticWidgetNames = new HashSet<string>(widgetBuildersByName.Keys, SharpDocuments.StringComparer);
		}

		private static TypeDetailsCollection? _allStaticBuilderDetails;
		private static TypeDetailsCollection AllStaticBuilderDetails {
			get {
				if (_allStaticBuilderDetails == null) {
					List<BuilderDetails> builders = new List<BuilderDetails>();
					foreach ((string widgetName, MethodInfo widgetBuilder) in widgetBuildersByName) {
						builders.Add(DocumentationGenerator.GetBuilderDetails(typeof(SharpWidget), widgetBuilder, widgetName));
					}
					_allStaticBuilderDetails = new TypeDetailsCollection(builders, SharpDocuments.StringComparer);
				}
				return _allStaticBuilderDetails;
			}
		}

		private static BuilderDetails? _widgetSetupBuilder;
		public static BuilderDetails WidgetSetupBuilder {
			get {
				if (_widgetSetupBuilder == null) {
					_widgetSetupBuilder = DocumentationGenerator.GetBuilderDetails(typeof(WidgetSetup), widgetSetupBuilder, "WidgetSetup");
				}
				return _widgetSetupBuilder;
			}
		}

		private static BuilderDetails? _divBuilder;
		public static BuilderDetails DivBuilder {
			get {
				if (_divBuilder == null) {
					if(!AllStaticBuilderDetails.TryGetValue(typeof(Div), out _divBuilder)) {
						throw new NotSupportedException("Builder for Div not found."); // This should never happen
					}
				}
				return _divBuilder;
			}
		}

		public static MethodInfo? GetBuilderInfo(Type type) {
			return widgetBuildersByType.TryGetValue(type, out MethodInfo? builder) ? builder : null;
		}

		private static MethodInfo? GetBuilderInfo(string name) {
			return widgetBuildersByName.TryGetValue(name, out MethodInfo? builder) ? builder : null;
		}

		private static IEnumerable<Regex> GetNamedChildren(MethodInfo builder) {
			foreach (ParameterInfo parameter in builder.GetParameters()) {
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

		private static bool IsNamedChild(MethodInfo builder, string child) {
			foreach(ParameterInfo parameter in builder.GetParameters()) {
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

		private IWidget? ConstructWidget(MethodInfo builder, IContext context, DirectoryPath source, WidgetSetup? knownSetup, out SharpParsingException[] buildErrors) {
			if (knownSetup.HasValue) {
				return (IWidget?)SharpFactory.Build(builder, context, source, this, shapeFactory, new object[] { knownSetup.Value }, out buildErrors);
			}
			else {
				return (IWidget?)SharpFactory.Build(builder, context, source, this, shapeFactory, Array.Empty<object>(), out buildErrors);
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
				else if (widgetBuildersByName.TryGetValue(type, out MethodInfo? widgetBuilder)) {
					IWidget? builtWidget = ConstructWidget(widgetBuilder, context, source, knownSetup, out SharpParsingException[] widgetBuildErrors);
					// TODO This error widget construction can be improved
					widget = builtWidget ?? MakeErrorWidget("Could not construct widget.", new InvalidOperationException("Could not construct widget."), context, source, out _);
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
			if (this.origins is not null) { this.origins.Add(widget, context); }

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
					object? constructed = SharpFactory.Build(widgetSetupBuilder, context, source, this, shapeFactory, Array.Empty<object>(), out SharpParsingException[] setupBuildErrors);
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

		public Div? MakeDiv(IContext context, DirectoryPath source, out SharpParsingException[] buildErrors, WidgetSetup? knownSetup = null) {
			return MakeWidget(typeof(Div), context, source, out buildErrors, knownSetup: knownSetup) as Div;
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
				else if (widgetBuildersByName.TryGetValue(type, out MethodInfo? widgetBuilder)) {
					//throw new SharpParsingException(DocumentSpan.Imaginary, "Cannot create example of built-in widget type.");
					if (AllStaticBuilderDetails.TryGetValue(type, out BuilderDetails? builder)) {
						IContext context = new BuilderContext(builder, new Dictionary<string, object>());
						IWidget? builtExample = ConstructWidget(widgetBuilder, context, source, exampleSetup, out buildErrors);
						widget = builtExample ?? MakeErrorWidget("Could not build example.", new InvalidOperationException("Could not build example."), context, source, out _);
					}
					else {
						throw new SharpParsingException(DocumentSpan.Imaginary, $"No matching builder details found for {type}.");
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
			if (GetBuilderInfo(parent) is MethodInfo builder) {
				return IsNamedChild(builder, child);
			}
			else if (GetCustomWidgetPattern(parent) is MarkupWidgetPattern pattern) {
				return pattern.HasNamedChild(child);
			}
			else {
				return false;
			}
		}

		public IEnumerable<Regex> GetNamedChildren(string parentType) {
			if (GetBuilderInfo(parentType) is MethodInfo builder) {
				return GetNamedChildren(builder);
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

		private BuilderDetails? GetCustomWidgetBuilder(string name) {
			if (customWidgets == null || name == null) {
				return null;
			}
			return customWidgets.GetBuilder<MarkupWidgetPattern>(PatternName.Parse(name));
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
			return widgetBuildersByName.Keys.Concat(GetAllCustomWidgetNames()).Distinct().ToArray();
		}

		public bool ContainsKey(Type type) {
			return widgetBuildersByType.ContainsKey(type);
		}

		public bool ContainsKey(string name) {
			return widgetBuildersByName.ContainsKey(name) || IsCustomWidgetPattern(name);
		}

		public bool TryGetValue(Type type, [MaybeNullWhen(false)] out BuilderDetails builder) {
			return AllStaticBuilderDetails.TryGetValue(type, out builder);
		}

		public bool TryGetValue(string name, [MaybeNullWhen(false)] out BuilderDetails builder) {
			if (AllStaticBuilderDetails.TryGetValue(name, out builder)) {
				return true;
			}
			else {
				builder = GetCustomWidgetBuilder(name);
				return builder is BuilderDetails;
			}
		}

		public IEnumerator<BuilderDetails> GetEnumerator() {
			if (customWidgets != null) {
				return new BuilderDetailsUniqueNameEnumerator(
					AllStaticBuilderDetails.Concat(customWidgets.GetAllBuilderDetails<MarkupWidgetPattern>()),
					SharpDocuments.StringComparer);
			}
			else {
				return AllStaticBuilderDetails.GetEnumerator();
			}
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public IEnumerable<KeyValuePair<string, BuilderDetails>> GetBuilderNames() {
			return AllStaticBuilderDetails.Select(c => new KeyValuePair<string, BuilderDetails>(c.FullName, c))
				.Concat(customWidgets.GetMinimalBuilderNames<MarkupWidgetPattern>(staticWidgetNames));
		}

		#endregion

		#region Origin-Tracking

		private class OriginTrackingFactory : WidgetFactory {
			public OriginTrackingFactory(WidgetFactory factory, ParseOrigins<IDocumentEntity> origins) : base(factory.customWidgets, factory.shapeFactory) {
				this.origins = origins;
			}
		}

		public WidgetFactory TrackOrigins(ParseOrigins<IDocumentEntity> origins) {
			return new OriginTrackingFactory(this, origins);
		}

		#endregion

	}

}