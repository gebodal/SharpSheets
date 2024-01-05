using SharpSheets.Colors;

namespace SharpSheets.Canvas {

	/// <summary>
	/// Indicates the content type for a text field.
	/// </summary>
	public enum TextFieldType {
		/// <summary>
		/// This field may contain any arbitrary text string.
		/// </summary>
		STRING,
		/// <summary>
		/// This field should contain only strings representing integer values.
		/// </summary>
		INT,
		/// <summary>
		/// This field should contain only strings representing floating point (decimal) values.
		/// </summary>
		FLOAT
	}

	/// <summary>
	/// Indicates the symbol to be used in a check field to indicate an "On" state.
	/// </summary>
	public enum CheckType : int {
		/// <summary>
		/// A "check" or "tick" mark.
		/// </summary>
		CHECK = 1,
		/// <summary>
		/// A filled circle.
		/// </summary>
		CIRCLE = 2,
		/// <summary>
		/// An "X" shaped (i.e. diagonal) cross.
		/// </summary>
		CROSS = 3,
		/// <summary>
		/// A filled rhombus, or "diamond", shape.
		/// </summary>
		DIAMOND = 4,
		/// <summary>
		/// A filled square.
		/// </summary>
		SQUARE = 5,
		/// <summary>
		/// A filled, five pointed star.
		/// </summary>
		STAR = 6
	}

	public class StrokeDash {
		public float[] Values { get; }
		public float Offset { get; }

		public StrokeDash(float[] values, float offset) {
			Values = values;
			Offset = offset;
		}
	}

	public class ColorStop {
		public float Stop { get; }
		public Color Color { get; }

		public ColorStop(float stop, Color color) {
			this.Stop = stop;
			this.Color = color;
		}
	}

	public abstract class CanvasPaint {

		protected CanvasPaint() { }

	}

	public class CanvasSolidPaint : CanvasPaint {

		public Color Color { get; }

		public CanvasSolidPaint(Color color) {
			this.Color = color;
		}

	}

	public class CanvasLinearGradientPaint : CanvasPaint {

		public float X1 { get; }
		public float Y1 { get; }
		public float X2 { get; }
		public float Y2 { get; }
		public IReadOnlyList<ColorStop> Stops { get; }

		public CanvasLinearGradientPaint(float x1, float y1, float x2, float y2, IReadOnlyList<ColorStop> stops) {
			X1 = x1;
			Y1 = y1;
			X2 = x2;
			Y2 = y2;
			Stops = stops;
		}

	}

	public class CanvasRadialGradientPaint : CanvasPaint {

		public float X1 { get; }
		public float Y1 { get; }
		public float R1 { get; }
		public float X2 { get; }
		public float Y2 { get; }
		public float R2 { get; }
		public IReadOnlyList<ColorStop> Stops { get; }

		public CanvasRadialGradientPaint(float x1, float y1, float r1, float x2, float y2, float r2, IReadOnlyList<ColorStop> stops) {
			X1 = x1;
			Y1 = y1;
			R1 = r1;
			X2 = x2;
			Y2 = y2;
			R2 = r2;
			Stops = stops;
		}

	}

	/// <summary>
	/// Indicates whether the text glyph outlines should be stroked, filled,
	/// used as a clipping path, or some combination thereof. Filling is
	/// performed using the current canvas text color, and stroking with the
	/// current canvas stroking color.
	/// </summary>
	public enum TextRenderingMode : int {
		/// <summary>
		/// The text should neither be filled nor stroked (this means it will be
		/// "rendered" invisibly).
		/// </summary>
		INVISIBLE = 0b000,
		/// <summary>
		/// The text should be filled with the current text color, and no other
		/// rendering actions taken.
		/// </summary>
		FILL = 0b100,
		/// <summary>
		/// The text should be stroked with the current stroke color, and no other
		/// rendering actions taken. This means the glyph outlines will be shown,
		/// but not filled.
		/// </summary>
		STROKE = 0b010,
		/// <summary>
		/// The text outline is to be used as a path for clipping, and no other
		/// rendering actions should be taken.
		/// </summary>
		CLIP = 0b001,
		/// <summary>
		/// The text should both be filled and stroked, with the current text color
		/// and stroking color, respectively.
		/// </summary>
		FILL_STROKE = FILL | STROKE, // 0b110,
		/// <summary>
		/// The text should be filled with the current text color, and the outline
		/// used as a path for clipping. No stroking is performed.
		/// </summary>
		FILL_CLIP = FILL | CLIP, // 0b101,
		/// <summary>
		/// The text should be stroked with the current stroking color, and the outline
		/// used as a path for clipping. No fill is performed.
		/// </summary>
		STROKE_CLIP = STROKE | CLIP, // 0b011,
		/// <summary>
		/// The text should be filled and stroked, with the current text and stroking
		/// color, respectively, and the outline used as a path for clipping.
		/// </summary>
		FILL_STROKE_CLIP = FILL | STROKE | CLIP // 0b111
	}

	/// <summary>
	/// Indicates the shape to be used at the ends of open paths (and dashes, if any)
	/// when they are stroked.
	/// </summary>
	public enum LineCapStyle : int {
		/// <summary>
		/// The stroke should be be squared off at the end of the path,
		/// such that the path end point lies on this squared end (i.e.
		/// there is no projection beyond the end of the path).
		/// </summary>
		BUTT,
		/// <summary>
		/// The stroke should end with a semi-circular cap, whose diameter is
		/// equal to the stroke width, such that the path end point is at the
		/// centre of the semi-circle.
		/// </summary>
		ROUND,
		/// <summary>
		/// The stroke continues beyond the end point of the path for a distance
		/// equal to half the line width and is squared off.
		/// </summary>
		PROJECTING_SQUARE
	}

	/// <summary>
	/// Indicates the way in which the outer edges of two connected path
	/// segments should be joined, before the inside of the stroke is filled
	/// with the current stroke color. The outer edges are those on the side
	/// where the angle is greater than 180 degrees.
	/// </summary>
	public enum LineJoinStyle : int {
		/// <summary>
		/// The outer edges of the strokes for the two segments are extended
		/// until they meet at an angle. If the segments meet at too sharp of an
		/// angle, a bevel join is used instead.
		/// </summary>
		MITER,
		/// <summary>
		/// The outer edges of the strokes are connected with a circular arc,
		/// centred at the point where the lines meet, with a diameter equal
		/// to the line width.
		/// </summary>
		ROUND,
		/// <summary>
		/// The two segments are finished with butt caps, and the resulting
		/// corners of the outer edge are connected.
		/// </summary>
		BEVEL
	}

	/*
	public enum FillingRule : int {
		NONZERO_WINDING = 1,
		EVEN_ODD = 2
	}
	*/

}
