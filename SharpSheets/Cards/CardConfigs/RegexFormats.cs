using SharpSheets.Parsing;
using SharpSheets.Canvas.Text;
using System.Text.RegularExpressions;

namespace SharpSheets.Cards.CardConfigs {

	public class RegexFormats : ISharpArgsGrouping {
		public Regex? regular;
		public Regex? bold;
		public Regex? italic;
		public Regex? bolditalic;

		public RegexFormats(Regex? regular = null, Regex? bold = null, Regex? italic = null, Regex? bolditalic = null) {
			this.regular = regular;
			this.bold = bold;
			this.italic = italic;
			this.bolditalic = bolditalic;
		}

		public RichString Apply(RichString text) {
			if (bold != null) { text = text.ApplyFormat(bold, TextFormat.BOLD); }
			if (italic != null) { text = text.ApplyFormat(italic, TextFormat.ITALIC); }
			if (bolditalic != null) { text = text.ApplyFormat(bolditalic, TextFormat.BOLDITALIC); }
			// Apply regular formatting last in case we need to undo a previous match?
			if (regular != null) { text = text.ApplyFormat(regular, TextFormat.REGULAR); }
			return text;
		}
	}

}
