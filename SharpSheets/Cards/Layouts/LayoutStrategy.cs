using SharpSheets.Canvas;
using SharpSheets.Cards.Card;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Exceptions;
using SharpSheets.Layouts;
using System.Collections.Generic;
using System.Threading;

namespace SharpSheets.Cards.Layouts {

	public abstract class AbstractLayoutStrategy {

		public static readonly AbstractLayoutStrategy Card = new CardLayoutStrategy();
		public static readonly AbstractLayoutStrategy Scroll = new ScrollLayoutStrategy();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="document"></param>
		/// <param name="configuration"></param>
		/// <param name="cards"></param>
		/// <param name="errors"></param>
		/// <param name="errorCards"></param>
		/// <param name="cancellationToken"></param>
		public abstract void Draw(ISharpDocument document, CardSetConfig configuration, DynamicCard[] cards, out List<SharpDrawingException> errors, out List<DynamicCard> errorCards, CancellationToken cancellationToken);

	}

	public class CardLayoutException : SharpSheetsException {
		public CardLayoutException(string message) : base(message) { }
	}

}
