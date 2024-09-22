using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpSheets.Fonts;
using SharpSheets.Exceptions;
using SharpSheets.Colors;
using SharpSheets.Canvas.Text;

namespace SharpSheets.Canvas {

	public interface ISharpGraphicsData {

		Rectangle CanvasRect { get; }

		float GetDefaultLineWidth();
		float GetLineWidth();

		CanvasPaint GetStrokePaint();
		CanvasPaint GetFillPaint();

		Color GetForegroundColor();
		Color GetBackgroundColor();
		Color GetMidtoneColor();
		Color GetTextColor();

		LineCapStyle GetLineCapStyle();
		LineJoinStyle GetLineJoinStyle();
		float GetMiterLimit();
		TextRenderingMode GetTextRenderingMode();

		StrokeDash? GetStrokeDash(); // null if not set

		float GetTextSize();
		TextFormat GetTextFormat();

		FontSettingGrouping GetFonts();

		float GetWidth(string text, TextFormat format, float fontsize);
		float GetAscent(string text, TextFormat format, float fontsize);
		float GetDescent(string text, TextFormat format, float fontsize);

		bool IsFieldsEnabled();

		string GetFieldPrefix();

		Transform GetTransform();

		float GetUserUnits();

	}

	public static class SharpGraphicsDefaults {

		public static readonly float LineWidth = 1f;

		public static readonly StrokeDash? StrokeDash = null;

		public static readonly Color Foreground = Color.Black;
		public static readonly Color Background = Color.White;
		public static readonly Color Midtone = Color.Gray;
		public static readonly Color TextColor = Color.Black;

		public static readonly CanvasPaint StrokePaint = new CanvasSolidPaint(Color.Black);
		public static readonly CanvasPaint FillPaint = new CanvasSolidPaint(Color.White);

		public static readonly LineCapStyle LineCapStyle = LineCapStyle.BUTT;
		public static readonly LineJoinStyle LineJoinStyle = LineJoinStyle.MITER;
		public static readonly float MitreLimit = 10.0f;

		public static readonly FontSettingGrouping Fonts = new FontSettingGrouping((FontSetting?)null, (FontSetting?)null, (FontSetting?)null, (FontSetting?)null);
		public static readonly TextFormat TextFormat = TextFormat.REGULAR;
		public static readonly float Fontsize = 0f;
		public static readonly TextRenderingMode TextRenderingMode = TextRenderingMode.FILL;

		public static readonly Transform Transform = Transform.Identity;

		public static readonly bool FieldsEnabled = true;
		public static readonly string FieldPrefix = "";

	}

	public interface ISharpGraphicsState : ISharpGraphicsData {

		ISharpGraphicsState SaveState();

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		ISharpGraphicsState RestoreState();

		int GetStateDepth();

		// TODO Need to decide on required interface
		//ISharpCanvas BeginLayer(string layerName);
		//ISharpCanvas EndLayer();

		ISharpGraphicsState SetDefaultLineWidth(float linewidth); // This should also set the current line width
		ISharpGraphicsState SetLineWidth(float linewidth);

		ISharpGraphicsState SetStrokeColor(Color color);
		ISharpGraphicsState SetStrokeLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops);
		ISharpGraphicsState SetStrokeRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops);

		ISharpGraphicsState SetFillColor(Color color);
		ISharpGraphicsState SetFillLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops);
		ISharpGraphicsState SetFillRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops);

		ISharpGraphicsState SetForegroundColor(Color color);
		ISharpGraphicsState SetBackgroundColor(Color color);
		ISharpGraphicsState SetMidtoneColor(Color color);
		ISharpGraphicsState SetTextColor(Color color);

		ISharpGraphicsState SetLineCapStyle(LineCapStyle capStyle);
		ISharpGraphicsState SetLineJoinStyle(LineJoinStyle joinStyle);
		ISharpGraphicsState SetMiterLimit(float mitreLimit);
		ISharpGraphicsState SetTextRenderingMode(TextRenderingMode mode);

		ISharpGraphicsState SetStrokeDash(StrokeDash? strokeDash);

		ISharpGraphicsState SetTextFormatAndSize(TextFormat font, float size);
		ISharpGraphicsState SetTextSize(float size);

		ISharpGraphicsState SetFont(TextFormat format, FontSetting? font); // TODO Nullable correct here?

		ISharpGraphicsState SetFieldsEnabled(bool enabled);

		ISharpGraphicsState SetFieldPrefix(string prefix);

		/// <summary></summary>
		/// <param name="transform"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		ISharpGraphicsState SetTransform(Transform transform);

		/// <summary></summary>
		/// <param name="transform"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		ISharpGraphicsState ApplyTransform(Transform transform);

	}

	public interface ISharpCanvas : ISharpGraphicsState {

		//void RegisterArea(object owner, Rectangle area);
		void RegisterAreas(object owner, Rectangle originalArea, Rectangle? adjustedArea, Rectangle[] innerAreas, PathHandleData[]? handles);

		ISharpCanvas Clip();
		ISharpCanvas EoClip();

		ISharpCanvas Rectangle(float x, float y, float width, float height);
		ISharpCanvas Ellipse(float x1, float y1, float x2, float y2);

		ISharpCanvas Stroke();
		ISharpCanvas Fill();
		ISharpCanvas EoFill();
		ISharpCanvas FillStroke();
		ISharpCanvas EoFillStroke();

		DrawPoint? CurrentPenLocation { get; }

		ISharpCanvas MoveTo(float x, float y);
		ISharpCanvas LineTo(float x, float y);
		ISharpCanvas CurveTo(float x2, float y2, float x3, float y3);
		ISharpCanvas CurveTo(float x1, float y1, float x2, float y2, float x3, float y3);
		ISharpCanvas ClosePath();
		ISharpCanvas EndPath();

		ISharpCanvas DrawText(string text, float x, float y);

		string? TextField(Rectangle rect, string name, string? tooltip, TextFieldType fieldType, string? value, TextFormat format, float fontSize, Color color, bool multiline, bool rich, Justification justification, int maxLen = -1);
		string? CheckField(Rectangle rect, string name, string? tooltip, CheckType checkType, Color color);


		/// <summary>
		/// 
		/// </summary>
		/// <param name="rect"></param>
		/// <param name="name"></param>
		/// <param name="tooltip"></param>
		/// <param name="defaultImage"></param>
		/// <returns></returns>
		/// <exception cref="IOException"></exception>
		string? ImageField(Rectangle rect, string name, string? tooltip, CanvasImageData? defaultImage = null); // ImageData defaultImage?
		
		IReadOnlyCollection<string> FieldNames { get; }
		IReadOnlyCollection<string> DocumentFieldNames { get; }

		//ISharpCanvas AddImage(FilePath image, Rectangle rect, float? imageAspect);
		//ISharpCanvas AddImage(Image image, Rectangle rect, float? imageAspect);
		/// <summary>
		/// 
		/// </summary>
		/// <param name="image"></param>
		/// <param name="rect"></param>
		/// <param name="imageAspect"></param>
		/// <returns></returns>
		/// <exception cref="IOException"></exception>
		ISharpCanvas AddImage(CanvasImageData image, Rectangle rect, float? imageAspect);

		void LogError(SharpDrawingException error);
		IEnumerable<SharpDrawingException> GetDrawingErrors();
	}

	public class PathHandleData {

		public DrawPoint[] Locations { get; }
		public bool[] OnCurve { get; }
		public bool IsClosed { get; }

		public int Length => Locations.Length;

		public PathHandleData(DrawPoint[] locations, bool[] onCurve, bool isClosed) {
			if(locations.Length != onCurve.Length) {
				throw new ArgumentException("Number of location and onCurve values must match.", nameof(onCurve));
			}

			Locations = locations;
			OnCurve = onCurve;
			IsClosed = isClosed;
		}

		public PathHandleData(DrawPoint[] locations, bool isClosed) {
			Locations = locations;
			OnCurve = true.MakeArray(Locations.Length);
			IsClosed = isClosed;
		}

	}

	public class SharpCanvasGraphicsSnapshot { // TODO ISharpGraphicsData?

		public Rectangle CanvasRect { get; }

		public float DefaultLineWidth { get; }
		public float LineWidth { get; }

		public CanvasPaint StrokePaint { get; }
		public CanvasPaint FillPaint { get; }

		public Color ForegroundColor { get; }
		public Color BackgroundColor { get; }
		public Color MidtoneColor { get; }
		public Color TextColor { get; }

		public LineCapStyle LineCapStyle { get; }
		public LineJoinStyle LineJoinStyle { get; }
		public float MiterLimit { get; }
		public TextRenderingMode TextRenderingMode { get; }

		public StrokeDash? StrokeDash { get; }

		public float TextSize { get; }
		public TextFormat TextFormat { get; }
		public FontSettingGrouping Fonts { get; }

		public bool FieldsEnabled { get; }
		public string FieldPrefix { get; }

		public Transform Transform { get; }
		public float UserUnits { get; }

		public SharpCanvasGraphicsSnapshot(ISharpGraphicsData graphicsData) {
			CanvasRect = graphicsData.CanvasRect;

			DefaultLineWidth = graphicsData.GetDefaultLineWidth();
			LineWidth = graphicsData.GetLineWidth();

			StrokePaint = graphicsData.GetStrokePaint();
			FillPaint = graphicsData.GetFillPaint();

			ForegroundColor = graphicsData.GetForegroundColor();
			BackgroundColor = graphicsData.GetBackgroundColor();
			MidtoneColor = graphicsData.GetMidtoneColor();
			TextColor = graphicsData.GetTextColor();

			LineCapStyle = graphicsData.GetLineCapStyle();
			LineJoinStyle = graphicsData.GetLineJoinStyle();
			MiterLimit = graphicsData.GetMiterLimit();
			TextRenderingMode = graphicsData.GetTextRenderingMode();

			StrokeDash = graphicsData.GetStrokeDash();

			TextSize = graphicsData.GetTextSize();
			TextFormat = graphicsData.GetTextFormat();
			Fonts = graphicsData.GetFonts();

			FieldsEnabled = graphicsData.IsFieldsEnabled();
			FieldPrefix = graphicsData.GetFieldPrefix();

			Transform = graphicsData.GetTransform();
			UserUnits = graphicsData.GetUserUnits();
		}

		public SharpCanvasGraphicsSnapshot(
			Rectangle canvasRect,
			float defaultLineWidth,
			float lineWidth,
			CanvasPaint strokePaint,
			CanvasPaint fillPaint,
			Color foregroundColor,
			Color backgroundColor,
			Color midtoneColor,
			Color textColor,
			LineCapStyle lineCapStyle,
			LineJoinStyle lineJoinStyle,
			float miterLimit,
			TextRenderingMode textRenderingMode,
			StrokeDash? strokeDash,
			float textSize,
			TextFormat textFormat,
			FontSettingGrouping fonts,
			bool fieldsEnabled,
			string fieldPrefix,
			Transform transform,
			float userUnits) {

			CanvasRect = canvasRect;

			DefaultLineWidth = defaultLineWidth;
			LineWidth = lineWidth;

			StrokePaint = strokePaint;
			FillPaint = fillPaint;

			ForegroundColor = foregroundColor;
			BackgroundColor = backgroundColor;
			MidtoneColor = midtoneColor;
			TextColor = textColor;

			LineCapStyle = lineCapStyle;
			LineJoinStyle = lineJoinStyle;
			MiterLimit = miterLimit;
			TextRenderingMode = textRenderingMode;

			StrokeDash = strokeDash;

			TextSize = textSize;
			TextFormat = textFormat;
			Fonts = fonts;

			FieldsEnabled = fieldsEnabled;
			FieldPrefix = fieldPrefix;

			Transform = transform;
			UserUnits = userUnits;
		}

		public float GetAscent(string text, TextFormat format, float fontsize) {
			return FontMetrics.GetAscent(text, Fonts, format, fontsize);
		}
		public float GetDescent(string text, TextFormat format, float fontsize) {
			return FontMetrics.GetDescent(text, Fonts, format, fontsize);
		}
		public float GetWidth(string text, TextFormat format, float fontsize) {
			return FontMetrics.GetWidth(text, Fonts, format, fontsize);
		}

	}

	public static class ISharpGraphicsStateExtensions {

		public static Rectangle GetPageSpaceRect(this ISharpGraphicsState graphicsState, Rectangle rect) {
			Transform currentTransform = graphicsState.GetTransform();
			DrawPoint bottomLeft = currentTransform.Map(rect.Left, rect.Bottom);
			DrawPoint topLeft = currentTransform.Map(rect.Left, rect.Top);
			DrawPoint bottomRight = currentTransform.Map(rect.Right, rect.Bottom);
			DrawPoint topRight = currentTransform.Map(rect.Right, rect.Top);

			#pragma warning disable GJT0001 // Unhandled thrown exception from statement
			float x1 = MathUtils.Min(bottomLeft.X, topLeft.X, bottomRight.X, topRight.X);
			float y1 = MathUtils.Min(bottomLeft.Y, topLeft.Y, bottomRight.Y, topRight.Y);
			float x2 = MathUtils.Max(bottomLeft.X, topLeft.X, bottomRight.X, topRight.X);
			float y2 = MathUtils.Max(bottomLeft.Y, topLeft.Y, bottomRight.Y, topRight.Y);
			#pragma warning restore GJT0001 // Unhandled thrown exception from statement

			return new Rectangle(x1, y1, x2 - x1, y2 - y1);
		}

		public static SharpCanvasGraphicsSnapshot GetSnapshot(this ISharpGraphicsData graphicsData) {
			return new SharpCanvasGraphicsSnapshot(graphicsData);
		}

		public static ISharpGraphicsState SetStrokePaint(this ISharpGraphicsState graphicsState, CanvasPaint paint) {
			if(paint is CanvasSolidPaint solidPaint) {
				graphicsState.SetStrokeColor(solidPaint.Color);
			}
			else if (paint is CanvasLinearGradientPaint lPaint) {
				graphicsState.SetStrokeLinearGradient(lPaint.X1, lPaint.Y1, lPaint.X2, lPaint.Y2, lPaint.Stops);
			}
			else if (paint is CanvasRadialGradientPaint rPaint) {
				graphicsState.SetStrokeRadialGradient(rPaint.X1, rPaint.Y1, rPaint.R1, rPaint.X2, rPaint.Y2, rPaint.R2, rPaint.Stops);
			}

			return graphicsState;
		}

		public static ISharpGraphicsState SetFillPaint(this ISharpGraphicsState graphicsState, CanvasPaint paint) {
			if (paint is CanvasSolidPaint solidPaint) {
				graphicsState.SetFillColor(solidPaint.Color);
			}
			else if (paint is CanvasLinearGradientPaint lPaint) {
				graphicsState.SetFillLinearGradient(lPaint.X1, lPaint.Y1, lPaint.X2, lPaint.Y2, lPaint.Stops);
			}
			else if (paint is CanvasRadialGradientPaint rPaint) {
				graphicsState.SetFillRadialGradient(rPaint.X1, rPaint.Y1, rPaint.R1, rPaint.X2, rPaint.Y2, rPaint.R2, rPaint.Stops);
			}

			return graphicsState;
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static ISharpGraphicsState LoadSnapshot(this ISharpGraphicsState graphicsState, SharpCanvasGraphicsSnapshot snapshot) {

			graphicsState.SetDefaultLineWidth(snapshot.DefaultLineWidth);
			graphicsState.SetLineWidth(snapshot.LineWidth);

			graphicsState.SetStrokePaint(snapshot.StrokePaint);
			graphicsState.SetFillPaint(snapshot.FillPaint);
			//graphicsState.SetFillLinearGradient(float x1, float y1, float x2, float y2, IList<ColorStop> stops);
			//graphicsState.SetFillRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IList<ColorStop> stops);

			graphicsState.SetForegroundColor(snapshot.ForegroundColor);
			graphicsState.SetBackgroundColor(snapshot.BackgroundColor);
			graphicsState.SetMidtoneColor(snapshot.MidtoneColor);
			graphicsState.SetTextColor(snapshot.TextColor);

			graphicsState.SetLineCapStyle(snapshot.LineCapStyle);
			graphicsState.SetLineJoinStyle(snapshot.LineJoinStyle);
			graphicsState.SetMiterLimit(snapshot.MiterLimit);
			graphicsState.SetTextRenderingMode(snapshot.TextRenderingMode);

			graphicsState.SetStrokeDash(snapshot.StrokeDash);

			graphicsState.SetFonts(snapshot.Fonts);
			graphicsState.SetTextFormatAndSize(snapshot.TextFormat, snapshot.TextSize);

			graphicsState.SetFieldsEnabled(snapshot.FieldsEnabled);

			graphicsState.SetFieldPrefix(snapshot.FieldPrefix);

			graphicsState.SetTransform(snapshot.Transform);

			return graphicsState;
		}

	}

	public static class ISharpCanvasExtentions {

		public static void LogError(this ISharpCanvas canvas, object origin, string message) {
			canvas.LogError(new SharpDrawingException(origin, message));
		}

		public static void LogError(this ISharpCanvas canvas, object origin, string message, Exception innerException) {
			canvas.LogError(new SharpDrawingException(origin, message, innerException));
		}

		public static void RegisterAreas(this ISharpCanvas canvas, object owner, Rectangle originalArea, Rectangle? adjustedArea, Rectangle[] innerAreas) {
			canvas.RegisterAreas(owner, originalArea, adjustedArea, innerAreas, null);
		}

	}

}
