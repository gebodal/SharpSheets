using SharpSheets.Utilities;
using System.Collections;
using SharpSheets.Parsing;
using SharpSheets.Documentation;
using SharpSheets.Markup.Patterns;
using SharpSheets.Exceptions;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Widgets;

namespace SharpSheets.Shapes {

	[Factory(typeof(IBox), new Type[] { typeof(float) }, new string[] { "aspect" }, new bool[] { false }, typeof(NoOutline))]
	[Factory(typeof(ILabelledBox), new Type[] { typeof(float) }, new string[] { "aspect" }, new bool[] { false }, typeof(SimpleLabelledBox))]
	[Factory(typeof(IEntriedShape), new Type[] { typeof(float) }, new string[] { "aspect" }, new bool[] { false }, typeof(SimpleEntried))]
	[Factory(typeof(IBar), new Type[] { typeof(float) }, new string[] { "aspect" }, new bool[] { false }, typeof(SimpleBar))]
	[Factory(typeof(IUsageBar), new Type[] { typeof(float) }, new string[] { "aspect" }, new bool[] { false }, typeof(SimpleUsageBar))]
	[Factory(typeof(ITitledBox), new Type[] { typeof(float) }, new string[] { "aspect" }, new bool[] { false }, typeof(BlockTitledBox))]
	[Factory(typeof(ITitleStyle), new Type[0], new string[0], new bool[0], typeof(Named))]
	[Factory(typeof(IDetail), new Type[0], new string[0], new bool[0], typeof(Blank))]
	public sealed partial class ShapeFactory : ITypeDetailsCollection {

		public static readonly ShapeFactory StaticOnly = new ShapeFactory();

		private readonly IMarkupRegistry? customStyles;
		private readonly WidgetFactory dummyWidgetFactory;

		public ShapeFactory(IMarkupRegistry customStyles) {
			this.customStyles = customStyles;
			dummyWidgetFactory = new WidgetFactory(MarkupRegistry.Empty, this);
		}

		private ShapeFactory() {
			this.customStyles = null;
			dummyWidgetFactory = new WidgetFactory(MarkupRegistry.Empty, this);
		}

		private static TypeDetailsCollection? _allStaticBuilderDetails;
		private static TypeDetailsCollection AllStaticBuilderDetails {
			get {
				if (_allStaticBuilderDetails == null) {
					Dictionary<DisplayType, BuilderDetails> builders = new Dictionary<DisplayType, BuilderDetails>();
					foreach(BuilderDetails builderDetails in GetAllBuilderNames().GetValues()) {
						DisplayType builderType = builderDetails.DeclaringType;
						if (builders.TryGetValue(builderType, out BuilderDetails? existing)) {
							if (existing.DisplayType.IsSimple<IBox>() && builderDetails.DisplayType.IsSimple<ITitledBox>()) {
								builders[builderType] = builderDetails;
							}
						}
						else {
							builders.Add(builderType, builderDetails);
						}
					}
					_allStaticBuilderDetails = new TypeDetailsCollection(builders.Values, SharpDocuments.StringComparer);
				}
				return _allStaticBuilderDetails;
			}
		}

		private static HashSet<string>? _allStaticShapeNames;
		private static HashSet<string> AllStaticShapeNames {
			get {
				if (_allStaticShapeNames == null) {
					_allStaticShapeNames = new HashSet<string>(AllStaticBuilderDetails.Select(b => b.Name));
				}
				return _allStaticShapeNames;
			}
		}

		public static Type? GetDefaultStyle(Type type) {
			return TryGetDefault(type, out Type? defaultType) ? defaultType : null;
		}

		#region Make Examples

		public static IShape? GetDefaultShape(Type type) {
			Type? defaultStyle = GetDefaultStyle(type);

			return defaultStyle != null ? BuildExample(type, defaultStyle.Name, null, out _) : null; // TODO Very sketchy. This should be redone.
		}

		public static IShape? GetExampleShape(Type type, string style) {
			return BuildExample(type, style, null, out _);
		}

		private static IShape? BuildExample(Type shapeType, string styleName, DirectoryPath? source, out SharpParsingException[] buildErrors) {
			source ??= new DirectoryPath(Directory.GetCurrentDirectory());

			if (shapeType == typeof(IBox)) {
				return Build_IBox(styleName, Context.Empty, -1f, source, out buildErrors);
			}
			else if (shapeType == typeof(ILabelledBox)) {
				return Build_ILabelledBox(styleName, Context.Empty, -1f, source, out buildErrors);
			}
			else if (shapeType == typeof(IEntriedShape)) {
				return Build_IEntriedShape(styleName, Context.Empty, -1f, source, out buildErrors);
			}
			else if (shapeType == typeof(IBar)) {
				return Build_IBar(styleName, Context.Empty, -1f, source, StaticOnly, out buildErrors);
			}
			else if (shapeType == typeof(IUsageBar)) {
				return Build_IUsageBar(styleName, Context.Empty, -1f, source, out buildErrors);
			}
			else if (shapeType == typeof(ITitledBox)) {
				return Build_ITitledBox(styleName, Context.Empty, -1f, source, out buildErrors);
			}
			else if (shapeType == typeof(ITitleStyle)) {
				return Build_ITitleStyle(styleName, Context.Empty, source, StaticOnly, out buildErrors);
			}
			else if (shapeType == typeof(IDetail)) {
				return Build_IDetail(styleName, Context.Empty, source, out buildErrors);
			}
			else {
				buildErrors = Array.Empty<SharpParsingException>();
				return null;
			}
		}

		public IShape MakeExample(Type type, string style, DirectoryPath source, out List<SharpParsingException> errors) {
			
			IShape? shape = null;
			errors = new List<SharpParsingException>();

			try {
				if (AllStaticBuilderDetails.TryGetValue(style, out BuilderDetails? staticBuilderDetails) && staticBuilderDetails.DisplayType.IsAssignableTo(type)) {
					if (staticBuilderDetails.Example != null) {
						shape = (IShape)staticBuilderDetails.Example.Value;
					}
					else {
					IContext context = new BuilderContext(staticBuilderDetails, new Dictionary<string, object>() { { "style", staticBuilderDetails.FullName } });
					shape = MakeShape(type, context, source, out SharpParsingException[] shapeBuildErrors);
					errors.AddRange(shapeBuildErrors);
				}
				}
				else if (GetCustomStylePattern<MarkupShapePattern>(style) is MarkupShapePattern pattern && pattern.MakeExample(dummyWidgetFactory, this, false, out SharpParsingException[] markupBuildErrors) is IShape markupShape) {
					shape = markupShape;
					errors.AddRange(markupBuildErrors);
				}
				else {
					throw new SharpParsingException(DocumentSpan.Imaginary, $"Unrecognized shape style: {style} ({type.Name})");
				}
			}
			catch (InvalidCastException e) {
				//shape = new Simple(-1, stroke: Colors.Color.Red);
				errors.Add(new SharpParsingException(DocumentSpan.Imaginary, "Invalid shape type.", e));
			}
			catch (MissingParameterException e) {
				//shape = new Simple(-1, stroke: Colors.Color.Red);
				errors.Add(e);
			}
			catch (SharpParsingException e) {
				//shape = new Simple(-1, stroke: Colors.Color.Red);
				errors.Add(e);
			}
			catch (SharpFactoryException e) {
				//shape = new Simple(-1, stroke: Colors.Color.Red);
				errors.AddRange(e.Errors);
			}

			return shape ?? MakeShape(type, Context.Empty, source, out _);
		}

		public static Type[]? GetRequiredArguments(Type type) {
			if (type == typeof(IBox)) {
				return new Type[] { typeof(float) };
			}
			else if(type == typeof(ILabelledBox)) {
				return new Type[] { typeof(float) };
			}
			else if(type == typeof(IEntriedShape)) {
				return new Type[] { typeof(float) };
			}
			else if(type == typeof(IBar)) {
				return new Type[] { typeof(float) };
			}
			else if(type == typeof(IUsageBar)) {
				return new Type[] { typeof(float) };
			}
			else if (type == typeof(ITitledBox)) {
				return new Type[] { typeof(float), typeof(string) };
			}
			else if (type == typeof(ITitleStyle)) {
				return Array.Empty<Type>();
			}
			else if(type == typeof(IDetail)) {
				return Array.Empty<Type>();
			}
			else {
				return null;
			}
		}

		#endregion

		public static string? GetStyleNameFromContext(IContext context, out DocumentSpan? location) {
			return context.GetProperty("style", false, context, null, out location);
		}

		public IBox MakeBox(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			buildErrors = null!;
			
			string? styleName = GetStyleNameFromContext(context, out DocumentSpan? location);
			if (styleName is not null) {
				if (CanBuild_IBox(styleName)) {
					IBox? constructed = Build_IBox(styleName, context, aspect, source, out buildErrors);
					if (constructed is not null) {
						return constructed;
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern<IBox>>(styleName) is MarkupShapePattern<IBox> customPattern) {
					return customPattern.MakeShape(context, new AreaShapeParams(aspect), source, this, false, out buildErrors);
				}
				else {
					buildErrors = new SharpParsingException[] { new SharpParsingException(location, $"Unrecognized style \"{styleName}\" for box.") };
				}
			}

			IBox fallback = Build_IBox_Default(context, aspect, source, out SharpParsingException[] defaultBuildErrors);
			// If build errors is null, then we didn't attempt an override above, so the fallback is the correct set of errors
			buildErrors = buildErrors is null ? defaultBuildErrors : buildErrors;
			return fallback;
		}

		public ILabelledBox MakeLabelledBox(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			buildErrors = null!;

			string? styleName = GetStyleNameFromContext(context, out DocumentSpan? location);
			if (styleName is not null) {
				if (CanBuild_ILabelledBox(styleName)) {
					ILabelledBox? constructed = Build_ILabelledBox(styleName, context, aspect, source, out buildErrors);
					if (constructed is not null) {
						return constructed;
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern<ILabelledBox>>(styleName) is MarkupShapePattern<ILabelledBox> customPattern) {
					return customPattern.MakeShape(context, new AreaShapeParams(aspect), source, this, false, out buildErrors);
				}
				else {
					buildErrors = new SharpParsingException[] { new SharpParsingException(location, $"Unrecognized style \"{styleName}\" for labelled box.") };
				}
			}

			ILabelledBox fallback = Build_ILabelledBox_Default(context, aspect, source, out SharpParsingException[] defaultBuildErrors);
			// If build errors is null, then we didn't attempt an override above, so the fallback is the correct set of errors
			buildErrors = buildErrors is null ? defaultBuildErrors : buildErrors;
			return fallback;
		}

		public IEntriedShape MakeEntried(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			buildErrors = null!;

			string? styleName = GetStyleNameFromContext(context, out DocumentSpan? location);
			if (styleName is not null) {
				if (CanBuild_IEntriedShape(styleName)) {
					IEntriedShape? constructed = Build_IEntriedShape(styleName, context, aspect, source, out buildErrors);
					if (constructed is not null) {
						return constructed;
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern<IEntriedShape>>(styleName) is MarkupShapePattern<IEntriedShape> customPattern) {
					return customPattern.MakeShape(context, new AreaShapeParams(aspect), source, this, false, out buildErrors);
				}
				else {
					buildErrors = new SharpParsingException[] { new SharpParsingException(location, $"Unrecognized style \"{styleName}\" for entried shape.") };
				}
			}

			IEntriedShape fallback = Build_IEntriedShape_Default(context, aspect, source, out SharpParsingException[] defaultBuildErrors);
			// If build errors is null, then we didn't attempt an override above, so the fallback is the correct set of errors
			buildErrors = buildErrors is null ? defaultBuildErrors : buildErrors;
			return fallback;
		}

		public IBar MakeBar(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			buildErrors = null!;

			string? styleName = GetStyleNameFromContext(context, out DocumentSpan? location);
			if (styleName is not null) {
				if (CanBuild_IBar(styleName)) {
					IBar? constructed = Build_IBar(styleName, context, aspect, source, this, out buildErrors);
					if (constructed is not null) {
						return constructed;
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern<IBar>>(styleName) is MarkupShapePattern<IBar> customPattern) {
					return customPattern.MakeShape(context, new AreaShapeParams(aspect), source, this, false, out buildErrors);
				}
				else {
					buildErrors = new SharpParsingException[] { new SharpParsingException(location, $"Unrecognized style \"{styleName}\" for bar.") };
				}
			}

			IBar fallback = Build_IBar_Default(context, aspect, source, this, out SharpParsingException[] defaultBuildErrors);
			// If build errors is null, then we didn't attempt an override above, so the fallback is the correct set of errors
			buildErrors = buildErrors is null ? defaultBuildErrors : buildErrors;
			return fallback;
		}

		private IUsageBar BuildConcreteUsageBar(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			buildErrors = null!;

			string? styleName = GetStyleNameFromContext(context, out DocumentSpan? location);
			if (styleName is not null) {
				if (CanBuild_IUsageBar(styleName)) {
					IUsageBar? constructed = Build_IUsageBar(styleName, context, aspect, source, out buildErrors);
					if (constructed is not null) {
						return constructed;
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern<IUsageBar>>(styleName) is MarkupShapePattern<IUsageBar> customPattern) {
					return customPattern.MakeShape(context, new AreaShapeParams(aspect), source, this, false, out buildErrors);
				}
				else {
					buildErrors = new SharpParsingException[] { new SharpParsingException(location, $"Unrecognized style \"{styleName}\" for box.") };
				}
			}

			IUsageBar fallback = Build_IUsageBar_Default(context, aspect, source, out SharpParsingException[] defaultBuildErrors);
			// If build errors is null, then we didn't attempt an override above, so the fallback is the correct set of errors
			buildErrors = buildErrors is null ? defaultBuildErrors : buildErrors;
			return fallback;
		}

		public IUsageBar MakeUsageBar(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			IUsageBar? bar;
			if (this.IsBarPattern(context.GetProperty("style", false, context, ""))) {
				bar = new SlashedUsageBar(this.MakeBar(context, -1, source, out buildErrors) ?? new SimpleBar(-1), aspect);
			}
			else {
				bar = this.BuildConcreteUsageBar(context, aspect, source, out buildErrors);
			}
			return bar;
		}

		public ITitledBox MakeTitledBox(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			buildErrors = null!;

			string? styleName = GetStyleNameFromContext(context, out DocumentSpan? location);
			if (styleName is not null) {
				if (CanBuild_ITitledBox(styleName)) {
					ITitledBox? constructed = Build_ITitledBox(styleName, context, aspect, source, out buildErrors);
					if (constructed is not null) {
						return constructed;
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern<ITitledBox>>(styleName) is MarkupShapePattern<ITitledBox> customPattern) {
					return customPattern.MakeShape(context, new AreaShapeParams(aspect), source, this, false, out buildErrors);
				}
				else {
					buildErrors = new SharpParsingException[] { new SharpParsingException(location, $"Unrecognized style \"{styleName}\" for titled box.") };
				}
			}

			ITitledBox fallback = Build_ITitledBox_Default(context, aspect, source, out SharpParsingException[] defaultBuildErrors);
			// If build errors is null, then we didn't attempt an override above, so the fallback is the correct set of errors
			buildErrors = buildErrors is null ? defaultBuildErrors : buildErrors;
			return fallback;
		}

		public ITitleStyle MakeTitleStyle(IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			buildErrors = null!;

			string? styleName = GetStyleNameFromContext(context, out DocumentSpan? location);
			if (styleName is not null) {
				if (CanBuild_ITitleStyle(styleName)) {
					ITitleStyle? constructed = Build_ITitleStyle(styleName, context, source, this, out buildErrors);
					if (constructed is not null) {
						return constructed;
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern<ITitleStyle>>(styleName) is MarkupShapePattern<ITitleStyle> customPattern) {
					return customPattern.MakeShape(context, new TitleStyleParams(), source, this, false, out buildErrors);
				}
				else {
					buildErrors = new SharpParsingException[] { new SharpParsingException(location, $"Unrecognized style \"{styleName}\" for title style.") };
				}
			}

			ITitleStyle fallback = Build_ITitleStyle_Default(context, source, this, out SharpParsingException[] defaultBuildErrors);
			// If build errors is null, then we didn't attempt an override above, so the fallback is the correct set of errors
			buildErrors = buildErrors is null ? defaultBuildErrors : buildErrors;
			return fallback;
		}

		public IDetail MakeDetail(IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			buildErrors = null!;

			string? styleName = GetStyleNameFromContext(context, out DocumentSpan? location);
			if (styleName is not null) {
				if (CanBuild_IDetail(styleName)) {
					IDetail? constructed = Build_IDetail(styleName, context, source, out buildErrors);
					if (constructed is not null) {
						return constructed;
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern<IDetail>>(styleName) is MarkupShapePattern<IDetail> customPattern) {
					return customPattern.MakeShape(context, new DetailParams(), source, this, false, out buildErrors);
				}
				else {
					buildErrors = new SharpParsingException[] { new SharpParsingException(location, $"Unrecognized style \"{styleName}\" for detail.") };
				}
			}

			IDetail fallback = Build_IDetail_Default(context, source, out SharpParsingException[] defaultBuildErrors);
			// If build errors is null, then we didn't attempt an override above, so the fallback is the correct set of errors
			buildErrors = buildErrors is null ? defaultBuildErrors : buildErrors;
			return fallback;
		}

		public IShape MakeShape(Type shapeType, IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			if (typeof(IAreaShape).IsAssignableFrom(shapeType)) {
				float aspect = context.GetProperty("aspect", true, context, -1f, float.Parse);
				string? styleName = GetStyleNameFromContext(context, out _);

				if (shapeType == typeof(IBox)) {
					return MakeBox(context, aspect, source, out buildErrors);
				}
				else if (shapeType == typeof(ILabelledBox)) {
					return MakeLabelledBox(context, aspect, source, out buildErrors);
				}
				else if (shapeType == typeof(IEntriedShape)) {
					return MakeEntried(context, aspect, source, out buildErrors);
				}
				else if (shapeType == typeof(IBar)) {
					return MakeBar(context, aspect, source, out buildErrors);
				}
				else if (shapeType == typeof(IUsageBar)) {
					return MakeUsageBar(context, aspect, source, out buildErrors);
				}
				else if (shapeType == typeof(ITitledBox)) {
					return MakeTitledBox(context, aspect, source, out buildErrors);
				}
			}
			else if (shapeType == typeof(ITitleStyle)) {
				return MakeTitleStyle(context, source, out buildErrors);
			}
			else if (shapeType == typeof(IDetail)) {
				return MakeDetail(context, source, out buildErrors);
			}

			throw new ArgumentException($"Provided type {shapeType.Name} is not a valid subtype of {nameof(IShape)}.");
		}

		/*
		private bool IsTitledBoxPattern(string style) {
			return CanBuild_ITitledBox(style) || IsCustomStylePattern<ITitledBox>(style);
		}
		*/

		private bool IsBarPattern(string style) {
			return CanBuild_IBar(style) || IsCustomStylePattern<IBar>(style);
		}

		#region Custom Style Patterns

		private IEnumerable<string> GetAllCustomStyleNames() {
			if (customStyles != null) {
				return customStyles.GetValidNames<MarkupShapePattern>(AllStaticShapeNames);
			}
			else {
				return Enumerable.Empty<string>();
			}
		}

		private T? GetCustomStylePattern<T>(string styleName) where T : MarkupShapePattern {
			if (customStyles != null && customStyles.GetPattern<T>(PatternName.Parse(styleName)) is T pattern) {
				return pattern;
			}
			else {
				return null;
			}
		}

		private bool IsCustomStylePattern(string name) {
			if (customStyles == null || name == null) {
				return false;
			}
			return customStyles.IsPattern<MarkupShapePattern>(PatternName.Parse(name));
		}

		private bool IsCustomStylePattern<T>(string name) where T : IShape {
			if (customStyles == null || name == null) {
				return false;
			}
			MarkupShapePattern? pattern = customStyles.GetPattern<MarkupShapePattern>(PatternName.Parse(name));
			return pattern != null && pattern is MarkupShapePattern<T>;
		}

		private BuilderDetails? GetCustomStyleBuilder(string name) {
			if (customStyles == null || name == null) {
				return null;
			}
			return customStyles.GetBuilder<MarkupShapePattern>(PatternName.Parse(name));
		}

		#endregion

		#region Type Utilities

		public string[] GetAllNames() {
			return AllStaticBuilderDetails.Select(b => b.Name).Concat(GetAllCustomStyleNames()).Distinct().ToArray();
		}

		public bool ContainsKey(Type type) {
			return AllStaticBuilderDetails.ContainsKey(type);
		}

		public bool ContainsKey(string name) {
			return AllStaticBuilderDetails.ContainsKey(name) || IsCustomStylePattern(name);
		}

		public bool TryGetValue(Type type, [MaybeNullWhen(false)] out BuilderDetails builder) {
			return AllStaticBuilderDetails.TryGetValue(type, out builder);
		}

		public bool TryGetValue(string name, [MaybeNullWhen(false)] out BuilderDetails builder) {
			if(GetCustomStyleBuilder(name) is BuilderDetails customBuilder) {
				builder = customBuilder;
				return true;
			}
			else {
				return AllStaticBuilderDetails.TryGetValue(name, out builder);
			}
		}

		public IEnumerator<BuilderDetails> GetEnumerator() {
			if (customStyles != null) {
				return new BuilderDetailsUniqueNameEnumerator(
					AllStaticBuilderDetails.Concat(customStyles.GetAllBuilderDetails<MarkupShapePattern>()),
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
				.ConcatOrNothing(customStyles?.GetMinimalBuilderNames<MarkupShapePattern>(AllStaticShapeNames));
		}

		#endregion

		public abstract class ShapeParams {
			public abstract object[] ToArray();

			public TParams As<TParams>() where TParams : ShapeParams {
				return (this as TParams) ?? throw new InvalidOperationException($"Cannot convert {this.GetType().FullName} to {typeof(TParams).FullName}.");
			}
		}

		public class AreaShapeParams(float aspect) : ShapeParams {
			public float Aspect => aspect;

			public override object[] ToArray() {
				return new object[] { aspect };
			}
		}

		public class TitleStyleParams : ShapeParams {
			public override object[] ToArray() {
				return Array.Empty<object>();
			}
		}

		public class DetailParams : ShapeParams {
			public override object[] ToArray() {
				return Array.Empty<object>();
			}
		}

	}
}