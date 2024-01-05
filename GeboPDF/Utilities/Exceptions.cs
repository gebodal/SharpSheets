using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Utilities {

	// TODO This needs refactoring for better behaviour and usability

	public class PdfException : Exception {

		public PdfException(string message) : base(message) { }

		public PdfException(string message, Exception innerException) : base(message, innerException) { }

	}

	public class PdfInvalidOperationException : PdfException {

		public PdfInvalidOperationException(string message) : base(message) { }

		public PdfInvalidOperationException(string message, Exception innerException) : base(message, innerException) { }

	}

	public class PdfInvalidGraphicsOperationException : PdfInvalidOperationException {

		public PdfInvalidGraphicsOperationException(string message) : base(message) { }

		public PdfInvalidGraphicsOperationException(string message, Exception innerException) : base(message, innerException) { }

	}

	public class PdfMissingIndirectReferenceException : PdfException {

		public PdfMissingIndirectReferenceException(string message) : base(message) { }

		public PdfMissingIndirectReferenceException(string message, Exception innerException) : base(message, innerException) { }

	}

}
