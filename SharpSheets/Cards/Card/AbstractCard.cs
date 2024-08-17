using SharpSheets.Layouts;
using System.Collections.Generic;
using SharpSheets.Canvas;
using SharpSheets.Cards.Card.SegmentRects;

namespace SharpSheets.Cards.Card {

	public abstract class AbstractCard {

		public abstract IFixedCardSegmentRect[] Segments { get; }

		public abstract bool CropOnFinalCard { get; }
		public abstract float Gutter { get; }

		/// <summary></summary>
		/// <exception cref="Exceptions.SharpDrawingException"></exception>
		/// <exception cref="InvalidRectangleException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public abstract void DrawOutline(ISharpCanvas canvas, Rectangle rect, int card, int totalCards);

		/// <summary></summary>
		/// <exception cref="Exceptions.SharpDrawingException"></exception>
		/// <exception cref="InvalidRectangleException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public abstract void DrawBackground(ISharpCanvas canvas, Rectangle[] rects);

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public abstract void DrawGutter(ISharpCanvas canvas, Rectangle rect);

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		public abstract Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle rect, int card, int totalCards);

	}

}