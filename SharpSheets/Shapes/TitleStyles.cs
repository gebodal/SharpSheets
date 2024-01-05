using SharpSheets.Canvas;
using SharpSheets.Layouts;
using SharpSheets.Canvas.Text;
using SharpSheets.Utilities;
using System;
using System.Linq;

namespace SharpSheets.Shapes {

	/// <summary>
	/// This style will draw no title around the box, and will not affect the remaining
	/// area of the shape.
	/// </summary>
	public class Untitled : TitleStyledBoxBase {

		/// <summary>
		/// Constructor for Untitled.
		/// </summary>
		/// <param name="box">Base shape.</param>
		/// <param name="name">Title text.</param>
		public Untitled(IContainerShape box, string name) : base(box, name, TextFormat.REGULAR, 0f, default, 0f) { }

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle aspectRect) {
			box.Draw(canvas, aspectRect);
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle aspectRect) {
			return box.RemainingRect(graphicsState, aspectRect);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return box.FullRect(graphicsState, rect);
		}
	}

	public abstract class AbstractPositionedTitleStyle : TitleStyledBoxBase {

		protected readonly TitlePosition position;
		protected readonly Justification justification;
		protected readonly Layout layout;
		protected readonly Direction orientation;
		protected readonly Margins padding;
		protected readonly TextHeightStrategy heightStrategy;

		protected readonly ParagraphSpecification paragraphSpec;
		protected readonly RichString[] richParts;

		public AbstractPositionedTitleStyle(IContainerShape box, string name, TitlePosition position, Layout layout, Direction orientation, Margins padding, TextFormat format, float fontSize, Vector offset, float spacing, Justification justification, float lineSpacing, TextHeightStrategy heightStrategy) : base(box, name, format, fontSize, offset, spacing) {
			this.position = position;
			this.layout = layout;
			this.orientation = orientation;
			this.padding = padding;
			this.justification = justification;
			this.heightStrategy = heightStrategy;

			this.paragraphSpec = new ParagraphSpecification(lineSpacing, 0f, 0f, 0f);
			this.richParts = this.parts.Select(p => RichString.Create(p, format)).ToArray();
		}

		protected virtual Size GetNameSpace(ISharpGraphicsState graphicsState) => TitleUtils.GetNameSpace(graphicsState, richParts, fontSize, paragraphSpec, heightStrategy, orientation, padding);

		protected (Transform t, Rectangle pageRect, Rectangle transformedRect) GetNameArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			Size nameSpace = GetNameSpace(graphicsState);
			return TitleUtils.GetNameArea(rect, nameSpace, position, offset, orientation);
		}

		protected (Transform t, Rectangle r) TransformRect(Rectangle pageRect) => TitleUtils.TransformRect(pageRect, orientation);

		protected Margins GetNameMargins(ISharpGraphicsState graphicsState) {
			Size nameSpace = GetNameSpace(graphicsState);
			return TitleUtils.GetNameMargins(nameSpace, position, layout, offset, spacing);
		}

		protected void DrawTitleText(ISharpCanvas canvas, Rectangle textRect) {
			canvas.DrawRichText(textRect, richParts, fontSize, paragraphSpec, justification, Alignment.CENTRE, heightStrategy, false);
		}

		protected void DrawTitle(ISharpCanvas canvas, Rectangle rect) {
			canvas.SaveState();

			(Transform transform, _, Rectangle textArea) = GetNameArea(canvas, rect);
			canvas.ApplyTransform(transform);

			DrawTitleText(canvas, textArea);

			canvas.RestoreState();
		}

	}

	/// <summary>
	/// This style will draw the title text inside the shape outline, adjusting the remaining
	/// area appropriately. The position of the title text can be set and adjusted, along with the
	/// title text, font format, and size. The position of the title text is specified relative
	/// to the whole shape area, not the remaining area, and as such the offset may need to be
	/// adjusted for individual outline styles.
	/// </summary>
	public class Named : AbstractPositionedTitleStyle {

		/// <summary>
		/// Constructor for Named.
		/// </summary>
		/// <param name="box">Base shape.</param>
		/// <param name="name">Title text.</param>
		/// <param name="position">The position of the title text around the inside of the shape
		/// area. This will control the starting location of the text, which may then be adjusted
		/// using <paramref name="offset"/>.</param>
		/// <param name="layout">The layout of the remaining area relative to the title text.
		/// The title text will be considered to be taking up either a row or a column inside the shape
		/// area, and the remaining area will take up the remaining row or column, as appropriate.
		/// This is ignored when a non-corner value is provided (e.g. <see cref="TitlePosition.TOPLEFT"/>),
		/// where the arrangement of the remaining area is controlled solely by the <paramref name="position"/>.
		/// </param>
		/// <param name="orientation">Orientation for the title text, which will control the "up" direction
		/// when the text is drawn. Note that any adjustment from <paramref name="justification"/> will be
		/// relative to the internal text direction, not the direction relative to the page.</param>
		/// <param name="format">Font format to use for the title text. This will use the appropriate font
		/// format from the current font selection.</param>
		/// <param name="fontSize">The fontsize to use for the title text.</param>
		/// <param name="offset" default="(0,3)">The offset for the title text, relative to its initial
		/// layout based on <paramref name="position"/>. This can be used to move the title away from
		/// the shape outline, and for making design adjustments. This offset is directed "away" from the
		/// edge, meaning that for top-aligned titles, positive offsets will move the title downwards,
		/// whereas bottom-aligned titles will be offset upwards - and vice versa for left and right.</param>
		/// <param name="spacing">The spacing between the title text and the remaining area, if the remaining
		/// area requires adjustment after the title has been drawn.</param>
		/// <param name="justification">The justification for the title text, relative to the widest line
		/// of the title text. Note this this will not move the position of the title relative to the outline,
		/// but only within the bounding box created by the text height and maximum line width.</param>
		/// <param name="lineSpacing">The line spacing to use when drawing multi-line titles. This is
		/// expressed as a multiple of <paramref name="fontSize"/>.</param>
		/// <param name="heightStrategy">The height strategy to use when determining title text height.</param>
		public Named(IContainerShape box, string name, TitlePosition position = TitlePosition.BOTTOM, Layout layout = Layout.ROWS, Direction orientation = Direction.NORTH, TextFormat format = TextFormat.BOLD, float fontSize = 6f, Vector? offset = null, float spacing = 3f, Justification justification = Justification.CENTRE, float lineSpacing = 1f, TextHeightStrategy heightStrategy = TextHeightStrategy.FontsizeBaseline) : base(box, name, position, layout, orientation, Margins.Zero, format, fontSize, offset ?? new Vector(0f, 3f), spacing, justification, lineSpacing, heightStrategy) { }

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			box.Draw(canvas, rect);
			DrawTitle(canvas, rect);
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle boxRect = box.RemainingRect(graphicsState, rect);

			Margins margins = GetNameMargins(graphicsState);
			Rectangle afterName = rect.Margins(margins, false);

			return Rectangle.Intersection(boxRect, afterName);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle boxRect = box.FullRect(graphicsState, rect);

			Margins margins = GetNameMargins(graphicsState);
			Rectangle beforeName = rect.Margins(margins, true);

			return Rectangle.Union(boxRect, beforeName);
		}
	}

	/// <summary>
	/// This style will draw the title text outside the shape outline, adjusting the available
	/// outline area within the full shape area appropriately. The position of the title text
	/// can be set and adjusted, along with the title text, font format, and size. The position of
	/// the title text is specified relative to the full shape area, and as such the offset may
	/// need a zero value on at least one axis in order for the title to be drawn at the edge
	/// of the shape area.
	/// </summary>
	public class Titled : AbstractPositionedTitleStyle {

		/// <summary>
		/// Constructor for Titled.
		/// </summary>
		/// <param name="box">Base shape.</param>
		/// <param name="name">Title text.</param>
		/// <param name="position">The position of the title text around the outside of the shape
		/// area. This will control the starting location of the text, which may then be adjusted
		/// using <paramref name="offset"/>.</param>
		/// <param name="layout">The layout of the shape area relative to the title text. The title text
		/// will be considered to be taking up either a row or a column inside the full shape area, and
		/// the outline shape area will take up the remaining row or column, as appropriate.
		/// This is ignored when a non-corner value is provided (e.g. <see cref="TitlePosition.TOPLEFT"/>),
		/// where the arrangement of the outline shape area is controlled solely by the <paramref name="position"/>.
		/// </param>
		/// <param name="orientation">Orientation for the title text, which will control the "up" direction
		/// when the text is drawn. Note that any adjustment from <paramref name="justification"/> will be
		/// relative to the internal text direction, not the direction relative to the page.</param>
		/// <param name="format">Font format to use for the title text. This will use the appropriate font
		/// format from the current font selection.</param>
		/// <param name="fontSize">The fontsize to use for the title text.</param>
		/// <param name="offset" default="(0,0)">The offset for the title text, relative to its initial
		/// layout based on <paramref name="position"/>. This can be used to move the title away from
		/// the full area edge, and for making design adjustments. This offset is directed "away" from the
		/// edge, meaning that for top-aligned titles, positive offsets will move the title downwards,
		/// whereas bottom-aligned titles will be offset upwards - and vice versa for left and right.</param>
		/// <param name="spacing">The spacing between the title text and the outline shape area.</param>
		/// <param name="justification">The justification for the title text, relative to the widest line
		/// of the title text. Note this this will not move the position of the title relative to the outline,
		/// but only within the bounding box created by the text height and maximum line width.</param>
		/// <param name="lineSpacing">The line spacing to use when drawing multi-line titles. This is
		/// expressed as a multiple of <paramref name="fontSize"/>.</param>
		/// <param name="heightStrategy">The height strategy to use when determining title text height.</param>
		public Titled(IContainerShape box, string name, TitlePosition position = TitlePosition.BOTTOM, Layout layout = Layout.ROWS, Direction orientation = Direction.NORTH, TextFormat format = TextFormat.BOLD, float fontSize = 6f, Vector? offset = null, float spacing = 3f, Justification justification = Justification.CENTRE, float lineSpacing = 1f, TextHeightStrategy heightStrategy = TextHeightStrategy.FontsizeBaseline) : base(box, name, position, layout, orientation, Margins.Zero, format, fontSize, offset ?? new Vector(0f, 0f), spacing, justification, lineSpacing, heightStrategy) { }

		protected Rectangle BoxRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return box.AspectRect(graphicsState, rect.Margins(GetNameMargins(graphicsState), false));
		}

		public override Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return BoxRect(graphicsState, rect).Margins(GetNameMargins(graphicsState), true);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			DrawTitle(canvas, rect);

			Margins nameMargins = GetNameMargins(canvas);
			Rectangle boxArea = rect.Margins(nameMargins, false);
			box.Draw(canvas, boxArea);
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Margins margins = GetNameMargins(graphicsState);
			Rectangle afterName = rect.Margins(margins, false);

			return box.RemainingRect(graphicsState, afterName);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle boxRect = box.FullRect(graphicsState, rect);

			Margins margins = GetNameMargins(graphicsState);
			Rectangle beforeName = boxRect.Margins(margins, true);

			return beforeName;
		}
	}

	/// <summary>
	/// This style will draw the title text outside the shape outline, inside its own shape
	/// outline. The available outline area (i.e. that used for the actual outline style being
	/// drawn for the main shape) within the full shape area will be adjusted appropriately.
	/// The position of the title box can set and adjusted, along with the title text font format
	/// and size, and positioning within the title box. The position of the title box is specified
	/// relative to the full shape area, and as such the offset may need a zero value on at least
	/// one axis in order for the title to be drawn at the edge of the shape area.
	/// </summary>
	public class BoxedTitle : AbstractPositionedTitleStyle {

		protected readonly IBox outline;
		protected readonly Margins trim;

		/// <summary>
		/// Constryctor for BoxedTitle.
		/// </summary>
		/// <param name="box">Base shape.</param>
		/// <param name="name">Title text.</param>
		/// <param name="box_">The box style to draw around the title text. This style must support
		/// inferring the full area from a content area.</param>
		/// <param name="trim">Spacing to use around the title text inside the title box.</param>
		/// <param name="position">The position of the title box around the outside of the shape
		/// area. This will control the starting location of the title box, which may then be adjusted
		/// using <paramref name="offset"/>.</param>
		/// <param name="layout">The layout of the outline area relative to the title box. The title box
		/// will be considered to be taking up either a row or a column inside the full shape area, and
		/// the outline shape area will take up the remaining row or column, as appropriate.
		/// This is ignored when a non-corner value is provided (e.g. <see cref="TitlePosition.TOPLEFT"/>),
		/// where the arrangement of the outline shape area is controlled solely by the <paramref name="position"/>.
		/// </param>
		/// <param name="orientation">Orientation for the title text, which will control the "up" direction
		/// when the text is drawn. Note that any adjustment from <paramref name="justification"/> will be
		/// relative to the internal text direction, not the direction relative to the page.</param>
		/// <param name="format">Font format to use for the title text. This will use the appropriate font
		/// format from the current font selection.</param>
		/// <param name="fontSize">The fontsize to use for the title text.</param>
		/// <param name="offset">The offset for the title box, relative to its initial
		/// layout based on <paramref name="position"/>. This can be used to move the title box away from
		/// the full area edge, and for making design adjustments. This offset is directed "away" from the
		/// edge, meaning that for top-aligned titles, positive offsets will move the title downwards,
		/// whereas bottom-aligned titles will be offset upwards - and vice versa for left and right.</param>
		/// <param name="spacing">The spacing between the title box and the outline shape area.</param>
		/// <param name="justification">The justification for the title text, relative to the widest line
		/// of the title text. Note this this will not move the position of the title text or box relative to
		/// the outline, but only within the bounding box created by the text height and maximum line width.
		/// </param>
		/// <param name="lineSpacing">The line spacing to use when drawing multi-line titles. This is
		/// expressed as a multiple of <paramref name="fontSize"/>.</param>
		/// <param name="heightStrategy">The height strategy to use when determining title text height.</param>
		public BoxedTitle(IContainerShape box, string name, IBox box_, Margins trim = default, TitlePosition position = TitlePosition.TOP, Layout layout = Layout.ROWS, Direction orientation = Direction.NORTH, TextFormat format = TextFormat.BOLD, float fontSize = 11f, Vector offset = default, float spacing = 3f, Justification justification = Justification.CENTRE, float lineSpacing = 1f, TextHeightStrategy heightStrategy = TextHeightStrategy.AscentBaseline) : base(box, name, position, layout, orientation, Margins.Zero, format, fontSize, offset, spacing, justification, lineSpacing, heightStrategy) {
			this.outline = box_ ?? new NoOutline(-1f);
			this.trim = trim;
		}

		protected Rectangle BoxRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return box.AspectRect(graphicsState, rect.Margins(GetNameMargins(graphicsState), false));
		}

		public override Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return BoxRect(graphicsState, rect).Margins(GetNameMargins(graphicsState), true);
		}

		protected override Size GetNameSpace(ISharpGraphicsState graphicsState) {
			Size nameSize = base.GetNameSpace(graphicsState);
			return outline.FullSize(graphicsState, nameSize.Margins(trim, true));
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			Margins nameMargins = GetNameMargins(canvas);
			Rectangle boxArea = rect.Margins(nameMargins, false);
			box.Draw(canvas, boxArea);

			canvas.SaveState();

			(_, Rectangle pageTitleRect, _) = GetNameArea(canvas, rect);
			outline.Draw(canvas, pageTitleRect, out Rectangle titleBoxRemainingPage);

			(Transform transform, Rectangle titleTextRect) = TransformRect(titleBoxRemainingPage.Margins(trim, false));
			canvas.ApplyTransform(transform);
			DrawTitleText(canvas, titleTextRect);

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Margins margins = GetNameMargins(graphicsState);
			Rectangle afterName = rect.Margins(margins, false);

			return box.RemainingRect(graphicsState, afterName);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle boxRect = box.FullRect(graphicsState, rect);

			Margins margins = GetNameMargins(graphicsState);
			Rectangle beforeName = boxRect.Margins(margins, true);

			return beforeName;
		}
	}

	/// <summary>
	/// This style will draw the title text outside of the shape outline, inside its own tab
	/// outline that is drawn connected to the main outline. The tab shape will be draw "behind"
	/// the main outline, and continued such that the tab appears to "protrude" from behind the
	/// main shape. The main outline area can either be left as is, or repositioned to allow for
	/// the size of the tab. The tab position is specified as a cardinal direction relative to
	/// the main shape area. The position of the tab can be adjusted, and the layout of the title
	/// text inside the tab specified. 
	/// </summary>
	public class TabTitle : TitleStyledBoxBase {

		protected readonly IBox tabBox;
		protected readonly Dimension? protrusionLength;
		protected readonly Dimension? tabBreadth;
		protected readonly bool includeProtrusion;
		protected readonly Direction position;
		protected readonly Justification justification;
		protected readonly Direction orientation;
		protected readonly Margins trim;
		protected readonly TextHeightStrategy heightStrategy;

		protected readonly ParagraphSpecification paragraphSpec;
		protected readonly RichString[] richParts;

		/// <summary>
		/// Constructor for TabTitle.
		/// </summary>
		/// <param name="box" example="Simple">Base shape.</param>
		/// <param name="name">Title text.</param>
		/// <param name="tabBox" example="Simple">The box style to draw around the title tab.
		/// This style must support inferring the full area from a content area.</param>
		/// <param name="trim" example="2">Spacing to use around the title text inside the title
		/// tab box.</param>
		/// <param name="position">The position of the title tab box around the outside of the shape
		/// area. This will control the starting location of the title box, which may then be adjusted
		/// using <paramref name="offset"/>.</param>
		/// <param name="protrusion">A specific length for the tab protrusion. If no value is provided,
		/// the protrusion is calculated from the title text size and other parameters. Percentage values
		/// are calculated based on the total area (width or height, according to the value of
		/// <paramref name="position"/>), and relative values are considered to be fractions of the total
		/// area (and hence should be between 0 and 1). Note that percentage or relative values do not
		/// allow for inferring the full shape size.</param>
		/// <param name="tabBreadth">A specific length for the tab breadth, i.e. it's size perpendicular
		/// to the protrusion. If no value is provided, the breadth is calculated from the title text size
		/// and other parameters. Percentage values are calculated based on the total area (width or height,
		/// according to the value of <paramref name="position"/>), and relative values are considered to
		/// be fractions of the total area (and hence should be between 0 and 1). Note that percentage or
		/// relative values do not allow for inferring the full shape size.</param>
		/// <param name="includeProtrusion" example="true">Flag to indicate if the tab protrusion should
		/// be included in the width of the shape. If true, the outline area will be shortened to allow
		/// the protrusion to be contained in the full area. If false, the outline area will take up the
		/// full area, and the title tab will protrude outside the shape area.</param>
		/// <param name="orientation">Orientation for the title text, which will control the "up" direction
		/// when the text is drawn. Note that any adjustment from <paramref name="justification"/> will be
		/// relative to the internal text direction, not the direction relative to the page.</param>
		/// <param name="format">Font format to use for the title text. This will use the appropriate font
		/// format from the current font selection.</param>
		/// <param name="fontSize">The fontsize to use for the title text.</param>
		/// <param name="offset">The offset for the title tab, relative to its initial layout based on
		/// <paramref name="position"/>. This can be used to fine-tune the position of the title relative to
		/// the main outline. This offset is directed "away" from the edge of the full area, meaning that for
		/// <see cref="Direction.WEST"/> the offset will increase rightwards, and for <see cref="Direction.EAST"/>
		/// it will increase leftwards (and vica versa for vertical arrangements).</param>
		/// <param name="spacing">The spacing between the title text and the main outline inside the tab
		/// area.</param>
		/// <param name="justification">The justfication for the title text inside the tab label area. Note
		/// that this justification includes the entire tab area, and is not just relative to the widest
		/// title line. This means that if <paramref name="protrusion"/> or <paramref name="tabBreadth"/>
		/// have been set, the justification may reposition the title within the tab area.</param>
		/// <param name="lineSpacing">The line spacing to use when drawing multi-line titles. This is
		/// expressed as a multiple of <paramref name="fontSize"/>.</param>
		/// <param name="heightStrategy">The height strategy to use when determining title text height.</param>
		/// <canvas>120 60</canvas>
		public TabTitle(IContainerShape box, string name,
				IBox tabBox, Margins trim = default,
				Direction position = Direction.WEST,
				Dimension? protrusion = null, Dimension? tabBreadth = null, bool includeProtrusion = true,
				Direction orientation = Direction.NORTH, TextFormat format = TextFormat.BOLD, float fontSize = 6f,
				Vector offset = default, float spacing = 3f,
				Justification justification = Justification.CENTRE,
				float lineSpacing = 1f, TextHeightStrategy heightStrategy = TextHeightStrategy.AscentDescent
			) : base(box, name, format, fontSize, offset, spacing) {
			
			this.tabBox = tabBox ?? new NoOutline(-1);
			this.protrusionLength = protrusion;
			this.tabBreadth = tabBreadth;
			this.includeProtrusion = includeProtrusion;
			this.position = position;
			this.justification = justification;
			this.heightStrategy = heightStrategy;
			this.orientation = orientation;
			this.trim = trim;

			this.paragraphSpec = new ParagraphSpecification(lineSpacing, 0f, 0f, 0f);
			this.richParts = this.parts.Select(p => RichString.Create(p, format)).ToArray();
		}

		protected Size GetTitleSize(ISharpGraphicsState graphicsState) {
			return TitleUtils.GetNameSpace(graphicsState, richParts, fontSize, paragraphSpec, heightStrategy, orientation, Margins.Zero);
		}

		protected static float? GetLength(Dimension? length, float? absolute) {
			if (!length.HasValue) {
				return absolute;
			}
			else if (length.Value.IsAbsolute) {
				return length.Value.Absolute;
			}
			else if (length.Value.Percent > 0 && absolute.HasValue) {
				return absolute.Value * length.Value.Percent;
			}
			else if (absolute.HasValue) {
				return absolute.Value * length.Value.Relative;
			}
			else {
				return null;
			}
		}

		protected Size GetTabSize(ISharpGraphicsState graphicsState, Rectangle? rect, Size nameSize) {
			Size nameArea = nameSize.Margins(trim, true);
			if (position == Direction.NORTH || position == Direction.SOUTH) {
				nameArea = new Size(nameArea.Width, nameArea.Height + spacing);
			}
			else { // position == Direction.EAST || position == Direction.WEST
				nameArea = new Size(nameArea.Width + spacing, nameArea.Height);
			}

			Rectangle nameRect = new Rectangle(0f, 0f, nameArea.Width, nameArea.Height);
			Rectangle tabRect = tabBox.FullRect(graphicsState, nameRect);
			Size tabSize;
			if (position == Direction.NORTH) {
				tabSize = (Size)Rectangle.RectangleFromBounding(tabRect.Left, nameRect.Bottom, tabRect.Right, tabRect.Top);
			}
			else if (position == Direction.EAST) {
				tabSize = (Size)Rectangle.RectangleFromBounding(nameRect.Left, tabRect.Bottom, tabRect.Right, tabRect.Top);
			}
			else if (position == Direction.SOUTH) {
				tabSize = (Size)Rectangle.RectangleFromBounding(tabRect.Left, tabRect.Bottom, tabRect.Right, nameRect.Top);
			}
			else { // direction == Direction.WEST
				tabSize = (Size)Rectangle.RectangleFromBounding(tabRect.Left, tabRect.Bottom, nameRect.Right, tabRect.Top);
			}

			float tabWidth = tabSize.Width, tabHeight = tabSize.Height;

			if (protrusionLength.HasValue) {
				if (position == Direction.NORTH || position == Direction.SOUTH) {
					tabHeight = GetLength(protrusionLength.Value, rect?.Height) ?? tabHeight;
				}
				else { // position == Direction.EAST || position == Direction.WEST
					tabWidth = GetLength(protrusionLength.Value, rect?.Width) ?? tabWidth;
				}
			}

			if (tabBreadth.HasValue) {
				if (position == Direction.NORTH || position == Direction.SOUTH) {
					tabWidth = GetLength(tabBreadth.Value, rect?.Width) ?? tabWidth;
				}
				else { // position == Direction.EAST || position == Direction.WEST
					tabHeight = GetLength(tabBreadth.Value, rect?.Height) ?? tabHeight;
				}
			}

			return new Size(tabWidth, tabHeight);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {

			Size nameSize = GetTitleSize(canvas);
			Size tabSize = GetTabSize(canvas, rect, nameSize);

			Rectangle boxRect;
			Rectangle tabRect; // For the tab box
			Rectangle labelRect; // The visible part of the tab box

			if (includeProtrusion) {
				if (position == Direction.NORTH) {
					boxRect = rect.Margins(tabSize.Height + offset.Y, 0f, 0f, 0f, false);
				}
				else if (position == Direction.EAST) {
					boxRect = rect.Margins(0f, tabSize.Width + offset.X, 0f, 0f, false);
				}
				else if (position == Direction.SOUTH) {
					boxRect = rect.Margins(0f, 0f, tabSize.Height + offset.Y, 0f, false);
				}
				else { // direction == Direction.WEST
					boxRect = rect.Margins(0f, 0f, 0f, tabSize.Width + offset.X, false);
				}
			}
			else {
				if (position == Direction.NORTH) {
					boxRect = rect.Margins(offset.Y, 0f, 0f, 0f, false);
				}
				else if (position == Direction.EAST) {
					boxRect = rect.Margins(0f, offset.X, 0f, 0f, false);
				}
				else if (position == Direction.SOUTH) {
					boxRect = rect.Margins(0f, 0f, offset.Y, 0f, false);
				}
				else { // direction == Direction.WEST
					boxRect = rect.Margins(0f, 0f, 0f, offset.X, false);
				}
			}

			if (position == Direction.NORTH || position == Direction.SOUTH) {
				tabRect = boxRect.Margins(0f, (boxRect.Width - tabSize.Width) / 2f, false).Offset(offset.X, 0f);
				if (position == Direction.NORTH) {
					tabRect = tabRect.Margins(tabSize.Height, 0f, -boxRect.Height / 2f, 0f, true);
					Rectangle tabRemaining = tabBox.RemainingRect(canvas, tabRect);
					labelRect = Rectangle.RectangleFromBounding(tabRemaining.Left, boxRect.Top + spacing, tabRemaining.Right, tabRemaining.Top);
				}
				else { // direction == Direction.SOUTH
					tabRect = tabRect.Margins(-boxRect.Height / 2f, 0f, tabSize.Height, 0f, true);
					Rectangle tabRemaining = tabBox.RemainingRect(canvas, tabRect);
					labelRect = Rectangle.RectangleFromBounding(tabRemaining.Left, tabRemaining.Bottom, tabRemaining.Right, boxRect.Bottom - spacing);
				}
			}
			else { // direction == Direction.EAST || direction == Direction.WEST
				tabRect = boxRect.Margins((boxRect.Height - tabSize.Height) / 2f, 0f, false).Offset(0f, offset.Y);
				if (position == Direction.EAST) {
					tabRect = tabRect.Margins(0f, tabSize.Width, 0f, -boxRect.Width / 2f, true);
					Rectangle tabRemaining = tabBox.RemainingRect(canvas, tabRect);
					labelRect = Rectangle.RectangleFromBounding(boxRect.Right + spacing, tabRemaining.Bottom, tabRemaining.Right, tabRemaining.Top);
				}
				else { // direction == Direction.WEST
					tabRect = tabRect.Margins(0f, -boxRect.Width / 2f, 0f, tabSize.Width, true);
					Rectangle tabRemaining = tabBox.RemainingRect(canvas, tabRect);
					labelRect = Rectangle.RectangleFromBounding(tabRemaining.Left, tabRemaining.Bottom, boxRect.Left - spacing, tabRemaining.Top);
				}
			}

			labelRect = labelRect.Margins(trim, false);

			// Draw title tab box
			tabBox.Draw(canvas, tabRect);

			// Draw title text
			(Transform transform, Rectangle textArea) = TitleUtils.TransformRect(labelRect, orientation);
			canvas.SaveState();
			canvas.ApplyTransform(transform);
			canvas.DrawRichText(textArea, richParts, fontSize, paragraphSpec, justification, Alignment.CENTRE, heightStrategy, false);
			canvas.RestoreState();
			
			// Draw main box outline
			box.Draw(canvas, boxRect);
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Size nameSize = GetTitleSize(graphicsState);
			Size tabSize = GetTabSize(graphicsState, rect, nameSize);

			Rectangle boxRect;
			if (includeProtrusion) {
				if (position == Direction.NORTH) {
					boxRect = rect.Margins(tabSize.Height + offset.Y, 0f, 0f, 0f, false);
				}
				else if (position == Direction.EAST) {
					boxRect = rect.Margins(0f, tabSize.Width + offset.X, 0f, 0f, false);
				}
				else if (position == Direction.SOUTH) {
					boxRect = rect.Margins(0f, 0f, tabSize.Height + offset.Y, 0f, false);
				}
				else { // direction == Direction.WEST
					boxRect = rect.Margins(0f, 0f, 0f, tabSize.Width + offset.X, false);
				}
			}
			else {
				if (position == Direction.NORTH) {
					boxRect = rect.Margins(offset.Y, 0f, 0f, 0f, false);
				}
				else if (position == Direction.EAST) {
					boxRect = rect.Margins(0f, offset.X, 0f, 0f, false);
				}
				else if (position == Direction.SOUTH) {
					boxRect = rect.Margins(0f, 0f, offset.Y, 0f, false);
				}
				else { // direction == Direction.WEST
					boxRect = rect.Margins(0f, 0f, 0f, offset.X, false);
				}
			}

			return box.RemainingRect(graphicsState, boxRect);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle boxRect = box.FullRect(graphicsState, rect);

			Size nameSize = GetTitleSize(graphicsState);
			Size tabSize = GetTabSize(graphicsState, null, nameSize);

			Rectangle fullRect;
			if (includeProtrusion) {
				if (position == Direction.NORTH) {
					fullRect = boxRect.Margins(tabSize.Height + offset.Y, 0f, 0f, 0f, true);
				}
				else if (position == Direction.EAST) {
					fullRect = boxRect.Margins(0f, tabSize.Width + offset.X, 0f, 0f, true);
				}
				else if (position == Direction.SOUTH) {
					fullRect = boxRect.Margins(0f, 0f, tabSize.Height + offset.Y, 0f, true);
				}
				else { // direction == Direction.WEST
					fullRect = boxRect.Margins(0f, 0f, 0f, tabSize.Width + offset.X, true);
				}
			}
			else {
				if (position == Direction.NORTH) {
					fullRect = boxRect.Margins(offset.Y, 0f, 0f, 0f, true);
				}
				else if (position == Direction.EAST) {
					fullRect = boxRect.Margins(0f, offset.X, 0f, 0f, true);
				}
				else if (position == Direction.SOUTH) {
					fullRect = boxRect.Margins(0f, 0f, offset.Y, 0f, true);
				}
				else { // direction == Direction.WEST
					fullRect = boxRect.Margins(0f, 0f, 0f, offset.X, true);
				}
			}

			return fullRect;
		}
	}

}