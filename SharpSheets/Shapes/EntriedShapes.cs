using SharpSheets.Canvas;
using SharpSheets.Colors;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Shapes {

	public class SimpleEntried : EntriedShapeBase {

		protected readonly Dimension[] sizes;
		protected readonly Layout layout;

		protected readonly Color? stroke;
		protected readonly Color? fill;
		protected readonly float[]? dashes;
		protected readonly float dashOffset;
		protected readonly Margins trim;

		protected bool HasGraphicsChanges { get { return fill.HasValue || stroke.HasValue || (dashes != null && dashes.Length > 0); } }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="aspect"></param>
		/// <param name="sizes" default="1" example="2,2,3"></param>
		/// <param name="layout"></param>
		/// <param name="stroke"></param>
		/// <param name="fill"></param>
		/// <param name="dashes"></param>
		/// <param name="dashOffset"></param>
		/// <param name="trim" default="0" example="2"></param>
		public SimpleEntried(float aspect, Dimension[]? sizes = null, Layout layout = Layout.ROWS, Color? stroke = null, Color? fill = null, float[]? dashes = null, float? dashOffset = null, Margins? trim = null) : base(aspect) {
			this.sizes = sizes ?? new Dimension[] { Dimension.Single };
			this.layout = layout;

			this.stroke = stroke;
			this.fill = fill;
			this.dashes = dashes;
			this.dashOffset = dashOffset ?? 0f;
			this.trim = trim ?? Margins.Zero;
		}

		protected Rectangle?[] GetEntryRects(ISharpGraphicsState graphicsState, Rectangle rect, out Rectangle?[] gutters) {
			float gutterSize = graphicsState.GetLineWidth();

			Rectangle?[] entryRects;
			if (layout == Layout.COLUMNS) {
				entryRects = Divisions.Columns(rect, sizes, gutterSize, out gutters, true, Arrangement.FRONT, LayoutOrder.FORWARD);
			}
			else { // layout == Layout.ROWS
				entryRects = Divisions.Rows(rect, sizes, gutterSize, out gutters, true, Arrangement.FRONT, LayoutOrder.FORWARD);
			}

			for (int i = 0; i < entryRects.Length; i++) {
				entryRects[i] = entryRects[i]?.Margins(trim, false);
			}

			return entryRects;
		}

		protected sealed override void DrawFrame(ISharpCanvas canvas, Rectangle rect) {
			if (HasGraphicsChanges) {
				canvas.SaveState();
				if (fill.HasValue) {
					canvas.SetFillColor(fill.Value);
				}
				if (stroke.HasValue) {
					canvas.SetStrokeColor(stroke.Value);
				}
				if (dashes != null && dashes.Length > 0) {
					canvas.SetStrokeDash(new StrokeDash(dashes, dashOffset));
				}
			}

			Rectangle?[] entryRects = GetEntryRects(canvas, rect, out Rectangle?[] gutters);

			canvas.Rectangle(rect).Fill();

			foreach(Rectangle? gutter in gutters) {
				if (gutter is not null) {
					if (layout == Layout.COLUMNS) {
						canvas.MoveTo(gutter.CentreX, rect.Bottom).LineTo(gutter.CentreX, rect.Top);
					}
					else { // layout == Layout.ROWS
						canvas.MoveTo(rect.Left, gutter.CentreY).LineTo(rect.Right, gutter.CentreY);
					}
					canvas.Stroke();
				}
			}

			canvas.Rectangle(rect).Stroke();

			if (HasGraphicsChanges) {
				canvas.RestoreState();
			}
		}

		public override int EntryCount(ISharpGraphicsState graphicsState, Rectangle rect) {
			return sizes.Length;
		}

		public override Rectangle EntryRect(ISharpGraphicsState graphicsState, int entryIndex, Rectangle rect) {
			Rectangle?[] entryRects = GetEntryRects(graphicsState, rect, out _);

			if(entryIndex >= 0 && entryIndex < entryRects.Length && entryRects[entryIndex] is not null) {
				return entryRects[entryIndex]!;
			}
			else {
				throw new InvalidRectangleException($"No area available for entry {entryIndex}.");
			}
		}

	}

}
