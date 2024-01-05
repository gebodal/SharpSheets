using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using System.Linq;
using SharpSheets.Canvas;

namespace SharpSheets.Markup.Canvas {

	public class NSliceValues {
		public readonly float[] xs;
		public readonly float[] ys;

		public NSliceValues(float[] xs, float[] ys) {
			this.xs = xs;
			this.ys = ys;
		}
	}

	public class NSliceScaling {

		public readonly Size reference;
		public readonly Rectangle available;

		public readonly float scale;

		public readonly float[] xSlices;
		public readonly Dimension[] xWidths;
		public readonly float[] availableXLengths;
		public readonly float[] availableXSlices;

		public readonly float[] ySlices;
		public readonly Dimension[] yHeights;
		public readonly float[] availableYLengths;
		public readonly float[] availableYSlices;

		public NSliceScaling(Size reference, Rectangle available, float[] xs, float[] ys) {
			this.reference = reference;
			this.available = available;

			ProcessLengths(reference.Width, xs, out xSlices, out xWidths);
			ProcessLengths(reference.Height, ys, out ySlices, out yHeights);

			float xAbsTotal = xWidths.Select(x => x.Absolute).Sum();
			float yAbsTotal = yHeights.Select(y => y.Absolute).Sum();

			// Calculate scale
			scale = 1f;
			if (available.Width < xAbsTotal) {
				scale = available.Width / xAbsTotal;
			}
			if (available.Height < yAbsTotal) {
				scale = Math.Min(scale, available.Height / yAbsTotal);
			}

			// Calculate available slices
			// X
			availableXLengths = Divisions.ToAbsolute(xWidths.Select(w => w * scale).ToArray(), available.Width, 0f, out _);
			availableXSlices = new float[xSlices.Length];
			float runningTotalX = 0f;
			for (int i = 0; i < availableXSlices.Length; i++) {
				runningTotalX += availableXLengths[i];
				availableXSlices[i] = runningTotalX;
			}
			// Y
			availableYLengths = Divisions.ToAbsolute(yHeights.Select(w => w * scale).ToArray(), available.Height, 0f, out _);
			availableYSlices = new float[ySlices.Length];
			float runningTotalY = 0f;
			for (int i = 0; i < availableYSlices.Length; i++) {
				runningTotalY += availableYLengths[i];
				availableYSlices[i] = runningTotalY;
			}
		}

		public NSliceScaling(Size reference, Rectangle available, NSliceValues values) : this(reference, available, values.xs, values.ys) { }

		private static void ProcessLengths(float total, float[] inputSlices, out float[] slices, out Dimension[] lengths) {
			if (inputSlices.Length == 0) {
				slices = Array.Empty<float>();
				lengths = Array.Empty<Dimension>();
				return;
			}

			slices = inputSlices.Where(pos => pos >= 0 && pos <= total).OrderBy(f => f).ToArray(); // Data sanitization: sort values, remove out-of-bounds values

			if (slices.Length == 0) {
				//slices = new float[0];
				lengths = Array.Empty<Dimension>();
				return;
			}

			lengths = new Dimension[slices.Length + 1];
			for (int i = 0; i < slices.Length; i++) {
				if (i % 2 == 0) {
					lengths[i] = Dimension.FromPoints(slices[i] - (i > 0 ? slices[i - 1] : 0f));
				}
				else {
					lengths[i] = Dimension.FromRelative(slices[i] - slices[i - 1]);
				}
			}
			if (slices.Length % 2 == 0) {
				lengths[lengths.Length - 1] = Dimension.FromPoints(total - slices[slices.Length - 1]);
			}
			else {
				lengths[lengths.Length - 1] = Dimension.FromRelative(total - slices[slices.Length - 1]);
			}
		}

		private static int GetIndex(float pos, float[] slices) {
			for (int i = 0; i < slices.Length; i++) {
				if ((i % 2 == 0 && pos <= slices[i]) || (i % 2 != 0 && pos < slices[i])) {
					return i;
				}
			}
			return slices.Length;
		}

		private static float TransformPos(float pos, float scale, float[] slices, float[] availableSlices, Dimension[] lengths, float[] availableLengths, Size reference, Rectangle available) {
			if (slices.Length == 0) {
				float relPos = pos / reference.Width;
				float finalPos = relPos * available.Width;
				return finalPos; // TODO Is this right?
			}

			int index = GetIndex(pos, slices);

			float referencePoint = index == 0 ? 0f : slices[index - 1];
			float availableReference = index == 0 ? 0f : availableSlices[index - 1];
			float offset = pos - referencePoint;

			float transformedPos;
			if (index % 2 == 0) {
				// In abs slice
				transformedPos = availableReference + scale * offset;
			}
			else {
				// In var slice
				float size = lengths[index].Relative;
				float availableSize = availableLengths[index];
				transformedPos = availableReference + (offset / size) * availableSize;
			}
			return transformedPos;
		}

		private static float InvertTransformPos(float pos, float scale, float[] slices, float[] availableSlices, Dimension[] lengths, float[] availableLengths, Size reference, Rectangle available) {
			if (slices.Length == 0) {
				float relPos = pos / available.Width;
				float finalPos = relPos * reference.Width;
				return finalPos; // TODO Is this right?
			}

			int index = GetIndex(pos, availableSlices);
			float referencePoint = index == 0 ? 0f : slices[index - 1];
			float availableReference = index == 0 ? 0f : availableSlices[index - 1];
			float offset = pos - availableReference;

			float originalPos;
			if (index % 2 == 0) {
				// In abs slice
				originalPos = referencePoint + offset / scale;
			}
			else {
				// In var slice
				float size = lengths[index].Relative;
				float availableSize = availableLengths[index];
				originalPos = referencePoint + (offset / availableSize) * size;
			}
			return originalPos;
		}

		public DrawPoint TransformPoint(DrawPoint point) {
			return new DrawPoint(
				TransformPos(point.X, scale, xSlices, availableXSlices, xWidths, availableXLengths, reference, available),
				TransformPos(point.Y, scale, ySlices, availableYSlices, yHeights, availableYLengths, reference, available)
				);
		}
		public DrawPoint InvertTransformPoint(DrawPoint point) {
			return new DrawPoint(
				InvertTransformPos(point.X, scale, xSlices, availableXSlices, xWidths, availableXLengths, reference, available),
				InvertTransformPos(point.Y, scale, ySlices, availableYSlices, yHeights, availableYLengths, reference, available)
				);
		}

		public float TransformLength(float length) {
			return length * scale;
		}

		public float InvertTransformLength(float length) {
			return length / scale;
		}

		public static DrawPoint StretchPoint(Size reference, DrawPoint point, Rectangle available) {
			float relPosX = point.X / reference.Width; // (point.X - reference.Left) / reference.Width;
			float x = available.Left + relPosX * available.Width;

			float relPosY = point.Y / reference.Height; //  (point.Y - reference.Bottom) / reference.Height;
			float y = available.Bottom + relPosY * available.Height;

			return new DrawPoint(x, y);
		}

		public static DrawPoint InvertStretchPoint(Size reference, DrawPoint point, Rectangle available) {
			float relPosX = (point.X - available.Left) / available.Width;
			float x = relPosX * reference.Width; // reference.Left + relPosX * reference.Width;

			float relPosY = (point.Y - available.Bottom) / available.Height;
			float y = relPosY * reference.Height; // reference.Bottom + relPosY * reference.Height;

			return new DrawPoint(x, y);
		}

		public static float StretchLength(Size reference, float length, Rectangle available) {
			float factor = Math.Min(available.Width / reference.Width, available.Height / reference.Height);
			return length * factor;
		}

		public static float InvertStretchLength(Size reference, float length, Rectangle available) {
			float factor = Math.Min(available.Width / reference.Width, available.Height / reference.Height);
			return length / factor;
		}

		private readonly struct InferDetails {
			public readonly float startAbs;
			public readonly float startRel;
			public readonly float innerAbs;
			public readonly float innerRel;
			public readonly float totalAbs;
			public readonly float totalRel;
			public readonly float scale;

			public InferDetails(float startAbs, float startRel, float innerAbs, float innerRel, float totalAbs, float totalRel, float scale) {
				this.startAbs = startAbs;
				this.startRel = startRel;
				this.innerAbs = innerAbs;
				this.innerRel = innerRel;
				this.totalAbs = totalAbs;
				this.totalRel = totalRel;
				this.scale = scale;
			}
		}

		private static void InferCanvasPositionDetails(float startRef, float endRef, float startVal, float endVal, float refLength, float[] refSlices, Dimension[] refLengths, out InferDetails details) {
			int startIndex = GetIndex(startRef, refSlices);
			int endIndex = GetIndex(endRef, refSlices);
			float innerLength = endVal - startVal;

			float startAbs = 0f;
			float startRel = 0f;
			for (int i = 0; i < startIndex; i++) {
				Dimension length = refLengths[i];
				startAbs += length.Absolute;
				startRel += length.Relative;
			}
			float startReferencePoint = startIndex == 0 ? 0f : refSlices[startIndex - 1];
			float startSliceOffset = startRef - startReferencePoint;
			if (startIndex % 2 == 0) { startAbs += startSliceOffset; }
			else { startRel += startSliceOffset; }

			float innerAbs = 0f;
			float innerRel = 0f;
			for (int i = startIndex + 1; i < endIndex; i++) {
				Dimension length = refLengths[i];
				innerAbs += length.Absolute;
				innerRel += length.Relative;
			}
			if (startIndex == endIndex) {
				float diff = endRef - startRef;
				if (startIndex % 2 == 0) { innerAbs += diff; }
				else { innerRel += diff; }
			}
			else {
				float startRefPoint = startIndex < refSlices.Length ? refSlices[startIndex] : refLength;
				float startOffset = startRefPoint - startRef;
				if (startIndex % 2 == 0) { innerAbs += startOffset; }
				else { innerRel += startOffset; }

				float endRefPoint = endIndex > 0 ? refSlices[endIndex - 1] : 0f;
				float endOffset = endRef - endRefPoint;
				if (endIndex % 2 == 0) { innerAbs += endOffset; }
				else { innerRel += endOffset; }
			}

			float totalAbs = 0f;
			float totalRel = 0f;
			for (int i = 0; i < refLengths.Length; i++) {
				Dimension length = refLengths[i];
				totalAbs += length.Absolute;
				totalRel += length.Relative;
			}

			float scale = innerLength < innerAbs ? innerLength / innerAbs : 1f;

			details = new InferDetails(startAbs, startRel, innerAbs, innerRel, totalAbs, totalRel, scale);
		}

		public static Rectangle InferFullRect(Size reference, float[] xs, float[] ys, Rectangle innerReference, Rectangle example) {
			ProcessLengths(reference.Width, xs, out float[] xSlices, out Dimension[] xWidths);
			ProcessLengths(reference.Height, ys, out float[] ySlices, out Dimension[] yHeights);

			InferCanvasPositionDetails(innerReference.Left, innerReference.Right, example.Left, example.Right, reference.Width, xSlices, xWidths, out InferDetails xDetails);
			InferCanvasPositionDetails(innerReference.Bottom, innerReference.Top, example.Bottom, example.Top, reference.Height, ySlices, yHeights, out InferDetails yDetails);

			float scale = Math.Min(xDetails.scale, yDetails.scale);

			float relRemainingX = example.Width - scale * xDetails.innerAbs;
			float width = (relRemainingX / xDetails.innerRel) * xDetails.totalRel + scale * xDetails.totalAbs;
			float x = example.Left - ((relRemainingX / xDetails.innerRel) * xDetails.startRel + scale * xDetails.startAbs);

			float relRemainingY = example.Height - scale * yDetails.innerAbs;
			float height = (relRemainingY / yDetails.innerRel) * yDetails.totalRel + scale * yDetails.totalAbs;
			float y = example.Bottom - ((relRemainingY / yDetails.innerRel) * yDetails.startRel + scale * yDetails.startAbs);

			return new Rectangle(x, y, width, height);
		}

		public static Rectangle InferFullRectStretch(Size reference, Rectangle innerReference, Rectangle example) {
			float width = (reference.Width / innerReference.Width) * example.Width;
			float height = (reference.Height / innerReference.Height) * example.Height;
			float xOffset = (example.Width / innerReference.Width) * innerReference.Left; // (example.Width / innerReference.Width) * (innerReference.Left - reference.Left);
			float yOffset = (example.Height / innerReference.Height) * innerReference.Bottom; // (example.Height / innerReference.Height) * (innerReference.Bottom - reference.Bottom);
			return new Rectangle(example.Left - xOffset, example.Bottom - yOffset, width, height);
		}
	}

}
