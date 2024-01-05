using GeboPdf.IO;
using GeboPdf.XObjects;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSheets.Canvas {

	public class CanvasImageData {

		public readonly float Width;
		public readonly float Height;

		public readonly FilePath Path;

		public readonly bool IsPdf;

		/// <summary></summary>
		/// <param name="path"></param>
		/// <exception cref="IOException"></exception>
		/// <exception cref="NotSupportedException"></exception>
		/// <exception cref="System.Security.SecurityException"></exception>
		/// <exception cref="UnauthorizedAccessException"></exception>
		public CanvasImageData(FilePath path) {
			this.Path = path;

			// TODO Allow this class to be an argument in documentation (must also be added to ValueParsing)
			// TODO This needs to account for possibly incoming PDF files

			if (Path.IsFile && Path.HasExtension(".pdf")) {
				FileStream srcStream = new FileStream(Path.Path, FileMode.Open, FileAccess.Read);
				PdfStreamReader srcReader = new PdfStreamReader(srcStream);
				PdfFormXObject xObj = srcReader.GetPageAsXObject(0); // TODO This is actually quite a slow process, can we make a faster alternative?
				srcStream.Close();

				this.Width = xObj.Width;
				this.Height = xObj.Height;

				this.IsPdf = true;
			}
			else if(Path.IsFile) {
				(int width, int height) = ImageHelpers.GetDimensions(this.Path.Path);

				this.Width = width;
				this.Height = height;

				this.IsPdf = false;
			}
			else {
				throw new FileNotFoundException("No such file: " + path.Path);
			}
		}

	}

}
