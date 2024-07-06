using GeboPdf.Fonts;
using GeboPdf.Fonts.TrueType;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Exceptions;
using SharpSheets.Fonts;
using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditorAvalonia.Designer.DrawingCanvas {

	// SharpGeometryDrawingCanvas
	// ActualWidth -> Bounds.Width

	public class SharpGeometryDrawingDocument : ISharpDocument {

		public IList<SharpGeometryDrawingCanvas> Pages { get; }
		public int PageCount { get { return Pages.Count; } }

		public IReadOnlyCollection<string> FieldNames { get { return new HashSet<string>(Pages.SelectMany(p => p.FieldNames)); } }

		public SharpGeometryDrawingDocument() {
			Pages = new List<SharpGeometryDrawingCanvas>();
		}

		ISharpCanvas ISharpDocument.AddNewPage(Size pageSize) => AddNewPage(pageSize);

		public SharpGeometryDrawingCanvas AddNewPage(Size pageSize) {
			SharpGeometryDrawingCanvas page = new SharpGeometryDrawingCanvas(this, pageSize);
			Pages.Add(page);
			return page;
		}

		public int? GetOwnerPage(object owner) {
			for (int i = 0; i < Pages.Count; i++) {
				if (Pages[i].ContainsAreaOwner(owner)) {
					// This is perhaps imperfect, as there is currently no guarantee that an owner only have areas on one page
					// But it should do for now, as they generally should
					return i;
				}
			}
			return null;
		}

	}

	public class SharpGeometryDrawingGraphicsState {

		public float defaultLineWidth = 1f;
		public float lineWidth = 1f;

		public StrokeDash? strokeDash = null;

		public SharpSheets.Colors.Color foreground = SharpGraphicsDefaults.Foreground;
		public SharpSheets.Colors.Color background = SharpGraphicsDefaults.Background;
		public SharpSheets.Colors.Color midtone = SharpGraphicsDefaults.Midtone;
		public SharpSheets.Colors.Color textColor = SharpGraphicsDefaults.TextColor;

		public CanvasPaint strokePaint = new CanvasSolidPaint(SharpSheets.Colors.Color.Black);
		public CanvasPaint fillPaint = new CanvasSolidPaint(SharpSheets.Colors.Color.White);
		public BrushData strokeBrush;
		public BrushData fillBrush;

		public LineJoinStyle lineJoinStyle = LineJoinStyle.MITER;
		public LineCapStyle lineCapStyle = LineCapStyle.BUTT;
		public float mitreLimit = 10f;

		public TypefaceGrouping typefaces;
		public TextFormat font = TextFormat.REGULAR;
		public float fontsize = 0f;
		public TextRenderingMode textRenderingMode = TextRenderingMode.FILL;

		public GeometryData? clipGeometry = null;
		public Transform pagetransform = Transform.Identity;

		public bool fieldsEnabled = true;
		public string currentPrefix = "";

		public SharpGeometryDrawingGraphicsState() {
			strokeBrush = new SolidColorBrushData(SharpSheets.Colors.Color.Black);

			fillBrush = new SolidColorBrushData(SharpSheets.Colors.Color.White);

			typefaces = new TypefaceGrouping();
		}
		public SharpGeometryDrawingGraphicsState(SharpGeometryDrawingGraphicsState source) {
			defaultLineWidth = source.defaultLineWidth;
			lineWidth = source.lineWidth;
			strokeDash = source.strokeDash;
			foreground = source.foreground;
			background = source.background;
			midtone = source.midtone;
			textColor = source.textColor;
			strokePaint = source.strokePaint;
			fillPaint = source.fillPaint;
			strokeBrush = source.strokeBrush;
			fillBrush = source.fillBrush;
			lineJoinStyle = source.lineJoinStyle;
			mitreLimit = source.mitreLimit;
			typefaces = new TypefaceGrouping(source.typefaces);
			font = source.font;
			fontsize = source.fontsize;
			textRenderingMode = source.textRenderingMode;
			clipGeometry = source.clipGeometry;
			pagetransform = source.pagetransform;
			fieldsEnabled = source.fieldsEnabled;
			currentPrefix = source.currentPrefix;
		}
	}

	public class SharpGeometryDrawingCanvas : ISharpDesignerCanvas {

		public SharpGeometryDrawingDocument Document { get; }
		public Rectangle CanvasRect { get; }

		public readonly DrawingGroupData drawingGroup;

		private SharpGeometryDrawingGraphicsState gsState;
		private readonly Stack<SharpGeometryDrawingGraphicsState> gsStack;

		public DrawPoint? CurrentPenLocation { get; private set; } = null;

		public IReadOnlyCollection<string> DocumentFieldNames { get { return Document.FieldNames; } }
		public IReadOnlyCollection<string> FieldNames { get { return Fields.Keys; } }
		public Dictionary<string, CanvasField> Fields { get; }

		private readonly Dictionary<object, List<RegisteredAreas>> areas;

		private readonly Transform GeometryTransform;

		public SharpGeometryDrawingCanvas(SharpGeometryDrawingDocument document, Size pageSize) {
			Document = document;
			CanvasRect = (Rectangle)pageSize;

			drawingGroup = new DrawingGroupData();

			Fields = new Dictionary<string, CanvasField>();

			areas = new Dictionary<object, List<RegisteredAreas>>(new IdentityEqualityComparer<object>());

			gsState = new SharpGeometryDrawingGraphicsState();
			gsStack = new Stack<SharpGeometryDrawingGraphicsState>();

			GeometryTransform = GetBaseTransform(CanvasRect);

			this.Rectangle(CanvasRect).Fill();

			drawingErrors = new List<SharpDrawingException>();
		}

		#region Drawing Errors
		private readonly List<SharpDrawingException> drawingErrors;
		public void LogError(SharpDrawingException error) {
			drawingErrors.Add(error);
		}
		public IEnumerable<SharpDrawingException> GetDrawingErrors() {
			return drawingErrors;
		}
		#endregion

		#region Areas
		public void RegisterAreas(object owner, Rectangle originalArea, Rectangle? adjustedArea, Rectangle[] innerAreas) {
			if (!areas.ContainsKey(owner)) {
				areas.Add(owner, new List<RegisteredAreas>());
			}
			areas[owner].Add(new RegisteredAreas(originalArea, adjustedArea, innerAreas));
		}

		public Dictionary<object, RegisteredAreas> GetAreas(Avalonia.Point point) {
			return areas
				.Where(kv => kv.Value.Any(r => r.Contains((float)point.X, (float)(CanvasRect.Height - point.Y))))
				.ToDictionary(kv => kv.Key, kv => kv.Value.MaxBy(r => r.Total.Area)!);
		}

		public IEnumerable<RegisteredAreas> GetAreas(object owner) {
			if (areas.TryGetValue(owner, out List<RegisteredAreas>? rectangles)) {
				return rectangles;
			}
			else {
				return Enumerable.Empty<RegisteredAreas>();
			}
		}

		public bool ContainsAreaOwner(object owner) {
			return areas.ContainsKey(owner);
		}
		#endregion

		#region Utilities
		static Avalonia.Point MakePoint(float x, float y) {
			return new Avalonia.Point(x, y);
		}

		static Avalonia.Rect ConvertRect(Rectangle rect) {
			Avalonia.Point topLeft = MakePoint(rect.Left, rect.Top);
			return new Avalonia.Rect(topLeft, new Avalonia.Size(rect.Width, rect.Height));
		}

		private static Transform GetBaseTransform(Rectangle pageSize) {
			return Transform.Translate(0, pageSize.Height) * Transform.Scale(1, -1);
		}
		#endregion

		#region Graphics State
		public ISharpGraphicsState SaveState() {
			gsStack.Push(gsState);
			gsState = new SharpGeometryDrawingGraphicsState(gsState);
			return this;
		}

		public ISharpGraphicsState RestoreState() {
			gsState = gsStack.Pop();
			CurrentPenLocation = null;
			return this;
		}

		public int GetStateDepth() { return gsStack.Count; }

		#region Colors
		public CanvasPaint GetStrokePaint() {
			return gsState.strokePaint;
		}
		public CanvasPaint GetFillPaint() {
			return gsState.fillPaint;
		}

		public SharpSheets.Colors.Color GetForegroundColor() {
			return gsState.foreground;
		}
		public SharpSheets.Colors.Color GetBackgroundColor() {
			return gsState.background;
		}
		public SharpSheets.Colors.Color GetMidtoneColor() {
			return gsState.midtone;
		}
		public SharpSheets.Colors.Color GetTextColor() {
			return gsState.textColor;
		}

		public ISharpGraphicsState SetForegroundColor(SharpSheets.Colors.Color color) {
			gsState.foreground = color;
			return this;
		}
		public ISharpGraphicsState SetBackgroundColor(SharpSheets.Colors.Color color) {
			gsState.background = color;
			return this;
		}
		public ISharpGraphicsState SetMidtoneColor(SharpSheets.Colors.Color color) {
			gsState.midtone = color;
			return this;
		}
		public ISharpGraphicsState SetTextColor(SharpSheets.Colors.Color color) {
			gsState.textColor = color;
			return this;
		}

		private SolidColorBrushData MakeSolidBrush(SharpSheets.Colors.Color color) {
			SolidColorBrushData brush = new SolidColorBrushData(color);
			return brush;
		}

		public ISharpGraphicsState SetStrokeColor(SharpSheets.Colors.Color color) {
			gsState.strokePaint = new CanvasSolidPaint(color);
			gsState.strokeBrush = MakeSolidBrush(color);
			return this;
		}
		public ISharpGraphicsState SetFillColor(SharpSheets.Colors.Color color) {
			gsState.fillPaint = new CanvasSolidPaint(color);
			gsState.fillBrush = MakeSolidBrush(color);
			return this;
		}

		private LinearGradientBrushData MakeLinearGradientBrush(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			stops = stops.Select(s => new ColorStop(s.Stop, s.Color.WithOpacity(1.0f))).OrderBy(s => s.Stop).ToList();

			GradientStopsData gradientStops = new GradientStopsData();
			gradientStops.AddRange(stops);
			LinearGradientBrushData brush = new LinearGradientBrushData() {
				StartPoint = new Avalonia.RelativePoint(MakePoint(x1, y1), Avalonia.RelativeUnit.Absolute),
				EndPoint = new Avalonia.RelativePoint(MakePoint(x2, y2), Avalonia.RelativeUnit.Absolute),
				GradientStops = gradientStops,
				Transform = GetCurrentTransformMatrix()
				//MappingMode = BrushMappingMode.Absolute
			};

			return brush;
		}

		public ISharpGraphicsState SetStrokeLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			gsState.strokePaint = new CanvasLinearGradientPaint(x1, y1, x2, y2, stops);
			gsState.strokeBrush = MakeLinearGradientBrush(x1, y1, x2, y2, stops);
			return this;
		}
		public ISharpGraphicsState SetFillLinearGradient(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			gsState.fillPaint = new CanvasLinearGradientPaint(x1, y1, x2, y2, stops);
			gsState.fillBrush = MakeLinearGradientBrush(x1, y1, x2, y2, stops);
			return this;
		}

		private RadialGradientBrushData MakeRadialGradientBrush(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			stops = stops.Select(s => new ColorStop(s.Stop, s.Color.WithOpacity(1.0f))).OrderBy(s => s.Stop).ToList();

			if (r2 > r1) {
				(r1, r2) = (r2, r1);
				(x1, x2) = (x2, x1);
				(y1, y2) = (y2, y1);
				stops = stops.Reverse().Select(s => new ColorStop(1f - s.Stop, s.Color)).ToList();
			}

			float rStartFactor = r2 / r1;
			float rRangeFactor = 1f - rStartFactor;

			List<ColorStop> GeometryStops = new List<ColorStop>();
			foreach (ColorStop stop in stops) {
				float finalStop = rStartFactor + stop.Stop * rRangeFactor;
				GeometryStops.Add(new ColorStop(finalStop, stop.Color));
			}

			GradientStopsData gradientStops = new GradientStopsData();
			gradientStops.AddRange(GeometryStops);
			RadialGradientBrushData brush = new RadialGradientBrushData() {
				GradientStops = gradientStops,
				Center = new Avalonia.RelativePoint(MakePoint(x1, y1), Avalonia.RelativeUnit.Absolute),
				GradientOrigin = new Avalonia.RelativePoint(MakePoint(x2, y2), Avalonia.RelativeUnit.Absolute),
				Radius = r1,
				Transform = GetCurrentTransformMatrix()
			};

			return brush;
		}

		public ISharpGraphicsState SetStrokeRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			gsState.strokePaint = new CanvasRadialGradientPaint(x1, y1, r1, x2, y2, r2, stops);
			gsState.strokeBrush = MakeRadialGradientBrush(x1, y1, r1, x2, y2, r2, stops);
			return this;
		}

		public ISharpGraphicsState SetFillRadialGradient(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			gsState.fillPaint = new CanvasRadialGradientPaint(x1, y1, r1, x2, y2, r2, stops);
			gsState.fillBrush = MakeRadialGradientBrush(x1, y1, r1, x2, y2, r2, stops);
			return this;
		}
		#endregion

		#region Line Information
		public float GetDefaultLineWidth() {
			return gsState.defaultLineWidth;
		}
		public float GetLineWidth() {
			return gsState.lineWidth;
		}

		public ISharpGraphicsState SetDefaultLineWidth(float linewidth) {
			gsState.defaultLineWidth = linewidth;
			gsState.lineWidth = linewidth;
			return this;
		}
		public ISharpGraphicsState SetLineWidth(float linewidth) {
			gsState.lineWidth = linewidth;
			return this;
		}

		public StrokeDash? GetStrokeDash() {
			return gsState.strokeDash;
		}

		public ISharpGraphicsState SetStrokeDash(StrokeDash? strokeDash) {
			gsState.strokeDash = strokeDash;
			return this;
		}

		private DashStyleData CurrentDashStyle {
			get {
				return gsState.strokeDash != null ? new DashStyleData(gsState.strokeDash.Values.Select(f => f / gsState.lineWidth), gsState.strokeDash.Offset) : new DashStyleData(null, 0f);
			}
		}
		#endregion

		#region Font

		public ISharpGraphicsState SetFont(TextFormat format, FontSetting? font) {
			if (font?.Path != null) {
				gsState.typefaces.SetFont(format, font);
			}
			else {
				gsState.typefaces.SetNull(format);
			}
			return this;
		}

		public FontSettingGrouping GetFonts() {
			return gsState.typefaces.GetFonts();
		}

		public TextFormat GetTextFormat() {
			return gsState.font;
		}

		public float GetTextSize() {
			return gsState.fontsize;
		}

		public ISharpGraphicsState SetTextFormatAndSize(TextFormat font, float size) {
			gsState.font = font;
			gsState.fontsize = size;
			return this;
		}

		public ISharpGraphicsState SetTextSize(float size) {
			gsState.fontsize = size;
			return this;
		}
		#endregion

		#region Misc
		public ISharpGraphicsState SetLineCapStyle(LineCapStyle capStyle) {
			gsState.lineCapStyle = capStyle;
			return this;
		}
		public LineCapStyle GetLineCapStyle() {
			return gsState.lineCapStyle;
		}

		public ISharpGraphicsState SetLineJoinStyle(LineJoinStyle joinStyle) {
			gsState.lineJoinStyle = joinStyle;
			return this;
		}
		public LineJoinStyle GetLineJoinStyle() {
			return gsState.lineJoinStyle;
		}

		public ISharpGraphicsState SetMiterLimit(float mitreLimit) {
			gsState.mitreLimit = mitreLimit;
			return this;
		}
		public float GetMiterLimit() {
			return gsState.mitreLimit;
		}

		public ISharpGraphicsState SetTextRenderingMode(TextRenderingMode mode) {
			gsState.textRenderingMode = mode;
			return this;
		}
		public TextRenderingMode GetTextRenderingMode() {
			return gsState.textRenderingMode;
		}

		public ISharpGraphicsState SetFieldsEnabled(bool enabled) {
			gsState.fieldsEnabled = enabled;
			return this;
		}
		public bool IsFieldsEnabled() {
			return gsState.fieldsEnabled;
		}

		public ISharpGraphicsState SetFieldPrefix(string prefix) {
			gsState.currentPrefix = prefix;
			return this;
		}
		public string GetFieldPrefix() {
			return gsState.currentPrefix;
		}
		#endregion

		#endregion

		#region Drawing

		PathGeometryData? _pathGeometry;
		PathFigureData? _pathFigure;

		PathGeometryData PathGeometry {
			get {
				if (_pathGeometry == null) {
					_pathGeometry = new PathGeometryData(); ;
				}
				return _pathGeometry;
			}
		}
		PathFigureData PathFigure {
			get {
				if (_pathFigure == null) {
					_pathFigure = new PathFigureData() { IsClosed = false };
				}
				return _pathFigure;
			}
			set {
				_pathFigure = value;
			}
		}

		private void ResetPathGeometry() {
			_pathFigure = null;
			_pathGeometry = null;
		}
		private void ResetPathFigure() {
			_pathFigure = null;
		}

		public ISharpCanvas MoveTo(float x, float y) {
			if (PathFigure.Count > 0) {
				PathGeometry.Add(PathFigure);
			}
			PathFigure = new PathFigureData() { IsClosed = false, StartPoint = MakePoint(x, y) };
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas LineTo(float x, float y) {
			PathFigure.Add(new LineSegmentData() { Point = MakePoint(x, y) });
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas CurveTo(float x2, float y2, float x3, float y3) {
			PathFigure.Add(new QuadraticBezierSegmentData() { Point1 = MakePoint(x2, y2), Point2 = MakePoint(x3, y3) });
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas CurveTo(float x1, float y1, float x2, float y2, float x3, float y3) {
			PathFigure.Add(new BezierSegmentData() { Point1 = MakePoint(x1, y1), Point2 = MakePoint(x2, y2), Point3 = MakePoint(x3, y3) });
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas ClosePath() {
			if (PathFigure.Count > 0) {
				PathFigure.IsClosed = true;
				PathGeometry.Add(PathFigure);
			}
			ResetPathFigure();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EndPath() {
			ResetPathGeometry();
			otherGeometry = null;
			CurrentPenLocation = null;
			return this;
		}

		private GeometryData? otherGeometry;

		public ISharpCanvas Rectangle(float x, float y, float width, float height) {
			otherGeometry = new RectangleGeometryData() {
				Rect = new Avalonia.Rect(MakePoint(x, y), MakePoint(x + width, y + height))
			};
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Ellipse(float x1, float y1, float x2, float y2) {
			otherGeometry = new EllipseGeometryData() {
				Center = MakePoint((x1 + x2) / 2, (y1 + y2) / 2),
				RadiusX = (x2 - x1) / 2,
				RadiusY = (y2 - y1) / 2
			};
			CurrentPenLocation = null;
			return this;
		}

		private DrawingData ClipDrawing(DrawingData drawing) {
			if (gsState.clipGeometry != null) {
				DrawingGroupData clippedDrawing = new DrawingGroupData() { ClipGeometry = gsState.clipGeometry };
				clippedDrawing.Children.Add(drawing);
				return clippedDrawing;
			}
			else {
				return drawing;
			}
		}

		private GeometryDrawingData GetDrawing() {
			GeometryDrawingData drawing = new GeometryDrawingData();
			if (otherGeometry != null) {
				drawing.Geometry = otherGeometry;
			}
			else {
				PathGeometry.Add(PathFigure);
				drawing.Geometry = PathGeometry;
			}
			drawing.Geometry.Transform = GetCurrentTransformMatrix();
			return drawing;
		}

		private PenData GetCurrentPen() {
			return new PenData(gsState.strokeBrush, GetLineWidth()) {
				LineJoin = gsState.lineJoinStyle,
				MiterLimit = gsState.mitreLimit,
				LineCap = gsState.lineCapStyle,
				DashStyle = CurrentDashStyle
				//DashCap = PenLineCap.Flat // TODO Necessary?
			};
		}

		public ISharpCanvas Stroke() {
			GeometryDrawingData strokeDrawing = GetDrawing();
			strokeDrawing.Pen = GetCurrentPen();
			strokeDrawing.Brush = null;

			DrawingData finalDrawing = ClipDrawing(strokeDrawing);
			drawingGroup.Children.Add(finalDrawing);

			EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Fill() {
			GeometryDrawingData fillDrawing = GetDrawing();
			fillDrawing.Pen = null;
			fillDrawing.Brush = gsState.fillBrush;

			DrawingData finalDrawing = ClipDrawing(fillDrawing);
			drawingGroup.Children.Add(finalDrawing);

			EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas FillStroke() {
			GeometryDrawingData fillStrokeDrawing = GetDrawing();
			fillStrokeDrawing.Pen = GetCurrentPen();
			fillStrokeDrawing.Brush = gsState.fillBrush;

			DrawingData finalDrawing = ClipDrawing(fillStrokeDrawing);

			drawingGroup.Children.Add(finalDrawing);

			EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EoFill() {
			PathGeometry.FillRule = Avalonia.Media.FillRule.EvenOdd;
			return Fill();
		}

		public ISharpCanvas EoFillStroke() {
			PathGeometry.FillRule = Avalonia.Media.FillRule.EvenOdd;
			return FillStroke();
		}

		private void AppendClipGeometry(GeometryData geometry) {
			if (gsState.clipGeometry == null) {
				gsState.clipGeometry = geometry;
			}
			else {
				gsState.clipGeometry = new CombinedGeometryData(Avalonia.Media.GeometryCombineMode.Intersect, gsState.clipGeometry, geometry);
			}
		}

		public ISharpCanvas Clip() {
			GeometryData newClip;
			if (otherGeometry != null) {
				newClip = otherGeometry;
			}
			else {
				ClosePath();
				PathGeometry.Add(PathFigure);
				newClip = PathGeometry;
			}
			newClip.Transform = GetCurrentTransformMatrix();
			AppendClipGeometry(newClip);
			EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EoClip() {
			PathGeometry.FillRule = Avalonia.Media.FillRule.EvenOdd;
			return Clip();
		}

		public ISharpGraphicsState SetTransform(Transform transform) {
			gsState.pagetransform = GetBaseTransform(CanvasRect) * transform;
			return this;
		}

		public ISharpGraphicsState ApplyTransform(Transform transform) {
			gsState.pagetransform *= transform;
			return this;
		}

		public Transform GetTransform() {
			return gsState.pagetransform;
		}

		private Transform GetCurrentTransformMatrix(Transform? append = null) {
			Transform current = GeometryTransform * GetTransform();
			if (append != null) { current *= append; }
			return current;
		}

		#endregion

		#region Text

		private bool FillText {
			get {
				TextRenderingMode current = gsState.textRenderingMode;
				return (current & TextRenderingMode.FILL) == TextRenderingMode.FILL;
			}
		}

		private bool StrokeText {
			get {
				TextRenderingMode current = gsState.textRenderingMode;
				return (current & TextRenderingMode.STROKE) == TextRenderingMode.STROKE;
			}
		}

		private bool ClipText {
			get {
				TextRenderingMode current = gsState.textRenderingMode;
				return (current & TextRenderingMode.CLIP) == TextRenderingMode.CLIP;
			}
		}

		public ISharpCanvas DrawText(string text1, float x, float y) {
			if (gsState.fontsize <= 0) {
				// TODO Throw error?
				return this; // Don't bother drawing if text is invisible
			}

			Avalonia.Point point = MakePoint(x, y);
			GeometryGroupData textGeometryGroup = new GeometryGroupData() { FillRule = Avalonia.Media.FillRule.NonZero };

			SolidColorBrushData? brush = new SolidColorBrushData(GetTextColor());

			//Pen? pen = StrokeText ? new Pen(gsState.strokeBrush, GetLineWidth()) { DashStyle = CurrentDashStyle } : null;
			PenData? pen = StrokeText ? GetCurrentPen() : null;
			brush = FillText ? brush : null;

			float fontsize = gsState.fontsize;

			TrueTypeFontFileOutlines glyphOutlines = gsState.typefaces.GetOutlines(gsState.font);
			PdfGlyphFont pdfFont = gsState.typefaces.GetPdfFont(gsState.font); // Feels messy to be referencing PDF library here

			PositionedGlyphRun glyphRun = pdfFont.GetGlyphRun(text1);

			// Draw each character individually
			// This will need updating for vertical writing directions
			float runningX = 0f;
			for (int i = 0; i < glyphRun.Count; i++) {
				ushort g = glyphRun[i];

				PathGeometryData? glyphGeometry = glyphOutlines.GetGlyphGeometryData(g, fontsize);

				if (glyphGeometry is not null) {
					(short xPlacement, short yPlacement) = glyphRun.GetPlacement(i);
					float xPlaceSized = PdfFont.ConvertDesignSpaceValue(xPlacement, fontsize);
					float yPlaceSized = PdfFont.ConvertDesignSpaceValue(yPlacement, fontsize);
					glyphGeometry.Transform = Transform.Translate(runningX + xPlaceSized, yPlaceSized);
					textGeometryGroup.Children.Add(glyphGeometry);
				}

				//runningX += pdfFont.GetWidth(g, fontsize);
				runningX += PdfFont.ConvertDesignSpaceValue(glyphRun.GetAdvanceTotal(i).xAdvance, fontsize);
			}

			//textGeometryGroup.Transform = GetCurrentTransformMatrix(SharpSheets.Canvas.Transform.Translate((float)point.X, (float)point.Y) * SharpSheets.Canvas.Transform.Scale(1, -1));
			textGeometryGroup.Transform = GetCurrentTransformMatrix(Transform.Translate((float)point.X, (float)point.Y));

			GeometryDrawingData textDrawing = new GeometryDrawingData() { Brush = brush, Pen = pen, Geometry = textGeometryGroup };

			if ((gsState.textRenderingMode & TextRenderingMode.FILL_STROKE) != TextRenderingMode.INVISIBLE) {
				drawingGroup.Children.Add(textDrawing);
			}

			if (ClipText) {
				AppendClipGeometry(textGeometryGroup);
			}

			CurrentPenLocation = null;

			return this;
		}

		public float GetAscent(string text, TextFormat format, float fontsize) {
			return gsState.typefaces.GetAscent(text, format, fontsize);
		}

		public float GetDescent(string text, TextFormat format, float fontsize) {
			return gsState.typefaces.GetDescent(text, format, fontsize);
		}

		public float GetWidth(string text, TextFormat format, float fontsize) {
			return gsState.typefaces.GetWidth(text, format, fontsize);
		}

		#endregion

		#region Fields

		public string? TextField(Rectangle rect, string name, string? tooltip, TextFieldType fieldType, string? value, TextFormat font, float fontSize, SharpSheets.Colors.Color color, bool multiline, bool rich, Justification justification, int maxLen = -1) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);
				Rectangle pageSpaceRect = this.GetPageSpaceRect(rect);
				Avalonia.Point rectPoint = MakePoint(pageSpaceRect.X, pageSpaceRect.Y);
				Fields.Add(fieldName, new CanvasField(fieldName, tooltip, new Rectangle((float)rectPoint.X, (float)rectPoint.Y, pageSpaceRect.Width, pageSpaceRect.Height), FieldType.TEXT));
				return fieldName;
			}
			else {
				return null;
			}
		}

		public string? CheckField(Rectangle rect, string name, string? tooltip, CheckType checkType, SharpSheets.Colors.Color color) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);
				Rectangle pageSpaceRect = this.GetPageSpaceRect(rect);
				Avalonia.Point rectPoint = MakePoint(pageSpaceRect.X, pageSpaceRect.Y);
				Fields.Add(fieldName, new CanvasField(fieldName, tooltip, new Rectangle((float)rectPoint.X, (float)rectPoint.Y, pageSpaceRect.Width, pageSpaceRect.Height), FieldType.CHECK));
				return fieldName;
			}
			else {
				return null;
			}
		}

		public string? ImageField(Rectangle rect, string name, string? tooltip, CanvasImageData? defaultImage = null) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);
				Rectangle pageSpaceRect = this.GetPageSpaceRect(rect);
				Avalonia.Point rectPoint = MakePoint(pageSpaceRect.X, pageSpaceRect.Y);
				Fields.Add(fieldName, new CanvasField(fieldName, tooltip, new Rectangle((float)rectPoint.X, (float)rectPoint.Y, pageSpaceRect.Width, pageSpaceRect.Height), FieldType.IMAGE));

				if (defaultImage != null) {
					// TODO We shouldn't be accepting PDFs here
					AddImage(defaultImage, rect, rect.AspectRatio);
				}

				return fieldName;
			}
			else {
				return null;
			}
		}

		#endregion

		#region Misc

		private void AddRastorImage(CanvasImageData? image, Rectangle rect, float? aspect) {
			if (image is not null && !string.IsNullOrWhiteSpace(image.Path.Path) && File.Exists(image.Path.Path)) {
				float imageAspect = (float)(aspect ?? image.Width / image.Height);
				if (imageAspect > 0) {
					rect = rect.Aspect(imageAspect);
				}

				Avalonia.Point point = MakePoint(rect.X, rect.Y + rect.Height);
				ImageDrawingData drawing = new ImageDrawingData() { ImageSource = image, Rect = new Avalonia.Rect(new Avalonia.Point(0, 0), new Avalonia.Size(rect.Width, rect.Height)) };

				DrawingGroupData imageDrawingGroup = new DrawingGroupData();
				imageDrawingGroup.Children.Add(drawing);
				imageDrawingGroup.Transform = GetCurrentTransformMatrix(Transform.Translate((float)point.X, (float)point.Y) * Transform.Scale(1, -1));


				DrawingData clippedImageDrawing = ClipDrawing(imageDrawingGroup);

				drawingGroup.Children.Add(clippedImageDrawing);
			}
			else {
				if (aspect != null) {
					rect = rect.Aspect(aspect.Value);
				}
				SaveState();
				SetFillColor(SharpSheets.Colors.Color.Red);
				this.Rectangle(rect).Fill();
				RestoreState();
			}
		}

		public ISharpCanvas AddImage(CanvasImageData image, Rectangle rect, float? imageAspect) {
			if (image.IsPdf) {
				SaveState();
				SetFillColor(SharpSheets.Colors.Color.LightGray.WithOpacity(0.58f));
				this.Rectangle(rect).Fill();
				SetTextColor(SharpSheets.Colors.Color.DarkGray.WithOpacity(0.58f));
				this.FitRichTextLine(rect, (RichString)"PDF", new ParagraphSpecification(1f, 0f, default), new FontSizeSearchParams(0.5f, 100f, 0.1f), Justification.CENTRE, Alignment.CENTRE, TextHeightStrategy.AscentDescent);
				RestoreState();
			}
			else {
				AddRastorImage(image, rect, imageAspect);
			}
			return this;
		}

		public float GetUserUnits() {
			return 1f; // Implement?
		}

		#endregion
	}

}
