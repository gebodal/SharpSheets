using SharpSheets.Evaluations;
using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Fonts;
using SharpSheets.Colors;
using SharpSheets.Markup.Parsing;
using SharpSheets.Evaluations.Nodes;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Markup.Canvas {

	public class MarkupEvaluationContext {

		public EvaluationContext TypeSystem { get; }

		public VariableNode WidthNode { get; }
		public VariableNode HeightNode { get; }
		public EvaluationNode BoundingBoxLengthNode { get; }

		public XLengthExpression ZeroWidthExpression { get; }
		public XLengthExpression CentreXExpression { get; }
		public XLengthExpression WidthExpression { get; }
		public YLengthExpression ZeroHeightExpression { get; }
		public YLengthExpression CentreYExpression { get; }
		public YLengthExpression HeightExpression { get; }
		public BoundingBoxLengthExpression BoundingBoxLengthExpression { get; }

		public DrawPointExpression CentreExpression { get; }

		public RectangleExpression WholeAreaRectExpression { get; }

		public ColorExpression BackgroundExpression { get; }
		public ColorExpression TextColorExpression { get; }


		// SharpCanvas variables
		public readonly EnvironmentVariableInfo LineWidth;
		public readonly EnvironmentVariableInfo Foreground;
		public readonly EnvironmentVariableInfo Background;
		public readonly EnvironmentVariableInfo Midtone;
		public readonly EnvironmentVariableInfo TextColor;

		// Canvas area variables
		public readonly EnvironmentVariableInfo Width;
		public readonly EnvironmentVariableInfo Height;
		public readonly EnvironmentVariableInfo Left;
		public readonly EnvironmentVariableInfo Right;
		public readonly EnvironmentVariableInfo Bottom;
		public readonly EnvironmentVariableInfo Top;

		// Drawing rect variables
		public readonly EnvironmentVariableInfo DrawWidth;
		public readonly EnvironmentVariableInfo DrawHeight;
		public readonly EnvironmentVariableInfo DrawLeft;
		public readonly EnvironmentVariableInfo DrawRight;
		public readonly EnvironmentVariableInfo DrawBottom;
		public readonly EnvironmentVariableInfo DrawTop;

		// Drawing rect variables
		public readonly EnvironmentVariableInfo PageWidth;
		public readonly EnvironmentVariableInfo PageHeight;
		public readonly EnvironmentVariableInfo PageLeft;
		public readonly EnvironmentVariableInfo PageRight;
		public readonly EnvironmentVariableInfo PageBottom;
		public readonly EnvironmentVariableInfo PageTop;

		// Random seed calculated from area variables
		public readonly EnvironmentVariableInfo Seed;


		public MarkupEvaluationContext(EvaluationContext context) {
			this.TypeSystem = context;

			FloatEvaluationType floatType = TypeSystem.GetType<FloatEvaluationType>();
			IntEvaluationType intType = TypeSystem.GetType<IntEvaluationType>();
			ColorEvaluationType colorType = TypeSystem.GetType<ColorEvaluationType>();

			WidthNode = new VariableNode("width", floatType);
			HeightNode = new VariableNode("height", floatType);
			BoundingBoxLengthNode = MinVarFunction.Instance.MakeNode(TypeSystem, WidthNode, HeightNode);

			ZeroWidthExpression = new XLengthExpression(0f, TypeSystem);
			CentreXExpression = new XLengthExpression(WidthNode * floatType.MakeValue(0.5f));
			WidthExpression = new XLengthExpression(WidthNode);
			ZeroHeightExpression = new YLengthExpression(0f, TypeSystem);
			CentreYExpression = new YLengthExpression(HeightNode * floatType.MakeValue(0.5f));
			HeightExpression = new YLengthExpression(HeightNode);
			BoundingBoxLengthExpression = new BoundingBoxLengthExpression(BoundingBoxLengthNode);

			CentreExpression = new DrawPointExpression(CentreXExpression, CentreYExpression);

			WholeAreaRectExpression = new RectangleExpression(
				new FloatExpression(0f, TypeSystem), // new FloatExpression(new VariableNode("left", EvaluationType.FLOAT)),
				new FloatExpression(0f, TypeSystem), // new FloatExpression(new VariableNode("bottom", EvaluationType.FLOAT)),
				WidthExpression,
				HeightExpression
				);

			BackgroundExpression = new ColorExpression(new VariableNode("background", colorType));
			TextColorExpression = new ColorExpression(new VariableNode("textcolor", colorType));



			// SharpCanvas variables
			LineWidth = new EnvironmentVariableInfo("linewidth", floatType, "The current default line width.");
			Foreground = new EnvironmentVariableInfo("foreground", colorType, "The current foreground color.");
			Background = new EnvironmentVariableInfo("background", colorType, "The current background color.");
			Midtone = new EnvironmentVariableInfo("midtone", colorType, "The current midtone color.");
			TextColor = new EnvironmentVariableInfo("textcolor", colorType, "The current text color.");

			// Canvas area variables
			Width = new EnvironmentVariableInfo("width", floatType, "The width of the drawing canvas.");
			Height = new EnvironmentVariableInfo("height", floatType, "The height of the drawing canvas.");
			Left = new EnvironmentVariableInfo("left", floatType, "The left-hand side x-coordinate of the drawing canvas.");
			Right = new EnvironmentVariableInfo("right", floatType, "The right-hand side x-coordinate of the drawing canvas.");
			Bottom = new EnvironmentVariableInfo("bottom", floatType, "The bottom edge y-coordinate of the drawing canvas.");
			Top = new EnvironmentVariableInfo("top", floatType, "The top edge y-coordinate of the drawing canvas.");

			// Drawing rect variables
			DrawWidth = new EnvironmentVariableInfo("drawwidth", floatType, "The width of the actual drawing area on the document page.");
			DrawHeight = new EnvironmentVariableInfo("drawheight", floatType, "The height of the actual drawing area on the document page.");
			DrawLeft = new EnvironmentVariableInfo("drawleft", floatType, "The left-hand side x-coordinate of the drawing area in its own coordinate view.");
			DrawRight = new EnvironmentVariableInfo("drawright", floatType, "The right-hand side x-coordinate of the drawing area in its own coordinate view.");
			DrawBottom = new EnvironmentVariableInfo("drawbottom", floatType, "The bottom edge y-coordinate of the drawing area in its own coordinate view.");
			DrawTop = new EnvironmentVariableInfo("drawtop", floatType, "The top edge y-coordinate of the drawing area in its own coordinate view.");

			// Drawing rect variables
			PageWidth = new EnvironmentVariableInfo("pagewidth", floatType, "The width of the actual drawing area on the document page.");
			PageHeight = new EnvironmentVariableInfo("pageheight", floatType, "The height of the actual drawing area on the document page.");
			PageLeft = new EnvironmentVariableInfo("pageleft", floatType, "The left-hand side x-coordinate of the actual drawing area on the document page.");
			PageRight = new EnvironmentVariableInfo("pageright", floatType, "The right-hand side x-coordinate of the actual drawing area on the document page.");
			PageBottom = new EnvironmentVariableInfo("pagebottom", floatType, "The bottom edge y-coordinate of the actual drawing area on the document page.");
			PageTop = new EnvironmentVariableInfo("pagetop", floatType, "The top edge y-coordinate of the actual drawing area on the document page.");

			// Random seed calculated from area variables
			Seed = new EnvironmentVariableInfo("seed", intType, "A random seed for this drawing area (based on the actual position on the document page).");

		}

		public static bool IsWidthDefined(IVariableBox variables) {
			return variables.IsVariable("width");
		}
		public static bool IsHeightDefined(IVariableBox variables) {
			return variables.IsVariable("height");
		}
		public static bool IsBoundingBoxDefined(IVariableBox variables) {
			return IsWidthDefined(variables) && IsHeightDefined(variables);
		}

		/// <summary>
		/// Variable state for when only the graphics state is known (linewidth, colours, etc.), including functions for calculating text sizes, dimensions, and colors.
		/// </summary>
		public IVariableBox GraphicsStateVariables() {
			return BasisEnvironment.MakeInstance(TypeSystem).AppendVariables(
				new EnvironmentVariableInfo[] {
					// SharpCanvas variables
					LineWidth,
					Foreground,
					Background,
					Midtone,
					TextColor
				},
				new IEnvironmentFunctionInfo[] {
					MarkupEnvironmentFunctions.WidthFunctionInfo.Instance,
					MarkupEnvironmentFunctions.HeightFunctionInfo.Instance,
					MarkupEnvironmentFunctions.AscentFunctionInfo.Instance,
					MarkupEnvironmentFunctions.FromRelativeFunction.Instance,
					MarkupEnvironmentFunctions.FromPointsFunction.Instance,
					MarkupEnvironmentFunctions.FromPercentFunction.Instance,
					MarkupEnvironmentFunctions.FromCentimetresFunction.Instance,
					MarkupEnvironmentFunctions.FromMillimetresFunction.Instance,
					MarkupEnvironmentFunctions.FromInchesFunction.Instance,
					MarkupEnvironmentFunctions.FromAutoFunction.Instance,
					MarkupEnvironmentFunctions.DarkenColorFunction.Instance,
					MarkupEnvironmentFunctions.LightenColorFunction.Instance
				});
		}

		/// <summary>
		/// Variable state for when the exact drawing dimensions are unknown ("drawwidth"/"drawheight"/etc.).
		/// Includes graphics state variables and markup canvas area values ("width"/"height"/etc.).
		/// </summary>
		public IVariableBox InferenceDrawingStateVariables() {
			return GraphicsStateVariables().AppendVariables(
				new EnvironmentVariableInfo[] {
					// SharpCanvas variables
					LineWidth,
					Foreground,
					Background,
					Midtone,
					TextColor,
					// MarkupCanvas area variables
					Width,
					Height,
					Left,
					Right,
					Bottom,
					Top,
					// Random seed calculated from area variables
					Seed
				});
		}

		/// <summary>
		/// Variable state for when the full drawing information is available, including the exact dimensions of the drawing area.
		/// </summary>
		public IVariableBox DrawingStateVariables() {
			return InferenceDrawingStateVariables().AppendVariables(
				new EnvironmentVariableInfo[] {
					// Drawing rect variables
					DrawWidth,
					DrawHeight,
					DrawLeft,
					DrawRight,
					DrawBottom,
					DrawTop,
					// Page rect variables
					PageWidth,
					PageHeight,
					PageLeft,
					PageRight,
					PageBottom,
					PageTop
				});
		}

		public IEnvironment MakeGraphicsStateEnvironment(MarkupCanvasGraphicsData graphicsData) {
			return BasisEnvironment.MakeInstance(TypeSystem).AppendEnvironment(
				Environments.Create(
					new (EvaluationValue, EnvironmentVariableInfo)[] {
						// SharpCanvas variables
						(LineWidth.EvaluationType.MakeValue(graphicsData.DefaultLineWidth), LineWidth),
						(Foreground.EvaluationType.MakeValue(graphicsData.ForegroundColor), Foreground),
						(Background.EvaluationType.MakeValue(graphicsData.BackgroundColor), Background),
						(Midtone.EvaluationType.MakeValue(graphicsData.MidtoneColor), Midtone),
						(TextColor.EvaluationType.MakeValue(graphicsData.TextColor), TextColor)
					},
					new List<IEnvironmentFunction> {
						MarkupEnvironmentFunctions.WidthFunctionInfo.GetFunction(graphicsData),
						MarkupEnvironmentFunctions.HeightFunctionInfo.GetFunction(graphicsData),
						MarkupEnvironmentFunctions.AscentFunctionInfo.GetFunction(graphicsData),
						MarkupEnvironmentFunctions.FromRelativeFunction.Instance,
						MarkupEnvironmentFunctions.FromPointsFunction.Instance,
						MarkupEnvironmentFunctions.FromPercentFunction.Instance,
						MarkupEnvironmentFunctions.FromCentimetresFunction.Instance,
						MarkupEnvironmentFunctions.FromMillimetresFunction.Instance,
						MarkupEnvironmentFunctions.FromInchesFunction.Instance,
						MarkupEnvironmentFunctions.FromAutoFunction.Instance,
						MarkupEnvironmentFunctions.DarkenColorFunction.Instance,
						MarkupEnvironmentFunctions.LightenColorFunction.Instance
					},
					TypeSystem)
				);
		}

		public IEnvironment MakeDrawingStateEnvironment(MarkupCanvasGraphicsData graphicsData, Layouts.Rectangle pageRect, Layouts.Rectangle drawingRect, Layouts.Size? referenceRect) {
			return MakeGraphicsStateEnvironment(graphicsData).AppendEnvironment(
				Environments.Create(
					new (object?, EnvironmentVariableInfo)[] {
						// Canvas area variables
						(referenceRect?.Width ?? drawingRect.Width, Width),
						(referenceRect?.Height ?? drawingRect.Height, Height),
						(referenceRect != null ? 0f : drawingRect.Left, Left),
						(referenceRect != null ? referenceRect.Width : drawingRect.Right, Right),
						(referenceRect != null ? 0f : drawingRect.Bottom, Bottom),
						(referenceRect != null ? referenceRect.Height : drawingRect.Top, Top),
						// Drawing rect variables
						(drawingRect.Width, DrawWidth),
						(drawingRect.Height, DrawHeight),
						(drawingRect.Left, DrawLeft),
						(drawingRect.Right, DrawRight),
						(drawingRect.Bottom, DrawBottom),
						(drawingRect.Top, DrawTop),
						// Page rect variables
						(pageRect.Width, PageWidth),
						(pageRect.Height, PageHeight),
						(pageRect.Left, PageLeft),
						(pageRect.Right, PageRight),
						(pageRect.Bottom, PageBottom),
						(pageRect.Top, PageTop),
						// Random seed calculated from area variables
						(drawingRect.GetHashCode(), Seed)
					}, TypeSystem)
				);
		}
	}

	// TODO This should probably be a class
	public readonly struct MarkupCanvasGraphicsData {
		public readonly float DefaultLineWidth;
		public readonly Color ForegroundColor;
		public readonly Color BackgroundColor;
		public readonly Color MidtoneColor;
		public readonly Color TextColor;
		public readonly FontSettingGrouping Fonts;

		public MarkupCanvasGraphicsData(float defaultLineWidth, Color foregroundColor, Color backgroundColor, Color midtoneColor, Color textColor, FontSettingGrouping fonts) {
			this.DefaultLineWidth = defaultLineWidth;
			this.ForegroundColor = foregroundColor;
			this.BackgroundColor = backgroundColor;
			this.MidtoneColor = midtoneColor;
			this.TextColor = textColor;
			this.Fonts = fonts;
		}
	}

	public static class MarkupCanvasGraphicsDataUtils {

		public static MarkupCanvasGraphicsData GetMarkupData(this SharpCanvasGraphicsSnapshot snapshot) {
			return new MarkupCanvasGraphicsData(
				snapshot.DefaultLineWidth,
				snapshot.ForegroundColor,
				snapshot.BackgroundColor,
				snapshot.MidtoneColor,
				snapshot.TextColor,
				snapshot.Fonts
				);
		}

		public static MarkupCanvasGraphicsData GetMarkupData(this ISharpGraphicsData snapshot) {
			return new MarkupCanvasGraphicsData(
				snapshot.GetDefaultLineWidth(),
				snapshot.GetForegroundColor(),
				snapshot.GetBackgroundColor(),
				snapshot.GetMidtoneColor(),
				snapshot.GetTextColor(),
				snapshot.GetFonts()
				);
		}

	}

	public static class MarkupEnvironmentFunctions {

		public class WidthFunctionInfo : IEnvironmentFunctionInfo {
			public static readonly WidthFunctionInfo Instance = new WidthFunctionInfo();
			private WidthFunctionInfo() { }

			public EvaluationName Name { get; } = "width";
			public string? Description { get; } = "Returns the width of the input text, at the given fontsize, for the given font format (which will use the font associated with that format in the current graphics state).";

			public EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(
						new EnvironmentFunctionArg("text", context.GetType<StringEvaluationType>(), null),
						new EnvironmentFunctionArg("format", context.GetSystemType<TextFormat>(), null), // TODO This would be better as some kind of getter on the context using the system enum type?
						new EnvironmentFunctionArg("fontsize", context.GetType<FloatEvaluationType>(), null)
					)
				);
			}

			public EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				return context.GetType<FloatEvaluationType>();
			}

			public static IEnvironmentFunction GetFunction(MarkupCanvasGraphicsData graphicsData) {
				return new WidthFunctionEvaluator(graphicsData);
			}

			private class WidthFunctionEvaluator : WidthFunctionInfo, IEnvironmentFunction {
				private readonly MarkupCanvasGraphicsData graphicsData;

				public WidthFunctionEvaluator(MarkupCanvasGraphicsData graphicsData) {
					this.graphicsData = graphicsData;
				}

				public EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
					EvaluationValue textVal = args[0].Evaluate(environment);
					EvaluationValue formatVal = args[1].Evaluate(environment);
					EvaluationValue fontsizeVal = args[2].Evaluate(environment);

					if(StringEvaluationType.TryGetString(textVal, out string? text) && EnumEvaluationType.TryGetEnumValue(formatVal, out TextFormat? format) && FloatEvaluationType.TryGetFloat(fontsizeVal, out float fontsize)) {
						float width = FontMetrics.GetWidth(text, graphicsData.Fonts, format.Value, fontsize);
						return new EvaluationValue(width, environment.GetType<FloatEvaluationType>());
					}
					else {
						throw new EvaluationCalculationException($"Cannot call {Name} with values of types {textVal.Type.Name}, {formatVal.Type.Name}, and {fontsizeVal.Type.Name}.");
					}
					
				}
			}
		}

		public class HeightFunctionInfo : IEnvironmentFunctionInfo {
			public static readonly HeightFunctionInfo Instance = new HeightFunctionInfo();
			private HeightFunctionInfo() { }

			public EvaluationName Name { get; } = "height";
			public string? Description { get; } = "Returns the height of the input text (the ascent plus the descent of the text), at the given fontsize, for the given font format (which will use the font associated with that format in the current graphics state).";

			public EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(
						new EnvironmentFunctionArg("text", context.GetType<StringEvaluationType>(), null),
						new EnvironmentFunctionArg("format", context.GetSystemType<TextFormat>(), null), // TODO This would be better as some kind of getter on the context using the system enum type?
						new EnvironmentFunctionArg("fontsize", context.GetType<FloatEvaluationType>(), null)
					)
				);
			}

			public EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				return context.GetType<FloatEvaluationType>();
			}

			public static IEnvironmentFunction GetFunction(MarkupCanvasGraphicsData graphicsData) {
				return new HeightFunctionEvaluator(graphicsData);
			}

			private class HeightFunctionEvaluator : HeightFunctionInfo, IEnvironmentFunction {
				private readonly MarkupCanvasGraphicsData graphicsData;

				public HeightFunctionEvaluator(MarkupCanvasGraphicsData graphicsData) {
					this.graphicsData = graphicsData;
				}

				public EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
					EvaluationValue textVal = args[0].Evaluate(environment);
					EvaluationValue formatVal = args[1].Evaluate(environment);
					EvaluationValue fontsizeVal = args[2].Evaluate(environment);

					if (StringEvaluationType.TryGetString(textVal, out string? text) && EnumEvaluationType.TryGetEnumValue(formatVal, out TextFormat? format) && FloatEvaluationType.TryGetFloat(fontsizeVal, out float fontsize)) {
						float ascent = FontMetrics.GetAscent(text, graphicsData.Fonts, format.Value, fontsize);
						float descent = FontMetrics.GetDescent(text, graphicsData.Fonts, format.Value, fontsize);
						float height = Math.Abs(ascent) + Math.Abs(descent); // TODO This doesn't make sense
						return new EvaluationValue(height, environment.GetType<FloatEvaluationType>());
					}
					else {
						throw new EvaluationCalculationException($"Cannot call {Name} with values of types {textVal.Type.Name}, {formatVal.Type.Name}, and {fontsizeVal.Type.Name}.");
					}

				}
			}
		}

		public class AscentFunctionInfo : IEnvironmentFunctionInfo {
			public static readonly AscentFunctionInfo Instance = new AscentFunctionInfo();
			private AscentFunctionInfo() { }

			public EvaluationName Name { get; } = "ascent";
			public string? Description { get; } = "Returns the ascent of the input text " +
				"(the distance of the highest point in that text above the text baseline), " +
				"at the given fontsize, for the given font format (which will use the font " +
				"associated with that format in the current graphics state).";

			public EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(
						new EnvironmentFunctionArg("text", context.GetType<StringEvaluationType>(), null),
						new EnvironmentFunctionArg("format", context.GetSystemType<TextFormat>(), null), // TODO This would be better as some kind of getter on the context using the system enum type?
						new EnvironmentFunctionArg("fontsize", context.GetType<FloatEvaluationType>(), null)
					)
				);
			}

			public EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				return context.GetType<FloatEvaluationType>();
			}

			public static IEnvironmentFunction GetFunction(MarkupCanvasGraphicsData graphicsData) {
				return new AscentFunctionEvaluator(graphicsData);
			}

			private class AscentFunctionEvaluator : AscentFunctionInfo, IEnvironmentFunction {
				private readonly MarkupCanvasGraphicsData graphicsData;

				public AscentFunctionEvaluator(MarkupCanvasGraphicsData graphicsData) {
					this.graphicsData = graphicsData;
				}

				public EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
					EvaluationValue textVal = args[0].Evaluate(environment);
					EvaluationValue formatVal = args[1].Evaluate(environment);
					EvaluationValue fontsizeVal = args[2].Evaluate(environment);

					if (StringEvaluationType.TryGetString(textVal, out string? text) && EnumEvaluationType.TryGetEnumValue(formatVal, out TextFormat? format) && FloatEvaluationType.TryGetFloat(fontsizeVal, out float fontsize)) {
						float ascent = FontMetrics.GetAscent(text, graphicsData.Fonts, format.Value, fontsize);
						return new EvaluationValue(ascent, environment.GetType<FloatEvaluationType>());
					}
					else {
						throw new EvaluationCalculationException($"Cannot call {Name} with values of types {textVal.Type.Name}, {formatVal.Type.Name}, and {fontsizeVal.Type.Name}.");
					}

				}
			}
		}

		public class FromRelativeFunction : AbstractSingleArgFunction {
			public static readonly FromRelativeFunction Instance = new FromRelativeFunction();
			private FromRelativeFunction() { }

			public override EvaluationName Name { get; } = "fromrelative";
			public override string? Description { get; } = "Returns a relative Dimension value with the argument as the relative size.";

			protected override string? Warning => null;

			protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
				return new EnvironmentFunctionArg("relative", context.GetType<FloatEvaluationType>(), null);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
				return context.GetType<DimensionEvaluationType>();
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
				EvaluationValue a = arg.Evaluate(environment);
				if(FloatEvaluationType.TryGetFloat(a, out float relative)) {
					return new EvaluationValue(Dimension.FromRelative(relative), environment.GetType<DimensionEvaluationType>());
				}
				else {
					throw new InvalidCastException($"Cannot cast from {a.Type.Name} to float.");
				}
			}
		}

		public class FromPointsFunction : AbstractSingleArgFunction {
			public static readonly FromPointsFunction Instance = new FromPointsFunction();
			private FromPointsFunction() { }

			public override EvaluationName Name { get; } = "frompoints";
			public override string? Description { get; } = "Returns an absolute Dimension value with the argument as the size in points.";

			protected override string? Warning => null;

			protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
				return new EnvironmentFunctionArg("points", context.GetType<FloatEvaluationType>(), null);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
				return context.GetType<DimensionEvaluationType>();
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
				EvaluationValue a = arg.Evaluate(environment);
				if (FloatEvaluationType.TryGetFloat(a, out float points)) {
					return new EvaluationValue(Dimension.FromPoints(points), environment.GetType<DimensionEvaluationType>());
				}
				else {
					throw new InvalidCastException($"Cannot cast from {a.Type.Name} to float.");
				}
			}
		}

		public class FromPercentFunction : AbstractSingleArgFunction {
			public static readonly FromPercentFunction Instance = new FromPercentFunction();
			private FromPercentFunction() { }

			public override EvaluationName Name { get; } = "frompercent";
			public override string? Description { get; } = "Returns a percentage Dimension value with the argument as the percentage size (range from 0 to 100).";

			protected override string? Warning => null;

			protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
				return new EnvironmentFunctionArg("percent", context.GetType<FloatEvaluationType>(), null);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
				return context.GetType<DimensionEvaluationType>();
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
				EvaluationValue a = arg.Evaluate(environment);
				if (FloatEvaluationType.TryGetFloat(a, out float percent)) {
					return new EvaluationValue(Dimension.FromPercent(percent), environment.GetType<DimensionEvaluationType>());
				}
				else {
					throw new InvalidCastException($"Cannot cast from {a.Type.Name} to float.");
				}
			}
		}

		public class FromCentimetresFunction : AbstractSingleArgFunction {
			public static readonly FromCentimetresFunction Instance = new FromCentimetresFunction();
			private FromCentimetresFunction() { }

			public override EvaluationName Name { get; } = "fromcentimetres";
			public override string? Description { get; } = "Returns an absolute Dimension value with the argument as the size in centimetres.";

			protected override string? Warning => null;

			protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
				return new EnvironmentFunctionArg("centimetres", context.GetType<FloatEvaluationType>(), null);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
				return context.GetType<DimensionEvaluationType>();
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
				EvaluationValue a = arg.Evaluate(environment);
				if (FloatEvaluationType.TryGetFloat(a, out float centimetres)) {
					return new EvaluationValue(Dimension.FromCentimetres(centimetres), environment.GetType<DimensionEvaluationType>());
				}
				else {
					throw new InvalidCastException($"Cannot cast from {a.Type.Name} to float.");
				}
			}
		}

		public class FromMillimetresFunction : AbstractSingleArgFunction {
			public static readonly FromMillimetresFunction Instance = new FromMillimetresFunction();
			private FromMillimetresFunction() { }

			public override EvaluationName Name { get; } = "frommillimetres";
			public override string? Description { get; } = "Returns an absolute Dimension value with the argument as the size in millimetres.";

			protected override string? Warning => null;

			protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
				return new EnvironmentFunctionArg("millimetres", context.GetType<FloatEvaluationType>(), null);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
				return context.GetType<DimensionEvaluationType>();
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
				EvaluationValue a = arg.Evaluate(environment);
				if (FloatEvaluationType.TryGetFloat(a, out float millimetres)) {
					return new EvaluationValue(Dimension.FromMillimetres(millimetres), environment.GetType<DimensionEvaluationType>());
				}
				else {
					throw new InvalidCastException($"Cannot cast from {a.Type.Name} to float.");
				}
			}
		}

		public class FromInchesFunction : AbstractSingleArgFunction {
			public static readonly FromInchesFunction Instance = new FromInchesFunction();
			private FromInchesFunction() { }

			public override EvaluationName Name { get; } = "frominches";
			public override string? Description { get; } = "Returns an absolute Dimension value with the argument as the size in inches.";

			protected override string? Warning => null;

			protected override EnvironmentFunctionArg GetArgument(EvaluationContext context) {
				return new EnvironmentFunctionArg("inches", context.GetType<FloatEvaluationType>(), null);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode arg) {
				return context.GetType<DimensionEvaluationType>();
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode arg) {
				EvaluationValue a = arg.Evaluate(environment);
				if (FloatEvaluationType.TryGetFloat(a, out float inches)) {
					return new EvaluationValue(Dimension.FromCentimetres(inches), environment.GetType<DimensionEvaluationType>());
				}
				else {
					throw new InvalidCastException($"Cannot cast from {a.Type.Name} to float.");
				}
			}
		}

		// TODO We should be able to remove this
		public class FromAutoFunction : AbstractFunction {
			public static readonly FromAutoFunction Instance = new FromAutoFunction();
			private FromAutoFunction() { }

			public override EvaluationName Name { get; } = "fromauto";
			public override string? Description { get; } = "Returns an automatic Dimension value.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				return context.GetType<DimensionEvaluationType>();
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				return new EvaluationValue(Dimension.Automatic, environment.GetType<DimensionEvaluationType>());
			}
		}

		public class DarkenColorFunction : AbstractFunction {
			public static readonly DarkenColorFunction Instance = new DarkenColorFunction();
			private DarkenColorFunction() { }

			public override EvaluationName Name { get; } = "darken";
			public override string? Description { get; } = "Darkens a color by the given factor (which will be clamped to the range 0 to 1). This amounts to multiplying the HSV value/lightness by the factor, and keeping the same hue and saturation.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(
						new EnvironmentFunctionArg("color", context.GetType<ColorEvaluationType>(), null),
						new EnvironmentFunctionArg("factor", context.GetType<FloatEvaluationType>(), null)
						)
				);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				return context.GetType<ColorEvaluationType>();
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue colorVal = args[0].Evaluate(environment);
				EvaluationValue factorVal = args[1].Evaluate(environment);

				if (ColorEvaluationType.TryGetColor(colorVal, out Color color) && FloatEvaluationType.TryGetFloat(factorVal, out float factor)) {
					Color result = color.Darken(factor);
					return new EvaluationValue(result, environment.GetType<ColorEvaluationType>());
				}
				else {
					throw new EvaluationCalculationException($"Cannot call {Name} with values of types {colorVal.Type.Name} and {factorVal.Type.Name}.");
				}
			}
		}

		public class LightenColorFunction : AbstractFunction {
			public static readonly LightenColorFunction Instance = new LightenColorFunction();
			private LightenColorFunction() { }

			public override EvaluationName Name { get; } = "lighten";
			public override string? Description { get; } = "Lightens a color by the given factor (which will be clamped to the range 0 to 1). This amounts to changing the HSV value/lightness (L) with the factor (f) to 1-((1-L)*f), and keeping the same hue and saturation.";

			public override EnvironmentFunctionArguments GetArguments(EvaluationContext context) {
				return new EnvironmentFunctionArguments(null,
					new EnvironmentFunctionArgList(
						new EnvironmentFunctionArg("color", context.GetType<ColorEvaluationType>(), null),
						new EnvironmentFunctionArg("factor", context.GetType<FloatEvaluationType>(), null)
						)
				);
			}

			public override EvaluationType GetReturnType(EvaluationContext context, EvaluationNode[] args) {
				return context.GetType<ColorEvaluationType>();
			}

			public override EvaluationValue Evaluate(IEnvironment environment, EvaluationNode[] args) {
				EvaluationValue colorVal = args[0].Evaluate(environment);
				EvaluationValue factorVal = args[1].Evaluate(environment);

				if (ColorEvaluationType.TryGetColor(colorVal, out Color color) && FloatEvaluationType.TryGetFloat(factorVal, out float factor)) {
					Color result = color.Lighten(factor);
					return new EvaluationValue(result, environment.GetType<ColorEvaluationType>());
				}
				else {
					throw new EvaluationCalculationException($"Cannot call {Name} with values of types {colorVal.Type.Name} and {factorVal.Type.Name}.");
				}
			}
		}

	}

}
