using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using PathSegmentCollection = System.Windows.Media.PathSegmentCollection;
using FormattedText = System.Windows.Media.FormattedText;
using Typeface = System.Windows.Media.Typeface;
using System.Windows;
using System.Globalization;
using System.Windows.Media;
using System.Linq;
using System.Windows.Media.Imaging;
using System.IO;
using SharpSheets.Utilities;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Fonts;
using SharpSheets.Exceptions;

namespace SharpEditor {

	public class SharpWPFDrawingDocument : ISharpDocument {

		public IList<SharpWPFDrawingCanvas> Pages { get; }
		public int PageCount { get { return Pages.Count; } }

		public IReadOnlyCollection<string> FieldNames { get { return new HashSet<string>(Pages.SelectMany(p => p.FieldNames)); } }

		public SharpWPFDrawingDocument() {
			Pages = new List<SharpWPFDrawingCanvas>();
		}

		ISharpCanvas ISharpDocument.AddNewPage(SharpSheets.Layouts.Size pageSize) => AddNewPage(pageSize);

		public SharpWPFDrawingCanvas AddNewPage(SharpSheets.Layouts.Size pageSize) {
			SharpWPFDrawingCanvas page = new SharpWPFDrawingCanvas(this, pageSize);
			Pages.Add(page);
			return page;
		}

		public void Freeze() {
			foreach (SharpWPFDrawingCanvas canvas in Pages) { canvas.Freeze(); }
		}

		public int? GetOwnerPage(object owner) {
			for(int i=0; i<Pages.Count; i++) {
				if (Pages[i].ContainsAreaOwner(owner)) {
					// This is perhaps imperfect, as there is currently no guarantee that an owner only have areas on one page
					// But it should do for now, as they generally should
					return i;
				}
			}
			return null;
		}

	}

	public class SharpWPFDrawingGraphicsState {

		public float defaultLineWidth = 1f;
		public float lineWidth = 1f;

		//public float[] strokeDasheValues = null;
		//public float strokeDashOffset = 0f;
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

		public SharpWPFDrawingGraphicsState() {
			strokeBrush = new SolidColorBrush(Colors.Black);
			strokeBrush.Freeze();

			fillBrush = new SolidColorBrush(Colors.White);
			fillBrush.Freeze();

			typefaces = new TypefaceGrouping();
		}
		public SharpWPFDrawingGraphicsState(SharpWPFDrawingGraphicsState source) {
			this.defaultLineWidth = source.defaultLineWidth;
			this.lineWidth = source.lineWidth;
			//this.strokeDasheValues = source.strokeDasheValues;
			//this.strokeDashOffset = source.strokeDashOffset;
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

	public struct WPFCanvasField {
		public string Name { get; private set; }
		public string? Tooltip { get; private set; }
		public SharpSheets.Layouts.Rectangle Rect { get; private set; }
		public FieldType Type { get; private set; }
		public WPFCanvasField(string name, string? tooltip, SharpSheets.Layouts.Rectangle rect, FieldType type) {
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

	public class SharpWPFDrawingCanvas : ISharpCanvas {

		public SharpWPFDrawingDocument Document { get; }
		public SharpSheets.Layouts.Rectangle CanvasRect { get; }

		public readonly DrawingGroup drawingGroup;

		private SharpWPFDrawingGraphicsState gsState;
		private readonly Stack<SharpWPFDrawingGraphicsState> gsStack;

		public DrawPoint? CurrentPenLocation { get; private set; } = null;

		public IReadOnlyCollection<string> DocumentFieldNames { get { return Document.FieldNames; } }
		public IReadOnlyCollection<string> FieldNames { get { return Fields.Keys; } }
		public Dictionary<string, WPFCanvasField> Fields { get; }

		//private readonly Dictionary<object, List<Rectangle>> areas;
		private readonly Dictionary<object, List<RegisteredAreas>> areas;

		private readonly SharpSheets.Canvas.Transform wpfTransform;

		public SharpWPFDrawingCanvas(SharpWPFDrawingDocument document, SharpSheets.Layouts.Size pageSize) {
			Document = document;
			CanvasRect = (Rectangle)pageSize;

			drawingGroup = new DrawingGroup();

			Fields = new Dictionary<string, WPFCanvasField>();
			
			//areas = new Dictionary<object, List<Rectangle>>(new IdentityEqualityComparer<object>());
			areas = new Dictionary<object, List<RegisteredAreas>>(new IdentityEqualityComparer<object>());

			gsState = new SharpWPFDrawingGraphicsState();
			gsStack = new Stack<SharpWPFDrawingGraphicsState>();

			wpfTransform = GetBaseTransform(CanvasRect);

			this.Rectangle(CanvasRect).Fill();

			this.drawingErrors = new List<SharpDrawingException>();
		}

		public void Freeze() {
			drawingGroup.Freeze();
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
			//areas[owner].Add(area);
			areas[owner].Add(new RegisteredAreas(originalArea, adjustedArea, innerAreas));
		}

		public Dictionary<object, RegisteredAreas> GetAreas(Point point) {
			return areas
				.Where(kv => kv.Value.Any(r => r.Contains((float)point.X, (float)(CanvasRect.Height - point.Y))))
				.ToDictionary(kv => kv.Key, kv => kv.Value.MaxBy(r => r.Total.Area)!);

			/*
			return areas
				.Where(kv => kv.Value.Any(r => r.Contains((float)point.X, (float)(CanvasRect.Height - point.Y))))
				.ToDictionary(kv => kv.Key, kv => kv.Value.MinBy(r => r.Area)!);
			*/
				
			//return Areas.Where(kv => kv.Value.Contains((float)point.X, (float)(CanvasRect.Height - point.Y))).ToDictionary();
		}

		public IEnumerable<RegisteredAreas> GetAreas(object owner) {
			if(areas.TryGetValue(owner, out List<RegisteredAreas>? rectangles)) {
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
			//return new Point(x, CanvasRect.Height - y);
			return new Point(x, y);
		}

		Rect ConvertRect(SharpSheets.Layouts.Rectangle rect) {
			Point topLeft = MakePoint(rect.Left, rect.Top);
			return new Rect(topLeft, new System.Windows.Size(rect.Width, rect.Height));
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
			gsState = new SharpWPFDrawingGraphicsState(gsState);
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

		/*
		public SharpSheets.Colors.Color GetStrokeColor() {
			return gsState.strokeColor;
		}
		public SharpSheets.Colors.Color GetFillColor() {
			return gsState.fillColor;
		}
		*/

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
			brush.Freeze();
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

			GradientStopCollection gradientStops = new GradientStopCollection(stops.Select(s => new GradientStop(ConvertColor(s.Color), s.Stop)));
			LinearGradientBrush brush = new LinearGradientBrush(gradientStops, MakePoint(x1, y1), MakePoint(x2, y2)) {
				SpreadMethod = GradientSpreadMethod.Pad,
				MappingMode = BrushMappingMode.Absolute,
				Transform = GetCurrentTransformMatrix()
			};
			brush.Freeze();

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

			List<GradientStop> wpfStops = new List<GradientStop>();
			foreach (ColorStop stop in stops) {
				float finalStop = rStartFactor + stop.Stop * rRangeFactor;
				wpfStops.Add(new GradientStop(ConvertColor(stop.Color), finalStop));
			}

			//GradientStopCollection gradientStops = new GradientStopCollection(stops.Select(s => new GradientStop(ConvertColor(s.Color), s.Stop)));
			GradientStopCollection gradientStops = new GradientStopCollection(wpfStops);
			RadialGradientBrush brush = new RadialGradientBrush(gradientStops) {
				SpreadMethod = GradientSpreadMethod.Pad,
				MappingMode = BrushMappingMode.Absolute,
				Center = MakePoint(x1, y1),
				GradientOrigin = MakePoint(x2, y2),
				RadiusX = r1,
				RadiusY = r1,
				Transform = GetCurrentTransformMatrix()
			};
			brush.Freeze();

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
			//gsState.strokeDasheValues = values;
			//gsState.strokeDashOffset = offset;
			gsState.strokeDash = strokeDash;
			return this;
		}

		private DashStyle CurrentDashStyle {
			get {
				return gsState.strokeDash != null ? new DashStyle(gsState.strokeDash.Values.Select(f => (double)f / gsState.lineWidth), gsState.strokeDash.Offset) : DashStyles.Solid;
			}
		}
		#endregion

		#region Font

		public ISharpGraphicsState SetFont(TextFormat format, FontPath? font) {
			if (font?.Path != null) {
				gsState.typefaces.SetFont(format, font);
				//Console.WriteLine($"Setting {format} to {font.Path}");
			}
			else {
				gsState.typefaces.SetNull(format);
				//Console.WriteLine($"Setting {format} to null");
			}
			return this;
		}

		public FontPathGrouping GetFonts() {
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
						Figures = new PathFigureCollection()
					};
				}
				return _pathGeometry;
			}
			/*
			set {
				_pathFigure = null;
				_pathGeometry = value;
			}
			*/
		}
		PathFigure PathFigure {
			get {
				if (_pathFigure == null) {
					_pathFigure = new PathFigure() { Segments = new PathSegmentCollection() };
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

		/*
		void NewPath() {
			pathSegments = new PathSegmentCollection();
			pathFigure = new System.Windows.Media.PathFigure() { Segments = pathSegments };
			pathGeometry = new System.Windows.Media.PathGeometry() {
				Figures = new System.Windows.Media.PathFigureCollection() { pathFigure }
			};
			currentPath = new Path() { Data = pathGeometry };
		}
		*/

		public ISharpCanvas MoveTo(float x, float y) {
			if (PathFigure.Segments.Count > 0) {
				PathGeometry.Figures.Add(PathFigure);
			}
			PathFigure = new PathFigure() { Segments = new PathSegmentCollection(), StartPoint = MakePoint(x, y) };
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas LineTo(float x, float y) {
			PathFigure.Segments.Add(new LineSegment(MakePoint(x, y), true));
			CurrentPenLocation = new DrawPoint(x, y);
			return this;
		}

		public ISharpCanvas CurveTo(float x2, float y2, float x3, float y3) {
			PathFigure.Segments.Add(new QuadraticBezierSegment(MakePoint(x2, y2), MakePoint(x3, y3), true));
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas CurveTo(float x1, float y1, float x2, float y2, float x3, float y3) {
			PathFigure.Segments.Add(new BezierSegment(MakePoint(x1, y1), MakePoint(x2, y2), MakePoint(x3, y3), true));
			CurrentPenLocation = new DrawPoint(x3, y3);
			return this;
		}

		public ISharpCanvas ClosePath() {
			if (PathFigure.Segments.Count > 0) {
				PathFigure.IsClosed = true;
				PathGeometry.Figures.Add(PathFigure);
			}
			ResetPathFigure(); // PathFigure = null;
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas EndPath() {
			ResetPathGeometry(); // PathGeometry = null;
			otherGeometry = null;
			CurrentPenLocation = null;
			return this;
		}

		private Geometry? otherGeometry;

		public ISharpCanvas Rectangle(float x, float y, float width, float height) {
			//MoveTo(x, y).LineTo(x + width, y).LineTo(x + width, y + height).LineTo(x, y + height).ClosePath();
			otherGeometry = new RectangleGeometry(new Rect(MakePoint(x, y), MakePoint(x + width, y + height)));
			CurrentPenLocation = null;
			return this;
		}

		/*
		public ISharpCanvas RoundRectangle(float x, float y, float width, float height, float radiusX, float radiusY) {
			otherGeometry = new RectangleGeometry(new Rect(MakePoint(x, y), MakePoint(x + width, y + height))) { RadiusX = radiusX, RadiusY = radiusY };
			CurrentPenLocation = null;
			return this;
		}
		*/

		public ISharpCanvas Ellipse(float x1, float y1, float x2, float y2) {
			otherGeometry = new EllipseGeometry(MakePoint((x1 + x2) / 2, (y1 + y2) / 2), (x2 - x1) / 2, (y2 - y1) / 2);
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
				PathGeometry.Figures.Add(PathFigure);
				drawing.Geometry = PathGeometry;
			}
			drawing.Geometry.Transform = GetCurrentTransformMatrix();
			return drawing;
		}

		private Pen GetCurrentPen() {
			return new Pen(gsState.strokeBrush, GetLineWidth()) {
				LineJoin = gsState.lineJoinStyle,
				MiterLimit = gsState.mitreLimit,
				StartLineCap = gsState.lineCapStyle,
				EndLineCap = gsState.lineCapStyle,
				DashStyle = CurrentDashStyle,
				DashCap = PenLineCap.Flat
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
			fillDrawing.Brush = gsState.fillBrush; // new SolidColorBrush(ConvertColor(GetFillColor()));

			Drawing finalDrawing = ClipDrawing(fillDrawing);
			drawingGroup.Children.Add(finalDrawing);

			EndPath();
			CurrentPenLocation = null;
			return this;
		}

		public ISharpCanvas FillStroke() {
			GeometryDrawing fillStrokeDrawing = GetDrawing();
			fillStrokeDrawing.Pen = GetCurrentPen();
			fillStrokeDrawing.Brush = gsState.fillBrush; // new SolidColorBrush(ConvertColor(GetFillColor()));

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
				PathGeometry.Figures.Add(PathFigure);
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
			SharpSheets.Canvas.Transform current = wpfTransform * GetTransform();
			if (append != null) { current *= append; }
			return new MatrixTransform(current.a, current.b, current.c, current.d, current.e, current.f);
		}

		#endregion

		#region Text

		private FormattedText GetFormattedText(string text, TextFormat font, float fontsize, SolidColorBrush? brush, out Typeface typeface) {

			/*
			FontStyle style = FontStyles.Normal;
			FontWeight weight = FontWeights.Normal;
			if (font == TextFormat.BOLD) {
				style = FontStyles.Normal;
				weight = FontWeights.Bold;
			}
			else if (font == TextFormat.ITALIC) {
				style = FontStyles.Italic;
				weight = FontWeights.Normal;
			}
			else if (font == TextFormat.BOLDITALIC) {
				style = FontStyles.Italic;
				weight = FontWeights.Bold;
			}
			// else Normal, leave defaults
			*/

			//typeface = new Typeface(fontFamily, style, weight, FontStretches.Normal);
			typeface = gsState.typefaces.GetTypeface(font);
			FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture,
				FlowDirection.LeftToRight, typeface, fontsize, brush,
				96f); // PixelsPerDip // TODO Make this actually use proper value - might need to be imported from outside canvas at instantiation
			return formattedText;
		}

		private bool FillText {
			get {
				SharpSheets.Canvas.TextRenderingMode current = gsState.textRenderingMode;
				//return current == SharpSheets.Canvas.TextRenderingMode.FILL || current == SharpSheets.Canvas.TextRenderingMode.FILL_STROKE || current == SharpSheets.Canvas.TextRenderingMode.FILL_CLIP || current == SharpSheets.Canvas.TextRenderingMode.FILL_STROKE_CLIP;
				return (current & SharpSheets.Canvas.TextRenderingMode.FILL) == SharpSheets.Canvas.TextRenderingMode.FILL;
			}
		}

		private bool StrokeText {
			get {
				SharpSheets.Canvas.TextRenderingMode current = gsState.textRenderingMode;
				//return current == SharpSheets.Canvas.TextRenderingMode.STROKE || current == SharpSheets.Canvas.TextRenderingMode.FILL_STROKE || current == SharpSheets.Canvas.TextRenderingMode.STROKE_CLIP || current == SharpSheets.Canvas.TextRenderingMode.FILL_STROKE_CLIP;
				return (current & SharpSheets.Canvas.TextRenderingMode.STROKE) == SharpSheets.Canvas.TextRenderingMode.STROKE;
			}
		}

		private bool ClipText {
			get {
				SharpSheets.Canvas.TextRenderingMode current = gsState.textRenderingMode;
				//return current == SharpSheets.Canvas.TextRenderingMode.CLIP || current == SharpSheets.Canvas.TextRenderingMode.FILL_CLIP || current == SharpSheets.Canvas.TextRenderingMode.STROKE_CLIP || current == SharpSheets.Canvas.TextRenderingMode.FILL_STROKE_CLIP;
				return (current & SharpSheets.Canvas.TextRenderingMode.CLIP) == SharpSheets.Canvas.TextRenderingMode.CLIP;
			}
		}

		public ISharpCanvas DrawText(string text, float x, float y) {
			if (gsState.fontsize <= 0) {
				// TODO Throw error?
				return this; // Don't bother drawing if text is invisible
			}

			Point point = MakePoint(x, y);
			GeometryGroup textGeometryGroup = new GeometryGroup();

			SolidColorBrush? brush = new SolidColorBrush(ConvertColor(GetTextColor()));

			Pen? pen = StrokeText ? new Pen(gsState.strokeBrush, GetLineWidth()) { DashStyle = CurrentDashStyle } : null;
			brush = FillText ? brush : null;

			// Draw each character individually, as there is no way to turn off kerning in WPF
			float runningX = 0f;
			for (int i = 0; i < text.Length; i++) {
				string c = text[i].ToString();

				if (!string.IsNullOrWhiteSpace(c)) {
					FormattedText formatted = GetFormattedText(c, gsState.font, gsState.fontsize, brush, out _);

					Geometry textGeometry = formatted.BuildGeometry(new Point(runningX, -formatted.Baseline));

					textGeometryGroup.Children.Add(textGeometry);
				}

				runningX += GetWidth(c, gsState.font, gsState.fontsize); // This will need to be adjusted when GeboPDF kerning is implemented
			}

			textGeometryGroup.Transform = GetCurrentTransformMatrix(SharpSheets.Canvas.Transform.Translate((float)point.X, (float)point.Y) * SharpSheets.Canvas.Transform.Scale(1, -1));

			GeometryDrawing textDrawing = new GeometryDrawing(brush, pen, textGeometryGroup);

			if ((gsState.textRenderingMode & SharpSheets.Canvas.TextRenderingMode.FILL_STROKE) != SharpSheets.Canvas.TextRenderingMode.INVISIBLE) {
				drawingGroup.Children.Add(textDrawing);
			}

			if (ClipText) {
				AppendClipGeometry(textGeometryGroup);
			}

			CurrentPenLocation = null;

			return this;
		}

		/*
		public ISharpCanvas DrawText(string text, float x, float y) {
			//Console.WriteLine($"Text color: {GetTextColor()} ({ConvertColor(GetTextColor())})");

			if(gsState.fontsize <= 0) {
				// TODO Throw error?
				return this; // Don't bother drawing if text is invisible
			}

			SolidColorBrush? brush = new SolidColorBrush(ConvertColor(GetTextColor()));
			FormattedText formatted = GetFormattedText(text, gsState.font, gsState.fontsize, brush, out _);

			Pen? pen = StrokeText ? new Pen(new SolidColorBrush(ConvertColor(gsState.strokeColor)), GetLineWidth()) { DashStyle = CurrentDashStyle } : null;
			brush = FillText ? brush : null;

			//Point point = MakePoint(x, y + (float)formatted.Baseline);
			//Geometry textGeometry = formatted.BuildGeometry(point);
			Point point = MakePoint(x, y + (float)formatted.Baseline);
			Geometry textGeometry = formatted.BuildGeometry(new Point(0, 0));
			if(!string.IsNullOrWhiteSpace(text)) {
				textGeometry.Transform = GetCurrentTransformMatrix(SharpSheets.Canvas.Transform.Translate((float)point.X, (float)point.Y) * SharpSheets.Canvas.Transform.Scale(1, -1));
			}

			GeometryDrawing textDrawing = new GeometryDrawing(brush, pen, textGeometry);

			if (gsState.textRenderingMode != SharpSheets.Canvas.TextRenderingMode.INVISIBLE) {
				drawingGroup.Children.Add(textDrawing);
			}

			if (ClipText) {
				AppendClipGeometry(textGeometry);
			}

			//SaveState();
			//SetLineWidth(0.1f);
			//SetStrokeColor(SharpSheets.Colors.Color.Red);
			//this.MoveTo(x, y).LineTo(x + this.GetWidth(text), y).Stroke();
			//float ascent = this.GetAscent(text);
			//SetStrokeColor(SharpSheets.Colors.Color.Green);
			//this.MoveTo(x, y + ascent).LineTo(x + this.GetWidth(text), y + ascent).Stroke();
			//float descent = this.GetDescent(text);
			//SetStrokeColor(SharpSheets.Colors.Color.Blue);
			//this.MoveTo(x, y + descent).LineTo(x + this.GetWidth(text), y + descent).Stroke();
			//RestoreState();

			CurrentPenLocation = null;

			return this;
		}
		*/

		public float GetAscent(string text, TextFormat format, float fontsize) {
			/*
			SolidColorBrush brush = new SolidColorBrush(ConvertColor(GetTextColor()));
			FormattedText formatted = GetFormattedText(text, format, fontsize, brush, out Typeface typeface);
			return (float)formatted.Baseline;
			*/
			return gsState.typefaces.GetAscent(text, format, fontsize);
		}

		public float GetDescent(string text, TextFormat format, float fontsize) {
			/*
			SolidColorBrush brush = new SolidColorBrush(ConvertColor(GetTextColor()));
			FormattedText formatted = GetFormattedText(text, format, fontsize, brush, out Typeface typeface);
			return -(float)formatted.OverhangAfter;
			*/
			return gsState.typefaces.GetDescent(text, format, fontsize);
		}

		public float GetWidth(string text, TextFormat format, float fontsize) {
			/*
			SolidColorBrush brush = new SolidColorBrush(ConvertColor(GetTextColor()));
			FormattedText formatted = GetFormattedText(text, format, fontsize, brush, out _);
			return (float)formatted.WidthIncludingTrailingWhitespace;
			*/
			return gsState.typefaces.GetWidth(text, format, fontsize);
		}

		#endregion

		#region Fields

		public string? TextField(SharpSheets.Layouts.Rectangle rect, string name, string? tooltip, TextFieldType fieldType, string? value, TextFormat font, float fontSize, SharpSheets.Colors.Color color, bool multiline, bool rich, Justification justification, int maxLen = -1) {
			if (IsFieldsEnabled()) {
				string fieldName = this.GetAvailableFieldName(name);
				Rectangle pageSpaceRect = this.GetPageSpaceRect(rect);
				Point rectPoint = MakePoint(pageSpaceRect.X, pageSpaceRect.Y);
				Fields.Add(fieldName, new WPFCanvasField(fieldName, tooltip, new SharpSheets.Layouts.Rectangle((float)rectPoint.X, (float)rectPoint.Y, pageSpaceRect.Width, pageSpaceRect.Height), FieldType.TEXT));
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
				Fields.Add(fieldName, new WPFCanvasField(fieldName, tooltip, new SharpSheets.Layouts.Rectangle((float)rectPoint.X, (float)rectPoint.Y, pageSpaceRect.Width, pageSpaceRect.Height), FieldType.CHECK));
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
				Fields.Add(fieldName, new WPFCanvasField(fieldName, tooltip, new SharpSheets.Layouts.Rectangle((float)rectPoint.X, (float)rectPoint.Y, pageSpaceRect.Width, pageSpaceRect.Height), FieldType.IMAGE));

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

		static ImageSource BitmapFromUri(Uri source) {
			BitmapImage bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.UriSource = source;
			bitmap.CacheOption = BitmapCacheOption.OnLoad;
			bitmap.EndInit();
			return bitmap;
		}

		private void AddImage(ImageSource image, Rectangle rect, float? aspect) {
			if (image != null) {
				float imageAspect = (float)(aspect ?? image.Width / image.Height);
				if (imageAspect > 0) {
					rect = rect.Aspect(imageAspect);
				}

				Point point = MakePoint(rect.X, rect.Y + rect.Height);
				ImageDrawing drawing = new ImageDrawing(image, new Rect(new Point(0, 0), new System.Windows.Size(rect.Width, rect.Height)));
				
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

		/*
		public ISharpCanvas AddImage(System.Drawing.Image image, Rectangle rect, float? aspect) {
			ImageSource imageSource = image.ToImageSource();
			AddImage(imageSource, rect, aspect);
			return this;
		}
		*/

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
				ImageSource imageSource = BitmapFromUri(new Uri(image.Path.Path));
				AddImage(imageSource, rect, imageAspect);
			}
			/*
			else {
				if (imageAspect != null) {
					rect = rect.Aspect(imageAspect.Value);
				}
				this.SaveState();
				this.SetFillColor(SharpSheets.Colors.Color.Red);
				this.Rectangle(rect).Fill();
				this.RestoreState();
			}
			*/
			return this;
		}

		/*
		public ISharpCanvas AddImage(ImageData imageData, SharpSheets.Layouts.Rectangle rect, float? imageAspect) {
			ImageSource imageSource = BitmapFromUri(new Uri(imageData.Path.Path));
			AddImage(imageSource, rect, imageAspect);
			return this;
		}
		*/

		public float GetUserUnits() {
			return 1f; // TODO Implement
		}

		#endregion
	}

	/*
	public static class WPFCanvasUtils {

		public static ImageSource ToImageSource(this System.Drawing.Image image) {
			BitmapImage bitmapImage;
			using (MemoryStream memory = new MemoryStream()) {
				image.Save(memory, ImageFormat.Bmp);
				memory.Position = 0;
				bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.StreamSource = memory;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
			}
			return bitmapImage;
		}

	}
	*/
}
