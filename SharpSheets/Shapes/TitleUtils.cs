using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Shapes {

	/// <summary>
	/// Indicates the position of a title around the outside of a rectangular area.
	/// </summary>
	public enum TitlePosition {
		/// <summary>
		/// Indicates that the title should be placed at the top of the area,
		/// horizontally centred.
		/// </summary>
		TOP,
		/// <summary>
		/// Indicates that the title should be placed at the top of the area,
		/// on the right-hand side.
		/// </summary>
		TOPRIGHT,
		/// <summary>
		/// Indicates that the title should be placed to the right of the area,
		/// vertically centred.
		/// </summary>
		RIGHT,
		/// <summary>
		/// Indicates that the title should be placed at the bottom of the area,
		/// on the right-hand side.
		/// </summary>
		BOTTOMRIGHT,
		/// <summary>
		/// Indicates that the title should be placed at the bottom of the area,
		/// horizontally centred.
		/// </summary>
		BOTTOM,
		/// <summary>
		/// Indicates that the title should be placed at the bottom of the area,
		/// on the left-hand side.
		/// </summary>
		BOTTOMLEFT,
		/// <summary>
		/// Indicates that the title should be placed to the left of the area,
		/// vertically centred.
		/// </summary>
		LEFT,
		/// <summary>
		/// Indicates that the title should be placed at the top of the area,
		/// on the left-hand side.
		/// </summary>
		TOPLEFT
	}

	public static class TitlePositionExtensions {

		public static bool IsTop(this TitlePosition position) {
			return position == TitlePosition.TOP || position == TitlePosition.TOPLEFT || position == TitlePosition.TOPRIGHT;
		}

		public static bool IsBottom(this TitlePosition position) {
			return position == TitlePosition.BOTTOM || position == TitlePosition.BOTTOMLEFT || position == TitlePosition.BOTTOMRIGHT;
		}

		public static bool IsLeft(this TitlePosition position) {
			return position == TitlePosition.LEFT || position == TitlePosition.TOPLEFT || position == TitlePosition.BOTTOMLEFT;
		}

		public static bool IsRight(this TitlePosition position) {
			return position == TitlePosition.RIGHT || position == TitlePosition.TOPRIGHT || position == TitlePosition.BOTTOMRIGHT;
		}

		public static bool IsVerticallyCentered(this TitlePosition position) {
			return position == TitlePosition.LEFT || position == TitlePosition.RIGHT;
		}

		public static bool IsHorizontallyCentered(this TitlePosition position) {
			return position == TitlePosition.TOP || position == TitlePosition.BOTTOM;
		}

	}

	public static class TitleUtils {

		public static Size GetNameSpace(ISharpGraphicsState graphicsState, RichString[] richParts, float fontSize, ParagraphSpecification paragraphSpec, TextHeightStrategy heightStrategy, Direction orientation, Margins padding) {
			
			float width = RichStringLayout.CalculateWidth(graphicsState, richParts, fontSize, paragraphSpec, false);
			float height = RichStringLayout.CalculateHeight(graphicsState, richParts, fontSize, paragraphSpec, heightStrategy);

			Size nameSpace = new Size(width, height).Margins(padding, true);

			if (orientation == Direction.EAST || orientation == Direction.WEST) {
				nameSpace = new Size(nameSpace.Height, nameSpace.Width);
			}

			return nameSpace;
		}

		public static (Transform t, Rectangle pageRect, Rectangle transformedRect) GetNameArea(Rectangle rect, Size nameSpace, TitlePosition position, Vector offset, Direction orientation) {

			float titleRectX, titleRectY;

			if (position == TitlePosition.TOP || position == TitlePosition.BOTTOM) {
				titleRectX = rect.CentreX - nameSpace.Width / 2 + offset.X;
			}
			else if (position.IsRight()) {
				titleRectX = rect.Right - nameSpace.Width - offset.X;
			}
			else {
				titleRectX = rect.Left + offset.X;
			}

			if (position == TitlePosition.LEFT || position == TitlePosition.RIGHT) {
				titleRectY = rect.CentreY - nameSpace.Height / 2 + offset.Y;
			}
			else if (position.IsTop()) {
				titleRectY = rect.Top - offset.Y - nameSpace.Height;
			}
			else {
				titleRectY = rect.Bottom + offset.Y;
			}

			Rectangle pageTitleRect = new Rectangle(titleRectX, titleRectY, nameSpace.Width, nameSpace.Height);

			(Transform transform, Rectangle drawTitleRect) = TransformRect(pageTitleRect, orientation);

			return (transform, pageTitleRect, drawTitleRect);
		}

		public static (Transform t, Rectangle r) TransformRect(Rectangle pageRect, Direction orientation) {
			Transform transform;
			Rectangle drawRect;
			if (orientation == Direction.SOUTH) {
				transform = Transform.Translate(pageRect.Right, pageRect.Top) * Transform.Rotate180;
				drawRect = new Rectangle(0, 0, pageRect.Width, pageRect.Height);
			}
			else if (orientation == Direction.WEST) {
				transform = Transform.Translate(pageRect.Right, pageRect.Bottom) * Transform.Rotate90Clockwise;
				drawRect = new Rectangle(0, 0, pageRect.Height, pageRect.Width);
			}
			else if (orientation == Direction.EAST) {
				transform = Transform.Translate(pageRect.Left, pageRect.Top) * Transform.Rotate90CounterClockwise;
				drawRect = new Rectangle(0, 0, pageRect.Height, pageRect.Width);
			}
			else { // orientation == Direction.NORTH
				transform = Transform.Identity;
				drawRect = pageRect;
			}

			return (transform, drawRect);
		}

		public static Margins GetNameMargins(Size nameSpace, TitlePosition position, Layout layout, Vector offset, float spacing) {
			Margins margins;
			if (position == TitlePosition.LEFT || (layout == Layout.COLUMNS && position.IsLeft())) {
				margins = new Margins(0f, 0f, 0f, nameSpace.Width + offset.X + spacing);
			}
			else if (position == TitlePosition.RIGHT || (layout == Layout.COLUMNS && position.IsRight())) {
				margins = new Margins(0f, nameSpace.Width + offset.X + spacing, 0f, 0f);
			}
			else if (position == TitlePosition.TOP || (layout == Layout.ROWS && position.IsTop())) {
				margins = new Margins(nameSpace.Height + offset.Y + spacing, 0f, 0f, 0f);
			}
			else { // if (position == Anchor.BOTTOM || (layout==Layout.ROWS && position.IsBottom())) {
				margins = new Margins(0f, 0f, nameSpace.Height + offset.Y + spacing, 0f);
			}

			return margins;
		}

		public static Layout GetTitleLayout(TitlePosition position, Layout layout) {
			if (position == TitlePosition.LEFT || (layout == Layout.COLUMNS && position.IsLeft())) {
				return Layout.COLUMNS;
			}
			else if (position == TitlePosition.RIGHT || (layout == Layout.COLUMNS && position.IsRight())) {
				return Layout.COLUMNS;
			}
			else if (position == TitlePosition.TOP || (layout == Layout.ROWS && position.IsTop())) {
				return Layout.ROWS;
			}
			else { // if (position == Anchor.BOTTOM || (layout==Layout.ROWS && position.IsBottom())) {
				return Layout.ROWS;
			}
		}

		public static Direction GetTitleDirection(TitlePosition position, Layout layout) {
			if (position == TitlePosition.LEFT || (layout == Layout.COLUMNS && position.IsLeft())) {
				return Direction.WEST;
			}
			else if (position == TitlePosition.RIGHT || (layout == Layout.COLUMNS && position.IsRight())) {
				return Direction.EAST;
			}
			else if (position == TitlePosition.TOP || (layout == Layout.ROWS && position.IsTop())) {
				return Direction.NORTH;
			}
			else { // if (position == Anchor.BOTTOM || (layout==Layout.ROWS && position.IsBottom())) {
				return Direction.SOUTH;
			}
		}

	}

}
