namespace SharpSheets.Markup.Parsing {

	/// <summary>
	/// Indicates the type of a Markup pattern, describing how the pattern contents
	/// should be interpreted and made available to the user.
	/// </summary>
	public enum MarkupPatternType {
		/// <summary>
		/// A widget pattern.
		/// </summary>
		WIDGET,
		/// <summary>
		/// A box pattern.
		/// </summary>
		BOX,
		/// <summary>
		/// A labelled box pattern.
		/// </summary>
		LABELLEDBOX,
		/// <summary>
		/// A titled box pattern.
		/// </summary>
		TITLEDBOX,
		/// <summary>
		/// An entried shape pattern.
		/// </summary>
		ENTRIEDSHAPE,
		/// <summary>
		/// A bar pattern.
		/// </summary>
		BAR,
		/// <summary>
		/// A usage bar pattern.
		/// </summary>
		USAGEBAR,
		/// <summary>
		/// A detail pattern.
		/// </summary>
		DETAIL
	}

}
