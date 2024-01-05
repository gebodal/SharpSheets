using SharpSheets.Exceptions;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace SharpSheets.Layouts {

	/// <summary>
	/// Indicates the strategy to use when dividing a rectangle into segments based on
	/// a <see cref="Dimension"/> list.
	/// </summary>
	public enum DivisionStrategy : byte {
		/// <summary>
		/// Relative dimensions are interpreted to mean the distances between the centre of the gutters.
		/// </summary>
		RELATIVE_DIVISIONS,
		/// <summary>
		/// Relative dimensions are interpreted as the sizes of the rectangles themselves.
		/// </summary>
		RELATIVE_RECTANGLES
	}

	/// <summary>
	/// Indicates the arrangement of sub-rectangles within a rectangular area, and how
	/// those sub-rectangles should be positioned as a group within the larger area.
	/// This does not affect the ordering of those rectangles within the space.
	/// The exact meaning of this arrangement is dependent on whether the layout is to
	/// be arranged vertically or horizontally.
	/// </summary>
	public enum Arrangement : byte {
		/// <summary>
		/// Indicates that the layout should be left or top aligned, with any remaining space
		/// to the right or bottom.
		/// </summary>
		FRONT,
		/// <summary>
		/// Indicates that the layout should be aligned centrally in the available space, with
		/// and remaining space equally distributed to the left and right, or top and bottom,
		/// as appropriate.
		/// </summary>
		CENTRAL,
		/// <summary>
		/// Indicates that the layout should be right or bottom aligned, with any remaining space
		/// to the left or top.
		/// </summary>
		BACK
	}

	/// <summary>
	/// Indicates the order for a series of sub-areas within a larger area.
	/// The exact meaning of this ordering is dependent on whether the layout it so
	/// be arranged vertically or horizontally.
	/// </summary>
	public enum LayoutOrder : byte {
		/// <summary>
		/// Indicates that the layout should be ordered left-to-right or top-to-bottom.
		/// </summary>
		FORWARD,
		/// <summary>
		/// Indicates that the layout should be ordered right-to-left or bottom-to-top.
		/// </summary>
		BACKWARD
	}

	public static class Divisions {

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Rows(Rectangle rect, int nRows, float gutter) {
			return Rows(rect, ArrayUtils.MakeArray(nRows, Dimension.FromRelative(1f)), gutter, out _, false, Arrangement.FRONT, LayoutOrder.FORWARD);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Rows(Rectangle rect, int nRows, float gutter, out Rectangle?[] gutters) {
			return Rows(rect, ArrayUtils.MakeArray(nRows, Dimension.FromRelative(1f)), gutter, out gutters, false, Arrangement.FRONT, LayoutOrder.FORWARD);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Rows(Rectangle fullRect, Dimension[] heights, float gutter, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			return Rows(fullRect, heights, gutter, 0f, out _, out _, out _, preventOverflow, arrangement, order, strategy);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Rows(Rectangle fullRect, Dimension[] heights, float gutter, out Rectangle?[] gutters, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			return Rows(fullRect, heights, gutter, 0f, out _, out gutters, out _, preventOverflow, arrangement, order, strategy);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Rows(Rectangle fullRect, Dimension[] heights, float gutter, float remainingGutter, out Rectangle? remainingRect, out Rectangle?[] gutters, out Rectangle? remainingGutterRect, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {

			ValidateRect(fullRect);

			float[] absoluteHeights = ToAbsolute(heights, fullRect.Height, gutter, out float remainingLength, strategy);

			Rectangle?[] rects = new Rectangle[heights.Length];
			gutters = new Rectangle[Math.Max(0, heights.Length - 1)];
			float current = arrangement == Arrangement.FRONT ? 0f : (arrangement == Arrangement.CENTRAL ? remainingLength / 2f : remainingLength);
			bool overflow = false;

			if (order == LayoutOrder.BACKWARD) {
				for (int i = 0; i < heights.Length; i++) {
					Rectangle nextRect = new Rectangle(fullRect.X, fullRect.Bottom + current, fullRect.Width, absoluteHeights[i]);
					current += absoluteHeights[i];
					Rectangle nextGutter = new Rectangle(fullRect.X, fullRect.Bottom + current, fullRect.Width, gutter);
					current += gutter;
					if (preventOverflow && nextRect.Top > fullRect.Top) {
						overflow = true;
						break;
					}
					if (i < heights.Length - 1) {
						gutters[i] = nextGutter;
					}
					rects[i] = nextRect;
				}

				float remainingRectBottom = fullRect.Bottom + current - gutter + remainingGutter;
				if (!overflow && remainingRectBottom < fullRect.Top) {
					remainingRect = new Rectangle(fullRect.X, remainingRectBottom, fullRect.Width, fullRect.Top - remainingRectBottom);
					remainingGutterRect = new Rectangle(fullRect.X, remainingRectBottom - remainingGutter, fullRect.Width, remainingGutter);
				}
				else {
					remainingRect = null;
					remainingGutterRect = null;
				}
			}
			else {
				for (int i = 0; i < heights.Length; i++) {
					current += absoluteHeights[i];
					Rectangle nextRect = new Rectangle(fullRect.X, fullRect.Top - current, fullRect.Width, absoluteHeights[i]);
					if (preventOverflow && nextRect.Bottom < fullRect.Bottom) {
						overflow = true;
						break;
					}
					current += gutter;
					if (i < heights.Length - 1) {
						gutters[i] = new Rectangle(fullRect.X, fullRect.Top - current, fullRect.Width, gutter);
					}
					rects[i] = nextRect;
				}

				float remainingRectTop = fullRect.Top - current + gutter - remainingGutter;
				if (!overflow && remainingRectTop > fullRect.Bottom) {
					remainingRect = new Rectangle(fullRect.X, fullRect.Y, fullRect.Width, remainingRectTop - fullRect.Bottom);
					remainingGutterRect = new Rectangle(fullRect.X, remainingRectTop, fullRect.Width, remainingGutter);
				}
				else {
					remainingRect = null;
					remainingGutterRect = null;
				}
			}


			return rects;
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Rows(Rectangle rect, Dimension height, int num, float gutter, float remainingGutter, out Rectangle? remainingRect, out Rectangle?[] gutters, out Rectangle? remainingGutterRect, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			return Rows(rect, ArrayUtils.MakeArray(num, height), gutter, remainingGutter, out remainingRect, out gutters, out remainingGutterRect, preventOverflow, arrangement, order, strategy);
		}
		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Rows(Rectangle rect, Dimension height, int num, float gutter, out Rectangle?[] gutters, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			return Rows(rect, ArrayUtils.MakeArray(num, height), gutter, 0f, out _, out gutters, out _, preventOverflow, arrangement, order, strategy);
		}
		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle? Row(Rectangle rect, Dimension height, float gutter, out Rectangle? remainingRect, out Rectangle? remainingGutterRect, bool preventOverflow, Arrangement arrangement, LayoutOrder order) {
			return Rows(rect, new Dimension[] { height }, gutter, gutter, out remainingRect, out _, out remainingGutterRect, preventOverflow, arrangement, order, DivisionStrategy.RELATIVE_DIVISIONS)[0];
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Columns(Rectangle rect, int nCols, float gutter) {
			return Columns(rect, ArrayUtils.MakeArray(nCols, Dimension.FromRelative(1f)), gutter, out _, false, Arrangement.FRONT, LayoutOrder.FORWARD, DivisionStrategy.RELATIVE_DIVISIONS);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Columns(Rectangle rect, int nCols, float gutter, out Rectangle?[] gutters) {
			return Columns(rect, ArrayUtils.MakeArray(nCols, Dimension.FromRelative(1f)), gutter, out gutters, false, Arrangement.FRONT, LayoutOrder.FORWARD, DivisionStrategy.RELATIVE_DIVISIONS);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Columns(Rectangle fullRect, Dimension[] widths, float gutter, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			return Columns(fullRect, widths, gutter, 0f, out _, out _, out _, preventOverflow, arrangement, order, strategy);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Columns(Rectangle fullRect, Dimension[] widths, float gutter, out Rectangle?[] gutters, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			return Columns(fullRect, widths, gutter, 0f, out _, out gutters, out _, preventOverflow, arrangement, order, strategy);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Columns(Rectangle fullRect, Dimension[] widths, float gutter, float remainingGutter, out Rectangle? remainingRect, out Rectangle?[] gutters, out Rectangle? remainingGutterRect, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {

			ValidateRect(fullRect);

			float[] absoluteWidths = ToAbsolute(widths, fullRect.Width, gutter, out float remainingLength, strategy);

			Rectangle?[] rects = new Rectangle[widths.Length];
			gutters = new Rectangle[Math.Max(0, widths.Length - 1)];
			float current = arrangement == Arrangement.FRONT ? 0f : (arrangement == Arrangement.CENTRAL ? remainingLength / 2f : remainingLength);
			bool overflow = false;

			if (order == LayoutOrder.BACKWARD) {
				for (int i = 0; i < widths.Length; i++) {
					current += absoluteWidths[i];
					Rectangle nextRect = new Rectangle(fullRect.Right - current, fullRect.Bottom, absoluteWidths[i], fullRect.Height);
					if (preventOverflow && nextRect.Left < fullRect.Left) {
						overflow = true;
						break;
					}
					current += gutter;
					if (i < widths.Length - 1) {
						gutters[i] = new Rectangle(fullRect.Right - current, fullRect.Bottom, gutter, fullRect.Height);
					}
					rects[i] = nextRect;
				}

				float remainingRectRight = fullRect.Right - current + gutter - remainingGutter;
				if (!overflow && remainingRectRight > fullRect.Left) {
					remainingRect = new Rectangle(fullRect.X, fullRect.Y, remainingRectRight - fullRect.Left, fullRect.Height);
					remainingGutterRect = new Rectangle(remainingRectRight, fullRect.Y, remainingGutter, fullRect.Height);
				}
				else {
					remainingRect = null;
					remainingGutterRect = null;
				}
			}
			else {
				for (int i = 0; i < widths.Length; i++) {
					Rectangle nextRect = new Rectangle(fullRect.Left + current, fullRect.Bottom, absoluteWidths[i], fullRect.Height);
					current += absoluteWidths[i];
					Rectangle nextGutter = new Rectangle(fullRect.Left + current, fullRect.Bottom, gutter, fullRect.Height);
					current += gutter;
					if (preventOverflow && nextRect.Right > fullRect.Right) {
						overflow = true;
						break;
					}
					if (i < widths.Length - 1) {
						gutters[i] = nextGutter;
					}
					rects[i] = nextRect;
				}

				float remainingRectLeft = fullRect.Left + current - gutter + remainingGutter;
				if (!overflow && remainingRectLeft < fullRect.Right) {
					remainingRect = new Rectangle(remainingRectLeft, fullRect.Y, fullRect.Right - remainingRectLeft, fullRect.Height);
					remainingGutterRect = new Rectangle(remainingRectLeft - remainingGutter, fullRect.Y, remainingGutter, fullRect.Height);
				}
				else {
					remainingRect = null;
					remainingGutterRect = null;
				}
			}

			return rects;
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Columns(Rectangle rect, Dimension width, int num, float gutter, float remainingGutter, out Rectangle? remainingRect, out Rectangle?[] gutters, out Rectangle? remainingGutterRect, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			return Columns(rect, ArrayUtils.MakeArray(num, width), gutter, remainingGutter, out remainingRect, out gutters, out remainingGutterRect, preventOverflow, arrangement, order, strategy);
		}
		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] Columns(Rectangle rect, Dimension width, int num, float gutter, out Rectangle?[] gutters, bool preventOverflow, Arrangement arrangement, LayoutOrder order, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			return Columns(rect, ArrayUtils.MakeArray(num, width), gutter, 0f, out _, out gutters, out _, preventOverflow, arrangement, order, strategy);
		}
		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle? Column(Rectangle rect, Dimension width, float gutter, out Rectangle? remainingRect, out Rectangle? remainingGutterRect, bool preventOverflow, Arrangement arrangement, LayoutOrder order) {
			return Columns(rect, new Dimension[] { width }, gutter, gutter, out remainingRect, out _, out remainingGutterRect, preventOverflow, arrangement, order, DivisionStrategy.RELATIVE_DIVISIONS)[0];
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle[] FillRows(Rectangle rect, Dimension height, float gutter, out Rectangle?[] gutters, Arrangement arrangement, LayoutOrder order) {
			Dimension[] heights = Fill(rect.Height, height, gutter);
			Rectangle?[] createdRows = Rows(rect, heights, gutter, out Rectangle?[] createdGutters, false, arrangement, order);

			Rectangle[] rows = CheckRectAray(createdRows, "Error filling rows.");
			gutters = CheckRectAray(createdGutters, "Error filling rows.");
			return rows;
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle[] FillColumns(Rectangle rect, Dimension width, float gutter, out Rectangle[] gutters, Arrangement arrangement, LayoutOrder order) {
			Dimension[] widths = Fill(rect.Width, width, gutter);
			Rectangle?[] createdCols = Columns(rect, widths, gutter, out Rectangle?[] createdGutters, false, arrangement, order);

			Rectangle[] columns = CheckRectAray(createdCols, "Error filling columns.");
			gutters = CheckRectAray(createdGutters, "Error filling columns.");
			return columns;
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		private static Rectangle[] CheckRectAray(Rectangle?[] rects, string message) {
			Rectangle[] processed = new Rectangle[rects.Length];
			for (int i = 0; i < rects.Length; i++) {
				processed[i] = rects[i] ?? throw new InvalidRectangleException(message);
			}
			return processed;
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		private static void ValidateRect(Rectangle rect) {
			if(rect == null) {
				throw new InvalidRectangleException("Provided rectangle is null.");
			}
			else if(rect.Width < 0 || rect.Height < 0) {
				throw new InvalidRectangleException("Provided rectangle is invalid.");
			}
		}

		public static float[] ToAbsolute(Dimension[] source, float available, float gutter, out float remaining, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			float[] result = new float[source.Length];

			if(source.Length == 0) {
				remaining = available;
				return result;
			}

			Dimension total = source.Sum();

			float runningTotal = 0f;

			for (int i = 0; i < result.Length; i++) {
				if (source[i].Auto) {
					throw new ArgumentException("Cannot create absolute length from automatic Dimension.");
				}

				result[i] = source[i].Absolute;
				result[i] += available * source[i].Percent;

				if (strategy == DivisionStrategy.RELATIVE_RECTANGLES || source[i].Absolute > 0 || source[i].IsZero) {
					result[i] += gutter;
				}

				runningTotal += result[i];
			}

			float remainingLength = (available + gutter) - runningTotal;

			for (int i = 0; total.Relative > 0 && i < result.Length; i++) {
				result[i] += remainingLength * (source[i].Relative / total.Relative);
			}

			for(int i=0; i<result.Length; i++) {
				result[i] -= gutter;

				/*
				if(result[i] < 0) {
					throw new ArgumentException($"Dimension {i} ({source[i]}) produces a negative length for this division.");
				}
				*/
			}

			remaining = total.Relative > 0 ? 0f : remainingLength;

			return result;
		}

		public static float InferLength(Dimension[] source, float gutter, Dimension accounted, float accountedLength, DivisionStrategy strategy = DivisionStrategy.RELATIVE_DIVISIONS) {
			
			// TODO Need to check that this works as expected
			
			if (source.Length == 0) {
				throw new ArgumentException("Must provide valid list of source dimensions.");
			}

			Dimension totalDimension = source.Sum();

			float remaining = accounted.Relative > 0 ? (accountedLength + gutter) * totalDimension.Relative / accounted.Relative : 0f;
			float availableMinusRunningTotal = remaining - gutter;

			float available = availableMinusRunningTotal;
			for (int i = 0; i < source.Length; i++) {
				available -= source[i].Absolute;
				if (strategy == DivisionStrategy.RELATIVE_RECTANGLES || source[i].Absolute > 0) {
					available -= gutter;
				}
			}

			float final = available / totalDimension.Percent;

			return final;
		}

		public static Dimension[] Fill(float available, Dimension length, float gutter) {
			if((length.Absolute == 0f && length.Percent == 0f) || length.Relative > 0f) {
				return new Dimension[] { length };
			}

			List<Dimension> result = new List<Dimension>();
			float remaining = available;

			bool first = true;

			float individualLength = length.Absolute + available * length.Percent;

			while (remaining >= (individualLength + (first ? 0f : gutter))) {
				result.Add(length);

				remaining -= individualLength;

				if (first) {
					first = false;
				}
				else {
					remaining -= gutter;
				}
			}

			return result.ToArray();
		}

		public static Rectangle[,] Grid(Rectangle rect, int rows, int columns, float horizontalGutter, float verticaGutter, out Rectangle[] rowGutters, out Rectangle[] columnGutters) {
			return Grid(rect, ArrayUtils.MakeArray(rows, 1f), ArrayUtils.MakeArray(columns, 1f), horizontalGutter, verticaGutter, out rowGutters, out columnGutters);
		}

		public static Rectangle[,] Grid(Rectangle fullRect, float[] heights, float[] widths, float horizontalGutter, float verticalGutter, out Rectangle[] rowGutters, out Rectangle[] columnGutters) {
			// TODO heights and widths should be Dimension[]
			Rectangle[,] grid = new Rectangle[heights.Length, widths.Length];

			// TODO Return gutters
			rowGutters = new Rectangle[heights.Length - 1];
			columnGutters = new Rectangle[widths.Length - 1];

			Rectangle rect = fullRect.Margins(verticalGutter / 2, horizontalGutter / 2, true);

			float totalHeight = heights.Sum();
			float totalWidth = widths.Sum();

			float currentY = 0f;
			for(int i=0; i<grid.GetLength(0); i++) { // Rows
				currentY += heights[i];
				float currentX = 0f;
				for(int j=0; j<grid.GetLength(1); j++) { // Columns
					grid[i,j] =
						new Rectangle(
							rect.Left + (currentX / totalWidth) * rect.Width, 
							rect.Top - (currentY / totalHeight) * rect.Height, 
							(widths[j] / totalWidth) * rect.Width, 
							(heights[i] / totalHeight) * rect.Height)
						.Margins(verticalGutter / 2, horizontalGutter / 2, false);
					currentX += widths[j];
					if(grid[i,j].Width<0 || grid[i, j].Height < 0) {
						throw new InvalidRectangleException($"Cannot create ({i},{j})th Rectangle for grid.");
					}

					if(i==0 && j < columnGutters.Length) {
						columnGutters[j] = new Rectangle(rect.Left + (currentX / totalWidth) * rect.Width - horizontalGutter / 2, fullRect.Bottom, horizontalGutter, fullRect.Height);
					}
				}

				if(i < rowGutters.Length) {
					rowGutters[i] = new Rectangle(fullRect.Left, rect.Top - (currentY / totalHeight) * rect.Height - verticalGutter / 2, fullRect.Width, verticalGutter);
				}
			}

			return grid;
		}

		public static float CalculateTotalLength(IEnumerable<float> lengths, float gutter) {
			float total = 0f;
			bool first = true;
			foreach(float length in lengths) {
				if (first) { first = false; }
				else { total += gutter; }
				total += length;
			}
			return total;
		}

		public static float CalculateTotalLength(float length, int num, float gutter) {
			return (length * num) + Math.Max(0, num - 1) * gutter;
		}

		public static Dimension CalculateTotalLength(IEnumerable<Dimension> lengths, float gutter) {
			Dimension[] values = lengths.ToArray();
			Dimension totalLength = values.Sum();
			Dimension gutterLength = Dimension.FromPoints(gutter * Math.Max(0, values.Length - 1));
			return totalLength + gutterLength;
		}

		public static Dimension CalculateTotalLength(Dimension length, int num, float gutter) {
			return CalculateTotalLength(ArrayUtils.MakeArray(num, length), gutter);
		}
	}

	[Serializable]
	public class InvalidRectangleException : SharpSheetsException {
		public InvalidRectangleException(string message) : base(message) { }
		public InvalidRectangleException(string message, Exception innerException) : base(message, innerException) { }
		protected InvalidRectangleException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}
