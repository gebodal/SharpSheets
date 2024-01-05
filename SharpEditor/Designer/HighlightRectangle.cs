using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SharpEditor.Designer {

	class HighlightRectangle : Shape {

		// TODO This implementation isn't finished yet.

		protected override Geometry DefiningGeometry {
			get {
				return new RectangleGeometry(new Rect(new Point(0, 0), new Vector(Width, Height)));
			}
		}

		public HighlightRectangle() : base() {
			this.MouseEnter += HighlightRectangle_MouseEnter;
			this.MouseLeave += HighlightRectangle_MouseLeave;
		}

		private void HighlightRectangle_MouseLeave(object? sender, System.Windows.Input.MouseEventArgs e) {
			throw new NotImplementedException();
		}

		private void HighlightRectangle_MouseEnter(object? sender, System.Windows.Input.MouseEventArgs e) {
			throw new NotImplementedException();
		}
	}

}
