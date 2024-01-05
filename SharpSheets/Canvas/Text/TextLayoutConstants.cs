namespace SharpSheets.Canvas.Text {
	
	/// <summary>
	/// Indicates the horizontal justification of text, as either Left, Centre, or Right.
	/// </summary>
	public enum Justification : int {
		/// <summary>
		/// Indicates that the text should be left justified, with its leftmost character aligned with the left edge of text area.
		/// </summary>
		LEFT = 0,
		/// <summary>
		/// Indicates that the text should be centre justified, with its centre (determined by total character widths) aligned with the centre of the text area.
		/// </summary>
		CENTRE = 1,
		/// <summary>
		/// Indicates that the text should be right justified, with its rightmost character aligned with the right edge of text area.
		/// </summary>
		RIGHT = 2
	}

	/// <summary>
	/// Indicated the vertical alignment of text, as either Bottom, Centre, or Top.
	/// </summary>
	public enum Alignment : int {
		/// <summary>
		/// Indicates that the text should be bottom aligned, with the baseline of the text aligned with the bottom edge of the text area.
		/// </summary>
		BOTTOM = 0,
		/// <summary>
		/// Indicates that the text should be centre aligned, with the centre of the text (determined by greatest character ascension) aligned with the centre of the text area.
		/// </summary>
		CENTRE = 1,
		/// <summary>
		/// Indicates that the text should be top aligned, with the highest ascender of the text aligned with the top edge of the text area.
		/// </summary>
		TOP = 2
	}

}
