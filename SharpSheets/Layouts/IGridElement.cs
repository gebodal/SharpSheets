using SharpSheets.Canvas;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SharpSheets.Layouts {

	public interface IGridElement {

		// TODO float AspectRatio { get; } ???

		Dimension? Size { get; }
		Position? Position { get; }
		Margins Margins { get; }

		Layout Layout { get; }
		Arrangement Arrangement { get; }
		LayoutOrder Order { get; }
		float Gutter { get; }

		bool ProvidesRemaining { get; }

		IReadOnlyList<IGridElement> Children { get; } // TODO If these are here, why do some of the methods below take a Children argument separately? (There may be a reason, I just can't remember)

		/*
		Dimension? GetSize(ISharpCanvas canvas);
		Position? GetPosition(ISharpCanvas canvas);
		Margins GetMargins(ISharpCanvas canvas);

		//IList<IGridElement> GetChildElements(ISharpCanvas canvas);
		Layout GetLayout(ISharpCanvas canvas);
		float GetGutter(ISharpCanvas canvas);
		*/

		// The areas for both of these methods have already had their margins adjusted
		/// <summary></summary>
		/// <param name="graphicsState"></param>
		/// <param name="fullArea"></param>
		/// <returns></returns>
		/// <exception cref="InvalidRectangleException"></exception>
		Rectangle? ContainerArea(ISharpGraphicsState graphicsState, Rectangle fullArea);

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		Size? MinimumContentSize(ISharpGraphicsState graphicsState, Size available);

		[MemberNotNullWhen(true, nameof(Size))]
		sealed bool IsAutoSize() {
			return Size.HasValue && Size.Value.Auto;
		}

		[MemberNotNullWhen(true, nameof(Position))]
		sealed bool IsInset() {
			//return !element.Size.HasValue && element.Position.HasValue;
			return Position.HasValue;
		}
	}

	//public enum Axis : byte { ROWS, COLUMNS };
	/// <summary>
	/// An enum to indicate the direction of arrangement for element contents, either vertically (rows) or horizontally (columns).
	/// </summary>
	public enum Layout {
		/// <summary>Indicates that the contents will be arranged vertically, in rows.</summary>
		ROWS,
		/// <summary>Indicates that the contents will be arrange horizontally, in columns.</summary>
		COLUMNS
	}

	public static class GridElementUtils {

		/*
		public static bool IsAutoSize(this IGridElement element) {
			Dimension? elementSize = element.Size;
			return elementSize.HasValue && elementSize.Value.Auto;
		}

		public static bool IsInset(this IGridElement element) {
			//return !element.Size.HasValue && element.Position.HasValue;
			return element.Position.HasValue;
		}
		*/

		// TODO What exactly does availableSpace mean here?
		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Size MinimumSize(this IGridElement element, ISharpGraphicsState graphicsState, Layout parentLayout, Size availableSpace) {

			Margins elementMargins = element.Margins;
			Dimension? elementSize = element.Size;

			availableSpace = availableSpace.Margins(elementMargins, false); // This seems right...?

			// Size intrinsic to this element
			Size minimum = element.MinimumContentSize(graphicsState, availableSpace) ?? new Size(0f, 0f);

			// Fix size to any absolute value provided
			if (elementSize.HasValue && elementSize.Value.HasAbsolute) {
				if (parentLayout == Layout.COLUMNS) {
					//minimum.Width = element.Size.Value.Absolute;
					minimum = new Size(elementSize.Value.Absolute, minimum.Height);
				}
				else {
					//minimum.Height = element.Size.Value.Absolute;
					minimum = new Size(minimum.Width, elementSize.Value.Absolute);
				}
			}

			// Take account of margins of this rect
			minimum = minimum.Margins(elementMargins, true);

			return minimum;
		}

		// TODO Need to formally define meaning of overallLayout and availableSpace
		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Dimension? FinalSizeFromArrangement(this IGridElement element, ISharpGraphicsState graphicsState, Layout overallLayout, Size availableSpace) {
			Dimension? finalSize;
			if (element.IsAutoSize()) {
				Size minSize = element.MinimumSize(graphicsState, overallLayout, availableSpace);
				finalSize = Dimension.FromPoints(overallLayout == Layout.COLUMNS ? minSize.Width : minSize.Height);
			}
			else {
				finalSize = element.Size;
			}
			return finalSize;
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] GetElementRects(this IGridElement element, ISharpGraphicsState graphicsState, Rectangle rect, out Rectangle elementRect, out Rectangle? containerRect, out Rectangle?[] gutters, out Rectangle? remainingRect, out Rectangle? remainingGutter) {
			return GridElements.GetElementRects(element, element.Children, graphicsState, rect, out elementRect, out containerRect, out gutters, out remainingRect, out remainingGutter);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] GetElementRects(this IGridElement element, ISharpGraphicsState graphicsState, Rectangle rect, out Rectangle? remainingRect) {
			return GridElements.GetElementRects(element, element.Children, graphicsState, rect, out _, out _, out _, out remainingRect, out _);
		}

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle? RemainingRect(this IGridElement container, ISharpGraphicsState graphicsState, Rectangle rect) {
			if (!container.ProvidesRemaining) {
				return null;
			}

			Rectangle?[] elementRects = container.GetElementRects(graphicsState, rect, out _, out _, out _, out Rectangle? remainingRect, out _);

			// TODO Is this really the behaviour we want?
			if (remainingRect != null) {
				// TODO There is a problem here
				// If all the children are Inset (i.e. Position instead of Size),
				// then the remainingRect will be the full rect, without accounting for
				// one of the Inset children providing the remaining space.
				// Is this the correct behaviour?
				return remainingRect;
			}
			else {
				IReadOnlyList<IGridElement> containerElements = container.Children;
				for (int i = containerElements.Count - 1; i >= 0; i--) {
					if (elementRects[i] != null && containerElements[i].RemainingRect(graphicsState, elementRects[i]!) is Rectangle elementRemainder) {
						return elementRemainder;
					}
				}
				return null;
			}
		}

	}

	public static class GridElements {

		private readonly struct CanvasElement {
			public readonly IGridElement element;
			public readonly Dimension? dimension;

			public CanvasElement(IGridElement element, Dimension? dimension) {
				this.element = element;
				this.dimension = dimension;
			}
		}

		// TODO This method should possibly throw an error rather than return a nullable in failure cases
		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Size? OverallMinimumSize(IReadOnlyList<IGridElement> elements, ISharpGraphicsState graphicsState, Size availableSpace, Layout layout, float gutter) {
			// Order of children not important, so a list like this will work fine
			List<Size> elementMinimums = new List<Size>();

			List<CanvasElement> remainingElements = new List<CanvasElement>();
			float remainingLength = layout == Layout.COLUMNS ? availableSpace.Width : availableSpace.Height;
			for (int i = 0; i < elements.Count; i++) {
				IGridElement element = elements[i];
				Dimension? elementSize = element.Size;
				if (element.IsAutoSize()) {
					Size elementMin = element.MinimumSize(graphicsState, layout, availableSpace);
					remainingLength -= layout == Layout.COLUMNS ? elementMin.Width : elementMin.Height;
					elementMinimums.Add(elementMin);
				}
				else if (elementSize?.HasAbsolute ?? false) {
					Size elementAvailable;
					if (layout == Layout.COLUMNS) {
						elementAvailable = new Size(elementSize.Value.Absolute, availableSpace.Height);
					}
					else {
						elementAvailable = new Size(availableSpace.Width, elementSize.Value.Absolute);
					}
					Size elementMin = element.MinimumSize(graphicsState, layout, elementAvailable);
					remainingLength -= layout == Layout.COLUMNS ? elementMin.Width : elementMin.Height;
					elementMinimums.Add(elementMin);
				}
				else if (elementSize?.Relative > 0f || elementSize?.Percent > 0f) {
					remainingElements.Add(new CanvasElement(element, elementSize));
				}
			}

			// Need to remove gutter spacing for the children with absolute sizes
			remainingLength -= Math.Max(0, elementMinimums.Count - 1) * gutter;
			// Also need to remove an extra gutter spacing if there are remaining children
			if (remainingElements.Count > 0 && elementMinimums.Count > 0) { remainingLength -= gutter; }

			Rectangle remainingElementsRect;
			if (layout == Layout.COLUMNS) {
				remainingElementsRect = new Rectangle(remainingLength, availableSpace.Height);
			}
			else {
				remainingElementsRect = new Rectangle(availableSpace.Width, remainingLength);
			}

			if (remainingElementsRect.Width < 0f || remainingElementsRect.Height < 0f) {
				//throw new SharpLayoutException(this, "Not enough space to draw children.");
				//return null; // TODO Better to just return zero?

				Size requiredSpace;
				if (layout == Layout.COLUMNS) {
					requiredSpace = new Size(availableSpace.Width - remainingLength, availableSpace.Height);
				}
				else {
					requiredSpace = new Size(availableSpace.Width, availableSpace.Height - remainingLength);
				}

				if (requiredSpace.Width >= 0f && requiredSpace.Height >= 0f) {
					return requiredSpace;
				}
				else {
					return null;
				}
			}

			Dimension[] rectSizes = remainingElements.Select(c => c.dimension ?? Dimension.Single).ToArray();
			Rectangle?[] remainingElementsAvailableSpaces;

			if (layout == Layout.COLUMNS) {
				remainingElementsAvailableSpaces = Divisions.Columns(remainingElementsRect, rectSizes, gutter, false, Arrangement.FRONT, LayoutOrder.FORWARD);
			}
			else {
				remainingElementsAvailableSpaces = Divisions.Rows(remainingElementsRect, rectSizes, gutter, false, Arrangement.FRONT, LayoutOrder.FORWARD);
			}

			for (int i = 0; i < remainingElements.Count; i++) {
				Size availableSpace_i = ((Size?)remainingElementsAvailableSpaces[i]) ?? new Size(0f, 0f);
				elementMinimums.Add(remainingElements[i].element.MinimumSize(graphicsState, layout, availableSpace_i));
			}

			float height = 0f, width = 0f;

			if (layout == Layout.COLUMNS) {
				width = Divisions.CalculateTotalLength(elementMinimums.Select(m => m.Width), gutter);
				height = elementMinimums.Select(m => m.Height).MaxOrFallback(0f);
			}
			else {
				width = elementMinimums.Select(m => m.Width).MaxOrFallback(0f);
				height = Divisions.CalculateTotalLength(elementMinimums.Select(m => m.Height), gutter);
			}

			return new Size(width, height);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parentElement"></param>
		/// <param name="children"></param>
		/// <param name="graphicsState"></param>
		/// <param name="rect"></param>
		/// <param name="elementRect"></param>
		/// <param name="containerRect"></param>
		/// <param name="gutters"></param>
		/// <param name="remainingRect"></param>
		/// <param name="remainingGutter"></param>
		/// <returns></returns>
		/// <exception cref="InvalidRectangleException"></exception>
		public static Rectangle?[] GetElementRects(IGridElement parentElement, IReadOnlyList<IGridElement> children, ISharpGraphicsState graphicsState, Rectangle rect, out Rectangle elementRect, out Rectangle? containerRect, out Rectangle?[] gutters, out Rectangle? remainingRect, out Rectangle? remainingGutter) {

			elementRect = rect.Margins(parentElement.Margins, false);
			containerRect = parentElement.ContainerArea(graphicsState, elementRect);

			if (children.Count == 0) {
				gutters = Array.Empty<Rectangle>();
				remainingRect = containerRect;
				remainingGutter = null;
				return Array.Empty<Rectangle>();
			}

			List<int> rectIdxs = new List<int>();
			List<Dimension> rectSizes = new List<Dimension>();

			Layout parentLayout = parentElement.Layout;
			Arrangement parentArrangement = parentElement.Arrangement;
			LayoutOrder parentOrder = parentElement.Order;

			Rectangle?[] elementRects = new Rectangle[children.Count];
			for (int i = 0; i < children.Count; i++) {
				IGridElement child = children[i];
				if (child.IsInset()) {
					elementRects[i] = child.Position.Value.GetFrom(elementRect);
				}
				else if (child.Size.HasValue) {
					if (containerRect == null) {
						throw new InvalidRectangleException("No container area for children.");
					}
					rectIdxs.Add(i);
					Dimension finalChildSize = child.FinalSizeFromArrangement(
						graphicsState, parentLayout, (Size)containerRect
						) ?? throw new InvalidRectangleException("Could not find final Size for child.");
					rectSizes.Add(finalChildSize);
				}
			}

			if (containerRect != null && rectSizes.Count > 0) {
				float parentGutter = parentElement.Gutter;
				Rectangle?[] divisionRects;
				if (parentLayout == Layout.COLUMNS) {
					divisionRects = Divisions.Columns(containerRect, rectSizes.ToArray(), parentGutter, parentGutter, out remainingRect, out gutters, out remainingGutter, false, parentArrangement, parentOrder);
				}
				else {
					divisionRects = Divisions.Rows(containerRect, rectSizes.ToArray(), parentGutter, parentGutter, out remainingRect, out gutters, out remainingGutter, false, parentArrangement, parentOrder);
				}

				for (int i = 0; i < divisionRects.Length; i++) {
					int idx = rectIdxs[i];
					Rectangle? childRect = divisionRects[i];
					elementRects[idx] = childRect;
				}
			}
			else {
				gutters = Array.Empty<Rectangle>();
				remainingRect = containerRect;
				remainingGutter = null;
			}

			return elementRects;
		}

		public static IGridElement PlaceHolder(Dimension size, Margins margins, bool isContainer, Size minimumSize) {
			return new PlaceHolderGridElement(size, null, margins, isContainer, minimumSize);
		}

		public static IGridElement PlaceHolder(Position position, Margins margins, bool isContainer, Size minimumSize) {
			return new PlaceHolderGridElement(null, position, margins, isContainer, minimumSize);
		}

		public static IGridElement BackgroundPlaceholder() {
			return new PlaceHolderGridElement(
				null,
				new Position(Anchor.BOTTOMLEFT, Dimension.FromPoints(0), Dimension.FromPoints(0), Dimension.FromPercent(100), Dimension.FromPercent(100)),
				Margins.Zero,
				false,
				null);
		}

		public static IGridElement SinglePlaceholder(bool isContainer, Size minimumSize) {
			return new PlaceHolderGridElement(
				Dimension.Single,
				null,
				Margins.Zero,
				isContainer,
				minimumSize);
		}

		private class PlaceHolderGridElement : IGridElement {
			public Dimension? Size { get; }
			public Position? Position { get; }
			public Margins Margins { get; }

			public IReadOnlyList<IGridElement> Children { get; } = Array.Empty<IGridElement>(); // new List<IGridElement>();
			public Layout Layout { get; } = Layout.ROWS;
			public Arrangement Arrangement { get; } = Arrangement.FRONT;
			public LayoutOrder Order { get; } = LayoutOrder.FORWARD;
			public float Gutter { get; } = 0f;

			public bool ProvidesRemaining { get; } = true;

			private readonly bool isContainer;
			private readonly Size? minimumSize;

			public PlaceHolderGridElement(Dimension? size, Position? position, Margins margins, bool isContainer, Size? minimumSize) {
				this.Size = size;
				this.Position = position;
				this.Margins = margins;
				this.isContainer = isContainer;
				this.minimumSize = minimumSize ?? new Size(0f, 0f);
			}

			public Rectangle? ContainerArea(ISharpGraphicsState graphicsState, Rectangle fullArea) {
				return isContainer ? fullArea : null;
			}

			public Size? MinimumContentSize(ISharpGraphicsState graphicsState, Size available) {
				return minimumSize;
			}
		}

	}

}
