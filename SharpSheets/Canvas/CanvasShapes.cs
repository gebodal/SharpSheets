using System;

namespace SharpSheets.Canvas {

	public static class ISharpCanvasShapes {

		public static ISharpCanvas RoundRectangle(this ISharpCanvas canvas, float x, float y, float width, float height, float radiusX, float radiusY) {
			if (width < 0) {
				x += width;
				width = -width;
			}
			if (height < 0) {
				y += height;
				height = -height;
			}

			if (radiusX < 0) {
				radiusX = -radiusX;
			}
			if (radiusY < 0) {
				radiusY = -radiusY;
			}

			float curv = 0.4477f; // 0.5523? https://stackoverflow.com/a/27863181/11002708

			canvas.MoveTo(x + radiusX, y)
				.LineTo(x + width - radiusX, y)
				.CurveTo(x + width - radiusX * curv, y, x + width, y + radiusY * curv, x + width, y + radiusY)
				.LineTo(x + width, y + height - radiusY)
				.CurveTo(x + width, y + height - radiusY * curv, x + width - radiusX * curv, y + height, x + width - radiusX, y + height)
				.LineTo(x + radiusX, y + height)
				.CurveTo(x + radiusX * curv, y + height, x, y + height - radiusY * curv, x, y + height - radiusY)
				.LineTo(x, y + radiusY)
				.CurveTo(x, y + radiusY * curv, x + radiusX * curv, y, x + radiusX, y)
				.ClosePath();

			return canvas;
		}

		public static ISharpCanvas RoundRectangle(this ISharpCanvas canvas, Layouts.Rectangle rect, float radiusX, float radiusY) {
			canvas.RoundRectangle(rect.X, rect.Y, rect.Width, rect.Height, radiusX, radiusY);
			return canvas;
		}

		public static ISharpCanvas RoundRectangle(this ISharpCanvas canvas, Layouts.Rectangle rect, float radius) {
			canvas.RoundRectangle(rect.X, rect.Y, rect.Width, rect.Height, radius, radius);
			return canvas;
		}

		public static ISharpCanvas BevelledRectangle(this ISharpCanvas canvas, Layouts.Rectangle rect, float bevelX, float bevelY) {
			canvas.MoveTo(rect.Left + bevelX, rect.Bottom)
				.LineTo(rect.Right - bevelX, rect.Bottom)
				.LineTo(rect.Right, rect.Bottom + bevelY)
				.LineTo(rect.Right, rect.Top - bevelY)
				.LineTo(rect.Right - bevelX, rect.Top)
				.LineTo(rect.Left + bevelX, rect.Top)
				.LineTo(rect.Left, rect.Top - bevelY)
				.LineTo(rect.Left, rect.Bottom + bevelY)
				.ClosePath();
			return canvas;
		}

		public static ISharpCanvas BevelledRectangle(this ISharpCanvas canvas, Layouts.Rectangle rect, float bevel) {
			return canvas.BevelledRectangle(rect, bevel, bevel);
		}

	}

}
