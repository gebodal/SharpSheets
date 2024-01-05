using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas.Text {

	/// <summary>
	/// Indicates a font format, which can be used to select a particular font
	/// from a given font selection. Note that the current font selection may
	/// not have appropriate fonts in each of the possible categories (i.e. the 
	/// "bold" font may in fact be a different standard/roman typeface). These
	/// names are simply for ease of use, and to correspond with the Markdown-style
	/// text formatting options.
	/// </summary>
	public enum TextFormat : byte {
		/// <summary>
		/// A regular or standard typeface.
		/// </summary>
		REGULAR = 0b00,
		/// <summary>
		/// A bold or emphasised typeface.
		/// </summary>
		BOLD = 0b01,
		/// <summary>
		/// An italic or oblique typeface.
		/// </summary>
		ITALIC = 0b10,
		/// <summary>
		/// A typeface which is both emboldened and italicised or oblique.
		/// </summary>
		BOLDITALIC = 0b11
	}

}
