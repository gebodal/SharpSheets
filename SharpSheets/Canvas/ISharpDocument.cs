using SharpSheets.Layouts;
using System.Collections.Generic;

namespace SharpSheets.Canvas {

	public interface ISharpDocument {

		int PageCount { get; }

		IReadOnlyCollection<string> FieldNames { get; }

		ISharpCanvas AddNewPage(Size pageSize);

	}

}

