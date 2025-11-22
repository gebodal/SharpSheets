using SharpSheets.Shapes;
using System.Collections;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Documentation;
using SharpSheets.Markup.Patterns;
using SharpSheets.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Widgets {

	[Factory(typeof(IWidget), new Type[] { typeof(WidgetSetup) }, new string[] { "setup" }, new bool[] { false }, null)]
	[Factory(typeof(WidgetSetup), new Type[0], new string[0], new bool[0], typeof(WidgetSetup))]
	public partial class WidgetFactory : ITypeDetailsCollection {

		private readonly IMarkupRegistry? customWidgets;
		private readonly ShapeFactory? shapeFactory;

		private readonly ParseOrigins<IDocumentEntity>? origins;

		public WidgetFactory(IMarkupRegistry customWidgets, ShapeFactory? shapeFactory) : this(customWidgets, shapeFactory, null) { }

		private WidgetFactory(IMarkupRegistry? customWidgets, ShapeFactory? shapeFactory, ParseOrigins<IDocumentEntity>? origins) {
			this.customWidgets = customWidgets;
			this.shapeFactory = shapeFactory;
			this.origins = origins;
		}

		//public static BuilderDetails WidgetSetupBuilder => BuilderDocs.SharpSheets_Widgets_WidgetSetup;
		public static BuilderDetails DivBuilder => BuilderDocs.SharpSheets_Widgets_Div;

		private IWidget ConstructWidget(MarkupWidgetPattern pattern, IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return pattern.MakeWidget(context, source, this, shapeFactory, out buildErrors);
		}

		public IWidget MakeWidget(string type, IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			IWidget widget;
			List<SharpParsingException> errors = new List<SharpParsingException>();
			
			try {
				if (CanBuild_IWidget(type)) {
					WidgetSetup setup = Build_WidgetSetup(context, source, shapeFactory ?? ShapeFactory.StaticOnly, out SharpParsingException[] setupBuildErrors);
					errors.AddRange(setupBuildErrors);
					IWidget? builtWidget = Build_IWidget(type, context, setup, source, this, shapeFactory ?? ShapeFactory.StaticOnly, out SharpParsingException[] widgetBuildErrors);
					widget = builtWidget ?? MakeErrorWidget(new InvalidOperationException($"Could not construct widget ({type})."), context, source, out _);
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
				widget.AddChild(MakeWidget(child.SimpleName, child, source, out SharpParsingException[] childErrors));
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
					setup = Build_WidgetSetup(context, source, shapeFactory ?? ShapeFactory.StaticOnly, out SharpParsingException[] setupBuildErrors);
					buildErrors = setupBuildErrors;
				}
				catch (Exception) { }
			}

			return new ErrorWidget(message, e, setup);
		}
		private ErrorWidget MakeErrorWidget(Exception e, IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeErrorWidget(e.Message, e, context, source, out buildErrors);
		}

		public Page MakePage(IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			List<SharpParsingException> errors = new List<SharpParsingException>();
			IWidget pageWidget = MakeWidget("Page", context, source, out SharpParsingException[] baseErrors);
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

		public Div? MakeDiv(IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeWidget(typeof(Div), context, source, out buildErrors) as Div;
		}

		public IWidget MakeWidget(Type type, IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			if (!TryGetIWidgetDetails(type, out BuilderDetails? builder)) {
				return MakeErrorWidget(new InvalidOperationException($"No known widget builder for type {type}."), context, source, out buildErrors);
			}
			else {
				return MakeWidget(builder.FullName, context, source, out buildErrors);
			}
		}

		#region Example Widget

		public IWidget MakeExample(string type, DirectoryPath source, bool diagnostic, out List<SharpParsingException> errors) {
			IWidget widget;
			errors = new List<SharpParsingException>();

			SharpParsingException[]? buildErrors;
			try {
				WidgetSetup exampleSetup = new WidgetSetup(gutter: 8f, _diagnostic: diagnostic);

				if (CanBuild_IWidget(type)) {
					if (TryGetIWidgetDetails(type, out BuilderDetails? builder)) {
						IContext context = new BuilderContext(builder, new Dictionary<string, object>());
						IWidget? builtExample = Build_IWidget(type, context, exampleSetup, source, this, shapeFactory ?? ShapeFactory.StaticOnly, out buildErrors);
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

		#endregion

		#region Custom Style Patterns

		private IEnumerable<string> GetAllCustomWidgetNames() {
			if (customWidgets != null) {
				return customWidgets.GetValidNames<MarkupWidgetPattern>(AllIWidgetNames);
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
			return GetIWidgetBuilderNames().GetValues().Select(b => b.Name).Concat(GetAllCustomWidgetNames()).Distinct().ToArray();
		}

		public bool ContainsKey(Type type) {
			return ContainsIWidgetDetails(type);
		}

		public bool ContainsKey(string name) {
			return ContainsIWidgetDetails(name) || IsCustomWidgetPattern(name);
		}

		public bool TryGetValue(Type type, [MaybeNullWhen(false)] out BuilderDetails builder) {
			return TryGetIWidgetDetails(type, out builder);
		}

		public bool TryGetValue(string name, [MaybeNullWhen(false)] out BuilderDetails builder) {
			if (TryGetIWidgetDetails(name, out builder)) {
				return true;
			}
			else {
				builder = GetCustomWidgetBuilder(name);
				return builder is not null;
			}
		}

		public IEnumerator<BuilderDetails> GetEnumerator() {
			if (customWidgets != null) {
				return new BuilderDetailsUniqueNameEnumerator( // Is this necessary?
					GetIWidgetBuilderNames().GetValues().Concat(customWidgets.GetAllBuilderDetails<MarkupWidgetPattern>()),
					SharpDocuments.StringComparer);
			}
			else {
				return GetIWidgetBuilderNames().GetValues().GetEnumerator();
			}
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public IEnumerable<KeyValuePair<string, BuilderDetails>> GetBuilderNames() {
			return GetIWidgetBuilderNames().GetValues().Select(c => new KeyValuePair<string, BuilderDetails>(c.FullName, c))
				.ConcatOrNothing(customWidgets?.GetMinimalBuilderNames<MarkupWidgetPattern>(AllIWidgetNames));
		}

		#endregion

		#region Origin-Tracking

		private class OriginTrackingFactory : WidgetFactory {
			public OriginTrackingFactory(WidgetFactory factory, ParseOrigins<IDocumentEntity> origins) : base(factory.customWidgets, factory.shapeFactory, origins) { }
		}

		public WidgetFactory TrackOrigins(ParseOrigins<IDocumentEntity> origins) {
			return new OriginTrackingFactory(this, origins);
		}

		#endregion

	}

}