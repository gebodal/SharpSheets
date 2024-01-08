﻿using SharpSheets.Evaluations;
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

namespace SharpSheets.Markup.Canvas {

	public static class MarkupEnvironments {
		// This class only accepts graphics snapshots, which means all the environments it produces should be static/immutable

		public static VariableNode WidthNode { get; } = new VariableNode("width", EvaluationType.FLOAT);
		public static VariableNode HeightNode { get; } = new VariableNode("height", EvaluationType.FLOAT);
		public static EvaluationNode BoundingBoxLengthNode { get; } = MinVarFunction.Instance.MakeNode(WidthNode, HeightNode);

		public static XLengthExpression ZeroWidthExpression { get; } = new XLengthExpression(0f);
		public static XLengthExpression CentreXExpression { get; } = new XLengthExpression(WidthNode * 0.5f);
		public static XLengthExpression WidthExpression { get; } = new XLengthExpression(WidthNode);
		public static YLengthExpression ZeroHeightExpression { get; } = new YLengthExpression(0f);
		public static YLengthExpression CentreYExpression { get; } = new YLengthExpression(HeightNode * 0.5f);
		public static YLengthExpression HeightExpression { get; } = new YLengthExpression(HeightNode);
		public static BoundingBoxLengthExpression BoundingBoxLengthExpression { get; } = new BoundingBoxLengthExpression(BoundingBoxLengthNode);

		public static DrawPointExpression CentreExpression { get; } = new DrawPointExpression(CentreXExpression, CentreYExpression);

		public static bool IsWidthDefined(IVariableBox variables) {
			return variables.IsVariable("width");
		}
		public static bool IsHeightDefined(IVariableBox variables) {
			return variables.IsVariable("height");
		}
		public static bool IsBoundingBoxDefined(IVariableBox variables) {
			return IsWidthDefined(variables) && IsHeightDefined(variables);
		}

		public static RectangleExpression WholeAreaRectExpression { get; } = new RectangleExpression(
			0f, // new FloatExpression(new VariableNode("left", EvaluationType.FLOAT)),
			0f, // new FloatExpression(new VariableNode("bottom", EvaluationType.FLOAT)),
			WidthExpression,
			HeightExpression
			);

		public static ColorExpression BackgroundExpression { get; } = new ColorExpression(new VariableNode("background", EvaluationType.COLOR));
		public static ColorExpression TextColorExpression { get; } = new ColorExpression(new VariableNode("textcolor", EvaluationType.COLOR));

		/// <summary>
		/// Variable state for when only the graphics state is known (linewidth, colours, etc.), including functions for calculating text sizes, dimensions, and colors.
		/// </summary>
		public static IVariableBox GraphicsStateVariables { get; } =
			BasisEnvironment.Instance.AppendVariables(SimpleVariableBoxes.Create(
				new EnvironmentVariableInfo[] {
					// SharpCanvas variables
					MarkupEnvironmentVariables.LineWidth,
					MarkupEnvironmentVariables.Foreground,
					MarkupEnvironmentVariables.Background,
					MarkupEnvironmentVariables.Midtone,
					MarkupEnvironmentVariables.TextColor
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
					MarkupEnvironmentFunctions.SumDimensionsFunction.Instance,
					MarkupEnvironmentFunctions.MultiplyDimensionFunction.Instance,
					MarkupEnvironmentFunctions.DarkenColorFunction.Instance,
					MarkupEnvironmentFunctions.LightenColorFunction.Instance
				}));

		/// <summary>
		/// Variable state for when the exact drawing dimensions are unknown ("drawwidth"/"drawheight"/etc.).
		/// Includes graphics state variables and markup canvas area values ("width"/"height"/etc.).
		/// </summary>
		public static IVariableBox InferenceDrawingStateVariables { get; } =
			GraphicsStateVariables.AppendVariables(SimpleVariableBoxes.Create(
				new EnvironmentVariableInfo[] {
					// SharpCanvas variables
					MarkupEnvironmentVariables.LineWidth,
					MarkupEnvironmentVariables.Foreground,
					MarkupEnvironmentVariables.Background,
					MarkupEnvironmentVariables.Midtone,
					MarkupEnvironmentVariables.TextColor,
					// MarkupCanvas area variables
					MarkupEnvironmentVariables.Width,
					MarkupEnvironmentVariables.Height,
					MarkupEnvironmentVariables.Left,
					MarkupEnvironmentVariables.Right,
					MarkupEnvironmentVariables.Bottom,
					MarkupEnvironmentVariables.Top,
					// Random seed calculated from area variables
					MarkupEnvironmentVariables.Seed
				}));

		/// <summary>
		/// Variable state for when the full drawing information is available, including the exact dimensions of the drawing area.
		/// </summary>
		public static IVariableBox DrawingStateVariables { get; } =
			InferenceDrawingStateVariables.AppendVariables(SimpleVariableBoxes.Create(
				new EnvironmentVariableInfo[] {
					// Drawing rect variables
					MarkupEnvironmentVariables.DrawWidth,
					MarkupEnvironmentVariables.DrawHeight,
					MarkupEnvironmentVariables.DrawLeft,
					MarkupEnvironmentVariables.DrawRight,
					MarkupEnvironmentVariables.DrawBottom,
					MarkupEnvironmentVariables.DrawTop
				}));

		public static IEnvironment MakeGraphicsStateEnvironment(MarkupCanvasGraphicsData graphicsData) {
			return BasisEnvironment.Instance.AppendEnvironment(
				SimpleEnvironments.Create(
					new (object?, EnvironmentVariableInfo)[] {
						// SharpCanvas variables
						(graphicsData.DefaultLineWidth, MarkupEnvironmentVariables.LineWidth),
						(graphicsData.ForegroundColor, MarkupEnvironmentVariables.Foreground),
						(graphicsData.BackgroundColor, MarkupEnvironmentVariables.Background),
						(graphicsData.MidtoneColor, MarkupEnvironmentVariables.Midtone),
						(graphicsData.TextColor, MarkupEnvironmentVariables.TextColor)
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
						MarkupEnvironmentFunctions.SumDimensionsFunction.Instance,
						MarkupEnvironmentFunctions.MultiplyDimensionFunction.Instance,
						MarkupEnvironmentFunctions.DarkenColorFunction.Instance,
						MarkupEnvironmentFunctions.LightenColorFunction.Instance
					})
				);
		}

		public static IEnvironment MakeDrawingStateEnvironment(MarkupCanvasGraphicsData graphicsData, Layouts.Rectangle drawingRect, Layouts.Size? referenceRect) {
			return MakeGraphicsStateEnvironment(graphicsData).AppendEnvironment(
				SimpleEnvironments.Create(
					new (object?, EnvironmentVariableInfo)[] {
						// Canvas area variables
						(referenceRect?.Width ?? drawingRect.Width, MarkupEnvironmentVariables.Width),
						(referenceRect?.Height ?? drawingRect.Height, MarkupEnvironmentVariables.Height),
						(referenceRect != null ? 0f : drawingRect.Left, MarkupEnvironmentVariables.Left),
						(referenceRect != null ? referenceRect.Width : drawingRect.Right, MarkupEnvironmentVariables.Right),
						(referenceRect != null ? 0f : drawingRect.Bottom, MarkupEnvironmentVariables.Bottom),
						(referenceRect != null ? referenceRect.Height : drawingRect.Top, MarkupEnvironmentVariables.Top),
						// Drawing rect variables
						(drawingRect.Width, MarkupEnvironmentVariables.DrawWidth),
						(drawingRect.Height, MarkupEnvironmentVariables.DrawHeight),
						(drawingRect.Left, MarkupEnvironmentVariables.DrawLeft),
						(drawingRect.Right, MarkupEnvironmentVariables.DrawRight),
						(drawingRect.Bottom, MarkupEnvironmentVariables.DrawBottom),
						(drawingRect.Top, MarkupEnvironmentVariables.DrawTop),
						// Random seed calculated from area variables
						(drawingRect.GetHashCode(), MarkupEnvironmentVariables.Seed)
					})
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
		public readonly FontPathGrouping Fonts;

		public MarkupCanvasGraphicsData(float defaultLineWidth, Color foregroundColor, Color backgroundColor, Color midtoneColor, Color textColor, FontPathGrouping fonts) {
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

	public static class MarkupEnvironmentVariables {

		// SharpCanvas variables
		public static readonly EnvironmentVariableInfo LineWidth = new EnvironmentVariableInfo("linewidth", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo Foreground = new EnvironmentVariableInfo("foreground", EvaluationType.COLOR, null);
		public static readonly EnvironmentVariableInfo Background = new EnvironmentVariableInfo("background", EvaluationType.COLOR, null);
		public static readonly EnvironmentVariableInfo Midtone = new EnvironmentVariableInfo("midtone", EvaluationType.COLOR, null);
		public static readonly EnvironmentVariableInfo TextColor = new EnvironmentVariableInfo("textcolor", EvaluationType.COLOR, null);

		// Canvas area variables
		public static readonly EnvironmentVariableInfo Width = new EnvironmentVariableInfo("width", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo Height = new EnvironmentVariableInfo("height", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo Left = new EnvironmentVariableInfo("left", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo Right = new EnvironmentVariableInfo("right", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo Bottom = new EnvironmentVariableInfo("bottom", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo Top = new EnvironmentVariableInfo("top", EvaluationType.FLOAT, null);

		// Drawing rect variables
		public static readonly EnvironmentVariableInfo DrawWidth = new EnvironmentVariableInfo("drawwidth", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo DrawHeight = new EnvironmentVariableInfo("drawheight", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo DrawLeft = new EnvironmentVariableInfo("drawleft", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo DrawRight = new EnvironmentVariableInfo("drawright", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo DrawBottom = new EnvironmentVariableInfo("drawbottom", EvaluationType.FLOAT, null);
		public static readonly EnvironmentVariableInfo DrawTop = new EnvironmentVariableInfo("drawtop", EvaluationType.FLOAT, null);

		// Random seed calculated from area variables
		public static readonly EnvironmentVariableInfo Seed = new EnvironmentVariableInfo("seed", EvaluationType.INT, null);

	}

	public static class MarkupEnvironmentFunctions {

		public class WidthFunctionInfo : IEnvironmentFunctionInfo {
			public static readonly WidthFunctionInfo Instance = new WidthFunctionInfo();
			private WidthFunctionInfo() { }

			public EvaluationName Name { get; } = "width";
			public string? Description { get; } = null;

			public EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("text", EvaluationType.STRING, null),
					new EnvironmentFunctionArg("format", MarkupEvaluationTypes.TEXT_FORMAT, null),
					new EnvironmentFunctionArg("fontsize", EvaluationType.FLOAT, null)
					)
			);

			public EvaluationType GetReturnType(EvaluationNode[] args) {
				return EvaluationType.FLOAT;
			}

			public static IEnvironmentFunction GetFunction(MarkupCanvasGraphicsData graphicsData) {
				return new WidthFunctionEvaluator(graphicsData);
			}

			private class WidthFunctionEvaluator : WidthFunctionInfo, IEnvironmentFunction {
				private readonly MarkupCanvasGraphicsData graphicsData;

				public WidthFunctionEvaluator(MarkupCanvasGraphicsData graphicsData) {
					this.graphicsData = graphicsData;
				}

				public object? Evaluate(IEnvironment environment, EvaluationNode[] args) {
					string text = ConvertValue<string>(args[0].Evaluate(environment));
					TextFormat format = ParseEnumArg<TextFormat>(args[1].Evaluate(environment));
					float fontsize = CastToFloat(args[2].Evaluate(environment));
					return FontMetrics.GetWidth(text, graphicsData.Fonts, format, fontsize);
				}
			}
		}

		public class HeightFunctionInfo : IEnvironmentFunctionInfo {
			public static readonly HeightFunctionInfo Instance = new HeightFunctionInfo();
			private HeightFunctionInfo() { }

			public EvaluationName Name { get; } = "height";
			public string? Description { get; } = null;

			public EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("text", EvaluationType.STRING, null),
					new EnvironmentFunctionArg("format", MarkupEvaluationTypes.TEXT_FORMAT, null),
					new EnvironmentFunctionArg("fontsize", EvaluationType.FLOAT, null)
					)
			);

			public EvaluationType GetReturnType(EvaluationNode[] args) {
				return EvaluationType.FLOAT;
			}

			public static IEnvironmentFunction GetFunction(MarkupCanvasGraphicsData graphicsData) {
				return new HeightFunctionEvaluator(graphicsData);
			}

			private class HeightFunctionEvaluator : HeightFunctionInfo, IEnvironmentFunction {
				private readonly MarkupCanvasGraphicsData graphicsData;

				public HeightFunctionEvaluator(MarkupCanvasGraphicsData graphicsData) {
					this.graphicsData = graphicsData;
				}

				public object? Evaluate(IEnvironment environment, EvaluationNode[] args) {
					string text = ConvertValue<string>(args[0].Evaluate(environment));
					TextFormat format = ParseEnumArg<TextFormat>(args[1].Evaluate(environment));
					float fontsize = CastToFloat(args[2].Evaluate(environment));

					float ascent = FontMetrics.GetAscent(text, graphicsData.Fonts, format, fontsize);
					float descent = FontMetrics.GetDescent(text, graphicsData.Fonts, format, fontsize);
					return Math.Abs(ascent) + Math.Abs(descent);
				}
			}
		}

		public class AscentFunctionInfo : IEnvironmentFunctionInfo {
			public static readonly AscentFunctionInfo Instance = new AscentFunctionInfo();
			private AscentFunctionInfo() { }

			public EvaluationName Name { get; } = "ascent";
			public string? Description { get; } = null;

			public EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("text", EvaluationType.STRING, null),
					new EnvironmentFunctionArg("format", MarkupEvaluationTypes.TEXT_FORMAT, null),
					new EnvironmentFunctionArg("fontsize", EvaluationType.FLOAT, null)
					)
			);

			public EvaluationType GetReturnType(EvaluationNode[] args) {
				return EvaluationType.FLOAT;
			}

			public static IEnvironmentFunction GetFunction(MarkupCanvasGraphicsData graphicsData) {
				return new AscentFunctionEvaluator(graphicsData);
			}

			private class AscentFunctionEvaluator : AscentFunctionInfo, IEnvironmentFunction {
				private readonly MarkupCanvasGraphicsData graphicsData;

				public AscentFunctionEvaluator(MarkupCanvasGraphicsData graphicsData) {
					this.graphicsData = graphicsData;
				}

				public object? Evaluate(IEnvironment environment, EvaluationNode[] args) {
					string text = ConvertValue<string>(args[0].Evaluate(environment));
					TextFormat format = ParseEnumArg<TextFormat>(args[1].Evaluate(environment));
					float fontsize = CastToFloat(args[2].Evaluate(environment));
					return FontMetrics.GetAscent(text, graphicsData.Fonts, format, fontsize);
				}
			}
		}

		public class FromRelativeFunction : AbstractFunction {
			public static readonly FromRelativeFunction Instance = new FromRelativeFunction();
			private FromRelativeFunction() { }

			public override EvaluationName Name { get; } = "fromrelative";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("relative", EvaluationType.FLOAT, null))
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return DimensionExpression.DimensionType;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				object? a = args[0].Evaluate(environment);
				return Dimension.FromRelative(CastToFloat(a));
			}
		}

		public class FromPointsFunction : AbstractFunction {
			public static readonly FromPointsFunction Instance = new FromPointsFunction();
			private FromPointsFunction() { }

			public override EvaluationName Name { get; } = "frompoints";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("points", EvaluationType.FLOAT, null))
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return DimensionExpression.DimensionType;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				float a = CastToFloat(args[0].Evaluate(environment));
				return Dimension.FromPoints(a);
			}
		}

		public class FromPercentFunction : AbstractFunction {
			public static readonly FromPercentFunction Instance = new FromPercentFunction();
			private FromPercentFunction() { }

			public override EvaluationName Name { get; } = "frompercent";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("percent", EvaluationType.FLOAT, null))
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return DimensionExpression.DimensionType;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				float a = CastToFloat(args[0].Evaluate(environment));
				return Dimension.FromPercent(a);
			}
		}

		public class FromCentimetresFunction : AbstractFunction {
			public static readonly FromCentimetresFunction Instance = new FromCentimetresFunction();
			private FromCentimetresFunction() { }

			public override EvaluationName Name { get; } = "fromcentimetres";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("centimetres", EvaluationType.FLOAT, null))
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return DimensionExpression.DimensionType;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				float a = CastToFloat(args[0].Evaluate(environment));
				return Dimension.FromCentimetres(a);
			}
		}

		public class FromMillimetresFunction : AbstractFunction {
			public static readonly FromMillimetresFunction Instance = new FromMillimetresFunction();
			private FromMillimetresFunction() { }

			public override EvaluationName Name { get; } = "frommillimetres";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("millimetres", EvaluationType.FLOAT, null))
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return DimensionExpression.DimensionType;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				float a = CastToFloat(args[0].Evaluate(environment));
				return Dimension.FromMillimetres(a);
			}
		}

		public class FromInchesFunction : AbstractFunction {
			public static readonly FromInchesFunction Instance = new FromInchesFunction();
			private FromInchesFunction() { }

			public override EvaluationName Name { get; } = "frominches";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("inches", EvaluationType.FLOAT, null))
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return DimensionExpression.DimensionType;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				float a = CastToFloat(args[0].Evaluate(environment));
				return Dimension.FromCentimetres(a);
			}
		}

		public class FromAutoFunction : AbstractFunction {
			public static readonly FromAutoFunction Instance = new FromAutoFunction();
			private FromAutoFunction() { }

			public override EvaluationName Name { get; } = "fromauto";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return DimensionExpression.DimensionType;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				return Dimension.Automatic;
			}
		}

		public class SumDimensionsFunction : AbstractFunction {
			public static readonly SumDimensionsFunction Instance = new SumDimensionsFunction();
			private SumDimensionsFunction() { }

			public override EvaluationName Name { get; } = "sumdimensions";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(new EnvironmentFunctionArg("dim", DimensionExpression.DimensionType, null), true)
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return DimensionExpression.DimensionType;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				return DimensionUtils.Sum(args.Select(a => ConvertValue<Dimension>(a.Evaluate(environment))));
			}
		}

		public class MultiplyDimensionFunction : AbstractFunction {
			public static readonly MultiplyDimensionFunction Instance = new MultiplyDimensionFunction();
			private MultiplyDimensionFunction() { }

			public override EvaluationName Name { get; } = "multiplydimension";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("dimension", DimensionExpression.DimensionType, null),
					new EnvironmentFunctionArg("factor", EvaluationType.FLOAT, null)
					)
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return DimensionExpression.DimensionType;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				Dimension dim = ConvertValue<Dimension>(args[0].Evaluate(environment));
				float factor = CastToFloat(args[1].Evaluate(environment));
				return factor * dim;
			}
		}

		public class DarkenColorFunction : AbstractFunction {
			public static readonly DarkenColorFunction Instance = new DarkenColorFunction();
			private DarkenColorFunction() { }

			public override EvaluationName Name { get; } = "darken";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("color", EvaluationType.COLOR, null),
					new EnvironmentFunctionArg("factor", EvaluationType.FLOAT, null)
					)
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return EvaluationType.COLOR;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				Color color = ConvertValue<Color>(args[0].Evaluate(environment));
				float factor = CastToFloat(args[1].Evaluate(environment));
				return color.Darken(factor);
			}
		}

		public class LightenColorFunction : AbstractFunction {
			public static readonly LightenColorFunction Instance = new LightenColorFunction();
			private LightenColorFunction() { }

			public override EvaluationName Name { get; } = "lighten";
			public override string? Description { get; } = null;

			public override EnvironmentFunctionArguments Args { get; } = new EnvironmentFunctionArguments(null,
				new EnvironmentFunctionArgList(
					new EnvironmentFunctionArg("color", EvaluationType.COLOR, null),
					new EnvironmentFunctionArg("factor", EvaluationType.FLOAT, null)
					)
			);

			public override EvaluationType GetReturnType(EvaluationNode[] args) {
				return EvaluationType.COLOR;
			}

			public override object Evaluate(IEnvironment environment, EvaluationNode[] args) {
				Color color = ConvertValue<Color>(args[0].Evaluate(environment));
				float factor = CastToFloat(args[1].Evaluate(environment));
				return color.Lighten(factor);
			}
		}

		#region Helper Functions

		private static T ConvertValue<T>(object? value) {
			if (value is T converted) {
				return converted;
			}
			else {
				throw new InvalidCastException($"Cannot cast from {value?.GetType().Name ?? "null"} to {typeof(T).Name}.");
			}
		}

		private static float CastToFloat(object? value) {
			return value switch {
				int intVal => intVal,
				float floatVal => floatVal,
				double doubleVal => (float)doubleVal,
				uint uintVal => uintVal,
				UFloat ufloatVal => ufloatVal.Value,
				_ => throw new InvalidCastException($"Cannot cast from {value?.GetType().Name ?? "null"} to float.")
			};
		}

		private static T ParseEnumArg<T>(object? arg) where T : Enum {
			if (arg is T enumVal) {
				return enumVal;
			}
			else if (arg is string stringVal) {
				try {
					return EnumUtils.ParseEnum<T>(stringVal);
				}
				catch (FormatException e) {
					throw new EvaluationCalculationException("Invalid value for enum argument.", e);
				}
			}
			else {
				throw new EvaluationCalculationException($"Invalid type for enum argument, must be string or {typeof(T).Name}, got {arg?.GetType().Name ?? "null"}.");
			}
		}

		#endregion

	}

}
