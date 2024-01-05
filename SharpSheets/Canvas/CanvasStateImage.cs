using SharpSheets.Fonts;
using SharpSheets.Canvas.Text;
using System.Collections.Generic;
using SharpSheets.Colors;

namespace SharpSheets.Canvas {

	public class CanvasStateImage : ISharpGraphicsState {

		private class CanvasStateImageGraphicsState {
			public float defaultLineWidth;
			public float lineWidth;

			public StrokeDash? strokeDash;

			public Color foreground;
			public Color background;
			public Color midtone;
			public Color? textColor;

			public CanvasPaint strokePaint;
			public CanvasPaint fillPaint;

			public LineJoinStyle lineJoinStyle;
			public LineCapStyle lineCapStyle;
			public float mitreLimit;

			public FontPathGrouping fonts;
			public TextFormat textFormat;
			public float fontsize;
			public TextRenderingMode textRenderingMode;

			public Transform transform;

			public bool fieldsEnabled;
			public string currentPrefix;

			public CanvasStateImageGraphicsState(ISharpGraphicsData graphicsData) {
				this.defaultLineWidth = graphicsData.GetDefaultLineWidth();
				this.lineWidth = graphicsData.GetLineWidth();
				this.strokeDash = graphicsData.GetStrokeDash();
				this.foreground = graphicsData.GetForegroundColor();
				this.background = graphicsData.GetBackgroundColor();
				this.midtone = graphicsData.GetMidtoneColor();
				this.textColor = graphicsData.GetTextColor();
				this.strokePaint = graphicsData.GetStrokePaint();
				this.fillPaint = graphicsData.GetFillPaint();
				this.lineJoinStyle = graphicsData.GetLineJoinStyle();
				this.mitreLimit = graphicsData.GetMiterLimit();
				this.fonts = new FontPathGrouping(graphicsData.GetFonts());
				this.textFormat = graphicsData.GetTextFormat();
				this.fontsize = graphicsData.GetTextSize();
				this.textRenderingMode = graphicsData.GetTextRenderingMode();
				this.transform = graphicsData.GetTransform();
				this.fieldsEnabled = graphicsData.IsFieldsEnabled();
				this.currentPrefix = graphicsData.GetFieldPrefix();
			}

			public CanvasStateImageGraphicsState(CanvasStateImageGraphicsState source) {
				this.defaultLineWidth = source.defaultLineWidth;
				this.lineWidth = source.lineWidth;
				this.strokeDash = source.strokeDash;
				this.foreground = source.foreground;
				this.background = source.background;
				this.midtone = source.midtone;
				this.textColor = source.textColor;
				this.strokePaint = source.strokePaint;
				this.fillPaint = source.fillPaint;
				this.lineJoinStyle = source.lineJoinStyle;
				this.mitreLimit = source.mitreLimit;
				this.fonts = new FontPathGrouping(source.fonts);
				this.textFormat = source.textFormat;
				this.fontsize = source.fontsize;
				this.textRenderingMode = source.textRenderingMode;
				this.transform = source.transform;
				this.fieldsEnabled = source.fieldsEnabled;
				this.currentPrefix = source.currentPrefix;
			}
		}

		public Layouts.Rectangle CanvasRect { get; }
		private readonly float userUnits;

		private readonly Stack<CanvasStateImageGraphicsState> stateStack;
		private CanvasStateImageGraphicsState gState;

		public CanvasStateImage(ISharpGraphicsData graphicsData) {
			this.stateStack = new Stack<CanvasStateImageGraphicsState>();
			this.gState = new CanvasStateImageGraphicsState(graphicsData);

			this.CanvasRect = graphicsData.CanvasRect;
			this.userUnits = graphicsData.GetUserUnits();
		}

		#region States
		public ISharpGraphicsState SaveState() {
			stateStack.Push(gState);
			gState = new CanvasStateImageGraphicsState(gState);
			return this;
		}
		public ISharpGraphicsState RestoreState() {
			gState = stateStack.Pop();
			return this;
		}
		public int GetStateDepth() { return stateStack.Count; }
		#endregion

		#region Transform
		public ISharpGraphicsState ApplyTransform(Transform transform) {
			gState.transform *= transform;
			return this;
		}
		public Transform GetTransform() {
			return gState.transform;
		}
		public ISharpGraphicsState SetTransform(Transform transform) {
			gState.transform = transform;
			return this;
		}
		#endregion

		#region Base Geometry
		public float GetDefaultLineWidth() {
			return gState.defaultLineWidth;
		}
		public ISharpGraphicsState SetDefaultLineWidth(float linewidth) {
			gState.defaultLineWidth = linewidth;
			gState.lineWidth = linewidth;
			return this;
		}
		public float GetLineWidth() {
			return gState.lineWidth;
		}
		public ISharpGraphicsState SetLineWidth(float linewidth) {
			gState.lineWidth = linewidth;
			return this;
		}

		public StrokeDash? GetStrokeDash() {
			return gState.strokeDash;
		}
		public ISharpGraphicsState SetStrokeDash(StrokeDash? strokeDash) {
			gState.strokeDash = strokeDash;
			return this;
		}

		public LineJoinStyle GetLineJoinStyle() {
			return gState.lineJoinStyle;
		}
		public ISharpGraphicsState SetLineJoinStyle(LineJoinStyle joinStyle) {
			gState.lineJoinStyle = joinStyle;
			return this;
		}
		public LineCapStyle GetLineCapStyle() {
			return gState.lineCapStyle;
		}
		public ISharpGraphicsState SetLineCapStyle(LineCapStyle capStyle) {
			gState.lineCapStyle = capStyle;
			return this;
		}
		public float GetMiterLimit() {
			return gState.mitreLimit;
		}
		public ISharpGraphicsState SetMiterLimit(float mitreLimit) {
			gState.mitreLimit = mitreLimit;
			return this;
		}
		public TextRenderingMode GetTextRenderingMode() {
			return gState.textRenderingMode;
		}
		public ISharpGraphicsState SetTextRenderingMode(TextRenderingMode mode) {
			gState.textRenderingMode = mode;
			return this;
		}
		#endregion

		#region Colors
		public Color GetForegroundColor() {
			return gState.foreground;
		}
		public Color GetBackgroundColor() {
			return gState.background;
		}
		public Color GetMidtoneColor() {
			return gState.midtone;
		}
		public Color GetTextColor() {
			return gState.textColor ?? gState.foreground;
		}

		public ISharpGraphicsState SetForegroundColor(Color color) {
			gState.foreground = color;
			return this;
		}
		public ISharpGraphicsState SetBackgroundColor(Color color) {
			gState.background = color;
			return this;
		}
		public ISharpGraphicsState SetMidtoneColor(Color color) {
			gState.midtone = color;
			return this;
		}
		public ISharpGraphicsState SetTextColor(Color color) {
			gState.textColor = color;
			return this;
		}

		public CanvasPaint GetStrokePaint() {
			return gState.strokePaint;
		}

		public ISharpGraphicsState SetStrokeColor(Color color) {
			gState.strokePaint = new CanvasSolidPaint(color);
			return this;
		}

		public ISharpGraphicsState SetStrokeLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			gState.strokePaint = new CanvasLinearGradientPaint(x1, y1, x2, y2, stops);
			return this;
		}

		public ISharpGraphicsState SetStrokeRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			gState.strokePaint = new CanvasRadialGradientPaint(x1, y1, r1, x2, y2, r2, stops);
			return this;
		}

		public CanvasPaint GetFillPaint() {
			return gState.fillPaint;
		}

		public ISharpGraphicsState SetFillColor(Color color) {
			gState.fillPaint = new CanvasSolidPaint(color);
			return this;
		}

		public ISharpGraphicsState SetFillLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			gState.fillPaint = new CanvasLinearGradientPaint(x1, y1, x2, y2, stops);
			return this;
		}

		public ISharpGraphicsState SetFillRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			gState.fillPaint = new CanvasRadialGradientPaint(x1, y1, r1, x2, y2, r2, stops);
			return this;
		}
		#endregion

		#region Text
		public float GetTextSize() {
			return gState.fontsize;
		}
		public ISharpGraphicsState SetTextSize(float size) {
			gState.fontsize = size;
			return this;
		}
		public TextFormat GetTextFormat() {
			return gState.textFormat;
		}
		public ISharpGraphicsState SetTextFormatAndSize(TextFormat font, float size) {
			gState.textFormat = font;
			gState.fontsize = size;
			return this;
		}
		public FontPathGrouping GetFonts() {
			return gState.fonts;
		}
		public ISharpGraphicsState SetFont(TextFormat format, FontPath? font) {
			FontPath? regular = format == TextFormat.REGULAR ? font : gState.fonts.Regular;
			FontPath? bold = format == TextFormat.BOLD ? font : gState.fonts.Bold;
			FontPath? italic = format == TextFormat.ITALIC ? font : gState.fonts.Italic;
			FontPath? boldItalic = format == TextFormat.BOLDITALIC ? font : gState.fonts.BoldItalic;
			gState.fonts = new FontPathGrouping(regular, bold, italic, boldItalic);
			return this;
		}
		public float GetWidth(string text, TextFormat format, float fontsize) {
			return FontMetrics.GetWidth(text, gState.fonts, format, fontsize);
		}
		public float GetAscent(string text, TextFormat format, float fontsize) {
			return FontMetrics.GetAscent(text, gState.fonts, format, fontsize);
		}
		public float GetDescent(string text, TextFormat format, float fontsize) {
			return FontMetrics.GetDescent(text, gState.fonts, format, fontsize);
		}
		#endregion

		#region Fields
		public bool IsFieldsEnabled() {
			return gState.fieldsEnabled;
		}
		public ISharpGraphicsState SetFieldsEnabled(bool enabled) {
			gState.fieldsEnabled = enabled;
			return this;
		}
		public string GetFieldPrefix() {
			return gState.currentPrefix;
		}
		public ISharpGraphicsState SetFieldPrefix(string prefix) {
			gState.currentPrefix = prefix;
			return this;
		}
		#endregion

		#region Misc
		public float GetUserUnits() {
			return userUnits;
		}
		#endregion
	}

}
