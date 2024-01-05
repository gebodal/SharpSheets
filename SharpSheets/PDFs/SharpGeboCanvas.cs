using GeboPdf.Documents;
using GeboPdf.Fonts;
using GeboPdf.Graphics;
using GeboPdf.IO;
using GeboPdf.Objects;
using GeboPdf.XObjects;
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
using GeboPdf.Patterns;

namespace SharpSheets.PDFs {

	public class SharpGeboDocument : ISharpDocument {

		private readonly PdfDocument pdf;

		public int PageCount => pdf.PageCount;
		private readonly List<SharpGeboCanvas> pages;

		public readonly IReadOnlyDictionary<string, PdfObject> FieldValues;

		public SharpGeboDocument(PdfDocument pdf, Dictionary<string, PdfObject>? fieldValues) {
			this.pdf = pdf;
			this.pdf.Creator = SharpSheetsData.GetCreatorString();

			this.pages = new List<SharpGeboCanvas>();

			this.FieldValues = fieldValues != null ? fieldValues : new Dictionary<string, PdfObject>();
		}

		public SharpGeboDocument(PdfDocument pdf) : this(pdf, null) { }

		public IReadOnlyCollection<string> FieldNames {
			get {
				return new HashSet<string>(pages.SelectMany(p => p.FieldNames));
			}
		}

		ISharpCanvas ISharpDocument.AddNewPage(Size pageSize) => AddNewPage(pageSize);

		public SharpGeboCanvas AddNewPage(Size pageSize) {
			PdfPage pdfPage = pdf.AddPage(pageSize.Width, pageSize.Height);
			SharpGeboCanvas canvas = new SharpGeboCanvas(this, pdf, pdfPage);
			pages.Add(canvas);
			return canvas;
		}
	}

	public class SharpGeboGraphicsState {

		public float defaultLineWidth = SharpGraphicsDefaults.LineWidth;
		public float lineWidth = SharpGraphicsDefaults.LineWidth;

		public StrokeDash? strokeDash = SharpGraphicsDefaults.StrokeDash;

		public Color foreground = SharpGraphicsDefaults.Foreground;
		public Color background = SharpGraphicsDefaults.Background;
		public Color midtone = SharpGraphicsDefaults.Midtone;
		public Color textColor = SharpGraphicsDefaults.TextColor;

		public CanvasPaint strokePaint = SharpGraphicsDefaults.StrokePaint;
		public CanvasPaint fillPaint = SharpGraphicsDefaults.FillPaint;

		public Canvas.LineCapStyle lineCapStyle = SharpGraphicsDefaults.LineCapStyle;
		public Canvas.LineJoinStyle lineJoinStyle = SharpGraphicsDefaults.LineJoinStyle;
		public float mitreLimit = SharpGraphicsDefaults.MitreLimit;

		public GeboFontPathGrouping fonts = new GeboFontPathGrouping();
		public TextFormat textFormat = SharpGraphicsDefaults.TextFormat;
		public float fontsize = SharpGraphicsDefaults.Fontsize;
		public Canvas.TextRenderingMode textRenderingMode = SharpGraphicsDefaults.TextRenderingMode;

		public Transform transform = SharpGraphicsDefaults.Transform;

		public bool fieldsEnabled = SharpGraphicsDefaults.FieldsEnabled;
		public string fieldPrefix = SharpGraphicsDefaults.FieldPrefix;

		public SharpGeboGraphicsState() { }

		public SharpGeboGraphicsState(SharpGeboGraphicsState source) {
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
			this.fonts = new GeboFontPathGrouping(source.fonts);
			this.textFormat = source.textFormat;
			this.fontsize = source.fontsize;
			this.textRenderingMode = source.textRenderingMode;
			this.transform = source.transform;
			this.fieldsEnabled = source.fieldsEnabled;
			this.fieldPrefix = source.fieldPrefix;
		}
	}

	public static class SharpGeboFonts {

		private static readonly Dictionary<FontPath, PdfFont> registry = new Dictionary<FontPath, PdfFont>();
		private static readonly Dictionary<PdfStandardFonts, PdfFont> standardRegistry = new Dictionary<PdfStandardFonts, PdfFont>();

		public static PdfFont GetFont(FontPath path) {
			if (registry.TryGetValue(path, out PdfFont? existing)) {
				return existing;
			}

			PdfFont font = CIDFontFactory.CreateFont(path.Path);
			registry.Add(path, font);
			return font;
		}

		public static PdfFont GetFont(PdfStandardFonts standardFont) {
			if (standardRegistry.TryGetValue(standardFont, out PdfFont? existing)) {
				return existing;
			}

			PdfFont font = new PdfStandardFont(standardFont, new GeboPdf.Objects.PdfName("WinAnsiEncoding")); // TODO Encoding???
			standardRegistry.Add(standardFont, font);
			return font;
		}

	}

	public class GeboFontPathGrouping {

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
			PdfFont pdfFont = SharpGeboFonts.GetFont(origin);
			SetFont(format, origin, pdfFont);
		}

		public void SetFont(TextFormat format, PdfStandardFonts standardFont) {
			PdfFont pdfFont = SharpGeboFonts.GetFont(standardFont);
			SetFont(format, null, pdfFont);
		}

		public void SetNull(TextFormat format) {
			if (format == TextFormat.REGULAR) {
				//regular = null; // Throw error?
				SetFont(TextFormat.REGULAR, PdfStandardFonts.Helvetica); // Better?
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

		public GeboFontPathGrouping() {
			SetFont(TextFormat.REGULAR, PdfStandardFonts.Helvetica);
			SetFont(TextFormat.BOLD, PdfStandardFonts.HelveticaBold);
			SetFont(TextFormat.ITALIC, PdfStandardFonts.HelveticaOblique);
			SetFont(TextFormat.BOLDITALIC, PdfStandardFonts.HelveticaBoldOblique);
		}

		public GeboFontPathGrouping(GeboFontPathGrouping source) {
			this.regular = source.regular;
			this.bold = source.bold;
			this.italic = source.italic;
			this.bolditalic = source.bolditalic;
		}
	}

	public class SharpGeboCanvas : ISharpCanvas {

		private readonly SharpGeboDocument document;
		private readonly PdfDocument pdf;
		private readonly PdfPage pdfPage;
		private readonly GraphicsStream canvas;

		private readonly List<SharpDrawingException> drawingErrors = new List<SharpDrawingException>();
		private readonly HashSet<string> fields = new HashSet<string>();

		private SharpGeboGraphicsState state;
		private readonly Stack<SharpGeboGraphicsState> stateStack = new Stack<SharpGeboGraphicsState>();

		public SharpGeboCanvas(SharpGeboDocument document, PdfDocument pdf, PdfPage pdfPage) {
			this.document = document;
			this.pdf = pdf;
			this.pdfPage = pdfPage;
			this.canvas = pdfPage.contents;

			this.CanvasRect = new Rectangle(this.pdfPage.width, this.pdfPage.height);

			this.state = new SharpGeboGraphicsState();

			// Need to set non-default canvas values to the SharpSheets defaults
			canvas.SetNonStrokingGray(1f); // Set canvas fill to White (default is Black)
		}

		public Rectangle CanvasRect { get; }

		public void RegisterAreas(object owner, Rectangle original, Rectangle? adjusted, Rectangle[] inner) { } // Unused for this canvas

		public void LogError(SharpDrawingException error) => drawingErrors.Add(error);
		public IEnumerable<SharpDrawingException> GetDrawingErrors() => drawingErrors;

		public IReadOnlyCollection<string> FieldNames => fields;
		public IReadOnlyCollection<string> DocumentFieldNames => document.FieldNames;

		#region Converters

		private static GeboPdf.Objects.PdfRectangle ConvertRectangle(Rectangle rectangle) {
			return GeboPdf.Objects.PdfRectangle.FromDimensions(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
		}

		private static GeboPdf.Graphics.PdfDeviceColor ConvertColor(Color color, out float alpha) {
			alpha = color.A / 255f;
			if (color.R == color.G && color.G == color.B) {
				return new PdfGrayColor(color.R / 255f);
			}
			else {
				return new PdfRGBColor(color.R / 255f, color.G / 255f, color.B / 255f);
			}
		}

		private static GeboPdf.Objects.PdfMatrix ConvertTransform(Transform t) {
			return new PdfMatrix(t.a, t.b, t.c, t.d, t.e, t.f);
		}

		private static PdfVariableTextQuadding ConvertJustification(Justification justification) {
			if (justification == Justification.LEFT) {
				return PdfVariableTextQuadding.LeftJustified;
			}
			else if (justification == Justification.CENTRE) {
				return PdfVariableTextQuadding.Centered;
			}
			else {
				return PdfVariableTextQuadding.RightJustified;
			}
		}

		private static GeboPdf.Graphics.LineCapStyle ConvertCapStyle(Canvas.LineCapStyle style) {
			if (style == Canvas.LineCapStyle.BUTT) {
				return GeboPdf.Graphics.LineCapStyle.Butt;
			}
			else if (style == Canvas.LineCapStyle.ROUND) {
				return GeboPdf.Graphics.LineCapStyle.Round;
			}
			else {
				return GeboPdf.Graphics.LineCapStyle.Square;
			}
		}

		private static GeboPdf.Graphics.LineJoinStyle ConvertJoinStyle(Canvas.LineJoinStyle style) {
			if (style == Canvas.LineJoinStyle.MITER) {
				return GeboPdf.Graphics.LineJoinStyle.Mitre;
			}
			else if (style == Canvas.LineJoinStyle.ROUND) {
				return GeboPdf.Graphics.LineJoinStyle.Round;
			}
			else {
				return GeboPdf.Graphics.LineJoinStyle.Bevel;
			}
		}

		private static GeboPdf.Graphics.TextRenderingMode ConvertTextRenderingMode(Canvas.TextRenderingMode mode) {
			if (mode == Canvas.TextRenderingMode.FILL) {
				return GeboPdf.Graphics.TextRenderingMode.FillText;
			}
			else if (mode == Canvas.TextRenderingMode.STROKE) {
				return GeboPdf.Graphics.TextRenderingMode.StrokeText;
			}
			else if (mode == Canvas.TextRenderingMode.FILL_STROKE) {
				return GeboPdf.Graphics.TextRenderingMode.FillThenStrokeText;
			}
			else if (mode == Canvas.TextRenderingMode.INVISIBLE) {
				return GeboPdf.Graphics.TextRenderingMode.NeitherFillNorStrokeText;
			}
			else if (mode == Canvas.TextRenderingMode.FILL_CLIP) {
				return GeboPdf.Graphics.TextRenderingMode.FillTextAndAddToPathForClipping;
			}
			else if (mode == Canvas.TextRenderingMode.STROKE_CLIP) {
				return GeboPdf.Graphics.TextRenderingMode.StrokeTextAndAddToPathForClipping;
			}
			else if (mode == Canvas.TextRenderingMode.FILL_STROKE_CLIP) {
				return GeboPdf.Graphics.TextRenderingMode.FillThenStrokeTextAndAddToPathForClipping;
			}
			else {
				return GeboPdf.Graphics.TextRenderingMode.AddTextToPathForClipping;
			}
		}

		private static char ConvertCheckType(CheckType checktype) {
			if (checktype == CheckType.CHECK) {
				return '4';
			}
			else if (checktype == CheckType.CIRCLE) {
				return 'l';
			}
			else if (checktype == CheckType.CROSS) {
				return '8';
			}
			else if (checktype == CheckType.DIAMOND) {
				return 'u';
			}
			else if (checktype == CheckType.SQUARE) {
				return 'n';
			}
			else {
				return 'H';
			}
		}

		#endregion

		public ISharpGraphicsState SaveState() {
			stateStack.Push(state);
			state = new SharpGeboGraphicsState(state);
			canvas.SaveState();
			return this;
		}

		public ISharpGraphicsState RestoreState() {
			state = stateStack.Pop();
			canvas.RestoreState();
			return this;
		}

		public int GetStateDepth() { return stateStack.Count; }

		public Transform GetTransform() => state.transform;
		public ISharpGraphicsState ApplyTransform(Transform transform) {
			state.transform *= transform;
			canvas.ConcatenateMatrix(transform.a, transform.b, transform.c, transform.d, transform.e, transform.f);
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
			canvas.LineWidth(linewidth);
			return this;
		}

		public float GetMiterLimit() => state.mitreLimit;
		public ISharpGraphicsState SetMiterLimit(float mitreLimit) {
			state.mitreLimit = mitreLimit;
			canvas.MitreLimit(mitreLimit);
			return this;
		}

		public Canvas.LineCapStyle GetLineCapStyle() => state.lineCapStyle;
		public ISharpGraphicsState SetLineCapStyle(Canvas.LineCapStyle capStyle) {
			state.lineCapStyle = capStyle;
			canvas.LineCapStyle(ConvertCapStyle(capStyle));
			return this;
		}

		public Canvas.LineJoinStyle GetLineJoinStyle() => state.lineJoinStyle;
		public ISharpGraphicsState SetLineJoinStyle(Canvas.LineJoinStyle joinStyle) {
			state.lineJoinStyle = joinStyle;
			canvas.LineJoinStyle(ConvertJoinStyle(joinStyle));
			return this;
		}

		public StrokeDash? GetStrokeDash() => state.strokeDash;
		public ISharpGraphicsState SetStrokeDash(StrokeDash? strokeDash) {
			state.strokeDash = strokeDash;
			canvas.LineDashPattern(strokeDash?.Values, strokeDash?.Offset ?? 0f);
			return this;
		}

		public CanvasPaint GetStrokePaint() => state.strokePaint;
		public CanvasPaint GetFillPaint() => state.fillPaint;

		public ISharpGraphicsState SetStrokeColor(Color color) {
			state.strokePaint = new CanvasSolidPaint(color);
			canvas.SetStrokingColor(ConvertColor(color, out float alpha));
			canvas.SetStrokingAlphaConstant(alpha);
			return this;
		}

		public ISharpGraphicsState SetFillColor(Color color) {
			state.fillPaint = new CanvasSolidPaint(color);
			canvas.SetNonStrokingColor(ConvertColor(color, out float alpha));
			canvas.SetNonStrokingAlphaConstant(alpha);
			return this;
		}

		private static readonly float GradientInterpolationExponent = 1f;

		private static float[] GetColorValues(Color color) {
			return new float[] { color.R / (float)255, color.G / (float)255, color.B / (float)255 };
		}

		private static PdfFunction MakeGradientInterpolationFunction(IReadOnlyList<ColorStop> stops) {

			stops = stops.Select(s => new ColorStop(s.Stop, s.Color.WithOpacity(1.0f))).OrderBy(s => s.Stop).ToList();

			if (stops.Count == 0) {
				// Return a simple interpolation between black and white
				return PdfExponentialInterpolationFunction.MakeFunction(
					(0f, 1f), new (float, float)[] { (0, 1), (0, 1), (0, 1) },
					new float[] { 0f, 0f, 0f }, new float[] { 1f, 1f, 1f }, GradientInterpolationExponent);
			}
			else if(stops.Count == 1) {
				// Return a fake interpolation between the same two values
				float[] colorValues = GetColorValues(stops[0].Color);
				return PdfExponentialInterpolationFunction.MakeFunction(
					(0f, 1f), new (float, float)[] { (0, 1), (0, 1), (0, 1) },
					colorValues, colorValues, GradientInterpolationExponent);
			}
			else if(stops.Count == 2) {
				float[] colorValues1 = GetColorValues(stops[0].Color);
				float[] colorValues2 = GetColorValues(stops[1].Color);
				(float, float) domain = (stops[0].Stop, stops[1].Stop);
				return PdfExponentialInterpolationFunction.MakeFunction(
					domain, new (float, float)[] { (0, 1), (0, 1), (0, 1) },
					colorValues1, colorValues2, GradientInterpolationExponent);
			}
			else {
				(float,float) domain = (stops[0].Stop, stops[^1].Stop);
				PdfFunction[] functions = new PdfFunction[stops.Count - 1];
				float[] bounds = new float[functions.Length - 1];
				float[] encode = new float[functions.Length * 2];

				for (int i = 0; i < functions.Length; i++) {

					float[] colorValues1 = GetColorValues(stops[i].Color);
					float[] colorValues2 = GetColorValues(stops[i + 1].Color);

					functions[i] = PdfExponentialInterpolationFunction.MakeFunction(
						(0f, 1f), new (float, float)[] { (0, 1), (0, 1), (0, 1) },
						colorValues1, colorValues2, GradientInterpolationExponent);

					if (i < bounds.Length) {
						bounds[i] = stops[i + 1].Stop;
					}

					encode[i * 2] = 0f;
					encode[i * 2 + 1] = 1f;
				}

				PdfStitchingFunction stitch = PdfStitchingFunction.MakeFunction(
					domain, new (float, float)[] { (0, 1), (0, 1), (0, 1) },
					functions,
					bounds, encode);

				return stitch;
			}
		}

		private PdfShadingPattern MakeLinearShadingPattern(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			PdfFunction shadingFunction = MakeGradientInterpolationFunction(stops);

			PdfShadingDictionary shadingDictionary = PdfAxialShadingDictionary.Create(
				PdfColorSpace.DeviceRGB, null,
				null,
				false,
				x1, y1, x2, y2,
				null, shadingFunction, (true, true));

			PdfShadingPattern shadingPattern = new PdfShadingPattern(shadingDictionary, ConvertTransform(GetTransform()), null);

			return shadingPattern;
		}

		public ISharpGraphicsState SetStrokeLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			state.strokePaint = new CanvasLinearGradientPaint(x1, y1, x2, y2, stops);

			PdfShadingPattern shadingPattern = MakeLinearShadingPattern(x1, y1, x2, y2, stops);

			canvas.SetStrokingColorSpace(PdfColorSpace.PatternNoParams);
			canvas.SetStrokingPattern(shadingPattern, null);

			return this;
		}

		public ISharpGraphicsState SetFillLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			state.fillPaint = new CanvasLinearGradientPaint(x1, y1, x2, y2, stops);

			PdfShadingPattern shadingPattern = MakeLinearShadingPattern(x1, y1, x2, y2, stops);

			canvas.SetNonStrokingColorSpace(PdfColorSpace.PatternNoParams);
			canvas.SetNonStrokingPattern(shadingPattern, null);

			return this;
		}

		private PdfShadingPattern MakeRadialShadingPattern(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			// This function is not working perfectly
			// It does not quite agree with the WPF version
			// But perhaps only in edge cases?

			if (r2 < r1) {
				(r1, r2) = (r2, r1);
				(x1, x2) = (x2, x1);
				(y1, y2) = (y2, y1);
				stops = stops.Reverse().Select(s => new ColorStop(1f - s.Stop, s.Color)).ToList();
			}

			PdfFunction shadingFunction = MakeGradientInterpolationFunction(stops);

			PdfColor? backgroundColor = stops.Count > 0 ? (r1 < r2 ? ConvertColor(stops[0].Color, out _) : ConvertColor(stops[^1].Color, out _)) : null;

			PdfShadingDictionary shadingDictionary = PdfRadialShadingDictionary.Create(
				PdfColorSpace.DeviceRGB, backgroundColor,
				null,
				false,
				x2, y2, r2, x1, y1, r1,
				null, shadingFunction, (true, true));

			PdfShadingPattern shadingPattern = new PdfShadingPattern(shadingDictionary, ConvertTransform(GetTransform()), null);

			return shadingPattern;
		}

		public ISharpGraphicsState SetStrokeRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			state.strokePaint = new CanvasRadialGradientPaint(x1, y1, r1, x2, y2, r2, stops);

			PdfShadingPattern shadingPattern = MakeRadialShadingPattern(x1, y1, r1, x2, y2, r2, stops);

			canvas.SetStrokingColorSpace(PdfColorSpace.PatternNoParams);
			canvas.SetStrokingPattern(shadingPattern, null);

			return this;
		}

		public ISharpGraphicsState SetFillRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			state.fillPaint = new CanvasRadialGradientPaint(x1, y1, r1, x2, y2, r2, stops);

			PdfShadingPattern shadingPattern = MakeRadialShadingPattern(x1, y1, r1, x2, y2, r2, stops);

			canvas.SetNonStrokingColorSpace(PdfColorSpace.PatternNoParams);
			canvas.SetNonStrokingPattern(shadingPattern, null);

			return this;
		}

		/*
		public ISharpGraphicsState SetFillLinearGradient(float x1, float y1, float x2, float y2, IList<ColorStop> stops) {
			PdfFunction gradientFunction = ConstructGradientFunction(stops);
			PdfShading.Axial axial = new PdfShading.Axial(new PdfDeviceCs.Rgb(), new PdfArray(new float[] { x1, y1, x2, y2 }), gradientFunction);
			axial.SetExtend(true, true);
			PdfPattern.Shading pattern = new PdfPattern.Shading(axial);
			canvas.SetFillColorShading(pattern);
			return this;
		}

		public ISharpGraphicsState SetFillRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IList<ColorStop> stops) {
			PdfFunction gradientFunction = ConstructGradientFunction(stops);
			PdfShading.Radial radial = new PdfShading.Radial(new PdfDeviceCs.Rgb(), new PdfArray(new float[] { x1, y1, r1, x2, y2, r2 }), gradientFunction);
			radial.SetExtend(true, true);
			PdfPattern.Shading pattern = new PdfPattern.Shading(radial);
			canvas.SetFillColorShading(pattern);
			return this;
		}
		*/

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
			canvas.Move(x, y);
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas LineTo(float x, float y) {
			canvas.Line(x, y);
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas CurveTo(float x2, float y2, float x3, float y3) {
			canvas.Quadratic(x2, y2, x3, y3);
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas CurveTo(float x1, float y1, float x2, float y2, float x3, float y3) {
			canvas.Cubic(x1, y1, x2, y2, x3, y3);
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas ClosePath() {
			canvas.Close();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EndPath() {
			canvas.EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Ellipse(float x1, float y1, float x2, float y2) {
			return this.BezierEllipse(x1, y1, x2, y2);
		}

		public ISharpCanvas Rectangle(float x, float y, float width, float height) {
			canvas.Rectangle(x, y, width, height);
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Stroke() {
			canvas.Stroke();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Fill() {
			canvas.FillNonZero();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EoFill() {
			canvas.FillEvenOdd();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas FillStroke() {
			canvas.FillStrokeNonZero();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EoFillStroke() {
			canvas.FillStrokeEvenOdd();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Clip() {
			canvas.ClipNonZero();
			// This does not actually close the path, so CurrentPenLocation is not set to null
			return this;
		}
		public ISharpCanvas EoClip() {
			canvas.ClipEvenOdd();
			// This does not actually close the path, so CurrentPenLocation is not set to null
			return this;
		}


		public FontPathGrouping GetFonts() => state.fonts.GetFonts();
		public float GetTextSize() => state.fontsize;
		public TextFormat GetTextFormat() => state.textFormat;

		public ISharpGraphicsState SetTextFormatAndSize(TextFormat font, float size) {
			state.textFormat = font;
			state.fontsize = size;
			canvas.FontAndSize(state.fonts.GetPdfFont(state.textFormat), state.fontsize);
			return this;
		}

		public ISharpGraphicsState SetTextSize(float size) {
			state.fontsize = size;
			canvas.FontAndSize(state.fonts.GetPdfFont(state.textFormat), state.fontsize);
			return this;
		}

		public ISharpGraphicsState SetFont(TextFormat format, FontPath? font) {
			if (font?.Path != null) {
				state.fonts.SetFont(format, font);
			}
			else {
				state.fonts.SetNull(format);
			}

			if (format == state.textFormat) {
				canvas.FontAndSize(state.fonts.GetPdfFont(state.textFormat), state.fontsize);
			}

			return this;
		}

		public Canvas.TextRenderingMode GetTextRenderingMode() => state.textRenderingMode;
		public ISharpGraphicsState SetTextRenderingMode(Canvas.TextRenderingMode mode) {
			state.textRenderingMode = mode;
			canvas.TextRenderingMode(ConvertTextRenderingMode(mode));
			return this;
		}

		public float GetAscent(string text, TextFormat format, float fontsize) => state.fonts.GetPdfFont(format).GetAscent(text, fontsize);
		public float GetDescent(string text, TextFormat format, float fontsize) => state.fonts.GetPdfFont(format).GetDescent(text, fontsize);
		public float GetWidth(string text, TextFormat format, float fontsize) => state.fonts.GetPdfFont(format).GetWidth(text, fontsize);

		public ISharpCanvas DrawText(string text, float x, float y) {
			canvas.SaveState();
			SetFillColor(GetTextColor());
			canvas.BeginText()
				.MoveToStart(x, y)
				.ShowText(text)
				.EndText();
			canvas.RestoreState();

			CurrentPenLocation = null;
			return this;
		}


		public ISharpCanvas AddImage(CanvasImageData image, Rectangle rect, float? imageAspect) {
			if (image.IsPdf) {
				FileStream srcStream = new FileStream(image.Path.Path, FileMode.Open, FileAccess.Read);
				PdfStreamReader srcReader = new PdfStreamReader(srcStream);
				PdfFormXObject xObj = srcReader.GetPageAsXObject(0);
				srcStream.Close();

				float aspect = imageAspect ?? xObj.Width / xObj.Height;
				if (aspect > 0) {
					rect = rect.Aspect(aspect);
				}

				canvas.SaveState();
				canvas.ConcatenateMatrix(rect.Width / xObj.Width, 0, 0, rect.Height / xObj.Height, rect.X, rect.Y);
				canvas.PaintXObject(xObj);
				canvas.RestoreState();
			}
			else {
				PdfImageXObject imageObj = new PdfImageXObject(image.Path.Path, 100, true); // TODO Need a way of setting Quality and Interpolate

				float aspect = imageAspect ?? (float)imageObj.Width / (float)imageObj.Height;
				if (aspect > 0) {
					rect = rect.Aspect(aspect);
				}

				canvas.SaveState();
				canvas.ConcatenateMatrix(rect.Width, 0, 0, rect.Height, rect.X, rect.Y);
				canvas.PaintXObject(imageObj);
				canvas.RestoreState();
			}
			/*
			else {
				if (imageAspect.HasValue) {
					rect = rect.Aspect(imageAspect.Value);
				}
				SaveState();
				SetFillColor(Color.Red);
				this.Rectangle(rect).Fill();
				RestoreState();
			}
			*/
			return this;
		}

		/*
		public ISharpCanvas AddImage(ImageData image, Rectangle rect, float? imageAspect) {
			return AddImage(image.Path, rect, imageAspect);
		}
		*/


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

				PdfTextFieldFlags textFieldFlags = PdfTextFieldFlags.DoNotScroll;
				if (multiline) { textFieldFlags = textFieldFlags | PdfTextFieldFlags.Multiline; }

				PdfAcroField textField = PdfAcroFormManager.AddTextField(pdf.AcroForm, pdfPage, ConvertRectangle(this.GetPageSpaceRect(rect)),
					PdfAnnotationFlags.Print, fieldName, tooltip,
					PdfFieldFlags.None, textFieldFlags, (maxLen > 0 ? (int?)maxLen : null),
					new PdfTextString(value ?? ""), new PdfTextString(value ?? ""),
					state.fonts.GetPdfFont(format), fontSize, ConvertColor(color, out _), ConvertJustification(justification),
					PdfWidgetRotation.R0 // TODO This should be editable
					);

				// TODO Implement
				if (fieldType == TextFieldType.FLOAT) {
					//field.SetAdditionalAction(PdfName.K, PdfAction.CreateJavaScript("AFNumber_Keystroke(0,0,0,0,\"\",true);"));
				}
				else if (fieldType == TextFieldType.INT) {
					//field.SetAdditionalAction(PdfName.K, PdfAction.CreateJavaScript("if(!event.willCommit) event.rc = /^\\d*$/.test(event.change);"));
				}

				if (!string.IsNullOrEmpty(value)) {
					pdf.AcroForm.NeedsAppearances = true;
				}

				// TODO Implement rich?

				if (document.FieldValues.TryGetValue(fieldName, out PdfObject? fieldValue) && fieldValue is PdfString textValue) {
					textField.Value = textValue;
					pdf.AcroForm.NeedsAppearances = true;
				}

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

				PdfAcroField checkField = PdfAcroFormManager.AddStandardCheckBoxField(
						pdf.AcroForm, pdfPage, ConvertRectangle(this.GetPageSpaceRect(rect)),
						PdfAnnotationFlags.Print, fieldName, tooltip,
						PdfFieldFlags.None, false, false,
						ConvertCheckType(checkType), ConvertColor(color, out _),
						null
					);

				if(document.FieldValues.TryGetValue(fieldName, out PdfObject? fieldValue) && fieldValue is PdfName checkValue) {
					checkField.Value = checkValue;
					// pdf.AcroForm.NeedsAppearances = true; // Actually not necessary for checkfields, as their appearances are already defined (or, should be)
				}

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

				PdfAction buttonAction = new PdfJavaScriptAction("event.target.buttonImportIcon();", null);

				Rectangle pageSpaceRect = this.GetPageSpaceRect(rect);

				PdfPushButtonAcroField buttonField = PdfAcroFormManager.AddPushButtonField(
						pdf.AcroForm, pdfPage, ConvertRectangle(pageSpaceRect),
						PdfAnnotationFlags.Print, name, tooltip,
						PdfFieldFlags.None, buttonAction,
						null
					);

				if (defaultImage != null) {
					if (defaultImage.IsPdf) {
						FileStream srcStream = new FileStream(defaultImage.Path.Path, FileMode.Open, FileAccess.Read);
						PdfStreamReader srcReader = new PdfStreamReader(srcStream);
						PdfFormXObject srcObj = srcReader.GetPageAsXObject(0);
						srcStream.Close();

						float srcAspect = srcObj.Width / srcObj.Height;
						Rectangle srcAppearanceRect = new Rectangle(pageSpaceRect.Width, pageSpaceRect.Height).Aspect(srcAspect);

						buttonField.Appearance.graphics.SaveState();
						buttonField.Appearance.graphics.ConcatenateMatrix(srcAppearanceRect.Width, 0, 0, srcAppearanceRect.Height, srcAppearanceRect.X, srcAppearanceRect.Y);
						buttonField.Appearance.graphics.PaintXObject(srcObj);
						buttonField.Appearance.graphics.RestoreState();
					}
					else {
						PdfImageXObject imageObj = new PdfImageXObject(defaultImage.Path.Path, 100, true); // Add some way to adjust quality and interpolation?

						float imageAspect = (float)imageObj.Width / (float)imageObj.Height;
						Rectangle imageAppearanceRect = new Rectangle(pageSpaceRect.Width, pageSpaceRect.Height).Aspect(imageAspect);

						buttonField.Appearance.graphics.SaveState();
						buttonField.Appearance.graphics.ConcatenateMatrix(imageAppearanceRect.Width, 0, 0, imageAppearanceRect.Height, imageAppearanceRect.X, imageAppearanceRect.Y);
						buttonField.Appearance.graphics.PaintXObject(imageObj);
						buttonField.Appearance.graphics.RestoreState();
					}
				}

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
