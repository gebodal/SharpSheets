using GeboPdf.IO;
using GeboPdf.Objects;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpSheets.PDFs {

	public static class FieldTools {

		/// <summary>
		/// 
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		/// <exception cref="IOException"></exception>
		/// <exception cref="NotSupportedException"></exception>
		/// <exception cref="System.Security.SecurityException"></exception>
		/// <exception cref="UnauthorizedAccessException"></exception>
		public static Dictionary<string, PdfObject> ExtractFields(string source) {
			using (FileStream objStream = new FileStream(source, FileMode.Open, FileAccess.Read)) {
				PdfStreamReader pdfStreamReader = new PdfStreamReader(objStream);

				return new Dictionary<string, PdfObject>(pdfStreamReader.GetFormFields(), StringComparer.Ordinal);
			}
		}

	}

}
