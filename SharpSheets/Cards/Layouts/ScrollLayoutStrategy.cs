using SharpSheets.Canvas;
using SharpSheets.Cards.Card;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Exceptions;
using SharpSheets.Fonts;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SharpSheets.Cards.Layouts {

	public class ScrollLayoutStrategy : AbstractLayoutStrategy {

		public override void Draw(ISharpDocument document, CardSetConfig configuration, DynamicCard[] cards, out List<SharpDrawingException> errors, out List<DynamicCard> errorCards, CancellationToken cancellationToken) {
			errors = new List<SharpDrawingException>();
			errorCards = new List<DynamicCard>();

			if (cancellationToken.IsCancellationRequested) { return; }

			PageSize pageSize = configuration.paper;
			Margins pageMargin = configuration.pageMargins;
			float cardGutter = configuration.cardGutter;
			int columns = configuration.columns;

			//FontPathGrouping fonts = configuration.fonts;
			//int maxScrolls = Math.Min(configuration.MaxCards, columns);

			List<DynamicCard> cardList = new List<DynamicCard>(cards);

			Rectangle pageRect = new Rectangle(0, 0, pageSize.Width, pageSize.Height);
			Rectangle?[] columnRects = Divisions.Columns(pageRect.Margins(pageMargin, false), columns, cardGutter);

			while (cardList.Count > 0) {
				if (cancellationToken.IsCancellationRequested) { return; }

				ISharpCanvas canvas = document.AddNewPage(pageSize);
				//if (fonts != null) { canvas.SetFonts(fonts); } // TODO When to set these?

				for (int col = 0; col < columnRects.Length; col++) {
					Rectangle? available = columnRects[col];

					if (available is not null) {

						while (available.Height > cardGutter) {
							throw new NotImplementedException();
						}

					}


				}





			}



			throw new NotImplementedException();
		}

	}

}
