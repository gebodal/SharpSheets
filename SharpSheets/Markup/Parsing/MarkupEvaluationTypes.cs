using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using SharpSheets.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SharpSheets.Canvas;
using SharpSheets.Documentation;
using SharpSheets.Utilities;
using SharpSheets.Widgets;
using System.Diagnostics.CodeAnalysis;
using SharpSheets.Markup.Patterns;
using SharpSheets.Colors;
using SharpSheets.Parsing;
using System.Globalization;
using SharpSheets.Evaluations.Nodes;

namespace SharpSheets.Markup.Parsing {

	public static partial class MarkupEvaluationTypes {

		public static readonly EvaluationContext BaseContext;

		static MarkupEvaluationTypes() {
			BaseContext = Create().Build();
		}

		/*
		public static readonly FloatEvaluationType FLOAT;
		public static readonly UFloatEvaluationType UFLOAT;
		public static readonly IntEvaluationType INT;
		public static readonly UIntEvaluationType UINT;
		public static readonly BoolEvaluationType BOOL;
		public static readonly StringEvaluationType STRING;

		public static readonly ColorEvaluationType COLOR;

		public static readonly DimensionEvaluationType DIMENSION;
		public static readonly MarginsEvaluationType MARGINS;

		public static readonly FilePathEvaluationType FILE_PATH;

		// Enum types
		public static readonly EnumEvaluationType TEXT_FORMAT;
		public static readonly EnumEvaluationType TEXT_HEIGHT_STRATEGY;
		public static readonly EnumEvaluationType JUSTIFICATION;
		public static readonly EnumEvaluationType ALIGNMENT;
		public static readonly EnumEvaluationType LAYOUT;
		public static readonly EnumEvaluationType CHECK_TYPE;

		public static readonly EvaluationType WIDGET;

		private static readonly Dictionary<string, EvaluationType> typeRegistry;

		public static readonly EvaluationType CONTAINER;
		public static readonly EvaluationType BOX;
		public static readonly EvaluationType LABELLED_BOX;
		public static readonly EvaluationType BAR;
		public static readonly EvaluationType USAGE_BAR;
		public static readonly EvaluationType DETAIL;

		static MarkupEvaluationTypes() {

			BaseContext = EvaluationContext.Build(ctx => {
				ctx.SetType<ColorEvaluationType>(new ColorEvaluationType(ctx));
				ctx.SetType<DimensionEvaluationType>(new DimensionEvaluationType(ctx));
				ctx.SetType<MarginsEvaluationType>(new MarginsEvaluationType(ctx));
				ctx.SetType<FilePathEvaluationType>(new FilePathEvaluationType(ctx));
			});

			FLOAT = BaseContext.GetType<FloatEvaluationType>();
			UFLOAT = BaseContext.GetType<UFloatEvaluationType>();
			INT = BaseContext.GetType<IntEvaluationType>();
			UINT = BaseContext.GetType<UIntEvaluationType>();
			BOOL = BaseContext.GetType<BoolEvaluationType>();
			STRING = BaseContext.GetType<StringEvaluationType>();

			COLOR = BaseContext.GetType<ColorEvaluationType>();

			DIMENSION = BaseContext.GetType<DimensionEvaluationType>();
			MARGINS = BaseContext.GetType<MarginsEvaluationType>();

			FILE_PATH = BaseContext.GetType<FilePathEvaluationType>();

			// Enum types
			TEXT_FORMAT = EnumEvaluationType.FromSystemType<TextFormat>(BaseContext);
			TEXT_HEIGHT_STRATEGY = EnumEvaluationType.FromSystemType<TextHeightStrategy>(BaseContext);
			JUSTIFICATION = EnumEvaluationType.FromSystemType<Justification>(BaseContext);
			ALIGNMENT = EnumEvaluationType.FromSystemType<Alignment>(BaseContext);
			LAYOUT = EnumEvaluationType.FromSystemType<Layout>(BaseContext);
			CHECK_TYPE = EnumEvaluationType.FromSystemType<CheckType>(BaseContext);
			
			// TODO Is this right?
			WIDGET = new CustomEvaluationType(BaseContext, "widget", Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>(), typeof(IWidget));

			// TODO There are missing types here
			CONTAINER = new CustomEvaluationType(BaseContext, "TitledBox", Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>(), typeof(IContainerShape));
			BOX = new CustomEvaluationType(BaseContext, "Box", Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>(), typeof(IBox));
			LABELLED_BOX = new CustomEvaluationType(BaseContext, "LabelledBox", Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>(), typeof(ILabelledBox));
			BAR = new CustomEvaluationType(BaseContext, "Bar", Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>(), typeof(IBar));
			USAGE_BAR = new CustomEvaluationType(BaseContext, "UsageBar", Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>(), typeof(IUsageBar));
			DETAIL = new CustomEvaluationType(BaseContext, "Detail", Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>(), typeof(IDetail));

			typeRegistry = new Dictionary<string, EvaluationType> {
				{ "float", BaseContext.GetType<FloatEvaluationType>() },
				{ "ufloat", BaseContext.GetType<UFloatEvaluationType>() },
				{ "int", BaseContext.GetType<IntEvaluationType>() },
				{ "uint", BaseContext.GetType<UIntEvaluationType>() },
				{ "bool", BaseContext.GetType<BoolEvaluationType>() },
				{ "string", BaseContext.GetType<StringEvaluationType>() },
				{ "color", BaseContext.GetType<ColorEvaluationType>() },
				{ "dimension", DIMENSION },
				{ "margins", MARGINS },
				{ "filepath", FILE_PATH },
				{ "textformat", TEXT_FORMAT },
				{ "textheightstrategy", TEXT_HEIGHT_STRATEGY },
				{ "justification", JUSTIFICATION },
				{ "alignment", ALIGNMENT },
				{ "checktype", CHECK_TYPE },
				{ "widget", WIDGET },
				{ "titledbox", CONTAINER }, // Confusing name, but makes more sense to user?
				{ "box", BOX },
				{ "labelledbox", LABELLED_BOX },
				{ "bar", BAR },
				{ "usagebar", USAGE_BAR },
				{ "detail", DETAIL }
				// TODO More types here?
			};
		}
		*/

		public static EvaluationContext.Builder Create() {
			EvaluationContext.Builder builder = EvaluationContext.Create();

			// Base data types
			builder.SetDataType<ColorEvaluationType, SharpSheets.Colors.Color>(ctx => new ColorEvaluationType(ctx));
			builder.SetDataType<DimensionEvaluationType, Dimension>(ctx => new DimensionEvaluationType(ctx));
			builder.SetDataType<MarginsEvaluationType, Margins>(ctx => new MarginsEvaluationType(ctx));
			builder.SetDataType<FilePathEvaluationType, SharpSheets.Utilities.FilePath>(ctx => new FilePathEvaluationType(ctx));
			builder.SetDataType<RichStringEvaluationType, RichString>(ctx => new RichStringEvaluationType(ctx));

			// Enum types
			builder.SetSystemType<TextFormat, EnumEvaluationType>(ctx => EnumEvaluationType.FromSystemType<TextFormat>(ctx));
			builder.SetSystemType<TextHeightStrategy, EnumEvaluationType>(ctx => EnumEvaluationType.FromSystemType<TextHeightStrategy>(ctx));
			builder.SetSystemType<Justification, EnumEvaluationType>(ctx => EnumEvaluationType.FromSystemType<Justification>(ctx));
			builder.SetSystemType<Alignment, EnumEvaluationType>(ctx => EnumEvaluationType.FromSystemType<Alignment>(ctx));
			builder.SetSystemType<LayoutDirection, EnumEvaluationType>(ctx => EnumEvaluationType.FromSystemType<LayoutDirection>(ctx));
			builder.SetSystemType<CheckType, EnumEvaluationType>(ctx => EnumEvaluationType.FromSystemType<CheckType>(ctx));

			// Widget types
			// TODO Is this right?
			builder.SetSystemType<IWidget, EvaluationType>(ctx => new CustomEvaluationType<IWidget?>(ctx, "widget", Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>(), null, null, null));

			// Shape types
			TypeField GetAspectField<TShape>(EvaluationContext ctx) where TShape : IAreaShape {
				return new TypeField("aspect", ctx.GetType<FloatEvaluationType>(), v => ctx.GetType<FloatEvaluationType>().MakeValue(((TShape)v.Value!).Aspect));
			}
			// TODO There are missing types here
			builder.SetSystemType<IContainerShape, EvaluationType>(ctx => new CustomEvaluationType<IContainerShape?>(ctx, "TitledBox", [GetAspectField<IContainerShape>(ctx)], Enumerable.Empty<TypeField>(), null, null, null));
			builder.SetSystemType<IBox, EvaluationType>(ctx => new CustomEvaluationType<IBox?>(ctx, "Box", [GetAspectField<IBox>(ctx)], Enumerable.Empty<TypeField>(), null, null, null));
			builder.SetSystemType<ILabelledBox, EvaluationType>(ctx => new CustomEvaluationType<ILabelledBox?>(ctx, "LabelledBox", [GetAspectField<ILabelledBox>(ctx)], Enumerable.Empty<TypeField>(), null, null, null));
			builder.SetSystemType<IEntriedShape, EvaluationType>(ctx => new CustomEvaluationType<IEntriedShape?>(ctx, "Entried", [GetAspectField<IEntriedShape>(ctx)], Enumerable.Empty<TypeField>(), null, null, null));
			builder.SetSystemType<IBar, EvaluationType>(ctx => new CustomEvaluationType<IBar?>(ctx, "Bar", [GetAspectField<IBar>(ctx)], Enumerable.Empty<TypeField>(), null, null, null));
			builder.SetSystemType<IUsageBar, EvaluationType>(ctx => new CustomEvaluationType<IUsageBar?>(ctx, "UsageBar", [GetAspectField<IUsageBar>(ctx)], Enumerable.Empty<TypeField>(), null, null, null));
			builder.SetSystemType<IDetail, EvaluationType>(ctx => new CustomEvaluationType<IDetail?>(ctx, "Detail", Enumerable.Empty<TypeField>(), Enumerable.Empty<TypeField>(), null, null, null));
			
			return builder;
		}

		private static bool TryGetType(string typeName, EvaluationContext context, [NotNullWhen(true)] out EvaluationType? result) {
			switch (typeName) { // Shouldn't this be lowercased? Why are we not being case-insensitive here?
				case "float":
					result = context.GetType<FloatEvaluationType>();
					return true;
				case "ufloat":
					result = context.GetType<UFloatEvaluationType>();
					return true;
				case "int":
					result = context.GetType<IntEvaluationType>();
					return true;
				case "uint":
					result = context.GetType<UIntEvaluationType>();
					return true;
				case "bool":
					result = context.GetType<BoolEvaluationType>();
					return true;
				case "string":
					result = context.GetType<StringEvaluationType>();
					return true;
				case "color":
					result = context.GetType<ColorEvaluationType>();
					return true;
			}

			return context.TryGetType(typeName, out result);
		}

		//public static readonly EvaluationType DIMENSION = EvaluationType.FromSystemType(typeof(Dimension));
		//public static readonly EvaluationType MARGINS = EvaluationType.FromSystemType(typeof(Margins));

		//public static readonly EvaluationType FILE_PATH = EvaluationType.FromSystemType(typeof(SharpSheets.Utilities.FilePath));

		// TODO Is this right?
		//public static readonly EvaluationType WIDGET = EvaluationType.CustomType("Widget", Enumerable.Empty<TypeField>(), typeof(IWidget));

		// TODO There are missing types here
		//public static readonly EvaluationType CONTAINER = EvaluationType.CustomType("TitledBox", Enumerable.Empty<TypeField>(), typeof(IContainerShape));
		//public static readonly EvaluationType BOX = EvaluationType.CustomType("Box", Enumerable.Empty<TypeField>(), typeof(IBox));
		//public static readonly EvaluationType LABELLED_BOX = EvaluationType.CustomType("LabelledBox", Enumerable.Empty<TypeField>(), typeof(ILabelledBox));
		//public static readonly EvaluationType BAR = EvaluationType.CustomType("Bar", Enumerable.Empty<TypeField>(), typeof(IBar));
		//public static readonly EvaluationType USAGE_BAR = EvaluationType.CustomType("UsageBar", Enumerable.Empty<TypeField>(), typeof(IUsageBar));
		//public static readonly EvaluationType DETAIL = EvaluationType.CustomType("Detail", Enumerable.Empty<TypeField>(), typeof(IDetail));

		[GeneratedRegex(@"\[(?:(?<tuple>[0-9]+)|(?<dictKey>[a-z][a-z0-9_]*))?\]", RegexOptions.IgnoreCase)]
		private static partial Regex ArrayTupleRegex();
		[GeneratedRegex(@"\s+")]
		private static partial Regex SpaceRegex();

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static EvaluationType ParseArgumentType(string text, string? description, EvaluationContext context) {
			text = SpaceRegex().Replace(text, "");
			string textKey = text.ToLowerInvariant();
			
			Match arrayMatch = ArrayTupleRegex().Match(text);
			if (arrayMatch.Success) {
				// The "top level" array specification comes first, followed by descreasingly significant "[]"
				string baseTypeStr = text.Substring(0, arrayMatch.Index) + text.Substring(arrayMatch.Index + arrayMatch.Length);
				// A bit clunky, but it should work
				EvaluationType baseType = ParseArgumentType(baseTypeStr, description, context);
				if (arrayMatch.Groups["tuple"].Success) {
					int tupleSize = int.Parse(arrayMatch.Groups["tuple"].Value);
					return baseType.MakeTuple(tupleSize);
				}
				else if (arrayMatch.Groups["dictKey"].Success) {
					string keyName = arrayMatch.Groups["dictKey"].Value;
					EvaluationType keyType = ParseArgumentType(keyName, null, context);
					return new DictionaryEvaluationType(context, keyType, baseType);
				}
				else {
					return baseType.MakeArray();
				}
			}
			else if (TryGetType(textKey, context, out EvaluationType? argType)) {
				return argType;
			}
			else {
				throw new FormatException("Invalid argument type string.");
			}
		}

		public static EvaluationType MakeGroupType(string name, IEnumerable<IMarkupArgument> args, EvaluationContext context) {
			List<TypeField> fields = new List<TypeField>();

			foreach (IMarkupArgument arg in args) {
				TypeField field = new TypeField(arg.VariableName, arg.Type, obj => GroupFieldAccessor(obj, arg.VariableName));
				fields.Add(field);
			}

			return new CustomEvaluationType<Dictionary<EvaluationName, EvaluationValue>>(context, name, fields, Enumerable.Empty<TypeField>(), null, null, new Dictionary<EvaluationName, EvaluationValue>());
		}

		/// <summary></summary>
		/// <exception cref="UndefinedVariableException"></exception>
		/// <exception cref="EvaluationTypeException"></exception>
		private static EvaluationValue GroupFieldAccessor(EvaluationValue obj, EvaluationName name) {
			// TODO Should this be "Dictionary<EvaluationName, object?>"? (Cf. With argument parsing)
			if (obj.Value is Dictionary<EvaluationName, EvaluationValue> values) {
				if (values.TryGetValue(name, out EvaluationValue result)) {
					return result;
				}
				else {
					throw new UndefinedVariableException("Cannot find field value.");
				}
			}
			else {
				throw new EvaluationTypeException("Cannot access group field from invalid object.");
			}
		}

	}


	public sealed class ColorEvaluationType : SingleDataType<SharpSheets.Colors.Color> {

		public override string Name { get; } = "color";

		public ColorEvaluationType(EvaluationContext context) : base(context) {
			foreach((string name, SharpSheets.Colors.Color color) in SharpSheets.Colors.Color.NamedColors) {
				AddStaticField(new TypeField(name, this, t => new EvaluationValue(color, this)));
			}
		}

		protected override Color ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseColor(text);
		}

		protected override string GetEvaluationSingleString(Color value) {
			return value.IsNamedColor ? value.Name : value.ToHexString();
		}

		protected override Color DefaultValueDataSingle() {
			return Color.None;
		}

		public static bool IsColor(EvaluationType type) {
			return type is ColorEvaluationType;
		}

		public static bool TryGetColor(EvaluationValue value, out SharpSheets.Colors.Color color) {
			if (value.Value is SharpSheets.Colors.Color colorValue) {
				color = colorValue;
				return true;
			}
			else {
				color = default;
				return false;
			}
		}

		public override IEnvironmentFunction GetTypeFunction() => ColorCreateFunction.Instance;

		public override EvaluationType? EqualResult(EvaluationType other) {
			return this == other ? Context.GetType<BoolEvaluationType>() : null;
		}
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) {
			if (TryGetColor(left, out SharpSheets.Colors.Color leftColor) && TryGetColor(right, out SharpSheets.Colors.Color rightColor)) {
				return new EvaluationValue(leftColor == rightColor, Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? NotEqualResult(EvaluationType other) {
			return this == other ? Context.GetType<BoolEvaluationType>() : null;
		}
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) {
			if (TryGetColor(left, out SharpSheets.Colors.Color leftColor) && TryGetColor(right, out SharpSheets.Colors.Color rightColor)) {
				return new EvaluationValue(leftColor != rightColor, Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public class ColorCreateFunction : AbstractFunction {

			public static readonly ColorCreateFunction Instance = new ColorCreateFunction();
			private ColorCreateFunction() { }

			public override EvaluationName Name { get; } = "color";
			public override string? Description { get; } = "Creates a color from the arguments, either from a single greyscale value, 3 RGB values, 4 ARGB values, or a string to be parsed. All numeric arguments will be clamped between 0 and 1.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				FloatEvaluationType floatType = context.GetType<FloatEvaluationType>();
				StringEvaluationType stringType = context.GetType<StringEvaluationType>();

				return new EnvironmentFunctionArguments(
						"Color must take 1, 3, or 4 real-valued arguments, or one string argument.",
						new EnvironmentFunctionArgList(
							new EnvironmentFunctionArg("a", floatType, null),
							new EnvironmentFunctionArg("r", floatType, null),
							new EnvironmentFunctionArg("g", floatType, null),
							new EnvironmentFunctionArg("b", floatType, null)
							),
						new EnvironmentFunctionArgList(
							new EnvironmentFunctionArg("r", floatType, null),
							new EnvironmentFunctionArg("g", floatType, null),
							new EnvironmentFunctionArg("b", floatType, null)
							),
						new EnvironmentFunctionArgList(
							new EnvironmentFunctionArg("gray", floatType, null)
							),
						new EnvironmentFunctionArgList(
							new EnvironmentFunctionArg("colorStr", stringType, null)
							)
					);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType[] argTypes = args.Select(a => a.GetReturnType()).ToArray();
				if (argTypes.All(FloatEvaluationType.IsReal)) {
					int count = args.Length;
					if (count == 1 || count == 3 || count == 4) {
						return context.GetType<ColorEvaluationType>();
					}
					else {
						throw new EvaluationTypeException("Color must take 1, 3, or 4 real-valued arguments.");
					}
				}
				else if (argTypes.Length == 1 && StringEvaluationType.IsString(argTypes[0])) {
					return context.GetType<ColorEvaluationType>();
				}
				else {
					string s = (args.Length > 1) ? "s" : "";
					throw new EvaluationTypeException($"Cannot create a color from arguments with type{s}: " + string.Join(", ", args.Select(a => a.GetReturnType().ToString())));
				}
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue[] argVals = args.Select(a => a.Evaluate(environment)).ToArray();

				if (argVals.Length != 1 && argVals.Length != 3 && argVals.Length != 4) {
					throw new EvaluationCalculationException("Color must take 1, 3, or 4 real-valued arguments, or a single string.");
				}

				Color result;

				if (argVals.Length == 1 && StringEvaluationType.TryGetString(argVals[0], out string? colorStr)) {
					try {
						result = ColorUtils.Parse(colorStr);
					}
					catch (FormatException e) {
						throw new EvaluationCalculationException(e.Message, e);
					}
				}
				else {
					bool badTypes = false;
					float[] values = new float[argVals.Length];

					for (int i = 0; i < argVals.Length; i++) {
						if (FloatEvaluationType.TryGetFloat(argVals[i], out float realVal)) {
							values[i] = realVal.Clamp(0f, 1f);
						}
						else {
							badTypes = true;
							break;
						}
					}

					if (badTypes) {
						Type[] types = argVals.Select(a => a.GetType()).Distinct().ToArray();
						string s = (types.Length > 1) ? "s" : "";
						throw new EvaluationTypeException($"Cannot create a color from arguments with type{s}: " + string.Join(", ", types.Select(t => t.Name)));
	}

					if (values.Length == 1) {
						result = ColorUtils.FromGrayscale(values[0]);
					}
					else if (values.Length == 3) {
						result = ColorUtils.FromRGB(values[0], values[1], values[2]);
					}
					else {
						result = ColorUtils.FromRGBA(values[1], values[2], values[3], values[0]);
					}
				}

				return environment.Context.MakeValue<ColorEvaluationType>(result);
			}

		}

	}

	public sealed class DimensionEvaluationType : SingleDataType<Dimension> {

		public override string Name { get; } = "dimension";

		public DimensionEvaluationType(EvaluationContext context) : base(context) {
			AddStaticField(new TypeField("auto", this, t => new EvaluationValue(Dimension.Automatic, this)));

			FloatEvaluationType floatType = context.GetType<FloatEvaluationType>();
			BoolEvaluationType boolType = context.GetType<BoolEvaluationType>();

			AddField(new TypeField("absolute", floatType, v => new EvaluationValue(((Dimension)v.Value!).Absolute, floatType)));
			AddField(new TypeField("relative", floatType, v => new EvaluationValue(((Dimension)v.Value!).Relative, floatType)));
			AddField(new TypeField("percent", floatType, v => new EvaluationValue(((Dimension)v.Value!).Percent, floatType)));
			AddField(new TypeField("auto", boolType, v => new EvaluationValue(((Dimension)v.Value!).Auto, boolType)));

			// Should be some static methods in here
		}

		protected override Dimension ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseDimension(text);
		}

		protected override string GetEvaluationSingleString(Dimension value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override Dimension DefaultValueDataSingle() {
			return Dimension.Zero;
		}

		public static bool IsDimension(EvaluationType type) {
			return type is DimensionEvaluationType;
		}

		public static bool TryGetDimension(EvaluationValue value, out Dimension dimension) {
			if (value.Value is Dimension dimensionValue) {
				dimension = dimensionValue;
				return true;
			}
			else {
				dimension = default;
				return false;
			}
		}

		private EvaluationType? AddResultAny(EvaluationType other) {
			return this == other ? this : null;
		}
		private EvaluationValue? AddAny(EvaluationValue left, EvaluationValue right) {
			if (TryGetDimension(left, out Dimension leftDimension) && TryGetDimension(right, out Dimension rightDimension)) {
				return new EvaluationValue(leftDimension + rightDimension, this);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? AddResult(EvaluationType right) => AddResultAny(right);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => AddAny(left, right);
		public override EvaluationType? RAddResult(EvaluationType left) => AddResultAny(left);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => AddAny(left, right);

		private EvaluationType? MulResultAny(EvaluationType other) {
			return FloatEvaluationType.IsReal(other) ? this : null;
		}
		private EvaluationValue? MulAny(EvaluationValue dimension, EvaluationValue factor) {
			if (TryGetDimension(dimension, out Dimension dimensionVal) && FloatEvaluationType.TryGetFloat(factor, out float factorVal)) {
				return new EvaluationValue(dimensionVal * factorVal, this);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? MulResult(EvaluationType right) => MulResultAny(right);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => MulAny(left, right);
		public override EvaluationType? RMulResult(EvaluationType left) => MulResultAny(left);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => MulAny(right, left);

		public override EvaluationType? DivResult(EvaluationType right) {
			return FloatEvaluationType.IsReal(right) ? this : null;
		}
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) {
			if(TryGetDimension(left, out Dimension dimension) && FloatEvaluationType.TryGetFloat(right, out float divisor)) {
				return new EvaluationValue(dimension / divisor, this);
			}
			else {
				return null;
			}
		}
		// Only know how to divide with a float on the RHS

		public override EvaluationType? EqualResult(EvaluationType other) {
			return this == other ? Context.GetType<BoolEvaluationType>() : null;
		}
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) {
			if (TryGetDimension(left, out Dimension a) && TryGetDimension(right, out Dimension b)) {
				return new EvaluationValue(a == b, Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? NotEqualResult(EvaluationType other) {
			return this == other ? Context.GetType<BoolEvaluationType>() : null;
		}
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) {
			if (TryGetDimension(left, out Dimension a) && TryGetDimension(right, out Dimension b)) {
				return new EvaluationValue(a != b, Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

	}

	public sealed class MarginsEvaluationType : SingleDataType<Margins> {

		public override string Name { get; } = "margins";

		public MarginsEvaluationType(EvaluationContext context) : base(context) {
			AddField(new TypeField("top", Context.GetType<FloatEvaluationType>(), value => new EvaluationValue(((Margins)value.Value!).Top, value.Type.Context.GetType<FloatEvaluationType>())));
			AddField(new TypeField("right", Context.GetType<FloatEvaluationType>(), value => new EvaluationValue(((Margins)value.Value!).Right, value.Type.Context.GetType<FloatEvaluationType>())));
			AddField(new TypeField("bottom", Context.GetType<FloatEvaluationType>(), value => new EvaluationValue(((Margins)value.Value!).Bottom, value.Type.Context.GetType<FloatEvaluationType>())));
			AddField(new TypeField("left", Context.GetType<FloatEvaluationType>(), value => new EvaluationValue(((Margins)value.Value!).Left, value.Type.Context.GetType<FloatEvaluationType>())));
		}

		protected override Margins ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseMargins(text);
		}

		protected override string GetEvaluationSingleString(Margins value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		protected override Margins DefaultValueDataSingle() {
			return Margins.Zero;
		}

		public static bool IsMargins(EvaluationType type) {
			return type is MarginsEvaluationType;
		}

		public static bool TryGetMargins(EvaluationValue value, out Margins margins) {
			if(value.Value is Margins marginsValue) {
				margins = marginsValue;
				return true;
			}
			else {
				margins = default;
				return false;
			}
		}

		private EvaluationType? AddResultAny(EvaluationType other) {
			return this == other ? this : null;
		}
		private EvaluationValue? AddAny(EvaluationValue left, EvaluationValue right) {
			if(TryGetMargins(left, out Margins leftMargins) && TryGetMargins(right, out Margins rightMargins)) {
				return new EvaluationValue(leftMargins + rightMargins, this);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? AddResult(EvaluationType right) => AddResultAny(right);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => AddAny(left, right);
		public override EvaluationType? RAddResult(EvaluationType left) => AddResultAny(left);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => AddAny(left, right);

		private EvaluationType? MulResultAny(EvaluationType other) {
			return FloatEvaluationType.IsReal(other) ? this : null;
		}
		private EvaluationValue? MulAny(EvaluationValue margins, EvaluationValue factor) {
			if (TryGetMargins(margins, out Margins marginsVal) && FloatEvaluationType.TryGetFloat(factor, out float factorVal)) {
				return new EvaluationValue(marginsVal * factorVal, this);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? MulResult(EvaluationType right) => MulResultAny(right);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) => MulAny(left, right);
		public override EvaluationType? RMulResult(EvaluationType left) => MulResultAny(left);
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) => MulAny(right, left);

		public override EvaluationType? DivResult(EvaluationType right) {
			return FloatEvaluationType.IsReal(right) ? this : null;
		}
		public override EvaluationValue? Div(EvaluationValue left, EvaluationValue right) {
			if(TryGetMargins(left, out Margins margins) && FloatEvaluationType.TryGetFloat(right, out float divisor)) {
				return new EvaluationValue(margins / divisor, this);
			}
			else {
				return null;
			}
		}
		// Only know how to divide with a float on the RHS

		public override EvaluationType? EqualResult(EvaluationType other) {
			return this == other ? Context.GetType<BoolEvaluationType>() : null;
		}
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) {
			if (TryGetMargins(left, out Margins leftMargins) && TryGetMargins(right, out Margins rightMargins)) {
				return new EvaluationValue(leftMargins == rightMargins, Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? NotEqualResult(EvaluationType other) {
			return this == other ? Context.GetType<BoolEvaluationType>() : null;
		}
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) {
			if (TryGetMargins(left, out Margins leftMargins) && TryGetMargins(right, out Margins rightMargins)) {
				return new EvaluationValue(leftMargins != rightMargins, Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

	}

	public sealed class FilePathEvaluationType : SingleDataType<SharpSheets.Utilities.FilePath> {

		public override string Name { get; } = "filepath";

		public FilePathEvaluationType(EvaluationContext context) : base(context) { }

		protected override FilePath ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseFilePath(text, source);
		}

		protected override string GetEvaluationSingleString(FilePath value) {
			return value.Path;
		}

		protected override FilePath DefaultValueDataSingle() {
			return null!;
		}

		public static bool IsFilePath(EvaluationType type) {
			return type is FilePathEvaluationType;
		}

		public static bool TryGetFilePath(EvaluationValue value, [NotNullWhen(true)] out SharpSheets.Utilities.FilePath? filePath) {
			if (value.Value is SharpSheets.Utilities.FilePath filePathValue) {
				filePath = filePathValue;
				return true;
			}
			else {
				filePath = null;
				return false;
			}
		}

		public override EvaluationType? EqualResult(EvaluationType other) {
			return this == other ? Context.GetType<BoolEvaluationType>() : null;
		}
		public override EvaluationValue? Equal(EvaluationValue left, EvaluationValue right) {
			if (TryGetFilePath(left, out SharpSheets.Utilities.FilePath? leftPath) && TryGetFilePath(right, out SharpSheets.Utilities.FilePath? rightPath)) {
				return new EvaluationValue(leftPath == rightPath, Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

		public override EvaluationType? NotEqualResult(EvaluationType other) {
			return this == other ? Context.GetType<BoolEvaluationType>() : null;
		}
		public override EvaluationValue? NotEqual(EvaluationValue left, EvaluationValue right) {
			if (TryGetFilePath(left, out SharpSheets.Utilities.FilePath? leftPath) && TryGetFilePath(right, out SharpSheets.Utilities.FilePath? rightPath)) {
				return new EvaluationValue(leftPath != rightPath, Context.GetType<BoolEvaluationType>());
			}
			else {
				return null;
			}
		}

	}

	public sealed class RichStringEvaluationType : SingleDataType<RichString> {

		public override string Name { get; } = "richstr";

		public RichStringEvaluationType(EvaluationContext context) : base(context) {
			AddField(new TypeField("length", Context.GetType<IntEvaluationType>(), value => value.Type.Context.GetValue<IntEvaluationType>(((RichString)value.Value!).Length)));
		}

		protected override RichString ParseValueDataSingle(string text, DirectoryPath source) {
			return ValueParsers.ParseRichString(text);
		}

		protected override string GetEvaluationSingleString(RichString value) {
			return value.Formatted;
		}

		protected override RichString DefaultValueDataSingle() {
			return RichString.Empty;
		}

		public static bool IsRichString(EvaluationType other) {
			return other is RichStringEvaluationType || StringEvaluationType.IsString(other);
		}

		public static bool AllRichString(params EvaluationType[] types) {
			for (int i = 0; i < types.Length; i++) {
				if (!IsRichString(types[i])) {
					return false;
				}
			}
			return true;
		}

		public static bool TryGetRichString(EvaluationValue value, [NotNullWhen(true)] out RichString? str) {
			if (value.Type is RichStringEvaluationType && value.Value is RichString richStringVal) {
				str = richStringVal;
				return true;
			}
			else if (StringEvaluationType.TryGetString(value, out string? stringVal)) {
				str = RichString.Create(stringVal, TextFormat.REGULAR);
				return true;
			}

			str = null;
			return false;
		}

		public override IEnvironmentFunction GetTypeFunction() => RichStringCastFunction.Instance;

		public override bool CanImplicitCastFrom(EvaluationType other) => IsRichString(other);

		public override EvaluationValue? Cast(EvaluationValue other) {
			if (TryGetRichString(other, out RichString? value)) {
				return MakeValue(value);
			}
			else {
				return null;
			}
		}

		private EvaluationType? AddResultAny(EvaluationType other) {
			if (IsRichString(other)) { // Another string
				return this;
			}
			else {
				return null;
			}
		}

		private EvaluationValue? AddAny(EvaluationValue left, EvaluationValue right) {
			if (TryGetRichString(left, out RichString? leftVal) && TryGetRichString(right, out RichString? rightVal)) {
				return MakeValue(leftVal + rightVal);
			}
			else {
				return null;
			}
		}

		public override EvaluationType? AddResult(EvaluationType right) => AddResultAny(right);
		public override EvaluationValue? Add(EvaluationValue left, EvaluationValue right) => AddAny(left, right);
		public override EvaluationType? RAddResult(EvaluationType left) => AddResultAny(left);
		public override EvaluationValue? RAdd(EvaluationValue left, EvaluationValue right) => AddAny(left, right);

		private EvaluationType? MulResultAny(EvaluationType other) {
			if (IntEvaluationType.IsIntegral(other)) { // An int-like
				return this;
			}
			else {
				return null;
			}
		}

		private static RichString RepeatString(RichString str, int count) {
			return str.Repeat(count);
		}

		public override EvaluationType? MulResult(EvaluationType right) => MulResultAny(right);
		public override EvaluationType? RMulResult(EvaluationType left) => MulResultAny(left);
		public override EvaluationValue? Mul(EvaluationValue left, EvaluationValue right) {
			if (TryGetRichString(left, out RichString? stringVal) && IntEvaluationType.TryGetInt(right, out int intVal)) {
				return new EvaluationValue(RepeatString(stringVal, intVal), this);
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? RMul(EvaluationValue left, EvaluationValue right) {
			if (IntEvaluationType.TryGetInt(left, out int intVal) && TryGetRichString(right, out RichString? stringVal)) {
				return MakeValue(RepeatString(stringVal, intVal));
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IndexerResult(EvaluationType index) {
			if (IntEvaluationType.IsIntegral(index)) {
				return this;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? Indexer(EvaluationValue subject, EvaluationValue index) {
			if (TryGetRichString(subject, out RichString? subjectVal) && IntEvaluationType.TryGetInt(index, out int indexVal)) {
				int indexFinal = EvaluationTypeHelpers.GetIndex(indexVal, subjectVal.Length);
				return MakeValue(new RichString(subjectVal.chars[indexFinal].Yield().ToArray(), subjectVal.formats[indexFinal].Yield().ToArray()));
			}
			else {
				return null;
			}
		}

		public override EvaluationType? IndexerSliceResult(EvaluationType start, EvaluationType end) {
			if (IntEvaluationType.IsIntegral(start) && IntEvaluationType.IsIntegral(end)) {
				return this;
			}
			else {
				return null;
			}
		}
		public override EvaluationValue? IndexerSlice(EvaluationValue subject, EvaluationValue start, EvaluationValue end) {
			if (TryGetRichString(subject, out RichString? subjectVal) && IntEvaluationType.TryGetInt(start, out int startVal) && IntEvaluationType.TryGetInt(end, out int endVal)) {
				EvaluationTypeHelpers.GetSliceIndexes(startVal, endVal, subjectVal.Length, out int startFinal, out int endFinal);
				return MakeValue(subjectVal[startFinal..endFinal]);
			}
			else {
				return null;
			}
		}

		public class RichStringCastFunction : AbstractFunction {

			public static readonly RichStringCastFunction Instance = new RichStringCastFunction();
			private RichStringCastFunction() { }

			public override EvaluationName Name { get; } = "richstr";
			public override string? Description { get; } = "Convert a string into a rich string with some stated text format, or convert a rich string into a new rich string with some new starting text format.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(
						new EnvironmentFunctionArg("text", context.GetType<StringEvaluationType>(), null),
						new EnvironmentFunctionArg("format", context.GetSystemType<TextFormat>(), null)
						),
					new EnvironmentFunctionArgList(
						new EnvironmentFunctionArg("richText", context.GetType<RichStringEvaluationType>(), null),
						new EnvironmentFunctionArg("startingFormat", context.GetSystemType<TextFormat>(), null)
						)
				);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				EvaluationType[] argTypes = args.Select(a => a.GetReturnType()).ToArray();
				if (argTypes.Length == 2 && (IsRichString(argTypes[0]) || StringEvaluationType.IsString(argTypes[0])) && EnumEvaluationType.IsEnum<TextFormat>(argTypes[1])) {
					return context.GetType<StringEvaluationType>();
				}
				else {
					throw new EvaluationTypeException($"Invalid arguments to {Name}, expected a string-type and a text format.");
				}
			}

			private static EvaluationValue MakeResult(RichString result, EvaluationContext context) {
				return new EvaluationValue(result, context.GetType<RichStringEvaluationType>());
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				if (args.Length != 2) {
					throw new EvaluationCalculationException($"Expected 2 arguments for {Name}, for {args.Length}.");
				}

				EvaluationValue a = args[0].Evaluate(environment);
				EvaluationValue b = args[1].Evaluate(environment);

				if (StringEvaluationType.TryGetString(a, out string? str) && EnumEvaluationType.TryGetEnumValue(b, out TextFormat? strFormat)) {
					return MakeResult(RichString.Create(str, strFormat.Value), environment.Context);
				}
				else if (TryGetRichString(a, out RichString? richStr) && EnumEvaluationType.TryGetEnumValue(b, out TextFormat? startingFormat)) {
					return MakeResult(richStr.ApplyFormat(startingFormat.Value), environment.Context);
				}
				else {
					throw new EvaluationCalculationException($"Cannot convert argument of types {a.Type} and {b.Type} to rich string.");
				}
			}

		}

	}

}
