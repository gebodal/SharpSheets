using System.Threading;
using SharpSheets.Exceptions;

namespace SharpSheets.Canvas {

	public interface IDocumentContent {

		bool HasContent { get; }

		void DrawTo(ISharpDocument document, out SharpDrawingException[] errors, CancellationToken cancellationToken);

	}

}
