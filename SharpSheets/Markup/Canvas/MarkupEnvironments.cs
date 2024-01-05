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

namespace SharpSheets.Markup.Canvas {

	public static class MarkupEnvironments {
		// This class only accepts graphics snapshots, which means all the environments it produces should be static/immutable

		public static VariableNode WidthNode { get; } = new VariableNode("width", EvaluationType.FLOAT);
		public static VariableNode HeightNode { get; } = new VariableNode("height", EvaluationType.FLOAT);
		public static EvaluationNode BoundingBoxLengthNode { get; } = new MinVarNode().AssignArguments(WidthNode, HeightNode);

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
			VariableBoxes.Simple(
				new Dictionary<EvaluationName, EvaluationType> {
					// SharpCanvas variables
					{ "linewidth",  EvaluationType.FLOAT },
					{ "foreground",  EvaluationType.COLOR },
					{ "background",  EvaluationType.COLOR },
					{ "midtone",  EvaluationType.COLOR },
					{ "textcolor",  EvaluationType.COLOR }
				},
				new List<EnvironmentFunctionInfo> {
					new EnvironmentFunctionInfo("width", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("height", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("fromrelative", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("frompoints", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("frompercent", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("fromcentimetres", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("frominches", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("fromauto", DimensionExpression.DimensionType, Array.Empty<EvaluationType>()),
					new EnvironmentFunctionInfo("sumdimensions", DimensionExpression.DimensionType, new EvaluationType[] { DimensionExpression.DimensionType, DimensionExpression.DimensionType }),
					new EnvironmentFunctionInfo("multiplydimension", DimensionExpression.DimensionType, new EvaluationType[] { DimensionExpression.DimensionType, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("darken", EvaluationType.COLOR, new EvaluationType[] { EvaluationType.COLOR, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("lighten", EvaluationType.COLOR, new EvaluationType[] { EvaluationType.COLOR, EvaluationType.FLOAT })
				});

		// TODO Why do these IVariableBoxes contain so much repetition? There is a better way of doing this.
		/// <summary>
		/// Variable state for when the exact drawing dimensions are unknown ("drawwidth"/"drawheight"/etc.).
		/// Includes graphics state variables and markup canvas area values ("width"/"height"/etc.).
		/// </summary>
		public static IVariableBox InferenceDrawingStateVariables { get; } =
			VariableBoxes.Simple(
				new Dictionary<EvaluationName, EvaluationType> {
					// SharpCanvas variables
					{ "linewidth",  EvaluationType.FLOAT },
					{ "foreground",  EvaluationType.COLOR },
					{ "background",  EvaluationType.COLOR },
					{ "midtone",  EvaluationType.COLOR },
					{ "textcolor",  EvaluationType.COLOR },
					// MarkupCanvas area variables
					{ "width", EvaluationType.FLOAT },
					{ "height",  EvaluationType.FLOAT },
					{ "left",  EvaluationType.FLOAT },
					{ "right",  EvaluationType.FLOAT },
					{ "bottom",  EvaluationType.FLOAT },
					{ "top",  EvaluationType.FLOAT },
					// Random seed calculated from area variables
					{ "seed",  EvaluationType.INT }
				},
				new List<EnvironmentFunctionInfo> {
					new EnvironmentFunctionInfo("width", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("height", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("fromrelative", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("frompoints", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("frompercent", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("fromcentimetres", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("frominches", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("fromauto", DimensionExpression.DimensionType, Array.Empty<EvaluationType>()),
					new EnvironmentFunctionInfo("sumdimensions", DimensionExpression.DimensionType, new EvaluationType[] { DimensionExpression.DimensionType, DimensionExpression.DimensionType }),
					new EnvironmentFunctionInfo("multiplydimension", DimensionExpression.DimensionType, new EvaluationType[] { DimensionExpression.DimensionType, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("darken", EvaluationType.COLOR, new EvaluationType[] { EvaluationType.COLOR, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("lighten", EvaluationType.COLOR, new EvaluationType[] { EvaluationType.COLOR, EvaluationType.FLOAT })
				});

		/// <summary>
		/// Variable state for when the full drawing information is available, including the exact dimensions of the drawing area.
		/// </summary>
		public static IVariableBox DrawingStateVariables { get; } =
			VariableBoxes.Simple(
				new Dictionary<EvaluationName, EvaluationType> {
					// SharpCanvas variables
					{ "linewidth",  EvaluationType.FLOAT },
					{ "foreground",  EvaluationType.COLOR },
					{ "background",  EvaluationType.COLOR },
					{ "midtone",  EvaluationType.COLOR },
					{ "textcolor",  EvaluationType.COLOR },
					// MarkupCanvas area variables
					{ "width", EvaluationType.FLOAT },
					{ "height",  EvaluationType.FLOAT },
					{ "left",  EvaluationType.FLOAT },
					{ "right",  EvaluationType.FLOAT },
					{ "bottom",  EvaluationType.FLOAT },
					{ "top",  EvaluationType.FLOAT },
					// Drawing rect variables
					{ "drawwidth", EvaluationType.FLOAT },
					{ "drawheight",  EvaluationType.FLOAT },
					{ "drawleft",  EvaluationType.FLOAT },
					{ "drawright",  EvaluationType.FLOAT },
					{ "drawbottom",  EvaluationType.FLOAT },
					{ "drawtop",  EvaluationType.FLOAT },
					// Random seed calculated from area variables
					{ "seed",  EvaluationType.INT }
				},
				new List<EnvironmentFunctionInfo> {
					new EnvironmentFunctionInfo("width", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("ascent", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("height", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("fromrelative", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("frompoints", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("frompercent", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("fromcentimetres", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("frominches", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("fromauto", DimensionExpression.DimensionType, Array.Empty<EvaluationType>()),
					new EnvironmentFunctionInfo("sumdimensions", DimensionExpression.DimensionType, new EvaluationType[] { DimensionExpression.DimensionType, DimensionExpression.DimensionType }),
					new EnvironmentFunctionInfo("multiplydimension", DimensionExpression.DimensionType, new EvaluationType[] { DimensionExpression.DimensionType, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("darken", EvaluationType.COLOR, new EvaluationType[] { EvaluationType.COLOR, EvaluationType.FLOAT }),
					new EnvironmentFunctionInfo("lighten", EvaluationType.COLOR, new EvaluationType[] { EvaluationType.COLOR, EvaluationType.FLOAT })
				});

		private static T ConvertValue<T>(object? value) {
			if(value is T converted) {
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
			if(arg is T enumVal) {
				return enumVal;
			}
			else if(arg is string stringVal) {
				try {
					return EnumUtils.ParseEnum<T>(stringVal);
				}
				catch(FormatException e) {
					throw new EvaluationCalculationException("Invalid value for enum argument.", e);
				}
			}
			else {
				throw new EvaluationCalculationException($"Invalid type for enum argument, must be string or {typeof(T).Name}, got {arg?.GetType().Name ?? "null"}.");
			}
		}

		public static IEnvironment MakeGraphicsStateEnvironment(MarkupCanvasGraphicsData graphicsData) {
			return Environments.Simple(
				new Dictionary<EvaluationName, object> {
					// SharpCanvas variables
					{ "linewidth", graphicsData.DefaultLineWidth },
					{ "foreground",  graphicsData.ForegroundColor },
					{ "background",  graphicsData.BackgroundColor },
					{ "midtone",  graphicsData.MidtoneColor },
					{ "textcolor",  graphicsData.TextColor }
				},
				Enumerable.Empty<KeyValuePair<EvaluationName, EvaluationNode>>(),
				new List<EnvironmentFunctionDefinition> {
					new EnvironmentFunctionDefinition(
						"width", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT },
						args => FontMetrics.GetWidth(ConvertValue<string>(args[0]), graphicsData.Fonts, ParseEnumArg<TextFormat>(args[1]), CastToFloat(args[2]))),
					new EnvironmentFunctionDefinition(
						"height", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT },
						args => {
							string text = ConvertValue<string>(args[0]);
							TextFormat format = ParseEnumArg<TextFormat>(args[1]);
							float fontsize = CastToFloat(args[2]);
							
							float ascent = FontMetrics.GetAscent(text, graphicsData.Fonts, format, fontsize);
							float descent = FontMetrics.GetDescent(text, graphicsData.Fonts, format, fontsize);
							return Math.Abs(ascent) + Math.Abs(descent);
							}),
					new EnvironmentFunctionDefinition(
						"ascent", EvaluationType.FLOAT, new EvaluationType[] { EvaluationType.STRING, MarkupEvaluationTypes.TEXT_FORMAT, EvaluationType.FLOAT },
						args => {
							string text = ConvertValue<string>(args[0]);
							TextFormat format = ParseEnumArg<TextFormat>(args[1]);
							float fontsize = CastToFloat(args[2]);

							float ascent = FontMetrics.GetAscent(text, graphicsData.Fonts, format, fontsize);
							return ascent;
							}),
					new EnvironmentFunctionDefinition(
						"fromrelative", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT },
						args => Dimension.FromRelative(CastToFloat(args[0]))),
					new EnvironmentFunctionDefinition(
						"frompoints", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT },
						args => Dimension.FromPoints(CastToFloat(args[0]))),
					new EnvironmentFunctionDefinition(
						"frompercent", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT },
						args => Dimension.FromPercent(CastToFloat(args[0]))),
					new EnvironmentFunctionDefinition(
						"fromcentimetres", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT },
						args => Dimension.FromCentimetres(CastToFloat(args[0]))),
					new EnvironmentFunctionDefinition(
						"frominches", DimensionExpression.DimensionType, new EvaluationType[] { EvaluationType.FLOAT },
						args => Dimension.FromInches(CastToFloat(args[0]))),
					new EnvironmentFunctionDefinition(
						"fromauto", DimensionExpression.DimensionType, Array.Empty<EvaluationType>(),
						args => Dimension.Automatic),
					new EnvironmentFunctionDefinition(
						"sumdimensions", DimensionExpression.DimensionType, new EvaluationType[] { DimensionExpression.DimensionType, DimensionExpression.DimensionType },
						args => ConvertValue<Dimension>(args[0]) + ConvertValue<Dimension>(args[1])),
					new EnvironmentFunctionDefinition(
						"multiplydimension", DimensionExpression.DimensionType, new EvaluationType[] { DimensionExpression.DimensionType, EvaluationType.FLOAT },
						args => CastToFloat(args[1]) * ConvertValue<Dimension>(args[0])),
					new EnvironmentFunctionDefinition(
						"darken", EvaluationType.COLOR, new EvaluationType[] { EvaluationType.COLOR, EvaluationType.FLOAT },
						args => ConvertValue<Color>(args[0]).Darken(CastToFloat(args[1]))),
					new EnvironmentFunctionDefinition(
						"lighten", EvaluationType.COLOR, new EvaluationType[] { EvaluationType.COLOR, EvaluationType.FLOAT },
						args => ConvertValue<Color>(args[0]).Lighten(CastToFloat(args[1])))
				},
				null);
		}

		public static IEnvironment MakeDrawingStateEnvironment(MarkupCanvasGraphicsData graphicsData, Layouts.Rectangle drawingRect, Layouts.Size? referenceRect) {
			return MakeGraphicsStateEnvironment(graphicsData).AppendEnvironment(
				Environments.Simple(
					new Dictionary<EvaluationName, object> {
						// Canvas area variables
						{ "width", referenceRect?.Width ?? drawingRect.Width },
						{ "height", referenceRect?.Height ?? drawingRect.Height },
						{ "left", referenceRect!=null ? 0f : drawingRect.Left },
						{ "right", referenceRect !=null ? referenceRect.Width : drawingRect.Right },
						{ "bottom", referenceRect!=null ? 0f : drawingRect.Bottom },
						{ "top", referenceRect!=null ? referenceRect.Height : drawingRect.Top },
						// Drawing rect variables
						{ "drawwidth", drawingRect.Width },
						{ "drawheight",  drawingRect.Height },
						{ "drawleft",  drawingRect.Left },
						{ "drawright",  drawingRect.Right },
						{ "drawbottom",  drawingRect.Bottom },
						{ "drawtop",  drawingRect.Top },
						// Random seed calculated from area variables
						{ "seed",  drawingRect.GetHashCode() }
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

}
