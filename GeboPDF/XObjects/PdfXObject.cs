using GeboPdf.Documents;
using GeboPdf.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.XObjects {

	public abstract class PdfXObject : AbstractPdfStream, IPdfDocumentContents {

		protected PdfXObject() { }

		public abstract IEnumerable<PdfObject> CollectObjects();

	}

}
