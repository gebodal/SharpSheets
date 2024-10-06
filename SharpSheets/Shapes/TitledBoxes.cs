using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;

namespace SharpSheets.Shapes {

	/// <summary>
	/// Indicates a cardinal direction.
	/// </summary>
	public enum Direction {
		/// <summary>
		/// Indicates that the content should be positioned at the top, or oriented upwards.
		/// </summary>
		NORTH,
		/// <summary>
		/// Indicates that the content should be positioned on the right, or oriented rightwards.
		/// </summary>
		EAST,
		/// <summary>
		/// Indicates that the content should be positioned at the bottom, or oriented downwards.
		/// </summary>
		SOUTH,
		/// <summary>
		/// Indicates that the content should be positioned on the left, or oriented leftwards.
		/// </summary>
		WEST
	}

	/// <summary>
	/// This box draws a rectangle around the shape area, with a larger block along one edge
	/// in which to draw the title text. By default, the outline will be drawn using the
	/// current foreground color, and filled with the current background color (which will
	/// also be used for the title text color). All of these may be altered, and the title
	/// positioning, fontsize, and arrangement can be controlled.
	/// </summary>
	public class BlockTitledBox : TitledBoxBase {

		protected readonly TitlePosition position;
		protected readonly Direction orientation;
		protected readonly Layout layout;
		protected readonly Margins padding;
		protected readonly Margins trim;
		protected readonly (float x, float y) offset;
		protected readonly float? headerSize;
		protected readonly RichString[] richParts;

		protected readonly ParagraphSpecification paragraphSpec;
		protected readonly Justification justification;
		protected readonly TextHeightStrategy heightStrategy;

		protected readonly Colors.Color? outlineColor;
		protected readonly Colors.Color? backgroundColor;
		protected readonly Colors.Color? titleColor;

		/// <summary>
		/// Constructor for BlockTitledBox.
		/// </summary>
		/// <param name="aspect">Aspect ratio.</param>
		/// <param name="name">Title text.</param>
		/// <param name="fontSize">Font size at which to draw the title text.</param>
		/// <param name="format">Font format to use for the title text. This will use
		/// the appropriate font format from the current font selection.</param>
		/// <param name="position">The positioning for the title text around the edge of
		/// the box area. This will be used in conjuction with <paramref name="layout"/>
		/// to determine the placement of the title block.</param>
		/// <param name="layout"> The layout for the title block. If ROWS, the block will
		/// be a horizontal block, otherwise vertical for COLUMNS. This is only relevant
		/// if the title is placed in one of the corners of the available area, otherwise
		/// the block placement is determined solely by <paramref name="position"/>, i.e.
		/// ignored if "position" is one of TOP, BOTTOM, LEFT, or RIGHT.</param>
		/// <param name="headerSize">A fixed size for the title block. If the block is
		/// horizontal, this will be a height, otherwise a width for vertical blocks.
		/// If no value is provided, the block size is calculated based on the size of the
		/// title text.</param>
		/// <param name="stroke">The color to use for the shape outline and title block.
		/// If no value is given, the current foreground color will be used.</param>
		/// <param name="fill">The color with which to fill the inside of the outline.
		/// If no value is given, the current background color will be used.</param>
		/// <param name="text">The color for the title text. If no value is given, the
		/// value for <paramref name="fill"/> will be used. If no value is given for
		/// <paramref name="fill"/>, the current background color will be used.</param>
		/// <param name="padding" default="2,2,2,2">Padding around the title text within
		/// the title block. This is used when automatically calculating the title block
		/// size.</param>
		/// <param name="offset">An offset for the title from its position indicated by
		/// the other parameters. The title block will also be repositioned such that the
		/// title text is still within the block.</param>
		/// <param name="trim">A margin around the inside of the box for calculating the
		/// remaining area.</param>
		/// <param name="justification">The justification of the title text. This
		/// justification is used only for titles with multiple lines of text, and adjusts
		/// the horizontal position of each line within the maximum width of any line.
		/// It does not move the title position around the shape area.</param>
		/// <param name="lineSpacing">The line spacing to use for multi-line title texts.</param>
		/// <param name="heightStrategy">The height strategy to use when calculating the
		/// size of title texts for position and auto-calculation purposes.</param>
		/// <param name="orientation">The orientation of the title text. This does not
		/// change the position of the title text, only its arrangement at that position.</param>
		public BlockTitledBox(float aspect, string name = "NAME",
				float fontSize = 8f, TextFormat format = TextFormat.REGULAR,
				TitlePosition position = TitlePosition.TOP,
				Layout layout = Layout.ROWS,
				float? headerSize = null,
				Colors.Color? stroke = null,
				Colors.Color? fill = null,
				Colors.Color? text = null,
				Margins? padding = null, (float x, float y) offset = default,
				Margins trim = default,
				Justification justification = Justification.CENTRE,
				float lineSpacing = 1f,
				TextHeightStrategy heightStrategy = TextHeightStrategy.AscentDescent,
				Direction orientation = Direction.NORTH
			) : base(aspect, name, format, fontSize) {

			this.padding = padding ?? new Margins(2f);
			this.trim = trim;
			this.offset = offset;
			this.headerSize = headerSize;
			this.position = position;
			this.orientation = orientation;
			this.layout = layout;

			this.outlineColor = stroke;
			this.backgroundColor = fill;
			this.titleColor = text;

			this.justification = justification;
			this.heightStrategy = heightStrategy;
			this.paragraphSpec = new ParagraphSpecification(lineSpacing, 0f, 0f, 0f);
			this.richParts = this.parts.Select(p => RichString.Create(p, format)).ToArray();
		}

		protected Size GetNameSpace(ISharpGraphicsState graphicsState) {
			Size nameSpace = TitleUtils.GetNameSpace(graphicsState, richParts, fontSize, paragraphSpec, heightStrategy, orientation, padding);
			return TitleUtils.GetTitleLayout(position, layout) switch {
				Layout.ROWS => new Size(nameSpace.Width, headerSize ?? nameSpace.Height),
				Layout.COLUMNS => new Size(headerSize ?? nameSpace.Width, nameSpace.Height),
				_ => nameSpace,
			};
		}

		protected Margins GetNameMargins(ISharpGraphicsState graphicsState) {
			Size nameSpace = GetNameSpace(graphicsState);
			return TitleUtils.GetNameMargins(nameSpace, position, layout, offset, 0f);
		}

		protected override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			Size nameSpace = GetNameSpace(canvas);
			(Transform transform, Rectangle pageRect, Rectangle textArea) = TitleUtils.GetNameArea(rect, nameSpace, position, offset, orientation);

			Rectangle blockRect = TitleUtils.GetTitleDirection(position, layout) switch {
				Direction.NORTH => Rectangle.RectangleFromBounding(rect.Left, pageRect.Bottom, rect.Right, Math.Max(rect.Top, pageRect.Top)),
				Direction.EAST => Rectangle.RectangleFromBounding(pageRect.Left, rect.Bottom, Math.Max(rect.Right, pageRect.Right), rect.Top),
				Direction.SOUTH => Rectangle.RectangleFromBounding(rect.Left, Math.Min(rect.Bottom, pageRect.Bottom), rect.Right, pageRect.Top),
				Direction.WEST => Rectangle.RectangleFromBounding(Math.Min(rect.Left, pageRect.Left), rect.Bottom, pageRect.Right, rect.Top),
				_ => pageRect, // Should never happen
			};

			Rectangle fullRect = Rectangle.Union(rect, blockRect);

			canvas.SaveState();

			canvas.SetFillColor(backgroundColor ?? canvas.GetBackgroundColor());
			canvas.Rectangle(fullRect).Fill();

			canvas.SetFillColor(outlineColor ?? canvas.GetForegroundColor());
			canvas.Rectangle(blockRect).Fill();

			canvas.SetStrokeColor(outlineColor ?? canvas.GetForegroundColor());
			switch (TitleUtils.GetTitleDirection(position, layout)) {
				case Direction.NORTH:
					canvas.MoveToRel(blockRect, 0, 0).LineToRel(blockRect, 1, 0);
					break;
				case Direction.SOUTH:
					canvas.MoveToRel(blockRect, 0, 1).LineToRel(blockRect, 1, 1);
					break;
				case Direction.WEST:
					canvas.MoveToRel(blockRect, 1, 0).LineToRel(blockRect, 1, 1);
					break;
				case Direction.EAST:
					canvas.MoveToRel(blockRect, 0, 0).LineToRel(blockRect, 0, 1);
					break;
				default:
					canvas.Rectangle(blockRect);
					break;
			}
			canvas.Stroke();

			canvas.Rectangle(fullRect).Stroke();

			canvas.SaveState();
			canvas.ApplyTransform(transform);
			canvas.SetTextColor(titleColor ?? backgroundColor ?? canvas.GetBackgroundColor());
			canvas.DrawRichText(textArea.Margins(padding, false), richParts, fontSize, paragraphSpec, justification, Alignment.CENTRE, heightStrategy, false);
			canvas.RestoreState();

			canvas.RestoreState();
		}

		protected override Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Margins margins = GetNameMargins(graphicsState);
			Rectangle afterName = rect.Margins(margins, false);

			return afterName.Margins(trim, false);
		}

		public override Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle contentRect = rect.Margins(trim, true);

			Margins margins = GetNameMargins(graphicsState);
			Rectangle beforeName = contentRect.Margins(margins, true);

			return beforeName;
		}
	}

}