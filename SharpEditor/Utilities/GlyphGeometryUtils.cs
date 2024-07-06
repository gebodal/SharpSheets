using Avalonia;
using Avalonia.Media;
using GeboPdf.Fonts.TrueType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.Utilities {

	public static class GlyphGeometryUtils {

		public static Geometry? GetGlyphGeometry(this TrueTypeFontFileOutlines glyphOutlines, ushort glyph, float fontsize) {

			double ProcessShort(short value) {
				return (fontsize * (1000.0 * (value / (double)glyphOutlines.UnitsPerEm))) / 1000.0;
			}

			if (glyphOutlines.glyf?.glyphOutlines[glyph] is TrueTypeGlyphOutline gOutline) {
				// Create a geometry for the glyph
				PathGeometry geometry = new PathGeometry() { FillRule = FillRule.NonZero };

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

				return geometry;
			}
			else if (glyphOutlines.cff?.glyphs[glyph] is Type2Glyph g2Outline) {
				// Create a geometry for the glyph
				PathGeometry geometry = new PathGeometry() { FillRule = FillRule.NonZero };

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

				return geometry;
			}
			else if(glyph != 0) {
				return GetGlyphGeometry(glyphOutlines, 0, fontsize);
			}
			else {
				// Could not find glyph outline data
				return null;
			}
		}

	}

}
