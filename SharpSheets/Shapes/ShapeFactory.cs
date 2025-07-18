using SharpSheets.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SharpSheets.Parsing;
using SharpSheets.Documentation;
using SharpSheets.Markup.Patterns;
using SharpSheets.Exceptions;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;

namespace SharpSheets.Shapes {

	public sealed class ShapeFactory : ITypeDetailsCollection {

		private readonly IMarkupRegistry customStyles;

		public ShapeFactory(IMarkupRegistry customStyles) {
			this.customStyles = customStyles;
		}

		#region Static Initialisation

		private static readonly Dictionary<Type, Dictionary<Type, MethodInfo>> shapeBuilders;
		private static readonly Dictionary<Type, Dictionary<string, Type>> shapeNames;
		//private static readonly Dictionary<Type, HashSet<string>> staticShapeNames;
		private static readonly HashSet<string> allStaticShapeNames;

		//private static readonly Dictionary<Type, ConstructorInfo> allConstructors;

		private static readonly Dictionary<Type, Type> defaultStyles = new Dictionary<Type, Type>() {
			[typeof(IContainerShape)] = typeof(NoOutline),
			[typeof(IBox)] = typeof(NoOutline),
			[typeof(ILabelledBox)] = typeof(SimpleLabelledBox),
			[typeof(ITitledBox)] = typeof(BlockTitledBox),
			[typeof(ITitleStyledBox)] = typeof(Named),
			[typeof(IEntriedShape)] = typeof(SimpleEntried),
			[typeof(IBar)] = typeof(SimpleBar),
			[typeof(IUsageBar)] = typeof(SimpleUsageBar),
			[typeof(IDetail)] = typeof(Blank)
		};

		private static readonly Dictionary<Type, Type[]> requiredArgumentsByType = new Dictionary<Type, Type[]>() {
			[typeof(IBox)] = new Type[] { typeof(float) },
			[typeof(ILabelledBox)] = new Type[] { typeof(float) },
			[typeof(ITitledBox)] = new Type[] { typeof(float), typeof(string) },
			[typeof(ITitleStyledBox)] = new Type[] { typeof(IContainerShape), typeof(string) },
			[typeof(IEntriedShape)] = new Type[] { typeof(float) },
			[typeof(IBar)] = new Type[] { typeof(float) },
			[typeof(IUsageBar)] = new Type[] { typeof(float) },
			[typeof(IDetail)] = Array.Empty<Type>()
		};

		static ShapeFactory() {
			shapeBuilders = requiredArgumentsByType
				.ToDictionary(
					kv => kv.Key,
					kv => SharpFactory.GetBuilders(kv.Key, kv.Value)
						.Where(kv => !typeof(MarkupShape).IsAssignableFrom(kv.Key)) // Ignore MarkupDetail
						.ToDictionary()
					);

			shapeNames = shapeBuilders.ToDictionary(kv => kv.Key, kv => kv.Value.ToDictionary(kvc => kvc.Key.Name, kvc => FactoryBuilderAttribute.GetBuilderType(kvc.Value), SharpDocuments.StringComparer));

			//staticShapeNames = shapeNames.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value.Keys, SharpDocuments.StringComparer));
			allStaticShapeNames = new HashSet<string>(shapeNames.SelectMany(kv => kv.Value.Keys), SharpDocuments.StringComparer);

			//allConstructors = shapeConstructors.SelectMany(kv => kv.Value).ToDictionaryAllowRepeats(false);
		}

		private static TypeDetailsCollection? _allStaticBuilderDetails;
		private static TypeDetailsCollection AllStaticBuilderDetails {
			get {
				if (_allStaticBuilderDetails == null) {
					//List<ConstructorDetails> constructors = new List<ConstructorDetails>();
					Dictionary<Type, BuilderDetails> builders = new Dictionary<Type, BuilderDetails>();
					foreach ((Type super, Dictionary<Type, MethodInfo> builderDict) in shapeBuilders) {
						foreach ((Type builderType, MethodInfo builder) in builderDict) {
							BuilderDetails builderDetails = DocumentationGenerator.GetBuilderDetails(super, builder, builderType.Name);
							if (builders.TryGetValue(builderType, out BuilderDetails? existing)) {
								if (existing.DisplayType == typeof(IBox) && super == typeof(ITitledBox)) {
									builders[builderType] = builderDetails;
								}
							}
							else {
								builders.Add(builderType, builderDetails);
							}
						}
					}
					_allStaticBuilderDetails = new TypeDetailsCollection(builders.Values, SharpDocuments.StringComparer);
				}
				return _allStaticBuilderDetails;
			}
		}

		#endregion

		public static Type? GetDefaultStyle(Type type) {
			return defaultStyles.GetValueOrFallback(type, null);
		}

		#region Make Examples

		public static IShape? GetDefaultShape(Type type) {
			Type? defaultStyle = GetDefaultStyle(type);

			return defaultStyle != null ? BuildExample(type, defaultStyle, out _) : null;
		}

		public static IShape? GetExampleShape(Type type, string style) {
			Type? shapeType = shapeNames[type == typeof(IContainerShape) ? typeof(IBox) : type].GetValueOrFallback(style, null);

			return shapeType != null ? BuildExample(type, shapeType, out _) : null;
		}

		private static IShape? BuildExample(Type shapeType, Type styleType, out SharpParsingException[] buildErrors) {

			MethodInfo builder = shapeBuilders[shapeType == typeof(IContainerShape) ? typeof(IBox) : shapeType][styleType];

			object[] defaultArgs;

			if (shapeType == typeof(IContainerShape)) {
				defaultArgs = new object[] { -1f };
			}
			else if (shapeType == typeof(IBox)) {
				defaultArgs = new object[] { -1f };
			}
			else if (shapeType == typeof(ILabelledBox)) {
				defaultArgs = new object[] { -1f };
			}
			else if (shapeType == typeof(ITitledBox)) {
				defaultArgs = new object[] { -1f, "NAME" };
			}
			else if (shapeType == typeof(ITitleStyledBox)) {
				defaultArgs = new object[] { GetDefaultShape(typeof(IContainerShape))!, "NAME" };
			}
			else if (shapeType == typeof(IEntriedShape)) {
				defaultArgs = new object[] { -1f };
			}
			else if (shapeType == typeof(IBar)) {
				defaultArgs = new object[] { -1f };
			}
			else if (shapeType == typeof(IUsageBar)) {
				defaultArgs = new object[] { -1f };
			}
			else if (shapeType == typeof(IDetail)) {
				defaultArgs = Array.Empty<object>();
			}
			else {
				buildErrors = Array.Empty<SharpParsingException>();
				return null;
			}

			return (IShape?)SharpFactory.Build(builder, Context.Empty, new DirectoryPath(Directory.GetCurrentDirectory()), null, null, defaultArgs, out buildErrors);
		}

		public IShape MakeExample(Type type, string style, DirectoryPath source, out List<SharpParsingException> errors) {
			
			IShape shape;
			errors = new List<SharpParsingException>();

			try {
				if (style == null) {
					throw new SharpParsingException(DocumentSpan.Imaginary, $"No shape type provided.");
				}
				else if (shapeNames[type].TryGetValue(style, out Type? styleType)) {
					MethodInfo builder = shapeBuilders[type][styleType];
					Type? shapeType = shapeNames[type == typeof(IContainerShape) ? typeof(IBox) : type].GetValueOrFallback(style, null);

					if (shapeType != null && AllStaticBuilderDetails.TryGetValue(shapeType, out BuilderDetails? builderDoc)) {
						IContext context = new BuilderContext(builderDoc, new Dictionary<string, object>() { { "style", builderDoc.FullName } });
						shape = MakeShape(type, context, builderDoc.Name, source, out SharpParsingException[] shapeBuildErrors);
						errors.AddRange(shapeBuildErrors);
					}
					else {
						throw new SharpParsingException(DocumentSpan.Imaginary, $"No matching builder details found for {shapeType?.Name ?? "UNKNOWN TYPE"}.");
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern>(style) is MarkupShapePattern pattern && pattern.MakeExample(null, this, false, out SharpParsingException[] markupBuildErrors) is IShape markupShape) {
					shape = markupShape;
					errors.AddRange(markupBuildErrors);
				}
				else {
					throw new SharpParsingException(DocumentSpan.Imaginary, $"Unrecognized shape type: {type}");
				}
			}
			catch (InvalidCastException e) {
				shape = new Simple(-1, stroke: Colors.Color.Red);
				errors.Add(new SharpParsingException(DocumentSpan.Imaginary, "Invalid shape type.", e));
			}
			catch (MissingParameterException e) {
				shape = new Simple(-1, stroke: Colors.Color.Red);
				errors.Add(e);
			}
			catch (SharpParsingException e) {
				shape = new Simple(-1, stroke: Colors.Color.Red);
				errors.Add(e);
			}
			catch (SharpFactoryException e) {
				shape = new Simple(-1, stroke: Colors.Color.Red);
				errors.AddRange(e.Errors);
			}

			return shape;
		}

		public static Type[]? GetRequiredArguments(Type type) {
			Type[]? reqArgs = requiredArgumentsByType.GetValueOrFallback(type, null);
			if (reqArgs == null) {
				Type? superType = requiredArgumentsByType.Keys.FirstOrDefault(t => t.IsAssignableFrom(type));
				if (superType != null) {
					reqArgs = requiredArgumentsByType.GetValueOrFallback(superType, null);
				}
			}
			return reqArgs;
		}

		#endregion

		public static string? GetStyleNameFromContext(IContext context, out DocumentSpan? location) {
			return context.GetProperty("style", false, context, null, out location);
		}

		private static MethodInfo GetDefaultBuilder(Type baseType, Type defaultStyle) {
			if (defaultStyle != null && shapeBuilders[baseType].TryGetValue(defaultStyle, out MethodInfo? builder)) {
				return builder;
			}
			else {
				throw new ArgumentException($"Default builder \"{defaultStyle?.Name ?? "ERROR"}\" could not be found for {baseType.Name}.");
			}
		}

		private static bool TryGetBuilder(string styleName, Type baseType, [MaybeNullWhen(false)] out MethodInfo result) {
			Type? style = shapeNames[baseType].GetValueOrFallback(styleName, null);

			if (style != null) {
				if (shapeBuilders[baseType].TryGetValue(style, out MethodInfo? builder)) {
					result = builder;
					return true;
				}
				else {
					throw new ArgumentException($"Could not find builder for built-in {baseType.Name} type \"{style.Name}\".");
				}
			}
			else {
				result = null;
				return false;
			}
		}

		private T? Build<T>(MethodInfo builder, IContext context, object[] shapeParams, DirectoryPath source, out SharpParsingException[] buildErrors) where T : IShape {
			return (T?)SharpFactory.Build(builder, context, source, null, this, shapeParams, out buildErrors);
		}

		private T MakeShape<T>(IContext context, string name, float aspect, object[] shapeParams, Type defaultStyle, DirectoryPath source, out SharpParsingException[] buildErrors) where T : IShape {
			Type baseType = typeof(T);
			string? styleName = GetStyleNameFromContext(context, out DocumentSpan? location);

			buildErrors = null!;

			if (styleName is not null) {
				if (TryGetBuilder(styleName, baseType, out MethodInfo? builder)) {
					T? constructed = Build<T>(builder, context, shapeParams, source, out buildErrors);
					if (constructed is not null) {
						return constructed;
					}
				}
				else if (GetCustomStylePattern<MarkupShapePattern<T>>(styleName) is MarkupShapePattern<T> customPattern) {
					return customPattern.MakeShape(context, name, aspect, source, this, false, out buildErrors);
				}
				else {
					throw new SharpParsingException(location, $"Unrecognized style \"{styleName}\" for {baseType.Name}.");
				}
			}

			// If all else fails
			MethodInfo defaultBuilder = GetDefaultBuilder(baseType, defaultStyle);
			T fallback = Build<T>(defaultBuilder, context, shapeParams, source, out SharpParsingException[] defaultBuildErrors) ?? throw new InvalidOperationException($"Could not build default shape for {typeof(T)}");
			// If we haven't got any build errors, then just use those from the fallback build. (Should the fallback errors be included even if we already tried a build?)
			buildErrors = buildErrors is null ? defaultBuildErrors : buildErrors; //buildErrors.Concat(defaultBuildErrors).ToArray();
			return fallback;
		}

		public IBox MakeBox(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeShape<IBox>(context, "INVALID", aspect, new object[] { aspect }, defaultStyles[typeof(IBox)], source, out buildErrors);
		}

		public ILabelledBox MakeLabelledBox(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeShape<ILabelledBox>(context, "INVALID", aspect, new object[] { aspect }, defaultStyles[typeof(ILabelledBox)], source, out buildErrors);
		}

		public ITitledBox MakeTitledBox(IContext context, float aspect, string name, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeShape<ITitledBox>(context, name, aspect, new object[] { aspect, name }, defaultStyles[typeof(ITitledBox)], source, out buildErrors);
		}

		public ITitleStyledBox MakeTitleStyle(IContext context, IContainerShape box, string name, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeShape<ITitleStyledBox>(context, name, -1f, new object[] { box, name }, defaultStyles[typeof(ITitleStyledBox)], source, out buildErrors);
			//return (ITitleStyledBox)SharpFactory.Construct(GetConstructor(typeof(ITitleStyledBox), context, defaultStyles[typeof(ITitleStyledBox)]), context, source, null, this, new object[] { box, name }, out buildErrors);
		}

		public IEntriedShape MakeEntried(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeShape<IEntriedShape>(context, "INVALID", aspect, new object[] { aspect }, defaultStyles[typeof(IEntriedShape)], source, out buildErrors);
		}

		public IBar MakeBar(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeShape<IBar>(context, "INVALID", aspect, new object[] { aspect }, defaultStyles[typeof(IBar)], source, out buildErrors);
		}

		public IUsageBar MakeUsageBar(IContext context, float aspect, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeShape<IUsageBar>(context, "INVALID", aspect, new object[] { aspect }, defaultStyles[typeof(IUsageBar)], source, out buildErrors);
		}

		public IDetail MakeDetail(IContext context, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeShape<IDetail>(context, "INVALID", -1f, Array.Empty<object>(), defaultStyles[typeof(IDetail)], source, out buildErrors);
		}

		/// <summary></summary>
		/// <exception cref="ArgumentException">Thrown if <paramref name="defaultStyle"/> is not a valid shape of the requested type <paramref name="shapeType"/>.</exception>
		public IShape MakeShape(Type shapeType, IContext context, string? name, Type defaultStyle, DirectoryPath source, out SharpParsingException[] buildErrors) {
			// TODO Feels very strange to be providing the name argument here. Can't we restructure the title styles to work another way?

			if (!shapeType.IsAssignableFrom(defaultStyle)) {
				throw new ArgumentException($"Default style ({defaultStyle.Name}) is not a valid shape of the requested type ({shapeType.Name}).");
			}

			if (typeof(IAreaShape).IsAssignableFrom(shapeType)) {
				float aspect = context.GetProperty("aspect", true, context, -1f, float.Parse);
				string? styleName = GetStyleNameFromContext(context, out _);

				if (shapeType == typeof(IContainerShape)) {
					if (this.IsPattern<ITitledBox>(context.GetProperty("style", false, context, ""))) {
						//return this.MakeTitledBox(context, aspect, context.GetProperty("name", true, context, "NAME"));
						return this.MakeTitledBox(context, aspect, name ?? "NAME", source, out buildErrors);
					}
					else {
						List<SharpParsingException> containerErrors = new List<SharpParsingException>();
						IBox? outline = this.MakeBox(context, aspect, source, out SharpParsingException[] boxBuildErrors);
						containerErrors.AddRange(boxBuildErrors);
						if (!string.IsNullOrEmpty(name)) { // context.HasProperty("name", true, context)
							new NamedContext(context, "title").HasProperty("style", false, context, out DocumentSpan? titleStyleLocation);
							//return this.MakeTitleStyle(new NamedContext(context, "title", line: titleStyleLine), (IBox)outline, context.GetProperty("name", true, context, "NAME"));
							ITitleStyledBox? titleStyledBox = this.MakeTitleStyle(new NamedContext(context, "title", location: titleStyleLocation), ((IBox?)outline) ?? new NoOutline(-1), name ?? "NAME", source, out SharpParsingException[] titleStyleBuildErrors);
							containerErrors.AddRange(titleStyleBuildErrors);
							buildErrors = containerErrors.ToArray();
							return titleStyledBox;
						}
						else {
							buildErrors = containerErrors.ToArray();
							return outline;
						}
					}
				}
				else if (shapeType == typeof(ITitledBox)) {
					return this.MakeTitledBox(context, aspect, name ?? "NAME", source, out buildErrors);
				}
				else if (shapeType == typeof(IBox)) {
					return this.MakeBox(context, aspect, source, out buildErrors);
				}
				else if (shapeType == typeof(ILabelledBox)) {
					return this.MakeLabelledBox(context, aspect, source, out buildErrors);
				}
				else if (shapeType == typeof(IEntriedShape)) {
					return this.MakeEntried(context, aspect, source, out buildErrors);
				}
				else if (shapeType == typeof(IBar)) {
					return this.MakeBar(context, aspect, source, out buildErrors);
				}
				else if (shapeType == typeof(IUsageBar)) {
					IUsageBar? bar;
					if (this.IsPattern<IBar>(context.GetProperty("style", false, context, ""))) {
						bar = new SlashedUsageBar(this.MakeBar(context, -1, source, out buildErrors) ?? new SimpleBar(-1), aspect);
					}
					else {
						bar = this.MakeUsageBar(context, aspect, source, out buildErrors);
					}
					return bar;
				}
			}
			else if (shapeType == typeof(IDetail)) {
				return this.MakeDetail(context, source, out buildErrors);
			}

			throw new ArgumentException($"Provided type {shapeType.Name} is not a valid subtype of IShape.");
		}

		public IShape MakeShape(Type shapeType, IContext context, string? name, DirectoryPath source, out SharpParsingException[] buildErrors) {
			return MakeShape(shapeType, context, name, defaultStyles[shapeType], source, out buildErrors);
		}

		public bool IsPattern<T>(string style) where T : IShape {
			return shapeNames[typeof(T)].ContainsKey(style) || IsCustomStylePattern<T>(style);
		}

		#region Custom Style Patterns

		private IEnumerable<string> GetAllCustomStyleNames() {
			if (customStyles != null) {
				//return customStyles.GetAllPatterns().Where(kv => kv.Value is MarkupShapePattern).GetKeys();
				return customStyles.GetValidNames<MarkupShapePattern>(allStaticShapeNames);
			}
			else {
				return Enumerable.Empty<string>();
			}
		}

		/// <summary></summary>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="styleName"/> is <see langword="null"/>.</exception>
		private T? GetCustomStylePattern<T>(string styleName) where T : MarkupShapePattern {
			if (customStyles == null) {
				return null;
			}
			if (styleName == null) {
				throw new ArgumentNullException(nameof(styleName));
			}

			if (customStyles.GetPattern<T>(PatternName.Parse(styleName)) is T pattern) {
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
			return shapeNames.SelectMany(kv => kv.Value.Keys).Concat(GetAllCustomStyleNames()).Distinct().ToArray();
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
				.Concat(customStyles.GetMinimalBuilderNames<MarkupShapePattern>(allStaticShapeNames));
		}

		#endregion
	}
}