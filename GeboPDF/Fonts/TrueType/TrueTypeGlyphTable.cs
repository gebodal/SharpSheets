using GeboPdf.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	[Flags]
	enum SimpleGlyphFlags : byte {
		/// <summary>
		/// If set, the point is on the curve; otherwise, it is off the curve.
		/// </summary>
		ON_CURVE_POINT = 1 << 0,
		/// <summary>
		/// If set, the corresponding x-coordinate is 1 byte long, and the sign
		/// is determined by the X_IS_SAME_OR_POSITIVE_X_SHORT_VECTOR flag. If not
		/// set, its interpretation depends on the X_IS_SAME_OR_POSITIVE_X_SHORT_VECTOR
		/// flag: If that other flag is set, the x-coordinate is the same as the
		/// previous x-coordinate, and no element is added to the xCoordinates
		/// array. If both flags are not set, the corresponding element in the
		/// xCoordinates array is two bytes and interpreted as a signed integer.
		/// See the description of the X_IS_SAME_OR_POSITIVE_X_SHORT_VECTOR flag
		/// for additional information.
		/// </summary>
		X_SHORT_VECTOR = 1 << 1,
		/// <summary>
		/// If set, the corresponding y-coordinate is 1 byte long, and the sign is
		/// determined by the Y_IS_SAME_OR_POSITIVE_Y_SHORT_VECTOR flag. If not set,
		/// its interpretation depends on the Y_IS_SAME_OR_POSITIVE_Y_SHORT_VECTOR
		/// flag: If that other flag is set, the y-coordinate is the same as the
		/// previous y-coordinate, and no element is added to the yCoordinates array.
		/// If both flags are not set, the corresponding element in the yCoordinates
		/// array is two bytes and interpreted as a signed integer. See the description
		/// of the Y_IS_SAME_OR_POSITIVE_Y_SHORT_VECTOR flag for additional information.
		/// </summary>
		Y_SHORT_VECTOR = 1 << 2,
		/// <summary>
		/// If set, the next byte (read as unsigned) specifies the number of additional
		/// times this flag byte is to be repeated in the logical flags array — that is,
		/// the number of additional logical flag entries inserted after this entry.
		/// (In the expanded logical array, this bit is ignored.) In this way, the
		/// number of flags listed can be smaller than the number of points in the
		/// glyph description.
		/// </summary>
		REPEAT_FLAG = 1 << 3,
		/// <summary>
		/// This flag has two meanings, depending on how the X_SHORT_VECTOR flag is set.
		/// If X_SHORT_VECTOR is set, this bit describes the sign of the value, with 1
		/// equalling positive and 0 negative. If X_SHORT_VECTOR is not set and this bit
		/// is set, then the current x-coordinate is the same as the previous x-coordinate.
		/// If X_SHORT_VECTOR is not set and this bit is also not set, the current
		/// x-coordinate is a signed 16-bit delta vector.
		/// </summary>
		X_IS_SAME_OR_POSITIVE_X_SHORT_VECTOR = 1 << 4,
		/// <summary>
		/// This flag has two meanings, depending on how the Y_SHORT_VECTOR flag is set.
		/// If Y_SHORT_VECTOR is set, this bit describes the sign of the value, with 1
		/// equalling positive and 0 negative. If Y_SHORT_VECTOR is not set and this bit
		/// is set, then the current y-coordinate is the same as the previous y-coordinate.
		/// If Y_SHORT_VECTOR is not set and this bit is also not set, the current
		/// y-coordinate is a signed 16-bit delta vector.
		/// </summary>
		Y_IS_SAME_OR_POSITIVE_Y_SHORT_VECTOR = 1 << 5,
		/// <summary>
		/// If set, contours in the glyph description may overlap. Use of this flag is not
		/// required in OpenType — that is, it is valid to have contours overlap without
		/// having this flag set. It may affect behaviors in some platforms, however.
		/// (See the discussion of “Overlapping contours” in Apple’s specification for
		/// details regarding behavior in Apple platforms.) When used, it must be set on
		/// the first flag byte for the glyph. See additional details below.
		/// </summary>
		OVERLAP_SIMPLE = 1 << 6
		// Last bit (7) is reserved, and should be set to zero
	}

	[Flags]
	public enum CompositeGlyphFlags : ushort {
		/// <summary>
		/// If this is set, the arguments are 16-bit (uint16 or int16); otherwise, they
		/// are bytes (uint8 or int8).
		/// </summary>
		ARG_1_AND_2_ARE_WORDS = 1 << 0,
		/// <summary>
		/// If this is set, the arguments are signed xy values; otherwise, they are unsigned
		/// point numbers.
		/// </summary>
		ARGS_ARE_XY_VALUES = 1 << 1,
		/// <summary>
		/// If set and ARGS_ARE_XY_VALUES is also set, the xy values are rounded to the nearest
		/// grid line. Ignored if ARGS_ARE_XY_VALUES is not set.
		/// </summary>
		ROUND_XY_TO_GRID = 1 << 2,
		/// <summary>
		///This indicates that there is a simple scale for the component. Otherwise, scale = 1.0.
		/// </summary>
		WE_HAVE_A_SCALE = 1 << 3,
		/// <summary>
		/// Indicates at least one more glyph after this one.
		/// </summary>
		MORE_COMPONENTS = 1 << 5,
		/// <summary>
		/// The x direction will use a different scale from the y direction.
		/// </summary>
		WE_HAVE_AN_X_AND_Y_SCALE = 1 << 6,
		/// <summary>
		/// There is a 2 by 2 transformation that will be used to scale the component.
		/// </summary>
		WE_HAVE_A_TWO_BY_TWO = 1 << 7,
		/// <summary>
		/// Following the last component are instructions for the composite character.
		/// </summary>
		WE_HAVE_INSTRUCTIONS = 1 << 8,
		/// <summary>
		/// If set, this forces the aw and lsb (and rsb) for the composite to be equal to those from
		/// this component glyph. This works for hinted and unhinted glyphs.
		/// </summary>
		USE_MY_METRICS = 1 << 9,
		/// <summary>
		/// If set, the components of the compound glyph overlap. Use of this flag is not required in
		/// OpenType — that is, it is valid to have components overlap without having this flag set.
		/// It may affect behaviors in some platforms, however. (See Apple’s specification for details
		/// regarding behavior in Apple platforms.) When used, it must be set on the flag word for the
		/// first component. See additional remarks, above, for the similar OVERLAP_SIMPLE flag used
		/// in simple-glyph descriptions.
		/// </summary>
		OVERLAP_COMPOUND = 1 << 10,
		/// <summary>
		/// The composite is designed to have the component offset scaled. Ignored if
		/// ARGS_ARE_XY_VALUES is not set.
		/// </summary>
		SCALED_COMPONENT_OFFSET = 1 << 11,
		/// <summary>
		/// The composite is designed not to have the component offset scaled. Ignored if
		/// ARGS_ARE_XY_VALUES is not set.
		/// </summary>
		UNSCALED_COMPONENT_OFFSET = 1 << 12
	}

	public class TrueTypeGlyphOutline {

		public readonly IReadOnlyList<short> xCoordinates;
		public readonly IReadOnlyList<short> yCoordinates;
		public readonly IReadOnlyList<bool> onCurve;
		public readonly IReadOnlyList<ushort> endPtsOfContours;

		public int PointCount => xCoordinates.Count;

		public TrueTypeGlyphOutline(IReadOnlyList<short> xCoordinates, IReadOnlyList<short> yCoordinates, IReadOnlyList<bool> onCurve, IReadOnlyList<ushort> endPtsOfContours) {
			if(xCoordinates.Count != yCoordinates.Count || xCoordinates.Count != onCurve.Count) {
				throw new ArgumentException("Provided outline data must all have the same length.");
			}

			this.xCoordinates = xCoordinates;
			this.yCoordinates = yCoordinates;
			this.onCurve = onCurve;
			this.endPtsOfContours = endPtsOfContours;
		}

		public TrueTypeGlyphOutline Transform(TrueTypeTransform transform) {
			short[] xCoords = new short[PointCount];
			short[] yCoords = new short[PointCount];

			for (int p = 0; p < PointCount; p++) {
				(xCoords[p], yCoords[p]) = transform.Map(xCoordinates[p], yCoordinates[p]);
			}

			return new TrueTypeGlyphOutline(xCoords, yCoords, onCurve, endPtsOfContours);
		}

	}

	public class TrueTypeTransform {

		public readonly float a, b, c, d, e, f;

		private TrueTypeTransform(float a, float b, float c, float d, float e, float f) {
			this.a = a;
			this.b = b;
			this.c = c;
			this.d = d;
			this.e = e;
			this.f = f;
		}

		public static TrueTypeTransform Matrix(float a, float b, float c, float d, float e, float f) {
			return new TrueTypeTransform(a, b, c, d, e, f);
		}

		public static TrueTypeTransform Translate(float x, float y) {
			return new TrueTypeTransform(1, 0, 0, 1, x, y);
		}

		public (short xt, short yt) Map(short x, short y) {
			short xt = (short)((a * x) + (c * y) + e);
			short yt = (short)((b * x) + (d * y) + f);
			return (xt, yt);
		}

		public override string ToString() {
			return $"[{a}, {b}, {c}, {d}, {e}, {f}]";
		}

	}

	public class TrueTypeGlyphContourTable {

		public readonly TrueTypeGlyphOutline[] glyphOutlines;

		internal TrueTypeGlyphContourTable(TrueTypeGlyphOutline[] glyphOutlines) {
			this.glyphOutlines = glyphOutlines;
		}

		internal static TrueTypeGlyphContourTable Read(FontFileReader reader, long offset, TrueTypeIndexToLocationTable loca) {

			//reader.Position = offset;

			TrueTypeGlyphOutline[] glyphOutlines = new TrueTypeGlyphOutline[loca.lengths.Length];

			for (int i = 0; i < loca.offsets.Length; i++) {
				glyphOutlines[i] = ReadGlyphData(reader, offset, loca, i);
			}

			return new TrueTypeGlyphContourTable(glyphOutlines);
		}

		private static TrueTypeGlyphOutline ReadGlyphData(FontFileReader reader, long tableOffset, TrueTypeIndexToLocationTable loca, int glyphIdx) {
			if (loca.lengths[glyphIdx] > 0) {

				reader.Position = tableOffset + loca.offsets[glyphIdx];

				short numberOfContours = reader.ReadInt16();

				reader.SkipFWord(4);
				//short xMin = reader.ReadFWord();
				//short yMin = reader.ReadFWord();
				//short xMax = reader.ReadFWord();
				//short yMax = reader.ReadFWord();

				if (numberOfContours >= 0) { // Simple glyph description
					return ReadSingleGlyphData(reader, numberOfContours);
				}
				else { // Composite glyph description
					return ReadCompoundGlyphData(reader, tableOffset, loca);
				}
			}
			else {
				return new TrueTypeGlyphOutline(Array.Empty<short>(), Array.Empty<short>(), Array.Empty<bool>(), Array.Empty<ushort>());
			}
		}

		private static TrueTypeGlyphOutline ReadSingleGlyphData(FontFileReader reader, short numberOfContours) {

			ushort[] endPtsOfContours = reader.ReadUInt16(numberOfContours);
			ushort instructionLength = reader.ReadUInt16();
			reader.SkipUInt8(instructionLength); // Instruction bytes

			int numberOfPoints = endPtsOfContours[^1] + 1;

			SimpleGlyphFlags[] flags = new SimpleGlyphFlags[numberOfPoints];
			for (int p = 0; p < numberOfPoints; p++) {

				SimpleGlyphFlags flag = (SimpleGlyphFlags)reader.ReadUInt8();
				flags[p] = flag;

				if (flags[p].HasFlag(SimpleGlyphFlags.REPEAT_FLAG)) {
					byte repeats = reader.ReadUInt8();
					for (int r = 1; r <= repeats; r++) {
						flags[p + r] = flag; // TODO This isn't right, is it?
					}
					p += repeats;
				}
			}

			short[] xCoordinates = new short[numberOfPoints];
			for (int p = 0; p < numberOfPoints; p++) {
				if (flags[p].HasFlag(SimpleGlyphFlags.X_SHORT_VECTOR)) {
					short value = reader.ReadUInt8();
					short sign = flags[p].HasFlag(SimpleGlyphFlags.X_IS_SAME_OR_POSITIVE_X_SHORT_VECTOR) ? (short)1 : (short)-1;
					xCoordinates[p] = (short)(sign * value);

					if (p > 0) { xCoordinates[p] += xCoordinates[p - 1]; }
				}
				else if (flags[p].HasFlag(SimpleGlyphFlags.X_IS_SAME_OR_POSITIVE_X_SHORT_VECTOR)) {
					xCoordinates[p] = p > 0 ? xCoordinates[p - 1] : (short)0;
				}
				else { // Both bits not set
					xCoordinates[p] = reader.ReadInt16();

					if (p > 0) { xCoordinates[p] += xCoordinates[p - 1]; }
				}
				
			}

			short[] yCoordinates = new short[numberOfPoints];
			for (int p = 0; p < numberOfPoints; p++) {
				if (flags[p].HasFlag(SimpleGlyphFlags.Y_SHORT_VECTOR)) {
					short value = reader.ReadUInt8();
					short sign = flags[p].HasFlag(SimpleGlyphFlags.Y_IS_SAME_OR_POSITIVE_Y_SHORT_VECTOR) ? (short)1 : (short)-1;
					yCoordinates[p] = (short)(sign * value);

					if (p > 0) { yCoordinates[p] += yCoordinates[p - 1]; }
				}
				else if (flags[p].HasFlag(SimpleGlyphFlags.Y_IS_SAME_OR_POSITIVE_Y_SHORT_VECTOR)) {
					yCoordinates[p] = p > 0 ? yCoordinates[p - 1] : (short)0;
				}
				else { // Both bits not set
					yCoordinates[p] = reader.ReadInt16();

					if (p > 0) { yCoordinates[p] += yCoordinates[p - 1]; }
				}
				
			}

			bool[] onCurve = new bool[numberOfPoints];
			for(int p=0; p<numberOfPoints; p++) {
				onCurve[p] = flags[p].HasFlag(SimpleGlyphFlags.ON_CURVE_POINT);
			}

			return ConstructOutline(xCoordinates, yCoordinates, onCurve, endPtsOfContours);
		}

		private static TrueTypeGlyphOutline ReadCompoundGlyphData(FontFileReader reader, long tableOffset, TrueTypeIndexToLocationTable loca) {

			List<short> xCoordinates = new List<short>();
			List<short> yCoordinates = new List<short>();
			List<bool> onCurve = new List<bool>();
			List<ushort> endPtsOfContours = new List<ushort>();

			CompositeGlyphFlags flags;
			do {
				flags = (CompositeGlyphFlags)reader.ReadUInt16();
				ushort glyphIndex = reader.ReadUInt16();

				(short x, short y)? xyOffset = null;
				(ushort parent, ushort child)? pointNumbers = null;

				if (flags.HasFlag(CompositeGlyphFlags.ARGS_ARE_XY_VALUES)) {
					if (flags.HasFlag(CompositeGlyphFlags.ARG_1_AND_2_ARE_WORDS)) {
						short arg1 = reader.ReadInt16();
						short arg2 = reader.ReadInt16();
						xyOffset = (arg1, arg2);
					}
					else {
						sbyte arg1 = reader.ReadInt8();
						sbyte arg2 = reader.ReadInt8();
						xyOffset = (arg1, arg2);
					}
				}
				else {
					if (flags.HasFlag(CompositeGlyphFlags.ARG_1_AND_2_ARE_WORDS)) {
						ushort arg1 = reader.ReadUInt16();
						ushort arg2 = reader.ReadUInt16();
						pointNumbers = (arg1, arg2);
					}
					else {
						byte arg1 = reader.ReadUInt8();
						byte arg2 = reader.ReadUInt8();
						pointNumbers = (arg1, arg2);
					}
				}

				float xscale = 1f, scale01 = 0f, scale10 = 0f, yscale = 1f;
				if (flags.HasFlag(CompositeGlyphFlags.WE_HAVE_A_SCALE)) {
					xscale = yscale = reader.ReadF2Dot14();
				}
				else if (flags.HasFlag(CompositeGlyphFlags.WE_HAVE_AN_X_AND_Y_SCALE)) {
					xscale = reader.ReadF2Dot14();
					yscale = reader.ReadF2Dot14();
				}
				else if (flags.HasFlag(CompositeGlyphFlags.WE_HAVE_A_TWO_BY_TWO)) {
					xscale = reader.ReadF2Dot14();
					scale01 = reader.ReadF2Dot14();
					scale10 = reader.ReadF2Dot14();
					yscale = reader.ReadF2Dot14();
				}

				TrueTypeTransform transform = TrueTypeTransform.Matrix(xscale, scale01, scale10, yscale, 0, 0);

				long oldPosition = reader.Position;
				TrueTypeGlyphOutline component = ReadGlyphData(reader, tableOffset, loca, glyphIndex).Transform(transform);
				reader.Position = oldPosition;

				short xOffset;
				short yOffset;
				if (xyOffset.HasValue) {
					xOffset = xyOffset.Value.x;
					yOffset = xyOffset.Value.y;

					if (flags.HasFlag(CompositeGlyphFlags.SCALED_COMPONENT_OFFSET) && !flags.HasFlag(CompositeGlyphFlags.UNSCALED_COMPONENT_OFFSET)) {
						(xOffset, yOffset) = transform.Map(xOffset, yOffset);
					}
				}
				else {
					xOffset = (short)(xCoordinates[pointNumbers!.Value.parent] - component.xCoordinates[pointNumbers!.Value.child]);
					yOffset = (short)(yCoordinates[pointNumbers!.Value.parent] - component.yCoordinates[pointNumbers!.Value.child]);
				}

				component = component.Transform(TrueTypeTransform.Translate(xOffset, yOffset));

				int previousPointsCount = xCoordinates.Count;

				xCoordinates.AddRange(component.xCoordinates);
				yCoordinates.AddRange(component.yCoordinates);
				onCurve.AddRange(component.onCurve);
				
				endPtsOfContours.AddRange(component.endPtsOfContours.Select(e => (ushort)(previousPointsCount + e)));

			} while (flags.HasFlag(CompositeGlyphFlags.MORE_COMPONENTS));

			if (flags.HasFlag(CompositeGlyphFlags.WE_HAVE_INSTRUCTIONS)) {
				ushort numInstructions = reader.ReadUInt16();
				reader.SkipUInt8(numInstructions); // Instructions
			}

			return ConstructOutline(xCoordinates, yCoordinates, onCurve, endPtsOfContours);
		}

		private static TrueTypeGlyphOutline ConstructOutline(IReadOnlyList<short> xCoordinates, IReadOnlyList<short> yCoordinates, IReadOnlyList<bool> onCurve, IReadOnlyList<ushort> endPtsOfContours) {
			if (xCoordinates.Count != yCoordinates.Count || xCoordinates.Count != onCurve.Count) {
				throw new ArgumentException("Provided outline data must all have the same length.");
			}

			List<short> xCoordinatesFinal = new List<short>();
			List<short> yCoordinatesFinal = new List<short>();
			List<bool> onCurveFinal = new List<bool>();
			List<ushort> endPtsOfContoursFinal = new List<ushort>();

			int contour = 0;
			int contourStart = 0;
			int contourStartFinal = 0;
			for (int i = 0; i < xCoordinates.Count; i++) {

				if (i == contourStart && !onCurve[i]) {
					int contourEndPt = contour < endPtsOfContours.Count ? endPtsOfContours[contour] : xCoordinates.Count - 1;
					if (onCurve[contourEndPt]) {
						xCoordinatesFinal.Add(xCoordinates[contourEndPt]);
						yCoordinatesFinal.Add(yCoordinates[contourEndPt]);
						onCurveFinal.Add(true);
					}
					else { // !onCurve[contourEndPt]
						xCoordinatesFinal.Add((short)((xCoordinates[contourEndPt] + xCoordinates[i]) / 2f));
						yCoordinatesFinal.Add((short)((yCoordinates[contourEndPt] + yCoordinates[i]) / 2f));
						onCurveFinal.Add(true);
					}
				}

				if (onCurve[i]) {
					xCoordinatesFinal.Add(xCoordinates[i]);
					yCoordinatesFinal.Add(yCoordinates[i]);
					onCurveFinal.Add(true);
				}
				else {
					if (i > 0 && i != contourStart && !onCurve[i - 1]) {
						xCoordinatesFinal.Add((short)((xCoordinates[i - 1] + xCoordinates[i]) / 2f));
						yCoordinatesFinal.Add((short)((yCoordinates[i - 1] + yCoordinates[i]) / 2f));
						onCurveFinal.Add(true);
					}

					xCoordinatesFinal.Add(xCoordinates[i]);
					yCoordinatesFinal.Add(yCoordinates[i]);
					onCurveFinal.Add(false);
				}

				if (contour < endPtsOfContours.Count && endPtsOfContours[contour] == i) {

					if (!onCurve[i]) {
						// All final contours should begin with an onCurve point
						xCoordinatesFinal.Add(xCoordinatesFinal[contourStartFinal]);
						yCoordinatesFinal.Add(yCoordinatesFinal[contourStartFinal]);
						onCurveFinal.Add(true);
					}

					endPtsOfContoursFinal.Add((ushort)(xCoordinatesFinal.Count - 1));
					contour++;
					contourStart = i + 1;
					contourStartFinal = xCoordinatesFinal.Count;
				}
			}

			return new TrueTypeGlyphOutline(xCoordinatesFinal, yCoordinatesFinal, onCurveFinal, endPtsOfContoursFinal);
		}

	}

}
