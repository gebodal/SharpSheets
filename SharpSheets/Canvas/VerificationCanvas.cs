using SharpSheets.Canvas;
using SharpSheets.Colors;
using SharpSheets.Exceptions;
using SharpSheets.Fonts;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeboPdf.Fonts;

namespace SharpSheets.Canvas {

	public class VerificationDocument : ISharpDocument {

		public int PageCount => pages.Count;
		private readonly List<VerificationCanvas> pages;

		public VerificationDocument() {
			this.pages = new List<VerificationCanvas>();
		}

		public IReadOnlyCollection<string> FieldNames {
			get {
				return new HashSet<string>(pages.SelectMany(p => p.FieldNames));
			}
		}

		ISharpCanvas ISharpDocument.AddNewPage(Size pageSize) => AddNewPage(pageSize);

		public VerificationCanvas AddNewPage(Size pageSize) {
			VerificationCanvas canvas = new VerificationCanvas(this, pageSize);
			pages.Add(canvas);
			return canvas;
		}
	}

	public class VerificationGraphicsState {

		public float defaultLineWidth = SharpGraphicsDefaults.LineWidth;
		public float lineWidth = SharpGraphicsDefaults.LineWidth;

		public StrokeDash? strokeDash = SharpGraphicsDefaults.StrokeDash;

		public Color foreground = SharpGraphicsDefaults.Foreground;
		public Color background = SharpGraphicsDefaults.Background;
		public Color midtone = SharpGraphicsDefaults.Midtone;
		public Color textColor = SharpGraphicsDefaults.TextColor;

		public CanvasPaint strokePaint = SharpGraphicsDefaults.StrokePaint;
		public CanvasPaint fillPaint = SharpGraphicsDefaults.FillPaint;

		public LineCapStyle lineCapStyle = SharpGraphicsDefaults.LineCapStyle;
		public LineJoinStyle lineJoinStyle = SharpGraphicsDefaults.LineJoinStyle;
		public float mitreLimit = SharpGraphicsDefaults.MitreLimit;

		public VerificationFontPathGrouping fonts = new VerificationFontPathGrouping();
		public TextFormat textFormat = SharpGraphicsDefaults.TextFormat;
		public float fontsize = SharpGraphicsDefaults.Fontsize;
		public TextRenderingMode textRenderingMode = SharpGraphicsDefaults.TextRenderingMode;

		public Transform transform = SharpGraphicsDefaults.Transform;

		public bool fieldsEnabled = SharpGraphicsDefaults.FieldsEnabled;
		public string fieldPrefix = SharpGraphicsDefaults.FieldPrefix;

		public VerificationGraphicsState() { }

		public VerificationGraphicsState(VerificationGraphicsState source) {
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
			this.fonts = new VerificationFontPathGrouping(source.fonts);
			this.textFormat = source.textFormat;
			this.fontsize = source.fontsize;
			this.textRenderingMode = source.textRenderingMode;
			this.transform = source.transform;
			this.fieldsEnabled = source.fieldsEnabled;
			this.fieldPrefix = source.fieldPrefix;
		}
	}

	public class VerificationFontPathGrouping {

		private (FontPath? path, PdfFont font) regular;
		private (FontPath? path, PdfFont font)? bold;
		private (FontPath? path, PdfFont font)? italic;
		private (FontPath? path, PdfFont font)? bolditalic;

		public PdfFont GetPdfFont(TextFormat format) {
			if (format == TextFormat.REGULAR) {
				return regular.font;
			}
			else if (format == TextFormat.BOLD) {
				return bold?.font ?? regular.font;
			}
			else if (format == TextFormat.ITALIC) {
				return italic?.font ?? regular.font;
			}
			else { // format == TextFormat.BOLDITALIC
				return bolditalic?.font ?? regular.font;
			}
		}

		private void SetFont(TextFormat format, FontPath? origin, PdfFont font) {
			(FontPath?, PdfFont) info = (origin, font);
			if (format == TextFormat.REGULAR) {
				regular = info;
			}
			else if (format == TextFormat.BOLD) {
				bold = info;
			}
			else if (format == TextFormat.ITALIC) {
				italic = info;
			}
			else { // format == TextFormat.BOLDITALIC
				bolditalic = info;
			}
		}

		public void SetFont(TextFormat format, FontPath origin) {
			PdfFont pdfFont = FontGraphicsRegistry.GetPdfFont(origin);
			SetFont(format, origin, pdfFont);
		}

		public void SetNull(TextFormat format) {
			if (format == TextFormat.REGULAR) {
				//regular = null; // Throw error?
				SetFont(TextFormat.REGULAR, null, FontGraphicsRegistry.GetRegularDefault()); // Better?
			}
			else if (format == TextFormat.BOLD) {
				bold = null;
			}
			else if (format == TextFormat.ITALIC) {
				italic = null;
			}
			else { // format == TextFormat.BOLDITALIC
				bolditalic = null;
			}
		}

		public FontPathGrouping GetFonts() {
			return new FontPathGrouping(regular.path, bold?.path, italic?.path, bolditalic?.path);
		}

		public VerificationFontPathGrouping() {
			SetFont(TextFormat.REGULAR, null, FontGraphicsRegistry.GetRegularDefault());
			SetFont(TextFormat.BOLD, null, FontGraphicsRegistry.GetBoldDefault());
			SetFont(TextFormat.ITALIC, null, FontGraphicsRegistry.GetItalicDefault());
			SetFont(TextFormat.BOLDITALIC, null, FontGraphicsRegistry.GetBoldItalicDefault());
		}

		public VerificationFontPathGrouping(VerificationFontPathGrouping source) {
			this.regular = source.regular;
			this.bold = source.bold;
			this.italic = source.italic;
			this.bolditalic = source.bolditalic;
		}
	}

	public class VerificationCanvas : ISharpCanvas {

		private readonly VerificationDocument document;

		private readonly List<SharpDrawingException> drawingErrors = new List<SharpDrawingException>();
		private readonly HashSet<string> fields = new HashSet<string>();

		private VerificationGraphicsState state;
		private readonly Stack<VerificationGraphicsState> stateStack = new Stack<VerificationGraphicsState>();

		public VerificationCanvas(VerificationDocument document, Size pageSize) {
			this.document = document;

			this.CanvasRect = new Rectangle(pageSize.Width, pageSize.Height);

			this.state = new VerificationGraphicsState();
		}

		public Rectangle CanvasRect { get; }

		public void RegisterAreas(object owner, Rectangle original, Rectangle? adjusted, Rectangle[] inner) { } // Unused for this canvas

		public void LogError(SharpDrawingException error) => drawingErrors.Add(error);
		public IEnumerable<SharpDrawingException> GetDrawingErrors() => drawingErrors;

		public IReadOnlyCollection<string> FieldNames => fields;
		public IReadOnlyCollection<string> DocumentFieldNames => document.FieldNames;

		public ISharpGraphicsState SaveState() {
			stateStack.Push(state);
			state = new VerificationGraphicsState(state);
			return this;
		}

		public ISharpGraphicsState RestoreState() {
			state = stateStack.Pop();
			return this;
		}

		public int GetStateDepth() { return stateStack.Count; }

		public Transform GetTransform() => state.transform;
		public ISharpGraphicsState ApplyTransform(Transform transform) {
			state.transform *= transform;
			return this;
		}
		public ISharpGraphicsState SetTransform(Transform transform) {
			return ApplyTransform(GetTransform().Invert() * transform);
		}


		public float GetDefaultLineWidth() => state.defaultLineWidth;
		public ISharpGraphicsState SetDefaultLineWidth(float linewidth) {
			state.defaultLineWidth = linewidth;
			SetLineWidth(linewidth);
			return this;
		}

		public float GetLineWidth() => state.lineWidth;
		public ISharpGraphicsState SetLineWidth(float linewidth) {
			state.lineWidth = linewidth;
			return this;
		}

		public float GetMiterLimit() => state.mitreLimit;
		public ISharpGraphicsState SetMiterLimit(float mitreLimit) {
			state.mitreLimit = mitreLimit;
			return this;
		}

		public LineCapStyle GetLineCapStyle() => state.lineCapStyle;
		public ISharpGraphicsState SetLineCapStyle(LineCapStyle capStyle) {
			state.lineCapStyle = capStyle;
			return this;
		}

		public LineJoinStyle GetLineJoinStyle() => state.lineJoinStyle;
		public ISharpGraphicsState SetLineJoinStyle(LineJoinStyle joinStyle) {
			state.lineJoinStyle = joinStyle;
			return this;
		}

		public StrokeDash? GetStrokeDash() => state.strokeDash;
		public ISharpGraphicsState SetStrokeDash(StrokeDash? strokeDash) {
			state.strokeDash = strokeDash;
			return this;
		}

		public CanvasPaint GetStrokePaint() => state.strokePaint;
		public CanvasPaint GetFillPaint() => state.fillPaint;

		public ISharpGraphicsState SetStrokeColor(Color color) {
			state.strokePaint = new CanvasSolidPaint(color);
			return this;
		}
		public ISharpGraphicsState SetFillColor(Color color) {
			state.fillPaint = new CanvasSolidPaint(color);
			return this;
		}

		public ISharpGraphicsState SetStrokeLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			state.strokePaint = new CanvasLinearGradientPaint(x1, y1, x2, y2, stops);
			return this;
		}
		public ISharpGraphicsState SetFillLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			state.fillPaint = new CanvasLinearGradientPaint(x1, y1, x2, y2, stops);
			return this;
		}

		public ISharpGraphicsState SetStrokeRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			state.strokePaint = new CanvasRadialGradientPaint(x1, y1, r1, x2, y2, r2, stops);
			return this;
		}
		public ISharpGraphicsState SetFillRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			state.fillPaint = new CanvasRadialGradientPaint(x1, y1, r1, x2, y2, r2, stops);
			return this;
		}

		public Color GetForegroundColor() => state.foreground;
		public ISharpGraphicsState SetForegroundColor(Color color) {
			state.foreground = color;
			return this;
		}
		public Color GetBackgroundColor() => state.background;
		public ISharpGraphicsState SetBackgroundColor(Color color) {
			state.background = color;
			return this;
		}
		public Color GetMidtoneColor() => state.midtone;
		public ISharpGraphicsState SetMidtoneColor(Color color) {
			state.midtone = color;
			return this;
		}
		public Color GetTextColor() => state.textColor;
		public ISharpGraphicsState SetTextColor(Color color) {
			state.textColor = color;
			return this;
		}


		public DrawPoint? CurrentPenLocation { get; private set; } = null;

		public ISharpCanvas MoveTo(float x, float y) {
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas LineTo(float x, float y) {
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas CurveTo(float x2, float y2, float x3, float y3) {
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas CurveTo(float x1, float y1, float x2, float y2, float x3, float y3) {
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas ClosePath() {
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EndPath() {
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Ellipse(float x1, float y1, float x2, float y2) {
			return this.BezierEllipse(x1, y1, x2, y2);
		}

		public ISharpCanvas Rectangle(float x, float y, float width, float height) {
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Stroke() {
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Fill() {
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EoFill() {
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas FillStroke() {
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EoFillStroke() {
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Clip() {
			// This does not actually close the path, so CurrentPenLocation is not set to null
			return this;
		}
		public ISharpCanvas EoClip() {
			// This does not actually close the path, so CurrentPenLocation is not set to null
			return this;
		}


		public FontPathGrouping GetFonts() => state.fonts.GetFonts();
		public float GetTextSize() => state.fontsize;
		public TextFormat GetTextFormat() => state.textFormat;

		public ISharpGraphicsState SetTextFormatAndSize(TextFormat font, float size) {
			state.textFormat = font;
			state.fontsize = size;
			return this;
		}

		public ISharpGraphicsState SetTextSize(float size) {
			state.fontsize = size;
			return this;
		}

		public ISharpGraphicsState SetFont(TextFormat format, FontPath? font) {
			if (font?.Path != null) {
				state.fonts.SetFont(format, font);
			}
			else {
				state.fonts.SetNull(format);
			}

			return this;
		}

		public TextRenderingMode GetTextRenderingMode() => state.textRenderingMode;
		public ISharpGraphicsState SetTextRenderingMode(TextRenderingMode mode) {
			state.textRenderingMode = mode;
			return this;
		}

		public float GetAscent(string text, TextFormat format, float fontsize) => state.fonts.GetPdfFont(format).GetAscent(text, fontsize);
		public float GetDescent(string text, TextFormat format, float fontsize) => state.fonts.GetPdfFont(format).GetDescent(text, fontsize);
		public float GetWidth(string text, TextFormat format, float fontsize) => state.fonts.GetPdfFont(format).GetWidth(text, fontsize);

		public ISharpCanvas DrawText(string text, float x, float y) {
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas AddImage(CanvasImageData image, Rectangle rect, float? imageAspect) {
			return this; // Anything here?
		}

		public string GetFieldPrefix() => state.fieldPrefix;
		public ISharpGraphicsState SetFieldPrefix(string prefix) {
			state.fieldPrefix = prefix;
			return this;
		}

		public bool IsFieldsEnabled() => state.fieldsEnabled;
		public ISharpGraphicsState SetFieldsEnabled(bool enabled) {
			state.fieldsEnabled = enabled;
			return this;
		}

		public string? TextField(Rectangle rect, string name, string? tooltip, TextFieldType fieldType, string? value, TextFormat format, float fontSize, Color color, bool multiline, bool rich, Justification justification, int maxLen = -1) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);

				fields.Add(fieldName);
				return fieldName;
			}
			else {
				return null;
			}
		}

		public string? CheckField(Rectangle rect, string name, string? tooltip, CheckType checkType, Color color) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);

				fields.Add(fieldName);
				return fieldName;
			}
			else {
				return null;
			}
		}

		public string? ImageField(Rectangle rect, string name, string? tooltip, CanvasImageData? defaultImage = null) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);

				fields.Add(fieldName);
				return fieldName;
			}
			else {
				return null;
			}
		}


		public float GetUserUnits() => 1f; // TODO This needs removing.

	}

}
