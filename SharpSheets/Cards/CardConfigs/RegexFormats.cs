using SharpSheets.Parsing;
using SharpSheets.Canvas.Text;
using System.Text.RegularExpressions;

namespace SharpSheets.Cards.CardConfigs {

	/// <summary>
	/// This class holds a set of regular expressions which can be used to apply
	/// formatting to a <see cref="RichString"/>.
	/// </summary>
	public class RegexFormats : ISharpArgsGrouping {
		public Regex? regular;
		public Regex? bold;
		public Regex? italic;
		public Regex? bolditalic;

		/// <summary>
		/// Constructor for RegexFormats.
		/// </summary>
		/// <param name="regular">The pattern to match for regular formatting. This will override other formats.</param>
		/// <param name="bold">The pattern to match for bold formatting.</param>
		/// <param name="italic">The pattern to match for italic formatting.</param>
		/// <param name="bolditalic">The pattern to match for bold-italic formatting.</param>
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
