using GeboPdf.Fonts.TrueType;
using SharpSheets.Canvas;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.Designer.DrawingCanvas {

	public static class ConverterUtils {

		public static Avalonia.Media.Color Convert(SharpSheets.Colors.Color color) {
			return Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
		}

		[return: NotNullIfNotNull(nameof(transform))]
		public static Avalonia.Media.Transform? Convert(Transform? transform) {
			if (transform is null) { return null; }
			return new Avalonia.Media.MatrixTransform(new Avalonia.Matrix(transform.a, transform.b, transform.c, transform.d, transform.e, transform.f));
		}

		public static Avalonia.Media.PenLineCap Convert(LineCapStyle capStyle) {
			if (capStyle == LineCapStyle.BUTT) { return Avalonia.Media.PenLineCap.Flat; }
			else if (capStyle == LineCapStyle.ROUND) { return Avalonia.Media.PenLineCap.Round; }
			else if (capStyle == LineCapStyle.PROJECTING_SQUARE) { return Avalonia.Media.PenLineCap.Square; }
			else { return Avalonia.Media.PenLineCap.Flat; } // Fallback
		}

		public static Avalonia.Media.PenLineJoin Convert(LineJoinStyle joinStyle) {
			if (joinStyle == LineJoinStyle.MITER) { return Avalonia.Media.PenLineJoin.Miter; }
			else if (joinStyle == LineJoinStyle.ROUND) { return Avalonia.Media.PenLineJoin.Round; }
			else if (joinStyle == LineJoinStyle.BEVEL) { return Avalonia.Media.PenLineJoin.Bevel; }
			else { return Avalonia.Media.PenLineJoin.Miter; } // Fallback
		}

	}

	public abstract class BrushData {

		public abstract Avalonia.Media.IBrush Build();

	}

	public class SolidColorBrushData : BrushData {

		public Avalonia.Media.Color Color { get; }

		public SolidColorBrushData(SharpSheets.Colors.Color color) : base() {
			Color = ConverterUtils.Convert(color);
		}

		public override Avalonia.Media.IBrush Build() {
			return new Avalonia.Media.SolidColorBrush(Color);
		}

	}

	public class LinearGradientBrushData : BrushData {

		public Avalonia.RelativePoint StartPoint { get; set; }
		public Avalonia.RelativePoint EndPoint { get; set; }
		public GradientStopsData GradientStops { get; set; }
		public Transform? Transform { get; set; }

		public LinearGradientBrushData() : base() {
			GradientStops = new GradientStopsData();
		}

		public override Avalonia.Media.IBrush Build() {
			return new Avalonia.Media.LinearGradientBrush() {
				StartPoint = StartPoint,
				EndPoint = EndPoint,
				GradientStops = GradientStops.Build(),
				SpreadMethod = Avalonia.Media.GradientSpreadMethod.Pad,
				Transform = ConverterUtils.Convert(Transform)
				//MappingMode = BrushMappingMode.Absolute
			};
		}

	}

	public class RadialGradientBrushData : BrushData {

		public Avalonia.RelativePoint Center { get; set; }
		public Avalonia.RelativePoint GradientOrigin { get; set; }
		public float Radius { get; set; }
		public GradientStopsData GradientStops { get; set; }
		public Transform? Transform { get; set; }

		public RadialGradientBrushData() : base() {
			GradientStops = new GradientStopsData();
		}

		public override Avalonia.Media.IBrush Build() {
			return new Avalonia.Media.RadialGradientBrush() {
				GradientStops = GradientStops.Build(),
				SpreadMethod = Avalonia.Media.GradientSpreadMethod.Pad,
				//MappingMode = BrushMappingMode.Absolute,
				Center = Center,
				GradientOrigin = GradientOrigin,
				Radius = Radius,
				Transform = ConverterUtils.Convert(Transform)
			};
		}

	}

	public class GradientStopsData {

		private List<ColorStop> Stops { get; set; }

		public GradientStopsData() {
			Stops = new List<ColorStop>();
		}

		public void AddRange(IEnumerable<ColorStop> stops) {
			Stops.AddRange(stops);
		}

		public Avalonia.Media.GradientStops Build() {
			Avalonia.Media.GradientStops built = new Avalonia.Media.GradientStops();
			built.AddRange(Stops.Select(s => new Avalonia.Media.GradientStop(ConverterUtils.Convert(s.Color), s.Stop)));
			return built;
		}
	}

	public class PenData {

		public BrushData Brush { get; }
		public float LineWidth { get; }

		//Avalonia.Media.PenLineCap

		public Avalonia.Media.PenLineJoin PenLineJoin { get; set; }
		public float MiterLimit { get; set; }
		public Avalonia.Media.PenLineCap PenLineCap { get; set; }
		public DashStyleData? DashStyle { get; set; }

		public LineJoinStyle LineJoin { set { PenLineJoin = ConverterUtils.Convert(value); } }
		public LineCapStyle LineCap { set { PenLineCap = ConverterUtils.Convert(value); } }

		public PenData(BrushData brush, float lineWidth) {
			Brush = brush;
			LineWidth = lineWidth;
		}

		public Avalonia.Media.IPen Build() {
			return new Avalonia.Media.Pen(Brush.Build(), LineWidth) {
				LineJoin = PenLineJoin,
				MiterLimit = (double)MiterLimit,
				LineCap = PenLineCap,
				DashStyle = DashStyle?.Build()
				//DashCap = PenLineCap.Flat // TODO Necessary?
			};
		}
	}

	public class DashStyleData {

		private readonly double[]? dashes;
		private readonly float offset;

		public DashStyleData(IEnumerable<float>? dashes, float offset) {
			this.dashes = dashes?.Select(d => (double)d).ToArray();
			this.offset = offset;
		}

		public Avalonia.Media.IDashStyle Build() {
			return new Avalonia.Media.DashStyle(dashes, offset);
		}

	}

	public abstract class GeometryData {
		public Transform? Transform { get; set; }

		public abstract Avalonia.Media.Geometry Build();
	}

	public abstract class DrawingData {
		public abstract Avalonia.Media.Drawing Build();
	}

	public class DrawingGroupData : DrawingData {
		public GeometryData? ClipGeometry { get; set; }
		public IList<DrawingData> Children { get; }
		public Transform? Transform { get; set; }

		public DrawingGroupData() : base() {
			Children = new List<DrawingData>();
		}

		public override Avalonia.Media.Drawing Build() {
			return BuildGroup();
		}

		public Avalonia.Media.DrawingGroup BuildGroup() {
			Avalonia.Media.DrawingGroup built = new Avalonia.Media.DrawingGroup() {
				ClipGeometry = ClipGeometry?.Build(),
				Transform = ConverterUtils.Convert(Transform)
			};
			built.Children.AddRange(Children.Select(c => c.Build()));
			return built;
		}

	}

	public class ImageDrawingData : DrawingData {
		public CanvasImageData? ImageSource { get; set; }
		public Avalonia.Rect Rect { get; set; }

		public ImageDrawingData() : base() { }

		private static Avalonia.Media.IImage? GetImage(CanvasImageData? image) {
			if (image is not null && !string.IsNullOrWhiteSpace(image.Path.Path) && File.Exists(image.Path.Path)) {
				return new Avalonia.Media.Imaging.Bitmap(image.Path.Path);
			}
			else {
				return null;
			}
		}

		public override Avalonia.Media.Drawing Build() {
			return new Avalonia.Media.ImageDrawing() {
				ImageSource = GetImage(ImageSource),
				Rect = Rect
			};
		}

	}

	public class GeometryDrawingData : DrawingData {
		public GeometryData? Geometry { get; set; }
		public PenData? Pen { get; set; }
		public BrushData? Brush { get; set; }

		public GeometryDrawingData() : base() { }

		public override Avalonia.Media.Drawing Build() {
			return new Avalonia.Media.GeometryDrawing() {
				Geometry = Geometry?.Build(),
				Pen = Pen?.Build(),
				Brush = Brush?.Build()
			};
		}

	}

	public class RectangleGeometryData : GeometryData {

		public Avalonia.Rect Rect { get; set; }

		public RectangleGeometryData() : base() { }

		public override Avalonia.Media.Geometry Build() {
			return new Avalonia.Media.RectangleGeometry() {
				Rect = Rect,
				Transform = ConverterUtils.Convert(Transform)
			};
		}

	}

	public class EllipseGeometryData : GeometryData {

		public Avalonia.Point Center { get; set; }
		public float RadiusX { get; set; }
		public float RadiusY { get; set; }

		public EllipseGeometryData() : base() { }

		public override Avalonia.Media.Geometry Build() {
			return new Avalonia.Media.EllipseGeometry() {
				Center = Center,
				RadiusX = RadiusX,
				RadiusY = RadiusY,
				Transform = ConverterUtils.Convert(Transform)
			};
		}

	}

	public class PathGeometryData : GeometryData {
		private readonly List<PathFigureData> figures;

		public Avalonia.Media.FillRule FillRule { get; set; } = Avalonia.Media.FillRule.NonZero;

		public int Count => figures.Count;
		public PathFigureData this[int index] => figures[index];

		public PathGeometryData() : base() {
			figures = new List<PathFigureData>();
		}

		public void Add(PathFigureData figure) {
			figures.Add(figure);
		}

		public override Avalonia.Media.Geometry Build() {
			Avalonia.Media.PathFigures builtFigures = new Avalonia.Media.PathFigures();
			builtFigures.AddRange(figures.Select(f => f.Build()));

			return new Avalonia.Media.PathGeometry() {
				Figures = builtFigures,
				Transform = ConverterUtils.Convert(Transform),
				FillRule = FillRule
			};
		}

	}

	public class PathFigureData {
		private readonly List<PathSegmentData> segments;

		public bool IsClosed { get; set; }
		public bool IsFilled { get; set; } = true;
		public Avalonia.Point StartPoint { get; set; }

		public int Count => segments.Count;
		public PathSegmentData this[int index] => segments[index];

		public PathFigureData() {
			segments = new List<PathSegmentData>();
		}

		public void Add(PathSegmentData segment) {
			segments.Add(segment);
		}

		public Avalonia.Media.PathFigure Build() {
			Avalonia.Media.PathSegments builtSegments = new Avalonia.Media.PathSegments();
			builtSegments.AddRange(segments.Select(s => s.Build()));

			return new Avalonia.Media.PathFigure() {
				StartPoint = StartPoint,
				IsClosed = IsClosed,
				IsFilled = IsFilled,
				Segments = builtSegments
			};
		}

	}

	public abstract class PathSegmentData {
		public abstract Avalonia.Media.PathSegment Build();
	}

	public class LineSegmentData : PathSegmentData {
		public Avalonia.Point Point { get; set; }

		public LineSegmentData() : base() { }

		public override Avalonia.Media.PathSegment Build() {
			return new Avalonia.Media.LineSegment() {
				Point = Point
			};
		}
	}

	public class QuadraticBezierSegmentData : PathSegmentData {
		public Avalonia.Point Point1 { get; set; }
		public Avalonia.Point Point2 { get; set; }

		public QuadraticBezierSegmentData() : base() { }

		public override Avalonia.Media.PathSegment Build() {
			return new Avalonia.Media.QuadraticBezierSegment() {
				Point1 = Point1,
				Point2 = Point2
			};
		}
	}

	public class BezierSegmentData : PathSegmentData {
		public Avalonia.Point Point1 { get; set; }
		public Avalonia.Point Point2 { get; set; }
		public Avalonia.Point Point3 { get; set; }

		public BezierSegmentData() : base() { }

		public override Avalonia.Media.PathSegment Build() {
			return new Avalonia.Media.BezierSegment() {
				Point1 = Point1,
				Point2 = Point2,
				Point3 = Point3
			};
		}
	}

	public class CombinedGeometryData : GeometryData {

		private readonly Avalonia.Media.GeometryCombineMode mode;
		private readonly GeometryData geometry1;
		private readonly GeometryData geometry2;

		public CombinedGeometryData(Avalonia.Media.GeometryCombineMode mode, GeometryData geometry1, GeometryData geometry2) {
			this.mode = mode;
			this.geometry1 = geometry1;
			this.geometry2 = geometry2;
		}

		public override Avalonia.Media.Geometry Build() {
			return new Avalonia.Media.CombinedGeometry() {
				GeometryCombineMode = mode,
				Geometry1 = geometry1.Build(),
				Geometry2 = geometry2.Build(),
				Transform = ConverterUtils.Convert(Transform)
			};
		}

	}

	public class GeometryGroupData : GeometryData {
		public Avalonia.Media.FillRule FillRule { get; set; } = Avalonia.Media.FillRule.EvenOdd;
		public IList<GeometryData> Children { get; }

		public GeometryGroupData() : base() {
			Children = new List<GeometryData>();
		}

		public override Avalonia.Media.Geometry Build() {
			Avalonia.Media.GeometryGroup built = new Avalonia.Media.GeometryGroup() {
				FillRule = FillRule,
				Transform = ConverterUtils.Convert(Transform)
			};
			built.Children.AddRange(Children.Select(c => c.Build()));
			return built;
		}

	}

	public static class GlyphGeometryUtils {

		public static PathGeometryData? GetGlyphGeometryData(this TrueTypeFontFileOutlines glyphOutlines, ushort glyph, float fontsize) {
			float ProcessShort(short value) {
				return fontsize * (1000f * (value / (float)glyphOutlines.UnitsPerEm)) / 1000f;
			}

			if (glyphOutlines.glyf?.glyphOutlines[glyph] is TrueTypeGlyphOutline gOutline) {
				PathGeometryData geometry = new PathGeometryData() { FillRule = Avalonia.Media.FillRule.NonZero };

				int p = 0, c = 0;
				bool first = true;
				while (p < gOutline.PointCount) {
					if (gOutline.onCurve[p]) {
						float x = ProcessShort(gOutline.xCoordinates[p]);
						float y = ProcessShort(gOutline.yCoordinates[p]);

						if (first) {
							geometry.Add(new PathFigureData() { StartPoint = new Avalonia.Point(x, y), IsClosed = true, IsFilled = true });
							first = false;
						}
						else if (p > 0 && !gOutline.onCurve[p - 1]) {
							float cx = ProcessShort(gOutline.xCoordinates[p - 1]);
							float cy = ProcessShort(gOutline.yCoordinates[p - 1]);

							geometry[^1].Add(new QuadraticBezierSegmentData() { Point1 = new Avalonia.Point(cx, cy), Point2 = new Avalonia.Point(x, y) });
						}
						else {
							geometry[^1].Add(new LineSegmentData() { Point = new Avalonia.Point(x, y) });
						}
					}

					if (c < gOutline.endPtsOfContours.Count && p == gOutline.endPtsOfContours[c]) {
						c += 1;
						first = true;
					}

					p += 1;
				}

				return geometry;
			}
			else if (glyphOutlines.cff?.glyphs[glyph] is Type2Glyph g2Outline) {
				PathGeometryData geometry = new PathGeometryData() { FillRule = Avalonia.Media.FillRule.NonZero };

				int p = 0, c = 0;
				bool first = true;
				while (p < g2Outline.PointCount) {
					if (g2Outline.OnCurve[p]) {
						float x = ProcessShort(g2Outline.Xs[p]);
						float y = ProcessShort(g2Outline.Ys[p]);

						if (first) {
							geometry.Add(new PathFigureData() { StartPoint = new Avalonia.Point(x, y), IsClosed = true, IsFilled = true });
							first = false;
						}
						else if (p > 1 && !g2Outline.OnCurve[p - 1]) {
							float cx1 = ProcessShort(g2Outline.Xs[p - 2]);
							float cy1 = ProcessShort(g2Outline.Ys[p - 2]);
							float cx2 = ProcessShort(g2Outline.Xs[p - 1]);
							float cy2 = ProcessShort(g2Outline.Ys[p - 1]);

							geometry[^1].Add(new BezierSegmentData() { Point1 = new Avalonia.Point(cx1, cy1), Point2 = new Avalonia.Point(cx2, cy2), Point3 = new Avalonia.Point(x, y) });
						}
						else {
							geometry[^1].Add(new LineSegmentData() { Point = new Avalonia.Point(x, y) });
						}
					}

					if (c < g2Outline.EndPtsOfContours.Count && p == g2Outline.EndPtsOfContours[c]) {
						c += 1;
						first = true;
					}

					p += 1;
				}

				return geometry;
			}
			else if (glyph != 0) {
				return glyphOutlines.GetGlyphGeometryData(0, fontsize);
			}
			else {
				// Could not find glyph outline data
				return null;
			}
		}

	}
}
