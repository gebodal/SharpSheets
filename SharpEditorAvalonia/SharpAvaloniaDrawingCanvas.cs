using Avalonia;
using Avalonia.Media;
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditorAvalonia {

	// SharpAvaloniaDrawingCanvas

	public class SharpAvaloniaDrawingDocument : ISharpDocument {

		public IList<SharpAvaloniaDrawingCanvas> Pages { get; }
		public int PageCount { get { return Pages.Count; } }

		public IReadOnlyCollection<string> FieldNames { get { return new HashSet<string>(Pages.SelectMany(p => p.FieldNames)); } }

		public SharpAvaloniaDrawingDocument() {
			Pages = new List<SharpAvaloniaDrawingCanvas>();
		}

		ISharpCanvas ISharpDocument.AddNewPage(SharpSheets.Layouts.Size pageSize) => AddNewPage(pageSize);

		public SharpAvaloniaDrawingCanvas AddNewPage(SharpSheets.Layouts.Size pageSize) {
			SharpAvaloniaDrawingCanvas page = new SharpAvaloniaDrawingCanvas(this, pageSize);
			Pages.Add(page);
			return page;
		}

		public void Freeze() {
			foreach (SharpAvaloniaDrawingCanvas canvas in Pages) { canvas.Freeze(); }
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

	public class SharpAvaloniaDrawingGraphicsState {

		public float defaultLineWidth = 1f;
		public float lineWidth = 1f;

		public StrokeDash? strokeDash = null;

		public SharpSheets.Colors.Color foreground = SharpGraphicsDefaults.Foreground;
		public SharpSheets.Colors.Color background = SharpGraphicsDefaults.Background;
		public SharpSheets.Colors.Color midtone = SharpGraphicsDefaults.Midtone;
		public SharpSheets.Colors.Color textColor = SharpGraphicsDefaults.TextColor;

		public CanvasPaint strokePaint = new CanvasSolidPaint(SharpSheets.Colors.Color.Black);
		public CanvasPaint fillPaint = new CanvasSolidPaint(SharpSheets.Colors.Color.White);
		public Brush strokeBrush;
		public Brush fillBrush;

		public PenLineJoin lineJoinStyle = PenLineJoin.Miter;
		public PenLineCap lineCapStyle = PenLineCap.Flat;
		public double mitreLimit = 10.0;

		public TypefaceGrouping typefaces;
		public TextFormat font = TextFormat.REGULAR;
		public float fontsize = 0f;
		public SharpSheets.Canvas.TextRenderingMode textRenderingMode = SharpSheets.Canvas.TextRenderingMode.FILL;

		public Geometry? clipGeometry = null;
		public SharpSheets.Canvas.Transform pagetransform = SharpSheets.Canvas.Transform.Identity;

		public bool fieldsEnabled = true;
		public string currentPrefix = "";

		public SharpAvaloniaDrawingGraphicsState() {
			strokeBrush = new SolidColorBrush(Colors.Black);
			strokeBrush.ToImmutable();

			fillBrush = new SolidColorBrush(Colors.White);
			fillBrush.ToImmutable();

			typefaces = new TypefaceGrouping();
		}
		public SharpAvaloniaDrawingGraphicsState(SharpAvaloniaDrawingGraphicsState source) {
			this.defaultLineWidth = source.defaultLineWidth;
			this.lineWidth = source.lineWidth;
			this.strokeDash = source.strokeDash;
			this.foreground = source.foreground;
			this.background = source.background;
			this.midtone = source.midtone;
			this.textColor = source.textColor;
			this.strokePaint = source.strokePaint;
			this.fillPaint = source.fillPaint;
			this.strokeBrush = source.strokeBrush;
			this.fillBrush = source.fillBrush;
			this.lineJoinStyle = source.lineJoinStyle;
			this.mitreLimit = source.mitreLimit;
			this.typefaces = new TypefaceGrouping(source.typefaces);
			this.font = source.font;
			this.fontsize = source.fontsize;
			this.textRenderingMode = source.textRenderingMode;
			this.clipGeometry = source.clipGeometry;
			this.pagetransform = source.pagetransform;
			this.fieldsEnabled = source.fieldsEnabled;
			this.currentPrefix = source.currentPrefix;
		}
	}

	public enum FieldType { TEXT, CHECK, IMAGE }

	public struct AvaloniaCanvasField {
		public string Name { get; private set; }
		public string? Tooltip { get; private set; }
		public SharpSheets.Layouts.Rectangle Rect { get; private set; }
		public FieldType Type { get; private set; }
		public AvaloniaCanvasField(string name, string? tooltip, SharpSheets.Layouts.Rectangle rect, FieldType type) {
			Name = name;
			Tooltip = tooltip;
			Rect = rect;
			Type = type;
		}
	}

	public class RegisteredAreas {
		public Rectangle Original { get; }
		public Rectangle? Adjusted { get; }
		public Rectangle[] Inner { get; }

		public Rectangle Total { get; }

		public RegisteredAreas(Rectangle original, Rectangle? adjusted, Rectangle[] inner) {
			Original = original;
			Adjusted = adjusted;
			Inner = inner;

			Total = Adjusted is not null ? Rectangle.Union(Original, Adjusted) : Original;
		}

		public bool Contains(float x, float y) {
			return Original.Contains(x, y) || (Adjusted?.Contains(x, y) ?? false) || Inner.Any(i => i.Contains(x, y));
		}
	}

	public class SharpAvaloniaDrawingCanvas : ISharpCanvas {

		public SharpAvaloniaDrawingDocument Document { get; }
		public SharpSheets.Layouts.Rectangle CanvasRect { get; }

		public readonly DrawingGroup drawingGroup;

		private SharpAvaloniaDrawingGraphicsState gsState;
		private readonly Stack<SharpAvaloniaDrawingGraphicsState> gsStack;

		public DrawPoint? CurrentPenLocation { get; private set; } = null;

		public IReadOnlyCollection<string> DocumentFieldNames { get { return Document.FieldNames; } }
		public IReadOnlyCollection<string> FieldNames { get { return Fields.Keys; } }
		public Dictionary<string, AvaloniaCanvasField> Fields { get; }

		private readonly Dictionary<object, List<RegisteredAreas>> areas;

		private readonly SharpSheets.Canvas.Transform avaloniaTransform;

		public SharpAvaloniaDrawingCanvas(SharpAvaloniaDrawingDocument document, SharpSheets.Layouts.Size pageSize) {
			Document = document;
			CanvasRect = (Rectangle)pageSize;

			drawingGroup = new DrawingGroup();

			Fields = new Dictionary<string, AvaloniaCanvasField>();

			areas = new Dictionary<object, List<RegisteredAreas>>(new IdentityEqualityComparer<object>());

			gsState = new SharpAvaloniaDrawingGraphicsState();
			gsStack = new Stack<SharpAvaloniaDrawingGraphicsState>();

			avaloniaTransform = GetBaseTransform(CanvasRect);

			this.Rectangle(CanvasRect).Fill();

			this.drawingErrors = new List<SharpDrawingException>();
		}

		public void Freeze() {
			// TODO Necessary?
			//drawingGroup.Freeze();
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

		public Dictionary<object, RegisteredAreas> GetAreas(Point point) {
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
		Point MakePoint(float x, float y) {
			return new Point(x, y);
		}

		Rect ConvertRect(SharpSheets.Layouts.Rectangle rect) {
			Point topLeft = MakePoint(rect.Left, rect.Top);
			return new Rect(topLeft, new Avalonia.Size(rect.Width, rect.Height));
		}

		Color ConvertColor(SharpSheets.Colors.Color color) {
			return Color.FromArgb(color.A, color.R, color.G, color.B);
		}
		SharpSheets.Colors.Color ConvertColor(Color color) {
			return new SharpSheets.Colors.Color(color.R, color.G, color.B, color.A);
		}

		private static SharpSheets.Canvas.Transform GetBaseTransform(SharpSheets.Layouts.Rectangle pageSize) {
			return SharpSheets.Canvas.Transform.Translate(0, pageSize.Height) * SharpSheets.Canvas.Transform.Scale(1, -1);
		}
		#endregion

		#region Graphics State
		public ISharpGraphicsState SaveState() {
			gsStack.Push(gsState);
			gsState = new SharpAvaloniaDrawingGraphicsState(gsState);
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

		private SolidColorBrush MakeSolidBrush(SharpSheets.Colors.Color color) {
			SolidColorBrush brush = new SolidColorBrush(ConvertColor(color));
			brush.ToImmutable();
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

		private LinearGradientBrush MakeLinearGradientBrush(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			stops = stops.Select(s => new ColorStop(s.Stop, s.Color.WithOpacity(1.0f))).OrderBy(s => s.Stop).ToList();

			GradientStops gradientStops = new GradientStops();
			gradientStops.AddRange(stops.Select(s => new GradientStop(ConvertColor(s.Color), s.Stop)));
			LinearGradientBrush brush = new LinearGradientBrush() {
				StartPoint = new RelativePoint(MakePoint(x1, y1), RelativeUnit.Absolute),
				EndPoint = new RelativePoint(MakePoint(x2, y2), RelativeUnit.Absolute),
				GradientStops = gradientStops,
				SpreadMethod = GradientSpreadMethod.Pad,
				Transform = GetCurrentTransformMatrix()
				//MappingMode = BrushMappingMode.Absolute
			};
			brush.ToImmutable(); // TODO Necessary?

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

		private RadialGradientBrush MakeRadialGradientBrush(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			stops = stops.Select(s => new ColorStop(s.Stop, s.Color.WithOpacity(1.0f))).OrderBy(s => s.Stop).ToList();

			if (r2 > r1) {
				(r1, r2) = (r2, r1);
				(x1, x2) = (x2, x1);
				(y1, y2) = (y2, y1);
				stops = stops.Reverse().Select(s => new ColorStop(1f - s.Stop, s.Color)).ToList();
			}

			float rStartFactor = r2 / r1;
			float rRangeFactor = 1f - rStartFactor;

			List<GradientStop> avaloniaStops = new List<GradientStop>();
			foreach (ColorStop stop in stops) {
				float finalStop = rStartFactor + stop.Stop * rRangeFactor;
				avaloniaStops.Add(new GradientStop(ConvertColor(stop.Color), finalStop));
			}

			GradientStops gradientStops = new GradientStops();
			gradientStops.AddRange(avaloniaStops);
			RadialGradientBrush brush = new RadialGradientBrush() {
				GradientStops = gradientStops,
				SpreadMethod = GradientSpreadMethod.Pad,
				//MappingMode = BrushMappingMode.Absolute,
				Center = new RelativePoint(MakePoint(x1, y1), RelativeUnit.Absolute),
				GradientOrigin = new RelativePoint(MakePoint(x2, y2), RelativeUnit.Absolute),
				Radius = r1,
				Transform = GetCurrentTransformMatrix()
			};
			brush.ToImmutable(); // TODO Necessary?

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

		private DashStyle CurrentDashStyle {
			get {
				return gsState.strokeDash != null ? new DashStyle(gsState.strokeDash.Values.Select(f => (double)f / gsState.lineWidth), gsState.strokeDash.Offset) : new DashStyle(new double[] { 1.0 }, 0.0);
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
		public ISharpGraphicsState SetLineCapStyle(SharpSheets.Canvas.LineCapStyle capStyle) {
			if (capStyle == SharpSheets.Canvas.LineCapStyle.BUTT) { gsState.lineCapStyle = PenLineCap.Flat; }
			else if (capStyle == SharpSheets.Canvas.LineCapStyle.ROUND) { gsState.lineCapStyle = PenLineCap.Round; }
			else if (capStyle == SharpSheets.Canvas.LineCapStyle.PROJECTING_SQUARE) { gsState.lineCapStyle = PenLineCap.Square; }
			return this;
		}
		public SharpSheets.Canvas.LineCapStyle GetLineCapStyle() {
			if (gsState.lineCapStyle == PenLineCap.Flat) { return SharpSheets.Canvas.LineCapStyle.BUTT; }
			else if (gsState.lineCapStyle == PenLineCap.Round) { return SharpSheets.Canvas.LineCapStyle.ROUND; }
			else { return SharpSheets.Canvas.LineCapStyle.PROJECTING_SQUARE; } // gsState.lineCapStyle = PenLineCap.Square
		}

		public ISharpGraphicsState SetLineJoinStyle(SharpSheets.Canvas.LineJoinStyle joinStyle) {
			if (joinStyle == SharpSheets.Canvas.LineJoinStyle.MITER) { gsState.lineJoinStyle = PenLineJoin.Miter; }
			else if (joinStyle == SharpSheets.Canvas.LineJoinStyle.ROUND) { gsState.lineJoinStyle = PenLineJoin.Round; }
			else if (joinStyle == SharpSheets.Canvas.LineJoinStyle.BEVEL) { gsState.lineJoinStyle = PenLineJoin.Bevel; }
			return this;
		}
		public SharpSheets.Canvas.LineJoinStyle GetLineJoinStyle() {
			if (gsState.lineJoinStyle == PenLineJoin.Miter) { return SharpSheets.Canvas.LineJoinStyle.MITER; }
			else if (gsState.lineJoinStyle == PenLineJoin.Round) { return SharpSheets.Canvas.LineJoinStyle.ROUND; }
			else { return SharpSheets.Canvas.LineJoinStyle.BEVEL; } // gsState.lineJoinStyle == PenLineJoin.Bevel
		}

		public ISharpGraphicsState SetMiterLimit(float mitreLimit) {
			gsState.mitreLimit = mitreLimit;
			return this;
		}
		public float GetMiterLimit() {
			return (float)gsState.mitreLimit;
		}

		public ISharpGraphicsState SetTextRenderingMode(SharpSheets.Canvas.TextRenderingMode mode) {
			gsState.textRenderingMode = mode;
			return this;
		}
		public SharpSheets.Canvas.TextRenderingMode GetTextRenderingMode() {
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

		PathGeometry? _pathGeometry;
		PathFigure? _pathFigure;

		PathGeometry PathGeometry {
			get {
				if (_pathGeometry == null) {
					_pathGeometry = new PathGeometry() {
						Figures = new PathFigures()
					};
				}
				return _pathGeometry;
			}
		}
		PathFigure PathFigure {
			get {
				if (_pathFigure == null) {
					_pathFigure = new PathFigure() { Segments = new PathSegments() };
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
			if (PathFigure.Segments?.Count > 0) {
				PathGeometry.Figures?.Add(PathFigure);
			}
			PathFigure = new PathFigure() { Segments = new PathSegments(), StartPoint = MakePoint(x, y) };
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas LineTo(float x, float y) {
			PathFigure.Segments?.Add(new LineSegment() { Point = MakePoint(x, y) });
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas CurveTo(float x2, float y2, float x3, float y3) {
			PathFigure.Segments?.Add(new QuadraticBezierSegment() { Point1 = MakePoint(x2, y2), Point2 = MakePoint(x3, y3) });
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas CurveTo(float x1, float y1, float x2, float y2, float x3, float y3) {
			PathFigure.Segments?.Add(new BezierSegment() { Point1 = MakePoint(x1, y1), Point2 = MakePoint(x2, y2), Point3 = MakePoint(x3, y3) });
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas ClosePath() {
			if (PathFigure.Segments?.Count > 0) {
				PathFigure.IsClosed = true;
				PathGeometry.Figures?.Add(PathFigure);
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

		private Geometry? otherGeometry;

		public ISharpCanvas Rectangle(float x, float y, float width, float height) {
			otherGeometry = new RectangleGeometry(new Rect(MakePoint(x, y), MakePoint(x + width, y + height)));
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Ellipse(float x1, float y1, float x2, float y2) {
			otherGeometry = new EllipseGeometry() { Center = MakePoint((x1 + x2) / 2, (y1 + y2) / 2), RadiusX = (x2 - x1) / 2, RadiusY = (y2 - y1) / 2 };
			CurrentPenLocation = null;
			return this;
		}

		private Drawing ClipDrawing(Drawing drawing) {
			if (gsState.clipGeometry != null) {
				DrawingGroup clippedDrawing = new DrawingGroup() { ClipGeometry = gsState.clipGeometry };
				clippedDrawing.Children.Add(drawing);
				return clippedDrawing;
			}
			else {
				return drawing;
			}
		}

		private GeometryDrawing GetDrawing() {
			GeometryDrawing drawing = new GeometryDrawing();
			if (otherGeometry != null) {
				drawing.Geometry = otherGeometry;
			}
			else {
				PathGeometry.Figures?.Add(PathFigure);
				drawing.Geometry = PathGeometry;
			}
			drawing.Geometry.Transform = GetCurrentTransformMatrix();
			return drawing;
		}

		private Pen GetCurrentPen() {
			return new Pen(gsState.strokeBrush, GetLineWidth()) {
				LineJoin = gsState.lineJoinStyle,
				MiterLimit = gsState.mitreLimit,
				LineCap = gsState.lineCapStyle,
				DashStyle = CurrentDashStyle
				//DashCap = PenLineCap.Flat // TODO Necessary?
			};
		}

		public ISharpCanvas Stroke() {
			GeometryDrawing strokeDrawing = GetDrawing();
			strokeDrawing.Pen = GetCurrentPen();
			strokeDrawing.Brush = null;

			Drawing finalDrawing = ClipDrawing(strokeDrawing);
			drawingGroup.Children.Add(finalDrawing);

			EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas Fill() {
			GeometryDrawing fillDrawing = GetDrawing();
			fillDrawing.Pen = null;
			fillDrawing.Brush = gsState.fillBrush;

			Drawing finalDrawing = ClipDrawing(fillDrawing);
			drawingGroup.Children.Add(finalDrawing);

			EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas FillStroke() {
			GeometryDrawing fillStrokeDrawing = GetDrawing();
			fillStrokeDrawing.Pen = GetCurrentPen();
			fillStrokeDrawing.Brush = gsState.fillBrush;

			Drawing finalDrawing = ClipDrawing(fillStrokeDrawing);

			drawingGroup.Children.Add(finalDrawing);

			EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EoFill() {
			PathGeometry.FillRule = FillRule.EvenOdd;
			return Fill();
		}

		public ISharpCanvas EoFillStroke() {
			PathGeometry.FillRule = FillRule.EvenOdd;
			return FillStroke();
		}

		private void AppendClipGeometry(Geometry geometry) {
			if (gsState.clipGeometry == null) {
				gsState.clipGeometry = geometry;
			}
			else {
				gsState.clipGeometry = new CombinedGeometry(GeometryCombineMode.Intersect, gsState.clipGeometry, geometry);
			}
		}

		public ISharpCanvas Clip() {
			Geometry newClip;
			if (otherGeometry != null) {
				newClip = otherGeometry;
			}
			else {
				ClosePath();
				PathGeometry.Figures?.Add(PathFigure);
				newClip = PathGeometry;
			}
			newClip.Transform = GetCurrentTransformMatrix();
			AppendClipGeometry(newClip);
			EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EoClip() {
			PathGeometry.FillRule = FillRule.EvenOdd;
			return Clip();
		}

		public ISharpGraphicsState SetTransform(SharpSheets.Canvas.Transform transform) {
			gsState.pagetransform = GetBaseTransform(CanvasRect) * transform;
			return this;
		}

		public ISharpGraphicsState ApplyTransform(SharpSheets.Canvas.Transform transform) {
			gsState.pagetransform *= transform;
			return this;
		}

		public SharpSheets.Canvas.Transform GetTransform() {
			return gsState.pagetransform;
		}

		private MatrixTransform GetCurrentTransformMatrix(SharpSheets.Canvas.Transform? append = null) {
			SharpSheets.Canvas.Transform current = avaloniaTransform * GetTransform();
			if (append != null) { current *= append; }
			return new MatrixTransform(new Matrix(current.a, current.b, current.c, current.d, current.e, current.f));
		}

		#endregion

		#region Text

		private static Geometry? GetGlyphGeometry(ushort glyph, TrueTypeFontFileOutlines glyphOutlines, float fontsize) {
			// Create a geometry for the glyph
			PathGeometry geometry = new PathGeometry() { FillRule = FillRule.NonZero };

			double ProcessShort(short value) {
				return (fontsize * (1000.0 * (value / (double)glyphOutlines.UnitsPerEm))) / 1000.0;
			}

			if (glyphOutlines.glyf?.glyphOutlines[glyph] is TrueTypeGlyphOutline gOutline) {
				PathFigures figures = new PathFigures();
				geometry.Figures = figures;

				int p = 0, c = 0;
				bool first = true;
				while (p < gOutline.PointCount) {
					if (gOutline.onCurve[p]) {
						double x = ProcessShort(gOutline.xCoordinates[p]);
						double y = ProcessShort(gOutline.yCoordinates[p]);

						if (first) {
							figures.Add(new PathFigure() { StartPoint = new Point(x, y), IsClosed = true, IsFilled = true, Segments = new PathSegments() });
							first = false;
						}
						else if (p > 0 && !gOutline.onCurve[p - 1]) {
							double cx = ProcessShort(gOutline.xCoordinates[p - 1]);
							double cy = ProcessShort(gOutline.yCoordinates[p - 1]);

							figures[^1].Segments?.Add(new QuadraticBezierSegment() { Point1 = new Point(cx, cy), Point2 = new Point(x, y) });
						}
						else {
							figures[^1].Segments?.Add(new LineSegment() { Point = new Point(x, y) });
						}
					}

					if (c < gOutline.endPtsOfContours.Count && p == gOutline.endPtsOfContours[c]) {
						c += 1;
						first = true;
					}

					p += 1;
				}
			}
			else if (glyphOutlines.cff?.glyphs[glyph] is Type2Glyph g2Outline) {
				PathFigures figures = new PathFigures();
				geometry.Figures = figures;

				int p = 0, c = 0;
				bool first = true;
				while (p < g2Outline.PointCount) {
					if (g2Outline.OnCurve[p]) {
						double x = ProcessShort(g2Outline.Xs[p]);
						double y = ProcessShort(g2Outline.Ys[p]);

						if (first) {
							figures.Add(new PathFigure() { StartPoint = new Point(x, y), IsClosed = true, IsFilled = true, Segments = new PathSegments() });
							first = false;
						}
						else if (p > 1 && !g2Outline.OnCurve[p - 1]) {
							double cx1 = ProcessShort(g2Outline.Xs[p - 2]);
							double cy1 = ProcessShort(g2Outline.Ys[p - 2]);
							double cx2 = ProcessShort(g2Outline.Xs[p - 1]);
							double cy2 = ProcessShort(g2Outline.Ys[p - 1]);

							figures[^1].Segments?.Add(new BezierSegment() { Point1 = new Point(cx1, cy1), Point2 = new Point(cx2, cy2), Point3 = new Point(x, y) });
						}
						else {
							figures[^1].Segments?.Add(new LineSegment() { Point = new Point(x, y) });
						}
					}

					if (c < g2Outline.EndPtsOfContours.Count && p == g2Outline.EndPtsOfContours[c]) {
						c += 1;
						first = true;
					}

					p += 1;
				}
			}

			return geometry;
		}

		private bool FillText {
			get {
				SharpSheets.Canvas.TextRenderingMode current = gsState.textRenderingMode;
				return (current & SharpSheets.Canvas.TextRenderingMode.FILL) == SharpSheets.Canvas.TextRenderingMode.FILL;
			}
		}

		private bool StrokeText {
			get {
				SharpSheets.Canvas.TextRenderingMode current = gsState.textRenderingMode;
				return (current & SharpSheets.Canvas.TextRenderingMode.STROKE) == SharpSheets.Canvas.TextRenderingMode.STROKE;
			}
		}

		private bool ClipText {
			get {
				SharpSheets.Canvas.TextRenderingMode current = gsState.textRenderingMode;
				return (current & SharpSheets.Canvas.TextRenderingMode.CLIP) == SharpSheets.Canvas.TextRenderingMode.CLIP;
			}
		}

		public ISharpCanvas DrawText(string text1, float x, float y) {
			if (gsState.fontsize <= 0) {
				// TODO Throw error?
				return this; // Don't bother drawing if text is invisible
			}

			Point point = MakePoint(x, y);
			GeometryGroup textGeometryGroup = new GeometryGroup() { FillRule = FillRule.NonZero };

			SolidColorBrush? brush = new SolidColorBrush(ConvertColor(GetTextColor()));

			//Pen? pen = StrokeText ? new Pen(gsState.strokeBrush, GetLineWidth()) { DashStyle = CurrentDashStyle } : null;
			Pen? pen = StrokeText ? GetCurrentPen() : null;
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

				Geometry? glyphGeometry = GetGlyphGeometry(g, glyphOutlines, fontsize);

				if (glyphGeometry is not null) {
					(short xPlacement, short yPlacement) = glyphRun.GetPlacement(i);
					float xPlaceSized = PdfFont.ConvertDesignSpaceValue(xPlacement, fontsize);
					float yPlaceSized = PdfFont.ConvertDesignSpaceValue(yPlacement, fontsize);
					glyphGeometry.Transform = new TranslateTransform(runningX + xPlaceSized, yPlaceSized);
					textGeometryGroup.Children.Add(glyphGeometry);
				}

				//runningX += pdfFont.GetWidth(g, fontsize);
				runningX += PdfFont.ConvertDesignSpaceValue(glyphRun.GetAdvanceTotal(i).xAdvance, fontsize);
			}

			//textGeometryGroup.Transform = GetCurrentTransformMatrix(SharpSheets.Canvas.Transform.Translate((float)point.X, (float)point.Y) * SharpSheets.Canvas.Transform.Scale(1, -1));
			textGeometryGroup.Transform = GetCurrentTransformMatrix(SharpSheets.Canvas.Transform.Translate((float)point.X, (float)point.Y));

			GeometryDrawing textDrawing = new GeometryDrawing() { Brush = brush, Pen = pen, Geometry = textGeometryGroup };

			if ((gsState.textRenderingMode & SharpSheets.Canvas.TextRenderingMode.FILL_STROKE) != SharpSheets.Canvas.TextRenderingMode.INVISIBLE) {
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

		public string? TextField(SharpSheets.Layouts.Rectangle rect, string name, string? tooltip, TextFieldType fieldType, string? value, TextFormat font, float fontSize, SharpSheets.Colors.Color color, bool multiline, bool rich, Justification justification, int maxLen = -1) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);
				Rectangle pageSpaceRect = this.GetPageSpaceRect(rect);
				Point rectPoint = MakePoint(pageSpaceRect.X, pageSpaceRect.Y);
				Fields.Add(fieldName, new AvaloniaCanvasField(fieldName, tooltip, new SharpSheets.Layouts.Rectangle((float)rectPoint.X, (float)rectPoint.Y, pageSpaceRect.Width, pageSpaceRect.Height), FieldType.TEXT));
				return fieldName;
			}
			else {
				return null;
			}
		}

		public string? CheckField(SharpSheets.Layouts.Rectangle rect, string name, string? tooltip, CheckType checkType, SharpSheets.Colors.Color color) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);
				Rectangle pageSpaceRect = this.GetPageSpaceRect(rect);
				Point rectPoint = MakePoint(pageSpaceRect.X, pageSpaceRect.Y);
				Fields.Add(fieldName, new AvaloniaCanvasField(fieldName, tooltip, new SharpSheets.Layouts.Rectangle((float)rectPoint.X, (float)rectPoint.Y, pageSpaceRect.Width, pageSpaceRect.Height), FieldType.CHECK));
				return fieldName;
			}
			else {
				return null;
			}
		}

		public string? ImageField(SharpSheets.Layouts.Rectangle rect, string name, string? tooltip, CanvasImageData? defaultImage = null) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);
				Rectangle pageSpaceRect = this.GetPageSpaceRect(rect);
				Point rectPoint = MakePoint(pageSpaceRect.X, pageSpaceRect.Y);
				Fields.Add(fieldName, new AvaloniaCanvasField(fieldName, tooltip, new SharpSheets.Layouts.Rectangle((float)rectPoint.X, (float)rectPoint.Y, pageSpaceRect.Width, pageSpaceRect.Height), FieldType.IMAGE));

				if (defaultImage != null) {
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

		static IImage BitmapFromPath(string filename) {
			return new Avalonia.Media.Imaging.Bitmap(filename);
		}

		private void AddImage(IImage image, Rectangle rect, float? aspect) {
			if (image != null) {
				float imageAspect = (float)(aspect ?? image.Size.Width / image.Size.Height);
				if (imageAspect > 0) {
					rect = rect.Aspect(imageAspect);
				}

				Point point = MakePoint(rect.X, rect.Y + rect.Height);
				ImageDrawing drawing = new ImageDrawing() { ImageSource = image, Rect = new Rect(new Point(0, 0), new Avalonia.Size(rect.Width, rect.Height)) };

				DrawingGroup imageDrawingGroup = new DrawingGroup();
				imageDrawingGroup.Children.Add(drawing);
				imageDrawingGroup.Transform = GetCurrentTransformMatrix(SharpSheets.Canvas.Transform.Translate((float)point.X, (float)point.Y) * SharpSheets.Canvas.Transform.Scale(1, -1));


				Drawing clippedImageDrawing = ClipDrawing(imageDrawingGroup);

				drawingGroup.Children.Add(clippedImageDrawing);
			}
			else {
				if (aspect != null) {
					rect = rect.Aspect(aspect.Value);
				}
				this.SaveState();
				this.SetFillColor(SharpSheets.Colors.Color.Red);
				this.Rectangle(rect).Fill();
				this.RestoreState();
			}
		}

		public ISharpCanvas AddImage(CanvasImageData image, SharpSheets.Layouts.Rectangle rect, float? imageAspect) {
			if (image.IsPdf) {
				this.SaveState();
				this.SetFillColor(SharpSheets.Utilities.ColorUtils.WithOpacity(SharpSheets.Colors.Color.LightGray, 0.58f));
				this.Rectangle(rect).Fill();
				this.SetTextColor(SharpSheets.Utilities.ColorUtils.WithOpacity(SharpSheets.Colors.Color.DarkGray, 0.58f));
				this.FitRichTextLine(rect, (RichString)"PDF", new ParagraphSpecification(1f, 0f, default), new FontSizeSearchParams(0.5f, 100f, 0.1f), Justification.CENTRE, Alignment.CENTRE, TextHeightStrategy.AscentDescent);
				this.RestoreState();
			}
			else {
				IImage imageObj = BitmapFromPath(image.Path.Path);
				AddImage(imageObj, rect, imageAspect);
			}
			return this;
		}

		public float GetUserUnits() {
			return 1f; // Implement?
		}

		#endregion
	}

}
